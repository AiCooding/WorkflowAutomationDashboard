namespace WorkflowDashboard.Api.Models;

public class ApprovalRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string PipelineRunId { get; set; } = string.Empty;
    public string StepRunId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending|approved|rejected
    public string? FeedbackText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }

    public PipelineRun? PipelineRun { get; set; }
    public PipelineStepRun? StepRun { get; set; }
}
