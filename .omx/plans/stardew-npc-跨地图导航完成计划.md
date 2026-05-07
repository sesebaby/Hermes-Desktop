# Stardew NPC Cross-Map Navigation Completion Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把当前已验证的同地图 move 和已实现的跨地图 `routeProbe` 推进到“NPC 能从一个地图自然走到另一个地图目标 tile”的完整闭环。

**Architecture:** 主模型负责通过分层地图 skill 选择 `locationName/x/y/source`；本地小模型不加载地图 skill，也不推理坐标；宿主以 `host_deterministic` 固定调用 executor-only `stardew_navigate_to_tile`。SMAPI bridge 负责 route probe、当前地图内 segment path、等待自然 warp、切图后重规划，并把每一步写入 `task_status` / runtime log。

**Tech Stack:** C# / .NET 10 Hermes runtime, SMAPI bridge `net6.0`, Stardew Valley 1.6 API, `WarpPathfindingCache.GetLocationRoute`, `GameLocation.getWarpPointTo`, `PathFindController.findPathForNPCSchedules`, MSTest。

---

## 当前状态

已完成提交：

- `f605663c`：机械坐标移动绕过弱本地模型。
- `acd64419`：把导航探针放到任务状态面。
- `7bf20112`：把 OMX 计划/spec artifact 纳入版本控制。
- `8815ac97`：实现真实跨地图 `routeProbe`，使用 `WarpPathfindingCache.GetLocationRoute`、`currentLocation.getWarpPointTo`、schedule path probe；当前仍不执行跨图移动。

已验证：

- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMoveCommandQueueRegressionTests|FullyQualifiedName~BridgeMovementPathProbeTests|FullyQualifiedName~BridgeMoveFailureMapperTests"`：26 passed。
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug`：89 passed。
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewCommandServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests|FullyQualifiedName~NpcLocalExecutorRunnerTests|FullyQualifiedName~NpcLocalActionIntentTests"`：64 passed。

当前限制：

- `routeProbe.status=route_found` 只证明能算 route 和下一段 `standTile`，不代表 NPC 已经移动或过图。
- 跨地图 command 当前允许 `blocked/cross_location_execution_not_enabled`，这是探针阶段设计，不是最终完成态。
- 还没有跨图 segment 状态机、自然 warp 等待、切图后重规划、最终目标完成判断。

## 必守原则

- 实现 SMAPI/Stardew API 前必须先查官方或一手资料；不能凭记忆改 bridge。
- 地图 skill 必须分层披露，不能一次塞全地图坐标。
- 本地小模型能力弱，不让它加载地图 skill，不让它选择或改写 `locationName/x/y`。
- `source` 只做 Hermes runtime 审计字段：`NpcLocalExecutorResult.TargetSource` / `NpcRuntimeLogRecord.TargetSource`。不得进入 tool schema 或 bridge payload。
- `stardew_navigate_to_tile` 保持 executor-only，不进主 agent 默认工具面。
- `routeProbe` 只在 `task_status` / `GameCommandStatus.RouteProbe` 状态面，不进 `TaskMove` accepted response。
- 不用 `Game1.warpCharacter` 冒充自然移动成功；它只能作为显式调试 fallback，且不能计入自然导航验收。
- 小步提交。每个阶段完成后必须提交，commit message 使用 Lore protocol。

## 参考依据

### 官方 / 一手 API 依据

需要在每次改 bridge 前复核：

- `WarpPathfindingCache.GetLocationRoute(currentLocationName, targetLocationName, npc.Gender)`：原版 location route 入口。
- `GameLocation.getWarpPointTo(nextLocationName)`：当前地图到下一地图的 warp tile 解析。
- `PathFindController.findPathForNPCSchedules(...)`：原版 NPC schedule pathfinding 的 tile path 探测。
- 原版 schedule 条目本质是 `time location x y direction`；跨地图移动不是全局 tile A*，而是 location route + 每张图局部 path + warp。

### 参考 mod

#### Market Day

路径：`参考项目/Mod参考/Stardew-GitHub-ncarigon-CeruleanStardewMods/Market/MarketDay`

借鉴：

- `Utility/Schedule.cs` 的 `PathfindToNextScheduleLocation(...)` 先算 location route，再逐图生成 `SchedulePathDescription`。
- 中间地图通过 `getWarpPointTo(nextLocation)` 找 warp。
- 每张地图内仍复用 schedule pathfinding。

不照搬：

- 不 patch `NPC.parseMasterSchedule` 作为当前主路线。
- 不复制商店、摊位、日程替换业务逻辑。

#### BotFramework

路径：`参考项目/Mod参考/Stardew-GitHub-andyruwruw-BotFramework`

借鉴：

- `WorldTour` / `WorldPath` 把 world 拆成 location graph。
- `WorldParser.FindPathToNextLocation()` 将跨地图目标拆成“下一张地图”和“当前图 warp tile”。
- `LocationParser.WarpToTile(warp)` 把 warp 转成当前地图内可执行 tile。

不照搬：

- 不搬整个 bot action/tour 框架。
- 不替代 Hermes NPC runtime 的人格、任务、权限和工具边界。

#### CustomNPCFixes

路径：`参考项目/Mod参考/Stardew-GitHub-spacechase0-CustomNPCFixes/Mod.cs`

借鉴：

- route/schedule 失败时先考虑原版 route cache 或 schedule 需要刷新。
- 诊断顺序应先查 route cache，再查 warp，再查 tile path。

不照搬：

- 不做全 NPC schedule 修复。
- Stardew 1.6 优先验证 `WarpPathfindingCache`，不能直接照搬旧版本 API。

## 完成定义

功能完成必须同时满足：

- 主模型说“去海边”时，能通过分层 skill 选择明确 `Beach/x/y/source`。
- local executor 对 mechanical target 始终 `host_deterministic`，不调用本地模型生成坐标参数。
- bridge 从 `Town` 到 `Beach` 能：
  - 算出 route。
  - 执行当前地图内到 warp tile 的第一段移动。
  - 到达 warp tile 后等待原版自然切图。
  - 切图后重新计算下一段。
  - 最终到达 `Beach/x/y` 或给出结构化失败。
- `task_status` 能持续反映 `routeProbe`、当前 phase、当前 segment、失败码或完成证据。
- 日志可解释每一次移动为什么失败：不是一句自然语言，而是稳定字段。
- 现有同地图 move、`destinationId` move、NPC 私聊、自主循环不退化。

## 路线选项

### 选项 A：按阶段完成自然跨地图导航

内容：live probe -> 第一段移动 -> 等待自然 warp -> 切图后重规划 -> 完整状态机 -> skill 分层验收。

理由：

- 每一步都有可独立验证的证据。
- 失败能定位在 route、warp、tile path、controller、状态机或 skill 坐标。
- 不需要用瞬移伪造成功。

推荐：最高。本文后续任务按选项 A 编排。

### 选项 B：直接一次性做完整状态机

理由：

- 用户可见结果最快。

不推荐理由：

- route、path、warp、状态机、日志和 UI 同时变，失败时无法判断根因。
- 违反小步快跑和及时提交。

推荐：不采用。

### 选项 C：先用临时 schedule 注入实现跨图

理由：

- 最贴近原版 NPC schedule 机制。
- 可能减少手写状态机。

不作为主线理由：

- 会影响 NPC 当日日程，需要恢复和冲突处理。
- 与 Hermes “agent 意图 -> 宿主执行器”边界更远。

推荐：只作为后续对照实验，不阻塞主线。

### 选项 D：`Game1.warpCharacter` fallback

理由：

- 调试方便，可用于解卡。

不推荐理由：

- 不是自然移动，会掩盖真实寻路问题。

推荐：只做显式 debug/fallback，永远不计入自然导航通过。

## 文件责任图

### Bridge 主改动

- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
  - 当前 command phase、route probe、segment 执行、自然 warp 等待、重规划和状态写入的主位置。
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
  - DTO：`RouteProbeData`、segment data、未来 phase/status 字段。
- `Mods/StardewHermesBridge/Bridge/BridgeMovementPathProbe.cs`
  - 同地图 path probe；跨图每段都必须复用它，不另造全局 A*。
- `Mods/StardewHermesBridge/Bridge/BridgeMoveFailureMapper.cs`
  - 稳定失败码映射。

### Bridge 测试

- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`
  - 防回归：不使用 `warpCharacter`，API 名称和关键路径不漂移。
- `Mods/StardewHermesBridge.Tests/BridgeMovementPathProbeTests.cs`
  - path probe 行为。
- 新增或扩展 `Mods/StardewHermesBridge.Tests/BridgeCrossMapNavigationStateTests.cs`
  - phase、segment、warp wait、replan、completion。

### Hermes runtime / DTO

- `src/games/stardew/StardewBridgeDtos.cs`
  - 接收 bridge task status 的 route/phase/segment 字段。
- `src/game/core/GameAction.cs`
  - `GameCommandStatus` 扩展 phase/route/segment/diagnostic 字段。
- `src/games/stardew/StardewCommandService.cs`
  - bridge DTO 到 core status 的映射。
- `src/runtime/NpcLocalExecutorRunner.cs`
  - 保持 mechanical target 的 `host_deterministic` 路径不变。
- `src/runtime/NpcRuntimeLogWriter.cs`
  - 写 `targetSource`、phase、route diagnostics。

### Hermes 测试

- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
  - DTO 映射。
- `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs`
  - 本地小模型不参与坐标改写。
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
  - executor-only 工具面。
- 新增或扩展 `Desktop/HermesDesktop.Tests/Stardew/StardewCrossMapStatusMappingTests.cs`
  - phase/segment/status 映射验收。

### Skill / 文档

- `skills/gaming/stardew-navigation/SKILL.md`
  - 只披露导航策略、分层索引、如何请求更细 skill。
- `skills/gaming/stardew-world/SKILL.md`
  - 只披露地点类别和区域入口，不一次性披露坐标。
- 后续新增细粒度 POI reference 或 child skill：
  - `skills/gaming/stardew-navigation/references/regions/beach.md`
  - `skills/gaming/stardew-navigation/references/regions/town.md`
  - `skills/gaming/stardew-navigation/references/poi/beach-shoreline.md`
  - `skills/gaming/stardew-navigation/references/poi/town-square.md`

## Phase 0：真实环境 live probe 复核

目标：证明当前提交在真实 SMAPI 环境里能从 `Town -> Beach` 产出 `routeProbe.status=route_found` 和下一段 warp tile。

不做：不执行第一段移动，不等待过图，不改状态机。

- [ ] **Step 0.1：复核官方/一手 API**

  查 SMAPI/Stardew 1.6 一手资料或当前反编译签名，确认：

  - `WarpPathfindingCache.GetLocationRoute(...)` 当前签名。
  - `GameLocation.getWarpPointTo(...)` 当前行为。
  - `PathFindController.findPathForNPCSchedules(...)` 可用于 NPC schedule path。

  停止条件：把结论写入本阶段 commit body 或 `.omx/context/...`，不得只靠记忆。

- [ ] **Step 0.2：运行现有自动测试**

  ```powershell
  dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
  dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewCommandServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests|FullyQualifiedName~NpcLocalExecutorRunnerTests|FullyQualifiedName~NpcLocalActionIntentTests"
  ```

  预期：全部通过。

- [ ] **Step 0.3：按用户视角手测 `Town -> Beach`**

  使用 `.omx/plans/测试规范-stardew-npc-跨地图导航最小探针.md` 的“手工验证（用户视角）”章节。

  预期：

  - `routeProbe.status=route_found`。
  - `routeProbe.route` 包含 `Town` 和 `Beach`。
  - `routeProbe.nextSegment.targetKind=warp_to_next_location`。
  - `routeProbe.nextSegment.nextLocationName=Beach`。
  - `routeProbe.nextSegment.standTile` 有明确 `x/y`。

- [ ] **Step 0.4：提交 live probe 结果**

  如果无需改代码，只提交日志/文档结果；如果需要补诊断，只补日志或映射，不扩大到状态机。

  推荐提交边界：只包含测试、诊断或文档证据。

## Phase 1：执行跨地图第一段同地图移动

目标：当 `routeProbe.status=route_found` 且目标跨地图时，bridge 不再立即 blocked，而是把当前 command 的执行目标改为当前地图内通向下一地图的 `standTile`，并复用同地图 path probe 移动到该 tile。

不做：不处理切图后续段；到达 warp tile 后可以停在 `awaiting_warp`。

选项：

- 选项 A：在现有 move command 内加入 `CrossMapPhase=executing_segment`。
- 选项 B：生成一个新的内部 child command 表示 segment。

推荐：选项 A。

理由：第一段仍属于同一个玩家/NPC 意图，保持一个 commandId 更容易让 `task_status` 连续追踪；内部 child command 会增加 UI 和日志关联成本。

- [ ] **Step 1.1：写失败测试**

  文件：`Mods/StardewHermesBridge.Tests/BridgeCrossMapNavigationStateTests.cs`

  测试名建议：`PumpMoveCommand_CrossLocationRouteFound_ExecutesFirstSegmentToWarpTile`

  断言：

  - cross-location target 不再立刻 `Blocked("cross_location_execution_not_enabled")`。
  - command phase 进入 `executing_segment`。
  - 当前 segment target tile 等于 `routeProbe.nextSegment.standTile`。
  - segment path probe 使用 `PathFindController.findPathForNPCSchedules`。
  - `routeProbe` 仍保留在 task status。

- [ ] **Step 1.2：实现最小代码**

  文件：`Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`

  实现要求：

  - route found 后设置 command phase：`executing_segment`。
  - `CurrentSegment.LocationName=currentLocationName`。
  - `CurrentSegment.TargetKind=warp_to_next_location`。
  - `CurrentSegment.TargetTile=routeProbe.nextSegment.standTile`。
  - 使用现有同地图 move controller/path probe 走到该 tile。

- [ ] **Step 1.3：状态面映射**

  文件：

  - `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
  - `src/games/stardew/StardewBridgeDtos.cs`
  - `src/game/core/GameAction.cs`
  - `src/games/stardew/StardewCommandService.cs`

  最小字段：

  - `crossMapPhase`
  - `currentSegment`
  - `routeProbe`
  - `lastFailureCode`

- [ ] **Step 1.4：运行测试**

  ```powershell
  dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeCrossMapNavigationStateTests|FullyQualifiedName~BridgeMoveCommandQueueRegressionTests|FullyQualifiedName~BridgeMovementPathProbeTests"
  dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewCommandServiceTests"
  ```

- [ ] **Step 1.5：提交**

  提交意图：让跨地图目标先执行当前地图内 warp segment。

## Phase 2：等待自然 warp 并识别切图

目标：NPC 到达当前地图 warp tile 后，不调用 `Game1.warpCharacter`，而是等待 Stardew 原版自然切图；检测 `npc.currentLocation` 变化后进入重规划。

不做：不做完整多段循环；只处理一次 `Town -> Beach` 的自然切图识别。

选项：

- 选项 A：到达 warp tile 后进入 `awaiting_warp`，轮询 `npc.currentLocation.NameOrUniqueName`。
- 选项 B：hook warp 事件或 SMAPI event。

推荐：先选项 A。

理由：当前 bridge command queue 已经 tick 驱动，轮询状态最小、可测、可回滚；事件 hook 适合作为后续优化。

- [ ] **Step 2.1：写失败测试**

  测试名建议：`PumpMoveCommand_ReachesWarpTile_WaitsForNaturalLocationChange`

  断言：

  - 到达 segment target 后 phase 变为 `awaiting_warp`。
  - 未切图时 command 不完成。
  - 未切图超时后返回 `warp_transition_timeout`，不是自然语言失败。
  - 代码不调用 `Game1.warpCharacter`。

- [ ] **Step 2.2：实现 phase**

  文件：`Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`

  最小字段：

  - `ExpectedNextLocationName`
  - `AwaitingWarpStartedTick`
  - `WarpTimeoutTicks`
  - `LastKnownLocationName`

  规则：

  - 如果 `npc.currentLocation` 变成 expected next location，进入 `replanning_after_warp`。
  - 如果超时，blocked/failed：`warp_transition_timeout`。
  - 如果 NPC 被外力带到其他地图，failed：`unexpected_location_after_warp`，附带 actual/expected。

- [ ] **Step 2.3：手测**

  用户视角：

  - NPC 从 `Town` 走向去 `Beach` 的 warp tile。
  - 到达后不瞬移。
  - 如果原版自然切图成功，`task_status.crossMapPhase` 变为 `replanning_after_warp`。
  - 如果没有切图，看到 `warp_transition_timeout`。

- [ ] **Step 2.4：提交**

  提交意图：用原版自然切图推进跨地图 segment。

## Phase 3：切图后重规划并循环执行后续段

目标：每次 NPC 到达下一地图后重新 route probe；如果当前地图等于最终目标地图，则执行最终 tile segment；否则执行下一 warp segment。

不做：不做多 NPC 资源协调优化；只保证单 NPC 单 command 完整闭环。

选项：

- 选项 A：每次切图后重新调用 route probe。
- 选项 B：初始 route 全量缓存，然后逐段消费。

推荐：选项 A。

理由：Stardew 地图状态可能变化，重新 probe 更稳；route 列表很短，性能不是瓶颈；失败诊断更准确。

- [ ] **Step 3.1：写失败测试**

  测试名建议：`PumpMoveCommand_AfterWarp_ReplansUntilFinalTargetLocation`

  场景：

  - 初始 `Town -> Beach`。
  - mock/fixture 让 NPC currentLocation 从 `Town` 变为 `Beach`。
  - 重规划后目标变为 final `Beach/x/y`。

  断言：

  - phase 从 `replanning_after_warp` 进入 `executing_segment`。
  - 当前地图等于目标地图时 `currentSegment.targetKind=final_target_tile`。
  - segment target 是 parent target tile，不是 warp tile。

- [ ] **Step 3.2：实现循环**

  文件：`Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`

  phase：

  - `resolving_route`
  - `executing_segment`
  - `awaiting_warp`
  - `replanning_after_warp`
  - `arriving`
  - `completed`
  - `blocked`
  - `failed`

  规则：

  - 当前地图 == 目标地图：执行 final tile segment。
  - 当前地图 != 目标地图：执行 warp segment。
  - 每段失败都要写 `failureCode` 和 `failureDetail`。

- [ ] **Step 3.3：状态映射和日志**

  Hermes `GameCommandStatus` 必须能看到：

  - `crossMapPhase`
  - `currentLocationName`
  - `targetLocationName`
  - `currentSegment`
  - `routeProbe`
  - `failureCode`

  runtime log 必须能回答：

  - 主模型给的目标是什么。
  - 坐标来源哪个 skill。
  - 当前第几段。
  - 是否等待 warp。
  - 为什么失败。

- [ ] **Step 3.4：运行测试并提交**

  ```powershell
  dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeCrossMapNavigationStateTests|FullyQualifiedName~BridgeMoveCommandQueueRegressionTests|FullyQualifiedName~BridgeMovementPathProbeTests"
  dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewCommandServiceTests|FullyQualifiedName~NpcLocalExecutorRunnerTests"
  ```

## Phase 4：最终到达判定、失败恢复和观测面

目标：把“NPC 到达目标 tile 附近”稳定判为完成，把常见失败稳定分类，并让用户能从日志知道下一步该修什么。

选项：

- 选项 A：到达精确 tile 才完成。
- 选项 B：沿用现有邻点 fallback / 到达邻近 tile 也可完成。

推荐：选项 B。

理由：Stardew NPC pathfinding 已经有目标邻点 fallback；精确 tile 可能被障碍、NPC 或临时物体占用，过严会导致可用性差。

- [ ] **Step 4.1：补完成判定测试**

  测试名建议：

  - `PumpMoveCommand_FinalTargetReachable_CompletesAtTargetTile`
  - `PumpMoveCommand_FinalTargetBlocked_CompletesAtReachableAdjacentTileWhenAllowed`
  - `PumpMoveCommand_FinalTargetUnreachable_ReturnsTargetTileUnreachable`

- [ ] **Step 4.2：补失败分类**

  稳定失败码至少包括：

  - `location_not_found`
  - `route_not_found`
  - `warp_point_not_found`
  - `segment_path_unreachable`
  - `warp_transition_timeout`
  - `unexpected_location_after_warp`
  - `target_tile_unreachable`
  - `npc_controller_interrupted`

- [ ] **Step 4.3：补观测面**

  最少要在这些地方看得到：

  - SMAPI log：每个 command phase transition。
  - Hermes log：local executor result、targetSource、commandId。
  - runtime `runtime.jsonl`：NPC 视角行动结果。
  - `stardew_task_status`：当前 phase/route/segment/failure。

- [ ] **Step 4.4：手测并提交**

  手测场景：

  - `Town -> Beach` 成功。
  - `Beach -> Town` 成功。
  - 不存在地图名失败。
  - 可达地图但不可达 tile 失败。

## Phase 5：地图 skill 分层披露和自然语言泛化

目标：支持 agent 说“去海边”“去广场”“去铁匠铺附近”时，主模型逐层加载 skill 找到地图名和坐标；但上下文不爆炸。

选项：

- 选项 A：一个大 skill 写全部地点坐标。
- 选项 B：index -> region -> poi 分层披露。

推荐：选项 B。

理由：用户已明确要求地图 skill 必须分层披露；全量坐标会爆上下文，也会诱导模型在低层工具参数里漂移。

- [ ] **Step 5.1：设计 skill 文件结构**

  建议：

  - `skills/gaming/stardew-navigation/SKILL.md`：导航总规则和分层索引。
  - `skills/gaming/stardew-navigation/references/index.md`：只列 region。
  - `skills/gaming/stardew-navigation/references/regions/town.md`：Town 出口、常用区域。
  - `skills/gaming/stardew-navigation/references/regions/beach.md`：Beach 出口、常用区域。
  - `skills/gaming/stardew-navigation/references/poi/beach-shoreline.md`：海边坐标。
  - `skills/gaming/stardew-navigation/references/poi/town-square.md`：广场坐标。

- [ ] **Step 5.2：写 skill 验收测试**

  文件建议：`Desktop/HermesDesktop.Tests/Stardew/StardewNavigationSkillTests.cs`

  断言：

  - index 不含大批坐标。
  - region 只含该区域坐标。
  - poi 才含具体 `locationName/x/y/source`。
  - prompt 要求 parent intent 填 `target.source`。

- [ ] **Step 5.3：接入主模型决策提示**

  文件：

  - `src/runtime/NpcAutonomyLoop.cs`
  - `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`

  规则：

  - 主模型可以输出 `destinationId` 或 `target(locationName,x,y,source)`。
  - target 必须来自已披露地图 skill。
  - local executor 不加载地图 skill。

- [ ] **Step 5.4：手测泛化语句**

  用户输入：

  ```text
  让这个 NPC 去海边。
  ```

  通过标准：

  - 主模型逐层选出 `Beach/x/y/source`。
  - local executor 使用 `host_deterministic`。
  - bridge 自然跨图或结构化失败。

## Phase 6：可选 schedule 注入对照实验

目标：如果手写 segment 状态机在原版 NPC 行为上长期不稳定，做一个独立对照实验验证“临时 schedule 注入”是否更接近原版。

不作为主线：除非 Phase 1-5 显示自然 segment 状态机不可稳定修复，否则不要切换。

选项：

- 选项 A：只做只读 schedule probe，不改 NPC schedule。
- 选项 B：临时注入 schedule，执行后恢复。
- 选项 C：永久改写当日日程。

推荐：必要时只做选项 B，并且 gated。

理由：B 能验证原版机制，但必须恢复 schedule；C 风险过大。

- [ ] **Step 6.1：写 ADR**

  说明为什么 Phase 1-5 不足，为什么需要 schedule 对照。

- [ ] **Step 6.2：写 isolated 实验，不接主线**

  要求：

  - 单独 debug flag。
  - 单独测试。
  - 不默认启用。
  - 不影响 `stardew_navigate_to_tile` 主路径。

## 用户视角总手测脚本

在完成每个 phase 后，用户应按以下顺序测：

1. 启动 Hermes：

   ```powershell
   .\run-desktop.ps1 -Rebuild
   ```

2. 启动 SMAPI Stardew，进入存档。
3. 打开日志：

   ```powershell
   Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\hermes.log" -Tail 300 -Wait
   Get-Content "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" -Tail 300 -Wait
   ```

4. 找一个当前在 `Town` 的 NPC。
5. 在 Hermes 里发：

   ```text
   让这个 NPC 去海边，不要瞬移。使用地图 skill 选一个明确 Beach 坐标。
   ```

6. Phase 0 通过：看到 `routeProbe.route_found` 和 `nextSegment.nextLocationName=Beach`。
7. Phase 1 通过：NPC 走向 Town 通往 Beach 的 warp tile。
8. Phase 2 通过：NPC 到达 warp tile 后自然切到 Beach，状态进入 `replanning_after_warp`。
9. Phase 3 通过：NPC 切到 Beach 后继续走向最终 Beach 坐标。
10. Phase 4 通过：任务完成或返回稳定失败码。
11. Phase 5 通过：用户只说“去海边”，系统也能通过分层 skill 找到 `Beach/x/y/source`。

## 排障矩阵

- 无 `routeProbe`：查 DTO 映射和 task status，不要先改寻路。
- `location_not_found`：地图 skill 给错内部地图名。
- `route_not_found`：查 `WarpPathfindingCache` / route cache。
- `warp_point_not_found`：查 `getWarpPointTo` 和当前地图出口。
- `segment_path_unreachable`：查当前地图内 path probe 或 stand tile。
- `warp_transition_timeout`：NPC 到了 warp tile 但原版没切图，查是否 stand tile 错、controller 未触发、时间/事件阻塞。
- `unexpected_location_after_warp`：NPC 被其他系统移动或 route 过期。
- `npc_controller_interrupted`：NPC schedule、event、其他 mod 或 bridge command 打断。
- NPC 不动但 status 正常：先看 controller 是否创建，再看 SMAPI log，不要直接改 skill。

## 每阶段提交要求

每个 phase 至少一个提交，除非只是文档/日志补充。提交必须遵守 Lore protocol。

推荐 commit intent：

- Phase 0：`Record live route-probe evidence before enabling cross-map movement`
- Phase 1：`Execute the first route segment instead of stopping at cross-map probe`
- Phase 2：`Wait for natural Stardew warp transitions during NPC navigation`
- Phase 3：`Replan cross-map NPC movement after each location transition`
- Phase 4：`Expose stable completion and failure diagnostics for cross-map movement`
- Phase 5：`Layer Stardew navigation skills around explicit mechanical targets`
- Phase 6：`Compare schedule injection only after segment navigation evidence`

每个 commit body 必须说明：

- Constraint：本地小模型不做坐标推理。
- Rejected：`Game1.warpCharacter` 不能作为自然成功。
- Tested：列出实际运行命令。
- Not-tested：如未跑真实 SMAPI 手测必须写明。

## 最终验收命令

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewCommandServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests|FullyQualifiedName~NpcLocalExecutorRunnerTests|FullyQualifiedName~NpcLocalActionIntentTests|FullyQualifiedName~StardewNavigationSkillTests"
```

最终手测必须覆盖：

- `Town -> Beach`。
- `Beach -> Town`。
- 同地图机械坐标移动。
- 不存在地图名。
- 可达地图但不可达 tile。

## 下一步推荐

推荐马上执行 Phase 0，然后 Phase 1。

理由：

- Phase 0 把当前 `8815ac97` 的真实游戏证据补齐，避免在未验证真实 SMAPI 的情况下继续写状态机。
- Phase 1 是最小用户可见进展：NPC 至少会走向跨图 warp tile。
- 如果 Phase 1 失败，失败面仍然很窄，只在“routeProbe -> 当前地图 segment”之间，不会污染后续 warp/replan 逻辑。

不推荐直接做 Phase 3 或 Phase 5。

理由：

- Phase 3 依赖 Phase 1/2 的 phase 和自然 warp 证据。
- Phase 5 的自然语言泛化依赖底层跨图执行稳定，否则 skill 会把执行问题伪装成 prompt 问题。
