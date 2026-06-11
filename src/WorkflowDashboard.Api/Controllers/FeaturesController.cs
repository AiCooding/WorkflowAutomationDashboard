using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Models;
using WorkflowDashboard.Api.Services.Repositories;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeaturesController : ControllerBase
{
    private static readonly Regex KebabSlugRegex = new("^[a-z0-9][a-z0-9-]*[a-z0-9]$", RegexOptions.Compiled);

    private readonly WorkflowDbContext _db;
    private readonly IHubContext<WorkflowHub> _hub;

    public FeaturesController(WorkflowDbContext db, IHubContext<WorkflowHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<List<object>>> GetAll([FromQuery] string? status)
    {
        var query = _db.Features.AsQueryable();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(f => f.Status == status);

        var features = await query.OrderByDescending(f => f.UpdatedAt).ToListAsync();
        var repoPaths = await LoadRepoPaths(features);
        return features.Select(f => (object)ToDto(f, repoPaths)).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetById(string id)
    {
        var feature = await _db.Features.FirstOrDefaultAsync(f => f.Id == id);
        if (feature is null) return NotFound();
        var repoPaths = await LoadRepoPaths([feature]);
        return ToDto(feature, repoPaths);
    }

    [HttpPost]
    public async Task<ActionResult<Feature>> Create([FromBody] CreateFeatureBody body)
    {
        if (body is null)
            return BadRequest(new { message = "Body is required." });
        if (string.IsNullOrWhiteSpace(body.RepositoryId))
            return BadRequest(new { message = "repositoryId is required." });
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { message = "name is required." });

        var mode = (body.Mode ?? string.Empty).Trim().ToLowerInvariant();
        if (mode is not ("link" or "stub" or "inline"))
            return BadRequest(new { message = "mode must be one of: link, stub, inline." });

        var repo = await _db.Repositories.FindAsync(body.RepositoryId);
        if (repo is null)
            return BadRequest(new { message = $"Repository '{body.RepositoryId}' not found." });
        if (!Directory.Exists(repo.Path))
            return BadRequest(new { message = $"Repository path '{repo.Path}' is missing on disk (repository is broken). Repair it before creating features." });

        string specFolderRel;

        switch (mode)
        {
            case "link":
            {
                if (string.IsNullOrWhiteSpace(body.SpecFolderSlug))
                    return BadRequest(new { message = "specFolderSlug is required for mode=link." });

                var slug = body.SpecFolderSlug.Trim();
                if (!KebabSlugRegex.IsMatch(slug))
                    return BadRequest(new { message = "specFolderSlug must be kebab-case (lowercase letters, digits, hyphens)." });

                specFolderRel = $"openspec/specs/{slug}";
                var resolved = RepositoryPathHelper.TryResolveInside(repo, specFolderRel);
                if (resolved is null)
                    return BadRequest(new { message = "Resolved spec folder escapes the repository root." });
                if (!Directory.Exists(resolved))
                    return BadRequest(new { message = $"Spec folder '{specFolderRel}' does not exist in the repository." });
                if (!System.IO.File.Exists(Path.Combine(resolved, "proposal.md")))
                    return BadRequest(new { message = $"Spec folder '{specFolderRel}' does not contain proposal.md." });
                break;
            }
            case "stub":
            case "inline":
            {
                var rawSlug = (body.SpecSlug ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(rawSlug))
                    return BadRequest(new { message = $"specSlug is required for mode={mode}." });
                if (!KebabSlugRegex.IsMatch(rawSlug))
                    return BadRequest(new { message = "specSlug must be kebab-case (lowercase letters, digits, hyphens)." });

                specFolderRel = $"openspec/specs/{rawSlug}";
                var resolved = RepositoryPathHelper.TryResolveInside(repo, specFolderRel);
                if (resolved is null)
                    return BadRequest(new { message = "Resolved spec folder escapes the repository root." });
                if (Directory.Exists(resolved))
                    return BadRequest(new { message = $"Spec folder '{specFolderRel}' already exists; refusing to overwrite." });
                if (mode == "inline" && string.IsNullOrWhiteSpace(body.SpecBody))
                    return BadRequest(new { message = "specBody is required for mode=inline." });

                Directory.CreateDirectory(resolved);
                var proposalPath = Path.Combine(resolved, "proposal.md");
                var content = mode == "inline"
                    ? body.SpecBody!
                    : $"# {body.Name}\n\n## Problem\n\n## Goals\n";
                await System.IO.File.WriteAllTextAsync(proposalPath, content);
                break;
            }
            default:
                return BadRequest(new { message = "Unsupported mode." });
        }

        if (await _db.Features.AnyAsync(f => f.RepositoryId == repo.Id && f.SpecFolder == specFolderRel))
            return BadRequest(new { message = $"A feature for spec '{specFolderRel}' on this repository already exists." });

        var feature = new Feature
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = body.Name.Trim(),
            Description = body.Description?.Trim(),
            Status = "backlog",
            Priority = 0,
            SpecFolder = specFolderRel,
            RepositoryId = repo.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Features.Add(feature);
        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync(WorkflowHubMethods.FeatureUpdated, feature);
        return CreatedAtAction(nameof(GetById), new { id = feature.Id }, feature);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Feature>> Update(string id, [FromBody] FeatureUpdateBody body)
    {
        var feature = await _db.Features.FindAsync(id);
        if (feature is null) return NotFound();
        if (body is null) return BadRequest();

        if (body.Name is not null) feature.Name = body.Name.Trim();
        if (body.Description is not null) feature.Description = body.Description;
        if (body.Status is not null) feature.Status = body.Status;
        if (body.Priority.HasValue) feature.Priority = body.Priority.Value;
        feature.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.FeatureUpdated, feature);
        return feature;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var feature = await _db.Features.FindAsync(id);
        if (feature is null) return NotFound();

        _db.Features.Remove(feature);
        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync(
            WorkflowHubMethods.FeatureUpdated,
            new FeatureUpdatedEnvelope(feature, Deleted: true));
        return NoContent();
    }

    [HttpGet("{id}/spec")]
    public async Task<ActionResult<SpecManifest>> GetSpec(string id)
    {
        var feature = await _db.Features.FindAsync(id);
        if (feature is null) return NotFound();
        if (string.IsNullOrEmpty(feature.SpecFolder))
            return NotFound(new { message = "This feature has no spec folder." });
        if (string.IsNullOrEmpty(feature.RepositoryId))
            return NotFound(new { message = "This feature is orphaned (no linked repository)." });

        var repo = await _db.Repositories.FindAsync(feature.RepositoryId);
        if (repo is null)
            return NotFound(new { message = "Linked repository is missing." });

        var folderAbs = RepositoryPathHelper.TryResolveInside(repo, feature.SpecFolder);
        if (folderAbs is null)
            return BadRequest(new { message = "Resolved spec folder escapes the repository root." });

        async Task<SpecFile> ReadFile(string name)
        {
            if (!Directory.Exists(folderAbs))
                return new SpecFile(false, null);
            var path = Path.Combine(folderAbs, name);
            if (!System.IO.File.Exists(path))
                return new SpecFile(false, null);
            var content = await System.IO.File.ReadAllTextAsync(path);
            return new SpecFile(true, content);
        }

        return new SpecManifest(
            Folder: feature.SpecFolder,
            RepositoryId: feature.RepositoryId,
            Proposal: await ReadFile("proposal.md"),
            Design: await ReadFile("design.md"),
            Tasks: await ReadFile("tasks.md"));
    }

    private async Task<Dictionary<string, string>> LoadRepoPaths(IEnumerable<Feature> features)
    {
        var ids = features
            .Where(f => f.RepositoryId != null)
            .Select(f => f.RepositoryId!)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return new();
        return await _db.Repositories
            .Where(r => ids.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Path);
    }

    private static object ToDto(Feature f, Dictionary<string, string> repoPaths) => new
    {
        f.Id,
        f.Name,
        f.Description,
        f.Status,
        DerivedStatus = DeriveStatus(f, repoPaths),
        f.Priority,
        f.SpecFolder,
        f.RepositoryId,
        f.CreatedAt,
        f.UpdatedAt,
    };

    private static string DeriveStatus(Feature f, Dictionary<string, string> repoPaths)
    {
        if (string.IsNullOrEmpty(f.SpecFolder) || string.IsNullOrEmpty(f.RepositoryId))
            return "backlog";
        if (!repoPaths.TryGetValue(f.RepositoryId, out var repoPath))
            return "backlog";

        var folder = Path.Combine(repoPath, f.SpecFolder);
        if (System.IO.File.Exists(Path.Combine(folder, "tasks.md"))) return "ready";
        if (System.IO.File.Exists(Path.Combine(folder, "design.md"))) return "design";
        if (System.IO.File.Exists(Path.Combine(folder, "proposal.md"))) return "planning";
        return "backlog";
    }
}

public record CreateFeatureBody(
    string RepositoryId,
    string Name,
    string? Description,
    string Mode,
    string? SpecFolderSlug = null,
    string? SpecSlug = null,
    string? SpecBody = null);

public record FeatureUpdateBody(
    string? Name = null,
    string? Description = null,
    string? Status = null,
    int? Priority = null);

public record SpecFile(bool Exists, string? Content);

public record SpecManifest(
    string Folder,
    string RepositoryId,
    SpecFile Proposal,
    SpecFile Design,
    SpecFile Tasks);

public record FeatureUpdatedEnvelope(Feature Feature, bool Deleted);
