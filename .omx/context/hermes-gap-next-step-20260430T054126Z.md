# Deep Interview Context: Hermes Gap Next Step

- Task statement: Read `docs/Hermes参考项目功能模块效果差距对比.md`, inspect recent commits because another AI is working on `agentloop`, and determine what should be done next.
- Desired outcome: A grounded next-step recommendation that avoids duplicating current `agentloop` work and preserves the project goal: Stardew multi-NPC autonomous village behavior.
- Stated solution: Use `$deep-interview` to clarify and prepare a handoff-ready recommendation/spec rather than directly implementing.
- Probable intent hypothesis: The user wants sequencing guidance for the next engineering lane after the recent autonomy-loop commits.

## Known Facts / Evidence

- The gap document says the highest-priority gap is not UI or one-shot debug actions; it is long-lived NPC autonomy: observe, think, act via tools, poll results, write memory, and use that memory in later rounds.
- Recent commits on `allgameinai` include:
  - `1febd004 Persist meaningful NPC decisions to local memory`
  - `f2fe6d44 Record autonomy tick traces in NPC activity logs`
  - `981ea107 Poll Stardew move commands to terminal status`
  - `6f828516 Let autonomy ticks invoke Agent decisions after facts`
  - `c137b67a Create NPC agents with only scoped Stardew tools`
  - `3eafa056 Build NPC context from runtime-local Hermes services`
- Current `src/runtime/NpcAutonomyLoop.cs` exposes `RunOneTickAsync(...)`. It gathers observations/events, calls an optional agent once, writes activity, and writes memory, but it is not yet a supervised long-running loop.
- Current `src/runtime/NpcAutonomyBudget.cs` exists and has LLM slot and limit checks, but search evidence shows `TryAcquireLlmSlotAsync(...)` and `CheckToolIterationLimit(...)` are only used in tests, not in the runtime loop.
- Current `src/runtime/NpcRuntimeSupervisor.cs` registers, starts, stops, and snapshots runtime instances, but does not own background tasks, event cursors, restart cooldown, or loop lifecycle.
- `ResourceClaimRegistry` and `WorldCoordinationService` exist, but `StardewCommandService` currently submits move commands directly to the SMAPI client; search evidence did not find integration with resource claims.
- `omx explore` was attempted first for codebase fact gathering but is unavailable on this Windows environment because the built-in explore harness depends on POSIX sh/bash wrappers.

## Constraints

- Deep-interview must not implement directly.
- Use `omx question` for interview rounds.
- Keep recommendations grounded in repo evidence and recent commits.
- Avoid interfering with the other AI's current `agentloop` work.
- No destructive git operations.

## Unknowns / Open Questions

- Should the very next lane continue hardening the autonomous loop itself, or move to resource-claim / world-coordination integration for the real `move` bridge path?
- Is the other AI still editing `NpcAutonomyLoop.cs` or adjacent runtime files, or are the recent commits already complete?
- Does the user want the next step framed as a plan/spec handoff only, or should it trigger a downstream execution workflow after the interview?

## Decision-Boundary Unknowns

- Whether OMX may choose the next engineering lane without further confirmation.
- Whether to pause work that overlaps `agentloop` to avoid merge conflicts.
- Whether "next" means immediate implementation task, next planning artifact, or next validation target.

## Likely Codebase Touchpoints

- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcAutonomyBudget.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcRuntimeHost.cs`
- `src/runtime/ResourceClaimRegistry.cs`
- `src/runtime/WorldCoordinationService.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
- `docs/superpowers/plans/2026-04-29-cross-game-npc-runtime-architecture.md`
