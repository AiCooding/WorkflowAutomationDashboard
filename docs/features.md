---
title: Features — Help
layout: default
---

# Features

The Features page lets you track individual units of work (features, tasks, bugs) and optionally attach **specification documents** that agents can read during a pipeline run.

---

## Feature fields

| Field | Description |
|---|---|
| **Name** | Short label for the feature. |
| **Description** | Optional longer description. |
| **Status** | Current lifecycle state (see below). |
| **Priority** | Integer; higher numbers mean higher priority. Used for sorting. |
| **Repository** | Optional link to a repository. Filters spec folder lookups to that repo. |

### Feature statuses

| Status | Meaning |
|---|---|
| `backlog` | Not yet started |
| `planning` | Being scoped or estimated |
| `in_progress` | Actively being worked |
| `review` | In code or design review |
| `done` | Completed |
| `cancelled` | Abandoned |

---

## Creating a feature

Click **New feature** to open the creation dialog. You can create a feature in three modes:

| Mode | Description |
|---|---|
| **Inline** | Write the spec body directly in the dialog. The content is saved as a spec file in the repository. |
| **Stub** | Create the feature with no spec yet. You can add spec documents later. |
| **Link** | Link to an existing spec folder already present in the repository. |

---

## Spec documents

Each feature can have up to three spec documents attached:

| Document | File | Purpose |
|---|---|---|
| **Proposal** | `proposal.md` | High-level description of what to build |
| **Design** | `design.md` | Technical approach and architecture decisions |
| **Tasks** | `tasks.md` | Breakdown of implementation steps |

Click the **Spec** (document) icon on a feature row to open the spec viewer. From there you can:

- View each document with rendered Markdown.
- Edit content inline and save changes.
- See the full file path on disk.

---

## Filtering

Use the **Repository** filter dropdown above the table to narrow the list to features for a specific repository or to show only features not linked to any repository (**Orphans**).

---

## Linking a feature to a pipeline run

When starting a pipeline run you can optionally select a feature from the **Feature** dropdown in the run form. The feature's spec documents are then available to agents as context during the run.

---

## Deleting a feature

Click the **Delete** (trash) icon on a feature row. This removes the feature record from the database but does **not** delete any spec files from disk.

---

[← Back to help home](index)
