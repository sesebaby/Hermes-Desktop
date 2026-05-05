# Test Spec: Stardew NPC Autonomy Context Budget

Date: 2026-05-05
Status: Approved by `$ralplan` consensus review
PRD: `.omx/plans/prd-stardew-npc-autonomy-context-budget.md`

> **For agentic workers:** Implement tests before production code where practical. Prompt/skill boundary tests must read real repo assets, not fixture text.

## Test Goals

1. Prove first-call budgeting activates only for explicit Stardew autonomy sessions.
2. Prove ordinary `Agent`, NPC private chat, and marker-absent NPC sessions remain no-op.
3. Prove latest continuation group protection uses `ToolCalls` / `ToolCallId` structure.
4. Prove status-class budget is marker-gated and `stardew_task_status` is a continuation exception.
5. Prove deterministic pruning does not create a second recap/persona/memory summary lane.
6. Prove structured logs contain counters and `budget_unmet_reason` values.

## Likely Test Files

- Create: `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`
- Modify or extend: `Desktop/HermesDesktop.Tests/Runtime/NpcAgentFactoryTests.cs`
- Modify or extend: `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- Modify or extend: `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- Modify or extend: `Desktop/HermesDesktop.Tests/Services/AgentInvariantTests.cs`

## Test Matrix

### A. Marker Gating And No-Op Paths

1. `Agent_WithoutBudgetPolicy_FirstCallBehaviorUnchanged`
   - Construct ordinary `Agent` without policy.
   - Capture first `CompleteWithToolsAsync(...)` messages.
   - Assert no budgeting logs and original messages are preserved.

2. `BudgetPolicy_MarkerMissing_NoOps`
   - Inject policy but omit autonomy marker.
   - Assert returned messages are reference- or value-equivalent to input.
   - Assert no `autonomy_context_budget_started` / `completed`.

3. `BudgetPolicy_MarkerFalse_NoOps`
   - Set marker to false.
   - Assert no pruning and no budget logs.

4. `PrivateChat_WithTraceAndNpcIdButNoAutonomyMarker_DoesNotBudget`
   - Simulate NPC private chat session with `traceId` and `npcId`.
   - Do not set autonomy marker.
   - Assert budget policy and status-class blocking do not activate.

5. `NpcNonAutonomySession_NoMarker_DoesNotBudgetOrStatusGate`
   - Simulate NPC session without autonomy marker.
   - Assert first-call messages and broad status execution are unchanged.

6. `NpcAutonomyLoop_SetsAutonomyMarker`
   - Drive or inspect an autonomy decision session.
   - Assert `session.State[AutonomyMarker] == true`.

### B. Injection And First-Call Timing

7. `NpcAgentFactory_Create_PassesBudgetPolicyFromContextBundle`
   - Build `NpcRuntimeContextBundle` with a test policy.
   - Create agent through `NpcAgentFactory`.
   - Assert first call reaches the injected policy when marker is true.

8. `Agent_FirstToolIteration_AppliesPolicyBeforeClientCall`
   - Fake policy returns a known trimmed message set.
   - Fake chat client captures first `CompleteWithToolsAsync(...)` payload.
   - Assert captured payload is the policy output.

9. `Agent_LaterToolIterations_DoNotReapplyFirstCallPolicy`
   - Simulate two tool iterations.
   - Assert policy called once, only on iteration 1.
   - Assert later iteration uses `session.Messages`.

### C. Character Budget Behavior

10. `BudgetPolicy_WhenProtectedContentFits_ReducesToFiveThousandChars`
    - Construct large old tool payloads and a small protected tail.
    - Assert output chars `<= 5000`.
    - Assert current autonomy user message and protected blocks remain.

11. `BudgetPolicy_ProtectedContentOverBudget_PreservesProtectedContentAndLogsReason`
    - Construct protected content alone above 5,000 chars.
    - Assert protected content remains.
    - Assert `budgetMet == false`.
    - Assert reason is `protected_content_over_budget` or more specific protected reason.

12. `BudgetPolicy_RecallBlockOverBudget_LogsRecallBlockReason`
    - Construct recall/memory block as the protected budget pressure source.
    - Assert `budget_unmet_reason == recall_block`.

13. `BudgetPolicy_ActiveTaskContextOverBudget_LogsActiveTaskContextReason`
    - Construct active task context as the protected budget pressure source.
    - Assert `budget_unmet_reason == active_task_context`.

### D. Structural Continuation Protection

14. `BudgetPolicy_ProtectsLatestAssistantToolRequestAndMatchingToolResults`
    - Build multiple assistant tool-request groups.
    - Latest assistant message has multiple `ToolCall.Id` values.
    - Assert latest assistant message and every matching tool message remain.
    - Assert older unprotected groups can be pruned.

15. `BudgetPolicy_DoesNotUseKeywordGuessingForContinuationProtection`
    - Include a structurally valid group without continuation keywords.
    - Include a keyword-looking old message without matching `ToolCallId`.
    - Assert structure wins and keywords do not protect old content.

16. `BudgetPolicy_DoesNotCreateOrphanedProtectedToolResults`
    - After pruning, every remaining protected tool message should either have its assistant request preserved or be an allowed deterministic placeholder for unprotected old content.

### E. Allowed Pruning Only

17. `BudgetPolicy_PrunesOnlyByAllowedOperations`
    - Inputs include old long tool results, old assistant tool args, duplicate status results.
    - Assert changes are only delete/truncate/dedupe/deterministic placeholder.

18. `BudgetPolicy_DoesNotCreateNpcRecapPersonaOrMemorySummaryBlock`
    - Compare message count/types/content patterns before and after.
    - Assert no new recap/persona/memory summary role block exists.
    - Assert no content outside existing memory/recall systems claims to summarize NPC identity or history.

### F. Status-Class Budget

19. `StatusBudget_MarkerTrue_BlocksSecondBroadStatusAcrossDifferentToolNames`
    - In one autonomy-marked turn, call `stardew_status`, then `stardew_social_status`.
    - Assert second call returns `status_tool_budget_exceeded`.
    - Assert real tool/bridge executor is not invoked for second call.

20. `StatusBudget_MarkerTrue_AllowsStardewTaskStatusAfterBroadStatus`
    - Call one `broad_status`, then `stardew_task_status`.
    - Assert task status executes.

21. `StatusBudget_MarkerTrue_BlocksRepeatedBroadStatusAfterTaskStatus`
    - Sequence: broad status -> `stardew_task_status` -> broad status.
    - Assert final broad status is blocked.

22. `StatusBudget_NoMarker_DoesNotBlockBroadStatusClass`
    - Same cross-tool broad status sequence without autonomy marker.
    - Assert new status-class budget does not activate.

23. `StatusBudget_MarkerFalse_DoesNotBlockBroadStatusClass`
    - Same cross-tool broad status sequence with marker false.
    - Assert no class blocking.

24. `StatusBudget_BlockedBroadStatus_LogsStatusClass`
    - Assert warning includes `status_tool_budget_exceeded`, `sessionId`, `toolName`, and status class.

### G. Skill Asset Tests

25. `StardewSkillAssets_ContainSinglePurposeAndOnDemandRecallGuidance`
    - Read real files from repo:
      - `skills/gaming/stardew-core.md`
      - `skills/gaming/stardew-social.md`
      - `skills/gaming/stardew-navigation.md`
    - Assert guidance exists for one turn purpose, on-demand recall/tools, no repeated broad status scans, and `stardew_task_status` continuation.

26. `StardewSkillAssets_DoNotIntroduceSecondSummaryLane`
    - Read the same real files.
    - Assert no instruction creates a new NPC recap/persona/memory summary lane.

### H. Logging

27. `BudgetPolicy_MarkerTrue_LogsStartedAndCompleted`
    - Use in-memory logger provider.
    - Assert started/completed events contain `sessionId`, `traceId`, `npcId`, `charsBefore`, `charsAfter`, `budgetChars`, `budgetMet`.

28. `BudgetPolicy_NoMarker_DoesNotLogBudgetEvents`
    - Omit marker.
    - Assert no autonomy budget events.

29. `BudgetPolicy_BudgetMiss_LogsReasonCode`
    - Trigger each supported miss reason where practical:
      - `protected_tail`
      - `recall_block`
      - `active_task_context`
      - `protected_content_over_budget`
      - `unknown` only for fallback/default case.

### I. End-To-End Autonomy Regression

30. `NpcAutonomy_FirstRequestIsBudgetedBeforeFakeChatClientReceivesIt`
    - Drive one autonomy turn through `NpcAutonomyLoop`.
    - Seed transcript/context with old long tool payloads.
    - Assert fake chat client receives budgeted first-call messages.
    - Assert marker is true.

31. `NpcAutonomy_ProtectedContentPreservedWhenBudgetMissOccurs`
    - Drive high-pressure autonomy sample.
    - Assert current decision message, active task context, recall block, and latest continuation group remain.
    - Assert reason-coded budget miss log exists.

## Fixtures And Helpers

Suggested helpers:

- fake `IChatClient` capturing every `CompleteWithToolsAsync(...)` payload;
- in-memory `ILoggerProvider`;
- message builders for system/user/assistant/tool messages;
- `ToolCall` builders with explicit IDs;
- sample prepared context builders for:
  - old long tool result payloads;
  - duplicate broad status results;
  - latest continuation groups;
  - recall block over budget;
  - active task context over budget;
  - protected content over budget.

## Acceptance Mapping

- PRD AC1-3 -> tests 2, 3, 4, 5, 6, 22, 23, 28.
- PRD AC4-5 -> tests 10, 11, 12, 13, 29, 31.
- PRD AC6 -> tests 14, 15, 16.
- PRD AC7-8 -> tests 17, 18, 26.
- PRD AC9-10 -> tests 19, 20, 21, 22, 23, 24.
- PRD AC11 -> tests 25, 26.
- PRD AC12 -> tests 27, 28, 29.

## Verification Commands

Targeted:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~ContextBudget"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StatusBudget"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomy"
```

Full desktop tests:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## Exit Criteria

1. Marker true/false/absent behavior is tested.
2. Private chat with `traceId` / `npcId` but without autonomy marker remains no-op.
3. First-call payload budgeting is proven before fake chat client receives messages.
4. Latest continuation group is structurally protected by `ToolCallId`.
5. Status-class budget only activates under autonomy marker.
6. `stardew_task_status` remains a continuation exception.
7. No second summary/persona/memory lane is introduced.
8. Reason-coded logs are asserted.
