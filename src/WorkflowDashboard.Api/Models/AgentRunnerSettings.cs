namespace WorkflowDashboard.Api.Models;

/// <summary>
/// Singleton DB row (Id = 1) that overrides <see cref="WorkflowDashboard.Api.Services.AgentRunner.AgentRunnerOptions"/> from appsettings.
/// If the row does not exist, appsettings values are used as defaults.
/// </summary>
public class AgentRunnerSettings
{
    public int Id { get; set; } = 1;
    public string CliTool { get; set; } = "Copilot";
    public string Executable { get; set; } = "copilot";
    /// <summary>JSON array of extra CLI arguments.</summary>
    public string ExtraArgsJson { get; set; } = "[]";
    public string InstructionsRelativePath { get; set; } =
        ".github/instructions/active-workflow.instructions.md";
    public string InputFileRelativePath { get; set; } =
        ".github/copilot/workflow-input.md";
    public bool InteractiveTerminal { get; set; } = true;
    public string InteractiveStartPrompt { get; set; } =
        "Begin the workflow session. Read `.github/copilot/workflow-input.md` and follow the workflow instructions you have been given.";
    public bool Enabled { get; set; } = true;

    /// <summary>Overrides <c>Catalog.AgentsDir</c> from appsettings. Null = use default (~/.copilot/agents).</summary>
    public string? AgentsDir { get; set; }
}
