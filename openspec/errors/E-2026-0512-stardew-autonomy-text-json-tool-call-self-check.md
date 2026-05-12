---
id: E-2026-0512-stardew-autonomy-text-json-tool-call-self-check
title: Stardew autonomy text JSON tool requests need an agent-native self-check, not host execution
updated_at: 2026-05-12
keywords:
  - stardew
  - npc-autonomy
  - toolCalls=0
  - text_json
  - self_check
  - active_todo
  - task_continuity
  - Haley
---

## symptoms

- 手测中 NPC 能完成第一步真实动作，例如 Haley 走到海边。
- 后续 autonomy turn 返回普通文本 JSON，例如 `{"tool":"stardew_status"}`，但 Hermes 日志显示 `toolCalls=0`。
- runtime 只记录 `task_continuity_unresolved`，NPC 不会真的观察、说话、更新 todo 或继续行动。
- 表面看像“模型一开始知道调用工具，后面忘了”；实际是模型输出了看起来像工具请求的文本，而不是 provider/tool protocol 里的真实 tool call。

## trigger_scope

- 修改 `NpcAutonomyLoop` 的 no-tool 诊断、自主循环连续性、active todo + terminal action 处理。
- 对齐参考项目的 task/action lifecycle 反馈。
- 调试到达后 `toolCalls=0`、文本 JSON、`task_continuity_unresolved`、active todo 不收口。

## root_cause

- `Agent.ChatAsync` 在 provider 没有返回 tool calls 时会把内容当 final text。
- Autonomy prompt 已经注入 `last_action_result` 与 active todo continuity，但没有像 private chat 那样做 bounded self-check。
- 所以模型偶尔会把工具调用写成文本 JSON。宿主不能执行这个 JSON；不执行又会停在 unresolved。

## bad_fix_paths

- 不要解析文本 JSON 并替 NPC 调用 `stardew_status`、move、speak、todo 或 local executor。
- 不要全局改成 `tool_choice=required`，否则会影响普通聊天、wait/no-action、预算耗尽等路径。
- 不要新增第二套任务状态或 closure lock。
- 不要把完成的 world action 自动映射为 todo completed。
- 不要把 Minecraft 参考项目的 task runner 直接搬过来。

## corrective_constraints

- 正确修复是 agent-native bounded self-check：同一 autonomy tick 内额外问一次模型，说明上一轮没有真实工具调用、JSON 文本不会执行，并要求用可见工具或明确 `no-action:`/`wait:` 收口。
- self-check 只在真实 world-action terminal + active todo + 无 Stardew action tool call + 无 todo tool call + 无明确 no-action reason 时触发。
- self-check 最多一次；工具预算耗尽 fallback 不触发 self-check，只保留预算诊断。
- 参考项目只借鉴短 lifecycle state 反馈，例如 `task_done`、`task_error`、`task_stuck`，不借鉴 Minecraft 专用 runner。

## verification_evidence

- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcAutonomyLoopTests.RunOneTickAsync_WithTerminalActionActiveTodoAndJsonToolText_RunsAutonomySelfCheckOnce" -p:UseSharedCompilation=false` failed because the agent was called once instead of twice.
- GREEN: same test passed after adding the bounded self-check harness.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcAutonomyLoopTests" -p:UseSharedCompilation=false` passed, 48/48.
- GREEN: Stardew/private-chat/autonomy slice passed, 185/185.
- GREEN: `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false` succeeded with 2 existing Stardew bridge warnings.

## related_files

- `src/runtime/NpcAutonomyLoop.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `external/hermescraft-main/bot/server.js`

