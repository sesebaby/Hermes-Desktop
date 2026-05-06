# RALPLAN Draft: Stardew Single NPC Autonomy Alignment

## RALPLAN-DR Summary

### Principles

1. Prove the smallest real life loop before expanding the village.
2. Keep NPC identity, memory, transcript, trace, and activity isolated by `game/save/npc/profile`.
3. Let game actions go only through typed Stardew bridge contracts; no direct ad hoc HTTP from the loop.
4. Make every tick auditable: observation, decision, tool call, command status, memory/activity write.
5. Preserve hard autonomy: the Agent owns cadence, observation, decision, and action; game events are facts only.
6. Keep prompt assembly on the existing `ContextManager` / `PromptBuilder` path; no independent Stardew prompt assembler.
7. Cap cost and runaway behavior before adding richer behavior.

### Decision Drivers

1. The first user-visible milestone must demonstrate `observe -> context -> decide -> act -> poll -> persist -> next tick`.
2. The implementation should reuse existing `NpcRuntime*`, `NpcNamespace`, `Agent`, `MemoryManager`, and Stardew bridge abstractions.
3. Tests must run without Stardew Valley or SMAPI by using fake bridge/client interfaces.

### Viable Options

#### Option A: Put the loop directly in `NpcRuntimeInstance`

Pros:
- Fewest new classes.
- The current lifecycle state already lives there.

Cons:
- `NpcRuntimeInstance` becomes both state holder and orchestration engine.
- Harder to test one tick without also testing lifecycle/background concerns.
- More likely to grow into a mixed runtime/LLM/bridge class.

#### Option B: Add a dedicated `NpcAutonomyLoop` owned by the runtime instance

Pros:
- Keeps `NpcRuntimeInstance` as lifecycle/state while the loop owns tick orchestration.
- Gives tests a direct `RunOneTickAsync` target.
- Can inject fake observer, command service, LLM, memory, and log writer independently.
- Natural place to enforce `NpcAutonomyBudget`.
- Enforces that the game/bridge can only provide facts; it cannot call the LLM or force a tool action.

Cons:
- Adds a small orchestration layer.
- Requires careful DI wiring so Desktop does not start loops unintentionally.

#### Option C: Reuse the general `Agent` as the entire NPC runtime immediately

Pros:
- Closest to the reference project's long-running tool-calling agent model.
- Reuses existing tool loop, transcript, context, activity, and memory behavior.

Cons:
- Too broad for Phase 1 because desktop tools are not NPC-scoped.
- Needs NPC-specific Stardew tool wrappers and prompt assembly first.
- Harder to guarantee one bounded tick and no unrelated desktop tool access.

### Favored Option

Option B, with a narrow bridge into Option C: create a dedicated `NpcAutonomyLoop` for tick lifecycle, but inside each tick use an NPC-scoped `Agent` only after registering a minimal allowlist of NPC tools.

Hard boundary: the loop must not be event-driven. A SMAPI event, bridge event, inbox message, proximity change, dialogue click, or scheduler notification can only become an observable fact in the NPC's next self-driven tick. It must not directly invoke `Agent.ChatAsync`, directly call `move` / `speak`, or enqueue a semantic command such as "Haley should respond now."

## Requirements Summary

- Add a single-NPC autonomy loop that can run one bounded tick for Haley using existing persona packs and runtime namespace.
- The tick must observe current Stardew NPC/world status, assemble NPC-specific context, let the NPC decide via LLM, execute only NPC-scoped Stardew tools, poll long-running command status, and persist trace/activity/memory evidence.
- The first implementation should support existing bridge actions only: query/status observation, `move`, `speak`, `task/status`, and `task/cancel` if needed.
- The loop must not require a live game during tests.
- Desktop should expose or wire the service without auto-launching uncontrolled background behavior unless a later execution decision explicitly chooses that.
- Game events and bridge messages are not a trigger language. They are written to an observation/fact store and are read only when the NPC's own autonomy loop decides to observe.
- NPC-specific prompt material must enter through runtime-local `SoulService`, `MemoryManager`, `TranscriptStore`, `ContextManager`, and `PromptBuilder` wiring, not through a separate Stardew prompt assembler.

## Acceptance Criteria

1. Starting a discovered Haley runtime creates the existing namespace directories and a loop owner, while `NpcRuntimeSupervisor.Snapshot()` still reports lifecycle state.
2. `RunOneTickAsync` for a fake Haley runtime calls observe before any action decision.
3. The existing `ContextManager` / `PromptBuilder` path, when constructed for Haley's runtime namespace, produces context that includes Haley pack material (`SOUL.md`, facts, voice/boundaries or their resolved text), required Stardew skills, recent observation facts, and NPC namespace/session id.
4. The LLM sees only NPC-safe tools: Stardew status/query, move, speak, task status/cancel, and NPC memory write/read if enabled. Generic desktop tools are not exposed.
5. A move decision submits a `GameActionType.Move` through `StardewCommandService`, captures `commandId`, and polls until terminal status or configured iteration limit.
6. A speak decision submits `GameActionType.Speak` through `StardewCommandService` and records the result.
7. A blocked bridge state or unavailable discovery produces a paused/no-op trace record instead of throwing or retry-spinning.
8. A completed tick writes at least one trace/log record under the NPC namespace and records the latest trace id on the runtime instance.
9. A completed meaningful outcome writes a concise NPC memory entry through `MemoryManager.AddAsync("memory", ...)` or an explicit deterministic memory writer backed by `MemoryManager`.
10. Tests prove the loop uses fake bridge clients and does not require live Stardew/SMAPI.
11. Tests prove an incoming bridge/game event does not directly invoke LLM completion, `Agent.ChatAsync`, `StardewCommandService.SubmitAsync`, `move`, or `speak`; it only appears as a fact available to the next autonomous tick.

## Implementation Steps

1. Add observer/query boundary.
   - Add `IStardewWorldObserver` and `StardewWorldObserver` under `src/games/stardew/`.
   - Wrap `ISmapiModApiClient` calls to `StardewBridgeRoutes.QueryStatus` for `StardewNpcStatusData`.
   - Keep `/query/world_snapshot` as DTO/extension point only if needed for the first tick.
   - Extend `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs` or add `StardewWorldObserverTests.cs`.
   - Add or designate an observation fact type; if bridge events are later captured, they are appended as facts only.

2. Add NPC-scoped Stardew tools.
   - Add small `ITool` wrappers under `src/games/stardew/tools/` or `src/runtime/tools/`.
   - Tools should bind `npcId`, `saveId`, `traceId`, and `idempotencyKey` internally from the runtime context, not accept arbitrary NPC ids from the model.
   - Include `stardew_status`, `stardew_move`, `stardew_speak`, and `stardew_task_status`.
   - Reuse `StardewCommandService` for move/speak/status.

3. Wire NPC runtime-local context through existing prompt infrastructure.
   - Do not add a separate `StardewPromptAssembler` / `NpcPromptContextBuilder`.
   - Seed or copy pack files from `NpcRuntimeDescriptor.PackRoot` into the NPC namespace so `NpcNamespace.CreateSoulService(...)` can feed `SoulService.AssembleSoulContextAsync(...)`.
   - Create runtime-local `MemoryManager`, `TranscriptStore`, `ContextManager`, and `PromptBuilder` instances from `NpcNamespace` rather than using the Desktop singletons.
   - Add a narrow `StardewSkillBinding`/provider that only reads and validates `skills.json` names, resolves them to `skills/gaming/stardew-*.md`, and hands the text to the existing `ContextManager`/`PromptBuilder` path as retrieved/plugin context.
   - Pass observation/status facts, recent command status, session id, and memory summary through the existing context inputs, not through a custom prompt string builder.

4. Add `NpcAutonomyLoop`.
   - New class under `src/runtime/`.
   - Public `RunOneTickAsync(NpcRuntimeInstance instance, CancellationToken ct)` for tests.
   - Internally: acquire `NpcAutonomyBudget`, observe bridge status, build context, create NPC-scoped `Session`, instantiate or use an NPC-scoped `Agent`, register only NPC tools, run one bounded decision turn, poll command status where required, write trace/activity, write memory, update `LastTraceId`.
   - Keep background scheduling outside the first class or behind an explicit `StartLoopAsync` wrapper.
   - The only caller allowed to enter the LLM/tool decision path is the autonomy loop's own tick method. Event handlers may record facts, set coarse runtime state such as "world unavailable", or release resources, but cannot drive the Agent.

5. Wire lifecycle without uncontrolled startup.
   - Extend `NpcRuntimeInstance` to hold/own a loop handle and cancellation token only if execution chooses continuous mode.
   - Extend `NpcRuntimeSupervisor` or `NpcRuntimeHost` to accept/use `NpcAutonomyLoop`.
   - Keep current Dashboard discovery behavior; add a clear service method for starting Haley's loop rather than starting all NPCs at app boot.
   - Do not introduce event callbacks that translate game events into NPC commands. A scheduler can decide when to tick, but not what the NPC decides.

6. Add tests before/with implementation.
   - Runtime tick tests for observe-first, tool allowlist, trace write, memory write, and budget exhaustion.
   - Autonomy-boundary tests for "event recorded as fact only; no LLM/tool/command side effects until `RunOneTickAsync`".
   - Stardew observer/tool wrapper tests for typed envelopes and scoped NPC id.
   - Supervisor/lifecycle tests for start/stop cancellation and snapshot trace id.
   - Regression tests that existing manual debug speak and command service tests still pass.

## Risks And Mitigations

- Risk: The loop accidentally exposes generic desktop tools to NPCs.
  - Mitigation: build NPC agents with an explicit local tool registry and assert allowlist in tests.
- Risk: Background mode causes cost/runaway loops.
  - Mitigation: first expose `RunOneTickAsync`; continuous scheduling requires budget, cancellation, cooldown, and explicit start.
- Risk: Event routing slips into hidden semantic control of NPCs.
  - Mitigation: create a strict fact-only event ingestion boundary and tests that assert no LLM/tool/command calls happen from event handlers.
- Risk: Memory writes become noisy or exceed limits.
  - Mitigation: write only concise, result-backed entries; test duplicate/limit handling through `MemoryManager`.
- Risk: Prompt assembly duplicates global context behavior.
  - Mitigation: do not create a separate assembler; construct runtime-local `ContextManager` / `PromptBuilder` instances and feed NPC facts/skills through their existing input layers.
- Risk: Live Stardew state is unavailable during CI.
  - Mitigation: fake `ISmapiModApiClient`, `IStardewWorldObserver`, and `IGameCommandService` in tests.

## Verification Steps

1. `dotnet test -c Debug -p:Platform=x64 --filter "FullyQualifiedName~Runtime|FullyQualifiedName~Stardew|FullyQualifiedName~GameCore"`
2. `dotnet test -c Debug -p:Platform=x64 --filter "FullyQualifiedName~NpcAutonomyLoop"`
3. `dotnet test -c Debug -p:Platform=x64 --filter "FullyQualifiedName~AutonomyBoundary|FullyQualifiedName~EventFact"`
4. `dotnet build Desktop/HermesDesktop/HermesDesktop.csproj -c Debug -p:Platform=x64`
5. Optional manual smoke only after tests pass: start the app, confirm Haley is discovered, run one Haley autonomy tick with fake or live bridge, inspect NPC runtime trace/activity/memory files.

## PRD Artifact Notes

- PRD should define the Phase 1 product story as "Haley can complete one auditable autonomous tick."
- It should explicitly exclude multi-NPC village coordination, advanced interaction/farming/crafting, and always-on startup.
- It must state the autonomy boundary as non-negotiable: game/bridge events are facts, never semantic drivers.

## Test Spec Artifact Notes

- Test spec should anchor around fake bridge behavior, deterministic LLM/tool decisions, namespace persistence, and no live SMAPI requirement.
- It must include negative tests proving event ingestion cannot directly trigger decisions or actions.
- It must include tests proving NPC context still flows through `ContextManager` / `PromptBuilder`, not a custom Stardew assembler.
