# PRD: Single NPC Agent-Driven Stardew Autonomy Loop

## Objective

Enable one Stardew NPC runtime, Haley first, to complete a real autonomous life loop:

`self-driven tick -> observe facts -> assemble NPC-local context -> decide -> call NPC-scoped tool -> poll result -> persist trace/activity/memory -> next tick`

The hard product rule is that the game never drives the Agent. Stardew and the bridge provide facts only.

## Problem Statement

Hermes Desktop already has NPC runtime skeletons, Stardew bridge command contracts, Haley/Penny persona packs, NPC namespace isolation, memory primitives, trace/log primitives, and manual `move` / `speak` debug paths. The missing behavior is an NPC-owned autonomy loop.

Today `NpcRuntimeInstance.StartAsync` moves state to `Running` and creates directories, but it does not keep Haley awake, observing, deciding, acting, polling, and remembering.

## Hard Rules

1. No event-driven Agent.
2. Game, SMAPI, bridge, proximity, dialogue, inbox, and scheduler events are facts only.
3. Event handlers must not call LLM completion, `Agent.ChatAsync`, `StardewCommandService.SubmitAsync`, `move`, or `speak`.
4. Event handlers must not enqueue semantic commands such as "Haley should respond now."
5. Only the NPC autonomy loop tick may enter the observe/decide/tool/action path.
6. Prompt assembly must remain on runtime-local `ContextManager` / `PromptBuilder`; no independent Stardew prompt assembler.
7. CLI, typed tools, and future UI must go through `StardewCommandService`; no parallel SMAPI HTTP path.

## Scope

In scope:
- Add a bounded `NpcAutonomyLoop` under `src/runtime`.
- Support one NPC profile first, preferably Haley default profile.
- Add fact-only Stardew observation/query boundary for NPC status.
- Add NPC-scoped Stardew tools for status, move, speak, and task status.
- Construct runtime-local `SoulService`, `MemoryManager`, `TranscriptStore`, `ContextManager`, and `PromptBuilder` from `NpcNamespace`.
- Seed/copy persona pack material into the NPC namespace so Haley's soul/persona is loaded from files.
- Feed Stardew skills and observation facts through existing context inputs.
- Persist per-tick trace/activity/memory under the NPC namespace.
- Enforce `NpcAutonomyBudget` and no live-game requirement in tests.

Out of scope:
- Multi-NPC scheduling beyond not breaking existing discovery.
- Offscreen NPC chat, full SocialRouter, social graph, economy, farming, crafting, collect/interact/goto expansion.
- Always-on startup at app launch.
- UI redesign beyond minimal status/service wiring.
- New third-party dependencies.

## Requirements

1. `NpcAutonomyLoop.RunOneTickAsync` must be testable without a live Stardew process.
2. A tick must observe bridge/NPC facts before any LLM decision or tool action.
3. Observation facts must be passive inputs. They must not initiate a decision by themselves.
4. The NPC Agent must receive only NPC-safe tools, not the global Desktop tool registry.
5. Runtime-local context must include Haley identity, persona pack material, skills, recent observation facts, session id, and NPC memory.
6. Move and speak actions must go through `StardewCommandService`.
7. Long-running commands must poll `task/status` until terminal status or configured limit.
8. Trace/activity records must include enough identity and command context to connect `npcId`, `saveId`, `profileId`, `traceId`, and `commandId`.
9. Meaningful outcomes must be written as concise NPC memory entries through the NPC-local `MemoryManager`.
10. Starting/stopping the runtime must preserve existing `NpcRuntimeSupervisor.Snapshot()` semantics.

## Acceptance Criteria

1. Starting Haley creates the runtime namespace and a loop-capable runtime handle.
2. `RunOneTickAsync` calls observe before invoking any LLM or tool.
3. An injected fake event/fact source records facts but causes zero LLM/tool/command calls until `RunOneTickAsync` is called.
4. Haley context is prepared through runtime-local `ContextManager` / `PromptBuilder`, not a custom Stardew prompt assembler.
5. NPC tool definitions are limited to Stardew status/query, move, speak, task status/cancel where enabled, and NPC-local memory where enabled.
6. A fake move decision submits `GameActionType.Move` through `StardewCommandService`, captures `commandId`, and polls status.
7. A fake speak decision submits `GameActionType.Speak` through `StardewCommandService`.
8. Bridge unavailable or world-blocked facts result in a no-op/paused trace, not retry spin or forced action.
9. A completed tick writes trace/log output under the NPC namespace and updates `LastTraceId`.
10. A completed meaningful tick writes or attempts a concise NPC memory entry.

## Touchpoints

- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcRuntimeHost.cs`
- `src/runtime/NpcNamespace.cs`
- `src/runtime/NpcAutonomyBudget.cs`
- `src/runtime/NpcRuntimeTrace.cs`
- `src/runtime/NpcRuntimeLogWriter.cs`
- `src/game/core/GameAction.cs`
- `src/game/core/NpcPackManifest.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `src/games/stardew/StardewBridgeDiscovery.cs`
- `src/game/stardew/personas/haley/default/*`
- `skills/gaming/stardew-core.md`
- `skills/gaming/stardew-navigation.md`
- `skills/gaming/stardew-social.md`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- `Desktop/HermesDesktop.Tests/Runtime/*`
- `Desktop/HermesDesktop.Tests/Stardew/*`

## Evidence Basis

- Gap analysis prioritizes Agent runtime/autonomy loop first.
- Multi-NPC design says NPC agents observe and decide by themselves while bridge exposes facts only.
- Multi-NPC design requires prompt assembly through `ContextManager` / `PromptBuilder`.
- Existing code already has runtime namespace isolation, budget primitives, bridge DTOs, command service, persona packs, and focused tests.
