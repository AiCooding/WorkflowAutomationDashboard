using System.Diagnostics;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Services.AgentRunner;

public interface IProcessLauncher
{
    Process Start(PipelineStepRun stepRun, PipelineRun run, Repository repo, string apiBaseUrl);
}

public sealed class ProcessLauncher : IProcessLauncher
{
    private readonly IAgentRunnerSettingsProvider _provider;

    public ProcessLauncher(IAgentRunnerSettingsProvider provider)
    {
        _provider = provider;
    }

    public Process Start(PipelineStepRun stepRun, PipelineRun run, Repository repo, string apiBaseUrl)
    {
        var envVars = new Dictionary<string, string>
        {
            ["WORKFLOW_DASHBOARD_API_URL"] = apiBaseUrl,
            ["PIPELINE_RUN_ID"] = run.Id,
            ["STEP_RUN_ID"] = stepRun.Id,
            ["STEP_ID"] = stepRun.StepId,
            ["ATTEMPT_NUMBER"] = stepRun.AttemptNumber.ToString(),
            ["REPOSITORY_PATH"] = repo.Path,
            ["FEATURE_ID"] = run.FeatureId ?? string.Empty,
        };

        var opts = _provider.GetEffective();

        ProcessStartInfo psi = opts.InteractiveTerminal
            ? BuildInteractivePsi(repo, envVars, opts)
            : BuildHeadlessPsi(repo, envVars, opts);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();
        return process;
    }

    private static ProcessStartInfo BuildInteractivePsi(
        Repository repo,
        Dictionary<string, string> envVars,
        AgentRunnerOptions opts)
    {
        var sets = string.Join(" && ", envVars.Select(kv => $"set {kv.Key}={kv.Value}"));

        var parts = new List<string> { opts.Executable };

        foreach (var arg in opts.ExtraArgs)
            parts.Add($"\"{arg}\"");

        if (!string.IsNullOrWhiteSpace(opts.InteractiveStartPrompt))
            parts.Add(BuildPromptFlag(opts.CliTool, opts.InteractiveStartPrompt));

        foreach (var flag in GetToolFlags(opts.CliTool))
            parts.Add(flag);

        var fullCmd = string.Join(" ", parts);
        var repoName = Path.GetFileName(repo.Path.TrimEnd(Path.DirectorySeparatorChar));
        var toolName = opts.CliTool.ToString();

        var cmdBody = $"title {toolName} - {repoName} && {sets} && cd /d \"{repo.Path}\" && {fullCmd}";

        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/k \"{cmdBody}\"",
            UseShellExecute = true,
            CreateNoWindow = false,
        };
    }

    private static string BuildPromptFlag(CliTool tool, string prompt) => tool switch
    {
        CliTool.Claude => $"-p \"{EscapeForCmd(prompt)}\"",
        _ => $"-i \"{EscapeForCmd(prompt)}\"",
    };

    private static IEnumerable<string> GetToolFlags(CliTool tool) => tool switch
    {
        CliTool.Copilot => ["--allow-all-tools"],
        _ => [],
    };

    private static string EscapeForCmd(string s) => s.Replace("\"", "\\\"");

    private static ProcessStartInfo BuildHeadlessPsi(
        Repository repo,
        Dictionary<string, string> envVars,
        AgentRunnerOptions opts)
    {
        var psi = new ProcessStartInfo
        {
            FileName = opts.Executable,
            WorkingDirectory = repo.Path,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
        };
        foreach (var arg in opts.ExtraArgs)
            psi.ArgumentList.Add(arg);
        foreach (var kv in envVars)
            psi.Environment[kv.Key] = kv.Value;
        return psi;
    }
}
