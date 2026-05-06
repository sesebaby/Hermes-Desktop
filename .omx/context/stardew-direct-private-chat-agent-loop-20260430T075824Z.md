# Stardew Direct Private Chat Agent Loop Context

- task statement: Plan the next implementation slice for Haley private chat so the game opens the private-chat input immediately after vanilla dialogue ends, then sends the submitted player message to the Desktop/core Haley agent for a response.
- desired outcome:
  - Player-facing flow:
    1. Click Haley and finish vanilla dialogue.
    2. HUD: `海莉知道你想和她聊天`.
    3. Open private-chat input directly.
    4. Player submits text.
    5. Input closes; HUD: `海莉正在思考怎么回答你`.
    6. Desktop/core Haley agent generates a reply.
    7. Game displays Haley's reply.
    8. Optionally reopen input for another round when Haley remains willing to chat.
  - Avoid the current dead wait state where HUD appears but no Desktop tick runs.
- known facts/evidence:
  - Current bridge records `vanilla_dialogue_completed` and sets pending HUD in `Mods/StardewHermesBridge/ModEntry.cs`.
  - Current `BridgeCommandQueue.OpenPrivateChat` opens a Stardew text-entry menu and records `player_private_message_submitted`, but only after Desktop/core explicitly calls `/action/open_private_chat`.
  - Manual test evidence from SMAPI/bridge logs showed `vanilla_dialogue_completed_fact` was recorded but no `action_open_private_chat_*` events occurred; therefore the command never reached the mod.
  - `StardewAutonomyTickDebugService.RunOneTickAsync` currently runs a one-shot autonomy tick from the Desktop debug button, not an automatic private-chat response path.
  - NPC tool allowlist already includes `stardew_speak` and `stardew_open_private_chat` via `StardewNpcToolFactory`.
- constraints:
  - Preserve the broad autonomy boundary: ordinary game/environment events remain facts only.
  - Introduce a narrow exception for explicit player private-chat submissions: player text is a direct conversational input to the Haley agent, not an environment event.
  - SMAPI/mod must not host LLM calls or Agent state. It may open UI, collect input, record facts, and expose bridge routes.
  - Desktop/core owns Haley persona/session/memory/tool execution and sends the reply/action back to SMAPI.
  - No new dependencies.
  - Work with existing uncommitted changes and do not revert user/previous-agent edits.
- unknowns/open questions:
  - Whether first implementation should be manual Desktop-poll triggered or background polling; the desired UX implies background/private-chat response service.
  - Whether continued chat should always reopen input after every reply or only when Agent calls `stardew_open_private_chat`; plan should keep this Agent-controlled.
  - How much transcript/memory persistence is needed in this slice; should use existing NPC namespace where feasible.
- likely codebase touchpoints:
  - `Mods/StardewHermesBridge/ModEntry.cs`
  - `Mods/StardewHermesBridge/Ui/BridgeStatusOverlay.cs`
  - `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
  - `Mods/StardewHermesBridge/Bridge/BridgeEventBuffer.cs`
  - `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
  - `src/games/stardew/StardewEventSource.cs`
  - `src/games/stardew/StardewAutonomyTickDebugService.cs`
  - `src/runtime/NpcAutonomyLoop.cs`
  - `Desktop/HermesDesktop/App.xaml.cs`
  - `Desktop/HermesDesktop.Tests/Runtime/*`
  - `Desktop/HermesDesktop.Tests/Stardew/*`
  - `Mods/StardewHermesBridge.Tests/*`
