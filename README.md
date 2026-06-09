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

Agents call REST endpoints to register, report status, and request input. The backend broadcasts changes to all connected browsers via SignalR.

---

## Running locally

### Option 1 — Visual Studio (F5)

1. Open `WorkflowDashboard.slnx` in Visual Studio 2026+.
2. Ensure Node.js 22+ is installed.
3. Press **F5** with the `http` (or `https`) launch profile.
4. The API starts on `http://localhost:5165` and SpaProxy launches the Angular dev server on `http://localhost:4200` automatically (a `cmd` window will appear running `npm start`).
5. The browser opens at the API URL; SpaProxy transparently forwards SPA traffic to Angular.

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

Override the host port via env var:

```bash
DASHBOARD_PORT=8080 docker compose up --build
```

---

## Project layout

```
.
├── docs/open-spec.md              # Feature spec
├── Dockerfile                     # Multi-stage build (Angular → .NET → runtime)
├── docker-compose.yml
├── global.json                    # Pins JavaScript SDK for Visual Studio
├── WorkflowDashboard.slnx         # Solution file
└── src/
    ├── WorkflowDashboard.Api/     # ASP.NET Core 10 API + SignalR + EF Core
    ├── WorkflowDashboard.Shared/  # WorkflowClient for agents
    └── workflow-dashboard-ui/     # Angular 21 SPA
```

---

## API surface

All REST endpoints live under `/api/*`. SignalR hub is at `/hubs/workflow`.

| Resource | Endpoints |
|----------|-----------|
| Features | `GET/POST /api/features`, `GET/PUT/DELETE /api/features/{id}`, `GET/POST /api/features/{id}/spec` |
| Workflows | `GET/POST /api/workflows`, `GET /api/workflows/{id}`, `PUT /api/workflows/{id}/status` |
| Agents | `GET/POST /api/agents`, `GET/PUT /api/agents/{id}` |
| Input requests | `GET/POST /api/inputrequests`, `GET/PUT /api/inputrequests/{id}` |
| Commands | `GET/POST /api/commands`, `PUT /api/commands/{id}` |
| Events | `GET/POST /api/events` |
| Dashboard | `GET /api/dashboard/summary` |

The SignalR hub broadcasts: `WorkflowUpdated`, `AgentUpdated`, `InputRequested`, `InputAnswered`, `CommandIssued`, `EventLogged`.

See [`docs/open-spec.md`](docs/open-spec.md#4-api-design) for full payloads.

---

## Integrating an agent

Reference the `WorkflowDashboard.Shared` library and use `WorkflowClient`:

```csharp
var client = new WorkflowClient("http://localhost:5080");

var workflow = await client.CreateWorkflow("full-pipeline", featureId: "auth");
var agent = await client.RegisterAgent(workflow.Id, "architect", sessionId);

await client.UpdateAgentStatus(agent.Id, "running", "Designing schema");
var req = await client.RequestInput(workflow.Id, agent.Id, "Use PostgreSQL or SQLite?", new[] { "postgres", "sqlite" });

// Poll for the user's answer
while (true)
{
    var answered = await client.PollForAnswer(req.Id);
    if (answered is not null) { Console.WriteLine(answered.Response); break; }
    await Task.Delay(2000);
}

await client.UpdateWorkflowStatus(workflow.Id, "completed");
```

---

## Workflow types

The dashboard hosts two main workflow types out of the box:

### Implementing a feature (`full-pipeline`)
The original flow — runs orchestrator → architect → developer → code-review against an existing feature description. Start from the Control panel or directly via `POST /api/workflows`.

### Drafting a feature (`feature-spec`)
A two-stage approach where the **PM agent** dialogs with the user to author a feature description *before* any code is written.

1. From the **Control panel** → **Draft a feature with the PM agent** card, the user submits a rough idea.
2. A `feature-spec` workflow is created. The PM agent picks it up by polling `/api/commands`.
3. The PM agent asks follow-up questions via `POST /api/input-requests`; the user answers from the **Input requests** view (also surfaced as browser notifications).
4. When the user approves the final draft, the PM agent:
   - creates the `Feature` row,
   - saves the markdown spec via `POST /api/features/{id}/spec` (the dashboard writes it to `docs/features/{id}.md` by default and stores the relative path in `Feature.SpecPath`),
   - completes the workflow with the new `featureId` attached.
5. The new feature shows up in the **Features** view with a **View spec** action that renders the markdown in the dashboard. From there, the user can manually kick off a `full-pipeline` workflow against it.

The full Q&A dialog stays visible inside the `feature-spec` workflow's expansion panel on the **Workflows** view.

Example PM agent loop (using `WorkflowClient`):

```csharp
var commands = await client.PollCommands();
foreach (var cmd in commands.Where(c => c.WorkflowId is not null))
{
    await client.MarkCommandProcessed(cmd.Id, "processing");
    var pm = await client.RegisterAgent(cmd.WorkflowId!, "pm");
    await client.UpdateWorkflowStatus(cmd.WorkflowId!, "running");

    // …dialog loop using RequestInput / PollForAnswer …

    var feature = await client.CreateFeature(name: "user-auth", description: "JWT-based login", status: "planning");
    await client.SaveFeatureSpec(feature.Id, markdownContent);
    await client.UpdateWorkflowStatus(cmd.WorkflowId!, "completed", featureId: feature.Id);
    await client.MarkCommandProcessed(cmd.Id, "completed");
}
```

The full PM agent contract is documented in [`docs/open-spec.md` §10.1](docs/open-spec.md#101-feature-spec--drafting-a-feature-with-the-pm-agent).
