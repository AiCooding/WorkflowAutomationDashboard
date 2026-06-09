using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Data;
using WorkflowDashboard.Api.Hubs;

namespace WorkflowDashboard.Api.Services;

public class PollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<WorkflowHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PollingService> _logger;

    private DateTime _lastPollTime = DateTime.UtcNow;

    public PollingService(
        IServiceScopeFactory scopeFactory,
        IHubContext<WorkflowHub> hubContext,
        IConfiguration configuration,
        ILogger<PollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _configuration.GetValue<int>("Polling:IntervalSeconds", 3);
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        _logger.LogInformation("Polling service started with interval {Interval}s", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollForChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during polling cycle");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task PollForChanges()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

        var cutoff = _lastPollTime;
        _lastPollTime = DateTime.UtcNow;

        // Check for new events since last poll
        var newEvents = await db.Events
            .Where(e => e.CreatedAt > cutoff)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        foreach (var evt in newEvents)
        {
            await _hubContext.Clients.All.SendAsync(WorkflowHubMethods.EventLogged, evt);
        }

        // Check for workflow status changes
        var updatedWorkflows = await db.Workflows
            .Include(w => w.Agents)
            .Where(w => w.StartedAt > cutoff || w.CompletedAt > cutoff)
            .ToListAsync();

        foreach (var workflow in updatedWorkflows)
        {
            await _hubContext.Clients.All.SendAsync(WorkflowHubMethods.WorkflowUpdated, workflow);
        }

        // Check for pending input requests
        var newInputRequests = await db.InputRequests
            .Where(i => i.Status == "pending" && i.CreatedAt > cutoff)
            .ToListAsync();

        foreach (var input in newInputRequests)
        {
            await _hubContext.Clients.All.SendAsync(WorkflowHubMethods.InputRequested, input);
        }
    }
}
