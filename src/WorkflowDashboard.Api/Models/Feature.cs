namespace WorkflowDashboard.Api.Models;

public class Feature
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "backlog"; // backlog|planning|in_progress|review|done|cancelled
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Path **relative to <see cref="Repository.Path"/>** to the OpenSpec folder, e.g.
    /// <c>openspec/specs/repo-linking</c>. The folder is expected to contain
    /// <c>proposal.md</c>, optionally <c>design.md</c> and <c>tasks.md</c>.
    /// The dashboard never stores per-file paths — those filenames are convention.
    /// </summary>
    public string? SpecFolder { get; set; }

    /// <summary>
    /// FK to <see cref="Repository.Id"/>. Nullable so a feature can become "orphaned"
    /// when its repository is deleted (<c>OnDelete: SetNull</c>).
    /// </summary>
    public string? RepositoryId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Repository? Repository { get; set; }
}
