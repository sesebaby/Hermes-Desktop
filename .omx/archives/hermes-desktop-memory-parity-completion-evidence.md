# Ralph Completion Evidence: Hermes Desktop Memory Parity

## Verification

- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --filter MemoryParityTests --no-restore`: 8 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore`: 540 passed.
- `dotnet build HermesDesktop.sln --no-restore`: succeeded, 0 warnings, 0 errors.
- `dotnet build Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 --no-restore`: succeeded, 0 warnings, 0 errors.
- `git diff --check`: no whitespace errors; line-ending warnings only for existing Git normalization.

## Architecture Gate

- Prompt boundary: `PromptBuilder` no longer emits transcript recall as a synthetic system layer.
- API-call-time injection: `TurnMemoryCoordinator` augments only the outbound current user message copy.
- Non-persistence: tests assert JSONL persists the original user message without injected recall.
- Shared corpus: automatic recall and `session_search` both use `TranscriptRecallService`.
- Authority: `TranscriptStore` JSONL remains authoritative; SQLite FTS is derived and rebuildable.
- Indexing failure isolation: transcript writes still succeed when observer/indexing throws.

## Architect Review Note

Codex architect subagent was attempted and timed out twice. Completion is based on local architecture self-review plus fresh automated evidence.

## Built-In Curated Memory Continuation

Additional implementation on 2026-04-27 aligned the built-in Python `memory` tool contract:

- `memory` now supports Python-compatible `add`, `replace`, `remove` actions against `memory` / `user` targets.
- Durable files are now fixed `HERMES_HOME\memories\MEMORY.md` and `HERMES_HOME\memories\USER.md`, with `\n§\n` entry delimiter.
- Built-in memory prompt injection is now a frozen session-start snapshot, not per-turn live query recall.
- Optimized desktop context assembly now includes plugin system blocks, so the frozen built-in memory snapshot reaches the real outbound prompt.
- Compression/summarization now invokes plugin pre-compress hooks, and built-in memory refreshes its frozen snapshot from disk for the same compressed outbound turn.
- Chat and Stream tool-loop continuations now use the refreshed plugin system block after compression; if the refreshed snapshot is empty, the old transient memory block is removed instead of leaking stale memory.
- Built-in curated memory blocks are appended as direct system-prompt content, not recall-fenced `<memory-context>` blocks; recall fencing remains limited to prefetched transcript/provider recall.
- The model-facing `memory` tool schema now requires `action` and `target` and exposes the Python enum constraints.
- Successful `add` / `replace` writes bridge to memory-category plugins; `remove` intentionally does not bridge.
- Periodic background memory review/nudge is present through `MemoryReviewService`.
- Memory must be explicitly enabled via `memory.memory_enabled` or `memory.user_profile_enabled`; disabled memory returns the Python-shaped unavailable error.
- The two config flags match Python semantics: they separately control prompt injection; once either flag enables the built-in store, the single `memory` tool still exposes both `memory` and `user` targets.
- Background memory review skips incomplete/partial turns, including stopped streams.

Fresh verification:

- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "BuiltinMemoryPlugin_OnPreCompress_RefreshesFrozenSnapshot|ContextManager_PrepareContext_CallsPluginPreCompressBeforeSummarizingEvictedMessages"`: 2 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "Agent_OptimizedContextIncludesBuiltinMemoryPluginSnapshot|Agent_CompressionTurnUsesRefreshedBuiltinMemorySnapshot"`: 2 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "StreamChatAsync_CompressionToolContinuationUsesRefreshedBuiltinMemorySnapshot|Agent_CompressionToolContinuationRemovesStaleBuiltinMemoryWhenSnapshotBecomesEmpty"`: 2 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "BuiltinMemoryPlugin_SystemPromptBlock_IsNotRecallFenced|MemoryToolSchema_RequiresActionAndTargetAndUsesPythonEnums"`: 2 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "MemoryToolTests|MemoryReviewServiceTests|AgentStreamChatTests|MemoryParityTests"`: 79 passed.
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore`: 592 passed.
- `dotnet build Desktop\HermesDesktop\HermesDesktop.csproj --no-restore`: succeeded, 0 warnings, 0 errors.
- `dotnet build HermesDesktop.sln --no-restore`: succeeded, 0 warnings, 0 errors.
- `dotnet build Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 --no-restore`: succeeded, 0 warnings, 0 errors.
- `git diff --check`: exit code 0, line-ending normalization warnings only.

Final architect review:
- `APPROVED` after fixing the last two source-faithfulness gaps.
- Built-in `MEMORY.md` / `USER.md` snapshots now enter the system prompt directly; `<memory-context>` remains recall-only.
- The public `memory` tool schema now requires `action` / `target` and exposes the Python enum constraints.
- Deslop pass found no high-signal cleanup edits inside the Ralph-owned file scope; post-deslop state is therefore the verified state above.
