# NPC Long-Term Memory Context Snapshot

## Task statement

Align Hermes Desktop NPC private chat memory behavior with the reference project at
`external/hermes-agent-main`, with the final implementation required to provide
verifiable long-term memory for NPC private chat.

## Desired outcome

When the player tells an NPC a stable personal fact such as "我叫远古牛哥, 你记住",
the fact is persisted as long-term memory and can be recalled in a later private
chat turn or later session, not merely while recent transcript context happens to
contain the original statement.

## Known facts / evidence

- Runtime logs showed the relevant Haley transcript was persisted in SQLite, but
  the NPC did not recall it in a later turn.
- The immediate transcript-context bug was in Hermes Core, not the Stardew mod:
  `src/transcript/TranscriptStore.cs` now hydrates cache from SQLite on fresh
  store instances after `SaveMessageAsync`.
- Regression test added:
  `Desktop/HermesDesktop.Tests/Services/TranscriptStoreTests.cs`
  `SaveMessageAsync_NewStoreInstancePreservesExistingSessionHistoryInCache`.
- Reference project separates:
  - transcript/session persistence via `hermes_state.py` / `SessionDB`
  - built-in durable memory via `tools/memory_tool.py` (`MEMORY.md` / `USER.md`)
  - optional external memory providers via `agent/memory_provider.py`
  - per-turn recall injection via `agent/memory_manager.py` fenced
    `<memory-context>` blocks.
- C# already has comparable pieces:
  - `src/memory/MemoryManager.cs`
  - `src/Tools/MemoryTool.cs`
  - `src/memory/MemoryReviewService.cs`
  - `src/memory/HermesMemoryOrchestrator.cs`
  - `src/search/TurnMemoryCoordinator.cs`
  - `src/plugins/BuiltinMemoryPlugin.cs`
- `StardewNpcPrivateChatAgentRunner` currently creates an NPC runtime context and
  agent with `Array.Empty<ITool>()`, so the model cannot call `memory` during
  private chat.
- `NpcAgentFactory.Create` currently passes transcript, memory manager, context
  manager, and soul service to `Agent`, but not plugin manager,
  turn-memory coordinator, or memory-review service.
- `NpcRuntimeContextFactory.Create` creates `MemoryManager`, `TranscriptStore`,
  `PromptBuilder`, and `ContextManager`, but does not include `PluginManager`,
  `BuiltinMemoryPlugin`, `TurnMemoryCoordinator`, `HermesMemoryOrchestrator`,
  or `MemoryReviewService`.
- `TurnMemoryCoordinator` already implements Python-style prefetch/sync shape:
  `OnTurnStart`, `PrefetchAll`, fenced `<memory-context>`, and completed-turn
  sync.
- `CuratedMemoryLifecycleProvider` intentionally does not dynamic-prefetch curated
  memory; curated memory currently enters via snapshot/system prompt pathways.

## Constraints

- Keep the Stardew mod as transport/UI bridge; memory behavior belongs in Hermes
  Desktop/Core runtime.
- Preserve reference-project boundary: transcript is session history; long-term
  memory is a separate durable store.
- Avoid new dependencies.
- Keep diffs small and testable.
- Do not rewrite the whole agent loop.
- Existing worktree has uncommitted transcript bugfix files; do not revert them.

## Unknowns / open questions

- Whether private chat should write memory only through an exposed `memory` tool,
  only through automatic post-turn review/extraction, or both.
- How aggressive automatic extraction should be for explicit "remember" phrases
  in Chinese and English.
- Whether private chat should include only NPC-local memory or also player-wide
  user profile memory.
- Whether existing `BuiltinMemoryPlugin` snapshot semantics are sufficient for
  fresh per-turn NPC agents once wired.

## Likely codebase touchpoints

- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/runtime/NpcAgentFactory.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/Core/Agent.cs`
- `src/search/TurnMemoryCoordinator.cs`
- `src/memory/MemoryReviewService.cs`
- `src/memory/MemoryManager.cs`
- `src/Tools/MemoryTool.cs`
- `Desktop/HermesDesktop.Tests/Runtime/*`
- `Desktop/HermesDesktop.Tests/Stardew/*`
- `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
