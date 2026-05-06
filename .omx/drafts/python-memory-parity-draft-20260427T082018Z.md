# Draft Evidence Matrix: Python Memory Parity

## C# Current Evidence

| Claim | Evidence |
|---|---|
| PromptBuilder can carry retrieved context | src/Context/PromptBuilder.cs:55, :103-109, :153, :169 |
| ContextManager accounts for retrievedContext | src/Context/ContextManager.cs:106, :167, :274 |
| Agent currently passes retrievedContext null | src/Core/Agent.cs:692-693; src/Core/AgentLoopScaffold.cs:89-93 |
| Plugin memory blocks may be bypassed by preparedContext | src/Core/Agent.cs:203-208 inserts plugin blocks; src/Core/Agent.cs:280-289 first tool call uses preparedContext |
| SessionSearchIndex exists but is not wired | src/search/SessionSearchIndex.cs:18, :65; no DI registration found |
| TranscriptStore persists JSONL but has no search-index hook | src/transcript/TranscriptStore.cs:33 |
| SessionSearchTool is model-invoked only and JSONL-scan based | src/Tools/SessionSearchTool.cs:10-18, :37-70 |
| MemoryTool and SessionSearchTool are registered in app | Desktop/HermesDesktop/App.xaml.cs:739, :744 |
| BuiltinMemoryPlugin registered unconditionally | Desktop/HermesDesktop/App.xaml.cs:468 |

## Python Reference Evidence

| Reference behavior | Evidence |
|---|---|
| SQLite session database owns searchable history | external/hermes-agent-main/hermes_state.py:123 |
| Message writeback appends to DB | external/hermes-agent-main/hermes_state.py:966 |
| FTS5 message search powers recall | external/hermes-agent-main/hermes_state.py:1309 |
| session_search groups/summarizes sessions | external/hermes-agent-main/tools/session_search_tool.py:319 |
| memory context is fenced | external/hermes-agent-main/agent/memory_manager.py:66 |
| providers expose system prompt/prefetch/sync | external/hermes-agent-main/agent/memory_manager.py:158, :179, :198, :211 |
| run_agent syncs and queues prefetch after completed turns | external/hermes-agent-main/run_agent.py:4318-4319 |
| run_agent builds system prompt with memory/user blocks | external/hermes-agent-main/run_agent.py:4472, :4555-4569 |
| run_agent injects external prefetch into current user message | external/hermes-agent-main/run_agent.py:9566-9584, :9726-9729 |

## Planning Bias

Prefer current-project repair. The project already has most receiver structures, but recall/writeback/prompt authority boundaries are incomplete.
