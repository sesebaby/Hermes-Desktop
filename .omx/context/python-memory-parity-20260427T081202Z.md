# Context Snapshot: Python Memory Parity

Task statement: Align Hermes Desktop C# memory behavior with the Python reference implementation in external/hermes-agent-main.

Desired outcome: The C# app should actually remember across sessions in the sense users expect: durable user/project facts, prior task/session recall, and prompt injection that is visible to the model.

Stated solution: Decide next step and whether to use ralplan before implementation.

Probable intent hypothesis: Avoid another narrow "memory fix" that only passes unit tests while failing manual cross-session recall. Establish an implementation-ready scope based on the Python reference.

Known facts/evidence:
- ReadmeCn.md claims cross-session context and compiled memory stack.
- Runtime memory directories were empty during manual test, while transcripts existed.
- PR #43 commits only aligned MemoryTool and MemoryManager storage/frontmatter/path safety.
- ContextManager supports RetrievedContext, but Agent/AgentLoopScaffold pass retrievedContext: null.
- SessionSearchIndex exists but is not registered or called by TranscriptStore.
- SessionSearchTool scans JSONL only when the model voluntarily calls the tool.
- Python reference uses SessionDB SQLite FTS5, MemoryStore MEMORY.md/USER.md, MemoryManager provider prefetch/sync hooks, and fenced ephemeral memory context injection.

Constraints:
- Do not implement directly inside deep-interview.
- Preserve current C# app unless explicitly choosing a rewrite.
- Use Python reference as parity anchor, not just inspiration.
- No destructive git operations.

Unknowns/open questions:
- Should first parity milestone target exact Python semantics or user-visible behavior equivalence?
- How much external memory provider/plugin parity is in scope for first pass?
- Should automatic transcript recall be always-on or gated by explicit memory settings?
- How strict should UI/explainability be in the first pass?

Decision-boundary unknowns:
- Whether OMX may choose architecture details if behavior matches Python.
- Whether SQLite FTS5 SessionDB equivalent may replace/augment JSONL transcript scanning.
- Whether new dependencies are allowed if current Microsoft.Data.Sqlite is insufficient.

Likely codebase touchpoints:
- src/Core/Agent.cs
- src/Core/AgentLoopScaffold.cs
- src/Context/ContextManager.cs
- src/Context/PromptBuilder.cs
- src/transcript/TranscriptStore.cs
- src/search/SessionSearchIndex.cs
- src/Tools/SessionSearchTool.cs
- src/memory/MemoryManager.cs
- src/plugins/BuiltinMemoryPlugin.cs
- Desktop/HermesDesktop/App.xaml.cs

Reference touchpoints:
- external/hermes-agent-main/run_agent.py
- external/hermes-agent-main/hermes_state.py
- external/hermes-agent-main/agent/memory_manager.py
- external/hermes-agent-main/agent/memory_provider.py
- external/hermes-agent-main/tools/memory_tool.py
- external/hermes-agent-main/tools/session_search_tool.py
