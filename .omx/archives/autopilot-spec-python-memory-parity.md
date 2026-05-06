# Autopilot Spec: Python Memory Parity

## Goal
Replicate the Python `external/hermes-agent-main` memory behavior in C# Hermes Desktop beyond simple storage/search.

## Reference Contract
- `agent/memory_manager.py`: provider lifecycle, `prefetch_all`, `sync_all`, `queue_prefetch_all`, tool routing, failure isolation.
- `run_agent.py`: turn-start notification, prefetch once before tool loop, API-call-time current-user injection, post-turn sync/queue, interrupted-turn skip.
- `tools/session_search_tool.py`: recent-session browse mode, FTS search, session grouping, focused LLM summaries, bounded concurrency/fallback.
- `hermes_state.py`: SQLite FTS search semantics and session metadata.
- `tools/memory_tool.py`: stable memory snapshot remains distinct from ephemeral recall.

## C# Target Behavior
1. Add a memory lifecycle orchestrator with provider calls: `OnTurnStart`, `PrefetchAll`, `SyncAll`, `QueuePrefetchAll`.
2. Wrap transcript recall as a provider so automatic recall is not a one-off helper.
3. Keep current API-call-time user-message injection and non-persistence semantics.
4. Sanitize old injected memory/context blocks before they can be recalled or summarized.
5. Expand `session_search` into Python-like two-mode behavior:
   - Empty query: recent sessions metadata/previews.
   - Query: session-grouped focused summaries, using LLM summarization when available and excerpt fallback when unavailable.
6. Preserve JSONL as authoritative C# transcript store; SQLite FTS remains derived/rebuildable.
7. Skip sync/queue on interrupted or empty-response turns.

## Acceptance Criteria
- First-call automatic recall still works for no-tools, tools, and streaming paths.
- Memory provider lifecycle methods are called in the right order and failures are non-fatal.
- Completed turns trigger sync + queued prefetch; cancelled/interrupted turns do not sync partial output.
- `session_search` with no query lists recent sessions.
- `session_search` with a query returns session-level summaries, not only raw snippets.
- Injected `<memory-context>` / `<memory_context>` blocks are stripped from recalled context.
- Existing 540 tests remain green and solution builds.
