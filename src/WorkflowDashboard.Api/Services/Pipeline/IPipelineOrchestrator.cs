using WorkflowDashboard.Api.Services.AgentRunner;

namespace WorkflowDashboard.Api.Services.Pipeline;

public interface IPipelineOrchestrator
{
    Task StartRunAsync(string pipelineRunId, CancellationToken ct = default);
    Task CancelRunAsync(string pipelineRunId, CancellationToken ct = default);
    Task RestartRunAsync(string pipelineRunId, string fromStepId, CancellationToken ct = default);
    IReadOnlyList<LogLine> GetLogTail(string stepRunId);
    void NotifyStepCompleted(string stepRunId, string? decision = null, string? feedbackText = null);
    void NotifyApprovalDecided(string approvalRequestId);
}
