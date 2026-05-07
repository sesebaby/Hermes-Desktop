# Stardew Cross-Map Navigation Probe Context

## Task Statement

Plan and execute the next approved step after same-location Stardew NPC `move` validation: build a minimal cross-map navigation probe with small, reviewable commits.

## Desired Outcome

Create a grounded PRD and test specification, then implement the first minimal slice without re-asking for permission. The implementation should preserve the existing `destinationId` local executor contract while enabling an executor-only mechanical target path for future cross-map route probing.

## Known Facts / Evidence

- `.omx/specs/stardew-npc-local-executor-minimal-test.md` records the implemented local executor model: parent cloud model emits short intent, local executor handles low-risk mechanics.
- `.omx/specs/stardew-npc-cross-map-navigation-minimal-probe.md` defines the agreed next direction: `stardew_navigate_to_tile` should be executor-only and should not replace `stardew_move(destinationId)`.
- `src/runtime/NpcLocalActionIntent.cs` currently requires `destinationId` for every `move` intent.
- `src/runtime/NpcLocalExecutorRunner.cs` currently maps `move` only to `stardew_move`.
- `src/games/stardew/StardewNpcTools.cs` exposes local executor tools through `CreateLocalExecutorTools`; current set is `stardew_status`, `stardew_move`, and `stardew_task_status`.
- `src/games/stardew/StardewCommandService.cs` can already submit move requests using either `destinationId` or `GameActionTarget.LocationName + Tile`.
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs` currently resolves target payloads, but blocks actual cross-location execution with `cross_location_unsupported`.
- Reference mod `Market Day` shows schedule/path assembly using route-by-location plus per-map pathfinding.
- Reference mod `BotFramework` shows a world graph built from warps and next-location navigation actions.
- Reference mod `CustomNPCFixes` shows route/schedule refresh as an important failure-recovery concept.

## Constraints

- All user-facing communication must be Chinese.
- Every plan must include options, reasons, and explicit recommendation.
- Do not ask for confirmation; user already approved execution.
- Work in small steps and commit promptly.
- Do not let the main agent see or use the mechanical coordinate navigation tool.
- Do not make the local small model load map skills or invent coordinates.
- Do not treat `Game1.warpCharacter` as natural movement success.
- Do not add dependencies.
- Do not overwrite unrelated user changes.

## Unknowns / Open Questions

- Exact Stardew 1.6 callable surface for `WarpPathfindingCache` and route lookup still needs compile/source validation before bridge route probe implementation.
- Whether a first bridge route probe should be a new route/action or a payload flag on existing move.
- Whether cross-map execution should initially stop at route probe or execute the first same-map segment.

## Likely Codebase Touchpoints

- `.omx/plans/产品需求文档-stardew-npc-跨地图导航最小探针.md`
- `.omx/plans/测试规范-stardew-npc-跨地图导航最小探针.md`
- `src/runtime/NpcLocalActionIntent.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewCommandService.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs`
- `Mods/StardewHermesBridge.Tests/*`

