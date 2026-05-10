# Stardew Local Executor Feedback Context

## Task Statement

User asked to inspect `.omx/plans/stardew-npc-跨地图导航完成计划.md` because the plan forced the main NPC agent to delegate execution to a local small model to save cloud-token cost, and the user suspects there may be no feedback mechanism from the local executor back to the main agent.

## Desired Outcome

Initial investigation: determine whether the suspected feedback gap exists and identify the relevant code paths.

Execution decision after discussion: move routine autonomy navigation target resolution and `stardew_navigate_to_tile` tool calls back to the parent NPC agent, while keeping real game-world execution in the host/Stardew bridge. Keep the old local/delegation path only for compatibility ingress such as private-chat delegated actions.

## Known Evidence

- Original plan architecture: parent/main model selects high-level intent and natural destination text; local executor/delegation lane resolves navigation through `skill_view` and calls executor-only `stardew_navigate_to_tile`.
- `src/runtime/NpcRuntimeSupervisor.cs` creates autonomy parent with `combinedToolSurface = NpcToolSurface.FromTools([])` and `registerCapabilities: false`, then creates `NpcLocalExecutorRunner` only when `DelegationChatClient` exists.
- `src/runtime/NpcAutonomyLoop.cs` runs parent `_agent.ChatAsync(...)` first, then calls `RunLocalExecutorAsync(...)`, and replaces the returned tick `decisionResponse` with `local_executor_completed:*` / `blocked:*` / `escalated:*`.
- `src/runtime/NpcLocalExecutorRunner.cs` keeps its own local `messages` list for tool loops. Its tool result messages are not appended to the parent autonomy `Session`.
- `src/runtime/NpcAutonomyLoop.cs` writes executor outcome to `runtime.jsonl` through `WriteLocalExecutorResultAsync(...)`, but `WriteSessionTaskContinuityEvidenceAsync(...)` only inspects parent `decisionSession.Messages` tool calls. Since the autonomy parent has no Stardew action tools, this path does not see local executor tool calls.
- `src/games/stardew/StardewNpcTools.cs` records terminal command status in `NpcRuntimeInstance.LastTerminalCommandStatus` via `StardewRuntimeActionController.RecordStatusAsync(...)`.
- `src/runtime/NpcAutonomyLoop.cs` injects `LastTerminalCommandStatus` into the next parent decision prompt only for the special action-slot-timeout case via `BuildActionSlotTimeoutFact(...)`; ordinary completed/blocked/failed executor results are not injected as a feedback fact.
- Tests encode the current design:
  - `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs` asserts autonomy parent has zero tool calls and local executor uses the delegation client.
  - `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs` asserts local executor move logs evidence and does not write memory.

## Preliminary Conclusion

The suspected issue is real at the parent-cognition layer. The system has observability feedback to runtime logs, UI/inspector, controller state, and task status, but it does not have a general protocol that feeds local executor tool results back into the parent agent's transcript or next prompt. The parent only sees future effects indirectly if it chooses to query status or if the special timeout fact is injected.

## Implemented Direction

- Routine autonomy navigation now exposes a controlled parent tool surface containing `skill_view` plus Stardew action/status tools such as `stardew_navigate_to_tile`.
- The parent autonomy prompt tells the NPC to load `stardew-navigation` references, resolve `target(locationName,x,y,source)`, and call `stardew_navigate_to_tile` itself.
- Because the parent agent calls the navigation tool directly, the host/bridge execution result remains in the parent session/tool transcript instead of living only inside the local executor turn.
- `NpcLocalExecutorRunner` remains available for delegated ingress and legacy JSON intent compatibility, but it is no longer the routine autonomy move target-resolution path.

## Constraints

- Do not add an external MCP server for this slice; use the existing Hermes `ITool` surface as the MCP-like call/return loop.
- Keep real game-world writes in the host/Stardew bridge.
- Do not change the bridge cross-map state machine.
- Do not delete the private-chat `npc_delegate_action`/delegated ingress compatibility path.

## Unknowns / Discussion Questions

- Whether private-chat delegated movement should later be migrated to a parent-visible follow-up turn instead of the retained local executor compatibility lane.
- Whether additional compact host feedback facts are still useful for long-running command completion after the initial tool call result.
