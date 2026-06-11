# Tasks: Pipeline Orchestration Engine

> 🛠️ Authored by: Architect persona  
> 📅 Date: 2025-01-27

---

## Phase 1 — Data Model

### 1. Remove old model files

- [ ] 1.1 Delete `src/WorkflowDashboard.Api/Models/Workflow.cs`
- [ ] 1.2 Delete `src/WorkflowDashboard.Api/Models/WorkflowEvent.cs`
- [ ] 1.3 Delete `src/WorkflowDashboard.Api/Models/Agent.cs`
- [ ] 1.4 Delete `src/WorkflowDashboard.Api/Models/InputRequest.cs`
- [ ] 1.5 Delete `src/WorkflowDashboard.Api/Models/WorkflowDto.cs`
- [ ] 1.6 Remove `ICollection<Workflow> Workflows` navigation property from `Feature.cs`

### 2. Create new model files

- [ ] 2.1 Create `Models/Pipeline.cs`

  ```csharp
  namespace WorkflowDashboard.Api.Models;

  public class Pipeline
  {
      public string Id { get; set; } = string.Empty;
      public string Name { get; set; } = string.Empty;
      public string? Description { get; set; }
      public string StepsJson { get; set; } = "[]";   // JSON: PipelineStepDefinition[]
      public string? RepositoryId { get; set; }       // optional default repo
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
      public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

      public Repository? Repository { get; set; }
      public ICollection<PipelineRun> Runs { get; set; } = new List<PipelineRun>();
  }
  ```

- [ ] 2.2 Create `Models/PipelineRun.cs`

  ```csharp
  namespace WorkflowDashboard.Api.Models;

  /// <summary>Status values: pending|running|waiting_approval|completed|failed|cancelled</summary>
  public class PipelineRun
  {
      public string Id { get; set; } = string.Empty;
      public string PipelineId { get; set; } = string.Empty;
      public string? FeatureId { get; set; }
      public string RepositoryId { get; set; } = string.Empty;
      public string Status { get; set; } = "pending";
      public string? CurrentStepId { get; set; }
      public string? Slug { get; set; }               // used for {slug} token in reviewFiles
      public string? InitialDescription { get; set; }
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
      public DateTime? StartedAt { get; set; }
      public DateTime? CompletedAt { get; set; }
      public string? ErrorMessage { get; set; }

      public Pipeline Pipeline { get; set; } = null!;
      public Feature? Feature { get; set; }
      public Repository Repository { get; set; } = null!;
      public ICollection<PipelineStepRun> StepRuns { get; set; } = new List<PipelineStepRun>();
      public ICollection<ApprovalRequest> ApprovalRequests { get; set; } = new List<ApprovalRequest>();
  }
  ```

- [ ] 2.3 Create `Models/PipelineStepRun.cs`

  ```csharp
  namespace WorkflowDashboard.Api.Models;

  /// <summary>Status values: pending|running|waiting_approval|completed|failed|cancelled|returned</summary>
  public class PipelineStepRun
  {
      public string Id { get; set; } = string.Empty;
      public string PipelineRunId { get; set; } = string.Empty;
      public string StepId { get; set; } = string.Empty;     // matches PipelineStepDefinition.Id
      public string StepType { get; set; } = string.Empty;   // "agent" | "userApproval"
      public string StepName { get; set; } = string.Empty;
      public int AttemptNumber { get; set; } = 1;
      public string Status { get; set; } = "pending";
      public int? ProcessId { get; set; }
      public string? AgentSlug { get; set; }
      public bool? IsInteractive { get; set; }
      public string? OutputSummary { get; set; }
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
      public DateTime? StartedAt { get; set; }
      public DateTime? CompletedAt { get; set; }
      public string? ErrorMessage { get; set; }

      public PipelineRun PipelineRun { get; set; } = null!;
  }
  ```

- [ ] 2.4 Create `Models/ApprovalRequest.cs`

  ```csharp
  namespace WorkflowDashboard.Api.Models;

  /// <summary>Status values: pending|approved|feedback_sent</summary>
  public class ApprovalRequest
  {
      public string Id { get; set; } = string.Empty;
      public string PipelineRunId { get; set; } = string.Empty;
      public string StepRunId { get; set; } = string.Empty;
      public string StepId { get; set; } = string.Empty;
      /// <summary>JSON string[] — {slug} tokens already resolved by orchestrator.</summary>
      public string ReviewFilesJson { get; set; } = "[]";
      public string Status { get; set; } = "pending";
      public string? FeedbackText { get; set; }
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
      public DateTime? DecidedAt { get; set; }

      public PipelineRun PipelineRun { get; set; } = null!;
      public PipelineStepRun StepRun { get; set; } = null!;
  }
  ```

- [ ] 2.5 Create `Models/PipelineRunLog.cs`

  ```csharp
  namespace WorkflowDashboard.Api.Models;

  /// <summary>EventType: state_change|log|error|step_completed|approval_created|approval_decided</summary>
  public class PipelineRunLog
  {
      public int Id { get; set; }
      public string PipelineRunId { get; set; } = string.Empty;
      public string? StepRunId { get; set; }
      public string EventType { get; set; } = string.Empty;
      public string? Message { get; set; }
      public string? MetadataJson { get; set; }
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  }
  ```

- [ ] 2.6 Create `Models/PipelineStepDefinition.cs`

  ```csharp
  namespace WorkflowDashboard.Api.Models;

  /// <summary>
  /// Deserialised from Pipeline.StepsJson. Not an EF entity.
  /// </summary>
  public sealed record PipelineStepDefinition(
      string Id,
      string Type,           // "agent" | "userApproval"
      string Name,
      string? AgentSlug,
      bool? Interactive,
      List<string>? ReviewFiles,
      FeedbackAction? OnFeedback,
      string? OnApprove);    // "continue" | step-id

  public sealed record FeedbackAction(string ReturnTo);
  ```

- [ ] 2.7 Create `Models/PipelineRunDto.cs`

  ```csharp
  namespace WorkflowDashboard.Api.Models;

  public sealed record PipelineRunDto(
      string Id,
      string PipelineId,
      string PipelineName,
      string? FeatureId,
      string? FeatureName,
      string RepositoryId,
      string? RepositoryPath,
      string? RepositoryName,
      string Status,
      string? CurrentStepId,
      string? Slug,
      string? InitialDescription,
      DateTime CreatedAt,
      DateTime? StartedAt,
      DateTime? CompletedAt,
      string? ErrorMessage,
      List<PipelineStepRunDto> StepRuns,
      List<ApprovalRequestDto> PendingApprovals);

  public sealed record PipelineStepRunDto(
      string Id,
      string PipelineRunId,
      string StepId,
      string StepType,
      string StepName,
      int AttemptNumber,
      string Status,
      int? ProcessId,
      string? AgentSlug,
      bool? IsInteractive,
      string? OutputSummary,
      DateTime CreatedAt,
      DateTime? StartedAt,
      DateTime? CompletedAt,
      string? ErrorMessage);

  public sealed record ApprovalRequestDto(
      string Id,
      string PipelineRunId,
      string StepRunId,
      string StepId,
      string[] ReviewFiles,     // already resolved, deserialized from JSON
      string Status,
      string? FeedbackText,
      DateTime CreatedAt,
      DateTime? DecidedAt);
  ```

### 3. Update `WorkflowDbContext`

- [ ] 3.1 Remove `DbSet<Workflow>`, `DbSet<Agent>`, `DbSet<InputRequest>`, `DbSet<WorkflowEvent>`
- [ ] 3.2 Add `DbSet<Pipeline>`, `DbSet<PipelineRun>`, `DbSet<PipelineStepRun>`,
  `DbSet<ApprovalRequest>`, `DbSet<PipelineRunLog>`
- [ ] 3.3 In `OnModelCreating`, remove all old entity configurations (Workflow, Agent,
  InputRequest, WorkflowEvent)
- [ ] 3.4 Add EF configurations for all new entities:

  **`Pipeline`**: PK on `Id`; `StepsJson` required; optional FK to `Repository` with `SetNull`;
  index on `RepositoryId`.

  **`PipelineRun`**: PK on `Id`; required FK to `Pipeline` with `Restrict`; nullable FK to
  `Feature` with `SetNull`; required FK to `Repository` with `Restrict`; index on `PipelineId`,
  `Status`, `RepositoryId`.

  **`PipelineStepRun`**: PK on `Id`; required FK to `PipelineRun` with `Cascade`; index on
  `PipelineRunId`, `Status`.

  **`ApprovalRequest`**: PK on `Id`; required FK to `PipelineRun` with `Cascade`; required FK
  to `PipelineStepRun` with `Cascade`; index on `PipelineRunId`, `Status`.

  **`PipelineRunLog`**: PK on `Id` (auto-increment); no FK (intentional — logs survive run
  deletion for audit); composite index on `(PipelineRunId, CreatedAt)`.

- [ ] 3.5 Update `Feature` entity config: remove `HasMany(f => f.Workflows)` configuration

### 4. Create and verify EF Core migration

- [ ] 4.1 Run `dotnet ef migrations add PipelineSchema --project src/WorkflowDashboard.Api`
- [ ] 4.2 Open the generated migration file and manually add `DROP TABLE` statements at the
  **start** of `Up()` (before any `CREATE TABLE`), in this dependency order:
  1. `migrationBuilder.Sql("DROP TABLE IF EXISTS InputRequests;");`
  2. `migrationBuilder.Sql("DROP TABLE IF EXISTS Agents;");`
  3. `migrationBuilder.Sql("DROP TABLE IF EXISTS WorkflowEvents;");`
  4. `migrationBuilder.Sql("DROP TABLE IF EXISTS Workflows;");`
- [ ] 4.3 Delete the existing `workflow.db` file (clean break — no data preservation)
- [ ] 4.4 Run `dotnet ef database update` and verify it succeeds
- [ ] 4.5 Build the solution — fix all compile errors caused by removal of old types
  (controllers, services that reference old models will fail; that is expected — addressed
  in later tasks)

---

## Phase 2 — Execution Engine

### 5. Update `ProcessLauncher`

- [ ] 5.1 Create `Services/Pipeline/PipelineStepContext.cs`:

  ```csharp
  namespace WorkflowDashboard.Api.Services.Pipeline;

  public sealed record PipelineStepContext(
      string PipelineRunId,
      string StepRunId,
      string StepId,
      int AttemptNumber,
      string? FeatureId,
      string RepositoryId,
      bool IsInteractive);
  ```

- [ ] 5.2 Update `IProcessLauncher` interface:
  - Change `Process Start(Workflow workflow, Repository repo, string apiBaseUrl)` →
    `Process Start(PipelineStepContext ctx, Repository repo, string apiBaseUrl)`

- [ ] 5.3 Update `ProcessLauncher.Start(...)` to accept `PipelineStepContext ctx`:
  - Env vars: replace `WORKFLOW_ID` / `FEATURE_ID` with `PIPELINE_RUN_ID`, `STEP_RUN_ID`,
    `STEP_ID`, `ATTEMPT_NUMBER`, `FEATURE_ID`. Keep `REPOSITORY_PATH` and
    `WORKFLOW_DASHBOARD_API_URL`.
  - `IsInteractive` check: use `ctx.IsInteractive` instead of `_options.InteractiveTerminal`.
    (Per-step interactivity overrides global option.)
  - Retain `BuildInteractivePsi` / `BuildHeadlessPsi` internal methods unchanged.

- [ ] 5.4 Update `RunningProcess.cs`: replace `Workflow` field with `PipelineStepContext`.

### 6. Update `InstructionsInjector`

- [ ] 6.1 Change `Inject(Workflow, Repository, string, string)` signature to
  `Inject(PipelineStepContext ctx, Repository repo, string agentMarkdownBody, string inputFilePath)`
- [ ] 6.2 Update the context comment block to use `pipeline_run_id`, `step_run_id`, `step_id`,
  `attempt_number` instead of `workflow_id` / `feature_id`
- [ ] 6.3 Update the autostart directive text to reference pipeline context
- [ ] 6.4 Keep `Cleanup(Repository)` and `CleanupForRepository(Repository)` unchanged

### 7. Update `WorkflowInputWriter`

- [ ] 7.1 Add `WritePipelineHeader(PipelineRun run, Repository repo, Pipeline pipeline, string apiBaseUrl)`:
  - Creates `.github/copilot/workflow-input.md` (atomic write via temp-rename, same pattern as
    existing `Write` method)
  - Writes the run-level header block (Run ID, Pipeline, Repository, Feature, API Base URL,
    Started timestamp)
  - Appends `## [initial-request] — by: user — for: {steps[0].Id}` section with
    `run.InitialDescription` as body

- [ ] 7.2 Add `AppendStepSection(Repository repo, string header, string? contentBody = null)`:
  - Opens the existing file and appends `\n---\n\n{header}\n` + optional content body
  - Uses a file-level lock (`SemaphoreSlim`, one per repository path in a static
    `ConcurrentDictionary<string, SemaphoreSlim>`) to prevent concurrent appends from two
    steps running in parallel (future-proofing)
  - Uses temp-file-then-rename for crash safety (read existing + append + write to .tmp + rename)

- [ ] 7.3 Keep existing `Write(Workflow, ...)`, `Cleanup(Repository)` methods for the transition
  period; mark them `[Obsolete]`

### 8. Delete old AgentRunner service files

- [ ] 8.1 Delete `Services/AgentRunner/AgentRunner.cs`
- [ ] 8.2 Delete `Services/AgentRunner/IAgentRunner.cs`
- [ ] 8.3 Delete `Services/WorkflowProjector.cs`

### 9. Create `PipelineOrchestrator` service

- [ ] 9.1 Create `Services/Pipeline/OrchestratorMessage.cs`:

  ```csharp
  namespace WorkflowDashboard.Api.Services.Pipeline;

  public abstract record OrchestratorMessage;
  public sealed record StartRun(string RunId) : OrchestratorMessage;
  public sealed record StepCompleted(string RunId, string StepRunId, string? Summary) : OrchestratorMessage;
  public sealed record ApprovalDecided(
      string RunId,
      string ApprovalId,
      string Decision,       // "approved" | "feedback"
      string? FeedbackText) : OrchestratorMessage;
  ```

- [ ] 9.2 Create `Services/Pipeline/IPipelineOrchestrator.cs`:

  ```csharp
  namespace WorkflowDashboard.Api.Services.Pipeline;

  public interface IPipelineOrchestrator
  {
      void EnqueueStartRun(string runId);
      void EnqueueStepCompleted(string runId, string stepRunId, string? summary);
      void EnqueueApprovalDecided(string runId, string approvalId, string decision, string? feedbackText);
      IReadOnlyList<LogLine> GetStepLogTail(string stepRunId);
      Task CancelRunAsync(string runId, CancellationToken ct = default);
  }
  ```

- [ ] 9.3 Create `Services/Pipeline/PipelineOrchestrator.cs` — full implementation:

  **Class skeleton:**
  ```
  public sealed class PipelineOrchestrator : BackgroundService, IPipelineOrchestrator
  ```

  **Fields:**
  - `Channel<OrchestratorMessage> _channel` (UnboundedChannel, SingleReader)
  - `ConcurrentDictionary<string, RunningProcess> _running` (keyed by stepRunId)
  - `SemaphoreSlim _claimLock` (1,1)
  - Dependencies: `IServiceScopeFactory`, `IHubContext<WorkflowHub>`, `IProcessLauncher`,
    `InstructionsInjector`, `WorkflowInputWriter`, `ICatalogStore`, `IConfiguration`,
    `AgentRunnerOptions`, `ILogger`, `ILoggerFactory`

  **`EnqueueStartRun`**: `_channel.Writer.TryWrite(new StartRun(runId))`

  **`EnqueueStepCompleted`**: `_channel.Writer.TryWrite(new StepCompleted(...))`

  **`EnqueueApprovalDecided`**: `_channel.Writer.TryWrite(new ApprovalDecided(...))`

  **`ExecuteAsync`** (background loop):
  ```
  await foreach (var msg in _channel.Reader.ReadAllAsync(ct))
      await (msg switch {
          StartRun m      => HandleStartRunAsync(m.RunId, ct),
          StepCompleted m => HandleStepCompletedAsync(m.RunId, m.StepRunId, m.Summary, ct),
          ApprovalDecided m => HandleApprovalDecidedAsync(m, ct),
          _ => Task.CompletedTask
      });
  ```

  **`HandleStartRunAsync(runId, ct)`**:
  1. Load `PipelineRun` (include `Pipeline`, `Repository`, `Feature`) from DB
  2. Validate: run must exist, status must be `pending`, repo path must exist
  3. Parse `pipeline.StepsJson` → `List<PipelineStepDefinition>` via `JsonSerializer`
  4. If steps is empty: fail run with "Pipeline has no steps"
  5. Set `run.Status = "running"`, `run.StartedAt = now`
  6. Call `_inputWriter.WritePipelineHeader(run, repo, pipeline, apiBaseUrl)`
  7. Save + hub broadcast `PipelineRunUpdated`
  8. Call `AdvanceToStepAsync(run, steps, 0, ct)` (index 0)

  **`AdvanceToStepAsync(run, steps, index, ct)`**:
  1. If `index >= steps.Count`: mark run completed, save, broadcast, return
  2. `stepDef = steps[index]`
  3. Compute `attemptNumber` = (existing PipelineStepRuns for same stepId in this run) + 1
  4. Insert `PipelineStepRun` with `Status = "pending"`, `AttemptNumber`, etc.
  5. Update `run.CurrentStepId = stepDef.Id`; save
  6. If `stepDef.Type == "agent"`: call `LaunchAgentStepAsync(run, stepDef, stepRun, steps, index, ct)`
  7. If `stepDef.Type == "userApproval"`: call `CreateApprovalRequestAsync(run, stepDef, stepRun, ct)`

  **`LaunchAgentStepAsync(run, stepDef, stepRun, steps, index, ct)`**:
  1. Validate `agentSlug` is set; resolve catalog entry (`kind="agent"`)
  2. Read agent markdown body from `catalogEntry.SourcePath`
  3. Determine `nextStepIds` (for `for:` field in header): next step id(s) considering `onApprove`
  4. Build header: `## [{stepDef.Id}] attempt {stepRun.AttemptNumber} — by: {agentSlug} — for: {nextStepIds}`
  5. Call `_inputWriter.AppendStepSection(repo, header, contentBody: null)`
  6. Build `PipelineStepContext ctx`; call `_injector.Inject(ctx, repo, agentBody, inputFilePath)`
  7. Spawn process via `_launcher.Start(ctx, repo, apiBaseUrl)`
  8. Set `stepRun.Status = "running"`, `stepRun.ProcessId = pid`, `stepRun.StartedAt = now`
  9. Create and register `LogBatcher(stepRunId, ...)` in `_running`
  10. Wire stdout/stderr capture (headless only); begin async read
  11. Persist PID; save; hub broadcast `PipelineStepRunUpdated` + `PipelineRunUpdated`
  12. Fire-and-forget `Task.Run(WaitForProcessExitAsync(run, stepRun, stepDef, steps, index, rp, ct))`

  **`WaitForProcessExitAsync(...)`**:
  1. `await process.WaitForExitAsync(CancellationToken.None)`
  2. Determine final status from exit code and `IsInteractive` flag (same logic as existing
     `AgentRunner.RunProcessAsync`)
  3. If process exited and status not cancelled: treat as implicit completion (interactive steps
     don't always call the API endpoint; process exit = completion signal)
  4. Mark `stepRun.Status = completed/failed/cancelled`; `stepRun.CompletedAt = now`
  5. Cleanup injected instructions file
  6. If status is `completed`: call `AdvanceToNextStepAsync(run, steps, index, stepDef, ct)`
  7. If `failed`: mark run as failed; broadcast
  8. Remove from `_running`; dispose `RunningProcess`

  **`AdvanceToNextStepAsync(run, steps, currentIndex, currentStepDef, ct)`**:
  1. Determine next step index using `currentStepDef.OnApprove`:
     - `null` or `"continue"` → `currentIndex + 1`
     - specific step-id → find that step's index in `steps`
  2. Call `AdvanceToStepAsync(run, steps, nextIndex, ct)`

  **`CreateApprovalRequestAsync(run, stepDef, stepRun, ct)`**:
  1. Resolve `reviewFiles`: replace `{slug}` with `run.Slug` in each path
  2. Insert `ApprovalRequest{Status="pending", ReviewFilesJson=json, ...}`
  3. Set `stepRun.Status = "waiting_approval"`, `run.Status = "waiting_approval"`
  4. Build header: `## [{stepDef.Id}] — by: user — decision: pending — for: {nextStepIds}`
  5. Call `_inputWriter.AppendStepSection(repo, header, contentBody: null)`
  6. Save; hub broadcast `ApprovalRequested` + `PipelineStepRunUpdated` + `PipelineRunUpdated`

  **`HandleStepCompletedAsync(runId, stepRunId, summary, ct)`**:
  1. Load run (with pipeline, step runs); load stepRun
  2. If stepRun.Status == "running": set to "completed"; set `OutputSummary = summary`
  3. Parse steps; find current index
  4. Cleanup process if still in `_running` (for headless steps where we already have exit)
  5. Call `AdvanceToNextStepAsync(run, steps, currentIndex, stepDef, ct)`

  **`HandleApprovalDecidedAsync(msg, ct)`**:
  1. Load `ApprovalRequest` + `PipelineRun` + steps
  2. Set `approval.Status` = `approved` or `feedback_sent`; `approval.DecidedAt = now`
  3. Set `stepRun.Status = completed` (approved) or `returned` (feedback)
  4. Append section to `workflow-input.md`:
     - approved: `## [{stepId}] — by: user — decision: approved — for: {nextStep.Id}`
     - feedback: `## [{stepId}] — by: user — decision: feedback — for: {returnTo}\n{feedbackText}`
  5. Save; broadcast `ApprovalDecided` + `PipelineStepRunUpdated`
  6. If approved: `AdvanceToNextStepAsync` using `stepDef.OnApprove`
  7. If feedback: `AdvanceToStepAsync(run, steps, returnToIndex, ct)` (new attempt)

  **`CancelRunAsync(runId, ct)`**:
  1. Find all running processes for this run in `_running`
  2. Kill process trees; mark step runs as cancelled
  3. Mark `run.Status = cancelled`; `run.CompletedAt = now`
  4. Save; broadcast
  5. Cleanup instructions file and workflow-input.md (best-effort)

### 10. Update `StartupReconciler`

- [ ] 10.1 Change orphan query from `Workflow` to `PipelineStepRun WHERE Status='running'`
- [ ] 10.2 For each orphaned step run: set status to `failed`; set parent `PipelineRun` to
  `failed` if no completed step runs exist after it
- [ ] 10.3 Remove `IAgentRunner` dependency; inject `IPipelineOrchestrator` instead (for
  rebuild-queue step)
- [ ] 10.4 Remove the "rebuild queue" step (pipeline runs are event-driven, not re-queued on
  startup; orphaned `pending` runs can be retried by the user)

### 11. Create `PipelineRunProjector` service

- [ ] 11.1 Create `Services/Pipeline/PipelineRunProjector.cs` (scoped service):
  - `Project(IEnumerable<PipelineRun>)` → `IReadOnlyList<PipelineRunDto>` — loads step runs
    and approvals, resolves pipeline/feature/repo names
  - `ProjectOne(PipelineRun)` → `PipelineRunDto`
  - `ToApprovalDto(ApprovalRequest)` → `ApprovalRequestDto` — deserialises `ReviewFilesJson`

### 12. Create API controllers

- [ ] 12.1 Create `Controllers/PipelinesController.cs`:

  Routes: `GET /api/pipelines`, `GET /api/pipelines/{id}`, `POST /api/pipelines`,
  `PUT /api/pipelines/{id}`, `DELETE /api/pipelines/{id}`.

  - `GET /{id}`: include parsed steps in response (deserialise `StepsJson`)
  - `POST /`: validate `stepsJson` is valid JSON before inserting; return 400 on invalid JSON
  - `DELETE /{id}`: return 409 if any `PipelineRun` for this pipeline has
    `Status IN (pending, running, waiting_approval)`
  - On create/update: set `UpdatedAt = now`

- [ ] 12.2 Create `Controllers/PipelineRunsController.cs`:

  **`POST /api/pipeline-runs`** (start a run):
  - Validate `pipelineId`, `repositoryId`; check repo exists on disk
  - Insert `PipelineRun{Status="pending", ...}`; insert initial `PipelineRunLog`
  - Call `_orchestrator.EnqueueStartRun(runId)`
  - Return 202 with `PipelineRunDto`

  **`GET /api/pipeline-runs`**:
  - Query filters: `?pipelineId=`, `?repositoryId=`, `?status=`, `?featureId=`
  - Include `StepRuns` and `PendingApprovals`; project via `PipelineRunProjector`
  - Order by `CreatedAt DESC`; default limit 50

  **`GET /api/pipeline-runs/{runId}`**:
  - Include `Pipeline`, `Feature`, `Repository`, `StepRuns`, `ApprovalRequests`
  - Return `PipelineRunDto`

  **`POST /api/pipeline-runs/{runId}/cancel`**:
  - Validate run exists and is cancellable
  - Call `_orchestrator.CancelRunAsync(runId)`
  - Return 204

  **`POST /api/pipeline-runs/{runId}/steps/{stepId}/complete`**:
  - Body: `{ summary?: string, outputSummary?: string }`
  - Find the latest `PipelineStepRun` for this stepId in this run with `Status=running`
  - Call `_orchestrator.EnqueueStepCompleted(runId, stepRunId, summary)`
  - Return 202

  **`POST /api/pipeline-runs/{runId}/steps/{stepId}/log`**:
  - Body: `{ stream: "stdout" | "stderr", line: string }`
  - Find the running step run; push log line via `_hub` to group `step-run:{stepRunId}`
  - Insert `PipelineRunLog{EventType="log", ...}` (batched — use same LogBatcher pattern or
    inline insert with a lightweight write-behind queue)
  - Return 200

  **`POST /api/pipeline-runs/{runId}/approvals/{approvalId}/decide`**:
  - Body: `{ decision: "approved" | "feedback", feedbackText?: string }`
  - Validate approval exists, is `pending`, belongs to this run
  - Call `_orchestrator.EnqueueApprovalDecided(runId, approvalId, decision, feedbackText)`
  - Return 202

- [ ] 12.3 Delete `Controllers/WorkflowsController.cs`
- [ ] 12.4 Delete `Controllers/InputRequestsController.cs`
- [ ] 12.5 Update `Controllers/AgentsController.cs`:
  - Change query to: `PipelineStepRun WHERE StepType='agent' AND Status='running'`
  - Return `List<PipelineStepRunDto>`
- [ ] 12.6 Update `Controllers/EventsController.cs`:
  - Query `PipelineRunLog` instead of `WorkflowEvent`
  - Filter by `?runId=`, `?stepRunId=`
- [ ] 12.7 Update `Controllers/DashboardController.cs`:
  - Replace workflow counts with pipeline run counts (`running`, `waiting_approval`, etc.)
  - `activeAgents` = count of `PipelineStepRun WHERE Status='running' AND StepType='agent'`

### 13. Update `WorkflowHub` and hub methods

- [ ] 13.1 Update `WorkflowHub.cs`:
  - Remove `SubscribeToWorkflow` / `UnsubscribeFromWorkflow` methods
  - Add `SubscribeToPipelineRun(string runId)` — joins `pipeline-run:{runId}`; returns 204
  - Add `UnsubscribeFromPipelineRun(string runId)` — leaves group
  - Add `SubscribeToStepRun(string stepRunId)` — joins `step-run:{stepRunId}`; pushes log tail
    via `_orchestrator.GetStepLogTail(stepRunId)`
  - Add `UnsubscribeFromStepRun(string stepRunId)` — leaves group
  - Update static `GroupFor` methods: `RunGroupFor(runId)`, `StepGroupFor(stepRunId)`
  - Remove `IAgentRunner` dependency; inject `IPipelineOrchestrator`

- [ ] 13.2 Update `WorkflowHubMethods` static class — replace old constants, add:
  ```
  PipelineRunUpdated, PipelineStepRunUpdated, ApprovalRequested, ApprovalDecided,
  PipelineRunLog, PipelineRunLogTail
  ```

### 14. Update `Program.cs`

- [ ] 14.1 Remove `AgentRunner` singleton + hosted service registration
- [ ] 14.2 Remove `IAgentRunner` → `AgentRunner` mapping
- [ ] 14.3 Remove `WorkflowProjector` scoped registration
- [ ] 14.4 Add `PipelineOrchestrator` singleton + hosted service registration
- [ ] 14.5 Add `IPipelineOrchestrator` → `PipelineOrchestrator` mapping
- [ ] 14.6 Add `PipelineRunProjector` scoped registration

### 15. Build and verify Phase 2

- [ ] 15.1 Build the full solution — zero compile errors
- [ ] 15.2 Run `dotnet run` and verify:
  - App starts without exceptions
  - `GET /api/pipelines` returns `[]`
  - `POST /api/pipelines` with a valid steps JSON creates a pipeline
  - `POST /api/pipeline-runs` with a valid pipeline starts a run, appears in `GET /api/pipeline-runs`
  - `GET /api/pipeline-runs/{id}` returns step runs

---

## Phase 3 — Pipeline Dashboard UI

### 16. Update TypeScript models

- [ ] 16.1 Add to `core/models.ts`:

  ```typescript
  export type PipelineRunStatus =
    | 'pending' | 'running' | 'waiting_approval' | 'completed' | 'failed' | 'cancelled';

  export type PipelineStepStatus =
    | 'pending' | 'running' | 'waiting_approval' | 'completed' | 'failed' | 'cancelled' | 'returned';

  export type ApprovalDecisionType = 'approved' | 'feedback';

  export interface Pipeline {
    id: string;
    name: string;
    description?: string | null;
    stepsJson: string;          // raw JSON — parsed client-side when needed
    repositoryId?: string | null;
    createdAt: string;
    updatedAt: string;
  }

  export interface PipelineRun {
    id: string;
    pipelineId: string;
    pipelineName: string;
    featureId?: string | null;
    featureName?: string | null;
    repositoryId: string;
    repositoryPath?: string | null;
    repositoryName?: string | null;
    status: PipelineRunStatus;
    currentStepId?: string | null;
    slug?: string | null;
    initialDescription?: string | null;
    createdAt: string;
    startedAt?: string | null;
    completedAt?: string | null;
    errorMessage?: string | null;
    stepRuns: PipelineStepRun[];
    pendingApprovals: ApprovalRequest[];
  }

  export interface PipelineStepRun {
    id: string;
    pipelineRunId: string;
    stepId: string;
    stepType: 'agent' | 'userApproval';
    stepName: string;
    attemptNumber: number;
    status: PipelineStepStatus;
    processId?: number | null;
    agentSlug?: string | null;
    isInteractive?: boolean | null;
    outputSummary?: string | null;
    createdAt: string;
    startedAt?: string | null;
    completedAt?: string | null;
    errorMessage?: string | null;
  }

  export interface ApprovalRequest {
    id: string;
    pipelineRunId: string;
    stepRunId: string;
    stepId: string;
    reviewFiles: string[];
    status: 'pending' | 'approved' | 'feedback_sent';
    feedbackText?: string | null;
    createdAt: string;
    decidedAt?: string | null;
  }

  export interface StartPipelineRunBody {
    pipelineId: string;
    repositoryId: string;
    featureId?: string | null;
    description?: string | null;
    slug?: string | null;
  }

  export interface StepCompleteBody {
    summary?: string | null;
    outputSummary?: string | null;
  }

  export interface ApprovalDecideBody {
    decision: ApprovalDecisionType;
    feedbackText?: string | null;
  }

  export interface PipelineRunLogLine {
    stepRunId: string;
    stream: 'stdout' | 'stderr';
    line: string;
    ts: string;
  }
  ```

- [ ] 16.2 Remove old `Workflow`, `WorkflowStatus`, `WorkflowType`, `Agent`, `AgentStatus`,
  `InputRequest`, `InputRequestStatus`, `WorkflowLogLine`, `WorkflowStatusUpdate`,
  `LaunchWorkflowBody` types from `models.ts` (keep `Feature`, `Repository`, `CatalogEntry`,
  `SpecManifest`, `DashboardSummary` and related types)

- [ ] 16.3 Update `DashboardSummary` interface: replace `runningWorkflows`, `pausedWorkflows`,
  `waitingInputWorkflows` with `runningPipelineRuns`, `waitingApprovalRuns`, `completedRuns`

### 17. Create `PipelinesService`

- [ ] 17.1 Create `core/api/pipelines.service.ts`:

  ```typescript
  @Injectable({ providedIn: 'root' })
  export class PipelinesService {
    private readonly http = inject(HttpClient);
    private readonly base = '/api';

    // Pipeline CRUD
    listPipelines(): Observable<Pipeline[]>
    getPipeline(id: string): Observable<Pipeline>
    createPipeline(body: Partial<Pipeline>): Observable<Pipeline>
    updatePipeline(id: string, body: Partial<Pipeline>): Observable<Pipeline>
    deletePipeline(id: string): Observable<void>

    // Pipeline Runs
    listRuns(params?: { pipelineId?: string; repositoryId?: string; status?: string }): Observable<PipelineRun[]>
    getRun(runId: string): Observable<PipelineRun>
    startRun(body: StartPipelineRunBody): Observable<PipelineRun>
    cancelRun(runId: string): Observable<void>
    decideApproval(runId: string, approvalId: string, body: ApprovalDecideBody): Observable<void>
  }
  ```

- [ ] 17.2 Delete `core/api/runs.service.ts`

### 18. Update `SignalRService`

- [ ] 18.1 Add new subjects:
  ```typescript
  readonly pipelineRunUpdated$ = new Subject<PipelineRun>();
  readonly pipelineStepRunUpdated$ = new Subject<PipelineStepRun>();
  readonly approvalRequested$ = new Subject<ApprovalRequest>();
  readonly approvalDecided$ = new Subject<ApprovalRequest>();
  readonly pipelineRunLog$$ = new Subject<PipelineRunLogLine>();
  readonly pipelineRunLogTail$$ = new Subject<{ stepRunId: string; lines: PipelineRunLogLine[] }>();
  ```
- [ ] 18.2 Remove old subjects: `workflowUpdated$`, `workflowLog$$`, `workflowLogTail$$`
  (keep `agentUpdated$` temporarily during transition if agents page still uses it — remove
  in Phase 4)
- [ ] 18.3 Register new hub event handlers in `start()` for all new event names
- [ ] 18.4 Add `subscribeToPipelineRun(runId)` / `unsubscribeFromPipelineRun(runId)` methods
- [ ] 18.5 Add `subscribeToStepRun(stepRunId)` / `unsubscribeFromStepRun(stepRunId)` methods
- [ ] 18.6 Add helper observables:
  `pipelineRunLog$(stepRunId)`: filters `pipelineRunLog$$` by stepRunId
  `pipelineRunLogTail$(stepRunId)`: filters tail by stepRunId

### 19. Create Pipelines page

- [ ] 19.1 Create `pages/pipelines/` directory
- [ ] 19.2 Create `pages/pipelines/pipelines.ts` — page component:
  - On init: `PipelinesService.listRuns()` ordered by `createdAt DESC`
  - Subscribe to `SignalRService.pipelineRunUpdated$`: merge run into list; sort
  - Subscribe to `SignalRService.pipelineStepRunUpdated$`: find matching run; update its step
  - Subscribe to `SignalRService.approvalRequested$`: find matching run; add to pendingApprovals
  - Template: header + "New Run" button + `<pipeline-run-row>` for each run

- [ ] 19.3 Create `pages/pipelines/pipeline-run-row.ts` — inline collapsible run component:
  - `@Input() run: PipelineRun`
  - Expand/collapse toggle showing step list
  - Status badge (colour-coded: running=blue, waiting_approval=amber, completed=green,
    failed=red)
  - Duration display (seconds/minutes since `startedAt`)
  - Step list: one row per `PipelineStepRun`, grouped by `stepId` (latest attempt shown,
    prior attempts collapsible)
  - Each step row: step name, type icon, status badge, attempt number, log button (headless)
  - Cancel run button (when run is `running` or `waiting_approval`)

- [ ] 19.4 Create `pages/pipelines/approval-panel.ts` — approval UI component:
  - `@Input() approval: ApprovalRequest`
  - `@Input() run: PipelineRun`
  - `@Output() decided = new EventEmitter<ApprovalDecideBody>()`
  - Shows: `reviewFiles` list (each file is a link to read the file content via a new
    `GET /api/pipeline-runs/{runId}/files?path=...` endpoint OR just displayed as a path)
  - Feedback textarea (visible only when user clicks "Send Feedback")
  - Approve button: emits `{ decision: 'approved' }`
  - Send Feedback button: shows textarea; emits `{ decision: 'feedback', feedbackText }`
  - Approval panel is embedded inside `pipeline-run-row` for the waiting step

- [ ] 19.5 Create `pages/pipelines/step-log-panel.ts` — log viewer component:
  - `@Input() stepRun: PipelineStepRun`
  - On open: calls `SignalRService.subscribeToStepRun(stepRun.id)` for tail + live
  - On close: calls `SignalRService.unsubscribeFromStepRun(stepRun.id)`
  - Scrolling log output; stdout/stderr coloured

- [ ] 19.6 Create `pages/pipelines/start-run-dialog.ts` — dialog to start a new run:
  - Pipeline selector (dropdown from `PipelinesService.listPipelines()`)
  - Repository selector
  - Feature selector (optional)
  - Description textarea
  - Slug input
  - "Start" button calls `PipelinesService.startRun(...)`

### 20. Update routing

- [ ] 20.1 Add `/pipelines` route pointing to `PipelinesPage`
- [ ] 20.2 Add `/pipelines/designer` route pointing to `PipelineDesignerPage` (stub for Phase 5)
- [ ] 20.3 Change `/runs` redirect to point to `/pipelines` (old bookmark support)
- [ ] 20.4 Delete `pages/runs/` directory and all files inside it
- [ ] 20.5 Update navigation menu (if present) to replace "Runs" with "Pipelines"

---

## Phase 4 — Agents Page (Runtime Tracking)

### 21. Update backend agents endpoint

- [ ] 21.1 In `AgentsController.cs`: change `GET /api/agents` to return
  `List<PipelineStepRunDto>` where `StepType = "agent"` and `Status = "running"`
  (include `PipelineRun.Pipeline.Name` and `Repository.Name` for context)

### 22. Update frontend agents page

- [ ] 22.1 Update `pages/agents/agents.ts`:
  - Change service call from `AgentsService.getAgents()` to
    `PipelinesService.listRuns({ status: 'running' })` then filter for running agent steps,
    OR call the updated `AgentsService.getRunningAgentSteps()` (update `agents.service.ts`)
  - Update `core/api/agents.service.ts` to return `PipelineStepRun[]`
  - Display: pipeline name, step name, agent slug, interactive flag, elapsed time, log button
  - Log button: opens inline `StepLogPanel` (headless only)
  - Subscribe to `SignalRService.pipelineStepRunUpdated$`: refresh step status live

---

## Phase 5 — Pipeline Designer UI

### 23. Create Pipeline Designer page

- [ ] 23.1 Create `pages/pipelines/designer/pipeline-designer.ts` — full page component:
  - "New Pipeline" mode (no id param) or "Edit Pipeline" mode (id from route param)
  - Form: pipeline name, description, repository selector (optional default)
  - Step list: CDK `DragDropModule` for reordering
  - "Add Step" button: opens `StepBuilderComponent` in a side-panel or dialog
  - Save button: `PipelinesService.createPipeline(...)` or `updatePipeline(...)`
  - Import JSON button: allows pasting raw steps JSON
  - Export JSON button: copies steps JSON to clipboard

- [ ] 23.2 Create `pages/pipelines/designer/step-builder.ts` — step editing component:
  - `@Input() step: Partial<PipelineStepDefinition> | null` (null = new step)
  - `@Output() saved = new EventEmitter<PipelineStepDefinition>()`
  - Type selector: "Agent Step" | "Approval Step" (radio or segmented control)
  - **Agent step fields:**
    - Step ID (auto-derived from name, editable)
    - Step Name
    - Agent Slug (dropdown populated from `CatalogService.list('agent')`)
    - Interactive toggle (yes/no/auto)
    - On Feedback: "Return to step" dropdown (shows earlier steps in the pipeline)
    - On Approve: "Continue" (default) or specific step-id dropdown
  - **Approval step fields:**
    - Step ID, Step Name
    - Review Files list (add/remove; hint about `{slug}` token)
    - On Feedback: "Return to step" dropdown
    - On Approve: "Continue" or step-id dropdown

- [ ] 23.3 Add route `/pipelines/:id/edit` → `PipelineDesignerPage` with id param

### 24. Add "Edit Pipeline" links from Pipelines page

- [ ] 24.1 Add edit (pencil) icon button on each pipeline header row in `PipelinesPage`
  that routes to `/pipelines/{id}/edit`
- [ ] 24.2 Add "New Pipeline" button in page header that routes to `/pipelines/designer`

---

## Phase 6 — Agent `.md` File Updates

### 25. Update `pm-draft.md` (workflow agent)

- [ ] 25.1 Update the "Context" section env var table to list the new pipeline env vars:
  `PIPELINE_RUN_ID`, `STEP_RUN_ID`, `STEP_ID`, `ATTEMPT_NUMBER`, plus `WORKFLOW_DASHBOARD_API_URL`
  and `REPOSITORY_PATH`
- [ ] 25.2 Add a "Reading Pipeline Context" section instructing the agent to read
  `.github/copilot/workflow-input.md` to understand the full pipeline history
- [ ] 25.3 Add a "Pipeline Completion" section (markdown code block with the HTTP call):
  ```
  ## Pipeline Completion

  Before exiting, if PIPELINE_RUN_ID is set, call:

  POST {WORKFLOW_DASHBOARD_API_URL}/api/pipeline-runs/{PIPELINE_RUN_ID}/steps/{STEP_ID}/complete
  Content-Type: application/json

  { "summary": "<one line>", "outputSummary": "<2-4 sentences>" }
  ```
- [ ] 25.4 Remove references to `WORKFLOW_ID` env var (replaced by `PIPELINE_RUN_ID`)
- [ ] 25.5 Update the "On approval, create the feature" step: replace
  `?workflowId={WORKFLOW_ID}` with `?pipelineRunId={PIPELINE_RUN_ID}` in the API call URL
  (and update `FeaturesController` to accept `pipelineRunId` query param instead of
  `workflowId`)

### 26. Update `architect.md` (agent)

- [ ] 26.1 Add a "Pipeline Context" section at the top (before "Your deliverable"):
  instructing the agent to check for `PIPELINE_RUN_ID` env var; if set, read
  `workflow-input.md` first for the PM's proposal and any prior iteration context
- [ ] 26.2 Add a "Pipeline Completion" section (same pattern as pm-draft.md):
  - After writing `design.md` and `tasks.md`, call the completion endpoint
  - Summary should reference the created files
- [ ] 26.3 Update the "Rules" section: when `PIPELINE_RUN_ID` is set, write the plan to
  `openspec/changes/{STEP_ID}/design.md` and `tasks.md` (STEP_ID carries the slug-like step
  name; alternatively, read `Slug` from `workflow-input.md`)

### 27. Update `developer.md` (agent)

- [ ] 27.1 Add a "Pipeline Context" section: if `PIPELINE_RUN_ID` is set, read
  `workflow-input.md` for context from prior steps (PM proposal, Architect plan)
- [ ] 27.2 Add a "Pipeline Completion" section: call completion endpoint after last task

### 28. Update `docs/copilot-defaults/README.md`

- [ ] 28.1 Update the README to document the new pipeline env vars and the pipeline completion
  protocol that all agents must follow

---

## Phase — Validation

### 29. Integration tests (manual)

- [ ] 29.1 Create a simple 2-step pipeline in the Designer: one `agent` step (architect),
  one `userApproval` step
- [ ] 29.2 Start a run against a test repository; verify `workflow-input.md` is created with
  the correct header
- [ ] 29.3 Let the agent step complete (or trigger it manually via the complete endpoint);
  verify the approval request appears in the dashboard
- [ ] 29.4 Click "Approve" in the dashboard; verify the run completes
- [ ] 29.5 Test the feedback loop: click "Send Feedback" with text; verify a second attempt
  of the first step is launched and `workflow-input.md` contains both attempts
- [ ] 29.6 Cancel a running run; verify the process is killed and run status is `cancelled`
- [ ] 29.7 Restart the API with a `running` step in DB; verify `StartupReconciler` marks it
  `failed`

### 30. Build and quality checks

- [ ] 30.1 `dotnet build` — zero warnings (treat warnings as errors)
- [ ] 30.2 `ng build` — zero errors; bundle size within acceptable range
- [ ] 30.3 All existing Angular unit tests pass (update test fixtures for new models)
- [ ] 30.4 Review final `workflow.db` schema matches the design with `PRAGMA table_info`
- [ ] 30.5 Verify SignalR events arrive in the browser for all state transitions
  (use browser devtools Network → WS tab)
