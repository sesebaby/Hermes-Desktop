# E-2026-0507-stardew-post-warp-boundary-replan

- id: E-2026-0507-stardew-post-warp-boundary-replan
- title: Stardew post-warp final route planning failed from transient Beach boundary landing tiles
- status: active
- updated_at: 2026-05-07T21:45:00+08:00
- keywords: [stardew, npc-autonomy, stardew_move, Haley, Beach, replanning_after_warp, segment_path_unreachable, boundary_tile, off_map_warp]
- trigger_scope: [stardew, bridge, move, bugfix, diagnostics]

## Symptoms

- Clicking the desktop debug action to send Haley to the Beach enqueues a bridge move command and visibly walks Haley through `HaleyHouse -> Town -> Beach`.
- SMAPI logs show the command reaches `awaiting_warp;nextLocation=Beach`, then `replanning_after_warp;location=Beach`.
- The command then fails with `task_failed ... error=segment_path_unreachable` before `post_warp_final_segment_started`.
- In the same run, an autonomy move from `Beach` back to `Town` selected `Beach:38,-1` as a segment target and failed with `target_tile_open_false`.

## Root Cause

- After vanilla warp handling, Haley can be observed on a Beach boundary landing tile such as `Beach:38,0`.
- The bridge immediately tried to plan the final same-location segment to the Beach POI from that transient boundary tile.
- Stardew schedule pathfinding can return an empty route from the boundary landing tile before the NPC is fully settled into a routeable in-map state.
- Separately, `getWarpPointTo(...)` can expose an off-map warp trigger such as `Beach:38,-1`; treating that trigger as the stand tile made the bridge try to walk to a negative coordinate.

## Bad Fix Paths

- Do not replace natural movement with `Game1.warpCharacter` to fake completion.
- Do not assign `npc.controller = new PathFindController(...)` to make vanilla own movement execution.
- Do not loosen same-location target affordance checks; unsafe final targets must still fail.
- Do not keep changing Beach POI coordinates when the log already shows `target=Beach:32,34`.
- Do not treat off-map warp trigger tiles as walkable stand tiles.

## Corrective Constraints

- Post-warp final replanning may defer only transient `PathEmpty` probes from boundary landing tiles; `TargetUnsafe` must still fail closed.
- Deferred post-warp replanning must keep the command in `running/replanning_after_warp` and retry with a bounded budget.
- Failure logs after post-warp route probes must include the route probe summary, not just `segment_path_unreachable`.
- Cross-location warp probing must preserve off-map trigger tiles for `handleWarps(...)` while using an in-bounds approach tile as the stand/walk target.

## Verification Evidence

- RED: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeCrossMapNavigationStateTests.PostWarpFinalReplan"` failed because `BridgeMoveCommand.TryDeferPostWarpFinalReplan` did not exist.
- RED: added `BridgeMovementPathProbeTests.BuildCrossLocationRouteProbe_WhenWarpPointIsOffMap_UsesReachableApproachTile` to cover `Beach:38,-1` trigger with `Beach:38,0` stand tile.
- GREEN: targeted tests for `PostWarpFinalReplan` and off-map warp approach passed, 4/4.
- GREEN: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMovementPathProbeTests|FullyQualifiedName~BridgeCrossMapNavigationStateTests|FullyQualifiedName~BridgeMoveCommandQueueRegressionTests|FullyQualifiedName~BridgeMoveFailureMapperTests"` passed, 52/52.

## Related Files

- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeMovementPathProbe.cs`
- `Mods/StardewHermesBridge.Tests/BridgeCrossMapNavigationStateTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMovementPathProbeTests.cs`
