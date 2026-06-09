using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly IHubContext<WorkflowHub> _hub;

    public WorkflowsController(WorkflowDbContext db, IHubContext<WorkflowHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<List<Workflow>>> GetAll([FromQuery] string? status, [FromQuery] string? featureId)
    {
        var query = _db.Workflows.Include(w => w.Agents).AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(w => w.Status == status);
        if (!string.IsNullOrEmpty(featureId))
            query = query.Where(w => w.FeatureId == featureId);

        return await query.OrderByDescending(w => w.CreatedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Workflow>> GetById(string id)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Agents)
            .Include(w => w.InputRequests)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workflow is null) return NotFound();
        return workflow;
    }

    [HttpPost]
    public async Task<ActionResult<Workflow>> Create(Workflow workflow)
    {
        if (string.IsNullOrEmpty(workflow.Id))
            workflow.Id = Guid.NewGuid().ToString("N")[..12];

        workflow.CreatedAt = DateTime.UtcNow;
        _db.Workflows.Add(workflow);

        _db.Events.Add(new WorkflowEvent
        {
            WorkflowId = workflow.Id,
            EventType = "state_change",
            Message = $"Workflow '{workflow.Type}' created",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.WorkflowUpdated, workflow);

        return CreatedAtAction(nameof(GetById), new { id = workflow.Id }, workflow);
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<Workflow>> UpdateStatus(string id, [FromBody] WorkflowStatusUpdate update)
    {
        var workflow = await _db.Workflows.FindAsync(id);
        if (workflow is null) return NotFound();

        var oldStatus = workflow.Status;
        workflow.Status = update.Status;

        if (update.Status is "running" && workflow.StartedAt is null)
            workflow.StartedAt = DateTime.UtcNow;
        if (update.Status is "completed" or "failed" or "cancelled")
            workflow.CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(update.ErrorMessage))
            workflow.ErrorMessage = update.ErrorMessage;
        if (!string.IsNullOrEmpty(update.FeatureId))
            workflow.FeatureId = update.FeatureId;

        _db.Events.Add(new WorkflowEvent
        {
            WorkflowId = id,
            EventType = "state_change",
            Message = $"Workflow status changed: {oldStatus} → {update.Status}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.WorkflowUpdated, workflow);

        return workflow;
    }
}

public record WorkflowStatusUpdate(string Status, string? ErrorMessage = null, string? FeatureId = null);
