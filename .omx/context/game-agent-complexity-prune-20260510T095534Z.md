# Game Agent Complexity Prune Context

- Task statement: Re-evaluate Hermes-Desktop complexity from the product position of a game-focused agent project, currently Stardew Valley and later possibly other games.
- Desired outcome: Discuss the existing cleanup plan item by item and decide which modules are unnecessary, which are core infrastructure, and which need evidence before removal.
- Stated solution: Use `$deep-interview` before execution; no code deletion in this mode.
- Source plan: `.omx/plans/Hermes-Desktop复杂度精简计划（详细审查版）.md`
- Context type: brownfield
- Prompt-safe initial-context summary status: not_needed

## Evidence Collected

- `git status --short --branch`: current branch is `allgameinai...origin/allgameinai`, ahead by 1 commit.
- Plan candidates include Buddy, Wiki, Coordinator/AgentService, AutoDreamService, MixtureOfAgentsTool, Dreamer, InsightsService, multi-language resources, `external/`, `参考项目/`, `.omx/archives`, and outdated docs.
- `omx explore` was unavailable on Windows because the POSIX allowlist harness is not ready; local read-only `rg` was used instead.
- Buddy has Desktop UI, DI registration, resources, and tests, but no direct `src/runtime` or `src/games/stardew` references found.
- Wiki has a data layer and DI registration in `Desktop/HermesDesktop/App.xaml.cs`, but no obvious UI/tool entry found in the read-only grep pass.
- `CoordinatorService` and `AgentService` are DI-registered, but only directly referenced by their own definitions and registration in the read-only grep pass.
- `AutoDreamService` exists and tests assert it is not the default Desktop startup path.
- `MixtureOfAgentsTool` exists but is not listed in `AgentCapabilityAssembler.BuiltInToolNames`.
- Dreamer and Insights are not zero-dependency: `StartDreamerBackground`, `DreamerStatus`, `InsightsService`, and `DashboardPage` references show active Desktop startup/UI wiring.

## Constraints

- Discuss and clarify first; do not implement inside `$deep-interview`.
- Preserve game-agent core: Skills, Memory, Soul, MCP, Transcript, SessionTodoStore, Cron/Schedule, NPC runtime, Stardew bridge/autonomy/private chat.
- Hard non-goal from user: do not touch core NPC runtime.
- Avoid deleting modules just because they look generic; judge by whether they support game-agent product goals.
- Existing user changes must not be reverted.

## Unknowns / Open Questions

- Resolved boundary: user wants "a multi-game agent platform with Stardew as the first scenario", not a strict Stardew-only minimum runtime.
- Resolved outcome preference: user prefers "make the game agent platform core clearer" over "smallest repository" or "documentation cleanup first".
- Resolved decision boundary: for clearly irrelevant or unwired candidates, default to direct deletion in the cleanup plan.
- Refinement from pressure pass: "unwired" is not enough for deletion if the module clearly maps to future multi-game substrate.
- Connected or conceptually reusable systems that may become cross-game substrate require explicit discussion before deletion, and should be renamed/reconnected to game semantics if kept.
- Whether reference material should be deleted, archived, or excluded from search/build only.

## Interview Rounds

### Round 1

- Question: Should cleanup use a strict current-game-runtime boundary or preserve a future multi-game agent platform base?
- Answer: Preserve a multi-game agent platform with Stardew as the first scenario.
- Impact: Deletion standard should distinguish dead/unwired product clutter from generic infrastructure that can become cross-game world knowledge, NPC cognition, orchestration, or observability.

### Round 2

- Question: In the multi-game agent platform direction, what should the first cleanup pass explicitly not do?
- Answer: Do not touch core NPC runtime.
- Impact: Cleanup discussion must not propose removing or restructuring the core NPC runtime path; candidates should be evaluated as peripheral UI, unregistered/dead code, optional background systems, documentation noise, or default-startup configuration.

### Round 3

- Question: Should the cleanup optimize for clearer game-agent platform core, smaller repository, or reduced architecture misunderstanding first?
- Answer: A. Make the game agent platform core clearer.
- Impact: Prefer reducing default UI/startup/registration clutter while preserving reusable cross-game substrate. Deletion is not the only acceptable cleanup operation; demotion to optional module may be better for connected but non-core systems.

### Round 4

- Question: For first-pass candidates, should default handling be demotion from default UI/startup/DI, direct deletion for clearly unrelated/unwired code, or documentation-only classification?
- Answer: B. Direct deletion for clearly irrelevant or unwired candidates.
- Impact: The cleanup plan may recommend direct removal for product-noise/dead-code items such as purely game-unrelated UI toys or unregistered tools. This does not automatically apply to connected optional systems such as Dreamer/Insights or potentially reusable platform substrate.

### Round 5

- Question: If a module is currently unwired but conceptually fits a future multi-game platform, such as Wiki becoming world-setting/location/rules knowledge, should it still be deleted or kept with game-semantic renaming/reconnection?
- Answer: Keep it.
- Impact: The deletion criterion becomes two-part: delete only when both current wiring and future platform fit are weak. If future platform fit is strong, the cleanup action should be "reshape into game substrate" rather than delete.

### Round 6

- Question: For each cleanup candidate, should conclusions use the fixed shape: keep/delete/reshape-as-platform-module/defer, reason, and core-NPC-runtime risk check?
- Answer: Enough.
- Impact: Subsequent item-by-item review should use this fixed output format and avoid expanding into implementation unless the user explicitly switches from discussion to execution.

## Likely Touchpoints

- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/MainWindow.xaml`
- `Desktop/HermesDesktop/Views/**`
- `src/runtime/AgentCapabilityAssembler.cs`
- `src/runtime/**`
- `src/games/stardew/**`
- `src/buddy/**`
- `src/wiki/**`
- `src/agents/**`
- `src/coordinator/**`
- `src/dream/**`
- `src/dreamer/**`
- `src/analytics/**`
- `src/Tools/MixtureOfAgentsTool.cs`
