# OpenSpec: Workflow Dashboard

## 1. Overview

### 1.1 Problem Statement

When running AI-powered development workflows (using GitHub Copilot CLI agents like orchestrator, architect, developer, code-review), there is no visibility into:
- What agents are currently running and what they're doing
- Which workflows are waiting for user input
- The overall status of features being developed
- Historical activity and event logs

There is also no control mechanism to start, pause, cancel, or retry workflows from a central UI.

### 1.2 Solution

A self-hosted web dashboard that provides real-time monitoring and control of AI agent workflows. It runs as a Docker container and exposes a REST API that agents call to report their state.

### 1.3 Goals

- **Visibility**: See all running agents, their current tasks, and workflow states at a glance
- **Control**: Start new workflows, pause, resume, cancel, or retry from the UI
- **Notifications**: Browser push notifications when workflows need user input
- **Decoupled**: Agents communicate via REST API (no direct DB coupling)
- **Self-contained**: Single Docker image, single `docker compose up`

### 1.4 Non-Goals (for v1)

- Authentication / multi-user support
- Remote access (localhost only)
- Persistent agent logs (beyond event stream)
- Integration with GitHub Issues/PRs (future)

---

## 2. Architecture

### 2.1 System Diagram

```
┌─── Docker ──────────────────────────────────┐
│                                              │
│  ┌────────────────────────────────────────┐  │
│  │  ASP.NET Core 10 Container             │  │
│  │  - Serves Angular 21 static files      │  │
│  │  - REST API  (configurable port)       │  │
│  │  - SignalR Hub (real-time push)         │  │
│  │  - Background polling service          │  │
│  └───────────────┬────────────────────────┘  │
│                  │                            │
│                  ▼                            │
│         /data/workflow.db  (volume)           │
│                                              │
└──────────────────────────────────────────────┘
        ▲                          ▲
        │ REST API                 │ Browser (SignalR + HTTP)
        │                          │
┌───────┴────────┐        ┌───────┴────────┐
│ Copilot Agents │        │ User Browser   │
│ (host machine) │        │ localhost:5080 │
└────────────────┘        └────────────────┘
```

### 2.2 Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Backend | ASP.NET Core | .NET 10 |
| Frontend | Angular + TypeScript | Angular 21 |
| Real-time | SignalR | (bundled with ASP.NET Core) |
| Database | SQLite via EF Core | latest |
| Containerization | Docker + Docker Compose | latest |
| UI Components | Angular Material | v21 (matching Angular) |

### 2.3 Communication Patterns

- **Agents → API**: REST calls to report state, log events, request input
- **API → Browser**: SignalR for real-time updates (state changes, new events, input requests)
- **Browser → API**: REST calls for control actions (start, pause, cancel)
- **Internal**: Background polling service watches DB for changes → broadcasts via SignalR

---

## 3. Data Model

### 3.1 Database: `workflow.db` (SQLite)

#### features
Represents a high-level feature being developed.

| Column | Type | Description |
|--------|------|-------------|
| id | TEXT PK | Unique identifier (kebab-case) |
| name | TEXT | Human-readable feature name |
| description | TEXT | Feature description |
| status | TEXT | backlog \| planning \| in_progress \| review \| done \| cancelled |
| priority | INTEGER | Sort priority (0 = default) |
| spec_path | TEXT | Relative path (inside `Specs:RootDir`) to the markdown feature description file — set by the feature-spec workflow |
| created_at | DATETIME | Creation timestamp |
| updated_at | DATETIME | Last update timestamp |

#### workflows
A workflow instance. One feature may have multiple workflow runs.

| Column | Type | Description |
|--------|------|-------------|
| id | TEXT PK | Unique identifier |
| feature_id | TEXT FK | References features.id (nullable for standalone workflows). For `feature-spec` workflows this is set at the end, when the PM agent has produced a Feature. |
| type | TEXT | full-pipeline \| bugfix \| review-only \| feature-spec \| custom |
| status | TEXT | pending \| running \| paused \| waiting_input \| completed \| failed \| cancelled |
| started_at | DATETIME | When workflow started |
| completed_at | DATETIME | When workflow finished |
| error_message | TEXT | Error details if failed |
| created_at | DATETIME | Creation timestamp |

#### agents
Individual agent executions within a workflow.

| Column | Type | Description |
|--------|------|-------------|
| id | TEXT PK | Unique identifier |
| workflow_id | TEXT FK | References workflows.id |
| agent_type | TEXT | orchestrator \| architect \| developer \| code-review \| pm \| plan-reviewer |
| status | TEXT | idle \| running \| waiting_input \| completed \| failed |
| current_task | TEXT | Human-readable description of current activity |
| started_at | DATETIME | When agent started |
| completed_at | DATETIME | When agent finished |
| session_id | TEXT | Copilot CLI session ID (for cross-reference) |

#### input_requests
Questions from agents that require user answers.

| Column | Type | Description |
|--------|------|-------------|
| id | TEXT PK | Unique identifier |
| workflow_id | TEXT FK | References workflows.id |
| agent_id | TEXT FK | References agents.id |
| question | TEXT | The question being asked |
| options_json | TEXT | JSON array of choices (optional) |
| response | TEXT | User's answer |
| status | TEXT | pending \| answered \| expired |
| created_at | DATETIME | When question was asked |
| answered_at | DATETIME | When user responded |

#### commands
Control commands from the UI to the orchestrator.

| Column | Type | Description |
|--------|------|-------------|
| id | TEXT PK | Unique identifier |
| workflow_id | TEXT FK | References workflows.id (nullable for global commands) |
| command_type | TEXT | start \| pause \| resume \| cancel \| retry |
| payload_json | TEXT | Additional data (e.g., workflow config for start) |
| status | TEXT | pending \| processing \| completed \| failed |
| created_at | DATETIME | When command was issued |
| processed_at | DATETIME | When command was handled |

#### events
Activity log for the real-time event stream.

| Column | Type | Description |
|--------|------|-------------|
| id | INTEGER PK | Auto-increment |
| workflow_id | TEXT | References workflows.id (nullable) |
| agent_id | TEXT | References agents.id (nullable) |
| event_type | TEXT | state_change \| log \| error \| input_requested \| command_received |
| message | TEXT | Human-readable event description |
| metadata_json | TEXT | Additional structured data |
| created_at | DATETIME | Event timestamp |

---

## 4. API Design

### 4.1 REST Endpoints

#### Features
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/features | List all features (with optional status filter) |
| GET | /api/features/{id} | Get feature details |
| POST | /api/features | Create a new feature |
| PUT | /api/features/{id} | Update feature |
| DELETE | /api/features/{id} | Delete feature |
| GET | /api/features/{id}/spec | Read the markdown feature description file. Returns `{ featureId, specPath, content, fullPath }`. 404 if no spec has been written. |
| POST | /api/features/{id}/spec | Body `{ content, fileName? }`. Writes markdown to `Specs:RootDir/{fileName}` (defaults to `{id}.md`) and updates `Feature.SpecPath`. Used by the PM agent when finalising a feature-spec workflow. |

#### Workflows
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/workflows | List workflows (filterable by status, feature) |
| GET | /api/workflows/{id} | Get workflow details (includes agents) |
| POST | /api/workflows | Create and start a new workflow |
| PUT | /api/workflows/{id}/status | Update workflow status. Body `{ status, errorMessage?, featureId? }`. The optional `featureId` lets agents (e.g. the PM agent) link the workflow to a feature it produced at completion time. |

#### Agents
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/agents | List all agents (filterable by status) |
| GET | /api/agents/{id} | Get agent details |
| POST | /api/agents | Register a new agent |
| PUT | /api/agents/{id} | Update agent status/current task |

#### Input Requests
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/input-requests | List pending input requests |
| GET | /api/input-requests/{id} | Get input request details |
| POST | /api/input-requests | Create input request (agent asks a question) |
| PUT | /api/input-requests/{id} | Answer an input request |

#### Commands
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/commands?status=pending | Poll for pending commands |
| POST | /api/commands | Issue a command (from UI) |
| PUT | /api/commands/{id} | Mark command as processed |

#### Events
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/events | List events (paginated, filterable) |
| POST | /api/events | Log a new event |

### 4.2 SignalR Hub

**Hub endpoint**: `/hubs/workflow`

**Server → Client events:**
| Event | Payload | Trigger |
|-------|---------|---------|
| WorkflowUpdated | Workflow object | Status change on any workflow |
| AgentUpdated | Agent object | Agent status/task change |
| InputRequested | InputRequest object | New input request created |
| InputAnswered | InputRequest object | Input request answered |
| CommandIssued | Command object | New command from UI |
| EventLogged | Event object | New event in the log |

---

## 5. Frontend Views

### 5.1 Dashboard Home (`/`)
- **Summary cards**: Running workflows, active agents, pending inputs, failed count
- **Recent activity**: Last 20 events in a live-updating list
- **Quick actions**: Start new workflow button

### 5.2 Features View (`/features`)
- Table/Kanban of all features with status columns
- Click to drill into feature → shows associated workflows
- Inline status editing
- **View spec** action (only visible when `spec_path` is set) opens an inline panel rendering the markdown feature description that the PM agent wrote during the `feature-spec` workflow

### 5.3 Workflows View (`/workflows`)
- Filterable table: status, type, feature, date range
- Each row expandable to show child agents
- Expanded panel also shows the full **dialog history** (numbered Q&A timeline) for any input requests the workflow has made — particularly important for `feature-spec` workflows where the PM↔user dialog is the workflow
- Status badges with color coding

### 5.4 Agents View (`/agents`)
- Live list of all agents with: type, status, current task, duration
- Auto-refreshes via SignalR
- Visual indicators for waiting-for-input agents

### 5.5 Input Requests View (`/inputs`)
- List of pending questions with agent context
- Answer form inline (text input or option selection)
- Browser notification badge count

### 5.6 Control Panel (`/control`)
- **Draft a feature** card: rough description textarea + start button → spawns a `feature-spec` workflow for the PM agent to pick up
- Start arbitrary workflow form (select type, link to feature, configure)
- Bulk actions: cancel all, pause all
- Command history log

### 5.7 Event Log (`/events`)
- Real-time scrolling event stream
- Filter by: workflow, agent, event type, date range
- Search within messages

---

## 6. Configuration

### 6.1 appsettings.json

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://+:5080"
      }
    }
  },
  "ConnectionStrings": {
    "WorkflowDb": "/data/workflow.db"
  },
  "Polling": {
    "IntervalSeconds": 3
  },
  "Specs": {
    "RootDir": "docs/features"
  },
  "Notifications": {
    "Enabled": true
  }
}
```

`Specs:RootDir` is the directory (relative to ContentRoot, or absolute) where the dashboard writes the markdown spec files produced by `feature-spec` workflows. Defaults to `docs/features` so files live alongside the rest of the repo's documentation and are committable to git. In Docker, the directory is bind-mounted from the host.

### 6.2 Environment Variable Overrides

All settings can be overridden via environment variables in docker-compose:
- `Kestrel__Endpoints__Http__Url`
- `ConnectionStrings__WorkflowDb`
- `Polling__IntervalSeconds`
- `Specs__RootDir`

---

## 7. Docker

### 7.1 Dockerfile (multi-stage)

| Stage | Base Image | Purpose |
|-------|-----------|---------|
| 1 | node:22-alpine | Build Angular 21 app |
| 2 | mcr.microsoft.com/dotnet/sdk:10.0 | Build .NET 10 API |
| 3 | mcr.microsoft.com/dotnet/aspnet:10.0 | Runtime (serves both API + static files) |

### 7.2 docker-compose.yml

- Single service: `dashboard`
- Port mapping: `5080:5080` (configurable)
- Named volume: `dashboard-data` → `/data/`
- Restart policy: `unless-stopped`

---

## 8. Implementation Plan

### Phase 1: Foundation
1. **Create .NET solution structure** — API project, Shared library project
2. **Define EF Core DbContext & entities** — All 6 tables with migrations
3. **Scaffold REST controllers** — CRUD for all entities
4. **Basic Angular project setup** — Angular 21, Angular Material, routing

### Phase 2: Core API & Real-Time
5. **Implement SignalR hub** — WorkflowHub with all server→client events
6. **Implement background polling service** — Detects DB changes, triggers SignalR
7. **Implement Angular SignalR service** — Connect, reconnect, event handling
8. **Browser notifications service** — Request permission, show notifications on input requests

### Phase 3: Frontend Views
9. **Dashboard home** — Summary cards + recent activity stream
10. **Features view** — Table with CRUD
11. **Workflows view** — Table with agent drill-down
12. **Agents view** — Live agent list
13. **Input requests view** — Pending questions with answer forms
14. **Control panel** — Start/cancel/pause workflows
15. **Event log** — Filterable real-time stream

### Phase 4: Integration & Docker
16. **WorkflowClient shared library** — Helper class agents use to call the API
17. **Dockerfile** — Multi-stage build
18. **docker-compose.yml** — Full configuration
19. **Integration testing** — End-to-end workflow: start → agent reports → input → answer → complete
20. **README** — Setup instructions, API docs, usage guide

---

## 9. Success Criteria

- [ ] `docker compose up` starts the dashboard, accessible at `localhost:5080`
- [ ] Angular UI shows real-time agent/workflow state updates
- [ ] Agents can register, report status, and request input via REST API
- [ ] User can answer input requests in the browser
- [ ] User can start/cancel workflows from the control panel
- [ ] Browser notifications fire when input is requested
- [ ] All state survives container restarts (volume-mounted SQLite)

---

## 10. Workflow Types

The dashboard hosts multiple flavours of workflow. New types can be added at any time — the `type` field is a free-form string so agents and the UI can collaborate on whatever conventions make sense.

### 10.1 `feature-spec` — Drafting a feature with the PM agent

Use this when you have a rough idea and want a PM agent to help you turn it into a proper feature description (markdown file) before any code is written. The implementation workflow (e.g. `full-pipeline`) is a separate, manually-started step.

**Lifecycle:**

```
User                         Dashboard                        PM agent
 │                                │                              │
 │ Control → "Draft a feature"     │                              │
 ├──────────────────────────────▶ │                              │
 │ (rough description)             │ Workflow{type:feature-spec} │
 │                                │ Command{type:start,          │
 │                                │   payloadJson:{name,desc}}   │
 │                                │ ─────────────────────────▶ │
 │                                │                              │ polls /api/commands
 │                                │                              │ marks command processed
 │                                │                              │ registers itself as agent
 │                                │                              │
 │                                │ ◀── RequestInput("How should…") ─
 │ Inputs view (or notification)   │                              │
 │ ◀── pending input ───────────  │                              │
 │ types answer                    │                              │
 ├──────────────────────────────▶ │ PollForAnswer → answered    │
 │                                │ ─────────────────────────▶ │
 │                                │       (loop until satisfied) │
 │                                │                              │
 │                                │ ◀─ RequestInput("Approve?") ─
 │ "yes"                           │                              │
 ├──────────────────────────────▶ │ ─────────────────────────▶ │
 │                                │                              │ POST /api/features
 │                                │                              │ POST /api/features/{id}/spec
 │                                │                              │   (writes docs/features/{id}.md)
 │                                │                              │ PUT  /api/workflows/{id}/status
 │                                │                              │   {status:completed, featureId}
 │                                │                              │
 │ Features view: new entry         │                              │
 │ with "View spec" button         │                              │
```

**PM agent contract:**

1. Poll `/api/commands?status=pending` until a `start` command for the PM's domain arrives. The `payloadJson` is `{ "name": string | null, "description": string }` from the Control panel.
2. Mark the command as `processing`, then register an agent via `POST /api/agents` with `agentType: "pm"` and the workflow id.
3. Set workflow status to `running` (`PUT /api/workflows/{id}/status`).
4. Drive a dialog using `POST /api/input-requests` for each question and polling `GET /api/input-requests/{id}` for the user's response. Optionally ask the user to "Approve final draft?" before committing.
5. On approval:
   - `POST /api/features` to create the feature (a sensible `name` derived from the dialog; description = a one-paragraph summary; status = `planning`).
   - `POST /api/features/{id}/spec` with the full markdown body. The dashboard writes the file under `Specs:RootDir/{id}.md` (or a custom `fileName` if provided) and sets `Feature.SpecPath`.
   - `PUT /api/workflows/{id}/status` with `{ status: "completed", featureId: <new feature id> }`.
6. On user cancellation: `PUT /api/workflows/{id}/status` with `{ status: "cancelled" }`. Do not write a spec file or create a feature.

The `WorkflowClient` shared library has helpers for all of this: `PollCommands`, `MarkCommandProcessed`, `RegisterAgent`, `UpdateAgentStatus`, `RequestInput`, `PollForAnswer`, `CreateFeature`, `SaveFeatureSpec`, `UpdateWorkflowStatus(workflowId, status, featureId: …)`.

### 10.2 `full-pipeline` — Implementing a feature

The original workflow type. Started against an existing feature (typically one that already has a `SpecPath`). Runs the architect → developer → code-review chain against the spec.

### 10.3 Other types

- `bugfix` — minimal pipeline for a single bug
- `review-only` — runs code review against an existing branch
- `custom` — escape hatch for ad-hoc workflows where the orchestrator decides the pipeline

---

## 11. Future Enhancements (out of scope for v1)

- Authentication (JWT/OAuth)
- GitHub Issues/PR integration
- Agent log streaming (full agent conversation visible in dashboard)
- Workflow templates (predefined pipeline configurations)
- Metrics & analytics (avg time per phase, agent utilization)
- Multi-node support (multiple dashboard instances sharing state)
- Dark/light theme toggle
