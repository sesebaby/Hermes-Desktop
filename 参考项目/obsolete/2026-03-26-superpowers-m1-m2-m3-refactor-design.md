# All Game In AI 分阶段重构版设计（M1 / M2 / M3）

> Historical phase-refactor note as of 2026-03-27.
>
> This draft remains useful as a record of earlier phase-scope reduction decisions, but its "M1 mainly runs on local BYOK text path" assumptions are no longer the current target architecture.
> For current authority, use:
> - `docs/superpowers/specs/2026-03-26-superpowers-double-core-framework-design.md`
> - `docs/superpowers/governance/current-phase-boundary.md`
> - `docs/superpowers/governance/client-exposure-threat-model.md`
>
> Read this obsolete draft as phase-history only. Current direction is:
> - hosted narrative orchestration by default
> - local deterministic execution retained as the host-truth boundary
> - `user_byok` and `platform_hosted` coexisting within one hosted base-narrative chain
> - the current `M1` planning assumption no longer uses `鬼谷八荒` as the third launch title; planning has moved to `太吾绘卷`, while the detailed matrices below remain preserved as historical draft content

## 1. 文档定位

本文档不是对现有两份设计稿的重复展开，而是一份收敛版重构决议稿。

目标只有一个：

- 把当前过重、过早平台化的方案，重构成一个可以真实交付的三阶段路线

本文档直接回答以下问题：

- `M1` 到底做什么，不做什么
- 哪些能力必须延后到 `M2` 或 `M3`
- 跨游戏扩展能力应该如何从“硬门槛”改成“分层解锁”
- 授权、恢复、兼容、日志这些平台能力，分别在哪一阶段做到什么程度

本文档优先级高于当前“写满未来形态”的表述。
若与以下文档冲突，以本文档的阶段边界为准：

- [2026-03-25-all-game-in-ai-tech-stack-design.md](D:/Projects/AllGameInAI/docs/superpowers/specs/2026-03-25-all-game-in-ai-tech-stack-design.md)
- [2026-03-26-cross-genre-extensibility-design.md](D:/Projects/AllGameInAI/docs/superpowers/specs/2026-03-26-cross-genre-extensibility-design.md)

## 2. 重构目标

本次重构不是为了否定长期平台方向，而是为了修正首发阶段的工程顺序。

重构目标如下：

1. 先交付一个可卖、可启动、可恢复、可支持的第一代产品
2. 先验证 `3` 款首发游戏的真实用户价值，再扩展协议和平台内核
3. 让宿主保持薄，让复杂性尽量留在 adapter、capability 和可选 profile，而不是提前压进 Runtime Core
4. 让局部失败可以局部降级，而不是一个控制面故障拖死整条链路
5. 让跨游戏扩展从“统一最低门槛”改成“分层解锁”

## 3. 核心判断

当前方案最大的问题不是架构方向错误，而是 `M1` 承担了过多未来平台职责。

具体表现为：

- 把跨类型扩展模型过早升级成统一硬门槛
- 把强在线授权做得过硬，缺少已验证设备的离线宽限
- 把 `Runtime` 生命周期绑死在短租约和短令牌上
- 把版本协商面铺得过宽，导致升级、回滚和支持成本急剧上升
- 把日志、计费、恢复、兼容一次性做成完整平台治理体系

因此，重构后的路线必须改成：

- `M1`：先做产品
- `M2`：再做可扩展性
- `M3`：最后做平台化

## 4. 总体分期结论

### 4.1 M1

`M1` 的产品定义是：

- 一个可交付的 Windows 首发产品
- 首发暂定支持 `3` 款游戏：`星露谷物语`、`环世界`、`鬼谷八荒`
- 重点验证 `对话 + 记忆 + 创造物品`
- 允许未来扩展，但不提前为所有未来游戏类型支付平台成本
- 这 `3` 款游戏在 `M1` 先按同一基础版验收线交付，不在首发阶段拉开协议深度差异

`M1` 的关键词：

- 薄宿主
- 小协议
- 强恢复
- 软降级
- 少协商

`M1` 在当前前提下不再是“单游戏验证架构”，而是“多游戏宿主治理的最小可交付版本”。

因此：

- `M1` 必须补齐按游戏隔离的宿主治理
- `M1` 必须允许按游戏定义不同的 launch profile
- `M1` 必须先建立一套三款游戏共用的基础版能力验收线
- `M1` 仍然不得因此回退到完整跨类型平台内核

### 4.1.1 M1 Launch Titles

`M1` 首发游戏固定如下：

- `星露谷物语`
- `环世界`
- `鬼谷八荒`

这三款游戏都属于 `launch title`，并且在 `M1` 必须按同一基础版支持。

这里的“同一基础版支持”含义很明确：

- 三款游戏都先交付同一条最小主链路
- 三款游戏都先满足同一条宿主级诊断与恢复验收线
- 三款游戏都允许有各自的 loader、适配器和文案差异
- 三款游戏在 `M1` 不允许因为某一款的局部需求而提前拉开协议深度
- 三款游戏后续要不要继续加深，属于 `M2` 之后的决策，不属于 `M1` 首发门槛

`M1` 必须为每款游戏维护独立的 `Launch Profile`，至少声明：

- `integration tier`
- `supported capability set`
- `supported command classes`
- `required prerequisites`
- `degraded modes`
- `rollback target`
- `recovery promise`

最低产品原则如下：

- 三款游戏都必须可安装、可启动、可诊断、可恢复
- 三款游戏都必须支持对话、记忆和创造物品
- 三款游戏都必须通过同一条基础版验收线，而不是首发就分 `A/B` 深度
- 三款游戏都允许存在不同的 game-local 适配与内容差异，但这些差异只允许落在 `per-game launch profile`、adapter 和 trigger mapping
- 不允许因为追求三款“功能一样多”而把 `M2/M3` 的平台抽象强行拉回 `M1`
- 也不允许因为追求某一款“先做深一点”而把单游戏例外提前升级成 `M1` 平台能力

### 4.1.2 M1 三款 Mod 并行开发原则

`M1` 的三款首发 Mod 必须按“共享底座 + 各游戏适配器并行开发”的方式推进。

共享底座范围固定如下：

- `Launcher`
- `Local Runtime`
- `Cloud Control`
- `Shared Contracts`
- 本地记忆主链路
- 基础授权、诊断、恢复、quarantine
- 基础版 capability allowlist 与命令白名单

各游戏并行开发只允许各自负责：

- loader / prerequisite 适配
- 安装路径、存档路径、日志路径接入
- `Mod -> Runtime` translator
- game-local prompt vocabulary
- game-local dialogue UI binding
- game-local gift trigger mapping
- game-local gift delivery flow
- game-local fault message 和 recovery copy

不允许在并行开发过程中发生以下漂移：

- 某一款游戏先新增更深一层的共享协议字段
- 某一款游戏先把 `world read model`、`control profile` 或额外命令类型做成 `M1` 默认要求
- 某一款游戏为了省事把自己的局部例外塞进共享平台抽象

`M1` 的成功标准不是“三款游戏同时做深”，而是“三款游戏都先有一个同一标准的基础版可卖、可启、可恢复版本”。

### 4.2 M2

`M2` 的产品定义是：

- 在 `M1` 已验证产品价值的前提下，扩到更多游戏
- 正式引入“按档位解锁”的跨游戏协议能力
- 开始支持部分更重的 genre profile

`M2` 的关键词：

- 分层扩展
- profile 化协议
- 受控协商
- 能力隔离

### 4.3 M3

`M3` 的产品定义是：

- 当至少已有多款不同类型游戏稳定接入后，再把复用事实沉淀成平台内核
- 这时才值得引入更重的 world graph、control plane、托管计费控制面

`M3` 的关键词：

- 平台内核
- 多类型统一
- 托管能力执行面
- 完整治理

### 4.4 分阶段成本核算结论

成本核算在本次重构中不是缺席，而是必须跟随阶段边界一起收缩。

核心原则如下：

- 成本核算首先服务阶段决策与 go/no-go 判断，不先做成财务级平台
- 所有成本至少拆成四类：`platform fixed cost`、`per-game integration cost`、`operations/support cost`、`AI variable cost`
- 任何核算口径都必须能回答“成本发生在哪个 `gameId`、哪类 capability、哪种 `billingSource`、哪个阶段”，否则不算可用核算
- `billingSource` 至少保留三种来源：`user_byok`、`platform_hosted`、`mixed`
- 先做事实归因，再做计费控制，最后才做完整账本与审计对账

`M1` 的成本核算目标不是建立完整计费系统，而是回答：

- 三款首发游戏各自的接入与维护成本是否值得继续投入
- 当前 Windows 首发宿主的固定成本和支持成本是否可控
- 玩家自带 Key 的文本链路是否带来了足够的使用价值信号

因此 `M1` 只要求做到以下轻量核算：

- 按游戏记录 `per-game integration cost`，至少能区分 adapter 开发、兼容修复、版本跟进、故障回退与支持工时
- 按游戏记录 `operations/support cost`，至少能区分诊断次数、quarantine 次数、恢复次数、升级失败与人工介入频率
- 对 AI 消耗只做轻量事实归因，不做完整平台扣费；`M1` 实际以 `user_byok` 文本链路为主，并为未来可能出现的 `platform_hosted` 预留归因字段
- 允许为 `BYOK` 文本链路提供本地软预算、并发提示和 usage 摘要，但这些只是用户体验与阶段判断工具，不是平台账本
- `M1` 的输出应能支持每款首发游戏的阶段复盘，而不是生成统一商业报表

`M1` 明确不做：

- 完整托管计费账本
- `quote / reservation / commit / release / compensation`
- 跨类别统一余额池
- 供应商级精细对账与退款审计
- 为三款首发先做统一的财务级成本控制面

`M2` 的成本核算目标才升级为“受控托管能力”的类别级核算：

- 只对 `M2` 新引入的有限托管能力入口做成本归因与预算控制
- 必须开始区分 `estimated cost` 与 `settled cost`
- 必须按 `gameId`、`capability category`、`provider`、`model`、`billingSource`、`operationId` 做最小归因
- 可以开始引入类别级 soft budget、preflight 和限流，但只覆盖真实上线的托管类别
- 仍然不把所有本地 `BYOK` 路径和所有游戏能力强行并入一个统一账本

`M3` 才进入完整平台化成本治理：

- 托管能力成为真实商业主线后，再建设完整账本状态机
- 正式支持 `reservation / commit / release / compensation`
- 正式支持供应商对账、退款审计、类别毛利分析与平台级经营报表
- 可以回答“某次调用是否真的发生、平台是否真的承担成本、最终是否应该扣费或退款”

## 5. 分阶段架构图

### 5.1 M1 架构

`M1` 只保留以下核心子系统：

- `Launcher`
- `Local Runtime`
- `Game Adapter Mod`
- `Cloud Control Service`

`M1` 明确不把以下内容作为首发硬依赖：

- 通用 `Hosted Capability Service`
- 完整 `Capability Host ABI`
- `WorldSnapshot / WorldDelta` 通用模型
- 长生命周期 `Control / Intent Command` 执行平面
- 全平台 `Facet Registry` 治理体系

### 5.2 M2 架构

在 `M2`，允许新增：

- `Capability Host` 最小 ABI
- 协议 profile 协商
- 可选 world profile
- 可选 command profile
- 有限托管能力执行入口

### 5.3 M3 架构

在 `M3`，再引入：

- 完整 `Hosted Capability Service`
- 完整计费状态机
- 全局 facet/version 治理
- 统一 world graph 与 control plane

## 6. M1 设计

### 6.1 M1 首发游戏与支持矩阵

`M1` 必须把“3 款首发”写成明确矩阵，而不是抽象口号。

三款首发的共同最低支持面如下：

- NPC 对话
- 基础记忆
- NPC 创造物品
- 按游戏隔离的安装、启动、诊断、恢复

三款首发的宿主级支持矩阵至少要覆盖：

- `Game Integration Profile`
- `Game Readiness Checks`
- `Capability Allowlist`
- `Gift Trigger Mapping`
- `Gift Delivery Flow`
- `Degraded Startup Policy`
- `Recovery Entry`

其中必须明确：

- `星露谷物语` 的差异主要落在头顶气泡、原版对话框、事件式多人对话、白名单礼物模板和邮件发放
- `环世界` 的差异主要落在头顶气泡、连续气泡群聊、现有 `ThingDef` / 随机奖励机制接入和地面掉落
- `鬼谷八荒` 的差异主要落在原版对话框、多人头像式对话框、官方奇遇奖励链路接入和直接入包
- 这些差异在 `M1` 只影响 adapter、UI 绑定、触发表、模板来源和发放路径，不影响共享协议深度

三款首发的最低可执行矩阵固定如下：

- `星露谷物语`
  - `integration tier`: `foundation`
  - `supported capability set`: 对话、记忆、基于白名单模板和邮件投递的 NPC 创造物品
  - `supported command classes`: `render_command`、`transactional_command`
  - `dialogue presentation`: 头顶气泡、原版风格对话框、NPC 群聊（`M1` 先做事件式多人连续发言）
  - `gift trigger sources`: 好感度、时间、事件、生日、随机、纪念日、活动
  - `gift delivery flow`: NPC 提示后，通过 `Data/Mail` / `AddMail` 链路发给玩家
  - `degraded modes`: 对话绑定、触发表、邮件投递或 adapter 异常时允许退化为只读诊断和恢复入口
  - `recovery promise`: 可按游戏单独回退最近一次成功配置并禁用问题 capability
- `环世界`
  - `integration tier`: `foundation`
  - `supported capability set`: 对话、记忆、基于现有 `ThingDef` / 随机奖励和地面掉落的 NPC 创造物品
  - `supported command classes`: `render_command`、`transactional_command`
  - `dialogue presentation`: 头顶气泡、气泡群聊（`M1` 先做多 NPC 连续气泡）
  - `gift trigger sources`: 好感度、时间、事件、生日、随机、纪念日、活动
  - `gift delivery flow`: NPC 提示后，用 `ThingMaker.MakeThing` + `GenPlace.TryPlaceThing` 掉落到地上，由玩家拾取
  - `degraded modes`: 对话绑定、触发表、掉落流程或 adapter 异常时允许退化为只读诊断和恢复入口
  - `recovery promise`: 可按游戏单独回退最近一次成功配置并禁用问题 capability
- `鬼谷八荒`
  - `integration tier`: `foundation`
  - `supported capability set`: 对话、记忆、基于官方奇遇 `产出物品` 链路的 NPC 创造物品
  - `supported command classes`: `render_command`、`transactional_command`
  - `dialogue presentation`: 原版风格对话框、多人头像式对话框
  - `gift trigger sources`: 好感度、时间、事件、生日、随机、纪念日、活动
  - `gift delivery flow`: NPC 提示后，通过奇遇 / 事件 `产出物品` 直接进入玩家获得流程并放入背包
  - `degraded modes`: 对话绑定、触发表、赠送流程或 adapter 异常时允许退化为只读诊断和恢复入口
  - `recovery promise`: 可按游戏单独回退最近一次成功配置并禁用问题 capability

宿主实现时必须把这张矩阵落成可执行配置，而不是只保留文档说明。

这张矩阵的真正含义是：

- 三款游戏在 `M1` 共享同一条基础版产品线
- `Launch Profile` 仍然按游戏独立维护
- 但 `Launch Profile` 在 `M1` 只允许承载“接入差异”和“恢复差异”，不允许承载“协议深度差异”
- 任一游戏若需要超出这张矩阵的能力，默认进入 `M2` backlog，而不是回写成 `M1` 首发要求

### 6.1.1 M1 可落地实现基线

本次重构后的 `M1` 不是继续凭想象写“以后也许能做什么”，而是必须绑定到已确认存在的官方资料、官方示例和开源轮子。

当前已整理到仓库内、可直接给研发查阅的资料如下：

- 星露谷物语参考：[README.md](D:/Projects/AllGameInAI/参考项目/参考文档/星露谷物语/README.md)
- 星露谷“新增种子物品但只做文本感知”附件：[2026-03-26-stardew-seed-item-text-only-reference.md](D:/Projects/AllGameInAI/docs/superpowers/specs/attachments/2026-03-26-stardew-seed-item-text-only-reference.md)
- 环世界参考：[README.md](D:/Projects/AllGameInAI/参考项目/参考文档/环世界/README.md)
- 鬼谷八荒参考总目录：[README.md](D:/Projects/AllGameInAI/参考项目/参考文档/鬼谷八荒/README.md)
- 鬼谷八荒官方文档索引：[00-索引.md](D:/Projects/AllGameInAI/参考项目/参考文档/鬼谷八荒官方文档/00-索引.md)

三款首发游戏的可落地基线固定如下：

- `星露谷物语`
  - 官方底座是 `SMAPI` 文档与 Stardew Valley Wiki 的 `Dialogue`、`Mail data`、`Event data`、`Trigger actions`、`Items`、`Data API`、`Harmony API`
  - 最值得复用的开源轮子是 `CustomGiftDialogue`、`BirthdayMail`、`HappyBirthday`、`ImmersiveFestivalDialogue`、`StardewGPT`、`GoldPerEnergyDisplay`
  - `M1` 最短主链路是：NPC 气泡 / 对话 -> 条件命中 -> 邮件发礼物 -> 物品实例写 `modData` -> 需要时 patch tooltip
  - `M1` 不把“真正持续聊天群 UI”当硬门槛，先收敛为事件式多人连续对话
  - 若需求收敛为“新增一个种子物品但主要只做文本层感知”，默认仍优先走现有模板实例化与展示层覆写；只有在产品上明确要求背包中出现新 seed 条目时，才允许把 `Content Patcher + Data/Objects + i18n` 作为星露谷 game-local 受控例外，详见附件：[2026-03-26-stardew-seed-item-text-only-reference.md](D:/Projects/AllGameInAI/docs/superpowers/specs/attachments/2026-03-26-stardew-seed-item-text-only-reference.md)
- `环世界`
  - 工程底座是 `Harmony`、`HugsLib`、RimWorld Wiki 的 `ThingComp`、`Harmony`、`BigAssListOfUsefulClasses`
  - 最值得复用的开源轮子是 `Interaction Bubbles / Bubbles`、`SpeakUp`
  - `M1` 最短主链路是：社交气泡 / 连续气泡 -> 条件命中 -> `ThingMaker.MakeThing` 创建礼物 -> `GenPlace.TryPlaceThing` 掉落 -> 玩家拾取
  - 物品实例态优先落在 `ThingComp`；名称和说明如果需要额外展示，优先走 `TransformLabel`、`CompInspectStringExtra`、inspect patch，而不是改成新物品系统
- `鬼谷八荒`
  - 官方底座是游戏内置 `MOD 制作工具`、官方示例 `〖官方示例〗八九玄功-残卷` 与 `〖官方示例〗灵田秘境`
  - 当前最关键的官方资料不是公开网站 API 门户，而是内置或外链文档包：`事件编辑器使用说明.pdf`、`配置对照表.xlsx`、`GGBH_API.chm`
  - `M1` 最短主链路是：NPC 对话提示 -> 奇遇 / 事件 `产出物品` -> 玩家直接获得并入包
  - `M1` 不把“NPC 背包中转再赠送”当首发硬门槛；这条链路可保留为后续加深方向

这部分结论同时意味着：

- `M1` 可以明确复用轮子，不能再把三款游戏都写成“从零创造完整玩法系统”
- `M1` 的 `per-game launch profile` 必须补充“当前落地依赖了哪些官方文档、哪些开源轮子、哪些仍是存疑点”
- 任一需求如果在现有官方资料和现有轮子上都没有证据，默认不进入 `M1` 验收线

### 6.2 M1 必做范围

`M1` 必须只做以下能力：

- `Launcher` 登录、启动、升级、诊断入口
- 单游戏 `Runtime` 拉起与最小监控
- `Mod -> Runtime` 的本地调用链路
- 玩家自带 Key 的对话主链路
- 本地记忆
- 基于现有模板的 NPC 创造物品
- 基础礼物触发调度
- 版本检查
- 问题诊断包
- 基础恢复能力

`M1` 只承诺以下执行模型：

- `request/response`
- `streaming`
- `event-triggered`

`M1` 不承诺：

- `continuous`
- `scheduled`
- 真正后台常驻 agent
- 通用长生命周期控制流

此外必须明确：

- `M1` 的三款首发游戏都必须具备原生风格的对话表现
- `M1` 的三款首发游戏都必须具备由 NPC 主动触发的送礼链路
- `M1` 不上线托管高级能力
- `M1` 不上线平台托管绘画、语音、视频入口

### 6.2.1 M1 礼物触发模型

`M1` 的 NPC 创造物品不以“玩家主动索要”为主，而以“NPC 在合适时机主动送礼”为主。

三款首发游戏统一支持以下触发来源：

- `relationship_trigger`
- `time_trigger`
- `event_trigger`
- `birthday_trigger`
- `random_trigger`
- `anniversary_trigger`
- `festival_trigger`

这里的统一是产品口径统一，不是底层接法统一。

`M1` 允许每个游戏按自己的系统把这些触发来源映射到本地事件，但必须满足：

- 玩家最终能感知到 NPC 是因为关系、时间、事件或活动而自然送礼
- 送礼前后必须有对话表现
- 送礼一定要带上下文，不能变成无缘无故刷出道具
- 触发映射写在 `per-game launch profile` 中，不升级成平台级世界模型

### 6.2.2 M1 创造物品规则

`M1` 的“创造物品”必须明确收敛为“现有模板实例化”，而不是“发明新物品系统”。

统一规则如下：

- 不新增美术素材
- 不新增全新物品类别
- 不引入新的复杂数值体系
- 直接复用游戏现有物品模板
- AI 负责生成该次送礼事件的 `名称文案` 与 `描述文案`
- `M1` 至少要在对话框、邮件、气泡、奖励提示或 tooltip 其中一种原生表现层把这组名称与描述展示给玩家
- 是否真正写回“物品实例字段”，由 `per-game launch profile` 按游戏能力决定
- 未被证实支持实例级覆写的游戏，不把“背包内永久显示 AI 名称与描述”作为 `M1` 验收线

三款首发游戏的模板来源策略固定如下：

- `星露谷物语`：只允许小批白名单物品模板；动态名称与描述优先走 `item.modData` + Harmony patch
- `环世界`：优先复用现有 `ThingDef`、随机物品、掉落或奖励机制；实例态优先挂在 `ThingComp`
- `鬼谷八荒`：优先复用官方奇遇 / 事件奖励与现有道具模板；若背包 tooltip 覆写无铁证，则先把 AI 名称与描述放在对话层和奖励层

对 `星露谷物语` 额外补充一条受控例外规则：

- 若只是让玩家感知到“有情绪名称和描述的种子礼物”，默认仍以模板实例化 + 展示层覆写为先
- 只有在产品上明确要求背包中出现新 seed 条目时，才允许用 `Content Patcher + Data/Objects + i18n` 新增极少量 game-local 条目
- 即便启用该例外，也不得把 `Json Assets` / `DGA` 升级成 `M1` 默认依赖
- 详细参考与本地已下载开源项目见附件：[2026-03-26-stardew-seed-item-text-only-reference.md](D:/Projects/AllGameInAI/docs/superpowers/specs/attachments/2026-03-26-stardew-seed-item-text-only-reference.md)

`M1` 的名称与描述规则固定如下：

- 产品语义上，名称与描述属于本次礼物事件，不得被设计成全局永久改模板
- 技术实现上，允许按游戏能力分层：能做实例级就做实例级，做不到实例级就做局部模板池或展示层覆写
- 名称与描述应体现 NPC 身份、关系、当前时机与送礼语境
- 物品真实功能、数值、素材与基础类别仍跟模板走
- 文案不得把物品描述成与真实用途完全不符的东西

`M1` 在创造物品上明确不做：

- 玩家自由输入想要什么物品
- 全量物品开放给 AI 任意选择
- 素材采集与配方系统
- 复杂制造链
- AI 发明全新物品类型
- 平台级统一物品 schema
- 在证据不足的游戏里承诺“背包内实例级名称 / 描述永久覆写”

### 6.3 M1 协议边界

`M1` 共享协议只保留两层：

- `Core Envelope`
- `Optional Game Extensions`

`M1` 的 `Core Envelope` 只要求以下字段：

- `gameId`
- `requestId`
- `traceId`
- `launchSessionId`
- `identityContext`
- `simulationContext`
- `payload`

其中：

- `identityContext` 在 `M1` 只要求最小集合：`saveId/worldId`、`actorId`、`subjectRefs[]`
- `simulationContext` 在 `M1` 只要求最小集合：`stateVersion`、`inGameTimestamp` 或同等游戏时钟
- `M1` 的三款首发游戏还必须额外实现 `Typed Launch Profile`
- 其余字段优先放入 `extensions.<gameId>.*`

`M1` 明确不把以下内容作为统一最低协议：

- `partyId`
- `factionId`
- `sceneId/locationId`
- 通用 `WorldSnapshot`
- 通用 `WorldDelta`
- 通用 `control_intent` 命令

### 6.4 M1 Typed Identity / Time Profile

`M1` 不引入完整 `Identity Graph` 和完整 `Temporal Model`，但对三款首发游戏必须增加一个更明确的最小 profile。

`M1 Typed Identity Profile` 最少包含：

- `saveId/worldId`
- `actorId`
- `objectId`
- `controllerId`
- `ownerId`
- `locationId|zoneId`
- `subjectRefs[]`

规则如下：

- `objectId` 与 `controllerId` 进入 `M1`，是为了覆盖首发游戏的最小多主体语义
- `partyId`、`factionId`、`world graph` 仍留在 `M2+`
- `subjectRefs[]` 必须是 typed refs，不允许只是无类型字符串集合
- `ownerId` 与 `locationId|zoneId` 作为 launch-game typed profile 的正式支持字段进入 `M1`
- 三款首发游戏都必须映射到同一最小 typed identity profile，不允许某一款先引入更深一层的共享身份模型

`M1 Time Profile` 最少拆成两层：

- `causalCursor`
- `gameClock`

规则如下：

- `causalCursor` 负责排序、因果和回放
- `gameClock` 负责游戏语义时间
- `gameClock` 在 `M1` 至少允许两种 profile：`calendar`、`tick`
- `星露谷物语` 可优先使用 `calendar`
- `环世界` 可优先使用 `tick`
- `鬼谷八荒` 可按 game-local profile 实现 `session/world` 风格时钟，但仍需映射到 `causalCursor`
- 时钟映射差异只代表 adapter 层差异，不代表某一款游戏在 `M1` 先获得更深的 world 或 control profile

### 6.5 M1 命令模型

`M1` 只支持两类命令：

- `render_command`
- `transactional_command`

`M1` 不支持：

- `coarse_queued_action_command`
- `control_intent`
- `supersede`
- `cancel queue`
- 长生命周期 `desiredStateVersion` 协调
- 平台级边缘执行状态机

这意味着：

- `M1` 的命令结果要么成功、要么失败、要么明确可重试
- 不做跨多秒、多阶段的通用控制平面
- `M1` 的首发三款游戏共用同一命令白名单，不允许某一款先引入额外命令类型
- `M1` 的 NPC 创造物品落在受限的 `transactional_command` 中：只允许基于现有模板生成礼物实例，并写入 AI 名称与描述
- 不允许引入完整 `supersede/cancel/replay` 状态机
- `M1` 明确禁止 gameplay-authoritative action execution
- `M1` 不允许把“创造物品”扩张成开放式全物品生成、复杂制造链或平台级物品系统

### 6.6 M1 Facet 策略

`M1` 不建设完整 `Facet Registry`。

`M1` 只允许以下两类语义：

- 少量稳定公共字段
- 单游戏扩展字段

若确实需要中层语义，只允许保留极少数已被首发游戏证明必需的 facet，例如：

- `conversationFacet`
- `relationshipFacet`
- `inventoryFacet`

其余 facet 全部延后。

### 6.7 M1 Runtime 与 Game Integration Profile

`M1` 保持 `Launcher + Local Runtime` 结构，但收紧目标：

- `Runtime` 首先是本地 AI 会话运行时，不是通用平台内核
- `Launcher` 首先是产品入口和运行控制器，不是未来完整 control plane

`M1` 要求：

- 支持单游戏活跃会话
- 支持最小本地隔离
- 支持崩溃后受控恢复

`M1` 不要求：

- 真正的 UI / Runtime 强解耦
- 同会话自动重建 Runtime
- 多层执行权威拆分

推荐直接写死为：

- `M1` 是“单桌面产品 + 本地运行时协作架构”
- 不再宣传“UI 重启不影响 Runtime”这类超过现阶段实现能力的语义

`M1` 必须新增 `Game Integration Profile` 作为宿主级一等对象。

每个首发游戏都必须有自己的 `Game Integration Profile`，至少包含：

- loader / prerequisite rules
- install path
- mod path
- save path
- log path
- runtime health checks
- capability allowlist
- dialogue presentation binding
- gift trigger mapping
- gift template sourcing rule
- gift delivery flow
- degraded startup policy
- rollback target
- recovery entry
- fault message templates
- per-game launch contract

`M1` 明确允许：

- `game-local protocol/profile`
- `game-local vocabulary / trigger mapping`
- `game-local capability exceptions`

`M1` 明确禁止：

- 把这些例外直接升级成平台默认 schema
- 先为三款首发游戏建立统一 `World Profile`
- 让某一款首发游戏在 `M1` 先拥有超出基础版的 world / command 深度

其中必须补充一条例外规则：

- `M1` 禁止共享 `WorldSnapshot / WorldDelta`
- `M1` 也不允许任一首发游戏把单游戏 world read profile 变成首发门槛
- `M1` 允许三款首发游戏分别维护自己的 launch contract、capability matrix 和 degraded mode 列表，但这些差异只服务基础版交付与恢复，不服务协议加深

### 6.8 M1 授权模型

`M1` 的授权模型重构如下：

- 首次启动或未验证设备：必须在线
- 已验证设备：允许有限离线宽限
- 已启动会话：允许短时刷新失败宽限
- 明确撤销、完整性失败、安全失败：仍然 `fail-closed`

`M1` 推荐改为：

- `Runtime Access Token` 生命周期可适度拉长
- 已验证设备持有本地授权快照
- `Launcher` 刷新失败时，不立即打断已启动会话
- 只有到达受控宽限上限后，才拒绝新的 AI 请求

`M1` 本地链路只保留两层凭证：

- `Runtime Access Token`
- `runtimeSessionSecret`

`M1` 不启用：

- `Hosted Capability Invocation Token`
- 完整 reservation / commit / release / compensation 账本

同时必须写死：

- 三款首发游戏在 `M1` 全局关闭托管高级能力
- `M1` 不展示 premium 入口
- `M1` 不做 premium preflight
- `M1` 不做 premium quote
- `M1` 不做 premium 计次

### 6.9 M1 恢复、诊断与局部隔离

恢复能力必须从“附属能力”提升为 `M1` 一等能力。

`M1` 必做：

- 最近一次成功运行配置快照
- 升级前恢复点
- `Runtime` 崩溃后的 guided recovery
- 授权刷新失败后的状态快照
- 一键导出诊断包
- `per-game health state`
- `per-game quarantine`
- `per-game diagnostics / recovery UI`

`M1` 的诊断包至少应包含：

- `Launcher Log`
- `Runtime Log`
- `Mod Log`
- 最近一次授权决策快照
- 最近一次失败请求 ID
- 版本信息
- 当前运行配置摘要
- 按游戏的 readiness 状态
- 按游戏的 quarantine 状态
- 按游戏的最近一次成功配置快照
- 按游戏的 capability allowlist 与当前降级状态

`M1` 不做：

- 五层全平台日志治理
- 细粒度问题域动态诊断开关
- 完整 Hosted AI 不可变审计体系

宿主级局部失败治理必须固定如下：

- 单游戏 integration/profile 崩溃时，只允许该游戏被局部禁用
- 单 capability 不兼容时，允许启动游戏，但禁用该 capability
- 单游戏运行配置损坏时，允许回退到最近一次成功配置
- 单游戏升级失败时，只隔离该游戏，不阻断其他两款启动与恢复
- 宿主 UI 必须提供按游戏分开的 readiness、诊断、恢复、quarantine 和 capability 禁用状态

### 6.10 M1 版本策略与启动前检查

`M1` 只保留少数硬兼容线：

- `Launcher + Runtime bundle version`
- `Mod protocol major`
- `mod-state schema major`

`M1` 不把以下内容放到启动前硬门槛：

- `Facet schema`
- `World graph schema`
- `Command semantics schema`
- 大量 `requiredCapabilities`

`M1` 允许：

- `N-1` 受支持回滚窗口
- “安全最低版本”与“推荐升级版本”分离

`M1` 还必须新增 `Game Readiness Checks`。

每个首发游戏都至少要有：

- loader / prerequisite check
- install path / save path / log path check
- adapter version check
- capability allowlist check
- degraded startup policy
- per-game launch contract check

这些检查必须是：

- 游戏级阻断或降级
- 不得默认升级成平台级阻断

`M1 manifest compatibility override` 固定如下：

- `supportedFacetSchemas`：可缺省，可忽略，不得作为启动阻断条件
- `supportedWorldGraphSchemas`：可缺省，可忽略，不得作为启动阻断条件
- `supportedCommandSemantics`：可缺省，可忽略，不得作为启动阻断条件
- `requiredCapabilities`：在 `M1` 只允许用于单游戏 capability allowlist，不得作为平台级启动协商

`M1 per-game launch contract` 至少声明：

- `supported command classes`
- `required adapter capabilities`
- `rollback target`
- `known degraded modes`
- `integration tier`

## 7. M1 禁做清单

以下内容一律不进入 `M1`：

- 通用 `WorldSnapshot / WorldDelta`
- 通用 `Control / Intent Command`
- 通用 `Hosted Capability Service`
- 完整平台托管计费账本
- 完整 `Facet Registry`
- 启动前大范围 schema 协商
- 真正后台常驻 agent
- 跨多游戏类型的一体化世界模型
- gameplay-authoritative action execution
- 开放式全物品生成
- 复杂制造链
- 平台级统一物品系统
- premium 托管能力入口
- premium preflight / quote / 计次

若团队讨论中出现“为了以后省事，M1 先做进去”，默认结论应为：

- 不做

除非能证明该项能力是首发主游戏无法上线的阻塞项。

### 7.1 Three-Game Launch Clarification

三款首发不等于把 `M1` 提升成通用跨类型平台。

必须写死以下原则：

- 三款首发的差异由 `per-game launch profile` 兜底
- 三款首发的差异主要落在对话表现、礼物模板来源、触发映射与发放路径
- 三款首发不把 `World Profile` 拉回 `M1`
- 三款首发不把 `Control Profile` 拉回 `M1`
- 三款首发不把完整 `Facet Registry` 拉回 `M1`
- 三款首发可以上调宿主治理，但不能上调平台抽象

## 8. M2 设计

### 8.1 M2 启动条件

只有在以下条件满足后，才进入 `M2`：

- `M1` 的三款首发游戏已稳定交付
- 已有明确用户价值与留存信号
- 已沉淀真实故障模型和恢复经验
- 至少已有 `2` 种 launch profile 被验证稳定
- 至少出现 `2` 个游戏对某些共享语义有重复需求

### 8.2 M2 新增目标

`M2` 重点建设：

- 分层协议 profile
- 最小 `Capability Host ABI`
- 有限 `Facet` 升级
- 有限 world profile
- 有限 command profile
- 有限托管能力入口
- 在共享基础版稳定后，再允许三款首发游戏分别进入自己的加深轨道

### 8.3 M2 协议分层

从 `M2` 起，协议改成四档：

1. `Core Event / Command`
2. `Typed Identity / Time`
3. `World Profile`
4. `Control Profile`

规则如下：

- 所有游戏都必须满足第 `1` 档
- 大多数游戏需要满足第 `2` 档
- 只有特定类型游戏才需要第 `3` 档
- 只有明确需要持续控制语义的游戏才需要第 `4` 档

### 8.4 M2 Facet 策略

`M2` 开始允许 `Facet`，但必须分层：

- `stable`
- `experimental`
- `game-local`

只有同时满足以下条件的语义，才可升入 `stable`：

- 被至少 `2` 个游戏复用
- 被至少 `2` 个 capability 依赖
- 被 replay 或恢复流程证明稳定

### 8.5 M2 World 策略

`M2` 才允许引入 `WorldSnapshot / WorldDelta`，但必须 profile 化：

- 不是所有游戏默认启用
- 只允许 `1` 种 snapshot 基础格式
- 只允许 `1` 种 delta 语义
- 先在确实需要大世界同步的游戏里试点

### 8.6 M2 命令策略

`M2` 才允许引入有限的可取消队列命令。

但仍不默认开放完整 `control_intent` 平台控制面。

`M2` 的命令增强必须满足：

- 有清晰 ack 超时策略
- 有状态漂移收敛策略
- 有单游戏实证需求
- 若某一款首发游戏需要 `coarse_queued_action_command` 或其他更重命令类型，也只能从这里开始进入受控试点

## 9. M3 设计

### 9.1 M3 启动条件

只有在以下条件满足后，才进入 `M3`：

- 已有多款不同类型游戏稳定接入
- `M2` 的 profile 机制已被验证
- 已有明确复用事实，而非理论需求
- 托管能力已成为真实商业主线

### 9.2 M3 新增目标

`M3` 可以正式建设以下平台能力：

- 完整 `Capability Host ABI`
- 完整 `Hosted Capability Service`
- 完整托管计费状态机
- 完整 `World graph schema`
- 完整 `Control Plane`
- 完整平台日志与审计治理

### 9.3 M3 设计原则

`M3` 的核心原则不是“把所有游戏差异都吸进平台”，而是：

- 只把已被证明稳定复用的部分平台化
- 继续保留 adapter、profile、capability 的隔离边界
- 继续允许局部禁用、局部降级、局部恢复

## 10. 对现有 spec 的 Redline

### 10.1 对主技术栈设计的 Redline

以下内容从“`M1` 默认要求”改为“`M2+` 可选能力”：

- 完整 `Capability Host` 最小 ABI
- `Hosted Capability Invocation Token`
- 完整托管计费状态机
- 平台级成本核算报表与供应商对账
- 启动前多层 schema 协商
- 大范围 capability requirements
- premium preflight / quote / 计次入口
- gameplay-authoritative action execution
- 开放式全物品生成
- 复杂制造链
- 平台级统一物品系统

以下内容从“附属能力”改为“`M1` 必做能力”：

- 恢复点
- 运行配置快照
- 授权决策快照
- guided recovery
- `per-game readiness checks`
- `per-game quarantine`
- `per-game recovery entry`
- `per-game launch profile`
- 三款首发游戏的原生风格对话表现
- 三款首发游戏的基础记忆链路
- 三款首发游戏基于现有模板的 NPC 创造物品

### 10.2 对跨游戏扩展设计的 Redline

以下内容从“统一最低门槛”改为“按档位启用”：

- `Identity Graph`
- `Temporal Model`
- `WorldSnapshot / WorldDelta`
- `Facet Registry`
- `Command Lifecycle & Execution Plane`

其中：

- `Identity / Time` 保留最小核心
- `World / Command / Facet` 延后并 profile 化
- `Identity / Time` 在三款首发中允许提升到 `Typed Launch Profile`
- `M1` 不要求 world read model；任一单游戏 world read model 若确有必要，也默认归入 `M2` 的 game-local profile
- 任一首发游戏想先做更深的上下文读取、额外命令类型或更重的 game-local profile，默认归入 `M2`

## 11. 推荐落地顺序

最稳的执行顺序如下：

1. 先把 `M1` 文档边界写死
2. 先把三款首发统一成同一条基础版能力验收线
3. 先删掉 `M1` 禁做项
4. 先补恢复和授权宽限
5. 先收缩版本与协商面
6. 先建立共享底座，再让三款游戏 adapter 并行接入
7. 先建立 `M1` 轻量成本归因口径
8. 再为 `M2` 预留 profile 扩展点

不推荐的顺序如下：

1. 先做完整跨类型协议
2. 先做完整托管计费平面
3. 先做完整 facet/world/control 平台治理
4. 再回头补恢复

这会让平台先变重，再发现产品还没稳定。

## 12. 最终结论

这份重构版的核心结论只有一句：

- `M1` 先做一个不比 `Vortex` 更脆的产品

更具体地说：

- `M1` 先做薄宿主、小协议、强恢复、软降级
- `M1` 先让三款首发游戏都交付同一标准的基础版
- `M2` 再做分层扩展和协议 profile
- `M3` 最后再做真正的平台化内核

如果后续讨论再次出现“为了未来扩展，先把它做成平台硬约束”，默认应回到本文档检查：

- 这是不是 `M1` 不该承担的成本
