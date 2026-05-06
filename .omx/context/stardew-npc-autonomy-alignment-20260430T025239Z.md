# Context Snapshot: Stardew NPC Autonomy Alignment

## Task Statement

Align Hermes Desktop with the reference-project gap analysis by starting with the highest-priority module: a single NPC long-lived autonomous Stardew runtime loop.

## Desired Outcome

Create an execution-ready plan that turns one discovered Stardew NPC runtime, preferably Haley first, from a static/discovered shell into a small but real observe -> context -> decide -> act -> poll -> persist loop using the existing persona pack, NPC namespace, Stardew bridge command service, activity/trace primitives, and memory/session stores.

## Known Facts / Evidence

- The gap-analysis document says the main gap is that each NPC lacks an independent long-term life loop that observes, thinks, calls tools, polls results, writes memory, and uses it next round: `docs/Hermes参考项目功能模块效果差距对比.md:13`.
- Its priority order puts `Agent 运行时 / 自治循环` first, then `Context / Prompt 组装`, then `Memory / Soul 独立隔离`: `docs/Hermes参考项目功能模块效果差距对比.md:36`.
- The multi-NPC design document explicitly keeps the direction that NPC agents observe and decide by themselves while the bridge only exposes factual capability interfaces: `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md:8`.
- The same design document says prompt assembly must stay bound to `ContextManager` / `PromptBuilder`, there is no semantic director/main agent, SocialRouter only delivers messages, and the bridge cannot summarize or narrate facts for NPCs: `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md:11`, `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md:57`, `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md:60`, `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md:69`.
- User hard rule added on 2026-04-30: no event-driven agent. Game events and bridge updates are facts only. They must never directly force an NPC decision, directly call the LLM, directly issue a tool call, or enqueue a semantic task for the NPC.
- `NpcRuntimeInstance` currently mostly manages lifecycle state and namespace directory creation; `StartAsync` sets `Running` and returns immediately: `src/runtime/NpcRuntimeInstance.cs:23`.
- `NpcRuntimeSupervisor` registers, starts, stops, and snapshots runtime instances, but does not drive a background loop: `src/runtime/NpcRuntimeSupervisor.cs:28`.
- `NpcRuntimeHost` discovers persona packs and derives a stable session id like `sdv_{saveId}_{npcId}_{profileId}`: `src/runtime/NpcRuntimeHost.cs:18`.
- `NpcNamespace` already isolates runtime, memory, transcript, trace, activity, soul, and session source paths per game/save/npc/profile: `src/runtime/NpcNamespace.cs:16`, `src/runtime/NpcNamespace.cs:31`, `src/runtime/NpcNamespace.cs:39`.
- `NpcAutonomyBudget` already has LLM concurrency, tool-iteration, restart-limit primitives: `src/runtime/NpcAutonomyBudget.cs:15`, `src/runtime/NpcAutonomyBudget.cs:24`, `src/runtime/NpcAutonomyBudget.cs:29`.
- `NpcRuntimeTrace` and `NpcRuntimeLogWriter` already model trace events and JSONL log records: `src/runtime/NpcRuntimeTrace.cs:34`, `src/runtime/NpcRuntimeLogWriter.cs:27`.
- Stardew bridge routes include query status, world snapshot, events poll, move, task status, cancel, and speak: `src/games/stardew/StardewCommandContracts.cs:5`.
- `StardewCommandService` currently submits move/speak, polls task status, and cancels commands through `ISmapiModApiClient`: `src/games/stardew/StardewCommandService.cs:16`, `src/games/stardew/StardewCommandService.cs:75`.
- The DTO layer already contains `StardewNpcStatusData` for NPC observe/status facts: `src/games/stardew/StardewBridgeDtos.cs:81`.
- The bridge discovery layer reads a local loopback discovery file and rejects stale/unsafe bridge options: `src/games/stardew/StardewBridgeDiscovery.cs:37`, `src/games/stardew/StardewBridgeOptions.cs:17`.
- Haley and Penny packs exist under `src/game/stardew/personas/*/default`, with manifests requiring `move` and `speak`: `src/game/stardew/personas/haley/default/manifest.json:21`.
- Haley's `SOUL.md` contains persona motives that should influence decisions: `src/game/stardew/personas/haley/default/SOUL.md:1`.
- Stardew skills already describe the desired active-agent loop and Phase 1 navigation limits: `skills/gaming/stardew-core.md:3`, `skills/gaming/stardew-navigation.md:3`.
- The general `Agent` already supports registered tool calling and transcript/activity logging, but app startup registers desktop tools only, not NPC-scoped Stardew tools: `src/Core/Agent.cs:158`, `Desktop/HermesDesktop/App.xaml.cs:772`.
- Desktop DI already registers NPC pack loader, runtime supervisor, resource claims, world coordination, trace index, autonomy budget, runtime host, workspace service, bridge discovery, and debug action service: `Desktop/HermesDesktop/App.xaml.cs:356`.
- The dashboard workspace service currently displays discovered packs when no active runtime instances exist; it does not start the autonomy loop: `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs:28`.
- Existing tests cover pack loading, NPC binding, resource claims, autonomy budget, runtime supervisor, log writer, Stardew command contracts/service, manual debug speak, and bridge dialogue behavior.

## Constraints

- Planning-only for `$ralplan` unless the user explicitly switches to execution mode.
- No new dependencies.
- Keep Phase 1 narrow: one NPC, existing bridge surface, `status` / `move` / `speak`, no advanced follow/interact/farm/craft.
- Autonomy is agent-driven, not event-driven. The `NpcAutonomyLoop` owns cadence, observation, decision, and action. Game/bridge events may be persisted as facts for later observation, but no event handler may directly drive the NPC.
- Prompt assembly must use runtime-local `ContextManager` / `PromptBuilder` wiring. Do not add an independent Stardew prompt assembler.
- Preserve existing desktop UX and reliability guidance in `Desktop/HermesDesktop/AGENTS.md`.
- Existing uncommitted changes must not be reverted.
- Avoid direct production/game side effects in tests; use fake bridge/client abstractions.

## Unknowns / Open Questions

- Whether the autonomy loop should be started from UI controls, app startup, or an explicit service call in the first execution slice.
- Whether `StardewCommandService` should grow observe/query methods or whether observation should be a separate `IStardewWorldObserver`.
- Whether the NPC loop should reuse the general `Agent` directly or wrap a smaller NPC-specific planner/action parser around it.
- Current worktree/build health is unknown.

## Likely Codebase Touchpoints

- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcRuntimeHost.cs`
- `src/runtime/NpcNamespace.cs`
- `src/runtime/NpcAutonomyBudget.cs`
- `src/runtime/NpcRuntimeTrace.cs`
- `src/runtime/NpcRuntimeLogWriter.cs`
- `src/runtime/WorldCoordinationService.cs`
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
