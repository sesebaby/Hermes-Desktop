# Stardew NPC 低成本唤醒与本地执行路由实施方案

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**目标:** 降低 Stardew NPC 空转 LLM 调用，保留 agent 自主决策，让 `schedule_cron`、事件、命令完成和本地执行层成为主要唤醒/执行路径。

**架构:** 对齐 `openspec/project.md` 和 `external/hermes-agent-main`：`todo` 是 agent 自己维护的任务本子，不是宿主自动恢复器；需要稍后继续时由 agent 用 `schedule_cron` 表达，宿主只负责到期 ingress、事实、工具结果和安全/调度门控。低风险世界动作继续走本地小模型执行层，宿主不替 NPC 选目标、不把观察事实自动变成行动。

**Tech Stack:** C#/.NET 10, WinUI host, Stardew SMAPI bridge, Hermes runtime, `SessionTodoStore`, `ScheduleCronTool`, `NpcAutonomyLoop`, `NpcLocalExecutorRunner`, `StardewNpcAutonomyBackgroundService`.

---

## 白话结论

参考项目不是靠 `blocked todo` 自动复活任务。它的 `todo` 只有 `pending / in_progress / completed / cancelled`，只在压缩后把 `pending / in_progress` 注入上下文；真正“到点再跑”走 `cronjob`，调度器到期后启动一次新的 agent run。

所以本项目也不要做“宿主看到 blocked 就自动恢复/自动唤醒”。这会让宿主替 NPC 决策，违反 `openspec/project.md` 的边界。

推荐路线是：

- `blocked` 继续表示“当前不能推进”，不会自动恢复。
- 如果 NPC 只是想稍后再试，应该保持/写入 `pending` 或 `in_progress`，并调用 `schedule_cron` 预约下一次继续。
- `schedule_cron` 到期后，宿主只追加 ingress，让 NPC 自己在下一轮决定怎么继续。
- 空闲状态不要每 2 秒打大模型；低频唤醒加事件/cron/命令完成即时唤醒。
- `wait` 不再作为普通世界动作鼓励模型输出；明显没法行动的状态由宿主暂停调度，不消耗 LLM turn。

## 参考项目对齐证据

- `external/hermes-agent-main/tools/todo_tool.py:13`：todo 设计说明是“不改 system prompt、不改 tool response”，行为约束在 tool schema。
- `external/hermes-agent-main/tools/todo_tool.py:22`：todo 状态只有 `pending / in_progress / completed / cancelled`，没有 `blocked` 自动恢复概念。
- `external/hermes-agent-main/tools/todo_tool.py:90` 和 `external/hermes-agent-main/tools/todo_tool.py:108`：压缩后只注入 `pending / in_progress`。
- `external/hermes-agent-main/run_agent.py:4426`：新 agent 从历史里恢复最近 todo tool response。
- `external/hermes-agent-main/run_agent.py:8125`：压缩后把 todo snapshot 注入上下文。
- `external/hermes-agent-main/run_agent.py:8275` 和 `external/hermes-agent-main/run_agent.py:8280`：`todo` 是 agent-level tool，直接接当前 session 的 store。
- `external/hermes-agent-main/cron/scheduler.py:5`：cron scheduler 每 60 秒检查 due jobs。
- `external/hermes-agent-main/cron/scheduler.py:782` 和 `external/hermes-agent-main/cron/scheduler.py:1038`：cron 到期后创建 `AIAgent` 并跑 `run_conversation(prompt)`。
- `external/hermes-agent-main/cron/scheduler.py:1207` 和 `external/hermes-agent-main/cron/scheduler.py:1219`：先取 due jobs，再推进 next run，保持调度语义。

## 当前项目现状

- `src/runtime/AgentCapabilityAssembler.cs:41` 注册 `todo`，`src/runtime/AgentCapabilityAssembler.cs:43` 注册 `schedule_cron`。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:143` 当前默认 host poll 是 2 秒。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:144` 和 `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:399` 已订阅 cron 到期。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:425` 和 `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:928` 已把到期 cron 转成 `scheduled_private_chat` ingress。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:463` 优先推进已有 pending command，`src/games/stardew/StardewNpcAutonomyBackgroundService.cs:466` 优先处理 ingress。
- `src/tasks/SessionTodoStore.cs:82` 和 `src/tasks/SessionTodoStore.cs:86` 当前 active task 只注入 `pending / in_progress`。
- `src/runtime/NpcAutonomyLoop.cs:287` 当前 parent JSON contract 还暴露 `wait` 和 `taskUpdate.status=blocked`。
- `src/runtime/NpcLocalExecutorRunner.cs:41` 和 `src/runtime/NpcLocalExecutorRunner.cs:85` 当前 `wait/escalate` 是 host interpreted，没有走本地模型。
- `src/runtime/NpcLocalExecutorRunner.cs:172` 到 `src/runtime/NpcLocalExecutorRunner.cs:174` 当前本地执行层只为 `move/task_status/observe` 选择真实 Stardew 工具。

## 推荐方案

采用“低频主脑 + 事件驱动唤醒 + 本地执行层”的单一路线。

不做：

- 不做 `blocked todo` 自动恢复。
- 不让宿主根据 `blocked reason` 判断何时继续。
- 不新增 Stardew 专属 `defer/pause` 任务系统。
- 不把 `wait` 当世界动作暴露给模型高频输出。
- 不让宿主在菜单、动画、睡眠、command running 时替 NPC 决策。

要做：

- 降低空闲 LLM cadence，默认从 2 秒改为 20 秒左右。
- 保留 cheap host poll / event poll，但如果没有新事件、没有 ingress、没有 command 状态变化、没有到期 wake，就不启动 LLM turn。
- `schedule_cron` 作为 agent 表达“稍后继续”的标准能力。
- parent prompt 从“遇到做不了就 blocked/wait”调整为“短暂无法行动时用 `schedule_cron` 预约继续；长期无法完成才 `blocked/failed`”。
- 本地执行层继续负责 `move / observe / task_status` 的工具参数生成和调用。
- `speak` 仍由 parent contract 的 `speech` 表达内容，但宿主只负责执行 `stardew_speak`，不生成话术。
- 对特殊状态只做事实和调度门控：菜单打开、NPC 正在移动、节日/剧情动画、睡眠/结算、已有 command running 时，不消费新的主 LLM turn。

## 验收标准

- 空闲无事件时，不再每 2 秒出现 `Stardew autonomy LLM turn started`。
- `schedule_cron` 到期后，能看到 `scheduled_private_chat` ingress 入队，并在下一轮被 NPC 自主处理。
- `blocked` todo 不会被宿主自动改成 `pending/in_progress`。
- NPC 如果想稍后继续，应该能通过 `schedule_cron` 安排下一次继续。
- `move / observe / task_status` 仍由本地执行层调用 Stardew 工具。
- `wait` 不再成为最常见的模型输出动作；明显不可行动状态不消耗主 LLM turn。
- 日志能区分：host skipped、cron ingress、command running、command terminal、LLM turn started、local executor called。

## 文件职责

- Modify `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
  - 调整默认 poll/LLM cadence。
  - 增加或收敛 skip reason，避免无变化空转进 LLM。
  - 保持 cron ingress 和 command advance 优先。

- Modify `src/runtime/NpcAutonomyLoop.cs`
  - 调整 parent decision prompt。
  - 弱化 `wait`，强调 `schedule_cron` 是“稍后继续”的表达方式。
  - 保留 `taskUpdate.blocked` 仅表示真实阻塞，不表示“稍后再试”。

- Modify `src/runtime/NpcLocalActionIntent.cs`
  - 如需要，收窄 `wait` 的 contract 语义，或保留解析但不鼓励 prompt 使用。
  - 不新增宿主决策型 action。

- Modify `src/runtime/NpcLocalExecutorRunner.cs`
  - 保持 `wait/escalate` 不走模型；必要时把 wait 记录为调度意图而不是世界动作。
  - 继续只给本地模型 `stardew_move / stardew_task_status / stardew_status`。

- Modify `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
  - 加 idle cadence / skip / cron ingress / command running 的窄测试。

- Modify `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
  - 加 prompt contract 测试，确认 `schedule_cron` 指导存在，`wait` 不被鼓励。

- Modify `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs`
  - 确认 `wait` 不调用本地模型，`move/observe/task_status` 继续调用本地模型。

## 实施任务

### Task 1: 固定参考项目边界

**Files:**
- Modify: `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- Modify: `src/runtime/NpcAutonomyLoop.cs`

- [ ] **Step 1: 写 prompt 测试**

覆盖点：

- prompt 明确说 `schedule_cron` 是稍后继续的工具。
- prompt 不再鼓励把“稍后再试”写成 `blocked`。
- prompt 不再鼓励普通 `wait`。

Run:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcAutonomyLoopTests"
```

Expected: 新测试先失败。

- [ ] **Step 2: 修改 `BuildDecisionMessage`**

改动原则：

- 保留 raw JSON contract。
- 保留宿主不决策边界。
- 加一句白话规则：如果只是需要以后再继续，用 `schedule_cron`；`blocked` 只表示当前任务真的被阻断。
- `wait` 描述改成兜底/极少使用，不作为正常 idle 行为。

- [ ] **Step 3: 跑窄测试**

Run:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcAutonomyLoopTests"
```

Expected: PASS。

### Task 2: 降低空闲 LLM cadence

**Files:**
- Modify: `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- Modify: `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`

- [ ] **Step 1: 写 idle cadence 测试**

覆盖点：

- 默认 poll interval 不再是 2 秒，目标 20 秒。
- 无事件、无 ingress、无 command、未到 `NextWakeAtUtc` 时，不启动 LLM turn。
- 有 event / ingress / command terminal 时仍能尽快处理。

Run:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Stardew.StardewNpcAutonomyBackgroundServiceTests"
```

Expected: 新测试先失败。

- [ ] **Step 2: 修改默认 cadence**

推荐：

- host loop 默认 `PollInterval = 20s`。
- 如果已有配置明确传入，尊重配置。
- 不用宿主主动决定 NPC 行为，只决定“现在是否值得启动一轮 LLM”。

- [ ] **Step 3: 增加 skip 日志**

日志必须能看懂：

- `reason=idle_no_event_no_ingress`
- `reason=command_running`
- `reason=restart_cooldown`
- `reason=private_chat_active`
- `reason=world_state_blocked` 如果后续有菜单/剧情/睡眠事实门控

### Task 3: 保持 cron 为标准“稍后继续”入口

**Files:**
- Modify: `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- Optionally Modify: `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`

- [ ] **Step 1: 补 cron 到期验收测试**

现有测试已覆盖 `CronTaskDue_WhenTaskBelongsToNpcSession_AppendsDurableIngressBeforeSubmitting`。新增断言：

- cron 到期不直接执行世界动作。
- cron 到期只入队 ingress。
- 下一轮 worker 自己处理 ingress。
- 日志能看到 task id / npc id / ingress id。

- [ ] **Step 2: 保持当前实现，不新增 blocked 恢复**

不要让 `SessionTaskProjectionService.SnapshotChanged` 触发 Stardew NPC wake。

原因：

- 参考项目没有这个语义。
- todo 状态变化是 agent 自己的任务记录，不是世界事实。
- 宿主订阅它自动恢复会越过“宿主不替 NPC 决策”的边界。

### Task 4: 收敛 wait 语义

**Files:**
- Modify: `src/runtime/NpcLocalExecutorRunner.cs`
- Modify: `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs`
- Optionally Modify: `src/runtime/NpcLocalActionIntent.cs`

- [ ] **Step 1: 写 wait 不调用模型测试**

当前已有 `wait/escalate` host interpreted 行为，测试要固定它：

- `wait` 不调用本地模型。
- `wait` 不提交世界动作。
- `wait` 只记录为 completed/host_interpreted 或后续改名后的 scheduling intent。

- [ ] **Step 2: 如果改名，保持兼容最小化**

推荐暂不改 enum，避免大面积重构。先通过 prompt 和日志语义收敛，不做 action schema 破坏。

### Task 5: 验证本地小模型仍承担低风险工具调用

**Files:**
- Modify: `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs`
- Modify: `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`

- [ ] **Step 1: 覆盖 `move / observe / task_status`**

确认：

- `move` 仍选择 `stardew_move`。
- `observe` 仍选择 `stardew_status`。
- `task_status` 仍选择 `stardew_task_status`。
- `speech` 不走本地模型，而是 parent contract 内容 + 宿主执行。

- [ ] **Step 2: 运行窄测试**

Run:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcLocalExecutorRunnerTests|FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcAutonomyLoopTests"
```

Expected: PASS。

### Task 6: 手测日志验收

**Files:**
- No source change.

- [ ] **Step 1: 启动游戏和 Hermes**

不要跑全量测试，不要杀游戏。

- [ ] **Step 2: 看日志**

Commands:

```powershell
Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\hermes.log" -Tail 300 -Wait
Get-ChildItem "$env:LOCALAPPDATA\hermes\hermes-cs\runtime\stardew\games\stardew-valley" -Recurse -Filter runtime.jsonl | Sort-Object LastWriteTime -Descending | Select-Object -First 5
```

Expected evidence:

- 空闲时 host 有 skip 日志，但没有持续 LLM turn。
- 有私聊/cron/command terminal 时能重新进入 NPC turn。
- `move/observe/task_status` 仍出现 `local_executor` 证据。
- `wait` 数量明显下降。

## 风险和处理

- 风险：cadence 太低导致 NPC 反应慢。
  - 处理：事件、私聊、cron、command terminal 不走纯 idle cadence；它们仍可快速触发。

- 风险：去掉 wait 语义后 NPC 不知道如何表达“现在不做”。
  - 处理：不删除 `wait` 解析，只是不鼓励；真正稍后继续用 `schedule_cron`。

- 风险：模型不会主动用 `schedule_cron`。
  - 处理：先 prompt + 测试固定；如果手测仍不使用，再考虑把“长期等待建议”写进 Stardew navigation skill，而不是宿主自动做。

- 风险：`blocked` 被过滤出 active task，长期阻塞任务不再出现。
  - 处理：这是合理语义。要继续的任务不要标 `blocked`；标 `pending/in_progress` 并用 `schedule_cron` 预约。

## 不做项

- 不实现 `blocked todo` 自动恢复。
- 不新增第二套 Stardew 任务系统。
- 不让宿主根据地图/时间/菜单状态替 NPC 决策。
- 不让宿主自动把观察事实转成行动。
- 不跑全量测试，除非用户之后明确要求。

## 推荐执行顺序

1. 先做 Task 1 和 Task 4，收敛语义，风险最低。
2. 再做 Task 2，降低真实成本。
3. 再做 Task 3，确认 cron 是“稍后继续”的主通道。
4. 最后做 Task 5/6，验证本地执行层和手测日志。

理由：先改语义和测试，避免直接调 cadence 后不知道行为变化是不是预期；再改调度，最后用日志验证真实运行成本和 NPC 行为。

## RALPLAN / RALPH 收口记录

状态：计划已进入执行并完成本轮代码落地；`ralplan` 不再停留在 planning/reviewing。

本轮已完成：

- Prompt 收敛：`schedule_cron` 明确作为“稍后继续”的标准表达；`blocked` 只表示真实阻塞；`wait` 只作为兜底调度意图。
- 默认 cadence：Stardew NPC autonomy 默认 `PollInterval` 从 2 秒改为 20 秒，显式配置仍可覆盖。
- 本地执行器行为固定：`move / observe / task_status` 仍走本地执行层；`wait / escalate` 不调用本地模型。
- Cron 语义保持：到期只追加 `scheduled_private_chat` ingress，不直接执行世界动作，不做 `blocked todo` 自动恢复。

验证证据：

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcAutonomyLoopTests"`：28 passed。
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcLocalExecutorRunnerTests"`：10 passed。
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --no-build --filter "Name=Constructor_WithoutExplicitPollInterval_UsesLowCostIdleCadence|Name=Constructor_WithExplicitPollInterval_PreservesOverride|Name=CronTaskDue_WhenTaskBelongsToNpcSession_AppendsDurableIngressBeforeSubmitting|Name=RunOneIterationAsync_WhenPendingActionIsRunning_PausesWithoutStartingNewChat"`：4 passed。
- `git diff --check`：无空白错误。

未完成/保留：

- 未跑全量测试，遵守用户“不跑全量测试”的约束。
- `StardewNpcAutonomyBackgroundServiceTests` 类级筛选曾超时，本轮只验证直接相关方法。
- 未实现 `blocked todo` 自动恢复；这是有意不做，避免宿主替 NPC 决策。
