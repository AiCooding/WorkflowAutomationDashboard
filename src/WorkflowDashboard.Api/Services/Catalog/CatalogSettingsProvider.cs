using Microsoft.Extensions.Options;
using WorkflowDashboard.Api.Data;

namespace WorkflowDashboard.Api.Services.Catalog;

public sealed class CatalogSettingsProvider : ICatalogSettingsProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CatalogOptions _defaults;
    private CatalogOptions? _cached;
    private readonly object _lock = new();

    public CatalogSettingsProvider(
        IServiceScopeFactory scopeFactory,
        IOptions<CatalogOptions> defaults)
    {
        _scopeFactory = scopeFactory;
        _defaults = defaults.Value;
    }

    public CatalogOptions GetEffective()
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

    private CatalogOptions? LoadFromDb()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var row = db.AgentRunnerSettings.Find(1);
            if (row is null || string.IsNullOrWhiteSpace(row.AgentsDir))
                return null;

            return new CatalogOptions
            {
                AgentsDir = row.AgentsDir,
                WorkflowsDir = _defaults.WorkflowsDir,
            };
        }
        catch
        {
            return null;
        }
    }
}
