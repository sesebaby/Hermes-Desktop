# Stardew NPC 常驻自治循环修复审慎计划草案

## 计划定位

本草案聚焦修复一个明确缺口：最近一次提交已经把 NPC runtime 的 shared handle、shared capability assembly、supervisor ownership 收拢到统一方向，但 Stardew NPC autonomy 仍未进入“每个 NPC 像常驻 Hermes agent 一样长期运行”的最终态。当前实现已经具备共享 runtime 的骨架，却还缺少真正的后台 owner、持续调度、生命周期耦合和验证闭环，因此仍停留在“可复用单次 tick”而不是“常驻自治”。

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

因此，下一步修复不是重做 shared runtime，而是在现有 shared runtime 之上补齐“长期后台拥有者 + 生命周期编排 + 互斥与重绑策略 + 自动验证”。

## RALPLAN-决策记录

### 原则

1. **复用优先**：所有常驻自治能力都必须建立在现有 `NpcRuntimeSupervisor` / `NpcRuntimeInstance` / shared handle 机制之上，不再创建第二套 runtime 装配链。
2. **单进程多租户**：保持单 Desktop 进程内托管多个 NPC runtime；隔离通过 namespace、session、memory、transcript、tool surface 完成，而不是拆成多进程。
3. **生命周期单一事实源**：private-chat 与 autonomy 必须共享同一个 NPC runtime 生命周期主线，不能各自独立漂移。
4. **后台 owner 明确**：谁负责常驻循环、何时 attach/detach、何时 pause/resume、何时 rebind，都必须有唯一 owner。
5. **可验证优先**：计划必须落到可自动化验证的启动、循环、重绑、互斥、恢复路径，不接受“只能手动点 UI 看起来像工作”的验收方式。

### 决策驱动

1. **效果驱动**：目标是实现参考项目那种“每个 NPC 长期活着”的 autonomy 体验，而不是仅提供复用式单次 tick。
2. **约束驱动**：必须维持现有单 Desktop 进程模型和 shared runtime 方向，避免把问题退化成“一 NPC 一进程”的简单复制。
3. **风险驱动**：private-chat、autonomy、bridge attach、MCP discovered tools、世界互斥如果各自独立处理，会迅速引入重复 owner、热替换竞态和跨 NPC 冲突。

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
- discovered tools rebind、bridge save attach、默认启用 NPC 选择等都属于上层编排问题，放进 instance 会让通用层承担过多策略。

结论：
可行但不推荐。它复用了 shared runtime，但把 orchestration 责任压到了错误层级。

#### 方案 B：新增 `StardewNpcAutonomyBackgroundService` 作为 autonomy owner，`NpcRuntimeSupervisor` 继续负责身份与 handle 复用

思路：
新增一个和 `StardewPrivateChatBackgroundService` 对位的 autonomy 常驻后台服务；它负责 bridge attach、discover/start、每个 NPC loop 调度、pause/resume、重试与桥接恢复。`NpcRuntimeSupervisor` 仍只负责 runtime 实例与 handle/rebind；`NpcRuntimeHost` 负责 discovered pack 的预热与实例存在性。

优点：

- 与当前 Desktop 启动链最一致，易于在 `App.xaml.cs` 中注册/启动/停止。
- 保持 `NpcRuntimeSupervisor` 的边界清晰：负责“身份与 handle”，不负责“游戏后台编排”。
- 可以与 `StardewPrivateChatBackgroundService` 明确协同，形成 shared runtime + 双后台 owner 的受控模型。
- 更容易在单进程多 NPC 下实现“按 save attach、按 NPC 调度、按 bridge key rebind”的治理。

缺点：

- 需要定义 autonomy service 与 private-chat service 的协作契约，否则会出现两个后台服务共享同一 runtime 但缺少协调。
- 需要补一层 runtime-level pause lease / channel activity 状态，避免并发时状态漂移。

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

采用 **方案 B**：

- 新增 `StardewNpcAutonomyBackgroundService`，作为 Stardew autonomy 的唯一后台 owner。
- 保留 `NpcRuntimeSupervisor` 为 runtime identity / handle / rebind 工厂，不下沉常驻调度职责。
- 让 `NpcRuntimeHost` 在 bridge attach 后负责 discovered NPC runtime 的预热与 pack 种子落盘，但不自己跑永续 loop。
- private-chat 与 autonomy 继续共享同一个 `NpcRuntimeInstance`，但通过明确的“channel activity / pause lease / world gating”协议协调生命周期。

## 生命周期设计

### private-chat 与 autonomy 的关系

目标关系：

- `NpcRuntimeInstance` 是 NPC runtime 的共享身份根。
- `private-chat handle` 与 `autonomy handle` 都从同一 `NpcRuntimeInstance` 派生。
- autonomy 是默认常驻通道；private-chat 是事件驱动通道。
- private-chat 不创建独立 runtime，不拥有独立 SOUL/transcript/memory/session 根目录。

建议约束：

- runtime 启动后先进入 autonomy 常驻待命态。
- private-chat 开始处理“玩家直接输入 -> NPC 回复”期间，对同一 NPC 的 autonomy loop 施加临时 pause lease。
- private-chat 完成一次会话后，由 coordinator 释放 pause lease；autonomy 恢复常驻循环。
- 如果 private-chat 只是在读取事件但未进入 `WaitingAgentReply` / `ShowingReply` 关键区，不必暂停 autonomy；只有真正占用 NPC 行为决策窗口时才暂停。

### 后台服务 owner

建议 owner 分工：

- `App.xaml.cs`
  - 注册并启动 `StardewNpcAutonomyBackgroundService`
  - 与现有 `StartStardewPrivateChatBackground(...)` 并列，新增 `StartStardewNpcAutonomyBackground(...)`
- `StardewNpcAutonomyBackgroundService`
  - 读取 bridge discovery
  - 确认 saveId / bridgeKey
  - 调用 `NpcRuntimeHost.StartDiscoveredAsync(...)` 预热 runtime
  - 对已启用 autonomy 的 NPC 建立/维护后台 loop
  - 在 bridgeKey 或 discovered tools 指纹变化时触发 rebind
  - 在 bridge 不可用、world 未就绪、private-chat 关键区占用时执行 pause/backoff
- `NpcRuntimeSupervisor`
  - `GetOrStartAsync(...)`
  - `GetOrCreatePrivateChatHandleAsync(...)`
  - `GetOrCreateAutonomyHandleAsync(...)`
  - 继续作为 runtime 实例与 handle 复用工厂
- `NpcRuntimeInstance`
  - 增加最小必要的运行时控制状态，不承接 Stardew 专属轮询逻辑

### pause / resume / 互斥策略

建议分两层：

#### 第一层：同一 NPC 的 channel 互斥

- 在 `NpcRuntimeInstance` 增加轻量级 channel activity / pause lease 能力，至少区分：
  - `AutonomyRunning`
  - `AutonomyPausedByPrivateChat`
  - `AutonomyPausedByBridgeUnavailable`
  - `AutonomyFaulted`
- private-chat 关键区获得 lease 后，autonomy loop 下一轮不再执行 agent 决策，只保留健康检查或直接 sleep。
- lease 必须带 reason，便于 trace 与测试断言。

#### 第二层：跨 NPC 的世界资源互斥

- 继续复用已有 `ResourceClaimRegistry` / `WorldCoordinationService`，不再创造新的跨 NPC 冲突系统。
- autonomy loop 中的实际动作冲突仍由 world claim 约束；本次只补“同一 NPC 的通道互斥”和“常驻循环治理”，不重做已有资源声明机制。

### MCP discovered tools rebind 策略

建议策略：

- rebind 触发条件基于 `NpcToolSurface.Fingerprint`，继续复用现有 supervisor 的 key 机制。
- autonomy 后台服务在每轮外层调度时读取最新 discovered tools，计算 fingerprint。
- 如果 fingerprint 改变，不在 mid-turn 热替换正在执行的 handle；而是在“本轮 tick 完成后、下一轮获取 handle 前”重新调用 `GetOrCreateAutonomyHandleAsync(...)`。
- private-chat 维持相同原则：一轮 `ReplyAsync(...)` 内不热替换，下一次请求自然拿到新 handle。
- bridge attach 变化与 discovered tools 变化统一体现在 rebind key 上：
  - bridge 变化 -> `AdapterKey` 变化
  - discovered tools 变化 -> `ToolSurface.Fingerprint` 变化
- 验证要求：
  - 同 fingerprint 复用同 handle
  - fingerprint 变化后下一轮 rebind
  - 正在处理中的对话/tick 不因重绑被中断

## 风险预案

1. **桥接抖动导致循环反复重建**
   - 预案：后台服务按 `bridgeKey` 变化判定 attach；bridge 不可用时进入退避，而不是销毁全部 runtime 身份。
2. **private-chat 与 autonomy 同时驱动同一 NPC**
   - 预案：引入 pause lease；private-chat 关键区显式挂起 autonomy，退出后恢复。
3. **MCP discovered tools 热更新打断当前回合**
   - 预案：仅在 tick 边界 / reply 边界 rebind，不允许 mid-turn 替换。
4. **常驻 loop 出错后静默停止**
   - 预案：每个 NPC loop 都要记录 fault、重试次数、最后 traceId；达到 budget 限额后转为 paused/faulted，而不是消失。
5. **默认启用范围过大导致启动成本和噪音上升**
   - 预案：首版采用配置/manifest 白名单或显式 capability gate，只对声明启用 autonomy 的 NPC 常驻运行，再逐步放开。

## 测试计划

### 单元测试

- `NpcRuntimeInstance`
  - pause lease / resume 状态转换
  - private-chat 关键区进入后 autonomy 不再执行下一轮
- `NpcRuntimeSupervisor`
  - `AdapterKey` 不变时 autonomy handle 复用
  - tool fingerprint 变化时下一轮 rebind
- `StardewNpcAutonomyBackgroundService`
  - Desktop 启动后会 attach bridge 并启动已发现 NPC 的常驻 loop
  - bridge 丢失后进入 pause/backoff，恢复后继续
  - private-chat 关键区期间不会再触发 autonomy agent 决策

### 集成测试

- `App.xaml.cs` wiring 测试
  - 注册 `StardewNpcAutonomyBackgroundService`
  - 在启动后调用 `StartStardewNpcAutonomyBackground(...)`
- Stardew runtime 集成
  - discovered NPC 在启动后无需 UI 手动点击即可产生新的 runtime trace / activity log
  - private-chat 与 autonomy 共享同一 runtime namespace、transcript 根和 memory 根
  - discovered tools 更新后，下一轮 tick 使用新工具面

### 行为验证

- 启动 Desktop 后，不点 `RunOneTick`，目标 NPC 目录下仍持续出现新的 `runtime.jsonl` tick 记录。
- 触发 private-chat 会话时，对应 NPC autonomy 暂停；会话结束后恢复。
- 多 NPC 同时运行时，world claim 仍有效，且不会因为 autonomy 服务引入同 tile 冲突回归。

### 可观测性

- 为 autonomy 背景服务补最小日志：
  - attach/detach bridge
  - NPC loop start/pause/resume/fault/rebind
  - pause reason
  - rebind reason
- 验收必须能通过日志和 runtime activity 文件证明“后台持续运行”。

## 实施步骤

### 步骤 1：补齐 Desktop 启动链

工作内容：

- 在 `App.xaml.cs` 注册 `StardewNpcAutonomyBackgroundService`
- 在应用启动后自动 `Start()`
- 在 `ProcessExit` / stop 路径补齐 `Stop()`

验收标准：

- Desktop 启动无需手动点 UI，即会启动 autonomy 后台 owner。
- 新增 wiring 测试证明 private-chat 与 autonomy 两个后台服务都已接线。

### 步骤 2：把 discovered runtime 预热真正接入启动流

工作内容：

- 在 autonomy 后台服务 bridge attach 成功后调用 `NpcRuntimeHost.StartDiscoveredAsync(...)`
- 明确默认启用 autonomy 的 NPC 过滤规则（建议先走 manifest/config gate）

验收标准：

- 已启用 autonomy 的 NPC 会在 attach 后自动创建/复用 runtime namespace。
- 不再依赖手动 `RunOneTickAsync(...)` 才第一次生成 runtime。

### 步骤 3：新增 autonomy 常驻循环 owner

工作内容：

- 实现 `StardewNpcAutonomyBackgroundService`
- 按 NPC 维护长生命周期 loop：poll -> handle 获取/复用 -> 单轮 tick -> delay/backoff -> 下一轮
- 使用 `NpcAutonomyBudget` 约束并发、重启冷却、重试上限

验收标准：

- 单个 NPC 在 bridge 可用时能稳定连续产生多轮 tick。
- loop 故障不会导致后台服务整体退出。

### 步骤 4：定义 private-chat 与 autonomy 的共享生命周期契约

工作内容：

- 在 `NpcRuntimeInstance` 或等价共享状态层补 channel activity / pause lease
- private-chat 进入关键区时挂起 autonomy，退出后恢复
- 明确 pause reason 与状态暴露方式

验收标准：

- private-chat 回复阶段同一 NPC 不再并发产生 autonomy 决策。
- private-chat 结束后 autonomy 自动恢复，无需人工干预。

### 步骤 5：落地 rebind 边界与恢复策略

工作内容：

- autonomy 服务在 tick 边界比较 bridge key 与 tool fingerprint
- 变化后下一轮 rebind，不做 mid-turn 替换
- 记录 rebind 日志与 generation 变化

验收标准：

- 同一运行周期内 handle 仅在边界切换。
- 测试可断言 generation 增长符合预期，且当前轮不被打断。

### 步骤 6：补齐验证闭环

工作内容：

- 新增 unit/integration/wiring tests
- 保留 `RunOneTickAsync(...)` 作为 debug 工具，但从“唯一入口”降级为“观测/诊断入口”
- 用 activity log + runtime snapshot + 日志证明常驻自治已成立

验收标准：

- 自动化测试覆盖“自动启动、持续循环、pause/resume、rebind、恢复”。
- 人工验证不需要点 `RunOneTick` 也能看到 NPC 持续自治。

## 架构决策记录（ADR）

### 决策

采用“**新增 `StardewNpcAutonomyBackgroundService` 承接常驻 autonomy 编排；继续由 `NpcRuntimeSupervisor` 持有 shared runtime 身份与 handle 复用；`NpcRuntimeHost` 负责 discovered runtime 预热**”的方案。

### 决策驱动

- 需要对齐参考项目的常驻 agent 效果，但不能改成多进程。
- 当前 shared runtime 方向已经基本正确，问题在于缺少后台 owner 和持续调度。
- private-chat 已经有后台服务模式，autonomy 采用对位实现能最大限度复用现有结构并降低回归。

### 备选方案

- 方案 A：把常驻 loop 内嵌到 `NpcRuntimeInstance`
- 方案 C：重构成统一 `StardewNpcRuntimeCoordinatorService`

### 为什么选择当前方案

- 它在“尽快补齐常驻自治”与“避免重做 private-chat 编排”之间取得了最好平衡。
- 它保持通用 runtime 层纯净，把 Stardew 场景 owner 放在游戏后台服务层。
- 它自然复用现有 App 启动模式、supervisor rebind 机制、runtime namespace 和工具装配链。

### 后果

- 后台服务数量从 1 个 Stardew private-chat owner 增加到 2 个并行 owner，需要明确定义共享状态与互斥协议。
- `NpcRuntimeInstance` 需要承担少量生命周期协作状态，但不应膨胀成完整场景编排器。
- `RunOneTickAsync(...)` 将继续保留，但定位从功能入口变为 debug/诊断入口。

### 后续跟进

- 若后续出现更多 Stardew 后台通道（如 schedule follower、relationship loop），再评估是否把 private-chat 与 autonomy 合并进统一 coordinator。
- 若常驻 NPC 数量继续增长，再评估基于配置的默认启用范围、loop cadence、分级调度和可观测性聚合。

## 架构审查重点

- `NpcRuntimeInstance` 的最小新增状态是否足够支撑 pause lease，而不污染通用 runtime 抽象。
- `StardewNpcAutonomyBackgroundService` 与 `StardewPrivateChatBackgroundService` 的边界是否清晰，是否需要共享一个更薄的协调接口。
- discovered tools rebind 的“仅在 tick/reply 边界切换”是否足够，是否还需要显式版本观测或延迟生效策略。
- 默认启用 autonomy 的 NPC 选择规则是否应先走 manifest capability，再落到 user config 白名单。
