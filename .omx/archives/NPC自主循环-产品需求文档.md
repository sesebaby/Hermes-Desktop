# 产品需求文档：单个 NPC 驱动的 Stardew 自主循环

## 目标

让一个 Stardew NPC 运行时，优先从 Haley 开始，完成一个真实的自主生命循环：

`self-driven tick -> observe facts -> assemble NPC-local context -> decide -> call NPC-scoped tool -> poll result -> persist trace/activity/memory -> next tick`

硬性的产品规则是：游戏永远不能驱动 Agent。Stardew 和 bridge 只提供事实。

## 问题陈述

Hermes Desktop 已经具备 NPC 运行时骨架、Stardew bridge 命令契约、Haley/Penny persona pack、NPC 命名空间隔离、记忆原语、trace/log 原语，以及手动 `move` / `speak` 调试路径。缺失的是一个由 NPC 自己拥有的自主循环。

当前 `NpcRuntimeInstance.StartAsync` 会把状态切到 `Running` 并创建目录，但它不会让 Haley 持续保持清醒、观察、决策、行动、轮询和记忆。

## 硬性规则

1. 不允许事件驱动的 Agent。
2. 游戏、SMAPI、bridge、proximity、dialogue、inbox 和 scheduler 事件都只是事实。
3. 事件处理器不能调用 LLM completion、`Agent.ChatAsync`、`StardewCommandService.SubmitAsync`、`move` 或 `speak`。
4. 事件处理器不能排队类似“Haley 现在应该回应”的语义命令。
5. 只有 NPC 自主循环 tick 可以进入 observe/decide/tool/action 路径。
6. 提示词组装必须保留在运行时本地的 `ContextManager` / `PromptBuilder` 上；不能再有独立的 Stardew prompt assembler。
7. CLI、typed tools 以及未来 UI 都必须通过 `StardewCommandService`；不能再有并行的 SMAPI HTTP 路径。

## 范围

范围内：
- 在 `src/runtime` 下增加一个有界的 `NpcAutonomyLoop`。
- 先支持一个 NPC profile，优先使用 Haley 默认 profile。
- 为 NPC 状态增加仅事实型的 Stardew observation/query 边界。
- 为 status、move、speak 和 task status 增加 NPC 作用域的 Stardew 工具。
- 依据 `NpcNamespace` 构造运行时本地的 `SoulService`、`MemoryManager`、`TranscriptStore`、`ContextManager` 和 `PromptBuilder`。
- 将 persona pack 材料种子化/复制到 NPC 命名空间中，让 Haley 的 soul/persona 从文件加载。
- 通过现有上下文输入传递 Stardew skills 和 observation facts。
- 将每个 tick 的 trace/activity/memory 持久化到 NPC 命名空间下。
- 在测试中强制执行 `NpcAutonomyBudget`，并且不要求真实游戏运行。

范围外：
- 超过“不破坏现有 discovery”的多 NPC 调度。
- 远程 NPC 聊天、完整 `SocialRouter`、social graph、经济、种植、制作、collect/interact/goto 扩展。
- 应用启动即常驻运行。
- 除最小的状态/服务接线外，不做 UI 重设计。
- 新增第三方依赖。

## 要求

1. `NpcAutonomyLoop.RunOneTickAsync` 必须能在没有真实 Stardew 进程的情况下测试。
2. 一个 tick 必须先观察 bridge/NPC 事实，再做任何 LLM 决策或工具动作。
3. observation facts 必须是被动输入，不能自己触发决策。
4. NPC Agent 只能接收 NPC 安全工具，不能接收全局 Desktop 工具注册表。
5. 运行时本地上下文必须包含 Haley 身份、persona pack 材料、skills、最近的 observation facts、session id 和 NPC memory。
6. move 和 speak 动作必须通过 `StardewCommandService`。
7. 长时间运行的命令必须轮询 `task/status`，直到终态或达到配置上限。
8. trace/activity 记录必须包含足够的身份和命令上下文，以关联 `npcId`、`saveId`、`profileId`、`traceId` 和 `commandId`。
9. 有意义的结果必须通过 NPC 本地的 `MemoryManager` 写成简洁的 NPC memory 条目。
10. 启动/停止运行时必须保留现有 `NpcRuntimeSupervisor.Snapshot()` 语义。

## 验收标准

1. 启动 Haley 会创建运行时命名空间和一个可循环运行的 runtime handle。
2. `RunOneTickAsync` 在调用任何 LLM 或工具前先执行 observe。
3. 注入的假的 event/fact source 会记录事实，但在调用 `RunOneTickAsync` 之前不会产生任何 LLM/tool/command 调用。
4. Haley 上下文通过运行时本地的 `ContextManager` / `PromptBuilder` 准备，而不是通过自定义 Stardew prompt assembler。
5. NPC 工具定义只限于 Stardew status/query、move、speak、启用时的 task status/cancel，以及启用时的 NPC 本地 memory。
6. 一个假的 move 决策通过 `StardewCommandService` 提交 `GameActionType.Move`，捕获 `commandId`，并轮询状态。
7. 一个假的 speak 决策通过 `StardewCommandService` 提交 `GameActionType.Speak`。
8. bridge 不可用或 world-blocked 的事实会产生 no-op/paused trace，而不是重试旋转或强制动作。
9. 完成的 tick 会在 NPC 命名空间下写入 trace/log，并更新 `LastTraceId`。
10. 完成且有意义的 tick 会写入或尝试写入一条简洁的 NPC memory 条目。

## 触点

- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcRuntimeHost.cs`
- `src/runtime/NpcNamespace.cs`
- `src/runtime/NpcAutonomyBudget.cs`
- `src/runtime/NpcRuntimeTrace.cs`
- `src/runtime/NpcRuntimeLogWriter.cs`
- `src/game/core/GameAction.cs`
- `src/game/core/NpcPackManifest.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `src/games/stardew/StardewBridgeDiscovery.cs`
- `src/game/stardew/personas/haley/default/*`
- `skills/gaming/stardew-core.md`
- `skills/gaming/stardew-navigation.md`
- `skills/gaming/stardew-social.md`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- `Desktop/HermesDesktop.Tests/Runtime/*`
- `Desktop/HermesDesktop.Tests/Stardew/*`

## 证据基础

- 差距分析优先解决 Agent 运行时/自主循环。
- 多 NPC 设计说明：NPC agent 自己观察和决策，bridge 只暴露事实。
- 多 NPC 设计要求通过 `ContextManager` / `PromptBuilder` 进行提示词组装。
- 现有代码已经具备运行时命名空间隔离、预算原语、bridge DTO、命令服务、persona packs 和聚焦测试。
