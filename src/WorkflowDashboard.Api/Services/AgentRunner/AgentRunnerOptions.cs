namespace WorkflowDashboard.Api.Services.AgentRunner;

public enum CliTool { Copilot, Claude, Custom }

/// <summary>Bound from <c>appsettings.json</c> section <c>AgentRunner</c>. Used as fallback defaults when no DB settings row exists.</summary>
public sealed class AgentRunnerOptions
{
    public const string SectionName = "AgentRunner";

    public bool Enabled { get; set; } = true;

    /// <summary>Which CLI tool to use. Drives default flags and file paths.</summary>
    public CliTool CliTool { get; set; } = CliTool.Copilot;

    /// <summary>Bare name (PATH-resolved) or absolute path of the CLI executable.</summary>
    public string Executable { get; set; } = "copilot";

    /// <summary>Optional extra arguments prepended before the tool-specific flags.</summary>
    public List<string> ExtraArgs { get; set; } = new();

    /// <summary>Relative path inside each repo where the instructions file is written.</summary>
    public string InstructionsRelativePath { get; set; } =
        ".github/instructions/active-workflow.instructions.md";

    /// <summary>Relative path inside each repo where the workflow input file is written.</summary>
    public string InputFileRelativePath { get; set; } =
        ".github/copilot/workflow-input.md";

    public bool InteractiveTerminal { get; set; } = true;

    public string InteractiveStartPrompt { get; set; } =
        "Begin the workflow session. Read `.github/copilot/workflow-input.md` and follow the workflow instructions you have been given.";

    public bool PersistLogsToRepo { get; set; } = false;
    public int LogTailLines { get; set; } = 500;
    public TimeSpan LogBatchInterval { get; set; } = TimeSpan.FromSeconds(1);
}
