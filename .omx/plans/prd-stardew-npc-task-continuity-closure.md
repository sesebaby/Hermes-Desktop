# Stardew NPC Task Continuity Closure PRD

## RALPLAN-DR Summary

### Principles

1. Preserve the single Hermes-native continuity surface: `todo` + `memory` + `session_search` + per-NPC session/runtime state. No second task system.
2. Keep the NPC agent as the decision-maker. The host/bridge only exposes facts, tools, command truth, and logs.
3. Prove closure with action evidence, not narration: a player promise must survive into autonomy, drive Stardew tool use, surface terminal command truth, let the agent update todo state, and leave runtime/UI-visible traces.
4. Copy reference-project structure, not reference-project baggage: inherit HermesCraft/Hermes Agent continuity patterns and only VillagerAgent's feedback-loop visibility, without importing DAG/task-graph machinery.

### Decision Drivers

1. `P0` requires an end-to-end closure proof from private chat promise to autonomy execution, terminal feedback, and visible evidence.
2. The repository already has the substrate (`ToolSessionId` / `TaskSessionId`, todo projection, restart hydration, `stardew-task-continuity` skill), so the plan must target closure gaps rather than rebuild foundations.
3. OpenSpec constraints forbid second task stores, host-side promise detection, cross-map movement, public move coordinate inputs, and host-led NPC decisions.

### Viable Options

#### Option A: Prompt/skill-first hardening with targeted runtime diagnostics

- Shape: tighten autonomy/private-chat prompts and Stardew skill text, add/adjust small runtime evidence hooks and narrow loop behavior, then lock the closure with integration-style tests.
- Pros:
  - Best matches HermesCraft/Hermes Agent: agent-led todo continuity over host orchestration.
  - Low architectural risk because it reuses existing `todo`, task projection, autonomy loop, and runtime log surfaces.
  - Keeps the implementation path narrow and reversible.
- Cons:
  - Relies on prompt/tool-contract quality; weak assertions can still allow regressions where the model narrates instead of acting.
  - May need one or two carefully scoped runtime hooks to make failure/feedback evidence machine-checkable.

#### Option B: Host-mediated continuity controller inside Stardew runtime

- Shape: add a runtime-side controller that interprets private chat promises, decides when to update todo, injects player feedback, and converts terminal command status into task transitions.
- Pros:
  - More deterministic closure behavior.
  - Easier to assert mechanically in tests.
- Cons:
  - Violates the current architecture and reference direction by shifting intent ownership from agent to host.
  - Risks becoming a de facto second task system / promise detector.
  - Encourages brittle game-specific logic rather than Hermes-native continuity habits.

#### Option C: VillagerAgent-style task graph/state machine overlay

- Shape: keep current todo substrate but add graph/state-machine structures to decompose and track NPC work.
- Pros:
  - Rich status visibility.
  - Explicit blocked/running/failed semantics.
- Cons:
  - Over-scoped for `P0`.
  - Conflicts with the “single truth = todo” rule and would reintroduce a second task layer by indirection.
  - Pulls in reference baggage the user explicitly rejected.

### Recommended Option

Choose **Option A**.

It best fits the repo’s actual state: the system already routes private-chat tool writes to long-term session, hydrates todo state on restart, injects active todo into autonomy, and exposes runtime task view plus `runtime.jsonl`. The missing work is not a new scheduler or controller; it is closing the proof that the agent uses Stardew tools to advance or explain the task, sees terminal `blocked` / `failed` command truth, writes short todo reasons itself through `todo` / `todo_write`, and leaves coherent player/UI-visible evidence across the same NPC runtime surfaces.

## ADR Draft

### Decision

Implement `P0` task continuity closure by reinforcing the existing per-NPC Hermes agent loop: private chat commits to long-term `todo`, autonomy consumes the same active todo, Stardew action tools perform or fail the work, runtime exposes terminal command truth, the agent updates the same todo with short reasons through `todo` / `todo_write`, and runtime/UI surfaces expose that lifecycle without adding a second controller or task store.

### Drivers

- Existing substrate already supports long-term task continuity across private chat, autonomy, restart hydration, and task-view projection.
- Reference projects favor standard Hermes tools plus game interface, not game-owned task orchestration.
- The requested `P0` acceptance criteria are evidence-oriented and can be met with narrow loop/prompt/log changes plus tests.

### Alternatives Considered

- Host-managed continuity controller inside Stardew runtime: rejected because it shifts agency away from the NPC and trends toward a promise detector.
- DAG / graph-based task manager overlay: rejected because it duplicates `todo` semantics and exceeds `P0`.

### Why Chosen

This path is the smallest change set that satisfies the closure proof while preserving the architecture already converging toward HermesCraft/Hermes Agent.

### Consequences

- Prompt, skill, tool-result, and logging contracts become more important and must be regression-tested with real repo skill assets.
- The system will intentionally fail loudly when the agent narrates actions instead of calling Stardew tools.
- Some runtime evidence may need expansion so UI/debug consumers can distinguish “observed”, “actioned”, and “terminal failure explained”.

### Follow-ups

1. If `P0` is stable, `P1` can deepen world/social/navigation knowledge without changing the continuity architecture.
2. If repeated regressions show insufficient observability, add structured runtime events before adding any new control layer.

## Scope

- Focus on closure gaps only.
- Preserve completed substrate:
  - `ToolSessionId` / `TaskSessionId`
  - `todo` projection into long-term NPC session
  - restart hydration
  - `skills/gaming/stardew-task-continuity/SKILL.md`
- Do not add:
  - second task store
  - host promise detector
  - public `stardew_move` coordinate/label inputs
  - cross-map movement implementation
  - host-authored NPC decisions

## Requirements

### Functional Requirements

1. A private-chat commitment that the NPC accepts must continue to write/update `todo` in `descriptor.SessionId`, while the transcript session remains `${descriptor.SessionId}:private_chat:{conversationId}`.
2. A later autonomy tick must receive the existing active todo as concrete prompt/runtime context and treat it as a continuation candidate before freeform idling.
3. When autonomy progresses a task, success/progress must be grounded in Stardew action tools such as `stardew_move`, `stardew_task_status`, `stardew_speak`, or `stardew_open_private_chat`, not narration-only final text.
4. When a Stardew command reaches terminal `blocked` or `failed`, runtime surfaces may persist and expose that command truth through controller snapshot, `stardew_recent_activity`, prompt facts, and append-only logs; a subsequent agent/autonomy turn must call `todo` / `todo_write` to update the same long-term todo with a short fact-style `reason`.
5. When the blocked/failed todo came from a player commitment, the NPC must attempt player-visible feedback through `stardew_speak` or private chat path, still as an agent decision.
6. The resulting continuity evidence must be readable from both runtime surfaces:
  - `NpcRuntimeSupervisor.TryGetTaskView(...)`
  - `runtime.jsonl`

### Non-Functional Requirements

1. Tests involving prompt/skill boundaries must use real repository gaming skill assets where applicable.
2. Changes must stay narrow: prompt text, runtime evidence hooks, and small autonomy/tool handling adjustments are in scope; new orchestration layers are not.
3. Behavior must remain per-NPC/per-save/per-profile isolated.

## Acceptance Criteria

1. A regression test proves private-chat-created todo stays in the root NPC session and remains visible after supervisor restart, while the private-chat transcript session task view stays empty.
2. A regression test proves an autonomy turn that sees an active todo performs at least one Stardew action tool call rather than only returning narration.
3. A regression test proves a terminal `blocked` or `failed` Stardew command is first exposed as runtime command truth, then on a later agent/autonomy turn causes:
   - a `todo` / `todo_write` update in the same long-term session,
   - a non-empty short `reason` to be stored,
   - an attempted player feedback action (`stardew_speak` or private-chat path) when the task originated from player commitment.
4. A regression test proves the same blocked/failed lifecycle is visible in `runtime.jsonl` with machine-checkable entries, not only in freeform final chat text.
5. A regression test proves task-view/UI-facing snapshots expose the updated blocked/failed todo status and reason from transcript-backed `todo` tool-result projection, without direct runtime-controller mutation and without needing a fresh private-chat handle.
6. Prompt/skill tests prove the autonomy/private-chat prompts continue to teach:
   - todo/memory/session_search division of responsibilities,
   - Stardew tool use over narration,
   - blocked/failed reason discipline,
   - player feedback expectations.
7. A schema/description regression test proves `stardew_move` remains a destinationId-only public tool contract:
   - allowed public input: semantic `destinationId`;
   - forbidden public inputs: `label`, `x`, `y`, `tile`, `facingDirection`, or any coordinate/facing substitute.

## Implementation Steps

### Step 1: Baseline the closure gaps with end-to-end tests

Files:
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`

Intent:
- Add or refine tests that currently stop at substrate proof so they assert the missing closure: actual tool progression, terminal todo updates, player feedback attempt, and runtime/UI evidence.

Acceptance:
- New failing tests isolate the remaining gap without introducing new infrastructure assumptions.

### Step 2: Tighten autonomy/private-chat continuity contracts at the agent boundary

Files:
- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `skills/gaming/stardew-task-continuity/SKILL.md`
- `skills/gaming/stardew-social.md`
- `skills/gaming/stardew-navigation.md`

Intent:
- Keep the existing Chinese continuity guidance but strengthen the minimum behavioral contract around:
  - resuming player commitments from active todo,
  - using Stardew tools rather than narration,
  - updating blocked/failed with short reasons,
  - attempting visible player feedback when the blocked/failed task is a promise to the player.
- Add a mechanical guard that `stardew_move` public schema/description still instructs semantic `destinationId` only and does not expose label, tile, coordinate, or facing-direction fields.

Acceptance:
- Prompt/skill tests show the required language survives and remains repo-asset-backed.
- Tool-contract tests fail if `stardew_move` regresses from semantic destination IDs to labels, coordinates, tiles, or facing inputs.

### Step 3: Close the runtime loop where terminal command truth meets todo truth

Files:
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcRuntimeBindings.cs`
- `src/runtime/NpcRuntimeInstance.cs`

Intent:
- Ensure the authoritative Stardew command result path is captured as runtime command truth only.
- Feed that truth back into the next agent/autonomy decision surface through controller snapshot, prompt facts, and `stardew_recent_activity`, so the agent can decide whether to call `todo` / `todo_write`, `stardew_speak`, or private chat.
- Keep “task truth” agent-authored and transcript-backed; runtime/background code must not directly mutate todo state in response to terminal command status.

Acceptance:
- A blocked/failed action becomes visible as runtime command truth first, then produces matching task-view state only after the agent writes the todo update through existing tool-result projection.

### Step 4: Make runtime evidence first-class and machine-checkable

Files:
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcRuntimeLogWriter.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`

Intent:
- Expand or standardize `runtime.jsonl` entries so reviewers can trace:
  - tick observed active task
  - action tool submitted
  - terminal blocked/failed command result
  - later todo update tool result
  - feedback attempt emitted or missing
- Keep this as log evidence, not a second task record.

Minimum append-only event vocabulary:
- `observed_active_todo`
- `action_submitted`
- `command_terminal`
- `todo_update_tool_result`
- `feedback_attempted`
- `feedback_missing`

Event intent:
- `observed_active_todo`: current tick/prompt consumed an active root-session todo.
- `action_submitted`: NPC submitted a Stardew action command such as move/speak/open-chat.
- `command_terminal`: runtime observed terminal command truth such as `blocked`, `failed`, `completed`, or `cancelled`.
- `todo_update_tool_result`: agent later used `todo` / `todo_write`, producing transcript-backed task truth update.
- `feedback_attempted`: agent attempted `stardew_speak` or private-chat feedback after a player-facing blocked/failed outcome.
- `feedback_missing`: diagnostic-only event for non-player-commitment tasks, tool-unavailable negative tests, or explicit failure-observation tests; it must not satisfy the main player-promise blocked/failed acceptance path.

Minimum `NpcRuntimeLogRecord` mapping:
- `ActionType = "task_continuity"` for these closure evidence events.
- `Target` is one of the vocabulary values above.
- `Stage` carries the lifecycle phase: `observed`, `submitted`, `terminal`, `task_written`, `feedback`, or `diagnostic`.
- `Result` carries the machine status: `active`, `submitted`, `completed`, `blocked`, `failed`, `attempted`, or `missing`.
- `CommandId` is populated for action/command events when available.
- `Error` may contain a short reason/detail string, but tests must assert the structured fields first.

Turn-boundary rule:
- “Later agent/autonomy turn” means “not the background service and not any host/runtime code directly mutating todo”.
- The todo update may happen in the same Agent chat/tool loop after a Stardew tool result, or in a subsequent autonomy tick after command truth is exposed through `LastTerminalCommandStatus` / `stardew_recent_activity`.
- The forbidden path is runtime/controller/background code directly writing `SessionTodoStore` or fabricating a `todo` tool result.

Acceptance:
- Tests can assert exact runtime log records for the `P0` closure path.

### Step 5: Reconcile UI-facing task snapshots with runtime evidence

Files:
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`

Intent:
- Verify task-view snapshots surface the same blocked/failed status + reason seen in runtime actions/logs, including after restart and before any new chat handle is created.

Acceptance:
- UI consumers can read the closure state from existing `TryGetTaskView` APIs only.

## Risks and Mitigations

### Risk: model still narrates instead of acting

- Mitigation:
  - strengthen prompt/skill wording,
  - add diagnostics for “no action tool used” and “narrative move without `stardew_move`”,
  - require tests that assert tool calls, not just final text.

### Risk: todo reason updates drift from actual command failure

- Mitigation:
  - treat command result path as authoritative,
  - test blocked/failed mapping directly from tool result to task snapshot.

### Risk: player feedback becomes host-authored fallback behavior

- Mitigation:
  - keep feedback as agent tool usage expectation;
  - for player-promised blocked/failed tasks, the passing path requires agent-authored `feedback_attempted`;
  - `feedback_missing` is only a diagnostic/negative-test event and must not be accepted as player-promise closure;
  - host must not fabricate NPC speech.

### Risk: observability additions silently become a new state system

- Mitigation:
  - runtime evidence remains append-only logs plus existing task snapshots;
  - no new persistence model or scheduler.

### Risk: reference borrowing drifts into DAG/task-graph expansion

- Mitigation:
  - explicitly scope VillagerAgent borrowing to “feedback loop / status visibility only”.

## Verification

### Targeted Test Commands

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~NpcRuntimeSupervisorTests|FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewAutonomyTickDebugServiceTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests"
```

### Secondary Guardrail

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

### Evidence Review

1. Confirm todo lands in root session, not private-chat transcript session.
2. Confirm autonomy tick issues Stardew action tool calls for active todo continuation.
3. Confirm blocked/failed command truth appears in runtime evidence before any todo update.
4. Confirm a later agent/autonomy turn updates todo `status` + `reason` via `todo` / `todo_write`.
5. Confirm player-promised blocked/failed tasks include `feedback_attempted` in tool calls or runtime log evidence; `feedback_missing` is not acceptable on this main path.
6. Confirm `runtime.jsonl` and `TryGetTaskView` agree on the closure outcome, with task-view state sourced from transcript-backed todo projection.
7. Confirm `stardew_move` schema/description still exposes only semantic `destinationId` and does not expose `label`, coordinates, tile fields, or `facingDirection`.

## Execution Staffing

### Available Agent Types

- `planner`
- `architect`
- `critic`
- `executor`
- `debugger`
- `test-engineer`
- `verifier`
- `explore`

### Ralph Sequential

Recommended when:
- the team wants one owner to preserve the continuity model across prompts, tools, runtime logs, and tests;
- shared files like `NpcAutonomyLoop.cs` and `StardewNpcTools.cs` make parallel edits collision-prone.

Suggested lane order:
1. `executor` with high reasoning: add failing closure tests.
2. `executor` with high reasoning: implement narrow runtime/prompt/tool adjustments.
3. `test-engineer` with medium reasoning: harden test matrix and real-skill-asset coverage.
4. `verifier` with high reasoning: run targeted suite, inspect logs/task snapshots, then full desktop test suite.

Launch hint:
- `$ralph implement .omx/plans/prd-stardew-npc-task-continuity-closure.md with .omx/plans/test-spec-stardew-npc-task-continuity-closure.md`

### Team Parallel

Recommended when:
- you want to shorten cycle time by separating test design, runtime evidence mapping, and prompt/skill contract review.

Suggested staffing:
1. Lane A: `executor` high reasoning
   - Own `NpcAutonomyLoop.cs`, `StardewNpcTools.cs`, `StardewNpcAutonomyBackgroundService.cs`
2. Lane B: `test-engineer` medium reasoning
   - Own `NpcAutonomyLoopTests`, `NpcRuntimeSupervisorTests`, `StardewNpcAutonomyBackgroundServiceTests`
3. Lane C: `executor` / `writer` medium reasoning
   - Own `stardew-task-continuity`, `stardew-social`, `stardew-navigation`, private-chat wording checks
4. Lane D: `verifier` high reasoning
   - Own targeted command execution, runtime evidence inspection, and merge-time verification summary

Launch hint:
- `omx team run .omx/plans/prd-stardew-npc-task-continuity-closure.md`
- or `$team execute .omx/plans/prd-stardew-npc-task-continuity-closure.md`

### Team Verification Path

1. Lane B lands failing tests first.
2. Lane A lands runtime/tool fixes against those tests.
3. Lane C confirms prompt/skill assertions still use repo assets and no forbidden architecture drift appears.
4. Lane D runs targeted suite, then full `HermesDesktop.Tests`, and performs an evidence audit over:
   - task snapshots,
   - tool calls,
   - `runtime.jsonl` records.
