# Stardew Private Chat Open Decoupling Context

## Task statement

Plan a fix for Stardew NPC private chat so the input dialogue opens immediately after the vanilla NPC click/dialogue handoff, even when AI/provider/NPC autonomy configuration is wrong or unavailable. AI failures should surface after the player submits text, not prevent the input menu from opening.

## Desired outcome

- Clicking an enabled/recognized Stardew NPC such as Haley or Penny still routes through the existing vanilla-dialogue observation path.
- The host sends `OpenPrivateChat` to the bridge before any LLM reply attempt.
- If the AI/provider/runtime fails later, the already-open private chat should be able to show a clear connection-failure response or otherwise fail only in the reply phase.
- Missing or empty `stardew.npc_autonomy_enabled_ids` must not be the reason a click-triggered private chat input menu never opens.

## Known facts and evidence

- Local config was previously missing `stardew.npc_autonomy_enabled_ids`, causing `enabledNpcCount=0` / `reason=no_enabled_npcs`; this explained one environment mismatch, but it is not an acceptable dependency for opening the input menu.
- Current local log now shows `enabledNpcCount=2` and the private-chat runtime bridge attached, so the config sync itself is no longer the immediate local blocker.
- `Desktop/HermesDesktop/App.xaml.cs:432` registers `StardewPrivateChatRuntimeAdapter` with the real `INpcPrivateChatAgentRunner`.
- `Desktop/HermesDesktop/App.xaml.cs:437` wires that adapter into `StardewNpcAutonomyBackgroundService`.
- `Desktop/HermesDesktop/App.xaml.cs:453` constructs `StardewNpcAutonomyBackgroundOptions` from `ReadConfigList("stardew", "npc_autonomy_enabled_ids")`.
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:211` returns early when `_enabledNpcIds.Count == 0`, before discovery, event polling, and `_privateChatRuntimeAdapter.ProcessAsync(...)`.
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:278` processes shared bridge events through the private-chat runtime before dispatching per-NPC autonomy workers.
- `src/game/core/PrivateChatOrchestrator.cs:89` opens private chat after vanilla-dialogue trigger, sets `PendingOpen`, and submits `OpenPrivateChat` at `src/game/core/PrivateChatOrchestrator.cs:228`.
- `src/game/core/PrivateChatOrchestrator.cs:115` calls `_agentRunner.ReplyAsync(...)` only after the `player_private_message_submitted` event and state is `AwaitingPlayerInput`.
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:253` enqueues an open-private-chat UI command.
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:346` sets `Game1.activeClickableMenu = inputMenu` and records `private_chat_opened`.
- Existing tests already assert direct orchestrator behavior: `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs:14` confirms vanilla-dialogue completed submits `OpenPrivateChat` and does not call the fake agent.
- Existing background-host tests include `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs:1013`, which confirms a fresh private-chat trigger is processed after initial attach when NPCs are enabled.

## Constraints

- Keep bridge as the UI executor; do not move Stardew UI drawing into Desktop.
- Do not commit local secret-bearing config files.
- Preserve each NPC's independent runtime/session/memory boundary.
- Keep implementation narrow and test-backed.
- Do not make AI/provider availability a precondition for opening the input menu.
- Avoid introducing a second long-lived private-chat background loop unless evidence shows the existing host cannot own the shared event cursor safely.

## Unknowns / open questions

- Whether click-triggered private chat should be available for all vanilla NPCs, or only allowlisted NPCs, when autonomy allowlist is empty. The current user request focuses on Haley and Penny as active NPCs, but the architectural bug is that AI/autonomy config can suppress the UI open path entirely.
- Whether the post-submit AI failure should display a bridge `Speak` message, reopen the input with an error prompt, or record a clear bridge event only. User explicitly requires an AI connection-failure hint; execution should choose the least invasive existing UI path.
- Whether current game logs contain a post-submit AI failure case; the supplied logs primarily show open-trigger and config-gating symptoms.

## Likely touchpoints

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/game/core/PrivateChatOrchestrator.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs`
- Possibly `src/games/stardew/StardewNpcPrivateChatAgentRunner` failure handling via `INpcPrivateChatAgentRunner`
- Possibly bridge-side tests only if UI error message behavior changes
