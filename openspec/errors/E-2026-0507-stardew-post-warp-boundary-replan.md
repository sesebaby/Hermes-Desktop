# E-2026-0507-stardew-post-warp-boundary-replan

- id: E-2026-0507-stardew-post-warp-boundary-replan
- title: Stardew post-warp final route planning failed from transient Beach boundary landing tiles
- status: active
- updated_at: 2026-05-07T21:49:00+08:00
- keywords: [stardew, npc-autonomy, stardew_move, Haley, Beach, replanning_after_warp, segment_path_unreachable, boundary_tile, off_map_warp]
- trigger_scope: [stardew, bridge, move, bugfix, diagnostics]

## Symptoms

- Clicking the desktop debug action to send Haley to the Beach enqueues a bridge move command and visibly walks Haley through `HaleyHouse -> Town -> Beach`.
- SMAPI logs show the command reaches `awaiting_warp;nextLocation=Beach`, then `replanning_after_warp;location=Beach`.
- The command then fails with `task_failed ... error=segment_path_unreachable` before `post_warp_final_segment_started`.
- In the same run, an autonomy move from `Beach` back to `Town` selected `Beach:38,-1` as a segment target and failed with `target_tile_open_false`.
- A later manual test showed the debug command still failed after 30 bounded deferrals from `Beach:38,0`, then a queued autonomy command immediately moved Haley from Beach back to `HaleyHouse:6,4`.

## Root Cause

- After vanilla warp handling, Haley can be observed on a Beach boundary landing tile such as `Beach:38,0`.
- The bridge immediately tried to plan the final same-location segment to the Beach POI from that transient boundary tile.
- Stardew schedule pathfinding can return an empty route from the boundary landing tile before the NPC is fully settled into a routeable in-map state.
- Separately, `getWarpPointTo(...)` can expose an off-map warp trigger such as `Beach:38,-1`; treating that trigger as the stand tile made the bridge try to walk to a negative coordinate.
- Bounded post-warp deferral alone is insufficient when the NPC remains on the same boundary tile for the entire retry budget; the route must explicitly include a one-tile in-map escape step before planning toward the final POI.
- Desktop manual debug moves used the same queue priority as autonomous moves, so a stale autonomous command could run immediately after the manual command failed and overwrite the observed manual-test outcome.
- The first boundary recovery implementation incorrectly validated the escape tile with final-target affordance rules. A route step such as `Beach:38,1` may be usable as a movement step even if `CanSpawnCharacterHere` rejects it as a final stand/spawn target, causing recovery to return `null` and fall back to the old 30-attempt defer/fail behavior.

## Bad Fix Paths

- Do not replace natural movement with `Game1.warpCharacter` to fake completion.
- Do not assign `npc.controller = new PathFindController(...)` to make vanilla own movement execution.
- Do not loosen same-location target affordance checks; unsafe final targets must still fail.
- Do not keep changing Beach POI coordinates when the log already shows `target=Beach:32,34`.
- Do not treat off-map warp trigger tiles as walkable stand tiles.
- Do not rely on increasing the post-warp retry count when logs show the same `from=Beach:38,0` for every retry.
- Do not infer manual debug intent from localized button text or trace prefixes; carry an explicit debug/manual contract bit.

## Corrective Constraints

- Post-warp final replanning may defer only transient `PathEmpty` probes from boundary landing tiles; `TargetUnsafe` must still fail closed.
- Deferred post-warp replanning must keep the command in `running/replanning_after_warp` and retry with a bounded budget.
- Failure logs after post-warp route probes must include the route probe summary, not just `segment_path_unreachable`.
- Cross-location warp probing must preserve off-map trigger tiles for `handleWarps(...)` while using an in-bounds approach tile as the stand/walk target.
- Boundary landing recovery must physically walk to an in-map escape tile such as `Beach:38,1` before following the final POI route.
- Boundary escape tiles must be validated as route steps, not as final target stand tiles.
- Manual desktop debug moves should preempt queued/running move commands for the same NPC and log the cancellation reason.

## Verification Evidence

- RED: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeCrossMapNavigationStateTests.PostWarpFinalReplan"` failed because `BridgeMoveCommand.TryDeferPostWarpFinalReplan` did not exist.
- RED: added `BridgeMovementPathProbeTests.BuildCrossLocationRouteProbe_WhenWarpPointIsOffMap_UsesReachableApproachTile` to cover `Beach:38,-1` trigger with `Beach:38,0` stand tile.
- GREEN: targeted tests for `PostWarpFinalReplan` and off-map warp approach passed, 4/4.
- GREEN: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMovementPathProbeTests|FullyQualifiedName~BridgeCrossMapNavigationStateTests|FullyQualifiedName~BridgeMoveCommandQueueRegressionTests|FullyQualifiedName~BridgeMoveFailureMapperTests"` passed, 52/52.
- RED: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMovementPathProbeTests|FullyQualifiedName~BridgeMoveCommandQueueRegressionTests"` failed because `TryBuildRouteFromBoundaryLanding` and manual debug preemption did not exist.
- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Stardew.StardewCommandServiceTests.SubmitAsync_Move_WithManualDebugPayload_IncludesDebugManualInMoveEnvelope"` failed because `StardewMoveRequest.DebugManual` did not exist.
- GREEN: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` passed, 121/121.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Stardew.StardewCommandServiceTests|FullyQualifiedName~HermesDesktop.Tests.Stardew.StardewCommandContractTests|FullyQualifiedName~HermesDesktop.Tests.Stardew.StardewManualActionServiceTests"` passed, 34/34.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug` passed, 1012/1013 with 1 skipped.
- RED: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMovementPathProbeTests.TryBuildRouteFromBoundaryLanding_WhenEscapeCannotSpawnButIsRouteable_UsesInteriorEscapeTile"` failed because the escape tile was checked with final target affordance and recovery returned `null`.
- GREEN: same test passed after validating escape tiles through route-step safety instead of final target affordance.

## Related Files

- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeMovementPathProbe.cs`
- `Mods/StardewHermesBridge.Tests/BridgeCrossMapNavigationStateTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMovementPathProbeTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewBridgeDiscovery.cs`
