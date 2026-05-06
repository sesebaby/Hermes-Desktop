# Ralph Context Snapshot: Memory Importance Prompt Session Search Parity

## Task Statement
Align C# Hermes Desktop memory organization/consolidation, important memory behavior, system prompt guidance, and session_search behavior with the Python reference project at external/hermes-agent-main.

## Desired Outcome
C# behavior should match the Python reference where practical: durable curated memory remains MEMORY.md/USER.md, important memory is selected with the same prompt criteria, system prompt includes Python-style memory/session_search guidance, session_search tool description/output should be source-faithful enough for model behavior, and automatic organization/consolidation should not diverge from the reference design.

## Known Facts / Evidence
- Existing C# has MEMORY.md/USER.md via MemoryManager and MemoryTool.
- Existing C# has background MemoryReviewService every memory.nudge_interval turns.
- Existing C# has SQLite state.db + FTS5 transcript recall and session_search.
- Existing C# currently auto-injects transcript recall via TurnMemoryCoordinator, which differs from Python's explicit session_search guidance/tool usage model.
- AutoDreamService exists but is not registered in Desktop startup; DreamerService is unrelated background ideation.

## Constraints
- Reference-first: preserve Python behavior before local optimization.
- Do not reintroduce JSONL session storage.
- Do not implement external memory providers unless explicitly requested.
- Must use TDD and verify with tests/build.
- Preserve unrelated dirty .omx runtime files.

## Unknowns / Open Questions
- Whether AutoDreamService should be removed/disabled, wired, or adapted. Need inspect reference: Python built-in memory organization appears limited to tool writes + background review, not arbitrary generated md files.
- Exact session_search output parity required: JSON vs formatted text impacts tool consumer behavior.

## Likely Codebase Touchpoints
- src/Core/SystemPrompts.cs
- src/Tools/SessionSearchTool.cs
- src/Tools/MemoryTool.cs
- src/memory/MemoryReviewService.cs
- src/search/TurnMemoryCoordinator.cs
- src/search/TranscriptRecallService.cs
- Desktop/HermesDesktop/App.xaml.cs
- Desktop/HermesDesktop.Tests/Services/*
