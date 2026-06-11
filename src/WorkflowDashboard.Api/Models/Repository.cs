namespace WorkflowDashboard.Api.Models;

/// <summary>
/// A registered local repository the dashboard can launch workflows against.
/// Broken-state (path missing on disk) is derived live via <c>Directory.Exists(Path)</c>
/// per request — it is not persisted.
/// </summary>
public class Repository
{
    public string Id { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
