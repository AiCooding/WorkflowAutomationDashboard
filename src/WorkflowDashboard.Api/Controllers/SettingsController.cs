using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Models;
using WorkflowDashboard.Api.Services.AgentRunner;
using WorkflowDashboard.Api.Services.Catalog;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly IAgentRunnerSettingsProvider _agentProvider;
    private readonly ICatalogSettingsProvider _catalogProvider;
    private readonly AgentRunnerOptions _agentDefaults;
    private readonly CatalogOptions _catalogDefaults;

    public SettingsController(
        WorkflowDbContext db,
        IAgentRunnerSettingsProvider agentProvider,
        ICatalogSettingsProvider catalogProvider,
        Microsoft.Extensions.Options.IOptions<AgentRunnerOptions> agentDefaults,
        Microsoft.Extensions.Options.IOptions<CatalogOptions> catalogDefaults)
    {
        _db = db;
        _agentProvider = agentProvider;
        _catalogProvider = catalogProvider;
        _agentDefaults = agentDefaults.Value;
        _catalogDefaults = catalogDefaults.Value;
    }

    /// <summary>Returns the currently effective settings (DB overrides appsettings).</summary>
    [HttpGet]
    public ActionResult<AppSettingsDto> Get()
    {
        var opts = _agentProvider.GetEffective();
        var catalog = _catalogProvider.GetEffective();
        return ToDto(opts, catalog);
    }

    /// <summary>Saves settings to the DB singleton row and invalidates in-memory caches.</summary>
    [HttpPut]
    public async Task<ActionResult<AppSettingsDto>> Save([FromBody] AppSettingsDto dto)
    {
        var row = await _db.AgentRunnerSettings.FindAsync(1);
        if (row is null)
        {
            row = new AgentRunnerSettings { Id = 1 };
            _db.AgentRunnerSettings.Add(row);
        }

        row.CliTool = dto.CliTool;
        row.Executable = dto.Executable;
        row.ExtraArgsJson = JsonSerializer.Serialize(dto.ExtraArgs ?? []);
        row.InstructionsRelativePath = dto.InstructionsRelativePath;
        row.InputFileRelativePath = dto.InputFileRelativePath;
        row.InteractiveTerminal = dto.InteractiveTerminal;
        row.InteractiveStartPrompt = dto.InteractiveStartPrompt;
        row.Enabled = dto.Enabled;
        row.AgentsDir = string.IsNullOrWhiteSpace(dto.AgentsDir) ? null : dto.AgentsDir.Trim();

        await _db.SaveChangesAsync();
        _agentProvider.Invalidate();
        _catalogProvider.Invalidate();

        return ToDto(_agentProvider.GetEffective(), _catalogProvider.GetEffective());
    }

    /// <summary>Deletes the DB row, reverting to appsettings defaults.</summary>
    [HttpDelete]
    public async Task<IActionResult> Reset()
    {
        var row = await _db.AgentRunnerSettings.FindAsync(1);
        if (row is not null)
        {
            _db.AgentRunnerSettings.Remove(row);
            await _db.SaveChangesAsync();
        }
        _agentProvider.Invalidate();
        _catalogProvider.Invalidate();
        return NoContent();
    }

    private AppSettingsDto ToDto(AgentRunnerOptions opts, CatalogOptions catalog)
    {
        return new AppSettingsDto(
                opts.CliTool.ToString(),
                opts.Executable,
                opts.ExtraArgs,
                opts.InstructionsRelativePath,
                opts.InputFileRelativePath,
                opts.InteractiveTerminal,
                opts.InteractiveStartPrompt,
                opts.Enabled,
                catalog.AgentsDir);
        }
}

public record AppSettingsDto(
        string CliTool,
        string Executable,
        List<string> ExtraArgs,
        string InstructionsRelativePath,
        string InputFileRelativePath,
        bool InteractiveTerminal,
        string InteractiveStartPrompt,
        bool Enabled,
        string? AgentsDir);

/// <summary>Kept for backward compatibility — redirects to <see cref="AppSettingsDto"/>.</summary>
[System.Obsolete("Use AppSettingsDto")]
public record AgentRunnerSettingsDto(
    string CliTool,
    string Executable,
    List<string> ExtraArgs,
    string InstructionsRelativePath,
    string InputFileRelativePath,
    bool InteractiveTerminal,
    string InteractiveStartPrompt,
    bool Enabled);
