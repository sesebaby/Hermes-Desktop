# Stardew Private Chat Open Decoupling Plan

## Requirements Summary

Click-triggered Stardew private chat must be split into two phases:

1. Open the private-chat input menu as a local bridge/UI action after the vanilla NPC dialogue path finishes or is unavailable.
2. Run AI only after the player submits text; if AI/provider/runtime is misconfigured or unavailable, show a clear failure in the chat flow instead of preventing the input menu from opening.
3. Preserve existing reply display routing: `source=input_menu` and `source=phone_overlay` remain route selectors; system-error authorship must use a separate metadata field such as `message_kind=system_error`.

Evidence:

- `src/game/core/PrivateChatOrchestrator.cs:89` already models the trigger-to-open transition before player text exists.
- `src/game/core/PrivateChatOrchestrator.cs:142` calls the agent only after `player_private_message_submitted`.
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:346` is the bridge-side point that actually opens the input menu.
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:211` currently short-circuits the whole host loop when there are no enabled autonomy NPCs, before private-chat event processing at `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:278`.
- This plan intentionally narrows the meaning of an empty autonomy allowlist: it disables per-NPC autonomy dispatch, but does not disable shared private-chat bridge event consumption.

## RALPLAN-DR Summary

### Principles

- UI-open independence: opening `PrivateChatInputMenu` must not depend on AI/provider success.
- Single bridge event owner: keep shared Stardew event polling/cursor ownership in one host path.
- Narrow behavior change: do not redesign NPC runtime or bridge UI unless a test proves it is necessary.
- Observable failure: AI failure should become a user-visible reply-phase error and a logged state, not silent non-opening.
- Test-first regression lock: add failing tests for the exact gating and AI-failure behavior before changing implementation.

### Decision Drivers

- User-facing correctness: clicking Haley/Penny should produce the input box immediately after vanilla dialogue handoff.
- Runtime safety: avoid duplicate polling or double-opening from multiple services consuming the same bridge events.
- Maintainability: preserve existing `PrivateChatOrchestrator` state machine because it already separates open and reply phases.

### Viable Options

#### Option A: Let the existing autonomy host always process private-chat events before allowlist gating

Pros:

- Smallest implementation surface.
- Reuses current single shared event cursor, staging, drain-only behavior, and bridge adapter.
- Directly fixes `enabledNpcCount=0` preventing `OpenPrivateChat`.
- Existing tests can be extended in `StardewNpcAutonomyBackgroundServiceTests`.

Cons:

- The service name remains "autonomy" even though it also hosts private-chat event processing.
- If no enabled NPCs exist, host state/cursor still advances for private-chat drainage and open triggers.

#### Option B: Add a separate `StardewPrivateChatBackgroundService`

Pros:

- Conceptually clearer separation between private chat and autonomous NPC ticks.
- Allows private chat to have its own settings later.

Cons:

- Existing tests intentionally assert `StardewPrivateChatRuntimeIsAnAdapterNotAnIndependentBackgroundLoop`.
- Creates duplicate bridge polling/cursor coordination risk.
- Larger change for the current bug.

#### Option C: Move open-private-chat handling into the SMAPI bridge click path

Pros:

- Fastest visible input open, fully local to the game.
- Does not depend on Desktop host availability for the input box.

Cons:

- Violates current boundary where bridge observes facts and host decides actions.
- Harder to keep NPC allowlist, session identity, and transcript continuity coherent.
- Would duplicate host-side policy in the bridge.

Chosen option: Option A.

Option B is rejected for this fix because the current architecture already made private chat an adapter owned by the host loop, and adding a second polling service risks duplicate consumption. Option C is rejected because it bypasses the established host-decision boundary.

## Acceptance Criteria

- A host test proves `StardewNpcAutonomyBackgroundService.RunOneIterationAsync` still processes a fresh `vanilla_dialogue_completed` or `vanilla_dialogue_unavailable` event into exactly one `GameActionType.OpenPrivateChat` even when `enabledNpcIds` is empty.
- A bridge regression proves `/action/open_private_chat` still creates and opens the real `PrivateChatInputMenu`, not the phone overlay or a fake host-side UI.
- A test uses an injectable counting `INpcPrivateChatAgentRunner` and proves the open-trigger path does not call the private-chat agent before `player_private_message_submitted`; `Requests.Count` stays `0` after `vanilla_dialogue_completed` and becomes `1` only after the submitted-message event.
- A test proves an `INpcPrivateChatAgentRunner` exception after player submission does not retroactively close/block the input-open phase and produces an explicit reply-phase failure behavior marked with `message_kind=system_error` or an equivalent independent metadata field, while preserving the existing display-route `source`.
- A test proves that empty-allowlist private-chat processing advances the shared host cursor or staged-batch state so rebuilding the service does not reopen the same clicked-dialogue event.
- Existing drain-only behavior remains intact: historical first attach batches do not open private chat.
- Existing autonomy behavior remains intact for enabled Haley/Penny, including normal NPC dispatch after private-chat event processing.
- Existing single-owner bridge polling remains intact: no new private-chat background loop is introduced, and the shared bridge event source is still polled only once for multiple enabled NPC workers.
- No local `config.yaml` or secret-bearing file is committed.

## Implementation Steps

1. Add regression tests for private-chat event processing without enabled autonomy NPCs.

   Target: `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`.

   Add a test near `RunOneIterationAsync_WhenInitialAttachPollsEmptyBatch_NextPrivateChatTriggerIsProcessed` that constructs the service with `enabledNpcIds: []`, a fresh `vanilla_dialogue_completed` event for Haley, and a `RecordingCommandService`. Extend the `CreateService(...)` test helper so the private-chat adapter can receive a counting `INpcPrivateChatAgentRunner` instead of the current fixed `NoopPrivateChatAgentRunner`. Expected result: one `OpenPrivateChat` command is submitted, zero autonomy supervisors are started, `CountingChatClient.CompleteWithToolsCalls == 0`, and the counting private-chat runner has `Requests.Count == 0`.

2. Move the no-enabled-NPC early return below shared bridge discovery/event polling/private-chat processing.

   Target: `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`.

   Keep discovery unavailable and missing save ID behavior unchanged. After `_privateChatRuntimeAdapter.ProcessAsync(...)`, if `_enabledNpcIds.Count == 0`, commit any staged shared-event cursor state, log `reason=no_enabled_npcs_after_private_chat_processing`, and return before per-NPC tracker dispatch.

3. Preserve host cursor and initial drain semantics through one finalize path.

   Target: `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`.

   Ensure the no-enabled-NPC return path runs the same `CommitBatchAsync(...)` / initial-drain commit logic currently used after worker dispatch. Extract a small private helper for shared batch finalization rather than copying the commit branches. Document in a code comment near the allowlist gate that `enabledNpcIds` controls per-NPC autonomy workers only, not shared private-chat event handling.

   Add a regression that processes a fresh click-trigger event with `enabledNpcIds: []`, then rebuilds the service using the same host state store/runtime root and confirms the same event is not reopened a second time.

4. Add reply-phase AI failure behavior test.

   Target: `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs` or `Desktop/HermesDesktop.Tests/GameCore/PrivateChatOrchestratorTests.cs`.

   Use a fake agent runner that throws from `ReplyAsync`. Drive the state machine through open trigger and submitted player message. Assert `OpenPrivateChat` was already submitted before failure, and assert the selected failure behavior preserves the display-route `source` value from the submitted event (`input_menu` or `phone_overlay`) while carrying `message_kind=system_error` or an equivalent independent metadata marker.

5. Implement reply-phase failure message only if current behavior is insufficient.

   Target: likely `src/game/core/PrivateChatOrchestrator.cs`.

   Current code catches agent exceptions and calls `EndSession()` silently at `src/game/core/PrivateChatOrchestrator.cs:146`. Change this narrow block so it submits a `Speak` action with a concise private-chat error text such as `AI connection failed. Check Hermes provider settings.`. Preserve `source=input_menu` / `source=phone_overlay` because `Mods/StardewHermesBridge/Ui/StardewMessageDisplayRouter.cs` uses `source` for display routing. Add a separate payload field such as `message_kind=system_error` to mark authorship/failure semantics. Keep this scoped to the reply phase. If the bridge cannot visually style system-error replies yet, still preserve the marker in payload/logs and test it so a later UI polish can style it without reworking core state.

6. Run focused verification.

   Commands:

   - `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewPrivateChatOrchestratorTests|FullyQualifiedName~PrivateChatOrchestratorTests"`
   - `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewPrivateChatWiringTests|FullyQualifiedName~RunOneIterationAsync_WithMultipleEnabledNpc_PollsBridgeEventsOnlyOnce"`
   - `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~RawDialogueDisplayRegressionTests"`
   - `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64`

7. Manual/log verification after implementation.

   Restart Hermes Desktop and Stardew/SMAPI bridge. Click Haley and Penny after vanilla dialogue. Confirm:

   - `private_chat_opened` or `action_open_private_chat_completed` appears in SMAPI/bridge logs.
   - Hermes logs no longer show private-chat open suppressed solely by `reason=no_enabled_npcs`.
   - If provider config is intentionally broken, the input menu still opens; only the reply phase reports AI failure.

## Risks and Mitigations

- Risk: Advancing shared bridge cursor when no NPCs are enabled could consume events that would later matter after config is fixed.
  Mitigation: private-chat processing already owns shared event staging; add a test documenting cursor advancement, initial drain behavior, and no duplicate open after service rebuild.

- Risk: AI failure message could be mistaken for NPC-authored dialogue.
  Mitigation: use a clearly system-ish failure string and keep payload `channel=private_chat` plus `message_kind=system_error` or equivalent metadata. Preserve `source=input_menu` / `source=phone_overlay` for display routing; do not rely on text alone for authorship semantics.

- Risk: Reordering the no-enabled-NPC gate could start autonomy runtime accidentally.
  Mitigation: tests assert `NpcRuntimeSupervisor.Snapshot().Count == 0` and `chatClient.CompleteWithToolsCalls == 0` when no NPCs are enabled.

- Risk: Retryable bridge menu-blocked errors could interact with empty allowlist processing.
  Mitigation: keep `PrivateChatOrchestrator` pending-open retry logic unchanged; only move the host-level gate.

## Verification Steps

- Run the focused test command above and confirm all targeted tests pass.
- Run the bridge regression filter to ensure input menu lifecycle remains a real Stardew menu.
- Run `StardewPrivateChatWiringTests` and `RunOneIterationAsync_WithMultipleEnabledNpc_PollsBridgeEventsOnlyOnce` to prove no second background loop or duplicate polling path was introduced.
- Run the Desktop build to catch analyzer/compiler errors.
- Inspect `git diff` and verify no secret config or unrelated `.omx/state/session.json` is staged.

## ADR

### Decision

Use the existing `StardewNpcAutonomyBackgroundService` as the single bridge-event owner, but process shared private-chat events before gating per-NPC autonomy dispatch on `npc_autonomy_enabled_ids`.

### Drivers

- The bridge input menu is already a command-driven UI action, not an AI action.
- The current service already stages shared events and calls `StardewPrivateChatRuntimeAdapter`.
- The user explicitly requires AI misconfiguration to affect only the reply phase.

### Alternatives considered

- Add a new private-chat background service.
- Open the private-chat menu directly from SMAPI click handling.
- Treat config sync as sufficient and leave code unchanged.

### Why chosen

This is the least invasive change that preserves current architecture and directly fixes the observed coupling between autonomy config and private-chat UI opening.

### Consequences

- The autonomy host name remains broader than pure autonomy.
- Empty allowlist no longer means "do nothing"; it means "process shared private-chat bridge events, finalize shared cursor/staging state, then skip autonomy workers."
- Future maintainers must keep private-chat event processing before per-NPC autonomy gating.
- `enabledNpcIds` is not a private-chat UI-open allowlist. It controls per-NPC autonomy workers only.

### Follow-ups

- Consider renaming or documenting the service as a Stardew host loop if this dual ownership keeps expanding.
- Add user-facing settings text later to clarify that private chat click handling and autonomous NPC ticking are separate capabilities.

## Available-Agent-Types Roster

Relevant available Codex native roles:

- `explore`: fast file/symbol mapping.
- `debugger`: root-cause and regression isolation.
- `executor`: implementation/refactoring.
- `test-engineer`: regression test design and coverage.
- `verifier`: completion evidence and test adequacy.
- `code-reviewer`: final logic and maintainability review.
- `architect`: architecture boundary review.
- `critic`: plan/design challenge.

## Follow-up Staffing Guidance

### Ralph path

Use one sequential owner:

- `executor`, reasoning high: add tests, implement host gate reorder and reply-failure behavior.
- `verifier`, reasoning high: run targeted tests/build and inspect staged diff before commit.

Suggested launch:

```text
$ralph execute .omx/plans/stardew-private-chat-open-decoupling-plan.md
```

### Team path

Use parallel lanes only if speed matters:

- Lane 1 `test-engineer`, reasoning medium: write/adjust the no-enabled-NPC host regression tests.
- Lane 2 `executor`, reasoning high: implement the host gate reorder and commit helper extraction.
- Lane 3 `executor`, reasoning high: implement reply-phase AI failure behavior only after the failing test exists.
- Lane 4 `verifier`, reasoning high: run focused tests/build after integration.

Suggested launch:

```text
$team execute .omx/plans/stardew-private-chat-open-decoupling-plan.md
```

## Team Verification Path

- Team must prove tests cover both "input opens without enabled autonomy NPCs" and "agent failure happens after open."
- Team must prove no duplicate bridge polling/background loop was introduced.
- Team must prove targeted Desktop tests, bridge regression tests, and Desktop build pass.
- Ralph/final owner must inspect `git status --short --branch` and commit only source/test/doc changes relevant to this plan.

## Plan Changelog

- Created from code/log evidence gathered on 2026-05-06.
- Captures user correction that AI configuration errors must not block private-chat input opening.
- Applied Architect feedback: clarified empty-allowlist event-consumption semantics, required a single shared finalize path, added no-duplicate-open cursor coverage, and required system-error source marking for AI failure replies.
- Applied Critic feedback: required a counting private-chat agent runner test, preserved `source` as display routing while moving system-error authorship to a separate metadata field, made the host-to-bridge input-menu chain explicit, and added single-owner polling verification.
