---
id: E-2026-0510-private-chat-ingress-slow-poll-and-bridge-cursor
title: Stardew private chat ingress was delayed or skipped by slow host polling and stale bridge cursor state
updated_at: 2026-05-10
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
- Preserve old host cursor and initial drain state when adding `bridge_key` to an existing state row; schema migration must not silently rewind an existing bridge.
- The fallback `RecordDialogueFollowUpUnavailable` path must show `_overlay.SetPrivateChatPending(npcName)` before Desktop/core opens private chat.

## verification_evidence

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:UseSharedCompilation=false --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewRuntimeHostStateStoreTests|FullyQualifiedName~StardewPrivateChatOrchestratorTests|FullyQualifiedName~StardewEventSourceTests"` passed 65/65.
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
