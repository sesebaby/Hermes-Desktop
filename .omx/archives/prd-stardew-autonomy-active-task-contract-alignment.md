# PRD: Stardew Autonomy Active Task Contract Alignment

Date: 2026-05-06
Status: Approved by `$ralplan` consensus review
Context: `.omx/context/stardew-autonomy-reference-aligned-context-budget-20260505T125154Z.md`

## Goal

Align Stardew autonomy first-call context budgeting with the reference project's active-task handling by classifying active task context from the real injection contract only, not from broad keyword heuristics.

## Root Cause

`src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs` currently classifies `active_task_context` via broad text matching:

- `active todo`
- `active task`
- `Task Context`

`src/runtime/NpcAutonomyLoop.cs` includes `active todo` inside the ordinary autonomy decision user prompt. As a result, repeated autonomy user prompts are misclassified as protected active task context. Runtime evidence showed:

- `activeTaskChars=25593`
- `currentUserChars=0`

Direct transcript inspection showed the actual latest `todo` tool payloads are only about `412-1428` chars, so the protected `25K+` category is not real active task data.

## Reference Alignment

Reference project behavior:

- `external/hermes-agent-main/tools/todo_tool.py` injects active tasks with the fixed header `[Your active task list was preserved across context compression]`.
- `external/hermes-agent-main/agent/context_compressor.py` preserves task continuity through the last user message in the tail, not by broad keyword classification.

This fix should align with that behavior.

## Decision

Treat active task context as a strict injected-contract surface:

1. Only classify a message as `active_task_context` when:
   - `role == system`
   - content begins with `[Your active task list was preserved across context compression]`
2. Remove broad fallback matching for `active todo`, `active task`, and `Task Context` from runtime budgeting logic.
3. Do not rely on changing autonomy prompt wording to make the fix work.

## Scope

In scope:

- `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`

Optional only:

- `src/runtime/NpcAutonomyLoop.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`

Optional wording cleanup is allowed, but the root-cause fix must stand without it.

## Required Behavior

### Classification Contract

- Real preserved active task injection remains protected.
- Ordinary autonomy user prompts remain classified as `current_user`.
- A `user` message containing the same fixed header must still not be classified as `active_task_context`.

### Invariants

- Current-user protection remains unchanged.
- Latest assistant/tool continuation protection remains unchanged.
- Dynamic recall handling remains unchanged by this fix.
- No new memory, recap, persona, or summary lane is introduced.

## Acceptance Criteria

1. Ordinary autonomy user prompts containing `active todo` no longer count toward `activeTaskChars`.
2. Reproduction-shape budgeting no longer yields `currentUserChars=0` because of active-task misclassification.
3. Real system-message preserved-task header remains classified as `active_task_context`.
4. A user-role message containing the preserved-task header is not classified as `active_task_context`.
5. The fix does not require `NpcAutonomyLoop` wording changes.
6. No new budgeting category or memory lane is introduced.

## Risks

- Risk: classification becomes too narrow and misses future active-task injection variants.
  - Mitigation: lock the current injection contract with tests; if the injection contract changes later, update both producer and tests together.

- Risk: future contributors reintroduce keyword heuristics.
  - Mitigation: keep explicit regression coverage for ordinary user prompts that mention `active todo`.

## Verification

- Focused budget-policy unit tests for role + header classification.
- Existing broader autonomy context-budget test suite.

## ADR

Decision:
Classify active task context from the actual injected system-block contract only.

Drivers:
- Root cause is a false positive, not oversized todo data.
- Reference project uses a fixed preserved-task header and tail preservation, not keyword heuristics.
- Smallest safe fix is to tighten classification, not to redesign context assembly.

Alternatives considered:
- Keep broad keyword heuristics | rejected because they are the direct cause of misclassification.
- Change autonomy prompt wording only | rejected because it hides the symptom without fixing the broken classifier.
- Introduce another task summary lane | rejected because it duplicates existing systems and violates project constraints.

Consequences:
- Classification becomes stricter and more predictable.
- Producer/consumer contract for active task injection is now explicit and test-locked.

Follow-ups:
- Optional wording cleanup in autonomy prompt if desired, but not required for correctness.
