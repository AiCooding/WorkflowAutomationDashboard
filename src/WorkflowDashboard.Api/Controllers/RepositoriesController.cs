using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Models;
using WorkflowDashboard.Api.Services.Pipeline;
using WorkflowDashboard.Api.Services.Repositories;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoriesController : ControllerBase
{
    private static readonly string[] AllowedSpecFiles = { "proposal.md", "design.md", "tasks.md" };

    private readonly WorkflowDbContext _db;
    private readonly IHubContext<WorkflowHub> _hub;
    private readonly IPipelineOrchestrator _orchestrator;

    public RepositoriesController(
        WorkflowDbContext db,
        IHubContext<WorkflowHub> hub,
        IPipelineOrchestrator orchestrator)
    {
        _db = db;
        _hub = hub;
        _orchestrator = orchestrator;
    }

    [HttpGet]
    public async Task<ActionResult<List<RepositoryDto>>> GetAll()
    {
        var repos = await _db.Repositories
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync();

        var counts = await _db.Features
            .Where(f => f.RepositoryId != null)
            .GroupBy(f => f.RepositoryId!)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count);

        return repos.Select(r => ToDto(r, counts.TryGetValue(r.Id, out var c) ? c : 0)).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RepositoryDto>> GetById(string id)
    {
        var repo = await _db.Repositories.FindAsync(id);
        if (repo is null) return NotFound();
        var count = await _db.Features.CountAsync(f => f.RepositoryId == id);
        return ToDto(repo, count);
    }

    [HttpPost]
    public async Task<ActionResult<RepositoryDto>> Create([FromBody] RepositoryCreate body)
    {
        var path = (body.Path ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(path))
            return BadRequest(new { message = "Path is required." });

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Invalid path: {ex.Message}" });
        }

        if (!Directory.Exists(fullPath))
            return BadRequest(new { message = $"Path does not exist or is not a directory: {fullPath}" });

        if (await _db.Repositories.AnyAsync(r => r.Path == fullPath))
            return BadRequest(new { message = "A repository with this path is already registered." });

        var name = string.IsNullOrWhiteSpace(body.Name)
            ? DefaultNameFromPath(fullPath)
            : body.Name.Trim();

        var repo = new Repository
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Path = fullPath,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Repositories.Add(repo);
        await _db.SaveChangesAsync();

        var dto = ToDto(repo, 0);
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.RepositoryUpdated, dto);

        return CreatedAtAction(nameof(GetById), new { id = repo.Id }, dto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<RepositoryDto>> Update(string id, [FromBody] RepositoryUpdate body)
    {
        var repo = await _db.Repositories.FindAsync(id);
        if (repo is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(body.Name))
            repo.Name = body.Name.Trim();

        if (body.Path is not null)
        {
            var path = body.Path.Trim();
            if (string.IsNullOrEmpty(path))
                return BadRequest(new { message = "Path must not be empty when provided." });

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Invalid path: {ex.Message}" });
            }

            if (!Directory.Exists(fullPath))
                return BadRequest(new { message = $"Path does not exist or is not a directory: {fullPath}" });

            if (await _db.Repositories.AnyAsync(r => r.Path == fullPath && r.Id != id))
                return BadRequest(new { message = "Another repository with this path is already registered." });

            repo.Path = fullPath;
        }

        repo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var count = await _db.Features.CountAsync(f => f.RepositoryId == id);
        var dto = ToDto(repo, count);
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.RepositoryUpdated, dto);
        return dto;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var repo = await _db.Repositories.FindAsync(id);
        if (repo is null) return NotFound();

        var activeRunIds = await _db.PipelineRuns
            .Where(r => r.RepositoryId == id && (r.Status == "pending" || r.Status == "queued" || r.Status == "running" || r.Status == "waiting_approval"))
            .Select(r => r.Id)
            .ToListAsync();

        foreach (var runId in activeRunIds)
        {
            try { await _orchestrator.CancelRunAsync(runId); }
            catch { }
        }

        var orphaned = await _db.Features
            .Where(f => f.RepositoryId == id)
            .ToListAsync();
        foreach (var feature in orphaned)
        {
            feature.RepositoryId = null;
            feature.UpdatedAt = DateTime.UtcNow;
        }

        var runs = await _db.PipelineRuns
            .Where(r => r.RepositoryId == id)
            .ToListAsync();
        if (runs.Count > 0)
            _db.PipelineRuns.RemoveRange(runs);

        _db.Repositories.Remove(repo);
        await _db.SaveChangesAsync();

        var dto = ToDto(repo, 0);
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.RepositoryUpdated, dto with { Deleted = true });

        foreach (var f in orphaned)
            await _hub.Clients.All.SendAsync(WorkflowHubMethods.FeatureUpdated, f);

        return NoContent();
    }

    [HttpGet("{id}/specs")]
    public async Task<ActionResult<List<SpecFolderRow>>> ListSpecs(string id)
    {
        var repo = await _db.Repositories.FindAsync(id);
        if (repo is null) return NotFound();

        var specsRootRel = "openspec/specs";
        var specsRootAbs = RepositoryPathHelper.TryResolveInside(repo, specsRootRel);
        if (specsRootAbs is null)
            return BadRequest(new { message = "Resolved specs root escapes the repository root." });

        if (!Directory.Exists(specsRootAbs))
            return Ok(new List<SpecFolderRow>());

        var rows = new List<SpecFolderRow>();
        foreach (var dir in Directory.EnumerateDirectories(specsRootAbs))
        {
            var slug = Path.GetFileName(dir);
            var rel = $"{specsRootRel}/{slug}";
            var hasProposal = System.IO.File.Exists(Path.Combine(dir, "proposal.md"));
            var hasDesign = System.IO.File.Exists(Path.Combine(dir, "design.md"));
            var hasTasks = System.IO.File.Exists(Path.Combine(dir, "tasks.md"));
            var mtime = Directory.GetLastWriteTimeUtc(dir);
            rows.Add(new SpecFolderRow(slug, rel, hasProposal, hasDesign, hasTasks, mtime));
        }

        rows.Sort((a, b) => b.Mtime.CompareTo(a.Mtime));
        return Ok(rows);
    }

    [HttpGet("{id}/specs/{slug}/files/{filename}")]
    public async Task<IActionResult> GetSpecFile(string id, string slug, string filename)
    {
        if (!AllowedSpecFiles.Contains(filename, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = $"filename must be one of: {string.Join(", ", AllowedSpecFiles)}." });

        var repo = await _db.Repositories.FindAsync(id);
        if (repo is null) return NotFound();

        var rel = $"openspec/specs/{slug}/{filename}";
        var abs = RepositoryPathHelper.TryResolveInside(repo, rel);
        if (abs is null)
            return BadRequest(new { message = "Resolved file path escapes the repository root." });
        if (!System.IO.File.Exists(abs))
            return NotFound(new { message = $"File '{rel}' not found." });

        var content = await System.IO.File.ReadAllTextAsync(abs);
        return Content(content, "text/markdown");
    }

    private static RepositoryDto ToDto(Repository r, int featureCount) => new(
        r.Id,
        r.Path,
        r.Name,
        IsBroken: !Directory.Exists(r.Path),
        FeatureCount: featureCount,
        r.CreatedAt,
        r.UpdatedAt,
        Deleted: false);

    private static string DefaultNameFromPath(string fullPath)
    {
        var name = new DirectoryInfo(fullPath).Name;
        return string.IsNullOrWhiteSpace(name) ? fullPath : name;
    }
}

public record RepositoryCreate(string Path, string? Name = null);

public record RepositoryUpdate(string? Name = null, string? Path = null);

public record RepositoryDto(
    string Id,
    string Path,
    string Name,
    bool IsBroken,
    int FeatureCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool Deleted);

public record SpecFolderRow(
    string Slug,
    string Path,
    bool HasProposal,
    bool HasDesign,
    bool HasTasks,
    DateTime Mtime);
