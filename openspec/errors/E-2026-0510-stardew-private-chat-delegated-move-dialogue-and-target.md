# E-2026-0510-stardew-private-chat-delegated-move-dialogue-and-target

- id: E-2026-0510-stardew-private-chat-delegated-move-dialogue-and-target
- title: Stardew private-chat delegated move could start under DialogueBox and choose the wrong navigation POI
- status: active
- updated_at: 2026-05-10
- keywords: [stardew, private_chat, npc_delegate_action, local_executor, move, dialogue_started, navigation_target_mismatch, Haley, beach]
- trigger_scope: [stardew, runtime, private-chat, bugfix, diagnostics]

## Symptoms

- Manual test says an invited NPC did not move, for example inviting Haley to the beach.
- SMAPI logs can show `task_move_enqueued` and `task_running`, then `task_interrupted` with `dialogue_started`.
- Runtime logs can show `private_chat_delegation npc_delegate_action queued`, `local_executor selected action=move;lane=delegation`, and a loaded target such as `Town:42,17` even though the player asked for `海边`.
- The failure can look like bridge pathing did nothing, but the command was actually submitted to a wrong target and then interrupted by the reply dialogue lifecycle.

## Root Cause

- The previous parent-tool feedback fix moved regular autonomy movement back to the parent tool transcript, but private-chat `npc_delegate_action` still used the JSON/delegation compatibility path.
- That compatibility path routed move intent through `RunDelegatedIntentAsync` and `NpcLocalExecutorRunner`, where the local executor could read an unrelated POI file and treat its `target(locationName,x,y,source)` as valid.
- Private-chat delegated move ingress could run immediately after the reply was displayed, before the bridge emitted `private_chat_reply_closed`, so `BridgeCommandQueue.CheckInterrupt()` saw an active `DialogueBox` and interrupted the move with `dialogue_started`.

## Bad Fix Paths

- Do not diagnose this as "bridge never received the command" when SMAPI has `task_move_enqueued` / `task_running`.
- Do not fix by adding more prompt wording only; the compatibility path needs hard validation against the natural-language destination.
- Do not bypass bridge movement with `warpCharacter` or NPC controller ownership.
- Do not start private-chat delegated world actions while the reply dialogue for the same conversation is still open.

## Corrective Constraints

- Private-chat delegated move with a `conversationId` must wait for the matching `private_chat_reply_closed` event before starting the executor.
- Local executor navigation must reject a loaded skill target that clearly conflicts with the move `destinationText`, such as `destinationText=海边` with `source=...town-square`.
- The ingress work item should remain queued while waiting for reply close, so the next host event batch can process it.
- Regression tests must cover both the dialogue lifecycle gate and wrong-POI rejection.

## Verification Evidence

- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalExecutorRunnerTests.ExecuteAsync_WithMoveIntentAndMismatchedSkillTarget_BlocksWithoutSubmittingNavigation" -p:UseSharedCompilation=false` failed because the local executor kept using the mismatched target.
- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithDelegatedMoveBeforePrivateChatReplyClosed_DefersIngressWithoutStartingExecutor" -p:UseSharedCompilation=false` failed because the executor started before reply close.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalExecutorRunnerTests" -p:UseSharedCompilation=false` passed, 19/19.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests" -p:UseSharedCompilation=false` passed, 41/41.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false` passed, 213/213 with 3 live-AI tests skipped.
- GREEN: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug -p:UseSharedCompilation=false` passed, 133/133.
- GREEN: `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false` succeeded with existing Stardew bridge warnings.

## Related Files

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs`

