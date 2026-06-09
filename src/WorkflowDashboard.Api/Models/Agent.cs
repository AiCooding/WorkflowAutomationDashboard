namespace WorkflowDashboard.Api.Models;

public class Agent
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string AgentType { get; set; } = string.Empty; // orchestrator|architect|developer|code-review|pm|plan-reviewer
    public string Status { get; set; } = "idle"; // idle|running|waiting_input|completed|failed
    public string? CurrentTask { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? SessionId { get; set; }

    public Workflow Workflow { get; set; } = null!;
}
