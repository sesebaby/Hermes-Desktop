# Context Snapshot: Stardew Move DestinationId Contract

- Task statement: Create a `$ralplan` consensus plan for the next reference-alignment feature after P0 continuity work: converge Stardew NPC move so Agent/Desktop submits only `destinationId`, and Bridge owns destination resolution and execution.
- Desired outcome: A non-interactive planning artifact that can be used as the next execution basis, without implementing code in this planning turn.

## Known Facts / Evidence

- Reference-project alignment evidence:
  - `external/hermescraft-main/bot/server.js:318` loads `mineflayer-pathfinder`; `:324-329` configures movement rules in the runtime layer.
  - `external/hermescraft-main/bot/server.js:1272-1283` exposes high-level `goto({ x, y, z })`, then lets `pathfinder.goto(goal)` execute with timeout/cancel cleanup. The Agent/tool boundary is "submit goal", not "micro-control every step."
  - `external/hermescraft-main/bot/server.js:2587-2640` exposes a background task/status endpoint, so long-running movement is runtime-owned and observable.
  - `external/hermescraft-main/bot/server.js:2685-2697` performs stuck detection and cancels/marks movement at the runtime layer, not through LLM coordinate repair.
  - `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/Wrappers/SquadMateStateHelper.cs:54-66` clears vanilla NPC movement controllers before custom movement, proving the control handoff pattern is established for Stardew NPCs.
  - `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/FollowerManager.cs:821-889` drives NPC movement frame-by-frame with facing, velocity, position updates, animation, and halt at terminal.
  - `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Pathfinding/AStarPathfinder.cs:102-149` resolves closest passable neighbor with optional reachability validation, matching the desired Bridge-side arrival resolution.
- Current gap document identifies the next high-priority gap as move contract convergence:
  - `docs/Hermes参考项目功能模块效果差距对比.md` says the move chain has started moving from coordinate micro-control to `destinationId`.
  - It also states `StardewMoveTool` still parses observation facts into `location/tile` and has not fully become "submit only destinationId; Bridge executes."
- Existing architecture design already fixed the direction:
  - `docs/superpowers/specs/2026-05-04-stardew-npc-move-architecture-design.md` says `stardew_move` should express only destination, Bridge owns execution until terminal state, and registry is the executable truth.
- Current Agent/Desktop implementation is still mixed:
  - `src/games/stardew/StardewNpcTools.cs:329` still advertises destinationId preferred plus destination label fallback.
  - `src/games/stardew/StardewNpcTools.cs:342` calls `ResolveDestinationAsync`.
  - `src/games/stardew/StardewNpcTools.cs:426` re-observes world facts before submitting move.
  - `src/games/stardew/StardewNpcTools.cs:440` accepts either `destinationId` or `label`.
  - `src/games/stardew/StardewNpcTools.cs:452` can synthesize a destination id from location/label.
  - `src/games/stardew/StardewNpcTools.cs:491` parses `destination[n]` fact strings back into location/tile/facingDirection.
  - `src/games/stardew/StardewNpcTools.cs:370` constructs `GameActionTarget("destination", locationName, new GameTile(x, y))`.
- `StardewCommandService` already supports destinationId-only emission:
  - `src/games/stardew/StardewCommandService.cs:27` reads `destinationId` from action payload.
  - `src/games/stardew/StardewCommandService.cs:35` builds `StardewMoveRequest`.
  - `src/games/stardew/StardewCommandService.cs:36` emits `Target = null` when target tile/location is absent.
  - `src/games/stardew/StardewCommandService.cs:42` passes `DestinationId`.
- Bridge already supports destination registry resolution:
  - `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:87` resolves move target.
  - `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:89` prefers `payload.DestinationId`.
  - `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:91` checks `BridgeDestinationRegistry`.
  - `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:94` returns stable `invalid_destination_id`.
  - `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:105` still has legacy target fallback.
- Current tests prove the mixed state:
  - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs:181` still expects destination label in move description.
  - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs:278` expects destinationId to resolve observed coordinates.
  - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs:517` already proves destinationId-only moves can create Desktop runtime claims.
  - `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs:52` covers destinationId-only bridge envelope.
  - `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs` covers destinationId registry success and unknown-id failure.

## Constraints

- Planning-only turn because user invoked `$ralplan`; do not execute implementation.
- Reply in Chinese.
- Do not revert user changes.
- No new dependencies.
- Pre-release project rule: prefer one implementation path; avoid new compatibility branches or shadow behavior.
- Host/Bridge can expose facts and execute actions; Agent chooses intent/destination, not coordinates or host-side execution details.
- Tests touching prompt/skill boundaries must use real repo assets when applicable.

## Unknowns / Open Questions

- Whether execution should remove Bridge legacy `target` fallback in the same slice or as a follow-up after Agent/Desktop emission is destinationId-only.
- Whether the parameter property should be renamed from `destination` to `destinationId` immediately. Keeping `destination` as the JSON property with destinationId-only semantics avoids broader schema churn; renaming is cleaner but expands blast radius.
- Exact affected test names may shift if current worktree changes before execution.

## Likely Touchpoints

- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `src/games/stardew/StardewQueryService.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
- `skills/gaming/stardew-navigation.md`
- `skills/gaming/stardew-world/SKILL.md`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewQueryServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`
