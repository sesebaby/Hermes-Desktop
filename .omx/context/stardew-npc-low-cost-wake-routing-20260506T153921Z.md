# Stardew NPC 低成本唤醒与本地执行路由上下文

## task statement

用户已批准 `$ralplan` 后自动 `$ralph` 执行：落地 Stardew NPC 低成本唤醒、`schedule_cron` 续跑语义、本地小模型执行路由收敛方案。

## desired outcome

- 空闲 NPC 不再按 2 秒高频消耗 LLM。
- `schedule_cron` 成为 agent 表达“稍后继续”的标准入口。
- `move / observe / task_status` 继续由本地小模型执行层承担工具调用。
- `wait` 不作为常规世界动作鼓励模型滥用。
- 宿主只做事实、事件、工具、确认和执行结果，不替 NPC 决策。

## known facts/evidence

- `openspec/project.md` 明确要求宿主不替 NPC 决策。
- `external/hermes-agent-main` 的 todo 是 session task list，不做 blocked todo 自动恢复；到点继续走 cron。
- 当前 `StardewNpcAutonomyBackgroundService` 默认 poll interval 仍是 `TimeSpan.FromSeconds(2)`。
- 当前 `NpcAutonomyLoop.BuildDecisionMessage` 暴露 `wait` 和 `blocked`，但还未明确 `schedule_cron` 作为稍后继续方式。
- 当前工作区已有本地执行器相关改动：`wait/escalate` 宿主解释，`move/observe/task_status` 选择本地执行工具，并记录 `executorMode`。

## constraints

- 不实现 `blocked todo` 自动恢复或自动唤醒。
- 不新增 Stardew 第二套任务系统。
- 不跑全量测试，只跑聚焦测试。
- 不杀游戏/桌面进程。
- 不回退已有工作区改动。

## unknowns/open questions

- 手测中模型是否会稳定主动使用 `schedule_cron`，需要后续日志观察。
- 20 秒默认 cadence 是否过慢，后续可基于手测体验调到 15-30 秒区间内。

## likely codebase touchpoints

- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
