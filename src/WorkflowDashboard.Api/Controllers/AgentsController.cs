using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly IHubContext<WorkflowHub> _hub;

    public AgentsController(WorkflowDbContext db, IHubContext<WorkflowHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<List<Agent>>> GetAll([FromQuery] string? status)
    {
        var query = _db.Agents.AsQueryable();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(a => a.Status == status);

        return await query.OrderByDescending(a => a.StartedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Agent>> GetById(string id)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent is null) return NotFound();
        return agent;
    }

    [HttpPost]
    public async Task<ActionResult<Agent>> Create(Agent agent)
    {
        if (string.IsNullOrEmpty(agent.Id))
            agent.Id = Guid.NewGuid().ToString("N")[..12];

        agent.StartedAt = DateTime.UtcNow;
        _db.Agents.Add(agent);

        _db.Events.Add(new WorkflowEvent
        {
            WorkflowId = agent.WorkflowId,
            AgentId = agent.Id,
            EventType = "state_change",
            Message = $"Agent '{agent.AgentType}' started",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.AgentUpdated, agent);

        return CreatedAtAction(nameof(GetById), new { id = agent.Id }, agent);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Agent>> Update(string id, [FromBody] AgentUpdate update)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent is null) return NotFound();

        var oldStatus = agent.Status;

        if (update.Status is not null)
            agent.Status = update.Status;
        if (update.CurrentTask is not null)
            agent.CurrentTask = update.CurrentTask;
        if (update.Status is "completed" or "failed")
            agent.CompletedAt = DateTime.UtcNow;

        if (oldStatus != agent.Status)
        {
            _db.Events.Add(new WorkflowEvent
            {
                WorkflowId = agent.WorkflowId,
                AgentId = agent.Id,
                EventType = "state_change",
                Message = $"Agent '{agent.AgentType}' status: {oldStatus} → {agent.Status}",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.AgentUpdated, agent);

        return agent;
    }
}

public record AgentUpdate(string? Status, string? CurrentTask);
