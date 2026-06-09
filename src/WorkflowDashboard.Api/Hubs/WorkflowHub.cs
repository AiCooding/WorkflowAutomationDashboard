using Microsoft.AspNetCore.SignalR;

namespace WorkflowDashboard.Api.Hubs;

public class WorkflowHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}

public static class WorkflowHubMethods
{
    public const string WorkflowUpdated = "WorkflowUpdated";
    public const string AgentUpdated = "AgentUpdated";
    public const string InputRequested = "InputRequested";
    public const string InputAnswered = "InputAnswered";
    public const string CommandIssued = "CommandIssued";
    public const string EventLogged = "EventLogged";
}
