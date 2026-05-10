# HermesDesktop Agent 指南

本文件作用于 `Desktop/HermesDesktop/**`。仓库根 `AGENTS.md` 仍然生效；若两者冲突，以本文件为准。

## 先看这些文件

- `HermesDesktop.csproj`：当前 TFM、平台、打包模式、WinAppSDK 版本的唯一准确信息源。
- `App.xaml.cs`：桌面宿主的组合根；所有核心运行时能力都在这里注册与启动。
- `run-dev.ps1`：当前子项目目录下最可靠的本地启动/注册脚本。
- `README.md`：桌面壳的运行入口与启动排障。
- `.github/instructions/*.instructions.md`：按改动类型读取对应规则文件。

## 当前项目事实

这些结论以后续代码状态为准，优先级高于旧模板表述。

- 当前桌面项目：`net10.0-windows10.0.26100.0`
- 当前平台：`x64`
- 当前 `RuntimeIdentifier`：`win-x64`
- 当前默认项目配置：`WindowsPackageType=None`
- 支持 `launchSettings.json` 中的 `Unpackaged` 与 `Package` profile，但**日常开发不要假设这是一个 MSIX-only 项目**。
- `HermesMsixPublish=true` 时才进入 MSIX 发布路径。

## 常用命令

优先使用脚本，而不是手写一串易错的 `dotnet` / `Add-AppxPackage` 命令。

### 本地开发 / 启动

- 在当前目录启动桌面应用：
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\run-dev.ps1
  ```
- 启动并显示本地路径/端点细节：
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\run-dev.ps1 -ShowLocalDetails
  ```
- 从仓库根目录快速启动：
  ```powershell
  .\run-desktop.ps1
  .\run-desktop.ps1 -Rebuild
  ```

### 构建

- 在当前目录构建：
  ```powershell
  dotnet build .\HermesDesktop.csproj -c Debug -p:Platform=x64
  dotnet build .\HermesDesktop.csproj -c Release -p:Platform=x64
  ```

### 测试

- 运行桌面测试项目：
  ```powershell
  dotnet test ..\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
  ```
- 按测试类筛选：
  ```powershell
  dotnet test ..\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Dreamer.DreamerStatusTests"
  ```
- 按单个测试方法筛选：
  ```powershell
  dotnet test ..\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Dreamer.DreamerStatusTests.GetSnapshot_InitialState_PhaseIsIdle"
  ```

### 发布 / 打包

- 生成 portable 版本：
  ```powershell
  ..\..\scripts\publish-portable.ps1
  ..\..\scripts\publish-portable.ps1 -Zip
  ```
- 生成签名 MSIX：
  ```powershell
  ..\..\scripts\new-msix-dev-cert.ps1 -UpdateManifests
  ..\..\scripts\publish-msix.ps1 -CertificatePath "Desktop\HermesDesktop\packaging\dev-msix.pfx" -CertificatePassword dev
  ```
- 构建 smoke probe：
  ```powershell
  dotnet build ..\..\tools\Hermes.SmokeProbe\Hermes.SmokeProbe.csproj -c Release
  ```

## 组合根：Desktop 不是薄 UI 壳

`App.xaml.cs` 是本项目最重要的运行时入口。它当前会注册并装配：

- `TranscriptStore`、`SessionSearchIndex`
- `SessionTodoStore`、`SessionTaskProjectionService`
- `MemoryManager`、`HermesMemoryOrchestrator`、`TurnMemoryCoordinator`
- `SkillManager` 与 bundled skills reconcile
- `TaskManager`、`ICronScheduler`
- `SoulService`、`SoulExtractor`、`AgentProfileManager`
- `ContextManager`、`Agent`
- `AgentService`、`CoordinatorService`
- `McpManager`
- `DreamerStatus` 与 Dreamer 后台循环
- `NpcRuntimeSupervisor`、`NpcRuntimeHost`、`NpcRuntimeWorkspaceService`
- `StardewNpcAutonomyBackgroundService`
- `HermesChatService`、权限系统、运行时状态服务

启动时还会：

- 注册 Hermes-native 内建工具
- 初始化 MCP 并注册发现到的工具
- 启动 Dreamer 后台循环
- 启动 Stardew NPC autonomy 背景服务
- 绑定权限弹窗到原生 WinUI 对话框

## UI 大图景

### `MainWindow`

- 这是原生导航壳，不承载业务逻辑。
- 当前主导航项：`Dashboard`、`Chat`、`Agent`、`Skills`、`Memory`、`Settings`。

### `Views/ChatPage.xaml`

- 这是主工作台，不只是一个聊天框。
- 左侧是消息流、输入区、模型切换、权限模式切换。
- 右侧是与当前会话绑定的 `Sessions / Tasks / Replay` 面板。
- 如果你改聊天体验，通常还要同时检查：
  - `Views/Panels/SessionPanel*`
  - `Views/Panels/TaskPanel*`
  - `Views/Panels/ReplayPanel*`
  - `src/Desktop/HermesChatService.cs`

### `Views/DashboardPage.xaml`

- 这是运营/健康总览，不是主编辑面。
- 当前聚合：
  - session/tool/skill/LLM 状态
  - Dreamer 状态
  - NPC runtime 状态
  - recent sessions
  - Hermes home / config / logs 快捷入口

### `Views/AgentPage.xaml`

- 这是身份/人格/代理配置中心，不只是“关于页”。
- 当前承载：
  - SOUL / USER / saved agents / souls 模板
  - Stardew / NPC runtime 的工作台与调试入口

### `Views/SettingsPage.xaml`

- 这是运行时控制面板。
- 当前配置面覆盖：
  - 用户画像
  - provider / model / auth
  - agent 行为
  - memory
  - display/privacy
  - plugin
  - Dreamer
  - runtime 状态与路径

## 当前能力现状与边界

### 聊天运行时：已是纯 C# 主链

- `HermesChatService` 当前是 **pure C# in-process runtime**。
- 不要再把 Python sidecar 当成当前主链路前提。
- `README.md` 中仍有 sidecar 叙述时，以代码为准。

### Transcript / Memory / Todo

- `TranscriptStore` 是 SQLite-first 持久化层；不要再按旧 JSONL 主存储去理解会话主线。
- `MemoryManager` 是固定文件型 curated memory，不是数据库插件系统。
- `todo` / `todo_write` 的权威运行时状态在 `SessionTodoStore`。
- `SessionTaskProjectionService` 会从 transcript 中的 tool result 回放当前会话 todo。
- `TaskManager` 是另一套长期 JSON 文件任务系统；**不要把 `todo` 误认为 `TaskManager` 的 UI 壳**。

### Skills

- skills 当前是真实接线能力，不是文档摆设。
- `SkillManager` 会递归扫描 `skillsDir` 下的 `SKILL.md`。
- 启动时会先把 bundled skills reconcile 到活动技能树。
- 运行时工具面包括：
  - `skills_list`
  - `skill_view`
  - `skill_manage`
  - `skill_invoke`
- Chat 页 slash command 也会实际走 skill invocation。

### MCP

- MCP client 能力已接线。
- 启动时会从 `mcp.json` 搜索路径中加载配置、连接 server、注册发现到的工具。
- 但“某个具体 MCP 工具一定存在”不受仓库保证，取决于本机 `mcp.json` 和环境。

### Subagent / Coordinator

- `agent` 工具已实现。
- `AgentService` 与 `CoordinatorService` 已真实接线。
- 但不要把以下内容当成当前桌面产品面已经完整交付：
  - `SendMessageTool`
  - 完整 mailbox/team agent 协作界面
  - 任何只在 docs / plans / `.omx` 中出现的多 agent 未来设计

### Dreamer

- Dreamer 当前是**已启动的后台主线**，不是计划项。
- 启动时会 `StartDreamerBackground(...)`。
- 当前 Dreamer 主线职责：
  - 周期性读取 transcript / inbox / RSS
  - 运行 local-model walk
  - signal scoring
  - 触发 build sprint
  - 写本地 digest
- `DreamerRoom` 工作区位于 `HERMES_HOME/dreamer/`。
- Dashboard 和 Settings 都已接入 Dreamer 状态与配置。
- 旧 AutoDream 服务源码已移除；当前 Desktop 启动主链路是 `DreamerService` / `StartDreamerBackground(...)`。

### Stardew / NPC runtime

- 这是桌面宿主的真实运行时能力，不是只读展示。
- 每个 NPC runtime 会走自己的 namespace / transcript / memory / todo / tool registry。
- Dashboard 和 AgentPage 都已经提供了 NPC runtime 的状态与工作区入口。
- 修改这条链路时，优先从以下入口追：
  - `App.xaml.cs`
  - `src/runtime/*`
  - `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
  - `Views/AgentPage*`
  - `Views/DashboardPage*`

## 按改动类型读取这些 instruction files

这些文件不是“参考建议”，而是改到相关范围时必须读取的规则源。

- 改 XAML / 控件 / 页面布局：
  - `accessibility.instructions.md`
  - `performance.instructions.md`
  - `winui-best-practices.instructions.md`
- 改用户可见字符串：
  - `globalization.instructions.md`
- 改 secrets / auth / HTTP / 权限 / 文件访问：
  - `security.instructions.md`
- 改 analyzer / 风格 / 命名 / 清理：
  - `code-quality.instructions.md`
- 改测试：
  - `testing.instructions.md`
- 新接 WinAppSDK / Windows API：
  - `windows-apis.instructions.md`

## 特别注意的当前约束

- 不要再沿用“x86 / ARM64 当前都支持”的旧模板说法；当前项目文件写的是 `x64`。
- 不要把 MSIX 注册流当成唯一开发路径；日常开发以 `run-dev.ps1` 和根目录 `run-desktop.ps1` 为准。
- 不要把 README 里 sidecar 的旧描述当成当前聊天主线事实；当前代码是 pure C#。
- 不要把旧 AutoDream 服务、mailbox/team、多 agent 扩展设计或 `.omx` 计划中的内容写成“已实现”。
- 如果 instruction file 中存在模板化的泛用表述，与当前 `.csproj`、启动脚本或 `App.xaml.cs` 冲突，以真实代码和脚本为准。

## 日志与排障入口

优先看这些位置：

- 桌面启动异常：`%LOCALAPPDATA%\hermes\hermes-cs\logs\desktop-startup.log`
- 桌面主日志：`%LOCALAPPDATA%\hermes\hermes-cs\logs\hermes.log`
- Dreamer 工作区：`%LOCALAPPDATA%\hermes\dreamer\`
- NPC runtime：`%LOCALAPPDATA%\hermes\hermes-cs\runtime\stardew\...`
- Windows crash 报告：`C:\ProgramData\Microsoft\Windows\WER\ReportArchive`

常用 PowerShell：

```powershell
Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\desktop-startup.log" -Tail 200 -Wait
Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\hermes.log" -Tail 200 -Wait
```

## 完成标准

- 说明改了哪些文件、改动影响哪个 Desktop 主线。
- 报告实际跑过的 build / test / launch 验证命令。
- UI 改动如果可运行，优先实际启动验证；不要只靠静态阅读就声称“界面已正常”。
- 明确剩余风险、未测范围和任何受本机环境限制的点。
