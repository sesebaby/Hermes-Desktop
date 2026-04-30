# PRD: Dream Reference Memory Alignment

## Objective

Bring Hermes-Desktop's dream-adjacent long-term memory behavior closer to `external/hermes-agent-main` by closing the lifecycle gaps around curated memory, transcript recall, and pre-compression handoff while preserving the existing Dreamer background loop.

## User Outcome

Hermes should keep the current Dreamer feature working, but the memory layer behind conversations should behave more like the reference project:

- curated memory remains persisted and injected as a frozen session snapshot
- transcript/session recall remains a separate dynamic recall channel
- context compression gets a memory handoff before older context is summarized or dropped
- no dormant `AutoDreamService` starts by default
- Dreamer does not become the authoritative memory writer

## Scope

In scope:

- Add an explicit memory compression handoff contract.
- Wire the handoff through `HermesMemoryOrchestrator`.
- Invoke memory handoff from the live `ContextManager` compaction path before summarization.
- Add a curated-memory lifecycle participant that adapts `MemoryManager` without dynamically injecting curated memory through `PrefetchAsync`.
- Preserve existing plugin-system-prompt curated-memory injection.
- Add tests proving ordering, non-duplication, and no `AutoDreamService` startup activation.
- Re-run Dreamer tests and compare final behavior against the reference matrix.

Out of scope:

- Replacing Dreamer.
- Making Dreamer produce memory writes.
- Enabling `AutoDreamService` by default.
- Making curated memory per-turn dynamic recall.
- Changing `memory` or `session_search` tool responsibilities.
- Adding new third-party dependencies.

## Requirements

1. `MemoryManager` remains the durable curated-memory authority.
2. `BuiltinMemoryPlugin` remains the only curated-memory prompt injector in this pass.
3. `TurnMemoryCoordinator` remains the dynamic transcript recall injector.
4. A curated-memory lifecycle participant may participate in turn and compression lifecycle events, but must not return curated-memory content from `PrefetchAsync`.
5. `HermesMemoryOrchestrator` must offer a best-effort pre-compression handoff that isolates participant failures.
6. `ContextManager` must call the handoff before summarizing/replacing evicted messages.
7. Existing plugin pre-compression behavior must still run.
8. `DreamerService` startup and current tests must remain unchanged.
9. `AutoDreamService` must remain unregistered by default.

## Acceptance Criteria

1. Tests prove compression handoff runs before summary replacement.
2. Tests prove failing memory handoff participants are non-fatal.
3. Tests prove curated memory does not enter `<memory-context>` through provider prefetch.
4. Tests prove plugin frozen snapshot semantics still hold.
5. Tests prove startup wiring registers `DreamerService` behavior as before and does not register/start `AutoDreamService`.
6. Existing Dreamer-focused tests pass.
7. Final reference matrix marks the implemented rows aligned or explicitly explains any remaining partial status.

## Approved Plan

This PRD implements the approved plan at:

- `.omx/plans/dream-reference-alignment-ralplan-20260430.md`
