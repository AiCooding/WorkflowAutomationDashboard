using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommandsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly IHubContext<WorkflowHub> _hub;

    public CommandsController(WorkflowDbContext db, IHubContext<WorkflowHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<List<Command>>> GetAll([FromQuery] string? status)
    {
        var query = _db.Commands.AsQueryable();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.Status == status);

        return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Command>> Create(Command command)
    {
        if (string.IsNullOrEmpty(command.Id))
            command.Id = Guid.NewGuid().ToString("N")[..12];

        command.CreatedAt = DateTime.UtcNow;
        _db.Commands.Add(command);

        _db.Events.Add(new WorkflowEvent
        {
            WorkflowId = command.WorkflowId,
            EventType = "command_received",
            Message = $"Command '{command.CommandType}' issued",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.CommandIssued, command);

        return CreatedAtAction(nameof(GetAll), command);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Command>> MarkProcessed(string id, [FromBody] CommandProcessed processed)
    {
        var command = await _db.Commands.FindAsync(id);
        if (command is null) return NotFound();

        command.Status = processed.Status;
        command.ProcessedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return command;
    }
}

public record CommandProcessed(string Status);
