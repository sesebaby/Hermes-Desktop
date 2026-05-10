---
id: E-2026-0510-private-chat-ingress-slow-poll-and-bridge-cursor
title: Stardew private chat ingress was delayed or skipped by slow host polling and stale bridge cursor state
updated_at: 2026-05-11
keywords:
  - stardew
  - private_chat
  - npc_autonomy
  - bridge cursor
  - vanilla_dialogue_completed
  - vanilla_dialogue_unavailable
  - polling
---

## symptoms

- Player clicks an NPC and Hermes private chat opens tens of seconds later instead of immediately.
- Sometimes no private chat opens at all after a click, with no visible player feedback.
- SMAPI logs show `vanilla_dialogue_completed_fact` or `vanilla_dialogue_unavailable_fact`, but Desktop logs show no matching `action_open_private_chat_*` command.
- Desktop host logs can show a persisted `sourceSequence` higher than the current bridge event buffer sequence, followed by empty event batches.
- Desktop host logs can also show an internally inconsistent cursor such as `sourceCursor=evt_000000000003; sourceSequence=1`; this lets the bridge skip the first lower-sequence private-chat trigger because sequence is the primary cursor.

## trigger_scope

- Changing `StardewNpcAutonomyBackgroundService` polling, dispatch, host state, or event fanout.
- Changing `StardewRuntimeHostStateStore` cursor persistence or schema migration.
- Changing bridge discovery identity, `StartedAtUtc`, or same-save bridge rebinding behavior.
- Changing NPC click fallback paths that record `vanilla_dialogue_unavailable`.
- Debugging private chat open delays, missing `/action/open_private_chat`, or missing pending HUD feedback.

## root_cause

- Private chat ingress was consumed by the same background loop cadence as NPC autonomy. The default host poll interval was 20 seconds, so player-facing UI open could wait for the next autonomy poll.
- The host persisted a shared source cursor per save but did not persist which bridge instance produced that cursor. When SMAPI/bridge restarted for the same save, the new in-memory event buffer could restart at a lower sequence while Desktop reused the old higher sequence and skipped new click events.
- The same-save bridge rebind reset needed to rebase only the shared host cursor, not NPC tracker cursors. Tracker cursor preservation is correct for NPC autonomy continuity, but shared private chat ingress must poll the new bridge from root.
- `GameEventCursor.Advance` can combine the previous `Since` event id with a lower `NextSequence` from a reset bridge empty batch, producing a mixed old-id/new-sequence cursor. Because `BridgeEventBuffer.ResolveStartIndex` prioritizes `sequence`, `evt_000000000003/1` polls after sequence 1 and misses `evt_000000000001`.
- The `vanilla_dialogue_unavailable` fallback path recorded an event that Desktop would use to open private chat, but it did not show the same non-blocking pending HUD as the normal vanilla completion path.

## bad_fix_paths

- Lowering the host poll interval without adding a separate autonomy wake cadence; that turns empty polling into one autonomy LLM call per second.
- Resetting all NPC tracker cursors on same-save bridge rebind; that loses autonomy continuity and replays old NPC events.
- Treating the new bridge root batch as historical drain-only after an already-drained same-save bridge rebind; that swallows the first real click event on the new bridge.
- Fixing only the SMAPI overlay/HUD path while leaving Desktop to poll every 20 seconds.
- Fixing only Desktop polling while leaving persisted old bridge cursors able to skip new lower-sequence events.
- Replacing `/action/open_private_chat` with phone overlay behavior; this is a separate known bad path covered by `E-2026-0505-private-chat-input-menu-replaced-by-phone-plan`.

## corrective_constraints

- Host event polling for player-facing private chat ingress must be fast, currently one second by default.
- NPC autonomy LLM wake cadence must remain separately gated, currently 20 seconds by default, so fast empty polling does not run the parent LLM every second.
- Private chat ingress event types must bypass autonomy cooldown gates when needed: `vanilla_dialogue_completed`, `vanilla_dialogue_unavailable`, `player_private_message_submitted`, `player_private_message_cancelled`, and `private_chat_reply_closed`.
- Persist the bridge identity (`bridge_key`) with host shared cursor state.
- On same-save bridge identity changes, reset shared host source cursor and staged batch to root for the new bridge while preserving NPC tracker cursors.
- Normalize impossible canonical event cursors before bridge polling: if `Since` parses as `evt_N` and `N > Sequence`, rebase the shared poll to root so current-buffer lower sequence events are not skipped.
- When an empty poll response returns a `NextSequence` lower than the submitted cursor sequence, return a root cursor instead of preserving the old `Since`; otherwise the next tick can keep sending an impossible mixed cursor.
- Preserve old host cursor and initial drain state when adding `bridge_key` to an existing state row; schema migration must not silently rewind an existing bridge.
- The fallback `RecordDialogueFollowUpUnavailable` path must show `_overlay.SetPrivateChatPending(npcName)` before Desktop/core opens private chat.

## prompt_lessons

- Treat `PrivateChatInputMenu` opening as immediate UI ingress, not an NPC autonomy decision. If it takes more than one fast poll after `vanilla_dialogue_completed_fact` or `vanilla_dialogue_unavailable_fact`, debug event ingestion first.
- Ask for the timestamp chain in this order: SMAPI click/fact log, Desktop host batch log, Desktop `/action/open_private_chat` result, then bridge `action_open_private_chat_*`. Do not start with LLM/autonomy logs when the action was never submitted.
- When logs show a SMAPI fact but no Desktop action, inspect `sourceCursor`, `sourceSequence`, `nextCursor`, and `nextSequence`. Mixed values such as `evt_000000000003/1` are a cursor consistency bug, not a slow model.
- Remember that `BridgeEventBuffer` prioritizes `sequence` over `since`; a mismatched lower `sequence` can skip the current bridge's first click event even if `since` looks newer.
- Do not suggest phone overlay, host text parsing, or LLM decision gating as fixes for missing immediate input. `/action/open_private_chat` must remain the `PrivateChatInputMenu` path and return `input_menu_opened`.

## diagnostic_prompt_template

When private chat does not open immediately after clicking an NPC, first align SMAPI fact logs with Desktop host batch/action logs. If SMAPI recorded `vanilla_dialogue_completed_fact` or `vanilla_dialogue_unavailable_fact` but Desktop did not submit `/action/open_private_chat` within one to two seconds, prioritize `StardewEventSource` and host shared cursor state. Check whether `Since` and `Sequence` disagree or whether bridge `NextSequence` rolled back. Do not first blame LLM latency, NPC autonomy, or private-chat reply generation.

## verification_evidence

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:UseSharedCompilation=false --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewRuntimeHostStateStoreTests|FullyQualifiedName~StardewPrivateChatOrchestratorTests|FullyQualifiedName~StardewEventSourceTests"` passed 67/67.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~RawDialogueDisplayRegressionTests|FullyQualifiedName~BridgeEventBufferTests"` passed 23/23.
- `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false` succeeded with existing Stardew bridge warnings only.
- Regression coverage includes fast default host poll cadence, no repeated LLM turns on empty fast polls, same-save lower-sequence bridge rebind processing private chat trigger, `bridge_key` migration preserving cursor state, and fallback pending HUD display.

## related_files

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewRuntimeHostStateStore.cs`
- `Mods/StardewHermesBridge/ModEntry.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewRuntimeHostStateStoreTests.cs`
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs`
