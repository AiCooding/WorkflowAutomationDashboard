namespace WorkflowDashboard.Api.Services.AgentRunner;

public enum LogStream { Stdout, Stderr }

public sealed record LogLine(string EntityId, LogStream Stream, string Line, DateTime Ts)
{
    public string StreamName => Stream == LogStream.Stdout ? "stdout" : "stderr";
}
