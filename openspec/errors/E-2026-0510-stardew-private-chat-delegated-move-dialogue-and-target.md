# E-2026-0510-stardew-private-chat-delegated-move-dialogue-and-target

- id: E-2026-0510-stardew-private-chat-delegated-move-dialogue-and-target
- title: Stardew private-chat delegated move must not resolve map targets in the local executor
- status: active
- updated_at: 2026-05-10
- keywords: [stardew, private_chat, npc_delegate_action, local_executor, move, dialogue_started, navigation_target_mismatch, Haley, beach]
- trigger_scope: [stardew, runtime, private-chat, bugfix, diagnostics]

## Symptoms

- Manual test says an invited NPC did not move, for example inviting Haley to the beach.
- SMAPI logs can show `task_move_enqueued` and `task_running`, then `task_interrupted` with `dialogue_started`.
- Runtime logs can show `private_chat_delegation npc_delegate_action queued`, `local_executor selected action=move;lane=delegation`, and a loaded target such as `Town:42,17` even though the player asked for `海边`.
- The failure can look like bridge pathing did nothing, but the command was actually submitted to a wrong target and then interrupted by the reply dialogue lifecycle.
- A patch that blocks specific natural-language destination aliases, for example `海边` versus `town-square`, is a symptom fix and does not scale to the full Stardew map set.

## Root Cause

- The previous parent-tool feedback fix moved regular autonomy movement back to the parent tool transcript, but private-chat `npc_delegate_action` still used the JSON/delegation compatibility path.
- That compatibility path routed move intent through `RunDelegatedIntentAsync` and `NpcLocalExecutorRunner`, where the local executor could read an unrelated POI file and treat its `target(locationName,x,y,source)` as valid.
- Private-chat delegated move ingress could run immediately after the reply was displayed, before the bridge emitted `private_chat_reply_closed`, so `BridgeCommandQueue.CheckInterrupt()` saw an active `DialogueBox` and interrupted the move with `dialogue_started`.
- The first mitigation added local-executor alias matching against `destinationText`, but this kept the wrong ownership boundary: natural-language map target resolution still belonged to the local executor instead of the parent model/tool transcript.

## Bad Fix Paths

- Do not diagnose this as "bridge never received the command" when SMAPI has `task_move_enqueued` / `task_running`.
- Do not fix by adding more prompt wording only; the compatibility path needs hard validation against the natural-language destination.
- Do not add a growing destination alias table to `NpcLocalExecutorRunner`; that makes the local executor a second map resolver.
- Do not bypass bridge movement with `warpCharacter` or NPC controller ownership.
- Do not start private-chat delegated world actions while the reply dialogue for the same conversation is still open.

## Corrective Constraints

- Private-chat delegated move with a `conversationId` must wait for the matching `private_chat_reply_closed` event before starting the executor.
- Private-chat delegated move must carry a parent-resolved mechanical `target(locationName,x,y,source)` from loaded `stardew-navigation` references.
- Private-chat delegated move must submit the host `GameActionType.Move` directly from that target after reply close; it must not call `RunDelegatedIntentAsync`, `NpcLocalExecutorRunner`, or the delegation/local model lane.
- The ingress work item should remain queued while waiting for reply close, so the next host event batch can process it.
- Regression tests must cover the dialogue lifecycle gate, target payload contract, and zero delegation model calls for private-chat move.

## Verification Evidence

- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalExecutorRunnerTests.ExecuteAsync_WithMoveIntentAndMismatchedSkillTarget_BlocksWithoutSubmittingNavigation" -p:UseSharedCompilation=false` failed because the local executor kept using the mismatched target.
- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithDelegatedMoveBeforePrivateChatReplyClosed_DefersIngressWithoutStartingExecutor" -p:UseSharedCompilation=false` failed because the executor started before reply close.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalExecutorRunnerTests" -p:UseSharedCompilation=false` passed, 19/19.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests" -p:UseSharedCompilation=false` passed, 41/41.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false` passed, 213/213 with 3 live-AI tests skipped.
- GREEN: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug -p:UseSharedCompilation=false` passed, 133/133.
- GREEN: `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false` succeeded with existing Stardew bridge warnings.
- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenImmediateActionAccepted_QueuesDelegatedActionIngress|FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenImmediateMoveAcceptedWithoutDelegation_RetriesAndQueuesDelegatedAction" -p:UseSharedCompilation=false` failed because `npc_delegate_action` still rejected target payloads and no ingress was queued.
- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithDelegatedMoveTarget_SubmitsHostMoveWithoutLocalExecutor|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithDelegatedMoveBeforePrivateChatReplyClosed_DefersIngressWithoutStartingExecutor" -p:UseSharedCompilation=false` failed because delegated target payloads were not submitted as host move commands.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalExecutorRunnerTests|FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests" -p:UseSharedCompilation=false` passed, 67/67.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false` passed, 213/213 with 3 live-AI tests skipped.

## Related Files

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs`
