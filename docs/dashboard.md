---
title: Dashboard — Help
layout: default
---

# Dashboard

The Dashboard is the home page of the application. It gives you an instant health snapshot of the entire system and quick links to anything that needs attention.

---

## Summary cards

Each card displays a live count and links to the relevant page.

| Card | Description |
|---|---|
| **Repositories** | Total registered repositories |
| **Broken repositories** | Repos whose path no longer exists on disk |
| **Pipelines** | Total pipeline definitions |
| **Running pipeline runs** | Runs currently executing agent steps |
| **Waiting approval** | Runs that have paused and are waiting for a human decision |
| **Pending approvals** | Total individual approval requests not yet decided |
| **Completed runs** | Runs that finished successfully |
| **Failed runs** | Runs that stopped due to an error |
| **Total features** | Feature items tracked in the system |
| **Features in progress** | Features whose status is *in_progress* |
| **Catalog entries** | Agents and workflows loaded from the catalog |
| **Broken catalog entries** | Catalog entries that failed to load |

---

## Recent runs

Below the cards the Dashboard shows the **10 most recent pipeline runs** with their status, pipeline name, ticket number, and branch. Click any run to be taken to the Pipelines page where you can expand it for full details and logs.

---

## Live updates

The Dashboard refreshes automatically when any pipeline run or approval changes via the real-time SignalR connection. You do not need to reload the page.

---

[← Back to help home](index)
