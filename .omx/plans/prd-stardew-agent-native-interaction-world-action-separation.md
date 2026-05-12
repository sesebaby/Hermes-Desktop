# Stardew Agent-Native Interaction / World Action Separation PRD

## Requirements Summary

Current hand test evidence shows a completed real move can be followed by private-chat lifecycle commands. Because runtime exposes a single `LastTerminalCommandStatus` as both `last_action_result` and recent `lastAction`, the private-chat window lifecycle can overwrite the fact the agent needs: the last real world action completed.

The fix must be agent-native:

- `todo` remains agent-owned task state.
- Runtime only classifies and reports mechanical facts.
- Interaction/window lifecycle records are not world-action results.
- The host does not decide task completion or next action.

## RALPLAN-DR Summary

### Principles

1. **Agent owns commitments**: player promises and task completion stay in `todo`.
2. **Host owns mechanical facts**: action execution and interaction sessions are facts, not task decisions.
3. **Separate fact surfaces**: real world action facts and interaction/window facts must not overwrite each other semantically.
4. **Generalize by classifying actions**: private chat is the first case; crafting, trade, quest, inventory, and gathering windows should use the same classification shape.

### Decision Drivers

1. The observed failure is state/fact pollution, not save-file or bridge failure.
2. Reference projects map `currentTask` to a real execution slot, not to agent task state.
3. Existing code already has `ActionSlot`, `PendingWorkItem`, `LastTerminalCommandStatus`, `todo`, and recent activity, so the least risky fix is classification/filtering.

### Viable Options

#### Option A: Add host `TaskState`

Rejected. It would make the host decide task meaning and completion, violating project memory and OpenSpec.

#### Option B: Split/label terminal facts without adding task state

Adopted. It keeps the existing execution pipeline and changes only the agent-visible fact surface:

- `last_action_result` only for terminal real world actions.
- `interaction_session` / `lastInteraction` for terminal interaction lifecycle actions.
- Existing `LastTerminalCommandStatus` remains a raw controller field for diagnostics and compatibility.

#### Option C: Add a fully persisted `InteractionSessionState` now

Deferred. It is likely the right long-term shape for crafting/trade/quest windows, but the current bug can be fixed without a migration or larger state schema change. The current implementation should name helpers so adding persisted interaction state later is straightforward.

## Functional Requirements

### FR1: World-action fact filtering

`NpcAutonomyLoop.BuildLastActionResultFact` must not emit `last_action_result` for interaction lifecycle terminal records such as:

- `open_private_chat`
- `private_chat`
- `private_chat_reply`
- `work_private_chat:*`
- `work_private_chat_reply:*`
- `private_chat:*`
- `private_chat_reply:*`

Instead it may emit a short interaction fact that clearly says this is UI/session state, not a real world action result.

### FR2: Active todo continuity remains tied to real world actions

Active todo continuity facts must remain based on real world action terminal statuses (`move`, normal `speak`, `idle_micro_action`) and must not trigger from private-chat/window lifecycle terminal records.

### FR3: Recent activity fact classification

`StardewRecentActivityProvider` must classify terminal records:

- real world action terminal -> `lastAction=...`
- interaction lifecycle terminal -> `lastInteraction=...`

This prevents future agent turns from reading an `open_private_chat` terminal as the last real action.

### FR4: Action-chain continuity classification

`StardewRuntimeActionController` may still use command slot / pending work mechanics for interaction commands, but interaction lifecycle actions must not update world-action continuity state:

- `OpenPrivateChat` must not start or extend `ActionChainGuard`.
- terminal `open_private_chat` / `private_chat_reply` statuses must not update chain `LastAction`, failure counters, or action-loop inputs.
- real world actions (`move`, normal `speak`, `idle_micro_action`) keep existing chain behavior.

### FR5: Agent-native boundary

No code should auto-complete, block, fail, or rewrite `todo` based on the classification. The agent sees facts and decides through visible tools.

## Non-goals

- No new task store.
- No host todo completion.
- No new MCP runner.
- No special Haley/Beach rule.
- No large persisted schema migration in this pass.

## Implementation Steps

1. Add RED tests:
   - `NpcAutonomyLoop` does not include `last_action_result` for `open_private_chat`; it includes an interaction fact.
   - `StardewRecentActivityProvider` emits `lastInteraction=` instead of `lastAction=` for private-chat lifecycle terminal.
2. Add small classification helpers in `NpcAutonomyLoop`.
3. Add real-controller-path RED test proving `OpenPrivateChat` does not create/update action-chain continuity.
4. Split controller classification into command-slot mechanics vs world-action continuity.
5. Add equivalent/private helper in `StardewRecentActivityProvider` or share only if it stays simple and local.
6. Run focused tests.
7. Run desktop build.
8. Run code review.

## Acceptance Criteria

- A terminal `open_private_chat` status does not produce `last_action_result`.
- The same terminal status does not produce recent activity `lastAction=`.
- It does produce an interaction/window fact so the agent still sees the mechanical fact.
- A real `OpenPrivateChat` controller path records terminal status but leaves `ActionChainGuard` null/unchanged.
- Existing completed move behavior still produces `last_action_result` and active todo continuity.
- Focused Stardew/NPC tests pass.
- Desktop build succeeds.

## Verification Commands

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewNpcToolFactoryTests" -p:UseSharedCompilation=false
```

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false
```

## ADR

Decision:
Classify terminal command facts into world-action facts and interaction facts at the agent-visible prompt/recent-activity boundary. Do not add host task state.

Drivers:
- Agent-native constraints require `todo` to remain agent-owned.
- The bug is semantic pollution of fact surfaces.
- Existing raw terminal field is still useful for compatibility and diagnostics.

Alternatives considered:
- Host `TaskState`: rejected as a second task system.
- Persisted `InteractionSessionState` now: deferred until more window types exist or current raw terminal field is no longer enough.

Why chosen:
It fixes the observed failure with the smallest boundary-correct change and establishes the convention needed for future windows.

Consequences:
- `LastTerminalCommandStatus` remains raw; callers must use classification helpers before exposing it to agents as world-action state.
- A later PR can add persisted interaction session state without changing `todo`.

## Available-Agent-Types Roster

- `executor`: implement tests and classification helpers.
- `test-engineer`: verify focused regression coverage.
- `architect`: check agent-native boundary and future window extensibility.
- `code-reviewer`: review for behavior regressions and task-state drift.

## Follow-up Staffing Guidance

Ralph path:
- One executor performs test-first implementation.
- Architect/code-reviewer review the diff.

Team path:
- Not needed for this narrow fix.
