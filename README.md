# Workflow Automation Dashboard

A self-hosted dashboard for orchestrating AI agent pipelines using the Copilot CLI. The dashboard is the **orchestrator** — it manages pipeline runs, creates git branches, spawns Copilot CLI agents in terminal windows, and handles step-to-step handoffs including user approval gates.

---

## Architecture

| Layer | Tech |
|-------|------|
| Backend | ASP.NET Core 10 (REST + SignalR + EF Core/SQLite) |
| Frontend | Angular 21 + Angular Material |
| Real-time | SignalR hub at `/hubs/workflow` |
| Agent executor | Copilot CLI (`copilot` on PATH) |
| Git integration | `git` on PATH — branch creation and commit enforcement |

### How it works

1. You define a **Pipeline** in the designer (sequence of agent steps and user approval steps).
2. You start a **Pipeline Run** — providing a ticket number (e.g. `TEST-42`), branch prefix, repository, and optional initial instructions.
3. The dashboard creates a git branch (`feature/TEST-42`), writes `workflow-input.md` as the shared memory file, and launches the first agent in a terminal window.
4. Each agent reads `workflow-input.md`, does its domain work, commits, then calls the completion REST API.
5. The dashboard advances to the next step — either another agent or a user approval gate shown in the UI.
6. Reviewer agents write findings to `openspec/changes/{slug}/reviews/{stepId}-review.md` and can loop steps back for revision.

### Agent definitions

Agent `.md` files contain **domain content only** (persona, what to produce, templates, rules). All pipeline plumbing — env vars, git commit instructions, completion API calls, review file paths — is injected at runtime by the backend.

Agent definitions live in `~/.copilot/agents/`. See `docs/copilot-defaults/` for reference files and install instructions.

---

## Prerequisites

- .NET 10 SDK
- Node.js 22+
- `git` on PATH
- `copilot` CLI on PATH
- Agent definitions installed to `~/.copilot/agents/` (see `docs/copilot-defaults/`)

---

## Running

### Option 1 — Visual Studio (F5)

1. Open `WorkflowDashboard.slnx` in Visual Studio 2022+.
2. Press **F5** with the `http` or `https` launch profile.
3. The API starts on `http://localhost:5165`; the Angular dev server starts automatically on `http://localhost:4200`.

### Option 2 — CLI

```bash
# Terminal 1 — backend
dotnet run --project src/WorkflowDashboard.Api

# Terminal 2 — frontend (proxies /api and /hubs to :5165)
cd src/workflow-dashboard-ui
npm install
npm start
```

Open `http://localhost:4200`.

### Database

EF Core migrations run automatically on startup. To reset the database:

```bash
rm src/WorkflowDashboard.Api/workflow.db*
```

---

## Project layout

```
.
├── docs/
│   ├── open-spec.md                # Feature specification
│   └── copilot-defaults/           # Reference agent .md files
│       ├── README.md               # Agent architecture + install instructions
│       └── agents/                 # pm, architect, developer, code-review, etc.
├── WorkflowDashboard.slnx
└── src/
    ├── WorkflowDashboard.Api/      # ASP.NET Core 10 — REST + SignalR + orchestrator
    └── workflow-dashboard-ui/      # Angular 21 SPA
```

---

## API surface

All REST endpoints are under `/api/*`. SignalR hub at `/hubs/workflow`.

| Resource | Key endpoints |
|----------|---------------|
| Pipelines | `GET/POST /api/pipelines`, `GET/PUT/DELETE /api/pipelines/{id}` |
| Pipeline runs | `GET/POST /api/pipeline-runs`, `GET /api/pipeline-runs/{id}`, `POST /api/pipeline-runs/{id}/cancel` |
| Step completion | `POST /api/pipeline-runs/{runId}/steps/{stepRunId}/complete` |
| Approvals | `POST /api/pipeline-runs/{runId}/approvals/{approvalId}/decide` |
| Repositories | `GET/POST /api/repositories`, `GET/PUT/DELETE /api/repositories/{id}` |
| Features | `GET/POST /api/features`, `GET/PUT/DELETE /api/features/{id}` |
| Catalog | `GET /api/catalog`, `POST /api/catalog/refresh`, `GET /api/catalog/{slug}` |
| Dashboard | `GET /api/dashboard/summary` |

SignalR events: `PipelineRunUpdated`, `StepRunUpdated`, `ApprovalRequested`, `ApprovalDecided`, `RepositoryUpdated`, `CatalogRefreshed`, `StepLog`, `StepLogTail`.

---

## OpenSpec artifact layout

Each pipeline run writes artifacts under the linked repository:

```
{repo}/
├── .github/copilot/
│   └── workflow-input.md              # Shared memory — read by every agent
└── openspec/changes/{ticket-slug}/
    ├── proposal.md                    # PM output
    ├── design.md                      # Architect output
    ├── tasks.md                       # Architect output
    ├── specs/                         # Requirements and scenarios (flat)
    └── reviews/                       # Reviewer agent findings
        └── {stepId}-review.md
```

`workflow-input.md` persists for the entire pipeline run. Each agent appends its output under a structured header so the next agent always has full context.

