namespace WorkflowDashboard.Api.Models;

public class PipelineRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string PipelineId { get; set; } = string.Empty;
    public string? FeatureId { get; set; }
    public string RepositoryId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending|running|waiting_approval|completed|failed|cancelled|queued
    public string CurrentStepId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public string TicketNumber { get; set; } = string.Empty;
    public string? BranchPrefix { get; set; }
    public string? DefaultBranch { get; set; }
    public string FeatureSlug { get; set; } = string.Empty;
    public string? InitialInstructions { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string BranchName =>
        string.IsNullOrWhiteSpace(BranchPrefix)
            ? TicketNumber
            : $"{BranchPrefix.TrimEnd('/')}/{TicketNumber}";

    public Pipeline? Pipeline { get; set; }
    public Feature? Feature { get; set; }
    public Repository? Repository { get; set; }
    public ICollection<PipelineStepRun> StepRuns { get; set; } = new List<PipelineStepRun>();
    public ICollection<ApprovalRequest> ApprovalRequests { get; set; } = new List<ApprovalRequest>();
}
