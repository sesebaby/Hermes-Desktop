# 星露谷 NPC 常驻自治循环修复 RALPLAN-DR 草案

## 计划定位

本草案聚焦修复一个明确缺口：最近一次提交已经把 NPC runtime 的 shared handle、shared capability assembly、supervisor ownership 收拢到统一方向，但 Stardew NPC autonomy 仍未进入“每个 NPC 像常驻 Hermes agent 一样长期运行”的最终态。当前实现已经具备共享 runtime 的骨架，却还缺少真正的后台所有权契约、持续调度、生命周期耦合和验证闭环，因此仍停留在“可复用单次 tick”而不是“常驻自治”。

本计划严格遵守以下边界：

- 最终效果必须对齐参考项目：每个 NPC 都应表现为长期运行的 Hermes agent。
- 进程模型不变：仍为单 Desktop 进程内托管多个 NPC runtime，不能改成一 NPC 一进程。
- 架构方向不推倒重来：继续复用 `NpcRuntimeSupervisor`、`NpcRuntimeInstance`、shared handle / rebind 机制。
- 计划全文中文，供主代理直接提交给 architect 审查。

## 现状与为什么现在还“不够”

当前代码已经完成了“共享 runtime 身份与装配”的前半段，但还没有把“常驻自治”真正接上线：

- `Desktop/HermesDesktop/App.xaml.cs` 当前只注册并启动了 `StardewPrivateChatBackgroundService`，没有 autonomy 常驻后台服务。
- `src/runtime/NpcRuntimeHost.cs` 有 `StartDiscoveredAsync(...)`，但没有被 Desktop 启动链实际调用，因此 discovered NPC runtime 没有在启动后被拉起。
- `src/runtime/NpcRuntimeInstance.cs` 的 `StartAsync(...)` 目前只负责建目录、置 `Running`，不拥有任何后台循环、取消令牌、重启节流或 pause/resume 状态机。
- `src/games/stardew/StardewAutonomyTickDebugService.cs` 仍然只提供 `RunOneTickAsync(...)` 的单次 debug 入口，本质上还是手动 tick。
- 参考项目 `external/hermescraft-main/hermescraft.sh`、`civilization.sh`、`bot/server.js` 展示的目标形态是“常驻 body server + 常驻 agent 循环”，而不是“用户点一次按钮跑一次推理”。

因此，下一步修复不是重做 shared runtime，而是在现有 shared runtime 之上补齐“后台 owner 契约 + 生命周期编排 + 互斥与重绑策略 + 自动验证”。

## RALPLAN-DR

### 原则

1. **复用优先**：所有常驻自治能力都必须建立在现有 `NpcRuntimeSupervisor` / `NpcRuntimeInstance` / shared handle 机制之上，不再创建第二套 runtime 装配链。
2. **单进程多租户**：保持单 Desktop 进程内托管多个 NPC runtime；隔离通过 namespace、session、memory、transcript、tool surface 完成，而不是拆成多进程。
3. **生命周期单一事实源**：private-chat 与 autonomy 必须共享同一个 NPC runtime 生命周期主线，不能各自独立漂移。
4. **所有权不可歧义**：autonomy 的后台 owner、private-chat 的 ingress owner、runtime 身份容器、runtime 装配工厂必须各司其职，不允许一个职责被两个组件同时宣称拥有。
5. **首版 gate 单一**：本轮只允许一个“默认进入常驻 autonomy”的启用 gate，避免实现阶段在 manifest capability 与配置白名单之间摇摆。
6. **可验证优先**：计划必须落到可自动化验证的启动、循环、重绑、互斥、恢复路径，不接受“只能手动点 UI 看起来像工作”的验收方式。

### 决策驱动

1. **效果驱动**：目标是实现参考项目那种“每个 NPC 长期活着”的 autonomy 体验，而不是仅提供复用式单次 tick。
2. **约束驱动**：必须维持现有单 Desktop 进程模型和 shared runtime 方向，避免把问题退化成“一 NPC 一进程”的简单复制。
3. **风险驱动**：private-chat、autonomy、bridge attach、工具面重绑、世界互斥如果各自独立处理，会迅速引入重复 owner、热替换竞态和跨 NPC 冲突。
4. **落地驱动**：当前 pack validator 仍是 Phase 1，只接受 `move` / `speak` capability，因此“哪些 NPC 默认常驻”必须选一个不引入 schema 漂移的首版 gate。

### 可行方案对比

#### 方案 A：把常驻循环直接塞进 `NpcRuntimeInstance`

思路：
让 `NpcRuntimeInstance.StartAsync(...)` 直接启动 autonomy 背景任务，把 runtime instance 本身扩成“状态 + 后台线程 owner”。

优点：

- 生命周期表面上最集中，实例创建即运行。
- `State`、取消、异常状态可以都挂在 `NpcRuntimeInstance` 内部。

缺点：

- `NpcRuntimeInstance` 目前是纯 runtime 身份与 handle 缓存容器，若直接塞入 Stardew bridge 轮询与 adapter attach，会把通用 runtime 层污染成游戏场景 owner。
- private-chat 不需要常驻后台循环；把 autonomy loop 内嵌到实例层，会让抽象边界从“shared runtime”退化为“shared runtime + Stardew orchestration 混合体”。
- 工具面快照、bridge save attach、默认启用 NPC 选择等都属于上层编排问题，放进 instance 会让通用层承担过多策略。

结论：
可行但不推荐。它复用了 shared runtime，但把 orchestration 责任压到了错误层级。

#### 方案 B：新增 `StardewNpcAutonomyBackgroundService` 作为 autonomy owner，`NpcRuntimeSupervisor` 继续负责身份与 handle 复用

思路：
新增一个和 `StardewPrivateChatBackgroundService` 对位的 autonomy 常驻后台服务；它负责 bridge attach、discover/start、每个 NPC loop 调度、pause/resume、重试与桥接恢复。`NpcRuntimeSupervisor` 仍只负责 runtime 实例与 handle/rebind；`NpcRuntimeHost` 负责 discovered pack 的预热与实例存在性。

优点：

- 与当前 Desktop 启动链最一致，易于在 `App.xaml.cs` 中注册/启动/停止。
- 保持 `NpcRuntimeSupervisor` 的边界清晰：负责“身份与 handle”，不负责“游戏后台编排”。
- 可以与 `StardewPrivateChatBackgroundService` 明确协同，在单一 owner 契约下共享同一个 runtime 身份根。
- 更容易在单进程多 NPC 下实现“按 save attach、按 NPC 调度、按 bridge key rebind”的治理。

缺点：

- 需要定义 autonomy service 与 private-chat service 的协作契约，否则会出现 autonomy owner 与 private-chat ingress 共享同一 runtime 但缺少协调。
- 需要补一层 runtime-level lease 与结构化观测状态，避免并发时状态漂移。

结论：
推荐。它最符合“复用现有 shared runtime 方向、只补 owner 和编排层”的目标。

#### 方案 C：把 private-chat 与 autonomy 合并成单一 `StardewNpcRuntimeCoordinatorService`

思路：
新建统一 coordinator，同时托管 private-chat 和 autonomy 的后台行为，一个服务拥有全部 Stardew NPC runtime 场景逻辑。

优点：

- 生命周期 owner 最单一，概念上最完整。
- pause/resume、互斥、bridge attach 可以在同一地方统一决策。

缺点：

- 变更面偏大，接近重新梳理 Stardew 背景编排，不适合这次“在已有 shared runtime 方向上补最后一段”。
- 会把已经稳定的 private-chat 路径一起重构，回归风险高。

结论：
可作为后续收敛方向，但不适合作为当前修复的首选落点。

### 推荐方案

采用 **方案 B**，并把 recommendation 收敛为单一、可执行、可验证的表述：

- `StardewNpcAutonomyBackgroundService` 是 autonomy loop 的唯一 owner，且只有它能启动、暂停、恢复、重绑和重启 autonomy loop。
- `StardewPrivateChatBackgroundService` 是 private-chat 事件 ingress 的唯一 owner，只负责消费桥接事件并驱动 core 状态机推进会话；真正的 lease 申请/释放边界由 core 状态机决定，本身不拥有 autonomy loop。
- `NpcRuntimeInstance` 只是单个 NPC runtime 的共享身份根与协作状态容器，负责持有 handle、lease、观测字段，不直接驱动任何 Stardew 后台循环。
- `NpcRuntimeSupervisor` 只是 runtime 实例与 handle/rebind 的装配工厂，负责 `GetOrStart` / `GetOrCreate*Handle`，不拥有 bridge 轮询、loop 调度或 private-chat ingress。
- `NpcRuntimeHost` 只负责 discovered runtime 预热与 persona pack 种子落盘，不拥有 autonomy loop。
- 首版“哪些 NPC 默认进入常驻 autonomy”只走 **配置白名单 gate**，不在本轮引入新的 manifest capability。原因是当前 `FileSystemNpcPackLoader` 的 Phase 1 校验仍只接受 `move` / `speak`；若把 autonomy 放进 manifest，会额外引入 schema、validator、pack 数据与测试迁移，不符合本轮“补齐常驻 owner 与编排”的最小闭环。

## 生命周期设计

### private-chat 与 autonomy 的关系

目标关系：

- `NpcRuntimeInstance` 是 NPC runtime 的共享身份根。
- `private-chat handle` 与 `autonomy handle` 都从同一 `NpcRuntimeInstance` 派生。
- autonomy 是默认常驻通道；private-chat 是事件驱动通道。
- private-chat 不创建独立 runtime，不拥有独立 SOUL/transcript/memory/session 根目录。
- private-chat lease 覆盖 **整个私聊会话**，不是只覆盖单次 `ReplyAsync(...)`。也就是说，从 open 成功进入 `AwaitingPlayerInput` 开始，到会话 `EndSession()` 结束前，同一 NPC 的 autonomy 都保持暂停。

建议约束：

- runtime 启动后先进入 autonomy 常驻待命态。
- private-chat open 成功后，对同一 NPC 的 autonomy loop 立即施加 session-scope pause lease。
- private-chat 会话结束后，仅允许当前 lease 持有者释放该 lease；lease 释放后 autonomy 恢复常驻循环。
- 仅轮询事件、尚未成功打开私聊窗口时，不获取 lease；一旦进入 `AwaitingPlayerInput`，lease 持续覆盖到 `EndSession()`。

### 后台服务 owner

不可歧义的一句话契约：

`StardewNpcAutonomyBackgroundService` 是 autonomy loop 的唯一 owner，`StardewPrivateChatBackgroundService` 是 private-chat ingress 的唯一 owner，`NpcRuntimeInstance` 只保存共享 runtime 身份与协作状态，`NpcRuntimeSupervisor` 只负责实例/handle 创建与重绑。

对应分工：

- `App.xaml.cs`
  - 注册并启动 `StardewNpcAutonomyBackgroundService`
  - 与现有 `StartStardewPrivateChatBackground(...)` 并列，新增 `StartStardewNpcAutonomyBackground(...)`
- `StardewNpcAutonomyBackgroundService`
  - 读取 bridge discovery
  - 确认 saveId / bridgeKey
  - 调用 `NpcRuntimeHost.StartDiscoveredAsync(...)` 预热 runtime
  - 对已启用 autonomy 的 NPC 建立/维护后台 loop
  - 在 `BridgeKey` 或工具面快照版本变化时触发 rebind
  - 在 bridge 不可用、world 未就绪、private-chat 关键区占用时执行 pause/backoff
- `StardewPrivateChatBackgroundService`
  - 消费 private-chat 事件
  - 驱动 orchestrator 处理事件，但不直接判断 lease 边界
- `PrivateChatOrchestrator`（core 状态机）
  - 通过显式协调接口在 open 成功时申请 autonomy session lease
  - 在 `EndSession()` 内释放 lease
  - 因为真正的 `AwaitingPlayerInput -> WaitingAgentReply -> ShowingReply -> WaitingReplyDismissal -> EndSession` 边界都在 core 状态机内，所以 lease 的 acquire/release 必须绑定这里，而不是绑定外层轮询器
- `NpcRuntimeSupervisor`
  - `GetOrStartAsync(...)`
  - `GetOrCreatePrivateChatHandleAsync(...)`
  - `GetOrCreateAutonomyHandleAsync(...)`
  - 继续作为 runtime 实例与 handle 复用工厂
- `NpcRuntimeInstance`
  - 保存共享 runtime 身份、handle、lease、观测字段
  - 不承接 Stardew 专属轮询逻辑

### pause / resume / 互斥策略

建议分两层：

#### 第一层：同一 NPC 的 channel 互斥

- 在 `NpcRuntimeInstance` 增加结构化 pause lease，而不是只增加状态枚举。lease 至少包含：
  - `LeaseOwner`，例如 `private_chat`
  - `Reason`，例如 `private_chat_session_active`
  - `Generation`，单调递增，用于防止旧持有者释放新 lease
  - `AcquiredAtUtc`
- 新增一个显式协调接口，例如 `IPrivateChatSessionLeaseCoordinator`（命名可调整），由 core `PrivateChatOrchestrator` 调用，Stardew/runtime 层实现。接口至少提供：
  - `AcquireSessionLease(npcId, conversationId, owner, reason)`，返回 `leaseGeneration`
  - `ReleaseSessionLease(npcId, conversationId, owner, leaseGeneration)`
  - 只有 `owner + leaseGeneration` 匹配时才允许释放
- 只有当前 lease 持有者且 generation 匹配时，才允许释放该 lease。
- lease 的精确挂载点固定为：
  - `TrySubmitPendingOpenAsync(...)` 成功把状态推进到 `AwaitingPlayerInput` 后 acquire
  - `EndSession()` 内 release
  - 任何异常、取消、空回复、speak 失败只要走 `EndSession()`，都统一释放
- private-chat 会话 lease 生效后，autonomy owner 在下一轮调度前检查 lease；若 lease 仍有效，则不得继续执行 agent 决策。
- bridge 不可用、世界未就绪等非 private-chat 原因也统一走 lease 或等价 gate，并写入明确 owner/reason。

#### 第二层：跨 NPC 的世界资源互斥

- 继续复用已有 `ResourceClaimRegistry` / `WorldCoordinationService`，不再创造新的跨 NPC 冲突系统。
- autonomy loop 中的实际动作冲突仍由 world claim 约束；本次只补“同一 NPC 的通道互斥”和“常驻循环治理”，不重做已有资源声明机制。

### 工具面与 rebind 策略

建议策略：

- 不再把 `McpManager.Tools.Values` 当作隐式、随取随变的输入前提。
- 引入显式的 **版本化工具面快照提供者**，例如 `INpcToolSurfaceSnapshotProvider`，由它输出：
  - `ToolSurface`
  - `SnapshotVersion`
  - `CapturedAtUtc`
- rebind 输入源收敛为两个稳定量：
  - `BridgeKey`
  - `ToolSurfaceSnapshotVersion`
- autonomy owner 只在调度边界读取工具面快照；private-chat ingress owner 只在新一轮 `ReplyAsync(...)` 开始前读取工具面快照。
- 如果 `BridgeKey` 或 `ToolSurfaceSnapshotVersion` 改变，不在 mid-turn 热替换正在执行的 handle；而是在“本轮 tick 完成后、下一轮获取 handle 前”重新调用 `GetOrCreateAutonomyHandleAsync(...)`。
- private-chat 维持相同原则：一轮 `ReplyAsync(...)` 内不热替换，下一次请求自然拿到新 handle。
- bridge attach 变化与工具面快照变化统一体现在 rebind key 上：
  - bridge 变化 -> `BridgeKey` 变化
  - 工具面变化 -> `ToolSurfaceSnapshotVersion` 变化
- 验证要求：
  - 同 `BridgeKey` + `ToolSurfaceSnapshotVersion` 复用同 handle
  - `ToolSurfaceSnapshotVersion` 变化后下一轮 rebind
  - 正在处理中的对话/tick 不因重绑被中断

## 风险预案

1. **桥接抖动导致循环反复重建**
   - 预案：后台服务按 `BridgeKey` 变化判定 attach；bridge 不可用时进入退避，而不是销毁全部 runtime 身份。
2. **private-chat 与 autonomy 同时驱动同一 NPC**
   - 预案：引入 session-scope 结构化 lease；private-chat 在 open 成功后立刻持有 lease，到 `EndSession()` 才释放，避免玩家输入中途 autonomy 抢占 NPC。
3. **工具面热更新打断当前回合**
   - 预案：只从版本化工具面快照源读取输入；仅在 tick 边界 / reply 边界 rebind，不允许 mid-turn 替换。
4. **常驻 loop 出错后静默停止**
   - 预案：每个 NPC loop 都要记录 fault、重试次数、最后 traceId；达到 budget 限额后转为 paused/faulted，而不是消失。
5. **默认启用范围过大导致启动成本和噪音上升**
   - 预案：首版只采用配置白名单 gate，例如 `stardew:npc_autonomy_enabled_ids=haley,penny`；manifest capability 扩展明确延后到后续 schema 版本。

## 测试计划

### 单元测试

- `NpcRuntimeInstance`
  - 带 `owner/reason/generation` 的 lease 申请、覆盖、拒绝、释放
  - private-chat session lease 生效后 autonomy 不再执行下一轮
- `NpcRuntimeSupervisor`
  - `AdapterKey` 不变时 autonomy handle 复用
  - `ToolSurfaceSnapshotVersion` 变化时下一轮 rebind
- `PrivateChatOrchestrator`
  - open 成功进入 `AwaitingPlayerInput` 时 acquire lease
  - `EndSession()` 必定 release lease
  - 非持有者或旧 generation 释放 lease 失败
- `StardewNpcAutonomyBackgroundService`
  - Desktop 启动后会 attach bridge 并启动已发现 NPC 的常驻 loop
  - bridge 丢失后进入 pause/backoff，恢复后继续
  - private-chat session lease 期间不会再触发 autonomy agent 决策
  - 可导出 `autonomy loop state / pause reason / last automatic tick at / current bridgeKey / current handle generation / restart count`

### 集成测试

- `App.xaml.cs` wiring 测试
  - 注册 `StardewNpcAutonomyBackgroundService`
  - 在启动后调用 `StartStardewNpcAutonomyBackground(...)`
- Stardew runtime 集成
  - 只有配置白名单里的 NPC 会被 autonomy owner 自动拉起
  - discovered NPC 在启动后无需 UI 手动点击即可产生新的 runtime trace / activity log
  - private-chat 与 autonomy 共享同一 runtime namespace、transcript 根和 memory 根
  - 工具面快照版本更新后，下一轮 tick 使用新工具面
  - 同一 lease 的非持有者无法释放 pause 状态

### 行为验证

- 启动 Desktop 后，不点 `RunOneTick`，目标 NPC 必须在观测接口或快照中显示 `LastAutomaticTickAtUtc` 持续刷新。
- 触发 private-chat 会话时，对应 NPC autonomy 从 open 成功开始暂停；会话结束后恢复。
- 多 NPC 同时运行时，world claim 仍有效，且不会因为 autonomy 服务引入同 tile 冲突回归。

### 可观测性

- 必须为每个 NPC runtime 暴露以下字段或等价证明：
  - `AutonomyLoopState`
  - `PauseReason`
  - `LastAutomaticTickAtUtc`
  - `CurrentBridgeKey`
  - `CurrentAutonomyHandleGeneration`
  - `AutonomyRestartCount`
- 推荐落点：
  - `NpcRuntimeSnapshot` 扩展字段
  - 或专门的 autonomy runtime status DTO
- 日志仍需保留：
  - attach/detach bridge
  - NPC loop start/pause/resume/fault/rebind
  - pause reason
  - rebind reason
- 验收不能只靠 `runtime.jsonl` 增长；必须能通过结构化快照/状态接口直接证明“后台持续运行、为什么暂停、当前绑在哪个 bridge、当前 handle 代数是多少、重启过几次”。

## 实施步骤

### 步骤 1：补齐 Desktop 启动链

工作内容：

- 在 `App.xaml.cs` 注册 `StardewNpcAutonomyBackgroundService`
- 在应用启动后自动 `Start()`
- 在 `ProcessExit` / stop 路径补齐 `Stop()`

验收标准：

- Desktop 启动无需手动点 UI，即会启动 autonomy 唯一 owner。
- 新增 wiring 测试证明 private-chat 与 autonomy 两个后台服务都已接线。

### 步骤 2：把 discovered runtime 预热真正接入启动流

工作内容：

- 在 autonomy 后台服务 bridge attach 成功后调用 `NpcRuntimeHost.StartDiscoveredAsync(...)`
- 明确默认启用 autonomy 的 NPC 过滤规则：**首版只走配置白名单 gate**
- 新增对应配置读取与解析，例如 `stardew:npc_autonomy_enabled_ids`

验收标准：

- 只有白名单中的 NPC 会在 attach 后自动创建/复用 runtime namespace。
- 不再依赖手动 `RunOneTickAsync(...)` 才第一次生成 runtime。

### 步骤 3：新增 autonomy 常驻循环 owner

工作内容：

- 实现 `StardewNpcAutonomyBackgroundService`
- 按 NPC 维护长生命周期 loop：poll -> handle 获取/复用 -> 单轮 tick -> delay/backoff -> 下一轮
- 使用 `NpcAutonomyBudget` 约束并发、重启冷却、重试上限
- 把 loop 状态与 restart count 写入结构化观测快照

验收标准：

- 单个 NPC 在 bridge 可用时能稳定连续产生多轮 tick。
- loop 故障不会导致后台服务整体退出。
- 结构化快照能读出当前 loop state、bridgeKey、handle generation、restart count。

### 步骤 4：定义 private-chat 与 autonomy 的共享生命周期契约

工作内容：

- 在 `NpcRuntimeInstance` 或等价共享状态层补带 `owner/reason/generation` 的 session-scope pause lease
- 在 `PrivateChatOrchestrator` 内通过协调接口，在 open 成功推进到 `AwaitingPlayerInput` 时申请 lease，在 `EndSession()` 时释放
- 明确 pause reason 与状态暴露方式

验收标准：

- private-chat 整个会话期间同一 NPC 不再并发产生 autonomy 决策，且错误持有者不能释放 lease。
- private-chat 结束后 autonomy 自动恢复，无需人工干预。

### 步骤 5：落地 rebind 边界与稳定输入源

工作内容：

- 引入版本化工具面快照提供者，替代隐式 `McpManager.Tools.Values`
- autonomy 服务在 tick 边界比较 `BridgeKey` 与 `ToolSurfaceSnapshotVersion`
- 变化后下一轮 rebind，不做 mid-turn 替换
- 记录 rebind 日志与 generation 变化

验收标准：

- 同一运行周期内 handle 仅在边界切换。
- 测试可断言 generation 增长符合预期，且当前轮不被打断。

### 步骤 6：补齐验证闭环

工作内容：

- 新增 unit/integration/wiring tests
- 保留 `RunOneTickAsync(...)` 作为 debug 工具，但从“唯一入口”降级为“观测/诊断入口”
- 用结构化 runtime snapshot/status + activity log + 日志共同证明常驻自治已成立

验收标准：

- 自动化测试覆盖“自动启动、持续循环、pause lease、rebind、恢复、结构化观测字段”。
- 人工验证不需要点 `RunOneTick` 也能看到 NPC 持续自治，且能读出 `AutonomyLoopState`、`PauseReason`、`LastAutomaticTickAtUtc`、`CurrentBridgeKey`、`CurrentAutonomyHandleGeneration`、`AutonomyRestartCount`。

## ADR

### 决策

采用“**新增 `StardewNpcAutonomyBackgroundService` 作为 autonomy loop 唯一 owner；`StardewPrivateChatBackgroundService` 作为 private-chat ingress 唯一 owner；`NpcRuntimeSupervisor` 只负责 runtime 实例与 handle/rebind 装配；`NpcRuntimeInstance` 只负责共享身份、lease 与观测状态；`NpcRuntimeHost` 只负责 discovered runtime 预热；首版默认启用 gate 只走配置白名单；private-chat lease 精确挂载在 core `PrivateChatOrchestrator` 的 open-success 与 `EndSession()` 边界**”的方案。

### 决策驱动

- 需要对齐参考项目的常驻 agent 效果，但不能改成多进程。
- 当前 shared runtime 方向已经基本正确，问题在于缺少后台 owner 和持续调度。
- private-chat 已经有 ingress 后台模式，autonomy 采用对位实现能最大限度复用现有结构并降低回归。

### 备选方案

- 方案 A：把常驻 loop 内嵌到 `NpcRuntimeInstance`
- 方案 C：重构成统一 `StardewNpcRuntimeCoordinatorService`

### 为什么选择当前方案

- 它在“尽快补齐常驻自治”与“避免重做 private-chat 编排”之间取得了最好平衡。
- 它保持通用 runtime 层纯净，把 Stardew 场景 owner 放在游戏后台服务层。
- 它自然复用现有 App 启动模式、supervisor rebind 机制、runtime namespace 和工具装配链，同时把 owner 契约写成不可歧义的一句话。

### 后果

- 背景服务会同时存在 autonomy owner 和 private-chat ingress owner，因此必须以 lease 协议而不是口头约定来协调同一 NPC runtime。
- `NpcRuntimeInstance` 需要承担少量生命周期协作状态，但不应膨胀成完整场景编排器。
- `RunOneTickAsync(...)` 将继续保留，但定位从功能入口变为 debug/诊断入口。

### 后续跟进

- 若后续出现更多 Stardew 后台通道，如 schedule follower、relationship loop，再评估是否把 private-chat 与 autonomy 合并进统一 coordinator。
- 若常驻 NPC 数量继续增长，再评估基于配置的默认启用范围、loop cadence、分级调度和可观测性聚合。

## 执行编组建议

### 可用 agent 类型

- `architect`：只用于执行前再次核对边界，不参与写码。
- `executor`：主实现角色，负责后台服务、runtime 协调接口、启动接线与状态模型。
- `debugger`：专门盯 lease/rebind/bridge attach 的竞态与故障恢复。
- `test-engineer`：负责 unit/integration/wiring 测试设计与补齐。
- `verifier`：只做最终完成性验证，不参与方案发明。

### `ralph` 路线

适用场景：

- 你要一个单 owner 顺序推进，从计划到实现到验证一口气做完。
- 当前改动高度耦合：启动链、runtime 状态、private-chat 核心状态机、测试一起动，串行 owner 更稳。

建议 lane：

1. `executor`（高推理）先落启动链、autonomy owner、配置白名单 gate。
2. 同一个 `executor` 继续落 `PrivateChatOrchestrator` lease 协调接口与 `NpcRuntimeInstance` 状态。
3. `test-engineer`（中推理）补单元/集成/wiring 测试。
4. `verifier`（高推理）跑验证并做完成性复核。

### `team` 路线

适用场景：

- 你要并行加速，但能接受最后一轮集成收口。
- 可以把写入面切成互不冲突的几块。

推荐分工：

1. Worker A / `executor`（高推理）  
   负责：`Desktop/HermesDesktop/App.xaml.cs`、新增 `StardewNpcAutonomyBackgroundService`、配置白名单 gate、`NpcRuntimeHost` 预热接线。
2. Worker B / `executor`（高推理）  
   负责：`src/runtime/NpcRuntimeInstance.cs`、相关 runtime 状态对象、lease 与观测字段、工具面快照 provider。
3. Worker C / `executor`（高推理）  
   负责：`src/game/core/PrivateChatOrchestrator.cs` 协调接口挂载点、Stardew private-chat 入口适配。
4. Worker D / `test-engineer`（中推理）  
   负责：`Desktop/HermesDesktop.Tests/Runtime/*`、`Desktop/HermesDesktop.Tests/Stardew/*` 的新增/改造测试。
5. 收口由 leader 或 `verifier`（高推理）完成  
   负责：整体验证、残余风险确认、是否满足“无需点 RunOneTick 也能持续自治”。

### 推理强度建议

- 启动链 / 生命周期 / 协调接口：`high`
- lease 与 rebind 竞态：`high`
- 测试补齐：`medium`
- 最终验证：`high`

### 启动提示

- 顺序执行：`$ralph 执行 .omx/plans/星露谷NPC常驻自治循环修复-审慎计划草案.md`
- 并行执行：`$team 执行 .omx/plans/星露谷NPC常驻自治循环修复-审慎计划草案.md`

### 团队验证路径

1. 先跑 unit：lease、handle rebind、tool snapshot version。
2. 再跑 integration：Desktop 启动后 autonomy owner 自动拉起、白名单 NPC 自动常驻。
3. 再跑 wiring：`App.xaml.cs` 的注册/启动/停止接线。
4. 最后做人机验证：
   - 不点 `RunOneTick`
   - Haley / Penny（在白名单内）自动出现 `LastAutomaticTickAtUtc` 刷新
   - 进入 private-chat 后 autonomy 暂停
   - 关闭/结束私聊后 autonomy 恢复
