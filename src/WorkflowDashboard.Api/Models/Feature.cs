namespace WorkflowDashboard.Api.Models;

public class Feature
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "backlog"; // backlog|planning|in_progress|review|done|cancelled
    public int Priority { get; set; } = 0;
    public string? SpecPath { get; set; } // relative path inside Specs:RootDir to the feature description markdown
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Workflow> Workflows { get; set; } = new List<Workflow>();
}
