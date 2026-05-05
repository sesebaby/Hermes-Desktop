# Hermes-Desktop Agent 指南

本文件作用于整个 `D:\GitHubPro\Hermes-Desktop` 仓库。更深层目录里的 `AGENTS.md` 优先级更高；尤其是修改 `Desktop/HermesDesktop/**` 时，必须同时遵守 `Desktop/HermesDesktop/AGENTS.md` 以及它要求先读的 `.github/instructions/*.instructions.md`。

## 沟通与执行

- 默认用中文向用户汇报进展、风险和结果；代码标识符、命令、路径、API 名称保持原文。
- 在明确、低风险、可逆的任务上直接执行到完成，不把普通检查、构建、测试交回给用户。
- 工作区可能已有用户改动；不要回退、覆盖或清理自己没造成的变更。开始改动前先看 `git status --short --branch`。
- 搜索文件和符号优先用 `rg` / `rg --files`。
- 手工改文件使用 `apply_patch`；不要用 shell 拼接重写文件。

## 先读这些入口

- `HermesDesktop.sln` / `HermesDesktop.slnx`：仓库主解决方案与项目入口。
- `Desktop/HermesDesktop/AGENTS.md`：WinUI 3 桌面壳专用规则；改这个子树前必须先读。
- `Desktop/HermesDesktop/.github/instructions/*.instructions.md`：桌面子项目的 accessibility、security、performance、code-quality、testing、WinUI API 规则。
- `Desktop/HermesDesktop/README.md`：桌面壳运行方式、`run-dev.ps1` 用法和启动排障。
- `run-desktop.ps1` 与 `scripts/*.ps1`：本仓库日常开发、打包、部署的真实命令入口。

## 仓库主入口

- `Desktop/HermesDesktop/`：WinUI 3 / Windows App SDK 桌面壳，当前开发模式是 unpackaged `dotnet run`，不是日常 MSIX-only 工作流。
- `Desktop/HermesDesktop.Tests/`：主测试项目，`net10.0` + MSTest + Moq，引用 `src/Hermes.Core.csproj`。
- `Desktop/HermesDesktop.Package/`：桌面打包项目。
- `src/`：Hermes 核心运行时与 CLI，包含 agent、context、memory、skills、mcp、tasks、runtime、dreamer、games/stardew 等模块。
- `Mods/StardewHermesBridge/`：星露谷 SMAPI bridge。
- `Mods/StardewHermesBridge.Tests/`：bridge 测试，当前 `net6.0`。
- `tools/Hermes.SmokeProbe/`：发布后烟测工具。
- `skills/`、`souls/`：随仓库发布的技能与 soul 资产；`publish-portable.ps1` 会把它们打进产物。
- `scripts/`：portable/MSIX 发布、部署、证书、repo temp 等脚本。
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
- 运行 smoke probe：
  ```powershell
  dotnet build .\tools\Hermes.SmokeProbe\Hermes.SmokeProbe.csproj -c Release
  ```

## Lint / 静态分析

- 仓库当前没有独立的 lint 脚本。
- `Desktop/HermesDesktop/.github/instructions/code-quality.instructions.md` 是桌面子项目的静态分析、StyleCop、命名与清理规则来源。
- 对桌面子树来说，通常通过 `dotnet build` 暴露 analyzers / code style / warnings；不要再凭空发明一个不存在的 lint 流程。

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

## 运行时架构概览

### 1. Desktop 是组合根，不是薄壳

- `Desktop/HermesDesktop/App.xaml.cs` 负责把核心运行时能力接起来：`TranscriptStore`、`MemoryManager`、`SkillManager`、`TaskManager`、`ContextManager`、`McpManager`、`DreamerStatus`、`NpcRuntimeSupervisor`、`StardewNpcAutonomyBackgroundService` 等都在这里注册。
- 不要把桌面应用理解成“只包一层 UI”。当前产品价值就在这个本地宿主：本地 agent 生命周期、权限、skills、Dreamer、NPC runtime、日志和配置都由它承载。

### 2. 桌面 agent 与 NPC agent 走同一套能力装配

- 通用内建工具注册统一走 `src/runtime/AgentCapabilityAssembler.cs`。
- 当前已注册的 Hermes-native 内建工具是：`todo`、`todo_write`、`schedule_cron`、`agent`、`memory`、`session_search`、`skills_list`、`skill_view`、`skill_manage`、`skill_invoke`、`checkpoint`。
- 桌面主 agent 通过 `App.RegisterAllTools(...)` 接这套装配；NPC runtime 通过 `NpcRuntimeContextFactory` + `NpcRuntimeSupervisor.CreateAgentHandle(...)` 复用同一条链路，再叠加 Stardew 专用工具 surface。

### 3. 上下文主线：Transcript + Soul + Memory + Active Todo 注入

- `TranscriptStore` 是 SQLite-first 持久化层；session message 与 activity 的权威存储在 `state.db`，旧 JSONL 只做导入兼容。
- `MemoryManager` 是固定文件型 curated memory；不是另起数据库，也不是任意 KV 存储。
- `ContextManager` 不会把整段历史原样塞给模型，而是按 recent window、summary、soul、plugin blocks、retrieved context、active todo 组 prompt。
- `SessionTaskProjectionService` 会从 transcript 中持久化的 `todo` / `todo_write` tool result 回放出会话任务快照，再由 `ContextManager` 注入活跃任务。

### 4. Todo 是会话级模型任务循环；TaskManager 是宿主级长期任务系统

- `todo` / `todo_write` 的权威运行时状态在 `SessionTodoStore`，按 `sessionId` 分桶。
- `SessionTodoStore` 目前是内存态；跨会话/重载连续性依赖 `SessionTaskProjectionService` 从 transcript tool result 做 hydration。
- `TaskManager` 是另一套长期 JSON 文件任务系统，位于项目目录 `tasks/`，用于宿主级结构化任务、依赖和优先级管理。
- 结论：不要把 `todo` 当成 `TaskManager` 的前端壳；这两套能力当前没有打通。

### 5. Skills 是 Markdown 目录资产，不是硬编码 switch

- `SkillManager` 会递归扫描 `skillsDir` 下的 `SKILL.md`，解析 frontmatter 和正文，动态装入内存。
- 运行时技能相关工具是 `skills_list`、`skill_view`、`skill_manage`、`skill_invoke`。
- `AgentCapabilityAssembler.CreatePromptBuilder(...)` 会把 skills mandatory prompt 注入 system prompt，所以 agent 应先发现/加载相关 skill，而不是绕开 skill 系统硬写流程。
- 仓库里的 `skills/` 是 bundled source；桌面启动时会通过 `BundledSkillCatalogService` reconcile 到活动 skill tree。

### 6. MCP 是真实接线的能力，不是只存在于文档

- `McpManager` 会从配置文件加载 MCP server 配置、建立连接，并把发现到的 MCP tools 包装成统一 `ITool`。
- 桌面启动后会把已发现的 MCP tools 注册进主 tool registry；NPC runtime 也能通过 tool surface 看到这些外部工具。
- 但是“某个 MCP server 一定可用”并不是仓库保证；是否有具体外部能力取决于运行时 `mcp.json` 和本地环境。

### 7. Subagent / multi-agent 是真实功能，但边界要说清

- `src/Tools/AgentTool.cs` 的 `agent` 工具已实现；它会基于受限工具集拉起隔离上下文子 agent。
- `src/agents/AgentService.cs` 已实现 agent spawn、worktree isolation、remote isolation。
- `src/coordinator/CoordinatorService.cs` 已实现多 worker 编排，会拆解任务、并行 spawn worker、汇总结果。
- 但 `SendMessageTool` 没有现成实现；`MailboxService` / `TeamManager` 更像底层雏形，不要把它们当成当前产品里已经完整接通的 agent 间消息系统。

### 8. Dreamer 当前真实主线是后台 walk / signal / build / digest

- 桌面启动时会 `StartDreamerBackground(...)`，创建 `DreamerRoom`、`RssFetcher`、`DreamerService` 并起后台循环。
- `DreamerService` 当前职责是：周期性读 transcript / inbox / RSS，跑 local-model walk，做 signal scoring，必要时触发 build sprint，并写本地 digest。
- `DreamerRoom` 的工作区在 `HERMES_HOME/dreamer/`，里面有 `walks/`、`projects/`、`inbox/`、`inbox-rss/`、`feedback/digests/`、`signal-state.json`、`signal-log.jsonl`、`DREAMER_SOUL.md`。
- Dashboard 和 Settings 已接入 Dreamer 状态与配置。
- 代码里另有 `src/dream/AutoDreamService.cs` 这套 transcript→memory consolidation 逻辑，但当前桌面默认不走它；不要把它误判成现在线上主链路。

### 9. Stardew NPC runtime 不是第二套 Hermes

- `NpcRuntimeContextFactory` 会为每个 NPC 建独立 namespace 下的 `SoulService`、`MemoryManager`、`TranscriptStore`、`SessionTodoStore`、`ContextManager`、`ToolRegistry`。
- `NpcRuntimeSupervisor` 再把通用 Hermes-native 工具和 Stardew 专用工具接进去。
- 结论：每个 NPC 本质上是“正常 Hermes agent + 自己的 home / memory / transcript / todo / tool surface”，不要在宿主侧再发明第二套 NPC 任务或记忆系统。

## 当前已实现能力与未实现边界

- 已实现并可注册：`memory`、`session_search`、`todo`、`todo_write`、`schedule_cron`、`agent`、skills 工具链、checkpoint。
- 已实现并接入桌面：Dreamer 背景循环、Dashboard/Settings 的 Dreamer UI、MCP client discovery、Coordinator/AgentService、Stardew NPC runtime。
- 已有代码但不要当成当前默认主链路：`AutoDreamService`。
- 不要误报为已实现：`SendMessageTool`、完整 mailbox/team multi-agent 产品面、任何只在 docs / openspec / `.omx` 计划里存在的未来设计。

## 项目级 OpenSpec 约束

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
- 如果文档、计划、`.omx`/`.omc` 记录与源码现状冲突，以当前代码与 `.csproj`、实际 DI 注册链为准。

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
