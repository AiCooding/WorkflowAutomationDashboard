using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Services.Catalog;

/// <summary>
/// Singleton in-memory store backing <see cref="ICatalogStore"/>.
/// Uses a copy-on-write snapshot (immutable list reference + volatile read)
/// for lock-free reads after the initial scan.
/// </summary>
public sealed class CatalogStore : ICatalogStore
{
    private IReadOnlyList<CatalogEntry> _snapshot = Array.Empty<CatalogEntry>();

    public IReadOnlyList<CatalogEntry> List() => Volatile.Read(ref _snapshot);

    public bool TryGet(string kind, string slug, out CatalogEntry entry)
    {
        foreach (var e in Volatile.Read(ref _snapshot))
        {
            if (string.Equals(e.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.Slug, slug, StringComparison.Ordinal))
            {
                entry = e;
                return true;
            }
        }
        entry = null!;
        return false;
    }

    public void Replace(IEnumerable<CatalogEntry> entries)
    {
        var list = entries.ToList();
        Volatile.Write(ref _snapshot, list);
    }

    public CatalogCounts Counts()
    {
        var snap = Volatile.Read(ref _snapshot);
        int wf = 0, ag = 0, broken = 0;
        foreach (var e in snap)
        {
            if (e.Kind == "workflow") wf++;
            else if (e.Kind == "agent") ag++;
            if (e.IsBroken) broken++;
        }
        return new CatalogCounts(wf, ag, broken);
    }
}
