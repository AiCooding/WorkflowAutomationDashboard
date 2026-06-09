using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InputRequestsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly IHubContext<WorkflowHub> _hub;

    public InputRequestsController(WorkflowDbContext db, IHubContext<WorkflowHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<List<InputRequest>>> GetAll([FromQuery] string? status)
    {
        var query = _db.InputRequests.AsQueryable();
        if (!string.IsNullOrEmpty(status))
            query = query.Where(i => i.Status == status);

        return await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<InputRequest>> GetById(string id)
    {
        var input = await _db.InputRequests.FindAsync(id);
        if (input is null) return NotFound();
        return input;
    }

    [HttpPost]
    public async Task<ActionResult<InputRequest>> Create(InputRequest inputRequest)
    {
        if (string.IsNullOrEmpty(inputRequest.Id))
            inputRequest.Id = Guid.NewGuid().ToString("N")[..12];

        inputRequest.CreatedAt = DateTime.UtcNow;
        _db.InputRequests.Add(inputRequest);

        _db.Events.Add(new WorkflowEvent
        {
            WorkflowId = inputRequest.WorkflowId,
            AgentId = inputRequest.AgentId,
            EventType = "input_requested",
            Message = $"Input requested: {inputRequest.Question}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.InputRequested, inputRequest);

        return CreatedAtAction(nameof(GetById), new { id = inputRequest.Id }, inputRequest);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<InputRequest>> Answer(string id, [FromBody] InputAnswer answer)
    {
        var input = await _db.InputRequests.FindAsync(id);
        if (input is null) return NotFound();

        input.Response = answer.Response;
        input.Status = "answered";
        input.AnsweredAt = DateTime.UtcNow;

        _db.Events.Add(new WorkflowEvent
        {
            WorkflowId = input.WorkflowId,
            AgentId = input.AgentId,
            EventType = "state_change",
            Message = $"Input answered: {answer.Response}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync(WorkflowHubMethods.InputAnswered, input);

        return input;
    }
}

public record InputAnswer(string Response);
