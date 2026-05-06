# Reference Mapping: Hermes Agent Memory Parity

Reference project: `D:\Projects\Hermes-Desktop\external\hermes-agent-main`
Repo-relative path: `external/hermes-agent-main`

## Reference Chain

| Layer | Python reference | C# mapping in this execution |
| --- | --- | --- |
| Trigger | `run_agent.py` handles each user turn before the tool loop | `Agent.ChatAsync` and `Agent.StreamChatAsync` prepare the first outbound call through `TurnMemoryCoordinator` |
| Stable snapshot | `tools/memory_tool.py` loads `MEMORY.md` / `USER.md` for system prompt injection | Existing `MemoryManager` / `BuiltinMemoryPlugin` remains the stable file-memory lane |
| Prefetch/search | `agent/memory_manager.py::prefetch_all` and `hermes_state.py::search_messages` collect prior context | `TranscriptRecallService.RecallAsync` searches JSONL-authoritative transcripts and can backfill `SessionSearchIndex` |
| Prompt assembly | `run_agent.py` keeps external recall out of stable system layers | `PromptBuilder` no longer emits `RetrievedContext` as a system message |
| API-call injection | `run_agent.py` injects fenced recalled context into the current user message immediately before the API call | `TurnMemoryCoordinator` augments only the outbound current user `Message` with a Python-style system note and `<memory_context>` fence |
| Authoritative writeback | Python persists the clean user turn, not the injected recall block | `AgentSessionWriter.AppendUserMessageAsync` persists the original user text before coordination; tests assert recall is not written to JSONL |
| Manual search surface | `tools/session_search_tool.py` searches the same transcript memory domain | `SessionSearchTool` delegates to `TranscriptRecallService` instead of scanning a separate corpus |

## Deviations

- `equivalent mapping`: C# uses JSONL transcript scanning as the authoritative recall source and SQLite FTS as a derived acceleration layer. This matches the approved plan even though Python's storage primitive is SQLite-first in `hermes_state.py`.
- `title-local adaptation`: The C# coordinator is intentionally thin and does not recreate the full Python multi-provider `MemoryManager` in this milestone.
