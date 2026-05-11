---
id: E-2026-0511-stardew-hidden-local-executor-fallback
title: Stardew autonomy must not route parent no-tool text into hidden local executor
updated_at: 2026-05-11
keywords:
  - stardew
  - npc-autonomy
  - local_executor
  - hidden_fallback
  - no_tool_decision
  - tool_surface
  - mcp
  - Haley
---

## symptoms

- NPC 已完成真实动作，例如 Haley 已经走到海边，但下一轮到达目的地后不继续行动。
- Hermes 日志显示父层 LLM turn 完成且工具可用，但 `toolCalls=0`。
- NPC runtime `runtime.jsonl` 可能出现 `local_executor_escalated:intent_contract_invalid`、`local_executor_blocked:no_tool_call`、`local_executor_completed:wait` 或隐藏 `host_action`/`task_update_contract` 记录。
- 表面看像 prompt 主动性不足、bridge 没继续推进、或模型不知道下一步；实际是父层没有调用可见工具时，宿主把自然语言/JSON 文本塞进内部执行层，导致模型看不到有意义工具结果。
- 手测中会表现为 NPC 到达后“站住不动”，因为父层自由文本没有成为可见工具调用，也没有形成模型能理解的下一步闭环。

## trigger_scope

- 修改 `NpcAutonomyLoop` 的父层决策、no-tool 诊断、`localExecutorRunner` 路由。
- 修改 Stardew/NPC agent loop 主动性、到达后唤醒、active todo continuity 提示。
- 调试 `toolCalls=0`、`local_executor_*`、`intent_contract_invalid`、`no_tool_decision`、到达后无动作。
- 任何把 agent 文本输出解释成 move/speak/todo/task update 的宿主侧逻辑。

## root_cause

- `NpcAutonomyLoop.ShouldRouteToLocalExecutor` 把两类父层响应路由进 `RunLocalExecutorAsync`：
  - 看起来像 JSON 的响应。
  - 当前 session 没有任何 tool call 的自由文本响应。
- 这让 `local executor` 变成了父层 agent 的隐藏第二工具面。它不是 MCP，不在模型可见 tool surface 中，也不会以普通工具调用/工具结果的形式被父层理解。
- 父层 agent 原则上应该读 skill、调用可见 Hermes/MCP/Stardew 工具、接收工具结果；宿主只能执行显式工具调用并返回事实。
- 隐式 fallback 破坏了这个边界：宿主开始从文本里替 agent 推断行动或 task update，或者返回 `intent_contract_invalid` 这种对父层不友好的内部错误码。
- 参考项目的闭环形状是 queue/current task/status/visible tool result，不是“父层没调用工具就交给隐藏执行器猜”。

## bad_fix_paths

- 不要只加强 prompt 要求“必须行动”，同时保留 no-tool/free-text -> `local executor` fallback。
- 不要把 `local_executor_*` 内部错误码当成给父层模型看的主反馈渠道。
- 不要在宿主侧解析 NPC 自然语言或 JSON 文本来替它执行 move/speak/todo/task update。
- 不要把 `local executor` 当成 MCP。MCP/Hermes/Stardew 工具是 agent 可见能力；local executor 是内部实现细节。
- 不要用地点/台词/人物白名单修复，比如专门处理 Haley、beach、"到了"、"我等你"。
- 不要让隐藏 executor 承担地图解析、人格判断、玩家承诺收口或行动选择。

## corrective_constraints

- Stardew/NPC agent 的能力边界必须是模型可见的 Hermes/MCP/Stardew tool surface。
- 参考项目对齐证据：`external/hermes-agent-main` 的主循环在没有 tool call 时返回 final text；`delegate_task` 和 MCP tools 都是显式 tool schema，不是隐藏 fallback。`external/hermescraft-main` 也通过 `mc` 工具、`commandQueue`、`currentTask`、`task`/`read_chat` 反馈闭环，不把 no-tool/free text 当执行授权。
- 父层 autonomy turn 没有工具调用时，宿主最多写可读诊断或在下一轮事实提示中说“请进行下一步行动”；不得把自由文本或 JSON 文本送入 `local executor`。
- 到达/完成后的唤醒可以注入 `last_action_result`、active todo continuity、action history 等事实，但必须明确这些是事实，不是执行锁，也不是宿主替 agent 选下一步。
- 真实世界写操作必须来自显式可见工具调用，例如 `stardew_navigate_to_tile`、`stardew_speak`、`stardew_idle_micro_action`、`npc_delegate_action` 等。
- 内部兼容入口若短期保留，必须限定为显式 host/internal ingress，不得由普通父层 no-tool 输出触发。
- 回归测试必须覆盖：父层 JSON move/idle/wait/task update/free text 在普通 autonomy turn 中不会触发 `local_executor`、`intent_contract`、隐藏 `host_action` 或隐藏 task update。

## verification_evidence

- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests" -p:UseSharedCompilation=false` failed 8 tests because parent JSON/free text still routed to `local_executor_*` and hidden side effects.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests" -p:UseSharedCompilation=false` passed, 45/45, after removing ordinary parent-turn routing into local executor.

## related_files

- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `AGENTS.md`
- `.omx/project-memory.json`
