# 计划标题

星露谷 NPC 常驻智能体架构增强共识规划

## Plan Summary

目标是在保持 `Desktop` 单进程托管多个 NPC runtime 的前提下，把当前 `StardewNpcAutonomyBackgroundService` 的中心轮询形态，演进为“每个 NPC 拥有自己的常驻 driver/controller、收件箱、行动治理和可恢复任务”的架构；同时继续复用现有 `ContextManager` / `PromptBuilder` / `AgentCapabilityAssembler` 主链，不新造第二套 Stardew prompt/agent 装配链。

当前证据基础：
- 独立 namespace / SOUL / memory / transcript 已存在：`src/runtime/NpcNamespace.cs`、`src/runtime/NpcRuntimeContextFactory.cs`
- 私聊 session lease 已存在：`src/runtime/NpcRuntimeInstance.cs`、`src/game/core/PrivateChatOrchestrator.cs`
- 自治仍由中心服务轮询：`src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- runtime 还没有每 NPC 自己的收件箱、后台驱动任务、行动状态机：`src/runtime/NpcRuntimeInstance.cs`、`src/runtime/NpcRuntimeSupervisor.cs`
- 世界资源冲突已经有最小基础：`src/runtime/ResourceClaimRegistry.cs`、`src/runtime/WorldCoordinationService.cs`

## RALPLAN-DR

### Principles

1. **同源优先**：桌面 agent 与 NPC agent 只能在 `namespace / persona / session / body` 上不同，不能分叉出第二条 prompt、context、tool 装配主链。
2. **单进程多 runtime**：保持 `Desktop` 单进程托管，常驻性来自“每 NPC 一个 runtime 驱动任务”，不是“一 NPC 一 OS 进程”。
3. **控制面围绕 runtime 内聚**：会话租约、行动槽位、收件箱、恢复点、事件游标必须围绕单 NPC runtime 建模，但实现上拆成 `NpcRuntimeInstance`（身份/快照根）+ per-NPC `driver/controller`（运行控制）+ 宿主服务（全局 attach/预算）。
4. **显式仲裁优于隐式串行**：私聊、自治、移动/说话等冲突通过租约、槽位、短占位解决，不靠“谁先 tick 到谁赢”。
5. **先补可恢复性，再补复杂智能**：先锁定事件序号、命令状态、恢复点和唤醒契约，再增加 action arbitration、社会行为和多任务策略。

### Decision Drivers

1. **必须满足“桌面 agent 和 NPC 本质等同”**：不能再保留 NPC 专属控制主链，否则会持续偏离 `ContextManager` / `AgentCapabilityAssembler`。
2. **必须把中心轮询改造成宿主管理，而不是行为大脑**：否则一旦增加收件箱、任务恢复、行动槽位，`StardewNpcAutonomyBackgroundService` 会继续膨胀。
3. **必须补齐可靠性缺口**：参考项目已证明“单后台任务 + 轮询 + 无恢复”会导致任务覆盖、状态丢失和可观测性不足，本仓库要提前补齐。
4. **必须正视 host / runtime 张力**：全局 LLM 并发预算、bridge attach / rebind、pack discovery 天然属于宿主职责，不能伪装成单 NPC 内聚状态。

### Viable Options

#### 方案 A：增强现有中心轮询服务，继续由 `StardewNpcAutonomyBackgroundService` 持有所有 NPC 的控制状态

边界：
- `NpcRuntimeInstance` 继续只做轻量状态容器
- 收件箱、行动槽位、租约表、重启/恢复逻辑主要放进 `StardewNpcAutonomyBackgroundService`
- `NpcRuntimeSupervisor` 继续负责 handle 复用

Pros：
- 改动最集中，短期最容易落地
- 现有 `RunNpcIterationAsync(...)` 可逐步扩展
- 对现有私聊和 debug tick 兼容成本较低

Cons：
- 违反“控制面内聚到 runtime”的方向，形成第二套 Stardew 专属运行主脑
- 很难实现“桌面 agent 与 NPC 等同”，因为生命周期治理仍在 NPC 专属服务外壳里
- 收件箱、恢复点、任务状态都只能存在于中心服务内存态，重启与观测边界差
- 后续若扩到更多游戏或 body，中心服务会持续膨胀

#### 方案 B：控制面围绕 runtime 下沉，采用 `Instance + per-NPC driver/controller`，中心服务退化为宿主/事件泵/健康监视

边界：
- `NpcRuntimeInstance` 保持共享身份根、lease 快照、handle 代际、持久快照根
- 新增 per-NPC `driver/controller` 承载收件箱、事件游标、pending work item、action slot、恢复点、命令轮询
- `NpcRuntimeSupervisor` 负责创建与查询 runtime，但不持有行为循环细节
- `StardewNpcAutonomyBackgroundService` 负责 bridge attach、事件抓取/分发、全局预算、公平性和启动/停止宿主
- 继续复用 `NpcRuntimeSupervisor.GetOrCreateAutonomyHandleAsync(...)` 与 `NpcRuntimeContextFactory`

Pros：
- 最符合“同一个 Hermes agent，只是 namespace/persona/session/body 不同”的约束
- 常驻任务、会话租约、行动槽位、恢复点都能围绕单 NPC runtime 建模
- 可以在不改 prompt 主链的前提下，把自治、私聊、未来社会行为都并到同一控制面
- 便于做 per-NPC 可观测性、快照、恢复和测试

Cons：
- 需要重构 `NpcRuntimeInstance` 和 `NpcRuntimeSupervisor` 的职责
- 需要明确 runtime 内部状态边界，避免把 bridge 细节反向泄漏进通用 runtime
- 第一阶段测试量会明显增加

#### 方案 C：直接做完整 actor/event-sourcing 调度框架

边界：
- 引入通用事件总线、持久化事件账本、任务队列、完整 actor mailbox 和 replay
- Stardew 只是一个 adapter

Pros：
- 长期扩展性最强
- 理论上最适合多游戏、多 body、多社会行为

Cons：
- 当前范围过大，明显超出本次“基于现有仓库补齐常驻 NPC runtime”的任务
- 很容易演变成重构平台而不是修补当前 Stardew 路径
- 与“不新增第二套主链”和“尽量贴当前文件边界”的要求冲突

### 最终选择

选择 **方案 B：控制面围绕 runtime 下沉，采用 `Instance + per-NPC driver/controller`，中心服务退化为宿主/事件泵/健康监视**。

原因：
- 它同时满足三个硬约束：`单进程多 runtime`、`Desktop/NPC 同源`、`不新增第二套装配主链`。
- 它比参考项目更完善的关键，不是“更复杂的 prompt”，而是把参考项目缺失的收件箱、租约、行动槽位、短占位、恢复点、序号事件流，收敛成每 NPC runtime 的统一控制语义。
- 它避免继续把 Stardew 特有状态写死在 `StardewNpcAutonomyBackgroundService`，同时也避免把 adapter/bridge/全局预算一股脑塞进 `NpcRuntimeInstance`。

## 推荐方案

### 总体结构

把当前架构拆成三层：

1. **宿主层**
   - 现有：`src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
   - 目标：只负责发现 bridge、读取事件、启动/停止 runtime、汇报健康，不再直接决定每个 NPC 下一步行为。

2. **runtime 控制层**
   - 核心：`src/runtime/NpcRuntimeInstance.cs` + 新增 per-NPC `driver/controller`
   - `NpcRuntimeInstance` 负责：
     - 共享身份根
     - lease / rebind / restart / snapshot 根数据
     - handle 挂载点
   - per-NPC `driver/controller` 负责：
     - 收件箱
     - 驱动任务句柄
     - 事件游标
     - pending work item
     - action slot
     - 命令状态轮询
     - 恢复点

3. **body / 游戏适配层**
   - 现有：`src/games/stardew/StardewEventSource.cs`、`StardewGameAdapter.cs`、`StardewCommandService.cs`
   - 目标：提供“带序号事件流 + 命令状态查询 + 世界快照”，让 runtime 控制层可以用统一语义工作。

### 重点补齐项

#### 1. 收件箱

落点：
- 主体放在 `src/runtime/`，不放在 `src/games/stardew/`
- `NpcRuntimeInstance` 暴露共享身份与快照接口
- per-NPC `driver/controller` 暴露“追加事件 / 读取待处理项 / 推进游标 / 唤醒驱动”的运行接口

建议边界：
- 新增 runtime 级事件模型，例如：
  - 外部世界事件
  - 私聊事件
  - 命令状态变更事件
  - 定时唤醒事件
  - 系统恢复事件
- 事件游标显式分两层：
  - **Bridge 源游标**：宿主层持有，表示“已经从 bridge 拉取到了哪里”
  - **Runtime inbox ack 游标**：per-NPC driver 持有，表示“该 NPC 已确认消费到了哪里”
- 宿主层补一个 **durable ingress staging / fanout journal**：
  - bridge 拉到的新事件先进入 staging
  - 只有目标 NPC inbox durable append 成功后，才允许视为已完成 fanout
  - 只有 fanout 完成，才允许前移 `Bridge 源游标`
- `StardewNpcAutonomyBackgroundService` 只负责把 `StardewEventSource` 拉到的桥接事件转成 runtime inbox item，再投递给对应 NPC
- `PrivateChatOrchestrator` 不直接驱动 autonomy，只通过租约和 inbox 交互改变 runtime 状态
- `StardewPrivateChatBackgroundService` 不再作为独立后台服务保留；它改造成由宿主内部调用的 private-chat inbox 事件适配组件，不再拥有独立 `Start/Stop/PollAsync` 生命周期，bridge `PollAsync` 的唯一 owner 必须是宿主事件泵

为何这样做：
- 这样私聊、自治、将来社会消息都能共享一条 runtime 收件箱语义
- 避免再造 “Stardew private-chat state machine + autonomy loop state machine + future social loop” 三套并行调度器

#### 2. 会话租约

现状：
- 已有 `IPrivateChatSessionLeaseCoordinator` 和 `NpcRuntimeInstance.AcquirePrivateChatSessionLease(...)`

增强方向：
- 保持现有私聊 API 不破坏
- 在 `NpcRuntimeInstance` 内部把私聊 lease 提升为 runtime arbitration 的一个明确维度
- snapshot 中增加：
  - 当前 lease 持有者
  - lease 类型
  - lease 代数
  - 被暂停的行动槽位

约束：
- 不把所有互斥都粗暴塞进 lease；lease 只解决“私聊占用会话/注意力”的问题
- 行动执行冲突交给行动槽位与资源短占位

#### 3. 行动槽位

建议模型：
- `Session` 槽位：私聊或高优先级对话控制
- `Autonomy` 槽位：长期自治驱动
- `Action` 槽位：单个身体动作执行窗口

设计要点：
- 一个 NPC 可以同时“持有 autonomy driver + 等待命令状态回报”，但同一时刻只能有一个 `Action` 槽位持有者提交可冲突动作
- 私聊 lease 抢占 `Session`，并可暂停 `Autonomy`
- `Action` 槽位上记录：
  - work item id
  - command id
  - trace id
  - started at
  - timeout / stuck deadline

落点：
- 运行主状态在 per-NPC `driver/controller`
- `NpcRuntimeInstance` 只保留 action slot 的快照投影与持久快照根
- 世界冲突判定继续调用 `WorldCoordinationService` / `ResourceClaimRegistry`

#### 4. 资源短占位

现状：
- `ResourceClaimRegistry` 已支持同 NPC 互斥、tile/object 冲突

增强方向：
- 明确它是“短占位”，不是长期所有权
- 与 `Action` 槽位绑定：`Action` 槽位释放时必须顺带释放 claim
- claim key 统一使用 `commandId` 或 `workItemId`
- 对 blocked / expired / cancelled / retry 路径补齐释放与续租语义

建议不做的事：
- 本阶段不把 claim 落磁盘做强恢复；先保证进程内一致性和明确释放
- 跨进程共享不是当前目标，因为明确保持单 Desktop 进程

#### 5. 可恢复后台任务

缺口：
- 当前 autonomy 是 `RunOneTickAsync(...)` 的单次执行，没有 per-NPC 常驻 work item

建议模型：
- 每个 NPC runtime 维护一个“常驻驱动任务”，不是“只能一个全局 background task”
- 驱动任务负责：
  - 观察 inbox
  - 决定是否发起/恢复自治 work item
  - 轮询在途命令状态
  - 在 lease/blocked/cooldown 下等待或让出

恢复点至少包含：
- `LastEventSequence`
- `PendingWorkItem`
- `CurrentActionSlot`
- `CurrentLeaseSnapshot`
- `RestartCount`
- `NextWakeAtUtc`

durable 边界：
- **必须持久化**：
  - `Host ingress staging / fanout journal`
  - `Runtime inbox ack sequence`
  - `PendingWorkItem`
  - `CurrentActionSlot` 的最小恢复描述
  - `CurrentLeaseSnapshot`
  - `NextWakeAtUtc`
- **可保留内存态**：
  - `Bridge 源游标` 缓存（其 durable 事实由 staging / fanout journal 间接约束）
  - 瞬时调度预算
  - 进程内 trace 索引

提交顺序：
1. 宿主拉取事件批次后，先写入 ingress staging / fanout journal
2. fanout 到目标 NPC inbox，成功后前移 `Bridge 源游标`
3. 提交命令前，先写入待执行 `PendingWorkItem`
4. 命令被 bridge 接受后，写入 `CommandId` 与 `ActionSlot`
5. 命令状态回报被确认处理后，推进 `Runtime inbox ack sequence`
6. 完成 / 取消 / 失败后，清理 `PendingWorkItem` 与 `ActionSlot`，再记录下一次唤醒点

建议载体：
- **save 级宿主状态**：新增 `StardewRuntimeHostStateStore`，SQLite，路径落在 save 作用域的 host 目录，例如 `.../runtime/stardew/games/{game}/saves/{save}/host/state.db`
- **per-NPC runtime 控制状态**：新增 `NpcRuntimeStateStore`，SQLite，路径落在 `NpcNamespace` 新增的 `RuntimeStateDbPath`，例如 `{RuntimeRoot}/state.db`
- 不把上述 durable 状态混进 transcript state db，也不靠 `runtime.jsonl` 反推当前控制态

文件边界建议：
- `NpcRuntimeInstance.cs`：身份与快照根
- 新增 `NpcRuntimeDriver` / `NpcRuntimeController` 一类文件：运行控制与恢复
- `NpcRuntimeSupervisor.cs`：创建、拉起、枚举、停止
- `NpcRuntimeHost.cs`：按 pack/save 做启动编排
- `StardewNpcAutonomyBackgroundService.cs`：减少为 bridge attach + feed + health

#### 6. 带序号事件流

现状：
- `GameEventCursor` 只有 `Since` 字符串
- `StardewEventSource` 仍按 `EventId` 语义轮询

建议增强：
- 在 `GameEventCursor` / `GameEventRecord` 中引入显式序号字段，允许 `EventId` 保留作业务标识
- `StardewBridgeDtos` / `StardewEventSource` 对齐桥接侧 `seq` / `next_seq`
- runtime 以序号推进 inbox 消费点，而不是靠事件 id 字符串猜测顺序

为什么必须做：
- 私聊、自治、命令回报都需要稳定顺序，否则恢复时无法判断“已处理到哪里”
- 这正是参考项目在审计文档里承认的原型缺口之一，本仓库应该在 runtime 控制面阶段补齐
- 两层游标必须替代当前“谁处理事件谁顺手推进 `EventId` cursor”的模式，否则仍会重现 private-chat 与 autonomy 双消费者 split-brain

## 分阶段实施步骤

### 阶段 0：前置验证与契约收口

涉及文件：
- `src/game/core/IGameAdapter.cs`
- `src/game/core/GameObservation.cs`
- `src/game/core/GameAction.cs`
- `src/game/core/PrivateChatOrchestrator.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewEventSource.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `src/games/stardew/StardewCommandContracts.cs`

任务：
- 确认桥接事件接口是否能稳定提供单调序号；如果没有，先定义 `seq / next_seq` 契约
- 梳理现有 `GameCommandStatus` 状态机，补齐 `StartedAtUtc / UpdatedAtUtc / ElapsedMs / RetryAfterUtc` 等恢复必需字段，明确 `queued/running/completed/failed/blocked/cancelled/expired`
- 确认 `PrivateChatOrchestrator`、`NpcAutonomyLoop` 与 runtime lease / event cursor 的暂停、恢复和推进边界，避免 split-brain cursor

完成标志：
- 有一份明确的 runtime 事件与命令状态契约，且不需要新开第二条 prompt/agent 主链

### 阶段 1：先补可恢复性与唤醒契约

涉及文件：
- `src/game/core/GameObservation.cs`
- `src/game/core/GameAction.cs`
- `src/game/core/PrivateChatOrchestrator.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewEventSource.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewBridgeDtos.cs`

任务：
- 在共享契约层引入序号游标、命令时间语义、恢复点最小字段
- 明确 private-chat lease 的 acquire / release 与 runtime 唤醒关系
- 明确 `record.EventId` 与 `seq` 的角色，避免 private-chat 与 runtime driver 各走一套消费点
- 明确宿主侧 ingress staging / fanout journal 的最小契约，解决“已拉取但未成功投递 inbox”的补偿语义
- 明确两层游标的提交顺序：
  1. 宿主从 bridge 拉取事件批次
  2. 先 durably 写入 ingress staging / fanout journal
  3. fanout 到目标 NPC inbox，完成 durable append
  4. 只有 fanout 成功，才前移 **Bridge 源游标**
  5. per-NPC driver 成功处理后推进 **Runtime inbox ack 游标**
  6. 只有 ack 游标推进成功，相关恢复点才视为已提交

完成标志：
- 共享事件流与命令状态契约足以支撑恢复、超时、blocked/retry，而不依赖 Stardew 特例解释
- 已定义“源游标前移但 inbox fanout 失败”时的补偿路径：重启或异常后从 staging / fanout journal 重放，而不是直接丢事件

### 阶段 2：引入 per-NPC driver/controller，扩展 runtime 快照

涉及文件：
- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcRuntimeDriver.cs`（或等价命名的新文件）
- `src/runtime/NpcRuntimeStateStore.cs`（新文件）
- `src/runtime/NpcRuntimeDescriptor.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcRuntimeBindings.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeRecoveryTests.cs`（新文件）

任务：
- 扩展 runtime snapshot，纳入 inbox、lease、action slot、恢复点、driver 状态
- `NpcRuntimeInstance` 保持身份、lease、handle、快照根职责
- 新增 runtime driver/controller 承载 inbox、event cursor、pending work item、action slot、命令轮询
- 新增 `NpcRuntimeStateStore`，明确持久化 `Runtime inbox ack sequence / PendingWorkItem / CurrentLeaseSnapshot / ActionSlot / NextWakeAtUtc`
- 增加同一 `runtimeRoot` 下的 supervisor 重建恢复测试：重建后续接 durable ack/work item/lease，而不是重扫
- 保持现有 `AcquirePrivateChatSessionLease(...)` API 向后兼容

完成标志：
- 已形成 `Instance + driver/controller` 的单 NPC 控制面，而不是把运行控制全部塞进 `NpcRuntimeInstance`

### 阶段 3：将中心轮询改造为“宿主 + 事件泵”

涉及文件：
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewRuntimeHostStateStore.cs`（新文件）
- `src/runtime/NpcRuntimeHost.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatWiringTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`

任务：
- 把中心服务从“foreach NPC 执行行为”改成“发现 bridge -> 启动 runtime -> 投递事件 -> 触发唤醒”
- 保持 Desktop 单进程托管；一个服务可以持有多个 runtime 驱动，但不再替 runtime 做决策
- 启动/停止路径改成 runtime 级可恢复
- 将 `StardewPrivateChatBackgroundService` 改造成宿主内部 private-chat inbox 适配组件：移除它作为后台服务的独立注册与生命周期，不再暴露独立 `Start/Stop/PollAsync`，由宿主事件泵统一拉取 bridge 事件并完成私聊 fanout
- 同步改造 `Desktop/HermesDesktop/App.xaml.cs` 与 wiring tests，不再单独注册、启动、停止 `StardewPrivateChatBackgroundService`
- 新增 `StardewRuntimeHostStateStore`，持久化 `ingress staging / fanout journal`
- 增加宿主重启恢复测试：用同一 `runtimeRoot` 重建 host/service，断言不会重扫 bridge 头部，且 staging/fanout 能正确补偿

完成标志：
- 中心服务退化为宿主，不再直接承载每 NPC 的自治状态机，且 bridge `PollAsync` 只剩一个 owner

### 阶段 4：补齐行动槽位与资源短占位

涉及文件：
- `src/runtime/WorldCoordinationService.cs`
- `src/runtime/ResourceClaimRegistry.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/runtime/NpcAutonomyLoop.cs`

任务：
- 为移动/说话/互动等动作引入 `Action` 槽位
- 动作提交时申请 resource claim，动作完成/失败/取消时显式释放
- 对 stuck、blocked、retry、expired 补齐短占位释放和重试语义

完成标志：
- 不再靠“轮询刚好避开冲突”，而是有显式的 action arbitration

### 阶段 5：可观测性、UI 暴露与回归压测

涉及文件：
- `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- `Desktop/HermesDesktop/Models/NpcRuntimeItem.cs`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`
- `Desktop/HermesDesktop.Tests/Runtime/*`
- `Desktop/HermesDesktop.Tests/Stardew/*`

任务：
- 定义 observability contract：
  - `NpcRuntimeSnapshot` 负责当前态且是权威来源
  - `activity/runtime.jsonl` 负责状态迁移审计日志
  - `NpcRuntimeTraceIndex`（定义于 `src/runtime/NpcRuntimeTrace.cs`）负责进程内 trace 检索入口
- 在 UI 与 snapshot 里暴露 driver 状态、lease、action slot、last seq、pending work item
- 增加多 NPC 并发、私聊抢占、blocked/retry/recover 的回归测试

完成标志：
- 用户能在 Desktop 里看懂每个 NPC 当前在等什么、占着什么、卡在哪

## 风险与前置验证

### 主要风险

1. **把 Stardew 细节渗进通用 runtime**
   - 缓解：runtime 只持有抽象状态，桥接细节停留在 `StardewEventSource` / `StardewCommandService`

2. **租约与行动槽位职责混淆**
   - 缓解：明确 lease 管“会话控制权”，action slot 管“动作执行窗口”，resource claim 管“世界短占位”

3. **事件序号契约不稳定导致恢复失真**
   - 缓解：阶段 0 先锁定 bridge 事件契约，再推进 runtime 恢复

4. **中心服务与 runtime driver 双重调度**
   - 缓解：阶段 3 明确中心服务只做 attach/feed/wakeup，不再直接跑 autonomy tick

5. **host / runtime 职责继续漂移**
   - 缓解：计划中显式固定边界
     - 宿主层保留：全局 LLM 并发预算、bridge attach / rebind 判定、pack discovery
     - per-NPC driver 保留：event cursor、pending work item、lease snapshot、action slot、恢复点
     - `NpcRuntimeInstance` 保留：身份、handle、快照根

6. **私聊与自治双消费者继续共存**
   - 缓解：阶段 3 把 `StardewPrivateChatBackgroundService` 收编为宿主内部适配组件，删除其独立后台服务注册与轮询入口，bridge 事件只允许宿主事件泵单点拉取

### 前置验证

- 验证 bridge 事件能提供稳定单调序号，至少在同一 save 生命周期内成立
- 验证 `GameCommandStatus` 对 move/speak/open_private_chat 的状态回报足以驱动恢复
- 验证现有 private-chat lease 回收语义覆盖取消、reply close、异常退出

## 验收标准

1. 同一 `Desktop` 进程内，多个启用 NPC 均拥有各自常驻 runtime 驱动，且不是由单个 `foreach` tick 直接执行行为。
2. `Desktop agent` 与 `NPC agent` 继续共用 `ContextManager` / `PromptBuilder` / `AgentCapabilityAssembler` 主链，没有新增 Stardew 专属 prompt assembler。
3. 每个 NPC runtime 都有可观测的：
   - inbox 深度
   - 当前 lease
   - 当前 action slot
   - 当前 pending work item
   - last event sequence
   - restart / cooldown 状态
4. 私聊开启时能暂停自治；私聊结束后自治可恢复，不出现旧 lease 释放新 lease 的回归。
5. 两个 NPC 对同一 tile/object 冲突时，至少一方被显式 blocked / retry，不出现无记录的相互覆盖。
6. runtime 重启或 bridge 重绑后，不会从头重复消费全部事件；能从最近序号继续。
7. `Runtime inbox ack sequence`、`PendingWorkItem`、`CurrentLeaseSnapshot` 至少能在进程重启后恢复，不依赖纯内存态继续跑。
8. bridge 事件消费只保留宿主事件泵一个 owner，不再存在私聊/自治双轮询器并行推进各自 cursor。
9. 不引入第二套 Desktop/NPC agent 装配主链。

## 测试规划

### 单元测试

目标文件：
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeInstanceTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/ResourceClaimRegistryTests.cs`

覆盖点：
- inbox 追加、去重、序号推进
- bridge 源游标与 runtime inbox ack 游标的分层推进
- lease 代际替换与释放
- action slot 抢占、释放、超时
- work item 恢复点快照
- resource claim 释放与冲突

### 集成测试

目标文件：
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatWiringTests.cs`

覆盖点：
- bridge attach 后仅宿主负责 feed/wakeup，不再直接代跑行为
- bridge 事件只由宿主单点拉取，私聊路径不再单独 `PollAsync`
- 私聊事件进入 runtime inbox 后触发 lease 抢占
- `blocked -> retry -> completed` 的命令状态闭环
- runtime 在 bridge rebind 后保留最近序号和恢复点
- 重启后从 durable `ack sequence` 与 `PendingWorkItem` 恢复，而不是从桥接头部重扫

### 端到端测试

建议路径：
- Desktop 启动后自动拉起指定 NPC runtime
- 向 bridge 注入连续事件：观察、私聊触发、说话关闭、移动冲突、恢复
- 检查 UI / snapshot / runtime log 是否一致反映 runtime 状态

关键场景：
- Penny 私聊中 Haley 继续自治
- 两个 NPC 同时争抢一个目标 tile
- bridge 断开再恢复后，runtime 继续从上次序号跑

### 可观测性测试

关注输出：
- `NpcRuntimeSnapshot`
- `activity/runtime.jsonl`
- `NpcRuntimeTraceIndex`（定义在 `src/runtime/NpcRuntimeTrace.cs`）
- Desktop 面板中的 runtime 状态展示

验证点：
- 每次 lease / action slot / claim / restart / recovery 都有可读记录
- 能从日志和 snapshot 还原一个 NPC 当前为什么暂停、在等谁、下一次何时唤醒
- 明确谁写入、谁消费：
  - `NpcRuntimeSnapshot`：运行时当前态的权威来源，供 UI 与服务查询
  - `runtime.jsonl`：状态迁移审计日志，辅助诊断，不承担当前态权威
  - `NpcRuntimeTraceIndex`：进程内 trace 检索索引，不是 durable 事实源

## ADR

### Decision

采用“**runtime 控制面围绕 runtime 下沉，但拆成 `Instance + per-NPC driver/controller`**”方案：把收件箱、会话租约视图、行动槽位、资源短占位关联、恢复点、带序号事件游标集中到单 NPC runtime 语义下，其中 `NpcRuntimeInstance` 只做身份/快照根，运行控制交给 per-NPC driver/controller；`StardewNpcAutonomyBackgroundService` 退化为 bridge attach、事件泵、健康监视与 runtime 宿主。

### Drivers

1. 需要严格满足“桌面 agent 与 NPC agent 本质等同”的约束。
2. 需要在单 Desktop 进程中实现多个 NPC 的常驻 runtime，而不是继续依赖中心轮询代跑。
3. 需要一次性补齐参考项目已暴露的原型缺口：任务覆盖、单任务限制、恢复缺失、轮询时序脆弱。

### Alternatives considered

- **方案 A：继续增强中心轮询服务**
  - 放弃原因：会形成 Stardew 专属第二控制平面，长期与同源目标冲突。

- **方案 C：直接做完整 actor/event-sourcing 平台**
  - 放弃原因：范围过大，短期不利于贴合当前仓库边界交付。

### Why chosen

方案 B 在当前仓库里是最小但正确的结构调整：
- 保留现有 `NpcRuntimeSupervisor`、`NpcRuntimeHost`、`NpcRuntimeContextFactory`、`PrivateChatOrchestrator` 资产
- 不新增第二套 prompt/agent 主链
- 能把收件箱、租约、槽位、claim、恢复、序号事件流全部纳入同一个 runtime 控制语义，同时不把 adapter/bridge/全局预算硬塞进 `NpcRuntimeInstance`

### Consequences

正向后果：
- NPC runtime 从“轻量 handle 容器”升级为“常驻智能体控制面”
- 私聊与自治不再互相绕路，统一通过 runtime arbitration 协调
- Desktop 面板能展示真正有意义的 NPC 运行状态

代价：
- `NpcRuntimeInstance`、`NpcRuntimeSupervisor`、`StardewNpcAutonomyBackgroundService` 的职责会重排
- 需要为 runtime 状态、恢复和事件顺序补一批测试
- 需要新增一个 per-NPC driver/controller 层，并为其定义与 `AutonomyHandle`、宿主预算、private-chat 的边界

### Follow-ups

1. 先做阶段 0 契约验证，再改 runtime 控制面。
2. 若桥接侧还没有稳定序号，优先补 `events?since=seq` 一类契约。
3. 完成 runtime 控制面后，再评估是否需要把社会消息/任务优先级并入同一 inbox。

## 执行建议

建议后续执行以 `team` 模式并行推进三条线：
- `architect` / 高推理：锁定 runtime 控制面与 bridge 契约
- `executor` / 高推理：runtime 控制面与宿主改造
- `test-engineer` / 中推理：回归测试、恢复测试、可观测性断言

建议验证路径：
- 先跑 `Runtime` + `Stardew` 测试子集
- 再做 Desktop 启动与 bridge 重绑烟测
- 最后观察 runtime snapshot / activity / trace 是否能解释全部状态转换
