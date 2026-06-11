using Microsoft.AspNetCore.SignalR;
using WorkflowDashboard.Api.Hubs;

namespace WorkflowDashboard.Api.Services.Catalog;

/// <summary>
/// IHostedService that performs the initial eager catalog scan at startup,
/// on a background thread so it doesn't block app start.
/// Refresh is otherwise manual via POST /api/catalog/refresh.
/// </summary>
public sealed class CatalogStartupScanner : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CatalogStartupScanner> _logger;
    private readonly TaskCompletionSource<bool> _scanCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Awaitable that completes once the initial scan finishes (success or failure).
    /// Used by <see cref="WorkflowDashboard.Api.Services.AgentRunner.StartupReconciler"/>
    /// to avoid rebuilding the queue against an empty catalog (chosen over option (b),
    /// "defer the queue rebuild slightly", because it's deterministic).
    /// </summary>
    public Task ScanCompleted => _scanCompleted.Task;

    public CatalogStartupScanner(IServiceProvider services, ILogger<CatalogStartupScanner> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Fire-and-forget — do NOT block app startup.
        _ = Task.Run(() =>
        {
            try
            {
                using var scope = _services.CreateScope();
                var scanner = scope.ServiceProvider.GetRequiredService<CatalogScanner>();
                var counts = scanner.Scan();
                _logger.LogInformation(
                    "Catalog startup scan complete: {Workflows} workflows, {Agents} agents, {Broken} broken.",
                    counts.WorkflowCount, counts.AgentCount, counts.BrokenCount);

                // Broadcast on completion so any clients connected during the boot window
                // see the populated catalog without having to manually refresh.
                var hub = scope.ServiceProvider.GetRequiredService<IHubContext<WorkflowHub>>();
                hub.Clients.All.SendAsync(WorkflowHubMethods.CatalogRefreshed, new
                {
                    workflowCount = counts.WorkflowCount,
                    agentCount = counts.AgentCount,
                    brokenCount = counts.BrokenCount,
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Catalog startup scan failed.");
            }
            finally
            {
                _scanCompleted.TrySetResult(true);
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _scanCompleted.TrySetResult(false);
        return Task.CompletedTask;
    }
}
