using System.Diagnostics;
using Microsoft.Extensions.Options;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Services.AgentRunner;

public interface IProcessLauncher
{
    Process Start(PipelineStepRun stepRun, PipelineRun run, Repository repo, string apiBaseUrl);
}

public sealed class ProcessLauncher : IProcessLauncher
{
    private readonly AgentRunnerOptions _options;

    public ProcessLauncher(IOptions<AgentRunnerOptions> options)
    {
        _options = options.Value;
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

        ProcessStartInfo psi = _options.InteractiveTerminal
            ? BuildInteractivePsi(repo, envVars)
            : BuildHeadlessPsi(repo, envVars);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();
        return process;
    }

    private ProcessStartInfo BuildInteractivePsi(Repository repo, Dictionary<string, string> envVars)
    {
        var sets = string.Join(" && ", envVars.Select(kv => $"set {kv.Key}={kv.Value}"));

        var copilotCmd = _options.CopilotExecutable;
        var extraArgs = string.Join(" ", _options.CopilotArgs.Select(a => $"\"{a}\""));

        var copilotParts = new List<string> { copilotCmd };
        if (!string.IsNullOrEmpty(extraArgs)) copilotParts.Add(extraArgs);
        if (!string.IsNullOrWhiteSpace(_options.InteractiveStartPrompt))
            copilotParts.Add($"-i \"{EscapeForCmd(_options.InteractiveStartPrompt)}\"");
        copilotParts.Add("--allow-all-tools");

        var fullCmd = string.Join(" ", copilotParts);
        var repoName = Path.GetFileName(repo.Path.TrimEnd(Path.DirectorySeparatorChar));

        var cmdBody = $"title Copilot - {repoName} && {sets} && cd /d \"{repo.Path}\" && {fullCmd}";

        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/k \"{cmdBody}\"",
            UseShellExecute = true,
            CreateNoWindow = false,
        };
    }

    private static string EscapeForCmd(string s) => s.Replace("\"", "\\\"");

    private ProcessStartInfo BuildHeadlessPsi(Repository repo, Dictionary<string, string> envVars)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _options.CopilotExecutable,
            WorkingDirectory = repo.Path,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
        };
        foreach (var arg in _options.CopilotArgs)
            psi.ArgumentList.Add(arg);
        foreach (var kv in envVars)
            psi.Environment[kv.Key] = kv.Value;
        return psi;
    }
}
