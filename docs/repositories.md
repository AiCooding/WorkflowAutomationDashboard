---
title: Repositories — Help
layout: default
---

# Repositories

The Repositories page manages the list of local git repositories that pipelines work against. Every pipeline run must be associated with a repository.

---

## Adding a repository

Click **Add repository** to open the dialog.

| Field | Description |
|---|---|
| **Path** | Absolute path to the local folder that contains the `.git` directory (e.g. `C:\Projects\MyApp`). |
| **Name** *(optional)* | A friendly display name. If left blank the folder name is used. |

### Requirements

Before a pipeline can be started against a repository:

1. The folder must exist on disk.
2. The folder must be a valid git repository (`git init` must have been run).
3. At least one commit must exist in the repo.

If any of these conditions are not met the pipeline start will return an error and the repository row will be shown as **broken**.

---

## Repository status

| Status | Meaning |
|---|---|
| **OK** | Path exists and the dashboard can read it. |
| **Broken** | The path no longer exists on disk or is inaccessible. The row shows a warning chip. |

---

## Editing a repository

Click **Edit** (pencil icon) on any row to update the path or name. Changes take effect immediately for all future pipeline runs.

---

## Deleting a repository

Click **Delete** (trash icon). Existing pipeline runs linked to the repository are not deleted — they retain the snapshot of the path/name at the time they were created.

> **Warning:** Deleting a repository does not delete any files on disk. It only removes the registration from the dashboard.

---

## Live updates

The repository list updates in real time via SignalR. If another user adds or edits a repository in a different browser tab, your list reflects the change immediately.

---

[← Back to help home](index)
