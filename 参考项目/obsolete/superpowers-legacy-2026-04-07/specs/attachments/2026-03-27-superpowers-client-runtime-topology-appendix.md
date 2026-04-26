# RETIRED REFERENCE - Superpowers 客户端运行拓扑设计

> 本文已退居辅助参考，不再是当前正式设计真相。  
> 当前正式入口：`docs/superpowers/specs/2026-03-27-superpowers-master-design.md`  
> 当前正式附件：`docs/superpowers/specs/attachments/2026-04-07-superpowers-core-dialogue-memory-social-appendix.md`

# Superpowers 客户端运行拓扑设计

## 1. 文档定位

本文档定义 `All Game In AI / superpowers` 在客户端侧的最小运行拓扑。

它回答的问题不是：

- 玩家前台应该显示哪些页面

而是：

- `服务器 / Launcher / Local Runtime / Game Mod` 这四段到底怎么拆
- 在“服务器端编排、本地执行”的模式下，`Runtime` 是否还有必要存在
- 如果保留 `Runtime`，它应该做多薄、多小、多克制
- 哪些职责必须留在：
  - 服务器
  - Launcher
  - Runtime
  - Mod
- 如何在不把复杂度失控的前提下，为后续 `agent 驱动 NPC` 留出空间

本文档承接以下文档：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-03-27-superpowers-platform-control-plane-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-superpowers-player-launcher-appendix.md`
- `docs/superpowers/governance/current-phase-boundary.md`
- `docs/superpowers/governance/client-exposure-threat-model.md`
- `docs/superpowers/contracts/product/capability-claim-matrix.md`
- `docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md`
- `docs/superpowers/contracts/runtime/deterministic-command-event-contract.md`
- `docs/superpowers/contracts/runtime/narrative-degradation-contract.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`

若与当前阶段硬边界、client exposure 威胁模型或 claim / sellability / waiver 口径冲突，仍以对应治理真相源为准。

## 2. 设计目标

本设计的目标不是把客户端做成一个复杂的本地平台，而是用最小必要的运行边界支撑：

1. `服务器端编排 + 本地执行`
2. `per-game` 隔离
3. `deterministic execution` 不丢
4. 玩家前台可展示：
   - readiness
   - 运行状态
   - 帮助与修复入口
5. 平台控制面可拿到：
   - trace
   - recovery
   - health
   - 问题线索
6. 后续如进入：
   - `agent 驱动 NPC`
   - 多 NPC 调度
   - 本地恢复深化
   也不需要推翻边界重来

## 3. 顶层判断

### 3.1 `Runtime` 仍然有必要，但必须做薄

在“服务器端编排、本地执行”的路线下：

- `重 Runtime` 不再合理
- `没有 Runtime` 也不理想

推荐路线固定为：

- `薄 Runtime`

它不是：

- 本地 AI 大脑
- 本地 narrative truth source
- 本地长期记忆真源
- 本地运营控制面

它是：

- `本地执行协调层`

### 3.2 不保留 `Runtime` 的代价

如果完全不保留 `Runtime`，则大量本应共享的能力会散落进各个 `Mod`，包括：

- 请求与 trace 关联
- deterministic gate
- recovery hooks
- per-game health state
- 诊断包整合
- 本地执行前置校验

这样短期看简单，长期会导致：

- 多游戏协议碎片化
- Launcher 难以统一做按游戏状态与恢复
- Mod 逐步承担平台职责

### 3.3 `Runtime` 必须从“本地编排层”降级为“本地执行协调层”

当前固定判断如下：

- 主要编排真相源在服务器
- 主要 entitlement / claim / sellability / cost 真相源在服务器
- `Runtime` 只负责：
  - 本地会话边界
  - canonical input 整理
  - 结果接收
  - deterministic gate
  - host writeback 前置校验
  - trace / health / recovery

### 3.4 `Hosted Capability` 不是 `M1` 默认 launch dependency

当前 `M1` 默认服务器侧核心是：

- `Cloud Control`
- `Hosted Narrative Orchestration`
- `Platform Control Plane`（operator/support plane，不属于当前 launch-path runtime core）

`Hosted Capability` 只能在以下前提同时成立时接入：

- 当前 `capability` 已进入当前 phase 的批准范围，或已被批准为 experiment / preview
- 当前 `skuId + gameId + capability + billingSource` 在 claim / sellability artifact 中有明确状态
- 若涉及 `phase_waived`、`partial_preview` 或缺项能力，已有有效 waiver / disclosure / evidence linkage

因此：

- `Hosted Capability` 不能被写成 `M1` 默认首发强依赖
- `Launcher`、`Runtime`、`Mod` 都不得自行假定某能力默认走 hosted path

## 4. 推荐拓扑

`M1` 推荐拓扑固定为：

```text
Server Side
  - Cloud Control
  - Hosted Narrative Orchestration
  - Platform Control Plane (operator/support plane)
  - Hosted Capability (approved capability / sku / billingSource only)

Client Side
  - Launcher UI
  - Launcher Supervisor
  - Local Runtime (per gameId)
  - Game Adapter Mod
```

### 4.1 四段关系

#### 4.1.1 服务器

负责：

- entitlement enforcement 真源
- claim / disclosure / sellability 执行真源
- narrative orchestration 真源
- cost / usage / business 真源
- 运营控制面
- 通知与问题处理闭环
- 当前已批准 hosted capability 的服务端路由

不负责：

- 直接写回宿主游戏
- 直接替代本地 deterministic gate
- 直接替代 per-game adapter

#### 4.1.2 Launcher

负责：

- 玩家前台
- 登录承接
- 启动控制
- 产品与兑换入口
- 按游戏状态展示
- player-visible `launchReadinessVerdict` artifact
- 求助与回执展示

不负责：

- 每轮 AI 请求承载
- narrative candidate lowering
- authoritative host writeback
- 平台控制面后台职责
- 自行决定 capability support / sellability / waiver 状态

补充说明：

- `Launcher` 不拥有 readiness policy 真源
- 但它拥有玩家可见的 `launchReadinessVerdict` artifact
- 该 artifact 必须由以下输入 deterministic join 后生成：
  - `claimStateRef`
  - runtime health fact
  - quarantine state
  - recovery entry
- `launchReadinessVerdict` 是当前唯一允许展示给玩家的 ready / blocked verdict
- `Launcher UI`、`Runtime`、`Mod` 都不得各自再产出第二套 player-visible readiness verdict

#### 4.1.2A Launch Readiness Authority Contract

当前 `launch readiness` 固定拆成两层：

- `launchReadinessPolicySnapshot`
  - owner: `Cloud Control`
  - authority: server-side policy truth
  - 用途: 固定当前 `gameId + skuId + capability-set + billingSource` 在当前版本下的 ready / blocked policy、join 规则版本与 policy hash
- `launchReadinessVerdict`
  - owner: `Launcher Supervisor`
  - authority: player-visible derived verdict artifact
  - 用途: 基于 server-signed policy snapshot 与本地 runtime facts 生成唯一可见 verdict

`launchReadinessVerdict` 的 deterministic join 输入固定为：

- `launchReadinessPolicySnapshotRef`
- `capabilityAccessDecisionRef`
- `claimStateRef`
- `runtimeHealthRef` 或 `runtimePreflightRef`
- `quarantineStateRef`
- `recoveryEntryRef`

`launchReadinessVerdict` 最小字段固定为：

- `readinessVerdictId`
- `gameId`
- `policyVersion`
- `policyHash`
- `launchReadinessPolicySnapshotRef`
- `capabilityAccessDecisionRef`
- `claimStateRef`
- `runtimeHealthRef` 或 `runtimePreflightRef`
- `quarantineStateRef`
- `recoveryEntryRef`
- `verdict`
- `primaryReasonCode`
- `ctaKind`
- `generatedAt`

`verdict` 枚举固定为：

- `ready`
- `running`
- `needs_repair`
- `needs_update`
- `isolated`
- `blocked`

`ctaKind` 枚举固定为：

- `launch`
- `continue`
- `open_repair`
- `update_now`
- `contact_support`

verdict 裁决优先级固定为：

1. `blocked`
2. `isolated`
3. `needs_repair`
4. `needs_update`
5. `running`
6. `ready`

解释：

- `blocked` 是 fail-closed terminal verdict
- 一旦 server-side access 判定命中 `deny / expired`，`blocked` 必须压过任何本地 `running / ready`

规则：

- 只有 `Launcher Supervisor` 可以生成或刷新 `launchReadinessVerdict`
- `Runtime`、`Mod`、`AFW`、后台页面都只允许产出 inputs / facts，不得直接产出玩家可见 verdict
- 若 server-side policy snapshot 与本地 facts 版本不一致，`Launcher` 必须先刷新 verdict，不得沿用旧 verdict 放行启动
- 若 `capabilityAccessDecision.decision = deny`，当前 `launchReadinessVerdict` 必须 fail-closed 为 `blocked`
- 若 `capabilityAccessDecision.expiresAt` 已过期，当前 `launchReadinessVerdict` 必须 fail-closed 为 `blocked`
- `blocked` 一旦成立，不得被本地旧 `ready / running` verdict 覆盖
- `ctaKind` 不是独立优先级真源；它必须从最终 `verdict` deterministic 导出

`runtimePreflightRef` 规则：

- 仅用于 `Runtime` 尚未拉起前的首启 / 净安装 / 冷启动判定
- owner 固定为 `Launcher Supervisor`
- 只允许承载：
  - 安装完整性
  - 版本兼容性
  - 本地路径/资源可用性
  - 预检 quarantine 事实
- 一旦 `Runtime` 已存在可用的 `runtimeHealthFact`，`launchReadinessVerdict` 必须改用 `runtimeHealthRef`，不得继续沿用旧 `runtimePreflightRef`

#### 4.1.3 Local Runtime

负责：

- 单游戏本地会话执行边界
- canonical input builder
- deterministic execution gate
- runtime state store
- health / degradation / recovery
- trace / audit glue

不负责：

- 本地 claim truth
- 本地 waiver truth
- 本地 commercial policy
- 本地 hosted capability entitlement 决策

#### 4.1.3A Runtime State Authority Contract

`runtime state` 的唯一 owner 固定为：

- `Local Runtime`

对外消费的 authoritative artifact 固定为：

- `runtimeHealthFact`

`runtimeHealthFact` 最小字段固定为：

- `runtimeHealthRef`
- `launchSessionId`
- `gameId`
- `runtimeState`
- `healthState`
- `quarantineState`
- `quarantineStateRef`
- `recoveryEntryRef`
- `traceGroupId`
- `updatedAt`
- `stateVersion`

`runtimePreflightFact` 最小字段固定为：

- `runtimePreflightRef`
- `gameId`
- `preflightState`
- `quarantineStateRef`
- `recoveryEntryRef`
- `generatedAt`

`preflightState` 枚举固定为：

- `preflight_ready`
- `preflight_blocked`
- `preflight_repair_required`

`runtimeState` 枚举固定为：

- `not_started`
- `starting`
- `ready`
- `running`
- `degraded`
- `repair_required`
- `isolated`
- `stopped`

`healthState` 枚举固定为：

- `healthy`
- `degraded`
- `failed`

状态转移 authority 固定为：

- 只有 `Local Runtime` 可以写 `runtimeState`、`healthState`、`quarantineState`
- `Launcher` 只消费，不回写
- `Cloud Control / Platform Control Plane` 只消费 evidence 与 trace，不回写本地 runtime state

#### 4.1.4 Game Mod

负责：

- 宿主 hook
- 游戏事件采集
- 宿主 UI surface
- 命令落地
- authoritative writeback 的游戏侧具体实现

## 5. 进程与隔离模型

### 5.1 `M1` 默认按 `gameId` 隔离 Runtime

推荐固定为：

- 一个前台 `Launcher`
- 零到多个按 `gameId` 隔离的 `Runtime`
- 每个 `Runtime` 只服务一个活跃 `gameId` 会话

这意味着：

- `Launcher` 与 `Runtime` 的边界是明确的进程边界
- 不采用“所有游戏共用一个本地 Runtime”
- 不采用“把 Runtime 完全嵌进 Launcher UI 线程”

### 5.2 这样设计的原因

主要原因如下：

1. 单游戏故障不直接拖死其他游戏
2. `per-game readiness / diagnostics / recovery / quarantine` 更自然
3. trace、日志、恢复点可以按游戏隔离
4. 更适合后续做：
   - 单游戏重建
   - 局部恢复
   - agent 化调度

### 5.3 `M1` 不承诺什么

即使采用子进程隔离，`M1` 仍然不承诺：

- UI 与 Runtime 的完全独立生存语义
- 同一会话内无感自动重建 Runtime
- 多层执行权威细拆
- 默认存在 hosted capability 依赖

`M1` 应承诺的是：

- 受控隔离
- 受控退出
- 受控恢复

## 6. 各层职责边界

### 6.1 服务器职责

服务器固定负责：

- 登录与设备校验真源
- entitlement / sellability / billing 真源
- narrative orchestration decision
- 当前已批准 hosted capability 调用
- 平台控制面
- bug / 通知 / 兑换闭环

服务器不负责：

- 直接写回宿主游戏
- 直接替代本地 deterministic gate
- 直接替代 per-game adapter

#### 6.1.1 Access Authority Contract

当前 `access` 的唯一 server-side authority artifact 固定为：

- `capabilityAccessDecision`

owner 固定为：

- `Cloud Control`

`capabilityAccessDecision` 最小字段固定为：

- `decisionId`
- `playerId`
- `gameId`
- `skuId`
- `capability`
- `billingSource`
- `policyVersion`
- `policyHash`
- `decision`
- `claimStateRef`
- `waiverId` 或 `waiverLineageId`（条件适用）
- `decisionReasonCode`
- `expiresAt`
- `issuedAt`

`decision` 枚举固定为：

- `allow`
- `deny`
- `allow_with_disclosure`
- `allow_experiment_only`

规则：

- `Runtime` 只能消费 `capabilityAccessDecision`，不得自行拼 claim / waiver / entitlement 规则做 hosted-path 决策
- `Launcher` 只能用它参与 `launchReadinessVerdict` join，不得自创 access gating
- `Platform Control Plane` 可查看与追责，但不单独生成运行期 decision
- `capabilityAccessDecision` 与 `launchReadinessPolicySnapshot` 必须共享同一 `policyVersion + policyHash`；不同纪元的 access artifact 不得参与同一 verdict join

### 6.2 Launcher 职责

Launcher 固定负责：

- 产品入口
- 登录状态
- 游戏列表与启动按钮
- readiness 结果展示
- per-game 问题摘要
- 产品介绍与 Key 兑换
- 提交问题与接收回执

Launcher 不负责：

- 每轮 AI 请求承载
- narrative candidate lowering
- authoritative host writeback
- 平台控制面后台职责

### 6.3 Runtime 职责

`Runtime` 在 `M1` 只保留以下最小职责。

#### 6.3.1 本地会话边界

至少管理：

- `launchSessionId`
- 当前 `gameId`
- 当前 runtime state
- 本地执行上下文生命周期

#### 6.3.2 canonical input builder

负责把游戏内采集到的状态整理成：

- snapshot
- 当前输入
- 必要 raw history

然后交给服务器端编排层。

#### 6.3.3 服务器调用代理

`M1` 默认负责承接：

- hosted narrative orchestration 调用

仅在当前 `capability / sku / billingSource` 已获批准时，才额外承接：

- hosted capability 调用

但不持有：

- orchestration 真源
- entitlement 真源
- claim / sellability 真源
- waiver 真源
- cost 真源

#### 6.3.3A Cost Attribution Authority Contract

当前 `cost attribution` 的唯一 authoritative fact 固定为：

- `costAttributionFact`

owner 固定为：

- `Cloud Control`

`costAttributionFact` 最小字段固定为：

- `costFactId`
- `requestId`
- `traceId`
- `playerId`
- `gameId`
- `skuId`
- `capability`
- `billingSource`
- `providerRef`
- `modelRef`
- `usageBasis`
- `estimatedCost`
- `attributedAt`

规则：

- `billingSource=user_byok` 与 `billingSource=platform_hosted` 必须各自产出独立 fact，不得静默并账
- `Runtime` 只记录 trace-linked local usage evidence，不得产出最终 cost fact
- `商业 / 额度`、补偿、毛利预警、审计只允许消费 `costAttributionFact`

#### 6.3.4 deterministic execution gate

这是 Runtime 必保能力。

至少负责：

- 白名单检查
- 参数合法性检查
- 前置条件检查
- phase-scoped lowering
- fail-closed

相关真源以：

- `docs/superpowers/contracts/runtime/deterministic-command-event-contract.md`

为准。

#### 6.3.5 health / degradation / recovery

至少负责：

- 当前是否可用
- 当前是否 degraded
- 当前 recovery entry 是什么
- 当前是否需要人工修复

相关真源以：

- `docs/superpowers/contracts/runtime/narrative-degradation-contract.md`

为准。

#### 6.3.6 trace / audit glue

至少负责把本地运行态与宿主落地证据串起来。

相关真源以：

- `docs/superpowers/contracts/runtime/trace-audit-contract.md`

为准。

### 6.4 Mod 职责

每个游戏的 `Mod` 固定负责：

- 游戏内事件采集
- 游戏内 surface 渲染
- 宿主命令实际落地
- 游戏侧错误证据采集

`Mod` 不应直接承担：

- entitlement 真相判断
- claim / sellability / waiver 状态判断
- 跨游戏共享平台职责
- 服务端产品策略判断
- 平台控制面职责

## 7. 主数据流

### 7.1 启动流

推荐固定为：

1. 玩家打开 Launcher
2. Launcher 从服务器拿到登录 / 权益 / claim result / 版本信息
3. 玩家选择某个游戏并点击启动
4. Launcher 先汇总服务器 `launchReadinessPolicySnapshot`、`capabilityAccessDecision`、claim result，以及：
   - 若 `Runtime` 尚未拉起：`runtimePreflightFact`
   - 若 `Runtime` 已存在：`runtimeHealthFact`
   生成当前 `launchReadinessVerdict`
5. 只有 `launchReadinessVerdict` 允许启动时，Launcher Supervisor 才拉起该 `gameId` 的 Runtime
6. Runtime 初始化本地会话边界
7. Launcher 再启动或接入该游戏 Mod 链路

### 7.2 运行流

推荐固定为：

1. Mod 采集游戏状态与玩家输入
2. Mod 把最小必要数据交给 Runtime
3. Runtime 构建 canonical input
4. Runtime 调用服务器端 orchestration
5. 服务器返回结构化结果或 candidate
6. Runtime 经过 deterministic gate 做 lowering 与前置校验
7. Runtime 向 Mod 下发可执行命令
8. Mod 负责宿主落地与玩家可见 surface
9. 结果与 trace 证据回到 Runtime，再汇总到平台可审层

### 7.3 异常流

推荐固定为：

1. Mod 或 Runtime 发现错误
2. Runtime 记录 state transition、recovery entry 与 trace
3. Launcher 读取按游戏 health fact
4. 玩家前台只看到：
   - 出现了什么问题
   - 建议按哪个按钮
5. 若玩家提交问题，则 Launcher 汇总：
   - Launcher
   - Runtime
   - Mod
   的脱敏证据摘要进入平台闭环

### 7.4 Cross-End Contract Rule

并行开发时，四端固定只通过以下 authoritative artifacts 对齐：

- Launcher 消费：
  - `launchReadinessPolicySnapshot`
  - `launchReadinessVerdict`
  - `runtimePreflightFact`
  - `runtimeHealthFact`
  - `capabilityAccessDecision`
- Runtime 消费：
  - `capabilityAccessDecision`
  - deterministic command / event envelope
- Platform Control Plane 消费：
  - `runtimeHealthFact`
  - `costAttributionFact`
  - trace / recovery evidence
- Mod 消费：
  - Runtime 下发的 deterministic command envelope

不允许：

- 任何一端绕开上述 artifacts 自己重算 ready / blocked / cost / access
- 任何一端把内部 convenience state 暴露成第二套 authority contract

## 8. 为什么不直接让 Mod 连服务器

这条路线不是不可能，但在当前产品目标下不推荐作为默认结构。

不推荐原因如下：

1. 各游戏会各自长出一套本地协议与错误处理
2. readiness / recovery / quarantine 很难统一
3. 玩家前台很难稳定展示按游戏状态
4. deterministic gate 会被迫散落到各个 Mod
5. 后续做 agent 化、多 NPC 调度、本地事件缓冲会更痛苦

## 9. 为什么不做重 Runtime

当前也不推荐把 Runtime 做成重型本地 AI 层。

不推荐原因如下：

1. 服务器端编排已经是默认路线
2. 重 Runtime 会和服务器真相源打架
3. 复杂度会明显上升
4. 很容易把本地又做出一套“半个控制面”

因此 Runtime 必须被限制为：

- `本地执行协调层`

而不是：

- `本地 AI 编排平台`

## 10. 对未来 `agent 驱动 NPC` 的兼容性

保留薄 Runtime 的主要长期收益，在于它为后续 agent 化留了边界。

### 10.1 `M1`

`M1` 的 Runtime 不负责：

- 本地 agent 大脑
- 本地长期 memory 真源
- 多 NPC 自主调度平台
- 默认 hosted capability 编排

`M1` 只负责：

- 执行协调
- deterministic gate
- recovery
- trace

### 10.2 `M2`

若后续引入更强的 `agent 驱动 NPC`，Runtime 可以逐步增加：

- 本地事件缓冲
- 单游戏 turn queue
- 轻量本地调度
- 多结果串行落地控制

### 10.3 `M3`

到更后期，才考虑是否需要：

- 本地常驻 agent helper
- 更强的本地调度与恢复系统

但即使到那时，也不应推翻：

- 服务器持有主要 orchestration truth source

这条原则。

## 11. 与玩家前台 Launcher 的衔接

玩家前台文档见：

- `docs/superpowers/specs/attachments/2026-03-27-superpowers-player-launcher-appendix.md`

两份文档的关系应固定为：

- Launcher 文档负责玩家视角页面与交互
- 本文负责底层运行边界与职责分配

玩家前台看到的：

- readiness
- 运行中
- 需修复
- 已隔离
- 一键修复

其背后分别对应：

- Launcher-managed `launchReadinessVerdict`
- Runtime health fact
- recovery entry
- per-game quarantine
- guided recovery flow

`launchReadinessVerdict` 至少要能稳定回链：

- `gameId`
- `claimStateRef`
- `runtimeHealthRef`
- `quarantineStateRef`
- `readinessVerdictId`

但这些底层结构不应直接暴露成玩家术语，也不应绕开 claim / disclosure / sellability 真相源。

## 12. 最终结论

本设计的最终结论如下：

1. 在“服务器端编排、本地执行”的模式下，`Runtime` 仍然有必要保留，但必须做薄。
2. `M1` 默认采用：
   - `服务器 / Launcher / per-game Runtime / Game Mod`
   的四段结构。
3. `M1` 默认服务器侧核心是：
   - `Cloud Control + Hosted Narrative Orchestration`
   - `Platform Control Plane` 只作为 operator/support plane 存在，不属于当前 launch-path runtime core
4. `Hosted Capability` 只在当前 `capability / sku / billingSource` 已批准时接入，不能被写成默认 launch dependency。
5. `Runtime` 的定位固定为：
   - `本地执行协调层`
   而不是本地 AI 大脑。
6. `Launcher` 仍然只是：
   - 产品入口与运行控制器
   不是 AI 请求主承载层。
7. `Mod` 仍然只是：
   - 游戏适配与宿主落地层
   不应承担平台真相源职责。
