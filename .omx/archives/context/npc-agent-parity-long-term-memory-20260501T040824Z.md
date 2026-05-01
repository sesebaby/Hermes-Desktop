# Ralph Context Snapshot: NPC Agent Parity Long-Term Memory

## Task Statement
Execute the approved RALPLAN at `.omx/plans/npc-private-chat-long-term-memory-ralplan.md`.

## Desired Outcome
- Haley/海莉 and Penny/潘妮 NPC agents are equal-capability instances of the desktop agent.
- NPC registered tool names and skill tools exactly match the desktop agent surface.
- NPCs use their own NPC-scoped memory/transcript/session/save/profile/namespace state, not the desktop singleton/global memory/session.
- NPC private chat can persist `我叫远古牛哥,你记住` through the reference-style model-invoked `memory` tool and recall it from durable curated memory in a fresh NPC agent/session.

## Known Facts / Evidence
- Desktop `Agent` registration in `Desktop/HermesDesktop/App.xaml.cs` wires `PluginManager`, `BuiltinMemoryPlugin`, `TurnMemoryCoordinator`, `MemoryReviewService`, `ToolRegistry`, and `MemoryTool`.
- Current Stardew private chat in `src/games/stardew/StardewPrivateChatOrchestrator.cs` creates a fresh NPC context/agent, passes `Array.Empty<ITool>()`, and uses `maxToolIterations: 1`.
- `src/runtime/NpcRuntimeContextFactory.cs` currently returns only soul, memory, transcript, prompt builder, and context manager. It does not return plugin/memory lifecycle services.
- Existing persona packs exist at `src/game/stardew/personas/haley/default` and `src/game/stardew/personas/penny/default`.
- Reference project memory is model/tool mediated: no Core/Desktop deterministic remember/regex/rule trigger.
- Curated memory must enter prompt through `BuiltinMemoryPlugin` frozen snapshot/system prompt, not dynamic `<memory-context>` prefetch.
- Existing `NpcAutonomyLoop` deterministic `Autonomy tick ...` memory writes are reference-parity debt and must not prove private-chat memory correctness.

## Constraints
- Use TDD: write failing tests before production code.
- Do not add dependencies.
- Do not add deterministic explicit-capture logic in Core/Desktop/Stardew bridge.
- Do not introduce NPC/private-chat exclusion lists, safe-tool subsets, transport/UI omission lists, or reduced tool/skill surfaces.
- Do not reuse desktop singleton `Agent`, desktop global memory, desktop transcript/session, or desktop profile state for NPCs.
- Keep Stardew mod/bridge transport-only; it must not own memory or capability assembly.
- Current worktree already has unrelated/previous dirty files: `src/transcript/TranscriptStore.cs`, `Desktop/HermesDesktop.Tests/Services/TranscriptStoreTests.cs`, and planning/context artifacts. Do not revert user/previous changes.

## Unknowns / Open Questions
- Exact desktop final tool surface after dynamic MCP registration.
- Whether current app tool registration can be extracted without changing WinUI startup behavior.
- How much of desktop tool assembly depends on Desktop-only services versus core services.
- Whether tests should compare a shared tool-bundle factory output rather than the live WinUI DI container.

## Likely Codebase Touchpoints
- `Desktop/HermesDesktop/App.xaml.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/runtime/NpcAgentFactory.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/Tools/MemoryTool.cs`
- `src/plugins/PluginManager.cs`
- `src/plugins/BuiltinMemoryPlugin.cs`
- `src/search/TurnMemoryCoordinator.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeContextFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAgentFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs`
- New focused tests for desktop/NPC capability parity and NPC memory recall.
