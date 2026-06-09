namespace WorkflowDashboard.Api.Models;

public class WorkflowEvent
{
    public int Id { get; set; }
    public string? WorkflowId { get; set; }
    public string? AgentId { get; set; }
    public string EventType { get; set; } = string.Empty; // state_change|log|error|input_requested|command_received
    public string? Message { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
