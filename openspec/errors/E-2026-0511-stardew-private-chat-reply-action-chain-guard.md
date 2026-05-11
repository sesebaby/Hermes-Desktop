---
id: E-2026-0511-stardew-private-chat-reply-action-chain-guard
title: Stardew closure-lock execution guard could strand private chat and delegated moves
updated_at: 2026-05-11
keywords:
  - stardew
  - private_chat
  - private_chat_reply
  - action_chain
  - closure_missing
  - blocked_until_closure
  - Haley
---

## symptoms

- Player submits a private-chat message to an NPC, for example Haley, and receives no visible reply.
- SMAPI logs show `private_chat_message_submitted`, but no later `private_chat_reply_displayed`.
- Hermes logs show `StardewNpcPrivateChatAgentRunner` started and completed with non-empty `responseChars`.
- Runtime activity can continue recording `closure_missing` and terminal `work_private_chat:*` facts, but no new `work_private_chat_reply:*` terminal record appears for the failed turn.
- The bug can look like an LLM no-response or bridge display failure, but the reply was generated and never submitted as `GameActionType.Speak`.
- A later player request such as "Haley, go to the beach" can queue `npc_delegated_action` ingress with a concrete move target while SMAPI shows no `task_move_enqueued`.
- The NPC runtime state can show `action_chain_json.GuardStatus=blocked_until_closure`, `BlockedReasonCode=closure_missing`, and queued ingress for `action=move`.
- Local executor logs can show structured `action=wait` / `local_executor_completed:wait`, but the old guard could still remain blocked because the closure path treated text-format closure as execution authority.

## trigger_scope

- Changing `StardewRuntimeActionController`, `NpcActionChainGuardOptions`, or action-chain guard accounting.
- Changing `StardewPrivateChatRuntimeAdapter` / `StardewPrivateChatLifecycleCommandService`.
- Debugging missing private-chat replies after a recent autonomy action-chain, `closure_missing`, or `blocked_until_closure` change.
- Debugging delegated private-chat moves where Hermes has queued ingress but SMAPI has no `task_move_enqueued`.
- Changing `NpcAutonomyLoop.RequiresClosureChoice`, local-executor routing, or no-tool closure diagnostics.

## root_cause

- The previous action-chain guard added a project-local natural-language closure lock that the reference project does not have.
- The private-chat lifecycle command service reused `StardewRuntimeActionController` for `OpenPrivateChat` and private-chat reply `Speak`.
- Because the controller treated private-chat UI lifecycle actions as chain-counted world writes, an existing `blocked_until_closure` guard caused `TryBeginAsync` to return a blocked result before the reply `Speak` reached the bridge.
- `PrivateChatOrchestrator` then ended the session after a rejected speak result, leaving the player with no reply despite the parent model having generated one.
- A second failure path remained after replies were allowed: `NpcAutonomyLoop.RequiresClosureChoice` treated any terminal command plus any active todo as closure debt, including `open_private_chat` / private-chat UI lifecycle terminal records.
- The closure exit was also brittle: the prompt allowed "wait/no-action" in prose, but `WriteNoToolActionDiagnosticAsync` only accepted exact response text matching `wait: reason` or `no-action: reason`.
- The parent/local-executor route already produced structured `action=wait`, but the closure diagnostic ignored that structured intent and saw only `local_executor_completed:wait`, so it repeatedly recorded `closure_missing`.
- After the missing-closure count crossed the guard threshold, stale `blocked_until_closure` state blocked later real delegated move ingress before the bridge could receive `/task/move`.
- The deeper fix is to remove execution authority from `blocked_until_closure` entirely. Repeated failures, stale closure, and action-loop signals are facts for the agent, not global runtime locks.

## bad_fix_paths

- Do not diagnose this as "Hermes never saw the player message" when Hermes has private-chat parent start/completion logs.
- Do not diagnose this as "the model did not reply" when `responseChars` is non-zero.
- Do not bypass the bridge or show replies directly from host code.
- Do not reintroduce `blocked_until_closure` as a global execution lock. The reference shape is queue/current task/status/todo completion, not a text-closure gate.
- Do not solve by parsing the player's private-chat text or the NPC's reply text in host code.
- Do not add special recovery paths that clear one old reason before delegated ingress. If the old lock has no execution authority, those bypasses are unnecessary and misleading.
- Do not depend on exact natural-language `wait:` text as a critical execution exit.

## corrective_constraints

- Private-chat UI lifecycle actions may still use the runtime action slot and terminal command recording. Legacy action-chain state must not block them.
- Private-chat UI lifecycle terminal records must not create world-action closure debt in `NpcAutonomyLoop`.
- Private-chat reply terminal records should be structurally identifiable as `private_chat_reply`, not only generic `speak`, when recorded by the private-chat lifecycle adapter.
- Autonomous world actions submitted from parent tools or delegated ingress may continue to update action history facts, but those facts must not block fresh actions.
- Structured wait/no-action intent from the parent/local-executor route may be recorded as a diagnostic; it must not be required to unlock execution.
- Historical `blocked_until_closure` / `closure_missing` state is legacy diagnostic data only. New action acceptance should normalize or overwrite it through normal action-history updates.
- Regression coverage must set an existing `blocked_until_closure` guard before a private-chat player message and assert the reply `Speak` is still submitted.
- Regression coverage must set an existing `blocked_until_closure` guard before a delegated move and assert the move reaches the command service when the action slot is free.

## verification_evidence

- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewPrivateChatOrchestratorTests.RuntimeAdapter_WithBlockedActionChainGuard_SubmitsPrivateChatReply" -p:UseSharedCompilation=false` failed because only the open-private-chat command was submitted.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewPrivateChatOrchestratorTests" -p:UseSharedCompilation=false` passed, 28/28.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests" -p:UseSharedCompilation=false` passed, 26/26.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests" -p:UseSharedCompilation=false` passed, 48/48.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false` passed, 244/247 with 3 live-AI tests skipped.
- GREEN: `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false` succeeded with existing Stardew bridge warnings.
- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~NpcAutonomyLoopTests.RunOneTickAsync_AfterPrivateChatUiTerminalWithActiveTodo_DoesNotRequireWorldActionClosure"` failed because `closure_missing` was recorded for `open_private_chat`.
- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests.RunOneTickAsync_WithStructuredWaitIntentDuringClosureRequirement_ClosesActionChain" -p:UseSharedCompilation=false` failed because no `closure_no_action` was recorded for structured `action=wait`.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests.RunOneTickAsync_AfterPrivateChatUiTerminalWithActiveTodo_DoesNotRequireWorldActionClosure|FullyQualifiedName~NpcAutonomyLoopTests.RunOneTickAsync_WithStructuredWaitIntentDuringClosureRequirement_ClosesActionChain" -p:UseSharedCompilation=false` passed, 2/2.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithDelegatedMoveAndBlockedClosureGuard_SubmitsMoveAsFreshPlayerRequest|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithDelegatedMoveAndPathBlockedGuard_DoesNotBypassActionGuard"` passed, 2/2.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewPrivateChatOrchestratorTests" -p:UseSharedCompilation=false` passed, 28/28.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~StardewNpcToolFactoryTests"` passed, 26/26.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~Stardew"` passed, 246/249 with 3 live-AI tests skipped.

## related_files

- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcAutonomyBudget.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
