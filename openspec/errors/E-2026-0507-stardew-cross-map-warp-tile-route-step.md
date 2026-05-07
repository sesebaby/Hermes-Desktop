# E-2026-0507-stardew-cross-map-warp-tile-route-step

- id: E-2026-0507-stardew-cross-map-warp-tile-route-step
- title: Stardew cross-map move rejected a reachable warp tile as an unsafe route step
- status: active
- updated_at: 2026-05-07
- keywords: [stardew, npc-autonomy, stardew_move, Haley, Beach, cross_location, segment_path_unreachable, warp_tile]
- trigger_scope: [stardew, bridge, move, bugfix, diagnostics]

## Symptoms

- Clicking the desktop debug action to send Haley to the Beach appears to do nothing in game.
- Hermes and SMAPI logs show the click did reach the bridge.
- SMAPI reports `task_move_enqueued` followed immediately by `task_failed`.
- The failed probe can include a non-empty route, for example `routeSteps=25`, but still return `segment_path_unreachable`.

## Root Cause

- Cross-location route probing used Stardew schedule pathfinding to find the route from the current tile to the next map's warp tile.
- The bridge then ran its ordinary route-step safety check over every returned step.
- Warp tiles can be special transition tiles and may fail ordinary `isTileLocationOpen` checks even though they are the intended endpoint for Stardew's schedule route.
- Treating the warp tile like an ordinary intermediate tile converted a valid first cross-map segment into `segment_path_unreachable`.

## Bad Fix Paths

- Do not fix this by using `Game1.warpCharacter` to fake the transition.
- Do not assign `npc.controller = new PathFindController(...)` to delegate movement execution.
- Do not remove target-tile affordance checks for normal same-location moves.
- Do not assume a non-empty route with `segment_path_unreachable` means the place registry or Beach coordinate is wrong.

## Corrective Constraints

- Bridge-owned movement must still consume the schedule route through its own pixel stepper.
- Same-location target validation remains strict.
- Cross-location probes may treat the resolved warp tile as a permitted segment endpoint when Stardew schedule pathfinding returned a route to that warp tile.
- Intermediate non-warp steps should still use the normal route-step safety check.

## Verification Evidence

- Added regression test `BridgeMovementPathProbeTests.BuildCrossLocationRouteProbe_AllowsClosedWarpTileWhenScheduleRouteReachesWarp`.
- Verified the new regression test failed before the production fix with actual status `segment_path_unreachable`.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMovementPathProbeTests.BuildCrossLocationRouteProbe_AllowsClosedWarpTileWhenScheduleRouteReachesWarp"` passed after the fix.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMovementPathProbeTests|FullyQualifiedName~BridgeCrossMapNavigationStateTests|FullyQualifiedName~BridgeMoveCommandQueueRegressionTests"` passed, 38/38.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` passed, 107/107.
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug` passed and deployed the bridge DLL to the local Stardew Mods directory.

## Related Files

- `Mods/StardewHermesBridge/Bridge/BridgeMovementPathProbe.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMovementPathProbeTests.cs`
