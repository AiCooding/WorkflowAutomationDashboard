namespace WorkflowDashboard.Api.Services.AgentRunner;

/// <summary>
/// Bound from <c>appsettings.json</c> section <c>AgentRunner</c>.
/// </summary>
public sealed class AgentRunnerOptions
{
    public const string SectionName = "AgentRunner";

    /// <summary>
    /// When <c>false</c> the runner refuses to spawn processes. Set to <c>false</c>
    /// inside Docker (cannot see host <c>~/.copilot</c> or spawn host <c>copilot</c>).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Bare name (PATH-resolved) or absolute path. Default: <c>copilot</c>.</summary>
    public string CopilotExecutable { get; set; } = "copilot";

    /// <summary>
    /// Optional argument list passed to <see cref="CopilotExecutable"/>. Production usage
    /// leaves this empty (the workflow body is injected as an instructions file). Useful for
    /// pointing the runner at a stub script during testing (e.g. <c>powershell.exe -File stub.ps1</c>).
    /// </summary>
    public List<string> CopilotArgs { get; set; } = new();

    /// <summary>
    /// Relative path under each repo where the instructions file is injected.
    /// </summary>
    public string InstructionsRelativePath { get; set; } =
        ".github/instructions/active-workflow.instructions.md";

    /// <summary>
    /// When <c>true</c> the runner opens a new visible terminal window (Windows Terminal → cmd fallback)
    /// so the user can interact with the spawned process. stdout/stderr are NOT captured in this mode
    /// (log streaming to the dashboard is unavailable). Default: <c>true</c> — required for interactive
    /// workflows like the PM draft conversation.
    /// When <c>false</c> the process runs headlessly with stdout/stderr streamed to the dashboard log panel.
    /// </summary>
    public bool InteractiveTerminal { get; set; } = true;

    /// <summary>
    /// Initial prompt passed to <c>copilot -i "&lt;prompt&gt;"</c> when <see cref="InteractiveTerminal"/> is
    /// <c>true</c>. Copilot starts in interactive mode and auto-executes this prompt so the workflow
    /// begins without the user having to type anything. Leave empty to open a bare interactive session.
    /// </summary>
    public string InteractiveStartPrompt { get; set; } =
        "Begin the workflow session. Read `.github/copilot/workflow-input.md` and follow the workflow instructions you have been given.";

    /// <summary>Optional: tee stdout/stderr to <c>{repo}/.copilot/logs/{workflowId}.log</c>.</summary>
    public bool PersistLogsToRepo { get; set; } = false;

    /// <summary>In-memory sliding tail buffer size per workflow.</summary>
    public int LogTailLines { get; set; } = 500;

    /// <summary>Flush interval for batched WorkflowEvent rows; 200 lines forces an earlier flush.</summary>
    public TimeSpan LogBatchInterval { get; set; } = TimeSpan.FromSeconds(1);
}
