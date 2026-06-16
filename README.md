# Workflow Automation Dashboard

A self-hosted dashboard for orchestrating AI agent pipelines using Copilot or Claude CLI. The dashboard is the **orchestrator** — it manages pipeline runs, creates git branches, spawns AI CLI agents in terminal windows, and handles step-to-step handoffs including user approval gates. All the feature relevant docuemntation is using the openspec pattern.

> 📖 **Detailed documentation:** [aicooding.github.io/WorkflowAutomationDashboard](https://aicooding.github.io/WorkflowAutomationDashboard/)



---

## Architecture

| Layer | Tech |
|-------|------|
| Backend | ASP.NET Core 10 (REST + SignalR + EF Core/SQLite) |
| Frontend | Angular 21 + Angular Material |
| Real-time | SignalR hub at `/hubs/workflow` |
| Agent executor | Configurable AI CLI — GitHub Copilot, Claude Code, or custom executable |
| Git integration | `git` on PATH — branch creation and commit enforcement |

### How it works

1. You define a **Pipeline** in the designer (sequence of agent steps and user approval steps).
2. You start a **Pipeline Run** — providing a ticket number (e.g. `TEST-42`), branch prefix, repository, and optional initial instructions.
3. The dashboard creates a git branch (`feature/TEST-42`), writes the input file (path varies by configured CLI tool) as the shared memory file, and launches the first agent in a terminal window.
4. Each agent reads the configured input file and instructions (Copilot: `.github/instructions/active-workflow.instructions.md`; Claude: `CLAUDE.md`), does its domain work, commits, then calls the completion REST API.
5. The dashboard advances to the next step — either another agent or a user approval gate shown in the UI.
6. Reviewer agents write findings to `openspec/changes/{slug}/reviews/{stepId}-review.md` and can loop steps back for revision.

### Agent definitions

Agent `.md` files contain **domain content only** (persona, what to produce, templates, rules). All pipeline plumbing — env vars, git commit instructions, completion API calls, review file paths — is injected at runtime by the backend.

Agent definitions live in `~/.copilot/agents/`. See `docs/copilot-defaults/` for reference files and install instructions.

---

### OpenSpec artifact layout

Each pipeline run writes artifacts under the linked repository:

```
{repo}/
├── {InputFileRelativePath}            # Shared memory — read by every agent
│   # (Copilot default: .github/copilot/workflow-input.md)
│   # (Claude  default: .claude/workflow-input.md)
└── openspec/changes/{ticket-slug}/
    ├── proposal.md                    # PM output
    ├── design.md                      # Architect output
    ├── tasks.md                       # Architect output
    ├── specs/                         # Requirements and scenarios (flat)
    └── reviews/                       # Reviewer agent findings
        └── {stepId}-review.md
```

`workflow-input.md` persists for the entire pipeline run. Each agent appends its output under a structured header so the next agent always has full context.

## Prerequisites

- .NET 10 SDK
- Node.js 22+
- `git` on PATH
- An AI CLI tool on PATH — GitHub Copilot CLI (`copilot`), Claude Code (`claude`), or a custom executable
- Agents folder configured in the Settings page
- Agent definitions installed to your agents directory (see `docs/copilot-defaults/`)

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

## Deployment

Publishing produces a self-contained single-file executable that bundles the API **and** serves the Angular frontend from its `wwwroot/` folder. No separate Node.js process or `npm start` is needed.

### Build and publish

Run the appropriate command from the repository root. The Angular frontend is built automatically as part of the publish step (requires Node.js on the build machine).

```bash
# Windows (x64)
dotnet publish src/WorkflowDashboard.Api -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish

# macOS — Intel (x64)
dotnet publish src/WorkflowDashboard.Api -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -o ./publish

# macOS — Apple Silicon (ARM64)
dotnet publish src/WorkflowDashboard.Api -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o ./publish

# Linux (x64)
dotnet publish src/WorkflowDashboard.Api -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

### Running the published executable

```bash
# Windows
./publish/WorkflowDashboard.Api.exe

# macOS / Linux (make executable first if needed)
chmod +x ./publish/WorkflowDashboard.Api
./publish/WorkflowDashboard.Api
```

The app listens on `http://localhost:5000` by default. Open that URL in a browser — the Angular frontend is served directly by the API.

### Publish output layout

```
publish/
├── WorkflowDashboard.Api(.exe)   # Single self-contained executable
├── workflow.db                   # SQLite database (created on first run)
└── wwwroot/                      # Angular frontend static files (excluded from exe bundle)
    ├── index.html
    └── ...
```

> **Note:** The `wwwroot/` folder must stay next to the executable — ASP.NET Core reads static files from disk.

### Configuration

Copy `src/WorkflowDashboard.Api/appsettings.json` next to the executable and adjust as needed:

| Setting | Default | Description |
|---|---|---|
| `ApiBaseUrl` | `http://localhost:5000` | URL agents use to call back the API. |
| `ConnectionStrings.WorkflowDb` | `Data Source=workflow.db` | Path to the SQLite database file. |
| `Catalog.AgentsDir` | _(none)_ | Absolute path to your agent definitions directory. Can be set via the Settings page. |
| `AgentRunner.CliTool` | `Copilot` | Which CLI tool to use: `Copilot`, `Claude`, or `Custom`. Configurable via Settings page. |
| `AgentRunner.Executable` | `copilot` | Name or absolute path of the CLI executable. |
| `AgentRunner.ExtraArgs` | `[]` | Optional extra arguments prepended to every CLI invocation. |
| `AgentRunner.InstructionsRelativePath` | `.github/instructions/active-workflow.instructions.md` | Where the per-step instructions file is written in each repo. |
| `AgentRunner.InputFileRelativePath` | `.github/copilot/workflow-input.md` | Where the cumulative pipeline context file is written. |
| `AgentRunner.InteractiveTerminal` | `true` | Opens a visible terminal window for each agent step. |
| `AgentRunner.Enabled` | `true` | Set to `false` to disable process spawning. |

To bind to a different port:

```bash
ASPNETCORE_URLS=http://localhost:8080 ./publish/WorkflowDashboard.Api
```

Remember to update `ApiBaseUrl` in `appsettings.json` to match.

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
| Settings | `GET /api/settings`, `PUT /api/settings`, `DELETE /api/settings` |
| Dashboard | `GET /api/dashboard/summary` |

SignalR events: `PipelineRunUpdated`, `StepRunUpdated`, `ApprovalRequested`, `ApprovalDecided`, `RepositoryUpdated`, `CatalogRefreshed`, `StepLog`, `StepLogTail`.

---



