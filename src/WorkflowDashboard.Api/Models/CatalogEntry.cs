namespace WorkflowDashboard.Api.Models;

/// <summary>
/// In-memory representation of a discovered workflow or agent markdown file
/// from <c>~/.copilot/{workflows|agents}/*.md</c>. Not persisted to the DB.
/// </summary>
public sealed record CatalogEntry(
    string Slug,
    string Kind, // "workflow" | "agent"
    string DisplayName,
    string? Description,
    string SourcePath,
    bool IsBroken,
    string? BrokenReason,
    DateTime LoadedAt);

/// <summary>
/// Detail view of a catalog entry: the entry, raw markdown source, and
/// server-side sanitised HTML.
/// </summary>
public sealed record CatalogEntryDetail(
    CatalogEntry Entry,
    string MarkdownSource,
    string RenderedHtml);
