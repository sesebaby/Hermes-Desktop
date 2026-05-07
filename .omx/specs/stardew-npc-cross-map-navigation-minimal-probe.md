# Stardew NPC 跨地图导航最小探针方案

## 目标

本方案用于定义 `stardew-npc-local-executor-minimal-test.md` 之后的下一步：在不破坏现有 `destinationId` 本地执行层合同的前提下，验证 NPC 是否能从“同地图移动”推进到“跨地图目标导航”。

当前目标不是一次性做完整小镇导航系统，而是做一个可观测、可回滚、可验证的最小探针：

`主模型选择地点与坐标 -> 宿主校验明确机械目标 -> 本地执行层记录 host_deterministic 执行 -> bridge 计算/验证跨地图 route -> 同地图段复用原版路径探测 -> 失败时返回结构化原因`

## 已确认事实

1. 当前 `stardew_move` 是语义移动工具，只接受 `destinationId`，要求从 observation 的 `destination[n].destinationId` 精确复制，不能发明坐标。
2. 当前本地执行层工具面只包含 `stardew_status`、`stardew_move`、`stardew_task_status`。
3. 当前 bridge command 层已经能解析两类目标：
   - `destinationId`
   - `target.locationName + target.tile`
4. 当前 bridge 执行移动时，如果 `npc.currentLocation != targetLocation`，会直接返回 `cross_location_unsupported`。
5. 当前同地图移动已经复用 `BridgeMovementPathProbe` 包装原版 `PathFindController.findPathForNPCSchedules`，并有目标邻点 fallback 与运行中 replan。
6. 官方/源码证据显示原版 NPC 跨地图日程不是全局 tile A*，而是：
   - 先算地图名 route；
   - 每张地图内算 tile 路径；
   - 到 warp/door 后自然切图；
   - 最终目标仍是 `targetLocationName + targetTile`。

## 用户约束

1. 地图 skill 必须分层披露，不能一次把全地图和坐标塞进主模型上下文。
2. 本地小模型思考能力弱，不应让它加载地图 skill 或做深层路线规划。
3. 主模型负责根据分层 skill 选择地点名称、地图名、坐标和意图理由。
4. 对机械坐标目标，本地小模型不再中转工具参数；宿主直接按 parent intent 构造工具参数，防止弱模型改写坐标。
5. 不能回到固定脚本路线思想。路线和目标应来自 world/navigation skill 与 bridge 探针，而不是写死“去海边走哪几步”。
6. 方案中必须明确列出参考 mod、本地路径、借鉴点和不照搬的边界。

## 参考 mod

### Market Day

本地路径：

`参考项目/Mod参考/Stardew-GitHub-ncarigon-CeruleanStardewMods/Market/MarketDay`

关键文件：

- `Patches.cs`
- `Utility/Schedule.cs`

关键引用点：

- `Patches.cs` patch `PathFindController.findPathForNPCSchedules`、`NPC.parseMasterSchedule`、`NPC.getMasterScheduleEntry`。
- `Utility/Schedule.cs` 的 `PathfindToNextScheduleLocation(...)` 会先调用 NPC 私有 `getLocationRoute` 得到地图 route。
- 对 route 中间地图，使用 `getWarpPointTo(nextLocation)` 找到通向下一张地图的 warp 点。
- 每个地图段调用 `FindPathForNpcSchedules(...)` 或原版 `PathFindController.findPathForNPCSchedules(...)`。
- 最终构造 `SchedulePathDescription(..., endingLocation, new Point(endingX, endingY))`。
- `ScheduleStringForMarketVisit(...)` 证明社区成熟方案会动态生成 schedule 字符串，但仍依赖原版 schedule/path 机制。

借鉴点：

- “跨地图 route + 每图局部 path + schedule 描述”的组装方式。
- 对失败 route 做结构化错误，而不是让模型猜。
- 可观测日志和可替换 schedule 入口。

不照搬边界：

- 不直接 patch `NPC.parseMasterSchedule` 作为第一步。
- 不复制 Market Day 的摊位/午餐/访问商店逻辑。
- 不把完整 schedule replacement 做成 v1 主路线。

### BotFramework

本地路径：

`参考项目/Mod参考/Stardew-GitHub-andyruwruw-BotFramework`

关键文件：

- `BotFramework/World/WorldParser.cs`
- `BotFramework/World/WorldTour.cs`
- `BotFramework/World/WorldPath.cs`
- `BotFramework/Locations/LocationParser.cs`

关键引用点：

- `WorldTour` 通过 `GetWarps()` 建立 `GameLocation` 图。
- `WorldPath` 在 location graph 上找最短路径。
- `WorldParser.FindPathToNextLocation()` 会找到下一张地图。
- `WorldParser.GetActions(...)` 会把去下一张地图的 warp tile 转成 `ActionType.Navigate`。
- `LocationParser.WarpToTile(warp)` 把 warp 转换成当前地图内要走到的 tile。

借鉴点：

- 将跨地图导航拆成“世界图 route”和“当前地图 warp tile 导航动作”。
- 把可执行目标表达成明确 action，而不是让模型口头推理路线。

不照搬边界：

- BotFramework 是机器人任务框架，不是原版 NPC schedule 系统。
- 不把它的 target/action/tour 架构整体搬进 Hermes。
- 不让它取代 NPC runtime 的人格、任务和工具边界。

### CustomNPCFixes

本地路径：

`参考项目/Mod参考/Stardew-GitHub-spacechase0-CustomNPCFixes`

关键文件：

- `Mod.cs`

关键引用点：

- `NPC.populateRoutesFromLocationToLocationList()` 用于刷新原版 location route。
- `npc.Schedule = npc.getSchedule(Game1.dayOfMonth)` 重建 NPC 当日日程。
- `npc.checkSchedule(Game1.timeOfDay)` 让新 schedule 立即生效。

借鉴点：

- 自定义地图/NPC 路径失败时，先考虑 route/schedule 缓存未刷新，而不是直接重写寻路。
- bridge 应提供 route cache 刷新或诊断入口。

不照搬边界：

- `populateRoutesFromLocationToLocationList` 属于旧版本路径；1.6 中应优先验证 `WarpPathfindingCache`。
- 不把“全 NPC schedule 修复”作为 Hermes v1 目标。

### NPCMaker-CS

本地路径：

`参考项目/Mod参考/Stardew-GitHub-TamKungZ-NPCMaker-CS`

关键文件：

- `MainWindow.axaml.cs`

关键引用点：

- schedule UI 存储 `season|timeRange|location|x|y|z|direction`。
- 生成 `Characters/schedules/{NpcInternalName}` 的 schedule entry。

借鉴点：

- 地点、坐标、朝向最终应能落到 schedule 数据格式。

不照搬边界：

- 它是内容包生成器，不是运行时寻路或 bridge 执行参考。
- 只作为 schedule 数据格式和 UI 表达参考。

## Critic 后修订的硬合同

本节把执行者容易自行补设计的部分钉死，避免后续实现时重新猜测。

### route probe 返回面

选项：

1. 把 route probe 放进 `TaskMove` 的 `MoveAcceptedData`。
2. 把 route probe 放进 `TaskStatusData` / C# `GameCommandStatus` 状态面。
3. 两边都放。

推荐：选项 2。

理由：

- `TaskMove` 只是入队接口，入队时尚未在游戏主线程解析 `NPC.currentLocation`、`Game1.getLocationFromName(...)`、warp route 和 tile path。
- `PumpOneTick()` / `task_status` 才拥有真实世界上下文，能稳定返回当前地图、目标地图、route、next segment 或结构化失败。
- 避免 `MoveAcceptedData` 携带“尚未实际探测”的伪结果。

实现合同：

- Bridge DTO：在 `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs` 增加 `RouteProbeData`、`RouteProbeSegmentData`，并在 `TaskStatusData` 增加 `RouteProbe`。
- Hermes DTO：在 `src/games/stardew/StardewBridgeDtos.cs` 增加 `StardewRouteProbeData`、`StardewRouteProbeSegmentData`，并在 `StardewTaskStatusData` 增加 `RouteProbe`。
- Core 状态：在 `src/game/core/GameAction.cs` 的 `GameCommandStatus` 增加 `RouteProbe`。
- Service 映射：`src/games/stardew/StardewCommandService.cs` 的 `ToCommandStatus(...)` 必须映射 route probe。
- 第一竖切只要求 task status / `PumpOneTick()` 暴露 route probe；`TaskMove` 仍只返回 accepted/commandId。

### route probe 数据结构

```json
{
  "mode": "route_probe",
  "status": "route_found|location_not_found|route_not_found|warp_point_not_found|target_tile_unreachable|cross_location_execution_not_enabled",
  "currentLocationName": "Town",
  "currentTile": { "x": 80, "y": 93 },
  "targetLocationName": "Beach",
  "targetTile": { "x": 20, "y": 35 },
  "route": ["Town", "Beach"],
  "nextSegment": {
    "locationName": "Town",
    "standTile": { "x": 80, "y": 94 },
    "targetKind": "warp_to_next_location",
    "nextLocationName": "Beach"
  },
  "failureCode": null,
  "failureDetail": null
}
```

字段约束：

- `mode` 固定为 `route_probe`。
- `status=route_found` 时必须有 `route` 和 `nextSegment`。
- 失败时必须设置稳定 `failureCode`，不能只写自然语言。
- 第一竖切不允许把 `route_found` 解释为自然移动完成；命令状态可以是 `blocked`，`errorCode` 推荐为 `cross_location_execution_not_enabled`，但必须附带 `routeProbe.status=route_found` 与下一段建议。

### `target.source` 日志落点

选项：

1. 把 `target.source` 拼进 `Result` 文本。
2. 扩 `NpcRuntimeLogRecord` 字段。

推荐：选项 2。

理由：

- `target.source` 是坐标来源审计字段，不是执行结果文本。
- 单独字段可被测试、日志查询和后续 UI 过滤稳定读取。
- 不会污染 `result` 的现有语义。

实现合同：

- 在 `src/runtime/NpcRuntimeLogWriter.cs` 的 `NpcRuntimeLogRecord` 增加 `TargetSource`。
- 在 `NpcLocalExecutorResult` 增加 `TargetSource`，mechanical target 路径填入 parent intent 的 `target.source`。
- `NpcAutonomyLoop` 写 local executor 结果日志时必须把 `TargetSource` 写入 runtime log。
- `destinationId` 路径不需要 `TargetSource`。

### host deterministic runner 合同

机械 target move 的执行分支必须满足：

- `NpcLocalExecutorRunner.ExecuteAsync(...)` 发现 `intent.Action == Move && intent.Target != null` 后，完全跳过 `_chatClient.StreamAsync(...)`。
- 工具选择固定为 `stardew_navigate_to_tile`。
- 参数只来自 parent intent：`locationName/x/y/facingDirection/reason/thought`，不读取 local model 输出。
- `source` 是宿主审计字段，只进入 `NpcLocalExecutorResult.TargetSource` / `NpcRuntimeLogRecord.TargetSource`，不进入 `stardew_navigate_to_tile` schema 或 bridge payload。
- `NpcLocalExecutorResult.Target == "stardew_navigate_to_tile"`。
- `NpcLocalExecutorResult.DecisionResponse == "local_executor_completed:stardew_navigate_to_tile"`。
- `NpcLocalExecutorResult.ExecutorMode == "host_deterministic"`。
- `NpcLocalExecutorResult.TargetSource == intent.Target.Source`。
- 如果工具不存在，返回 blocked：`required_tool_unavailable:stardew_navigate_to_tile`。

`destinationId` move 仍保持旧合同：

- 只暴露 `stardew_move`。
- 继续调用 delegation local model。
- `executorMode=model_called`。
- 不暴露 `stardew_navigate_to_tile`。

### prompt 验收合同

必须同步更新两处 prompt / prompt supplement：

- `src/runtime/NpcAutonomyLoop.cs` 的 parent decision message 不再写死“move 必须 destinationId”，而是写成：`move` 必须提供 `destinationId` 或完整 `target(locationName,x,y,source)` 二选一；`target` 必须来自已披露地图 skill。
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs` 不再只描述 `stardew_move(destination, reason)` / `destinationId`，必须说明机械目标会交给 executor-only `stardew_navigate_to_tile`，主 agent 不直接调用低层工具。

测试必须断言这些 prompt 文本不再只锁定 `destinationId` 单一路径。

## 推荐方案

推荐新增一个 executor-only 的机械导航入口，而不是改宽现有 `stardew_move(destinationId)`。

原因：

1. 现有 `stardew_move` 的安全性来自 `destinationId` 语义注册表，直接加坐标会破坏既有测试和 prompt 边界。
2. 主 agent 不应直接看到机械坐标工具，否则它会重新进入低层工具参数推理。
3. 本地小模型能力弱，也不应加载地图 skill；机械坐标路径不应让它再拼一次 tool args。
4. bridge 底层已经有 `target` 分支，可作为最小接入点；真正缺的是 executor contract、宿主确定性参数保护、tool schema、跨地图 route 探针和执行策略。

建议命名：

- `stardew_navigate_to_tile`
- 或 `stardew_move_to_tile`

推荐命名：`stardew_navigate_to_tile`。

理由：

- `move` 容易和现有 `stardew_move(destinationId)` 混淆。
- `navigate` 暗示它可能包含跨地图 route、分段执行和失败诊断。
- `to_tile` 明确它是机械目标，不是语义 destination。

## 新合同

### Parent intent contract

主模型仍不直接调用工具，而是输出短合同。新增可选机械目标字段：

```json
{
  "action": "move|observe|wait|task_status|escalate",
  "reason": "short reason",
  "destinationId": "optional semantic destination",
  "target": {
    "locationName": "Beach",
    "x": 20,
    "y": 35,
    "facingDirection": 2,
    "source": "map-skill:stardew.locations.beach.v1"
  },
  "allowedActions": ["move", "observe", "wait", "task_status"],
  "escalate": false
}
```

规则：

1. `destinationId` 优先走现有 `stardew_move`。
2. `target.locationName/x/y` 只进入 executor-only 机械导航入口。
3. 主模型必须说明 `target.source`，用于追踪坐标来自哪一层地图 skill。
4. 机械 target 路径由宿主确定性构造工具参数；不得让本地模型修改 `locationName/x/y`。
5. 如果目标缺坐标、地图名不明确、地图 skill 未披露，则升级回主模型，不让本地小模型补猜。

### Local executor tool schema

`stardew_navigate_to_tile` 仅本地执行层可见。机械 target 路径由宿主直接调用该工具，并记录为 `executorMode=host_deterministic`；`destinationId` 路径继续走现有 `model_called` 本地执行器。

参数：

```json
{
  "locationName": "Beach",
  "x": 20,
  "y": 35,
  "facingDirection": 2,
  "reason": "Go to the beach because the NPC wants to wait near the shoreline.",
  "thought": "optional short overhead bubble"
}
```

限制：

1. 必须有 `locationName`、`x`、`y`。
2. `locationName/x/y` 必须来自 parent intent，不允许本地模型改写。
3. 该工具不进入主 agent 默认工具面。
4. 该工具不替代 `destinationId` 语义导航；它只用于地图 skill 已经披露明确坐标的情况。

## Bridge 探针设计

### 阶段 1：只做跨地图 route 诊断

输入：

- NPC 当前地图与 tile。
- 目标地图与 tile。

输出：

```json
{
  "mode": "route_probe",
  "currentLocationName": "Town",
  "targetLocationName": "Beach",
  "targetTile": { "x": 20, "y": 35 },
  "route": ["Town", "Beach"],
  "nextSegment": {
    "locationName": "Town",
    "standTile": { "x": 80, "y": 94 },
    "targetKind": "warp_to_next_location",
    "nextLocationName": "Beach"
  },
  "status": "route_found"
}
```

失败输出必须结构化，例如：

- `location_not_found`
- `route_not_found`
- `warp_point_not_found`
- `target_tile_unreachable`
- `route_cache_stale`
- `cross_location_execution_not_enabled`

验收目标：

- 能判断当前地图到目标地图是否存在 route。
- 能找出当前地图应先走向哪个 warp/door tile。
- 不能直接宣称“已经自然跨地图移动成功”。
- route probe 结果通过 `TaskStatusData.RouteProbe` / `GameCommandStatus.RouteProbe` 返回，不通过 `TaskMove` accepted response 返回。

### 阶段 2：执行第一段同地图移动

如果 route 找到，bridge 只执行当前地图内到下一 warp tile 的段：

1. route 为同地图：复用现有同地图移动逻辑，目标为最终 target tile。
2. route 为跨地图：当前命令目标先替换成当前地图内通向下一地图的 warp tile。
3. 到达 warp tile 后，观察是否自然切图。
4. 切图后生成新 segment，继续下一段。

阶段 2 可以在 v1 后半段实现；最小探针可以先只到阶段 1。

### 阶段 3：完整分段导航

完整版本应维护 command phase：

- `resolving_route`
- `executing_segment`
- `awaiting_warp`
- `replanning_after_warp`
- `arriving`
- `completed`
- `blocked`
- `failed`

每段必须写 runtime log 和 SMAPI bridge log。

## Skill 分层披露设计

地图 skill 不能一次披露全世界坐标。建议分层：

1. `stardew.navigation.index`
   - 只列地点类别和可用子 skill。
   - 例如：Town、Beach、Mountain、Forest、Farm、Homes、Shops。
2. `stardew.navigation.region.town`
   - 只列 Town 内的关键区域、出口、常用站位。
3. `stardew.navigation.region.beach`
   - 只列 Beach 内的关键区域、出口、常用站位。
4. `stardew.navigation.poi.beach.shoreline`
   - 只列海边等待、钓鱼、散步相关坐标。
5. `stardew.navigation.poi.town.square`
   - 只列广场、公告栏、长椅等坐标。

主模型流程：

1. 根据“去海边”加载 index。
2. index 指向 `region.beach`。
3. 如需要更细，加载 `poi.beach.shoreline`。
4. 主模型选择目标 `Beach (x,y)` 和原因。
5. parent intent 把 `target.source` 写成具体 skill id。

本地执行层流程：

1. 不加载任何地图 skill。
2. 只收到 `locationName/x/y/facingDirection/reason`。
3. 由宿主按 parent intent 直接构造 `stardew_navigate_to_tile` 参数。
4. 本地小模型不参与机械坐标参数重写。

## 方案选项

### 选项 A：先做合同 + 宿主确定性执行 + route probe，再做分段执行

内容：

- 新增 executor-only 机械导航合同。
- `move` 合同改成 `destinationId` 或 `target` 二选一。
- 机械 target 由宿主直接调用 `stardew_navigate_to_tile`，不让本地小模型改写坐标。
- bridge 同步实现跨地图 route probe。
- route probe 成功后，只返回下一段建议，不急着自动跨图执行。

理由：

- 风险最低。
- 能快速验证地图 route、warp tile、目标 tile 是否足够可靠。
- 不会把 `warpCharacter` 包装成伪自然移动。

缺点：

- 第一版看起来不像完整“走到海边”，只能证明跨地图 route 可被计算。

推荐程度：最高。

### 选项 B：直接做完整分段跨图移动

内容：

- route probe、第一段移动、等待 warp、切图后续段一次实现。

理由：

- 用户可见效果更接近目标。

缺点：

- 容易同时踩 route、warp、schedule controller、状态机、重试、日志多个坑。
- 如果失败，很难判断是 route 错、warp 未触发、controller 被打断，还是坐标不可达。

推荐程度：不推荐作为下一步。

### 选项 C：用临时 schedule 注入验证

内容：

- 仿照原版 schedule 数据，给 NPC 临时构造目标地图+坐标日程，再触发 schedule path。

理由：

- 最贴近原版 NPC 机制。
- 可能绕过部分手写 route 状态机。

缺点：

- 更容易影响 NPC 当日日程和人格连续性。
- 需要小心恢复原 schedule。
- 对 Hermes 动态意图合同来说过重。

推荐程度：作为第二阶段对照实验，不作为第一步。

### 选项 D：`Game1.warpCharacter` fallback

内容：

- 无法自然 route 时直接瞬移 NPC 到目标地图+坐标。

理由：

- 实现简单。
- 可用于调试、解卡、失败兜底。

缺点：

- 不是自然移动。
- 如果当作成功，会掩盖真正寻路问题。

推荐程度：只允许作为显式 fallback，不计入自然导航成功。

## 明确推荐

下一步采用选项 A：

`destinationId 继续走 stardew_move(model_called)；target 走 host_deterministic stardew_navigate_to_tile -> bridge route probe -> 返回 route/nextSegment/失败原因 -> 保持现有同地图 move 不变`

推荐理由：

1. 符合“本地小模型弱推理”的现实：它只执行明确目标，不规划地图。
2. 符合“地图 skill 分层披露”的上下文策略：主模型选择坐标，本地执行层不看地图 skill。
3. 不污染现有 `stardew_move(destinationId)`。
4. 先验证原版/参考 mod 证明的 route 模型，避免一口气做复杂状态机。
5. 可观测性强，失败也能沉淀为 route 诊断数据。

## 最小实现范围

新增或修改范围应控制在：

1. `src/runtime/NpcLocalActionIntent.cs`
   - 允许 `move` intent 携带 `target.locationName/x/y/facingDirection/source`。
   - 仍保留 `destinationId` 路径。
2. `src/runtime/NpcLocalExecutorRunner.cs`
   - `destinationId` move 仍走 `stardew_move(model_called)`。
   - 机械 target move 完全跳过本地模型，宿主确定性调用 `stardew_navigate_to_tile(host_deterministic)`。
   - 返回 `TargetSource`，用于 runtime log 审计。
3. `src/games/stardew/StardewNpcTools.cs`
   - 新增 executor-only tool。
   - 更新 `CreateLocalExecutorTools` 和 fingerprint。
   - 不把该 tool 加入主 agent 默认工具面，除非未来明确批准。
4. `src/games/stardew/StardewCommandService.cs`
   - 如已有 target 分支足够，尽量复用。
   - 映射 `StardewTaskStatusData.RouteProbe` 到 `GameCommandStatus.RouteProbe`。
5. `Mods/StardewHermesBridge/Bridge/*`
   - 新增 route probe 数据结构。
   - 跨地图 move 不再只写 `cross_location_unsupported`，而是生成 `RouteProbe`。
   - 第一竖切可保持命令 blocked，但必须提供 route/nextSegment 或结构化失败。
6. 测试：
   - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
   - `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
   - bridge 测试项目中新增 route probe 单元测试。

## 验收标准

### Contract 验收

- 主 agent 默认工具面仍不暴露机械坐标导航工具。
- 本地 executor 工具面包含 `stardew_navigate_to_tile`。
- `destinationId` move 仍走现有 `stardew_move`。
- `target.locationName/x/y` move 走 `stardew_navigate_to_tile`。
- 本地 executor 不允许改写 parent intent 中的坐标。

### Bridge 验收

- 同地图目标不退化，仍能用现有路径探测执行。
- 跨地图目标不再只有 `cross_location_unsupported`，而是至少能返回 route probe 结果或结构化失败。
- route probe 能返回下一段 `standTile` 和 `nextLocationName`。
- `location_not_found`、`route_not_found`、`warp_point_not_found` 可区分。
- `Game1.warpCharacter` 不计入 route probe 成功。

### 日志验收

runtime log / bridge log 至少包含：

- parent intent id
- target source skill id
- selected tool
- current location/tile
- target location/tile
- route probe result
- failure code 或 next segment

字段要求：

- runtime log 使用 `targetSource` 字段承载 skill id。
- bridge/task status 使用 `routeProbe` 字段承载 route 诊断。
- 不允许只把 `target.source` 或 route probe 编码进 `result` / `blockedReason` 自由文本。

### Skill 验收

- 地图 skill 不一次性披露全地图坐标。
- 至少有 index、region、poi 三层草案。
- prompt / skill 验收必须覆盖 `skills/gaming/stardew-navigation/SKILL.md` 与 `skills/gaming/stardew-world/SKILL.md`，不能只检查 C# prompt 文本。
- parent intent 必须包含 `target.source`。

## 不做事项

1. 不做完整多 NPC 交通系统。
2. 不做全局 tile A*。
3. 不让本地小模型加载地图 skill。
4. 不把 `stardew_move(destinationId)` 改造成万能工具。
5. 不把 `Game1.warpCharacter` 当自然移动成功。
6. 不改写 NPC 长期 schedule 作为第一步。
7. 不引入新依赖。

## 风险

1. Stardew 1.6 的 `WarpPathfindingCache` 可见性和签名需要实际编译验证，不能只按反编译旧版本实现。
2. 跨地图 route 找到不等于每段 tile path 一定可达。
3. 到达 warp tile 后是否自然切图可能受 NPC controller、地图状态、时间、事件、位置偏移影响。
4. 分层地图 skill 如果坐标过旧，route probe 会把 skill 数据问题暴露成目标不可达。
5. executor-only 工具如果误暴露给主 agent，会破坏上下文和成本目标。

## 后续路线

1. 写 route probe 设计与测试。
2. 实现 executor-only `stardew_navigate_to_tile` schema 和宿主确定性调用。
3. 同一竖切实现 bridge route probe，至少替换纯 `cross_location_unsupported` 为结构化 route/probe 结果。
4. bridge 先返回 route/nextSegment，不自动完整跨图。
5. 基于探针日志决定是否进入分段执行。
6. 再评估是否做临时 schedule 注入对照实验。
