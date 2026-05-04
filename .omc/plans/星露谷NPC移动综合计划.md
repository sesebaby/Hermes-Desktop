# 星露谷 NPC 移动综合计划

## 先说结论

这次用第二份计划的架构骨架，用第一份计划和 TheStardewSquad 的移动实现补齐真实走路能力。

主线是：

1. `destinationId` 是移动命令的主身份。
2. Bridge 自己有目的地表，能只凭 `destinationId` 找到目标。
3. LLM 只选“去哪”，不再逐格选坐标。
4. Bridge 用 Stardew 的寻路结果算路线，但自己执行移动。
5. 自然动画行走优先参考 TheStardewSquad：每 tick 改 `npc.Position`、`faceDirection`、`animateInFacingDirection(...)`，不再用 `setTilePosition(...)` 瞬移。
6. 跨地图要做 transition 状态机，`warpCharacter(...)` 可以用，但只能作为 transition 的一步，不能直接假装完成。
7. 日程事实最后做，只作为观察事实，不替 NPC 决策。

## 证据优先级

这份计划按这个顺序判断对错：

1. 当前 Hermes 源码。
2. TheStardewSquad 的可运行参考实现。
3. 旧计划里的设计意图。
4. 历史错误记录。

历史错误记录不能当成绝对真理。它只提醒我们不要走捷径，比如不要用 `warpCharacter(...)` 直接假装跨地图完成，也不要把 `npc.controller = new PathFindController(...)` 当成万能修复。真要违反历史经验，必须有当前源码和真实游戏验证支撑。

## 两份旧计划怎么取舍

### 第一份计划保留什么

- 根因判断正确：LLM 不该逐格微操，应该选语义目的地。
- 保留 `PathFindController.findPathForNPCSchedules()` 做路径计算。
- 保留家具挡住 anchor 时站到相邻格并面朝 anchor。
- 保留跨地图和日程作为后续阶段。
- 保留真实游戏验收，不能只靠 mock 测试。

### 第一份计划降级什么

- `label` 不再是主身份，只是给 LLM 和人看的展示字段。
- `locationName/x/y` 不再是最终命令真相，只做兼容和调试。
- `player_approached` 不作为默认 interrupt。

### 第二份计划保留什么

- `destinationId` 作为稳定身份。
- `GameCommandResult` / `GameCommandStatus` 暴露 `destinationId`、`phase`、`currentLocationName`、`resolvedStandTile`、`routeRevision`。
- `interrupted` 是 runtime terminal，需要清 slot、pending work item、claim，并保留最近终态快照。
- 旧字段分阶段退役，不一次性硬删。

### 第二份计划修正什么

- 它写的“跨地图已完成”不符合源码，源码仍有 `cross_location_unsupported`。
- 它写的“`setTilePosition` 已替换”不符合源码，当前仍在逐格瞬移。
- 它写的“Bridge 按 `destinationId + registry` 执行”还没真正完成，现在 registry 还只是 `BridgeHttpHost` 里的内联候选。
- 它写的 claim 主键迁移也只完成了一半，当前 `ResourceClaimRegistry` 仍主要按 NPC 和 tile 冲突。

## 阶段 1：抽 Bridge 目的地表

### 目标

Bridge 不能再依赖 Agent 帮它反解 `locationName/x/y`。Bridge 自己要能只凭 `destinationId` 找到目的地。

### 要做

- 新增 `BridgeDestinationRegistry`。
- 把 `BridgeHttpHost.BuildPlaceCandidateDefinitions(...)` 里的目的地移进去。
- 每个目的地至少有：
  - `destinationId`
  - `label`
  - `locationName`
  - `anchorTile`
  - `tags`
  - `reason`
  - `facingDirection`
  - `endBehavior`
  - `arrivalPolicy`
  - `transitionPolicy`
- `BuildDestinations(...)` 从 registry 取当前 location 的候选。
- `BridgeCommandQueue.EnqueueMove(...)` 收到 `destinationId` 时，从 registry 解析 target。
- 旧 `Payload.Target` 只做 legacy fallback。

### 验收

- `target=null`、`destinationId=town.fountain` 的 move 能被 Bridge accepted。
- 未知 `destinationId` 返回 `invalid_destination_id`。
- status 仍能展示 `destination[n].destinationId`。

## 阶段 2：Agent 和 skill 以 `destinationId` 为主

### 目标

工具和提示都明确：优先复制 `destination[n].destinationId`，label 只是 fallback。

### 要做

- `stardew_move(destination=...)` 参数名暂时不改，但说明改成优先 `destinationId`。
- `StardewMoveTool` 保留观察验证，不允许编造目的地。
- `GameActionTarget.LocationName/Tile` 降级为兼容快照。
- 更新 `stardew-navigation.md` 和 `stardew-world/SKILL.md`。

### 验收

- 输入 `town.fountain` 提交 `Payload.destinationId=town.fountain`。
- 输入 `Town fountain` 仍能 fallback 到 `town.fountain`。
- 文档不再把 label 或坐标说成执行真相。

## 阶段 3：自然动画行走

### 目标

同地图移动不再瞬移，要能看到 NPC 连续走路。

### TheStardewSquad 参考点

参考文件：

- `TheStardewSquad/Framework/Wrappers/SquadMateStateHelper.cs`
- `TheStardewSquad/Framework/FollowerManager.cs`
- `TheStardewSquad/Pathfinding/AStarPathfinder.cs`

可采用的做法：

- 开始移动前清：
  - `npc.controller`
  - `npc.temporaryController`
  - `npc.DirectionsToNewLocation`
  - `npc.IsWalkingInSquare`
- 移动期间每 tick 维护控制权，避免游戏日程抢回 NPC。
- 路径节点转像素中心点：`tile * 64 + (32, 32)`。
- 算方向，调用 `npc.faceDirection(...)`。
- 用 `Utility.getVelocityTowardPoint(...)` 算速度。
- `npc.Position += velocity`。
- 调 `npc.animateInFacingDirection(Game1.currentGameTime)`。
- 到达当前节点后 pop path。
- 路径结束后 `npc.Halt()`，再按目的地要求面朝 anchor。

### 要做

- 删除 Bridge 主路径里的 `npc.setTilePosition(...)`。
- 保留当前 `PathFindController.findPathForNPCSchedules()` 作为路线来源。
- Bridge 仍然自己消费路径，不把完整执行长期交给 `npc.controller`。
- 每 tick 更新 status，并保留 `executing_segment` phase。
- 如果走到中途堵住，进入 `replanning` 并递增 `routeRevision`。

### 验收

- `BridgeCommandQueue.cs` 主执行路径不再出现 `npc.setTilePosition(...)`。
- 代码里出现 TheStardewSquad 风格的自然移动核心调用：`Utility.getVelocityTowardPoint(...)`、`npc.Position += ...`、`npc.animateInFacingDirection(...)`。
- HaleyHouse 内部移动真实游戏可见为走路，不是瞬移。
- 移动结束后 `npc.Halt()`，并按 `facingDirection` 面朝目标。

## 阶段 4：目的地 claim

### 目标

claim 绑定的是“这个命令要去哪个 destination”，不是某个可能变化的站位 tile。

### 要做

- `ResourceClaimRequest` 增加 `DestinationId` 或等价资源键。
- `WorldCoordinationService.TryClaimMove(...)` 接收 destination resource。
- move submit 前创建 `workItemId + destinationId` 占位 claim。
- accepted 后 rekey 到 `commandId`。
- terminal 后释放。
- `resolvedStandTile` 只做执行快照。

### 验收

- `Target.Tile=null` 也能 claim。
- replan 改变 `resolvedStandTile` 不会新增第二份 claim。
- 同一 NPC 未完成 move 时，新 move 被挡住。

## 阶段 5：跨地图 transition

### 目标

跨地图移动不能再返回 `cross_location_unsupported`，但也不能直接 warp 后假装完成。

### 要做

- 移除 `cross_location_unsupported`。
- registry 给跨地图目标配置 `transitionPolicy`。
- 先只做小范围：
  - `HaleyHouse -> Town`
  - `Town -> HaleyHouse`
- phase 流程：
  - `transitioning_location`
  - `cross_location_warp_complete`
  - `planning_route`
  - `executing_segment`
  - `arriving`
  - terminal
- `Game1.warpCharacter(...)` 只用于切换地图；warp 后还要重新寻路到目标站位。

### 验收

- `BridgeCommandQueue.cs` 不再有 `"cross_location_unsupported"`。
- status 能看到 `transitioning_location`。
- 真实游戏：Haley 从 HaleyHouse 到 Town fountain，至少 3 次内 2 次成功。
- 失败时返回 `transition_failed` 或 `route_failed`，不能卡 running。

## 阶段 6：最近终态观察

### 目标

移动结束后，下一轮观察能知道刚才发生了什么。

### 要做

- Query/debug 投影最近一次 `LastTerminalCommandStatus`。
- 事实包括：
  - `lastMoveStatus`
  - `lastMoveDestinationId`
  - `lastMovePhase`
  - `lastMoveErrorCode`
  - `lastMoveResolvedStandTile`
  - `lastMoveRouteRevision`

### 验收

- terminal 后 `ActionSlot` 和 `PendingWorkItem` 已清空。
- 下一轮观察仍能看到最近终态。
- `interrupted` 不自动重试，除非 Bridge 给 `RetryAfterUtc`。

## 阶段 7：日程事实

### 目标

把游戏日程作为候选事实给 LLM，看见但不强迫跟随。

### 要做

- status DTO 增加 `ScheduleEntries`。
- 读取 `npc.Schedule`。
- 暴露 `schedule_entry[n]`：
  - `destinationId`
  - `time`
  - `label`
  - `locationName`
  - `x/y`
  - `facingDirection`
  - `endBehavior`
  - `reason`

### 验收

- 观察事实出现 `schedule_entry[n]`。
- LLM 可以选择 schedule，也可以选择自由 destination。

## 实施顺序

这次先做前 3 个阶段：

1. Bridge 目的地表。
2. Agent/skill 以 `destinationId` 为主。
3. TheStardewSquad 风格自然动画行走。

跨地图、claim 深度改造、最近终态观察、日程事实作为后续阶段继续推进。

原因：当前最大用户可见问题是“NPC 不会像人一样走路”和“Bridge 不能真正 destinationId-only”。先把这两个基础打稳，再扩跨地图更稳。

## 最小验证命令

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter Stardew
dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug
```

真实游戏验证另做，自动化测试不能替代自然动画和跨地图行为。

## 待解决问题：move 完成后的 hold / 控制租约

### 现在的问题

Hermes 现在只在一个 `move` 命令运行期间接管 NPC。

也就是说，海莉从 A 点走到 B 点的时候，Bridge 会持续清掉原版控制器，自己让她自然走过去。可一旦日志出现 `task_completed`，Bridge 就松手了。松手以后，星露谷原版日程会继续接管她：该回家就回家，该去下一个日程点就去下一个日程点。

所以手测里看到的“海莉刚走完，又像被原版系统拉走”，不一定是 Hermes 又发了 move，更可能是 Hermes 完成命令后没有继续保留控制权。

### 不能直接怎么做

不能粗暴永久清掉海莉的原版日程控制。

如果一直清 `controller`、`temporaryController`、`DirectionsToNewLocation`，海莉可能就不再正常生活：不回家、不参加日程、不触发原版事件，甚至可能卡在某个地方。TheStardewSquad 是“招募队友”玩法，所以它可以长期持续 `MaintainControl`；Hermes 的 NPC 自主系统不能默认把所有 NPC 都变成永久队友。

### 后续要设计什么

需要做一个明确的 hold / 控制租约机制。

白话说，就是 Hermes 要能说清楚：

- 这次 move 完成以后，要不要让 NPC 先站住。
- 站住多久。
- 什么情况下提前释放给原版日程。
- 什么情况下继续由 Hermes 下一轮行动接管。
- 私聊、事件、菜单、天黑、换日这些情况怎么打断。

可以先做几种短租约：

- `hold_until_next_decision`：move 完成后先别被原版日程带走，等 Hermes 下一轮决定。
- `hold_for_seconds`：move 完成后站住几秒，再还给原版。
- `hold_during_private_chat`：私聊期间不让 NPC 被原版日程拖走。
- `release_to_vanilla`：Hermes 明确松手，让原版日程恢复。

### 验收标准

- move `completed` 后，短时间内 NPC 不会立刻被原版日程拉走，除非租约已经过期或被事件打断。
- 租约过期后，NPC 能正常回到原版日程，不会永久卡住。
- 打开菜单、事件开始、换日、返回标题时，租约必须清理。
- status 能看见当前是否被 Hermes hold，比如 `controlLease=held`、`leaseReason=hold_until_next_decision`。
- 日志要能区分“Hermes move 完成后 hold 住了”和“已经释放给原版日程”。
