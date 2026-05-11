# Stardew Tool Orchestration Harness Context

## Task Statement

Continue `$ralph` execution of `.omx/plans/stardew-tool-orchestration-harness-plan.md` and commit useful, verified slices promptly.

## Desired Outcome

Stardew NPC world-writing tools share one lifecycle contract:

`agent tool call -> runtime action/pending fact -> bridge execution -> terminal status -> NPC runtime terminal fact -> next autonomy wake sees last_action_result`.

The current execution must avoid moving real write actions into the local executor and must avoid hardcoded NPC names, location names, or natural-language destination rules.

## Known Facts / Evidence

- Branch is `allgameinai`, ahead of `origin/allgameinai`.
- Working tree was clean before this context file was created.
- Existing plan is approved and stored at `.omx/plans/stardew-tool-orchestration-harness-plan.md`.
- Prior commits in this Ralph run:
  - `09d086d2` made the local executor exit NPC write actions.
  - `ed460422` made parent idle actions participate in the continuity evidence chain.
- Focused verification already passed after those commits: 67 tests passed for `StardewNpcToolFactoryTests`, `NpcLocalExecutorRunnerTests`, and `NpcAutonomyLoopTests`.
- Current slice is MCP/native parity for `stardew_idle_micro_action`, modeled after existing MCP move terminal feedback tests.

## Constraints

- Do not add an external MCP server in this slice.
- Do not add a second tool lane.
- Do not let `NpcLocalExecutorRunner` execute `move`, `speak`, `open_private_chat`, or `idle_micro_action`.
- Do not hardcode NPC, place, or Chinese destination matching rules.
- Use TDD where production changes are needed; for parity coverage, a passing regression test can be committed as coverage.
- Use Lore-style Chinese commit messages when committing.

## Unknowns / Open Questions

- Whether `stardew_idle_micro_action` MCP parity already passes through existing production code.
- Which later plan steps need production changes after MCP parity coverage.

## Likely Codebase Touchpoints

- `Desktop/HermesDesktop.Tests/Mcp/McpServerTests.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
