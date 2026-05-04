# E-2026-0504-stardew-moving-npc-status-gate

- id: E-2026-0504-stardew-moving-npc-status-gate
- title: Stardew status exposed move candidates while an NPC was already moving
- status: active
- updated_at: 2026-05-04
- keywords: [stardew, npc-autonomy, stardew_move, npc_moving, Haley, vanilla-schedule, velocity, status-gate]
- trigger_scope: [stardew, bridge, move, bugfix, diagnostics]

## Symptoms

- Manual Haley testing reports that `move` appears to conflict with vanilla schedule movement.
- SMAPI logs show later Haley moves failing before execution with `target_blocked:HaleyHouse:10,12;target_tile_open_false` or `path_unreachable:HaleyHouse:15,8;path_empty`.
- Runtime activity can show Haley drifting to impossible coordinates such as `(477,6)` and narrating that she is still moving or flying.
- The last failed bridge commands may not have `task_running started`, so they are symptoms of a bad current NPC state rather than the first movement fault.

## Root Cause

- Bridge-owned animated movement adjusted `npc.Position` directly, but terminal cleanup did not fully clear vanilla velocity/state in earlier builds.
- `/query/status` considered only global blockers such as `event_active` and `menu_open`, so a vanilla-scheduled or residual moving NPC could still report `isAvailableForControl=true`.
- Because status still exposed destinations and nearby tiles while `npc.isMoving()` was true, the Agent could submit another `stardew_move` from an unstable or invalid current tile.

## Bad Fix Paths

- Do not treat every later `target_blocked` or `path_unreachable` as a destination registry bug.
- Do not bypass Bridge-owned movement by assigning `npc.controller = new PathFindController(...)`.
- Do not use `Game1.warpCharacter(...)` to reset or fake completion.
- Do not rely on prompt wording to prevent new moves while status still exposes move candidates.

## Corrective Constraints

- When Bridge takes or releases movement control, clear vanilla motion with `Halt()`, zero velocities, and stop the sprite animation.
- Status availability must include the requested NPC's own movement state.
- If `npc.isMoving()` is true, return a stable `blockedReason` such as `npc_moving` and hide move candidates.
- Diagnostics must separate the first drift/root-cause command from later preflight failures caused by the drifted state.

## Verification Evidence

- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter StatusQueryTreatsMovingNpcAsTemporarilyUnavailable` passed.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` passed, 75/75.
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug` passed with 0 warnings and 0 errors, and deployed the mod DLL locally.
- `rg "npc\.controller = new PathFindController|Game1\.warpCharacter|started;using=PathFindController|npc\.setTilePosition|private const int StepDelayTicks" Mods\StardewHermesBridge\Bridge src\games\stardew -S` returned no production-code hits.

## Related Files

- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`
