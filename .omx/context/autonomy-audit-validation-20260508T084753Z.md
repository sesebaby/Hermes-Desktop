# Autonomy Audit Validation Context

Task statement: Validate `agent-autonomy-audit.zh-CN.md` against the current Hermes-Desktop source before deciding whether to repair according to it.

Desired outcome: Evidence-backed judgment of which audit findings are correct, stale, overstated, or lower priority.

Constraints:
- Read `AGENTS.md` first.
- Use `$deep-interview` as the intake/evaluation boundary.
- Do not implement repairs in this pass.
- Preserve existing workspace changes.

Known facts/evidence:
- `AGENTS.md` was read with UTF-8.
- `omx explore` was attempted first for brownfield lookup but is unavailable on this Windows setup.
- `deep-interview` state write failed because an existing `ralph` mode is active.
- Targeted test `NpcRuntimeContextFactoryTests.Create_AutonomyChannel_OmitsGlobalSkillsMandatoryIndex` currently fails at the stale `Do not call tools` assertion.

Likely codebase touchpoints:
- `src/Core/SystemPrompts.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/AgentCapabilityAssembler.cs`
- `src/LLM/ChatLaneClientProvider.cs`
- `src/Tools/AgentTool.cs`
- `Desktop/HermesDesktop/App.xaml.cs`

Open questions:
- Whether the next repair pass should preserve the current parent-intent/local-executor split, or move autonomy parent back toward direct tool use.
- Whether lane/tool-surface configurability is a first-pass requirement or should stay behind stricter prompt/skill/timeout boundary fixes.

Resolution update 2026-05-08:
- User explicitly rejected vague "mid/long-term" deferral.
- All reasonable audit findings must be persisted in a durable issue ledger.
- The durable source of truth for the next handoff is `.omx/specs/deep-interview-autonomy-audit-resolution.md`.
- Repair ordering may differ, but no reasonable issue may disappear from the plan.
