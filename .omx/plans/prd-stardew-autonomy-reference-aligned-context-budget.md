# PRD: Stardew Autonomy Reference-Aligned Context Budget

Date: 2026-05-05
Status: Approved by `$ralplan` consensus review
Context: `.omx/context/stardew-autonomy-reference-aligned-context-budget-20260505T125154Z.md`
Supersedes: `.omx/plans/prd-stardew-npc-autonomy-context-budget.md` for recall/memory protection behavior only

> Planning artifact only. Do not implement from the old context-budget PRD where it says existing recall or memory blocks are protected.

## Goal

Make Stardew NPC autonomy first-call context budget behavior match the reference project: built-in memory remains bounded and frozen, long history is retrieved through `session_search` on demand, and dynamic recall in the first request is capped instead of hard-protected.

The default target remains `<= 5000` chars for marker-gated autonomy first calls when core protected content fits.

## Problem

The current implementation faithfully followed a flawed prior plan. It protects recall-looking blocks, so large `Relevant Memories`, `USER PROFILE`, `session_search`, or `recall` text can exceed the 5K target and report `budgetUnmetReason=recall_block`.

Observed manual logs showed `budgetMet=False`, `budgetUnmetReason=recall_block`, and 31K-40K first-call payloads.

## RALPLAN-DR Summary

### Principles

1. Align with `external/hermes-agent-main`: bounded frozen curated memory, on-demand `session_search`, and separate long-session compression.
2. Keep Stardew autonomy first-call budgeting marker-gated and leave ordinary chat/private chat unchanged.
3. Reuse existing `MemoryManager`, `BuiltinMemoryPlugin`, `TurnMemoryCoordinator`, `TranscriptRecallService`, `ContextManager`, and `session_search`.
4. Do not create a new NPC recap, persona summary, memory summary, or second recall lane.
5. Protect structural continuation and current task intent; cap dynamic history by default.

### Decision Drivers

1. The actual first payload control point is `Agent.ChatAsync` before first `CompleteWithToolsAsync(...)` at `src/Core/Agent.cs:434`.
2. Reference memory is bounded/frozen (`external/hermes-agent-main/tools/memory_tool.py:116`), and reference `session_search` is a tool with default 3/max 5 summaries.
3. The failure is caused by hard-protected recall content in `StardewAutonomyFirstCallContextBudgetPolicy`, not by missing compaction.

### Viable Options

1. **Reference-aligned bounded dynamic recall.**
   - Pros: fixes observed 31K-40K payloads; matches reference boundaries; no new memory lane.
   - Cons: first call may need `session_search` more often when important history is outside the capped recall window.

2. **Keep all recall/memory blocks hard-protected.**
   - Pros: maximum immediate history visibility.
   - Cons: already failed 5K; contradicts reference design; preserves `recall_block` as a permanent escape hatch.

3. **Push the 5K rule into `ContextManager` or global compaction.**
   - Pros: centralized context policy.
   - Cons: broad blast radius; affects non-Stardew and non-autonomy paths; mixes workload-specific char budget with generic context management.

**Decision:** choose option 1.

## Required Behavior

### Marker Scope

- Budgeting remains active only when `StardewAutonomySessionKeys.IsAutonomyTurn` is true.
- Missing or false marker remains no-op.
- `traceId`, `npcId`, platform, and session ID shape remain observability fields only.

### Context Categories

Execution must classify and log these categories before and after budgeting:

- `systemChars`: core system/soul/mandatory skill/Stardew supplement.
- `builtinMemoryChars`: frozen curated memory/profile from `BuiltinMemoryPlugin` / `MemoryManager`.
- `dynamicRecallCharsBefore` and `dynamicRecallCharsAfter`: `TurnMemoryCoordinator` prefetch output, `<memory-context>`, legacy `[Relevant Memories]`, transcript recall snippets, or other recall/search-derived first-call content.
- `activeTaskChars`: active task/todo continuation context.
- `protectedTailChars`: latest assistant tool-call request plus matching tool results by `ToolCallId`.
- `currentUserChars`: current autonomy decision message after recall trimming.

Classification rules are part of the contract:

| Category | Detection source | Protection / trim behavior |
| --- | --- | --- |
| `systemChars` | system messages that are not built-in memory/profile and not dynamic recall. Prompt guidance that mentions `session_search` is still core system text. | protected |
| `builtinMemoryChars` | system message content rendered by `MemoryManager.RenderBlock`, identified by `MEMORY (your personal notes)` or `USER PROFILE (who the user is)` headers. | protected only as already-bounded curated memory/profile |
| `dynamicRecallChars` | legacy system `[Relevant Memories]`, any delimited `<memory-context>...</memory-context>` segment, transcript recall snippets injected by `TurnMemoryCoordinator`, or system note text beginning `The following is recalled memory context`. | trim-eligible, shared cap |
| `activeTaskChars` | active task context injected through `ContextManager` / `PromptBuilder.ActiveTaskContext`, primarily identified by fixed header `[Your active task list was preserved across context compression]`. Legacy test fixtures may fall back to `active todo`, `active task`, or `Task Context` only when no fixed header is present. | protected |
| `protectedTailChars` | latest assistant message with `ToolCalls` plus matching tool messages by `ToolCallId`. | protected |
| `currentUserChars` | current user/autonomy decision text after removing or capping dynamic recall segments. | protected |

Do not classify text as dynamic recall merely because it mentions `session_search`, `memory`, or `recall` in instructions. The old broad keyword heuristic is the bug.

Dynamic recall parsing should extend the existing recall fence/sanitizer semantics used by `TurnMemoryCoordinator.BuildMemoryContextBlock` and `TranscriptRecallService.SanitizeContext`; do not create a second parser lane with new headers or new persistent recall formats.

### Protected Content

Always preserve:

- core system/soul/mandatory skill/Stardew supplement;
- current autonomy decision request, excluding trim-eligible dynamic recall appended to it;
- active task context required to continue an in-flight action;
- latest structural continuation group: latest assistant `ToolCalls` and matching tool messages by `ToolCallId`;
- built-in curated memory/profile only within existing configured limits.

Do not hard-protect dynamic recall.

### Dynamic Recall Cap

Dynamic recall is soft, ephemeral, and budget-aware:

- default dynamic recall cap: 1000 chars inside the autonomy first-call budget;
- acceptable implementation range: 800-1200 chars if tests and logs justify exact split;
- cap applies across system-message recall blocks and current-user `<memory-context>` injection;
- trim deterministically with a marker such as `[trimmed dynamic recall: kept N of M chars; use session_search for more]`;
- if more history is needed, the NPC should call `session_search`.

`recall_block` is no longer a valid final miss reason for new budget decisions. It may remain only as a migration/legacy diagnostic label in old logs or comments.

`<memory-context>` trimming rules:

- If one or more complete `<memory-context>...</memory-context>` blocks exist, trim only those blocks and preserve surrounding current-user decision text verbatim.
- If a block is malformed or missing a closing tag, treat the malformed recall segment from the opening tag to the end of that message as dynamic recall; preserve all text before the opening tag.
- If there is no recognizable delimiter, do not guess by keyword inside user text; classify the message by role and other explicit markers.
- Multiple dynamic recall surfaces share one cap. Prefer preserving the most recent/current-user recall before older system recall when distributing the cap.

### Built-In Memory/Profile

- Keep `MemoryManager` defaults aligned with reference: memory 2200 chars, user/profile 1375 chars.
- Do not add another memory store or NPC persona summary.
- If built-in memory/profile alone exceeds its expected budget or pushes protected content over 5K, log `builtin_memory_over_budget`.
- Do not solve built-in memory pressure inside the autonomy policy by writing or summarizing memory; memory cleanup remains the existing memory tool/lifecycle responsibility.

### Budget Miss Reasons

Use these final miss reasons:

- `core_system_over_budget`
- `builtin_memory_over_budget`
- `protected_tail_over_budget`
- `active_task_context_over_budget`
- `protected_content_over_budget`
- `unknown`

Final miss reason priority is deterministic after dynamic recall and unprotected history have been trimmed:

1. `core_system_over_budget` if `systemChars` alone exceeds the budget.
2. `builtin_memory_over_budget` if `systemChars + builtinMemoryChars` exceeds the budget and built-in memory/profile contributes to the miss.
3. `protected_tail_over_budget` if the latest structural continuation group pushes protected content over the budget.
4. `active_task_context_over_budget` if active task context pushes protected content over the budget.
5. `protected_content_over_budget` if protected categories together exceed the budget but no single category above explains it.
6. `unknown` only for an unexpected residual miss after all known categories are within expected bounds.

Mixed-overflow cases must use the first matching reason in this priority order.

These are final miss reasons only. Trim diagnostics such as `dynamic_recall_trimmed` and `old_tool_results_trimmed` may be logged alongside them, but must not replace the final `budgetUnmetReason` taxonomy.

Use these trim diagnostics without making them final miss reasons when the final budget is met:

- `dynamic_recall_trimmed`
- `old_tool_results_trimmed`
- `old_status_results_deduped`
- `assistant_tool_args_trimmed`

### Tool-Use Behavior

- Keep existing status-class budget: one broad status tool per autonomy turn, `stardew_task_status` remains a continuation exception.
- Skill guidance should continue to require one turn purpose before tool calls.
- Repeated broad status scans should be blocked by runtime budget and discouraged by skills.
- Historical uncertainty should route to `session_search`, not more preloaded recall.

## Implementation Tasks

- [ ] Revise `StardewAutonomyFirstCallContextBudgetPolicy` category classification so recall/memory-looking text is not automatically protected as `recall_block`.
- [ ] Detect dynamic recall in both system messages and current-user `<memory-context>` content.
- [ ] Add deterministic dynamic recall trimming with the default 1000-char cap.
- [ ] Preserve current user decision text while trimming only the appended dynamic recall segment where possible.
- [ ] Implement the category classification table exactly enough for tests to assert each category.
- [ ] Implement deterministic final miss reason precedence for mixed-overflow cases.
- [ ] Keep `ToolCallId` structural latest continuation protection unchanged.
- [ ] Replace final `recall_block` reason behavior with the new reason-code taxonomy.
- [ ] Add per-category structured logging fields.
- [ ] Keep built-in memory/profile bounded through existing `MemoryManager`; do not add a new summary/memory lane.
- [ ] Confirm status-class budget and Stardew skill guidance still match one-purpose/on-demand-search behavior.
- [ ] Add and update tests from the test spec.

## Acceptance Criteria

1. Autonomy marker true activates the policy; marker false/absent no-ops.
2. Large dynamic recall in system messages is trimmed and no longer hard-protected.
3. Large `<memory-context>` appended to the current user message is trimmed while preserving the current decision text.
4. When core protected content fits, first-call payload is `<= 5000` chars.
5. `budgetUnmetReason=recall_block` is not emitted by new budget decisions.
6. Built-in memory/profile are counted separately and remain bounded by existing memory settings.
7. Latest continuation group is protected structurally by `ToolCallId`.
8. Dynamic recall trimming is ephemeral and does not persist trimmed text or markers into transcripts.
9. `session_search` remains registered and available to NPC autonomy agents.
10. No new NPC recap/persona/memory summary lane exists.
11. Logs include all required category sizes and trim diagnostics.
12. Existing memory/session_search parity tests remain passing.
13. Malformed or repeated `<memory-context>` blocks cannot remove the live decision text.
14. Mixed-overflow budget misses follow the documented reason priority.

## Risks And Mitigations

- **Risk:** NPC misses important prior context on first call.
  - Mitigation: keep a 1000-char dynamic recall window, preserve curated memory/profile, and make `session_search` guidance/test coverage explicit.

- **Risk:** trimming current-user `<memory-context>` accidentally removes the live decision request.
  - Mitigation: parse/trim only the delimited recall segment; add a direct regression test.

- **Risk:** built-in memory and core system together exceed 5K.
  - Mitigation: do not silently trim protected core; log `core_system_over_budget` or `builtin_memory_over_budget` with category counts for follow-up.

- **Risk:** implementation only fixes system recall blocks and misses user-message recall injection.
  - Mitigation: test both `[Relevant Memories]` and `<memory-context>`.

## ADR

### Decision

Use a marker-gated, reference-aligned first-call policy where dynamic recall is capped soft context, not hard-protected content.

### Drivers

- Manual logs proved `recall_block` can defeat the 5K budget.
- Reference project keeps curated memory bounded/frozen and uses `session_search` for long-term transcript recall.
- This project already has memory, recall, session search, and compaction systems; duplicating them would violate project constraints.

### Alternatives Considered

- Hard-protect all recall/memory blocks: rejected because it caused the current failure.
- Remove all memory/recall from first calls: rejected because it loses useful compact context and diverges from reference memory behavior.
- Add NPC recap/persona summary: rejected because it duplicates existing systems and violates project constraints.
- Move the rule into global `ContextManager`: rejected because scope is too broad.

### Consequences

- Dynamic recall may be less visible in the first request.
- NPC autonomy relies more clearly on `session_search` for history beyond the cap.
- Logs become the source of truth for future budget tuning.

### Follow-Ups

- Review manual logs after implementation for category sizes and repeated `session_search` behavior.
- If 1000 chars is too low or high, adjust the cap through evidence, not speculation.
- If core system prompts exceed budget, write a separate prompt-size reduction plan.

## Available Agent Types

- `executor`: source implementation owner.
- `test-engineer`: regression and parity test owner.
- `architect`: boundary and reference-alignment review.
- `critic`: plan/diff quality review.
- `verifier`: final evidence collection.
- `explore`: targeted repo lookup.

## Staffing Guidance

### Ralph Path

Use one `executor` to implement sequentially:

1. dynamic recall classification/trimming;
2. reason-code and logging changes;
3. focused tests;
4. verification.

Suggested reasoning: executor high, verifier medium.

### Team Path

- Lane 1 `executor`: `StardewAutonomyFirstCallContextBudgetPolicy` and reason/logging changes.
- Lane 2 `test-engineer`: recall trim, no-persist, marker/no-op, continuation, and parity tests.
- Lane 3 `verifier`: run targeted tests and inspect logs.

Suggested reasoning: executor high, test-engineer medium, verifier medium.

## Launch Hints

Ralph:

```powershell
$ralph implement .omx/plans/prd-stardew-autonomy-reference-aligned-context-budget.md with .omx/plans/test-spec-stardew-autonomy-reference-aligned-context-budget.md
```

Team:

```powershell
$team implement Stardew autonomy reference-aligned context budget per .omx/plans/prd-stardew-autonomy-reference-aligned-context-budget.md and .omx/plans/test-spec-stardew-autonomy-reference-aligned-context-budget.md
```

## Verification Path

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextBudget"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~MemoryParity"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

Manual log checks after execution:

- `%LOCALAPPDATA%\hermes\hermes-cs\logs\hermes.log`
- NPC runtime `runtime.jsonl`

Expected log evidence:

- `charsAfter <= 5000` when protected content fits;
- `dynamicRecallCharsBefore > dynamicRecallCharsAfter` for large recall inputs;
- no new `budgetUnmetReason=recall_block`;
- category counts identify any remaining budget pressure.

## Change Log

- Incorporated Architect feedback: removed hard protection for recall/memory blocks.
- Added `<memory-context>` as an explicit dynamic recall surface.
- Replaced `recall_block` final miss reason with category-based reasons.
- Added no-persist and parity requirements.
- Incorporated Critic feedback: added category classification rules, miss reason priority, malformed `<memory-context>` handling, and mixed-overflow requirements.
- Incorporated second Architect feedback: tightened active task classification, tied dynamic recall parsing to existing fence/sanitizer semantics, and clarified final reason vs trim diagnostics.
