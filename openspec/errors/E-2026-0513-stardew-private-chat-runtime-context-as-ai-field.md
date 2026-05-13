---
id: E-2026-0513-stardew-private-chat-runtime-context-as-ai-field
title: Stardew private-chat runtime context must not be optional AI-authored tool fields
updated_at: 2026-05-13
keywords:
  - stardew
  - private_chat
  - stardew_submit_host_task
  - conversationId
  - tool_schema
  - agent_native
trigger_scope:
  - stardew
  - private-chat
  - host-task
  - tool-calling
  - bugfix
---

## symptoms

- Manual Stardew test shows the NPC has a pending private-chat reply, but a delegated host task starts before the player clicks to read that reply.
- Hermes logs show `StardewSubmitHostTaskTool` queued `stardew_host_task_submission` with `conversationId=-`.
- The private-chat runner produced a natural reply, so the failure is not an empty-response problem.
- Bridge logs can still show waiting around real `DialogueBox` state, but pending-click replies are not an open dialogue window; the desktop host-task gate needs the matching `conversationId` to defer correctly.

## root_cause

- `stardew_submit_host_task` exposed `conversationId` as an optional model-authored field.
- Private-chat sessions intentionally set `ToolSessionId` to the long-term NPC session so `todo` writes durable NPC tasks instead of private-chat-local tasks.
- `StardewSubmitHostTaskTool` tried to infer `conversationId` from `CurrentSessionId`, but the tool received the long-term `ToolSessionId`, not the private-chat session id.
- When the model omitted optional `conversationId`, the host-task ingress lost the runtime identity required by the private-chat reply gate.

## bad_fix_paths

- Do not ask the model to remember and fill `conversationId`; it is host runtime context, not NPC business intent.
- Do not change generic `Agent` tool-session semantics just to repair this one Stardew tool; doing so can move `todo` writes back into private-chat-local task views.
- Do not parse natural-language replies to infer whether movement should wait for private-chat delivery.
- Do not add local executor, JSON text parsing, phrase tables, or hidden fallback lanes.

## corrective_constraints

- Agent-visible tool schemas must be minimal: AI fills only business decision fields that it actually owns.
- Host-known runtime context such as `conversationId`, session id, trace id, and root task bindings must be supplied by tool construction, binding context, or execution boundary.
- Keep legacy parameter parsing only for compatibility; do not keep runtime identity fields visible in the model-facing schema.
- Private-chat host-task ingress must carry the current runtime `conversationId` even when the model does not provide it.

## verification_evidence

- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenImmediateActionAccepted_QueuesHostTaskSubmissionIngress|FullyQualifiedName~StardewNpcToolFactoryTests.SubmitHostTaskTool_WithRuntimeConversationId_QueuesHostTaskWithConversationId|FullyQualifiedName~StardewNpcToolFactoryTests.SubmitHostTaskTool_SchemaDoesNotExposeRuntimeConversationId" -p:UseSharedCompilation=false` failed because `StardewSubmitHostTaskTool` had no runtime `conversationId` default injection point.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenImmediateActionAccepted_QueuesHostTaskSubmissionIngress|FullyQualifiedName~StardewNpcToolFactoryTests.SubmitHostTaskTool_WithRuntimeConversationId_QueuesHostTaskWithConversationId|FullyQualifiedName~StardewNpcToolFactoryTests.SubmitHostTaskTool_SchemaExposesOnlyAgentOwnedFields" -p:UseSharedCompilation=false` passed, 3/3.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false` passed, 264/266 with 2 live-AI tests skipped.
- GREEN: `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false` succeeded with two existing Stardew bridge warnings.

## related_files

- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
