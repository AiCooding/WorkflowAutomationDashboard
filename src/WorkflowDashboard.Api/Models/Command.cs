namespace WorkflowDashboard.Api.Models;

public class Command
{
    public string Id { get; set; } = string.Empty;
    public string? WorkflowId { get; set; }
    public string CommandType { get; set; } = string.Empty; // start|pause|resume|cancel|retry
    public string? PayloadJson { get; set; }
    public string Status { get; set; } = "pending"; // pending|processing|completed|failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public Workflow? Workflow { get; set; }
}
