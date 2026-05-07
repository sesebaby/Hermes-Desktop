# E-2026-0507-stardew-cross-map-warp-tile-route-step

- id: E-2026-0507-stardew-cross-map-warp-tile-route-step
- title: Stardew cross-map move rejected reachable schedule route tiles as unsafe route steps
- status: active
- updated_at: 2026-05-07T19:05:50+08:00
- keywords: [stardew, npc-autonomy, stardew_move, Haley, Beach, cross_location, segment_path_unreachable, warp_tile, schedule_path, isTileLocationOpen]
- trigger_scope: [stardew, bridge, move, bugfix, diagnostics]

## Symptoms

- Clicking the desktop debug action to send Haley to the Beach appears to do nothing in game.
- Hermes and SMAPI logs show the click did reach the bridge.
- SMAPI reports `task_move_enqueued` followed immediately by `task_failed`.
- The failed probe can include a non-empty route, for example `routeSteps=25`, but still return `segment_path_unreachable`.
- After allowing the resolved warp endpoint, the same manual beach command could still fail with `routeSteps=24` or `routeSteps=25`, showing that another schedule route step was being rejected before movement started.

## Root Cause

- Cross-location route probing used Stardew schedule pathfinding to find the route from the current tile to the next map's warp tile.
- The bridge then ran its ordinary route-step safety check over every returned step.
- Warp tiles can be special transition tiles and may fail ordinary `isTileLocationOpen` checks even though they are the intended endpoint for Stardew's schedule route.
- Intermediate tiles returned by `PathFindController.findPathForNPCSchedules(...)` have already passed Stardew's NPC schedule passability rules. Re-checking those returned schedule steps with generic `GameLocation.isTileLocationOpen(...)` can reject legitimate indoor route tiles in HaleyHouse.
- Treating schedule route steps like independently chosen target tiles converted a valid first cross-map segment into `segment_path_unreachable`.

## Bad Fix Paths

- Do not fix this by using `Game1.warpCharacter` to fake the transition.
- Do not assign `npc.controller = new PathFindController(...)` to delegate movement execution.
- Do not remove target-tile affordance checks for normal same-location moves.
- Do not assume a non-empty route with `segment_path_unreachable` means the place registry or Beach coordinate is wrong.
- Do not treat `GameLocation.isTileLocationOpen(...) == false` on a returned schedule step as proof that Stardew's NPC schedule route is invalid.

## Corrective Constraints

- Bridge-owned movement must still consume the schedule route through its own pixel stepper.
- Same-location target validation remains strict.
- Final target and fallback stand-tile validation remains strict through `CheckTargetAffordance(...)`.
- Cross-location probes may treat the resolved warp tile as a permitted segment endpoint when Stardew schedule pathfinding returned a route to that warp tile.
- Route-step validation should reject only obviously invalid route entries such as negative coordinates; it should not re-run generic target-tile openness checks against intermediate schedule steps.
- Failure logs for route probes must include enough detail to identify the failing rule and tile when a probe does fail.

## Verification Evidence

- Added regression test `BridgeMovementPathProbeTests.BuildCrossLocationRouteProbe_AllowsClosedWarpTileWhenScheduleRouteReachesWarp`.
- Verified the new regression test failed before the production fix with actual status `segment_path_unreachable`.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMovementPathProbeTests.BuildCrossLocationRouteProbe_AllowsClosedWarpTileWhenScheduleRouteReachesWarp"` passed after the fix.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMovementPathProbeTests|FullyQualifiedName~BridgeCrossMapNavigationStateTests|FullyQualifiedName~BridgeMoveCommandQueueRegressionTests"` passed, 38/38.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` passed, 107/107.
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug` passed and deployed the bridge DLL to the local Stardew Mods directory.
- Added regression test `BridgeMovementPathProbeTests.CheckRouteStepSafety_WhenScheduleStepTileLocationIsClosed_TrustsSchedulePath`; verified it failed before the production fix.
- Added regression test `BridgeMovementPathProbeTests.BuildCrossLocationRouteProbe_TrustsClosedIntermediateScheduleSteps`.
- Added diagnostics test `BridgeMoveCommandQueueRegressionTests.FormatRouteProbeLogDetail_WithCrossLocationFailure_IncludesFailureDetail`; verified it failed before the logging fix.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMovementPathProbeTests|FullyQualifiedName~BridgeCrossMapNavigationStateTests|FullyQualifiedName~BridgeMoveCommandQueueRegressionTests|FullyQualifiedName~BridgeMoveFailureMapperTests"` passed, 43/43.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` passed, 111/111.
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug` passed with 0 warnings and 0 errors, and deployed the bridge DLL to the local Stardew Mods directory.
- Deployed DLL hash matched the build output: `05FEDA842E8BE9ED2DAA5B2FF863B94823C5D263BCB0B4EB28757C0A026A4654`.

## Related Files

- `Mods/StardewHermesBridge/Bridge/BridgeMovementPathProbe.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMovementPathProbeTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`
