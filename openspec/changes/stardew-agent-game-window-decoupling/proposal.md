## 为什么要改

星露谷 NPC 的世界动作现在还会反复踩中同一类编排错误：游戏里的 UI / 窗口 / 动画生命周期，本该作为状态事实返回给 agent 判断，却会直接把 agent 可见工作卡住，甚至打成终态失败。最近一次私聊后移动回归就是直接例子：等待回复对话框生命周期时，复用了一个很短的通用 defer 预算，结果玩家只是还在看游戏 UI，一个本来合法的 host task 就会被拦死。

这个变更现在就必须做，因为如果不把这条边界修正，后面的任务窗口、交易、采集、制作、任务菜单，以及其他所有依赖 UI 的动作，都会继续在同一类问题上反复失败。

## 变更内容

- 明确把 Stardew agent 流程和游戏执行流程解耦：
  - agent turn 仍然可以继续思考、说话、查状态、更新 todo；
  - 游戏侧等待应当变成 host task / status 事实，而不是 agent 流程锁。
- Stardew 世界动作工具和状态工具统一返回“文本优先、结构保留”的结果：
  - 用简短的 `summary`，直接告诉 agent 当前状态；
  - 同时保留 `status`、`commandId`、`reason` / `errorCode` 以及关联字段，供测试、日志、UI 和后续状态查询使用。
- 收紧私聊 host task 提交等待的边界：
  - 等待私聊回复 UI 生命周期时，不能消耗通用 stale / busy `MaxDeferredIngressAttempts`；
  - 可恢复的 UI 等待继续保持为可恢复的 task / status 状态；
  - 真正的硬阻塞仍然返回终态 blocked 事实。
- 显式定义“单身体执行”边界：
  - 一个 NPC 同一时间最多只有一个正在运行的世界动作槽位；
  - 新来的冲突世界动作要立刻返回可读的 `blocked/action_slot_busy` 事实；
  - host 不能无限排队冲突工作，也不能替换当前动作。
- 更新 Stardew runtime prompt 和 skills，让 agent 明白：
  - 窗口、动画、菜单、事件都是状态事实，不是隐藏执行锁；
  - `stardew_task_status` 才是长时任务和等待任务的续接工具；
  - 终态 blocked / failed 事实出现后，必须由 agent 自己做下一步决策，不能靠 host 代推断。
- 为这类重复故障补回归覆盖：
  - 私聊 UI 等待不能再通过通用 defer 预算把 host task 打成终态 blocked；
  - 通用 busy / stale ingress 行为仍然要按既有预算正确阻塞；
  - 状态 / 动作输出在需要时必须带 agent 可读的 summary。

本次变更明确不做：

- 不实现真实的制作、交易、采集、任务窗口处理器。
- 不重写 `IngressWorkItems`、`ActionSlot`、`PendingWorkItem` 或整个任务系统。
- 不引入第二套队列模型。
- 不让 host 推断或替 agent 选择下一步动作。
- 不把广义状态工具扩成一个大而全的世界扫描器。
- 不顺手解决无关的移动 / 寻路问题。

## 能力影响

### 新增能力

无。本次变更不是新增平行能力，而是收紧现有 Stardew host-task 和 UI 生命周期契约。

### 修改的能力

- `stardew-host-task-runner`：host task 和 status 结果必须变成 agent 可读、带短 summary 的事实；可恢复的游戏等待不能再变成 agent 流程锁或无关的 defer 预算失败；冲突的新世界动作必须在保持单一运行动作槽位的前提下，返回可观察的 blocked 事实。
- `stardew-ui-task-lifecycle`：UI / 窗口生命周期等待必须表现为所属 task / status 事实或 UI lease 冲突，不能再掉进通用 ingress 失败；私聊回复显示 / 关闭也不能再被视为阻断无关世界动作提交的理由。
- `stardew-orchestration-harness`：harness 必须证明 agent / game 边界是非阻塞的，状态 summary 是文本优先的，并覆盖私聊 UI 等待回归，以及现有通用 busy / stale 阻塞行为仍被保留。

能力复用矩阵：

- agent 可见的 host task 执行，本来就属于 `stardew-host-task-runner`；这次只是收紧结果形状和等待分类。
- UI lease / 菜单安全，本来就属于 `stardew-ui-task-lifecycle`；这次只是明确可恢复 UI 等待是 task 事实，不是通用 ingress 失败。
- 回归证明，本来就属于 `stardew-orchestration-harness`；这次是补充必需用例，不是新建一套 harness 能力。

## 影响范围

- 代码：
  - `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
  - `src/games/stardew/StardewNpcTools.cs`
  - `src/runtime/NpcAutonomyBudget.cs` 仅在命名或边界拆分确有需要时调整，不能拿来放大全局重试上限
  - `src/Core/Agent.cs` 仅在需要澄清状态工具预算或续接语义时调整
- Prompt 与 skill 资产：
  - `skills/gaming/stardew-core/SKILL.md`
  - `skills/gaming/stardew-task-continuity/SKILL.md`
  - `skills/system/stardew-npc-runtime/SYSTEM.md`
- 测试：
  - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
  - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
  - 现有 harness 支持到的 prompt / skill 边界测试
- Specs：
  - `stardew-host-task-runner` 的 delta spec
  - `stardew-ui-task-lifecycle` 的 delta spec
  - `stardew-orchestration-harness` 的 delta spec
- Error memory：
  - 更新或新增一条 `openspec/errors` 记录，明确写下“游戏 UI / 窗口生命周期等待不能复用通用 agent / ingress 重试预算”。
- 依赖：
  - 不新增运行时依赖。
  - 不计划引入会破坏 schema 的外部 API 依赖。
