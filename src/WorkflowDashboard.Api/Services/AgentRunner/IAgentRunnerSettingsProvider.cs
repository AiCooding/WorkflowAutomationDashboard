namespace WorkflowDashboard.Api.Services.AgentRunner;

public interface IAgentRunnerSettingsProvider
{
    /// <summary>Returns the effective settings: DB row if present, otherwise appsettings defaults.</summary>
    AgentRunnerOptions GetEffective();

    /// <summary>Clears the in-memory cache so the next call reloads from DB.</summary>
    void Invalidate();
}
