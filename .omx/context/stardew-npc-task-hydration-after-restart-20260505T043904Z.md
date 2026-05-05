# Context Snapshot: Stardew NPC task hydration after restart

UTC timestamp: 2026-05-05T04:39:04Z

## Task statement

Plan how to make Stardew NPC promise/todo continuity survive Hermes Desktop process restart, not only private-chat/autonomy handle reuse within the same process.

## Desired outcome

Produce a consensus implementation plan that restores NPC long-term active todo state from persisted transcript history when an NPC runtime instance is recreated after Hermes Desktop restart.

## Known facts / evidence

- `src/tasks/SessionTodoStore.cs` stores todo snapshots in an in-memory `ConcurrentDictionary`; it has no direct disk persistence.
- `src/transcript/TranscriptStore.cs` persists messages to SQLite-backed `state.db` through `SessionSearchIndex.SaveMessage`.
- `src/Core/Agent.cs` saves tool result messages to transcript using `session.Id` and includes `Message.TaskSessionId = session.ToolSessionId`.
- `src/games/stardew/StardewPrivateChatOrchestrator.cs` now uses private-chat transcript session id plus `ToolSessionId = descriptor.SessionId`.
- `src/runtime/NpcRuntimeInstance.cs` owns a process-local `SessionTodoStore` shared by private-chat and autonomy handles inside the same instance.
- `src/runtime/NpcRuntimeContextFactory.cs` accepts a `sharedTodoStore`, creates a `SessionTaskProjectionService`, and wires it as the transcript message observer.
- `src/Desktop/HermesChatService.cs` already hydrates desktop session task projection explicitly by loading transcript messages and calling `SessionTaskProjectionService.HydrateSessionAsync`.
- `src/runtime/NpcRuntimeSupervisor.cs` creates NPC runtime instances and handles but does not currently show equivalent startup task hydration.

## Constraints

- Do not add a second NPC-specific task store.
- Keep `todo` / `SessionTodoStore` / `SessionTaskProjectionService` as the single active task truth path.
- Preserve private chat transcript granularity; do not collapse private chat sessions into the long-term NPC session id.
- Recover tasks from persisted transcript/tool evidence, not by reinterpreting user text or adding host-side promise detection.
- Maintain each NPC/save/profile namespace isolation.
- The current worktree is dirty with unrelated/user changes; planning should not edit source files.

## Unknowns / open questions

- Whether `SessionSearchIndex` can efficiently list/filter sessions by `SessionSource` and load only the NPC namespace sessions.
- Whether private-chat transcript sessions are marked parent/child in metadata or only discoverable by id prefix.
- Whether existing persisted tool messages from before `TaskSessionId` should be backfilled by session id convention.
- Whether hydration should run at runtime instance creation, handle creation, or before task view/autonomy decision.

## Likely codebase touchpoints

- `src/tasks/SessionTaskProjectionService.cs`
- `src/tasks/SessionTodoStore.cs`
- `src/transcript/TranscriptStore.cs`
- `src/search/SessionSearchIndex.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- `Desktop/HermesDesktop.Tests/Services/HermesChatServiceTaskLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`

## Current risk framing

Current behavior is good for same-process continuity: private chat and autonomy handles share one instance-level todo store. It is not sufficient for process restart because the active task store is memory-only unless rebuilt from transcript history.
