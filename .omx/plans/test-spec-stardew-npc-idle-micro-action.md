# Test Spec: Stardew NPC Idle Micro Action

## Objective

Prove that idle micro actions are implemented through one explicit path, remain whitelist-only, preserve bridge/host boundaries, and do not regress private chat or command lifecycle behavior.

## Required Test Areas

### Runtime contract

- `NpcLocalActionIntentTests.TryParse_IdleMicroAction_AcceptsWhitelistedFields`
- `NpcLocalActionIntentTests.TryParse_IdleMicroAction_RejectsSpeechDestinationTargetAndRawAnimationFields`
- `NpcLocalActionIntentTests.TryParse_IdleMicroAction_RejectsUnknownKind`
- `NpcLocalActionIntentTests.TryParse_IdleMicroAction_RejectsAnimationAliasOutsideAllowlistContractShape`

### Local executor

- `NpcLocalExecutorRunnerTests.ExecuteAsync_WithIdleMicroAction_ExposesOnlyStardewIdleMicroAction`
- `NpcLocalExecutorRunnerTests.ExecuteAsync_WithIdleMicroAction_WithoutDelegation_Blocks`
- `NpcLocalExecutorRunnerTests.ExecuteAsync_WithIdleMicroAction_DoesNotExposeSpeakMoveOrPrivateChatTools`

### Tool factory / command service

- `StardewNpcToolFactoryTests.CreateLocalExecutorTools_IncludesIdleMicroActionToolOnlyInExecutorSurface`
- `StardewCommandServiceTests.SubmitIdleMicroAction_UsesActionIdleMicroActionRoute`
- `StardewCommandServiceTests.SubmitIdleMicroAction_MapsDisplayedSkippedBlockedInterrupted`

### Bridge route / DTO

- `BridgeIdleMicroActionRouteRegressionTests.CommandContractsExposeActionIdleMicroAction`
- `BridgeIdleMicroActionRouteRegressionTests.HttpHostHandlesActionIdleMicroAction`
- `BridgeIdleMicroActionRouteRegressionTests.DtoFieldsMatchIdleMicroActionContract`

### Bridge queue lifecycle

- `BridgeIdleMicroActionQueueTests.Enqueue_WhenNpcVisibleAndIdle_Displays`
- `BridgeIdleMicroActionQueueTests.Enqueue_WhenPlayerNotVisible_SkipsNotVisible`
- `BridgeIdleMicroActionQueueTests.Enqueue_WhenNpcMoving_BlocksNpcBusy`
- `BridgeIdleMicroActionQueueTests.Enqueue_WhenMenuOrEventOpen_Blocks`
- `BridgeIdleMicroActionQueueTests.Enqueue_IdleAnimationAliasNotAllowed_BlocksAnimationNotAllowed`
- `BridgeIdleMicroActionQueueTests.ActiveIdleCommand_IsInterruptedByMove`
- `BridgeIdleMicroActionQueueTests.ActiveIdleCommand_IsInterruptedByWarpOrRemove`
- `BridgeIdleMicroActionQueueTests.ActiveIdleCommand_IsInterruptedByRuntimeDrainOrClear`
- `BridgeIdleMicroActionQueueTests.NewIdleCommand_OverridesOldIdleCommand`
- `BridgeIdleMicroActionQueueTests.IdleAnimationOnce_RestoresIdleFrameAfterCompletion`

### Overlay / private chat regression

- `RawDialogueDisplayRegressionTests.IdleMicroAction_DoesNotEmitPrivateChatReplyClosed`
- `RawDialogueDisplayRegressionTests.PrivateChatReplyClosed_RemainsPrivateChatChannelOnly`
- `RawDialogueDisplayRegressionTests.IdleMicroAction_UsesBubbleChannelIdleMicro`

## Verification Commands

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalActionIntentTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalExecutorRunnerTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewCommandServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests"
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~IdleMicro|FullyQualifiedName~RawDialogueDisplayRegressionTests|FullyQualifiedName~BridgeMoveCommandQueueRegressionTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
```

## Pass Criteria

1. Runtime parser accepts only the whitelist shape.
2. Local executor exposes exactly one idle tool path for idle micro actions.
3. Command service, route, DTO, handler, and queue agree on the new route and payload.
4. Bridge emits normalized `displayed/skipped/blocked/interrupted` semantics.
5. Private chat regression tests prove idle micro actions do not reuse private chat close events.
6. Interrupt cleanup works for move, warp/remove, menu/event, visibility loss, and runtime drain/clear.
