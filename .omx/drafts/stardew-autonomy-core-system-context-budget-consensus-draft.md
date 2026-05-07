# Stardew Autonomy Core System Context Budget

Goal: make the default 5K first-call budget realistic for Stardew autonomy without regressing NPC behavior contracts or adding duplicate memory/summary lanes.

## RALPLAN-DR

### Principles
- Keep autonomy first-call system prompt small, stable, and reference-aligned.
- Preserve only always-on contracts needed for continuity, task execution, and safety.
- Keep memory and `session_search` on-demand, not duplicated as a recap lane.
- Prefer removing/condensing prompt weight over widening the budget.

### Decision Drivers
- Current blocker is `core_system_over_budget`, with runtime `systemChars` around 22K-24K.
- `StardewNpcAutonomyPromptSupplementBuilder` still injects persona facts/voice/boundaries plus full required Stardew skill texts (~6.8K chars).
- `SkillManager.BuildSkillsMandatoryPrompt()` adds a repo-wide mandatory skill index (~5.2K chars).

### Viable Options
1. **Split autonomy into a compact system prompt path.**
   - Pros: directly targets the over-budget layer; preserves contracts with smaller text.
   - Cons: requires a Stardew-specific prompt assembly branch.
2. **Keep current prompt assembly and raise the budget.**
   - Pros: low implementation effort.
   - Cons: masks the problem; conflicts with the 5K target and reference alignment.
3. **Move required skills/persona into a new summary lane.**
   - Pros: reduces first-call size quickly.
   - Cons: duplicates memory/summary systems the user explicitly does not want.

### Recommended Path
Choose option 1: create a Stardew autonomy-specific compact prompt path that keeps only minimal always-on rules, replaces full required-skill text with a short on-demand index/digest, and avoids the repo-wide mandatory skills block in first-call autonomy.

## Implementation Steps
1. Define the exact minimum always-on autonomy contract: movement, speaking, task continuity, tool-first behavior, and `session_search` escalation.
2. Replace the full Stardew supplement payload with a compact autonomy digest plus short pointers to on-demand skills.
3. Stop injecting the global `Skills (mandatory)` index into Stardew autonomy first calls, or replace it with a Stardew-only micro-index.
4. Wire the autonomy prompt builder so private chat/global agent behavior stays unchanged.
5. Add tests that assert the first-call system budget drops below 5K when protected content fits, while required NPC contracts still load.

## Tests
- Prompt supplement size regression for `StardewNpcAutonomyPromptSupplementBuilder`.
- Mandatory-skills prompt size regression for autonomy sessions.
- Budget-policy test proving `systemChars` no longer lands in the 22K-24K range for the normal Stardew path.
- Contract tests for `session_search`, tool use, and no duplicate memory/summary lane.
- Background-service test proving NPC autonomy still starts with the compact prompt path.

## Risks
- Over-trimming may remove a behavior contract the NPC actually needs on first call.
- A micro-index may still be too large if it stays too descriptive.
- Tests may currently encode the old full-text prompt shape and need intentional updates.

## ADR

### Decision
Adopt a Stardew autonomy-specific compact system prompt path instead of expanding the budget or adding a second summary lane.

### Drivers
- The current failure is in core system size, not active-task misclassification or builtin memory.
- Reference behavior is tool-first and on-demand, not full-text preload-heavy.
- User constraint: no duplicated memory/summary systems.

### Alternatives Considered
- Raise the budget: rejected as a workaround.
- Add recap/persona memory lanes: rejected as duplication.
- Keep current prompt shape and hope later trimming fixes it: rejected because the blocker is upstream in the system layer.

### Consequences
- First-call prompt size should become realistic.
- Some background knowledge shifts to on-demand retrieval and skill loading.
- Prompt assembly becomes more explicit about what is always-on versus fetched when needed.

### Follow-ups
- Re-check runtime logs for `systemChars`, `budgetMet`, and `budgetUnmetReason`.
- If the compact path still exceeds target, shrink the micro-index before touching memory.
- Keep private chat and non-Stardew paths on the existing prompt assembly.

