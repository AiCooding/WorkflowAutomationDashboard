namespace WorkflowDashboard.Api.Models;

public class InputRequest
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string? OptionsJson { get; set; }
    public string? Response { get; set; }
    public string Status { get; set; } = "pending"; // pending|answered|expired
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnsweredAt { get; set; }

    public Workflow Workflow { get; set; } = null!;
    public Agent Agent { get; set; } = null!;
}
