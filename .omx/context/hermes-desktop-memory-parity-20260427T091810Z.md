# Hermes Desktop Memory Parity Context Snapshot

## Task Statement
Execute the approved Ralph/Ralplan implementation plan for C# Hermes Desktop memory parity with the Python reference implementation.

## Desired Outcome
C# Hermes Desktop automatically recalls relevant prior-session transcript context on the first outbound model call in a new/relevant session, matching Python external/hermes-agent-main semantics: stable curated memory stays in stable prompt layers; transcript recall is ephemeral current-user augmentation at API-call time; injected recall is not persisted as user-authored transcript content.

## Reference Project
- Absolute path: D:\Projects\Hermes-Desktop\external\hermes-agent-main
- Repo-relative path: external/hermes-agent-main
- Key reference files from plan: run_agent.py, hermes_state.py, tools/session_search_tool.py, tools/memory_tool.py, agent/memory_manager.py

## Known Facts / Evidence
- Approved plan: .omx\plans\hermes-desktop-memory-parity-initial-plan.md
- Approved PRD: .omx\plans\prd-hermes-desktop-memory-parity.md
- Approved test spec: .omx\plans\test-spec-hermes-desktop-memory-parity.md
- Current C# has context plumbing but passes retrievedContext: null at key first-call entry points.
- Current SessionSearchIndex exists but is not fully wired into transcript writes/app DI.
- Current SessionSearchTool is model-initiated/passive and scans JSONL separately.
- Python reference uses persistent stable memory plus ephemeral session recall injected into the current user message.

## Constraints
- Do not rewrite from scratch.
- Keep transcript JSONL authoritative; SQLite FTS is derived/rebuildable.
- No new dependency unless unavoidable and explicitly justified.
- Use TDD: write failing tests before production implementation.
- Keep diffs scoped and reversible.
- Do not modify unrelated dirty files.

## Unknowns / Open Questions
- Exact constructor/DI shape for current Agent, transcript store, and test harness must be confirmed from code.
- Whether SQLite provider package is already available must be confirmed before using SessionSearchIndex in production paths.
- Exact streaming first-call path must be mapped in current Agent.cs.

## Likely Codebase Touchpoints
- src/Core/Agent.cs
- src/Core/AgentLoopScaffold.cs
- src/Context/ContextManager.cs
- src/Context/PromptBuilder.cs
- src/transcript/TranscriptStore.cs
- src/search/SessionSearchIndex.cs
- src/Tools/SessionSearchTool.cs
- Desktop/HermesDesktop/App.xaml.cs
- Desktop/HermesDesktop.Tests/*
