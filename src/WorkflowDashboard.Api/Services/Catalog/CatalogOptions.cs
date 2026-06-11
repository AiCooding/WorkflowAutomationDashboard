namespace WorkflowDashboard.Api.Services.Catalog;

/// <summary>
/// Binds the <c>Catalog</c> section of appsettings.
/// Null values fall back to <c>~/.copilot/{workflows|agents}</c>.
/// </summary>
public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    public string? WorkflowsDir { get; set; }
    public string? AgentsDir { get; set; }
}
