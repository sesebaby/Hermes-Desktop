# Deep Interview Spec: Stardew Agent/Game Window Decoupling

## Metadata

- profile: standard
- rounds: 4
- final ambiguity: 0.16
- threshold: 0.20
- context type: brownfield
- context snapshot: `.omx/context/stardew-agent-game-window-decoupling-20260513T093144Z.md`
- transcript: `.omx/interviews/stardew-agent-game-window-decoupling-20260513T102641Z.md`

## Intent

Prevent repeated Stardew orchestration regressions where game UI/window/animation lifecycle blocks agent flow or consumes unrelated retry budgets. The architecture must scale beyond private chat to future task, trade, gather, craft, and quest windows without reintroducing per-window patches.

## Desired Outcome

Stardew NPC agents remain able to think, speak, query status, and update todo while the game side executes or waits. Host/bridge execution state is exposed as concise facts and status summaries. Game constraints still protect the world, but they do not suspend the agent runtime.

## Core Principles

- Game-side waits become task/status facts, not agent-flow blocks.
- A Stardew action/status tool result should be text-first and structure-retained.
- One NPC has at most one running world action slot.
- New conflicting world actions return readable `blocked/action_slot_busy` facts immediately.
- Recoverable waits remain with the current task/status and can be queried by `stardew_task_status`.
- Terminal blocked/failed facts return control to the agent for a new decision.
- The host never infers the agent's next step from reply text.

## In Scope

- Fix private-chat host task submission waiting so `private_chat_reply_closed` does not consume generic stale/busy defer attempts.
- Add or improve short `summary` text for Stardew action/status tool results used by the agent.
- Improve `stardew_task_status` description and output so it explains current state in one short sentence.
- Update `skills/gaming/stardew-core/SKILL.md`.
- Update `skills/gaming/stardew-task-continuity/SKILL.md`.
- Update `skills/system/stardew-npc-runtime/SYSTEM.md`.
- Add regression tests for private-chat UI wait not blocking or terminal-blocking host task ingress.
- Add tests for status/action summary behavior where practical.
- Update or create an `openspec/errors` entry for the repeated orchestration mistake.

## Out Of Scope

- Do not implement real craft, trade, gather, or quest window handlers.
- Do not rewrite `IngressWorkItems`, `ActionSlot`, `PendingWorkItem`, or the task system.
- Do not add a second queue model.
- Do not let the host decide the agent's next action.
- Do not expand broad world status tools into a large all-in-one scanner.
- Do not solve every existing Stardew movement/pathing issue.

## Decision Boundaries

- Implementation may decide exact `summary` wording and helper placement.
- Implementation may keep minimal machine fields needed for tests, UI, logs, and status lookup.
- Implementation may treat private-chat reply display/close as UI facts rather than world-action terminal conditions, as long as the host does not start unsafe UI manipulation that would overwrite an owned menu.
- Implementation must preserve the single running world-action slot per NPC.
- Implementation must not use a generic retry-count increase as the root fix.

## Acceptance Criteria

1. A private-chat `stardew_host_task_submission` with a `conversationId` remains recoverable while waiting for reply UI lifecycle and is not terminal-blocked by `MaxDeferredIngressAttempts`.
2. Generic busy/stale host task ingress still blocks after its configured budget where appropriate.
3. `stardew_task_status` provides an agent-readable `summary` in addition to machine-readable status fields.
4. Supported Stardew action tool results that return queued/running/blocked/completed status include a short `summary`.
5. Updated Stardew prompt/skill text states that windows, animation, menus, and events are game facts/status, not agent-flow locks.
6. Updated prompt/skill text tells the agent to use `stardew_task_status` for continuation and to decide next steps from blocked/running/completed facts.
7. Tests prove the private-chat UI wait regression does not repeat.
8. `$errors` contains the repeated lesson after implementation.

## Brownfield Evidence

- `openspec/specs/stardew-ui-task-lifecycle/spec.md` already requires UI lease, active menu safety, bounded steps, validation, cleanup, and host task extension for future window actions.
- `openspec/specs/stardew-orchestration-harness/spec.md` already requires host task lifecycle harness coverage.
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs` currently uses `DeferPrivateChatReplyClosedIngressAsync(...)`, which increments `DeferredAttempts` and reuses the generic short defer budget.
- `src/runtime/NpcAutonomyBudget.cs` sets `MaxDeferredIngressAttempts = 3`, which is appropriate for stale/busy guards but not human UI read time.
- `src/games/stardew/StardewNpcTools.cs` already provides `stardew_task_status`, but its description and result are too field-oriented for the agreed agent-facing status style.
- `src/Core/Agent.cs` already distinguishes broad Stardew status tools from `stardew_task_status` continuation status.
- `skills/gaming/stardew-core/SKILL.md` and `skills/gaming/stardew-task-continuity/SKILL.md` already tell the agent to query long-task status, but need the stronger non-blocking window/status boundary.
- `openspec/errors/E-2026-0510-stardew-private-chat-delegated-move-dialogue-and-target.md` records adjacent mistakes around private-chat reply lifecycle and host task movement.

## Pressure-Pass Finding

The original statement “agent should not be blocked by game windows” could have been misread as allowing multiple physical actions to run in parallel. The clarified boundary is stricter and simpler: agent cognition and conversation continue, but the NPC body has a single running world-action slot. Conflicts return readable facts instead of queueing unbounded work.

## Recommended Handoff

Use `$ralplan` with this spec before implementation. The plan should convert these boundaries into a small TDD implementation plan and explicitly preserve the non-goals.
