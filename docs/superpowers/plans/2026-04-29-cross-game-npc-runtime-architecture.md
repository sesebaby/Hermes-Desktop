# Cross-Game NPC Runtime Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在当前 Hermes Desktop 仓库中落地“跨游戏 NPC runtime core + 游戏 adapter + NPC 资料包”三层架构，并先支持 Stardew Valley 的双 NPC 真实隔离 MVP。

**Architecture:** 按平衡标准执行：先做真实 Stardew 双 NPC MVP，同时只切出最小干净的 `core` / `Stardew` 边界，不在第一阶段做完整跨游戏平台。现有桌面聊天路径围绕 `Desktop/HermesDesktop/Services/HermesChatService.cs:19` 的单会话 in-process agent 组织，而本方案会把它上提为“运行时宿主”而不是“唯一会话入口”。核心抽象只负责 NPC 生命周期、namespace、事件循环、trace、日志与 game contract；具体的 Stardew / SMAPI 语义全部落在 `src/games/stardew/`。NPC 本体通过资料包动态发现与装载，同游戏内新增 NPC 目标是不修改 core；`external/hermescraft-main` 只作为架构参考，不作为主实现栈。

**Tech Stack:** .NET 10, WinUI 3, MSTest, Hermes.Core, SQLite transcript store, JSON manifest loading, SMAPI adapter boundary, structured JSONL/SQLite-equivalent logging

---

## 实施进度快照（2026-04-29）

### 当前停止点
已按要求停在 **Phase 1 的 Stardew `move` + 对话 / `speak` 检查点**。

这里的“完成”只表示：桌面侧已经能通过统一 typed command contract 把 `move` / `speak` 发到 SMAPI bridge，SMAPI mod 能在游戏内处理这两类动作，并且 Visual Studio 启动桌面项目前会先构建并发布 mod。

这里**不表示**完整自治 loop 已经完成，也不表示已经完成复杂寻路、社交系统、群聊、经济系统、10+ NPC、多进程 host 或完整 replay UI。

### 已提交的实施切片
- `a1afd789`：固定 game-core / runtime 基础契约、pack loader、NPC namespace、Haley/Penny seed pack、Stardew game skills。
- `dad377f2`：把 Stardew `move` 接入统一 typed runtime command path，并加入预算、trace/log、resource claim、world coordination seam。
- `48170eb3`：把 NPC runtime host / supervisor 暴露到桌面端，并在 Dashboard 增加最小 runtime 概览。
- `e2f7cc5a`：新增 `Mods/StardewHermesBridge` SMAPI bridge 脚手架，包含 loopback HTTP bridge、bearer token、`/task/move`、`/task/status`、`/task/cancel`、F8 overlay、bridge JSONL 日志。
- `bfb4aa21`：新增 Stardew 对话 / `speak` 通路：`GameActionType.Speak` -> `StardewCommandService` -> `/action/speak` -> SMAPI NPC 对话 UI。
- `19b4785a`：把 `move` 从“只记录 tick 完成”升级为真实影响游戏内 NPC body：查找 NPC、查找目标地图、检查目标 tile、设置 NPC tile。
- `b73d9928`：配置 Visual Studio / solution 启动体验：启动 `HermesDesktop` 前先构建并发布 `StardewHermesBridge` 到 `D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\StardewHermesBridge`，再启动桌面程序。

### 当前已经验证
- `dotnet build Mods\StardewHermesBridge\StardewHermesBridge.csproj`：通过，并成功发布 mod 到 Stardew `Mods\StardewHermesBridge`。
- `dotnet build Desktop\HermesDesktop\HermesDesktop.csproj`：通过；构建链路会先构建并发布 SMAPI mod，再构建桌面程序。
- `dotnet build HermesDesktop.slnx`：通过。
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --filter 'FullyQualifiedName~Runtime|FullyQualifiedName~Stardew|FullyQualifiedName~GameCore'`：26 个测试通过。
- `git diff --check`：通过。

### 仍未人工确认
以下内容还没有在 Visual Studio + 真实游戏窗口里人工点一遍：
- 在 Visual Studio 里按 F5 启动 `HermesDesktop`，确认不会先报错。
- 启动 Stardew + SMAPI 后，确认游戏内能加载 `Stardew Hermes Bridge`。
- 进入存档后，按 F8 能看到 Hermes bridge overlay。
- 通过后续桌面端触发入口或临时调试入口触发 `speak`，确认游戏里真的弹出对应 NPC 的对话框。
- 通过后续桌面端触发入口或临时调试入口触发 `move`，确认对应 NPC 在游戏里移动/出现在目标 tile。

### 下次继续时不要偏的方向
- 继续保持参考 `external/hermescraft-main` 的精神：**agent 主动观察、决策、调用工具玩游戏**，不是“游戏事件来了才临时问一次 agent”。
- 事件只能作为事实、唤醒、暂停、阻塞或优先级输入；driver 必须是 `NpcAutonomyLoop`。
- `StardewCommandService` 仍然是桌面侧唯一命令源；UI、typed tools、CLI 都不能绕过它直接调用 SMAPI HTTP。
- 下一步优先补一个从桌面侧可操作的最小调试入口，用来让普通用户手动触发 `speak` 和 `move` 做端到端验证；然后再把它接入真正的 `NpcAutonomyLoop`。

---

## Final Architecture Decision

### Frozen boundaries
- `src/runtime/`：跨游戏 NPC runtime orchestration
- `src/game/core/`：跨游戏 game contract + NPC pack schema/loader
- `src/games/stardew/`：Stardew/SMAPI adapter only
- `skills/gaming/stardew-*.md`：Stardew game knowledge skills，参考 HermesCraft `external/hermescraft-main/skills/*.md`
- `src/game/stardew/personas/<npc-id>/default/`：Phase 1 NPC persona seed packs only
- `Mods/StardewHermesBridge/`：SMAPI mod bridge + mod-side debug UI + mod-side logging

### Hard rules
- 一个 NPC = 一个独立 Agent 实例
- 每个 NPC 有独立 namespace：soul / memory / transcript / runtime state
- `core runtime` 必须拓扑无关，不与单进程或多进程强绑定
- 资料包只承载内容与配置，不承载任意可执行逻辑
- 同游戏新增 NPC 优先通过资料包完成，不改 core
- 最低验收线仍然是：双 NPC + 真实 bridge + 记忆/人格隔离可证明
- 接管的是现有 Stardew NPC，不是创建全新角色
- 每个 NPC 的 persona 必须来自固定 seed pack / soul 文件，不允许实现阶段临场生成
- 节日、剧情、玩家不可控等重大原版状态必须优先于 Agent 计划
- 所有“已有能力”表述必须能回到代码证据；否则按新增设计处理
- Phase 1 必须包含最小 `move` 真实闭环、最小 SMAPI overlay/debug menu、最小 Desktop 调试面
- `move` 必须走统一 command envelope、loopback/token 安全、`commandId`、idempotency、resource claim、trace 和 failure reason
- `StardewCommandService` 是唯一命令源；typed tools、CLI、UI 不得各自直接调用 SMAPI HTTP
- 活跃 NPC 必须由 `NpcAutonomyLoop` 主动观察、决策、执行和轮询任务；SMAPI / scheduler / social 事件只能提供事实输入、唤醒、暂停、阻塞或优先级信号，不能代替 NPC 决策。
- 禁止把 MVP 做成“游戏事件来了才临时问一次 agent”的事件驱动问答系统；事件不是 driver，agent loop 才是 driver。

### HermesCraft patterns to borrow
- 独立 home / 独立长期会话 agent
- 通过 bridge 观察和操作游戏世界
- HTTP bridge / task status / 后台任务与冲突处理
- 工具观察世界而不是把世界写死进 prompt
- game-specific skills 作为单独知识层，而不是把全部游戏操作细节塞进 SOUL
- character prompt / soul 与 game skill 叠加，而不是只靠单一系统 prompt
- 明确参考 `external/hermescraft-main/README.md:34-43` 的链路：`Hermes Agent -> tools/CLI -> game bridge HTTP API -> embodied game body -> game world`
- 借鉴的是 agent 通过工具和 bridge 主动玩游戏；不是借鉴“游戏事件触发一次 agent 响应”的模式

### Reference implementation links
- HermesCraft main architecture: `external/hermescraft-main/README.md:22-43`
- HermesCraft persistent identity / social systems: `external/hermescraft-main/README.md:121-152`
- HermesCraft civilization launcher: `external/hermescraft-main/civilization.sh:5-17`, `external/hermescraft-main/civilization.sh:197-257`
- HermesCraft bridge server endpoints: `external/hermescraft-main/bot/server.js:2493-2665`
- HermesCraft navigation skill: `external/hermescraft-main/skills/minecraft-navigation.md`
- HermesCraft survival skill: `external/hermescraft-main/skills/minecraft-survival.md`
- HermesCraft shared soul baseline: `external/hermescraft-main/SOUL-landfolk.md`
- HermesCraft character prompt examples: `external/hermescraft-main/prompts/landfolk/steve.md`, `external/hermescraft-main/prompts/landfolk/moss.md`
- Current Stardew deep interview baseline: `.omc/specs/deep-interview-stardew-allgameinai.md`
- Current modification-map review spec: `.omc/specs/deep-interview-stardew-modification-map-2026-04-29.md`
- Architecture review context: `.omx/context/stardew-modification-map-review-20260429T032420Z.md`
- Existing draft design reference: `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md`
- Stardew NPC souls and personas: `docs/superpowers/specs/2026-04-29-stardew-npc-souls-haley-penny.md`
- SMAPI modding index: `https://stardewvalleywiki.com/Modding:Index`
- SMAPI docs root: `https://smapi.io/`

### Phase 1 guardrail
- 第一阶段只做双 NPC 真实闭环，不做完整跨游戏平台
- `core` 只扩到 Stardew MVP 当下真正需要的字段和接口
- 漂亮完整 UI、复杂社交网络、经济系统、10+ NPC、多进程编排都后置；最小 SMAPI overlay 和 Desktop 调试面不后置

---

## File Structure Map

### Existing files to keep and integrate
- Modify: `Desktop/HermesDesktop/App.xaml.cs` — DI 注册 runtime host、pack loader、game adapter、workspace service、日志服务
- Modify: `Desktop/HermesDesktop/Services/HermesChatService.cs` — 从“单聊天会话服务”转向“宿主 + 调试入口”协作
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml`
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`
- Modify: `Desktop/HermesDesktop/MainWindow.xaml.cs`
- Modify: `src/soul/SoulService.cs` — 支持基于 namespace 的路径装配
- Modify: `src/memory/MemoryManager.cs` — 支持基于 namespace 的实例化/路径隔离
- Modify: `src/transcript/TranscriptStore.cs` — 支持每 NPC 独立 transcript store / session source 标识
- Modify: `src/Core/Agent.cs` — 为 runtime host 暴露稳定的 agent 实例构建入口
- Modify: `src/soul/SoulRegistry.cs` — 复用其目录扫描经验，但不直接承担 NPC pack 发现
- Reference: `src/skills/BundledSkillCatalogService.cs` — 复用 manifest、校验、刷新思路

### New core runtime files
- Create: `src/runtime/NpcRuntimeHost.cs`
- Create: `src/runtime/NpcRuntimeSupervisor.cs`
- Create: `src/runtime/NpcRuntimeInstance.cs`
- Create: `src/runtime/NpcRuntimeDescriptor.cs`
- Create: `src/runtime/NpcNamespace.cs`
- Create: `src/runtime/NpcAutonomyLoop.cs`
- Create: `src/runtime/NpcAutonomyBudget.cs`
- Create: `src/runtime/NpcRuntimeTrace.cs`
- Create: `src/runtime/NpcRuntimeLogRecord.cs`
- Create: `src/runtime/NpcRuntimeLogWriter.cs`
- Create: `src/runtime/NpcRuntimeTraceIndex.cs`
- Create: `src/runtime/ResourceClaimRegistry.cs`
- Create: `src/runtime/WorldCoordinationService.cs`
- Create: `src/runtime/RuntimeTopology.cs`

### New game-core files
- Create: `src/game/core/IGameAdapter.cs`
- Create: `src/game/core/IGameCommandService.cs`
- Create: `src/game/core/IGameQueryService.cs`
- Create: `src/game/core/IGameEventSource.cs`
- Create: `src/game/core/GameAction.cs`
- Create: `src/game/core/GameObservation.cs`
- Create: `src/game/core/WorldSnapshot.cs`
- Create: `src/game/core/GameEntityBinding.cs`
- Create: `src/game/core/INpcPackLoader.cs`
- Create: `src/game/core/NpcPackManifest.cs`
- Create: `src/game/core/NpcPackValidationResult.cs`
- Create: `src/game/core/FileSystemNpcPackLoader.cs`

### New Stardew adapter files
- Create: `src/games/stardew/StardewAdapter.cs`
- Create: `src/games/stardew/StardewBridgeOptions.cs`
- Create: `src/games/stardew/BridgePortDiscovery.cs`
- Create: `src/games/stardew/StardewNpcCatalog.cs`
- Create: `src/games/stardew/StardewCommandService.cs`
- Create: `src/games/stardew/StardewCommandContracts.cs`
- Create: `src/games/stardew/StardewQueryService.cs`
- Create: `src/games/stardew/StardewEventIngestor.cs`
- Create: `src/games/stardew/StardewWorldAdapter.cs`
- Create: `src/games/stardew/SmapiModApiClient.cs`
- Create: `src/games/stardew/StardewBridgeDtos.cs`
- Create: `src/games/stardew/StardewNpcBindingResolver.cs`
- Create: `src/games/stardew/StardewBridgeLogRecord.cs`
- Create: `src/games/stardew/StardewBridgeDiagnostics.cs`

### New SMAPI mod project files
- Create: `Mods/StardewHermesBridge/StardewHermesBridge.csproj`
- Create: `Mods/StardewHermesBridge/ModEntry.cs`
- Create: `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- Create: `Mods/StardewHermesBridge/Bridge/BridgeHealthEndpoint.cs`
- Create: `Mods/StardewHermesBridge/Bridge/NpcStatusEndpoint.cs`
- Create: `Mods/StardewHermesBridge/Bridge/WorldSnapshotEndpoint.cs`
- Create: `Mods/StardewHermesBridge/Bridge/EventPollEndpoint.cs`
- Create: `Mods/StardewHermesBridge/Bridge/TaskStatusEndpoint.cs`
- Create: `Mods/StardewHermesBridge/Bridge/TaskCancelEndpoint.cs`
- Create: `Mods/StardewHermesBridge/Bridge/NpcMoveEndpoint.cs`
- Create: `Mods/StardewHermesBridge/Bridge/NpcSpeakEndpoint.cs`
- Create: `Mods/StardewHermesBridge/Ui/BridgeStatusOverlay.cs`
- Create: `Mods/StardewHermesBridge/Ui/BridgeDebugMenu.cs`
- Create: `Mods/StardewHermesBridge/Logging/SmapiBridgeLogger.cs`
- Create: `Mods/StardewHermesBridge/manifest.json`

### New desktop debug files
- Create: `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- Create: `Desktop/HermesDesktop/Models/NpcRuntimeItem.cs`
- Create: `Desktop/HermesDesktop/Models/NpcRuntimeLogItem.cs`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcRuntimePanel.xaml`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcRuntimePanel.xaml.cs`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcLogsPanel.xaml`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcLogsPanel.xaml.cs`

### New game skill files
- Create: `skills/gaming/stardew-core.md`
- Create: `skills/gaming/stardew-social.md`
- Create: `skills/gaming/stardew-navigation.md`
- Create later/gated: `skills/gaming/stardew-farming.md`
- Create later/gated: `skills/gaming/stardew-town-schedule.md`

### New content/schema files
- Create: `src/game/stardew/personas/haley/default/SOUL.md`
- Create: `src/game/stardew/personas/haley/default/facts.md`
- Create: `src/game/stardew/personas/haley/default/voice.md`
- Create: `src/game/stardew/personas/haley/default/boundaries.md`
- Create: `src/game/stardew/personas/haley/default/skills.json`
- Create: `src/game/stardew/personas/penny/default/SOUL.md`
- Create: `src/game/stardew/personas/penny/default/facts.md`
- Create: `src/game/stardew/personas/penny/default/voice.md`
- Create: `src/game/stardew/personas/penny/default/boundaries.md`
- Create: `src/game/stardew/personas/penny/default/skills.json`

### New verification files
- Create: `scripts/verification/verify-stardew-two-npc-mvp.ps1`
- Create: `Desktop/HermesDesktop.Tests/Integration/StardewTwoNpcIsolationFlowTests.cs`

### New tests
- Create: `Desktop/HermesDesktop.Tests/Runtime/NpcNamespaceTests.cs`
- Create: `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- Create: `Desktop/HermesDesktop.Tests/GameCore/NpcPackLoaderTests.cs`
- Create: `Desktop/HermesDesktop.Tests/GameCore/NpcPackManifestTests.cs`
- Create: `Desktop/HermesDesktop.Tests/Stardew/StardewNpcBindingResolverTests.cs`
- Create: `Desktop/HermesDesktop.Tests/Stardew/StardewBridgeSecurityTests.cs`
- Create: `Desktop/HermesDesktop.Tests/Stardew/StardewCommandContractTests.cs`
- Create: `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyBudgetTests.cs`
- Create: `Desktop/HermesDesktop.Tests/Runtime/ResourceClaimRegistryTests.cs`
- Create: `Desktop/HermesDesktop.Tests/Runtime/WorldCoordinationServiceTests.cs`

---

## Architecture Details

### Phase 1 architecture sketch
```text
Hermes Desktop host
  -> NpcRuntimeSupervisor
  -> NpcRuntimeInstance (NPC A / NPC B)
  -> Agent + Memory + Soul + Transcript namespace
  -> NpcRuntimeLogWriter / NpcRuntimeTraceIndex
  -> IGameAdapter / IGameCommandService / IGameQueryService / IGameEventSource
  -> StardewAdapter
  -> SmapiModApiClient (HTTP bridge first)
  -> Mods/StardewHermesBridge
  -> BridgeHttpHost + NpcMoveEndpoint + BridgeStatusOverlay
  -> Stardew Valley world
```

### A. Core runtime responsibilities
`src/runtime/` 只负责：
- 一个 NPC runtime 的生命周期
- 多 NPC registry 与 health
- namespace 装配
- 通用 autonomy loop
- trace 聚合
- structured runtime logs
- 与 `IGameAdapter` 交互

它**不负责**：
- Stardew 世界语义
- SMAPI 协议细节
- NPC 具体角色内容

### B. Game adapter responsibilities
`src/games/stardew/` 只负责：
- 把通用 `GameAction` 映射到 SMAPI bridge
- 把 SMAPI 返回映射成 `GameObservation` / `WorldSnapshot`
- 解析现有 NPC 实体绑定
- 产出节日 / 剧情 / 对话等游戏事件
- 维护 adapter-side diagnostics 与 bridge record

### C. NPC pack responsibilities
每个资料包负责声明：
- 我是谁（name / npcId / gameId）
- 我绑定谁（targetEntityId，必须是现有游戏实体）
- 我有哪些固定初始人格与记忆 seed
- 我有哪些默认策略 / policy
- 我属于哪个 adapter 与能力集
- 我依赖哪些 game skills（Phase 1 为 `stardew-core` / `stardew-social` / `stardew-navigation`；farming / town schedule 后置门控）

资料包**不负责**：
- 任意代码执行
- 调度循环
- adapter 行为实现
- 运行时临场生成人设

### D. Stardew skill layer
参考 HermesCraft `skills/minecraft-navigation.md`、`skills/minecraft-survival.md` 的做法，Stardew 也需要单独的 game skill 层，而不是把所有游戏知识塞进每个 NPC 的 soul。

Phase 1 最小 skill 集：
- `stardew-core`：观察→决策→执行循环，节日/剧情/玩家状态优先级，`commandId` 状态查询 / 取消，跨天收束和禁止模型猜世界状态。
- `stardew-social`：玩家命令优先、群聊/旁听 envelope、完成命令写回、MVP 玩家在场群聊限制。
- `stardew-navigation`：只覆盖 Phase 1 `move` 真实闭环需要的地图区域、tile 移动、室内外切换、无法到达时的退避策略。
- `stardew-farming` / `stardew-town-schedule`：后续受能力门控启用，Phase 1 不进入稳定系统上下文。

原则：
- soul 负责“这个 NPC 是谁”
- game skills 负责“在 Stardew 里怎么活”
- adapter 负责“怎么调用 SMAPI/bridge”

### Haley and Penny seed personas
Phase 1 需要把首发两名 NPC 的 persona 写死到 pack 文件里，不能留到实现时再想。具体正文与 seed 见：
- `docs/superpowers/specs/2026-04-29-stardew-npc-souls-haley-penny.md`

#### Haley soul direction
- 关键词：时尚、自我中心外壳、对环境挑剔、慢热、被认真对待时会软化
- 对话风格：短句、挑剔感、偏口语，不爱长解释
- 默认动机：维护体面、避开脏乱、对漂亮地点和高质量物品更敏感
- 行为偏好：更愿意去整洁、明亮、可展示的地点；社交时先观察态度再投入
- pack 文件：`src/game/stardew/personas/haley/default/SOUL.md`

#### Penny soul direction
- 关键词：温柔、内向、照顾他人、秩序感、容易不安但有责任心
- 对话风格：礼貌、克制、完整句，但仍要短
- 默认动机：帮助别人、维持日常秩序、偏好安静安全的地点
- 行为偏好：更重视照护、阅读/教学气质、规避冲突和高风险区域
- pack 文件：`src/game/stardew/personas/penny/default/SOUL.md`

#### Pack + skill composition
- Haley pack: `haley` + `stardew-core` + `stardew-social` + `stardew-navigation`
- Penny pack: `penny` + `stardew-core` + `stardew-social` + `stardew-navigation`
- `stardew-farming` / `stardew-town-schedule` 必须等对应 bridge DTO、resource claim、golden prompt preview 和工具注册完成后再启用。
日志必须从第一阶段就是一等能力，否则出问题无从检查。

#### Runtime-side logs
每条关键记录至少包含：
- `timestampUtc`
- `traceId`
- `npcId`
- `gameId`
- `sessionId`
- `actionType`
- `target`
- `stage`（plan / dispatch / bridge_send / bridge_ack / world_result / persisted）
- `result`
- `latencyMs`
- `error`

建议落盘位置：
- `%LOCALAPPDATA%/hermes/hermes-cs/runtime/<npcId>/runtime.jsonl`
- `%LOCALAPPDATA%/hermes/hermes-cs/runtime/<npcId>/trace.jsonl`

#### SMAPI bridge logs
SMAPI mod 侧至少要记录：
- 接收到的 endpoint
- 目标 NPC / tile / map
- 是否 accepted / started / completed / failed
- 路径失败、NPC 不可控、节日/剧情拦截原因
- 与 desktop 侧相同的 `traceId`

建议落盘位置：
- `Mods/StardewHermesBridge/logs/bridge.jsonl`
- 同时输出到 SMAPI console

### E. SMAPI mod UI requirements
第一阶段必须有最小 mod UI，不然游戏内 bridge 出错时无从检查。

最小要求：
- 一个可切换的 overlay / debug menu
- 显示 bridge 是否在线
- 显示最近一次请求的 `npcId`、`actionType`、`traceId`、结果
- 显示当前是否被节日 / 剧情 / 玩家控制等状态拦截
- 能看到 move 请求是否真正到达 mod

### F. Hermes Desktop UI changes
当前项目 UI 改造不能省略，至少要补成可调试面而不是只看 Dashboard 概览：
- Dashboard 增加 NPC Runtime 概览卡
- Dashboard 增加 Bridge Health / Last Error / Last Trace 卡
- 新增 `NpcRuntimePanel` 查看每个 NPC 的 state、pack、session、memory namespace
- 新增 `NpcLogsPanel` 查看最近 runtime log / bridge log / trace
- 增加“打开日志目录”“复制 traceId”“打开 replay/verification 输出”的入口

### G. Topology choice
当前不强制单进程。我的建议是：
- **Phase 1 先实现 topology-neutral in-process host**，因为与当前 `App.xaml.cs` DI、`HermesChatService`、`TranscriptStore` 的耦合最低
- 但 runtime host API 必须允许未来替换成 out-of-process worker host
- 所以 `NpcRuntimeSupervisor` 不直接暴露 WinUI 或 static path 依赖，只依赖 descriptor / namespace / adapter / agent factory

原因：这样最快落地，同时不给未来多进程封死路

### Phase 1 move-loop proof
最小真实闭环必须明确包含 `move`：
1. NPC runtime 读取 pack 与 namespace
2. NPC 产生 `GameAction(type=Move, target=Tile|Location)`
3. `StardewCommandService` 将 Move action 发到 `SmapiModApiClient`
4. SMAPI mod 在游戏内执行移动/寻路请求
5. bridge 返回结果（accepted / started / completed / failed）
6. 结果写回 `NpcRuntimeTrace` + transcript/activity + runtime log
7. mod log 与 desktop log 使用同一 `traceId`
8. 验证脚本读取 trace，证明 move 真正穿过了 bridge，而不是 UI 假象

## Minimal SMAPI Bridge Contract (Phase 1)

### Contract design rule
接口风格直接借鉴 HermesCraft `bot/server.js:2493-2665`：
- `GET /health` 只负责无 token 的最小健康探测
- `POST /query/*` 负责按需 observation / status 查询
- `POST /events/poll` 负责拉取事件事实；事件只影响 NPC loop 的上下文和优先级，不驱动一次性 agent 响应
- `POST /task/*` 负责长动作创建、状态轮询和取消
- `POST /action/*` 只保留极短同步动作

### Required mod lifecycle hooks
根据 SMAPI 官方模式，bridge mod 至少使用：
- `Entry(IModHelper helper)` 注册全部事件
- `helper.Events.GameLoop.UpdateTicked` 维护 task pump / pathing 状态
- `helper.Events.GameLoop.TimeChanged` 产出时间变化事件
- `helper.Events.GameLoop.ReturnedToTitle` 清理 bridge state
- `helper.Events.Player.Warped` 产出位置/地图变化
- `helper.Events.World.NpcListChanged` 或等价位置/NPC 状态事件，维护 NPC 可用性
- `this.Monitor.Log(...)` / `VerboseLog(...)` 输出 bridge diagnostics

### Endpoint set

#### 1. `GET /health`
用途：探测 bridge 是否在线。该 endpoint 允许无 token，但只能返回最小健康信息，不能泄露 NPC / save / command runtime 状态。

```json
{
  "ok": true,
  "bridgeVersion": "0.1.0",
  "adapter": "stardew"
}
```

#### 2. Authenticated request envelope
除 `/health` 外，所有 Bridge API 必须带：

```http
Authorization: Bearer <bridgeToken>
```

request envelope：

```json
{
  "traceId": "trace_haley_00012",
  "requestId": "req_1714351000",
  "idempotencyKey": "move_haley_town_42_17_1714351000",
  "gameId": "stardew-valley",
  "saveId": "farm_001",
  "npcId": "haley",
  "profileId": "default",
  "commandType": "move",
  "payload": {}
}
```

success envelope：

```json
{
  "ok": true,
  "traceId": "trace_haley_00012",
  "requestId": "req_1714351000",
  "commandId": "cmd_move_1714351000",
  "status": "queued",
  "data": {},
  "error": null,
  "state": {}
}
```

error envelope：

```json
{
  "ok": false,
  "traceId": "trace_haley_00012",
  "requestId": "req_1714351000",
  "commandId": "cmd_move_1714351000",
  "status": "blocked",
  "data": null,
  "error": {
    "code": "festival_blocked",
    "message": "NPC is blocked by Flower Dance.",
    "retryable": false
  },
  "state": {}
}
```

#### 3. `POST /query/status`
用途：返回单 NPC 或全局状态摘要，对齐 HermesCraft `/status` 的事实查询语义。使用统一 envelope；`payload` 为：

```json
{
  "npcId": "haley"
}
```

`data` 为：

```json
{
  "npcId": "haley",
  "smapiName": "Haley",
  "displayName": "Haley",
  "locationName": "Town",
  "tile": { "x": 42, "y": 17 },
  "isMoving": false,
  "isInDialogue": false,
  "isAvailableForControl": true,
  "blockedReason": null,
  "currentCommandId": "cmd_move_1714351000",
  "lastTraceId": "trace_haley_00012"
}
```

#### 4. `POST /query/world_snapshot`
用途：返回最小世界观察结果，供 runtime 做决策，不把 Stardew world 细节硬写进 prompt。使用统一 envelope；`payload` 为 `{ "npcId": "haley" }`。

#### 5. `POST /events/poll`
用途：拉取事件流，替代 Phase 1 的复杂推送；先用 poll，后面再升级。使用统一 envelope；`payload` 为 `{ "since": "evt_00128", "npcId": "haley" }`。

#### 6. `POST /task/move`
用途：Phase 1 的核心长动作；风格对齐 HermesCraft 的 `/task/*`。

request `payload`：
```json
{
  "target": {
    "locationName": "Town",
    "tile": { "x": 42, "y": 17 }
  },
  "reason": "inspect bulletin board"
}
```

响应：
```json
{
  "ok": true,
  "traceId": "trace_haley_00012",
  "requestId": "req_1714351000",
  "commandId": "cmd_move_1714351000",
  "status": "queued",
  "data": {
    "accepted": true,
    "claim": {
      "npcId": "haley",
      "targetTile": { "x": 42, "y": 17 },
      "interactionTile": { "x": 42, "y": 17 }
    }
  },
  "error": null,
  "state": {}
}
```

#### 7. `POST /task/status`
用途：轮询长动作状态，对齐 HermesCraft `/task`。

```json
{
  "commandId": "cmd_move_1714351000"
}
```

返回 envelope 的 `data`：

```json
{
  "commandId": "cmd_move_1714351000",
  "npcId": "haley",
  "action": "move",
  "status": "running",
  "startedAtUtc": "2026-04-29T04:00:00Z",
  "elapsedMs": 820,
  "progress": 0.4,
  "blockedReason": null,
  "errorCode": null
}
```

#### 8. `POST /task/cancel`
用途：取消当前长动作，对齐 HermesCraft `/task/cancel`。

```json
{
  "commandId": "cmd_move_1714351000",
  "reason": "runtime stopping"
}
```

#### 9. `POST /action/speak`
用途：保留一个极短同步动作，证明 `/action/*` 与 `/task/*` 分层。

```json
{
  "text": "Hi there.",
  "channel": "player"
}
```

### Error model
所有错误响应使用上面的 error envelope。错误码只能使用固定 lower-snake 枚举，例如 `bridge_unavailable`、`world_not_ready`、`player_not_free`、`festival_blocked`、`cutscene_blocked`、`menu_blocked`、`day_transition`、`invalid_target`、`invalid_state`、`command_conflict`、`command_not_found`、`command_expired`、`command_stuck`、`idempotency_conflict`、`bridge_unauthorized`、`bridge_stale_discovery`。

Security/config requirements:
- Bridge 默认绑定 `127.0.0.1`，不得默认绑定 `0.0.0.0`。
- `bridgeToken` 每次 bridge 启动生成，写入发现文件。
- Hermes Desktop 从 `HERMES_HOME/config.yaml` 的 `stardew` section 读取 bridge 默认 host、端口范围、discovery 文件位置和 `bridge_allow_lan=false`。
- discovery 文件缺 token、host 非 loopback、saveId 不匹配或过期时，拒绝连接并返回 `bridge_stale_discovery` 或 `bridge_unauthorized`。
- `/health` 无 token 时只返回最小健康信息；其它 endpoint 无 token 返回 `bridge_unauthorized`。

### Phase 1 event types
- `time_changed`
- `location_changed`
- `dialogue_started`
- `dialogue_ended`
- `task_started`
- `task_completed`
- `task_failed`
- `control_blocked`
- `control_unblocked`

---

## Recommended Build Order

### Task 1: Define game-core contracts and pack schema
**Files:**
- Create: `src/game/core/IGameAdapter.cs`
- Create: `src/game/core/IGameCommandService.cs`
- Create: `src/game/core/IGameQueryService.cs`
- Create: `src/game/core/IGameEventSource.cs`
- Create: `src/game/core/GameAction.cs`
- Create: `src/game/core/GameObservation.cs`
- Create: `src/game/core/WorldSnapshot.cs`
- Create: `src/game/core/GameEntityBinding.cs`
- Create: `src/game/core/NpcPackManifest.cs`
- Test: `Desktop/HermesDesktop.Tests/GameCore/NpcPackManifestTests.cs`

**Outcome:** 固定跨游戏 contract，防止 Stardew 语义进入 core。

### Task 2: Build pack loader, skill references, and validation
**Files:**
- Create: `src/game/core/INpcPackLoader.cs`
- Create: `src/game/core/NpcPackValidationResult.cs`
- Create: `src/game/core/FileSystemNpcPackLoader.cs`
- Test: `Desktop/HermesDesktop.Tests/GameCore/NpcPackLoaderTests.cs`
- Content: `src/game/stardew/personas/haley/default/*`, `src/game/stardew/personas/penny/default/*`
- Skills: `skills/gaming/stardew-core.md`, `skills/gaming/stardew-social.md`, `skills/gaming/stardew-navigation.md`

**Outcome:** 新增同游戏 NPC 可以通过资料包完成，且 pack 能明确引用外部 Stardew skills，而不是把所有游戏知识硬塞进 soul。

### Task 3: Introduce per-NPC namespace
**Files:**
- Create: `src/runtime/NpcNamespace.cs`
- Modify: `src/soul/SoulService.cs`
- Modify: `src/memory/MemoryManager.cs`
- Modify: `src/transcript/TranscriptStore.cs`
- Test: `Desktop/HermesDesktop.Tests/Runtime/NpcNamespaceTests.cs`

**Outcome:** 每个 NPC 独立 soul/memory/transcript/state。

### Task 4: Build runtime host and supervisor
**Files:**
- Create: `src/runtime/NpcRuntimeHost.cs`
- Create: `src/runtime/NpcRuntimeSupervisor.cs`
- Create: `src/runtime/NpcRuntimeInstance.cs`
- Create: `src/runtime/NpcRuntimeDescriptor.cs`
- Create: `src/runtime/RuntimeTopology.cs`
- Modify: `Desktop/HermesDesktop/App.xaml.cs`
- Test: `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`

**Outcome:** 当前仓库从单 `_currentSession` 走向多 NPC runtime registry。

### Task 5: Add autonomy loop, trace, and structured runtime logs
**Files:**
- Create: `src/runtime/NpcAutonomyLoop.cs`
- Create: `src/runtime/NpcAutonomyBudget.cs`
- Create: `src/runtime/NpcRuntimeTrace.cs`
- Create: `src/runtime/NpcRuntimeLogRecord.cs`
- Create: `src/runtime/NpcRuntimeLogWriter.cs`
- Create: `src/runtime/NpcRuntimeTraceIndex.cs`
- Modify: `src/Core/Agent.cs`
- Test: `Desktop/HermesDesktop.Tests/Services/AgentTests.cs`
- Test: `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyBudgetTests.cs`

**Outcome:** NPC 具备持续自治循环、结构化 runtime log、可验证 trace 和预算门闸。双 NPC loop 可以同时存活，但 LLM request 受 `NpcAutonomyBudget` 控制；达到 max tool iterations、并发上限或重启上限时必须记录 exit reason / cooldown / trace，不能静默死亡或无限续跑。

### Task 6: Add minimum move resource claims and world coordination seam
**Files:**
- Create: `src/runtime/ResourceClaimRegistry.cs`
- Create: `src/runtime/WorldCoordinationService.cs`
- Test: `Desktop/HermesDesktop.Tests/Runtime/ResourceClaimRegistryTests.cs`
- Test: `Desktop/HermesDesktop.Tests/Runtime/WorldCoordinationServiceTests.cs`

**Outcome:** Phase 1 `move` 不抢同一 NPC、target tile、interaction tile 或目标对象；重复 `idempotencyKey` 不创建第二条物理命令；`cancel_task`、`failed`、`stuck`、`DayEnding`、`Saving` 后释放 claim。完整 world coordination 可以后置，但 `move` 所需最小 claim 不能后置。

### Task 7: Implement Stardew adapter shell and SMAPI mod project
**Files:**
- Create: `src/games/stardew/StardewAdapter.cs`
- Create: `src/games/stardew/StardewBridgeOptions.cs`
- Create: `src/games/stardew/BridgePortDiscovery.cs`
- Create: `src/games/stardew/StardewNpcCatalog.cs`
- Create: `src/games/stardew/StardewCommandService.cs`
- Create: `src/games/stardew/StardewCommandContracts.cs`
- Create: `src/games/stardew/StardewQueryService.cs`
- Create: `src/games/stardew/StardewEventIngestor.cs`
- Create: `src/games/stardew/StardewWorldAdapter.cs`
- Create: `src/games/stardew/SmapiModApiClient.cs`
- Create: `src/games/stardew/StardewBridgeDtos.cs`
- Create: `src/games/stardew/StardewNpcBindingResolver.cs`
- Create: `src/games/stardew/StardewBridgeLogRecord.cs`
- Create: `src/games/stardew/StardewBridgeDiagnostics.cs`
- Create: `Mods/StardewHermesBridge/StardewHermesBridge.csproj`
- Create: `Mods/StardewHermesBridge/ModEntry.cs`
- Create: `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- Create: `Mods/StardewHermesBridge/Bridge/NpcMoveEndpoint.cs`
- Create: `Mods/StardewHermesBridge/Ui/BridgeStatusOverlay.cs`
- Create: `Mods/StardewHermesBridge/Ui/BridgeDebugMenu.cs`
- Create: `Mods/StardewHermesBridge/Logging/SmapiBridgeLogger.cs`
- Create: `Mods/StardewHermesBridge/manifest.json`
- Test: `Desktop/HermesDesktop.Tests/Stardew/StardewNpcBindingResolverTests.cs`
- Test: `Desktop/HermesDesktop.Tests/Stardew/StardewBridgeSecurityTests.cs`
- Test: `Desktop/HermesDesktop.Tests/Stardew/StardewCommandContractTests.cs`

**Outcome:** 与 SMAPI 的边界清晰，Stardew 语义不污染 core，并明确借鉴 hermescraft 的 `/health`、`/status`、`/task/*`、`/action/*` 分层与长会话观察世界模式。这里必须先打通 **move command** 的最小真实闭环，并且 mod 内要有最小 overlay / debug menu 与 bridge log。所有非 `/health` 请求必须通过 `Authorization: Bearer <bridgeToken>`，统一使用 `StardewBridgeDtos.cs` / `StardewCommandContracts.cs` 的 envelope、`commandId`、status、error code、idempotency 和 trace。

### Task 8: Add evidence harness and replay proof
**Files:**
- Create: `scripts/verification/verify-stardew-two-npc-mvp.ps1`
- Create: `Desktop/HermesDesktop.Tests/Integration/StardewTwoNpcIsolationFlowTests.cs`
- Modify: `src/runtime/NpcRuntimeTrace.cs`
- Modify: `src/transcript/TranscriptStore.cs`

**Outcome:** 能证明 NPC A 的秘密不会被 NPC B 读到，并能串起 NPC -> typed tool -> `StardewCommandService` -> move command -> SMAPI bridge -> `UpdateTicked` result -> transcript/activity/audit 的证据链。该脚本必须至少验证一次真实 move 闭环、token 拒绝、stale discovery 拒绝、resource claim 冲突和 claim release。

### Task 9: Wire Hermes Desktop debug UI and log surfaces
**Files:**
- Create: `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- Create: `Desktop/HermesDesktop/Models/NpcRuntimeItem.cs`
- Create: `Desktop/HermesDesktop/Models/NpcRuntimeLogItem.cs`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcRuntimePanel.xaml`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcRuntimePanel.xaml.cs`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcLogsPanel.xaml`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcLogsPanel.xaml.cs`
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml`
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`
- Modify: `Desktop/HermesDesktop/MainWindow.xaml.cs`

**Outcome:** 当前项目 UI 明确补上 NPC Runtime、Bridge Health、Last Error、Recent Trace、日志查看与日志目录入口，而不是只保留抽象 dashboard 概览。这里是 Phase 1 最小调试面，不是完整漂亮 UI；完整过滤、可视化 replay 和复杂管理面板后置。

---

## Suggested Initial Topology Decision

我建议**第一实现阶段先做 in-process host，但按 topology-neutral API 落地**。

### Why this is the best first move
1. 当前 DI、Agent、Memory、Transcript 都已经在 `App.xaml.cs:259-775` 里集中装配，先在同进程里拉起多实例成本最低。
2. 你的硬约束不是“必须多进程”，而是“不能被单进程绑死”。
3. 先把 `NpcRuntimeSupervisor + NpcNamespace + GameContract + NpcPackLoader` 抽象好，比过早引入 IPC 更能降低架构返工。
4. 如果后面 Stardew bridge 或隔离要求逼出多进程，只需要替换 host topology，不重写 core。

### When to switch to multi-process later
若出现以下任一情况，就应切多进程：
- SMAPI bridge 明显要求进程边界隔离
- 每 NPC 运行资源竞争严重
- 崩溃隔离成为核心需求
- 双 NPC MVP 很快验证通过，准备扩到更多 NPC

---

## NPC Pack Schema Draft

```json
{
  "schemaVersion": 1,
  "npcId": "haley",
  "gameId": "stardew-valley",
  "profileId": "default",
  "defaultProfileId": "default",
  "displayName": "Haley",
  "smapiName": "Haley",
  "aliases": ["haley", "Haley", "海莉"],
  "targetEntityId": "Haley",
  "adapterId": "stardew",
  "soulFile": "SOUL.md",
  "factsFile": "facts.md",
  "voiceFile": "voice.md",
  "boundariesFile": "boundaries.md",
  "skillsFile": "skills.json",
  "policies": {
    "schedulePriority": "agent_unless_story_event",
    "dialogStyle": "social"
  },
  "capabilities": ["move", "speak"]
}
```

### Required validation rules
- `npcId`, `gameId`, `profileId`, `defaultProfileId`, `smapiName`, `aliases`, `targetEntityId`, `adapterId` 必填
- `adapterId` 必须存在已注册 adapter
- `soulFile` / `factsFile` / `voiceFile` / `boundariesFile` / `skillsFile` 路径必须位于 pack root 内
- runtime 路径只使用 `npcId`；SMAPI 查询只使用 `smapiName`；玩家输入只经 `aliases` 归一化
- `profileId = default` 在 MVP 中不可省略
- `capabilities` 只能声明已知能力，不允许自由字符串驱动任意行为
- Phase 1 capability 只允许 `move` 和 `speak` 等已实现能力；`gather`、`collect`、`craft`、`foraging` 后置到对应 DTO、resource claim 和测试完成之后

---

## Roadmap Preservation

### Phase 1
- 双 NPC（建议首发 Haley / Penny，或资料包定义的两名 NPC）
- 真实 SMAPI bridge
- 独立 namespace
- 简单 tick loop
- `NpcAutonomyBudget` 预算门闸
- 最小 `move` resource claim / idempotency / failure reason
- 最小 world coordination seam
- loopback/token bridge security + `HERMES_HOME/config.yaml` 配置来源
- 明确 replay / trace 证据
- 结构化日志
- SMAPI mod 最小 overlay/debug UI
- Hermes Desktop 最小运行时调试 UI
- Haley / Penny 固定 soul seed + Stardew game skills 组合

### Later phases
- 3+ / 10+ NPC
- 更强 world coordination
- 更复杂社交与经济系统
- 多进程 / 分布式 topology
- 复用最小 core 接入其他游戏

## Evidence boundary
- 当前已有：Agent、tool loop、memory 基础件、soul 基础件、transcript store、activity 基础
- 借鉴参考：hermescraft 的长会话 agent、HTTP bridge、task status、后台冲突处理模式
- 新增设计：NPC runtime supervisor、NPC namespace、Stardew adapter、SMAPI bridge contract、pack loader、traceId 串联、commandId、idempotency、resource claim、runtime log、bridge token、config.yaml section、mod-side overlay、desktop-side log viewer、NpcAutonomyBudget

## Testing Strategy

### Unit tests first
- `NpcPackManifestTests`：校验 manifest 必填项与路径规则
- `NpcPackLoaderTests`：校验目录发现、schema 验证、重复 NPC 拒绝
- `NpcNamespaceTests`：校验每 NPC 路径隔离
- `NpcRuntimeSupervisorTests`：校验多 runtime 注册、恢复、停止
- `NpcAutonomyBudgetTests`：校验并发 LLM request、max tool iterations、restart cooldown、exit reason
- `ResourceClaimRegistryTests`：校验同一 NPC、target tile、interaction tile、目标对象冲突和释放
- `WorldCoordinationServiceTests`：校验双 NPC 冲突仲裁 seam
- `StardewNpcBindingResolverTests`：校验 pack 到现有 NPC 实体的绑定
- `StardewBridgeSecurityTests`：校验 loopback、token、stale discovery、`/health` 最小泄露
- `StardewCommandContractTests`：校验统一 envelope、`commandId`、status、error code、idempotency

### Integration tests later
- pack load -> namespace build -> runtime start -> adapter bind
- 双 NPC 启动后 transcript/state 分离
- 节日/剧情事件触发后 policy 生效
- `move` action 从 NPC runtime 穿过 typed tool、`StardewCommandService`、`SmapiModApiClient`、`POST /task/move`、SMAPI mod `UpdateTicked` 再回写 trace
- token 错误、stale discovery、saveId mismatch 会被拒绝
- 两个 NPC 抢同一 tile/object 时返回 `command_conflict`，取消/失败/跨天后释放 claim
- `POST /query/status`、`POST /query/world_snapshot`、`POST /events/poll` 能返回 runtime 需要的最小观察数据
- desktop log 与 mod log 共享同一 `traceId`，能从 UI 直接定位到失败链路
- mod overlay 能显示最近一次 move 请求与失败原因

---

## Risks and counters

### Risk 1: 抽象过早
**Counter:** `GameAction` / `GameObservation` / `WorldSnapshot` 先只覆盖 Stardew MVP 所需字段，不为未来虚构能力预埋复杂层。

### Risk 2: Pack 变成脚本平台
**Counter:** pack schema 只允许声明式内容与策略值，不允许自由代码或表达式执行。

### Risk 3: 仍然被当前 App/ChatService 单会话路径绑死
**Counter:** `NpcRuntimeSupervisor` 不依赖 `_currentSession`，只依赖 agent factory 和 namespace builder。

### Risk 4: Stardew adapter 侵入 core
**Counter:** 所有带 `Stardew`, `SMAPI`, `Festival`, `Friendship`, `Dialogue` 等游戏术语的类型禁止进入 `src/runtime/` 与 `src/game/core/`。

### Risk 5: 出问题无法定位
**Counter:** Phase 1 强制结构化日志 + traceId + mod overlay + desktop log viewer 同时落地。

### Risk 6: Bridge contract 双轨
**Counter:** `StardewBridgeDtos.cs` / `StardewCommandContracts.cs` 是唯一契约源；typed tools、CLI、UI、bridge 全部使用统一 envelope、`commandId`、status、error code、idempotency 和 trace。

### Risk 7: Bridge 被旧进程或脚本误调用
**Counter:** Phase 1 强制 loopback-only、runtime `bridgeToken`、`HERMES_HOME/config.yaml` 配置源、discovery stale/saveId 校验；无 token 的非 `/health` 请求返回 `bridge_unauthorized`。

### Risk 8: 双 NPC 常驻循环烧模型或无限重启
**Counter:** `NpcAutonomyBudget` 作为代码层预算门闸，限制并发 LLM request、max tool iterations、restart cooldown，并把超预算写入 trace。

### Risk 9: Phase 1 move 抢资源
**Counter:** `ResourceClaimRegistry` 与 move contract 同步落地；claim 冲突返回 `command_conflict`，命令终止、失败、取消、跨天和保存时释放 claim。

---

## Spec coverage check
- 跨游戏边界：covered by `src/game/core/` vs `src/games/stardew/`
- NPC 独立实例：covered by runtime host/supervisor/namespace
- 资料包动态加载：covered by pack schema + loader
- 同游戏新增 NPC 不改 core：covered by pack path + validation strategy
- 不强制单进程：covered by topology-neutral host design
- 双 NPC 隔离验收：covered by namespace / coordination / trace / tests
- `move` 真实 SMAPI bridge 闭环：covered by Task 7 + Task 8 + integration verification
- hermescraft 参考模式：covered by architecture sketch + reference links + adapter task
- 日志问题与排障能力：covered by logging design + Task 5 + Task 7 + Task 9
- mod UI 与当前项目 UI 改造：covered by SMAPI mod UI requirements + Hermes Desktop UI changes + Task 7 + Task 9
- bridge 安全与配置来源：covered by Task 7 + `StardewBridgeSecurityTests`
- LLM 预算与常驻 loop 控制：covered by Task 5 + `NpcAutonomyBudgetTests`
- move resource claim：covered by Task 6 + Task 7 + Task 8

---

## Immediate decision for implementation phase

如果现在进入实现，我建议先做这五个最小骨架：
1. `src/game/core/` contract + `NpcPackManifest`
2. `FileSystemNpcPackLoader`
3. `NpcNamespace`
4. `StardewBridgeDtos` / `StardewCommandContracts`
5. `NpcRuntimeLogWriter`

这是最小且最不容易返工的起步面。

---

Plan complete and saved to `docs/superpowers/plans/2026-04-29-cross-game-npc-runtime-architecture.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
