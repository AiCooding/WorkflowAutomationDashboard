using System.Text;
using Microsoft.Extensions.Options;
using WorkflowDashboard.Api.Models;
using WorkflowDashboard.Api.Services.Pipeline;
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
    /// Compose the full pipeline-integration header + agent body and write atomically.
    /// The header handles ALL pipeline plumbing so agent .md files only describe domain work.
    /// </summary>
    public string Inject(
        PipelineStepRun stepRun,
        PipelineRun run,
        Repository repo,
        PipelineStepDef stepDef,
        string agentMarkdownBody,
        string inputFilePath,
        string apiBaseUrl)
    {
        var target = ResolveTargetPath(repo)
            ?? throw new InvalidOperationException(
                $"Instructions path '{_options.InstructionsRelativePath}' resolves outside repo '{repo.Path}'.");

        var dir = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var inputRelative = Path.GetRelativePath(repo.Path, inputFilePath).Replace('\\', '/');
        var completeUrl = $"{apiBaseUrl}/api/pipeline-runs/{run.Id}/steps/{stepRun.Id}/complete";

        var sb = new StringBuilder();

        // ── Autostart directive ──────────────────────────────────────────────
        sb.AppendLine("## ⚡ Pipeline Step Active — Begin Immediately");
        sb.AppendLine();
        sb.AppendLine("This session was started by the **Workflow Automation Dashboard**.");
        sb.AppendLine("**Do NOT wait for user input.** Follow the steps below in order, starting now.");
        sb.AppendLine();

        // ── Context table ────────────────────────────────────────────────────
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Your Context");
        sb.AppendLine();
        sb.AppendLine("| Variable | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine($"| `PIPELINE_RUN_ID` | `{run.Id}` |");
        sb.AppendLine($"| `STEP_RUN_ID` | `{stepRun.Id}` |");
        sb.AppendLine($"| `STEP_ID` | `{stepRun.StepId}` |");
        sb.AppendLine($"| `ATTEMPT_NUMBER` | `{stepRun.AttemptNumber}` |");
        sb.AppendLine($"| `FEATURE_ID` | `{run.FeatureId ?? "none"}` |");
        sb.AppendLine($"| `REPOSITORY_PATH` | `{repo.Path}` |");
        sb.AppendLine($"| `WORKFLOW_DASHBOARD_API_URL` | `{apiBaseUrl}` |");
        sb.AppendLine();

        // ── Step 1: Read input ───────────────────────────────────────────────
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Step 1 — Read Your Input");
        sb.AppendLine();
        sb.AppendLine($"Read this file before doing anything else: `{inputRelative}`");
        sb.AppendLine();
        sb.AppendLine("It contains:");
        sb.AppendLine("- The feature you are working on (name, description, spec folder)");
        sb.AppendLine("- Output and notes written by previous pipeline steps");
        if (stepRun.AttemptNumber > 1)
            sb.AppendLine("- **Feedback directed at you** from the previous review (this is a retry — address all feedback points)");
        sb.AppendLine();

        // ── Retry callout ────────────────────────────────────────────────────
        if (stepRun.AttemptNumber > 1)
        {
            sb.AppendLine("> ⚠️ **Retry — Attempt " + stepRun.AttemptNumber + "**");
            sb.AppendLine("> A previous attempt of this step received feedback. Look for sections");
            sb.AppendLine($"> containing `[{stepRun.StepId}]` or `feedback` in `{inputRelative}` and");
            sb.AppendLine("> address every point before signalling completion.");
            sb.AppendLine();
        }

        // ── Step 2: Agent task (domain content) ──────────────────────────────
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Step 2 — Your Task");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(agentMarkdownBody))
            sb.Append(agentMarkdownBody);
        else
            sb.AppendLine("_(No agent definition found — use your best judgement based on the pipeline context.)_");
        sb.AppendLine();

        // ── Step 3: Completion ───────────────────────────────────────────────
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Step 3 — Signal Completion");
        sb.AppendLine();

        if (stepDef.CanGiveFeedback)
        {
            // Reviewer agent: must choose approved or feedback
            sb.AppendLine("You are a **reviewer** for this step. After reviewing the work, choose one of:");
            sb.AppendLine();
            sb.AppendLine("**✅ Approve** — work is good, pipeline advances:");
            sb.AppendLine("```powershell");
            sb.AppendLine($"$body = '{{\"decision\":\"approved\"}}' ");
            sb.AppendLine($"Invoke-RestMethod -Method Post -Uri \"{completeUrl}\" \\");
            sb.AppendLine("  -Body $body -ContentType 'application/json'");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("**🔁 Request changes** — work needs revision, sends it back:");
            sb.AppendLine("```powershell");
            sb.AppendLine("$feedback = \"Your specific, actionable feedback here\"");
            sb.AppendLine($"$body = \"{{\\\"decision\\\":\\\"feedback\\\",\\\"feedbackText\\\":\\\"$feedback\\\"}}\"");
            sb.AppendLine($"Invoke-RestMethod -Method Post -Uri \"{completeUrl}\" \\");
            sb.AppendLine("  -Body $body -ContentType 'application/json'");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("> Be specific in feedback — the receiving agent will read it directly from the shared input file.");
        }
        else
        {
            // Standard agent: just signal done
            sb.AppendLine("When your work is complete, call:");
            sb.AppendLine("```powershell");
            sb.AppendLine($"$body = '{{\"decision\":\"approved\"}}'");
            sb.AppendLine($"Invoke-RestMethod -Method Post -Uri \"{completeUrl}\" \\");
            sb.AppendLine("  -Body $body -ContentType 'application/json'");
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("Then exit the session.");
        sb.AppendLine();

        // ── Machine-readable context comment ─────────────────────────────────
        sb.AppendLine("<!-- pipeline-context");
        sb.AppendLine($"pipeline_run_id: {run.Id}");
        sb.AppendLine($"step_run_id: {stepRun.Id}");
        sb.AppendLine($"step_id: {stepRun.StepId}");
        sb.AppendLine($"attempt_number: {stepRun.AttemptNumber}");
        sb.AppendLine($"can_give_feedback: {stepDef.CanGiveFeedback.ToString().ToLower()}");
        sb.AppendLine($"return_to: {stepDef.ReturnTo ?? "none"}");
        sb.AppendLine($"feature_id: {run.FeatureId ?? "none"}");
        sb.AppendLine($"repository_path: {repo.Path}");
        sb.AppendLine($"input_file: {inputRelative}");
        sb.AppendLine($"api_base_url: {apiBaseUrl}");
        sb.AppendLine("-->");

        var tmp = target + ".tmp";
        File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tmp, target, overwrite: true);

        _logger.LogInformation("Injected pipeline instructions: {Path}", target);
        return target;
    }

    public void Cleanup(Repository repo) => CleanupForRepository(repo);

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
