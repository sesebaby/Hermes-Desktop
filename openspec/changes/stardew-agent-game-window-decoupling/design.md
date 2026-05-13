## Context

The Stardew v1 architecture already has the right broad shape: model-visible tools create host task/work item records, host/bridge code mechanically executes those tasks, and terminal facts return to the main NPC agent. Existing specs also require UI leases, bounded window operations, and fake-driven orchestration harness coverage.

The failure is at a narrower orchestration boundary. Private-chat host task submission currently treats waiting for the reply UI lifecycle like a generic ingress defer. Because the generic budget is intentionally short, a valid move can become terminally blocked while the player is simply reading a dialogue. That is the same class of error the host task runner was created to prevent: game lifecycle state is leaking into agent flow as an execution lock.

Stakeholders are the NPC agent, the host task runner, the Stardew bridge, prompt/skill authors, and tests that must catch this class before more UI-backed actions such as trade, craft, gather, and quest windows are added.

## Goals / Non-Goals

**Goals:**

- Keep the agent runtime non-blocking with respect to Stardew windows, menus, animations, and events.
- Preserve a single running world-action slot per NPC.
- Represent recoverable game waits as task/status facts that the agent can read or query.
- Return text-first tool results with a concise `summary` and minimal correlation/status fields.
- Keep generic stale/busy ingress blocking intact for real stale work.
- Update prompt and skill guidance so agents use `stardew_task_status` for continuation instead of relying on hidden host locks.
- Add regression coverage for the private-chat reply lifecycle bug and for status summaries.
- Record the repeated mistake in `openspec/errors` during implementation.

**Non-Goals:**

- No real craft, trade, gather, or quest window handlers.
- No task-system rewrite.
- No second queue model.
- No host-side inference of the agent's next action.
- No broad status mega-tool.
- No generic retry-count increase as the fix.

## Decisions

### Decision 1: Keep agent flow non-blocking, keep body execution single-slot

The agent may continue to think, speak, query status, and update todo while a game action is queued, running, or waiting on UI. The NPC body still has one running world-action slot. A conflicting new world action returns a readable `blocked/action_slot_busy` fact rather than being queued behind the current action.

Rationale:

- This keeps the architecture simple and matches the existing `ActionSlot`/`PendingWorkItem` design.
- It avoids unbounded queues of conflicting physical actions.
- It keeps the next decision with the agent rather than the host.

Alternatives considered:

- Allow multiple body actions in parallel. Rejected because Stardew NPC control, UI ownership, and bridge command semantics are single-body by nature.
- Queue every future body action behind the running one. Rejected because it creates hidden intent ordering and lets stale player requests execute after context has changed.

### Decision 2: Separate recoverable waits from generic stale/busy defer budget

Waiting for private-chat reply UI lifecycle, menu close, animation, or similar game conditions must be represented as recoverable task/status state. It must not consume the same short defer budget used for stale or busy ingress protection.

Rationale:

- Human UI read time is not a stale work item.
- Generic defer budgets should continue to catch real loops and busy slots.
- This targets the root cause without increasing global retry limits.

Alternatives considered:

- Increase `MaxDeferredIngressAttempts`. Rejected because it hides the incorrect classification and weakens stale-loop protection.
- Start world actions before any UI safety check. Rejected because UI lease/menu ownership still protects game state.
- Terminally block after reply UI is not closed quickly. Rejected because it makes player-visible UI timing break valid agent decisions.

### Decision 3: Use text-first, structure-retained tool results

Stardew action and status tool results should include a short `summary` written for the agent, while preserving `status`, `commandId`, `reason`/`errorCode`, and correlation fields.

Rationale:

- LLMs handle concise textual facts more reliably than raw field bags.
- Tests, UI, logs, and follow-up `stardew_task_status` still need stable machine fields.
- This avoids both extremes: pure prose with no verification surface and field-only output that forces the model to decode runtime internals.

Alternatives considered:

- Pure natural-language tool results. Rejected because it degrades testability and correlation.
- Field-only JSON results. Rejected because it repeats the current weakness: the model receives status but no obvious next-step interpretation.

### Decision 4: Teach the boundary in prompt and skill assets

`stardew-core`, `stardew-task-continuity`, and the runtime system prompt should explicitly state that windows, menus, animations, and events are game facts/status, not hidden locks on the agent process. They should direct the agent to use `stardew_task_status` for continuation and to make its own next decision from running/blocked/completed facts.

Rationale:

- Tool behavior alone is not enough; the agent must know how to react.
- This keeps the host from growing decision logic to compensate for confused model behavior.

Alternatives considered:

- Rely only on implementation changes. Rejected because the next failure mode could be the agent repeatedly submitting conflicting actions instead of querying status.
- Add host heuristics to pick the next action. Rejected because it violates the established host/agent boundary.

### Decision 5: Make the harness prove the architectural boundary, not just one symptom

Tests should prove both sides of the split:

- private-chat UI wait does not terminally block valid host task ingress via the generic defer budget;
- generic busy/stale ingress still blocks according to the configured budget;
- status/action tool results include an agent-readable summary;
- conflicting world actions return `blocked/action_slot_busy` without creating hidden queued work or host-selected retries;
- prompt/skill guidance preserves the non-blocking game-status boundary.

Rationale:

- This is a repeated class of bug, so a single move-specific test is not enough.
- The harness already exists to prevent regressions across host task lifecycle and prompt boundaries.

Alternatives considered:

- Only add one Haley/beach regression. Rejected because it would not catch the next trade/craft/window variant.

### Decision 6: Define a small status-code contract for tests

The implementation should keep exact field names aligned with current code, but tests must assert a minimal state vocabulary:

| Condition | Expected status shape | Required reason/code evidence | Summary expectation |
| --- | --- | --- | --- |
| Recoverable UI wait | non-terminal `queued`, `running`, or existing waiting-equivalent state | MUST NOT use `host_task_submission_deferred_exceeded` | Says the task is waiting on game UI/window lifecycle |
| Action slot conflict | terminal or immediate `blocked` | `action_slot_busy` | Says another world action is already in progress |
| Generic defer exhaustion | terminal `blocked` | `host_task_submission_deferred_exceeded` | Says the submission exceeded the generic busy/stale retry budget |
| Timeout/watchdog | terminal timeout/blocked/failed using existing runtime status | `action_slot_timeout` or mapped existing timeout code | Says the task timed out or watchdog stopped it |
| Lease/menu conflict | terminal or immediate `blocked` | `menu_blocked`, `ui_lease_busy`, `private_chat_active`, or mapped existing bridge reason | Says the menu/UI lease is unavailable without choosing the next action |

Rationale:

- Without a small matrix, tests can pass by checking only that "some error happened."
- The matrix keeps implementation bounded without inventing a new state machine.

Alternatives considered:

- Fully redesign status enums. Rejected because it is outside scope and risks breaking bridge/runtime contracts.
- Leave status codes implicit. Rejected because it allowed the current regression to hide behind a generic blocked fact.

## Risks / Trade-offs

- [Risk] The agent may continue thinking while the body is waiting and submit a conflicting action.  
  Mitigation: Preserve the single running action slot and return `summary + blocked/action_slot_busy`; teach the agent to query status or revise the plan.

- [Risk] A recoverable wait could persist forever if the game never emits the expected event.  
  Mitigation: Keep task/action timeouts and watchdog terminal facts; recoverable wait means not using the generic ingress budget, not infinite execution.

- [Risk] Adding `summary` fields could create two sources of truth if they drift from status fields.  
  Mitigation: Generate summaries from the same status/action result object where possible and assert representative summaries in tests.

- [Risk] Prompt/skill changes could accidentally encourage broad status scanning.  
  Mitigation: Keep existing broad status budget and phrase guidance around `stardew_task_status` for known command continuation.

- [Risk] Treating private-chat reply close as a UI fact could allow unsafe menu overlap.  
  Mitigation: Keep UI lease/menu conflict checks in window handlers; decoupling agent flow does not remove game-side safety.

- [Risk] Recoverable waits could wake or poll too aggressively and create noisy agent loops.  
  Mitigation: Reuse existing next-wake/backoff behavior and keep `stardew_task_status` as the continuation path for known commands.

## Migration Plan

1. Add failing regression tests for private-chat UI lifecycle wait exceeding the generic defer budget without terminally blocking the host task.
2. Add or adjust summary-output tests for `stardew_task_status` and representative action tool results.
3. Implement the smallest runtime change: classify private-chat UI lifecycle waiting separately from generic stale/busy ingress retry.
4. Add text-first summary shaping for status/action results.
5. Update Stardew prompt and skill assets.
6. Run targeted Stardew tests, then broader Stardew test filters.
7. Update or create the relevant `openspec/errors` entry with the repeated lesson.

Rollback is straightforward because the change is scoped to runtime classification, tool result shape, and prompt/skill text. If a summary shape causes unexpected model behavior, it can be reverted independently from the defer-budget classification fix.

## Open Questions

- Exact summary wording should be finalized during implementation from real `GameCommandStatus` and tool-result shapes.
- Whether all action tools should share one summary helper or start with a narrow helper for `stardew_task_status` and host task submission can be decided during implementation; avoid adding an abstraction until at least two call sites need it.
- The exact accepted timeout and menu/lease reason codes should be mapped from current code during RED test authoring; do not invent new codes unless the mapping proves none exist.
