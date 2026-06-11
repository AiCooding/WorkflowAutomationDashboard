using System.Text;
using Microsoft.Extensions.Options;
using WorkflowDashboard.Api.Models;
using WorkflowDashboard.Api.Services.Repositories;

namespace WorkflowDashboard.Api.Services.AgentRunner;

public sealed class InstructionsInjector
{
    private readonly AgentRunnerOptions _options;
    private readonly ILogger<InstructionsInjector> _logger;

    public InstructionsInjector(
        IOptions<AgentRunnerOptions> options,
        ILogger<InstructionsInjector> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string? ResolveTargetPath(Repository repo)
    {
        return RepositoryPathHelper.TryResolveInside(repo, _options.InstructionsRelativePath);
    }

    /// <summary>
    /// Compose header + body and write atomically to the instructions file.
    /// </summary>
    public string Inject(PipelineStepRun stepRun, PipelineRun run, Repository repo, string agentMarkdownBody, string inputFilePath)
    {
        var target = ResolveTargetPath(repo)
            ?? throw new InvalidOperationException(
                $"Instructions path '{_options.InstructionsRelativePath}' resolves outside repo '{repo.Path}'.");

        var dir = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var inputRelative = Path.GetRelativePath(repo.Path, inputFilePath).Replace('\\', '/');

        var sb = new StringBuilder();

        sb.AppendLine("## ⚡ Pipeline Step Active — Begin Immediately");
        sb.AppendLine();
        sb.AppendLine("This Copilot session was started by the **Workflow Automation Dashboard**.");
        sb.AppendLine();
        sb.AppendLine("**Do NOT wait for the user to type.** Your very first action when this");
        sb.AppendLine("session begins is:");
        sb.AppendLine();
        sb.AppendLine($"1. Read the file `{inputRelative}` in the repository root.");
        sb.AppendLine("2. Use its contents as the launch context for the step below.");
        sb.AppendLine("3. Follow the Agent Definition section and begin work immediately.");
        sb.AppendLine();

        sb.AppendLine("<!-- pipeline-context");
        sb.AppendLine($"pipeline_run_id: {run.Id}");
        sb.AppendLine($"step_run_id: {stepRun.Id}");
        sb.AppendLine($"step_id: {stepRun.StepId}");
        sb.AppendLine($"attempt_number: {stepRun.AttemptNumber}");
        sb.AppendLine($"feature_id: {run.FeatureId ?? "none"}");
        sb.AppendLine($"repository_path: {repo.Path}");
        sb.AppendLine($"input_file: {inputRelative}");
        sb.AppendLine("-->");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Agent Definition");
        sb.AppendLine();
        sb.Append(agentMarkdownBody);

        var tmp = target + ".tmp";
        File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tmp, target, overwrite: true);

        _logger.LogInformation("Injected agent instructions: {Path}", target);
        return target;
    }

    public void Cleanup(Repository repo)
    {
        CleanupForRepository(repo);
    }

    public void CleanupForRepository(Repository repo)
    {
        var target = ResolveTargetPath(repo);
        if (target is null) return;
        TryDelete(target);
        TryDelete(target + ".tmp");
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Removed injected instructions file: {Path}", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete injected instructions file {Path} (best-effort).", path);
        }
    }
}
