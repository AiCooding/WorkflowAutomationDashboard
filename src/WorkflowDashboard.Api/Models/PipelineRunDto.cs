namespace WorkflowDashboard.Api.Models;

public record PipelineRunDto(
    string Id,
    string PipelineId,
    string? FeatureId,
    string RepositoryId,
    string Status,
    string CurrentStepId,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    string? PipelineName,
    string? FeatureName,
    string? RepositoryPath,
    string? RepositoryName,
    IReadOnlyList<PipelineStepRunDto> StepRuns,
    ApprovalRequestDto? PendingApproval
);

public record PipelineStepRunDto(
    string Id,
    string PipelineRunId,
    string StepId,
    string StepType,
    string? AgentSlug,
    int AttemptNumber,
    string Status,
    int? ProcessId,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage
);

public record ApprovalRequestDto(
    string Id,
    string PipelineRunId,
    string StepRunId,
    string StepId,
    string Status,
    string? FeedbackText,
    DateTime CreatedAt,
    DateTime? DecidedAt,
    string? StepName
);
