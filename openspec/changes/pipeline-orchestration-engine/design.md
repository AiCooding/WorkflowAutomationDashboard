# Design: Pipeline Orchestration Engine

> 🏗️ Authored by: Architect persona  
> 📅 Date: 2025-01-27

---

## Technical Approach

The dashboard is redesigned from a "single-agent workflow runner" into a **pipeline orchestration
engine**. A **Pipeline** is a JSON-defined sequence of steps stored in the database. A
**PipelineRun** is one execution of a pipeline against a specific repository. Each step is
materialised as a **PipelineStepRun** row. The orchestrator advances the pipeline
step-by-step: launching agent processes for `agent` steps, or creating an `ApprovalRequest`
record and pausing for `userApproval` steps.

The existing `ProcessLauncher`, `InstructionsInjector`, and `WorkflowInputWriter` services are
retained and extended. The existing `AgentRunner` `BackgroundService`, `WorkflowsController`,
`Workflow`, `WorkflowEvent`, `Agent`, and `InputRequest` models are **deleted** (clean break).

`Feature` and `Repository` models are **unchanged**.

The `workflow-input.md` file evolves from a per-run write to a **pipeline-run-lifetime rolling
document**: the orchestrator appends a new section header before each step; the agent appends its
content body; approval decisions are also appended. The file is NOT deleted between steps — it
accumulates the full conversation history for the entire run.

---

## Architecture Decisions

### Decision: PipelineOrchestrator as a BackgroundService with Channel

Retain the channel-per-message dispatch pattern from the existing `AgentRunner`. The
`PipelineOrchestrator` owns a `Channel<OrchestratorMessage>` (union type: start a run, advance
after step completion, apply an approval decision). A single-reader background loop processes
messages sequentially per-run slot.

**Why:** Proven pattern already in the codebase. Keeps all orchestration logic in one place.
The channel decouples the HTTP controller (writes message) from the execution engine (reads
message), which prevents controller timeouts and simplifies error recovery.

**Trade-offs:** Messages are in-memory; a crash loses pending messages, which is identical to
the existing behaviour and acceptable for a developer tool.

### Decision: Pipeline step definitions stored as JSON column on `Pipeline`

The step array is stored as `StepsJson TEXT` on the `Pipeline` row. It is deserialised to
`List<PipelineStepDefinition>` records at runtime.

**Why:** Pipelines are low-volume developer artefacts. A separate `PipelineStep` table would
require complex ordered-row management (position column, cascade deletes, etc.) with no
query benefit. JSON column keeps the schema simple and the designer UI simpler.

**Trade-offs:** Cannot query individual steps from SQL; step-level queries go through the
object model. Acceptable given expected data volumes (< 50 pipelines).

### Decision: Per-step-run log groups in SignalR (not per-run)

Log lines from headless agent steps are pushed to a SignalR group keyed on
`step-run:{stepRunId}`. State-change events are pushed to `pipeline-run:{runId}` (all
subscribers of a run receive step-level updates).

**Why:** A pipeline run may have multiple steps active in rare cases (not in Phase 1, but
design should allow it). Per-step-run groups allow the UI to subscribe only to the log
stream of the currently expanded step without receiving noise from other steps.

### Decision: Reuse ProcessLauncher with a new overload

Add `Process Start(PipelineStepContext ctx, Repository repo, string apiBaseUrl)` to
`ProcessLauncher`. The new context record carries `PipelineRunId`, `StepRunId`, `StepId`,
`FeatureId`. The old `Workflow`-based overload is deleted. `IProcessLauncher` is updated
to the new signature.

**Why:** `ProcessLauncher` only builds `ProcessStartInfo` — no business logic. One
overload change is lower risk than creating a parallel launcher class.

### Decision: WorkflowInputWriter gets an Append API

`WorkflowInputWriter` gains new methods for pipeline mode:
- `WritePipelineHeader(PipelineRun, Repository, string initialDescription, string apiBaseUrl)` —
  creates the file with the run-level header (called once at run start).
- `AppendStepSection(Repository, string header, string? contentBody)` — appends a
  `## [step-id] attempt N — by: X — for: Y` section; content body is optional (null when the
  orchestrator writes the header before the agent starts and the agent will append content).
- The `Cleanup` and `Write` methods remain for backward compat during the transition but are
  not used in pipeline mode.

### Decision: Feature.Workflows nav property removed, Feature stays otherwise

`Feature` gains no new nav properties. `PipelineRun` carries a nullable `FeatureId` FK. The
old `ICollection<Workflow> Workflows` nav is removed from `Feature`. This is the only change to
the `Feature` C# class.

### Decision: No parallel step execution in Phase 1

Steps execute strictly sequentially. Parallelism is left for a future phase. The orchestrator
advances to `steps[currentIndex + 1]` after each step completes.

---

## Data / Message Flow

### Pipeline Run start

```
User POST /api/pipeline-runs (body: pipelineId, repositoryId, featureId?, description?, slug?)
  → PipelineRunsController inserts PipelineRun{status=pending}
  → sends OrchestratorMessage.StartRun(runId) to PipelineOrchestrator channel
  → controller returns 202 with run id

PipelineOrchestrator (background loop) receives StartRun:
  → loads Pipeline, resolves steps[]
  → sets run.Status = running, run.CurrentStepId = steps[0].Id
  → advances to step 0 (see "Step advance" below)
```

### Step advance — agent step

```
PipelineOrchestrator.AdvanceToStep(run, stepDef):
  → inserts PipelineStepRun{status=pending, attemptNumber=N}
  → updates run.CurrentStepId
  → WorkflowInputWriter.AppendStepSection(repo, header, contentBody: null)
     header = "## [{stepId}] attempt {N} — by: {agentSlug} — for: {nextSteps}"
  → InstructionsInjector.Inject(agentBody, context)
     agentBody = catalog entry body for agentSlug (kind="agent")
  → ProcessLauncher.Start(ctx, repo, apiBaseUrl)
  → stepRun.Status = running, stepRun.ProcessId = pid
  → hub.Clients.All.SendAsync("PipelineStepRunUpdated", stepRunDto)
  → hub.Clients.All.SendAsync("PipelineRunUpdated", runDto)
```

### Step complete callback — agent step

```
Agent calls POST /api/pipeline-runs/{runId}/steps/{stepId}/complete
  (body: { summary?, outputSummary? })
  → controller writes OrchestratorMessage.StepCompleted(runId, stepRunId, summary)

PipelineOrchestrator receives StepCompleted:
  → stepRun.Status = completed
  → resolves nextStep from pipeline definition:
       if onApprove == "continue" → nextStepDef = steps[currentIndex + 1]
       if onApprove is step-id → nextStepDef = steps[that id]
  → AdvanceToStep(run, nextStepDef)   ← recurse
     or if no next step: run.Status = completed
```

### Step advance — userApproval step

```
PipelineOrchestrator.AdvanceToStep(run, userApprovalStepDef):
  → inserts PipelineStepRun{status=waiting_approval}
  → inserts ApprovalRequest{status=pending, reviewFiles=..., stepRunId=...}
  → WorkflowInputWriter.AppendStepSection(repo,
       "## [{stepId}] — by: user — decision: pending — for: {nextSteps}")
  → run.Status = waiting_approval, run.CurrentStepId = stepId
  → hub broadcasts ApprovalRequested + PipelineRunUpdated
  → orchestrator does NOT advance further; waits for user
```

### Approval decision

```
User POST /api/pipeline-runs/{runId}/approvals/{approvalId}/decide
  (body: { decision: "approved" | "feedback", feedbackText? })
  → controller validates, writes OrchestratorMessage.ApprovalDecided(runId, approvalId, decision, feedback)

PipelineOrchestrator receives ApprovalDecided:
  → approvalRequest.Status = approved | feedback_sent
  → approvalRequest.DecidedAt = now
  → stepRun.Status = completed | returned
  → WorkflowInputWriter.AppendStepSection(repo,
       "## [{stepId}] — by: user — decision: {decision} — for: {returnToStepId|nextStep}")
       contentBody = feedbackText (if feedback)
  → if approved: AdvanceToStep(run, nextStepDef)
  → if feedback:
       returnToStepDef = pipeline.GetStep(onFeedback.ReturnTo)
       AdvanceToStep(run, returnToStepDef)  ← new attempt (attemptNumber++)
```

### Feedback loop — incrementing attempt number

```
Each call to AdvanceToStep for a step that has prior PipelineStepRuns:
  → attemptNumber = MAX(existing stepRuns for same stepId) + 1
  → new PipelineStepRun row inserted with attemptNumber
  → header written: "## [{stepId}] attempt {N} — by: ..."
```

---

## Affected Projects & Files

| Project | File | Change Type | Notes |
|---------|------|-------------|-------|
| `WorkflowDashboard.Api` | `Models/Workflow.cs` | **Delete** | Replaced by `PipelineRun` |
| `WorkflowDashboard.Api` | `Models/WorkflowEvent.cs` | **Delete** | Replaced by `PipelineRunLog` |
| `WorkflowDashboard.Api` | `Models/Agent.cs` | **Delete** | Replaced by `PipelineStepRun` |
| `WorkflowDashboard.Api` | `Models/InputRequest.cs` | **Delete** | Replaced by `ApprovalRequest` |
| `WorkflowDashboard.Api` | `Models/WorkflowDto.cs` | **Delete** | Replaced by new DTOs |
| `WorkflowDashboard.Api` | `Models/Feature.cs` | **Modify** | Remove `ICollection<Workflow>` nav |
| `WorkflowDashboard.Api` | `Models/Pipeline.cs` | **New** | Pipeline definition entity |
| `WorkflowDashboard.Api` | `Models/PipelineRun.cs` | **New** | One execution of a pipeline |
| `WorkflowDashboard.Api` | `Models/PipelineStepRun.cs` | **New** | One step execution within a run |
| `WorkflowDashboard.Api` | `Models/ApprovalRequest.cs` | **New** | Human review/approval gate |
| `WorkflowDashboard.Api` | `Models/PipelineRunLog.cs` | **New** | Log events per run/step |
| `WorkflowDashboard.Api` | `Models/PipelineStepDefinition.cs` | **New** | Record for JSON step defs |
| `WorkflowDashboard.Api` | `Models/PipelineRunDto.cs` | **New** | Read-model with step runs inline |
| `WorkflowDashboard.Api` | `Data/WorkflowDbContext.cs` | **Modify** | Replace old DbSets, add new |
| `WorkflowDashboard.Api` | `Migrations/YYYYMMDD_PipelineSchema.cs` | **New** | Drop old tables, add new tables |
| `WorkflowDashboard.Api` | `Services/AgentRunner/AgentRunner.cs` | **Delete** | Replaced by `PipelineOrchestrator` |
| `WorkflowDashboard.Api` | `Services/AgentRunner/IAgentRunner.cs` | **Delete** | Interface no longer needed |
| `WorkflowDashboard.Api` | `Services/AgentRunner/RunningProcess.cs` | **Modify** | Change `Workflow` → `PipelineStepContext` |
| `WorkflowDashboard.Api` | `Services/AgentRunner/ProcessLauncher.cs` | **Modify** | New `Start(PipelineStepContext, ...)` signature |
| `WorkflowDashboard.Api` | `Services/AgentRunner/LogBatcher.cs` | **Modify** | Key on `stepRunId` instead of `workflowId`; push to `PipelineRunLog` |
| `WorkflowDashboard.Api` | `Services/AgentRunner/WorkflowInputWriter.cs` | **Modify** | Add pipeline-mode append API |
| `WorkflowDashboard.Api` | `Services/AgentRunner/InstructionsInjector.cs` | **Modify** | Accept `PipelineStepContext` instead of `Workflow` |
| `WorkflowDashboard.Api` | `Services/AgentRunner/StartupReconciler.cs` | **Modify** | Reconcile `PipelineStepRun` orphans instead of `Workflow` orphans |
| `WorkflowDashboard.Api` | `Services/Pipeline/PipelineOrchestrator.cs` | **New** | Core orchestration BackgroundService |
| `WorkflowDashboard.Api` | `Services/Pipeline/OrchestratorMessage.cs` | **New** | Discriminated union for channel messages |
| `WorkflowDashboard.Api` | `Services/Pipeline/PipelineStepContext.cs` | **New** | Record passed to ProcessLauncher / InstructionsInjector |
| `WorkflowDashboard.Api` | `Services/WorkflowProjector.cs` | **Delete** | Replaced by `PipelineRunProjector` |
| `WorkflowDashboard.Api` | `Services/Pipeline/PipelineRunProjector.cs` | **New** | Projects `PipelineRun` + nav props to `PipelineRunDto` |
| `WorkflowDashboard.Api` | `Controllers/WorkflowsController.cs` | **Delete** | Replaced by new controllers |
| `WorkflowDashboard.Api` | `Controllers/EventsController.cs` | **Modify** | Query `PipelineRunLog` instead of `WorkflowEvent` |
| `WorkflowDashboard.Api` | `Controllers/AgentsController.cs` | **Modify** | Return running `PipelineStepRun` rows |
| `WorkflowDashboard.Api` | `Controllers/InputRequestsController.cs` | **Delete** | Replaced by approvals endpoint |
| `WorkflowDashboard.Api` | `Controllers/PipelinesController.cs` | **New** | CRUD for `Pipeline` definitions |
| `WorkflowDashboard.Api` | `Controllers/PipelineRunsController.cs` | **New** | Run lifecycle + step completion + approval decide |
| `WorkflowDashboard.Api` | `Controllers/DashboardController.cs` | **Modify** | Update summary to use new entities |
| `WorkflowDashboard.Api` | `Hubs/WorkflowHub.cs` | **Modify** | Add pipeline-run/step-run group methods; rename hub methods |
| `WorkflowDashboard.Api` | `Program.cs` | **Modify** | Register new services, remove old |
| `workflow-dashboard-ui` | `core/models.ts` | **Modify** | Add pipeline types; remove old workflow types |
| `workflow-dashboard-ui` | `core/api/pipelines.service.ts` | **New** | API calls for pipelines + runs + approvals |
| `workflow-dashboard-ui` | `core/api/runs.service.ts` | **Delete** | Replaced by `pipelines.service.ts` |
| `workflow-dashboard-ui` | `core/api/agents.service.ts` | **Modify** | Return `PipelineStepRun[]` |
| `workflow-dashboard-ui` | `core/realtime/signalr.service.ts` | **Modify** | Add pipeline event subjects; remove old workflow subjects |
| `workflow-dashboard-ui` | `app.routes.ts` | **Modify** | Add `/pipelines`, `/pipelines/designer`; redirect `/runs` |
| `workflow-dashboard-ui` | `pages/runs/` | **Delete** | Replaced by `pages/pipelines/` |
| `workflow-dashboard-ui` | `pages/pipelines/pipelines.ts` | **New** | Hierarchical run list page |
| `workflow-dashboard-ui` | `pages/pipelines/pipeline-run-row.ts` | **New** | Single run with inline step list |
| `workflow-dashboard-ui` | `pages/pipelines/approval-panel.ts` | **New** | Review files + feedback textarea + Approve/Feedback buttons |
| `workflow-dashboard-ui` | `pages/pipelines/designer/pipeline-designer.ts` | **New** | Form-based pipeline designer page |
| `workflow-dashboard-ui` | `pages/pipelines/designer/step-builder.ts` | **New** | Add/edit step form component |
| `workflow-dashboard-ui` | `pages/agents/agents.ts` | **Modify** | Show running `PipelineStepRun` (type=agent) rows |
| `docs/copilot-defaults/workflows/pm-draft.md` | `pm-draft.md` | **Modify** | Add pipeline completion call |
| `docs/copilot-defaults/agents/architect.md` | `architect.md` | **Modify** | Add pipeline completion call |
| `docs/copilot-defaults/agents/developer.md` | `developer.md` | **Modify** | Add pipeline completion call |

---

## New Types / Interfaces

### Backend — Models

**`Pipeline`** — Stored pipeline definition with name, description, and `StepsJson` column
(JSON array of `PipelineStepDefinition`). FK to optional default `Repository`.

**`PipelineRun`** — One execution instance. Status enum:
`pending | running | waiting_approval | completed | failed | cancelled`.
Carries `CurrentStepId`, `Slug` (for `{slug}` token substitution in `reviewFiles`),
nullable `FeatureId`, required `RepositoryId`.

**`PipelineStepRun`** — One step execution. Carries `StepId` (matches pipeline definition),
`StepType`, `AttemptNumber` (1-based), `Status`, `ProcessId?`, `AgentSlug?`, `IsInteractive?`,
`OutputSummary?`.

**`ApprovalRequest`** — Human gate. Carries `ReviewFilesJson` (JSON string array with `{slug}`
tokens already resolved), `Status` (`pending | approved | feedback_sent`), `FeedbackText?`,
`DecidedAt?`.

**`PipelineRunLog`** — Replaces `WorkflowEvent`. `EventType`:
`state_change | log | error | step_completed | approval_created | approval_decided`.

**`PipelineStepDefinition`** — Deserialised from `Pipeline.StepsJson`. Pure record, not an entity.
Fields: `Id`, `Type`, `Name`, `AgentSlug?`, `Interactive?`, `ReviewFiles?` (string list),
`OnFeedback?` (record: `ReturnTo`), `OnApprove?`.

**`PipelineStepContext`** — Value object passed to `ProcessLauncher` / `InstructionsInjector`.
Fields: `PipelineRunId`, `StepRunId`, `StepId`, `AttemptNumber`, `FeatureId?`, `RepositoryId`,
`IsInteractive`.

**`PipelineRunDto`** — Read-model. Includes `List<PipelineStepRunDto>` (all step runs),
`List<ApprovalRequestDto>` (pending approvals only), `RepositoryPath`, `RepositoryName`,
`FeatureName`, pipeline `Name`.

### Backend — Services

**`PipelineOrchestrator`** — `BackgroundService` + `IHostedService`. Owns
`Channel<OrchestratorMessage>`. Methods: `EnqueueStartRun`, `EnqueueStepCompleted`,
`EnqueueApprovalDecided`. Internal: `AdvanceToStep(run, stepDef, ct)`.

**`OrchestratorMessage`** — Abstract base (or discriminated union via records):
- `record StartRun(string RunId)`
- `record StepCompleted(string RunId, string StepRunId, string? Summary)`
- `record ApprovalDecided(string RunId, string ApprovalId, string Decision, string? Feedback)`

**`PipelineRunProjector`** — Scoped service. Projects `PipelineRun` + loaded nav props to
`PipelineRunDto`. Similar to the existing `WorkflowProjector`.

### Frontend — Models (additions to `models.ts`)

**`Pipeline`**, **`PipelineRun`**, **`PipelineStepRun`**, **`ApprovalRequest`**, 
**`PipelineRunLog`** — mirror the backend DTOs.

**`PipelineStepStatus`** — `'pending' | 'running' | 'waiting_approval' | 'completed' | 'failed' | 'cancelled'`

**`ApprovalDecision`** — `{ decision: 'approved' | 'feedback'; feedbackText?: string }`

---

## DI Registration

All in `Program.cs` (replace existing `AgentRunner` block):

```
// Existing (keep)
services.AddSingleton<IProcessLauncher, ProcessLauncher>()
services.AddSingleton<InstructionsInjector>()
services.AddSingleton<WorkflowInputWriter>()

// New
services.AddSingleton<PipelineOrchestrator>()
services.AddHostedService(sp => sp.GetRequiredService<PipelineOrchestrator>())
services.AddSingleton<IPipelineOrchestrator>(sp => sp.GetRequiredService<PipelineOrchestrator>())
services.AddScoped<PipelineRunProjector>()

// Remove
// AgentRunner (singleton + hosted service)
// IAgentRunner → AgentRunner mapping
// WorkflowProjector scoped registration
```

The `StartupReconciler` is updated in-place (no DI change needed; just changes its internal
query from `Workflow` to `PipelineStepRun`).

---

## Build-time Impact

None. No source generators, no MSBuild tasks. One new EF Core migration.

---

## API Surface

### Pipelines

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/pipelines` | List all pipeline definitions |
| `GET` | `/api/pipelines/{id}` | Get one pipeline with parsed steps |
| `POST` | `/api/pipelines` | Create pipeline (`name`, `description?`, `stepsJson`, `repositoryId?`) |
| `PUT` | `/api/pipelines/{id}` | Update pipeline definition |
| `DELETE` | `/api/pipelines/{id}` | Delete pipeline (only if no active runs) |

### Pipeline Runs

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/pipeline-runs` | List runs (`?pipelineId=`, `?repositoryId=`, `?status=`) |
| `GET` | `/api/pipeline-runs/{runId}` | Get run with step runs and pending approvals |
| `POST` | `/api/pipeline-runs` | Start a run (body: `pipelineId`, `repositoryId`, `featureId?`, `description?`, `slug?`) |
| `POST` | `/api/pipeline-runs/{runId}/cancel` | Cancel run (kills active process if any) |
| `POST` | `/api/pipeline-runs/{runId}/steps/{stepId}/complete` | Step completion callback (called by agent) |
| `POST` | `/api/pipeline-runs/{runId}/steps/{stepId}/log` | Stream log line from agent (body: `{ stream, line }`) |
| `POST` | `/api/pipeline-runs/{runId}/approvals/{approvalId}/decide` | User decision (body: `{ decision, feedbackText? }`) |

### Agents (updated)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/agents` | Returns running `PipelineStepRun` rows where `StepType=agent, Status=running` |

### Dashboard summary (updated)

`GET /api/dashboard` — counts updated to query `PipelineRun` and `PipelineStepRun`.

---

## SignalR Hub Changes

Hub URL stays `/hubs/workflow`. Methods added to `WorkflowHub`:

| Direction | Method / Event | Payload |
|-----------|---------------|---------|
| Client→Server | `SubscribeToPipelineRun(runId)` | joins group `pipeline-run:{runId}` |
| Client→Server | `UnsubscribeFromPipelineRun(runId)` | leaves group |
| Client→Server | `SubscribeToStepRun(stepRunId)` | joins group `step-run:{stepRunId}`; pushes log tail |
| Client→Server | `UnsubscribeFromStepRun(stepRunId)` | leaves group |
| Server→Client | `PipelineRunUpdated` | `PipelineRunDto` |
| Server→Client | `PipelineStepRunUpdated` | `PipelineStepRunDto` |
| Server→Client | `ApprovalRequested` | `ApprovalRequestDto` |
| Server→Client | `ApprovalDecided` | `ApprovalRequestDto` |
| Server→Client | `PipelineRunLog` | `{ stepRunId, stream, line, ts }` |
| Server→Client | `PipelineRunLogTail` | `{ stepRunId, lines[] }` |

Old hub methods (`WorkflowUpdated`, `WorkflowLog`, etc.) are **deleted**. `WorkflowHubMethods`
static class is updated with new constants.

---

## EF Core Model Details

### `Pipeline`
```
Id          TEXT PK  (nanoid 12)
Name        TEXT NOT NULL
Description TEXT NULL
StepsJson   TEXT NOT NULL  (JSON array)
RepositoryId TEXT NULL FK→Repositories
CreatedAt   TEXT NOT NULL
UpdatedAt   TEXT NOT NULL
```

### `PipelineRun`
```
Id               TEXT PK  (nanoid 12)
PipelineId       TEXT NOT NULL FK→Pipelines (Restrict)
FeatureId        TEXT NULL FK→Features (SetNull)
RepositoryId     TEXT NOT NULL FK→Repositories (Restrict)
Status           TEXT NOT NULL DEFAULT 'pending'
CurrentStepId    TEXT NULL
Slug             TEXT NULL
InitialDescription TEXT NULL
StartedAt        TEXT NULL
CompletedAt      TEXT NULL
ErrorMessage     TEXT NULL
CreatedAt        TEXT NOT NULL

INDEX: (PipelineId)
INDEX: (Status)
INDEX: (RepositoryId)
```

### `PipelineStepRun`
```
Id             TEXT PK  (nanoid 12)
PipelineRunId  TEXT NOT NULL FK→PipelineRuns (Cascade)
StepId         TEXT NOT NULL  (matches step definition Id)
StepType       TEXT NOT NULL  ('agent' | 'userApproval')
StepName       TEXT NOT NULL
AttemptNumber  INTEGER NOT NULL DEFAULT 1
Status         TEXT NOT NULL DEFAULT 'pending'
ProcessId      INTEGER NULL
AgentSlug      TEXT NULL
IsInteractive  INTEGER NULL  (0/1/null)
OutputSummary  TEXT NULL
StartedAt      TEXT NULL
CompletedAt    TEXT NULL
ErrorMessage   TEXT NULL
CreatedAt      TEXT NOT NULL

INDEX: (PipelineRunId)
INDEX: (Status)
```

### `ApprovalRequests`
```
Id            TEXT PK  (nanoid 12)
PipelineRunId TEXT NOT NULL FK→PipelineRuns (Cascade)
StepRunId     TEXT NOT NULL FK→PipelineStepRuns (Cascade)
StepId        TEXT NOT NULL
ReviewFilesJson TEXT NOT NULL  (JSON string[]; {slug} already resolved)
Status        TEXT NOT NULL DEFAULT 'pending'
FeedbackText  TEXT NULL
DecidedAt     TEXT NULL
CreatedAt     TEXT NOT NULL

INDEX: (PipelineRunId)
INDEX: (Status)
```

### `PipelineRunLogs`
```
Id            INTEGER PK AUTOINCREMENT
PipelineRunId TEXT NOT NULL FK→PipelineRuns (Cascade)
StepRunId     TEXT NULL FK→PipelineStepRuns (SetNull)
EventType     TEXT NOT NULL
Message       TEXT NULL
MetadataJson  TEXT NULL
CreatedAt     TEXT NOT NULL

INDEX: (PipelineRunId, CreatedAt)
```

---

## Migration Strategy

Single new migration `YYYYMMDD_PipelineSchema`:

1. **Drop** tables: `Agents`, `InputRequests`, `WorkflowEvents`, `Workflows`
   (in dependency order — FK children first: `InputRequests`, `Agents`, `WorkflowEvents`, `Workflows`).

2. **Create** tables: `Pipelines`, `PipelineRuns`, `PipelineStepRuns`, `ApprovalRequests`,
   `PipelineRunLogs`.

3. **No data preservation** — user confirmed clean break.

The migration is hand-edited after `dotnet ef migrations add` to insert the explicit `DROP TABLE`
statements for the old tables and remove the stale column-drop operations that EF may auto-generate.

---

## `workflow-input.md` Format (new)

File created at `{repo}/.github/copilot/workflow-input.md` at run start; lives for the entire
run (never deleted between steps); deleted only on run completion/failure (optional — keep for
debugging, controlled by `AgentRunner:CleanupInputFileOnComplete`).

```markdown
# Pipeline Run: Full Feature Pipeline
Run ID: run-abc123
Pipeline: full-feature-pipeline
Repository: /path/to/repo
Feature: repo-linking (feature-abc456)
API Base URL: http://localhost:5000
Started: 2025-01-27T10:00:00Z

---

## [initial-request] — by: user — for: pm
<user's initial description text>

---

## [pm] attempt 1 — by: pm-draft — for: pm-approval
<!-- ORCHESTRATOR WRITES THIS HEADER BEFORE LAUNCHING AGENT -->
<!-- AGENT APPENDS ITS CONTENT BELOW THIS LINE -->
<agent output here>

---

## [pm-approval] — by: user — decision: feedback — for: pm
<user feedback text>

---

## [pm] attempt 2 — by: pm-draft — for: pm-approval
<revised output>

---

## [pm-approval] — by: user — decision: approved — for: architect

---
```

**Rules:**
- Orchestrator writes all `## [...]` section headers.
- Agent appends its content body (no header) after the header.
- Agent must NOT write a new section header.
- `for:` field contains comma-separated step IDs that will read this section.
- On agent feedback loops, `attemptNumber` increments; prior attempts remain in the file.

---

## InstructionsInjector Changes

New method signature:
```
string Inject(PipelineStepContext ctx, Repository repo, string agentMarkdownBody, string inputFilePath)
```

The context block now includes:
```
<!-- pipeline-context
pipeline_run_id: {ctx.PipelineRunId}
step_run_id: {ctx.StepRunId}
step_id: {ctx.StepId}
attempt_number: {ctx.AttemptNumber}
feature_id: {ctx.FeatureId ?? "none"}
repository_path: {repo.Path}
input_file: {inputRelative}
-->
```

Env vars passed via `ProcessLauncher`:
- `PIPELINE_RUN_ID`
- `STEP_RUN_ID`
- `STEP_ID`
- `ATTEMPT_NUMBER`
- `FEATURE_ID`
- `REPOSITORY_PATH`
- `WORKFLOW_DASHBOARD_API_URL`

---

## Agent .md File Protocol (Phase 6)

Each agent `.md` file must include a **Pipeline Completion** section:

```markdown
## Pipeline Completion (when running inside a pipeline)

When you have finished your work, call the pipeline completion endpoint **before exiting**:

```http
POST {WORKFLOW_DASHBOARD_API_URL}/api/pipeline-runs/{PIPELINE_RUN_ID}/steps/{STEP_ID}/complete
Content-Type: application/json

{
  "summary": "<one-line summary of what you did>",
  "outputSummary": "<2-4 sentences describing key outputs>"
}
```

Read these values from environment variables: `PIPELINE_RUN_ID`, `STEP_ID`,
`WORKFLOW_DASHBOARD_API_URL`. If these variables are not set, you are running standalone
(not inside a pipeline) — skip this call.
```

The agent reads `workflow-input.md` to understand the full pipeline context (what previous
steps produced). The agent appends its output content directly after the orchestrator-written
section header already in the file. The agent does NOT write its own section header.

---

## Challenges & Open Questions

1. **Interactive agent process completion detection**: For interactive steps (`interactive: true`),
   the agent may forget to call the completion endpoint (user closes terminal). The orchestrator
   should treat process exit (code 0 or not) as completion signal for interactive steps, same as
   the existing `AgentRunner` behaviour.

2. **`{slug}` token resolution in `reviewFiles`**: The orchestrator resolves `{slug}` in
   `reviewFiles` paths using `PipelineRun.Slug` at the time the `ApprovalRequest` is created.
   If `Slug` is null, `{slug}` tokens are left as-is and the dashboard displays a warning.

3. **Concurrent runs of the same pipeline against the same repo**: The orchestrator does not
   enforce a "one active run per repo" constraint in Phase 1 (the old `_claimed` dict logic is
   not carried over). This is intentional — pipelines may run concurrently on different repos.
   A future phase can add per-repo concurrency control.

4. **StartupReconciler for orphaned step runs**: On restart, any `PipelineStepRun` with
   `Status=running` is transitioned to `failed`. The parent `PipelineRun` is also failed if
   no recovery is possible. `workflow-input.md` cleanup is best-effort.

5. **Log streaming for interactive steps**: In interactive mode (`UseShellExecute=true`),
   stdout is not redirected — log streaming via `PipelineRunLog` endpoint is the agent's
   responsibility. The `SubscribeToStepRun` hub method returns an empty tail for interactive
   steps (same as the existing interactive workflow behaviour).
