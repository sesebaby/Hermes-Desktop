# Stardew NPC Task Continuity Closure Test Spec

## Test Strategy

Target the closure gaps only. Existing substrate tests already prove `ToolSessionId`/`TaskSessionId`, todo projection, hydration, and required-skill injection. This spec adds end-to-end assertions for:

1. autonomy actually advancing player commitments with Stardew tools;
2. terminal `blocked` / `failed` command truth being surfaced by runtime first, then flowing into todo `status` + `reason` only when the agent later calls `todo` / `todo_write`;
3. player feedback attempts on blocked/failed promised tasks;
4. `runtime.jsonl` and task-view/UI evidence staying coherent.

## Proposed Tests

### 1. `StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_PlayerPromiseCreatesLongTermTodo_AndAutonomyLaterConsumesSameTodo`

- Intent:
  - Extend current private-chat promise coverage so it no longer stops at “todo exists”.
  - Prove the same NPC long-term session todo is the one later consumed by autonomy.
- Assertions:
  - root session contains the promised todo;
  - private-chat transcript session does not own that todo;
  - later autonomy handle sees the same active todo content.

### 2. `NpcRuntimeSupervisorTests.RunOneTickAsync_AfterPrivateChatPromise_AutonomyUsesStardewActionToolsForContinuation`

- Intent:
  - Upgrade the current “active task injected into prompt” test into a behavior test.
- Assertions:
  - autonomy tool surface includes `stardew_move`, `stardew_task_status`, `stardew_speak`, `stardew_open_private_chat`;
  - the chat/tool loop uses at least one Stardew action tool when an actionable todo exists;
  - the result is not accepted as closure if only narration text is produced.

### 3. `NpcAutonomyLoopTests.RunOneTickAsync_WhenActiveTodoExists_NarrationOnlyDecisionWritesNoToolClosureDiagnostic`

- Intent:
  - Keep a hard regression guard against narration masquerading as progression.
- Assertions:
  - if no action tool call occurs, `runtime.jsonl` records a warning entry tied to the tick;
  - the diagnostic is machine-checkable and points at missing visible action / feedback.

### 4. `StardewNpcAutonomyBackgroundServiceTests.ProcessAsync_WhenMoveOrSpeakCommandEndsBlocked_SurfacesTerminalStatusAndWritesRuntimeEvidence`

- Intent:
  - Prove the runtime/background path only surfaces terminal command truth and append-only evidence.
- Assertions:
  - runtime/controller snapshot or recent-activity surface exposes terminal `blocked` status;
  - `runtime.jsonl` contains `command_terminal`;
  - no assertion expects direct root-session todo mutation from background service.

### 5. `NpcAutonomyLoopTests.RunOneTickAsync_AfterTerminalBlockedOrFailedStatus_AgentWritesTodoReasonAndAttemptsPlayerFeedback`

- Intent:
  - Cover the second half of the closure: after runtime surfaces blocked/failed command truth, a later agent/autonomy turn authors task truth and feedback.
  - “Later” means agent-authored rather than background-authored; it may occur in the same Agent chat/tool loop after a Stardew tool result, or in a subsequent autonomy tick after command truth is exposed.
- Assertions:
  - agent sees the surfaced terminal status through prompt facts / `stardew_recent_activity` / runtime context;
  - agent calls `todo` / `todo_write` so the root-session todo becomes `blocked` or `failed` with non-empty short `reason`;
  - agent attempts `stardew_speak` or private-chat feedback path;
  - host does not synthesize NPC speech on its own.

### 6. `NpcRuntimeSupervisorTests.TryGetTaskView_AfterBlockedPromise_ReturnsUpdatedStatusAndReasonWithoutFreshChatHandle`

- Intent:
  - Lock the UI-facing requirement.
- Assertions:
  - `TryGetTaskView(descriptor.SessionId, ...)` returns the updated blocked/failed todo from transcript-backed tool-result projection;
  - this works after runtime start/hydration and before any new private-chat handle.

### 7. `NpcAutonomyLoopTests.RunOneTickAsync_WhenPromisedTaskBlocks_RuntimeJsonlIncludesTaskOutcomeAndFeedbackAttempt`

- Intent:
  - Ensure runtime evidence is sufficient for operator/debug review.
- Assertions:
  - `runtime.jsonl` shows `ActionType = "task_continuity"` records whose `Target` values include `observed_active_todo`, `action_submitted`, `command_terminal`, and then `feedback_attempted`;
  - for player-promised blocked/failed tasks, `feedback_missing` does not satisfy this test and should fail the main acceptance path;
  - when the agent updates task truth, `runtime.jsonl` also shows `Target = "todo_update_tool_result"`;
  - tests assert structured `ActionType` / `Target` / `Stage` / `Result` fields before any freeform `Error` detail;
  - the log record references the same session/task lifecycle under the NPC runtime.

### 8. `StardewAutonomyTickDebugServiceTests.RunOneTickAsync_WithRepositoryGamingSkillRoot_PreservesTaskContinuityAndVisibleFeedbackGuidance`

- Intent:
  - Keep repo-asset-backed prompt guarantees.
- Assertions:
  - required skill text still includes:
    - todo/memory/session_search division,
    - `stardew_task_status`,
    - blocked/failed short reasons,
    - player-visible feedback expectation;
  - no Minecraft/DAG/second-task-system leakage appears.

### 9. `StardewNpcToolFactoryTests.StardewMove_PublicContract_RemainsDestinationIdOnly`

- Intent:
  - Prevent regression from the semantic destination contract back to label/coordinate/facing inputs.
- Assertions:
  - `stardew_move` schema/description requires or documents `destinationId` as the public destination input;
  - public schema/description does not expose `label`, `x`, `y`, `tile`, `facingDirection`, or coordinate/facing substitutes;
  - repository navigation/world skill guidance still instructs agents to use canonical destination IDs, not labels or raw coordinates.

## Test Fixtures / Doubles Needed

1. Chat client double that:
   - writes/updates a todo from private chat,
   - later chooses Stardew action tools in autonomy,
   - can simulate blocked/failed follow-up decisions.
2. Command service double that:
  - returns terminal `blocked` / `failed` statuses for `stardew_move` or `stardew_speak`,
  - exposes those results to the runtime/background service and later autonomy prompt context.
3. Runtime log capture fixture around `runtime.jsonl`.
4. Real repository skill-root fixture for prompt-boundary assertions.

## Log Event Mapping

All closure evidence events must map onto the existing `NpcRuntimeLogRecord` shape:

- `ActionType = "task_continuity"`.
- `Target` is one of:
  - `observed_active_todo`
  - `action_submitted`
  - `command_terminal`
  - `todo_update_tool_result`
  - `feedback_attempted`
  - `feedback_missing`
- `Stage` identifies the lifecycle phase.
- `Result` carries the machine status.
- `CommandId` is present for command-related events when available.
- `Error` is optional detail and must not be the only assertion surface.
- For player-promised blocked/failed task closure, `feedback_attempted` is required. `feedback_missing` is reserved for negative diagnostic coverage or non-player-commitment/tool-unavailable cases.

## Commands

### Primary

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~NpcRuntimeSupervisorTests|FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewAutonomyTickDebugServiceTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests"
```

### Full Project Safety Net

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## Review Checklist

1. Does every new behavior assertion bind back to the existing root NPC session rather than a shadow session?
2. Do tests prove action-tool usage, not just prompt wording?
3. Is blocked/failed `reason` asserted as short fact output rather than long freeform explanation?
4. Do tests preserve the boundary that runtime surfaces command truth, while only agent `todo` / `todo_write` updates task truth?
5. Do prompt/skill tests use real repo skill assets where continuity boundaries matter?
6. Does every player-promised blocked/failed success path require `feedback_attempted`, with `feedback_missing` limited to negative diagnostics or non-player-commitment/tool-unavailable cases?
7. Does a mechanical tool-contract test prevent `stardew_move` from exposing `label`, raw coordinates, tile fields, or `facingDirection` as public inputs?
