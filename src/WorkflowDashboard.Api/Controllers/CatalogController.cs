using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Models;
using WorkflowDashboard.Api.Services.Catalog;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogStore _store;
    private readonly CatalogScanner _scanner;
    private readonly MarkdownRenderer _renderer;
    private readonly IHubContext<WorkflowHub> _hub;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(
        ICatalogStore store,
        CatalogScanner scanner,
        MarkdownRenderer renderer,
        IHubContext<WorkflowHub> hub,
        ILogger<CatalogController> logger)
    {
        _store = store;
        _scanner = scanner;
        _renderer = renderer;
        _hub = hub;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<CatalogEntry>> List()
    {
        return Ok(_store.List());
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<object>> Refresh()
    {
        _logger.LogInformation("Catalog refresh requested.");
        var counts = _scanner.Scan();
        var payload = new
        {
            workflowCount = counts.WorkflowCount,
            agentCount = counts.AgentCount,
            brokenCount = counts.BrokenCount,
        };
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.CatalogRefreshed, payload);
        _logger.LogInformation(
            "Catalog refresh completed with {WorkflowCount} workflows, {AgentCount} agents, and {BrokenCount} broken entries.",
            counts.WorkflowCount, counts.AgentCount, counts.BrokenCount);
        return Ok(payload);
    }

    [HttpGet("{kind}/{slug}")]
    public ActionResult<CatalogEntryDetail> Get(string kind, string slug)
    {
        if (!IsValidKind(kind))
        {
            _logger.LogWarning("Catalog entry requested with invalid kind '{Kind}'.", kind);
            return BadRequest(new { message = "kind must be 'workflow' or 'agent'." });
        }

        if (!_store.TryGet(kind, slug, out var entry))
            return NotFound();

        string source;
        try
        {
            source = System.IO.File.ReadAllText(entry.SourcePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Catalog source file could not be read for {Kind}/{Slug}.", kind, slug);
            return Ok(new CatalogEntryDetail(
                entry with { IsBroken = true, BrokenReason = $"Unable to read file: {ex.Message}" },
                MarkdownSource: string.Empty,
                RenderedHtml: string.Empty));
        }

        var body = CatalogScanner.StripFrontMatter(source);
        var html = _renderer.Render(body);

        return Ok(new CatalogEntryDetail(entry, MarkdownSource: source, RenderedHtml: html));
    }

    [HttpGet("{kind}/{slug}/source")]
    public IActionResult GetSource(string kind, string slug)
    {
        if (!IsValidKind(kind))
        {
            _logger.LogWarning("Catalog source requested with invalid kind '{Kind}'.", kind);
            return BadRequest(new { message = "kind must be 'workflow' or 'agent'." });
        }

        if (!_store.TryGet(kind, slug, out var entry))
            return NotFound();

        try
        {
            var text = System.IO.File.ReadAllText(entry.SourcePath);
            return Content(text, "text/markdown");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Catalog source file could not be read for {Kind}/{Slug}.", kind, slug);
            return NotFound(new { message = $"Source file unreadable: {ex.Message}" });
        }
    }

    private static bool IsValidKind(string kind) =>
        kind == "workflow" || kind == "agent";
}
