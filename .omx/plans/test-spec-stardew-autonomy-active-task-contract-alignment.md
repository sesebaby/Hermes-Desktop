# Test Spec: Stardew Autonomy Active Task Contract Alignment

Date: 2026-05-06
Status: Approved by `$ralplan` consensus review
PRD: `.omx/plans/prd-stardew-autonomy-active-task-contract-alignment.md`

## Goal

Prove the active-task classification path matches the reference-aligned contract and no longer protects ordinary autonomy user prompts by mistake.

## Target Files

- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`

Optional only if prompt wording changes:

- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`

## Required Tests

1. `BudgetPolicy_UserPromptContainingActiveTodo_IsCurrentUserNotActiveTask`
   - Build an autonomy budgeting input where the latest user message contains `active todo`.
   - Assert the output/log classification does not attribute that message to `activeTaskChars`.
   - Assert current user accounting is preserved.

2. `BudgetPolicy_SystemMessageWithPreservedTaskHeader_RemainsActiveTaskContext`
   - Provide a system message beginning with `[Your active task list was preserved across context compression]`.
   - Assert it remains protected as active task context.

3. `BudgetPolicy_UserMessageWithPreservedTaskHeader_IsNotActiveTaskContext`
   - Provide a user-role message containing the same header text.
   - Assert it is not classified as active task context.

4. `BudgetPolicy_ReproductionShape_DoesNotZeroCurrentUserChars`
   - Recreate the observed misclassification shape:
     - real preserved task block
     - latest autonomy user prompt containing `active todo`
   - Assert `currentUserChars > 0`.
   - Assert active task accounting reflects the real preserved block only.

## Regression Expectations

- Existing tests that prove real active task blocks are protected must continue to pass.
- Existing tests for dynamic recall trimming, protected tail, and budget reason logging must continue to pass.

## Verification Commands

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextBudget"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## Manual Verification After Execution

Inspect:

- `%LOCALAPPDATA%\hermes\hermes-cs\logs\hermes.log`

Expected evidence:

- reproduction no longer shows `currentUserChars=0` caused by active-task classification
- `activeTaskChars` reflects the real preserved active task block rather than repeated autonomy user prompts
- no regression in protected continuation behavior
