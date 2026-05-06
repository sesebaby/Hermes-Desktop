# Ralph Context Snapshot: C# Hermes Desktop Non-External Memory Parity

## Task statement
Align the C# Hermes Desktop memory behavior with `external/hermes-agent-main` for all previously compared memory functions except external memory provider backends.

## Desired outcome
Built-in curated memory, session storage/recall, prompt snapshot semantics, background memory review, and visible USER/MEMORY file behavior match the Python reference as closely as practical. External provider backends such as Honcho/Mem0/Supermemory/Hindsight are explicitly out of scope.

## Known facts / evidence
- C# session history and activities persist to `%LOCALAPPDATA%/hermes/hermes-cs/state.db` through `TranscriptStore`/`SessionSearchIndex`.
- C# built-in curated memory writes `%LOCALAPPDATA%/hermes/memories/MEMORY.md` and `USER.md` only through `MemoryTool`/`MemoryManager` or `MemoryReviewService`.
- Python built-in memory uses `~/.hermes/memories/MEMORY.md` and `USER.md`, frozen into the system prompt snapshot at session start; mid-session writes persist to disk but do not alter the active prompt snapshot.
- C# Desktop UI and Soul paths still write/read root `%LOCALAPPDATA%/hermes/USER.md`, which is separate from Python-compatible `memories/USER.md`.
- C# runtime treats missing `memory.memory_enabled` and `memory.user_profile_enabled` config values as false in startup wiring, while Settings UI presents true defaults.
- Python memory tool schema explicitly says save proactively and gives priority/skip guidance. C# tool description is similar but less source-faithful.

## Constraints
- Do not implement external provider backends in this task.
- Do not restore JSONL session/activity storage.
- Preserve existing user changes and do not revert unrelated `.omx` dirty files.
- Follow TDD: write failing regression tests before implementation changes.
- Final commit must follow Lore commit protocol.

## Unknowns / open questions
- Exact current C# test helpers for App.xaml.cs config default behavior.
- Whether root `USER.md` should be eliminated or mirrored. Reference parity suggests the memory-visible user profile should be `memories/USER.md`; C# Soul may still need root `USER.md` for legacy UI.

## Likely codebase touchpoints
- `src/Tools/MemoryTool.cs`
- `src/memory/MemoryManager.cs`
- `src/memory/MemoryReviewService.cs`
- `src/plugins/BuiltinMemoryPlugin.cs`
- `src/soul/SoulService.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/Services/HermesEnvironment.cs`
- `Desktop/HermesDesktop/Views/SettingsPage.xaml.cs`
- `Desktop/HermesDesktop/Views/ChatPage.xaml.cs`
- `Desktop/HermesDesktop/Views/Panels/MemoryPanel.xaml.cs`
- `Desktop/HermesDesktop/Views/AgentPage.xaml.cs`
- `Desktop/HermesDesktop/Views/Panels/AgentPanel.xaml.cs`
- `Desktop/HermesDesktop.Tests/Services/*Memory*Tests.cs`
