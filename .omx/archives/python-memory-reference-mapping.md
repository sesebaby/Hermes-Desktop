# Python Memory Reference Mapping

## Scope

This contract covers the C# implementation of the Python `external/hermes-agent-main`
memory lifecycle and `session_search` behavior.

It does not cover provider-specific integrations such as Honcho, Hindsight, or Mem0.
Those remain future providers behind the same lifecycle interface.

## Reference Anchors

- `external/hermes-agent-main/agent/memory_provider.py`
- `external/hermes-agent-main/agent/memory_manager.py`
- `external/hermes-agent-main/run_agent.py`
- `external/hermes-agent-main/tools/memory_tool.py`
- `external/hermes-agent-main/agent/prompt_builder.py`
- `external/hermes-agent-main/tools/session_search_tool.py`
- `external/hermes-agent-main/hermes_state.py`

## Reference Chain

1. `Trigger`: a user turn begins in `run_agent.py`.
2. `Snapshot`: providers receive `on_turn_start` with turn number and clean user message.
3. `Prompt / Summary Assembly`: `prefetch_all` collects provider recall once before the tool loop.
4. `Parse / Repair / Normalize`: prefetched context is sanitized and fenced by `build_memory_context_block`.
5. `Projector / Executor`: fenced recall is appended to the current API user message only.
6. `Authoritative Writeback`: after a completed non-interrupted turn, `sync_all` then `queue_prefetch_all` run.
7. `Player-visible Surface`: `session_search` either lists recent sessions or returns session-level summaries.

## Current C# Mapping

| Ability | Reference Anchor | Reference Behavior | C# Landing Point | Status | Allowed Deviation | No-Drift Check | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| built-in curated memory tool | `tools/memory_tool.py` | one `memory` tool with `add` / `replace` / `remove`, `target: memory|user`, `old_text` unique-substring edits | `src/Tools/MemoryTool.cs`, `src/memory/MemoryManager.cs` | aligned | none | no `save/list/delete`; no timestamped `memory_*.md` files | `MemoryToolTests` Python contract tests |
| built-in curated memory storage | `tools/memory_tool.py:105-446` | fixed `MEMORY.md` and `USER.md` under `HERMES_HOME/memories`, entries delimited by `\n§\n` | `src/memory/MemoryManager.cs`, `Desktop/HermesDesktop/App.xaml.cs` | aligned | non-destructive import from old `hermes-cs/memory` | fixed files remain canonical; legacy files are not copied forward as authoritative files | `MemoryToolTests`, solution build |
| memory safety semantics | `tools/memory_tool.py:51-446` | char budgets, duplicate suppression, prompt-injection/exfil scanning, file lock, atomic replace | `src/memory/MemoryManager.cs` | aligned | none | failed writes must not mutate files | `MemoryToolTests` limit/security/isolation tests |
| frozen memory prompt snapshot | `tools/memory_tool.py:121`, `tools/memory_tool.py:359-369`, `run_agent.py:4557-4564` | built-in memory is loaded at session start; mid-session writes update disk but do not change ordinary-turn prompt snapshots | `src/plugins/BuiltinMemoryPlugin.cs`, `src/Context/PromptBuilder.cs`, `src/Context/ContextManager.cs` | aligned | C# plugin lifecycle uses `turnNumber == 0` as session-start signal | optimized desktop prompt path must include the frozen plugin memory block | `BuiltinMemoryPlugin_UsesFrozenSnapshotUntilNewSession`, `Agent_OptimizedContextIncludesBuiltinMemoryPluginSnapshot` |
| built-in prompt block boundary | `run_agent.py:4555-4564`, `agent/memory_manager.py:66-80` | built-in `MEMORY.md` / `USER.md` snapshots are appended directly to the system prompt; only prefetched recall is wrapped in `<memory-context>` | `src/plugins/PluginManager.cs`, `src/search/TurnMemoryCoordinator.cs` | aligned | none | built-in memory system blocks must not be recall-fenced | `BuiltinMemoryPlugin_SystemPromptBlock_IsNotRecallFenced`, `TurnMemoryCoordinator_BuildMemoryContextBlock_UsesPythonFenceShape` |
| compression memory snapshot refresh | `run_agent.py:4847-4852` | after context compression invalidates/rebuilds the prompt, built-in memory reloads from disk so current-session writes can enter the rebuilt prompt immediately | `src/plugins/BuiltinMemoryPlugin.cs`, `src/Context/ContextManager.cs`, `src/Context/PromptBuilder.cs`, `src/Core/Agent.cs` | aligned | C# maps the refresh to `OnPreCompressAsync` because the plugin surface already models pre-compression lifecycle hooks | ordinary turns keep the frozen snapshot; compression refreshes it for the same outbound turn and Chat/Stream tool-loop continuations; empty refreshed snapshots remove stale transient blocks | `BuiltinMemoryPlugin_OnPreCompress_RefreshesFrozenSnapshot`, `ContextManager_PrepareContext_CallsPluginPreCompressBeforeSummarizingEvictedMessages`, `Agent_CompressionTurnUsesRefreshedBuiltinMemorySnapshot`, `StreamChatAsync_CompressionToolContinuationUsesRefreshedBuiltinMemorySnapshot`, `Agent_CompressionToolContinuationRemovesStaleBuiltinMemoryWhenSnapshotBecomesEmpty` |
| model-facing memory tool schema | `tools/memory_tool.py:541-560` | public schema requires `action` and `target`, with enums `add/replace/remove` and `memory/user` | `src/Core/Models.cs`, `src/Core/Agent.cs`, `src/Tools/MemoryTool.cs` | aligned | C# exposes this through a custom schema provider rather than reflection | the schema shown to the model must match the Python contract, not only runtime validation | `MemoryToolSchema_RequiresActionAndTargetAndUsesPythonEnums` |
| memory write bridge | `run_agent.py:8303-8312`, `run_agent.py:8818-8828`, `agent/memory_manager.py:342-369` | successful built-in `add` / `replace` writes notify external memory providers; `remove` is not bridged | `src/Tools/MemoryTool.cs`, `src/plugins/PluginManager.cs` | aligned | C# maps memory providers to `IPlugin` category `memory` instead of a separate `MemoryProvider` ABC | add/replace notify; remove does not notify | `MemoryTool_NotifiesPluginManagerOnWrites`, `MemoryTool_NotifiesPluginManagerOnReplace`, `MemoryTool_DoesNotNotifyPluginManagerOnRemove` |
| memory enablement | `run_agent.py:1603-1613`, `tools/memory_tool.py:475-476` | built-in memory is unavailable unless `memory_enabled` or `user_profile_enabled` is explicitly true | `Desktop/HermesDesktop/App.xaml.cs`, `Desktop/HermesDesktop/Views/SettingsPage.xaml.cs`, `src/Tools/MemoryTool.cs` | aligned | C# still constructs an inert `MemoryManager` for DI simplicity, but tool/plugin/review behavior stays disabled | missing config must not inject memory or allow writes | `Execute_WhenMemoryDisabled_FailsWithUnavailableError`, solution build |
| periodic memory review/nudge | `run_agent.py:1598-1601`, `run_agent.py:9364-9380`, `run_agent.py:12598-12608` | after every configured `memory.nudge_interval`, spawn a non-blocking background review only after a completed, non-interrupted turn | `src/memory/MemoryReviewService.cs`, `src/Core/Agent.cs`, `Desktop/HermesDesktop/App.xaml.cs` | aligned with controlled adaptation | lightweight C# `IChatClient.CompleteWithToolsAsync` review pass instead of full AIAgent fork | review must be post-response, non-blocking, use the same `memory` tool/write bridge, and skip interrupted/partial turns | `MemoryReviewServiceTests`, `StreamChatAsync_ConsumerStopsMidStream_DoesNotRunMemoryReview`, full test run |
| provider lifecycle | `agent/memory_provider.py`, `agent/memory_manager.py` | provider hooks: turn start, prefetch, sync, queue | `src/memory/IMemoryProvider.cs`, `src/memory/HermesMemoryOrchestrator.cs` | aligned | none | failures must be per-provider non-fatal | `MemoryParityTests` lifecycle tests |
| turn-start prefetch | `run_agent.py:9565-9585` | call `on_turn_start`, then `prefetch_all` once | `src/search/TurnMemoryCoordinator.cs` | aligned | none | no repeated prefetch per tool iteration | `MemoryParityTests` first-call tests |
| API-call injection | `run_agent.py:9727-9737` | append fenced context to current user API message only | `src/search/TurnMemoryCoordinator.cs` | aligned | none | injected context must not persist to transcript | `MemoryParityTests` injection persistence tests |
| completed-turn sync | `run_agent.py:4280-4320`, `run_agent.py:12589-12594` | skip interrupted/empty turns; otherwise sync then queue | `src/Core/Agent.cs`, `src/search/TurnMemoryCoordinator.cs` | aligned | none | sync must not run for empty output | `MemoryParityTests` sync/empty-response tests |
| context sanitation | `agent/memory_manager.py:66-81` | strip prior injected context before reinjection | `src/search/TranscriptRecallService.cs` | aligned | none | no nested `<memory-context>` / `<memory_context>` recall | `MemoryParityTests` sanitation test |
| recent sessions | `tools/session_search_tool.py:266-316` | empty query lists recent sessions | `src/search/TranscriptRecallService.cs`, `src/Tools/SessionSearchTool.cs` | aligned | JSONL metadata instead of SQLite session table | must return session metadata/previews, not failure | `MemoryParityTests` recent-mode test |
| session summaries | `tools/session_search_tool.py:196-257`, `tools/session_search_tool.py:407-510` | group hits by session and summarize each session | `src/search/TranscriptRecallService.cs`, `src/Tools/SessionSearchTool.cs` | aligned | deterministic preview fallback when no auxiliary model is available | output must be session-level summary entries | `MemoryParityTests` summary-mode test |

## Approved Deviations

| Ability | Layer | Reference Behavior | C# Behavior | Category | Approval Basis |
| --- | --- | --- | --- | --- | --- |
| transcript authority | Authoritative Writeback | SQLite `SessionDB` is the authoritative session store | existing C# JSONL `TranscriptStore` remains authoritative; SQLite FTS is derived/rebuildable | title-local adaptation | accepted in `.omx/plans/autopilot-spec-python-memory-parity.md` and current user request to execute that plan |
| auxiliary summarizer | Prompt / Summary Assembly | Python calls configured auxiliary model with bounded concurrency | C# uses optional `IChatClient` when wired and deterministic raw-preview fallback otherwise | title-local adaptation | avoids adding a new model configuration dependency in this pass |
| background memory review executor | Projector / Executor | Python forks a full `AIAgent` with inherited runtime and tools | C# runs a detached `IChatClient.CompleteWithToolsAsync` pass exposing only the `memory` tool | title-local adaptation | C# does not yet have a safe full-agent fork surface for this lifecycle; the adaptation preserves the user-visible memory behavior and avoids main-history mutation |
