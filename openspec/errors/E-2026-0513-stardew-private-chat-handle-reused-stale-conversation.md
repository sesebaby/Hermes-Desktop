# E-2026-0513-stardew-private-chat-handle-reused-stale-conversation

- id: E-2026-0513-stardew-private-chat-handle-reused-stale-conversation
- title: Stardew private-chat handle reuse must rebind conversation-scoped tools
- status: active
- updated_at: 2026-05-13
- keywords: [stardew, private_chat, conversationId, stardew_submit_host_task, runtime_binding, handle_reuse]
- trigger_scope: [stardew, private-chat, host-task, runtime-supervisor, bugfix]

## Symptoms

- Live private chat shows Haley agrees to go to the beach, but SMAPI logs contain no `task_move_enqueued`, `task_running`, or `task_completed`.
- Hermes logs show `StardewSubmitHostTaskTool` queued a `move` ingress.
- `runtime.jsonl` shows the queued ingress waiting on `waiting_private_chat_reply_delivery`.
- The queued ingress carries an old private-chat `conversationId` ending in `evt_000000000001`, while the visible reply delivery/close facts are for the current `conversationId` ending in `evt_000000000008`.

## Root Cause

- The private-chat agent handle was reused across player-click private-chat conversations.
- The reused handle retained the original `StardewSubmitHostTaskTool` instance and its `defaultConversationId`.
- `NpcToolSurface.FromTools()` fingerprint included only tool name and parameter type, so two `StardewSubmitHostTaskTool` instances with different runtime-bound default conversation ids looked identical.
- Host-task delivery gating then waited for the old conversation's reply delivery and never submitted the model-requested move to the bridge.

## Bad Fix Paths

- Do not expose `conversationId` back to the model-facing tool schema or ask the model to fill it.
- Do not parse the NPC reply text to infer that a move should be submitted.
- Do not increase UI/defer budgets to hide the stale conversation mismatch.
- Do not add a hidden retry queue, local executor, or natural-language fallback lane.

## Corrective Constraints

- Runtime-bound tool identity must participate in private-chat handle rebind decisions when the bound runtime context changes.
- Host-known runtime context such as private-chat `conversationId` must stay host-injected, not AI-authored.
- Tool-surface fingerprints that intentionally ignore instance state must be paired with an explicit runtime binding key for stateful tool instances.
- Rebinding private-chat tools must preserve long-term NPC `ToolSessionId` semantics so `todo` still writes to the durable NPC session.

## Verification Evidence

- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WithReusedPrivateChatHandle_QueuesHostTaskWithCurrentConversationId" -p:UseSharedCompilation=false` failed because the second ingress used `conversation-first`.
- GREEN: the same test passed after private-chat binding included a runtime binding key for the current conversation id.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~NpcRuntimeSupervisorTests|FullyQualifiedName~StardewNpcToolFactoryTests" -p:UseSharedCompilation=false` passed, 79/79.

## Related Files

- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/runtime/NpcRuntimeBindings.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
