using System.Diagnostics;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Services.AgentRunner;

public sealed class RunningProcess : IAsyncDisposable
{
    public PipelineStepRun StepRun { get; }
    public Repository Repository { get; }
    public Process Process { get; }
    public CancellationTokenSource Cts { get; }
    public LogBatcher LogBatcher { get; }
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public TaskCompletionSource ExitSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool WasCancelled { get; set; }

    public RunningProcess(
        PipelineStepRun stepRun,
        Repository repository,
        Process process,
        CancellationTokenSource cts,
        LogBatcher logBatcher)
    {
        StepRun = stepRun;
        Repository = repository;
        Process = process;
        Cts = cts;
        LogBatcher = logBatcher;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!Process.HasExited)
                Process.Kill(entireProcessTree: true);
        }
        catch { }
        try { Process.Dispose(); } catch { }
        try { Cts.Dispose(); } catch { }
        await LogBatcher.DisposeAsync();
    }
}
