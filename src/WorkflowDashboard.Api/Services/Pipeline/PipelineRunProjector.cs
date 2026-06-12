using System.Text.Json;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Services.Pipeline;

public sealed class PipelineRunProjector
{
    public PipelineRunDto Project(PipelineRun run, List<PipelineStepDef>? steps = null)
    {
        steps ??= ParseSteps(run.Pipeline?.StepsJson);

        var pendingApproval = run.ApprovalRequests
            .Where(a => a.Status == "pending")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        string? pendingApprovalStepName = null;
        if (pendingApproval is not null && steps is not null)
        {
            pendingApprovalStepName = steps.FirstOrDefault(s => s.Id == pendingApproval.StepId)?.Name;
        }

        return new PipelineRunDto(
            Id: run.Id,
            PipelineId: run.PipelineId,
            FeatureId: run.FeatureId,
            RepositoryId: run.RepositoryId,
            Status: run.Status,
            CurrentStepId: run.CurrentStepId,
            CreatedAt: run.CreatedAt,
            StartedAt: run.StartedAt,
            CompletedAt: run.CompletedAt,
            ErrorMessage: run.ErrorMessage,
            TicketNumber: run.TicketNumber,
            BranchPrefix: run.BranchPrefix,
            DefaultBranch: run.DefaultBranch,
            FeatureSlug: run.FeatureSlug,
            PipelineName: run.Pipeline?.Name,
            FeatureName: run.Feature?.Name,
            RepositoryPath: run.Repository?.Path,
            RepositoryName: run.Repository?.Name,
            StepRuns: run.StepRuns.Select(ToStepDto).ToList(),
            PendingApproval: pendingApproval is null ? null : ToApprovalDto(pendingApproval, pendingApprovalStepName)
        );
    }

    private static PipelineStepRunDto ToStepDto(PipelineStepRun s) => new(
        Id: s.Id,
        PipelineRunId: s.PipelineRunId,
        StepId: s.StepId,
        StepType: s.StepType,
        AgentSlug: s.AgentSlug,
        AttemptNumber: s.AttemptNumber,
        Status: s.Status,
        ProcessId: s.ProcessId,
        StartedAt: s.StartedAt,
        CompletedAt: s.CompletedAt,
        ErrorMessage: s.ErrorMessage
    );

    private static ApprovalRequestDto ToApprovalDto(ApprovalRequest a, string? stepName) => new(
        Id: a.Id,
        PipelineRunId: a.PipelineRunId,
        StepRunId: a.StepRunId,
        StepId: a.StepId,
        Status: a.Status,
        FeedbackText: a.FeedbackText,
        CreatedAt: a.CreatedAt,
        DecidedAt: a.DecidedAt,
        StepName: stepName
    );

    private static List<PipelineStepDef>? ParseSteps(string? stepsJson)
    {
        if (string.IsNullOrWhiteSpace(stepsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(stepsJson);
            if (doc.RootElement.TryGetProperty("steps", out var stepsEl))
            {
                return JsonSerializer.Deserialize<List<PipelineStepDef>>(
                    stepsEl.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
        catch
        {
        }

        return null;
    }
}
