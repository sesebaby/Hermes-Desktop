# Python Memory Parity Evidence

## Implemented

- Added Python-style provider lifecycle:
  - `IMemoryProvider`
  - `HermesMemoryOrchestrator`
  - `TranscriptMemoryProvider`
- Turn start now calls provider `OnTurnStartAsync` before `PrefetchAllAsync`.
- Prefetched memory is sanitized, fenced, and appended only to the current API user message.
- Memory context fence now matches Python's `<memory-context>` wrapper shape.
- Completed non-empty turns now call memory sync before queueing next prefetch.
- Empty assistant responses skip sync and queue prefetch.
- `session_search` now supports:
  - empty-query recent-session browse mode
  - keyword session grouping
  - session-level summary output
  - optional role filter
  - Python default `limit=3`, clamped to 1..5
- Old injected `<memory-context>` and `<memory_context>` blocks are stripped before recall, summary, and FTS indexing.
- Ralph deslop pass removed unused recall merge code and unreachable summary formatting logic without widening scope.

## Verified

- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --filter MemoryParityTests --no-restore`
  - Passed: 27
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore`
  - Passed: 559
- `dotnet build HermesDesktop.sln --no-restore`
  - Succeeded with 0 warnings and 0 errors
- `dotnet build Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 --no-restore`
  - Succeeded with 0 warnings and 0 errors
- `git diff --check`
  - Exit code 0
  - Only line-ending normalization warnings on already-dirty tracked files

## Remaining Non-Parity

- C# still uses JSONL `TranscriptStore` as authoritative storage; Python uses SQLite `SessionDB`.
- C# session summary uses optional app `IChatClient` and deterministic fallback; Python has a dedicated auxiliary `session_search` model path and bounded concurrency config.
- External provider plugins such as Honcho/Hindsight/Mem0 are not implemented in this pass, but the lifecycle seam now exists.

## Ralph Continuation 2026-04-27

- Scoped cleanup stayed within Ralph-owned memory parity files.
- Added regression coverage for Python fence shape and omitted `session_search` limit default.
- Removed the C#-only public `maxResults` alias so the public `session_search` schema matches Python's `query` / `role_filter` / `limit`; `CurrentSessionId` remains runtime-only.
- Final architect review: APPROVED after schema cleanup, with no blocking memory lifecycle findings.
- Post-cleanup verification is green:
  - MemoryParityTests: 27 passed
  - Full test project: 559 passed
  - Solution build: 0 warnings, 0 errors
  - x64 Debug desktop build: 0 warnings, 0 errors
  - `git diff --check`: exit code 0, only CRLF normalization warnings

## Built-In Curated Memory Parity Continuation 2026-04-27

- Replaced old C# `memory save/list/delete` timestamped-file behavior with Python-style `memory add/replace/remove`.
- Fixed curated memory storage to `HERMES_HOME/memories/MEMORY.md` and `HERMES_HOME/memories/USER.md`.
- Preserved Python entry delimiter `\n§\n`, duplicate suppression, target isolation, substring replace/remove, char limits, prompt-injection/exfil scanning, file locks, and atomic writes.
- Changed `BuiltinMemoryPlugin` from per-turn query-ranked memory lookup to frozen session-start snapshot semantics.
- Added Python-style plugin prompt integration on the optimized desktop path: `ContextManager` now threads plugin system blocks through `PromptBuilder`, so `MEMORY.md` / `USER.md` frozen snapshots are present in the real outbound request.
- Added Python-style compression refresh: context summarization invokes plugin pre-compress hooks, `BuiltinMemoryPlugin` rebuilds its frozen snapshot from disk on that hook, and the refreshed snapshot is used by the same compressed outbound turn.
- Synchronized refreshed plugin system blocks into Chat and Stream tool-loop continuation state, including the empty-snapshot case where stale turn-start memory blocks must be removed.
- Fixed prompt boundary parity: built-in curated memory plugin blocks are no longer wrapped in `<memory-context>`; only prefetched recall uses that fence.
- Fixed model-facing tool schema parity: the `memory` tool now exposes Python-compatible `action` / `target` required fields and enums.
- Added built-in memory write bridge: successful `add` and `replace` notify memory-category plugins; `remove` intentionally does not bridge, matching Python.
- Added non-blocking periodic memory review/nudge via `MemoryReviewService`, configured by `memory.nudge_interval`, using the same `memory` tool and write bridge.
- Fixed Python enablement parity: memory is unavailable unless `memory.memory_enabled` or `memory.user_profile_enabled` is explicitly `true`; disabled memory returns the Python-shaped unavailable error.
- Confirmed source-faithful enablement semantics: Python creates one built-in store when `memory_enabled || user_profile_enabled`; separate flags control prompt block injection, not per-target write authorization.
- Fixed interrupted-turn parity: background memory review is skipped for incomplete/partial turns, including a consumer-stopped stream.
- Updated Desktop startup and memory UI/panel to use the Python-compatible `memories` directory and fixed file names.
- Added non-destructive migration from legacy `hermes-cs/memory/*.md` YAML-frontmatter files into the fixed stores.

Verification:
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "BuiltinMemoryPlugin_OnPreCompress_RefreshesFrozenSnapshot|ContextManager_PrepareContext_CallsPluginPreCompressBeforeSummarizingEvictedMessages"`: 2 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "Agent_OptimizedContextIncludesBuiltinMemoryPluginSnapshot|Agent_CompressionTurnUsesRefreshedBuiltinMemorySnapshot"`: 2 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "StreamChatAsync_CompressionToolContinuationUsesRefreshedBuiltinMemorySnapshot|Agent_CompressionToolContinuationRemovesStaleBuiltinMemoryWhenSnapshotBecomesEmpty"`: 2 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "BuiltinMemoryPlugin_SystemPromptBlock_IsNotRecallFenced|MemoryToolSchema_RequiresActionAndTargetAndUsesPythonEnums"`: 2 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter MemoryToolTests`: 32 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "MemoryToolTests|MemoryReviewServiceTests|AgentStreamChatTests|MemoryParityTests"`: 79 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore`: 592 passed.
- `dotnet build Desktop\HermesDesktop\HermesDesktop.csproj --no-restore`: succeeded, 0 warnings, 0 errors.
- `dotnet build HermesDesktop.sln --no-restore`: succeeded, 0 warnings, 0 errors.
- `dotnet build Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 --no-restore`: succeeded, 0 warnings, 0 errors.
- `git diff --check`: exit code 0, line-ending normalization warnings only.

Remaining non-parity after this continuation:
- Background memory review uses a lightweight C# `IChatClient.CompleteWithToolsAsync` pass, not a full Python-style background `AIAgent` fork. This is logged as a title-local adaptation in `.omx/plans/python-memory-deviation-approvals.md`.
- Python's skill-review nudge is still outside memory parity scope.
- Provider-specific external memory backends remain unimplemented; C# now has the bridge/lifecycle seam for them.
