# All Game In AI 双核通用框架设计

## 1. 文档定位

本文档是 `All Game In AI` 的上位产品与架构总纲。

它高于当前以单项目首发为中心的分期稿，专门用于回答以下问题：

- 这套产品未来到底要卖什么
- 跨游戏通用框架的承重墙是什么
- `OpenAIWorld` 这类成熟 mod 应该如何被继承
- `Microsoft Agent Framework` 应该放在哪一层
- `M1 / M2 / M3` 在双核框架下如何保持连贯，而不是各自为政

本文档不是某一款游戏、某一阶段或某一条实验链路的实现细节稿。

它的职责是定义：

- 长期产品真相源
- 长期框架真相源
- 例外治理真相源
- 跨项目长期 guardrail 真相源

若与下列历史设计稿冲突，以本文档的上位框架与产品契约定义为准：

- `2026-03-25-all-game-in-ai-tech-stack-design.md`
- `2026-03-26-cross-genre-extensibility-design.md`
- `2026-03-26-relationship-foundation-design.md`
- `2026-03-26-superpowers-m1-m2-m3-refactor-design.md`

上述历史文档保留为归档参考，但不再承担最高层定义职责。

必须同时写死以下真相源顺序：

1. 当前生效的 `phase-boundary` 文档
2. 本文档
3. 历史专项与参考文档

当前生效的 `phase-boundary` 文档固定为：

- `docs/superpowers/governance/current-phase-boundary.md`

更具体地说：

- 本文档拥有长期产品 / 长期框架 / 长期契约定义权
- 当前阶段的 `M1 / M2 / M3` 硬边界、禁做项、托管依赖红线、协商面红线，仍由 `docs/superpowers/governance/current-phase-boundary.md` 拥有最高优先级
- 若冲突涉及阶段范围、托管依赖、计费账本、协商面、协议重负担引入，则必须以当前 phase-boundary 文档为准

## 2. 设计目标

本文档的目标不是继续做“更完整的平台想象”，而是建立一套以后可以持续支撑商业产品线的双核通用框架。

目标如下：

1. 让 `All Game In AI` 成为一条可持续出货的跨游戏 AI Mod 产品线，而不是一次性项目。
2. 让玩家在不同游戏中同时感知到：
   - 产品体验一致
   - 基础 AI 体验语义一致
3. 让宿主治理与叙事 AI 都成为一等公民，而不是互相附属。
4. 让 `OpenAIWorld` 这种成熟 mod 的真实工程经验，沉淀成可复用框架，而不是停留在个案崇拜。
5. 让 `Microsoft Agent Framework` 成为合适层级的叙事编排引擎，而不是吞掉整套产品治理与宿主执行边界。
6. 让 `M1 / M2 / M3` 的阶段边界既能支撑现实交付，也不反噬长期产品定义。

## 3. 产品定义

`All Game In AI` 的长期产品定义，不是“带 AI 的启动器”，也不是“某个游戏的聊天模组合集”。

它更准确的定义是：

- 一条跨游戏 AI Mod 产品线
- 一个统一的桌面产品入口
- 一套统一的叙事 AI 基础包
- 一套允许按游戏还原宿主风格的落地框架

玩家在不同游戏里默认应感知到同一套基础包语义：

- 对话
- 记忆
- 主动可确定性实体交互
- 群聊 / 多人连续发言
- 信息传播
- 主动世界 / 世界事件推进

这套能力集合在本文档中统一称为：

- `Narrative Base Pack`

高成本多媒体能力单独定义为：

- `Premium Media Pack`

`Premium Media Pack` 当前包括但不限于：

- `AI voice`
- `AI image`
- `AI video`

其商业策略原则如下：

- 不得污染 `Narrative Base Pack` 的生存主线
- 不得把基础包缺项伪装成 premium 升级点
- 可以独立计费、独立托管、独立风控与独立预算治理

## 4. 顶层判断

本框架不采用“宿主治理上位、AI 下挂”的单核路线，也不采用“AI 上位、宿主治理做外壳”的单核路线。

顶层路线固定为：

- `双核框架`

两核如下：

1. `Host Governance Core`
2. `Narrative Base Pack Core`

在这两核之下，再加一个必须独立存在的确定性层：

- `Deterministic Game Execution Layer`

在这三层之上，再由产品契约层约束玩家可见能力、例外治理与商业边界。

如果压成一句话：

- 这不是“AFW + 一堆游戏适配器”
- 而是“产品治理内核 + 服务端叙事真相源 + 本地确定性宿主执行层”的双核框架

## 5. 双核框架总图

```text
Product Contract Layer
  - Narrative Base Pack contract
  - Premium Media Pack contract
  - Capability visibility rules
  - Waiver / exception governance

Host Governance Core
  - launcher / runtime lifecycle
  - auth / entitlement
  - diagnostics / recovery / quarantine
  - per-game launch profile
  - trace / audit / supportability

Narrative Base Pack Core
  - hosted narrative orchestration truth source
  - snapshots
  - canonical recent history
  - long-term memory truth source
  - channel orchestration
  - intent protocols
  - validation / repair / normalize
  - narrative policy
  - sidecar / replay

Deterministic Game Execution Layer
  - typed commands / event contracts
  - whitelist execution
  - precondition checks
  - per-game mapping
  - authoritative host writeback

Per-Game Adapter + UI Binding
  - host hooks
  - game-local vocabulary
  - style-preserving presentation
  - tangible interaction / propagation / world-event surfaces
```

这里还必须补一条默认实现路线：

- `Narrative Base Pack Core` 默认由服务端持有编排真相源
- `Deterministic Game Execution Layer` 默认留在本地持有 authoritative apply 真相源
- 客户端只上传编排所需的 canonical input，不持有完整叙事编排脑子
- 服务端只返回当前轮落地所需的最小必要结果，不直接拥有宿主 authoritative apply 权

## 6. Host Governance Core

`Host Governance Core` 是产品可卖、可启动、可恢复、可支持的承重墙。

它负责跨游戏必须稳定一致的能力：

- 安装、更新、卸载
- 启动、拉起、关闭、并发控制
- 登录、授权、设备校验、宽限与撤销
- 运行配置真相源
- per-game readiness checks
- per-game diagnostics / recovery / quarantine
- 版本兼容线
- 日志、诊断包、支持面、trace 对账
- capability 启停与降级状态暴露
- 例外治理与可见性治理

它不直接承担 NPC 叙事编排，也不直接承担游戏 authoritative 写回。

它理解的是：

- 哪个游戏可运行
- 哪些能力当前可启用
- 当前处于何种降级状态
- 出问题时如何恢复
- 玩家前台要看到什么信息

## 7. Narrative Base Pack Core

`Narrative Base Pack Core` 是玩家真正会为之买单的灵魂部分。

它不是“若干 AI 功能项”的集合，而是一套必须保持语义一致的叙事交互内核。

我建议固定拆成以下 `8` 个子层：

### 7.1 Snapshot Layer

负责把宿主对象压成稳定摘要，而不是把宿主原始对象图直接交给模型。

至少包括：

- `ActorSnapshot`
- `RelationSnapshot`
- `SceneSnapshot`
- `TurnContext`

原则：

- 可裁剪
- 可缓存
- 可比较
- 可序列化
- 可跨游戏映射

同时必须写死输入边界：

- 默认上传给叙事编排层的是 canonical snapshot / digest，而不是宿主原始对象图
- 对 `group_chat` 等确实需要顺序连续性与语气保真的 channel，允许额外上传 canonical recent raw history / raw channel JSON
- 这里的 `raw` 指 canonical channel history artifact，不是宿主内部对象引用、原始指针或未裁剪对象树
- 服务端编排层可以消费 snapshot 与经批准的 canonical raw channel history，但不得回碰原始宿主对象

### 7.1.1 Prompt / Persona / World-Rule Governance Layer

`OpenAIWorld` 的可移植核心不只有 snapshot、memory 和 orchestration。

还必须把 prompt / persona / world-rule 的治理面正式提升为共享层。

但这里共享的不是某一种固定目录结构或某一个继承拓扑，而是以下治理要求：

- pack class 定义
- active pack resolution order
- override policy
- provenance / versioning
- profile binding

这里必须写死：

- `OpenAIWorld` 提供的是成熟参考模式，不是唯一 pack 拓扑模板
- 共享的是“如何声明来源、如何决定覆盖顺序、如何把当前生效 pack 绑定到 profile 与 trace”
- 不共享具体宿主世界观外壳、语料风格、fiction 文本或目录长相
- 每个游戏可以采用不同 pack hierarchy，但必须把当前生效 pack set、resolution order、override policy 与 provenance 写成可审查 artifact

这些 pack 还必须被当作一等商业资产，而不是普通配置文件。

同时必须进一步升级判断：

- 真正的商业资产不只包括 prompt / persona / world-rule pack
- 还包括 prompt assembly、memory selection、channel orchestration、speaker selection、propagation policy、world-event candidate generation、degradation policy 等完整叙事编排真相源

因此：

- prompt / persona / world-rule pack 默认视为 `proprietary asset`
- 完整 narrative orchestration truth source 默认视为 `hosted-only proprietary asset`
- 框架只复用治理规则与接口，不复用某个游戏或某个成熟 mod 的具体 prompt 财产
- 必须区分 `local-distributable`、`hosted-only`、`developer-only` 三类 narrative asset
- 不得默认向客户端、日志、诊断包、调试导出面暴露完整明文 prompt pack 或完整编排规则链
- 任何 narrative asset 的导出、复制、离线缓存、训练再利用都必须受显式治理
- 客户端分发、`.NET` 反编译、导出治理与 hosted-only 资产边界，统一以 `docs/superpowers/governance/client-exposure-threat-model.md` 为执行威胁模型

### 7.2 Memory Layer

固定分成两层：

- `short-term memory`
- `long-term memory`

默认长期记忆路线固定为：

- `summary memory + time bucket`

而不是一开始就把复杂 RAG、向量检索或知识图谱当作基础包默认前提。

同时必须写死部署边界：

- 长期记忆真相源默认在服务端，不在客户端
- `short-term memory` 中用于 recent-context replay 的 canonical recent history 默认也在服务端持有短窗口真相源
- 客户端只保留当前轮与最小必要缓存，不得把长期记忆或完整 recent-history store 当作本地真相源
- 对 `group_chat`，服务端必须持有 canonical recent raw history 短窗口，以支持与参考 mod 一致的轮流发言链路

### 7.3 Channel Orchestration Layer

这层是一等公民，不得退化为“几份 prompt 模板”。

必须显式支持并定义边界：

- `face_to_face_private_dialogue`
- `group_chat`
- `remote_communication`
- `information_propagation`
- `active_world`

关键不只是“生成什么”，而是：

- 何时触发
- 带哪些上下文
- 哪条链路先执行
- 谁先说、谁后说
- 哪些传播可以继续，哪些必须截止
- 哪些世界事件可以进入候选生成

默认运行路线固定为：

- 服务器先持有并解析 canonical snapshot、current input、approved recent raw history
- 服务器上的编排引擎负责 channel orchestration、speaker selection、per-speaker generation、structured parse、candidate generation
- 本地 deterministic layer 只负责 legality check、whitelist execution、UI 落地与 authoritative writeback

并且对于已被参考 mod 证明有效的重 channel，必须优先保留其链路结构，而不是为了框架整齐而改写语义顺序。

对于 `group_chat`，默认复现顺序固定为：

1. 玩家消息或系统触发先进入 group queue
2. 先做 `speaker selection`
3. 冻结本轮 speaker order
4. 再按顺序做 `per-speaker generation`
5. 对结果做结构化解析
6. 回写 canonical group history
7. 再进入动作分发与后续传播候选

这里允许使用 `Microsoft Agent Framework` 承载 workflow / session / checkpoint / middleware，但不得改变这条已证明有效的链路顺序与数据组织原则。

其中：

- `remote_communication` 固定视为共享内部 channel
- 在产品契约层，它归并到 `information_propagation` 的必审实现维度中

### 7.4 Intent Protocol Layer

所有 LLM 输出都必须先落在结构化意图上。

至少应包含：

- `content`
- `action_intents`
- `propagation_intents`
- `world_event_intents`
- `diagnostic_sidecar`

这层是共享 contract，不是某款游戏的内部 DTO。

### 7.5 Deterministic Validation Layer

模型永远只负责提议，不负责直接落地。

因此必须存在中间校验层，负责：

- typed parse
- schema check
- repair / normalize
- whitelist enforcement
- argument bounds
- precondition evaluation
- reject / degrade policy

### 7.6 Per-Game Narrative Semantic Binding Layer

这里开始进入 game-local 语义绑定，但它不拥有 authoritative writeback。

它负责的是 narrative 侧的宿主风格翻译，例如：

- 对话语气与表现语义绑定
- 群聊可见语义绑定
- 传播语义与频道语义绑定
- 世界事件命名、提示与可感知反馈语义绑定
- actor / place / faction 的 game-local vocabulary 绑定

这里明确不负责：

- 可确定性实体交互的宿主应用路径
- 传播动作应用
- 世界事件物化
- authoritative host writeback

以上内容统一归入：

- `Deterministic Game Execution Layer`

### 7.7 Sidecar & Replay Layer

这层必须升级成正式能力，不得仅作为调试附属。

至少保留：

- redacted context digest；完整 rendered prompt 只允许进入经显式批准的 `developer-only` 受控流程
- raw model output
- parsed intents
- executed actions / events
- rejected actions / events
- trace correlation

同时必须写死：

- 发给客户端或普通诊断导出面的 sidecar 默认只能包含 redacted digest 与最小必要执行结果
- 完整 replay envelope、完整 rendered prompt、完整 orchestration resolution 只允许留在服务端受控存储或 `developer-only` 流程
- 不允许通过 checkpoint、telemetry、diagnostic export 反推出完整 narrative orchestration truth source

### 7.8 Narrative Policy Layer

这一层承接产品契约与例外治理。

它负责：

- 哪些基础包能力必须可见
- 哪些降级允许存在
- 哪些降级必须显式说明
- 哪些缺项必须升级成产品决策
- 哪些 premium 媒体能力禁止侵入基础包生存线

## 8. Deterministic Game Execution Layer

我建议把这一层从叙事编排中明确剥离，作为双核框架下的第三承重点。

原因很简单：

- `OpenAIWorld` 真正最值钱的一点，不是 prompt，而是：
  - `结构化中间层 -> 白名单执行器 -> 宿主世界回写`

因此必须明确：

- LLM 不直接拥有宿主 authoritative 写回权
- AFW 不直接拥有宿主 authoritative 写回权
- workflow 成功不等于游戏状态正确落地

这一层至少负责：

- typed commands / events
- whitelist execution
- precondition checks
- game-local mapping
- gift / transfer application
- propagation application
- world event materialization
- authoritative writeback
- replay / rollback 兼容性
- fail-closed 执行失败语义

## 9. OpenAIWorld 作为成熟锚点

`OpenAIWorld` 在本框架里应被定义为：

- `Reference Anchor`

但必须分成三种锚点，而不是混着用。

### 9.1 Mechanics Anchor

这是必须继承的工程模式。

应继承的核心模式包括：

- `角色快照 -> 关系摘要 -> 短期历史 -> 长期记忆 -> 频道编排 -> LLM 结构化输出 -> 白名单动作/事件执行 -> sidecar 回写 -> 下一轮再用`
- 群聊作为轮流发言的多轮决策系统
- 传播作为真实可写回的消息流动动作
- 主动世界作为结构化世界事件候选生成，而非直接遥控 NPC
- 长期记忆优先走摘要压缩与时间分桶

### 9.2 UX Semantics Anchor

这部分不是要求所有游戏 UI 长得一样，而是要求玩家在不同游戏里都能感知到相同的核心语义：

- NPC 会记住你
- 关系会影响反应
- 群聊是活的，不是单段脚本
- 消息会流动
- 世界会继续生长事件
- 文本、动作、事件三层反馈彼此印证

### 9.3 Boundary Anchor

这一类恰恰是不能照搬的内容：

- 修仙世界观外壳
- 特定 prompt 文风与角色卡语料
- 某个宿主专有对象命名
- 某个宿主专有 UI 结构
- 某个宿主专有动作全集

因此必须写死一条红线：

- `OpenAIWorld` 是语义和工程模式锚点，不是实现外壳模板

## 10. UI 语义与宿主风格原则

`OpenAIWorld` 的 UI 应该被当成：

- `UI 语义锚点`

而不是统一皮肤模板。

规则如下：

1. 玩家可感知语义必须尽量一致。
2. 宿主游戏风格优先于跨游戏统一皮肤。
3. 允许同一语义在不同游戏里映射成完全不同表现形态。
4. 不追求统一组件外观、统一布局、统一视觉语言。
5. 追求的是：
   - 频道感
   - 持续性
   - 世界感
   - 角色感
   - 消息流动感

示意如下：

- `Stardew Valley`
  - 原版对话框
  - 头顶气泡
  - 邮件
  - 事件式多人连续发言
- `RimWorld`
  - 气泡
  - 日志
  - 地图事件
  - 连续多人对话
- `太吾绘卷`
  - 人物对谈
  - 门派议事 / 同伴围聊
  - 江湖传闻 / 留言或通知面
  - 江湖事件 / 门派事件 / 遭遇反馈

## 11. Microsoft Agent Framework 的定位

`Microsoft Agent Framework` 当前适合作为：

- `Narrative Orchestration Engine`

不适合作为：

- 整个双核框架本身
- 宿主治理核心
- 宿主 authoritative 写回层

基于微软官方当前文档，AFW 适合承担：

- agents
- sessions
- context providers
- memory provider integration
- workflows
- checkpointing
- tools / function registry
- middleware
- telemetry
- HITL

相关官方来源：

- [Microsoft Agent Framework Overview](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview)
- [Conversations & Memory overview](https://learn.microsoft.com/en-us/agent-framework/agents/conversations/)
- [Workflows overview](https://learn.microsoft.com/en-us/agent-framework/workflows/overview/)
- [Hosting / integrations overview](https://learn.microsoft.com/en-us/agent-framework/user-guide/hosting/)

同时必须注意：

- `Microsoft Agent Framework` 当前仍处于 `public preview`
- 因此不能把整套商业产品的真相源、长期记忆真相源和宿主执行真相源全部押在 AFW 抽象上

### 11.1 中用法固定结论

本框架对 AFW 的推荐落位固定为：

- `中用法`

含义如下：

- AFW 默认位于服务端 `Narrative Base Pack Core` 内，作为叙事编排运行时底座
- AFW 可以深度承载 agent/session/workflow/checkpoint/middleware/telemetry
- AFW 可以承载 `group_chat`、`information_propagation`、`active_world` 等 channel 的运行流程
- AFW 可以替换原参考 mod 的 orchestrator 承载方式，但不能改写其已证明有效的链路结构
- AFW 不管理 game-local UI binding
- AFW 不管理 game-local execution mapping
- AFW 不拥有最终宿主 authoritative apply 权
- AFW session / checkpoint / workflow state 一律视为 runtime-derived orchestration state，不得冒充产品真相源、长期记忆真相源或宿主执行真相源

### 11.1.1 M1 的 AFW 风险收敛规则

由于 `Microsoft Agent Framework` 当前仍处于 `public preview`，因此 `M1` 必须额外满足以下约束：

- AFW 默认位于服务端 `Narrative Base Pack Core` 的可替换叙事编排适配层之后
- AFW session / checkpoint / memory provider 数据在 `M1` 一律视为 `derived orchestration state`，不得视为产品真相源
- `M1` 不得把“AFW workflow 可恢复”写成产品承诺
- `M1` 不得把“AFW checkpoint resume”写成宿主恢复承诺
- `M1` 不得把长期记忆真相源直接绑定为 AFW memory provider 私有状态
- `M2` 之后才能在已有稳定性证据的前提下，逐步扩大对 AFW workflow / checkpoint 的依赖面

### 11.2 AFW 负责什么

- `AgentSession`
- `AIContextProvider`
- memory provider integration
- tool registry
- workflow orchestration
- checkpoint management
- middleware / HITL / telemetry

### 11.3 AFW 不负责什么

- `Host Governance Core`
- `Game Integration Profile` 真相源
- capability waiver 真相源
- long-term memory truth source
- product billing / entitlement truth source
- game authoritative execution
- 最终产品 contract
- per-game UI binding

因此应写死：

- `AFW = Narrative Orchestration Engine`
- `This Framework = Product + Contract + Deterministic Execution Framework`

## 12. 双核对齐面

如果 `Host Governance Core` 和 `Narrative Base Pack Core` 只是并列摆放，系统最后一定会裂开。

因此必须通过以下 `6` 个一等对象对齐。

### 12.0 Authority Matrix

在展开 `6` 个一等对象之前，必须先固定 authority matrix。

最少固定如下：

| 对象 | 单一 owner | 持久化真相源 | policy input / approval input | 非 owner 禁止事项 |
| --- | --- | --- | --- | --- |
| access / entitlement enforcement decision | `Cloud Control` | server-side entitlement store | `sku-entitlement-claim-matrix.md` + product contract | `Launcher`、`Runtime`、`Mod` 不得自判授权通过 |
| sellability / listing / entitlement policy | `SKU Entitlement & Claim Matrix` | product-owned sku claim artifact | product contract + waiver register | `Cloud Control` 只按政策执行，不得自创 entitlement policy |
| launch readiness policy | `Game Integration Profile` | approved profile artifact | current phase boundary | `Launcher`、`Runtime` 不得改写 readiness policy |
| current launch readiness result | `Launcher Supervisor` | launcher-managed readiness state | game integration profile | `Runtime`、`Mod` 不得单独宣布“可启动” |
| runtime state transition | `Local Runtime` | runtime state store + trace | current phase boundary | AFW session / checkpoint 不得冒充 runtime truth |
| narrative orchestration decision | `Narrative Base Pack Core` | server-side orchestration state + trace | product contract + approved profile + narrative policy | 客户端、AFW 外层包装与本地 Mod 不得私自改写编排结论 |
| capability support declaration | `Capability Claim Matrix` | product-owned claim artifact | product contract + waiver register | 工程实现与 game profile 不得自判“已支持” |
| current capability operational status | `Launcher Supervisor` | launcher-managed operational status artifact | capability claim matrix + runtime health facts | `Local Runtime` 只能上报 health fact，不得自判玩家可见支持状态 |
| waiver state | `Waiver Register` | waiver register artifact | approval authority | 工程团队不得口头续期或口头豁免 |
| authoritative host writeback | `Deterministic Game Execution Layer` | host writeback log + audit trace | deterministic command/event contract | AFW / LLM / prompt chain 不得直接写回宿主 |
| cost attribution for `platform_hosted` / `mixed` | `Cloud Control` | cost ledger | claim matrix + billing policy | 任意单侧日志不得独自形成商业归因结论 |
| cost usage fact for `user_byok` | `Cloud Control` | server-side usage fact log + billing trace | billing policy + credential handling policy | 本地不得以本地估算账本替代服务端实际调用事实 |
| first visible host / failure surface / recovery entry evidence | `Game Integration Profile` | approved profile artifact | capability visibility contract | 任意实现层不得私自更换用户可见宿主 |
| final `RC / GA` ship-gate decision | `Release Governance Owner` | release decision record + evidence review index | current phase boundary + product contract | evidence hub、实现 owner、artifact owner 均不得单独放行 |

### 12.1 Game Integration Profile

它不只是宿主技术接入文档，还必须是叙事落地证据与绑定契约。

必须按“当前阶段核心字段 + 更重能力 annex”拆分，而不是把未来阶段字段一次性压进 `M1` ship gate。

`M1 core profile` 至少包含：

- loader / prerequisite rules
- install / mod / save / log path
- runtime health checks
- dialogue presentation binding
- memory follow-up evidence rule
- tangible interaction sourcing rule
- gift trigger mapping
- gift template sourcing rule
- gift delivery flow
- item-instance text override rule
- game-local custom-item exception rule
- degraded narrative modes
- rollback target
- recovery entry

仅当对应能力进入当前阶段强制范围、进入当前 title 的 claim scope，或该能力被批准为 experiment / preview 时，才要求补充 `M2+ annex`：

- group chat presentation binding
- propagation channel binding
- world transfer application rule
- world event surface binding

并且必须写死：

- `M1` ship gate 只强制检查 `M1 core profile`
- `M2+ annex` 只有在能力进入当前阶段、进入当前 title 的 claim scope、或被批准为 experiment / preview 时才成为强制审查对象
- 对于未进入当前阶段的重能力，profile 允许把 annex 标成 `not-in-phase` 或 `experiment-only`
- `not-in-phase` 仅表示当前 phase schema 不要求该 annex，不得替代 sellability waiver
- 不得为了满足总纲文档而给 `M1` title 强塞未来阶段字段

### 12.2 Capability Visibility Contract

每个游戏都必须按当前阶段回答“哪些能力需要可见证明”。

对 `M1 core profile`，至少要显式回答：

- 玩家在哪里感知对话
- 玩家在哪里感知记忆
- 玩家在哪里感知主动可确定性实体交互

仅当能力进入当前阶段、进入当前 game claim scope、或被批准为外显试点时，才要求继续回答：

- 玩家在哪里感知群聊
- 玩家在哪里感知传播
- 玩家在哪里感知主动世界
- 哪些证据支持该能力的可见实现
- 哪些 surfaces 被绑定为该能力的玩家可见宿主
- 哪些 runtime degraded modes 可能出现

这里必须再写死一条：

- `Game Integration Profile` 提供的是 evidence 与 binding
- 它不是 capability support declaration 的最终 owner
- capability 是否可对外宣称为 `supported / variant / degraded / waived`，只能由产品侧 claim artifact 决定

并且必须显式声明以下字段：

- `first visible host`
- `confirmation surface`
- `persistence / follow-up surface`
- `success state`
- `delayed state`
- `failure copy`
- `degraded copy`
- `recovery entry`
- `minimum recurrence rule`
- `required evidence`

其中：

- `M1 core profile` 必填字段只覆盖当前阶段核心能力
- `M2+ annex` 才承载群聊、传播、主动世界的同构字段
- 若某能力状态为 `waived`，则该能力必须提供 claim state、披露 copy、recovery/closure link，而不是伪造完整可见证据
- 若 annex 仅为 `not-in-phase`，则它只表示当前 phase 不要求该能力进入 ship gate，不等于对外 sellable exception

同时，`Capability Visibility Contract` 必须包含一张强制的 `Portable Semantics Checklist`。

所有能力共享的最低证据维度如下：

- `trigger visibility`
- `actor attribution`
- `turn continuity`
- `world-state delta visibility`
- `cross-turn memory evidence`
- `relation-impact evidence`
- `traceable cause/result`

并且：

- `remote_communication` 固定视为 `information_propagation` 的必审实现维度之一
- 若宿主支持延迟送达、异地通信、邮件、传音、留言、通讯录等形态，则 `Capability Visibility Contract` 必须显式声明其可见 surface、失败 copy 与持久化 surface

### 12.2.1 Capability-Specific Portable Semantics Minimums

为了避免不同游戏都声称“已支持”，但实际行为语义完全不同，每项基础包能力还必须满足下列最小 portable contract。

| capability | minimum trigger rule | minimum actor roles | minimum persistence / continuity | minimum deterministic outcome + payload fields | semantic completion |
| --- | --- | --- | --- | --- | --- |
| dialogue | 玩家显式发起，或经 policy 批准的 NPC 主动发起 | `initiatorActorId`、`responderActorId / responderSet` | 至少保留 1 个可追溯 turn，并能在后续 turn 继续引用 | `dialogue_emitted`，至少带 `turnId`、`speakerId`、`listenerSet`、`surfaceId` | 玩家在宿主可见面收到带 actor attribution 的发言结果 |
| memory | 只能由已发生 turn / relation / world fact 触发，不得凭空写入长期记忆 | `subjectActorId`、`memoryOwnerId` | 至少支持 cross-turn recall 或 summary carry-forward | `memory_recorded` 必须带 `memoryKey`、`sourceTurnId`、`timeBucket`、`memoryOwnerId`；`memory_recalled` 必须带 `memoryKey`、`sourceTurnId`、`timeBucket`、`recallSurfaceId` | `memory_recorded` 以持久化完成为 committed；`memory_recalled` 以后续可见 turn 能引用该记忆为 committed |
| social transaction / commitment | 必须由可追溯提议触发，不能直接跳到完成态 | `offererId`、`counterpartyId`、可选 `brokerId` | 必须能表达 `offer / accept / reject / counter / obligation outstanding / fulfillment` 中的当前状态 | `transaction_state_committed`，至少带 `transactionId`、`offererId`、`counterpartyId`、可选 `brokerId`、`state`、`resourceOrServiceKey`、`targetScope` | 玩家能在宿主表面确认承诺状态被建立、拒绝或履约 |
| group_chat | 至少 3 方参与，且发言顺序可追溯 | `participantSet`、`speakerId`、`audienceSet` | 必须保留 sequence continuity，不得退化成无法区分说话人的散乱文本 | `group_turn_committed`，至少带 `groupTurnId`、`sequenceIndex`、`speakerId`、`surfaceId` | 玩家能看见多方连续发言及 actor attribution |
| information_propagation | 必须存在 source fact / source actor / propagation path | `sourceActorId`、`recipientScope`、`channelType` | 至少支持本轮传播结果或延迟送达状态之一 | `propagation_committed`，至少带 `propagationId`、`sourceFactId / sourceEventId`、`channelType`、`deliveryMode`、`deliveryState`、`targetScope` | 玩家能确认信息已扩散、延迟或失败，而不是只在后台发生 |
| active_world | 只能由 policy 批准的候选链路触发，不能绕过 deterministic gate | `initiatorKind`、`affectedScope`、可选 `responsibleActorId` | 必须能表达 event pending / applied / delayed / skipped / rolled back / failed 中之一 | `world_event_committed`，至少带 `eventId`、`eventType`、`eventState`、`affectedScope`、`rollbackHandle`、`skipOrFailureReason` | 玩家能在宿主表面感知世界状态变化或明确跳过原因 |

若某实现缺失表中的任一最小语义条件，则不得仅凭“有类似效果”宣称 capability 已支持。

### 12.2.2 Phase-Scoped Profile Rule

同一个 `Game Integration Profile` 允许包含多个 phase-scoped section，但 ship gate 只检查当前阶段要求的 section。

固定规则如下：

- `M1` 只强制 `dialogue + memory + social transaction / commitment`
- `group_chat`、`information_propagation`、`active_world` 在 `M1` 默认进入 `M2+ annex` 或 `experiment annex`
- `not-in-phase` 只允许出现在未出货 capability annex 中，不得作为已销售 title / SKU 的缺项豁免状态
- 若某 `M1` title 想提前对外声明其中任一重能力，则必须同时满足：
  - 该能力在 claim matrix 中被标为可对外声明状态
  - 对应 annex 已存在并完成审查
  - 不得反向扩大所有 `M1` title 的 ship-gate 负担

### 12.3 Deterministic Command / Event Contract

`Narrative Base Pack` 与宿主之间只允许通过受限 contract 通信。

例如：

- `dialogue_emitted`
- `group_turn_committed`
- `propagation_committed`
- `world_event_committed`
- `recovery_instruction`

同时必须相容于当前 phase boundary。

对于 `M1`：

- 上述语义 contract 若需要落地，必须编译到当前阶段允许的命令集合中
- 在当前边界下，默认编译到 `render_command` 与 `transactional_command`
- 不得因为本文档而为 `M1` 引入新的共享 command classes
- `propagation_command` 与 `world_event_apply_request` 仅作为 `M2+` 候选 profile 语义，不得作为当前 `M1` 共享命令硬门槛
- `M1` 必需语义的 outcome-to-command mapping、payload minima 与 completion condition 必须由 runtime contract 明确写死

### 12.4 Narrative Degradation Contract

必须明确：

- 文本失败是否还能保留动作
- 动作失败是否还能保留文本
- 群聊失败是否退化为单人连续发言
- 传播失败是否只保留本地结果
- 主动世界失败是否只跳过本轮而不拖死其他基础包能力

还必须明确一条 claim mapping 规则：

- 若某项能力长期失去 `writability`、`actor-to-actor propagation`、`cross-turn persistence` 或 `world-effect materialization`
- 则它不再属于可长期维持的 `degraded variant`
- 而属于 `missing capability`，必须升级成 waiver 与商业披露

还必须存在可审计的 degradation state 字段：

- `degradationWindowId`
- `degradationStartedAt`
- `escalationDeadlineAt`
- `escalatedAt`
- `recoveredAt`
- `claimStateRef`
- `waiverLineageId`
- `recoveryEvidenceRef`

### 12.5 Trace / Audit Contract

至少统一：

- `traceId`
- `requestId`
- `launchSessionId`
- `skuId`
- `gameId`
- `billingSource`
- `channelType`
- `capability`
- `narrativeTurnId`
- `executionResult`
- `degradedMode`
- `traceGroupId`
- `claimStateRef`
- `claimStateAtEvent`
- `degradationWindowId`
- `degradationStartedAt`
- `escalationDeadlineAt`
- `escalatedAt`
- `recoveredAt`
- `waiverId`
- `waiverLineageId`
- `evidenceReviewRef`
- `recoveryEvidenceRef`

### 12.6 Exception Governance Contract

必须存在正式对象，而不是口头约定。

至少记录：

- waiver request
- waiver owner
- waiver reason
- waiver expiry
- player-visible wording
- fallback semantics
- recovery impact
- approval authority

并且必须写死：

- waiver owner 与 approval authority 必须是不同责任人
- 提交 waiver 的责任人不得审批自己的 waiver
- 缺项 waiver 只允许批准“缺失 + 披露 + 恢复计划”，不允许批准“移入 premium entitlement”

## 13. 产品契约

### 13.1 Narrative Base Pack

`Narrative Base Pack` 默认必须包含：

- 对话
- 记忆
- 主动可确定性实体交互
- 群聊 / 多人连续发言
- 信息传播
- 主动世界 / 世界事件推进

其中“主动可确定性实体交互”必须进一步收敛为：

- `social transaction / commitment`

允许的宿主化实现至少包括：

- 送礼
- 资源转移
- 制作 / 委托产出
- 服务履约
- 任务交付
- 据点 / 阵营贡献

但最低语义状态至少要支持：

- `offer`
- `accept`
- `reject`
- `counter`
- `obligation outstanding`
- `fulfillment`

对于 `M1` 的物品落地，还必须补一条宿主实现红线：

- `M1` 的 NPC 创造物品默认收敛为“基于现有模板的受限实例化”，而不是开放式新物品系统
- AI 默认只负责生成该次事件的 `名称文案` 与 `描述文案`
- 不新增美术素材、不新增复杂数值体系、不引入平台级物品注册表
- 是否真正写回实例字段，由 `per-game launch profile` 按宿主能力决定
- 若宿主无法稳定支持实例级覆写，则至少必须在对话框、邮件、气泡、奖励提示或 tooltip 之一向玩家展示这次物品的 AI 名称与描述

并且必须允许少量 game-local 受控例外：

- 若某 title 在产品上明确要求“背包中出现新的受限条目”，可在对应 `game-integration-profile` 中批准极少量 game-local custom item implementation
- 该例外只能按 title 单独审批，不能自动升级成平台默认依赖
- `M1` 不允许把这条例外扩张成开放式全物品生成、完整内容包生态或平台级物品系统

`M1` 的礼物触发来源还必须进一步固定为以下最小集合：

- `relationship_trigger`
- `time_trigger`
- `event_trigger`
- `birthday_trigger`
- `random_trigger`
- `anniversary_trigger`
- `festival_trigger`

这些 trigger source 在产品语义上必须统一存在，但各游戏的具体映射仍由 `per-game launch profile` 负责，不得反向上拉成平台级世界模型。

`M1` 的名称与描述规则也必须写死：

- 产品语义上，名称与描述属于本次礼物事件，不得被设计成全局永久改模板
- 技术实现上，允许按游戏能力分层：能做实例级就做实例级，做不到实例级就做展示层覆写
- 名称与描述应体现 NPC 身份、关系、当前时机与送礼语境
- 物品真实功能、数值、素材与基础类别仍跟模板走
- 文案不得把物品描述成与真实用途完全不符的东西

这些能力默认都属于基础包，不得被工程侧自行裁剪成高级包。

### 13.2 可见性原则

对每个支持游戏，默认都必须满足：

- 基础包能力对玩家 `可见`

这里的可见，优先指：

- `语义一致`
- `结果体验一致`

不要求：

- 交互皮肤一致
- UI 组件一致
- 宿主原生实现路径一致

但必须满足：

- 玩家能在明确宿主表面上看到该能力被触发
- 玩家能在明确宿主表面上看到该能力产生结果
- 玩家能在失败或降级时看到清晰 copy 与 recovery entry
- 玩家能通过后续持久反馈确认它不是一次性假象

仅有后台 trace、隐藏 mod 效果、理论可触发路径，不得算作“可见能力”。

同时必须满足：

- `Capability Visibility Contract` 中的全部字段不能只停留在泛化矩阵
- 必须落成可审查的 per-game evidence artifact

### 13.3 缺项治理原则

若某一游戏确实无法在当前阶段实现某项基础包能力：

- 不得由工程团队自行决定裁掉
- 必须提交正式 waiver
- 必须由与 waiver owner 不同的 approval authority 明确批准
- 必须附带玩家可见说明、替代实现与恢复路径

同时必须写死以下商业规则：

- 任何游戏或 SKU 若缺少任一基础包能力，不得使用任何 pack-level shorthand 宣称自己“包含 / 默认附带 / 完整支持 `Narrative Base Pack`”
- 若仍然选择发售，只能按 claim matrix 中批准的精确状态对外披露，不得自行写营销短语
- SKU 是否允许继续销售，只能由 `SKU Entitlement & Claim Matrix` 的 `sellability state` 决定
- waiver 必须精确到 `skuId + gameId + capability + billingSource`
- waiver 默认不得自动续期
- waiver 必须带 `expiry phase`、`expiry date`、`reapproval cadence`、`closure criteria`、`expired blocking condition`
- waiver owner 与 approval authority 必须分离，且不得由同一责任人兼任
- 只有 `SKU Entitlement & Claim Matrix` 明确标为 `sellable_with_disclosure` 且其 `support claim` 为 `phase_waived` 或 `partial_preview` 的 SKU，才允许在有披露的前提下继续销售
- `phase_waived` 只表示“带缺项出售并持续披露”，不表示“基础包已由 premium 替代”
- `not-in-phase` 只允许用于未进入当前阶段的 annex schema，不得用于已销售 SKU 的缺项豁免

另外必须写死：

- 基础包缺项的商业声明按 `skuId + gameId + capability + billingSource` 治理
- 任何 bundle / edition / region / billingSource 差异，不得绕开 claim artifact 自行漂移

### 13.4 Premium Non-Substitution Rule

必须再加一条硬规则：

- `Narrative Base Pack` 的任何基础能力，不得依赖 `Premium Media Pack` entitlement 才能成立
- 不得把基础包的默认行为改成 hosted-only premium 路径再对玩家收费
- 若某项基础包能力需要 hosted infrastructure，也必须仍然包含在基础 entitlement 中
- waiver 只允许“缺失能力的持续披露与恢复计划”，不允许把该能力改名后迁入 premium entitlement
- 不存在任何“waiver + disclosure 就能把基础能力升级成 premium upsell”的例外

### 13.4.1 Billing-Source Rule

基础叙事能力与 premium 媒体能力的计费来源必须进一步写死：

- `Narrative Base Pack` 默认采用统一的服务端编排链路
- 基础叙事能力允许 `user_byok` 与 `platform_hosted` 两种 `billingSource`
- `user_byok` 的含义是：用户把自己的模型 key 提交到服务端，由服务端临时代调用基础叙事链路；这不改变服务端持有编排真相源的原则
- `user_byok` 默认不得把完整 prompt pack、完整编排规则或长期记忆真相源下放到客户端
- `platform_hosted` 与 `user_byok` 在基础叙事能力上应尽量保持一致的 capability semantics，而不是裂成两套产品
- `Premium Media Pack` 中的 `AI voice`、`AI image`、`AI video` 必须统一走 `platform_hosted`
- 不允许把 premium 媒体能力接到 `user_byok` 路径上，以免削弱成本治理、供应商治理与风控边界

## 14. 分期设计

双核框架下的分期，不再是“只描述宿主治理分期”的单线模型。

每一期都必须同时回答：

- `Host maturity`
- `Narrative maturity`

### 14.1 M1：产品化首发期

`M1` 的核心不是“先做最小 AI”，而是：

- 先交付一个可卖、可启、可恢复、可支持的 Windows 首发产品
- 同时把 `Narrative Base Pack` 的完整产品定义正式立起来

#### 14.1.1 M1 Host 目标

必须完成：

- `Launcher`
- `Local Runtime`
- `Cloud Control`
- hosted narrative orchestration service
- per-game launch profile
- per-game diagnostics / recovery / quarantine
- fail-closed + soft degradation
- trace / audit 主链
- 基础授权与运行期控制

#### 14.1.2 M1 Narrative 目标

`M1` 不是把基础包缩成三项，而是：

- 保持完整基础包定义不变
- 明确所有基础包能力都属于产品 contract
- 允许对最重能力采用 `phase-bound exception governance`

因此：

- 对话、记忆、主动可确定性实体交互应默认进入当前 `M1` 首发主链
- 群聊、传播、主动世界仍然属于长期 `Narrative Base Pack` 定义
- 但它们在当前 `M1` 是否进入 exit criteria，必须服从 `docs/superpowers/governance/current-phase-boundary.md`
- 在当前边界下，这三项可作为 `M1` `non-exit experiment` 或 `M2+ default expectation`
- 本文档不得单方面扩大当前 `M1` 的强制范围
- `M1` 的基础叙事主链默认走服务端编排 + 本地确定性执行，不再以“本地编排为默认前提”
- 服务器不可用时，基础叙事能力应明确标记为当前不可用，而不是假装存在本地完整兜底
- `group_chat` 若进入 experiment scope，默认应按参考 mod 的 `queue -> speaker selection -> per-speaker generation -> parse -> writeback -> dispatch` 链路复现，并允许由 AFW 承载其运行时
- `M1` 的主动可确定性实体交互默认包含 NPC 礼物 / 物品交付落地链：触发 -> 对话前后文 -> 模板实例化 -> game-local delivery flow -> 玩家可见文本覆写
- `M1` 默认坚持“模板实例化优先”；只有在 title 明确要求背包出现少量新条目时，才允许进入 game-local custom item 受控例外
- 当前 `M1` 规划中的第三款首发 title 改为 `太吾绘卷`，不再沿用 `鬼谷八荒` 的旧首发假设
- `太吾绘卷` 在 `M1` 的宿主级 gift delivery flow、可见 surface 与 active-world surface 必须以 `game-integration-profile` 中的证据化映射为准，不得直接继承 `鬼谷八荒` 的奇遇 / 直接入包链路
- 任何 title 若缺少这三项中的任一项，仍不得宣称自己 `完整支持 Narrative Base Pack`，也不得使用任何等价 pack-level shorthand
- waiver 必须显式记录：
  - `gameId`
  - `capability`
  - 哪款游戏缺哪项
  - 为什么缺
  - 玩家将看到什么替代表现
  - 计划在哪一阶段收回豁免
  - 对应 `skuId + gameId + capability + billingSource`

#### 14.1.3 M1 成功标准

`Host Governance Core`

- 可安装
- 可启动
- 可授权
- 可诊断
- 可恢复
- 可隔离局部故障

`Narrative Base Pack`

- 基础包完整定义已固定
- 当前 `M1` 主链路已在首发组合中建立统一产品语义
- 任何缺项都被正式治理，而不是隐性缺失
- 任何缺项都不能继续宣称为“完整基础包支持”

### 14.2 M2：复用扩张期

`M2` 的核心不是单纯“协议 profile 化”，而是：

- 在不破坏产品一致性的前提下，让 `Narrative Base Pack` 真正成为跨游戏复用内核
- 逐步回收 `M1` 的阶段豁免
- 扩展支持的游戏数量与类型
- 把服务端叙事编排从“单 title 可用”推进到“跨 title 可复用、可治理、可回放”

#### 14.2.1 M2 Host 目标

- 更多 `Game Integration Profile`
- 更成熟的 capability / version / profile governance
- 更强的成本归因与支持数据
- 受控引入有限托管能力入口
- hosted narrative orchestration service 从首发专用实现升级为多 title 共用能力
- `user_byok` 与 `platform_hosted` 的 billing / trace / support 路径完成统一治理
- 服务端 recent-history store、长期记忆存储、replay envelope 和 revoke / rotate runbook 完成产品化

#### 14.2.2 M2 Narrative 目标

- 群聊、传播、主动世界从局部试点走向受控普及
- 共享 typed narrative contracts 进一步稳定
- AFW workflow / checkpoint / context providers 形成稳定套路
- 更多 narrative schema 从 experimental 升到 stable
- `M1` 临时 waiver 必须在 `M2` 被逐步收回，而不是转成常态化例外
- `group_chat`、`information_propagation`、`active_world` 的服务端编排链默认沿用参考 mod 已验证的步骤顺序，只在承载层切换到 AFW
- 跨游戏 title 在 canonical snapshot、canonical recent raw history、长期记忆真相源和 deterministic outcome mapping 上形成稳定共识
- `user_byok` 与 `platform_hosted` 在基础叙事能力上完成 capability parity 验证，禁止长期演化成两套语义不同的产品
- 服务端不可用时的 player-visible copy、claim-state 映射和恢复入口在多游戏 title 上完成统一
- 受控 custom-item exception 的宿主规则、模板来源策略、实例文本覆写证据在多 title 上沉淀为可审查 annex，而不是散落在实现细节里

### 14.3 M3：平台沉淀期

`M3` 才是把复用事实正式沉淀为平台内核的阶段。

#### 14.3.1 M3 Host 目标

- 完整 capability host / hosted capability
- 完整成本治理与对账
- 更成熟的平台控制面与平台日志治理
- hosted narrative orchestration、hosted capability、Cloud Control 与 release governance 形成稳定平台控制面
- 服务端 credential handling、BYOK 临时凭据治理、租户隔离、rotate / revoke 自动化形成稳定运营能力

#### 14.3.2 M3 Narrative 目标

- 已被多游戏反复验证的 snapshot / memory / intent / world-event contracts 升格为稳定平台内核
- stable profiles 正式形成
- 仍然强宿主依赖的部分继续留在 per-game layer，不强吸进平台
- 服务端 narrative orchestration truth source、canonical recent-history store 与 long-term memory truth source 正式沉淀为平台级共享能力
- AFW 在平台中的角色固定为“可替换的编排运行时底座”，而不是产品 contract 或宿主执行真相源
- 参考 mod 中已证明有效的链路结构被沉淀为 repo-governed stable orchestration profiles，而不是继续散落在单项目实现里
- 物品 / 内容交付能力只把跨游戏稳定不变的 contract 留在平台；真正的 custom item registration、内容包写法与背包展示差异继续留在 per-game layer

### 14.4 分期总原则

分期不再是：

- 先平台后 AI
- 或先 AI 后平台

而是：

- 双核共同成熟，但成熟速度不同
- 且始终受同一份产品 contract 约束

### 14.5 分期证据要求

任何阶段结论都不得只靠宣称成立。

至少必须附带下表中的证据：

| 阶段 | 必要证据 |
| --- | --- |
| `M1` | 当前 phase-boundary 文档、当前 Product Contract、每个 launch title 的 `M1 core profile`、任何进入当前 title claim scope 或被批准为 experiment / preview 的 annex、当前 `deterministic-command-event-contract`、当前 `narrative-degradation-contract`、当前 `trace-audit-contract`、当前 `Evidence Review Index`、当前 Waiver Register、gate-time runtime degradation / recovery evidence、启动与恢复手验、失败路径手验、诊断包样例、review sign-off、当前 AFW Boundary Note |
| `M2` | 新增 game profiles、waiver 收回证据、cross-game capability matrix、shared contract 稳定性证据、AFW 依赖扩大前后的稳定性证据、hosted narrative orchestration 多 title 复用证据、`user_byok` / `platform_hosted` parity evidence、recent-history / long-memory / replay 存储治理证据 |
| `M3` | 多游戏复用事实、stable profile 升格证据、平台治理闭环证据、托管能力与成本治理证据、服务端 orchestration truth source 平台化证据、BYOK 临时凭据治理与 rotate / revoke 自动化证据 |

若没有证据链接，则不得宣称阶段目标已完成。

## 15. 文档体系建议

为了避免以后团队偷换定义，以下文档体系改为强制要求，而不是建议：

### 15.1 上位总纲

即本文档。

职责：

- 定义双核框架
- 定义产品契约
- 定义锚点策略
- 定义 AFW 边界
- 定义分期原则

### 15.2 产品 Contract 文档

路径固定为：

- `docs/superpowers/contracts/product/narrative-base-pack-contract.md`
- `docs/superpowers/contracts/product/premium-media-pack-contract.md`
- `docs/superpowers/contracts/product/capability-claim-matrix.md`
- `docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md`

owner：

- 产品 owner

必须字段：

- `Narrative Base Pack`
- `Premium Media Pack`
- capability visibility
- waiver rules
- capability support declaration
- `skuId + gameId + capability + billingSource` claim mapping
- `gameId + capability + billingSource` support declaration grain

更新触发：

- 基础包定义变化
- premium 边界变化
- 可见性规则变化

缺失或过期时：

- 禁止进入实现批准
- 禁止进入 release candidate
- 禁止对外宣称 capability support

### 15.3 项目化分期稿

当前 `M1 / M2 / M3` 项目落地稿保留，并继续承担当前阶段硬边界职责。

冲突规则固定为：

- `phase-boundary docs > this framework doc > obsolete/reference docs`

也就是说：

- 本文档不能单方面覆盖当前阶段红线
- 当前阶段要不要引入某项重能力，必须仍由当前生效的 phase-boundary 文档裁决

当前 phase-boundary artifact 固定为：

- `docs/superpowers/governance/current-phase-boundary.md`

并且必须写死：

- `phase exit criteria` 必须直接写在当前生效的 phase-boundary artifact 中
- 实现计划、项目化分期稿、review index 只能提供证据与链接，不能替代或改写当前阶段 exit gate

必须字段：

- `owner`
- `approver`
- `effective date`
- `current phase`
- `hard redlines`
- `phase exit criteria`
- `AFW-specific redlines`
- `phase-required profile schema`
- `waiver gate`
- `superseded revisions`

### 15.4 Per-Game Game Integration Profiles

路径固定为：

- `docs/superpowers/profiles/games/<gameId>/game-integration-profile.md`

owner：

- game integration owner

每款游戏一份，专门描述 game-local 落地差异。

必须字段按 phase 拆分：

`M1 core profile` 必须字段：

- first visible host
- failure surface
- recovery entry
- capability visibility evidence
- portable semantics checklist evidence
- deterministic command / event mapping
- gift trigger mapping
- gift template sourcing rule
- gift delivery flow
- item-instance text override rule
- game-local custom-item exception rule
- degraded modes
- trace / audit mapping

`M2+ annex` 仅在能力进入当前阶段、进入当前 title 的 claim scope、或被批准为 experiment / preview 时才要求：

- group chat binding
- propagation binding
- active world binding
- remote communication surface binding
- capability-specific annex evidence

更新触发：

- 游戏能力变化
- UI 可见面变化
- recovery / degraded mode 变化

### 15.5 Waiver Register

路径固定为：

- `docs/superpowers/governance/waivers/narrative-base-pack-waiver-register.md`

owner：

- product operations owner

approval authority：

- founder or delegated commercial governance approver

记录所有基础包例外与到期阶段。

必须字段：

- `skuId`
- `gameId`
- `capability`
- `billingSource`
- `reason`
- `player-visible disclosure`
- `fallback semantics`
- `owner`
- `approval authority`
- `approval date`
- `expiry phase`
- `expiry date`
- `reapproval cadence`
- `closure criteria`
- `expired blocking condition`
- `waiverLifecycleState`
- `waiverId`
- `waiverLineageId`
- `supersedesWaiverId`
- `recovery impact`
- `claimStateRef`
- `evidenceReviewRef`
- `traceGroupId`
- `degradationStartedAt`

仅在 waiver 关闭时必填：

- `recoveredAt`
- `recoveryEvidenceRef`

更新触发：

- 新 waiver
- waiver 续期
- waiver 关闭

同一未恢复 incident 的续批必须保持同一 `waiverLineageId + traceGroupId + degradationStartedAt`，并通过 `supersedesWaiverId` 串起审批链

缺失、过期或未链接到具体 `skuId + gameId + capability + billingSource` 时：

- 禁止该 title 以完整基础包支持状态进入销售或升级宣称
- 禁止任何缺项 SKU 使用 pack-level shorthand 销售文案
- 未链接 `waiverId`、`waiverLineageId`、`claimStateRef`、`evidenceReviewRef`、`traceGroupId` 或 `degradationStartedAt` 时，同样阻断 ship gate
- 若 waiver 已关闭但未链接 `recoveredAt` 或 `recoveryEvidenceRef`，同样阻断 ship gate

### 15.6 AFW Boundary Note

路径固定为：

- `docs/superpowers/governance/afw-boundary-note.md`

owner：

- runtime architecture owner
- co-approval by product / host-governance authority

短文档，写死：

- AFW 负责什么
- AFW 不负责什么
- 哪些工作流可以进 AFW
- 哪些绝不能进 AFW

更新触发：

- AFW 边界调整
- preview 风险策略调整
- shared narrative orchestration 范围变化

优先级固定为：

- `current-phase-boundary > this framework doc > AFW Boundary Note`

`AFW Boundary Note` 只能操作化既有批准边界，不能自行扩权。

### 15.6.1 Additional Mandatory Governance Artifacts

还必须存在以下治理工件：

- `docs/superpowers/contracts/runtime/deterministic-command-event-contract.md`
- `docs/superpowers/contracts/runtime/narrative-degradation-contract.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`
- `docs/superpowers/governance/evidence-review-index.md`

每份都必须定义：

- owner
- required fields
- update trigger
- review rule
- ship-gate linkage
- review freshness rule

### 15.7 Ship Gate

以下任一缺失时，不得进入实现批准、release candidate 或 GA：

- 当前 Product Contract
- 当前 `docs/superpowers/governance/current-phase-boundary.md`
- 当前 Capability Claim Matrix
- 当前 SKU Entitlement & Claim Matrix
- 当前每游戏 phase-scoped Game Integration Profile
- 当前 Waiver Register
- 当前 AFW Boundary Note
- 当前 deterministic command / event contract
- 当前 narrative degradation contract
- 当前 trace / audit contract
- 当前阶段证据链接

最终放行 authority：

- `release governance owner`

## 16. 最终结论

这套通用框架的核心结论如下：

1. `All Game In AI` 的长期方向应被定义为跨游戏 AI Mod 产品线，而不是单项目架构试验。
2. 顶层必须采用 `Host Governance Core + Narrative Base Pack Core + Deterministic Game Execution Layer` 的双核半分层结构。
3. `OpenAIWorld` 应被当作成熟锚点，但只继承其工程模式与 UX 语义，不照搬宿主外壳。
4. `Microsoft Agent Framework` 适合作为 `Narrative Orchestration Engine`，不适合作为整个产品框架或宿主 authoritative 写回框架。
5. `Narrative Base Pack` 的完整编排真相源默认在服务端，客户端只承担 canonical input 上传、本地展示与确定性执行。
6. `Narrative Base Pack` 默认必须包含：对话、记忆、主动可确定性实体交互、群聊、信息传播、主动世界。
7. 基础叙事能力允许 `user_byok` 与 `platform_hosted` 双 billing source 共用同一套服务端编排链路，但 `AI voice`、`AI image`、`AI video` 必须统一走 `platform_hosted`。
8. `M1 / M2 / M3` 必须始终同时描述宿主成熟度与叙事成熟度，不能再只描述宿主治理分期。
9. 任何基础包缺项都必须进入正式例外治理，不得由工程侧自行裁剪。

如果后续讨论再次出现以下说法：

- “这个基础包能力先不算基础包了”
- “这个能力某款游戏做不到，工程上先默认不做”
- “AFW 既然能编排，不如把宿主执行也一起塞进去”
- “某个游戏的 UI 长这样，所以通用框架 UI 也该长这样”

默认都应回到本文档核对：

- 是否破坏了双核边界
- 是否破坏了产品 contract
- 是否误把锚点当模板
- 是否误把编排框架当成产品框架
