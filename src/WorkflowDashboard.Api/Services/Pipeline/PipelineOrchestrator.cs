using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Models;
using WorkflowDashboard.Api.Services.AgentRunner;
using WorkflowDashboard.Api.Services.Catalog;

namespace WorkflowDashboard.Api.Services.Pipeline;

public enum OrchestratorMessageType { StartRun, StepCompleted, ApprovalDecided }

public sealed class OrchestratorMessage
{
    public OrchestratorMessageType Type { get; init; }
    public string? PipelineRunId { get; init; }
    public string? StepRunId { get; init; }
    public string? ApprovalRequestId { get; init; }
    /// <summary>"approved" | "feedback" — only set for StepCompleted messages from agent steps.</summary>
    public string? Decision { get; init; }
    public string? FeedbackText { get; init; }
}

public sealed class PipelineOrchestrator : BackgroundService, IPipelineOrchestrator
{
    private readonly Channel<OrchestratorMessage> _channel = Channel.CreateUnbounded<OrchestratorMessage>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly ConcurrentDictionary<string, RunningProcess> _running = new();
    private readonly ConcurrentDictionary<string, string> _activeRuns = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _runQueues = new();

    private readonly AgentRunnerOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<WorkflowHub> _hub;
    private readonly IProcessLauncher _launcher;
    private readonly InstructionsInjector _injector;
    private readonly WorkflowInputWriter _inputWriter;
    private readonly ICatalogStore _catalog;
    private readonly IConfiguration _configuration;
    private readonly IServer _server;
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(
        IOptions<AgentRunnerOptions> options,
        IServiceScopeFactory scopeFactory,
        IHubContext<WorkflowHub> hub,
        IProcessLauncher launcher,
        InstructionsInjector injector,
        WorkflowInputWriter inputWriter,
        ICatalogStore catalog,
        IConfiguration configuration,
        IServer server,
        ILogger<PipelineOrchestrator> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _hub = hub;
        _launcher = launcher;
        _injector = injector;
        _inputWriter = inputWriter;
        _catalog = catalog;
        _configuration = configuration;
        _server = server;
        _logger = logger;
    }

    private string GetApiBaseUrl()
    {
        var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses is { Count: > 0 })
        {
            var preferred = addresses.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                         ?? addresses.First();
            return NormalizeAddress(preferred);
        }

        var configured = _configuration["ApiBaseUrl"] ?? "http://localhost:5000";
        _logger.LogWarning(
            "Could not detect server address dynamically; falling back to configured ApiBaseUrl '{Url}'. " +
            "If agents cannot connect, verify 'ApiBaseUrl' in appsettings.json matches the actual listening port.",
            configured);
        return configured;
    }

    private static string NormalizeAddress(string address) =>
        address
            .Replace("://+:", "://localhost:", StringComparison.OrdinalIgnoreCase)
            .Replace("://0.0.0.0:", "://localhost:", StringComparison.OrdinalIgnoreCase)
            .Replace("://[::]:", "://localhost:", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');

    private async Task ValidateApiReachabilityAsync(CancellationToken ct)
    {
        var apiBaseUrl = GetApiBaseUrl();
        _logger.LogInformation("PipelineOrchestrator: agents will use API base URL '{Url}'.", apiBaseUrl);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            await http.GetAsync(apiBaseUrl, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(
                "PipelineOrchestrator: API is not reachable at '{Url}'. " +
                "Agents will fail to report progress. " +
                "Verify that 'ApiBaseUrl' in appsettings.json matches the port the server is listening on.",
                apiBaseUrl);
        }
    }

    public Task StartRunAsync(string pipelineRunId, CancellationToken ct = default)
    {
        _logger.LogInformation("Queueing pipeline run {RunId}.", pipelineRunId);
        _channel.Writer.TryWrite(new OrchestratorMessage
        {
            Type = OrchestratorMessageType.StartRun,
            PipelineRunId = pipelineRunId,
        });
        return Task.CompletedTask;
    }

    public async Task CancelRunAsync(string pipelineRunId, CancellationToken ct = default)
    {
        _logger.LogInformation("Cancelling pipeline run {RunId}.", pipelineRunId);
        foreach (var kvp in _running)
        {
            if (kvp.Value.StepRun.PipelineRunId == pipelineRunId)
            {
                kvp.Value.WasCancelled = true;
                try
                {
                    kvp.Value.Cts.Cancel();
                    if (!kvp.Value.Process.HasExited)
                        kvp.Value.Process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CancelRunAsync: kill failed for step run {StepRunId}", kvp.Key);
                }
            }
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var run = await db.PipelineRuns.FindAsync(new object?[] { pipelineRunId }, ct);
        if (run is null) return;

        if (run.Status is "queued")
        {
            run.Status = "cancelled";
            run.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);
            return;
        }

        if (run.Status is not ("completed" or "failed" or "cancelled"))
        {
            run.Status = "cancelled";
            run.CompletedAt = DateTime.UtcNow;

            // Dismiss any approvals that are still pending so the dashboard
            // count and nav badge are cleaned up immediately.
            var pendingApprovals = await db.ApprovalRequests
                .Where(a => a.PipelineRunId == pipelineRunId && a.Status == "pending")
                .ToListAsync(ct);
            foreach (var approval in pendingApprovals)
            {
                approval.Status = "cancelled";
                approval.DecidedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);
            FreeRepoSlot(run.RepositoryId, pipelineRunId);
        }
    }

    public async Task RestartRunAsync(string pipelineRunId, string fromStepId, CancellationToken ct = default)
    {
        _logger.LogInformation("Restarting pipeline run {RunId} from step {StepId}.", pipelineRunId, fromStepId);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

        var run = await db.PipelineRuns
            .Include(r => r.StepRuns)
            .FirstOrDefaultAsync(r => r.Id == pipelineRunId, ct);
        if (run is null) return;

        // Reset step runs from fromStepId onward
        var pipeline = await db.Pipelines.FindAsync(new object?[] { run.PipelineId }, ct);
        if (pipeline is null) return;

        var steps = ParseSteps(pipeline.StepsJson);
        var fromIdx = steps.FindIndex(s => s.Id == fromStepId);
        if (fromIdx < 0)
        {
            _logger.LogWarning("RestartRunAsync: step '{StepId}' not found in pipeline.", fromStepId);
            return;
        }

        var stepsToReset = steps.Skip(fromIdx).Select(s => s.Id).ToHashSet();
        var stepRunsToReset = run.StepRuns.Where(sr => stepsToReset.Contains(sr.StepId)).ToList();
        foreach (var sr in stepRunsToReset)
            db.PipelineStepRuns.Remove(sr);

        run.Status = "pending";
        run.CurrentStepId = fromStepId;
        run.CompletedAt = null;
        run.ErrorMessage = null;
        await db.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);

        _channel.Writer.TryWrite(new OrchestratorMessage
        {
            Type = OrchestratorMessageType.StartRun,
            PipelineRunId = pipelineRunId,
        });
    }

    public IReadOnlyList<LogLine> GetLogTail(string stepRunId)
    {
        return _running.TryGetValue(stepRunId, out var rp)
            ? rp.LogBatcher.GetTailSnapshot()
            : Array.Empty<LogLine>();
    }

    public void NotifyStepCompleted(string stepRunId, string? decision = null, string? feedbackText = null)
    {
        _channel.Writer.TryWrite(new OrchestratorMessage
        {
            Type = OrchestratorMessageType.StepCompleted,
            StepRunId = stepRunId,
            Decision = decision,
            FeedbackText = feedbackText,
        });
    }

    public void NotifyApprovalDecided(string approvalRequestId)
    {
        _channel.Writer.TryWrite(new OrchestratorMessage
        {
            Type = OrchestratorMessageType.ApprovalDecided,
            ApprovalRequestId = approvalRequestId,
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ValidateApiReachabilityAsync(stoppingToken);

        try
        {
            await foreach (var msg in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await HandleMessageAsync(msg, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception processing orchestrator message {Type}", msg.Type);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task HandleMessageAsync(OrchestratorMessage msg, CancellationToken ct)
    {
        switch (msg.Type)
        {
            case OrchestratorMessageType.StartRun:
                await HandleStartRunAsync(msg.PipelineRunId!, ct);
                break;
            case OrchestratorMessageType.StepCompleted:
                await HandleStepCompletedAsync(msg.StepRunId!, msg.Decision, msg.FeedbackText, ct);
                break;
            case OrchestratorMessageType.ApprovalDecided:
                await HandleApprovalDecidedAsync(msg.ApprovalRequestId!, ct);
                break;
        }
    }

    private async Task HandleStartRunAsync(string pipelineRunId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

        var run = await db.PipelineRuns
            .Include(r => r.Pipeline)
            .Include(r => r.Repository)
            .Include(r => r.Feature)
            .FirstOrDefaultAsync(r => r.Id == pipelineRunId, ct);

        if (run is null || run.Pipeline is null || run.Repository is null)
        {
            _logger.LogWarning("PipelineRun {RunId} not found or missing relations.", pipelineRunId);
            return;
        }

        var steps = ParseSteps(run.Pipeline.StepsJson);
        _logger.LogInformation(
            "Starting pipeline run {RunId} for repository {RepositoryId} with {StepCount} steps.",
            run.Id,
            run.RepositoryId,
            steps.Count);

        if (run.Status is "cancelled" or "completed" or "failed")
        {
            StartNextQueuedRun(run.RepositoryId);
            return;
        }

        if (_activeRuns.ContainsKey(run.RepositoryId))
        {
            var queue = _runQueues.GetOrAdd(run.RepositoryId, _ => new ConcurrentQueue<string>());
            queue.Enqueue(pipelineRunId);
            run.Status = "queued";
            await db.SaveChangesAsync(ct);
            await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);
            _logger.LogInformation("PipelineRun {RunId} queued (repo {RepoId} busy).", pipelineRunId, run.RepositoryId);
            return;
        }

        _activeRuns[run.RepositoryId] = pipelineRunId;
        _logger.LogInformation("Pipeline run {RunId} acquired repository slot {RepositoryId}.", run.Id, run.RepositoryId);

        if (steps.Count == 0)
        {
            await FailRunAsync(db, run, "Pipeline has no steps.", ct);
            return;
        }

        var apiBaseUrl = GetApiBaseUrl();
        try
        {
            _inputWriter.Write(run, run.Repository, apiBaseUrl, run.Pipeline, run.Feature);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write pipeline input file for run {RunId}.", pipelineRunId);
        }

        // Support restarting from a specific step (CurrentStepId pre-set by RestartRunAsync)
        var startStep = !string.IsNullOrEmpty(run.CurrentStepId)
            ? steps.FirstOrDefault(s => s.Id == run.CurrentStepId) ?? steps[0]
            : steps[0];

        run.Status = "running";
        run.StartedAt ??= DateTime.UtcNow;
        run.CurrentStepId = startStep.Id;
        await db.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);

        await AdvanceToStepAsync(db, run, startStep, steps, ct);
    }

    private async Task HandleStepCompletedAsync(string stepRunId, string? decision, string? feedbackText, CancellationToken ct)
    {
        if (_running.TryRemove(stepRunId, out var rp))
            await rp.DisposeAsync();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

        var stepRun = await db.PipelineStepRuns.FindAsync(new object?[] { stepRunId }, ct);
        if (stepRun is null) return;

        if (stepRun.Status != "completed")
        {
            stepRun.Status = "completed";
            stepRun.CompletedAt = DateTime.UtcNow;
        }

        var run = await db.PipelineRuns
            .Include(r => r.Pipeline)
            .Include(r => r.Repository)
            .Include(r => r.Feature)
            .FirstOrDefaultAsync(r => r.Id == stepRun.PipelineRunId, ct);

        if (run is null || run.Pipeline is null)
        {
            _logger.LogWarning("PipelineRun {RunId} not found for step {StepId}.", stepRun.PipelineRunId, stepRunId);
            return;
        }

        if (run.Status is "cancelled" or "completed" or "failed")
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        if (!string.Equals(run.CurrentStepId, stepRun.StepId, StringComparison.Ordinal))
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var steps = ParseSteps(run.Pipeline.StepsJson);
        var currentIdx = steps.FindIndex(s => s.Id == stepRun.StepId);
        if (currentIdx < 0)
        {
            await FailRunAsync(db, run, $"Completed step '{stepRun.StepId}' is not defined in pipeline.", ct);
            return;
        }

        var currentStepDef = steps[currentIdx];

        // Agent feedback loop: reviewer agent sent back "feedback" with returnTo configured.
        var isFeedback = string.Equals(decision, "feedback", StringComparison.OrdinalIgnoreCase);
        if (isFeedback && currentStepDef.CanGiveFeedback && !string.IsNullOrEmpty(currentStepDef.ReturnTo))
        {
            _logger.LogInformation("Pipeline run {RunId} step {StepId} returned feedback to {ReturnTo}.", run.Id, stepRun.StepId, currentStepDef.ReturnTo);
            var returnToStep = steps.FirstOrDefault(s => s.Id == currentStepDef.ReturnTo);
            if (returnToStep is null)
            {
                await FailRunAsync(db, run, $"ReturnTo step '{currentStepDef.ReturnTo}' not found in pipeline.", ct);
                return;
            }

            if (!string.IsNullOrWhiteSpace(feedbackText) && run.Repository is not null)
            {
                try
                {
                    var inputPath = _inputWriter.GetPath(run.Repository);
                    await _inputWriter.AppendSection(inputPath, currentStepDef.Id, stepRun.AttemptNumber,
                        stepRun.AgentSlug ?? "agent", returnToStep.Id, feedbackText);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to append agent feedback to workflow-input.md.");
                }
            }

            var existingAttempts = await db.PipelineStepRuns
                .Where(s => s.PipelineRunId == run.Id && s.StepId == returnToStep.Id)
                .CountAsync(ct);

            run.CurrentStepId = returnToStep.Id;
            run.Status = "running";
            await db.SaveChangesAsync(ct);
            await _hub.Clients.All.SendAsync(PipelineHubMethods.StepRunUpdated, stepRun, ct);
            await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);
            await AdvanceToStepAsync(db, run, returnToStep, steps, ct, attemptNumber: existingAttempts + 1);
            return;
        }

        var nextIdx = currentIdx + 1;

        await db.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync(PipelineHubMethods.StepRunUpdated, stepRun, ct);

        if (nextIdx >= steps.Count)
        {
            run.Status = "completed";
            run.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);
            _logger.LogInformation("Pipeline run {RunId} completed.", run.Id);
            FreeRepoSlot(run.RepositoryId, run.Id);
        }
        else
        {
            var nextStep = steps[nextIdx];
            run.CurrentStepId = nextStep.Id;
            await AdvanceToStepAsync(db, run, nextStep, steps, ct);
        }
    }


    private async Task HandleApprovalDecidedAsync(string approvalRequestId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

        var approval = await db.ApprovalRequests
            .Include(a => a.StepRun)
            .Include(a => a.PipelineRun)
                .ThenInclude(r => r!.Pipeline)
            .Include(a => a.PipelineRun)
                .ThenInclude(r => r!.Repository)
            .Include(a => a.PipelineRun)
                .ThenInclude(r => r!.Feature)
            .FirstOrDefaultAsync(a => a.Id == approvalRequestId, ct);

        if (approval is null || approval.PipelineRun is null || approval.PipelineRun.Pipeline is null)
        {
            _logger.LogWarning("ApprovalRequest {Id} not found.", approvalRequestId);
            return;
        }

        var run = approval.PipelineRun;
        if (run.Status is "cancelled" or "completed" or "failed")
        {
            return;
        }

        var steps = ParseSteps(run.Pipeline.StepsJson);
        var approvalStep = steps.FirstOrDefault(s => s.Id == approval.StepId);

        if (approval.Status == "approved")
        {
            _logger.LogInformation("Approval {ApprovalId} approved for run {RunId}.", approval.Id, run.Id);
            var currentIdx = steps.FindIndex(s => s.Id == approval.StepId);
            var nextIdx = currentIdx + 1;

            if (approval.StepRun is not null)
            {
                approval.StepRun.Status = "completed";
                approval.StepRun.CompletedAt = DateTime.UtcNow;
            }

            if (nextIdx >= steps.Count)
            {
                run.Status = "completed";
                run.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);
                _logger.LogInformation("Pipeline run {RunId} completed after approval {ApprovalId}.", run.Id, approval.Id);
                FreeRepoSlot(run.RepositoryId, run.Id);
            }
            else
            {
                var nextStep = steps[nextIdx];
                run.CurrentStepId = nextStep.Id;
                run.Status = "running";
                await db.SaveChangesAsync(ct);
                await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);
                if (approval.StepRun is not null)
                    await _hub.Clients.All.SendAsync(PipelineHubMethods.StepRunUpdated, approval.StepRun, ct);
                await AdvanceToStepAsync(db, run, nextStep, steps, ct);
            }
        }
        else if (approval.Status == "rejected" && approvalStep?.ReturnTo is not null)
        {
            _logger.LogInformation("Approval {ApprovalId} rejected for run {RunId}; returning to {ReturnTo}.", approval.Id, run.Id, approvalStep.ReturnTo);
            var returnToStep = steps.FirstOrDefault(s => s.Id == approvalStep.ReturnTo);
            if (returnToStep is null)
            {
                _logger.LogWarning("ReturnTo step '{ReturnTo}' not found in pipeline.", approvalStep.ReturnTo);
                await FailRunAsync(db, run, $"ReturnTo step '{approvalStep.ReturnTo}' not found.", ct);
                return;
            }

            if (approval.StepRun is not null)
            {
                approval.StepRun.Status = "completed";
                approval.StepRun.CompletedAt = DateTime.UtcNow;
            }

            if (!string.IsNullOrWhiteSpace(approval.FeedbackText) && run.Repository is not null)
            {
                try
                {
                    var inputPath = _inputWriter.GetPath(run.Repository);
                    await _inputWriter.AppendSection(inputPath, approval.StepId, 0, "user-feedback", returnToStep.Id, approval.FeedbackText);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to append feedback to workflow-input.md.");
                }
            }

            var existingAttempts = await db.PipelineStepRuns
                .Where(s => s.PipelineRunId == run.Id && s.StepId == returnToStep.Id)
                .CountAsync(ct);

            run.CurrentStepId = returnToStep.Id;
            run.Status = "running";
            await db.SaveChangesAsync(ct);
            await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);
            if (approval.StepRun is not null)
                await _hub.Clients.All.SendAsync(PipelineHubMethods.StepRunUpdated, approval.StepRun, ct);
            await AdvanceToStepAsync(db, run, returnToStep, steps, ct, attemptNumber: existingAttempts + 1);
        }
        else
        {
            await FailRunAsync(db, run, "Rejected at approval step with no returnTo.", ct);
        }
    }

    private async Task AdvanceToStepAsync(WorkflowDbContext db, PipelineRun run, PipelineStepDef stepDef, List<PipelineStepDef> allSteps, CancellationToken ct, int attemptNumber = 1)
    {
        if (stepDef.Type == "agent")
        {
            _logger.LogInformation("Starting agent step {StepId} for run {RunId} (attempt {AttemptNumber}).", stepDef.Id, run.Id, attemptNumber);
            await StartAgentStepAsync(db, run, stepDef, ct, attemptNumber);
        }
        else if (stepDef.Type == "userApproval")
        {
            _logger.LogInformation("Starting approval step {StepId} for run {RunId}.", stepDef.Id, run.Id);
            await StartApprovalStepAsync(db, run, stepDef, ct);
        }
        else
        {
            _logger.LogWarning("Unknown step type '{Type}' for step '{Id}'.", stepDef.Type, stepDef.Id);
            var currentIdx = allSteps.FindIndex(s => s.Id == stepDef.Id);
            if (currentIdx + 1 < allSteps.Count)
            {
                var next = allSteps[currentIdx + 1];
                run.CurrentStepId = next.Id;
                await db.SaveChangesAsync(ct);
                await AdvanceToStepAsync(db, run, next, allSteps, ct);
            }
        }
    }

    private async Task StartAgentStepAsync(WorkflowDbContext db, PipelineRun run, PipelineStepDef stepDef, CancellationToken ct, int attemptNumber)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Agent execution disabled. Failing step {StepId}.", stepDef.Id);
            await FailRunAsync(db, run, "Agent execution is disabled.", ct);
            return;
        }

        var stepRun = new PipelineStepRun
        {
            PipelineRunId = run.Id,
            StepId = stepDef.Id,
            StepType = "agent",
            AgentSlug = stepDef.AgentSlug,
            AttemptNumber = attemptNumber,
            Status = "running",
            StartedAt = DateTime.UtcNow,
        };
        db.PipelineStepRuns.Add(stepRun);
        await db.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync(PipelineHubMethods.StepRunUpdated, stepRun, ct);

        string agentMarkdownBody = string.Empty;
        if (!string.IsNullOrEmpty(stepDef.AgentSlug) && _catalog.TryGet("agent", stepDef.AgentSlug, out var catalogEntry))
        {
            try
            {
                agentMarkdownBody = CatalogScanner.StripFrontMatter(File.ReadAllText(catalogEntry.SourcePath));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read catalog entry for agent step {StepId}.", stepDef.Id);
            }
        }

        var inputFilePath = _inputWriter.GetPath(run.Repository!);
        var apiBaseUrl = GetApiBaseUrl();
        try
        {
            _injector.Inject(stepRun, run, run.Repository!, stepDef, agentMarkdownBody, inputFilePath, apiBaseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inject instructions for step {StepId}.", stepDef.Id);
        }

        Process process;
        try
        {
            process = _launcher.Start(stepRun, run, run.Repository!, apiBaseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process for step {StepId}.", stepDef.Id);
            stepRun.Status = "failed";
            stepRun.ErrorMessage = ex.Message;
            stepRun.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await _hub.Clients.All.SendAsync(PipelineHubMethods.StepRunUpdated, stepRun, ct);
            await FailRunAsync(db, run, $"Failed to start agent process: {ex.Message}", ct);
            return;
        }

        stepRun.ProcessId = process.Id;
        await db.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync(PipelineHubMethods.StepRunUpdated, stepRun, ct);
        _logger.LogInformation("Agent step {StepId} started process {ProcessId} for run {RunId}.", stepDef.Id, process.Id, run.Id);

        var logBatcher = new LogBatcher(stepRun.Id, _options, _hub, _logger);
        var cts = new CancellationTokenSource();
        var rp = new RunningProcess(stepRun, run.Repository!, process, cts, logBatcher);
        _running[stepRun.Id] = rp;

        if (!_options.InteractiveTerminal)
        {
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) logBatcher.Enqueue(LogStream.Stdout, e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) logBatcher.Enqueue(LogStream.Stderr, e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        _ = WaitForProcessExitAsync(stepRun.Id, process, rp, ct);
    }

    private async Task WaitForProcessExitAsync(string stepRunId, Process process, RunningProcess rp, CancellationToken ct)
    {
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WaitForExitAsync failed for step run {StepRunId}.", stepRunId);
        }

        try { _injector.Cleanup(rp.Repository); } catch { }

        rp.ExitSignal.TrySetResult();
        NotifyStepCompleted(stepRunId);
    }

    private async Task StartApprovalStepAsync(WorkflowDbContext db, PipelineRun run, PipelineStepDef stepDef, CancellationToken ct)
    {
        var existingAttempts = await db.PipelineStepRuns
            .Where(s => s.PipelineRunId == run.Id && s.StepId == stepDef.Id)
            .CountAsync(ct);

        var stepRun = new PipelineStepRun
        {
            PipelineRunId = run.Id,
            StepId = stepDef.Id,
            StepType = "userApproval",
            AttemptNumber = existingAttempts + 1,
            Status = "waiting_approval",
            StartedAt = DateTime.UtcNow,
        };
        db.PipelineStepRuns.Add(stepRun);
        await db.SaveChangesAsync(ct);

        var approval = new ApprovalRequest
        {
            PipelineRunId = run.Id,
            StepRunId = stepRun.Id,
            StepId = stepDef.Id,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
        };
        db.ApprovalRequests.Add(approval);

        run.Status = "waiting_approval";
        await db.SaveChangesAsync(ct);

        await _hub.Clients.All.SendAsync(PipelineHubMethods.StepRunUpdated, stepRun, ct);
        await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);
        await _hub.Clients.All.SendAsync(PipelineHubMethods.ApprovalRequested, approval, ct);
        _logger.LogInformation("Approval requested for run {RunId}, step {StepId}, approval {ApprovalId}.", run.Id, stepDef.Id, approval.Id);
    }

    private async Task FailRunAsync(WorkflowDbContext db, PipelineRun run, string reason, CancellationToken ct)
    {
        run.Status = "failed";
        run.ErrorMessage = reason;
        run.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync(PipelineHubMethods.PipelineRunUpdated, run, ct);
        _logger.LogWarning("Pipeline run {RunId} failed: {Reason}", run.Id, reason);
        FreeRepoSlot(run.RepositoryId, run.Id);
    }

    private void FreeRepoSlot(string repositoryId, string pipelineRunId)
    {
        _activeRuns.TryRemove(new KeyValuePair<string, string>(repositoryId, pipelineRunId));
        _logger.LogInformation("Released repository slot {RepositoryId} for run {RunId}.", repositoryId, pipelineRunId);
        StartNextQueuedRun(repositoryId);
    }

    private void StartNextQueuedRun(string repositoryId)
    {
        if (_runQueues.TryGetValue(repositoryId, out var queue) && queue.TryDequeue(out var nextRunId))
        {
            _logger.LogInformation("Dequeued next pipeline run {RunId} for repository {RepositoryId}.", nextRunId, repositoryId);
            _channel.Writer.TryWrite(new OrchestratorMessage
            {
                Type = OrchestratorMessageType.StartRun,
                PipelineRunId = nextRunId,
            });
        }
    }

    private static List<PipelineStepDef> ParseSteps(string stepsJson)
    {
        if (string.IsNullOrWhiteSpace(stepsJson)) return new();
        try
        {
            using var doc = JsonDocument.Parse(stepsJson);
            if (doc.RootElement.TryGetProperty("steps", out var stepsEl))
            {
                return JsonSerializer.Deserialize<List<PipelineStepDef>>(
                    stepsEl.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
        }
        catch
        {
        }

        return new();
    }
}
