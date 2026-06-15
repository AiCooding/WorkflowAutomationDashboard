namespace WorkflowDashboard.Api.Services.Catalog;

public interface ICatalogSettingsProvider
{
    /// <summary>Returns effective catalog options: DB overrides appsettings defaults.</summary>
    CatalogOptions GetEffective();

    /// <summary>Clears the in-memory cache so the next call reloads from DB.</summary>
    void Invalidate();
}
