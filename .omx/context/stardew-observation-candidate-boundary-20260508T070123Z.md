# Context: Stardew observation candidate boundary leak

Task statement:
Automatic autonomy observation currently injects host-generated movement candidates (destination[n], nearby[n]) into the agent-facing facts. This violates the intended boundary: the agent should learn raw world/control facts from automatic observation and obtain action candidates only through explicit tool use.

Desired outcome:
1. Write the boundary mistake into AGENTS.md and openspec/errors.
2. Stop automatic autonomy prompt injection of destination[n]/nearby[n].
3. Keep explicit status tools usable for manual discovery if needed.
4. Update tests to lock the new boundary.

Known facts/evidence:
- NpcAutonomyLoop.BuildDecisionMessage puts observation facts directly into [Observed Facts].
- StardewNpcAutonomyBackgroundService calls ObserveAsync every tick before agent decision.
- StardewQueryService.BuildStatusFacts currently serializes destination[n] and nearby[n] from bridge status into facts.
- BridgeDestinationRegistry hard-codes Haley House destinations: bedroom mirror, living room, front door.
- StardewNpcTools.stardew_status is a passive observation tool that returns ObserveAsync output.
- Existing tests assert destination facts are visible in autonomy prompt and query-service output.

Constraints:
- Keep the fix minimal and reversible.
- Do not add dependencies.
- Preserve raw world facts, control facts, and explicit tools.
- Use existing tests to lock the boundary.
- Keep any code comments minimal.

Unknowns/open questions:
- Whether to keep destination[n]/nearby[n] in stardew_status output or split into a separate explicit tool surface later.
- Whether some runtime tests should be updated to keep candidate visibility only where explicitly requested.

Likely touchpoints:
- AGENTS.md
- openspec/errors/E-2026-0508-stardew-observation-candidate-boundary-leak.md (new)
- src/games/stardew/StardewQueryService.cs
- Desktop/HermesDesktop.Tests/Stardew/StardewQueryServiceTests.cs
- Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs
- possibly src/games/stardew/StardewNpcTools.cs if we need to narrow the automatic surface further
