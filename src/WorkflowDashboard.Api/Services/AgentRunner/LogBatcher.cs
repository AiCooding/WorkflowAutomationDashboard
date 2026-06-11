using Microsoft.AspNetCore.SignalR;
using WorkflowDashboard.Api.Hubs;

namespace WorkflowDashboard.Api.Services.AgentRunner;

/// <summary>
/// Per-step-run log fan-out. Each captured line is:
///  - pushed to SignalR group steprun:{stepRunId} as StepLog,
///  - kept in a fixed-size in-memory tail buffer (last N lines).
/// </summary>
public sealed class LogBatcher : IAsyncDisposable
{
    private readonly string _stepRunId;
    private readonly AgentRunnerOptions _options;
    private readonly IHubContext<WorkflowHub> _hub;
    private readonly ILogger _logger;

    private readonly Queue<LogLine> _tail = new();
    private readonly object _tailLock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public LogBatcher(
        string stepRunId,
        AgentRunnerOptions options,
        IHubContext<WorkflowHub> hub,
        ILogger logger)
    {
        _stepRunId = stepRunId;
        _options = options;
        _hub = hub;
        _logger = logger;
        _loop = Task.CompletedTask;
    }

    public void Enqueue(LogStream stream, string line)
    {
        var entry = new LogLine(_stepRunId, stream, line, DateTime.UtcNow);

        lock (_tailLock)
        {
            _tail.Enqueue(entry);
            while (_tail.Count > _options.LogTailLines) _tail.Dequeue();
        }

        _ = _hub.Clients.Group($"steprun:{_stepRunId}").SendAsync(
            PipelineHubMethods.StepLog,
            new
            {
                stepRunId = entry.EntityId,
                stream = entry.StreamName,
                line = entry.Line,
                ts = entry.Ts,
            });
    }

    public IReadOnlyList<LogLine> GetTailSnapshot()
    {
        lock (_tailLock) return _tail.ToArray();
    }

    public IReadOnlyList<string> GetStderrTail(int n)
    {
        lock (_tailLock)
        {
            return _tail.Where(l => l.Stream == LogStream.Stderr)
                        .Reverse().Take(n).Reverse()
                        .Select(l => l.Line).ToArray();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        await _loop;
        GC.SuppressFinalize(this);
    }
}
