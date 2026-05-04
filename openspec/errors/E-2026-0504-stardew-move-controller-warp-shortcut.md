# E-2026-0504-stardew-move-controller-warp-shortcut

- id: E-2026-0504-stardew-move-controller-warp-shortcut
- title: Stardew move fix delegated execution to NPC controller and faked cross-location completion with warp
- status: active
- updated_at: 2026-05-04
- keywords: [stardew, npc-autonomy, stardew_move, PathFindController, warpCharacter, cross_location_unsupported, destinationId, path_empty]
- trigger_scope: [stardew, bridge, move, bugfix, review]

## Symptoms

- A move "fix" makes the mod build but Bridge move regression tests fail.
- Code review finds `npc.controller = new PathFindController(...)` in the move pump.
- Code review finds `Game1.warpCharacter(...)` used to handle cross-location move.
- `stardew_move` tells the model to prefer `destination[n].destinationId`, but the tool still rejects a valid id such as `town.fountain`.

## Root Cause

- The fix treated `path_empty` as permission to bypass the planned Bridge-owned movement architecture.
- It delegated movement execution to Stardew's `NPC.controller`, even though the project plan only allows using `PathFindController.findPathForNPCSchedules()` as a route probe.
- It used `warpCharacter` for cross-location requests without a transition state machine, leaving the command status in `queued` and risking fake completion or lost active command state.
- The public tool contract was updated before the parser was updated, so `destinationId` became documented but not executable.

## Bad Fix Paths

- Do not fix same-location `path_empty` by assigning `npc.controller`; Bridge must keep execution ownership.
- Do not use `warpCharacter` as a shortcut for cross-location move before implementing real transition phases.
- Do not change tool descriptions to prefer a new field unless the parser accepts that exact field.
- Do not rename stable error codes such as `path_blocked`/`path_unreachable` without updating public contracts, tool guidance, and tests together.

## Corrective Constraints

- Bridge move execution must use schedule pathfinding as a probe, then consume the prepared route through the Bridge stepper.
- Arrival fallback must resolve to a neighbor that is both passable and reachable from the current tile.
- Cross-location move must remain explicitly blocked, or be implemented through a real transition state machine; no fake warp completion.
- `stardew_move` must match both `destination[n].destinationId` and `destination[n].label`.
- Regression tests must assert the absence of `npc.controller = new PathFindController` and `Game1.warpCharacter` in the production move path.

## Verification Evidence

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~MoveTool_WhenDestinationIdIsProvided_ResolvesObservedDestination"` passed.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMoveCommandQueueRegressionTests|FullyQualifiedName~BridgeMoveFailureMapperTests"` passed, 9/9.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` passed, 66/66.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew"` passed, 123/123.
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug` passed with 0 warnings and 0 errors.

## Related Files

- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeMovementPathProbe.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
