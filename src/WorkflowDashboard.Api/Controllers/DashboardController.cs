using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Services.Catalog;

namespace WorkflowDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ICatalogStore _catalog;

    public DashboardController(WorkflowDbContext db, ICatalogStore catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> GetSummary()
    {
        var summary = new DashboardSummary
        {
            RunningPipelineRuns = await _db.PipelineRuns.CountAsync(r => r.Status == "running"),
            WaitingApprovalRuns = await _db.PipelineRuns.CountAsync(r => r.Status == "waiting_approval"),
            CompletedPipelineRuns = await _db.PipelineRuns.CountAsync(r => r.Status == "completed"),
            FailedPipelineRuns = await _db.PipelineRuns.CountAsync(r => r.Status == "failed"),
            PendingApprovals = await _db.ApprovalRequests
                .CountAsync(a => a.Status == "pending" &&
                                 a.PipelineRun!.Status == "waiting_approval"),
            TotalFeatures = await _db.Features.CountAsync(),
            FeaturesInProgress = await _db.Features.CountAsync(f => f.Status == "in_progress"),
            TotalRepositories = await _db.Repositories.CountAsync(),
            TotalPipelines = await _db.Pipelines.CountAsync(),
        };

        var paths = await _db.Repositories.Select(r => r.Path).ToListAsync();
        var catalogCounts = _catalog.Counts();
        summary = summary with
        {
            BrokenRepositories = paths.Count(p => !Directory.Exists(p)),
            TotalCatalogEntries = catalogCounts.WorkflowCount + catalogCounts.AgentCount,
            BrokenCatalogEntries = catalogCounts.BrokenCount,
        };

        return summary;
    }
}

public record DashboardSummary
{
    public int RunningPipelineRuns { get; init; }
    public int WaitingApprovalRuns { get; init; }
    public int CompletedPipelineRuns { get; init; }
    public int FailedPipelineRuns { get; init; }
    public int PendingApprovals { get; init; }
    public int TotalFeatures { get; init; }
    public int FeaturesInProgress { get; init; }
    public int TotalRepositories { get; init; }
    public int TotalPipelines { get; init; }
    public int BrokenRepositories { get; init; }
    public int TotalCatalogEntries { get; init; }
    public int BrokenCatalogEntries { get; init; }
}
