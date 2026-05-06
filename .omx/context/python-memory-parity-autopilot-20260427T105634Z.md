# Autopilot Context: Python Memory Parity

## Task Statement
Replicate the Python `external/hermes-agent-main` memory behavior in the C# Hermes Desktop codebase, beyond simple transcript storage/search.

## Desired Outcome
C# memory should follow the Python memory lifecycle: stable memory snapshots, provider lifecycle, turn-start notification, prefetch once before first API call, API-call-time current-user injection, completed-turn sync, queued next-turn prefetch, shared session_search corpus, session-level summaries, and injected-context sanitation.

## Known Facts / Evidence
- Previous Ralph pass implemented first-call transcript recall via `TurnMemoryCoordinator` and `TranscriptRecallService`.
- Current implementation still lacks Python-style `MemoryManager` provider lifecycle, `sync_all`, `queue_prefetch_all`, session summary search, recent-session browse mode, and sanitation of old injected context blocks.
- Reference files: `external/hermes-agent-main/agent/memory_manager.py`, `run_agent.py`, `tools/session_search_tool.py`, `hermes_state.py`, `tools/memory_tool.py`.

## Constraints
- Preserve JSONL as C# authoritative transcript store unless a deliberate migration is planned.
- SQLite FTS remains derived/rebuildable.
- No new package dependency.
- TDD first for behavior changes.
- Keep existing tests green.
- Do not revert unrelated dirty files.

## Likely Touchpoints
- `src/search/TranscriptRecallService.cs`
- `src/search/TurnMemoryCoordinator.cs`
- new memory lifecycle files under `src/memory/` or `src/search/`
- `src/Core/Agent.cs`
- `src/Tools/SessionSearchTool.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
