# Test Spec: Stardew Autonomy Reference-Aligned Context Budget

Date: 2026-05-05
Status: Approved by `$ralplan` consensus review
PRD: `.omx/plans/prd-stardew-autonomy-reference-aligned-context-budget.md`

## Test Goals

1. Prove dynamic recall no longer defeats the 5K autonomy budget.
2. Prove `<memory-context>` appended to the current user message is treated as trim-eligible recall while the live decision text is preserved.
3. Prove built-in memory/profile remain bounded and separately diagnosed.
4. Prove `session_search`, memory parity, continuation protection, and status-class budget do not regress.
5. Prove no new recap/persona/memory summary lane is introduced.

## Target Files

- Modify: `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`
- Modify or extend: `Desktop/HermesDesktop.Tests/Runtime/NpcAgentFactoryTests.cs`
- Modify or extend: `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
- Modify or extend: `Desktop/HermesDesktop.Tests/Services/MemoryToolTests.cs`

## Test Matrix

### A. Marker Scope

1. `BudgetPolicy_MarkerMissing_NoOps`
   - Existing behavior remains: no marker, no trimming, no budget logs.

2. `BudgetPolicy_MarkerFalse_NoOps`
   - Explicit false marker no-ops.

3. `PrivateChat_WithTraceAndNpcIdButNoAutonomyMarker_DoesNotBudget`
   - `traceId` and `npcId` do not activate the policy.

### B. Dynamic Recall Trimming

4. `BudgetPolicy_LargeRelevantMemoriesSystemBlock_TrimsDynamicRecallToMeetBudget`
   - Input has core system, current user, old tool noise, and a large system `[Relevant Memories]` block.
   - Assert output `<= 5000` when other protected content fits.
   - Assert recall text is truncated with deterministic marker.
   - Assert result does not report `recall_block`.

5. `BudgetPolicy_LargeMemoryContextInCurrentUser_TrimsRecallButKeepsDecisionText`
   - Current user content is `decision text + <memory-context>very long recall</memory-context>`.
   - Assert decision text survives exactly.
   - Assert `<memory-context>` content is capped.
   - Assert output `<= 5000`.

6. `BudgetPolicy_DynamicRecallAcrossSystemAndUser_UsesSharedCap`
   - Include both `[Relevant Memories]` and `<memory-context>`.
   - Assert combined dynamic recall after budget is within the configured cap range.

7. `BudgetPolicy_DynamicRecallTrim_DoesNotPersistIntoSessionMessages`
   - Drive through `Agent.ChatAsync` with fake client and transcript store.
   - Assert trimmed recall marker appears only in first outbound payload, not persisted transcript/session messages.

8. `BudgetPolicy_MalformedMemoryContext_TrimsFromOpeningTagAndKeepsDecisionPrefix`
   - Current user content is `decision text + <memory-context>unterminated recall`.
   - Assert decision text before the opening tag survives exactly.
   - Assert malformed recall text is capped and marked as trimmed.

9. `BudgetPolicy_RepeatedMemoryContextBlocks_UsesSharedDynamicRecallCap`
   - Current user content has two complete `<memory-context>` blocks.
   - Assert combined retained recall stays within the configured cap range.

### C. Reason Codes And Diagnostics

10. `BudgetPolicy_RecallTrimmed_BudgetMet_LogsDynamicRecallTrimmedDiagnostic`
   - Assert logs include `dynamicRecallCharsBefore`, `dynamicRecallCharsAfter`, and `dynamic_recall_trimmed`.
   - Assert final `budgetMet=True`.

11. `BudgetPolicy_LogsCompleteCategoryFieldSet`
   - Trigger any autonomy budget application.
   - Assert completed log includes `systemChars`, `builtinMemoryChars`, `dynamicRecallCharsBefore`, `dynamicRecallCharsAfter`, `activeTaskChars`, `protectedTailChars`, `currentUserChars`, `charsBefore`, `charsAfter`, `budgetMet`, and `budgetUnmetReason`.

12. `BudgetPolicy_RecallTrimmed_DoesNotEmitRecallBlockMissReason`
   - Regression guard for the observed failure.

13. `BudgetPolicy_CoreSystemOverBudget_LogsCoreSystemReason`
    - Core system alone exceeds 5K.
    - Assert protected content is preserved and reason is `core_system_over_budget`.

14. `BudgetPolicy_BuiltinMemoryOverBudget_LogsBuiltinMemoryReason`
    - Built-in memory/profile category exceeds expected pressure.
    - Assert reason or diagnostic identifies `builtin_memory_over_budget`.

15. `BudgetPolicy_ProtectedTailOverBudget_LogsProtectedTailReason`
    - Latest continuation group alone exceeds budget.
    - Assert `protected_tail_over_budget`.

16. `BudgetPolicy_ActiveTaskOverBudget_LogsActiveTaskReason`
    - Active task context alone exceeds budget.
    - Assert `active_task_context_over_budget`.

17. `BudgetPolicy_MixedOverflow_UsesDocumentedReasonPriority`
    - Construct a mixed miss, for example core system plus built-in memory plus protected tail.
    - Assert the first matching documented priority wins.

### D. Existing Invariants

18. `BudgetPolicy_ProtectsLatestAssistantToolRequestAndMatchingToolResults`
    - Existing structural `ToolCallId` protection remains.

19. `StatusBudget_MarkerTrue_BlocksSecondBroadStatusAcrossDifferentToolNames`
    - Existing status-class guard remains.

20. `StatusBudget_MarkerTrue_AllowsStardewTaskStatusAfterBroadStatus`
    - `stardew_task_status` remains exception.

21. `StatusBudget_NoMarker_AllowsBroadStatusAcrossDifferentToolNames`
    - Private/non-autonomy path remains unchanged.

### E. Memory And Session Search Parity

22. `MemoryManager_DefaultLimits_RemainReferenceAligned`
    - Construct `MemoryManager` default and assert memory/user cap behavior matches 2200/1375 through add failures or exposed usage.

23. `BuiltinMemoryPlugin_UsesFrozenSnapshotUntilNewSession`
    - Existing test remains passing.

24. `TurnMemoryCoordinator_DoesNotInjectCuratedMemoryThroughDynamicRecall`
    - Existing parity behavior remains passing.

25. `SessionSearchTool_RemainsAvailableToNpcAutonomyAgents`
    - NPC agent tool definitions include `session_search`.

26. `TranscriptRecallService_SearchSessionSummaries_StillClampsToFiveSessions`
    - Existing parity or new focused test proves summary search does not become a preloaded long-history lane.

### F. No New Lane

27. `BudgetPolicy_DoesNotCreateNpcRecapPersonaOrMemorySummaryBlock`
    - After budgeting, no new message claims to summarize NPC identity, persona, or complete memory.

28. `StardewSkillAssets_DoNotIntroduceSecondSummaryLane`
    - Real skill assets do not instruct creation of NPC recap/persona/memory summary.

29. `StardewSkillAssets_KeepOnePurposeAndOnDemandSessionSearchGuidance`
    - Real skill assets contain one-turn-purpose, `session_search`, durable-only `memory`, and no repeated broad status scan guidance.

## Fixtures And Helpers

- Capturing fake `IChatClient` for first outbound payload.
- In-memory logger provider that records structured log strings.
- Message builders for:
  - core system;
  - built-in memory/profile-looking system blocks;
  - `[Relevant Memories]`;
  - `<memory-context>`;
  - latest assistant/tool continuation group;
  - active task context.
- Transcript store fixture to prove dynamic recall trim markers are not persisted.

## Acceptance Mapping

- PRD AC1 -> tests 1-3.
- PRD AC2-5 -> tests 4-12.
- PRD AC6 -> tests 13-15, 22-24.
- PRD AC7 -> test 18.
- PRD AC8 -> test 7.
- PRD AC9 -> tests 25-26.
- PRD AC10 -> tests 27-28.
- PRD AC11 -> tests 10-17.
- PRD AC12 -> tests 22-26.
- PRD AC13 -> tests 8-9.
- PRD AC14 -> test 17.

## Verification Commands

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextBudget"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~MemoryParity"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## Manual Verification After Execution

Inspect:

- `%LOCALAPPDATA%\hermes\hermes-cs\logs\hermes.log`
- `%LOCALAPPDATA%\hermes\hermes-cs\runtime\stardew\games\stardew-valley\saves\<saveId>\npc\<npcId>\profiles\<profileId>\activity\runtime.jsonl`

Required evidence:

- `charsAfter <= 5000` when protected content fits;
- `dynamicRecallCharsAfter <= 1200`;
- no new `budgetUnmetReason=recall_block`;
- if budget still misses, reason identifies core system, built-in memory, protected tail, or active task pressure.
