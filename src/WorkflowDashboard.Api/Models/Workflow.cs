namespace WorkflowDashboard.Api.Models;

public class Workflow
{
    public string Id { get; set; } = string.Empty;
    public string? FeatureId { get; set; }
    public string Type { get; set; } = string.Empty; // full-pipeline|bugfix|review-only|custom
    public string Status { get; set; } = "pending"; // pending|running|paused|waiting_input|completed|failed|cancelled
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Feature? Feature { get; set; }
    public ICollection<Agent> Agents { get; set; } = new List<Agent>();
    public ICollection<InputRequest> InputRequests { get; set; } = new List<InputRequest>();
    public ICollection<Command> Commands { get; set; } = new List<Command>();
}
