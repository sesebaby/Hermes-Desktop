# Stardew 工具编排与 Harness 上下文快照

## Task statement

借鉴 `external/hermescraft-main` 与 `external/hermes-agent-main`，为 Hermes-Desktop 的 Stardew NPC 工具链制定编排与 harness 方案，覆盖 `move`、`speak`、`idle_micro_action`、`open_private_chat`、`task_status`、MCP 调用、private-chat delegated action、scheduled private chat，不再单点修 `move`。

## Desired outcome

- 形成一条统一的工具生命周期：agent 调用工具 -> runtime 记录动作 -> bridge 执行 -> 状态轮询/事件完成 -> terminal fact 写入 NPC runtime -> 唤醒 agent -> agent 在下一轮看到 `last_action_result` 并继续决策。
- 让所有真实动作共享同一套 action slot、terminal feedback、diagnostic、timeout、lease、ingress、cursor、wake 语义。
- 建立可复现 harness，能在不启动真实 Stardew 的情况下验证 orchestration 边界和失败恢复。

## Known facts / evidence

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:475` 的 worker 顺序是 `TryAdvancePendingActionAsync` -> `TryProcessIngressWorkAsync` -> private chat lease -> cooldown -> LLM turn。任何前置门不释放，agent 都不会进入工具调用。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:817` 的 pending/action advance 负责 command id lookup、status、timeout/cancel、pause。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:1073` 每轮只处理一个 ingress；`src/games/stardew/StardewNpcAutonomyBackgroundService.cs:1128` delegated action 遇到已有 action slot/pending work 时直接返回，诊断不足。
- `src/games/stardew/StardewNpcTools.cs:1079` 的 `StardewRuntimeActionController.TryBeginAsync` 是单 action slot 入口；`:1156` 记录 submit result；`:1211` 在 terminal status 写 `LastTerminalCommandStatus` 并清理 slot/claim。
- `src/runtime/NpcRuntimeInstance.cs` 与 `src/runtime/NpcRuntimeStateStore.cs` 持久化 pending/action/ingress/lease/last terminal。
- `src/runtime/NpcAutonomyLoop.cs:303` 会把 `LastTerminalCommandStatus` 转成 `last_action_result` 注入下一轮 autonomy。
- `Desktop/HermesDesktop.Tests/Mcp/McpServerTests.cs:243` 和 `:335` 已覆盖 MCP move terminal feedback，但其他动作的 parity 需要补齐。
- `external/hermescraft-main/bot/server.js:703` 的 `briefState()` 会把 action 后的新聊天、pending commands、task done/error/stuck、action loop 等状态随响应返回给 agent。
- `external/hermescraft-main/bot/server.js:1271` 的短动作有 15 秒 timeout，并返回实际进度和下一步建议，不无限阻塞。
- `external/hermescraft-main/bot/server.js:2587` 暴露 `/task` 状态；`:2610` 后台任务返回 task id；`:2676` watchdog 能把卡住的移动任务转成 `stuck` terminal 状态。
- `external/hermes-agent-main/acp_adapter/events.py:47` 对 `tool.started` 生成 tool call id 并按工具名 FIFO 记录；`:132` 的 step callback 将完成结果配回正确 tool call id，解决同名并发工具 start/complete 对齐。

## Constraints

- 宿主只执行、反馈、编排状态，不替 NPC 决策。
- 不硬编码 NPC、地点、自然语言规则。
- 预发布阶段只保留一条实现路径，禁止双轨、兼容分叉和影子实现。
- 真实世界写操作必须走宿主执行器。
- Agent 通过工具获取事实，不能由宿主在唤醒提示中预载行动候选。
- 编排修复要覆盖所有 Stardew 工具，不只 `move`。

## Unknowns / open questions

- `speak`、`idle_micro_action`、`open_private_chat` 是否都需要与 `move` 相同的 post-terminal wake，还是只有 failure/blocked 需要唤醒。
- non-move delegated actions 是否应完全移出 `NpcLocalExecutorRunner` compatibility path，统一改成 parent-visible tool/MCP 路径。
- harness 应优先做 service-level fake bridge，还是补一个内存化 bridge server contract fake。

## Likely codebase touchpoints

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/game/core/PrivateChatOrchestrator.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcRuntimeStateStore.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `Desktop/HermesDesktop.Tests/Stardew/*`
- `Desktop/HermesDesktop.Tests/Mcp/McpServerTests.cs`
- `Mods/StardewHermesBridge.Tests/*`
