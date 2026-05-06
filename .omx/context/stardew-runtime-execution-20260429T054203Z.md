# Stardew Runtime Plan Execution Context

## Task Statement
Execute `docs/superpowers/plans/2026-04-29-cross-game-npc-runtime-architecture.md` for the current Hermes Desktop repository.

## Desired Outcome
Land the Phase 1 Stardew two-NPC MVP foundation in code: cross-game contracts, NPC pack loading, per-NPC runtime namespace, active `NpcAutonomyLoop` direction, Stardew bridge command contracts, minimum move/resource-claim semantics, trace/log surfaces, and minimum desktop/debug visibility. Preserve later roadmap in docs, but implement the first safe MVP slice in the current codebase.

## Known Facts / Evidence
- Current plan is committed in `54d23f58 Align Stardew MVP plan around active NPC play`.
- The plan locks active NPC behavior around `NpcAutonomyLoop`: observe, decide, act, poll command status, write trace.
- Game/SMAPI/scheduler/social events are facts, wake/pause/block signals, or SMAPI execution hooks only; they are not the driver for one-shot agent responses.
- `Desktop/HermesDesktop/Services/HermesChatService.cs` still owns a single `_currentSession`.
- `src/memory/MemoryManager.cs` is directory-backed and can be instantiated per namespace.
- `src/soul/SoulService.cs` is home-directory-backed and can be instantiated per namespace with constructor-compatible roots.
- `src/transcript/TranscriptStore.cs` is SQLite-first and supports `sessionSource`, so per-NPC transcript stores can be separated by path/source.
- `Desktop/HermesDesktop/App.xaml.cs` registers global singletons for the existing desktop chat path.

## Constraints
- Do not drift into event-driven one-shot agent behavior.
- Do not put Stardew/SMAPI terms into `src/runtime` or `src/game/core`.
- `move` and minimum UI/overlay are Phase 1 hard requirements.
- `StardewCommandService` must be the unique desktop command source for Stardew actions.
- Bridge defaults must be loopback-only and bearer-token protected outside `/health`.
- NPC seed persona files must be fixed pack content, not generated during runtime.
- The user is a beginner; final explanations must be plain and concrete.

## Unknowns / Open Questions
- SMAPI assemblies may not be available locally, so the mod project may need to remain scaffolded outside the main solution until dependency paths are configured.
- No live Stardew/SMAPI process is available in this shell, so verification must use unit tests and fake bridge traces first.
- `docs/shared/agent-tiers.md` referenced by the Ralph skill is absent in this repository; AGENTS.md role routing is used instead.

## Likely Codebase Touchpoints
- `src/game/core/*`
- `src/runtime/*`
- `src/games/stardew/*`
- `skills/gaming/stardew-*.md`
- `src/game/stardew/personas/haley/default/*`
- `src/game/stardew/personas/penny/default/*`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`
- `Mods/StardewHermesBridge/*`
- `Desktop/HermesDesktop.Tests/Runtime/*`
- `Desktop/HermesDesktop.Tests/GameCore/*`
- `Desktop/HermesDesktop.Tests/Stardew/*`
