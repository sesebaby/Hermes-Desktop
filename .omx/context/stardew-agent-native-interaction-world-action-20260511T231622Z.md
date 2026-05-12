# Stardew Agent-Native Interaction / World Action Context

## Task statement

Implement the agent-native fix for Stardew NPC runtime state pollution where private-chat/window lifecycle terminal records can overwrite the agent-visible "last real world action" fact after a completed move.

## Desired outcome

- Preserve `todo` as the only agent-owned task/commitment loop.
- Keep host/runtime state mechanical: action slot, pending work, terminal execution facts, interaction/window facts.
- Do not add a second task store or have the host auto-complete todo.
- Prevent interaction lifecycle commands such as `open_private_chat` and `private_chat_reply` from being presented as `last_action_result` / `lastAction` world-action facts.
- Present interaction lifecycle terminal records as interaction facts instead, so future crafting/trade/quest windows can follow the same pattern.

## Known facts / evidence

- `.omx/project-memory.json` requires Stardew/NPC agent ability boundaries to be model-visible Hermes/MCP/Stardew tools. The host cannot infer move/speak/todo/task update from hidden text fallback.
- `openspec/project.md` says the host only provides facts, events, tools, confirmations, execution results, permission/safety gates, path probing, and state orchestration. It must not choose the NPC's world target or convert observations into actions.
- `.omx/plans/prd-stardew-action-chain-guards-reference-alignment.md` says `todo` is agent-owned, not a host story/task system.
- `.omx/plans/prd-stardew-remove-closure-lock-reference-task-state-alignment.md` says the correct reference shape is queue/current-task/status/todo completion, not a closure lock.
- Runtime logs from the user test showed a real move completed, then `LastTerminalCommandStatus` was overwritten by `work_private_chat:...` / `open_private_chat`, while active todo remained `in_progress`.
- `src/runtime/NpcAutonomyLoop.cs` currently builds `last_action_result` directly from `LastTerminalCommandStatus`.
- `src/games/stardew/StardewNpcTools.cs` currently emits `lastAction=` in recent activity directly from `LastTerminalCommandStatus`.

## Constraints

- No second task store.
- No host auto-completion of `todo`.
- No Haley/Beach special case.
- No hidden local executor fallback.
- No new dependencies.
- Keep changes small and test-first.

## Unknowns / open questions

- Whether a separate persisted `LastWorldActionStatus` is needed now, or whether filtering/classifying the existing terminal status is enough for this fix.
- Whether all future interaction windows should use the same classification helper now or later.

## Likely codebase touchpoints

- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
