using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeaturesController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly IHubContext<WorkflowHub> _hub;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public FeaturesController(
        WorkflowDbContext db,
        IHubContext<WorkflowHub> hub,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _db = db;
        _hub = hub;
        _config = config;
        _env = env;
    }

    [HttpGet]
    public async Task<ActionResult<List<Feature>>> GetAll([FromQuery] string? status)
    {
        var query = _db.Features.AsQueryable();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(f => f.Status == status);

        return await query.OrderByDescending(f => f.UpdatedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Feature>> GetById(string id)
    {
        var feature = await _db.Features
            .Include(f => f.Workflows)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (feature is null) return NotFound();
        return feature;
    }

    [HttpPost]
    public async Task<ActionResult<Feature>> Create(Feature feature)
    {
        if (string.IsNullOrEmpty(feature.Id))
            feature.Id = Guid.NewGuid().ToString("N")[..12];

        feature.CreatedAt = DateTime.UtcNow;
        feature.UpdatedAt = DateTime.UtcNow;

        _db.Features.Add(feature);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = feature.Id }, feature);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Feature>> Update(string id, Feature updated)
    {
        var feature = await _db.Features.FindAsync(id);
        if (feature is null) return NotFound();

        feature.Name = updated.Name;
        feature.Description = updated.Description;
        feature.Status = updated.Status;
        feature.Priority = updated.Priority;
        feature.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return feature;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var feature = await _db.Features.FindAsync(id);
        if (feature is null) return NotFound();

        _db.Features.Remove(feature);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Returns the markdown content of the feature's spec file.
    /// </summary>
    [HttpGet("{id}/spec")]
    public async Task<ActionResult<FeatureSpec>> GetSpec(string id)
    {
        var feature = await _db.Features.FindAsync(id);
        if (feature is null) return NotFound();
        if (string.IsNullOrEmpty(feature.SpecPath))
            return NotFound(new { message = "No spec has been written for this feature yet." });

        var rootDir = ResolveSpecsRoot();
        var fullPath = Path.GetFullPath(Path.Combine(rootDir, feature.SpecPath));
        if (!fullPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Spec path resolves outside the configured root." });
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = $"Spec file '{feature.SpecPath}' is missing on disk." });

        var content = await System.IO.File.ReadAllTextAsync(fullPath);
        return new FeatureSpec(feature.Id, feature.SpecPath, content, fullPath);
    }

    /// <summary>
    /// Writes the markdown content for a feature's spec file and updates Feature.SpecPath.
    /// Intended to be called by the PM agent at the end of a feature-spec workflow.
    /// </summary>
    [HttpPost("{id}/spec")]
    public async Task<ActionResult<FeatureSpec>> SaveSpec(string id, [FromBody] FeatureSpecUpdate body)
    {
        var feature = await _db.Features.FindAsync(id);
        if (feature is null) return NotFound();

        if (string.IsNullOrWhiteSpace(body.Content))
            return BadRequest(new { message = "Spec content must not be empty." });

        var fileName = SanitiseFileName(body.FileName ?? $"{feature.Id}.md");
        if (string.IsNullOrEmpty(fileName))
            return BadRequest(new { message = "Invalid file name." });

        var rootDir = ResolveSpecsRoot();
        Directory.CreateDirectory(rootDir);
        var fullPath = Path.GetFullPath(Path.Combine(rootDir, fileName));
        if (!fullPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Resolved file path escapes the configured root." });

        await System.IO.File.WriteAllTextAsync(fullPath, body.Content);

        feature.SpecPath = fileName;
        feature.UpdatedAt = DateTime.UtcNow;
        _db.Events.Add(new WorkflowEvent
        {
            EventType = "log",
            Message = $"Feature spec written: {feature.Name} ({feature.Id}) → {fileName}",
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        return new FeatureSpec(feature.Id, fileName, body.Content, fullPath);
    }

    private string ResolveSpecsRoot()
    {
        var configured = _config["Specs:RootDir"] ?? "docs/features";
        var rooted = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_env.ContentRootPath, configured);
        return Path.GetFullPath(rooted);
    }

    private static string SanitiseFileName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0) return string.Empty;
        // strip any path separators or traversal
        trimmed = Path.GetFileName(trimmed);
        // disallow control characters and unsafe symbols
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid) trimmed = trimmed.Replace(c, '_');
        if (!trimmed.Contains('.')) trimmed += ".md";
        return trimmed;
    }
}

public record FeatureSpec(string FeatureId, string SpecPath, string Content, string FullPath);
public record FeatureSpecUpdate(string Content, string? FileName = null);
