using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Models;
using WorkflowDashboard.Api.Services.Pipeline;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/pipeline-runs")]
public class PipelineRunsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly IPipelineOrchestrator _orchestrator;
    private readonly IHubContext<WorkflowHub> _hub;
    private readonly PipelineRunProjector _projector;

    public PipelineRunsController(
        WorkflowDbContext db,
        IPipelineOrchestrator orchestrator,
        IHubContext<WorkflowHub> hub,
        PipelineRunProjector projector)
    {
        _db = db;
        _orchestrator = orchestrator;
        _hub = hub;
        _projector = projector;
    }

    [HttpGet]
    public async Task<ActionResult<List<PipelineRunDto>>> GetAll(
        [FromQuery] string? repositoryId,
        [FromQuery] string? featureId,
        [FromQuery] string? pipelineId,
        [FromQuery] string? status)
    {
        var query = _db.PipelineRuns
            .Include(r => r.Pipeline)
            .Include(r => r.Feature)
            .Include(r => r.Repository)
            .Include(r => r.StepRuns)
            .Include(r => r.ApprovalRequests)
            .AsQueryable();

        if (!string.IsNullOrEmpty(repositoryId)) query = query.Where(r => r.RepositoryId == repositoryId);
        if (!string.IsNullOrEmpty(featureId)) query = query.Where(r => r.FeatureId == featureId);
        if (!string.IsNullOrEmpty(pipelineId)) query = query.Where(r => r.PipelineId == pipelineId);
        if (!string.IsNullOrEmpty(status)) query = query.Where(r => r.Status == status);

        var runs = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return runs.Select(r => _projector.Project(r)).ToList();
    }

    [HttpGet("{runId}")]
    public async Task<ActionResult<PipelineRunDto>> GetById(string runId)
    {
        var run = await _db.PipelineRuns
            .Include(r => r.Pipeline)
            .Include(r => r.Feature)
            .Include(r => r.Repository)
            .Include(r => r.StepRuns)
            .Include(r => r.ApprovalRequests)
            .FirstOrDefaultAsync(r => r.Id == runId);
        if (run is null) return NotFound();
        return _projector.Project(run);
    }

    [HttpPost]
    public async Task<ActionResult<PipelineRunDto>> StartRun([FromBody] StartPipelineRunBody body)
    {
        var pipeline = await _db.Pipelines.FindAsync(body.PipelineId);
        if (pipeline is null) return BadRequest("Pipeline not found.");

        var repo = await _db.Repositories.FindAsync(body.RepositoryId);
        if (repo is null) return BadRequest("Repository not found.");

        if (!string.IsNullOrWhiteSpace(body.FeatureId))
        {
            var feature = await _db.Features.FindAsync(body.FeatureId);
            if (feature is null) return BadRequest("Feature not found.");
        }

        var run = new PipelineRun
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            PipelineId = body.PipelineId,
            FeatureId = body.FeatureId,
            RepositoryId = body.RepositoryId,
            Status = "pending",
            CurrentStepId = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        _db.PipelineRuns.Add(run);
        await _db.SaveChangesAsync();

        await _orchestrator.StartRunAsync(run.Id);

        var reloaded = await _db.PipelineRuns
            .Include(r => r.Pipeline)
            .Include(r => r.Feature)
            .Include(r => r.Repository)
            .Include(r => r.StepRuns)
            .Include(r => r.ApprovalRequests)
            .FirstAsync(r => r.Id == run.Id);

        return CreatedAtAction(nameof(GetById), new { runId = run.Id }, _projector.Project(reloaded));
    }

    [HttpPost("{runId}/cancel")]
    public async Task<IActionResult> Cancel(string runId)
    {
        var run = await _db.PipelineRuns.FindAsync(runId);
        if (run is null) return NotFound();
        await _orchestrator.CancelRunAsync(runId);
        return Ok();
    }

    [HttpPost("{runId}/steps/{stepRunId}/complete")]
    public async Task<IActionResult> CompleteStep(string runId, string stepRunId)
    {
        var stepRun = await _db.PipelineStepRuns
            .FirstOrDefaultAsync(s => s.Id == stepRunId && s.PipelineRunId == runId);
        if (stepRun is null) return NotFound();

        if (stepRun.Status == "running")
        {
            stepRun.Status = "completed";
            stepRun.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        _orchestrator.NotifyStepCompleted(stepRunId);
        return Ok();
    }

    [HttpPost("{runId}/approvals/{approvalId}/decide")]
    public async Task<IActionResult> DecideApproval(string runId, string approvalId, [FromBody] ApprovalDecisionBody body)
    {
        var decision = (body.Decision ?? string.Empty).Trim().ToLowerInvariant();
        if (decision is not ("approved" or "rejected"))
            return BadRequest("Decision must be 'approved' or 'rejected'.");

        var approval = await _db.ApprovalRequests
            .FirstOrDefaultAsync(a => a.Id == approvalId && a.PipelineRunId == runId);
        if (approval is null) return NotFound();
        if (approval.Status != "pending") return BadRequest("Approval already decided.");

        approval.Status = decision;
        approval.FeedbackText = body.FeedbackText;
        approval.DecidedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync(PipelineHubMethods.ApprovalDecided, approval);
        _orchestrator.NotifyApprovalDecided(approvalId);

        return Ok();
    }
}

public record StartPipelineRunBody(string PipelineId, string RepositoryId, string? FeatureId);
public record ApprovalDecisionBody(string Decision, string? FeedbackText);
