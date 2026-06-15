---
title: Agents — Help
layout: default
---

# Agents

The Agents page shows every **agent** registered in the catalog — the individual AI worker processes that pipeline steps can execute.

---

## What is a catalog entry?

The dashboard scans a configured catalog directory on startup and whenever a manual refresh is triggered. Each entry in that directory describes an agent or a workflow:

| Kind | Description |
|---|---|
| **Agent** | A reusable AI worker that can be assigned to a pipeline step. |
| **Workflow** | A standalone workflow definition (used by the legacy Workflow Runs system). |

The Agents page shows only entries of kind **agent**.

---

## Agent card fields

| Field | Description |
|---|---|
| **Display name** | Human-readable name shown in the designer step dropdown and on run cards. |
| **Slug** | Unique machine-readable identifier used internally to launch the agent (e.g. `code-review-agent`). |
| **Description** | Optional free-text from the catalog entry. |
| **Source path** | Absolute path to the agent's definition file on disk. |
| **Broken** | A warning chip shown when the catalog entry failed to load (missing files, parse errors, etc.). |

---

## Broken agents

An agent marked as **broken** cannot be selected in the pipeline designer and will cause a pipeline step to fail if it was already assigned before the agent became broken. The **Broken** count at the top of the page tells you at a glance how many need attention.

Common causes:
- The agent definition file was moved or deleted.
- The catalog folder path in `appsettings.json` is incorrect.
- The agent definition has a syntax error.

Fix the underlying file issue and click **Refresh** to reload the catalog.

---

## Refreshing the catalog

Click the **Refresh** (↻) button to re-scan the catalog directory. New agents appear immediately; removed or broken agents are updated accordingly.

---

[← Back to help home](index)
