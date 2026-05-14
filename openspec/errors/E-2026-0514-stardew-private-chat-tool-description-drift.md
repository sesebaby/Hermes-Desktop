---
id: E-2026-0514-stardew-private-chat-tool-description-drift
title: Stardew private-chat protocol changes must update model-visible tool descriptions
updated_at: 2026-05-14
keywords:
  - stardew
  - private_chat
  - npc_no_world_action
  - tool_description
  - prompt_contract
  - contract_drift
trigger_scope:
  - stardew
  - private-chat
  - tool-calling
  - prompt
  - review-fix
---

## symptoms

- Runtime private-chat logic treats no-tool natural replies as a legal visible terminal state, but the model-visible `npc_no_world_action` description still says not to omit that declaration in plain text.
- Prompt and host behavior say `npc_no_world_action` is recommended diagnostic closure, while the tool description continues to imply a mandatory two-tool protocol.
- Review catches the mismatch after behavior tests pass, because the tests covered runtime outcomes but not the exact tool contract the model sees.

## root_cause

- The protocol change updated the private-chat orchestrator prompt and self-check logic, but did not audit the adjacent tool descriptions exposed to the model.
- Tool descriptions are part of the agent contract. If they retain old mandatory language, the model can still be trained toward outdated behavior even when host code no longer enforces it.

## bad_fix_paths

- Do not treat prompt text as the only agent contract when changing tool-calling rules.
- Do not leave old mandatory wording in tool descriptions after making a tool optional or diagnostic.
- Do not compensate for stale tool descriptions by adding host-side text inference, hidden executor fallback, or another second LLM self-check.
- Do not remove `npc_no_world_action`; keep it available as explicit no-world diagnostic closure.

## corrective_constraints

- For any Stardew private-chat protocol change, audit all model-visible surfaces together: system prompt, tool description, tool schema descriptions, and regression assertions.
- If a tool changes from mandatory closure to recommended diagnostic closure, its description must avoid mandatory phrases such as "must call", "must declare", "do not omit in plain text", or "hard gate".
- Add at least one test that inspects the actual tool definition passed into the private-chat model when the exact wording affects behavior.
- Runtime execution must remain gated by successful tool results, not by natural-language promises or failed/attempted tool calls.

## verification_evidence

- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_PrivateChatToolSurface_PrioritizesImmediateHostTaskSubmissionOverTodo" -p:UseSharedCompilation=false` failed because `npc_no_world_action` still described itself as a declaration that must not be omitted in plain text.
- GREEN: the same test passed after the description was changed to recommended diagnostic no-world closure.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests" -p:UseSharedCompilation=false` passed, 18/18.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false` passed, 278/280 with 2 live-AI tests skipped.

## related_files

- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `openspec/changes/stardew-private-chat-natural-reply-terminal/design.md`
