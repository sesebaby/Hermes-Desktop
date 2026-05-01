# NPC Private Chat Long-Term Memory RALPLAN

## RALPLAN-DR Summary

### Principles
1. Strictly preserve the `external/hermes-agent-main` boundary: transcript is session history, curated memory is a separate durable store.
2. Reuse the existing Hermes/reference memory lifecycle instead of inventing a private-chat-specific capture path.
3. Treat desktop agent and NPC agents, including the current Stardew NPCs Haley/海莉 and Penny/潘妮 plus future NPCs, as equal agent instances with the same capability surface.
4. Keep Stardew mod/bridge as transport only; NPC memory and tool/skill assembly belong in Hermes runtime, not bridge/mod code.
5. Make recall verifiable across fresh NPC agent instances and later sessions, not only within transcript recency.

### Decision Drivers
1. Strict reference parity: the MVP must match `external/hermes-agent-main`, not introduce a deterministic Core/Desktop remember/regex/rule trigger.
2. Product parity: NPC agents must receive the same registered tool and skill surface as the desktop agent. Transport/UI differences may only affect invocation adapters, presentation, context binding, or runtime parameters behind the same registered tool/skill contracts; they must not remove, hide, or subset capabilities.
3. Durable recall correctness: explicit user facts like player name must survive beyond recent transcript context.
4. Isolation correctness: NPC agents are equal-capability agent instances, not the desktop singleton reused with shared global memory/session state.
5. Scope control: no new dependencies, no full loop rewrite, no Stardew transport redesign.

### Reference Facts That Constrain The Plan
- Built-in reference capture is model/tool mediated: `external/hermes-agent-main/tools/memory_tool.py` exposes a `memory` tool whose schema tells the model to proactively save when the user says "remember this" and when the user shares personal details such as name or preferences.
- Built-in durable stores are `MEMORY.md` and `USER.md`. Mid-session `memory` tool writes update disk immediately, but the current system prompt keeps its frozen snapshot; builtin snapshot refresh happens on the next session/new agent.
- `external/hermes-agent-main/run_agent.py` handles `memory` tool calls and bridges successful built-in writes through `on_memory_write` to external memory providers.
- External memory provider lifecycle may include `sync_turn`, `prefetch`, `queue_prefetch`, and `on_memory_write`. These are provider hooks, not a Core hardcoded remember trigger.
- Background memory review is async/nudge-based enrichment after responses and still uses the `memory` tool. It is not an MVP correctness guarantee.
- Dynamic `<memory-context>` prefetch is for external/transcript/provider recall. Curated built-in memory must remain available through builtin snapshot/system-prompt injection, not dynamic prefetch.

### Current C# Facts To Preserve Or Update
- NPC private chat currently creates a fresh context/agent per `ReplyAsync`, passes `Array.Empty<ITool>()`, and sets `maxToolIterations: 1`.
- C# already has `MemoryManager`, `MemoryTool`, `BuiltinMemoryPlugin`, `PluginManager`, `TurnMemoryCoordinator`, `HermesMemoryOrchestrator`, and `MemoryReviewService`.
- Current Stardew NPC persona packs exist for both `haley/default` and `penny/default`; this plan must cover both current NPCs, not only Haley.
- The desktop agent already has a broader tool/skill assembly path. The implementation should extract or reuse that assembly as a shared capability source for desktop and NPC agents.
- Any existing tests or assumptions that NPC/Stardew agents intentionally use a reduced safe-tool subset are obsolete for this product goal and must be revised to assert desktop/NPC capability parity instead.
- Existing parity tests assert curated memory provider does not dynamic-prefetch; keep that boundary.
- `NpcRuntimeContextFactory` is shared by private chat and autonomy/debug flows such as `StardewAutonomyTickDebugService`; plan changes at that factory boundary must preserve per-NPC namespace/session isolation while moving capability assembly toward desktop parity.
- `NpcAutonomyLoop` currently writes deterministic `Autonomy tick ...` decision responses into curated memory. That behavior is known reference-parity debt because those deterministic task/session outcome writes do not match the reference memory schema; do not fold them into this private-chat MVP correctness proof.

### Viable Options

#### Option A: Minimal memory-only NPC private-chat exposure
- Shape:
  - Add only the `MemoryTool` to NPC private chat.
  - Make the minimum loop/config changes needed for a tool call to complete.
  - Do not fully validate fresh-session builtin snapshot recall or desktop/NPC tool parity.
- Pros:
  - Smallest implementation surface.
  - Preserves reference-style model-initiated memory writes.
- Cons:
  - Rejected by the latest product requirement: NPCs must not be capability-subset agents.
  - Too weak for MVP because it does not prove full private-chat memory lifecycle or desktop/NPC capability parity.
  - Leaves fresh-session recall and same-turn post-tool reply behavior under-specified.

#### Option B: Reference-aligned memory lifecycle plus NPC agent capability parity
- Shape:
  - Build NPC agents from the same tool and skill capability source used by the desktop agent.
  - Extract or reuse a shared assembly mechanism such as `AgentCapabilityProfile`, `ToolBundle`, or a registration helper. The exact name is implementation detail; the requirement is one source of truth for desktop/NPC tool and skill registration.
  - Bind that shared capability surface to NPC-scoped collaborators: `MemoryManager`, `PluginManager`, `TranscriptStore`, `SoulService`, `ContextManager`, session/save/profile identity, and NPC namespace.
  - Do not reuse the desktop singleton `Agent`, desktop global memory, or desktop session. Create separate equal-capability NPC agent instances.
  - Use the desktop agent's same or configurable tool-loop budget for NPC agents, with an effective minimum that allows a model-initiated `memory` tool call followed by a final user-facing reply in the same turn (`maxToolIterations >= 2`).
  - Wire `BuiltinMemoryPlugin` frozen snapshot/system-prompt recall into the fresh NPC private-chat agent/session path.
  - Preserve and rely on reference-style `memory` tool schema/system-prompt guidance for model-mediated capture; do not add a deterministic remember/regex/rule trigger in Core/Desktop.
  - Keep `TurnMemoryCoordinator` limited to transcript/external-provider dynamic `<memory-context>` only; curated built-in memory must not become dynamic prefetch.
  - Treat reference-style external provider hooks (`sync_turn`, `prefetch`, `queue_prefetch`, `on_memory_write`) as optional enrichment only, not MVP correctness guarantees.
  - Treat `MemoryReviewService` as non-blocking enrichment and outside MVP acceptance whether wired or not.
- Pros:
  - Matches the product goal: desktop agent and NPC agents are equal agent instances that differ by identity/persona/system prompt and scoped state, not by capability subset.
  - Matches the reference project’s built-in memory pattern: model-invoked `MemoryTool` plus durable builtin snapshot recall.
  - Preserves the architect-corrected boundary: curated memory recall comes from builtin snapshot/system prompt, not dynamic prefetch.
  - Reduces divergence by making desktop and NPC tool/skill registration share one implementation path.
- Cons:
  - Requires broader tests than memory-only exposure because tool names and skill tools must be compared against desktop registration.
  - MVP capture correctness still depends on model/tool behavior and reference schema/system-prompt guidance rather than a deterministic Core-side trigger.
  - Requires careful per-instance binding so desktop global memory/session state is not shared with NPCs.

#### Option C: Rejected future deviation: deterministic explicit remember trigger in Core/Desktop
- Shape:
  - Add a non-model rule/regex trigger that saves memory when users say `记住` / `remember`.
  - Keep using the existing memory store format, but add a new deterministic capture seam outside the reference lifecycle.
- Pros:
  - Stronger product determinism for explicit remember utterances.
  - Easier to make stable under tests because it does not depend on tool-choice behavior.
- Cons:
  - Not reference parity with `external/hermes-agent-main`.
  - Deliberately introduces a product deviation and a second capture policy that future maintainers must own explicitly.
  - Expands scope and policy surface beyond the MVP alignment request.
  - Forbidden for this MVP by the latest requirement; any future version requires a new ADR and explicit product decision.

### Recommendation
Choose **Option B**.

Reasoning:
- It restores reference alignment after removing the previously proposed Core/Desktop deterministic trigger from the MVP path.
- It aligns with the latest product requirement that Haley, Penny, and other NPCs are equal agent instances with desktop agent capability parity.
- It changes the capability strategy from "private chat gets one memory tool" to "desktop and NPC agents share the same tool/skill registry, bound to different scoped services."
- It preserves all memory-boundary corrections already established in this plan:
  - NPC agents use the exact same registered capability surface as the desktop agent; transport/UI differences cannot remove, hide, or subset registered tools or skills.
  - NPC agents use the desktop agent's same or configurable tool-loop budget, with enough iterations for model-invoked tool calls such as `memory` and a final reply.
  - Curated memory recall comes from `BuiltinMemoryPlugin` snapshot/system-prompt content, not dynamic prefetch.
  - Reference-style external provider lifecycle remains optional best-effort enrichment, and `MemoryReviewService` is non-blocking and outside MVP acceptance whether wired or not.
- It keeps the MVP focused on the same lifecycle used by the reference project: model-invoked memory save, durable curated storage, and fresh-session builtin snapshot recall.
- It states clearly that any future deterministic non-model remember trigger would be a product decision to deviate from the reference, not part of reference parity.
- It keeps autonomy deterministic memory writes out of the proof because those task/session outcome writes do not match the reference memory schema, not because NPC capability should be restricted.

## Requirements Summary

### Functional requirements
1. NPC private chat must be able to persist durable memory for explicit stable user facts, such as player name.
2. Later private chat turns, including a fresh NPC agent instance or later session, must be able to answer from durable memory even when the original statement is no longer in the recent transcript window.
3. Durable memory must live in the existing curated memory store (`MEMORY.md` / `USER.md`) under the NPC namespace, not be inferred only from transcript recency.
4. MVP parity must use the existing Hermes/reference-style memory lifecycle rather than a new Core/Desktop deterministic remember/regex/rule trigger.
5. Private chat must use Hermes runtime memory infrastructure, not custom Stardew-bridge memory logic.
6. NPC agents must be assembled with the same tool and skill capability surface as the desktop agent.
7. The only intended differences between desktop and NPC agents are identity/persona/system prompt and scoped runtime state: NPC namespace, NPC-scoped memory, transcript, session, save, and profile.
8. NPC agents must not reuse the desktop singleton `Agent`, desktop global memory, or desktop session state.
9. NPC agents must use the desktop agent's same or configurable tool-loop budget, with effective `maxToolIterations >= 2` so a model-initiated tool call can be followed by a user-facing final reply in the same turn.
10. A fresh private-chat context/agent/session must see durable curated memory through `BuiltinMemoryPlugin` frozen snapshot/system-prompt content, not through recent transcript or dynamic `<memory-context>` prefetch.
11. If shared runtime-factory wiring is touched, it must preserve per-instance/per-namespace isolation while enabling desktop/NPC capability parity.

### Non-functional requirements
1. No new dependencies.
2. No rewrite of the whole agent loop.
3. Keep Stardew mod as transport/UI bridge only. This means the bridge does not own memory or capability assembly and is not an NPC capability filtering layer.
4. Preserve the previously fixed `TranscriptStore` cache behavior as prerequisite context only; do not reopen that fix.
5. Do not maintain a reduced NPC/private-chat tool subset as product policy; default target is desktop/NPC capability equivalence.
6. Transport/UI differences may only affect invocation adapters, presentation, context binding, or runtime parameters behind the same registered tool/skill contracts. They must not remove, hide, or subset NPC capabilities.
7. Do not add deterministic explicit-capture logic in Core/Desktop, Stardew mod, or bridge as part of this MVP.
8. Any future deterministic explicit-capture trigger would require explicit product approval and a new ADR because it is a deliberate deviation from the reference path.
9. Do not treat `NpcAutonomyLoop` deterministic memory writes as reference-parity evidence; handle them separately as schema/parity debt.

### Non-goals / non-decisions
1. This MVP does not refactor or remove existing `NpcAutonomyLoop` deterministic `Autonomy tick ...` curated-memory writes.
2. This MVP does not claim reference parity for autonomy deterministic task/session outcome memory behavior; that is separate future cleanup if desired.
3. This MVP does not use autonomy-produced curated memory as evidence that private-chat long-term memory is correct.
4. This MVP does not define a new security or permission model for dangerous tools beyond matching the desktop agent's existing capability policy.

### Assumed product behavior
1. Player identity facts such as `我叫远古牛哥` should land in the curated `user` memory surface or an equivalent durable store that is available to the NPC on later turns.
2. NPC-local notes remain separate from player-profile facts, but both are stored within the existing NPC runtime namespace unless a broader user-global design is later requested.
3. Haley/海莉, Penny/潘妮, and other NPCs should feel like equal Hermes agents with different persona/identity and scoped memory, not like special limited chat endpoints.

## Implementation Steps

### 1. Extract or reuse a shared desktop/NPC capability assembly source
- Likely files:
  - `src/runtime/NpcAgentFactory.cs`
  - Desktop agent construction files discovered during implementation
  - Existing tool/skill registry or registration helpers under `src/`
  - Possibly a small shared helper such as `AgentCapabilityProfile`, `ToolBundle`, or `AgentCapabilityRegistry`
- Work:
  - Locate the desktop agent tool and skill registration path.
  - Extract the reusable part into one shared capability assembly path, or make NPC construction call the existing registration helper directly if one already exists.
  - Keep naming conservative; `AgentCapabilityProfile`, `ToolBundle`, or a registration helper are acceptable shapes, but the important property is one source of truth.
  - Ensure the shared path can bind tools to caller-provided scoped services rather than desktop singletons.
  - Preserve desktop behavior while making NPC agents consume the same registry.
  - Do not add a deterministic remember/regex/rule trigger while doing this extraction.
- Acceptance checkpoint:
  - Desktop and NPC agent construction share the same tool/skill assembly source, and registered tool-name plus skill-tool sets match exactly.

### 2. Parameterize NPC runtime context creation around scoped collaborators, not capability restriction
- Likely files:
  - `src/runtime/NpcRuntimeContextFactory.cs`
  - `src/runtime/NpcRuntimeContextBundle` definition in the same file
  - Possibly `src/runtime/NpcNamespace.cs` if helper creation methods improve clarity
- Work:
  - Keep `NpcRuntimeContextFactory` responsible for NPC-scoped state: namespace, save/profile identity, transcript store, memory manager, plugin manager, soul service, context manager, and session-scoped services.
  - Instantiate `PluginManager` and register `BuiltinMemoryPlugin` against the NPC `MemoryManager`.
  - Use `PluginManager` as the source of `BuiltinMemoryPlugin` snapshot/system-prompt blocks for curated builtin recall in the NPC path.
  - Instantiate `HermesMemoryOrchestrator` and `TurnMemoryCoordinator` as needed for provider lifecycle, transcript/external dynamic recall, and pre-compress participation.
  - Keep `CuratedMemoryLifecycleProvider` prefetch-inert; curated memory must still enter through builtin snapshot/system-prompt pathways, not dynamic prefetch.
  - Instantiate `MemoryReviewService` only if the existing bundle/agent lifecycle already expects it; it is async/nudge-based enrichment, non-blocking, and outside MVP acceptance whether wired or not.
  - Pass `pluginManager` into `ContextManager` for builtin snapshot/system-prompt blocks.
  - Keep capability parity separate from state sharing: NPCs get the same capability surface, but all memory/transcript/session/profile collaborators must be NPC-scoped.
- Acceptance checkpoint:
  - `NpcRuntimeContextFactory` returns a bundle that can bind the shared desktop capability surface to NPC-scoped collaborators without sharing desktop global memory/session state or turning curated-memory recall into a dynamic-prefetch path.

### 3. Wire NPC agent construction to the shared capability surface and desktop-equivalent loop budget
- Likely files:
  - `src/runtime/NpcAgentFactory.cs`
  - `src/games/stardew/StardewPrivateChatOrchestrator.cs`
  - Possibly tests under `Desktop/HermesDesktop.Tests/Runtime/`
- Work:
  - Replace `Array.Empty<ITool>()` for private chat with the shared desktop-equivalent tool bundle bound to NPC-scoped services.
  - Register skill tools through the same registry used by the desktop agent.
  - Pass `pluginManager`, `turnMemoryCoordinator`, and `memoryReviewService` into `Agent` using the same lifecycle semantics as the desktop agent, but backed by NPC-scoped collaborators.
  - Preserve existing transcript/context/soul/persona wiring.
  - Ensure NPC private-chat agent configuration uses the desktop agent's same or configurable tool-loop budget; effective budget must be at least enough for a tool call plus final answer (`maxToolIterations >= 2`).
  - If a desktop tool appears transport/UI-bound, keep it in the shared registry and implement or bind the NPC adapter against NPC-scoped services, or mark the implementation blocked for architectural follow-up. Do not silently omit it, do not introduce an NPC/private-chat exclusion list, and do not treat omission as an allowed MVP difference.
  - Keep capture mediated by the reference tool contract: the model chooses `memory` because the `memory` schema/system prompt guidance says to save "remember this" and personal details. Do not add a separate Chinese/English rule trigger in Core/Desktop or the Stardew bridge.
- Acceptance checkpoint:
  - An NPC agent created for private chat has desktop-equivalent registered tools and skill tools, can complete model-invoked tool calls such as `memory`, and still emits a final answer in the same turn.

### 4. Confirm recall path uses fresh-agent builtin snapshot recall on subsequent turns
- Likely files:
  - `src/runtime/NpcRuntimeContextFactory.cs`
  - `src/search/TurnMemoryCoordinator.cs` only if a small glue fix is needed to preserve current transcript/external-provider behavior without pulling curated memory into dynamic prefetch
  - Tests in `Desktop/HermesDesktop.Tests/Runtime/` and `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
- Work:
  - Ensure later turns and fresh private-chat sessions receive curated memory via `BuiltinMemoryPlugin` frozen snapshot/system-prompt wiring.
  - Keep `PluginManager` responsible for supplying `BuiltinMemoryPlugin` snapshot/system-prompt blocks, and keep `TurnMemoryCoordinator` limited to transcript/external provider dynamic `<memory-context>` responsibilities.
  - Keep curated memory snapshot semantics intact; do not change `BuiltinMemoryPlugin` session-freeze behavior unless a test shows private chat breaks because of it.
  - Verify that mid-session writes update `USER.md` / `MEMORY.md` on disk immediately but do not mutate the current prompt snapshot; the remembered fact appears in the next fresh private-chat context/session.
  - Structure the proof so autonomy-generated curated entries, especially deterministic `Autonomy tick ...` writes, are excluded from the private-chat recall evidence path because they do not match the reference memory schema.
- Acceptance checkpoint:
  - A new private-chat session created after a prior remembered fact sees the durable memory in outbound prompt context via builtin snapshot content without needing the original transcript in the recent window, and without relying on `Autonomy tick ...` curated entries.

### 5. Add tiered regression tests for capability parity, tool-driven durability, same-turn completion, and fresh-agent recall
- Likely files:
  - `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeContextFactoryTests.cs`
  - `Desktop/HermesDesktop.Tests/Runtime/NpcAgentFactoryTests.cs`
  - New or expanded tests under:
    - `Desktop/HermesDesktop.Tests/Runtime/NpcPrivateChatMemoryTests.cs`
    - or `Desktop/HermesDesktop.Tests/Stardew/*`
    - plus selective additions to `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
- Work:
  - Add factory-level tests proving the NPC bundle binds collaborators from the NPC namespace while using the shared capability registry.
  - Must-have coverage:
    - a current-cast regression proving both current Stardew NPCs, `haley/default` and `penny/default`, are covered by the shared NPC agent path;
    - a capability-surface regression proving NPC registered tool names exactly match desktop agent registered tool names;
    - a skill-surface regression proving NPC skill tools/skill registry entries exactly match desktop agent skill tools;
    - an isolation regression proving NPC tools are bound to NPC-scoped `MemoryManager`, `PluginManager`, `TranscriptStore`, `SoulService`, `ContextManager`, session/save/profile, and namespace, not desktop singleton state;
    - a fake LLM/tool-call harness where input `我叫远古牛哥,你记住` causes the model path to choose `memory` under the reference tool contract, then `MemoryTool` persists the fact to curated memory (`USER.md` preferred for player identity, or `MEMORY.md` only if existing target semantics require it);
    - a persistence assertion against the NPC namespace memory files, specifically proving the remembered name lands in `USER.md` / `MEMORY.md` on disk under each tested NPC namespace;
    - a same-turn completion assertion proving the tool call is followed by a final user-facing answer because the NPC agent uses the desktop-equivalent/configurable tool-loop budget with effective `maxToolIterations >= 2`;
    - fresh private-chat context/agent/session follow-ups for both Haley and Penny where `我叫什么?` succeeds because durable curated memory is injected through `BuiltinMemoryPlugin` frozen snapshot/system-prompt content;
    - a recall isolation assertion proving the original naming turn is absent from recent transcript context for the follow-up, so the proof is not transcript recency;
    - an isolation assertion that the proof runs in an isolated NPC namespace/session without autonomy tick writes, or explicitly asserts no `Autonomy tick` curated memory participates in the recall proof;
    - a focused regression ensuring future changes do not introduce an NPC/private-chat exclusion list, safe-tool subset, transport/UI omission list, or reduced NPC tool/skill surface.
  - Optional tier:
    - add background-review or external-provider-sync coverage only if it can be made deterministic and the feature is already wired/configured; otherwise mark it as deferred/non-blocking, with `MemoryReviewService` explicitly outside MVP acceptance whether wired or not.
- Acceptance checkpoint:
  - Tests fail before the wiring change and pass after it.

## Testable Acceptance Criteria

1. NPC private chat registered tool names exactly match desktop agent registered tool names.
2. NPC skill tools / skill registry entries exactly match desktop agent skill tools.
3. NPC agents use the same capability assembly source as the desktop agent, but bind it to NPC-scoped `MemoryManager`, `PluginManager`, `TranscriptStore`, `SoulService`, `ContextManager`, session/save/profile, and namespace.
4. NPC agents do not reuse the desktop singleton `Agent`, desktop global memory, desktop transcript/session, or desktop profile state.
5. No NPC/private-chat exclusion list, safe-tool subset, transport/UI omission list, or reduced NPC tool/skill surface is introduced.
6. Both current Stardew NPCs, Haley/海莉 (`haley/default`) and Penny/潘妮 (`penny/default`), are covered by the same shared NPC agent construction path.
7. A deterministic fake LLM chooses the `memory` tool for `我叫远古牛哥,你记住` using the reference tool contract, and curated memory on disk (`USER.md` preferred for player identity, or `MEMORY.md` if existing semantics require it) persists the remembered name under the tested NPC namespace.
8. The same turn can still produce a final user-facing answer after the `memory` tool call because the effective NPC tool-iteration budget is at least `2` and preferably matches the desktop agent's configured budget.
9. The durable entry remains available after disposing the first private-chat agent and creating a fresh one for the same NPC/save/profile.
10. On later private-chat turns asking `我叫什么?`, both Haley and Penny paths can receive builtin memory snapshot/system-prompt content that identifies the player as `远古牛哥` when the remembered fact was saved in that NPC's namespace.
11. The later answer succeeds even when the original naming turn is not present in the recent transcript window used for context assembly.
12. The recall proof uses isolated NPC namespaces/sessions without autonomy tick writes, or explicitly demonstrates that no `Autonomy tick ...` curated memory entry participates in the private-chat recall result.
13. Transcript history remains in transcript storage, but the proof of correctness is durable curated memory files plus later builtin snapshot recall, not transcript recency alone.
14. Stardew bridge/mod code remains transport-only; no custom deterministic memory persistence trigger or capability filtering layer is added there as part of the MVP alignment work.
15. `PluginManager` supplies `BuiltinMemoryPlugin` snapshot/system-prompt blocks, while `HermesMemoryOrchestrator` / `TurnMemoryCoordinator` remain for provider lifecycle, transcript/external/provider recall, and pre-compress participation only; curated built-in memory parity tests continue to prove no dynamic prefetch for curated memory.
16. `MemoryReviewService`, if present, is non-blocking enrichment and remains outside MVP acceptance whether wired or not.

## Risks and Mitigations

### Risk 1: Reference-aligned capture depends on model/tool behavior
- Mitigation:
  - Strict reference parity means capture is mediated by model/tool behavior, not a Core/Desktop deterministic trigger.
  - Mitigate with reference-style `memory` tool schema/system-prompt guidance that tells the model to save "remember this" requests and personal details such as names/preferences.
  - Use deterministic fake-LLM/tool-call tests so the intended save path is verifiable and non-flaky.
  - Any future non-model trigger requires a new ADR and explicit product decision because it is a deliberate deviation from the reference.

### Risk 2: Player facts are written to the wrong target (`memory` vs `user`)
- Mitigation:
  - Decide and document the target in tests. Prefer `user` for player identity/profile facts, since that matches existing semantics.
  - Assert the concrete file written (`USER.md` vs `MEMORY.md`) in regression tests.

### Risk 3: Duplicate or stale memory injection causes confusing prompts
- Mitigation:
  - Reuse `PluginManager` + `BuiltinMemoryPlugin` instead of adding manual prompt concatenation.
  - Preserve existing frozen-snapshot semantics and verify curated memory arrives through the builtin snapshot path while `TurnMemoryCoordinator` continues to own only transcript/external dynamic context.

### Risk 4: Background review cadence is too slow or too ephemeral for private-chat correctness
- Mitigation:
  - Treat review as non-blocking in MVP, with `MemoryReviewService` explicitly outside MVP acceptance whether wired or not.
  - Only expand scope later if product requirements explicitly demand configured private-chat review cadence.

### Risk 5: Shared capability extraction accidentally shares desktop state with NPCs
- Mitigation:
  - Separate capability registration from scoped service binding.
  - Add tests proving NPC tools use NPC-scoped memory/transcript/session/save/profile/namespace.
  - Do not pass desktop singleton agent, desktop global memory, or desktop session objects into NPC construction.

### Risk 6: Scope creeps into deterministic-trigger product deviation
- Mitigation:
  - Restrict memory-capture changes to reference-style model/tool behavior, builtin snapshot recall, scoped collaborator binding, capability parity tests, and focused memory durability tests.
  - Explicitly reject Core/Desktop remember/regex/rule capture and Stardew mod/bridge memory logic for this MVP.
  - Treat the prior `TranscriptStore` cache fix as already settled prerequisite context.

### Risk 7: Existing autonomy curated-memory writes are mistaken for private-chat memory correctness
- Mitigation:
  - Treat `NpcAutonomyLoop` deterministic `Autonomy tick ...` curated-memory writes as known reference-parity debt because those task/session outcome writes do not match the reference memory schema.
  - Require tests to use an isolated NPC namespace/session without autonomy tick writes, or to explicitly assert that no `Autonomy tick ...` curated memory participates in the private-chat recall proof.
  - Defer any autonomy-memory cleanup/refactor to a separate future plan/ADR.

## Verification Commands

Run the narrowest relevant tests first, then broaden only if the touched area justifies it:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcRuntimeContextFactoryTests|FullyQualifiedName~NpcAgentFactoryTests|FullyQualifiedName~NpcPrivateChatMemory"
```

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~MemoryParityTests|FullyQualifiedName~MemoryToolTests"
```

Verification must explicitly confirm that NPC registered tools and skill tools exactly match the desktop agent capability surface, with no NPC/private-chat exclusion list, safe-tool subset, transport/UI omission list, or reduced NPC tool/skill surface. It must also confirm NPC `MemoryTool` writes land in NPC-scoped `USER.md` / `MEMORY.md` and that fresh NPC agents recall through `BuiltinMemoryPlugin` snapshot/system-prompt content. If the private-chat regression test lives under a different class name, update the filter to that class. For final confidence after local green:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## ADR Draft

### Title
Adopt desktop/NPC capability parity with reference-aligned Hermes memory lifecycle for Stardew NPC private chat

### Status
Proposed

### Context
- NPC private chat currently persists transcript but does not provide verifiable long-term memory recall.
- User-required correctness includes explicit remember utterances such as `我叫远古牛哥,你记住` durably saving and being recallable in later private-chat turns.
- The latest product requirement is that desktop agent and NPC agents are equal agent instances. Current NPCs Haley/海莉 and Penny/潘妮, plus future NPCs, should receive the same complete tool and skill surface as the desktop agent.
- The only intended differences are identity/persona/system prompt and scoped runtime state: NPC namespace, NPC-scoped memory/transcript/session/save/profile.
- Current private chat creates a fresh context/agent per `ReplyAsync`, passes `Array.Empty<ITool>()`, and sets `maxToolIterations: 1`, so the model cannot call tools such as `memory` and then continue to a final answer.
- The repo already contains the architectural pieces needed for durable curated memory:
  - `MemoryManager`
  - `MemoryTool`
  - `BuiltinMemoryPlugin`
  - `PluginManager`
  - `TurnMemoryCoordinator`
  - `HermesMemoryOrchestrator`
  - `MemoryReviewService`
- The current private-chat runtime path omits these collaborators by constructing the agent with no tools and by not passing memory lifecycle services through the NPC runtime bundle/factory path.
- `NpcRuntimeContextFactory` is shared by private chat and autonomy/debug flows, so new wiring at that boundary must preserve per-NPC scoped state and not accidentally share desktop singleton/session state.
- Reference evidence from `external/hermes-agent-main` shows built-in memory capture is model-invoked via the `memory` tool plus schema guidance for "remember this" and user personal details; it does not use a Core-side deterministic remember trigger.
- Reference `MEMORY.md` / `USER.md` writes are durable immediately, but builtin system-prompt recall uses a frozen snapshot that refreshes on the next session/new agent.
- `run_agent.py` bridges successful built-in `memory` writes to external providers via `on_memory_write`; provider lifecycle hooks such as `sync_turn`, `prefetch`, and `queue_prefetch` are additive provider behavior, not hardcoded capture policy.
- Background memory review in the reference is async/nudge-based enrichment after responses and still works through the `memory` tool; it is not an MVP correctness guarantee.
- The reference project separates session transcript, curated memory files, optional external memory providers, and turn-time memory context injection. The Hermes Desktop path should match that separation, including keeping curated memory out of dynamic `<memory-context>` prefetch.
- The current autonomy path still writes deterministic `Autonomy tick ...` decision responses into curated memory. That is known reference-parity debt because those deterministic task/session outcome writes do not match the reference memory schema, but it is explicitly outside this MVP and must not be used as private-chat memory proof.

### Decision
Wire Stardew NPC private chat into the existing Hermes/reference-aligned curated-memory lifecycle and desktop-equivalent capability model by:
- extracting or reusing a shared desktop/NPC capability assembly source for tools and skill tools;
- binding that shared capability surface to NPC-scoped `MemoryManager`, `PluginManager`, `TranscriptStore`, `SoulService`, `ContextManager`, session/save/profile, and namespace;
- not reusing the desktop singleton `Agent`, desktop global memory, desktop transcript/session, or desktop profile state;
- extending `NpcRuntimeContextFactory` to build the plugin and memory collaborators needed for builtin snapshot recall and normal memory lifecycle support;
- extending `NpcAgentFactory` to pass the existing collaborators into `Agent` while preserving the correct recall boundary;
- using the desktop agent's same or configurable tool-loop budget for NPC agents, with effective `maxToolIterations >= 2`;
- relying on reference-style `memory` tool schema/system-prompt guidance so explicit remember-worthy facts flow through the model-invoked `MemoryTool` path;
- verifying desktop/NPC tool and skill parity, tool-driven durable save, same-turn final reply continuity, and fresh-agent snapshot recall with targeted tests.

Non-decisions:
- Do not add deterministic remember/regex/rule capture in Core/Desktop.
- Do not put memory persistence logic in the Stardew mod/bridge.
- Do not use dynamic `<memory-context>` prefetch for curated built-in memory.
- Do not treat background review or external providers as MVP correctness guarantees.
- Do not refactor or "fix" existing `NpcAutonomyLoop` deterministic `Autonomy tick ...` curated-memory writes in this MVP.
- Do not define a new reduced NPC capability subset as product policy.

### Drivers
1. NPC agents must be capability peers of the desktop agent.
2. The MVP must align with the reference project rather than add a new deterministic capture policy.
3. Durable recall must work across fresh private-chat sessions, not only via transcript recency.
4. The repo already has the needed memory architecture; duplicating it in the Stardew bridge would create divergence.
5. The change must stay dependency-free, testable, and isolated by scoped service binding.

### Alternatives Considered

#### Alternative 1: Minimal memory-only exposure
- Rejected because the user explicitly rejected limiting Haley/NPC private chat to `MemoryTool`; NPC agents must have desktop agent capability parity.

#### Alternative 2: Deterministic Core/Desktop explicit remember trigger
- Rejected and forbidden for this MVP because the reference project does not implement a non-model trigger for `记住` / `remember`; adopting one would be a deliberate future product deviation requiring a new ADR and explicit product decision.

#### Alternative 3: Stardew mod/bridge memory persistence
- Rejected because the Stardew mod/bridge must remain transport-only. Putting memory logic there would diverge from the existing Hermes runtime architecture and from the reference project lifecycle.

### Why Chosen
- Reuses existing, already-tested components.
- Preserves clean boundaries between transcript/dynamic memory context and curated durable memory snapshot recall.
- Matches the reference project’s built-in memory model instead of introducing a new Core-side policy.
- Makes NPC agents true capability peers of the desktop agent while preserving scoped memory/session isolation.
- Keeps the MVP honest about its dependency on model/tool behavior while still making that behavior testable through a controlled harness.

### Consequences

#### Positive
- NPC private chat gains durable memory with parity to the broader Hermes architecture.
- Haley/海莉 and Penny/潘妮 gain the same tool/skill affordances as the desktop agent while keeping their own identity and scoped state; the same rule extends to future NPCs.
- Explicit remember utterances can be handled through the same model/tool contract used by the reference path.
- Future NPC memory features can build on one path instead of maintaining private-chat-specific logic.
- Verification becomes straightforward through capability-surface comparisons, memory-file assertions, and later-turn recall tests.

#### Negative
- Runtime bundle and agent factory signatures may become broader.
- Shared capability assembly must be carefully separated from scoped service binding.
- MVP correctness remains sensitive to reference schema/system-prompt/tool-call behavior, so deterministic fake-LLM tests become part of the contract.
- Existing autonomy `Autonomy tick ...` curated-memory writes remain in the product and must be kept out of private-chat proof scenarios until a separate cleanup effort addresses them.

### Follow-ups
1. If product scope later requires deterministic non-model capture for explicit remember utterances, treat it as a separately approved product deviation from the reference path with its own ADR and tests.
2. After this lands, evaluate whether NPC capability parity should also be applied or audited in other NPC interaction modes beyond private chat.
3. If desired, create a separate reference-parity cleanup plan for autonomy curated-memory behavior, including whether deterministic `Autonomy tick ...` writes should be reduced, reclassified, or removed.

## Available-Agent-Types Roster

The requested execution surface is **5.5 agent**. For this plan, use Codex native `default` subagents with explicit `model: "gpt-5.5"` and role instructions in the prompt when a 5.5-backed lane is required. Do not substitute fixed-role native agents whose model is pinned below 5.5 unless the user explicitly relaxes this requirement.

- `5.5 planner` (`gpt-5.5`, reasoning `medium`): revise consensus plan, keep scope narrow, and maintain acceptance/test alignment.
- `5.5 architect` (`gpt-5.5`, reasoning `high`): validate memory-boundary correctness and option framing.
- `5.5 executor` (`gpt-5.5`, reasoning `high`): implement shared desktop/NPC capability assembly, NPC-scoped binding, and builtin snapshot recall after plan approval.
- `5.5 test-engineer` (`gpt-5.5`, reasoning `medium`): design must-have and optional test coverage and verification filters, especially desktop/NPC tool-skill parity, tool-driven save, same-turn completion, and fresh-agent recall coverage.
- `5.5 verifier` (`gpt-5.5`, reasoning `high`): confirm claimed behavior from test evidence and guard against scope drift.
- `explore` (`gpt-5.3-codex-spark`, reasoning `low`): optional fast read-only repo lookup only; do not use it as the decision-making or implementation authority for this 5.5-required plan.

## Follow-up Staffing Guidance

### Ralph follow-up
- Use `$ralph` / `omx ralph ...` only if implementation needs a persistent single-owner loop for fix-test-verify after this plan is approved.
- Suggested lane: 5.5 executor lead with `high` reasoning, plus periodic 5.5 verifier checks at major checkpoints.
- Best fit: one owner implementing shared desktop/NPC capability assembly, NPC-scoped runtime/context/agent binding, reference schema/system-prompt contract alignment, and iterating until capability-parity plus private-chat memory tests are green.

### Team follow-up
- Recommended team shape: `3` workers.
- Lane 1: 5.5 executor with `high` reasoning for shared capability assembly, runtime/context/agent wiring, NPC-scoped binding, and reference schema/system-prompt contract alignment.
- Lane 2: 5.5 test-engineer with `medium` reasoning for must-have desktop/NPC tool-skill parity, tool-driven durability, same-turn completion, and fresh-agent snapshot recall tests.
- Lane 3: 5.5 verifier with `high` reasoning for acceptance-criteria mapping, scoped-state isolation checks, and final evidence review.
- If a dedicated 5.5 test-engineer lane is unavailable, use a second 5.5 executor-style default agent for tests and keep the 5.5 verification lane separate.

## Suggested Reasoning Levels By Lane

- Plan revision / consensus: `medium`
- Boundary validation / architectural challenge: `high`
- Runtime implementation: `high`
- Test design and focused harness work: `medium`
- Final verification and regression audit: `high`

## Explicit Team Launch Hints

- Minimal coordinated launch, requiring 5.5-backed workers or 5.5 default agents with role prompts:
  ```powershell
  omx team 3:executor --model gpt-5.5 "Implement approved RALPLAN for NPC private chat long-term memory: NPC agents must have desktop agent tool/skill capability parity, shared capability assembly, NPC-scoped memory/transcript/session/save/profile binding, maxToolIterations>=2 or desktop-equivalent loop budget, builtin snapshot recall, must-have durability/recall tests, no deterministic remember trigger, no Stardew bridge memory logic, no new dependencies"
  ```
- Skill-form equivalent:
  ```text
  $team 3 --model gpt-5.5 "Implement approved RALPLAN for NPC private chat long-term memory: NPC agents must have desktop agent tool/skill capability parity, shared capability assembly, NPC-scoped memory/transcript/session/save/profile binding, maxToolIterations>=2 or desktop-equivalent loop budget, builtin snapshot recall, must-have durability/recall tests, no deterministic remember trigger, no Stardew bridge memory logic, no new dependencies"
  ```
- If you want a single-owner path instead:
  ```powershell
  omx ralph --model gpt-5.5 "Execute approved NPC private chat long-term memory RALPLAN with desktop/NPC capability parity and fix-test-verify loop"
  ```

## Team Verification Path

1. `explore` or leader confirms the implementation does not add a deterministic Core/Desktop remember trigger or Stardew mod/bridge memory logic for MVP parity, and still keeps curated-memory recall on the builtin snapshot/system-prompt path rather than dynamic prefetch.
2. Implementation lane verifies NPC agents use the same tool/skill capability assembly source as the desktop agent and have exact registered tool-name plus skill-tool parity. Transport/UI differences may only affect invocation adapters, presentation, context binding, or runtime parameters behind the same registered contracts; they must not remove, hide, or subset capabilities.
3. Implementation lane verifies NPC tools are bound to NPC-scoped memory/transcript/session/save/profile/namespace services and do not reuse desktop singleton/global state.
4. Test lane runs must-have regression coverage for:
   - desktop/NPC registered tool-name parity;
   - desktop/NPC skill-tool parity;
   - current-cast coverage for both `haley/default` and `penny/default`;
   - tool-driven durable write to curated memory from `我叫远古牛哥,你记住`;
   - same-turn tool call followed by final reply for the still-supported model-initiated path;
   - fresh-agent snapshot recall without transcript recency;
   - isolation from `Autonomy tick ...` curated-memory evidence, either by isolated namespace/session or explicit exclusion assertions.
5. Optional background-review or external-provider coverage runs only if deterministic and already configured; otherwise it is reported as deferred/best-effort.
   `MemoryReviewService` remains non-blocking and outside MVP acceptance whether wired or not.
6. Verifier lane checks acceptance criteria against actual test evidence, then broadens to the full targeted desktop test suite only if the touched area justifies it.
