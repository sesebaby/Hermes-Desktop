# Ralph Context Snapshot: C# Hermes Desktop Python Memory Full Parity

Timestamp UTC: 20260427T125424Z

## Task Statement
User invoked `$ralph` to align C# Hermes Desktop memory with `D:\Projects\Hermes-Desktop\external\hermes-agent-main`, explicitly saying this is not only first-stage transcript recall.

## Desired Outcome
Implement reference-faithful memory behavior across the built-in curated memory tool, stable memory prompt injection, transcript/session recall, background review/nudge, and provider lifecycle bridge where feasible in the current C# architecture.

## Known Facts / Evidence
- Existing PRD/test spec exist: `.omx/plans/prd-hermes-desktop-memory-parity.md`, `.omx/plans/test-spec-hermes-desktop-memory-parity.md`.
- Prior implementation aligned transcript recall/session_search but did not align `memory` tool semantics.
- Python reference built-in memory uses fixed `MEMORY.md` and `USER.md` files under profile `memories/`, entries separated by `\n§\n`, actions `add/replace/remove`, `target: memory|user`, char budgets, duplicate suppression, injection/exfil scan, file locks and atomic replace.
- C# current `MemoryTool` uses `save/list/delete`, creates many timestamped `.md` files with YAML frontmatter, and `MemoryManager` scans those files per query.
- Python reference injects built-in memory as a frozen session-start system prompt snapshot; mid-session writes persist to disk but do not mutate the current session prompt.
- Python reference also has background memory review/nudge and `on_memory_write` bridge to external providers.

## Constraints
- Preserve prior dirty user/runtime files; do not revert unrelated changes.
- Keep JSONL transcript store authoritative for transcript recall; SQLite FTS remains derived, per approved deviation.
- No new dependencies unless unavoidable.
- Test-first for behavior changes.
- Build and test must be freshly verified before completion.

## Unknowns / Open Questions
- Exact desktop UI representation for `MEMORY.md`/`USER.md` may need adaptation; likely show fixed memory files distinctly from transcript recall.
- External memory provider ecosystem can be scaffolded but not fully ported without provider SDK/API decisions.

## Likely Codebase Touchpoints
- `src/Tools/MemoryTool.cs`
- `src/memory/MemoryManager.cs`
- `src/plugins/BuiltinMemoryPlugin.cs`
- `src/plugins/PluginManager.cs`
- `src/Core/Agent.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop.Tests/Services/MemoryToolTests.cs`
- `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
- `Desktop/HermesDesktop/Views/MemoryPage.xaml.cs`
- `Desktop/HermesDesktop/Views/Panels/MemoryPanel.xaml.cs`
