# Superpowers 统一平台控制面设计

## 1. 文档定位

本文档用于把 `All Game In AI / superpowers` 的“统一平台”真正落到可运营、可控制、可计费、可回溯的服务器前端与配套控制面上。

它不替代以下文档，而是承接它们：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/governance/current-phase-boundary.md`
- `docs/superpowers/governance/evidence-review-index.md`
- `docs/superpowers/governance/client-exposure-threat-model.md`
- `docs/superpowers/governance/waivers/narrative-base-pack-waiver-register.md`
- `docs/superpowers/contracts/product/narrative-base-pack-contract.md`
- `docs/superpowers/contracts/product/capability-claim-matrix.md`
- `docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md`

本文档只回答以下问题：

- 单人运营时，服务器前端到底应该有哪些页面
- 每个页面到底管什么，不做什么
- prompt / orchestration 资产如何以治理一致的方式管理
- provider 如何热切换、如何按能力与 `billingSource` 路由
- 成本、次数、产品包、通知、反馈、审查如何形成闭环
- claim matrix / waiver / disclosure / Evidence Review Index / client exposure threat model 如何接到产品包、sellability、托管 / BYOK、后台管理口径上

补充说明：

- 本文档描述的是跨 `M1-M3` 的 operator/support control-plane blueprint
- 它不是当前 title 的 launch-path runtime dependency 清单
- 文中出现的“必须”默认解释为：
  - 若当前阶段 / 当前 build 启用了该 operator surface，则该 surface 内部必须满足这些治理要求
  - 不是指当前 `M1` 首发就必须全量落地整套控制面、双账本、全部产品线或全部高级包运营页
- 当前阶段到底哪些控制面能力进入首发必需范围，仍以：
  - `current-phase-boundary`
  - 当前 title profile
  - 当前 claim / waiver / evidence artifact
  为准

若与当前阶段硬边界、claim / sellability / waiver 真源或客户端暴露治理冲突，仍以对应治理 artifact 为准。

## 2. 设计目标

本设计的目标不是做一个“以后也许很好看”的后台，而是做一个你一个人就能真正运营首发产品的平台控制面。

目标如下：

1. 让一个人可以同时处理：
   - 系统健康
   - provider 路由
   - prompt 资产
   - 玩家问题
   - 产品包与次数
   - claim / waiver / disclosure / evidence linkage
   - 公告与通知
2. 让 `M1` 具备当前阶段允许的运营闭环：
   - 玩家可按已批准 claim / sellability 使用
   - 服务器可观测
   - 成本可预警
   - normalized 内容可修改
   - 问题可追溯
   - evidence review 可汇总
3. 让 `M2 / M3` 可以在不推翻后台结构的前提下扩张：
   - 次数包叠加
   - 多 provider 深化
   - 高级包独立运营
   - 平台自动化升级

## 3. 顶层原则

### 3.1 单人总控台原则

本后台不是企业多角色协作后台，而是：

- `单人总控台`

默认只有一个人登录并完成全部工作。

因此本设计不优先追求：

- 复杂 RBAC
- 多级审批流
- 部门分权

而优先追求：

- 一眼看清今天先做什么
- 一处完成大部分关键操作
- 不因为后台过度拆分而来回切页面

### 3.2 直接生效原则只适用于 normalized 生效层

prompt / orchestration 资产修改默认只允许发生在：

- `规范化资产`
- `映射与发布层`

不允许直接编辑：

- `原始保全层`

normalized 生效层默认允许：

- `立即生效`

但必须有 4 个硬护栏：

1. 明确提示当前修改的是：
   - `现行生效层`
2. 自动生成版本快照
3. 支持一键回滚
4. 首页必须可见最近内容变更

### 3.3 玩家看次数，平台看成本

玩家侧不看：

- token
- 美元
- provider 细节

玩家只看：

- `次数`

平台后台则必须同时维护两套账：

1. 玩家账：
   - 剩余次数
   - 扣减记录
   - 补偿记录
2. 平台账：
   - 预估成本
   - 收入
   - 毛利预警

但产品可卖、可如何披露、是否允许托管 / BYOK，不由后台文案手填决定，而必须回链到：

- `capability-claim-matrix`
- `sku-entitlement-claim-matrix`
- `narrative-base-pack-waiver-register`

### 3.4 明文审查是受控开发者路径，不是默认导出权限

平台当前允许在受控后台路径查看：

- 玩家 prompt
- AI 回复
- 记忆压缩结果
- owner-scoped actor memory 摘要
- 命中的审核规则

并允许人工修正：

- 记忆压缩结果
- owner-scoped actor memory 摘要

补充约束：

- 这里的长期记忆 / memory ownership 视角必须与 title profile 中的 actor-owned / owner-scoped memory 模型一致
- 不得把 recovered 参考链里的 actor-owned memory 重新建模成“用户画像真源”

但必须同时满足：

- 留下操作痕迹
- 不把完整 prompt asset 明文默认暴露到 checkpoint / telemetry / diagnostic sidecar / 导出面
- prompt-asset protection evidence 必须能被 `Evidence Review Index` 索引
- 任何完整 prompt / rendered prompt 明文仅允许出现在明确批准的 `developer-only` 受控流程

## 4. 页面信息架构

主导航固定为：

1. `首页`
2. `玩家`
3. `会话审查 / AI记录`
4. `内容`
5. `AI / Provider`
6. `商业 / 额度`
7. `运营通知`
8. `系统`

这 8 个页面的边界如下：

- `首页`：你的每日驾驶舱
- `玩家`：按人看全局状态
- `会话审查 / AI记录`：按链看受控明文执行记录
- `内容`：管理 normalized prompt 与 orchestration 资产，并承接原始保全层映射
- `AI / Provider`：管理 provider、模型、路由、热切换
- `商业 / 额度`：管理产品包、次数、订单、成本预警、claim / waiver / disclosure linkage
- `运营通知`：管理全体公告与个人通知
- `系统`：管理全局规则、保留策略、agent、危险操作、导出治理

## 5. 页面设计

### 5.1 首页

首页必须是：

- `决策首页`

而不是普通报表页。

首页固定分为 7 个区块：

#### 5.1.1 今日总览

- 今日活跃玩家
- 今日次数消耗
- 今日预估成本
- 今日收入
- 今日毛利预警
- 当前全局运行状态

#### 5.1.2 Provider 健康

- 当前主 provider / 模型
- 自动切换是否开启
- 最近一次切换原因
- 当前是否手动锁定
- 最近异常状态：限流、超时、报错、价格异常

#### 5.1.3 待处理玩家事务

- 新 bug 反馈
- 新意见反馈
- 新异常玩家记录
- 待处理申诉
- 待发送个人通知

#### 5.1.4 内容与 AI 风险

- 最近 normalized prompt 资产变更
- 最近规则包变更
- 记忆压缩异常
- owner-scoped actor memory 异常
- 会话异常增长

#### 5.1.5 产品包、额度与毛利预警

至少展示：

- `试用包`
- `基础包-BYOK`
- `基础包-托管`
- `高级包-绘画`
- `高级包-视频`
- `高级包-语音`

以及：

- 今日售卖 / 发放
- 玩家剩余次数分布
- 今日预估成本
- 今日收入
- 毛利预警

#### 5.1.6 治理阻断与证据提醒

- 当前 phase boundary 关键 gate 摘要
- 当前 `Evidence Review Index` 缺链提醒
- 当前 waiver 即将到期提醒
- 当前 prompt-asset protection evidence 状态
- 当前 client-exposure / 打包检查状态

#### 5.1.7 运营出口

- 发全体公告
- 发个人通知
- 查看异常会话
- 切换 provider
- 查看最近内容变更

### 5.2 玩家

`玩家` 页按“人”管理。

固定功能如下：

#### 5.2.1 基本信息

- `playerId`
- 最近登录
- 最近活跃
- 当前状态

#### 5.2.2 产品状态

- 当前试用包状态
- 当前 `基础包-BYOK`
- 当前 `基础包-托管`
- 当前高级包持有情况

#### 5.2.3 次数与额度

- 各产品线剩余次数
- 最近扣减记录
- 最近补偿记录
- 手动加次 / 扣次入口

#### 5.2.4 Owner-Scoped Memory

- 当前 owner-scoped actor memory 摘要
- 当前长期记忆摘要
- 最近一次压缩 / 更新的时间
- 手动修正入口

#### 5.2.5 玩家事务

- bug 反馈
- 意见反馈
- 申诉记录
- 异常标记
- 个人通知历史

#### 5.2.6 跳转

- 跳转到该玩家的会话列表
- 跳转到订单记录
- 跳转到额度账

### 5.3 会话审查 / AI记录

此页面必须拆成：

- `列表页`
- `详情页`

#### 5.3.1 列表页

必须支持按以下字段筛选：

- 玩家
- 时间范围
- provider
- 模型
- 包类型
- `billingSource`
- 是否异常
- 是否发生记忆压缩
- 是否人工修正过 owner-scoped memory / 记忆

列表至少显示：

- 时间
- 玩家
- 会话类型
- 玩家 prompt 摘要
- AI 回复摘要
- provider / 模型
- 扣次
- 预估成本
- 当前状态

#### 5.3.2 详情页

必须明文显示：

- 玩家原文 prompt
- AI 原文回复
- 命中的 normalized prompt 资产与版本
- provider
- 模型
- `billingSource`
- 扣次结果
- 预估成本
- 记忆压缩结果
- owner-scoped actor memory 更新结果
- 审核标记
- `traceId / requestId / sessionId`
- `claimStateRef`
- `waiverId` / `waiverLineageId`（如适用）

#### 5.3.3 人工操作

- 标记正常 / 异常 / 待处理
- 修正记忆压缩结果
- 修正 owner-scoped actor memory
- 添加备注
- 跳回玩家页

### 5.4 内容

`内容` 页不是普通 CMS，而是：

- `AI 资产控制台`

必须同时管理三层：

1. `原始保全层`
2. `规范化资产`
3. `映射与发布层`

#### 5.4.1 原始保全层

必须完整纳入：

- `recovered_mod/GGBH_OpenAIWorld_20260326/decoded_prompts/`

中的全量 corpus，而不是只抽几个核心 prompt。

至少包括：

- `常规*`
- `对话*`
- `群聊*`
- `传音*`
- `主动对话`
- `记忆压缩`
- `世界定义`
- `世界推演`
- `信息裂变`
- `行为指令`
- `交易`
- `身体交互`
- `道具使用`
- `自定义物品生成_*`
- `自定义状态生成`
- `角色卡模板`
- `竞技场_*`
- `双修场景`
- `解签大师`

该层的定位固定为：

- `只读保全层`

允许：

- 查看原始文件
- 查看 hash / version / source anchor
- 查看与 normalized key 的映射
- 查看差异

不允许：

- 直接编辑
- 直接发布
- 直接作为 ship-gate 生效层

#### 5.4.2 规范化资产

用于管理当前平台真正生效的 canonical key / normalized prompt。

至少要覆盖：

- `world.rules`
- `world.event_generation`
- `channel.private_dialogue`
- `channel.group_chat`
- `group.speaker_selection`
- `channel.remote_routed_communication`
- `group.remote_speaker_selection`
- `protocol.propagation`
- `protocol.actions`
- `protocol.memory_compression`
- `channel.proactive_dialogue`
- `protocol.transaction`
- `protocol.item_use`
- `protocol.body_interaction`
- `content.item_generation.*`
- `content.state_generation`
- `scenario.*`
- `utility.*`

该层允许编辑，并必须强提醒：

- 你正在修改 `现行生效层`

#### 5.4.3 资产映射与治理绑定

必须能查看：

- 原始文件 -> 规范化资产
- 规范化资产 -> 游戏
- 规范化资产 -> 能力
- 规范化资产 -> `billingSource`
- 规范化资产 -> 产品包
- 规范化资产 -> claim / disclosure 文案来源

该层不拥有最终 claim truth，而是回链到：

- `capability-claim-matrix`
- `sku-entitlement-claim-matrix`
- `narrative-base-pack-waiver-register`

#### 5.4.4 内容测试台

必须支持：

- 选游戏
- 选能力
- 选 `billingSource`
- 选 provider / 模型
- 输入测试上下文
- 直接查看输出
- 查看结构化解析
- 查看预估扣次
- 查看预估成本

默认只允许导出：

- diff
- digest
- redacted diagnostic

默认不允许导出：

- 完整 prompt asset 明文
- 可逆 rendered prompt
- 完整 orchestration rule chain

#### 5.4.5 发布、快照与回滚

虽然 normalized 生效层默认允许立即生效，但必须自动：

- 生成版本快照
- 保存 diff
- 提供一键回滚
- 记录操作者、时间、变更原因

#### 5.4.6 审核与记忆规则

用于管理：

- 记忆压缩 prompt
- owner-scoped actor memory prompt
- 审核 prompt
- 分类 / 标记规则

#### 5.4.7 Prompt Asset Protection Evidence

必须能沉淀并回链以下证据：

- prompt / orchestration asset classification
- checkpoint / telemetry / sidecar / crash dump redaction 样例
- hosted-only asset 未随客户端分发的检查证据
- revoke / rotate runbook

并要求：

- 默认由 `Evidence Review Index` 索引
- 默认与 `client-exposure-threat-model` 一致

### 5.5 AI / Provider

此页面是平台的 AI 运行控制台。

建议拆成 6 个子页：

#### 5.5.1 Provider 总览

- provider 列表
- 当前状态
- 当前主 provider / 备用 provider
- 最近切换原因
- 当前是否手动锁定

#### 5.5.2 路由策略

必须按能力与 `billingSource` 配置：

- 对话
- 群聊
- 记忆压缩
- owner-scoped actor memory
- 审核
- 绘画
- 视频
- 语音

并支持：

- 自动切换开关
- 手动强制覆盖
- 锁定持续时间

但路由页必须显式区分：

- `Hosted Narrative Orchestration` 默认主链
- `Hosted Capability` 仅限已批准 capability / SKU / `billingSource`

#### 5.5.3 模型与能力配置

每种能力至少要配置：

- 默认模型
- 一次扣多少次
- 预估单次成本
- 超时阈值
- fallback 行为
- 是否允许 `BYOK`
- 是否必须 `platform_hosted`
- 对应 claim / sellability / waiver reference

#### 5.5.4 切换与异常记录

- 自动切换日志
- 手动切换日志
- 失败原因
- 被切掉的 provider
- 切到的 provider

#### 5.5.5 成本与效能

- 按 provider 看预估成本
- 按模型看预估成本
- 按能力看预估成本
- 按 `billingSource` 看预估成本
- 成功率
- 延迟
- 平均单次成本

#### 5.5.6 高级包 Provider

必须单独管理：

- `高级包-绘画`
- `高级包-视频`
- `高级包-语音`

每条线独立看：

- provider
- 路由
- 强制覆盖
- 预估成本
- 毛利预警

### 5.6 商业 / 额度

此页面用于管理卖什么、怎么扣、谁买了、还剩多少。

建议拆成 6 个子页：

#### 5.6.1 产品包管理

必须管理：

- `试用包`
- `基础包-BYOK`
- `基础包-托管`
- `高级包-绘画`
- `高级包-视频`
- `高级包-语音`

每个包至少展示：

- 名称
- 当前 listing / entitlement 状态
- 价格
- 包含能力
- 是否包含次数
- 当前 claim copy / disclosure copy
- linked waiver

其中：

- sellability state 只能来自 `sku-entitlement-claim-matrix`
- support claim 不得宽于 `capability-claim-matrix`
- 任何 `phase_waived` / `partial_preview` 都必须显式展示 disclosure，而不是写成 pack-level shorthand

#### 5.6.2 次数规则与计费路由

必须定义：

- 每种能力一次扣多少次
- 哪些能力可走 `BYOK`
- 哪些能力必须 `platform_hosted`
- 哪些能力当前仅为 approved experiment / preview

并要求全部回链到：

- `sku-entitlement-claim-matrix`
- `capability-claim-matrix`

#### 5.6.3 玩家额度账

- 玩家当前剩余次数
- 扣减记录
- 补偿记录
- 手工加次 / 扣次
- 操作备注

#### 5.6.4 销售与订单

- 购买记录
- 生效状态
- 来源
- 当前关联产品包
- 当前 `billingSource`

#### 5.6.5 成本与毛利

必须把以下产品线拆开看：

- `基础包-BYOK`
- `基础包-托管`
- `高级包-绘画`
- `高级包-视频`
- `高级包-语音`

至少展示：

- 收入
- 预估成本
- 毛利
- 亏损预警

#### 5.6.6 Waiver / Disclosure / Sellability 绑定

必须明确：

- 是否 sellable 由 `sku-entitlement-claim-matrix` 决定
- 是否可用 pack-level shorthand 由 `narrative-base-pack-contract` 决定
- 是否需要 waiver 由 phase boundary 与 claim state 共同决定
- waiver 文本、expiry、closure criteria、player-visible disclosure 必须回链到 waiver register

### 5.7 运营通知

此页面管理平台对玩家发出的所有主动消息。

建议拆成 4 个子页：

#### 5.7.1 全体公告

用于：

- 维护通知
- 版本更新
- 产品包说明
- 大范围补偿说明

字段至少包括：

- 标题
- 正文
- 生效时间
- 下线时间
- 是否置顶
- 是否弹窗

#### 5.7.2 个人通知

用于：

- 额度补偿
- 问题回复
- 审核提醒
- 订单说明
- waiver / degraded disclosure follow-up

#### 5.7.3 通知模板

至少要有：

- 维护通知模板
- 额度补偿模板
- Bug 已处理模板
- 功能暂不可用模板
- 购买成功模板

#### 5.7.4 发送记录

- 发给谁
- 发了什么
- 什么时间
- 是否已读
- 是否撤回

### 5.8 系统

此页面放全局规则和危险操作。

固定功能如下：

#### 5.8.1 全局开关

- 功能开关
- provider 全局开关
- 后台实验开关

#### 5.8.2 数据保留策略

- 明文原始记录：`90 天`
- 审计摘要：`1 年`
- owner-scoped actor memory / 长期记忆：长期保留

#### 5.8.3 操作审计

必须记录：

- 改了什么
- 什么时候改的
- 改前值
- 改后值

#### 5.8.4 服务器 Agent 配置

至少包括：

- 记忆压缩 agent
- owner-scoped actor memory agent
- 审核 agent

#### 5.8.5 危险操作

- 全局回滚
- 下架包
- 关闭 provider
- 停止某项能力

#### 5.8.6 导出与诊断治理

必须管理：

- checkpoint redaction policy
- telemetry redaction policy
- diagnostic sidecar redaction policy
- export policy
- revoke / rotate runbook linkage

并默认要求：

- 不暴露完整 prompt asset 明文
- 不暴露完整 orchestration rule chain
- Evidence Review Index 可直接回链到样例证据

## 6. 玩家反馈与问题闭环

玩家反馈不应单独变成一个主导航页，而应主要挂在：

- `玩家`
- `首页`
- `会话审查 / AI记录`

必须支持三类记录：

1. `bug 反馈`
2. `意见反馈`
3. `异常 / 申诉`

并形成闭环：

1. 玩家提交问题
2. 你在后台看到
3. 关联到玩家与会话
4. 做人工处理
5. 通过个人通知回执

## 7. 产品包与商业模型

当前平台的产品线固定为：

### 7.1 试用包

- 含次数
- 次数可配置
- 用于转化

### 7.2 基础包-BYOK

- 玩家自己提供 key
- 平台卖授权
- 基础能力可用
- 不吃平台模型成本

### 7.3 基础包-托管

- 玩家使用平台 API
- 平台按次数卖
- 示例：`1000 次对话 = 68`

### 7.4 高级包

独立存在三条：

- `高级包-绘画`
- `高级包-视频`
- `高级包-语音`

它们必须：

- 单独配置 provider
- 单独看成本
- 单独看毛利

### 7.5 治理绑定

所有产品包都必须明确：

- capability support declaration 来自 `capability-claim-matrix`
- SKU 级 sellability / listing / entitlement 来自 `sku-entitlement-claim-matrix`
- 缺项只能通过 waiver + disclosure + recovery plan 进入 `sellable_with_disclosure`
- 不得通过后台 shorthand 把基础包缺项改写成 premium upsell

## 8. 当前 `M1` 最小 shared / runtime 接线

本节只定义当前 `M1` 必做的最小接线，不把超出当前阶段的 shared burden 冒充成现阶段必做范围。

### 8.1 `M1` 必做

平台控制面至少要能稳定回链以下最小键与引用：

- `playerId`
- `gameId`
- `sessionId`
- `requestId`
- `traceId`
- `skuId`
- `capability`
- `billingSource`
- `claimStateRef`
- `waiverId` / `waiverLineageId`（条件适用）
- prompt asset version reference

但具体 required field set 仍以 runtime contracts 为准，不在本文档重复扩权。

### 8.2 `M1` 必做页面绑定

`M1` 后台至少要把以下治理接线做实：

- `内容` 页能把 normalized asset 版本回链到 product / claim / disclosure
- `商业 / 额度` 页能把 sellability、waiver、billingSource 回链到对应 artifact
- `会话审查 / AI记录` 页能把 trace、claimStateRef、waiver linkage 串起来
- `系统` 页能把 prompt-asset protection evidence、client exposure、导出治理串到 Evidence Review Index

### 8.3 `M2 / M3 / approved experiment` 预留

以下结构允许预留，但不能写成当前 `M1` 必做：

- 更厚的 shared command classes
- 多包叠加与复杂优先扣减
- 更强的 world / control profile burden
- 更成熟的 hosted capability 批量编排面
- 更细的 cross-game shared module 标准化

## 9. 平台与运行时的 future-ready 边界

### 9.1 当前 `M1` 必做

平台控制面在 `M1` 只需要消费：

- runtime health fact
- degradation / recovery evidence
- trace linkage
- claim / waiver / disclosure linkage

不要求在当前阶段额外承担：

- 本地执行协议重定义
- 多游戏统一 command taxonomy 扩容
- AFW shared burden 扩张

### 9.2 `M2 / M3 / approved experiment`

到后续阶段才考虑增强：

- 次数包叠加与多包切换
- 更细的成本统计
- 高级包三条线独立运营
- 更成熟的平台自动化
- 更成熟的 provider 编排
- 更厚的 shared-layer 控制面

## 10. M1 / M2 / M3 落地深度

### 10.1 M1

当前 operator blueprint 下，`M1` 最多只要求：

- 若当前 build 启用了控制面：
  - 只需覆盖首发运营闭环所必需的最小页面集合
  - 不要求 8 个主页面全部在 `M1` 首发时一次性落地

`M1` operator 最小闭环：

- 首页总控台
- 玩家与会话受控明文审查
- 原始保全层只读 + normalized 生效层编辑
- provider 热切换
- 与当前首发 SKU / capability 范围直接相关的产品包与次数管理
- 次数账
- 成本预警
- 公告 / 个人通知
- 操作审计
- claim / waiver / disclosure / evidence linkage
- prompt-asset protection evidence 基础沉淀

`M1` 不做：

- 复杂 BI
- 复杂风控
- 复杂多包切换
- 企业级审批流
- 超出当前 phase 的 shared burden

### 10.2 M2

`M2` 重点补：

- 次数包叠加
- 多包切换
- 包升级 / 降级
- 更细的成本统计
- 更细的玩家分层
- 高级包三条线的独立运营面

### 10.3 M3

`M3` 再补：

- 更成熟的平台自动化
- 更成熟的 provider 编排
- 更厚的 shared-layer 控制面
- 多游戏复用下的后台标准模块

## 11. 当前已知边界与待补证据

当前控制面设计已经足够承接：

- `Stardew Valley`
- `RimWorld`
- 当前规划中的第三款 `太吾绘卷`

但必须明确：

- `Stardew` 当前仍是 working design，不等于自动完成 ship-gate 闭环
- `太吾绘卷` 目前只可作为规划中的第三款 title，不得因为后台已有位就视为已进入当前 claim scope
- 任一 title 的具体宿主级 gift delivery flow、active-world surface、玩家可见宿主，仍需在后续 `game-integration-profile` 与 Evidence Review Index 中补证据
- 不得直接继承 `鬼谷八荒` 的旧宿主链路假设

## 12. 最终结论

本设计的最终结论如下：

1. 平台后台必须按 `单人总控台` 设计，而不是企业多角色后台。
2. 主导航固定为：`首页 / 玩家 / 会话审查-AI记录 / 内容 / AI-Provider / 商业-额度 / 运营通知 / 系统`。
3. prompt 资产必须同时支持：
   - 原始保全层只读
   - 现行生效层编辑
   并明确提示当前层级。
4. 玩家侧统一按“次数”消费，平台后台同时维护玩家次数账与平台成本账。
5. 商业模型必须同时支持：
   - 试用包
   - 基础包-BYOK
   - 基础包-托管
   - 高级包-绘画 / 视频 / 语音
6. claim matrix / waiver / disclosure / Evidence Review Index / client exposure threat model 必须直接接到产品包、sellability、托管 / BYOK、导出治理与后台审查口径上。
7. `M1` 只要求最小 shared / runtime 接线；`M2 / M3 / approved experiment` 的更厚负担只能保留 future-ready 结构，不能冒充当前必做。
