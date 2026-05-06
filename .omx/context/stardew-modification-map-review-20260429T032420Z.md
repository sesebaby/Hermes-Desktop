# Context Snapshot: Stardew Modification Map Review

- Task statement: Review `.omc/specs/deep-interview-stardew-modification-map-2026-04-29.md` against the Hermes Desktop codebase and related design materials before any implementation.
- Desired outcome: Confirm whether the plan's direction is correct for a beginner user, and ask before deciding when the evidence is uncertain.
- Stated solution under review: Continue with current Hermes Desktop as the main stack and build a cross-game NPC runtime core, game contract layer, Stardew adapter, NPC packs, autonomy loop, coordination, trace evidence, and minimal debug surface.
- Probable intent hypothesis: User wants to avoid implementing a plausible-looking but wrong architecture, especially where previous specs may have overstated existing capabilities.

## Known Facts / Evidence

- Current repo is brownfield: .NET / WinUI Hermes Desktop app with existing Agent, memory, soul, transcript, tools, and desktop host code.
- `omx explore` was unavailable on Windows in this session; fallback read-only shell inspection was used.
- Current chat service is still centered on one `_currentSession` in `Desktop/HermesDesktop/Services/HermesChatService.cs`.
- `src/Core/Agent.cs` has an injectable Agent with tool loop, optional memory/soul/transcript/context dependencies, and max tool iteration limits.
- `src/memory/MemoryManager.cs` is directory-backed and can support isolation if multiple instances are deliberately constructed, but current desktop DI wires a singleton memory manager.
- `src/soul/SoulService.cs` uses home-level `SOUL.md`, `USER.md`, and journal paths. `src/soul/AgentProfile.cs` activates a profile by copying soul content into the global soul file, so profile switching is not concurrent NPC isolation.
- `src/transcript/TranscriptStore.cs` persists sessions to SQLite with a configurable session source, but NPC identity fields and bridge trace ids are not present in `ActivityEntry`.
- `Desktop/HermesDesktop/App.xaml.cs` registers singleton `TranscriptStore`, `MemoryManager`, `SoulService`, `ContextManager`, and one singleton `Agent` for the current desktop path.
- No current `src/runtime`, `src/game/core`, `src/games/stardew`, `content/npc-packs`, `NpcRuntimeSupervisor`, `IGameAdapter`, `StardewCommandService`, `NpcAutonomyLoop`, or `WorldCoordinationService` implementation was found.
- Recent commits match a direction of trimming non-game surfaces and separating skills from souls: `8434bd10`, `15e2b6bf`, `48f1a406`, `4dae1e42`.
- Related docs:
  - `.omc/specs/deep-interview-stardew-allgameinai.md`: earlier MVP favored SMAPI + Hermes Desktop and explicitly said "not cross-game general framework" for that phase.
  - `.omc/specs/deep-interview-stardew-route-decision-2026-04-27.md`: later decision favored current repo as main stack and `hermescraft-main` only as architecture reference.
  - `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md`: stronger evidence-layered plan; distinguishes current facts, reference facts, and new Stardew constraints.
  - `docs/superpowers/plans/2026-04-29-cross-game-npc-runtime-architecture.md`: implementation plan for cross-game NPC runtime core + Stardew adapter + NPC packs.

## Constraints

- Do not implement during deep-interview.
- Ask one question per round via `omx question`.
- Preserve non-goals and decision boundaries before handoff.
- User is a beginner; explain correctness in concrete terms and do not assume hidden architecture knowledge.

## Unknowns / Open Questions

- Whether the user wants the review to judge the "bigger cross-game architecture" as the new first-pass direction, or only to correct the narrower Stardew MVP plan.
- Whether "fastest MVP" still dominates over building the cross-game foundation now.
- Whether the first deliverable should be a review artifact only or a corrected implementation plan afterward.
- Exact tolerance for structural refactor size in the first implementation wave.

## Decision-Boundary Unknowns

- May the agent reject or rewrite parts of the current plan if evidence says the plan is over-abstracted for MVP speed?
- May the agent choose a narrower Stardew-first implementation lane if it still keeps future game boundaries clean?
- Does the user want decisions optimized for beginner comprehensibility, fastest playable demo, long-term architecture, or a balance?

## Likely Codebase Touchpoints

- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/Services/HermesChatService.cs`
- `src/Core/Agent.cs`
- `src/memory/MemoryManager.cs`
- `src/soul/SoulService.cs`
- `src/soul/AgentProfile.cs`
- `src/transcript/TranscriptStore.cs`
- New planned paths: `src/runtime/`, `src/game/core/`, `src/games/stardew/`, `content/npc-packs/`
