---
id: E-2026-0506-stardew-autonomy-tool-call-truncation-json-break
title: Stardew autonomy 上下文压缩截断旧 tool_call arguments 会把 provider replay JSON 弄坏
updated_at: 2026-05-06
keywords:
  - stardew
  - npc-autonomy
  - context-budget
  - tool_calls
  - function.arguments
  - invalid JSON
  - qwen
  - openai-compatible
---

## symptoms

- NPC autonomy 已经能在某些 tick 里走 `parent intent -> local executor -> stardew_move`，但后续 tick 会间歇性退回 `todo` / `stardew_speak` 循环。
- Desktop 日志出现 provider 400：
  - `InternalError.Algo.InvalidParameter: The "function.arguments" parameter of the code model must be in JSON format.`
- `runtime.jsonl` 会同时看到两类现象：
  - 成功 trace：`intent_contract accepted`、`parent_tool_surface verified`、`local_executor selected action=move;lane=delegation`、`local_executor_completed:stardew_move`
  - 失败 trace：`max_tool_iterations`、`intent_contract_invalid` 或 `action_not_allowed`
- 失败发生时，问题不一定在当前 tick 的 parent contract；历史 assistant tool-call replay 也可能触发 provider 请求失败。

## trigger_scope

- 改动 `StardewAutonomyFirstCallContextBudgetPolicy`、outbound context compaction、tool-call replay、OpenAI-compatible provider payload 组装。
- 调整 autonomy 历史消息保留策略，尤其是旧 assistant `tool_calls` 与 tool result 的裁剪逻辑。
- 引入新的 OpenAI-compatible 本地/远程 provider，且 provider 会严格校验 assistant 历史 `tool_calls[].function.arguments` 为 JSON 字符串。

## root_cause

- `StardewAutonomyFirstCallContextBudgetPolicy` 为了压缩旧 assistant tool request，会保留该 assistant 消息并调用 `TruncateToolCalls(...)` 截断旧 `tool_calls.arguments`。
- 截断后的 `arguments` 不再是合法 JSON，但 `OpenAiClient.BuildPayload(...)` 会把这些历史 assistant `tool_calls` 原样回放给 provider。
- 对严格校验 replay tool-call 参数的 provider（本次为 Qwen OpenAI-compatible）来说，非法 JSON 会直接返回 400，从而把后续 autonomy tick 打断。

## bad_fix_paths

- 不要通过重新暴露 `stardew_move` / `stardew_task_status` 给 parent agent 来绕过本问题；这会破坏 delegation 架构边界。
- 不要放宽 `NpcLocalActionIntent` 来掩盖 provider 400；这里的问题发生在历史 assistant tool-call replay，而不是当前 contract parser。
- 不要继续保留被压缩的旧 assistant `tool_calls`，哪怕只改短一点；只要 arguments 可能失去 JSON 完整性，就仍然会炸。
- 不要删除 latest protected tail 的 tool request/result；当前 turn continuation 仍需完整保留。

## corrective_constraints

- 旧历史 assistant tool request 一旦进入“压缩占位”路径，就不能继续带 `ToolCalls` 出站。
- 只允许完整保留 latest protected tail 的 assistant tool request 及其匹配 tool result；旧历史要么完整保留，要么完全去掉 `ToolCalls`。
- 回归测试必须覆盖：
  - 被压缩的旧 assistant tool request 不再保留 `ToolCalls`
  - latest protected tail 的 `ToolCalls` 仍保留
  - orphan/missing tool-pair sanitization 仍正常工作

## verification_evidence

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextBudgetTests.BudgetPolicy_TrimmedHistoricalAssistantToolRequest_DropsToolCallsToKeepArgumentsJsonValid"` passed.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextBudgetTests.BudgetPolicy_ProtectsLatestAssistantToolRequestAndMatchingToolResults|FullyQualifiedName~StardewAutonomyContextBudgetTests.BudgetPolicy_SanitizesOrphanAndMissingToolPairs|FullyQualifiedName~NpcAutonomyLoopTests.RunOneTickAsync_WithLocalExecutorMoveIntent_ExecutesRunnerLogsEvidenceAndWritesSummaryMemory|FullyQualifiedName~NpcAutonomyLoopTests.RunOneTickAsync_WithLocalExecutorInvalidParentContract_RejectsAndDoesNotWriteMemory"` passed.
- `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64` passed with the existing SMAPI x64/Any CPU warning.
- Live Stardew logs before the fix showed both:
  - successful delegation trace `trace_b8c0951a5e374a32bec68515fa9106fd`
  - provider 400 `function.arguments must be in JSON format`

