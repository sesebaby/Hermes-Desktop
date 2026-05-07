# 测试规范：Stardew NPC 跨地图导航最小探针

## 单元测试

### `NpcLocalActionIntentTests.TryParse_MoveWithTarget_AcceptsMechanicalTarget`

给定：

```json
{
  "action": "move",
  "reason": "go to beach",
  "target": {
    "locationName": "Beach",
    "x": 20,
    "y": 35,
    "facingDirection": 2,
    "source": "map-skill:stardew.navigation.poi.beach.shoreline"
  }
}
```

断言：

- parse 成功。
- `intent.Target.LocationName == "Beach"`。
- `intent.Target.Tile.X == 20`。
- `intent.Target.Tile.Y == 35`。
- `intent.Target.Source` 保留。

### `NpcLocalActionIntentTests.TryParse_MoveWithoutDestinationOrTarget_Fails`

给定 `action=move` 且没有 `destinationId` / `target`。

断言错误为固定值，例如 `move_target_required`。

### `NpcLocalActionIntentTests.TryParse_MoveWithIncompleteTarget_Fails`

覆盖：

- 缺 `locationName`。
- 缺 `x`。
- 缺 `y`。
- 缺 `source`。

断言不会让本地模型猜坐标。

### `NpcLocalExecutorRunnerTests.ExecuteAsync_WithDestinationMove_ExposesOnlyStardewMove`

给定 `move` + `destinationId`。

断言：

- 只暴露 `stardew_move`。
- 不暴露 `stardew_navigate_to_tile`。

### `NpcLocalExecutorRunnerTests.ExecuteAsync_WithMechanicalTargetMove_HostDeterministicExecutesNavigateToTile`

给定 `move` + `target.locationName/x/y/source`。

断言：

- 不调用 delegation stream。
- 宿主直接调用 `stardew_navigate_to_tile`。
- `executorMode=host_deterministic`。
- 工具参数的 `locationName/x/y/facingDirection/reason/thought` 完全来自 parent intent。
- `source` 不是工具参数；只作为审计字段进入 `result.TargetSource` 和 runtime log `targetSource`。
- 不暴露或调用 `stardew_move`。
- `result.Target == "stardew_navigate_to_tile"`。
- `result.DecisionResponse == "local_executor_completed:stardew_navigate_to_tile"`。
- `result.TargetSource == "map-skill:stardew.navigation.poi.beach.shoreline"`。

### `NpcLocalExecutorRunnerTests.ExecuteAsync_WithMechanicalTargetMove_DoesNotAllowModelToRewriteTarget`

断言：

- 即使 fake chat client 会返回不同坐标，也不会被调用。
- 执行工具收到的仍是 parent intent 中的 `target.locationName/x/y/source`。

### `StardewNpcToolFactoryTests.CreateLocalExecutorTools_IncludesNavigateToTile`

断言 local executor tool surface 包含：

- `stardew_status`
- `stardew_move`
- `stardew_navigate_to_tile`
- `stardew_task_status`

并断言不包含：

- `stardew_speak`
- `stardew_open_private_chat`
- gift/trade/memory/agent/todo 类工具

### `StardewNavigateToTileToolTests.ExecuteAsync_SubmitsTargetMove`

使用 fake `IGameCommandService` 捕获提交的 `GameAction`。

给定：

- `locationName=Beach`
- `x=20`
- `y=35`
- `facingDirection=2`

断言：

- `GameAction.Type == Move`。
- `GameAction.Target.LocationName == "Beach"`。
- `GameAction.Target.Tile == (20,35)`。
- payload 包含 `facingDirection`。
- payload 不包含 `destinationId`。

## Bridge 测试（第一竖切）

### `BridgeCommandContractTests.TaskStatusData_ExposesRouteProbe`

断言：

- Bridge `TaskStatusData` 有 `RouteProbe` 字段。
- `RouteProbeData` 有 `Mode`、`Status`、`CurrentLocationName`、`CurrentTile`、`TargetLocationName`、`TargetTile`、`Route`、`NextSegment`、`FailureCode`、`FailureDetail`。
- `RouteProbeSegmentData` 有 `LocationName`、`StandTile`、`TargetKind`、`NextLocationName`。

### `StardewCommandContractTests.TaskStatusData_MapsRouteProbeToGameCommandStatus`

断言：

- `StardewTaskStatusData` 有 `RouteProbe` 字段。
- `GameCommandStatus` 有 `RouteProbe` 字段。
- `StardewCommandService.ToCommandStatus(...)` 能把 `routeProbe.status`、`route`、`nextSegment.standTile` 映射出来。

### `BridgeCommandQueueTests.PumpMoveCommand_CrossLocation_ReturnsRouteProbe`

给定 NPC 在 `Town`，目标为 `Beach`。

断言：

- 不只返回 `cross_location_unsupported`。
- 返回 `route_found` 或结构化失败。
- 成功时包含 `route`、`nextSegment.locationName`、`nextSegment.standTile`。
- route probe 必须出现在 `TaskStatusData.RouteProbe`，不是 `MoveAcceptedData`。

### `BridgeCommandQueueTests.PumpMoveCommand_CrossLocation_DoesNotWarpAsSuccess`

断言：

- route probe 成功不调用 `Game1.warpCharacter` 作为完成条件。
- `Game1.warpCharacter` 只允许在显式 fallback 分支中出现。

### `BridgeCommandQueueTests.PumpMoveCommand_CrossLocation_MissingRoute_ReturnsRouteNotFound`

给定不可达地图。

断言错误可区分为：

- `location_not_found`
- 或 `route_not_found`
- 或 `warp_point_not_found`

## 集成测试

### `NpcAutonomyLoopTests.RunOneTickAsync_WithMechanicalMoveIntent_UsesLocalNavigateTool`

父层返回 mechanical target move。

断言：

- 父层工具面仍为 0。
- local executor 以 `executorMode=host_deterministic` 调用 `stardew_navigate_to_tile`。
- runtime log 包含 `target.source`。
- delegation stream 调用次数为 0。
- runtime log 使用 `targetSource` 字段承载 skill id，不只把 skill id 拼进 `result`。

### `NpcAutonomyLoopTests.RunOneTickAsync_WithDestinationMoveIntent_StillUsesStardewMove`

父层返回旧 `destinationId` move。

断言：

- 行为保持原样。
- 不触发 `stardew_navigate_to_tile`。

### `NpcAutonomyLoopTests.BuildDecisionMessage_AllowsDestinationIdOrMechanicalTarget`

断言：

- parent decision prompt 明确允许 `destinationId` 或 `target(locationName,x,y,source)` 二选一。
- prompt 不再写成“移动必须 destinationId”的单一路径。
- prompt 明确 `target` 必须来自已披露地图 skill。

### `StardewNavigationSkillTests.NavigationAndWorldSkills_DiscloseMechanicalTargetInLayers`

断言：

- `skills/gaming/stardew-navigation/SKILL.md` 描述 `destinationId` / mechanical `target(locationName,x,y,source)` 二选一。
- `skills/gaming/stardew-world/SKILL.md` 只披露地点索引或区域入口，不一次性披露全地图坐标。
- 具体 POI 坐标必须通过更细粒度 reference / child skill 披露，并要求 parent intent 写入 `target.source`。

### `StardewAutonomyTickDebugServiceTests.SystemPrompt_DescribesMechanicalTargetAsExecutorOnly`

断言：

- Stardew prompt supplement 仍保留 `stardew_move(destinationId)` 语义路径。
- Stardew prompt supplement 提到 mechanical `target(locationName,x,y,source)` 由 executor-only `stardew_navigate_to_tile` 执行。
- 主 agent 默认工具说明中不把 `stardew_navigate_to_tile` 当成主模型可调用工具。

## 手工验证

1. 配置 delegation 本地模型。
2. 让主模型通过地图 skill 选择一个同地图机械坐标。
3. 检查本地 executor 是否调用 `stardew_navigate_to_tile`。
4. 检查 SMAPI bridge 是否收到 `target.locationName/tile`。
5. 再选择一个跨地图目标。
6. 第一竖切后，跨地图目标必须返回 route probe 或结构化失败，不能只有 `cross_location_unsupported`。
7. 如果 route probe 还不能找到 route，失败码必须可区分 `location_not_found`、`route_not_found` 或 `warp_point_not_found`。

## 验证命令

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalActionIntentTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalExecutorRunnerTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests"
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```
