# Stardew Cross-Map Move Deep Interview Context

## Task Statement

Discuss the next step after same-location `move` has been validated: how to design and scope cross-map movement for Stardew NPC runtime.

## Desired Outcome

Clarify a requirements-ready direction for cross-map movement that can later feed planning without reopening baseline facts.

## Stated Solution

The user believes the next discussion should focus on cross-map movement because same-map `move` has already been validated.

## Probable Intent Hypothesis

The goal is to continue the existing path toward credible long-running Stardew NPC autonomy: parent agent chooses high-level intent, local executor handles low-risk action execution, Bridge executes real movement and reports terminal evidence.

## Known Facts / Evidence

- `docs/Hermes参考项目功能模块效果差距对比.md` marks P1 as Bridge real cross-map execution and movement reliability closure.
- `.omx/specs/stardew-npc-local-executor-minimal-test.md` records the implemented local executor direction: low-risk `move`, `observe`, `wait`, and `task_status` should be hard-routed away from the main agent.
- `src/runtime/NpcAutonomyLoop.cs` tells the parent agent to output one JSON intent contract, not direct tool parameters.
- `src/runtime/NpcLocalExecutorRunner.cs` selects `stardew_move`, `stardew_task_status`, or `stardew_status` for low-risk local executor actions.
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs` currently blocks cross-location movement with `cross_location_unsupported` when current location and target location differ.
- Same-location movement currently uses route probing, arrival fallback, pixel walking, replan on step block, and terminal status logs.
- `Mods/StardewHermesBridge/Bridge/BridgeDestinationRegistry.cs` owns destinationId entries across `HaleyHouse`, `Town`, `Beach`, `Forest`, and `Mountain`; `TransitionPolicy` currently defaults to `same_location_only`.

## Constraints

- Do not make the main agent handle path details or Stardew movement tool schema again.
- Do not use `warpCharacter` or direct endpoint teleport to fake successful movement.
- Do not create a second NPC task store or a second decision brain.
- Bridge/host may execute and validate low-level movement, but must not take over NPC personality or long-term decisions.
- Keep v1 narrow enough for real game verification.

## Unknowns / Open Questions

- Whether v1 must visibly walk through map exits, or may first support a real transition to the destination map entry plus natural walking inside that target map.
- How much of Stardew's native schedule/pathing transition behavior should be reused versus a Bridge-owned route graph.
- Which destinations form the acceptance set for v1.
- What terminal evidence is required for cross-map movement to count as successful.
- What failures should escalate to the parent agent versus be locally retried.

## Decision-Boundary Unknowns

- Whether OMX may choose the v1 movement architecture after the interview.
- Whether OMX may define the first acceptance route set.
- Whether tests may lock in current "no endpoint teleport" constraints while replacing `cross_location_unsupported`.

## Likely Codebase Touchpoints

- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeDestinationRegistry.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeMovementPathProbe.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMovementPathProbeTests.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`

## Prompt-Safe Initial-Context Summary Status

not_needed
