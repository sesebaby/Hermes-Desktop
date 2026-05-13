---
id: E-2026-0513-stardew-private-chat-tool-attempt-vs-success
title: Stardew private-chat closure tools must distinguish attempted calls from successful tool results
updated_at: 2026-05-13
keywords:
  - stardew
  - private_chat
  - stardew_submit_host_task
  - npc_no_world_action
  - tool_result
  - self_check
  - agent_native
trigger_scope:
  - stardew
  - private-chat
  - tool-calling
  - host-task
  - bugfix
---

## symptoms

- Live private-chat logs show `stardew_submit_host_task` was called, but no host-task ingress is queued.
- Tool execution fails before or during argument validation, yet the private-chat runner treats the tool call as if the world-action submission path was satisfied.
- The next self-check can enter the wrong branch, such as “missing todo only,” even though no successful host task exists.
- A sibling failure can happen for `npc_no_world_action`: an invalid no-world declaration can suppress bounded delegation self-check even though the no-action closure failed.

## root_cause

- Private-chat self-check logic used assistant tool-call presence as a proxy for successful closure.
- `Agent` records assistant tool-call requests and later tool result messages separately; a request can exist even when the tool returns `ToolResult.Fail`.
- For Stardew world-action closure, “attempted tool call” is not enough. The runner must observe a matching tool result that proves the host task was queued or the no-world action was accepted.

## bad_fix_paths

- Do not treat any assistant `stardew_submit_host_task` tool call as successful submission without checking the tool result.
- Do not parse natural-language `target` strings or infer coordinates in host code to make failed submissions succeed.
- Do not route failed or missing tool calls into `local_executor`, JSON text parsing, or any hidden fallback lane.
- Do not fix only `stardew_submit_host_task` while leaving sibling closure tools such as `npc_no_world_action` gated by call presence.

## corrective_constraints

- For private-chat closure, success gates must pair assistant tool-call id/name with a later tool result for the same id/name.
- `stardew_submit_host_task` success means the native tool returned a success marker after host-task ingress enqueue, such as `queued: true`.
- `npc_no_world_action` success means the native tool returned a validated no-world marker, such as `noWorldAction: true`.
- Failed native tool results should remain visible to the parent agent so it can retry with a valid visible-tool call.
- Larger hardening can add durable `ToolResult.Success` metadata to session messages, but scoped Stardew fixes must keep any result-body parsing localized.

## verification_evidence

- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenNoWorldActionFailsValidation_RunsDelegationSelfCheck" -p:UseSharedCompilation=false` failed because an invalid `npc_no_world_action` call suppressed self-check.
- GREEN: same test passed after private-chat self-check required successful tool results for both `stardew_submit_host_task` and `npc_no_world_action`.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests|FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests" -p:UseSharedCompilation=false` passed, 48/48.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:UseSharedCompilation=false` passed, 1078/1081 with 3 skipped.

## related_files

- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`

