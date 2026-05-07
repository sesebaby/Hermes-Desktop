# E-2026-0507-stardew-cross-map-warp-approach-trigger-tile

- id: E-2026-0507-stardew-cross-map-warp-approach-trigger-tile
- title: Stardew cross-map move conflated reachable warp approach tiles with warp trigger tiles
- status: active
- updated_at: 2026-05-07T20:45:00+08:00
- keywords: [stardew, npc-autonomy, stardew_move, Haley, Beach, cross_location, segment_path_unreachable, warp_approach_tile, warp_trigger_tile, handleWarps]
- trigger_scope: [stardew, bridge, move, bugfix, diagnostics]

## Symptoms

- Clicking the desktop debug action to send Haley to the Beach reached the bridge but Haley did not continue to the Beach.
- SMAPI showed the first cross-map segment from `HaleyHouse` to `Town` succeeded and warped.
- The next segment failed from `Town:20,89` to `Beach:20,35` with `segment_path_unreachable`, `routeSteps=0`, and `failureDetail=path_empty`.
- A same-map debug move inside Town still completed, so the bridge stepper and Town movement were not globally broken.

## Root Cause

- `ProbeCrossLocationRoute(...)` treated the current map's warp tile as both the pathfinding destination and the later transition trigger.
- For `Town -> Beach`, Stardew schedule pathfinding can return no path to the actual warp tile even though an adjacent approach tile can be reached and can collide with the warp when facing toward it.
- The command state did not preserve a separate `WarpTriggerTile`, so a fallback stand tile would have lost the tile needed for the vanilla transition hook.
- `PathFindController.handleWarps(...)` checks a collision rectangle through `GameLocation.isCollidingWithWarpOrDoor(...)`; using an NPC speed-sized `nextPosition(...)` probe can miss the warp tile when speed is zero or too small.

## Bad Fix Paths

- Do not fix this by using `Game1.warpCharacter` directly in the bridge move execution path.
- Do not assign `npc.controller = new PathFindController(...)` and let vanilla own movement execution.
- Do not assume `getWarpPointTo(...)` is reversed; reference code and Stardew assembly evidence show it is the current-map warp point to the next location.
- Do not expose only a stand tile in route diagnostics when the transition needs a distinct trigger tile.
- Do not use wide fallback approach candidates for warp transitions; only cardinal adjacent approach tiles can be safely converted into a one-tile collision probe.

## Corrective Constraints

- Cross-location route probing must first try the real warp tile, then bounded cardinal adjacent approach tiles when the direct route is empty.
- Route probe and move segment state must preserve both the movement target (`StandTile` / `TargetTile`) and actual `WarpTriggerTile`.
- The vanilla transition hook may call `PathFindController.handleWarps(...)` only after bridge-owned walking reaches the approach/warp segment target.
- The collision probe for an approach tile must offset the NPC bounding box by a full tile toward the trigger tile.
- Hermes-side DTOs must preserve `warpTriggerTile` so desktop status/debug logs can show the distinction.

## Verification Evidence

- Added regression test `BridgeMovementPathProbeTests.BuildCrossLocationRouteProbe_WhenWarpTilePathEmpty_UsesReachableWarpApproachTile`.
- Added state test `BridgeCrossMapNavigationStateTests.StartCrossMapSegment_WithWarpApproachTile_PreservesSeparateTriggerTile`.
- Added regression test `BridgeCrossMapNavigationStateTests.VanillaNpcWarpTransition_UsesFullTileOffsetForWarpCollisionProbe`.
- Added log regression test `BridgeMoveCommandQueueRegressionTests.FormatRouteProbeLogDetail_WithWarpApproachTile_IncludesTriggerTile`.
- Added Hermes DTO mapping coverage in `StardewCommandServiceTests.GetStatusAsync_MapsBridgeStatusData`.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMovementPathProbeTests|FullyQualifiedName~BridgeCrossMapNavigationStateTests|FullyQualifiedName~BridgeMoveCommandQueueRegressionTests|FullyQualifiedName~BridgeMoveFailureMapperTests"` passed, 48/48.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` passed, 115/115.
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug` passed with 0 warnings and 0 errors and deployed to the local Stardew Mods directory.
- Build output and deployed DLL SHA256 both matched: `3BE94F510F110AC55117E6F59A557CB5ABC9F772CA240272B1096967DCD60074`.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Stardew"` passed, 193/193.

## Related Files

- `Mods/StardewHermesBridge/Bridge/BridgeMovementPathProbe.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
- `src/game/core/GameAction.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `src/games/stardew/StardewCommandService.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMovementPathProbeTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeCrossMapNavigationStateTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
