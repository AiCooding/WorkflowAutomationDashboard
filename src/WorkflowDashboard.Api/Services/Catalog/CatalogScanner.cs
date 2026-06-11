using System.Text;
using Microsoft.Extensions.Options;
using WorkflowDashboard.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WorkflowDashboard.Api.Services.Catalog;

/// <summary>
/// Scans <c>~/.copilot/workflows/*.md</c> and <c>~/.copilot/agents/*.md</c>,
/// parses optional YAML front-matter, and populates the <see cref="ICatalogStore"/>.
/// "Broken" file-level conditions: I/O failure, unparseable YAML front-matter,
/// or duplicate slug within the same kind. No cross-reference validation.
/// </summary>
public sealed class CatalogScanner
{
    private readonly ICatalogStore _store;
    private readonly CatalogOptions _options;
    private readonly ILogger<CatalogScanner> _logger;

    public CatalogScanner(
        ICatalogStore store,
        IOptions<CatalogOptions> options,
        ILogger<CatalogScanner> logger)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    public string WorkflowsDir => string.IsNullOrWhiteSpace(_options.WorkflowsDir)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "workflows")
        : _options.WorkflowsDir;

    public string AgentsDir => string.IsNullOrWhiteSpace(_options.AgentsDir)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "agents")
        : _options.AgentsDir;

    /// <summary>
    /// Rescan both directories and replace the store contents.
    /// Returns counts {workflowCount, agentCount, brokenCount}.
    /// </summary>
    public CatalogCounts Scan()
    {
        var entries = new List<CatalogEntry>();
        ScanKind("workflow", WorkflowsDir, entries);
        ScanKind("agent", AgentsDir, entries);
        _store.Replace(entries);
        return _store.Counts();
    }

    private void ScanKind(string kind, string dir, List<CatalogEntry> sink)
    {
        if (!Directory.Exists(dir))
        {
            _logger.LogInformation(
                "Catalog: {Kind}s directory does not exist ({Dir}); treating as empty.",
                kind, dir);
            return;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(dir, "*.md", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Catalog: failed to enumerate {Dir}", dir);
            return;
        }

        // Parse each file, track duplicate slugs within this kind.
        var parsed = new List<CatalogEntry>(files.Length);
        var slugCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var entry = ParseFile(file, kind);
            parsed.Add(entry);
            if (!entry.IsBroken)
            {
                slugCounts[entry.Slug] = slugCounts.TryGetValue(entry.Slug, out var c) ? c + 1 : 1;
            }
        }

        // Flag duplicate slugs (within the same kind) as broken.
        foreach (var e in parsed)
        {
            if (!e.IsBroken && slugCounts.TryGetValue(e.Slug, out var c) && c > 1)
            {
                sink.Add(e with
                {
                    IsBroken = true,
                    BrokenReason = $"Duplicate slug '{e.Slug}' within {kind}s (found {c} files).",
                });
            }
            else
            {
                sink.Add(e);
            }
        }
    }

    private CatalogEntry ParseFile(string path, string kind)
    {
        var loadedAt = DateTime.UtcNow;
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            return new CatalogEntry(
                Slug: stem,
                Kind: kind,
                DisplayName: Humanise(stem),
                Description: null,
                SourcePath: path,
                IsBroken: true,
                BrokenReason: $"Unable to read file: {ex.Message}",
                LoadedAt: loadedAt);
        }

        var (frontMatter, hasFrontMatter, frontMatterError) = ExtractFrontMatter(text);

        var filenameStem = Path.GetFileNameWithoutExtension(path);

        if (frontMatterError is not null)
        {
            return new CatalogEntry(
                Slug: filenameStem,
                Kind: kind,
                DisplayName: Humanise(filenameStem),
                Description: null,
                SourcePath: path,
                IsBroken: true,
                BrokenReason: $"Unparseable YAML front-matter: {frontMatterError}",
                LoadedAt: loadedAt);
        }

        FrontMatter? meta = null;
        if (hasFrontMatter)
        {
            try
            {
                var deser = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
                meta = deser.Deserialize<FrontMatter>(frontMatter ?? string.Empty);
            }
            catch (Exception ex)
            {
                return new CatalogEntry(
                    Slug: filenameStem,
                    Kind: kind,
                    DisplayName: Humanise(filenameStem),
                    Description: null,
                    SourcePath: path,
                    IsBroken: true,
                    BrokenReason: $"Unparseable YAML front-matter: {ex.Message}",
                    LoadedAt: loadedAt);
            }
        }

        var slug = !string.IsNullOrWhiteSpace(meta?.Slug) ? meta!.Slug!.Trim() : filenameStem;
        var displayName = !string.IsNullOrWhiteSpace(meta?.Name) ? meta!.Name!.Trim() : Humanise(filenameStem);
        var description = string.IsNullOrWhiteSpace(meta?.Description) ? null : meta!.Description!.Trim();

        return new CatalogEntry(
            Slug: slug,
            Kind: kind,
            DisplayName: displayName,
            Description: description,
            SourcePath: path,
            IsBroken: false,
            BrokenReason: null,
            LoadedAt: loadedAt);
    }

    /// <summary>
    /// Returns (frontMatterYaml, hasFrontMatter, error). If the file begins with
    /// "---\n" the front-matter is the text up to the next "---" delimiter.
    /// Missing closing delimiter is an error.
    /// </summary>
    internal static (string? Yaml, bool Has, string? Error) ExtractFrontMatter(string text)
    {
        if (string.IsNullOrEmpty(text)) return (null, false, null);

        // Tolerate BOM and CRLF.
        var s = text;
        if (s.Length > 0 && s[0] == '\uFEFF') s = s[1..];

        if (!s.StartsWith("---", StringComparison.Ordinal)) return (null, false, null);

        // Must be followed by a line break for it to be a front-matter delimiter.
        var afterFirst = 3;
        if (afterFirst >= s.Length) return (null, false, null);
        char ch = s[afterFirst];
        if (ch != '\n' && ch != '\r') return (null, false, null);

        // Skip the CR/LF after the opening ---.
        int contentStart = afterFirst;
        if (s[contentStart] == '\r') contentStart++;
        if (contentStart < s.Length && s[contentStart] == '\n') contentStart++;

        // Find closing --- delimiter on its own line.
        var sb = new StringBuilder();
        int i = contentStart;
        bool found = false;
        while (i < s.Length)
        {
            // Read one line
            int lineStart = i;
            while (i < s.Length && s[i] != '\n' && s[i] != '\r') i++;
            var line = s.Substring(lineStart, i - lineStart);
            // Consume line terminator
            if (i < s.Length && s[i] == '\r') i++;
            if (i < s.Length && s[i] == '\n') i++;

            if (line.Trim() == "---")
            {
                found = true;
                break;
            }
            sb.AppendLine(line);
        }

        if (!found)
        {
            return (null, true, "missing closing '---' delimiter");
        }

        return (sb.ToString(), true, null);
    }

    /// <summary>Strip optional front-matter, return body markdown.</summary>
    public static string StripFrontMatter(string text)
    {
        var (_, has, error) = ExtractFrontMatter(text);
        if (!has || error is not null) return text;

        // Locate body start
        var s = text;
        if (s.Length > 0 && s[0] == '\uFEFF') s = s[1..];
        // Skip opening "---" + line break
        int i = 3;
        if (i < s.Length && s[i] == '\r') i++;
        if (i < s.Length && s[i] == '\n') i++;
        // Walk lines until closing ---
        while (i < s.Length)
        {
            int lineStart = i;
            while (i < s.Length && s[i] != '\n' && s[i] != '\r') i++;
            var line = s.Substring(lineStart, i - lineStart);
            if (i < s.Length && s[i] == '\r') i++;
            if (i < s.Length && s[i] == '\n') i++;
            if (line.Trim() == "---") break;
        }
        return s[i..];
    }

    private static string Humanise(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem)) return stem;
        var parts = stem.Split(new[] { '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
        }
        return string.Join(' ', parts);
    }

    private sealed class FrontMatter
    {
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? Description { get; set; }
    }
}
