using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly WorkflowDbContext _db;

    public EventsController(WorkflowDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkflowEvent>>> GetAll(
        [FromQuery] string? workflowId,
        [FromQuery] string? agentId,
        [FromQuery] string? eventType,
        [FromQuery] int limit = 50)
    {
        var query = _db.Events.AsQueryable();

        if (!string.IsNullOrEmpty(workflowId))
            query = query.Where(e => e.WorkflowId == workflowId);
        if (!string.IsNullOrEmpty(agentId))
            query = query.Where(e => e.AgentId == agentId);
        if (!string.IsNullOrEmpty(eventType))
            query = query.Where(e => e.EventType == eventType);

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowEvent>> Create(WorkflowEvent evt)
    {
        evt.CreatedAt = DateTime.UtcNow;
        _db.Events.Add(evt);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), evt);
    }
}
