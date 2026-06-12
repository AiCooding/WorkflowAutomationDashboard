# Copilot Defaults

These are the default agent definition files for the Workflow Automation Dashboard.
Copy them to your global Copilot CLI folder so the dashboard can discover them.

## Installation

### Windows (PowerShell)

```powershell
# Create directories if they don't exist
New-Item -ItemType Directory -Force "$env:USERPROFILE\.copilot\agents" | Out-Null

# Install agents
Copy-Item docs\copilot-defaults\agents\* "$env:USERPROFILE\.copilot\agents\" -Force
```

### macOS / Linux (bash)

```bash
mkdir -p ~/.copilot/agents

# Install agents
cp docs/copilot-defaults/agents/* ~/.copilot/agents/
```

After copying, click **Refresh** on the **Agents** page in the dashboard to pick up the new
definitions without restarting.

## Adding your own definitions

Drop any `.md` file with an optional YAML front-matter block into:

- `~/.copilot/agents/` for agent persona definitions (listed in the Agents for reference).
