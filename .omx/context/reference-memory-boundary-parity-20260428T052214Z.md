# Reference Memory Boundary Parity

## Task Statement
Align Hermes-Desktop's memory and past-conversation recall architecture with the mature reference implementation in `external/hermes-agent-main`, without adding narrow fixes for a single prompt such as "oldest conversation" or "when did we meet".

## Desired Outcome
- Preserve the reference distinction between durable curated memory (`MEMORY.md` / `USER.md`) and searchable transcript recall (`session_search`).
- Remove or redesign repo-local behavior that automatically injects transcript recall as a memory-provider prefetch into every turn if it is not reference-aligned.
- Keep `session_search` as the agent-facing recall tool for cross-session questions.
- Produce an implementation plan with tests before any code changes.

## Known Facts / Evidence
- Reference `agent/prompt_builder.py` says task progress and session outcomes should not be saved to memory; use `session_search` for past transcripts.
- Reference `tools/session_search_tool.py` exposes two modes only: omitted query lists recent sessions; query performs FTS5 search and summarization.
- Reference `run_agent.py` calls memory-manager `prefetch_all()` for external memory providers before the tool loop, and separately routes `session_search` as a tool call with `current_session_id`.
- Reference `hermes_state.py` stores sessions/messages in SQLite with FTS5 and lineage metadata; `list_sessions_rich()` hides child sessions and projects compression chains to logical conversations.
- Current `Desktop/HermesDesktop/App.xaml.cs` registers `TranscriptMemoryProvider` as `IMemoryProvider`.
- Current `src/search/TurnMemoryCoordinator.cs` calls `PrefetchAllAsync(userMessage)` and injects recalled context into the current user message as `<memory-context>`.
- Current `src/search/TranscriptMemoryProvider.cs` adapts transcript recall into `IMemoryProvider`.
- Current `src/Tools/SessionSearchTool.cs` already maps to reference-style `session_search`.

## Constraints
- Do not implement special handling for "oldest conversation" / "when did we meet".
- Do not modify code until the ralplan consensus plan is approved.
- Preserve user preference: Chinese communication.
- Preserve repo constraints: small diffs, regression tests first, no new dependencies.
- Reference-first behavior: deviations from `external/hermes-agent-main` must be explicit.

## Unknowns / Open Questions
- Whether any external memory provider support in C# currently depends on `HermesMemoryOrchestrator`; if not, it may be premature abstraction.
- Whether `TurnMemoryCoordinator` should remain for non-transcript external providers or be narrowed to exclude transcript recall.
- Which existing tests should be rewritten versus removed to stop encoding a false Python parity claim.

## Likely Touchpoints
- `Desktop/HermesDesktop/App.xaml.cs`
- `src/search/TranscriptMemoryProvider.cs`
- `src/search/TurnMemoryCoordinator.cs`
- `src/memory/HermesMemoryOrchestrator.cs`
- `src/Core/AgentLoopScaffold.cs`
- `src/Tools/SessionSearchTool.cs`
- `src/Core/MemoryReferenceText.cs`
- `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
