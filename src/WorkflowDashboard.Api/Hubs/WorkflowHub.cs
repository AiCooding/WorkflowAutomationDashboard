using Microsoft.AspNetCore.SignalR;
using WorkflowDashboard.Api.Services.Pipeline;

namespace WorkflowDashboard.Api.Hubs;

public class WorkflowHub : Hub
{
    private readonly IPipelineOrchestrator _orchestrator;

    public WorkflowHub(IPipelineOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task SubscribeToStepRun(string stepRunId)
    {
        if (string.IsNullOrWhiteSpace(stepRunId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"steprun:{stepRunId}");

        var tail = _orchestrator.GetLogTail(stepRunId);
        var payload = new
        {
            stepRunId,
            lines = tail.Select(l => new
            {
                stream = l.StreamName,
                line = l.Line,
                ts = l.Ts,
            }),
        };
        await Clients.Caller.SendAsync(PipelineHubMethods.StepLogTail, payload);
    }

    public Task UnsubscribeFromStepRun(string stepRunId)
    {
        if (string.IsNullOrWhiteSpace(stepRunId)) return Task.CompletedTask;
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"steprun:{stepRunId}");
    }
}

public static class WorkflowHubMethods
{
    public const string RepositoryUpdated = "RepositoryUpdated";
    public const string CatalogRefreshed = "CatalogRefreshed";
    public const string FeatureUpdated = "FeatureUpdated";
}

public static class PipelineHubMethods
{
    public const string PipelineRunUpdated = "PipelineRunUpdated";
    public const string StepRunUpdated = "StepRunUpdated";
    public const string ApprovalRequested = "ApprovalRequested";
    public const string ApprovalDecided = "ApprovalDecided";
    public const string StepLog = "StepLog";
    public const string StepLogTail = "StepLogTail";
}
