---
title: Workflow Runs — Help
layout: default
---

# Workflow Runs

The Workflow Runs page shows **legacy standalone workflow runs** — single-agent executions launched directly from the [Catalog](agents) rather than through a multi-step [Pipeline](pipelines).

> **Note:** For new automation work, the [Pipelines](pipelines) system is recommended. It supports multi-step flows, approvals, feedback loops, and restart-from-step — features not available in standalone workflow runs.

---

## What is a workflow run?

A workflow run executes a single catalog entry (a workflow definition) against a repository. Unlike a pipeline run there are no steps, no approval gates, and no restart capability — the workflow either completes or fails as a single unit.

---

## Run statuses

| Status | Meaning |
|---|---|
| `pending` | Created, not yet started |
| `queued` | Waiting because the same repository has a run in progress |
| `running` | The workflow process is actively executing |
| `completed` | Finished successfully |
| `failed` | Stopped due to an error |
| `cancelled` | Manually cancelled |
| `broken` | The catalog entry that launched this run is now broken or missing |

---

## Filtering runs

Use the filter bar at the top of the page to narrow by:

- **Repository** — show only runs against a specific repo.
- **Status** — e.g. show only `running` or `failed` runs.

---

## Viewing logs

Expand a run panel to see live stdout/stderr output from the workflow process. Logs stream in real time while the run is active.

---

## Cancelling a run

Click **Cancel** inside the expanded panel of any run that is `pending`, `queued`, or `running`. The process is terminated and the status changes to `cancelled`.

---

## Relation to Pipelines

Workflow runs and pipeline runs share the same repository slot queue — only one active execution per repository at a time. A pipeline run and a workflow run targeting the same repository will queue behind each other.

---

[← Back to help home](index)
