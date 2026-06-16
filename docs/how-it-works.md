---
title: How It Works — Help
layout: default
---

# How It Works

This page explains the full technical flow of the Workflow Automation Dashboard — from clicking **Start run** to an agent committing code and signalling completion. Understanding this flow lets you write better agent definitions and debug problems when they occur.

---

## System architecture

```
┌──────────────────────────────────────────────────────────────┐
│                     Browser (Angular UI)                     │
│   Pipelines · Dashboard · Input Requests · Repositories …   │
└─────────────────────────┬────────────────────────────────────┘
						  │  REST API + SignalR (real-time)
┌─────────────────────────▼────────────────────────────────────┐
│            .NET 10 API  (ASP.NET Core)                       │
│                                                              │
│  Controllers  →  PipelineOrchestrator (BackgroundService)    │
│                         │                                    │
│            ┌────────────┴────────────┐                       │
│            │                         │                       │
│   InstructionsInjector        WorkflowInputWriter            │
│   (writes step instructions)  (writes context file)         │
│            │                         │                       │
│            └────────────┬────────────┘                       │
│                         │                                    │
│                   ProcessLauncher                            │
│              (spawns AI CLI process)                         │
│                                                              │
│  SQLite  ←─ EntityFramework Core                            │
└──────────────────────────────────────────────────────────────┘
						  │  spawns
┌─────────────────────────▼────────────────────────────────────┐
│        AI CLI agent (Copilot / Claude / Custom)              │
│   runs inside the target repository working directory        │
│   reads  {configured instructions file}                      │
│   reads  {configured input file}                             │
│   writes openspec/changes/{featureSlug}/ artifacts           │
│   commits via git                                            │
│   calls back  POST /api/pipeline-runs/{id}/steps/{id}/complete│
└──────────────────────────────────────────────────────────────┘
```

---

## Step-by-step run lifecycle

### 1. Creating the run (UI → API)

When you click **Start run** the UI posts to `POST /api/pipeline-runs` with the pipeline ID, repository, ticket number, initial instructions, and optional feature link.

The API:
1. Validates the repository is a valid git repo with at least one commit.
2. Creates (or checks out) the feature branch — e.g. `feature/TEST-42`.
3. Persists a `PipelineRun` row with `status = pending`.
4. Enqueues a `StartRun` message onto the `PipelineOrchestrator` channel.

---

### 2. Orchestrator picks it up

The `PipelineOrchestrator` is a .NET `BackgroundService` that processes messages one at a time from an in-memory channel.

On `StartRun`:
- If the same repository is already running a pipeline, the new run is set to `status = queued` and waits.
- Otherwise the run starts immediately.

The orchestrator writes two files to the repo (see next section) and then **spawns the AI CLI agent**.

---

### 3. Files written to the repository

Before spawning the agent process, the orchestrator writes two files into the target git repository:

#### `workflow-input.md` — context for all agents

Path: the configured `InputFileRelativePath` setting.
_Default for Copilot: `.github/copilot/workflow-input.md`. Default for Claude: `.claude/workflow-input.md`._

This file is written once per pipeline run and **accumulates information** as steps execute. It contains:

```markdown
# Pipeline Input

## Context
| Key           | Value          |
|---------------|----------------|
| Pipeline Run  | abc123         |
| Ticket Number | TEST-42        |
| Branch        | feature/TEST-42|
| API base URL  | http://…       |

## User Instructions
(whatever you typed into the run form)

## Linked Feature
(name, description, spec folder — if a feature was linked)

## [step-id] attempt N — by: agent-slug — for: next-step-id
(feedback appended here when a reviewer requests changes)
```

Every agent is instructed to read this file first. Reviewer feedback is appended as a new section each time a step loops back.

#### `active-workflow.instructions.md` — current step instructions

Path: the configured `InstructionsRelativePath` setting (default: `.github/instructions/active-workflow.instructions.md`).

This file is **overwritten** before each step starts. It contains:
- An autostart directive ("do not wait for user input, begin immediately")
- A context table with all run IDs and environment variable values
- The **OpenSpec folder rule** (see below)
- Step 1: read the input file
- Step 2: the agent's own markdown task definition (loaded from the catalog)
- Step 3: the PowerShell completion command (see below)

**Copilot:** reads `.github/instructions/` automatically on startup, so the agent receives its full task without any user interaction.

**Claude Code:** the instructions are written to `CLAUDE.md` in the repository root (set `InstructionsRelativePath` to `CLAUDE.md`), which Claude Code reads automatically on startup.

---

### 4. Spawning the AI CLI agent

The `ProcessLauncher` starts the configured AI CLI executable. The tool, executable, and flags are all configurable via the Settings page or `appsettings.json`.

#### Interactive mode (default)

A new **cmd.exe** terminal window opens. The CLI starts in interactive mode with an auto-start prompt:

```
# Copilot
copilot -i "Begin the workflow session. Read .github/copilot/workflow-input.md and follow the workflow instructions you have been given." --allow-all-tools

# Claude
claude -p "Begin the workflow session. Read .claude/workflow-input.md and follow the workflow instructions you have been given."
```

The agent reads the instructions file, does its work, commits, and calls the completion API — all autonomously. You can watch the terminal window but do not need to interact with it.

> **Note:** In interactive mode stdout/stderr are not captured — the log panel in the dashboard will show no output. This is by design; use headless mode if you want log streaming (see [Configuration](configuration)).

#### Headless mode (`InteractiveTerminal: false`)

The copilot process runs without a terminal window. stdout and stderr are streamed to the dashboard log panel in real time via SignalR.

#### Environment variables set on the process

| Variable | Value |
|---|---|
| `WORKFLOW_DASHBOARD_API_URL` | The API's base URL (e.g. `http://localhost:5000`) |
| `PIPELINE_RUN_ID` | ID of the current pipeline run |
| `STEP_RUN_ID` | ID of the current step run row |
| `STEP_ID` | The step's logical ID from the pipeline definition |
| `ATTEMPT_NUMBER` | `1` on first run, increments on feedback loops |
| `REPOSITORY_PATH` | Absolute path of the repository on disk |
| `FEATURE_ID` | ID of the linked feature, or empty string |

These are also echoed in the instructions file so the agent can reference them even if it cannot read environment variables.

---

### 5. What the agent does

A well-behaved agent follows this sequence after reading its instructions:

1. **Read** the configured input file (default: `.github/copilot/workflow-input.md` for Copilot, `.claude/workflow-input.md` for Claude) for context and any prior feedback.
2. **Do its domain work** — write files, run commands, make API calls.
3. **Write all output artifacts** to `openspec/changes/{featureSlug}/` (see OpenSpec convention below).
4. **Commit** all changes:
   ```powershell
   git add -A
   git commit -m "TEST-42 feat: PM draft complete"
   ```
5. **Signal completion** by calling the dashboard API (see next section).
6. **Exit** the session.

---

### 6. Agent completion protocol

The agent signals it is done by making a POST request back to the dashboard. The URL is injected into the instructions file so the agent always has it.

#### Standard completion (advance to next step)

```powershell
$body = '{"decision":"approved"}'
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5000/api/pipeline-runs/{runId}/steps/{stepRunId}/complete" `
  -Body $body -ContentType 'application/json'
```

#### Reviewer agent — request changes (loop back)

A step with **Can give feedback** enabled can instead write a review file and send feedback:

```powershell
# 1. Write detailed review to: openspec/changes/{featureSlug}/reviews/{stepId}-review.md
# 2. Signal feedback:
$feedback = "Review findings: openspec/changes/{featureSlug}/reviews/code-review-review.md"
$body = "{`"decision`":`"feedback`",`"feedbackText`":`"$feedback`"}"
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5000/api/pipeline-runs/{runId}/steps/{stepRunId}/complete" `
  -Body $body -ContentType 'application/json'
```

When feedback is received:
- The feedback text is appended to `workflow-input.md` as a new section.
- The run jumps back to the step defined in **Return to step on feedback**.
- `ATTEMPT_NUMBER` is incremented for the target step.
- The target agent's instructions file is rewritten with a **retry callout** that tells it to find and address all findings in the review file.

---

### 7. User approval steps

When a **User Approval** step is reached the run pauses (`status = waiting_approval`) and an `ApprovalRequest` row is created. A badge appears on the notification bell in the toolbar.

Expand the run panel and either:
- **Approve** → run advances to the next step.
- **Reject with feedback** → feedback text is appended to `workflow-input.md` and the run loops back (if **Return to step** is configured).

---

### 8. Pipeline completion

When the last step is completed (either by agent or approval), the run is marked `status = completed` and the repository slot is freed, allowing the next queued run for that repo to start.

---

## The OpenSpec convention

All pipeline output artifacts are stored under a predictable path inside the repository:

```
openspec/
└── changes/
	└── {featureSlug}/          ← e.g. "test-42"
		├── proposal.md
		├── design.md
		├── tasks.md
		└── reviews/
			├── code-review-review.md
			└── plan-reviewer-review.md
```

`featureSlug` is the ticket number lowercased (e.g. `TEST-42` → `test-42`). Agents are strictly instructed to use this exact folder name so output is predictable and reviewers can always find prior work.

---

## Feedback loop diagram

```
Step A (agent)  →  Step B (reviewer, canGiveFeedback=true, returnTo=A)
						 │
				  ┌──────┴──────────────┐
				  │ approved            │ feedback
				  ▼                     ▼
			  Step C              workflow-input.md
		   (next step)            gets new section
									   │
									   ▼
								   Step A (attempt 2)
								  reads feedback, revises
									   │
									   ▼
								   Step B (attempt 2)
								  re-reviews ...
```

---

[← Back to help home](index)  
[→ Configuration reference](configuration)
