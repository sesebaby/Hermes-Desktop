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

## 手工验证（用户视角）

本节不是开发者单元测试，而是我在本机真实打开 Hermes + Stardew + SMAPI 时应该怎么验证。验证目标是确认“主模型给出明确地图名和坐标后，本地执行层不再让弱模型改写坐标，bridge 能在真实游戏上下文里给跨地图 `routeProbe`”。本阶段不要求 NPC 真正自动过图。

### 我先准备环境

1. 我在仓库根目录打开 PowerShell。
2. 我确认本地代码已经包含当前计划对应提交，并且 bridge 测试已经通过；如果我刚换电脑，先执行：

   ```powershell
   git log --oneline -5
   dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
   ```

   预期：能看到最近的跨地图 route probe 相关提交；bridge 测试全部通过。

3. 我同步 Stardew NPC 配置：

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\sync-stardew-npc-config.ps1
   ```

4. 我启动 Hermes Desktop：

   ```powershell
   .\run-desktop.ps1 -Rebuild
   ```

5. 我启动 Stardew Valley 的 SMAPI 版本，进入一个存档，确保 StardewHermesBridge 已加载。
6. 我在 Hermes 的 Dashboard 或 Agent/NPC runtime 页面确认 Stardew bridge 处于已连接状态，且能看到当前存档 / NPC runtime 状态。

### 我怎么触发同地图机械坐标验证

1. 我先选一个和 NPC 当前所在地图相同的目标，避免跨图变量干扰。
2. 我在 Hermes 里让 NPC 去一个明确位置，例如“让当前 NPC 去当前地图附近某个明确坐标/地点”，并要求它使用已披露地图 skill 的坐标，不要让本地模型猜。
3. 我观察 NPC 是否按同地图移动逻辑开始移动。
4. 我打开 Hermes 日志：

   ```powershell
   Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\hermes.log" -Tail 200 -Wait
   ```

5. 我打开 SMAPI 日志：

   ```powershell
   Get-Content "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" -Tail 200 -Wait
   ```

同地图通过标准：

- Hermes runtime log 中能看到 `executorMode=host_deterministic`。
- local executor 选择的是 `stardew_navigate_to_tile`。
- 日志里能看到 `targetSource`，值类似 `map-skill:stardew.navigation...`。
- SMAPI bridge 收到的是 `target.locationName` + tile 坐标。
- 不应该看到本地 delegation 模型重新生成或改写 `locationName/x/y`。
- 不应该把 `target.source` 塞进 bridge payload；它只应该作为 Hermes runtime 审计字段出现。

### 我怎么触发跨地图 route probe 验证

推荐测试场景：让 NPC 当前在 `Town`，目标是 `Beach` 的一个明确坐标。原因是 Town -> Beach 是原版常见跨地图路线，最适合先证明 route probe。

1. 我把 NPC 放在或等待到 `Town`。
2. 我在 Hermes 里发起自然语言目标，例如：

   ```text
   让这个 NPC 去海边，不要瞬移。请使用地图 skill 选择 Beach 的明确坐标，并让本地执行层执行。
   ```

3. 如果 UI 支持选择具体 NPC，我选择一个当前在 `Town` 的 NPC。
4. 我等待一个 autonomy tick 或手动触发一次 NPC 行动。
5. 我查看 Hermes 主日志、SMAPI 日志和 NPC runtime 活动日志：

   ```powershell
   Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\hermes.log" -Tail 300 -Wait
   Get-Content "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" -Tail 300 -Wait
   Get-ChildItem "$env:LOCALAPPDATA\hermes\hermes-cs\runtime\stardew\games\stardew-valley" -Recurse -Filter runtime.jsonl | Sort-Object LastWriteTime -Descending | Select-Object -First 5
   ```

跨地图 probe 通过标准：

- `task_status` / `GameCommandStatus` 里出现 `routeProbe` 字段。
- `routeProbe.mode == "route_probe"`。
- 如果路线存在，`routeProbe.status == "route_found"`。
- `routeProbe.currentLocationName` 是 NPC 当前地图，例如 `Town`。
- `routeProbe.targetLocationName == "Beach"`。
- `routeProbe.route` 至少包含 `Town` 和 `Beach`。
- `routeProbe.nextSegment.targetKind == "warp_to_next_location"`。
- `routeProbe.nextSegment.nextLocationName == "Beach"`。
- `routeProbe.nextSegment.standTile` 有明确 `x/y`，表示 NPC 当前地图内下一步应走向的 warp/door tile。
- 命令当前可以仍是 blocked，`errorCode` 可以是 `cross_location_execution_not_enabled`；这在本阶段是允许的，因为本阶段只验证 route probe，不验证完整过图状态机。

跨地图不通过标准：

- 只有 `cross_location_unsupported`，没有 `routeProbe`。
- route probe 只出现在 `TaskMove` accepted response，而不是 `task_status` / `GameCommandStatus`。
- `routeProbe.status=route_found` 但没有 `route` 或没有 `nextSegment`。
- 系统调用 `Game1.warpCharacter` 并把瞬移当成自然移动成功。
- 本地小模型参与选择或改写 `Beach/x/y`。

### 如果失败，我怎么判断是哪类问题

- 看不到 `routeProbe`：优先怀疑 task status 映射或日志面没接通，检查 `TaskStatusData.RouteProbe`、`StardewTaskStatusData.RouteProbe`、`GameCommandStatus.RouteProbe`、`StardewCommandService.ToCommandStatus(...)`。
- `routeProbe.status=location_not_found`：优先检查地图 skill 给出的 `locationName` 是否是 Stardew 内部地图名，例如 `Beach`，而不是中文名或自然语言别名。
- `routeProbe.status=route_not_found`：优先检查原版 route cache / `WarpPathfindingCache` 是否能从当前地图到目标地图生成 route。
- `routeProbe.status=warp_point_not_found`：优先检查当前地图到下一地图的 warp/door 是否能通过 `getWarpPointTo(...)` 或等价 API 解析。
- `routeProbe.status=target_tile_unreachable`：说明地图 route 可能存在，但目标 tile 或下一段 stand tile 的同地图 path probe 不可达。
- NPC 完全没动但有 `routeProbe.route_found`：本阶段可能正常，因为第一竖切还不要求执行跨图第一段；下一步才验证是否执行到 warp tile。
- 日志没有 NPC runtime 记录：先检查 Hermes/Stardew bridge 连接、当前存档 runtime、NPC profile 是否启动，而不是先改寻路逻辑。

### 我这轮不应该验证什么

- 不验证完整 Town -> Beach 自动过图完成。
- 不验证多 NPC 同时跨地图。
- 不验证 schedule 注入。
- 不验证 `Game1.warpCharacter` fallback。
- 不要求本地小模型理解“海边在哪里”；主模型必须已经给出 `Beach/x/y/source`。

## 验证命令

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalActionIntentTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalExecutorRunnerTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests"
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```
