using System.Diagnostics;
using System.Text;
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
        if (OperatingSystem.IsWindows())
            return BuildInteractivePsiWindows(repo, envVars, opts);
        if (OperatingSystem.IsMacOS())
            return BuildInteractivePsiMacOs(repo, envVars, opts);
        return BuildInteractivePsiLinux(repo, envVars, opts);
    }

    private static ProcessStartInfo BuildInteractivePsiWindows(
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
        var title = BuildTitle(repo, opts);

        var cmdBody = $"title {title} && {sets} && cd /d \"{repo.Path}\" && {fullCmd}";

        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/k \"{cmdBody}\"",
            UseShellExecute = true,
            CreateNoWindow = false,
        };
    }

    private static ProcessStartInfo BuildInteractivePsiLinux(
        Repository repo,
        Dictionary<string, string> envVars,
        AgentRunnerOptions opts)
    {
        var terminal = FindLinuxTerminal()
            ?? throw new InvalidOperationException(
                "No supported terminal emulator found on PATH. Tried: gnome-terminal, konsole, " +
                "xfce4-terminal, xterm, x-terminal-emulator. Install one, or set " +
                "InteractiveTerminal=false in AgentRunner settings to use headless mode.");

        var innerCmd = BuildBashInnerCommand(repo, envVars, opts);
        var title = BuildTitle(repo, opts);

        var psi = new ProcessStartInfo
        {
            FileName = terminal,
            UseShellExecute = false,
            CreateNoWindow = false,
        };

        switch (Path.GetFileName(terminal))
        {
            case "gnome-terminal":
                psi.ArgumentList.Add($"--title={title}");
                psi.ArgumentList.Add("--");
                psi.ArgumentList.Add("bash");
                psi.ArgumentList.Add("-lc");
                psi.ArgumentList.Add(innerCmd);
                break;
            case "xterm":
                psi.ArgumentList.Add("-T");
                psi.ArgumentList.Add(title);
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add("bash");
                psi.ArgumentList.Add("-lc");
                psi.ArgumentList.Add(innerCmd);
                break;
            default: // konsole, xfce4-terminal, x-terminal-emulator — all accept `-e cmd args...`
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add("bash");
                psi.ArgumentList.Add("-lc");
                psi.ArgumentList.Add(innerCmd);
                break;
        }

        return psi;
    }

    private static ProcessStartInfo BuildInteractivePsiMacOs(
        Repository repo,
        Dictionary<string, string> envVars,
        AgentRunnerOptions opts)
    {
        var innerCmd = BuildBashInnerCommand(repo, envVars, opts);

        var psi = new ProcessStartInfo
        {
            FileName = "osascript",
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add(
            $"tell application \"Terminal\" to do script {AppleScriptQuote(innerCmd)}");
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add("tell application \"Terminal\" to activate");
        return psi;
    }

    private static string BuildBashInnerCommand(
        Repository repo,
        Dictionary<string, string> envVars,
        AgentRunnerOptions opts)
    {
        var sb = new StringBuilder();

        foreach (var kv in envVars)
            sb.Append("export ").Append(kv.Key).Append('=').Append(BashQuote(kv.Value)).Append("; ");

        sb.Append("cd ").Append(BashQuote(repo.Path)).Append("; ");

        // Set the terminal window title via the OSC 0 escape — works in VTE, konsole, xterm, Terminal.app.
        var title = BuildTitle(repo, opts);
        sb.Append("printf '\\033]0;%s\\007' ").Append(BashQuote(title)).Append("; ");

        sb.Append(BashQuote(opts.Executable));
        foreach (var arg in opts.ExtraArgs)
            sb.Append(' ').Append(BashQuote(arg));

        if (!string.IsNullOrWhiteSpace(opts.InteractiveStartPrompt))
        {
            var promptFlag = opts.CliTool == CliTool.Claude ? "-p" : "-i";
            sb.Append(' ').Append(promptFlag).Append(' ').Append(BashQuote(opts.InteractiveStartPrompt));
        }

        foreach (var flag in GetToolFlags(opts.CliTool))
            sb.Append(' ').Append(flag);

        // Keep the terminal window open after the agent exits (matches cmd.exe /k behavior).
        sb.Append("; exec bash");

        return sb.ToString();
    }

    private static string BashQuote(string s) =>
        "'" + s.Replace("'", "'\\''") + "'";

    private static string AppleScriptQuote(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string BuildTitle(Repository repo, AgentRunnerOptions opts)
    {
        var repoName = Path.GetFileName(repo.Path.TrimEnd(Path.DirectorySeparatorChar));
        return $"{opts.CliTool} - {repoName}";
    }

    private static string? FindLinuxTerminal()
    {
        string[] candidates =
            ["gnome-terminal", "konsole", "xfce4-terminal", "xterm", "x-terminal-emulator"];
        string[] dirs = ["/usr/bin", "/usr/local/bin", "/bin"];

        foreach (var name in candidates)
            foreach (var dir in dirs)
            {
                var full = Path.Combine(dir, name);
                if (File.Exists(full)) return full;
            }
        return null;
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
