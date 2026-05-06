# Hermes-Desktop Agent 指南

本文件作用于整个 `D:\GitHubPro\Hermes-Desktop` 仓库。更深层目录里的 `AGENTS.md` 优先级更高；尤其是修改 `Desktop/HermesDesktop/**` 时，必须同时遵守 `Desktop/HermesDesktop/AGENTS.md` 以及它要求先读的 `.github/instructions/*.instructions.md`。

## 沟通与执行

- 默认用中文向用户汇报进展、风险和结果；代码标识符、命令、路径、API 名称保持原文。
- 在明确、低风险、可逆的任务上直接执行到完成，不把普通检查、构建、测试交回给用户。
- 工作区可能已有用户改动；不要回退、覆盖或清理自己没造成的变更。开始改动前先看 `git status --short --branch`。
- 搜索文件和符号优先用 `rg` / `rg --files`。
- 手工改文件使用 `apply_patch`；不要用 shell 拼接重写文件。
- 如果目标是“补文档 / 校准事实 / 更新指南”，先从源码和 `.csproj` 建证据，再落文档，不要拿旧 README 或 plan 当权威。

## 先读这些入口

- `HermesDesktop.sln` / `HermesDesktop.slnx`：仓库主解决方案与项目入口。
- `Desktop/HermesDesktop/AGENTS.md`：WinUI 3 桌面壳专用规则；改这个子树前必须先读。
- `Desktop/HermesDesktop/.github/instructions/*.instructions.md`：桌面子项目的 accessibility、security、performance、code-quality、testing、WinUI API 规则。
- `Desktop/HermesDesktop/README.md`：桌面壳运行方式、`run-dev.ps1` 用法和启动排障。
- `run-desktop.ps1` 与 `scripts/*.ps1`：本仓库日常开发、打包、部署的真实命令入口。
- `Desktop/HermesDesktop/App.xaml.cs`：桌面宿主组合根，当前最重要的运行时事实来源。
- `src/runtime/AgentCapabilityAssembler.cs`：Hermes-native 内建工具面与 prompt 装配入口。

## 解决方案 / 项目结构

当前解决方案包含 6 个项目：

- `Desktop/HermesDesktop/`：WinUI 3 / Windows App SDK 桌面壳，当前开发模式是 unpackaged `dotnet run`，不是日常 MSIX-only 工作流。
- `Desktop/HermesDesktop.Tests/`：主测试项目，`net10.0` + MSTest + Moq，覆盖桌面宿主、runtime、Stardew/NPC 等主链路。
- `Desktop/HermesDesktop.Package/`：桌面打包项目。
- `src/`：Hermes 核心运行时与 CLI，包含 agent、context、memory、skills、mcp、tasks、runtime、dreamer、wiki、games/stardew 等模块。
- `Mods/StardewHermesBridge/`：星露谷 SMAPI bridge。
- `tools/Hermes.SmokeProbe/`：发布后烟测工具。

仓库里还要注意这些非 solution 目录：

- `Mods/StardewHermesBridge.Tests/`：bridge 测试，当前 `net6.0`。
- `skills/`、`souls/`：随仓库发布的 skill 与 soul 资产；`publish-portable.ps1` 会打进产物。
- `scripts/`：portable/MSIX 发布、部署、证书、配置同步、repo temp 等脚本。
- `external/`、`参考项目/`、`其他资料/`：参考资料和上游/样例代码。除非任务明确要求，默认只读。

## 常用命令

优先用仓库自带脚本；它们比手写 `dotnet` 命令更贴近当前项目的真实运行方式。

- 根目录快速启动桌面壳：
  ```powershell
  .\run-desktop.ps1
  .\run-desktop.ps1 -Rebuild
  ```
- 从桌面项目目录做开发启动：
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\run-dev.ps1
  powershell -ExecutionPolicy Bypass -File .\run-dev.ps1 -ShowLocalDetails
  ```
- 直接构建桌面壳：
  ```powershell
  dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
  ```
- 查看解决方案项目：
  ```powershell
  dotnet sln HermesDesktop.sln list
  ```
- 运行主测试项目：
  ```powershell
  dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
  ```
- 按测试类筛选运行：
  ```powershell
  dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Dreamer.DreamerStatusTests"
  ```
- 运行单个测试方法：
  ```powershell
  dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Dreamer.DreamerStatusTests.GetSnapshot_InitialState_PhaseIsIdle"
  ```
- 运行 Stardew bridge 测试：
  ```powershell
  dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
  ```
- 生成 portable 版本：
  ```powershell
  .\scripts\publish-portable.ps1
  .\scripts\publish-portable.ps1 -Zip
  ```
- 生成签名 MSIX：
  ```powershell
  .\scripts\publish-msix.ps1 -CertificatePath "Desktop\HermesDesktop\packaging\dev-msix.pfx" -CertificatePassword dev
  ```
- Stardew 本地配置同步：
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\sync-stardew-npc-config.ps1
  ```
- 运行 smoke probe：
  ```powershell
  dotnet build .\tools\Hermes.SmokeProbe\Hermes.SmokeProbe.csproj -c Release
  ```

## Lint / 静态分析

- 仓库当前没有独立的 lint 脚本。
- `Desktop/HermesDesktop/.github/instructions/code-quality.instructions.md` 是桌面子项目的静态分析、StyleCop、命名与清理规则来源。
- 对桌面子树来说，通常通过 `dotnet build` 暴露 analyzers / code style / warnings；不要再凭空发明一个不存在的 lint 流程。

## 手测日志检查

- 手测 Stardew / Hermes 时，不要先猜日志位置，也不要先全仓库搜索；先按固定顺序看：
  - 桌面主日志：`%LOCALAPPDATA%\hermes\hermes-cs\logs\hermes.log`
  - 桌面启动失败：`%LOCALAPPDATA%\hermes\hermes-cs\logs\desktop-startup.log`
  - SMAPI / bridge：`%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`
  - bridge discovery：`%LOCALAPPDATA%\hermes\hermes-cs\stardew-bridge.json`
  - NPC runtime：`%LOCALAPPDATA%\hermes\hermes-cs\runtime\stardew\games\stardew-valley\saves\<saveId>\npc\<npcId>\profiles\<profileId>\activity\runtime.jsonl`
- 默认先看 `hermes.log` 和 `SMAPI-latest.txt` 的最新 200 行，再看对应 NPC 的 `runtime.jsonl`；不要一上来把所有文件全量扫一遍。
- 常用命令：
  ```powershell
  Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\hermes.log" -Tail 200
  Get-Content "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" -Tail 200
  Get-ChildItem "$env:LOCALAPPDATA\hermes\hermes-cs\runtime\stardew\games\stardew-valley" -Recurse -Filter runtime.jsonl | Sort-Object LastWriteTime -Descending | Select-Object -First 5 FullName,LastWriteTime
  ```
- 先分层判断再追根因：
  - `runtime.jsonl` 里只有 `wait` / 没有 `local_executor`、`host_action`：先查父层决策是不是没发出行动。
  - `runtime.jsonl` 里有 `local_executor_blocked:*`：先查本地执行层 / tool-call / delegation lane。
  - `runtime.jsonl` 里有 `host_action stardew_speak` 但没有 `move`：说明父层在说话，不代表本地模型承担了动作。
  - SMAPI 有 `task_move_enqueued` / `task_running` / `task_completed`：说明 bridge 收到命令了；没有这些就先别怀疑游戏寻路。
  - `hermes.log` 里先看 `StardewNpcAutonomyBackgroundService`、`NpcAutonomyLoop`、`ChatLaneClientProvider` 这三类日志，优先回答“有没有唤醒 / 走了哪条模型 lane / 有没有进入 LLM turn”。

## 当前技术事实

这些事实来自当前 `.csproj`，后续若有差异，以项目文件为准。

- 桌面应用：`Desktop/HermesDesktop/HermesDesktop.csproj`
  - `TargetFramework`: `net10.0-windows10.0.26100.0`
  - `RootNamespace`: `HermesDesktop`
  - `Platforms`: `x64`
  - `RuntimeIdentifier`: `win-x64`
  - `Microsoft.WindowsAppSDK`: `1.7.250310001`
  - `WindowsPackageType`: `None`
- 核心库：`src/Hermes.Core.csproj`
  - `TargetFramework`: `net10.0`
  - 主要依赖：`JsonSchema.Net`、`Microsoft.Data.Sqlite`、`Cronos`、`Microsoft.Extensions.*`
- CLI：`src/Hermes.Agent.csproj`
  - `TargetFramework`: `net10.0`
  - 引用 `Hermes.Core.csproj`
- 桌面测试：`Desktop/HermesDesktop.Tests/HermesDesktop.Tests.csproj`
  - `TargetFramework`: `net10.0`
  - MSTest + Moq + coverlet
- Stardew bridge 测试：`Mods/StardewHermesBridge.Tests/Mods.StardewHermesBridge.Tests.csproj`
  - `TargetFramework`: `net6.0`

## 运行时架构总览

### 1. Desktop 是组合根，不是薄壳

- `Desktop/HermesDesktop/App.xaml.cs` 负责注册目录结构、DI、主 agent、子 agent、MCP、Dreamer、NPC runtime、权限、Buddy、wiki、analytics、任务系统与工具注册。
- 桌面应用不是“套一层聊天 UI”。当前产品价值就在本地宿主：本地 agent 生命周期、skills、Dreamer、NPC runtime、权限、日志、workspace、配置都由它承载。

### 2. `src/` 是核心运行时，Desktop 负责装配

- `src/Core`：agent loop、system prompt、tool 调用脚手架。
- `src/Context`：上下文裁剪、prompt 拼装、session state。
- `src/Memory`、`src/search`：memory lifecycle、transcript recall、memory orchestration。
- `src/skills`：`SKILL.md` 扫描、解析、管理、调用。
- `src/Tools`：Hermes-native 工具实现。
- `src/agents`、`src/coordinator`：subagent、worktree/remote isolation、多 worker 编排。
- `src/mcp`：MCP client、transport、tool discovery。
- `src/tasks`：宿主级任务系统、session todo projection / archive。
- `src/dreamer`、`src/dream`：Dreamer 背景系统与旧 AutoDream 代码。
- `src/runtime`：Desktop/NPC 共享装配、NPC runtime host/supervisor、autonomy loop 等。
- `src/games/stardew`：Stardew bridge 对接、私聊、autonomy、debug、world coordination。
- `src/wiki`、`src/buddy`、`src/soul`、`src/plugins`：知识库、伙伴系统、身份系统、插件系统。

### 3. Desktop agent 与 NPC agent 走同一套能力装配

- 通用内建工具注册统一走 `src/runtime/AgentCapabilityAssembler.cs`。
- 桌面主 agent 通过 `App.RegisterAllTools(...)` 注册 Hermes-native 工具，再叠加 MCP 发现到的工具。
- NPC runtime 通过 `NpcRuntimeContextFactory`、`NpcRuntimeSupervisor` 复用同一套装配，再叠加 Stardew 专用工具 surface。

### 4. UI 只是宿主前台，不是全部系统

- `MainWindow` 是导航壳，当前主导航项：`Dashboard`、`Chat`、`Agent`、`Skills`、`Memory`、`Buddy`、`Settings`。
- `ChatPage` 是主工作台，绑定 `HermesChatService`，并包含 `Sessions / Tasks / Replay / Buddy` 等侧边面板。
- `DashboardPage` 聚合 session/tool/skill/LLM、Dreamer、NPC runtime、logs/path 快捷入口。
- `AgentPage` 是 soul / user / saved agents / NPC runtime 的配置与调试工作台。
- `SkillsPage`、`MemoryPage`、`BuddyPage`、`SettingsPage` 各自有真实功能，不是占位页。

## 核心运行时主线

### 1. Transcript + Soul + Memory + Active Todo 才是上下文主线

- `TranscriptStore` 是 SQLite-first 持久化层；session message 与 activity 的权威存储在 `state.db`，旧 JSONL 只做兼容。
- `SoulService` 提供 `SOUL.md` / `USER.md` / mistakes / habits 等身份连续性。
- `MemoryManager` 是固定文件型 curated memory，不是随便挂 KV 的 memory 插件系统。
- `ContextManager` 不会把整个 transcript 原样塞给模型，而是按 recent window、summary、soul、plugin blocks、retrieved context、active todo 组 prompt。
- `TurnMemoryCoordinator` 与 `HermesMemoryOrchestrator` 负责 turn 级 recall / sync / compression。
- `SessionTaskProjectionService` 会从 transcript 中持久化的 `todo` / `todo_write` tool result 回放出会话任务快照，再注入活跃任务。

### 2. `todo` 是会话级任务循环；`TaskManager` 是宿主级长期任务系统

- `todo` / `todo_write` 的权威运行时状态在 `SessionTodoStore`，按 `sessionId` 分桶。
- `TaskManager` 是另一套长期 JSON 文件任务系统，位于项目目录 `tasks/`，用于宿主级结构化任务、依赖和优先级管理。
- `CoordinatorService` 会把复杂任务拆成 subtasks，并落到 `TaskManager`。
- 结论：不要把 `todo` 当成 `TaskManager` 的 UI 壳；这两套能力当前没有打通成一层。

### 3. Skills 是 Markdown 资产，不是硬编码 switch

- `SkillManager` 会递归扫描 `skillsDir` 下的 `SKILL.md`，解析 frontmatter 和正文，动态装入内存。
- 桌面启动时会通过 `BundledSkillCatalogService` 把仓库内 `skills/` reconcile 到活动技能树。
- 运行时技能相关工具是 `skills_list`、`skill_view`、`skill_manage`、`skill_invoke`。
- `AgentCapabilityAssembler.CreatePromptBuilder(...)` 会把 skills mandatory prompt 注入 system prompt，所以 agent 应先发现/加载相关 skill，而不是绕开 skill 系统硬写流程。

### 4. MCP 是真实接线能力，不是文档占位

- `McpManager` 会从配置文件加载 MCP server 配置、建立连接，并把发现到的 MCP tools 包装成统一 `ITool`。
- 桌面启动后会把已发现的 MCP tools 注册进主 tool registry；NPC runtime 也能通过 tool surface 看到这些外部工具。
- 但“某个 MCP server 一定可用”并不是仓库保证；是否有具体外部能力取决于运行时 `mcp.json` 和本地环境。

### 5. Chat runtime 当前主链是 pure C# in-process

- `HermesChatService` 是桌面聊天主链，负责会话、调度消息、权限模式、scheduled chat message、task projection 与 transcript 协作。
- 不要再把 Python sidecar 当成当前主链路前提。
- 如果 README 或旧文档还残留 sidecar 叙述，以代码为准。

## 已实现能力清单

下面这些能力是当前代码里已经真实存在、且适合写进仓库级指南的。

### A. Hermes-native 内建工具

当前内建工具注册入口在 `src/runtime/AgentCapabilityAssembler.cs`，已注册：

- `todo`
- `todo_write`
- `schedule_cron`
- `agent`
- `memory`
- `session_search`
- `skills_list`
- `skill_view`
- `skill_manage`
- `skill_invoke`
- `checkpoint`

### B. `schedule_cron` / 定时任务

- `src/Tools/ScheduleCronTool.cs` 已实现 `schedule_cron`，支持 cron 表达式校验、`Recurring`、`Durable`、按 `SessionId` 绑定任务。
- `InMemoryCronScheduler` 已实现 `TaskDue` 事件、`Schedule`、`Cancel`、`GetAllTasks`、`GetNextRun`。
- `HermesChatService` 与 `StardewNpcAutonomyBackgroundService` 都会订阅 `ICronScheduler.TaskDue`，说明它不只是“能注册”，还会驱动桌面聊天与 NPC 自主循环。
- 当前实现是 in-memory scheduler；`Durable` 字段已进入 schema，但不要把它写成“已持久化 cron 系统”。

### C. delegate / subagent / agent tool

- `src/Tools/AgentTool.cs` 的 `agent` 工具已实现，能基于受限工具集在同一 chat client 上拉起隔离上下文子 agent。
- 预置 agent 类型包括 `researcher`、`coder`、`analyst`、`planner`、`reviewer`、`general`，并按类型限制可见工具。
- 这是“模型内工具式子 agent”，不是完整的 mailbox/team 产品面。

### D. `AgentService` / worktree / remote isolation

- `src/agents/AgentService.cs` 已实现 `SpawnAgentAsync`，支持 `none`、`worktree`、`remote` 三类隔离路径。
- worktree 模式会创建/清理 git worktree。
- remote 模式会通过 `ssh` / `scp` 建远端临时工作区。
- 这是真实 agent spawn 能力，不只是接口雏形。
- 但 remote isolation 更适合写成“代码层支持的可选路径”，不要写成桌面默认稳定交付能力。

### E. `CoordinatorService` / 多 worker 编排

- `src/coordinator/CoordinatorService.cs` 已实现复杂任务分解、创建宿主级 task、并行 spawn worker、等待依赖满足、综合结果。
- 还支持 brief-driven orchestration，不只是一条 LLM decomposition 路径。
- 文档里应写成“多 worker orchestration engine 已实现”，不要写成“完整 team UI / mailbox 协作产品已经交付”。

### F. memory / session_search / todo / checkpoint

- `memory`：`src/Tools/MemoryTool.cs`，Python-compatible curated memory tool。
- `session_search`：`src/Tools/SessionSearchTool.cs`，跨 session transcript recall。
- `todo` / `todo_write`：`src/Tools/TodoWriteTool.cs`，`todo_write` 是兼容 alias。
- `checkpoint`：`src/Tools/CheckpointTool.cs`，支持创建、恢复、列出目录快照。

### G. wiki / soul / buddy / plugin

- `wiki`：`App.xaml.cs` 已注册 `WikiConfig`、`LocalWikiStorage`、`WikiSearchIndex`、`WikiManager`。
- `soul`：`SoulService`、`SoulExtractor`、`SoulRegistry`、`AgentProfileManager` 已接入桌面宿主。
- `buddy`：`src/buddy/Buddy.cs` + `BuddyService` 已实现 deterministic gacha、AI soul 生成、持久化、ASCII renderer，桌面中有 `BuddyPage` 和 `BuddyPanel`。
- `plugin`：`PluginManager` 与 `BuiltinMemoryPlugin` 已接线，memory plugin block 会进入系统 prompt。
- `Buddy` 当前应理解为“已接线的本地伙伴/身份数据能力 + 桌面入口”，不要写成完整社交 agent 子系统。

### H. 权限、workspace 与运行时状态

- `WorkspacePermissionRuleStore` + `PermissionManager` 已在桌面宿主注册，支持 workspace-scoped remembered permissions。
- `PermissionDialogService` 已把 permission prompt 接到 WinUI `ContentDialog`。
- `HermesChatService` 暴露 `PermissionMode` 切换与 remembered permission 清理，`ChatPage` 已有对应 UI。
- `HermesEnvironment` 提供 workspace / logs / config / privacy display 辅助。
- `RuntimeStatusService` 用于桌面状态聚合。
- 这部分应描述为“工作区作用域权限记忆 + 桌面权限交互”，不要夸大成完整沙箱策略平台。

### I. Dreamer

- 桌面启动时会 `StartDreamerBackground(...)`，创建 `DreamerRoom`、`RssFetcher`、`DreamerService` 并起后台循环。
- `DreamerService` 当前职责是：周期性读 transcript / inbox / RSS，跑 local-model walk，做 signal scoring，必要时触发 build sprint，并写本地 digest。
- `DreamerRoom` 的工作区在 `HERMES_HOME/dreamer/`，里面有 `walks/`、`projects/`、`inbox/`、`inbox-rss/`、`feedback/digests/`、`signal-state.json`、`signal-log.jsonl`、`DREAMER_SOUL.md`。
- Dashboard 和 Settings 已接入 Dreamer 状态与配置。

### J. Stardew / NPC runtime / autonomy / private chat

- `NpcRuntimeHost`、`NpcRuntimeSupervisor`、`NpcRuntimeContextFactory`、`NpcRuntimeWorkspaceService` 已真实接线。
- `StardewNpcAutonomyBackgroundService`、`StardewNpcPrivateChatAgentRunner`、`StardewPrivateChatRuntimeAdapter`、`StardewNpcAutonomyPromptSupplementBuilder`、`StardewAutonomyTickDebugService` 已注册进桌面宿主。
- `WorldCoordinationService`、`ResourceClaimRegistry`、`NpcAutonomyBudget` 说明 NPC 自主循环不是简单 demo。
- Dashboard / AgentPage 都已经提供 NPC runtime 状态与工作区入口。
- `NpcRuntimeStateStore` 与 `NpcRuntimeTaskHydrator` 说明 NPC runtime 还有自己的状态持久化与 todo hydration，而不是每次冷启动都从零开始。

## 项目架构关键判断

### 1. Desktop 不是“只包 Hermes CLI 的前端”

- 当前桌面宿主自己装配 transcript、memory、skills、wiki、plugin、agent、subagent、coordinator、MCP、Dreamer、NPC runtime、permission、Buddy。
- 所以做设计或排障时，不要先入为主地把问题归因到“UI 只是壳，逻辑都在外面”。

### 2. NPC runtime 不是第二套 Hermes

- `NpcRuntimeContextFactory` 会为每个 NPC 建独立 namespace 下的 `SoulService`、`MemoryManager`、`TranscriptStore`、`SessionTodoStore`、`ContextManager`、`ToolRegistry`。
- `NpcRuntimeSupervisor` 再把通用 Hermes-native 工具和 Stardew 专用工具接进去。
- 结论：每个 NPC 本质上是“正常 Hermes agent + 自己的 home / memory / transcript / todo / tool surface”，不要在宿主侧再发明第二套 NPC 任务或记忆系统。

### 3. delegation lane 与 player-visible lane 已经分离

- `ChatLaneClientProvider` 与 Desktop/Stardew runtime 会分别拿 `ChatRouteNames.Delegation`、`StardewAutonomy`、`StardewPrivateChat` 等 lane client。
- `sync-stardew-npc-config.ps1` 也明确写 delegation lane 只用于 child agent work。
- 文档里可以写“存在专用 delegation lane 支持子 agent / NPC autonomy”，不要写成“任意多层 agent 树默认打开”。
- 最近这条设计还要明确成“父云子本地”：parent decision / player-facing lane 继续走云模型，child agent / local executor / delegation work 才走本地小模型 lane。
- 当前本地小模型 lane 是配置层概念，走 OpenAI-compatible `delegation` endpoint；当前仓库文档与脚本示例里对应的是本地 `LM Studio`。
- 默认不要把 `stardew_autonomy` 或 `stardew_private_chat` 直接改到本地小模型；除非产品设计明确改变，否则保持 parent-cloud / delegation-local 路由。

## 当前已实现能力与未实现边界

### 已实现并应按已实现处理

- Hermes-native 工具链：`todo`、`todo_write`、`schedule_cron`、`agent`、`memory`、`session_search`、skills 工具链、`checkpoint`
- Desktop 组合根、pure C# chat runtime、权限系统、workspace 权限记忆
- `AgentService`、`CoordinatorService`
- MCP client discovery 与工具注册
- Dreamer 背景循环
- wiki / soul / buddy / plugin / analytics
- Stardew NPC runtime、private chat、自主循环、runtime workspace 观测

### 已有代码，但文档里不要夸大

- `schedule_cron` 目前是 `InMemoryCronScheduler` 驱动；不要写成“持久化 cron 平台”。
- `agent` 工具可 spawn 子 agent，但这是受限工具面上的轻量 subagent，不等于完整多 agent 产品协作层。
- `AgentService` 支持 remote isolation，但是否可用依赖本机 ssh/scp 与环境，不要写成默认可用路径。
- `CoordinatorService` 是后端编排引擎；不要写成桌面里已有完整 team mailbox / inbox UI。
- `mixture_of_agents` 在仓库中有独立工具实现时，也先按“存在代码”看待；除非确认进入当前桌面主注册链，否则不要写成默认能力面。

### 不要误报为“当前默认主链路”或“完整已交付”

- `SendMessageTool` 没有现成实现。
- `MailboxService` / `TeamManager` 更像底层雏形，不要把它们当成已经完整接通的 agent 间消息系统。
- `src/dream/AutoDreamService.cs` 有代码，但当前桌面默认不走它；不要把它误判成现在线上主链路。
- 任何只在 docs / openspec / `.omx` 计划里的未来设计，不要写成已实现。
- README 里仍提到的 sidecar 表述，如果跟当前 C# 主链冲突，以代码为准。

## 项目级 OpenSpec / 产品约束

- 本仓库当前主线是接入 Stardew Valley，目标是多 NPC 村庄模式；MVP 先做 1-3 个 NPC 并发。
- 方案优先对齐 `external/hermescraft-main` 和 `external/hermes-agent-main` 的架构思想，只复用“Agent 通过工具理解世界、桥接层只暴露能力接口”的部分，不照搬游戏特有实现。
- 本项目 fork 自 `RedWoodOG/Hermes-Desktop`；禁止直接 push 到 `upstream`，所有修改推送到 `origin`，`upstream` 只用于 fetch。
- 预发布阶段只允许一条实现路径，禁止双轨、兼容分叉和影子实现。
- 游戏侧和桥接层只负责把世界接到 Hermes 上，不能接管、替代或干涉 Hermes 原生能力。
- 不得代写或维护 `SOUL.md`、`MEMORY.md`、`USER.md`，也不得维护任何等价的人格摘要、记忆摘要、身份快照或第二 tool lane。
- 宿主只提供事实、事件、工具、确认和执行结果，不替 NPC 决策；任何真实世界写操作都必须走宿主执行器。
- 每个原版 NPC 必须保持独立 `home`、独立会话和独立记忆边界；群聊、私聊、偷听、送礼、交易都要保持“Agent 提意图，宿主负责许可和执行”的边界。
- `tool` / `action` schema 只承载可执行契约，地点意义、角色偏好、移动与失败恢复分别写入 world / navigation / persona skill。
- 涉及 prompt / skill 边界的测试，必须走真实仓库资产注入路径；定位 repo 资产时从 `AppContext.BaseDirectory` 向上查找，不能只靠当前工作目录或 fixture 文本。
- 新问题优先检索 `openspec/errors` 和 `external/hermescraft`，把历史失败和参考方案对齐后再改。
- 第一阶段不做复杂经济系统、自定义素材或写死剧情编排。
- 后台自主任务必须有可观测日志、明确超时、资源释放和可重试路径；“看起来对但没生效”的问题先加诊断日志再推断根因。

## 常用运行日志

手测 Stardew/Hermes 时不要重新猜日志位置，优先看这些文件：

- Hermes 桌面主日志：`%LOCALAPPDATA%\hermes\hermes-cs\logs\hermes.log`
- 桌面启动异常：`%LOCALAPPDATA%\hermes\hermes-cs\logs\desktop-startup.log`
- SMAPI/Bridge 日志：`%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`
- bridge discovery：`%LOCALAPPDATA%\hermes\hermes-cs\stardew-bridge.json`
- NPC runtime 活动日志：`%LOCALAPPDATA%\hermes\hermes-cs\runtime\stardew\games\stardew-valley\saves\<saveId>\npc\<npcId>\profiles\<profileId>\activity\runtime.jsonl`
- Dreamer 工作区：`%LOCALAPPDATA%\hermes\dreamer\`

常用 PowerShell：

```powershell
Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\hermes.log" -Tail 200 -Wait
Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\desktop-startup.log" -Tail 200 -Wait
Get-Content "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" -Tail 200 -Wait
Get-ChildItem "$env:LOCALAPPDATA\hermes\hermes-cs\runtime\stardew\games\stardew-valley" -Recurse -Filter runtime.jsonl | Sort-Object LastWriteTime -Descending | Select-Object -First 5
```

## 修改前检查

- 先读用户目标，再定位既有实现，避免重复造轮子。
- 对 `Desktop/HermesDesktop/**` 做任何代码改动前，打开并遵守：
  - `Desktop/HermesDesktop/AGENTS.md`
  - 它要求的 `.github/instructions/*.instructions.md`
- UI/XAML 改动要检查可访问性、性能、字符串资源和 WinUI 模式。
- 涉及用户输入、HTTP、权限、密钥、文件系统访问时，先检查安全规则，不写硬编码密钥。
- 不新增依赖，除非用户明确要求或已有项目模式强烈支持；新增前说明理由和替代方案。
- 如果文档、计划、`.omx`/`.omc` 记录与源码现状冲突，以当前代码、`.csproj`、DI 注册链和真实脚本为准。
- 如果你要更新项目级文档，优先从 `App.xaml.cs`、`AgentCapabilityAssembler.cs`、相关 service/tool 实现回填事实，不要抄旧 README。

## 代码风格

- 保持改动小、可审查、可回退。
- 优先删除无用代码、复用现有 helper 和服务边界，再考虑新增抽象。
- 新增公共方法/类时补测试；窄改动配窄测试，跨模块行为配更广验证。
- 保持 nullable 语义，不用 `!` 掩盖真实空值问题。
- 避免把 UI 逻辑、持久化、网络调用和领域状态混在一个方法里；跟随现有 Services / Models / Views 分层。
- 对 Desktop 子树，遵守 `.editorconfig`、analyzer、StyleCop 和 `code-quality.instructions.md`；没有“先写完再统一修风格”的豁免。

## 提交信息

如果用户要求提交，commit message 使用 Lore 风格：第一行写“为什么改”，正文说明约束和取舍，并用 git trailer 记录有价值的信息，例如：

```text
Prevent silent session loss during replay restore

The replay loader now rejects partial state before it can overwrite the active session.

Constraint: Existing session files may be partially written after a crash
Rejected: Ignore malformed replay files | hides recoverable data issues
Confidence: high
Scope-risk: narrow
Tested: dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
Not-tested: Full packaged MSIX launch
```

## 完成标准

- 说明改了哪些文件、做了哪些简化或行为修复。
- 报告实际运行过的验证命令和结果。
- 明确剩余风险、未测范围或环境限制。
- 如果验证失败，继续定位和修复；不要在失败状态下声称完成。
