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

    public CatalogController(
        ICatalogStore store,
        CatalogScanner scanner,
        MarkdownRenderer renderer,
        IHubContext<WorkflowHub> hub)
    {
        _store = store;
        _scanner = scanner;
        _renderer = renderer;
        _hub = hub;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<CatalogEntry>> List()
    {
        return Ok(_store.List());
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<object>> Refresh()
    {
        var counts = _scanner.Scan();
        var payload = new
        {
            workflowCount = counts.WorkflowCount,
            agentCount = counts.AgentCount,
            brokenCount = counts.BrokenCount,
        };
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.CatalogRefreshed, payload);
        return Ok(payload);
    }

    [HttpGet("{kind}/{slug}")]
    public ActionResult<CatalogEntryDetail> Get(string kind, string slug)
    {
        if (!IsValidKind(kind))
            return BadRequest(new { message = "kind must be 'workflow' or 'agent'." });

        if (!_store.TryGet(kind, slug, out var entry))
            return NotFound();

        string source;
        try
        {
            source = System.IO.File.ReadAllText(entry.SourcePath);
        }
        catch (Exception ex)
        {
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
            return BadRequest(new { message = "kind must be 'workflow' or 'agent'." });

        if (!_store.TryGet(kind, slug, out var entry))
            return NotFound();

        try
        {
            var text = System.IO.File.ReadAllText(entry.SourcePath);
            return Content(text, "text/markdown");
        }
        catch (Exception ex)
        {
            return NotFound(new { message = $"Source file unreadable: {ex.Message}" });
        }
    }

    private static bool IsValidKind(string kind) =>
        kind == "workflow" || kind == "agent";
}
