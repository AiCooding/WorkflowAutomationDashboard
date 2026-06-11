namespace WorkflowDashboard.Api.Services.Pipeline;

public class PipelineStepDef
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "agent"; // "agent" | "userApproval"
    public string Name { get; set; } = string.Empty;
    public string? AgentSlug { get; set; }
    public bool CanGiveFeedback { get; set; }
    public string? ReturnTo { get; set; }
}
