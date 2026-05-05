# Context Snapshot: Stardew Autonomy Reference-Aligned Context Budget

Date: 2026-05-05
Task: `$ralplan 对齐参考项目`
Mode: planning only

## Desired Outcome

Revise the Stardew NPC autonomy first-call context budget plan so it aligns with `external/hermes-agent-main`:

- default autonomy first-call payload should stay at or below 5,000 chars when core protected content fits;
- do not create another NPC recap, persona summary, memory summary, or second recall lane;
- reuse existing memory, transcript recall, `session_search`, turn memory, and context compression systems;
- make agents search relevant history on demand instead of stuffing long recall blocks into the same turn.

## Observed Failure

Manual logs showed the existing implementation did not meet the budget:

- `budgetMet=False`
- `budgetUnmetReason=recall_block`
- first-call payload remained around 31K-40K chars

The current policy treats recall-looking system messages as protected content. That makes `Relevant Memories`, `USER PROFILE`, `session_search`, and `recall` blocks able to keep the request above the 5K target.

## Current Project Evidence

- `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs:179` marks recall/memory-looking messages as protected `recall_block`.
- `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs:217` returns `recall_block` when protected recall drives the miss.
- `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs:234` matches `Relevant Memories`, `USER PROFILE`, `session_search`, and `recall`.
- `.omx/plans/prd-stardew-npc-autonomy-context-budget.md:87` explicitly protected existing recall or memory blocks, which caused the observed miss.
- `src/Core/Agent.cs:434` is the first-call policy invocation point before the first tool-capable request.
- `src/search/TurnMemoryCoordinator.cs:50` prefetches memory context and `src/search/TurnMemoryCoordinator.cs:111` appends `<memory-context>` to the current user message.
- `src/plugins/BuiltinMemoryPlugin.cs:8` and `src/plugins/BuiltinMemoryPlugin.cs:36` implement frozen built-in memory snapshots.
- `src/memory/MemoryManager.cs:47` already has bounded memory/profile limits: 2200 and 1375 chars.
- `src/search/TranscriptRecallService.cs:48` has `RecallAsync(maxChars=4000)`.
- `src/search/TranscriptRecallService.cs:277` clamps session summary search to 1..5 sessions.
- `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs` already covers memory/session_search parity, including no synthetic system recall layer and `session_search` summaries.
- `skills/gaming/stardew-core.md`, `stardew-navigation.md`, and `stardew-social.md` already instruct one-turn purpose, on-demand `session_search`, durable-only `memory`, and no repeated broad status scans.

## Reference Project Evidence

- `external/hermes-agent-main/tools/memory_tool.py:11` freezes MEMORY/USER snapshots at session start.
- `external/hermes-agent-main/tools/memory_tool.py:116` defaults to `memory_char_limit=2200`, `user_char_limit=1375`.
- `external/hermes-agent-main/tools/session_search_tool.py:3` defines session search as long-term conversation recall.
- `external/hermes-agent-main/tools/session_search_tool.py:5` uses FTS5 plus summaries.
- `external/hermes-agent-main/tools/session_search_tool.py:567` documents default 3, max 5 sessions.
- `external/hermes-agent-main/agent/prompt_builder.py:153` says task progress/session outcomes belong in `session_search`, not memory.
- `external/hermes-agent-main/agent/prompt_builder.py:480` caps context files at 20,000 chars with head/tail truncation.
- `external/hermes-agent-main/agent/context_compressor.py:1` handles long conversation compression separately from first-call budget.

## Constraints

- Do not implement source changes in this `$ralplan` turn.
- Do not duplicate existing memory, summary, compaction, or `session_search` systems.
- Do not maintain `SOUL.md`, `MEMORY.md`, `USER.md`, or equivalents for NPC identity/persona.
- Keep ordinary desktop chat, private NPC chat, and non-autonomy sessions unchanged unless explicitly scoped later.
- Use marker-gated behavior for Stardew autonomy only.

## Unknowns

- Exact real-world category sizes after repair: core system, built-in memory/profile, dynamic recall, active task, continuation tail.
- Whether 1,000 chars is the right dynamic recall cap long term; logs should decide later.

## Likely Touchpoints For Execution

- `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs`
- `src/Core/FirstCallContextBudget.cs`
- `src/search/TurnMemoryCoordinator.cs`
- `src/Core/Agent.cs`
- `src/memory/MemoryManager.cs`
- `src/plugins/BuiltinMemoryPlugin.cs`
- `src/search/TranscriptRecallService.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`
- `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
- `skills/gaming/stardew-core.md`
- `skills/gaming/stardew-navigation.md`
- `skills/gaming/stardew-social.md`
