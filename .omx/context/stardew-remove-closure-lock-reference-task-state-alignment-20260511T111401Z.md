# Stardew Remove Closure Lock Reference Task-State Alignment Context

## Task statement

用户明确要求删除 Stardew NPC runtime 中的 `blocked_until_closure` 自然语言收口锁，并采用参考项目 `external/hermescraft-main` 的方案形状：玩家请求进入队列，真实动作由单一当前任务槽执行，进度/完成/失败作为事实反馈给 agent，agent 用工具或 todo 自己收尾。

这不是继续把现有锁“改窄”，而是把锁从执行路径移除。

## Desired outcome

- 删除或废弃会阻断真实 world action 的 `blocked_until_closure` / `BlockedUntilClosure` 语义。
- 不再因为 `closure_missing`、自然语言 `wait:`、`no-action:` 文本解析失败而阻断后续私聊回复或玩家委托 move。
- 保留可观测事实：last terminal result、recent activity、action history、repeated failure/action_loop、ingress defer diagnostics。
- 执行安全改由现有/参考对齐机制承担：`ActionSlot` / `PendingWorkItem` 单任务槽、`IngressWorkItems` 队列、`stardew_task_status` 进度查询、`todo` 任务状态、bridge terminal result。
- 私聊即时行动对齐 hermescraft whisper/direct mention：进入待处理请求队列，不被旧动作链锁拦截。

## Reference evidence

### hermescraft-main

- `external/hermescraft-main/bot/server.js:107` 定义 `commandQueue`，用于保存玩家复杂请求。
- `external/hermescraft-main/bot/server.js:108` 定义 `currentTask`，作为单一 background task state。
- `external/hermescraft-main/bot/server.js:243` 非广播/direct message 会 push 到 `commandQueue`。
- `external/hermescraft-main/bot/server.js:353` whisper 直接 push 到 `commandQueue`。
- `external/hermescraft-main/bot/server.js:726` `briefState()` 暴露 pending command 数量。
- `external/hermescraft-main/bot/server.js:757` 到 `:763` 暴露 `task_stuck` / running task / `task_done` / `task_error`。
- `external/hermescraft-main/bot/server.js:1514` `complete_command` 只把 queued command 标 completed，不靠自然语言闭环锁阻断动作。
- `external/hermescraft-main/bot/server.js:2619` 如果 `currentTask.status === running`，新 background task 返回 409 busy。
- `external/hermescraft-main/bot/server.js:2623` 到 `:2639` background task 启动后更新 currentTask done/error 和 actionHistory。
- `external/hermescraft-main/bot/server.js:2687` 到 `:2696` stuck watchdog 把 running task 标 stuck，不靠 closure lock。
- `external/hermescraft-main/SOUL-minecraft.md:47` 到 `:50` 要求先处理 queued requests，完成后 chat 再 `mc complete_command`。
- `external/hermescraft-main/SOUL-minecraft.md:62` 到 `:71` 长任务用 background task，期间 poll `mc task` 和 `mc read_chat`，不用 sleep。

### Current Hermes-Desktop evidence

- `src/runtime/NpcRuntimeDescriptor.cs:62` 已有 `NpcRuntimeIngressWorkItemSnapshot`，可对应参考项目 `commandQueue`。
- `src/runtime/NpcRuntimeDescriptor.cs:54` 已有 `NpcRuntimeActionSlotSnapshot`，可对应参考项目 `currentTask`。
- `src/games/stardew/StardewNpcTools.cs:1139` `StardewRuntimeActionController.TryBeginAsync` 是真实动作入口。
- `src/games/stardew/StardewNpcTools.cs:1144` 到 `:1153` 已经在 pending/action slot 存在时返回 `action_slot_busy`。
- `src/games/stardew/StardewNpcTools.cs:1455` 到 `:1477` 当前 action-chain guard 会把动作变成 `blocked_until_closure` 并返回 blocked，问题就在这里。
- `src/runtime/NpcAutonomyLoop.cs:403` 到 `:416` 当前 prompt 会把 `blocked_until_closure` 注入给 agent 并要求先收口。
- `src/runtime/NpcAutonomyLoop.cs:1296` 到 `:1334` 当前 `closure_missing` 会累积并最终把 chain 设为 `blocked_until_closure`。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:1405` 到 `:1435` 上一版修补只是在 delegated move 前恢复 stale closure guard，仍然保留了锁模型。
- `skills/gaming/stardew-task-continuity/SKILL.md:17`、`:59`、`:60` 目前还在指导 agent 遇到 `blocked_until_closure` 时不要继续动作，需要改成参考项目的 task/status/complete/todo 收尾语言。

## Constraints

- 不新增第二套任务系统；继续使用 `IngressWorkItems`、`ActionSlot`、`PendingWorkItem`、`LastTerminalCommandStatus`、`SessionTodoStore`。
- 不把宿主改成剧情导演；宿主只接队列、执行动作、反馈状态、记录事实。
- 不把地点/NPC/台词硬编码进修复。
- 不用自然语言文本格式作为关键安全门。
- 不允许旧 `action_chain_json` 的历史 `blocked_until_closure` 再拦截新动作。

## Unknowns / decisions for execution

- 是否彻底删除 `NpcRuntimeActionChainGuardSnapshot`，还是先保留为兼容读取的 `ActionHistory`/diagnostic state。倾向：短期保留字段兼容旧 state，但移除所有执行阻断分支；后续单独清理命名。
- 是否删除 `ClosureMissing` 常量。倾向：可保留为历史 diagnostic code，但不再触发 block。
- 是否迁移旧 state。倾向：读取到旧 `blocked_until_closure` 时当作历史 diagnostic；下一次 action 接受时覆盖为 open/diagnostic 或清空，不需要手动数据库迁移。

## Likely codebase touchpoints

- `src/games/stardew/StardewNpcTools.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcAutonomyBudget.cs`
- `src/runtime/NpcRuntimeDescriptor.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `skills/gaming/stardew-task-continuity/SKILL.md`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
