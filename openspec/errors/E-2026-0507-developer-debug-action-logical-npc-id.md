# E-2026-0507-developer-debug-action-logical-npc-id

- id: E-2026-0507-debug-action-logical-npc-id
- title: Developer debug actions sent logical NPC ids without runtime body binding
- status: active
- updated_at: 2026-05-07
- keywords: [stardew, developer-page, debug-action, manual_debug, speak, invalid_target, BodyBinding, TargetEntityId]
- trigger_scope: [desktop, stardew, runtime, bugfix, diagnostics]

## Symptoms

- Developer page "发送调试台词" returned `invalid_target` for Haley.
- SMAPI logs showed the manual debug action as `action_speak_failed npc=haley ... error=invalid_target`.
- Nearby autonomy/private-chat speech succeeded for the same NPC as `action_speak_completed npc=Haley`, and "放到镇上" also succeeded after resolving the known alias.

## Root Cause

- Developer page debug actions used `NpcRuntimeSnapshot.NpcId`, which is the logical runtime id such as `haley`.
- Normal NPC tool/private-chat paths submit `GameAction.BodyBinding` so `StardewCommandService` can send `BodyBinding.TargetEntityId`, such as `Haley`, to the bridge.
- `NpcRuntimeSnapshot` did not expose the descriptor body binding, so the developer page could not use the same executable target identity as the runtime tools.

## Bad Fix Paths

- Do not hardcode Haley/Penny aliases in the Desktop developer page; the runtime descriptor already owns the binding.
- Do not treat every bridge `invalid_target` as a bridge resolver bug; first check the exact `npcId` that reached SMAPI logs.
- Do not fix only "发送调试台词"; the same manual action path also covers debug reposition and should keep one target identity contract.

## Corrective Constraints

- Runtime snapshots used by operational UI must expose `NpcBodyBinding` when actions need to cross into a game bridge.
- Manual developer actions should submit `GameAction.BodyBinding` and let `StardewCommandService.ResolveNpcId` choose the bridge target id.
- Diagnostics for `invalid_target` should compare logical `NpcId` with `TargetEntityId` before changing bridge code.

## Verification Evidence

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewManualActionServiceTests.SpeakAsync_WithBodyBinding_SubmitsTargetEntityIdToBridgeAction|FullyQualifiedName~StardewManualActionServiceTests.RepositionToTownAsync_WithBodyBinding_SubmitsTargetEntityIdToBridgeAction|FullyQualifiedName~NpcRuntimeInstanceTests.Snapshot_IncludesDescriptorBodyBinding"`
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcDeveloperInspectorServiceTests|FullyQualifiedName~NpcRuntimeInstanceTests|FullyQualifiedName~StardewManualActionServiceTests|FullyQualifiedName~StardewCommandServiceTests"`
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug`
- `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64`

## Related Files

- `Desktop/HermesDesktop/Views/DeveloperPage.xaml.cs`
- `src/games/stardew/StardewBridgeDiscovery.cs`
- `src/runtime/NpcRuntimeDescriptor.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewManualActionServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeInstanceTests.cs`
