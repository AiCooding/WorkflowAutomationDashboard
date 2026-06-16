---
title: Pipelines — Help
layout: default
---

# Pipelines

The Pipelines page is the core of the dashboard. It lets you **design** orchestration flows made of agent and approval steps, **start runs** against a repository, monitor **live logs**, handle **approvals**, and **import/export** pipeline definitions.

---

## Pipeline designer

Click **New Pipeline** (or **Edit** on an existing pipeline) to open the designer.

### Pipeline details
- **Name** — required; used on all cards and run listings.
- **Description** — optional free-text shown on the pipeline card.

### Steps

Each step can be one of two types:

| Type | Icon | Description |
|---|---|---|
| **Agent** | 🤖 | Runs a catalog agent as a sub-process. Requires selecting an agent from the dropdown. |
| **User approval** | ✅ | Pauses the run and waits for a human to approve or reject before continuing. |

#### Step fields

| Field | Description |
|---|---|
| **Step ID** | Unique identifier within the pipeline (e.g. `code-review`). Used internally for routing. |
| **Name** | Human-readable label shown on run cards. |
| **Agent** | *(agent steps only)* The catalog agent to execute. |
| **Can give feedback** | Enables a feedback text box on the approval card, or marks a reviewer agent as capable of sending back `feedback` with a message. |
| **Return to step on feedback/rejection** | If feedback is given, the run jumps back to this earlier step instead of ending. Leave blank to end the run on rejection. |

#### Reordering and removing steps

Use the **↑ / ↓** arrow buttons on each step card to reorder. Click the **🗑 delete** button to remove a step.

---

## Importing and exporting pipelines

### Export
Open a pipeline in the designer and click **Export JSON** in the top-right. A `.pipeline.json` file is downloaded to your computer containing the pipeline's name, description, and all step definitions.

### Import
On the Pipelines list page, click **Import JSON** in the header and pick a previously exported `.pipeline.json` file. A new pipeline is created immediately and appears at the top of the list. The original pipeline is unaffected.

> **Tip:** Use import/export to share pipeline templates between team members or to back up a configuration before making changes.

---

## Starting a pipeline run

> **Before you start:** Make sure the agents folder is configured in **Settings → Agent Catalog**. Without it, agent steps cannot discover their definitions and will fail immediately. A banner at the top of this page will warn you if it is not set.

Click **Run** on any pipeline card to expand the run form.

| Field | Description |
|---|---|
| **Repository** | The local git repository the pipeline will work against. |
| **Feature** *(optional)* | Link this run to an existing feature item. |
| **Ticket Number** | Required. Used as the branch name suffix (e.g. `TEST-42`). |
| **Branch Prefix** *(optional)* | Prepended to the ticket to form the full branch name (e.g. `feature/TEST-42`). |
| **Initial instructions** *(optional)* | Free-text guidance injected into the pipeline's input file and passed to the first agent. |

The computed branch name is shown as a preview while you type.

### Branch conflicts

If the branch already exists in the repository you will see a conflict dialog with two options:

- **Continue on existing branch** — checks out the existing branch and starts the run on it.
- **Create renamed branch** — enter a new ticket number / prefix and create a fresh branch.

---

## Monitoring a run

Expand any run panel to see:

- **Info grid** — current step, repository, feature, start/end times, error message if any.
- **Step chips** — visual status (pending / running / completed / failed / skipped) for each step.
- **Approval card** — appears when a step is waiting for a human decision (see below).
- **Logs** — live and historical stdout/stderr output from the currently active agent step.

### Run statuses

| Status | Meaning |
|---|---|
| `pending` | Created, not yet started |
| `queued` | Waiting because the same repository has another run in progress |
| `running` | An agent step is actively executing |
| `waiting_approval` | Paused, waiting for a human to decide |
| `completed` | All steps finished successfully |
| `failed` | A step encountered an unrecoverable error |
| `cancelled` | Manually cancelled by the user |

---

## Approvals

When a run reaches a **User Approval** step it pauses and shows an approval card inside the expanded run panel. The notification bell in the toolbar also shows a badge count.

- **Approve** — the run continues to the next step.
- **Reject** — if the step has *Can give feedback* enabled, you can type optional guidance before rejecting. If a *Return to step* is configured the run jumps back to that earlier step; otherwise the run ends.

---

## Cancelling a run

Click **Cancel** in the run panel actions while a run is `pending`, `queued`, `running`, or `waiting_approval`. Any pending approvals for that run are automatically dismissed and the dashboard badge count updates immediately.

---

## Restarting a cancelled run from a specific step

When a run is `cancelled` or `failed`, a **Restart from step** button appears in the run panel.

1. Click **Restart from step**.
2. In the dialog, pick the step you want to resume from. All steps before it are skipped; all steps from the chosen step onward are re-run fresh.
3. Click **Restart** to confirm.

> **Note:** The run reuses the same repository branch and ticket number. The original start time is preserved.

---

[← Back to help home](index)
