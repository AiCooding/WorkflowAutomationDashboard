---
title: Input Requests — Help
layout: default
---

# Input Requests

When a running agent needs clarification or a decision from you, it raises an **input request**. The Input Requests page shows all requests that are still waiting for a response.

The notification number next to **Input Requests** in the navigation shows how many are pending.

---

## Anatomy of an input request card

| Element | Description |
|---|---|
| **Question** | The text the agent sent asking for input. |
| **Agent / Workflow** | Which agent and workflow raised the request. |
| **Timestamp** | When the request was created. |
| **Option chips** *(optional)* | Pre-defined choices the agent provided. Click a chip to pre-fill the answer field. |
| **Your response** | Free-text input field where you type or paste your answer. |

---

## Answering a request

1. Read the question carefully.
2. Either click one of the **option chips** to select a pre-defined answer, or type your own response in the text field.
3. Press **Enter** or click **Submit**.

### Confirmation dialog

Before the response is sent you will see a confirmation popup showing:

- The original **question**
- Your **answer** in a highlighted block

You have two choices:

| Button | Action |
|---|---|
| **Confirm** | Submits the response to the agent and removes the card. |
| **Abort** | Closes the dialog without submitting — you can edit your answer and try again. |

This step prevents accidental submissions when an agent is waiting on a critical decision.

---

## What happens after you submit

- The agent receives your response and continues executing.
- The card disappears from the Input Requests page.
- If the agent raises another question, a new card will appear automatically via the real-time connection.

---

## Stale requests

If a pipeline run is **cancelled** while an agent is waiting for input, the input request card will remain visible until the page is refreshed or until the SignalR connection delivers the cancellation event. Refreshing the page with the **↻** button in the header will clear any stale cards.

---

[← Back to help home](index)
