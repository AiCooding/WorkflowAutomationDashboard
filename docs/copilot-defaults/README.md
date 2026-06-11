# Copilot Defaults

These are the default workflow and agent definition files for the Workflow Automation Dashboard.
Copy them to your global Copilot CLI folder so the dashboard can discover them.

## Installation

### Windows (PowerShell)

```powershell
# Create directories if they don't exist
New-Item -ItemType Directory -Force "$env:USERPROFILE\.copilot\workflows" | Out-Null
New-Item -ItemType Directory -Force "$env:USERPROFILE\.copilot\agents" | Out-Null

# Install workflows
Copy-Item docs\copilot-defaults\workflows\* "$env:USERPROFILE\.copilot\workflows\" -Force

# Install agents
Copy-Item docs\copilot-defaults\agents\* "$env:USERPROFILE\.copilot\agents\" -Force
```

### macOS / Linux (bash)

```bash
mkdir -p ~/.copilot/workflows ~/.copilot/agents

# Install workflows
cp docs/copilot-defaults/workflows/* ~/.copilot/workflows/

# Install agents
cp docs/copilot-defaults/agents/* ~/.copilot/agents/
```

After copying, click **Refresh** on the **Catalog** page in the dashboard to pick up the new
definitions without restarting.

## Contents

### Workflows (`~/.copilot/workflows/`)

| File | Slug | Description |
|------|------|-------------|
| `pm-draft.md` | `pm-draft` | Guides a PM conversation through drafting a feature proposal and creating it in the dashboard. |

### Agents (`~/.copilot/agents/`)

| File | Slug | Description |
|------|------|-------------|
| `architect.md` | `architect` | Senior .NET Architect — turns feature descriptions into implementation plans. |
| `developer.md` | `developer` | Senior .NET Developer — implements plans one TODO at a time. |

## Adding your own definitions

Drop any `.md` file with an optional YAML front-matter block into:

- `~/.copilot/workflows/` for workflow definitions (run as auto-loaded Copilot CLI instructions).
- `~/.copilot/agents/` for agent persona definitions (listed in the Catalog for reference).

Minimal front-matter:

```yaml
---
name: My Workflow         # display name; falls back to filename stem
slug: my-workflow         # stable id; falls back to filename stem
description: One liner.
---
```

Front-matter is optional — a plain markdown file with no front-matter is fully valid.

Click **Refresh** on the Catalog page after adding files; no dashboard restart is needed.
