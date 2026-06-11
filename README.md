# Workflow Dashboard

A self-hosted dashboard for monitoring and controlling AI agent workflows (Copilot CLI orchestrator, architect, developer, code-review, etc.).

See [`docs/open-spec.md`](docs/open-spec.md) for the full feature specification.

---

## Architecture at a glance

| Layer | Tech |
|-------|------|
| Backend | ASP.NET Core 10 (REST + SignalR + EF Core) |
| Frontend | Angular 21 + Angular Material |
| Database | SQLite (file-backed, volume-mounted in Docker) |
| Real-time | SignalR hub at `/hubs/workflow` |
| Container | Multi-stage Docker image (`docker compose up`) |

The dashboard is an **active agent supervisor**: it owns repositories, discovers workflow/agent definitions from `~/.copilot/`, spawns `copilot` CLI processes directly, and streams their output over SignalR.

---

## Running on the host (recommended)

Running on the host is the **supported execution model** for agent launching. Only the host can access `~/.copilot/` and spawn `copilot` processes.

### Prerequisites

- .NET 10 SDK
- Node.js 22+
- `copilot` CLI on PATH
- `~/.copilot/workflows/` and `~/.copilot/agents/` populated (see `docs/copilot-defaults/` for reference files and install instructions)

### Option 1 — Visual Studio (F5)

1. Open `WorkflowDashboard.slnx` in Visual Studio 2026+.
2. Press **F5** with the `http` (or `https`) launch profile.
3. The API starts on `http://localhost:5165` and SpaProxy launches the Angular dev server on `http://localhost:4200` automatically.

### Option 2 — CLI

In two separate terminals:

```bash
# Terminal 1 — backend
dotnet run --project src/WorkflowDashboard.Api

# Terminal 2 — frontend (proxies /api and /hubs to backend on :5165)
cd src/workflow-dashboard-ui
npm install
npm start
```

Then open `http://localhost:4200`.

---

## Running in Docker

```bash
docker compose up --build
```

The dashboard becomes available at `http://localhost:5080`. SQLite data persists in the `dashboard-data` named volume.

> ⚠️ **Agent execution is disabled in Docker by default** (`AgentRunner:Enabled=false`).
> The dashboard is read-only in Docker — it will not spawn `copilot` processes. Use the host execution model above to enable agent launching.

The optional `~/.copilot` bind mount in `docker-compose.yml` (commented out by default) enables **Catalog browsing** inside Docker, so you can view your workflow and agent definitions without running on the host.

Override the host port via env var:

```bash
DASHBOARD_PORT=8080 docker compose up --build
```

### Database migrations

The dashboard applies EF Core migrations on startup (`db.Database.Migrate()`).
If you had a `workflow.db` file created by an earlier build (which used
`EnsureCreated()`), delete it once before the next run so the baseline
`InitialSchema` migration can create the schema and record itself in
`__EFMigrationsHistory`:

```bash
rm src/WorkflowDashboard.Api/workflow.db*
```

In Docker the SQLite file lives in the `dashboard-data` volume; remove the
volume (`docker compose down -v`) to reset it.

---

## Project layout

```
.
├── docs/
│   ├── open-spec.md           # Feature spec
│   └── copilot-defaults/      # Reference workflow/agent markdown files
│                              # (copy into ~/.copilot/ to get started)
├── Dockerfile                 # Multi-stage build (Angular → .NET → runtime)
├── docker-compose.yml
├── global.json                # Pins JavaScript SDK for Visual Studio
├── WorkflowDashboard.slnx     # Solution file
└── src/
    ├── WorkflowDashboard.Api/     # ASP.NET Core 10 API + SignalR + EF Core
    ├── WorkflowDashboard.Shared/  # WorkflowClient for workflow implementations
    └── workflow-dashboard-ui/     # Angular 21 SPA
```

---

## API surface

All REST endpoints live under `/api/*`. SignalR hub is at `/hubs/workflow`.

| Resource | Endpoints |
|----------|-----------|
| Features | `GET/POST /api/features`, `GET/PUT/DELETE /api/features/{id}`, `GET /api/features/{id}/spec` |
| Workflows | `GET/POST /api/workflows`, `GET /api/workflows/{id}`, `PUT /api/workflows/{id}/status`, `POST /api/workflows/launch`, `POST /api/workflows/{id}/cancel`, `POST /api/workflows/{id}/requeue`, `POST /api/workflows/cancel-all` |
| Repositories | `GET/POST /api/repositories`, `GET/PUT/DELETE /api/repositories/{id}`, `GET /api/repositories/{id}/specs` |
| Catalog | `GET /api/catalog`, `POST /api/catalog/refresh`, `GET /api/catalog/{slug}`, `GET /api/catalog/{slug}/source` |
| Agents | `GET/POST /api/agents`, `GET/PUT /api/agents/{id}` |
| Input requests | `GET/POST /api/inputrequests`, `GET/PUT /api/inputrequests/{id}` |
| Events | `GET/POST /api/events` |
| Dashboard | `GET /api/dashboard/summary` |

The SignalR hub broadcasts: `WorkflowUpdated`, `AgentUpdated`, `InputRequested`, `InputAnswered`, `EventLogged`, `RepositoryUpdated`, `CatalogRefreshed`, `FeatureUpdated`, `WorkflowLog`, `WorkflowLogTail`.

See [`docs/open-spec.md`](docs/open-spec.md) for full payloads and the PM-agent contract.

---

## Integrating a workflow

Reference the `WorkflowDashboard.Shared` library and use `WorkflowClient` inside your workflow markdown/script running under `copilot`:

```csharp
using var client = new WorkflowClient(
    Environment.GetEnvironmentVariable("WORKFLOW_DASHBOARD_API_URL")!,
    Environment.GetEnvironmentVariable("WORKFLOW_ID")!);

// Ask the user a question from the Input Requests view
var reqId = await client.RequestInputAsync("Use PostgreSQL or SQLite?",
    options: new[] { "postgres", "sqlite" });
var answer = await client.PollForAnswerAsync(reqId);

// On approval: create the feature with an inline spec
await client.CreateFeatureWithSpecAsync(
    repositoryId: Environment.GetEnvironmentVariable("WORKFLOW_ID")!,
    name: "user-auth",
    description: "JWT-based login",
    specSlug: "user-auth",
    specBody: markdownContent);

// Log a structured event
await client.LogEventAsync("pm_approved", "User approved the feature draft");
```

The full PM-agent contract is documented in [`docs/open-spec.md` §10.1](docs/open-spec.md#101-pm-agent-draft-flow-current-contract).

