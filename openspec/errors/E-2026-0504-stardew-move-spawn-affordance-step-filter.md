# E-2026-0504-stardew-move-spawn-affordance-step-filter

- id: E-2026-0504-stardew-move-spawn-affordance-step-filter
- title: Stardew move rejected schedule path steps with spawn affordance checks
- status: active
- updated_at: 2026-05-04
- keywords: [stardew, npc-autonomy, stardew_move, HaleyHouse, CanSpawnCharacterHere, path_blocked, target_blocked, step_can_spawn_false, target_tile_open_false]
- trigger_scope: [stardew, bridge, move, bugfix, pathfinding]

## Symptoms

- Manual Haley move tests all fail inside HaleyHouse.
- SMAPI logs show a move to HaleyHouse failing with `path_blocked:HaleyHouse:6,7;step_can_spawn_false`.
- A second Haley move fails with `target_blocked:HaleyHouse:10,12;target_tile_open_false`.
- The command is accepted and enqueued, so the failure is inside Bridge route validation or arrival resolution rather than Desktop claim or destination ID parsing.

## Root Cause

- `BridgeMovementPathProbe.CheckRouteStepSafety(...)` reused the same tile safety function as final target affordance checks.
- That function required `GameLocation.CanSpawnCharacterHere(...)`, which is too strict for schedule path steps returned by `PathFindController.findPathForNPCSchedules(...)`.
- Bridge therefore asked Stardew for a valid schedule path, then rejected one of Stardew's own path steps with a spawn-only affordance gate.
- Arrival fallback searched only the four immediate neighbors of a blocked semantic anchor. Furniture/room anchors such as HaleyHouse living room can require a wider nearest-reachable fallback tile.

## Bad Fix Paths

- Do not assign `npc.controller = new PathFindController(...)` to bypass Bridge-owned stepping.
- Do not use `Game1.warpCharacter(...)` to fake movement success.
- Do not treat every `CanSpawnCharacterHere(...) == false` as proof that a schedule route step is unwalkable.
- Do not limit semantic-anchor fallback to only the four cardinal neighboring tiles.

## Corrective Constraints

- Use Stardew schedule pathfinding as a route probe, then let Bridge consume the prepared route.
- Route step validation should verify static map openness; spawn affordance belongs to final target/fallback stand tile checks, not every path step.
- Arrival fallback must preserve four-neighbor priority but continue a bounded nearest-tile search when those neighbors are blocked.
- Regression tests must cover both step safety semantics and wider fallback enumeration.

## Verification Evidence

- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMovementPathProbeTests"` passed, 6/6.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` passed, 68/68.
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug` passed with 0 warnings and 0 errors, and deployed the mod DLL locally.
- `rg "npc\.controller = new PathFindController|Game1\.warpCharacter|started;using=PathFindController" Mods\StardewHermesBridge\Bridge src\games\stardew -S` returned no production-code hits.

## Related Files

- `Mods/StardewHermesBridge/Bridge/BridgeMovementPathProbe.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMovementPathProbeTests.cs`
