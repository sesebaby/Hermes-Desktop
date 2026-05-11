# E-2026-0510-stardew-private-chat-delegated-move-dialogue-and-target

- id: E-2026-0510-stardew-private-chat-delegated-move-dialogue-and-target
- title: Stardew private-chat delegated move must not resolve map targets in the local executor
- status: active
- updated_at: 2026-05-11
- keywords: [stardew, private_chat, npc_delegate_action, npc_no_world_action, tool_calls_zero, local_executor, move, dialogue_started, navigation_target_mismatch, Haley, beach]
- trigger_scope: [stardew, runtime, private-chat, bugfix, diagnostics]

## Symptoms

- Manual test says an invited NPC did not move, for example inviting Haley to the beach.
- SMAPI logs can show `task_move_enqueued` and `task_running`, then `task_interrupted` with `dialogue_started`.
- Runtime logs can show `private_chat_delegation npc_delegate_action queued`, `local_executor selected action=move;lane=delegation`, and a loaded target such as `Town:42,17` even though the player asked for `海边`.
- The failure can look like bridge pathing did nothing, but the command was actually submitted to a wrong target and then interrupted by the reply dialogue lifecycle.
- A patch that blocks specific natural-language destination aliases, for example `海边` versus `town-square`, is a symptom fix and does not scale to the full Stardew map set.
- A later manual test can show the NPC says a natural reply such as "already walking" but does not move at all. In that shape, runtime/LLM diagnostics can show the private-chat parent turn completed with `toolCalls=0`, so no `npc_delegate_action` ingress exists and SMAPI receives no move command.
- A bridge/game restart can reuse event ids from `evt_000000000001`; if private-chat `conversationId` is derived only from that event id, a fresh private chat can reuse an old transcript session. The model may then see prior tool calls from an earlier run and claim movement is already queued.

## Root Cause

- The previous parent-tool feedback fix moved regular autonomy movement back to the parent tool transcript, but private-chat `npc_delegate_action` still used the JSON/delegation compatibility path.
- That compatibility path routed move intent through `RunDelegatedIntentAsync` and `NpcLocalExecutorRunner`, where the local executor could read an unrelated POI file and treat its `target(locationName,x,y,source)` as valid.
- Private-chat delegated move ingress could run immediately after the reply was displayed, before the bridge emitted `private_chat_reply_closed`, so `BridgeCommandQueue.CheckInterrupt()` saw an active `DialogueBox` and interrupted the move with `dialogue_started`.
- The first mitigation added local-executor alias matching against `destinationText`, but this kept the wrong ownership boundary: natural-language map target resolution still belonged to the local executor instead of the parent model/tool transcript.
- Another failure mode is a parent private-chat turn that returns plain dialogue without any tool call. Prompt wording alone allowed the model to verbally accept an immediate world action while leaving the host with no structured command to execute.
- Text markers such as `NO_WORLD_ACTION:` are not a reliable protocol. They hide the decision in natural language instead of using the same tool-call feedback channel as real world actions.
- Another failure mode is conversation identity collision across bridge restarts. The bridge event buffer restarts canonical event ids, so `pc_evt_000000000001` is not globally unique. Reusing that value in the private-chat transcript session id lets old assistant/tool messages contaminate a new player request.

## Bad Fix Paths

- Do not diagnose this as "bridge never received the command" when SMAPI has `task_move_enqueued` / `task_running`.
- Do not fix by adding more prompt wording only; the compatibility path needs hard validation against the natural-language destination.
- Do not add a growing destination alias table to `NpcLocalExecutorRunner`; that makes the local executor a second map resolver.
- Do not add phrase tables or destination alias checks to `StardewPrivateChatOrchestrator` to decide whether "go", "走吧", "海边", "beach", etc. imply movement. The parent model owns that decision.
- Do not use a textual `NO_WORLD_ACTION:` prefix as the "no action" signal; this is another natural-language convention that the host must parse.
- Do not bypass bridge movement with `warpCharacter` or NPC controller ownership.
- Do not start private-chat delegated world actions while the reply dialogue for the same conversation is still open.
- Do not "fix" restart collisions by clearing or deleting private-chat transcripts. Preserve history, but give each fresh bridge-backed private-chat session a unique conversation id.

## Corrective Constraints

- Private-chat delegated move with a `conversationId` must wait for the matching `private_chat_reply_closed` event before starting the executor.
- Private-chat delegated move must carry a parent-resolved mechanical `target(locationName,x,y,source)` from loaded `stardew-navigation` references.
- Private-chat delegated move must submit the host `GameActionType.Move` directly from that target after reply close; it must not call `RunDelegatedIntentAsync`, `NpcLocalExecutorRunner`, or the delegation/local model lane.
- The ingress work item should remain queued while waiting for reply close, so the next host event batch can process it.
- Private-chat parent turns use a two-tool decision protocol: call `npc_delegate_action` when the NPC accepts an immediate world action, or call `npc_no_world_action` when this turn has no immediate world action. If neither tool appears, the runner may do one generic self-check that reports only the structural fact that `npc_delegate_action` was absent and asks the parent model to decide.
- The self-check must not classify player text or NPC reply text in host code. It must not contain destination aliases or acceptance phrase matching.
- Regression tests must cover the dialogue lifecycle gate, target payload contract, and zero delegation model calls for private-chat move.
- Bridge-backed private-chat conversation ids must include a bridge/session scope before event ids so restarted `evt_000000000001` values do not collide in transcript storage.
- `npc_no_world_action` should log its reason to Hermes logs so manual tests can distinguish a model no-action decision from missing tool execution without reading SQLite activity rows.

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
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests" -p:UseSharedCompilation=false` passed, 11/11 after replacing text markers with `npc_no_world_action`.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalExecutorRunnerTests|FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewLiveAiSmokeTests" -p:UseSharedCompilation=false` passed, 70/70 with 3 live-AI tests skipped.
- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewPrivateChatOrchestratorTests.RuntimeAdapter_NewBridgeWithRestartedEventIds_GeneratesDistinctConversationIds" -p:UseSharedCompilation=false` failed because both bridge attachments mapped restarted `evt_000000000001` to `pc_evt_000000000001`.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewPrivateChatOrchestratorTests" -p:UseSharedCompilation=false` passed, 24/24.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests" -p:UseSharedCompilation=false` passed, 11/11.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false` passed, 217/217 with 3 live-AI tests skipped.
- GREEN: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug -p:UseSharedCompilation=false` passed, 133/133.
- GREEN: `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false` succeeded with existing Stardew bridge warnings.

## Related Files

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs`
