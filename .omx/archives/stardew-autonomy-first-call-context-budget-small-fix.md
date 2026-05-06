# Stardew Autonomy First-Call Context Budget Small Fix

## Requirements Summary

- Fix the false `active_task_context` classification in Stardew autonomy first-call budgeting.
- Keep the fix reference-aligned with `external/hermes-agent-main`:
  - active task context is recognized by the fixed preserved-task header, not broad keywords;
  - ordinary task continuity comes from the latest user-turn tail, not from keyword matching inside arbitrary prompts.
- Do not add any new memory, recap, persona, or summary lane.

## Grounded Findings

- `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs` currently treats any message containing `active todo`, `active task`, or `Task Context` as protected active-task context.
- `src/runtime/NpcAutonomyLoop.cs` currently emits the autonomy decision prompt text `先看当前观察事实和 active todo，再决定...`, so normal autonomy user prompts can be misclassified.
- The reference project injects preserved active tasks with the exact header `[Your active task list was preserved across context compression]` in `external/hermes-agent-main/tools/todo_tool.py:117`.
- The reference compressor protects task continuity by ensuring the most recent user message stays in the tail in `external/hermes-agent-main/agent/context_compressor.py:1134-1136`, not by broad keyword classification.

## Scope

- 1 bugfix path across 2-3 files.
- Estimated complexity: LOW.

## Minimum Files To Touch

- Code: `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs`
- Tests: `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`
- Optional defensive wording cleanup: `src/runtime/NpcAutonomyLoop.cs`

## Plan

1. Narrow active-task classification in `StardewAutonomyFirstCallContextBudgetPolicy`.
   - Replace the broad `LooksLikeActiveTaskContext()` keyword heuristic with reference-aligned detection centered on the fixed preserved-task header.
   - Keep protection for real injected active-task blocks.
   - Remove fallback matching on ordinary phrases like `active todo`, `active task`, and `Task Context` for runtime classification.

2. Add one defensive wording cleanup in `NpcAutonomyLoop`.
   - Rephrase the autonomy decision prompt so it no longer includes the exact `active todo` wording.
   - Keep intent unchanged: the NPC should still consider current observed facts and ongoing work before acting.
   - This is defense in depth; the primary fix remains classifier narrowing.

3. Add focused regression tests in `StardewAutonomyContextBudgetTests`.
   - Add a test proving a normal autonomy user prompt containing the old `active todo` phrase is counted as `current_user`, not `active_task_context`.
   - Keep or add a test proving the fixed preserved-task header still counts as `active_task_context`.
   - Assert completed logs no longer show inflated `activeTaskChars` for the ordinary autonomy-prompt case, and `currentUserChars` reflects the live user prompt instead.

4. Verify only the affected Stardew autonomy budget surface.
   - Run the focused autonomy context-budget test filter first.
   - If the focused suite passes, optionally run the full `StardewAutonomyContextBudgetTests` class filter for regression confidence.

## Acceptance Criteria

1. A normal autonomy decision user message is not classified as `active_task_context` just because it contains `active todo`, `active task`, or `Task Context`.
2. A real preserved active-task block with header `[Your active task list was preserved across context compression]` is still classified and protected as `active_task_context`.
3. For the reproduced false-positive shape, logs show `activeTaskChars` near the real preserved-task payload size instead of tens of thousands of chars from the current user prompt.
4. For the reproduced false-positive shape, `currentUserChars` is non-zero and reflects the current autonomy decision prompt.
5. The fix does not introduce any new memory/recap/persona/summary lane.
6. Existing protected-tail behavior and other current budget tests remain green.

## Risks And Mitigations

- Risk: older non-reference fixtures may rely on the removed keyword fallback.
  - Mitigation: update tests to use the fixed preserved-task header explicitly; that is the intended contract.

- Risk: changing `NpcAutonomyLoop` wording could slightly alter prompt behavior.
  - Mitigation: keep the rewrite minimal and semantic-equivalent; treat it as optional defense in depth, not the core fix.

- Risk: current workspace already has overlapping uncommitted edits in the same files.
  - Mitigation: implement on top of the existing diff carefully; do not revert unrelated user changes.

## Verification

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextBudgetTests"
```

Optional follow-up:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextBudget"
```
