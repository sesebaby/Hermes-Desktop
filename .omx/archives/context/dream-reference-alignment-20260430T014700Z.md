# Context Snapshot: Dream Reference Memory Alignment

## Task Statement

Continue and complete `.omx/plans/dream-reference-alignment-ralplan-20260430.md` under the Ralph workflow.

## Desired Outcome

Hermes-Desktop aligns its dream-adjacent long-term memory behavior with the reference plan while preserving current Dreamer behavior:

- curated memory stays authoritative through `MemoryManager`
- curated memory remains injected only through the frozen session plugin snapshot in this pass
- transcript/session recall remains the dynamic provider-prefetch lane
- context compression invokes a memory handoff before summarization/replacement
- `AutoDreamService` remains dormant by default
- relevant memory, context, startup, and Dreamer tests pass

## Known Facts / Evidence

- Planning gate artifacts exist:
  - `.omx/plans/prd-dream-reference-memory-alignment.md`
  - `.omx/plans/test-spec-dream-reference-memory-alignment.md`
  - `.omx/plans/dream-reference-alignment-ralplan-20260430.md`
- The current worktree already contains memory-alignment edits:
  - `src/memory/IMemoryCompressionParticipant.cs`
  - `src/memory/CuratedMemoryLifecycleProvider.cs`
  - `src/memory/HermesMemoryOrchestrator.cs`
  - `src/Context/ContextManager.cs`
  - `Desktop/HermesDesktop/App.xaml.cs`
  - `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
- The worktree also contains unrelated-looking task/todo edits and deleted `skills/errors/*` files; these must not be reverted unless they directly block verification.
- `docs/shared/agent-tiers.md` was not found.
- `.github/instructions/*.instructions.md` referenced by `Desktop/HermesDesktop/AGENTS.md` were not found.
- No matching historical error entries were found in `openspec/errors` or `D:\GitHubPro\AllGameInAI\openspec\errors`.

## Constraints

- Do not change Dreamer behavior or activate `AutoDreamService` by default.
- Do not introduce new dependencies.
- Keep curated memory out of dynamic `PrefetchAsync` / `<memory-context>` injection.
- `ContextManager` memory handoff must run before plugin pre-compression and summary generation.
- Existing uncommitted user/previous-agent changes must be preserved.
- Verification must include targeted memory tests, Dreamer regression filter, build, and full test pass where feasible.

## Unknowns / Open Questions

- Whether the existing in-progress edits compile.
- Whether current tests fully cover frozen snapshot semantics and startup non-activation.
- Whether unrelated task/todo edits introduce build or full-test failures outside this plan's scope.

## Likely Codebase Touchpoints

- `src/memory/IMemoryProvider.cs`
- `src/memory/IMemoryCompressionParticipant.cs`
- `src/memory/CuratedMemoryLifecycleProvider.cs`
- `src/memory/HermesMemoryOrchestrator.cs`
- `src/search/TurnMemoryCoordinator.cs`
- `src/Context/ContextManager.cs`
- `src/plugins/BuiltinMemoryPlugin.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
- `.omx/plans/reference-matrix-dream-reference-memory-alignment.md`
