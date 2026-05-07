# PRD：Stardew NPC 跨地图导航最小探针

## 目标

在不破坏现有 `stardew_move(destinationId)` 语义移动合同的前提下，为 Stardew NPC runtime 增加一个 executor-only 的机械坐标导航入口，并为后续 bridge 跨地图 route probe 留出明确合同。

本阶段目标是小步快跑：

1. 第一竖切：让 parent intent 可以携带已由地图 skill 披露的 `target.locationName/x/y/facingDirection/source`；机械 target 由宿主确定性调用新增 `stardew_navigate_to_tile`，不让本地小模型改写坐标；bridge 对跨地图目标返回 route probe / next segment / 结构化失败，不再只有 `cross_location_unsupported`。
2. 第二切片：基于 route probe 结果执行第一段同地图移动，并观察自然 warp。
3. 后续切片：再决定是否做完整分段执行或临时 schedule 注入对照实验。

## 用户需求

- 主模型根据分层地图 skill 决定地点和坐标。
- 地图 skill 必须分层披露，不能一次性塞全图。
- 本地小模型能力弱，不加载地图 skill，不做跨地图深推理。
- 机械坐标路径不经过本地小模型二次生成工具参数；宿主直接按 parent intent 调用受限工具。
- 方案必须显式写明参考 mod、借鉴点和不照搬边界。
- 不能用 `Game1.warpCharacter` 冒充自然移动成功。
- 小步实现，及时提交。

## 参考 mod

### Market Day

本地路径：`参考项目/Mod参考/Stardew-GitHub-ncarigon-CeruleanStardewMods/Market/MarketDay`

借鉴点：

- `Patches.cs` patch `PathFindController.findPathForNPCSchedules`、`NPC.parseMasterSchedule`、`NPC.getMasterScheduleEntry`。
- `Utility/Schedule.cs` 的 `PathfindToNextScheduleLocation(...)` 先获取 location route，再对每张地图调用局部 pathfinding。
- 中间地图通过 `getWarpPointTo(nextLocation)` 找到下一地图入口。

不照搬：

- 不复制 Market Day 的摊位、午餐、访问商店逻辑。
- 不在第一切片 patch `NPC.parseMasterSchedule`。

### BotFramework

本地路径：`参考项目/Mod参考/Stardew-GitHub-andyruwruw-BotFramework`

借鉴点：

- `WorldTour` / `WorldPath` 用 warps 建 `GameLocation` 图。
- `WorldParser` 将下一地图 warp tile 转成导航 action。

不照搬：

- 不搬整个 bot target/action/tour 框架。
- 不让它替代 Hermes NPC runtime 的人格与任务边界。

### CustomNPCFixes

本地路径：`参考项目/Mod参考/Stardew-GitHub-spacechase0-CustomNPCFixes`

借鉴点：

- route/schedule 失败时，先考虑刷新原版 route/schedule 缓存。
- `populateRoutesFromLocationToLocationList`、`getSchedule`、`checkSchedule` 体现“修原版链路优先于重写寻路”。

不照搬：

- 不把全 NPC schedule 修复做成本阶段目标。
- Stardew 1.6 需要优先验证 `WarpPathfindingCache`，不能照搬旧版本 API。

## 功能需求

### Parent intent contract

`move` intent 必须支持两种互斥路径：

- 语义路径：`destinationId`
- 机械路径：`target.locationName + target.x + target.y + optional facingDirection + source`

规则：

- `destinationId` 存在时，仍走现有 `stardew_move` 的 `model_called` 路径。
- `target` 存在时，走 executor-only `stardew_navigate_to_tile` 的宿主确定性路径，记录 `executorMode=host_deterministic`。
- `move` 至少需要 `destinationId` 或完整 `target` 之一。
- `target.source` 必须记录坐标来源 skill。
- 本地小模型不得改写 `target`；runner 必须直接使用 parent intent 中的 target 构造工具参数，或执行严格相等校验。
- mechanical target 分支必须完全跳过 `_chatClient.StreamAsync(...)`，固定返回 `Target=stardew_navigate_to_tile`、`DecisionResponse=local_executor_completed:stardew_navigate_to_tile`、`ExecutorMode=host_deterministic`、`TargetSource=target.source`。

### Local executor tool surface

新增工具：`stardew_navigate_to_tile`。

可见性：

- 只加入 `CreateLocalExecutorTools`。
- 不加入主 agent 默认工具面。
- 不替换现有 `stardew_move`。

参数：

- `locationName`
- `x`
- `y`
- `facingDirection`
- `reason`
- `thought`

审计字段：

- `source` 不进入 `stardew_navigate_to_tile` schema，也不进入 bridge payload。
- `source` 只作为 parent intent 的坐标来源审计字段，由 runner 写入 `NpcLocalExecutorResult.TargetSource`，再进入 `NpcRuntimeLogRecord.TargetSource`。

### Bridge route probe

第一竖切中，bridge 应对跨地图机械目标至少返回：

- 当前地图和 tile。
- 目标地图和 tile。
- 地图 route。
- 当前地图下一段的 `standTile`。
- 失败原因：`location_not_found`、`route_not_found`、`warp_point_not_found`、`target_tile_unreachable` 等。

返回面选择：

选项：

1. `TaskMove` accepted response 直接返回 route probe。
2. `TaskStatusData` / `GameCommandStatus` 状态面返回 route probe。
3. accepted response 和 status response 都返回。

推荐：选项 2。

理由：

- `TaskMove` 只负责入队，入队时没有稳定的游戏主线程世界上下文。
- `PumpOneTick()` / `task_status` 才能读取 NPC 当前地图、target location、route 和 warp tile。
- 避免 accepted response 里出现“还没真正 probe 的伪结果”。

实现合同：

- Bridge `TaskStatusData` 增加 `RouteProbe`。
- Hermes `StardewTaskStatusData` 增加 `RouteProbe`。
- Core `GameCommandStatus` 增加 `RouteProbe`。
- `StardewCommandService.ToCommandStatus(...)` 必须映射该字段。
- 第一竖切允许命令状态为 `blocked`，但 `RouteProbe` 必须给出 route/nextSegment 或结构化失败。

日志合同：

- `target.source` 不拼进自由文本，必须写入 `NpcRuntimeLogRecord.TargetSource`。
- `NpcLocalExecutorResult` 增加 `TargetSource` 并由 mechanical target 分支填入。
- `NpcAutonomyLoop` 写 local executor runtime log 时必须传递该字段。

## 非目标

- 不做完整多 NPC 交通系统。
- 不做全局 tile A*。
- 不让本地小模型加载地图 skill。
- 不把 `stardew_move(destinationId)` 改成万能工具。
- 不用 `Game1.warpCharacter` 伪装自然移动。
- 不引入新依赖。

## 验收标准

- `NpcLocalActionIntent` 能解析 `move` + `target`，并拒绝缺坐标或缺地图名的 target。
- `NpcLocalExecutorRunner` 对 `destinationId` move 仍只暴露 `stardew_move`。
- `NpcLocalExecutorRunner` 对 mechanical target move 不调用本地模型，直接宿主确定性调用 `stardew_navigate_to_tile`。
- `SerializeIntent` 对 mechanical move 包含 `target`，且包含 `source`。
- `CreateLocalExecutorTools` 包含 `stardew_navigate_to_tile`，主默认工具面不因本需求额外暴露机械工具。
- `stardew_navigate_to_tile` 通过现有 `StardewCommandService` target 分支提交 `locationName/tile`。
- 同地图移动行为不退化。
- 跨地图目标在第一竖切后不再只有 `cross_location_unsupported`，而是返回 route probe 或结构化失败。
- mechanical target 路径日志记录 `executorMode=host_deterministic`，并包含 `target.source`。
- `routeProbe` 通过 `TaskStatusData` / `GameCommandStatus` 可读取，成功时包含 `route`、`nextSegment.locationName`、`nextSegment.standTile`、`nextSegment.nextLocationName`。
- Prompt / skill 验收：`NpcAutonomyLoop`、`StardewNpcAutonomyPromptSupplementBuilder`、`skills/gaming/stardew-navigation/SKILL.md` 与 `skills/gaming/stardew-world/SKILL.md` 不再只描述 `destinationId` 单一路径，而是明确 `destinationId` / mechanical `target(locationName,x,y,source)` 二选一，并保持地图 skill 分层披露。

## ADR

决策：新增 executor-only 机械坐标工具，并让 mechanical target 路径由宿主确定性执行，而不是改宽现有 `stardew_move(destinationId)` 或让弱本地模型二次生成坐标参数。

驱动因素：

- 现有 `destinationId` 合同已经验证成功，应保持稳定。
- 机械坐标属于低层执行细节，不应暴露给主 agent。
- 本地小模型不能承担地图 skill 加载、路线推理或坐标参数重写。
- bridge 已经能接受 target payload，但跨地图探针必须同一竖切补上 route probe，否则 plumbing 不能独立证明跨地图导航。

备选方案：

- 直接改 `stardew_move` 支持坐标。拒绝：会污染现有语义工具和测试边界。
- 直接实现完整跨地图分段执行。拒绝：状态机风险过大，不利于小步提交。
- 让 local 小模型继续中转 `stardew_navigate_to_tile` 参数。拒绝：裸坐标没有 destination registry 保护，必须避免弱模型篡改坐标。
- 先做临时 schedule 注入。拒绝：更接近原版，但会影响 NPC 当日日程，适合作为后续对照实验。
- 只用 `Game1.warpCharacter`。拒绝：不是自然移动，只能作为显式 fallback。

影响：

- 本地执行层会有两个 move 工具，但按 intent 互斥选择。
- mechanical target 的 `executorMode` 与普通 model-called move 不同，需要日志和测试明确区分。
- 第一竖切会包含 route probe，但不要求一次完成跨地图自然移动。

后续事项：

- 实现 bridge route probe。
- 补地图 skill 三层草案：index、region、poi。
- 根据 probe 日志决定是否进入分段执行。
