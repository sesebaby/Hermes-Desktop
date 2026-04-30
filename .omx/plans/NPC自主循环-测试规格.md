# Test Spec: Single NPC Agent-Driven Stardew Autonomy Loop

## Strategy

Use test-first delivery. Tests must prove autonomy, isolation, and typed bridge behavior without requiring Stardew Valley, SMAPI, or a live bridge.

The most important negative test: events are facts only. No event ingestion path may call the LLM or issue a Stardew command.

## Required Tests

1. `NpcAutonomyLoop_RunOneTick_ObservesBeforeDecision`
   - Arrange fake observer, fake LLM/agent, fake command service.
   - Assert observation is called before any decision/tool action.

2. `NpcAutonomyLoop_EventFact_DoesNotDriveAgent`
   - Inject a fake bridge/game event.
   - Assert no LLM completion, no `Agent.ChatAsync`, no `StardewCommandService.SubmitAsync`, no move, and no speak.
   - Assert the event is available as an observation fact for the next tick.

3. `NpcAutonomyLoop_UsesRuntimeLocalContextManagerAndPromptBuilder`
   - Build a Haley runtime namespace.
   - Assert context preparation goes through runtime-local `ContextManager` / `PromptBuilder`.
   - Assert no custom Stardew prompt assembler is required or used.

4. `NpcAutonomyLoop_LoadsHaleyPersonaFromPackAndNamespace`
   - Use Haley default persona pack.
   - Assert `SOUL.md`, facts, voice/boundaries, and skills are resolved into the NPC-local context path.

5. `NpcAutonomyLoop_RegistersOnlyNpcSafeTools`
   - Inspect tool definitions available to the NPC agent.
   - Assert global desktop tools are absent.
   - Assert only allowed Stardew/NPC-local tools are present.

6. `NpcAutonomyLoop_MoveDecision_UsesStardewCommandService`
   - Fake model requests move.
   - Assert `GameActionType.Move` is submitted through `StardewCommandService`.
   - Assert `commandId`, `traceId`, and idempotency context are recorded.

7. `NpcAutonomyLoop_SpeakDecision_UsesStardewCommandService`
   - Fake model requests speak.
   - Assert `GameActionType.Speak` is submitted through `StardewCommandService`.
   - Assert the result is recorded.

8. `NpcAutonomyLoop_PollsLongRunningCommandUntilTerminalOrLimit`
   - Fake status sequence: queued -> running -> completed.
   - Assert polling stops at terminal status.
   - Add failure/blocked variant if implementation supports it in the same slice.

9. `NpcAutonomyLoop_BridgeUnavailable_WritesNoOpTrace`
   - Fake discovery/observer unavailable.
   - Assert no action command is submitted.
   - Assert no-op/paused trace is written with failure reason.

10. `NpcAutonomyLoop_CompletedTick_WritesTraceActivityAndMemory`
   - Fake successful meaningful action.
   - Assert trace/log entry is under the NPC namespace.
   - Assert `LastTraceId` is updated.
   - Assert NPC-local memory write is attempted or completed.

11. `NpcAutonomyLoop_EnforcesBudgetAndToolIterationLimit`
   - Use `NpcAutonomyBudget`.
   - Assert iteration/concurrency limit prevents runaway behavior.

12. `NpcRuntimeSupervisor_StartStop_PreservesSnapshotSemantics`
   - Extend existing supervisor tests.
   - Assert lifecycle state and trace id remain visible through snapshots.

## Regression Tests

- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyBudgetTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/ResourceClaimRegistryTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandContractTests.cs`
- `Desktop/HermesDesktop.Tests/GameCore/NpcPackLoaderTests.cs`
- `Desktop/HermesDesktop.Tests/GameCore/NpcPackManifestTests.cs`

## Verification Commands

Targeted:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:Platform=x64 --filter "FullyQualifiedName~NpcAutonomyLoop|FullyQualifiedName~AutonomyBoundary|FullyQualifiedName~EventFact"
```

Runtime and Stardew regression:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:Platform=x64 --filter "FullyQualifiedName~Runtime|FullyQualifiedName~Stardew|FullyQualifiedName~GameCore"
```

Final:

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:Platform=x64
```
