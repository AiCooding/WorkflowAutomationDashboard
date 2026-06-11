using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Services.Repositories;

/// <summary>
/// Shared helpers for resolving paths inside a registered repository,
/// with a path-traversal guard. Used by the spec-folder picker, the
/// per-file fetch endpoint, and the feature spec manifest.
/// </summary>
public static class RepositoryPathHelper
{
    /// <summary>
    /// Resolves <paramref name="relativePath"/> against <paramref name="repository"/>'s
    /// root, returning the absolute path on success, or <c>null</c> if the resolved
    /// path escapes the repository root (path-traversal attempt).
    /// </summary>
    public static string? TryResolveInside(Repository repository, string relativePath)
    {
        if (repository is null) return null;
        if (string.IsNullOrWhiteSpace(relativePath)) return null;

        var repoRoot = Path.GetFullPath(repository.Path);
        var combined = Path.Combine(repoRoot, relativePath);
        string resolved;
        try
        {
            resolved = Path.GetFullPath(combined);
        }
        catch
        {
            return null;
        }

        // Ensure resolved path is within the repo. Append separator to the root
        // so "/repo" does not match "/repo-evil".
        var rootWithSep = repoRoot.EndsWith(Path.DirectorySeparatorChar)
            ? repoRoot
            : repoRoot + Path.DirectorySeparatorChar;

        if (string.Equals(resolved, repoRoot, StringComparison.OrdinalIgnoreCase))
            return resolved;
        if (!resolved.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            return null;

        return resolved;
    }
}
