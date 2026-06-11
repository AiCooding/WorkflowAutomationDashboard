namespace WorkflowDashboard.Api.Models;

public class PipelineStepRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string PipelineRunId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty;
    public string? AgentSlug { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public string Status { get; set; } = "pending"; // pending|running|waiting_approval|completed|failed|skipped
    public int? ProcessId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public PipelineRun? PipelineRun { get; set; }
}
