# Copilot Defaults

Agent persona definitions for the Workflow Automation Dashboard.

## Architecture — Option D (Domain + Injected Plumbing)

Agent `.md` files contain **domain content only** — who the agent is, what it produces, its
templates and rules. Every agent receives the same pipeline plumbing block automatically at
runtime via the `InstructionsInjector` in the backend:

| Injected section | What it provides |
|---|---|
| Context table | Live values of `PIPELINE_RUN_ID`, `STEP_RUN_ID`, `REPO_PATH`, `FEATURE_SLUG`, `ATTEMPT_NUMBER` |
| Read input | Exact path and instruction to read `workflow-input.md` before starting |
| Retry callout | Summary of prior feedback when `ATTEMPT_NUMBER > 1` |
| Completion call | `curl` command with correct URL and JSON body to report completion |
| Reviewer section | Approve vs feedback examples (only when `canGiveFeedback = true` for this step) |

**Agents do not need to know about env vars, REST calls, or file paths — the injector handles everything.**

## Available Agents

| Slug | Name | File |
|---|---|---|
| `pm` | Product Manager | `pm.agent.md` |
| `architect` | Architect | `architect.agent.md` |
| `architect-plan-reviewer` | Architect Plan Reviewer | `architect-plan-reviewer.agent.md` |
| `developer` | Developer | `developer.agent.md` |
| `code-review` | Code Review | `code-review.agent.md` |
| `uiux-designer` | UI/UX Designer | `uiux-designer.agent.md` |

Use the **slug** value when configuring agent steps in the Pipeline Designer.

## Shared Memory — `workflow-input.md`

Each pipeline run has a single shared file at `{repo}/.github/copilot/workflow-input.md`.
Every agent reads it on startup and appends its outputs under a structured header:

```
## [{stepId}] attempt {N} — by: {agentSlug} — for: {nextStepId}
```

This is how agents pass information to each other across pipeline steps.

## Installation

### Windows (PowerShell)

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\.copilot\agents" | Out-Null
Copy-Item docs\copilot-defaults\agents\* "$env:USERPROFILE\.copilot\agents\" -Force
```

### macOS / Linux

```bash
mkdir -p ~/.copilot/agents
cp docs/copilot-defaults/agents/* ~/.copilot/agents/
```

## Adding Custom Agents

Drop any `.md` file with a YAML front-matter block into `~/.copilot/agents/`:

```yaml
---
name: My Agent
slug: my-agent
description: Short description shown in the pipeline designer
---

Your domain persona content here...
```

- `slug` must be unique and match the `agentId` used in pipeline step definitions.
- `name` and `description` are shown in the Pipeline Designer when selecting an agent for a step.
- If `slug` is omitted, the scanner falls back to the filename stem — always add an explicit `slug`
  to avoid issues with files named `something.agent.md` (which would produce slug `something.agent`).
