# Stardew Private Chat Move Thought Context

## Task statement

Manual Stardew testing found Hermes phone private-chat NPC messages sounding like the NPC's own thoughts instead of direct replies to the player. Also, `move` actions should be able to show a short inner thought above the NPC's head as movement starts.

## Desired outcome

- Private-chat replies read as direct player-facing dialogue, not inner monologue, narration, markdown, or assistant/process text.
- `stardew_move` can carry a short immersive thought line.
- The bridge displays that movement thought through the existing non-blocking NPC overhead bubble path when a move starts.

## Known facts and evidence

- `StardewNpcPrivateChatAgentRunner` builds the private-chat system prompt and returns the final `Agent.ChatAsync` text directly to `stardew_speak(private_chat)`.
- `PrivateChatOrchestrator` wraps that reply as `GameActionType.Speak` with `channel=private_chat`.
- `StardewCommandService` serializes move actions to `StardewMoveRequest`; current DTO has `target`, `reason`, `destinationId`, and `facingDirection`.
- `BridgeCommandQueue.EnqueueMove` accepts `MovePayload` and `PumpMoveCommand` owns visible movement execution.
- `NpcOverheadBubbleOverlay` already displays non-blocking bubbles and emits private-chat reply close events only when `privateChat=true`.

## Constraints

- Do not move decision-making into host-side Stardew branches.
- Do not use `NPC.controller` or `Game1.warpCharacter`.
- Do not revive retired private-chat `activeClickableMenu` lifecycle.
- Keep changes small and covered by tests.

## Unknowns / open questions

- Real game visual polish of the bubble still needs manual Stardew verification.

## Likely codebase touchpoints

- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Desktop/HermesDesktop.Tests/Stardew/*`
- `Mods/StardewHermesBridge.Tests/*`
