# Stardew NPC 移动执行架构设计

日期：2026-05-04  
状态：已在 brainstorming 中确认方向，待用户审阅  
范围：`stardew_move`、Stardew Bridge DTO、状态投影、Bridge 执行器边界

## 1. 设计结论

本次重构以 **可靠到达** 为最高优先级，采用以下固定边界：

1. `stardew_move` 只表达 **去哪个目的地**，不再表达坐标级执行细节。
2. move 一旦开始，**Bridge 独占执行控制权直到终态**；LLM 不再在途中补步、补坐标、补重规划。
3. Bridge 只维护 **destination registry** 这份可执行真相；世界语义解释由 Query / skill 投影层承担。
4. **玩家靠近不是 interrupted 条件**；临时挡路属于执行层重规划问题，不得让 move 因 proximity 自停。

一句话总结：

- **LLM 选目标。**
- **Query 层翻译世界。**
- **Registry 定义可执行目标。**
- **Executor 保证到达或明确失败。**

---

## 2. 当前实现的关键问题

### 2.1 Agent 名义上是目的地级，实际上仍然提交坐标

当前 `StardewMoveTool` 已要求 LLM 从 `destination[n].label` 里复制目标，但工具内部会重新观察、把 label 解析回 `(locationName, x, y)`，再构造坐标目标提交给 Bridge，见：

- `src/games/stardew/StardewNpcTools.cs:102-135`
- `src/games/stardew/StardewNpcTools.cs:188-247`

这意味着真正的执行契约仍是坐标级，而不是稳定的目的地标识。

### 2.2 Query 层把执行真相降解成字符串，再让 Agent 反向解析

`StardewQueryService.BuildStatusFacts()` 会把 destination 展平成 `destination[n]=label=...,locationName=...,x=...,y=...` 这样的字符串事实，见：

- `src/games/stardew/StardewQueryService.cs:95-157`

这导致执行标识在 Query 阶段丢失，Agent 只能靠字符串匹配再拼回执行参数。

### 2.3 Bridge 混杂了语义建模、候选生成、探测和执行职责

当前 `BridgeHttpHost` 同时负责：

- 生成 `nearbyTiles`
- 生成 `destinations`
- 在 `BuildPlaceCandidateDefinitions()` 中直接硬编码语义点及其标签/理由

见：

- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs:316-347`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs:350-470`

例如 `Bedroom mirror`、`Town fountain`、`Living room` 这些语义点目前直接定义在 Bridge HTTP 层，执行真相与世界解释耦合在一起。

### 2.4 执行器仍然保留与目标架构冲突的行为

`BridgeCommandQueue.PumpMoveCommand()` 当前仍然存在以下与目标架构冲突的行为：

- 跨地图移动直接 `cross_location_unsupported` 阻断，见 `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:370-375`
- 使用 `npc.setTilePosition()` 逐格瞬移，见 `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:468-483`
- 玩家靠近触发 interrupt，见 `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:419-424` 与 `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:527-549`

这三点分别破坏了跨图执行、自然移动和可靠到达。

---

## 3. 参考实现证据

### 3.1 TheStardewSquad 证明了“接管控制权 + 自己驱动移动”是可行的

`PrepareForRecruitment()` 会清空 `controller`、`temporaryController`、`DirectionsToNewLocation`，并关闭 `IsWalkingInSquare`，见：

- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/Wrappers/SquadMateStateHelper.cs:54-67`

这说明把 NPC 从原版调度手里短时接管出来，是成熟模式，不是这次设计独有的猜想。

### 3.2 TheStardewSquad 证明了“桥接层逐帧驱动移动”可替代瞬移

`ExecutePathMovement()` 使用：

- `npc.faceDirection(...)`
- `Utility.getVelocityTowardPoint(...)`
- `npc.Position += velocity`
- `npc.animateInFacingDirection(...)`

见：

- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/FollowerManager.cs:821-889`

这证明 Bridge 完全可以保留自己的执行循环，同时把步进方式替换成更接近原生的移动动画，而不必依赖 `setTilePosition()`。

### 3.3 TheStardewSquad 证明了“目标格不可站时，先找可达邻居”是正确处理方式

`FindClosestPassableNeighbor()` 会按邻格筛选、按距离排序，并通过 `ScopedTerrainFeatureRemoval` 避免地形特征误判，见：

- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Pathfinding/AStarPathfinder.cs:102-150`

这与本项目已落地的 `BridgeMovementPathProbe.FindClosestPassableNeighbor()` 方向一致，说明“arrival resolve 先于执行”是可靠做法。

### 3.4 HermesCraft 证明了“高层只做一次 goto 决策，底层 pathfinder 执行”是稳定边界

`hermescraft-main` 的 `goto({ x, y, z })` 由高层只提交目标，随后交给 pathfinder 执行、超时控制、失败返回，见：

- `external/hermescraft-main/bot/server.js:1272-1285`

这与本设计的核心一致：高层只做目的地级决策，执行器负责到达、超时和失败回报。

---

## 4. 目标架构

### 4.1 四层分工

#### A. Agent / Skill 层
职责：

- 根据观察事实决定“为什么去”和“去哪个 destinationId”
- 调用 `stardew_move(destinationId, reason)`

不负责：

- 坐标解析
- 邻居回退
- 路径规划
- 跨图切换
- 步进重规划

#### B. Query / Projection 层
职责：

- 读取 Bridge 返回的可执行 destination 视图
- 将其投影成 LLM 可理解的观察事实
- 负责 label / why / tags / availability 这类解释信息

不负责：

- 改写执行真相
- 拼装坐标提交参数

#### C. Bridge Destination Registry 层
职责：

- 维护本项目唯一承认的可执行目的地真相
- 定义每个目的地如何到达、何时可达、到达后如何站位

不负责：

- 为 LLM 讲故事
- 决定 NPC 该不该去那里

#### D. Bridge Executor 层
职责：

- 在 move 启动后独占控制权直到终态
- 执行 resolve / preflight / route plan / segment execute / replan / transition / arrival
- 以稳定错误码和阶段状态回报结果

不负责：

- 在途中向 LLM 要新坐标
- 因玩家靠近而自停

---

## 5. destination registry 设计

### 5.1 目的地标识

执行真相必须从 `label` 改为稳定 `destinationId`，例如：

- `haley_house.bedroom_mirror`
- `haley_house.living_room`
- `haley_house.front_door`
- `town.fountain`

`label` 可以改文案，但 `destinationId` 是唯一执行主键。

### 5.2 每个 registry entry 的最小字段

每个目的地至少包含：

- `destinationId`
- `locationName`
- `anchorTile`
- `arrivalPolicy`
- `fallbackPolicy`
- `transitionPolicy`
- `availabilityProbe`

### 5.3 策略含义

#### arrivalPolicy
定义最终完成条件，而不是只看 anchor tile。当前范围只需要两种：

1. `StandOnAnchor`
2. `StandAdjacentAndFaceAnchor`

`Bedroom mirror` 属于第二种；`Living room` 多数属于第一种。

#### fallbackPolicy
定义 anchor tile 不可站时是否允许解析成替代站位。当前范围只需要：

- `None`
- `ClosestPassableNeighbor`

#### transitionPolicy
定义是否允许跨图，以及跨图时如何分段。当前范围只需要：

- `SameLocationOnly`
- `WarpThenRoute`

#### availabilityProbe
定义当前命令开始时是否允许把这个点放进可执行集合，例如：

- 世界事件是否锁住控制
- 地点是否存在
- 是否存在稳定入口

### 5.4 registry 的边界要求

registry 只保存执行真相，不再把下面这些字段作为主真相来源：

- `reason`
- `tags`
- NPC 偏好文案
- 对 LLM 的说服性描述

这些字段可以存在于 projection 层，但不应主导执行器内部判断。

---

## 6. move 状态机设计

### 6.1 状态定义

Bridge executor 统一使用如下状态机：

1. `queued`
2. `resolving_destination`
3. `preflight`
4. `resolving_arrival`
5. `planning_route`
6. `executing_segment`
7. `replanning`
8. `transitioning_location`
9. `arriving`
10. terminal：`completed | failed | interrupted | cancelled`

### 6.2 状态语义

#### queued
命令入队，等待成为当前 active move。

#### resolving_destination
按 `destinationId` 查 registry。查不到直接失败：`invalid_destination_id`。

#### preflight
检查：

- 世界是否 ready
- NPC 是否存在
- 当前是否被全局系统接管
- 目标 location 是否可解析

这一层只处理“能不能开始”。

#### resolving_arrival
先把“最终站立点”解析出来，再做路线规划。

例如：

- anchor tile 是镜子所在格
- 如果镜子格不可站，则在这里就解析成“站在左侧一格并朝向镜子”

后续规划只面对一个已解析的、可执行的 stand tile，而不是把邻居回退散落在执行循环中当补丁。

#### planning_route
按当前 location 或跨图分段生成执行计划：

- 同图：当前 tile → stand tile
- 跨图：当前图出口段 → warp → 目标图入口段 → 目标 stand tile

#### executing_segment
按当前 segment 逐步执行，做步进安全检查、动画驱动和完成判断。

#### replanning
执行中若某步被临时阻挡，进入有限次重规划。

#### transitioning_location
执行跨图切换，再回到 `planning_route` 规划目标图内路径。

#### arriving
应用最终朝向、end behavior，并在满足 arrival policy 后完成命令。

### 6.3 中断语义

本次设计中：

- **玩家靠近不是 interrupted 条件**
- **玩家临时挡路不是 interrupted 条件**
- **短时受阻优先在执行层内做短等待后重试、replan 或绕行，而不是停机**

`interrupted` 只保留给真正的控制上下文失效，例如：

- 显式 cancel
- 原版系统或全局事件明确夺走执行上下文，导致当前命令无法继续推进

`player_approached` 不再是合法的 `interruptionReason`。

这保证 move 的默认行为是“尽量走完”，而不是“附近发生一点变化就自停”。

---

## 7. Query / Skill 投影设计

### 7.1 Bridge 返回原始可执行视图

Bridge 对 Query 返回的 destination 结构应至少包含：

- `destinationId`
- `locationName`
- `anchorTile`
- `arrivalPolicySummary`
- `currentlyAvailable`
- `availabilityReason`

这是机器真相，不是 prompt 文案。

### 7.2 Query 负责投影为 LLM 观察事实

Query 层再把它投影成面向 LLM 的事实，例如：

- `destination[0].id=haley_house.bedroom_mirror`
- `destination[0].label=Bedroom mirror`
- `destination[0].why=check her look before deciding whether to go out`
- `destination[0].availability=available`
- `destination[0].area=HaleyHouse`

坐标可以作为调试附带字段保留，但不再是 move 契约核心。

### 7.3 skill 层职责

- `stardew-world` 负责解释地点为何有意义、哪些 NPC 偏好它、何时适合去。
- `stardew-navigation` 负责解释如何使用 `stardew_move(destinationId, reason)`，以及失败后如何重新观察。

skill 层不再承载执行坐标真相，避免与 registry 双份漂移。

### 7.4 nearby 的处理

`nearbyTiles` 不再作为 `stardew_move` 的主要决策输入。

如果保留：

- 只能作为观察层的次级补充
- 不能继续承担“连续多步 nearby 模拟长距离移动”的职责

本设计的主路径是：**destinationId → executor 完整执行**。

---

## 8. 错误契约与状态回报

### 8.1 错误码分层

对上层暴露稳定、可行动的错误码；低层探测细节只进入日志和调试字段。

#### 契约层
- `invalid_destination_id`
- `destination_not_visible`
- `action_slot_busy`

#### 预检层
- `world_blocked`
- `npc_unavailable`
- `destination_unavailable`

#### 执行层
- `no_route`
- `step_blocked`
- `replan_exhausted`
- `transition_failed`
- `arrival_failed`

#### 中断层
- `interrupted(control_context_lost)`
- `cancelled`

`player_approached` 不属于中断层错误码。

像 `target_tile_open_false`、`step_tile_open_false` 这类底层探测细节，不应该直接成为 LLM 的一等错误契约。

### 8.2 提交回执与执行状态分离

提交成功只说明命令被受理，不说明已经走到终点。

#### 提交回执
至少包含：

- `accepted`
- `commandId`
- `destinationId`
- `initialPhase`

#### 执行状态
至少包含：

- `phase`
- `currentLocation`
- `resolvedStandTile`
- `replanAttempt`
- `lastErrorCode`
- `updatedAtUtc`
- terminal outcome

LLM 观察层只读取摘要，例如：

- `activeMove.destinationId=town.fountain`
- `activeMove.phase=transitioning_location`
- `lastMove.outcome=failed`
- `lastMove.errorCode=replan_exhausted`

这样它能理解 move 进度，但不会重新跌回坐标级微操。

---

## 9. DTO 与模块边界变更

### 9.1 Agent → Bridge move 契约

当前 `StardewMoveRequest` 仍以 `target.locationName + target.tile` 为核心，见：

- `src/games/stardew/StardewBridgeDtos.cs:56-63`

目标设计应改为以 `destinationId` 为核心，不再把执行坐标作为必填输入。

### 9.2 状态 DTO

当前 `StardewTaskStatusData` 已有 `interruptionReason`，但缺少显式阶段字段，见：

- `src/games/stardew/StardewBridgeDtos.cs:103-115`

目标设计应补齐：

- `phase`
- `destinationId`
- `resolvedStandTile`
- `replanAttempt`
- `currentLocation`
- `lastErrorCode`

若保留 `interruptionReason` 字段，其取值不得再包含 `player_approached`。

### 9.3 `StardewMoveTool`

当前 `StardewMoveTool` 的核心问题不是“有没有 destination 参数”，而是它仍然在工具内部做 `label -> 坐标` 解析，见：

- `src/games/stardew/StardewNpcTools.cs:188-247`

目标设计中：

- 工具只提交 `destinationId`
- 不再自行重查坐标
- 不再在 Agent 侧拼装 location/tile

### 9.4 `BridgeHttpHost`

当前 `BridgeHttpHost` 不应继续承担 `BuildPlaceCandidateDefinitions()` 这类语义硬编码职责，见：

- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs:366-470`

目标设计中：

- registry 提供可执行目的地
- projection 层生成人类可读解释
- `BridgeHttpHost` 只负责组织与返回，不再保存世界语义真相

---

## 10. 验证与验收

### 10.1 单元验证

#### Registry
- `destinationId` 唯一且稳定
- `arrivalPolicy` 与 `fallbackPolicy` 正确生效
- `transitionPolicy` 能区分同图/跨图目的地

#### Executor 状态机
- anchor tile 被占用时，先 resolve 为 stand tile 再规划
- 临时挡路时进入 `replanning`
- 超过预算时返回 `replan_exhausted`
- proximity 不触发 interrupted

#### Projection / Contract
- Query 投影包含 `destinationId`
- `stardew_move` 不再提交裸 `(location,x,y)`
- 错误码分层稳定，不直接把 probe failure 透出给 LLM

### 10.2 真实游戏验收

1. **单图家具遮挡**  
   海莉前往 `haley_house.bedroom_mirror`：镜子格不可站，但能站在相邻格并朝向镜子完成。

2. **单图稳定到达**  
   海莉从卧室到 `haley_house.living_room`：全过程不依赖 LLM 补步，稳定完成。

3. **跨图稳定到达**  
   海莉从 HaleyHouse 到 `town.fountain`：跨图切换与目标图内路径都由 executor 完成。

4. **玩家靠近不自停**  
   玩家在移动过程中靠近 NPC，不触发 interrupted；若暂时挡路，表现为执行层内短等待后继续或 replan，而不是终止命令。

5. **可诊断失败**  
   任意一次失败都能明确定位在：`resolving_destination / preflight / resolving_arrival / planning_route / executing_segment / transitioning_location / arriving` 之一。

---

## 11. 实施边界建议

为了让实现与架构一致，推荐将代码职责收敛为：

- `StardewNpcTools`：只负责提交 `destinationId` 契约
- `StardewQueryService`：只负责 projection
- 新的 `DestinationRegistry`：维护目的地执行真相
- `BridgeCommandQueue` 或新的 executor 组件：维护 move 状态机
- `BridgeMovementPathProbe`：保留为路径/站位探测支撑模块，而不是 move 总调度器

本次重构的关键，不是继续在现有 `PumpMoveCommand()` 上叠补丁，而是把 registry、projection、executor 三个概念真正拆开。

---

## 12. 最终决策

本设计选择：

- **方案 A：执行型 Bridge + destination registry**

原因：

1. 它最符合“可靠到达优先”的用户目标。
2. 它能把“目的地语义”与“移动执行”彻底拆开，避免继续把世界知识写进 Bridge HTTP 层。
3. 它把失败收敛成有限状态机，后续无论修单图、跨图、邻居回退还是动画，都能在统一执行模型下迭代。

本设计明确拒绝以下方向：

- 继续让 Agent 侧把 label 解析回坐标再提交
- 继续把 `nearbyTiles` 作为长距离移动主路径
- 继续在 Bridge 中同时维护语义文案与执行真相
- 因玩家靠近而 interrupt 正在执行的 move

这次重构的目标不是“让 move 成功率高一点”，而是建立一套 **即使失败也可诊断、即使扩展也不混层** 的 NPC 移动执行架构。
