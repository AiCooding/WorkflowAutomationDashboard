---
title: Workflow Automation Dashboard — Help
layout: default
---

# Workflow Automation Dashboard

Welcome to the help documentation for the **Workflow Automation Dashboard** — a self-hosted orchestration tool that lets you design multi-step AI agent pipelines, monitor live runs, manage git repositories, and review approvals all from one place.

---

## Pages at a glance

| Page | What it does |
|---|---|
| [Dashboard](dashboard) | Live health overview — counts, status cards, recent runs |
| [Pipelines](pipelines) | Design and run multi-step agent workflows |
| [Repositories](repositories) | Register local git repos that pipelines work against |
| [Agents](agents) | Browse the agent & workflow catalog |
| [Features](features) | Track feature items and attach spec documents |
| [Input Requests](input-requests) | Answer questions raised by a running agent |
| [Workflow Runs](runs) | Legacy standalone workflow run monitor |
| [How It Works](how-it-works) | Full technical walkthrough — Copilot CLI wiring, files, completion protocol |
| [Configuration](configuration) | `appsettings.json` reference for all server settings |

---

## Quick-start checklist

1. **Add a repository** — go to [Repositories](repositories) and register the local path of the git repo you want to automate.
2. **Check the catalog** — go to [Agents](agents) and confirm your agents are loaded and not broken.
3. **Design a pipeline** — go to [Pipelines](pipelines) → *New Pipeline*, add agent steps and approval steps, then save.
4. **Start a run** — click **Run** on a pipeline card, fill in the ticket number and optional instructions, then click **Start run**.
5. **Watch it live** — expand the run panel to see live logs; respond to any [Input Requests](input-requests) as agents ask questions.
6. **Review approvals** — when a step reaches *waiting approval*, the notification bell in the header will light up. Expand the run panel to approve or reject.

---

## Real-time updates

All pages subscribe to **SignalR** for live updates — no manual refresh needed. The toolbar icon shows the connection state:

- **Wi-Fi icon (solid)** — connected, updates are live.
- **Wi-Fi off icon** — disconnected, the page will show the last known state until the connection is restored.

---

## Getting help

- Browse the pages linked in the table above for feature-specific guidance.
- Source code and issues: [github.com/AiCooding/WorkflowAutomationDashboard](https://github.com/AiCooding/WorkflowAutomationDashboard)
