---
title: Configuration — Help
layout: default
---

# Configuration

All server-side settings are controlled through `appsettings.json` (or `appsettings.Development.json` for local overrides) in the `WorkflowDashboard.Api` project.

---

## Full reference

```json
{
  "ApiBaseUrl": "http://localhost:5000",

  "ConnectionStrings": {
	"WorkflowDb": "Data Source=workflow.db"
  },

  "Catalog": {
	"WorkflowsDir": null,
	"AgentsDir": null
  },

  "AgentRunner": {
	"Enabled": true,
	"CopilotExecutable": "copilot",
	"CopilotArgs": [],
	"InstructionsRelativePath": ".github/instructions/active-workflow.instructions.md",
	"InteractiveTerminal": true,
	"InteractiveStartPrompt": "Begin the workflow session. Read `.github/copilot/workflow-input.md` and follow the workflow instructions you have been given.",
	"PersistLogsToRepo": false,
	"LogTailLines": 500,
	"LogBatchInterval": "00:00:01"
  }
}
```

---

## `ApiBaseUrl`

The public URL agents use to call back to the dashboard API when signalling step completion.

- Must be reachable from the machine running the agent process.
- Defaults to `http://localhost:5000`.
- If the API listens on a different port (check `launchSettings.json`), update this value.
- If agents run on a different machine or inside a container, set this to the network-accessible address.

> **Common problem:** If agents report that the completion API is unreachable, this is almost always the wrong value for `ApiBaseUrl`.

---

## `ConnectionStrings.WorkflowDb`

SQLite connection string. The default `Data Source=workflow.db` places the database in the API's working directory. Use an absolute path to store it elsewhere:

```json
"WorkflowDb": "Data Source=C:\\Data\\workflow.db"
```

---

## `Catalog`

| Key | Description |
|---|---|
| `WorkflowsDir` | Absolute path to the folder containing workflow catalog entries (`.md` files). Leave `null` to disable. |
| `AgentsDir` | Absolute path to the folder containing agent catalog entries (`.md` files). Leave `null` to disable. |

The catalog is scanned on startup and can be refreshed manually from the [Agents](agents) page.

---

## `AgentRunner`

### `Enabled`

Set to `false` to prevent the server from spawning any agent processes. Useful in environments where the `copilot` CLI is not installed or not on the system PATH.

When disabled, attempting to start a pipeline run will immediately fail the first agent step.

---

### `CopilotExecutable`

The name or full path of the GitHub Copilot CLI executable.

| Value | When to use |
|---|---|
| `copilot` | Default. Resolves via `PATH`. Works when `gh copilot` is aliased or `copilot` is on the system PATH. |
| `gh` with `CopilotArgs: ["copilot", "agent", ...]` | If using the GitHub CLI extension form. |
| `C:\path\to\copilot.exe` | Absolute path if the executable is not on PATH. |

---

### `CopilotArgs`

An optional list of additional arguments prepended to every copilot invocation. Leave empty for the default single-executable setup.

Example — using a stub script during development:
```json
"CopilotExecutable": "powershell.exe",
"CopilotArgs": ["-File", "C:\\dev\\stub-agent.ps1"]
```

---

### `InstructionsRelativePath`

Relative path (from the repository root) where the per-step instructions file is written before each agent step.

Default: `.github/instructions/active-workflow.instructions.md`

GitHub Copilot reads all `.md` files under `.github/instructions/` automatically on startup — this is what allows the agent to receive its task without any manual typing.

> Do not change this unless you have a specific reason; the default path matches GitHub Copilot's standard instructions discovery.

---

### `InteractiveTerminal`

| Value | Behaviour |
|---|---|
| `true` (default) | Opens a new **cmd.exe** window for each agent step. The copilot CLI starts in interactive mode with an auto-start prompt. Stdout/stderr are **not** captured — the dashboard log panel will be empty. |
| `false` | Runs the copilot process headlessly (no terminal window). Stdout/stderr are captured and streamed to the dashboard log panel in real time. |

**When to use each:**

- Use `true` for workflows that require back-and-forth interaction or when you want the developer to see a familiar terminal.
- Use `false` for fully automated pipelines and when you want live log visibility in the dashboard.

---

### `InteractiveStartPrompt`

The initial prompt passed to `copilot -i "<prompt>"` when `InteractiveTerminal` is `true`. Copilot executes this prompt immediately on startup, so the agent begins its task without waiting for user input.

Default:
```
Begin the workflow session. Read `.github/copilot/workflow-input.md` and follow the workflow instructions you have been given.
```

You can customise this if your agents require a different bootstrap phrase, but the default works with standard pipeline agent definitions.

---

### `PersistLogsToRepo`

When `true`, stdout/stderr are also written to `{repo}/.copilot/logs/{stepRunId}.log` as a permanent record alongside the code. Defaults to `false`.

---

### `LogTailLines`

Number of log lines kept in memory per running step. The dashboard log panel shows the most recent lines up to this limit. Default: `500`.

---

### `LogBatchInterval`

How often buffered log lines are flushed to SignalR and the database. Default: `1 second`. Reduce for faster log streaming; increase to reduce database write frequency.

---

## Environment overrides

Any setting can be overridden at runtime via environment variables using the standard .NET configuration naming convention (`:` replaced with `__`):

```powershell
$env:AgentRunner__InteractiveTerminal = "false"
$env:ApiBaseUrl = "http://192.168.1.100:5000"
```

This is useful for CI/CD deployments or any environment where you want to override settings without modifying `appsettings.json`.

---

[← Back to help home](index)  
[← How it works](how-it-works)
