using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Services.Catalog;

/// <summary>
/// Thread-safe accessor for the in-memory catalog populated by <see cref="CatalogScanner"/>.
/// </summary>
public interface ICatalogStore
{
    /// <summary>All entries (workflows + agents), broken or not.</summary>
    IReadOnlyList<CatalogEntry> List();

    /// <summary>Lookup by kind ("workflow"|"agent") and slug.</summary>
    bool TryGet(string kind, string slug, out CatalogEntry entry);

    /// <summary>Replace the store contents atomically.</summary>
    void Replace(IEnumerable<CatalogEntry> entries);

    /// <summary>Summary counts (workflows, agents, broken).</summary>
    CatalogCounts Counts();
}

public sealed record CatalogCounts(int WorkflowCount, int AgentCount, int BrokenCount);
