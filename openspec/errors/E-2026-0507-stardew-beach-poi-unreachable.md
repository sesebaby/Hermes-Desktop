# E-2026-0507-stardew-beach-poi-unreachable

- id: E-2026-0507-stardew-beach-poi-unreachable
- title: Stardew Beach navigation skill used an unreachable final POI tile
- status: active
- updated_at: 2026-05-07T21:15:00+08:00
- keywords: [stardew, npc-autonomy, stardew_move, Haley, Beach, target_tile_unreachable, map-skill, debug-action]
- trigger_scope: [stardew, bridge, move, bugfix, skill, diagnostics]

## Symptoms

- Clicking the desktop debug action to send Haley to the Beach no longer stalls before the Beach transition.
- SMAPI logs show the command walks `HaleyHouse -> Town -> Beach`, reaches `awaiting_warp`, then enters `replanning_after_warp;location=Beach`.
- The same command then fails with `task_failed ... error=target_tile_unreachable`.
- The failed command target is `Beach:20,35`; this coordinate comes from the desktop debug button and `skills/gaming/stardew-navigation/references/poi/beach-shoreline.md`.

## Root Cause

- The desktop debug action and navigation skill both used `Beach:20,35` as the shoreline mechanical target.
- Live bridge execution proved the cross-map transition works, but the post-warp same-location route to that final target could not pass the bridge's final target or reachable-neighbor checks.
- The bridge destination registry already had a Beach photo spot at `Beach:32,34`, so the debug action and skill POI had drifted away from a known routeable Beach destination.

## Bad Fix Paths

- Do not keep changing cross-map warp handling when the log already reached `replanning_after_warp;location=Beach`.
- Do not loosen same-location final target validation to make a bad POI appear valid.
- Do not use `Game1.warpCharacter` or `npc.controller = new PathFindController(...)` to bypass the unreachable final target.
- Do not update only the desktop debug button while leaving the skill asset on the old coordinate.

## Corrective Constraints

- Skill mechanical targets and manual debug actions must use routeable coordinates that match known bridge destinations when available.
- A `target_tile_unreachable` after `replanning_after_warp` should be treated as a final POI/arrival issue, not as evidence that cross-map routing failed.
- Tests that assert the bundled navigation skill target must be updated with the debug action so the two user paths stay aligned.

## Verification Evidence

- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Stardew.StardewManualActionServiceTests.SendHaleyToBeachAsync_SubmitsSkillBeachTargetForHaley|FullyQualifiedName~HermesDesktop.Tests.Stardew.StardewAutonomyContextBudgetTests.StardewNavigationSkillAssets_UseLayeredDisclosureForMechanicalTargets"` failed with the old `20,35` target.
- GREEN: the same targeted desktop tests plus `NpcAutonomyLoopTests.RunOneTickAsync_WithMechanicalMoveIntent_WritesStructuredTargetSource` passed after switching to `Beach:32,34`.

## Related Files

- `src/games/stardew/StardewBridgeDiscovery.cs`
- `skills/gaming/stardew-navigation/references/poi/beach-shoreline.md`
- `Desktop/HermesDesktop.Tests/Stardew/StardewManualActionServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
