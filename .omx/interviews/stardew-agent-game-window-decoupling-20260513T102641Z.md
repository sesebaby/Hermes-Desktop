# Stardew Agent/Game Window Decoupling Interview

- profile: standard
- context type: brownfield
- rounds: 4
- final ambiguity: 0.16
- threshold: 0.20
- context snapshot: `.omx/context/stardew-agent-game-window-decoupling-20260513T093144Z.md`

## Summary

The user identified the Haley/private-chat move regression as an orchestration boundary problem, not a one-off retry bug. The agreed architecture is: Stardew game windows, animation, menus, and events may affect host task execution state, but they must not block the agent turn or consume unrelated ingress retry budgets.

The first implementation pass should stay small: repair private-chat host task waiting, improve action/status tool summaries, update prompt/skill guidance, add regression tests, and record the repeated mistake in `$errors`.

## Decisions

- Agent flow is not blocked by game UI/window/animation lifecycle.
- NPC body execution is not parallel: one NPC has at most one running world action slot.
- Conflicting new world actions immediately return readable `blocked/action_slot_busy` facts; they do not form a second queue and do not overwrite current work.
- Recoverable waits remain inside the current task/status, for example `running/waiting_for_menu_close`.
- Tool results should be text-first, structure-retained: include a short agent-readable `summary` plus minimal machine fields such as `status`, `commandId`, and `reason`.
- The existing `stardew_task_status` tool should remain the continuation status tool, but its description/result should be clearer and more agent-readable.

## In Scope

- Fix private-chat host task submission so waiting for `private_chat_reply_closed` does not use the generic stale/busy short defer budget.
- Add short `summary` text to relevant Stardew action/status tool results.
- Improve `stardew_task_status` description and returned summary.
- Update `stardew-core`, `stardew-task-continuity`, and Stardew runtime prompt guidance.
- Add regression tests for non-blocking agent flow and status-summary behavior.
- Write or update an `$errors` entry after implementation.

## Out Of Scope

- No real craft/trade/gather window handlers in this pass.
- No rewrite of the task system.
- No complex second queue model.
- No host-side automatic next-step decision for the agent.
- No broad, all-in-one world scan expansion.

## Transcript

### Round 1

Question: Should every Stardew world-action tool call return immediately with `queued/running/blocked/completed`, never waiting for player window close, animation end, or long action completion, with later progress exposed via status/terminal facts/watchdog wakeups?

Answer: The direction is reasonable. Keep the solution simple. Host should return text explanations, not only fields. The agent needs active status tools; prompts and skills must also be improved.

### Round 2

Question: Should status output be a `summary` field inside JSON, or pure short natural language?

Answer: Implementation may decide.

Decision: Use text-first JSON with `summary` plus minimal fields.

### Round 3

Question: Accept the first-pass scope and explicit non-goals?

Answer: Accepted.

### Round 4

Question: Accept the boundary that agent flow is parallel, but NPC body execution is not; conflicting new world actions return blocked and the agent decides next?

Answer: Agreed.
