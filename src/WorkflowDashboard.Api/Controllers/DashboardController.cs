using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly WorkflowDbContext _db;

    public DashboardController(WorkflowDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> GetSummary()
    {
        var summary = new DashboardSummary
        {
            RunningWorkflows = await _db.Workflows.CountAsync(w => w.Status == "running"),
            PausedWorkflows = await _db.Workflows.CountAsync(w => w.Status == "paused"),
            WaitingInputWorkflows = await _db.Workflows.CountAsync(w => w.Status == "waiting_input"),
            CompletedWorkflows = await _db.Workflows.CountAsync(w => w.Status == "completed"),
            FailedWorkflows = await _db.Workflows.CountAsync(w => w.Status == "failed"),
            ActiveAgents = await _db.Agents.CountAsync(a => a.Status == "running"),
            PendingInputRequests = await _db.InputRequests.CountAsync(i => i.Status == "pending"),
            TotalFeatures = await _db.Features.CountAsync(),
            FeaturesInProgress = await _db.Features.CountAsync(f => f.Status == "in_progress")
        };

        return summary;
    }
}

public record DashboardSummary
{
    public int RunningWorkflows { get; init; }
    public int PausedWorkflows { get; init; }
    public int WaitingInputWorkflows { get; init; }
    public int CompletedWorkflows { get; init; }
    public int FailedWorkflows { get; init; }
    public int ActiveAgents { get; init; }
    public int PendingInputRequests { get; init; }
    public int TotalFeatures { get; init; }
    public int FeaturesInProgress { get; init; }
}
