# RETIRED REFERENCE - Superpowers 玩家前台 Launcher 设计

> 本文已退居辅助参考，不再是当前正式设计真相。  
> 当前正式入口：`docs/superpowers/specs/2026-03-27-superpowers-master-design.md`  
> 当前正式附件：`docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`

# Superpowers 玩家前台 Launcher 设计

## 1. 文档定位

本文档定义 `All Game In AI / superpowers` 的：

- `玩家前台 Launcher`

它承接以下上位与配套文档，但不替代它们：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-03-27-superpowers-platform-control-plane-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-superpowers-client-runtime-topology-appendix.md`
- `docs/superpowers/governance/current-phase-boundary.md`
- `docs/superpowers/governance/client-exposure-threat-model.md`

本文档只回答以下问题：

- 玩家桌面端 Launcher 到底应该有哪些页面
- 每个页面主要做什么，不做什么
- 玩家前台如何保持：
  - 简洁
  - 友好
  - 状态清晰
  - 出问题时可快速求助
- Launcher 如何和平台控制面形成：
  - 问题提交闭环
  - 通知回执闭环
  - Key 兑换与权益生效闭环

若与当前阶段硬边界冲突，仍以：

- `docs/superpowers/governance/current-phase-boundary.md`

为准。

## 2. 设计目标

本设计的目标不是做一个“功能尽量多”的启动器，而是做一个：

- `普通玩家敢用、愿意用、用得明白`

的桌面产品入口。

目标如下：

1. 让玩家第一眼就知道：
   - 现在能不能玩
   - 下一步该按哪里
2. 让玩家在一个统一入口中完成：
   - 登录
   - 选游戏
   - 启动
   - 看状态
   - Key 兑换
   - 求助
3. 让复杂的运行态、诊断、恢复、后台闭环：
   - 存在
   - 可用
   - 但不要压到玩家脑子里
4. 让 Launcher 与平台控制面形成稳定闭环，而不是各说各话。

## 3. 顶层原则

### 3.0 UI/UX Basis

当前 Launcher 设计固定采用 `ui-ux-pro-max` 作为玩家可见 UI hard gate 依据。

本方案的明确 basis 如下：

- visual direction：
  - `状态优先的桌面驾驶舱`
  - `外层轻，内层深`
  - `按游戏组织，不按系统分层组织`
- accessibility：
  - 所有主要动作必须支持键盘导航
  - 所有 icon-only 或状态型入口必须有可读标签
  - 失败、空态、延迟态不得只用颜色表达
  - 主要文本与状态标签必须满足可读对比度要求
- responsive：
  - 必须至少覆盖桌面常规宽度与窄窗口收缩场景
  - 首页、游戏页、支持页都不得因宽度收缩而丢失主 CTA、主状态或主恢复入口
- interaction / feedback：
  - 启动、检查、修复、提交问题、刷新权益都必须有显式 loading / success / failure feedback
  - 主 CTA、convenience action、authoritative recovery path 必须可区分
- empty / failure / delayed / recovery surfaces：
  - `无记录`
  - `未开放`
  - `检查中`
  - `刷新失败`
  - `离线`
  - `已隔离`
  - `只提交文字说明`
  都属于必须显式设计的玩家可见 surface，不得留给实现时自由发挥

### 3.1 玩家只看结论和按钮

玩家前台不负责教育玩家理解：

- provider
- orchestration
- capability claim artifact
- runtime truth-source
- 降级链路内部原因

玩家只需要看到：

- 当前结论
- 对他有什么影响
- 推荐按哪个按钮

### 3.2 外层轻，内层深

Launcher 必须是：

- `外层轻`
- `内层深`

含义如下：

- 外层首页、产品页、通知页必须简单
- 按游戏工作区可以更深，但仍要保持玩家可读
- 真正复杂的技术与运营真相源仍留在平台控制面

### 3.3 按游戏组织问题，不按系统分层组织

玩家思考问题的方式通常不是：

- Launcher 出了问题
- Runtime 出了问题
- Adapter 出了问题

而是：

- `星露谷为什么今天不能正常玩`

因此前台的主要问题入口必须优先按：

- `gameId`

组织。

### 3.4 支付外置，兑换内置

`M1` 不在 Launcher 内承载支付流程。

产品购买通过外部平台完成：

- `https://pay.ldxp.cn/`

Launcher 内只承载：

- 产品介绍
- 外部购买跳转
- Key 输入与兑换
- 权益结果查看

### 3.5 平台闭环必须完整，但不要暴露后台感

玩家前台必须和平台控制面形成闭环，但闭环应表现为：

- 消息
- 状态
- 按钮
- 结果

而不是：

- 后台术语
- 后台页面镜像
- 运营字段直出

## 4. Launcher 在整体架构中的职责

根据上位设计，玩家前台 Launcher 更偏：

- `Host Governance Core` 的玩家可见入口

客户端运行拓扑、`Runtime` 边界、`Mod` 分工与服务器协作方式，不在本文展开定义，统一以：

- `docs/superpowers/specs/attachments/2026-03-27-superpowers-client-runtime-topology-appendix.md`

为准。

它主要负责：

- 登录与账号状态承接
- 游戏入口与启动控制
- 玩家可见 readiness / running / issue 状态展示
- Key 兑换与权益结果承接
- 问题提交与支持入口
- 通知接收与处理结果回执

它不负责：

- entitlement policy 真源
- sellability policy 真源
- narrative orchestration 真源
- prompt / provider / 会话审查后台能力
- 完整技术诊断内部面

补充规则：

- Launcher 当前所有玩家可见状态文案，都必须是 `launchReadinessVerdict`、runtime health fact、quarantine state、recovery entry 的派生展示
- `可启动 / 运行中 / 需修复 / 需更新 / 已隔离` 只允许作为 display label，不得形成第二套 readiness truth source
- 主 CTA 与 display label 必须来自同一份 `launchReadinessVerdict`
- 桌面端不得自己发明“看起来差不多”的 ready / blocked 判定表
- `readiness policy` owner 固定在 lower contracts，不在 Launcher UI
- Wave 1 前，Launcher 只允许消费以下 deterministic verdict inputs：
  - `launchReadinessPolicySnapshot`
  - `capabilityAccessDecision`
  - `runtimePreflightFact`
  - `runtimePreflightRef`
  - `runtimeHealthFact`
- `launchReadinessVerdict` 只允许由上述 inputs 派生

### 4.1 玩家可见 Verdict Contract

桌面端固定只消费以下 player-visible verdict：

- `ready`
- `running`
- `needs_repair`
- `needs_update`
- `isolated`
- `blocked`

它们与主 CTA 的对应关系固定为：

| Verdict | 主 CTA | 说明 |
| --- | --- | --- |
| `blocked` | `查看原因` | 无启动权限，且必须指向同一条 denied reason surface |
| `isolated` | `打开修复` | 仅次于 `blocked`，禁止启动 |
| `needs_repair` | `打开修复` | 高于更新与启动 |
| `needs_update` | `立即更新` | 禁止直接启动旧版本 |
| `running` | `继续运行` | 当前会话已存在 |
| `ready` | `启动游戏` | 可正常启动 |

冲突裁决规则固定为：

1. `blocked`
2. `isolated`
3. `needs_repair`
4. `needs_update`
5. `running`
6. `ready`

补充规则：

- `blocked` 必须进入首页主卡、游戏状态总览、游戏列表、运行 Tab 的固定展示集合
- `blocked` 的主 CTA 固定为 `查看原因`
- `查看原因` 必须深链到同一 denied reason surface；该 surface 内再提供 `联系支持`
- denied reason surface 只允许消费 server-signed access decision / disclosure result
- 一旦最新成功刷新得到 `blocked` verdict，任何旧的 `启动游戏 / 继续运行` CTA 都不得继续显示

任何首页卡片、游戏列表、工作区顶部、待处理事项都必须复用同一裁决顺序。

## 5. 一级导航信息架构

主导航固定为：

1. `首页`
2. `游戏`
3. `产品与兑换`
4. `通知`
5. `支持与帮助`
6. `设置`

全局固定入口建议如下：

- 右上角常驻：
  - `兑换 Key`
- 同区展示：
  - 账号状态
  - 通知提醒

## 6. 页面设计

### 6.1 首页

首页必须是：

- `状态优先的驾驶舱`

而不是：

- 内容堆叠页
- 活动促销页
- 技术监控页

首页固定分为以下区块：

#### 6.1.1 顶部总状态条

至少展示：

- 当前账号状态
- 当前权益状态摘要
- 云端连接状态摘要
- Launcher 版本
- 是否存在待处理问题

#### 6.1.2 最近游戏主卡

这是首页最重要的大卡片，至少提供：

- 最近玩的游戏
- 当前状态
- 主按钮：
  - `启动游戏`
  - 或 `继续运行`
  - 或 `立即更新`
  - 或 `打开修复`
  - 或 `查看原因`
- 最近一次问题摘要

补充规则：

- 主卡不得自己重算主 CTA
- 主卡只消费当前 `launchReadinessVerdict`
- 若 verdict 正在刷新，保留旧 verdict 与旧 CTA，只叠加 `检查中` overlay 并禁用按钮

#### 6.1.3 游戏状态总览

按游戏展示简明状态卡。

每张卡至少包括：

- 游戏名
- 当前状态：
  - `可启动`
  - `运行中`
  - `需修复`
  - `需更新`
  - `已隔离`
  - `不可用`
- 一条问题摘要

补充说明：

- 上述状态标签只是玩家可读 display label
- 它们必须能回链到当前 `launchReadinessVerdict` 与 runtime health / quarantine facts

异步状态固定为：

- `检查中`
- `刷新中`
- `提交中`
- `离线`
- `超时`

这些状态属于 UI interaction state，不得替代 authoritative verdict。

异步状态与 CTA 的关系固定为：

- 异步状态只作为 overlay / badge / disabled reason 存在
- 异步状态不得替换当前 verdict
- 异步状态不得生成第二套 CTA
- 异步期间沿用当前 verdict 对应的 CTA 文案，但按钮可被临时禁用
- 但若最新成功 verdict 已是 `blocked`，异步状态不得恢复旧的可启动 CTA

#### 6.1.4 待处理事项

只放需要玩家动作的事项，例如：

- 兑换后待刷新
- 启动失败待修复
- 版本不兼容
- 有新的官方回复

每个事项都必须提供：

- 一句人话说明
- 一个主按钮

#### 6.1.5 通知摘要

展示最近的重要通知与个人消息摘要。

优先级必须是：

1. 影响使用的消息
2. 处理结果回执
3. 一般更新通知

#### 6.1.6 快捷入口

至少保留：

- `兑换 Key`
- `查看产品`
- `提交问题`
- `打开帮助`

### 6.2 游戏

`游戏` 页是玩家主操作面。

页面结构建议固定为：

- 左侧：游戏列表
- 右侧：当前游戏工作区

#### 6.2.1 左侧游戏列表

每个游戏条目只显示：

- 游戏名与封面
- 当前状态
- 一条问题摘要
- 次级标记：
  - `有通知`
  - `需更新`
  - `可修复`

左侧不应堆放大量按钮。

#### 6.2.2 右侧工作区

工作区顶部至少提供：

- 当前游戏名
- 当前状态
- 主操作按钮
- 简短状态说明

工作区下方使用 `Tab`，固定为：

1. `概览`
2. `运行`
3. `帮助与修复`
4. `游戏设置`

#### 6.2.3 概览 Tab

用于展示玩家最常用信息。

至少包括：

- 大启动按钮
- 当前状态卡
- 最近通知
- 最近问题摘要
- 快捷入口：
  - `打开帮助与修复`
  - `查看设置`

#### 6.2.4 运行 Tab

这里只展示玩家能理解的运行结果，不展示系统内情。

至少包括：

- 当前 verdict
- 当前主 CTA
- 当前状态摘要
- 最近一次刷新时间
- 最近问题摘要

不应直接展示：

- provider 内部状态
- capability claim 工件
- 技术性降级细节
- 服务端编排术语

必须额外展示以下结果态：

- 当前 verdict
- 当前主 CTA
- 最近一次刷新时间
- 若正在检查：显示 `检查中`
- 若刷新失败：显示 `刷新失败，可重试`
- 若离线：显示 `当前离线，无法刷新状态`

#### 6.2.5 帮助与修复 Tab

该页不是技术诊断面，而是玩家求助面。

至少包括：

- 最近出现过什么问题
- 推荐操作
- 一键修复
- 重新检查
- 导出并提交问题
- 联系支持入口

原则如下：

- 只展示玩家能理解的问题结论
- 只给少量明确动作
- 不要求玩家自己翻日志排查
- 页面内的重试 / 刷新按钮只属于 convenience action，不构成第二套 recovery authority
- authoritative player-visible recovery path 仍固定为：
  - `游戏 -> 帮助与修复`
  - `支持与帮助`

动作路由固定为：

- 首页 / 游戏页 / 快捷入口上的 `打开修复`
  - 一律深链到 `游戏 -> 帮助与修复`
- `支持与帮助`
  - 作为跨游戏恢复面
- 页面内的 `一键修复 / 重新检查`
  - 只执行 convenience action
  - 执行失败后仍回到上述 authoritative recovery path

#### 6.2.6 游戏设置 Tab

仅承载当前游戏的本地设置。

至少包括：

- 游戏路径
- 启动方式
- 当前版本/兼容性摘要
- 少量玩家偏好项

不应塞入：

- 账号设置
- 全局下载策略
- 平台运营字段

### 6.3 产品与兑换

该页不是内嵌商城，而是：

- `产品介绍 + 外部购买引导 + Key 兑换入口`

固定分为以下区块：

#### 6.3.1 产品介绍

每张产品卡只讲清：

- 产品名称
- 适合谁
- 包含什么
- 为什么值得买

不使用复杂商业术语。

补充规则：

- 产品卡、listing 文案、可见权益状态只允许消费 server-signed listing / entitlement result
- Launcher 不得缓存或推导第二套 sellability / entitlement truth
- `基础包-BYOK` 与 `基础包-托管` 在玩家前台必须作为两条分离产品路径展示，不得合并成单一“基础包”文案

#### 6.3.2 前往购买

明确提示：

- 购买将在浏览器中打开外部平台完成

购买入口固定指向：

- server-signed `purchaseRouteInstruction`

当前默认期望仍可指向：

- `https://pay.ldxp.cn/`

规则：

- Launcher 不硬编码购买 URL
- Launcher 只消费 server-signed `purchaseRouteInstruction`
- 当前 route instruction 失效、停售、风控阻断或 billingSource 变化时，Launcher 不得继续沿用旧购买入口
- 默认使用系统浏览器打开，不内嵌支付页

#### 6.3.3 Key 兑换

必须是该页主功能之一，并在全局右上角保留快捷入口。

至少包括：

- Key 输入框
- `立即兑换` 按钮
- 兑换结果反馈

规则：

- 兑换结果只允许展示 server-signed redemption result
- 失败结果也必须来自 server result，不得由 Launcher 本地猜测 entitlement 生效状态

#### 6.3.4 我的权益

只展示玩家关心的结果：

- 当前已激活什么
- 是否生效
- 关联哪些游戏或能力
- 何时激活

若出现问题，只给：

- `刷新权益`
- `提交问题`

补充规则：

- `我的权益` 只允许展示 server-signed entitlement visibility result
- `刷新权益` 只允许重新拉取 signed result，不允许本地重算 entitlement
- `关联哪些游戏或能力` 必须来自 signed payload，不得由客户端文案层自由拼装
- 必须显式区分：
  - `BYOK` entitlement
  - `platform_hosted` entitlement

### 6.4 通知

`通知` 页是平台消息的玩家前台承接面。

建议拆成 3 个 Tab：

1. `重要通知`
2. `我的消息`
3. `更新记录`

#### 6.4.1 重要通知

用于承接：

- 维护通知
- 已知问题
- 大范围恢复说明
- 重要产品变更

#### 6.4.2 我的消息

用于承接：

- 兑换结果
- 问题处理回执
- 权益变更
- 个性化通知

#### 6.4.3 更新记录

用于承接：

- 新版本内容
- 修复说明
- 受影响游戏说明

### 6.5 支持与帮助

这是 Launcher 中和平台控制面闭环最强的页面。

它用于：

- 提交 bug
- 提交问题包
- 查看问题处理状态
- 获取帮助

建议固定分为以下区块：

#### 6.5.1 我现在遇到的问题

自动列出最近检测到或最近提交过的问题。

每条问题只展示：

- 一句人话描述
- 当前状态
- 一个推荐操作

#### 6.5.2 一键帮助

至少包括：

- `一键修复`
- `重新检查`
- `提交问题`
- `查看帮助文档`

补充规则：

- `支持与帮助` 页内的 `一键修复 / 重新检查` 也只属于 convenience action
- 执行失败后必须回到同一 authoritative recovery path：
  - `游戏 -> 帮助与修复`
  - `支持与帮助`

#### 6.5.3 提交问题

必须支持：

- 一键提交问题
- 自动附带必要诊断信息

不要求玩家手工收集多份日志。

提交问题的玩家可见合同固定为：

- 提交前必须展示：
  - 本次问题包会包含什么
  - 哪些内容已脱敏
  - 玩家是否允许上传必要诊断信息
- 默认成功上传载荷固定只允许包含：
  - `redacted digest`
  - `policy-safe metadata`
- 提交中必须显示：
  - `提交中`
- 提交成功必须显示：
  - `已提交`
  - `问题编号`
- 提交失败必须显示：
  - `提交失败`
  - `重试`
  - `稍后再试`
- 若用户关闭诊断上传：
  - 固定显示 `仅提交文字说明`
  - 固定显示 `继续只提交文本`

redaction / export 失败露出固定为：

- `脱敏失败，当前无法提交问题包`
- `导出失败，已保留本地最小错误摘要`
- support-path failureClass freeze：
  - `diagnostic_export_failed`
  - `diagnostic_redaction_failed`

并要求：

- 用户关闭诊断上传后，仍允许提交纯文本问题摘要
- 纯文本问题摘要不等于 full diagnostic path
- `脱敏失败` 或 `导出失败` 后，默认主按钮固定切换为：
  - `只提交文字说明`
- 上述“本地最小错误摘要”固定只允许包含：
  - `redacted digest`
  - `policy-safe metadata`
- 它仍必须受同一 redaction policy 约束，不得绕开 threat-model baseline 单独留盘

#### 6.5.4 我的问题记录

玩家可查看：

- 已提交
- 处理中
- 已回复
- 需要补充信息

这部分不需要大量字段，只要让玩家看得懂处理进度。

#### 6.5.5 常见问题

至少覆盖：

- 启动不了怎么办
- 更新后异常怎么办
- 购买后没生效怎么办
- 兑换失败怎么办

### 6.6 设置

`设置` 页必须克制，不做后台配置面。

建议拆成 4 个 Tab：

1. `账号`
2. `启动器`
3. `游戏管理`
4. `隐私与数据`

#### 6.6.1 账号

至少包括：

- 登录状态
- 账号绑定信息
- 退出登录

#### 6.6.2 启动器

至少包括：

- 自动更新
- 下载位置
- 基础界面偏好

#### 6.6.3 游戏管理

至少包括：

- 已安装游戏路径
- 扫描与重新识别
- 本地游戏资源状态摘要

#### 6.6.4 隐私与数据

至少包括：

- 问题包会包含什么
- 是否允许发送必要诊断信息
- 本地数据清理入口

并固定包括：

- 脱敏规则摘要
- 当前诊断上传开关状态
- 关闭诊断上传后的降级说明

## 7. 与平台控制面的闭环

Launcher 虽然是玩家前台，但当前 `M1` 只要求它与平台后端形成 server-result 闭环，不要求直接依赖 `Platform Control Plane` operator surface。

### 7.1 问题提交闭环

Launcher 的：

- `支持与帮助`
- `帮助与修复`

必须支持一键提交问题。

提交后，平台侧至少要能进入：

- `首页` 的待处理玩家事务
- `玩家` 页的反馈 / 异常记录
- 必要时 `会话审查 / AI记录`

Launcher 提交时建议自动附带最小必要信息：

- `playerId`
- `gameId`
- Launcher 版本
- 当前游戏版本摘要
- 最近失败时间
- 最近失败请求 ID 摘要
- 当前状态摘要
- 脱敏问题包引用

### 7.2 处理回执闭环

平台后端处理完问题后，结果必须能回到 Launcher。

建议通过：

- `运营通知 -> 个人通知`

回到 Launcher 的：

- `通知 / 我的消息`

必要时首页也显示摘要。

### 7.3 兑换与权益闭环

购买在外部发卡平台完成，Launcher 内的关键闭环是：

- `产品介绍 -> 外部购买 -> 输入 Key -> 兑换成功 -> 权益可见`

平台后端至少要承接：

- Key 兑换结果
- 关联玩家
- 生效产品
- 生效时间
- 失败结果

并在后端治理链中：

- `商业 / 额度`
- `玩家`

形成可追踪记录。

### 7.4 通知闭环

平台侧：

- `全体公告`
- `个人通知`

应分别映射到 Launcher 的：

- `重要通知`
- `我的消息`

### 7.5 异常线索闭环

Launcher 前台感知到的启动异常、兑换异常、支持异常，不应只是本地提示。

它们在平台侧应能沉淀为：

- 玩家异常记录
- 处理线索
- 必要时 trace 关联

但玩家前台只展示：

- 出现了什么问题
- 当前是否已处理
- 建议下一步动作

## 8. 不做什么

为保持玩家前台简单清晰，`M1` 明确不做以下内容：

- 不把运营后台直接做进 Launcher
- 不在前台展示 provider / prompt / orchestration 控制面
- 不在前台内嵌支付
- 不让玩家理解复杂降级原因链
- 不让玩家手工收集多份日志后再提交问题
- 不把“支持与帮助”做成工程术语面板

## 9. 最终结论

本设计的最终结论如下：

1. 玩家前台 Launcher 的一级导航固定为：
   - `首页 / 游戏 / 产品与兑换 / 通知 / 支持与帮助 / 设置`
2. 首页必须优先展示：
   - 当前状态
   - 最近游戏
   - 待处理事项
   - 快捷动作
3. 游戏页必须采用：
   - 左侧游戏列表
   - 右侧工作区
   - `概览 / 运行 / 帮助与修复 / 游戏设置` 四个 Tab
4. 购买流程默认在外部平台完成，Launcher 内只承接：
   - 产品介绍
   - 外部购买跳转
   - Key 兑换
   - 权益结果查看
5. `支持与帮助` 必须成为：
   - 问题提交
   - 问题包提交
   - 处理状态查看
   的统一入口
6. Launcher 必须与平台控制面形成：
   - 问题提交闭环
   - 通知回执闭环
   - 兑换与权益闭环
7. 整个前台必须坚持：
   - 玩家只看结论和按钮，不看系统内情
