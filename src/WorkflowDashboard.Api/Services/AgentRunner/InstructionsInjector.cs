using System.Text;
using WorkflowDashboard.Api.Models;
using WorkflowDashboard.Api.Services.Pipeline;
using WorkflowDashboard.Api.Services.Repositories;

namespace WorkflowDashboard.Api.Services.AgentRunner;

public sealed class InstructionsInjector
{
    private readonly IAgentRunnerSettingsProvider _provider;
    private readonly ILogger<InstructionsInjector> _logger;

    public InstructionsInjector(
        IAgentRunnerSettingsProvider provider,
        ILogger<InstructionsInjector> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public string? ResolveTargetPath(Repository repo)
    {
        return RepositoryPathHelper.TryResolveInside(repo, _provider.GetEffective().InstructionsRelativePath);
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
                $"Instructions path '{_provider.GetEffective().InstructionsRelativePath}' resolves outside repo '{repo.Path}'.");

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
        sb.AppendLine($"| `TICKET_NUMBER` | `{run.TicketNumber}` |");
        sb.AppendLine($"| `BRANCH_NAME` | `{run.BranchName}` |");
        sb.AppendLine($"| `FEATURE_SLUG` | `{run.FeatureSlug}` |");
        sb.AppendLine($"| `REPOSITORY_PATH` | `{repo.Path}` |");
        sb.AppendLine($"| `WORKFLOW_DASHBOARD_API_URL` | `{apiBaseUrl}` |");
        sb.AppendLine();

        // ── OpenSpec folder mandate ──────────────────────────────────────────
        sb.AppendLine("> ⚠️ **OpenSpec folder rule:** All artifacts you create MUST live under");
        sb.AppendLine($"> `openspec/changes/{run.FeatureSlug}/` — use EXACTLY `{run.FeatureSlug}` as the folder name.");
        sb.AppendLine("> Do NOT invent a folder name from the feature title or any other source.");
        sb.AppendLine();
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
            sb.AppendLine($"> ⚠️ **Retry — Attempt {stepRun.AttemptNumber}**");
            sb.AppendLine("> - You are **updating existing files** in `openspec/changes/{run.FeatureSlug}/` — do NOT create new folders or duplicate files.");
            sb.AppendLine("> - In `workflow-input.md` find the section containing `feedback` or your step ID — it points to a review file.");
            sb.AppendLine($">   Review file path pattern: `openspec/changes/{run.FeatureSlug}/reviews/...` — read it for the full findings.");
            sb.AppendLine("> - Address every finding before signalling completion.");
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

        // ── Git commit ────────────────────────────────────────────────────────
        if (!run.DisableGitCommit)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Before Signalling Completion — Commit Your Work");
            sb.AppendLine();
            sb.AppendLine("Before calling the completion API, commit all your changes:");
            sb.AppendLine("```powershell");
            sb.AppendLine("git add -A");
            sb.AppendLine($"git commit -m \"{run.TicketNumber} {stepDef.Type}: {stepDef.Name}\"");
            sb.AppendLine("# Adjust the commit message type and description as appropriate.");
            sb.AppendLine("# Example: TEST-42 feat(proposal): PM draft complete");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("> If there is nothing to commit (no file changes), skip the commit step.");
            sb.AppendLine();
        }

        // ── Step 3: Completion ───────────────────────────────────────────────
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Step 3 — Signal Completion");
        sb.AppendLine();

        if (stepDef.CanGiveFeedback)
        {
            var reviewFilePath = $"openspec/changes/{run.FeatureSlug}/reviews/{stepRun.StepId}-review.md";

            sb.AppendLine("You are a **reviewer** for this step. After reviewing the work:");
            sb.AppendLine();
            sb.AppendLine($"**1. Write your detailed review** to: `{reviewFilePath}`");
            sb.AppendLine();
            sb.AppendLine("Use this structure:");
            sb.AppendLine("```markdown");
            sb.AppendLine($"# Review — {stepDef.Name}");
            sb.AppendLine();
            sb.AppendLine("## Verdict");
            sb.AppendLine("`approved` | `changes-requested`");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine("Brief explanation of the verdict.");
            sb.AppendLine();
            sb.AppendLine("## Findings");
            sb.AppendLine("| Severity | Finding | File/Location | Recommended Fix |");
            sb.AppendLine("|----------|---------|---------------|-----------------|");
            sb.AppendLine("| high/medium/low | ... | ... | ... |");
            sb.AppendLine();
            sb.AppendLine("## Strengths");
            sb.AppendLine("- ...");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("**2. Then call the completion API:**");
            sb.AppendLine();
            sb.AppendLine("**✅ Approve** — pipeline advances:");
            sb.AppendLine("```powershell");
            sb.AppendLine($"$body = '{{\"decision\":\"approved\"}}'");
            sb.AppendLine($"Invoke-RestMethod -Method Post -Uri \"{completeUrl}\" -Body $body -ContentType 'application/json'");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("**🔁 Request changes** — sends back with review file path:");
            sb.AppendLine("```powershell");
            sb.AppendLine($"$feedback = \"Review findings: {reviewFilePath}\"");
            sb.AppendLine("$body = \"{`\"decision`\":`\"feedback`\",`\"feedbackText`\":`\"$feedback`\"}\"");
            sb.AppendLine($"Invoke-RestMethod -Method Post -Uri \"{completeUrl}\" -Body $body -ContentType 'application/json'");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("> The receiving agent will look for this path in `workflow-input.md` on its next attempt and read the full review file.");
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
