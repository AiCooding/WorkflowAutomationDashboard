using System.Text.Json;
using Microsoft.Extensions.Options;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Services.AgentRunner;

public sealed class AgentRunnerSettingsProvider : IAgentRunnerSettingsProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AgentRunnerOptions _defaults;
    private AgentRunnerOptions? _cached;
    private readonly object _lock = new();

    public AgentRunnerSettingsProvider(
        IServiceScopeFactory scopeFactory,
        IOptions<AgentRunnerOptions> defaults)
    {
        _scopeFactory = scopeFactory;
        _defaults = defaults.Value;
    }

    public AgentRunnerOptions GetEffective()
    {
        lock (_lock)
        {
            if (_cached is not null) return _cached;
            _cached = LoadFromDb() ?? _defaults;
            return _cached;
        }
    }

    public void Invalidate()
    {
        lock (_lock) { _cached = null; }
    }

    private AgentRunnerOptions? LoadFromDb()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var row = db.AgentRunnerSettings.Find(1);
            if (row is null) return null;

            return new AgentRunnerOptions
            {
                Enabled = row.Enabled,
                CliTool = Enum.TryParse<CliTool>(row.CliTool, ignoreCase: true, out var t) ? t : CliTool.Copilot,
                Executable = row.Executable,
                ExtraArgs = JsonSerializer.Deserialize<List<string>>(row.ExtraArgsJson) ?? new(),
                InstructionsRelativePath = row.InstructionsRelativePath,
                InputFileRelativePath = row.InputFileRelativePath,
                InteractiveTerminal = row.InteractiveTerminal,
                InteractiveStartPrompt = row.InteractiveStartPrompt,
                // These are not stored in DB; use appsettings defaults
                PersistLogsToRepo = _defaults.PersistLogsToRepo,
                LogTailLines = _defaults.LogTailLines,
                LogBatchInterval = _defaults.LogBatchInterval,
            };
        }
        catch
        {
            return null;
        }
    }
}
