Task statement: Design a reference-aligned plan to reduce Stardew autonomy first-call system context so the default 5K context budget is realistic, without regressing NPC behavior contracts.

Desired outcome:
- Keep autonomy execution behavior stable while shrinking system prompt layers dramatically.
- Preserve the minimal always-on rules needed for move/speak/task continuity.
- Move explanatory or on-demand knowledge out of the always-injected system layer.

Known facts/evidence:
- Runtime logs after the active-task classifier fix show `activeTaskChars` is now small (`124-171`) and `currentUserChars` is non-zero (`1901-2587`), so the old misclassification bug is fixed.
- Runtime logs still show `budgetMet=False` with `budgetUnmetReason=core_system_over_budget` and `systemChars=22284-24025`.
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs` currently injects persona facts, voice, boundaries, and the full text of every required Stardew skill into `SystemPromptSupplement`.
- Recomputed current supplement sizes after the user's latest skill edits are still about `6844` chars for both Haley and Penny.
- `SkillManager.BuildSkillsMandatoryPrompt()` still emits a repo-wide mandatory skill index. A rough current index-entry estimate is about `5266` chars.
- Builtin memory is not the dominant problem. Runtime files and logs align around `builtinMemoryChars≈2590-2604`.
- Current Stardew skill content is more compact than before and now better suited as source material for a short autonomy digest.

Constraints:
- Reuse existing compression, memory, session_search, and skill systems. Do not create parallel recap/persona-summary lanes.
- Keep the agent/tool boundary intact: tools and memory remain on-demand retrieval paths.
- Default context target remains around 5K, so the plan must assume aggressive system-layer reduction.
- Preserve autonomy behavior more than prose richness; private chat does not need to be optimized in the same pass unless required.

Unknowns/open questions:
- Whether autonomy runtime should disable global skills-mandatory guidance entirely or replace it with a Stardew-only micro-index.
- Whether soul/project-rules injection for autonomy needs a separate cap after supplement and skills changes.
- Whether any existing tests/assertions depend on current full-text supplement content.

Likely codebase touchpoints:
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/runtime/AgentCapabilityAssembler.cs`
- `src/skills/SkillManager.cs`
- `Desktop/HermesDesktop.Tests/Stardew/*`
