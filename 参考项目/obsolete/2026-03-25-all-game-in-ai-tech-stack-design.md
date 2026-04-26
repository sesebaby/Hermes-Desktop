# All Game In AI 技术栈与授权架构设计

> Historical design note as of 2026-03-27.
>
> This document remains useful as a technology-options and early-boundary record, but parts of its deployment assumptions are now superseded by:
> - `docs/superpowers/specs/2026-03-26-superpowers-double-core-framework-design.md`
> - `docs/superpowers/governance/client-exposure-threat-model.md`
>
> In particular, do not treat this obsolete draft's "Local Runtime hosts the main AI orchestration / BYOK text path" wording as the current target architecture.
> The current target architecture is:
> - service-side narrative orchestration truth source by default
> - local deterministic execution and authoritative host writeback
> - `user_byok` and `platform_hosted` sharing one hosted orchestration path for base narrative capabilities
> - `AI voice` / `AI image` / `AI video` remaining `platform_hosted`

## 1. 文档定位

本文档是《All Game In AI 设计方案》的技术栈与授权架构补充设计，专门用于收敛第一阶段的实现技术路线、运行边界与商业化控制模型。

本文档不替代原始产品设计稿，重点回答以下问题：

- 第一阶段各子系统采用什么技术栈
- `Launcher`、`Runtime`、`Cloud Control`、`Game Adapter Mod` 的边界如何划分
- `Microsoft Agent Framework` 在整体架构中的角色是什么
- 平台如何通过强在线授权实现商业化控制
- 玩家自带模型 API Key、平台授权体系与平台托管高级能力如何并存

## 2. 设计目标

本文档的目标是为第一阶段落地提供一套明确、可实施、可商业化的技术选择，避免后续在实现阶段反复摇摆。

第一阶段技术设计优先级如下：

1. Windows 首发稳定性优先
2. 核心主栈尽量统一在 `C# + .NET`
3. AI 编排能力必须以 `Microsoft Agent Framework` 为核心
4. 游戏适配层不做强制统一，但必须统一协议边界
5. 商业化控制必须成立，不能默认永久离线可用

## 3. 总体技术栈结论

第一阶段推荐技术栈如下：

- 主语言：`C#`
- 主平台版本：`.NET 10 LTS`
- `Launcher`：`WPF`
- `Local Runtime` 宿主：`ASP.NET Core`
- `AI 编排核心`：`Microsoft Agent Framework`
- `Cloud Control Service`：`ASP.NET Core Web API`
- 共享协议层：独立 `Shared Contracts` 类库
- 云端数据库：`PostgreSQL`
- 本地记忆存储：`SQLite`
- 模型接入：基础文本链路支持玩家自填云模型 API Key；按次计费的高级能力属于平台托管 AI 能力，由服务端执行并单独计次
- 游戏适配层：各游戏沿用自身成熟 Mod 技术栈，通过统一协议接入

总体判断如下：

- `WPF` 用于保证 Windows 首发阶段的桌面稳定性与系统集成能力
- `ASP.NET Core` 作为本地 Runtime 的服务宿主和云端控制面的服务框架
- `Microsoft Agent Framework` 是 AI 核心，不应被降级为普通依赖包或边缘能力
- `Shared Contracts` 是平台真正的共享层，负责跨子系统的协议一致性

## 4. 子系统划分

第一阶段平台按以下五个主要子系统设计：

- `Launcher`
- `Local Runtime`
- `Cloud Control Service`
- `Hosted Capability Service`
- `Game Adapter Mods`

这五层职责不同，不应强行合并为一个抽象概念，也不应为了统一而牺牲各自边界清晰度。

## 5. Launcher 技术策略

### 5.1 选择 `WPF` 的原因

第一阶段 `Launcher` 采用 `WPF + .NET 10`，核心理由如下：

- 当前首发重点是 Windows，而不是跨平台 UI 复用
- `WPF` 在 Windows 桌面能力、文件系统、安装流程、进程拉起、路径处理、日志展示等方面更成熟
- 项目整体希望主栈统一在 `C# + .NET`，`WPF` 与这一目标一致

不选择 `Avalonia` 作为第一阶段默认方案的原因不是它不可行，而是当前阶段更重视首发稳定性而非未来 macOS Launcher UI 复用。

### 5.2 产品形态

从玩家视角看，第一阶段只有一个产品入口：

- 玩家只安装一个 `Launcher`
- 玩家只启动一个 `Launcher`
- 玩家不需要手动安装、管理或理解第二个后台产品

### 5.3 运行形态

第一阶段把运行拓扑明确为：

- 一个前台 `Launcher` 主进程
- 零到多个按 `gameId` 隔离的 `Runtime` 子进程
- 每个 `Runtime` 子进程只服务一个活跃 `gameId` 会话

这不是进程内宿主，也不是单个共享 Runtime 多路复用。`Launcher` 与 `Runtime` 的边界就是父子进程边界。

这样定的原因是：

- 支持不同游戏并发运行
- 某个 `Runtime` 崩溃时，不直接拖死 `Launcher` 或其他游戏
- 端口、令牌、日志、SQLite 数据都可以按游戏会话隔离

`Launcher` 负责：

- `WPF UI`
- 游戏启动编排
- 已运行游戏检测
- 各游戏 `Runtime` 子进程的拉起、监控、热更新与关闭

每个 `Runtime` 子进程负责：

- 对应 `gameId` 的本地 `ASP.NET Core` 宿主
- 对应 `gameId` 的 `Microsoft Agent Framework` AI 编排能力
- 对应 `gameId` 的令牌、端口、本地会话密钥与 SQLite 会话数据生命周期

### 5.4 Launcher 职责

`Launcher` 的职责包括：

- 登录与账号状态管理
- 强在线授权校验
- 激活码或兑换码输入入口
- 玩家模型 API Key 配置（用于玩家自带 Key 的文本链路）
- Mod 下载、安装、更新
- 启动游戏
- 启动并管理各游戏本地 Runtime 实例
- 显示日志、状态与设置

`Launcher` 不是游戏内 AI 请求的中转中心。它是玩家入口、控制台与本地运行控制宿主。

同时必须从现在开始把 `Launcher` 的两个角色拆清：

- `Launcher UI`：负责玩家可见界面、设置、日志查看、升级与提示
- `Launcher Supervisor`：负责拉起 `Runtime`、维护本地租约、热更新令牌、控制诊断开关和有序关停

这里的“拆清”是职责语义拆分，不等于必须做成云端微服务。`M1` 可以仍然作为同一个桌面交付物发布，但不得再把“前台窗口进程是否还活着”直接等同于 `Runtime` 的生死权威。

同时，第一阶段运行规则明确如下：

- 不同游戏允许并发运行
- 同一游戏暂不支持多开
- 同一 `gameId` 同时最多一个活跃 Runtime 实例
- 若玩家再次启动同一游戏，`Launcher` 只聚焦到已运行实例，不创建新实例
- `Launcher UI` 关闭、重启或崩溃，不自动等同于全部 AI 能力失效
- 只有 `Launcher Supervisor` 租约终止、显式 `shutdown` 或连续心跳超时，相关 `Runtime` 才进入失效流程
- 只要某个游戏对应的 `Runtime` 进程结束，该游戏的 AI 能力立即失效
- 同一玩家本地填写的模型 API Key 在不同游戏并发时共享同一供应商额度与计费，`M1` 不做按 Runtime 的额度隔离，只在 `Launcher` 中提供并发提示
- 平台托管的高级云能力不走这条本地 API Key 路径，而是由平台侧按功能类别分别计次

## 6. Launcher、Mod、Runtime 交互方式

### 6.1 总体交互原则

第一阶段采用以下交互关系：

- `Launcher UI` 面向玩家
- `Launcher Supervisor` 通过父子进程本地控制通道管理 `Runtime`
- `Mod` 面向 `Runtime API`
- 每个 `Runtime` 实例负责对应 `gameId` 的 AI 主链路

因此：

- `Mod` 不与 `WPF UI` 直接交互
- `Mod` 调用的是对应 `Runtime` 子进程暴露的本地接口
- `Runtime` 不在 `Launcher` 进程内，也不复用 `Launcher` 的 UI 线程或内存边界
- `Launcher UI` 可以重启，但不得因此直接触发 `Runtime` 失活
- `Launcher Supervisor` 才是本地租约与运行期控制的权威来源

### 6.2 通信方式

第一阶段推荐通信方式如下：

- `Mod -> Runtime`：`localhost HTTP JSON`
- `Launcher Supervisor -> Runtime`：父子进程匿名管道 + 本地命名管道
- `Launcher -> Cloud Control`：`HTTPS API`

选择该组合的原因：

- 跨游戏与跨技术栈兼容性更高
- 调试、抓包、排错成本更低
- `Mod` 侧始终面向统一 `HTTP JSON` 协议
- `Launcher` 的控制流量不需要再暴露第二套可被 Mod 访问的 `HTTP` 管理面

`localhost HTTP` 在 `M1` 只是本机传输方式，不是信任边界。

必须增加以下强制约束：

- `Runtime` 只能监听 `loopback` 地址，不得绑定外部网卡或通配地址
- `Runtime` 必须使用每次运行随机端口，不得使用固定众所周知端口
- `runtimeBaseUrl` 仅是发现信息，不构成授权凭证
- `Runtime` 必须将所有 `localhost` 调用方默认视为不可信
- 每个 `Mod -> Runtime` 请求都必须携带有效本地会话凭证
- `Launcher Supervisor` 在拉起 `Runtime` 后，必须先通过父子进程控制通道完成启动握手，握手成功前 `Runtime` 不得对 Mod 开放业务端口
- `Launcher Supervisor -> Runtime` 控制通道必须同时承担心跳租约职责
- `Launcher Supervisor` 必须每 `1` 秒发送一次心跳
- 若控制通道断开，或 `Runtime` 连续 `3` 秒未收到心跳，则 `Runtime` 必须在本地立即进入 `runtime_unavailable` 状态，并停止对 Mod 提供新的业务请求处理
- 上述心跳租约绑定的是 `Launcher Supervisor`，而不是任意前台 UI 窗口
- 一旦租约丢失，`Runtime` 只能停止接收新请求，并按 `drainDeadline` 有序收尾 in-flight 请求，不得直接把所有进行中操作静默中断
- 控制通道至少要支持 `bootstrap`、`heartbeat`、`refresh_token`、`shutdown`、`diagnostic_control` 五类消息
- 每条控制通道消息都必须带 `launchSessionId`、`launcherInstanceId`、`leaseEpoch`
- `bootstrap` 与 `shutdown` 还必须带 `traceId`
- `shutdown` 还必须带 `shutdownReason` 与 `drainDeadline`

### 6.3 运行流程

第一阶段典型运行流程如下：

1. 玩家打开 `Launcher`
2. `Launcher` 完成登录与强在线授权校验
3. 玩家从 `Launcher` 选择目标游戏
4. `Launcher Supervisor` 为本次游戏创建新的 `launchSessionId`、`launcherInstanceId` 与 `leaseEpoch`
5. `Launcher` 为该 `gameId` 与该 `deviceBindingId` 申请有效的 `Runtime Access Token`
6. `Launcher Supervisor` 拉起该游戏专属 `Runtime` 子进程，并通过父子进程控制通道下发 `Runtime Access Token`、`launchSessionId`、`runtimeSessionSecret`、`launcherInstanceId` 与 `leaseEpoch`
7. `Runtime` 完成本地验签、启动握手与心跳租约建立后，开始监听本次随机端口
8. `Launcher Supervisor` 原子性重写本次运行配置
9. `Launcher UI` 发起目标游戏启动
10. 游戏内 `Mod` 读取本次运行配置并连接对应游戏的 `Runtime`
11. `Mod` 将玩家输入、角色状态、地点与事件上下文发给该 `Runtime`
12. `Runtime` 使用 `Microsoft Agent Framework` 完成该游戏的编排、记忆与模型调用
13. `Runtime` 返回角色回复与相关结果
14. `Mod` 将结果渲染回游戏

第一阶段并发与恢复规则如下：

- 不同游戏可以同时运行，并各自连接自己的 Runtime 实例
- 同一游戏再次启动时，`Launcher UI` 只聚焦到已运行实例
- 只要某个游戏的 `Runtime` 实例结束，不论原因，该游戏当前 `launchSessionId` 立即失效
- 只要 `Launcher Supervisor` 租约终止，不论原因，相关 `launchSessionId` 立即失效
- `Launcher UI` 的关闭、重启或崩溃，不自动终止活跃 `launchSessionId`
- `Launcher` 不得在 `Runtime` 尚未就绪或运行期配置尚未写好的情况下启动游戏
- `M1` 不提供“游戏仍在运行时自动拉起 Runtime 并无感恢复”的能力
- 游戏内 `Mod` 一旦发现连接失败、`Launcher Supervisor` 已失活或 `Runtime` 已关闭，只提示玩家回到 `Launcher` 重新启动，不做自动恢复

### 6.4 Mod 发现 Runtime 的方式

第一阶段不做复杂服务发现。

由 `Launcher` 维护两类配置：

- 安装期静态配置
- 每次启动前重写的运行期配置

安装期静态配置只包含不会随每次运行变化的内容，例如：

- `gameId`
- `modConfigSchemaVersion`
- `supportedProtocolMajor`

运行期配置只包含当前这一次受支持启动所需的信息，例如：

- `runtimeBaseUrl`
- `protocolVersion`
- `launchSessionId`
- `runtimeInstanceId`
- `runtimeSessionSecret`

这里必须明确以下规则：

- 静态配置与运行期配置必须物理分离，不允许把 `runtimeSessionSecret` 写入安装期静态配置
- `gameId` 在 Mod 配置中只作为路由、适配器选择、兼容性判断和遥测元数据，不是收费判定权威来源
- 收费与准入的权威 `gameId` 只能来自 `Runtime Access Token`
- `runtimeInstanceId` 必须是每次 `Runtime` 成功 bootstrap 后生成的新 boot UUID，不得跨进程复用
- `Launcher` 必须在启动游戏前、且 `Runtime` 已就绪后，重写本次运行的运行期配置
- `runtimeSessionSecret` 是按 `launchSessionId` 生成的一次性本地会话密钥
- 上一次 `launchSessionId` 生成的 `runtimeSessionSecret` 在新一次启动前必须作废
- `Mod` 对每个 Runtime 请求都必须携带该密钥
- `Runtime` 必须拒绝缺失或无效密钥的请求
- 只要 `Launcher Supervisor` 租约失效或对应 `Runtime` 结束，当前 `runtimeSessionSecret` 就必须立即失效
- 运行期配置一旦对应的 `launchSessionId` 不再活跃，就必须视为陈旧配置
- `Runtime` 必须校验 `launchSessionId` 仍处于活跃租约中，否则必须拒绝
- `runtimeSessionSecret` 在 `Launcher Supervisor` 结束、对应 `Runtime` 结束、本次游戏会话结束或授权失效后立即失效
- `Launcher` 不得将云端授权令牌、可复用 bearer token、签名 key 或其他可复用授权材料写入 Mod 可读配置、日志、命令行、环境变量或注册表

`M1` 不尝试防御拥有同机、同用户态读配置能力的本地进程伪装成 Mod；该威胁不在 `M1` 商业化控制范围内。

### 6.5 启动入口规则

第一阶段 `Launcher` 是唯一受支持启动入口。

规则如下：

- 玩家应从 `Launcher` 启动受支持游戏
- 若玩家绕过 `Launcher` 直接启动游戏，Mod 不自动唤起 `Launcher`
- 非 `Launcher` 启动场景下，Mod 不提供可正常使用的 AI 功能
- 若运行期配置不存在、`launchSessionId` 不活跃、`Launcher Supervisor` 已关闭或 `Runtime` 不存在，Mod 只显示固定提示：“AI 功能已失效，请启动 Launcher 后重试”
- Mod 不负责自动接管或修复非 `Launcher` 启动场景

### 6.6 Mod 与 Runtime 协议约定

`M1` 必须统一 `Mod -> Runtime` 的响应信封，至少包含：

- `requestId`
- `success`
- `code`
- `message`
- `retryable`
- `runtimeInstanceId`
- `data`

`M1` 统一以下错误码：

- `launcher_start_required`
- `launch_session_inactive`
- `invalid_runtime_session_secret`
- `runtime_unavailable`
- `token_expired`
- `entitlement_denied`
- `maintenance_mode`
- `protocol_version_unsupported`
- `game_id_mismatch`
- `operation_recovering`

建议的 `HTTP` 语义如下：

- `200`：成功
- `400`：请求体错误或上下文字段缺失
- `401`：`invalid_runtime_session_secret`
- `403`：`token_expired`、`entitlement_denied`、`game_id_mismatch`
- `409`：`operation_recovering`
- `410`：`launcher_start_required`、`launch_session_inactive`
- `503`：`runtime_unavailable`、`maintenance_mode`
- `426`：`protocol_version_unsupported`

Mod 侧必须按以下规则处理：

- 收到 `runtime_unavailable` 或连接失败时，停止自动重试，只提示玩家启动 `Launcher`
- 收到 `launcher_start_required` 或 `launch_session_inactive` 时，停止重试，只提示玩家回到 `Launcher`
- 收到 `protocol_version_unsupported` 时，提示玩家通过 `Launcher` 更新 Mod
- 收到 `entitlement_denied`、`token_expired` 或 `maintenance_mode` 时，只展示原因，不自行猜测修复动作

### 6.7 重试、重连与幂等边界

`M1` 不提供无感自动重连，必须明确边界：

- 所有请求都必须带 `requestId`
- 会写记忆、会改游戏状态或会触发物品生成的请求，必须额外带 `operationId`
- `Runtime` 必须以 `operationId` 为键在本地落库去重，避免因崩溃重试导致重复副作用
- 同一 `operationId` 被重复提交时，`Runtime` 只能返回已知终态结果或 `operation_recovering`，不得重复执行副作用

`operationId` 的最小状态机必须固定为：

- `received`
- `executing`
- `completed`
- `failed_retryable`
- `failed_terminal`

状态规则必须固定如下：

- 请求刚进入 Runtime 且通过基础校验后，先落库为 `received`
- 真正开始执行副作用前，状态切到 `executing`
- 副作用和终态结果都成功落库后，才允许切到 `completed`
- 可由玩家重试的失败，切到 `failed_retryable`
- 不应再自动重试的失败，切到 `failed_terminal`

重复提交处理必须固定如下：

- 命中 `completed` 时，直接回放上次已落库结果
- 命中 `failed_terminal` 时，直接回放上次失败结果
- 命中 `received` 或 `executing` 时，返回 `operation_recovering`
- 命中 `failed_retryable` 时，只有在旧执行已明确结束后才允许重新进入新的执行轮次

持久化规则必须固定如下：

- `operationId` 的去重作用域必须是 `gameId + saveId/worldId + canonicalIdentityScope + operationId`
- `completed` 和 `failed_terminal` 记录至少保留 `24` 小时
- `Runtime` 启动时必须扫描处于 `received` 或 `executing` 的旧记录，并统一转成 `failed_retryable` 或可回放终态，不能永久卡在中间态
- 会写记忆或发放物品的结果，必须先持久化终态，再向 Mod 返回成功响应

这里必须明确：

- `operationId` 不能绑定到单次 `launchSessionId`
- `canonicalIdentityScope` 必须从 `identityContext` 计算，至少包含 `actorId`、`objectId`、`controllerId`、`ownerId`、`subjectRefs[]` 中对当前命令实际生效的稳定身份
- `characterId` 在 `M1` 仅作为 `actorId` 的兼容别名，不得继续作为新能力与新游戏的默认主键
- `Launcher` 或 `Runtime` 重启后，只要仍处于同一存档/身份上下文，重复提交同一 `operationId` 也必须命中同一幂等记录
- `Mod` 与 `Launcher` 必须在崩溃后重试场景中尽量复用原始 `operationId`

## 7. Local Runtime 技术策略

### 7.1 基本技术栈

`Local Runtime` 第一阶段采用以下技术方案：

- `ASP.NET Core` 作为本地服务宿主
- `Microsoft Agent Framework` 作为 AI 编排核心
- `SQLite` 作为本地记忆与会话数据存储
- `System.Text.Json` 作为默认序列化方案
- `Microsoft.Extensions.Logging + Serilog` 作为日志体系
- `HttpClientFactory` 作为外部模型供应商调用基础设施

### 7.2 Microsoft Agent Framework 的定位

必须明确：

- `Microsoft Agent Framework` 是 Runtime 的 AI 核心
- `ASP.NET Core` 是 Runtime 的宿主与接口承载层

换言之，`Local Runtime` 不是普通本地 Web 服务，而是以 `Microsoft Agent Framework` 为核心、以 `ASP.NET Core` 为外壳的本地 AI 运行时。

### 7.3 Runtime 内部模块边界

必须把 `Local Runtime` 定义为“统一 AI 能力运行时”，而不只是“统一对话后端”。

建议按以下逻辑模块组织：

- `Runtime Core`
- `Capability Host`
- `Conversation & Agent Engine`
- `Game Event Bus`
- `Game Command Gateway`
- `State & Memory Store`
- `LLM Provider Adapter`

其中：

- `Runtime Core` 负责启动握手、授权校验、心跳租约、请求入口、会话上下文、日志、幂等、配置加载与恢复调度
- `Capability Host` 负责能力注册、能力发现、能力生命周期、能力调度与能力级隔离
- `Conversation & Agent Engine` 负责基于 `Microsoft Agent Framework` 的会话主链路、Agent 编排、工具调用与记忆协作
- `Game Event Bus` 负责接收来自 Mod 的标准化游戏事件，并把事件分发给对应能力
- `Game Command Gateway` 负责把 Runtime 的决策结果转换成面向 Mod 的标准命令
- `State & Memory Store` 负责 `SQLite` 中的会话状态、能力状态、幂等记录与记忆持久化
- `LLM Provider Adapter` 负责屏蔽不同云模型供应商接口差异

这一模块边界必须直接服务以下目标：

- 让普通对话、流式输出、事件触发型互动、会话内陪伴等能力可以共存
- 让未来大多数新增功能通过“新增能力模块”接入，而不是反复侵入 `Runtime Core`
- 让 `M1` 先把“会话内能力”和“可持久化状态”边界定清楚，再把真正后台常驻能力留到后续里程碑

`M1` 还必须把逻辑身份边界提前写清：

- `gameId` 不是唯一状态路由键
- 共享协议必须提供一等 `identityContext`
- `identityContext` 至少包含 `saveId/worldId`、`actorId`、`objectId`、`ownerId`、`controllerId`、`subjectRefs[]`
- `characterId` 仍可保留，但只能视为 `actorId` 的特化别名
- `per_save`、`per_character`、`per_actor` 级别的能力状态、记忆和幂等记录都必须使用这些逻辑身份做隔离键

### 7.4 Runtime 的能力运行时定位

`M1` 虽然只上线按游戏 `基础包`，但 Runtime 架构现在就必须按“能力运行时”设计，而不是把未来能力简单留给“以后再说”。

必须明确以下原则：

- `Runtime` 的统一边界不是“所有游戏都共用一套对话 API”，而是“所有游戏都接入同一套能力宿主”
- 并非所有请求都属于“玩家输入一句 -> AI 返回一句”的同步问答模型
- `M1` 明确只承诺 `request/response`、`streaming`、`event-triggered` 三类执行模型
- `scheduled`、`continuous` 与真正后台常驻能力不属于 `M1` 承诺范围
- 大多数 AI 特色能力应以 `Capability` 的形式运行在 `Runtime` 内
- 少量强依赖特定游戏内部对象模型或执行方式的桥接逻辑，允许保留在 `Mod` 侧

因此，第一阶段推荐把职责拆分收敛为：

- `Deliberation Layer` 放在 `Runtime Capability` 内，负责规划、推理、记忆、会话状态与高层意图
- `Edge / Reflex Layer` 留在 `Mod` 或游戏适配层，负责帧关键、低延迟、紧贴游戏线程的即时执行

这样做的目的不是追求抽象优雅，而是避免两种长期风险：

- 把所有特色功能都塞入 `Mod`，最终把平台做成多个互不复用的孤岛
- 把所有游戏差异都塞入 `Runtime Core`，最终把核心做成不可维护的巨型进程

同时还必须承认 `M1` 的运行现实：

- 只要 `Launcher Supervisor` 租约失效，`Runtime` 就必须停止接收新请求并进入有序收尾
- 因此 `M1` 不承诺“`Launcher` 崩溃后能力继续后台存活”
- `M1` 的“宠物”“搭子”“主动问候”如果上线，只能定义为“会话内持续 + 事件触发 + 状态可跨启动延续”，而不是真正独立常驻 Agent

### 7.5 Capability 模型

每个游戏特色功能都应被定义为一个由 Runtime 托管的标准 `Capability`，而不是零散业务代码。

一个 `Capability` 最少必须包含以下结构：

- `Manifest`
- `Lifecycle`
- `Trigger Model`
- `Handler Surface`
- `State Contract`

各部分要求如下：

- `Manifest` 至少声明 `capabilityId`、`version`、`supportedGameIds`、`requiredEvents`、`emittedCommands`、`requiresStreaming`、`requiresActionExecution`、`requiredEntitlements`、`stateScope`、`configSchemaVersion`
- `Lifecycle` 在 `M1` 至少统一 `install`、`activate`、`deactivate`、`dispose`；`suspend`、`resume` 留待后续真正后台常驻能力再定义
- `Trigger Model` 在 `M1` 至少支持 `request_triggered`、`event_triggered`；`scheduled`、`continuous` 留作后续里程碑
- `Handler Surface` 在 `M1` 至少允许 `HandleRequestAsync`、`HandleEventAsync`、`RecoverAsync`、`GetStateSnapshotAsync`；`TickAsync` 不作为第一阶段必需面
- `State Contract` 至少允许 `per_request`、`per_session`、`per_save`、`per_character`、`global_per_game`

`M1` 对 `requiredEntitlements` 必须额外加一条硬约束：

- `M1` 只允许 `base` 这一类运行资格 entitlement
- 高级云能力的绘画次数、语音次数、视频次数不属于 entitlement，而属于平台托管调用前的计次检查
- `entitlement_denied` 在 `M1` 只用于基础包准入失败，不用于表达高级云能力余额不足

同时，`Capability Host` 不得只停留在概念层，必须提前固定最小 ABI 约束：

- 能力发现方式：`Capability` 必须通过受控注册表或受控装配清单加载，不允许运行时任意扫描目录执行未知代码
- 依赖边界：`Capability` 只能通过 `Capability Context` 访问白名单服务，不得直接依赖 `Runtime Core` 内部实现
- 调度规则：每个能力必须在受控调度器中运行，拥有明确的超时、取消和最大并发限制
- 故障边界：单个能力失败不得直接拖垮整个 `Runtime`，至少要支持按能力熔断、禁用和隔离
- 状态归属：能力只能读写自己声明的状态空间，不得任意读写其他能力状态
- 兼容性：能力清单必须声明最小 `Runtime` 版本和所需协议能力，未满足时 `Launcher` 应在启动前阻止对应组合

必须把未来能力首先理解为“能力模块”，而不是“额外字段”。

例如：

- `流式输出` 更适合作为 Runtime 的通用能力基础设施，也可被其他能力依赖
- `主动问候` 在 `M1` 应定义为“进入场景、空闲、交互等事件触发”能力，而不是后台定时器能力
- `AI 驱动动作` 在 `M1` 应定义为事件触发后的受控动作能力，而不是独立后台自治体
- `游戏搭子` 在 `M1` 应理解为“会话内陪伴 + 状态持久化”，而不是真正跨 `Launcher` 故障持续运行的同伴
- `智能宠物` 在 `M1` 应理解为“可持续记忆和会话内行为表现”的能力，而不是真正后台常驻生物

`M1` 不要求一次性做完全部能力，只要求把接入模型和运行边界定清楚，并避免过度承诺真实还不具备的后台常驻语义。

### 7.6 本地数据策略

第一阶段本地数据优先采用 `SQLite`，原因如下：

- 单机部署成本最低
- 适合存档级隔离
- 足以支撑第一阶段会话与记忆场景
- 有利于调试与快速落地

除普通会话与记忆外，还必须为以下运行时状态预留统一持久化边界：

- `Capability` 生命周期状态
- `Capability` 的长生命周期快照
- `operationId` 幂等记录
- 下一次受支持启动时可继续使用的最小执行上下文

这里必须明确：

- 这些持久化状态主要用于“下一次受支持启动后的连续体验”
- 不代表 `M1` 支持 `Launcher` 或 `Runtime` 中途失活后的无感续跑
- “宠物”“搭子”“主动行为冷却”等状态可以跨启动延续，但对应能力本身在 `M1` 仍视为会话内能力

### 7.7 能力执行位置模型

从 `M1` 开始，平台必须明确区分能力“长什么样”和“在哪里执行”是两件不同的事。

能力按执行位置至少分为三类：

- `Local Capability`
- `Hosted Capability`
- `Hybrid Capability`

定义如下：

- `Local Capability`：AI 主链路主要运行在本地 `Runtime`，典型场景是玩家自带 Key 的文本对话、记忆、本地事件驱动能力
- `Hosted Capability`：AI 主链路主要运行在服务端 `Hosted Capability Service`，本地只负责采集上下文、发起受控请求、接收结果和渲染
- `Hybrid Capability`：本地负责游戏上下文理解或执行反馈，服务端负责高成本生成、模型编排或供应商调用

这里必须写死一个关键结论：

- 只要某项高级能力采用“平台托管 + 按次数计费”的商业模式，它就不只是“远端扣费能力”，而是“服务端执行的 AI 能力”
- 这类能力的模型调用、供应商编排、成本控制、结果生成都在服务端执行
- 本地 `Runtime` 对这类能力的职责主要是：收集上下文、申请调用凭证、发起请求、接收结果、完成本地渲染或动作执行

因此，像以下能力在默认商业模式下都应首先归入 `Hosted Capability` 或 `Hybrid Capability`：

- `绘画`
- `语音`
- `视频`
- 未来任何高成本、强供应商依赖、需要平台统一计次和成本控制的 AI 能力

这条分类必须优先于“它看起来像不像某个 Runtime Capability”。
如果团队先按“功能名”讨论，而不先判断“执行位置”，后续一定会在本地 Runtime 和服务端执行面之间反复打架。

## 8. Game Adapter Mod 技术策略

### 8.1 适配层定位

`Game Adapter Mod` 是每个游戏的接入层，负责：

- 采集玩家输入
- 采集角色状态、地点、近期事件与游戏上下文
- 将游戏内结构翻译成共享协议
- 调用 Runtime
- 将返回结果重新渲染回游戏

### 8.2 技术栈原则

该层不强制统一技术栈，而应优先遵循各目标游戏最成熟、最稳定的 Mod 生态与开发方式。

平台统一的是：

- `Shared Contracts`
- `Runtime API`
- 协议版本
- 存档、角色、会话、事件等共享边界

平台不统一的是：

- Mod Loader
- 游戏内 UI 注入方式
- 游戏事件采集方式
- 各游戏内部对象模型

### 8.3 适配层约束

必须坚持以下约束：

- 适配层应尽量薄
- 重 AI 编排逻辑不应塞入 Mod
- 游戏专属上下文可以通过扩展字段透传
- 游戏专属字段不应污染共享核心协议
- 扩展字段必须使用 `extensions.<gameId>.*` 命名空间
- `Runtime` 必须忽略未知的可选扩展字段，核心协议字段变更必须通过协议版本升级处理
- 任一扩展字段一旦成为跨游戏必需语义，就必须优先评审是否提升为 `Facet`、`graph schema` 或 `command semantics`
- 若某项能力依赖特定扩展或 schema，必须通过运行时协商显式声明，并按 capability 维度降级或阻断，不得静默忽略

不同游戏的 Mod 项目会分别存在，并按各自游戏生态实现；它们共享的是平台协议和 Runtime，而不是同一份 Mod 代码。

### 8.4 Shared Contract 分层模型

不同游戏采集到的信息、对象结构和字段命名天然不会一致，这不是例外，而是平台长期常态。

因此，`Shared Contracts` 的目标不是“让所有游戏字段完全相同”，而是：

- 给 Runtime 与通用 Capability 提供稳定骨架
- 给可复用语义提供中间抽象层
- 给无法统一的游戏差异保留受控扩展出口

第一阶段必须把共享协议固定为三层：

- `Core Fields`
- `Domain Facets`
- `Game Extensions`

规则如下：

- `Core Fields` 只放跨游戏稳定且高频必需的最小公共字段
- `Domain Facets` 用于表达可跨游戏复用的中层语义，而不是直接暴露某个游戏的原始字段名
- `Game Extensions` 用于承载确实无法统一的游戏专属字段

`Core Fields` 至少应覆盖以下身份与上下文：

- `gameId`
- `identityContext`
- `simulationContext`
- `location`
- `eventType`
- `requestId`
- `traceId`
- `causationId`
- `operationId`
- `timestamp`

其中：

- `identityContext` 至少包含 `saveId/worldId`、`actorId`、`objectId`、`ownerId`、`controllerId`、`subjectRefs[]`
- `simulationContext` 至少包含 `timelineId`、`stateVersion`、`inGameTimestamp`，并按游戏类型补 `turnId/phaseId/stepId` 或 `tickId/frameId`
- `characterId`、`speaker`、`target` 可以保留为兼容字段，但不得再替代 `identityContext`

`Domain Facets` 至少允许以下语义面：

- `conversationFacet`
- `relationshipFacet`
- `inventoryFacet`
- `combatFacet`
- `questFacet`
- `scheduleFacet`

这里必须明确：

- `Facet` 是语义层，不是字段名搬运层
- 只要两个游戏存在相似的语义概念，就应尽量先映射到同一个 `Facet`
- 例如不同游戏的关系值、好感度、亲密度，只要语义相近，就应优先映射到 `relationshipFacet`
- 真正无法合理抽象的字段，才进入 `extensions.<gameId>.*`
- 同一语义若已在 `2` 个以上游戏中重复出现在 `extensions.<gameId>.*`，下一次接入前必须先评审是否晋升为新 `Facet`
- `Game Extensions` 必须有预算与回收计划，不能作为长期默认落点

统一顺序必须固定为：

1. 先映射到 `Core Fields`
2. 再尽量映射到 `Domain Facets`
3. 最后再把剩余差异放入 `Game Extensions`

不允许一开始就把大部分字段都塞进 `extensions.<gameId>.*`，否则 Runtime 会快速退化成“每个游戏一套私有协议”。

### 8.5 Game Translator 责任边界

解决“不同游戏字段不一致”的核心，不应该靠 `Runtime Core` 到处写特判，而应该靠每个游戏的 `Game Translator`。

`Game Translator` 可以位于对应游戏 Mod 内部，或位于该游戏专属适配层中，但职责必须固定：

- 把游戏原始对象模型翻译成 `Core Fields`
- 把可复用语义翻译成 `Domain Facets`
- 把无法统一的字段放入 `Game Extensions`
- 补齐 `identityContext`、`simulationContext` 等逻辑身份与时间字段
- 对缺失字段、低可信字段和估算字段做显式标记，而不是静默伪造

`Game Translator` 不得承担以下职责：

- 规划逻辑
- 长期状态修复逻辑
- 命令队列策略逻辑
- 运行时兼容热补丁垃圾场

每个新游戏在进入正式接入前，必须至少提交以下 `Translator Contract Pack`：

- `translatorManifest`
- 字段映射表
- `identityContext` 样例
- `simulationContext` 样例
- `WorldSnapshot / WorldDelta` 样例（若该游戏需要）
- `golden replay` 样例包
- `Game Extensions` 清单
- `unsupported semantics` 清单

Capability 依赖声明也必须遵循这套分层模型：

- 通用 Capability 优先依赖 `Core Fields + Domain Facets`
- 单游戏 Capability 才允许依赖 `extensions.<gameId>.*`
- 不允许通用 Capability 直接把某个具体游戏的扩展字段当成必需依赖

这样做的结果是：

- 字段不一致可以被吸收在翻译层
- Runtime 与通用 Capability 读的是稳定语义，而不是各游戏私有字段名
- 每增加一个新游戏，主要新增的是翻译器和少量扩展，而不是重写 Runtime 主链路

### 8.6 Runtime 与 Mod 的桥接边界

`Game Adapter Mod` 不应被设计成“只会转发文本”的薄壳，也不应被放任成长为“每个游戏各写一套 AI 系统”。

推荐把 `Mod <-> Runtime` 的桥接边界固定为两类协议：

- `Game Event`
- `Game Command`

其中：

- `Game Event` 负责把游戏内发生的事情标准化上报给 Runtime
- `Game Command` 负责让 Runtime 把决策结果标准化下发给 Mod 执行

典型 `Game Event` 包括但不限于：

- `player_spoke`
- `npc_nearby`
- `scene_entered`
- `player_idle`
- `combat_started`
- `quest_state_changed`

典型 `Game Command` 包括但不限于：

- `show_text`
- `stream_text_chunk`
- `play_emote`
- `move_to`
- `interact_with_target`
- `show_companion_hint`

必须明确以下设计原则：

- 新增游戏能力时，优先考虑“新增一个 `Capability`、少量事件、少量命令”，而不是修改 `Runtime Core`
- `Mod` 负责事件采集、命令执行、结果渲染和少量强游戏耦合桥接
- `Runtime` 负责理解、决策、授权、记忆、调度、恢复
- `Capability` 之间优先通过受控事件和上下文服务协作，避免随意硬编码互相依赖

这套边界的目标是让未来未知功能大多数都能通过增量接入，而不是迫使平台反复重做主架构

### 8.7 Shared Event / Command Contract 最小要求

`Game Event` 与 `Game Command` 不能只停留在命名示例，`M1` 必须至少固定最小共享信封。

`Game Event` 最少必须包含：

- `eventId`
- `eventType`
- `gameId`
- `launchSessionId`
- `requestId`
- `traceId`
- `causationId`
- `identityContext`
- `simulationContext`
- `occurredAt`
- `payload`

`Game Command` 最少必须包含：

- `commandId`
- `commandType`
- `commandSemantics`
- `gameId`
- `launchSessionId`
- `traceId`
- `causedByEventId`
- `identityContext`
- `simulationContext`
- `operationId`（若会产生副作用）
- `queueId`（若为 `control_intent`）
- `commandRevision`（若为 `control_intent`）
- `desiredStateVersion`（若为 `control_intent`）
- `supersedesCommandId`（若为 `control_intent`）
- `cancelCommandId`（若为 `control_intent`）
- `deadline/expiresAt`（若为 `control_intent`）
- `payload`

同时必须固定以下规则：

- `Game Event` 必须是 append-only 语义，不允许 Mod 事后修改已发送事件内容
- `Game Command` 的执行结果必须回传标准化 `commandResult`
- `commandSemantics` 在 `M1` 必须只允许 `transactional` 或 `control_intent`
- 需要副作用的命令必须进入与 `operationId` 关联的幂等模型
- 同一 `launchSessionId` 内的事件顺序必须至少在单个 Mod 实例内保持可解释顺序
- 当 Mod 执行命令失败时，失败原因必须可回传给 `Runtime`
- 若命令因协议版本、缺能力或上下文失效无法执行，必须返回可枚举错误，而不是静默丢弃
- `Runtime` 是意图队列与 supersede/cancel 决策的权威来源
- `Mod` 是边缘执行回执的权威来源，必须回传 `accepted/started/completed/interrupted/cancelled` 等执行状态
- 所有长生命周期命令都必须可回放 `intent -> accept -> edge_ack -> state_transition` 链路

## 9. Cloud Control Service 技术策略

### 9.1 基本技术栈

`Cloud Control Service` 第一阶段采用：

- `ASP.NET Core Web API`
- `PostgreSQL`
- `Serilog`
- 单体服务部署

第一阶段不默认引入复杂微服务拆分，不把资源消耗在过早的服务拆分与分布式治理上。

### 9.2 服务职责

`Cloud Control Service` 负责：

- 账号与登录
- 激活码、兑换码生成与核销
- `基础版` 运行资格状态校验
- `Launcher` 启动准入校验
- `Runtime` 运行许可控制
- 版本清单与最低兼容版本管理
- Mod 包与 Launcher 包索引
- 功能开关与配置下发
- 公告与运营信息

这里必须明确：

- `Cloud Control Service` 只负责 Mod 包索引、兼容矩阵、manifest 元数据与发布状态
- `Cloud Control Service` 不负责在 `M1` 直接存储或分发大文件二进制

`M1` 中，`功能开关与配置下发` 只用于发布、运维、兼容性与维护控制，不得承载以下商业化决策：

- 付费计划层级判断
- 单游戏收费判断
- 细粒度能力收费判断

### 9.3 不承担的职责

`Cloud Control Service` 不承担：

- 玩家日常 AI 对话推理
- 主要 Agent 编排链路
- 代替玩家保存模型供应商 API Key

它是控制面与准入面，不是推理面。

### 9.4 Hosted Capability Service

第一阶段需要明确增加一个独立逻辑角色：`Hosted Capability Service`。

它不等同于 `Cloud Control Service`，也不等同于本地 `Runtime`。它不是“远端扣费接口”，而是平台托管高级 AI 能力的执行面，专门负责：

- 平台托管高级云能力的调用前校验
- 绘画次数、语音次数、视频次数的预扣、提交、释放与审计
- 调用第三方绘画、语音、视频、音乐供应商
- 记录供应商请求标识、最终成本与平台账本结果

必须明确以下边界：

- `Cloud Control Service` 仍然是控制面和准入面
- `Hosted Capability Service` 是高级云能力的 AI 执行面与计次执行面
- `Runtime Access Token.aud` 只允许用于本地 Runtime 准入，不得复用于 `Hosted Capability Service`
- `Mod` 不得直接调用 `Hosted Capability Service`
- `Launcher` 不得绕过 `Runtime` 直接代表游戏逻辑调用 `Hosted Capability Service`

换言之：

- `Cloud Control Service` 不负责日常 AI 推理
- 但平台托管的绘画、语音、视频等高级能力，其日常 AI 推理与供应商执行链路确实发生在服务端
- 这些服务端 AI 链路不应再被误写成“只是多了一层远端计费”

同时必须固定最小调用顺序：

1. `Runtime` 先完成本地基础包准入和会话校验
2. `Runtime` 为单次高级云能力操作申请 `quoteId`、`billingOperationId` 与 `Hosted Capability Invocation Token`
3. `Hosted Capability Service` 必须通过单事务 `reserve_if_available` 原子完成价格快照锁定、`账号 + category` 余额校验、并发额度校验和 reservation 创建
4. 再调用第三方供应商
5. 再根据供应商终态执行 `commit`、`release` 或 `compensation_pending`

`M1` 必须同时定义最小资源控制：

- 按 `账号`、`gameId`、`capabilityCategory` 三个维度至少支持并发上限
- 平台托管高级云能力必须有 token bucket 或等价限流
- 必须存在远程 kill-switch，可按类别快速关闭异常能力
- 必须对第三方供应商调用配置统一超时、重试上限和熔断策略
- 必须按 `账号 + category` 维护 `pending reservation` 上限与超时回收

## 10. 强在线授权模型

### 10.1 总体原则

第一阶段采用强在线授权模式。

虽然模型推理与 Agent 编排在本地完成，但 `Launcher` 与 `Local Runtime` 的可用性必须受云端授权状态控制；未通过授权校验时，产品不得进入可正常使用状态。

这里的“强在线授权”在 `M1` 中有明确边界：

- 启动必须在线
- 运行期依赖短时 `Runtime Access Token` 持续刷新
- 它是账号准入与按游戏包控制模型，不等同于离线永久授权
- 它也不等同于对同机、同用户态本地进程的强对抗反破解

### 10.2 平台授权令牌的作用

平台授权令牌不用于调用外部模型供应商，而用于证明：

- 当前账号是否被允许使用本平台
- 当前用户是否具备当前目标游戏的基础包运行资格
- 当前版本是否满足最低兼容要求
- 当前设备是否具备该游戏包的运行绑定资格

玩家模型 API Key 只决定玩家自带 Key 的文本链路能否调用模型，平台授权令牌决定“允不允许运行本产品”，而平台托管高级云能力是否可用则由“基础包准入 + 云端计次”共同决定。

### 10.3 推荐令牌字段

`M1` 平台运行授权令牌必须采用可本地验签的紧凑序列化令牌格式，推荐使用带 `kid` 的 `JWT`。`M1` 约定至少满足以下要求：

- 签名算法固定为服务端签发、客户端只验签的非对称算法
- `Launcher` 与 `Runtime` 本地保存公钥信任锚，不保存私钥
- 时钟漂移容忍最多 `60` 秒
- `launchSessionId` 的作用域是“单个 `gameId` 的一次受支持启动会话”
- 令牌里的 `gameId` 是最终准入权威来源
- `Runtime Access Token` 的生命周期上限固定为 `10` 分钟
- `Launcher` 必须在令牌剩余 `3` 分钟时开始刷新
- 在平台故障场景下，旧令牌最多只允许继续运行到原始 `exp`，不允许额外宽限续命

`M1` 平台运行授权令牌必须至少包含：

- `iss`
- `aud`
- `kid`
- `jti`
- `launchSessionId`
- `deviceBindingId`
- `nbf`
- `userId`
- `gameId`
- `packageType`
- `packageAccessStatus`
- `minimumClientVersion`
- `iat`
- `exp`

字段约束如下：

- `aud` 固定表示本地 Runtime 准入，不允许复用到其他用途
- `packageType` 在 `M1` 只允许 `base`
- `packageAccessStatus` 在 `M1` 只允许 `granted` 或 `denied`
- `jti` 主要用于审计与日志关联，不要求本地做复杂撤销列表同步

`M1` 授权令牌不表达以下内容：

- 平台统一高级版
- 高级云能力剩余次数
- 与当前 `gameId` 无关的包授权

### 10.4 M1 授权凭证结构

`M1` 明确区分两层凭证：

- `Runtime Access Token`
- `runtimeSessionSecret`
- `Hosted Capability Invocation Token`

其中：

- `Runtime Access Token` 由 `Cloud Control Service` 签发，用于证明本次启动具备指定 `gameId` 的 `基础包` 运行资格
- `Runtime Access Token` 由 `Launcher` 获取，并由 `Runtime` 使用本地公钥信任锚按 `kid` 在本地验签
- `Runtime Access Token` 只允许驻留在 `Launcher` 与 `Runtime` 进程内存中，不得写入 Mod 可读配置或持久化介质
- `Runtime Access Token` 只能通过父子进程控制通道和 `Launcher -> Runtime` 命名管道热更新，不得通过命令行、环境变量或文件传递
- `runtimeSessionSecret` 由 `Launcher` 按 `launchSessionId` 本地生成，用于约束 `Mod -> Runtime` 的本机调用
- `Hosted Capability Invocation Token` 由平台在每次高级云能力调用前单独签发，只用于一次高级云能力操作

`Hosted Capability Invocation Token` 必须满足以下要求：

- `aud` 只能是 `Hosted Capability Service`
- 必须带 `userId`、`gameId`、`launchSessionId`、`operationId`、`billingOperationId`、`quoteId`、`capabilityCategory`
- 有效期必须按秒计，`M1` 上限固定为 `60` 秒
- 必须是一次性使用令牌，用后即作废
- `runtimeSessionSecret` 与 `Runtime Access Token` 都不得直接作为高级云能力调用凭证

`M1` 禁止 `Runtime` 在每个游戏请求上都回调云端做鉴权。

`Runtime` 的最终鉴权方式必须是：

- 对每个 AI 请求本地校验 `Runtime Access Token`
- 对每个 AI 请求本地校验 `runtimeSessionSecret`
- 对每个 AI 请求本地校验 `launchSessionId`、`protocolVersion`
- 对每个 AI 请求校验请求 `gameId` 与令牌 `gameId` 一致
- 不通过任一校验则拒绝处理

对于平台托管高级云能力，`Runtime` 的职责必须改为：

- 在本地完成基础包准入、协议与本机会话校验
- 为单次高级云能力操作申请 `Hosted Capability Invocation Token`
- 把该 token 与 `operationId`、`billingOperationId`、`billingSource` 一起转交给 `Hosted Capability Service`
- 不得把本地 `runtimeSessionSecret` 直接暴露给远端高级云能力服务

### 10.5 Launcher 到 Cloud Control 的授权与刷新协议

`M1` 不能只描述行为，必须把 `Launcher -> Cloud Control` 的控制面协议固定下来。

`Launcher` 至少需要两类接口：

- `startup_auth_check`
- `issue_or_refresh_runtime_token`

`startup_auth_check` 请求至少包含：

- `userAccessToken`
- `launcherVersion`
- `launcherInstanceId`
- `deviceBindingId`
- `clientRequestId`
- `traceId`

`startup_auth_check` 响应至少包含：

- `success`
- `reasonCode`
- `accountStatus`
- `ownedPackagesSnapshot`
- `minimumClientVersion`
- `serverTime`
- `requestId`

`issue_or_refresh_runtime_token` 请求至少包含：

- `userAccessToken`
- `launchSessionId`
- `launcherInstanceId`
- `gameId`
- `deviceBindingId`
- `launcherVersion`
- `currentRuntimeTokenJti`
- `clientRequestId`
- `traceId`

`issue_or_refresh_runtime_token` 响应至少包含：

- `success`
- `reasonCode`
- `scope`
- `runtimeAccessToken`
- `serverTime`
- `requestId`

`scope` 只允许以下值：

- `game`
- `account`
- `platform`

`reasonCode` 在 `M1` 至少要统一以下枚举：

- `ok`
- `not_logged_in`
- `package_not_owned`
- `device_limit_exceeded`
- `account_disabled`
- `client_too_old`
- `maintenance_mode`
- `service_unavailable`
- `invalid_signature`
- `invalid_request`

判定规则必须固定如下：

- `scope=game` 的拒绝，只影响当前 `gameId`
- `scope=account` 的拒绝，影响当前账号下全部活跃游戏
- `scope=platform` 的拒绝或故障，按维护态或安全失败规则处理
- `reasonCode` 不能由前端自由解释，必须直接驱动 `Launcher` 与 `Runtime` 的状态迁移

建议的 `HTTP` 语义固定如下：

- `200`：`ok`
- `400`：`invalid_request`
- `401`：`not_logged_in`
- `403`：`package_not_owned`、`device_limit_exceeded`、`account_disabled`
- `426`：`client_too_old`
- `503`：`maintenance_mode`、`service_unavailable`
- `502`：`invalid_signature`

### 10.6 M1 强在线授权行为

第一阶段 `M1` 的强在线授权不是抽象原则，而是明确的启动行为约束：

1. 玩家启动 `Launcher` 时必须联网
2. `Launcher` 启动阶段即向 `Cloud Control Service` 发起授权校验
3. `Launcher` 启动后可进入账号已认证状态，并拉取账号下各游戏包的权益快照
4. 当玩家启动某个游戏时，`Launcher` 必须先完成该游戏包的设备绑定检查，再为该 `gameId` 申请有效的 `Runtime Access Token`
5. 若该 `gameId` 未购买对应基础包、设备数超限或版本不满足要求，则不得启动该游戏的可正常使用 Runtime 链路
6. `Runtime` 只有在本地验签成功后才允许开始监听业务端口
7. `Runtime` 对每个 AI 请求都必须校验本地令牌未过期
8. `Launcher` 必须在 `exp` 前刷新令牌
9. `Runtime` 不得在每个 AI 请求上调用 `Cloud Control Service`

`M1` 采用以下暴露策略：

- 未通过授权时，`Launcher` 不启动 `Runtime` 监听端口
- 进入 `维护态` 时，若尚未成功授权，也不启动 `Runtime` 监听端口
- 已获得有效令牌并处于运行中的游戏，会在令牌到期前继续可用，但不因服务故障无限续期

### 10.7 运行中刷新、过期与撤销

`M1` 必须明确运行中的授权行为：

- 已开始执行的 AI 请求在令牌到期瞬间可以完成本次处理
- 令牌到期后，新的 AI 请求必须被拒绝，并返回授权过期错误
- `Launcher` 刷新成功后，必须通过本地命名管道把新令牌热更新给对应 `Runtime`，无需重启即可恢复新的 AI 请求
- 只要 `Launcher Supervisor` 或 `Runtime` 结束，当前 `launchSessionId`、`Runtime Access Token` 运行态和 `runtimeSessionSecret` 都必须视为终止
- `M1` 不支持在同一游戏会话里自动重建 `Runtime`
- 云端撤销、封禁、退款、基础版失效或最低版本提升，在 `M1` 中于下一次刷新检查或下一次启动时生效
- `M1` 不要求即时中断式的中途推送撤销
- 若刷新结果是明确的游戏级权益拒绝，则只使对应 `gameId` 的 `runtimeSessionSecret` 和运行态立即失效，其他正在运行的游戏不受影响
- 若刷新结果是账号级拒绝，例如封禁、登录失效或账号停用，则全部活跃 `Runtime` 和全部 `runtimeSessionSecret` 都必须立即失效
- 平台服务故障时，不旋转 `runtimeSessionSecret`，只保留旧令牌运行到当前 `exp`

因此，`M1` 的最大陈旧授权窗口等于访问令牌的有效期。

### 10.8 校验失败与维护态

第一阶段需要明确区分三类失败：

- `账号或权益失败`
- `平台服务故障`
- `安全或完整性失败`

若属于账号或权益失败，例如：

- 未登录
- 当前目标游戏基础包未开通
- 当前目标游戏设备数超限
- 账号被禁用或被撤销使用资格
- 版本低于最低兼容版本

则 `Launcher` 应直接拒绝进入对应游戏的可用状态，并向玩家展示明确的原因。

若属于平台服务故障，例如：

- 授权服务异常
- 平台登录服务异常
- 平台状态不可用
- 网络不可达
- DNS 或 TLS 失败
- 超时
- `429`
- `5xx`

则 `Launcher` 不应将其展示为玩家本地配置错误，而应进入 `维护态`。

若属于安全或完整性失败，例如：

- 服务返回无效签名或缺字段响应
- `kid` 不存在
- `aud`、`iss`、`gameId` 或 `deviceBindingId` 不匹配

则 `Launcher` 必须将其视为硬拒绝和安全告警，而不是普通维护态。

`维护态` 下允许：

- 展示平台状态说明
- 展示公告与恢复提示
- 展示预计恢复信息

`维护态` 下不允许：

- 启动需要授权放行的核心 AI 能力

如果平台在启动成功后出现服务故障，则：

- 当前仍未过期的 `Runtime Access Token` 在 `expiresAt` 前继续有效
- `Launcher` 应展示降级或维护提示
- `Launcher` 应在后台持续尝试刷新
- 若到 `expiresAt` 仍刷新失败，则新的 AI 请求必须被拒绝

如果启动后收到安全或完整性失败，则：

- 当前已生效且未过期的旧令牌可以运行到当前 `exp`
- `Launcher` 不得接受这次异常响应去替换本地旧令牌
- 若当前没有可继续使用的旧令牌，则对应游戏不得进入可用状态

`M1` 的 `Launcher` 维护态状态机必须固定如下：

- `normal`
- `maintenance_pending`
- `maintenance_blocking`
- `security_blocking`

迁移规则必须固定如下：

- 启动期命中 `maintenance_mode` 或 `service_unavailable` 时，进入 `maintenance_blocking`
- 运行中刷新命中 `maintenance_mode` 或 `service_unavailable`，但当前游戏仍有未过期令牌时，进入 `maintenance_pending`
- 任意阶段命中 `invalid_signature`、`kid` 缺失或关键字段不匹配时，进入 `security_blocking`
- 在 `maintenance_pending` 下，一旦全部相关游戏令牌过期且仍未恢复成功，必须转入 `maintenance_blocking`
- 只有后续一次成功的 `startup_auth_check` 或 `issue_or_refresh_runtime_token` 响应为 `ok`，才允许从 `maintenance_pending` 或 `maintenance_blocking` 回到 `normal`
- `security_blocking` 只能由一次完整成功的重新校验解除，不能由普通重试自动解除

各状态下的执行规则固定如下：

- `normal`：允许按文档正常启动和刷新
- `maintenance_pending`：不允许新启动受影响游戏，但允许未过期旧会话继续跑到当前令牌过期
- `maintenance_blocking`：不允许启动任何受影响游戏，也不允许继续刷新失败后的新请求
- `security_blocking`：不接受新的云端异常响应替换本地状态，并要求玩家重新校验

## 11. 商业化权益模型

### 11.1 长期商业模型

平台长期商业模型应拆成两层，但只保留一层授权：

- `按游戏基础包` 负责产品准入
- `平台托管高级云能力` 负责按类别计次

这意味着：

- 是否允许玩家正常使用某个游戏的 AI 产品，由该 `gameId` 的基础包决定
- 语音、绘画、视频、音乐等高成本云能力，不再单独做“功能包解锁”
- 高级云能力默认在对应游戏内开放入口，但实际使用按类别分别计次
- 不采用平台统一高级版，也不采用跨游戏共享功能包
- 这类按次计费的高级云能力，其 AI 执行主链路默认发生在服务端，而不是本地 Runtime

平台长期商业模型的核心不是“卖更多解锁项”，而是：

- 用 `基础包` 保持按游戏的产品价值与准入边界
- 用平台托管计次能力承接不同供应商、不同成本结构的高级云功能

### 11.2 M1 商业化收敛原则

第一阶段 `M1` 的授权层只上线一种包类型：

- `按游戏基础包`

例如：

- `星露谷基础包`
- `太吾绘卷基础包`

这样设计的原因是：

- 保留“玩家按游戏单独购买”的商业策略
- 避免在 `M1` 同时引入功能包解锁、复杂叠包和跨包依赖
- 先验证各游戏基础 AI 体验是否具备付费意愿
- 把高成本能力的商业风险从“包授权”转移到“按类别计次”

### 11.3 M1 基础包定义

每个游戏的 `基础包` 在 `M1` 包含该游戏的以下能力：

- `AI 会话`
- `记忆`
- `物品生成雏形`

`M1` 不存在平台统一基础版。

同时必须明确：

- `基础包` 决定的是“这个游戏能不能进入可用 AI 运行态”
- `基础包` 不是高级云能力的单独解锁器
- 只要该 `gameId` 的基础包有效，对应游戏内的高级云功能入口默认可见

但这里必须增加一个 UI 约束：

- 高级云能力是否处于“可点击可执行”状态，必须经过最新一次 preflight 校验
- preflight 至少返回当前类别剩余次数、单次价格、平台可用性、失败原因、`quoteId`、`quoteExpiresAt`、`displayPriceVersion`
- preflight 还必须返回 `requestedOutputClass` 与 `maxChargeableSpec`
- 次数不足、服务降级或平台不可用时，入口应显示不可执行原因，而不是让玩家点到最后一步才失败
- 高级云能力入口在 `M1` 至少需要支持 `visible_disabled`、`quote_required`、`quote_confirmed`、`executable` 四种 UI 状态

### 11.4 高级云能力计费模型

第一阶段起，平台托管的高级云能力统一采用“按功能类别分别计次”的模式，不做功能解锁，不采用统一高级次数池。

规则固定如下：

- `绘画` 按绘画次数计费
- `语音` 按语音次数计费
- `视频` 按视频次数计费
- 若未来上线 `音乐`，则按音乐次数单独计费

但“按次数”不是无规格的一次一扣，必须从现在开始绑定标准计费档位：

- 每个 `capabilityCategory` 都必须定义自己的 `requestedOutputClass`
- 每次调用都必须形成 `quoteId`、`billingPolicyVersion` 与 `maxChargeableSpec`
- 未命中已报价档位的请求不得执行
- 供应商真实计量必须沉淀为 `providerMeteringSnapshot`
- 必须为重试、部分结果和异步多产物返回定义 `retryBillingPolicy` 与 `partialOutputPolicy`

必须坚持以下原则：

- 各功能类别单独结算，不共池，不互抵
- 不把绘画、语音、视频压成统一高级次数
- 不要求玩家自行寻找或配置第三方绘画、语音、视频供应商 API
- 高级云能力默认由平台托管调用，平台自己承担供应商接入复杂度
- 玩家是否还能继续使用某项高级云能力，取决于该类别剩余次数或本次购买是否成功，不取决于额外功能包

次数账本作用域在 `M1` 必须明确固定为：

- 按 `账号 + 功能类别` 建立全局计次账本
- 不按单台设备拆账本
- 不按单游戏拆账本
- 这是一种平台托管消费钱包，不等同于跨游戏共享“功能包”

这样设计的原因如下：

- 玩家更容易理解“用了几次就扣几次”
- 平台可以在不改用户心智的前提下替换不同供应商
- 每类能力成本结构不同，必须单独核算，不能强行混成一个池子

### 11.5 不进入授权模型的内容

以下内容不进入 `M1` 以及后续高级云能力的授权模型：

- `语音包`
- `绘画包`
- `视频包`
- `音乐包`
- 平台统一高级版
- 跨游戏共享功能包

这些内容不作为解锁项存在。

语音、绘画、视频、音乐若上线，都是“默认开放入口 + 平台托管 + 分项计次”的商业模式，而不是“先买功能包再用”。

### 11.6 购买形态与设备规则

长期购买规则现在就定死：

- 基础包按游戏提供长期有效、但强依赖平台在线服务的服务型许可
- 每个游戏包默认允许 `3` 台设备
- 设备上限按游戏分别计算，不跨游戏共享
- 玩家可在 `Launcher` 中手动解绑旧设备
- 不采用自动顶替旧设备
- 不依赖人工客服解绑作为默认流程

必须避免向玩家宣传为“完全离线可用的永久买断”，因为：

- 启动与运行都依赖平台授权服务
- 平台故障时只能运行到当前令牌过期
- 这是一种带在线依赖的长期许可，而不是脱离平台即可永久运行的离线授权

高级云能力的计次购买与设备槽位是两回事：

- 设备槽位仍只跟 `基础包` 运行资格有关
- 绘画次数、语音次数、视频次数不占用额外设备槽位
- 计次购买记录应按账号归属，而不是按单台设备归属

`M1` 需要把“设备”定义清楚：

- `deviceBindingId` 必须优先使用服务端签发、并持久化在 Windows 受保护存储中的安装标识
- `deviceBindingId` 在 `M1` 仍不是强硬件指纹，但必须尽量避免被本地删除配置轻易重置
- 正常升级、重启与同机重复启动必须复用同一个 `deviceBindingId`
- 系统重装、手动删除本地数据或更换设备可能形成新的 `deviceBindingId`，但服务端必须进行同机重装识别、冷却与异常换绑检测
- 某游戏包的设备槽位在“该设备第一次成功拿到该游戏的 `Runtime Access Token`”时占用
- 同一设备后续再次启动同一游戏，不重复消耗新槽位
- 设备槽位信息至少要向玩家展示设备名、首次绑定时间、最近活跃时间
- 服务端必须保存每个 `deviceBindingId` 的注册时间、最近活跃时间和最近一次 `launcherVersion`
- 服务端必须对异常频繁的新 `deviceBindingId` 创建做风控与速率限制

为避免玩家被锁死，`M1` 必须提供受限管理模式：

- 即使某个游戏因设备数超限而拿不到运行令牌，`Launcher` 仍必须允许登录
- 在该模式下允许查看已购游戏包、解绑旧设备、输入兑换码、查看失败原因
- 在该模式下不允许启动被拦截的游戏 AI 运行链路

### 11.7 兑换码与运行授权令牌的关系

第一阶段必须区分两类凭证：

- `兑换码/激活码`：用于开通对应 `gameId` 的基础包
- `Runtime Access Token`：用于运行时准入控制

若后续支持高级云能力次数购买或赠送，应作为独立交易与独立计次记录处理，而不是写进 `Runtime Access Token`。

推荐链路如下：

1. 玩家登录 `Launcher`
2. 玩家输入兑换码或激活码
3. `Cloud Control Service` 核销并更新账号下对应 `gameId` 的包状态
4. `Launcher` 在启动该游戏前，按 `gameId` 申请本次运行的授权令牌
5. `Runtime` 根据令牌决定是否放行该游戏基础包对应的本地 AI 运行能力

高级云能力的次数校验与扣减不应混入本地运行授权令牌，而应在平台托管的高级云能力调用入口完成。

高级云能力的计次扣费必须固定为以下原子流程：

1. `Runtime` 先生成本地 `operationId`，并为 Hosted 账本申请独立 `billingOperationId`
2. `Hosted Capability Service` 通过单事务 `reserve_if_available(accountId, category, quotedUnits, quoteId)` 原子完成 reservation
3. reservation 成功后，才允许下游调用第三方供应商
4. 供应商明确成功后执行 `usage commit`
5. 供应商明确失败后执行 `usage release`
6. 供应商超时、异步 accepted 或未知终态时，必须进入 `compensation_pending` 或异步恢复流程，不得直接重复扣次

同时必须明确：

- Hosted 账本幂等键必须固定为 `userId + capabilityCategory + billingOperationId`
- 本地 `operationId` 只是上游映射键，不得直接代替 Hosted 账本主键
- 同一 `billingOperationId` 的高级云能力请求不得重复扣次
- 供应商适配层必须支持按 `billingOperationId` 或供应商等价幂等键做幂等
- 对于超时和未知终态，平台必须具备异步对账与补偿能力
- reservation 必须带 `reservationExpiresAt`
- 必须存在 `maxPendingReservationsPerAccountCategory`

### 11.8 权限拦截位置

商业化权限与计费采用三层结构：

- `Cloud Control` 负责权益源头与令牌签发
- `Launcher` 负责前置提示和入口控制
- `Runtime` 负责本地 AI 运行态的最终执行校验

对于高级云能力，还必须补充一层平台托管调用控制：

- 平台托管高级云能力入口负责 preflight、次数 reservation、扣减提交、释放、供应商调用和使用审计

其中：

- `Runtime` 是本地 AI 运行态的最终闸门
- `Hosted Capability Service` 是高级云能力执行与计次的最终闸门

这意味着：

- `Launcher` 可以提前提示某个 `gameId` 的包未购买
- `Launcher` 的提示只负责用户体验，不构成最终放行依据
- `Runtime` 在 `M1` 只对当前 `gameId` 做基础包准入判断
- `Runtime` 以令牌中的 `gameId` 作为唯一权威来源，Mod 配置中的 `gameId` 不得覆盖或修正令牌结论
- 若当前 `gameId` 的基础包有效，则放行该游戏的：
  - `AI 会话`
  - `记忆`
  - `物品生成雏形`
- 若当前 `gameId` 的基础包无效，则拒绝该游戏全部核心 AI API

而对于高级云能力：

- 只要基础包有效，对应入口默认可见
- 是否能成功执行绘画、语音、视频等高成本能力，由平台托管入口按类别做 preflight 与最终计次校验
- 某一类别次数不足时，只拒绝该类别调用，不影响同一游戏的基础 AI 运行态
- 平台托管高级云能力服务或下游供应商故障时，必须 fail-closed，且不得扣除已确认失败的次数

高级云能力还必须拥有独立于主授权平面的可用性状态：

- `premium_ready`
- `premium_balance_blocked`
- `premium_service_blocked`
- `premium_provider_blocked`
- `premium_quote_expired`
- `premium_reservation_pending`
- `premium_commit_pending`
- `premium_compensation_pending`
- `premium_byok_budget_blocked`
- `premium_abuse_blocked`

这些状态用于控制高级云能力入口是否可执行，不得混用主 `Launcher` 维护态去表达高级云能力的余额不足或下游供应商故障。

## 12. 玩家模型 API Key 与平台托管能力策略

第一阶段仍允许玩家在本地配置自己的模型 API Key，但该路径只适用于玩家自带 Key 的文本链路，不再承担高级云能力的主路径。

必须明确以下边界：

- 玩家模型 API Key 只用于玩家自带 Key 的文本模型调用
- 该 Key 必须存放在 Windows 受保护存储中，而不是明文配置文件
- 平台登录态、平台授权令牌、运行许可和高级云能力次数都不采用同样策略
- 平台托管的绘画、语音、视频等高级云能力，不要求玩家自行寻找或配置第三方供应商 API
- 平台托管高级云能力的调用与计费，由平台侧统一接入、统一计次
- `Launcher` 必须提供按供应商的并发限制、软预算和可视化用量提示
- 当用户配置了 BYOK 时，必须允许设置单游戏软预算、单会话软预算和超限硬停
- 每笔请求都必须显式记录 `billingSource = platform_hosted | user_byok | mixed`
- Hosted-only 类别若命中 BYOK 路由，必须直接拒绝并记录 `routing_rejected_by_policy`
- `Hosted Capability Service` 是 Hosted/Hybrid 高成本能力的唯一供应商出口与唯一账本出口

这样设计的原因是：

- 文本链路仍保留玩家自带 Key 的灵活性
- 高成本能力不把供应商接入复杂度转嫁给玩家
- 平台可以把“能不能用”和“这次要不要扣对应类别次数”拆开处理

## 13. 测试、日志与版本策略

### 13.1 测试策略

第一阶段测试重点如下：

- `Shared Contracts` 单元测试
- `Conversation Orchestrator` 输入输出规则测试
- `Memory Service` 测试
- `LLM Provider Adapter` 测试
- 授权校验逻辑测试
- `Mod -> Runtime API` 主链路集成测试
- `Hosted Capability Service` 的调用鉴权测试
- 高级云能力 usage reservation / commit / release 幂等测试
- `Hosted Capability Invocation Token` 的一次性、过期和 audience 校验测试
- `Game Event / Game Command` 标准信封与错误传播测试
- `deviceBindingId` 重装、换绑和风控场景测试
- BYOK 软预算、并发限制与超限停机测试

UI 自动化测试在 M1 不作为重投入方向，首发以关键路径手测为主。

### 13.2 日志策略

日志不是简单的“记录文本”，而是首发阶段排错、协议治理、计次审计和跨层追踪的基础设施。

其中“服务器日志”在本设计中明确指：

- `Hosted Capability Log`
- `Cloud Control Log`

平台日志至少必须分为五层：

- `Launcher Log`
- `Mod Log`
- `Runtime Log`
- `Hosted Capability Log`
- `Cloud Control Log`

各层最小职责如下：

- `Launcher Log`：登录、启动编排、授权刷新、Runtime 进程管理、版本检查、诊断开关状态
- `Mod Log`：字段采集、Game Translator 结果、命令执行结果、渲染侧失败、引擎侧异常
- `Runtime Log`：协议入口、Capability 调度、记忆读写、命令下发、幂等状态迁移、本地异常
- `Hosted Capability Log`：preflight、usage reservation、供应商调用、commit/release、providerRequestId、最终扣次结果
- `Cloud Control Log`：账号、授权、设备绑定、兑换码、最低版本控制、云端安全与维护态事件

日志体系应优先服务于：

- 首发阶段排错
- 授权问题定位
- 协议问题定位
- Hosted AI 计次与供应商审计
- 用户问题的一键诊断导出

### 13.2.1 统一关联字段

只有多层日志，没有统一关联字段，实际仍然很难定位问题。

因此以下字段必须尽量贯穿全链路：

- `requestId`
- `traceId`
- `causationId`
- `operationId`
- `billingOperationId`
- `launchSessionId`
- `launcherInstanceId`
- `leaseEpoch`
- `runtimeInstanceId`
- `gameId`
- `saveId/worldId`
- `characterId` 或 `actorId`
- `diagnosticSessionId`
- `jti` 哈希
- `quoteId`
- `providerRequestId`（若发生 Hosted AI 调用）

其中必须明确：

- `requestId` 用于一次请求链路追踪
- `traceId` 用于跨启动、事件、命令、云调用与审计的统一因果链
- `causationId` 用于表达父子事件或父子命令关系
- `operationId` 用于事务、副作用和计次对账
- `billingOperationId` 用于 Hosted 账本幂等与供应商对账
- `providerRequestId` 用于平台与供应商对账
- 这些字段必须在本地日志和云端日志中以统一命名出现

### 13.2.2 日志级别与诊断开关

为避免日志噪音和性能损耗，日志必须分为两种模式：

- `baseline logging`
- `diagnostic logging`

规则如下：

- `baseline logging` 默认开启，只记录低噪音、低成本、可长期保留的关键日志
- `diagnostic logging` 默认关闭，只用于排查指定问题
- `diagnostic logging` 必须支持一键开启、一键关闭，并带自动过期回落
- 不能要求用户手工改多个组件配置才能打开问题诊断
- `Launcher Supervisor` 必须负责下发统一 `diagnostic_control`，并收集各组件 ACK
- 组件必须回传当前 `diagnosticSessionId`、`diagnosticLevel`、`expiresAt`

诊断开关至少必须支持以下维度：

- 按组件开关：`Launcher`、`Mod`、`Runtime`、`Hosted Capability Service`、`Cloud Control`
- 按问题域开关：`auth`、`protocol`、`translator`、`capability`、`hosted_ai`、`billing`、`performance`

日志格式也必须固定如下：

- `baseline logging` 与 `diagnostic logging` 都必须输出结构化日志，首选 `JSON lines`
- 每条日志至少带 `logSchemaVersion`、`componentInstanceId`、`traceId`

### 13.2.3 性能保护策略

日志系统不得反向拖慢游戏主链路。

因此必须固定以下性能约束：

- 日志写入必须异步
- 必须支持批量 flush
- 高流量日志必须支持采样
- 大 payload 必须支持截断
- 当日志队列堆积时，必须优先丢弃低优先级诊断日志，而不是阻塞主链路
- 不能因为日志后端暂时不可写，就让 `Mod`、`Runtime` 或 `Hosted Capability Service` 出现同步卡顿
- 每个组件都必须记录 `droppedLogCount`、`clockOffsetMs` 与最近一次 flush 结果

### 13.2.4 Hosted AI 审计日志

对于平台托管高级 AI 能力，普通业务日志不够，必须额外有不可变审计记录。

每笔 Hosted AI 调用至少要记录：

- `ledgerEventId`
- `operationId`
- `billingOperationId`
- `userId` 脱敏标识
- `gameId`
- `category`
- `quoteId`
- `billingPolicyVersion`
- `providerRequestId`
- `providerName`
- `providerModelVersion`
- `usage reservation` 状态
- `usage commit/release` 状态
- `inputMeter`
- `outputMeter`
- `balanceBefore`
- `balanceAfter`
- 平台预估成本
- 供应商最终成本
- 最终扣次结果

必须支持按 `operationId` 回答以下问题：

- 玩家是否真的发起了这次调用
- 平台是否预扣了次数
- 供应商是否实际执行
- 平台最终是否扣次
- 若失败，失败发生在哪一层
- 若发生退款或补偿，回滚链条是什么

### 13.2.5 一键导出诊断包

首发阶段必须支持一键导出问题包，而不是让用户手工拷贝多份日志。

诊断包至少应包含：

- 最近窗口内的 `Launcher Log`
- 对应游戏的 `Mod Log`
- 对应 `Runtime Log`
- 最近的 `Hosted Capability` 摘要日志
- 当前版本信息
- 协议版本信息
- 关键环境信息
- 最近失败请求的关联 ID 摘要

诊断包必须默认脱敏，且不应包含完整 API Key、完整令牌或完整本地会话密钥。

同时必须满足以下脱敏规则：

- 不记录完整的玩家模型 API Key
- 不记录完整的 `Runtime Access Token`
- 不记录完整的 `runtimeSessionSecret`
- 不记录完整的 `deviceBindingId`
- 不输出包含以上敏感字段的完整配置快照
- 需要排查时，只允许记录哈希值、前缀或后缀片段
- 本地与云端日志都必须使用 `requestId`、`launchSessionId`、`runtimeInstanceId`、`jti` 哈希做关联
- 安全或完整性失败必须单独打点，不能混入普通维护态日志
- 必须为异常指标建立告警：扣次漂移、供应商错误率、异常设备换绑、令牌刷新失败、Premium 消费突增

### 13.3 版本策略

第一阶段以下对象必须显式版本化：

- `Launcher`
- `Runtime`
- `Shared Contracts`
- `Mod Protocol`

云端应能够下发最低兼容版本要求，并在必要时拒绝过旧客户端继续工作。

`M1` 兼容规则明确如下：

- `Launcher` 与内嵌 `Runtime` 视为同一交付单元，版本必须完全一致
- `minimumClientVersion` 只校验 `Launcher` 版本
- `Mod Protocol` 使用 `semver`
- `Runtime` 仅接受同 `major` 且 `Mod minor <= Runtime minor` 的请求
- `Shared Contracts` 不再只作为构建期依赖，运行时兼容必须拆分为 `Transport Compatibility`、`Schema Capability Negotiation`、`Facet / Graph / Command Schema Versioning` 三层
- 运行期配置与安装期静态配置都必须带自己的 schema version
- 运行时协商优先级必须固定为：`minimumClientVersion` -> `Transport Compatibility` -> 配置 schema version -> `Schema Capability Negotiation`
- `Launcher` 启动前必须拿到目标游戏的兼容性清单，至少包含：`supportedProtocolVersion`、`supportedFacetSchemas`、`supportedWorldGraphSchemas`、`supportedCommandSemantics`、`requiredCapabilities`

启动前的版本判定顺序必须固定如下：

1. 先校验 `Launcher` 是否满足云端 `minimumClientVersion`
2. 再校验当前 `Launcher` 携带的 `Runtime` 版本是否与 `Launcher` 完全一致
3. 再校验目标游戏 Mod 的 `supportedProtocolVersion`
4. 再校验运行期配置 schema version
5. 再协商 `supportedFacetSchemas`、`supportedWorldGraphSchemas`、`supportedCommandSemantics`
6. 最后校验 `requiredCapabilities`

任一步失败时的规则固定如下：

- `minimumClientVersion` 失败：直接阻止进入受支持运行态
- `Mod Protocol` 不兼容：必须先更新 Mod，不允许把必需能力留到运行中才失败
- 配置 schema version 不兼容：必须重写配置或更新对应组件
- schema 协商失败：优先按 capability 降级；只有主链路必需 schema 缺失时才返回 `protocol_version_unsupported`
- `requiredCapabilities` 不兼容：必须阻止启动并给出明确修复提示

更新顺序必须固定为：

1. 先更新 `Launcher`
2. 由 `Launcher` 携带同版本 `Runtime`
3. 启动游戏前检查目标游戏 Mod 是否满足 `Mod Protocol` 兼容要求
4. 若 Mod 不兼容，则先更新 Mod，再允许启动游戏
5. 若 Mod 更新失败，则阻止启动，并提供修复或重试入口

回滚规则必须固定为：

- `Launcher + Runtime` 只作为同一版本包整体回滚，不允许只回滚 Runtime
- Mod 若已升到更高兼容版本，旧版 `Launcher` 不得强行启动不兼容 Mod
- 云端最低版本提升后，旧版 `Launcher` 必须被阻止继续进入受支持运行态

M1 不追求复杂的长期多版本兼容体系，而是优先保证快速升级到当前受支持版本。

### 13.4 Mod 版本包分发策略

`M1` 当前明确面向中国国内，因此 Mod 版本包不必一开始就引入对象存储分发体系。

第一阶段默认采用以下分工：

- `Gitee Releases`：作为 Mod 版本包的默认托管位置
- `Cloud Control Service`：作为平台权威索引面
- `Launcher`：作为下载、校验、安装、切换、升级与回退执行器

必须明确以下边界：

- `Gitee Releases` 存放每个 Mod 的正式发布 artifact
- `Cloud Control Service` 不把 Gitee 当权威配置源，而是保存平台自己的 manifest、兼容矩阵、哈希和发布状态
- `Launcher` 不直接相信某个 release 页面展示的信息，而是只相信平台下发的 manifest
- `M1` 不默认依赖玩家手动去 Gitee 页面下载和覆盖文件

若后续出现下载量上升、包体明显增大、灰度发布、差分更新或带宽成本问题，可在不改变 Launcher 版本治理模型的前提下，把二进制托管平滑迁移到对象存储；但这不是 `M1` 默认方案。

### 13.4.1 Mod Artifact Repository 模型

为了支持升级和回退，Mod 代码包不能采用“只保留当前一份文件并反复覆盖”的模式。

`Launcher` 本地必须维护独立的 `Mod Artifact Repository`，最少包含：

- `repo/{gameId}/{modId}/{artifactVersion}/`
- `active/{gameId}/{modId}/current.json`
- `downloads/`

规则固定如下：

- 每个 `artifactVersion` 都是不可变版本包
- `active` 只记录当前启用版本，不直接存放唯一代码副本
- 升级不是覆盖旧文件，而是下载新 artifact 后切换 `active pointer`
- 回退不是“恢复被覆盖的文件”，而是切回先前保留的 artifact 版本
- 正式版本包至少必须包含 `manifest.json`、包哈希、构建版本和兼容信息

### 13.4.2 平台 Manifest 规则

平台必须维护自己的权威 manifest，而不是把 Git 平台页面当协议。

每个可分发 Mod 版本至少必须具备以下 manifest 字段：

- `gameId`
- `modId`
- `artifactVersion`
- `modDataSchemaVersion`
- `translatorContractVersion`
- `supportedLauncherRange`
- `supportedRuntimeRange`
- `supportedProtocolVersion`
- `downloadUrl`
- `sha256`
- `releaseChannel`
- `publishedAt`
- `rollbackTargets`

这里必须明确：

- `artifactVersion` 是代码包版本
- `modDataSchemaVersion` 是 Mod 数据结构版本
- `translatorContractVersion` 是翻译层契约版本
- 三者不得偷懒合并成同一个概念

### 13.4.2.1 `manifest.json` 样例

`M1` 可分发 Mod 的 `manifest.json` 可以采用如下结构：

```json
{
  "manifestVersion": 1,
  "gameId": "stardew-valley",
  "modId": "all-game-in-ai-stardew",
  "artifactVersion": "1.3.2",
  "modDataSchemaVersion": 4,
  "translatorContractVersion": "2.1.0",
  "releaseChannel": "stable",
  "publishedAt": "2026-03-26T18:30:00+08:00",
  "supportedLauncherRange": ">=1.3.0 <2.0.0",
  "supportedRuntimeRange": ">=1.3.0 <2.0.0",
  "supportedProtocolVersion": "1.4",
  "supportedFacetSchemas": {
    "conversationFacet": ">=1.0 <2.0",
    "relationshipFacet": ">=1.1 <2.0",
    "inventoryFacet": ">=1.0 <2.0"
  },
  "supportedCommandSemantics": [
    "transactional"
  ],
  "requiredCapabilities": [
    "conversation",
    "memory"
  ],
  "download": {
    "provider": "gitee-releases",
    "url": "https://gitee.com/example/all-game-in-ai-stardew/releases/download/v1.3.2/mod.zip",
    "sha256": "4c0f0d8f8b1ef0d8d4f7f2f7a9d267f8842b0c7c6c6d9a1d04d5a4c51c7cbe61",
    "sizeBytes": 3812456
  },
  "packageLayout": {
    "archiveFormat": "zip",
    "entryRoot": "mod/",
    "deploymentMethod": "copy",
    "targetPaths": [
      "Mods/AllGameInAI/"
    ]
  },
  "dataPolicy": {
    "stateScope": "per_game_profile",
    "requiresBackupBeforeMigration": true,
    "rollbackRequiresStateCompatibility": true
  },
  "migrations": {
    "fromSchemaVersions": [
      2,
      3
    ],
    "targetSchemaVersion": 4
  },
  "rollbackTargets": [
    "1.3.1",
    "1.3.0"
  ],
  "saveCompatibility": {
    "minimumSupportedSaveMarker": 12,
    "notes": "Rollback below 1.3.0 requires restoring a previous mod-state backup."
  }
}
```

该样例中的字段分工必须明确如下：

- `manifestVersion`：manifest 自身 schema 版本
- `artifactVersion`：代码包版本
- `modDataSchemaVersion`：本地 `mod-state` 结构版本
- `translatorContractVersion`：翻译层契约版本
- `supportedLauncherRange`、`supportedRuntimeRange`、`supportedProtocolVersion`：版本兼容边界
- `supportedFacetSchemas`、`supportedCommandSemantics`、`requiredCapabilities`：运行时协商与 capability 启用依据
- `download`：真实分发来源与校验信息
- `packageLayout`：安装包布局与部署方式
- `dataPolicy`：备份、迁移、回退的数据安全规则
- `migrations`：允许从哪些旧 schema 升级到当前 schema
- `rollbackTargets`：允许直接切换回的旧 artifact 版本
- `saveCompatibility`：给 Launcher 和玩家展示的存档兼容提示

在 `M1` 中，`Launcher` 至少必须把 `download`、`packageLayout`、`dataPolicy`、`rollbackTargets` 视为强制理解字段；缺任一项时，不得进入自动升级或自动回退流程。

### 13.4.3 M1 发布与下载规则

`M1` 的默认发布链路固定如下：

1. 开发侧为目标 Mod 构建 `artifactVersion`
2. 将正式包上传到 `Gitee Releases`
3. 将对应 manifest、哈希和兼容矩阵发布到 `Cloud Control Service`
4. `Launcher` 只从平台 manifest 获取下载地址和版本策略
5. `Launcher` 下载后必须做哈希校验，再进入安装或切换流程

同时必须固定：

- `M1` 默认以 `stable` 通道为主
- 若后续需要 `beta/canary`，也必须通过 manifest 显式声明
- `Launcher` 不得直接扫 Git 仓库 tag 或 release 列表来推断最新版本

### 13.5 Mod 数据、升级与回退策略

为了避免升级或回退导致数据丢失，必须把以下三类对象彻底分离：

- `Mod Artifact`
- `Mod State`
- `Game Save`

规则固定如下：

- `Mod Artifact` 是只读代码包，不承载运行期可变状态
- `Mod State` 必须独立于 artifact 存储
- `Game Save` 不是 Mod 状态数据库，不得承载完整 AI 运行状态
- 若某些游戏必须把少量信息写入游戏存档，也只能写最小镜像或引用标识，不得把完整 AI 状态直接塞进游戏存档

`Launcher` 本地至少必须维护：

- `mod-state/{gameId}/{modId}/state.db`
- `mod-state/{gameId}/{modId}/backups/`
- `mod-state/{gameId}/{modId}/migration-log/`

### 13.5.1 升级规则

升级流程必须固定如下：

1. 下载新的 `artifactVersion`
2. 校验与当前 `Launcher`、`Runtime`、`Mod Protocol` 的兼容性
3. 比较 `modDataSchemaVersion`
4. 若需要迁移，先自动备份当前 `Mod State`
5. 迁移成功后，才允许切换 `active pointer`
6. 迁移失败时，必须保持旧版本继续可用，不得切到半升级状态

这里必须明确：

- 不允许对生产状态做无备份 destructive migration
- 不允许先切版本、后迁移数据
- 迁移前必须让玩家能看到风险和备份点

### 13.5.2 回退规则

回退不是简单切代码版本，必须同时检查数据兼容性。

回退流程必须固定如下：

1. 玩家选择目标旧版本
2. `Launcher` 检查目标版本是否兼容当前 `modDataSchemaVersion`
3. 若兼容，则切换 `active pointer`
4. 若不兼容，则只能：
   - 恢复旧备份状态后再回退
   - 或明确阻止回退并提示原因

必须明确以下硬规则：

- 不允许在数据不兼容时“强行回退代码先试试”
- 不允许因为回退代码而自动删除当前新版本状态
- 回退动作必须生成审计记录，至少记录 `fromVersion`、`toVersion`、`stateBackupId`
- `Launcher + Runtime` 的整体回退规则，不能替代 Mod 自己的 artifact/state 回退规则

### 13.5.3 玩家体验规则

玩家看到的应该是“版本切换”，而不是“文件覆盖”。

因此 `Launcher` 至少需要向玩家展示：

- 当前 Mod 版本
- 可升级版本
- 可回退版本
- 当前数据 schema 状态
- 最近一次备份时间
- 当前操作是否 `safe_to_upgrade`
- 当前操作是否 `safe_to_rollback`

若回退需要恢复旧状态，必须明确提示，不允许伪装成一键无损回退。

## 14. 第一阶段推荐结论

综合本轮设计，第一阶段推荐技术路线如下：

- `Launcher`：`WPF + .NET 10`
- `Local Runtime`：`ASP.NET Core + Microsoft Agent Framework + Capability Host + SQLite`
- `Cloud Control Service`：`ASP.NET Core Web API + PostgreSQL`
- `Hosted Capability Service`：平台托管高级 AI 能力执行面
- `Shared Contracts`：独立 `C#` 类库
- `Game Adapter Mods`：按各游戏成熟生态实现，通过统一 `Game Event / Game Command` 协议接入
- 授权模式：`启动强在线 + 运行期短时令牌续租`
- `M1` 商业化模型：`按游戏基础包 + 高级云能力分项计次`
- 玩家模型 API Key：`仅用于玩家自带 Key 的文本链路，存于 Windows 受保护存储`

该方案的核心取向是：

- 用 `WPF` 换取 Windows 首发稳定性
- 用 `.NET 10 + Microsoft Agent Framework` 统一核心 AI 主栈
- 用 `Capability Host` 支撑每个游戏的特色能力和未来未知功能
- 用事件/命令桥接和薄适配层支持多游戏接入
- 用启动强在线、短时令牌续租、按游戏基础准入与高级云能力分项计次建立商业化控制能力
- 用 `Hosted Capability Service` 承接高级 AI 能力的鉴权、计次、供应商调用与审计

第一阶段最重要的架构判断现在可以明确写死：

- `Runtime` 的统一边界是“统一 AI 能力运行时”，不是“统一聊天接口后端”
- `M1` 的能力边界是“会话内能力 + 可持久化状态”，不承诺真正后台常驻能力
- 按次计费的高级能力在架构上首先是 `Hosted Capability`，其次才是商业化条目
- 未来大多数新功能应通过新增 `Capability` 接入
- 只有少数跨代能力才应升级为新的平台子系统
- `M1` 不做细粒度功能解锁，但必须从现在开始兼容能力级开关、灰度和未来 entitlement 扩展

## 15. 非目标与保留项

本文档当前不展开以下内容的详细实施方案：

- 各具体游戏 Mod 的逐项技术细节
- AI 视频、音乐、绘画能力的具体执行链路
- 复杂反作弊与内容治理专项规则
- 对同机、同用户态本地进程的强对抗反作弊或反破解
- 跨平台 Launcher 的后续实现策略
- 微服务化拆分与分布式治理

本文档已经确定 Runtime 的能力扩展方向，但以下内容仍留待进入对应里程碑前形成专项设计：

- `Capability` 的精确接口定义与装配方式
- `Game Event / Game Command` 的共享契约字段明细
- 哪些能力属于平台级能力，哪些能力属于单游戏能力
- 能力级 entitlement、灰度和遥测的服务端策略
- 长生命周期能力的资源配额、节流和故障隔离策略
- 真正独立于 `Launcher` 存活的后台常驻能力与重连模型

这些内容可在进入对应里程碑前，分别形成专项设计文档。
