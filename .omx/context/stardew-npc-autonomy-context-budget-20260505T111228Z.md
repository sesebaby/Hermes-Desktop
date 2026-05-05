# Stardew NPC Autonomy Context Budget Context Snapshot

Task statement:
- Plan implementation for `docs/superpowers/specs/2026-05-05-stardew-npc-autonomy-context-budget-design.md`.

Desired outcome:
- Produce a consensus implementation plan that constrains Stardew NPC autonomy first-call context to a default 5,000 character target while reusing existing context, memory, recall, session search, compaction concepts, and Stardew skill assets.
- Produce both PRD and test-spec artifacts so a later `$ralph` execution handoff has a concrete scope and verification contract.

Known facts and evidence:
- `src/runtime/NpcAutonomyLoop.cs:132-143` builds the autonomy decision message and calls `_agent.ChatAsync(...)`, so each autonomy tick is a full Agent turn.
- `src/Core/Agent.cs:348-357` prepares optimized context through `AgentContextAssembler.PrepareOptimizedContextAsync(...)`.
- `src/Core/Agent.cs:402-406` sends `preparedContext` only for the first tool iteration, then falls back to `session.Messages`.
- `src/Context/ContextManager.cs:14-18` says ContextManager replaces naive full-history sending with archive, session state, and selective recall.
- `src/Context/ContextManager.cs:98-100` trims archive messages to the recent window, but that window can still include large tool results.
- `src/Context/TokenBudget.cs:19` defaults to `maxTokens = 8000` and `recentTurnWindow = 6`.
- `src/runtime/NpcRuntimeContextFactory.cs:41-108` wires NPC context with `MemoryManager`, `TranscriptRecallService`, `HermesMemoryOrchestrator`, `TranscriptMemoryProvider`, `ContextManager`, and `TurnMemoryCoordinator`.
- `src/search/TurnMemoryCoordinator.cs:41` exposes `PrepareFirstCallAsync(...)` and preserves tool metadata when preparing memory context.
- `src/Tools/SessionSearchTool.cs:25` exposes `session_search`.
- `src/memory/MemoryManager.cs:51-52` already uses character limits: 2,200 for memory and 1,375 for user profile by default.
- `src/compaction/CompactionSystem.cs:241-291` has micro-compaction and orphaned tool-result sanitization concepts that can inform deterministic pruning.
- `src/runtime/AgentCapabilityAssembler.cs:20-25` registers `todo`, `todo_write`, `memory`, and `session_search`.
- `src/Core/Agent.cs:230-255` has an existing same-tool Stardew status budget; it currently tracks individual tool names, not the whole status class.
- `skills/gaming/stardew-social.md:30-40` already says ordinary autonomy turns should not query all status tools and should use at most one extra status tool.
- `skills/gaming/stardew-navigation.md:21-32` already defines destination movement and `stardew_task_status` polling.

Constraints:
- Scope is Stardew NPC autonomy only, not ordinary desktop chat.
- Do not create a second memory system, persona summary, tool lane, or host-side NPC brain.
- Host may budget and expose tools, but NPC Agent keeps intent ownership.
- Default autonomy first-call target is 5,000 characters; protected content must survive even when this target is exceeded.
- Use deterministic pruning in the first implementation; do not add per-tick LLM summarization.
- `stardew_task_status` should remain a likely continuation exception rather than being treated as exploratory broad status.
- Tests touching prompt/skill boundaries must read real repo assets, not fixture text.
- Current worktree has unrelated dirty/runtime files; do not revert or overwrite them.

Unknowns/open questions:
- Whether to expose a config knob immediately or keep 5,000 as an internal constant. Current recommendation: internal constant until logs prove repeated legitimate misses.
- Exact helper placement for budget logic. Current recommendation: a small focused helper under `src/Context/` or `src/runtime/` called from `Agent.ChatAsync` only for Stardew autonomy sessions.
- Exact logger event implementation surface. Current recommendation: structured `ILogger` events around the pruning call.

Likely codebase touchpoints:
- `src/Core/Agent.cs`
- `src/Core/AgentLoopScaffold.cs`
- `src/Context/TokenBudget.cs`
- `src/Context/ContextManager.cs`
- New helper likely under `src/Context/` for autonomy request budgeting.
- `src/runtime/NpcAutonomyLoop.cs` only if session state flags or trace propagation need adjustment.
- `skills/gaming/stardew-core.md`
- `skills/gaming/stardew-social.md`
- `skills/gaming/stardew-navigation.md`
- Tests under `Desktop/HermesDesktop.Tests/Services/`, `Desktop/HermesDesktop.Tests/Runtime/`, and `Desktop/HermesDesktop.Tests/Stardew/`.
