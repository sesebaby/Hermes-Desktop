# Test Spec: Dream Reference Memory Alignment

## Test Strategy

Use test-first implementation. Each production change must be preceded by a failing test that describes the desired reference-aligned behavior.

## Required Red Tests

1. `HermesMemoryOrchestrator_PreCompressAll_CallsParticipantsInOrder`
   - Create recording compression participants.
   - Assert the orchestrator calls them in registration order with the evicted message block and session id.
   - Expected initial failure: method/interface does not exist.

2. `HermesMemoryOrchestrator_PreCompressAll_ProviderFailuresAreNonFatal`
   - Add one failing participant and one healthy participant.
   - Assert the healthy participant still runs and no exception escapes.
   - Expected initial failure: method/interface does not exist.

3. `CuratedMemoryLifecycleProvider_Prefetch_IsInert`
   - Create a curated-memory lifecycle adapter around a populated `MemoryManager`.
   - Assert `PrefetchAsync` returns null/empty and does not include `MEMORY.md`/`USER.md` content.
   - Expected initial failure: adapter does not exist.

4. `TurnMemoryCoordinator_DoesNotInjectCuratedMemoryThroughDynamicRecall`
   - Compose transcript recall provider plus curated-memory lifecycle adapter.
   - Assert `<memory-context>` includes transcript recall only, not curated-memory snapshot text.
   - Expected initial failure: adapter does not exist.

5. `ContextManager_PreCompressesMemoryBeforeSummarizingEvictedMessages`
   - Use a fake compression participant and fake summarizer client.
   - Assert the handoff event occurs before summary generation.
   - Expected initial failure: `ContextManager` has no memory handoff dependency.

6. `BuiltinMemoryPlugin_FrozenSnapshot_RemainsSingleCuratedInjectionPath`
   - Keep or extend current frozen snapshot tests.
   - Assert mid-session curated-memory writes do not alter the active prompt snapshot until pre-compression/session boundary refresh.

7. `DesktopStartup_DoesNotRegisterAutoDreamServiceByDefault`
   - Static/wiring assertion that runtime startup does not register or instantiate `AutoDreamService`.

8. Dreamer regression filter
   - Run:
     `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --filter "FullyQualifiedName~Dreamer|FullyQualifiedName~RssFetcher|FullyQualifiedName~InsightsDreamer"`

## Verification Commands

Targeted during development:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --filter "FullyQualifiedName~MemoryParityTests|FullyQualifiedName~MemoryToolTests"
```

Dreamer regression:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --filter "FullyQualifiedName~Dreamer|FullyQualifiedName~RssFetcher|FullyQualifiedName~InsightsDreamer"
```

Final build/test:

```powershell
dotnet build .\HermesDesktop.slnx --no-restore
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj
```

## Reference Recheck

After implementation, compare against `.omx/plans/reference-matrix-dream-reference-memory-alignment.md` and update row states with evidence from code/tests.

