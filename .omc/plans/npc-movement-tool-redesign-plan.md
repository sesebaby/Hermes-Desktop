# NPC移动工具契约重构 — 实施计划 (v2，经Architect/Critic审查)

## 元数据
- 基于规格: `.omc/specs/deep-interview-npc-movement-tool-redesign.md`
- 歧义度: 13%
- 生成时间: 2026-05-04
- 修订: v2 (Architect APPROVE + Critic ITERATE → 已吸收全部反馈)
- 类型: brownfield

---

## RALPLAN-DR 摘要

### 原则 (5条)
1. **目的地级决策** — LLM 决定"去哪里"，桥接层负责"怎么去"
2. **复用而非重造** — 用游戏内置 `PathFindController.findPathForNPCSchedules()` 计算路径；桥接层自己执行步进循环
3. **日程是事实，不是命令** — 日程暴露给 LLM 作为可选参考，LLM 有权不跟
4. **工具契约定义意图，不定义执行** — stardew_move 收目的地标签而非坐标；桥接层不设 `npc.controller`（避免与游戏所有权战争）
5. **小步可验证** — 每轮改动都能在真实游戏中独立验证

### 决策驱动力 (Top 3)
1. **解决根因而非症状** — LLM 做逐格决策是根因，只扩展候选或只改执行层都治标不治本
2. **对齐参考项目架构** — hermescraft-main 的 goto 模式 + TheStardewSquad 的控制权接管模式
3. **利用已有游戏基础设施** — 星露谷自带 A*（路径计算用）、跨地图 warp、日程系统

### 可行选项

| 选项 | 描述 | 优点 | 缺点 |
|------|------|------|------|
| **A: 改工具契约+桥接层（选择）** | stardew_move 从坐标改为目的地标签；桥接层用 PathFindController.findPathForNPCSchedules() **仅计算路径**，保留步进执行循环（不设 npc.controller） | 解决根因，对齐架构原则，利用游戏内置A*，避免控制权战争 | 改动面大，涉及 Agent 侧工具+Bridge 侧执行+skill 文档+DTO |
| B: 只扩展候选+跨地图 | 保持坐标级工具不变，在观察中加更多远距离候选 | 改动最小 | 不解决根因——LLM 仍在做坐标级决策，仍需从坐标推理语义 |
| C: 移植 TheStardewSquad 全套 | 移植 A* + FollowerManager + 编队系统 | A*实现参考价值高，控制权接管模式成熟 | 不改决策层（仍是坐标级）；TheStardewSquad 为编队/跟随设计，比 Hermes 需要的"临时借道"更重；重复造轮子（游戏已有 A*） |
| D: 混合模式（已并入A） | PathFindController 算路径 + 桥接层执行 + 不设 npc.controller | 兼具A的架构优势和BC的安全执行 | 已在v2中吸收为A的执行策略 |

### 预检
- 非高风险改动：不改数据库、不涉及认证、不涉及 PII、不改公共 API

---

## ADR (架构决策记录)

### 决策: 将 NPC 移动工具从坐标级重构为目的地级，桥接层用 PathFindController 仅做路径计算，保留桥接层步进执行循环，不设置 npc.controller

**驱动力:**
- 当前 LLM 逐格选坐标导致 NPC 一次只能移动 1-2 格
- 星露谷内置 `PathFindController.findPathForNPCSchedules()` 可做 A* 路径计算
- 直接设置 `npc.controller` 会导致与游戏日程系统的所有权战争（Architect 确认的高概率风险）
- hermescraft-main 的 goto 模式已验证"LLM 选目的地 → 引擎执行"架构
- TheStardewSquad 的 `MaintainControl()` 模式已验证"桥接层每 tick 清掉 controller 引用然后自己驱动"是可行的

**考虑的替代方案:**
- 方案 B（只扩展候选）— 不改决策层，LLM 仍需从坐标推理语义，治标不治本
- 方案 C（移植 TheStardewSquad 全套）— 是为编队/跟随玩家设计，控制权模型"完全接管"比 Hermes 需要的"临时借道"更重；且不改决策层
- 方案 D（完全委托 PathFindController）— 通过 `npc.controller = new PathFindController(...)` 委托执行，存在已确认的高概率控制权争夺风险（游戏 `NPC.update` 每帧运行、`performTenMinuteUpdate` 触发日程检查、`GameLocation` 有独立 NPC 移动逻辑），被 Architected 否决

**为何选择 A（含混合执行模式）:**
- 解决根本矛盾（LLM 在做执行决策而非意图决策）
- 复用游戏 A* 做路径计算，但不委托执行——避免控制权战争
- 桥接层保留步进控制权，可在每个步进 tick 中检查中断条件
- 对齐 TheStardewSquad `ExecutePathMovement()` 的已验证模式

**后果:**
- 需要修改工具契约（`stardew_move` 参数从坐标改为目的地标签）
- 需要重写桥接层候选生成（从近邻坐标改为目的地列表+日程条目）
- 桥接层步进执行循环保留但将 `setTilePosition` 替换为游戏原生步行动画
- 需要取消 cross_location_unsupported 封锁并实现跨地图 warp
- 需要新增中断通道机制

### 决策: 目的地 tile 被家具遮挡时，桥接层自动搜索相邻可通行 tile 作为实际站立位置

**驱动力:**
- 真实游戏验证发现：HaleyHouse 的 "Bedroom mirror" (6,4) 和 "Living room" (10,12) 两个目的地 tile 均被家具占据，`location.isTileLocationOpen()` 返回 false
- 手工微调每个坐标不可扩展——游戏中存在大量家具/物体布局，每个存档也可能不同
- Agent 选择的是语义目的地（"照镜子"），站旁边面朝镜子和站镜子上面朝镜子行为等价
- TheStardewSquad 已有成熟的 `FindClosestPassableNeighbor` 模式处理同类问题

**考虑的替代方案:**
- 手工微调坐标 — 不可扩展，每个地图/存档都要调整
- 扩大搜索半径（距离 2-3）— 可能站在与目的地语义无关的位置，且偏离 TheStardewSquad 的简洁模式
- 直接取消家具碰撞 — 破坏游戏真实性

**为何选择邻居回退（距离=1）:**
- 对齐 TheStardewSquad 验证过的模式
- 距离 1 保证 NPC 仍紧邻目的地，语义不漂移
- `ScopedTerrainFeatureRemoval` 防止大体积地形特征误挡探测
- 桥接层透明处理，Agent 无需感知

**后果:**
- `BridgeMovementPathProbe` 新增约 60 行（含 ScopedTerrainFeatureRemoval）
- `BridgeCommandQueue` 新增约 15 行邻居回退逻辑
- 目的地定义不需要修改（保持原始坐标，桥接层自动解析）

**后续事项:**
- 第三迭代引入日程感知后，需评估 LLM 是否能合理平衡"跟日程"vs"自由行动"
- 若 `findPathForNPCSchedules` 在高频调用下性能不足，可引入路径缓存

---

## DTO 契约变更清单

以下类型在两个进程间共享，必须同步更新：

### Bridge 侧 (`Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`)
| 类型 | 变更 |
|------|------|
| `NpcStatusData` | **新增** `IReadOnlyList<DestinationData>? Destinations`，**新增** `IReadOnlyList<MoveCandidateData>? NearbyTiles`，**弃用** `MoveCandidates`/`PlaceCandidates`（设为 null） |
| `DestinationData` | **新增 record**: `Label, LocationName, TileDto, IReadOnlyList<string> Tags, string Reason, int? FacingDirection, string? EndBehavior` |
| `MovePayload` / `MoveTarget` | **新增** `string? DestinationLabel` 字段（坐标字段保留兼容，destination label 优先） |
| `TaskStatusData` | **新增** `string? InterruptionReason` 字段（`interrupted` 状态时携带） |

### Agent 侧 (`src/games/stardew/`)
| 文件:类型 | 变更 |
|-----------|------|
| `StardewBridgeDtos.cs:StardewNpcStatusData` | 对等新增 `Destinations`、`NearbyTiles` 字段 |
| `StardewBridgeDtos.cs:StardewMoveRequest` | **新增** `string? DestinationLabel` |
| `StardewNpcTools.cs:StardewMoveToolParameters` | `LocationName/X/Y/FacingDirection` → `Destination (string), Reason (string)` |
| `StardewNpcToolSchemas.cs:Move()` | 参数 schema 从 `locationName/x/y` 改为 `destination` |
| `GameAction.cs:GameActionTarget` | 语义从 `("tile", locationName, GameTile)` 改为 `("destination", label, null)` |
| `StardewCommandService.cs` | `SubmitAsync` 中 Move action 的序列化：坐标从 `destination[n]` 查找表中解析 |
| `StardewQueryService.cs:BuildStatusFacts()` | 解析 `Destinations`/`NearbyTiles` 新字段并生成 `destination[n]`/`nearby[n]` 事实 |

### Skill 文档
| 文件 | 变更 |
|------|------|
| `stardew-navigation.md` | 重写移动流程；移除 moveCandidate 逐字约束；新增 destination 使用规则 |
| `stardew-world/SKILL.md` | 新增目的地标签 → 语义 → 坐标映射表 |

---

## 实施步骤

### 迭代 1: 单地图内语义目的地移动

#### 步骤 1.1: Bridge 侧 — 目的地候选生成
**文件:** `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
**改动:**
- 重构 `BuildMoveCandidates()` (L314-346): 不再生成 12 方向近邻偏移，改为调用已有的 `BuildPlaceCandidateDefinitions()` 构建目的地列表
- 重构 `BuildPlaceCandidates()` (L348-365): 目的地候选作为主要候选项返回到新字段 `Destinations`；保留少量近邻坐标到 `NearbyTiles`
- 新增 `DestinationData` record 到 `BridgeCommandModels.cs`
- 在 `NpcStatusData` 中新增 `Destinations` 和 `NearbyTiles` 字段
- **产出:** 海莉的 `stardew_status` 中出现 `destination[0]=label=Bedroom mirror,locationName=HaleyHouse,x=6,y=4,tags=home|photogenic`

#### 步骤 1.2a: Bridge 侧 — 路径计算（仅计算，不设 controller）
**文件:** `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
**改动:**
- **不**设置 `npc.controller`（避免与游戏日程系统的所有权战争）
- 复用已有的 `BridgeMovementPathProbe.FindSchedulePath()` (L456) —— 它已经在调用 `PathFindController.findPathForNPCSchedules()` 获取路径栈
- 将路径栈存储在 `BridgeMoveCommand` 中（已有 `ReplaceSchedulePath` / `NextScheduleStepFrom` 方法）
- 参考 TheStardewSquad `SquadMateStateHelper.PrepareForRecruitment()`: 在开始移动前清掉 `npc.controller`、`npc.temporaryController`、`npc.DirectionsToNewLocation`、`npc.IsWalkingInSquare`
- **产出:** 桥接层拥有路径栈，但不委托给游戏执行

#### 步骤 1.2b: Bridge 侧 — 路径执行（桥接层步进循环 + 游戏原生步行）
**文件:** `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
**改动:**
- 保留当前 `PumpMoveCommand()` 的步进循环结构（每 tick 走一步、步进延迟 `StepDelayTicks`、安全检查）
- 将 `npc.setTilePosition(nextTile.X, nextTile.Y)` (L434-435) 替换为游戏原生步行动画:
  - **首选**: 使用 `npc.MovePosition()` 或设置 `npc.Speed` + `npc.faceDirection()` + `npc.animateInFacingDirection()`（参考 `FollowerManager.ExecutePathMovement` L867-870）
  - **备选**: 若游戏原生 API 不够用，保留 `setTilePosition` 但增加步间平滑过渡
- 保留现有的 `BridgeMovementPathProbe.CheckRouteStepSafety()` 步进安全检查
- 保留 `MaxReplanAttempts = 2` 的重新规划逻辑
- **产出:** NPC 在桥接层的步进控制下以游戏原生方式走到目的地

#### 步骤 1.2c: Bridge 侧 — 中断通道
**文件:** `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
**改动:**
- 在 `PumpMoveCommand()` 的每个步进 tick 中，执行移动前检查中断条件:
  1. 玩家进入交互距离: `npc.currentLocation == Game1.player.currentLocation && Vector2.Distance(npc.Tile, Game1.player.Tile) < 4`
  2. 游戏事件激活: `Game1.eventUp`
  3. NPC 进入对话: `Game1.activeClickableMenu is DialogueBox`
- 触发中断时: 终止移动 → 记录 `interruption_reason`（`player_approached` / `event_active` / `dialogue_started`）→ 返回 `interrupted` 状态码
- `TaskStatusData` 新增 `InterruptionReason` 字段，LLM 在轮询 `stardew_task_status` 时可见
- 中断事实同时写入观察: `lastMoveInterrupted=player_approached`
- **产出:** NPC 移动途中玩家接近时，LLM 可以观察到中断并决定新动作

#### 步骤 1.2d: Bridge 侧 — 目的地邻居回退（家具遮挡处理）
**文件:** `Mods/StardewHermesBridge/Bridge/BridgeMovementPathProbe.cs`, `BridgeCommandQueue.cs`
**问题:** 目的地 tile 被家具/物体挡住时，`CheckTargetAffordance` 返回 `target_tile_open_false`，导致移动直接失败。例：HaleyHouse 的 "Bedroom mirror" (6,4) 和 "Living room" (10,12) 均被家具占据。
**参考:** TheStardewSquad `AStarPathfinder.FindClosestPassableNeighbor()` + `ScopedTerrainFeatureRemoval`

**改动:**
- `BridgeMovementPathProbe` 新增 `FindClosestPassableNeighbor(NPC, GameLocation, TileDto blockedTarget, TileDto currentTile)`:
  - 搜索 4 个相邻 tile（上下左右），每个候选预先计算朝向（面朝原始目的地）
  - 使用 `ScopedTerrainFeatureRemoval` 临时移除目标 tile 的地形特征后再探测（防止大树等大体积物体误挡相邻格探测）
  - 筛选可通行 + 可站立的候选，按距离排序，返回最近的 `(TileDto StandTile, int FacingDirection)`
  - 全部不可通行 → 返回 null
- `BridgeMovementPathProbe` 新增内部类 `ScopedTerrainFeatureRemoval : IDisposable`:
  - 构造时移除目标 tile 的 `TerrainFeature`，Dispose 时恢复
  - 完全照搬 TheStardewSquad 的同名模式
- `BridgeMoveCommand` 新增 `ReplaceTarget(TileDto resolvedTile, int resolvedFacing)`:
  - 保留原始目标坐标到 `_resolvedTargetTile`（用于日志）
  - 替换 `TargetTile` 为实际站立 tile，替换 `FacingDirection` 为计算出的朝向
- `BridgeCommandQueue.PumpMoveCommand()` 在 `queued` 阶段，`ProbeRoute` 返回 `target_tile_open_false` 时:
  - 调用 `FindClosestPassableNeighbor` → 成功则 `ReplaceTarget` + 重新 `ProbeRoute` + 继续执行
  - 失败则标记 `destination_unreachable`，返回给 Agent
  - 整个邻居回退对 Agent 完全透明

**产出:** NPC 能站在家具旁边（面朝家具），而非站在家具上方。Agent 无感知。

#### 步骤 1.3: Agent 侧 — 工具契约改为目的地标签
**文件:** `src/games/stardew/StardewNpcTools.cs`
**改动:**
- 修改 `StardewMoveTool` (L64-238):
  - 参数从 `locationName, x, y, reason, facingDirection` 改为 `destination (string 标签), reason (string)`
  - `destination` 参数来源于观察中的 `destination[n].label`
  - 验证逻辑: `destination` label 必须在最新观察的 `destination[n]` 列表中（**精确匹配，大小写敏感**）
  - 查找 `destination[n]` 条目获取坐标 → 构造 `GameAction` → 提交
- 修改 `StardewNpcToolFactory.CreateDefault()` (L11-38): 更新 `stardew_move` 工具定义
- **产出:** LLM 调用 `stardew_move(destination="Bedroom mirror", reason="I want to check my appearance")`

#### 步骤 1.4: Agent 侧 — 观察事实格式更新
**文件:** `src/games/stardew/StardewQueryService.cs`
**改动:**
- 修改 `BuildStatusFacts()` (L95-160):
  - `destination[n]` 格式: `destination[0]=label=Bedroom mirror,locationName=HaleyHouse,x=6,y=4,tags=home|photogenic|Haley,reason=a place Haley likes to check her appearance,facingDirection=2`
  - `nearby[n]` 格式: `nearby[0]=locationName=HaleyHouse,x=3,y=5,reason=safe_nearby_fallback`
  - **新增** `lastMoveInterrupted` 事实（当最近一次移动被中断时）
  - 弃用 `moveCandidate[n]` 和 `placeCandidate[n]` 旧格式
- **产出:** LLM 在观察中看到语义丰富的目的地列表

#### 步骤 1.5: Skill 文档更新
**文件:** `skills/gaming/stardew-navigation.md`
**改动:**
- 重写移动流程: "观察 destination[n] → 选匹配意图的目的地 → `stardew_move(destination, reason)` → 桥接层自主执行 → 轮询状态（含 interrupted）→ 被中断时重新观察"
- 移除"参数必须逐字来自 moveCandidate[n]"的约束
- 新增规则:
  - "目的地标签来自 `destination[n].label`，**精确匹配，不得编造**"
  - "**`nearby[n]` 仅在确定没有任何 `destination[n]` 匹配当前意图时使用，限 1-2 格重定位**。不得连续多步 nearby 来模拟长距离移动"
  - "移动途中若收到 `interrupted` 状态码，读取 `interruption_reason` 并决定是否重新观察或更换目标"
- 新增跨地图移动指引（为迭代 2 准备）
**文件:** `skills/gaming/stardew-world/SKILL.md`
**改动:**
- 确保 HaleyHouse、Town 等地图的目的地语义有完整解释
- 新增目的地标签含义映射表

#### 步骤 1.6: 自动化测试
**文件:** `Desktop/HermesDesktop.Tests/Stardew/` (新增或修改)
**改动:**
- 新增测试 `MoveTool_DestinationLevel_SendsCorrectAction`: mock `FakeSmapiClient`，LLM 调用 `stardew_move(destination="Bedroom mirror")` → 验证 `GameAction` 的 `Target.Label == "Bedroom mirror"` 且 `Target.LocationName == "HaleyHouse"`
- 新增测试 `MoveTool_RejectsInventedDestination`: 观察中无 "Kitchen" 目的地 → LLM 调用 `stardew_move(destination="Kitchen")` → 工具返回 `blocked:invalid_target`
- 新增测试 `MoveTool_PollsUntilCompleted`: mock bridge 返回 `running` → `running` → `completed` → 验证最终状态为 `completed`
- 新增测试 `QueryService_BuildsDestinationFacts`: 给定含 `Destinations` 的 `NpcStatusData` → 验证 `BuildStatusFacts` 输出正确的 `destination[n]` 格式
- 新增测试 `MoveTool_HandlesInterruptedStatus`: mock bridge 返回 `running` → `interrupted(player_approached)` → 验证工具返回 interrupted 状态且含 `interruption_reason`
- **产出:** CI 可运行，覆盖目的地级决策链路 + 中断处理

---

### 迭代 2: 跨地图移动（迭代 1 全部完成后再启动）

#### 步骤 2.1: Bridge 侧 — 取消跨地图封锁 + warp 支持
**文件:** `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
**改动:**
- 移除 `cross_location_unsupported` 封锁 (L365-369)
- 新增跨地图移动逻辑: 检测 `currentLocation != targetLocation`：
  1. 先走到当前地图的 warp 出口 tile
  2. 使用 `Game1.warpCharacter(npc, targetLocationName, targetEntryTile)` 传送
  3. 在新地图中重新计算路径到精确目的地
- 新增 `PerformCrossLocationWarp()` 方法
- **产出:** NPC 能跨地图走到目的地

#### 步骤 2.2: Bridge 侧 — 跨地图日志
**文件:** `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
**改动:**
- 跨地图移动关键节点日志: `cross_location_warp_start` → `cross_location_warp_complete` → `cross_location_pathfind_start` → `arrival`
- 日志含 `traceId`, `npcId`, `fromLocation`, `toLocation`, `warpTile`

#### 步骤 2.3: 真实游戏验证
- 启动 Stardew+SMAPI+Hermes
- 验证: 海莉从 HaleyHouse 走到 Town 喷泉 (x=47,y=56)
- 判定标准: 日志记录 `task_completed` 且从 `task_running` 到 `task_completed` 不超过 120 个游戏 tick
- 重试上限: 若 3 次尝试均未到达，此迭代视为未通过（需调查 warp 点或路径计算问题）

---

### 迭代 3: 日程感知（迭代 2 完成后再启动）

#### 步骤 3.1: Bridge 侧 — 读取并暴露日程
**文件:** `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
**改动:**
- 在 `BuildStatusResponse()` 中新增日程读取逻辑（参考 ScheduleViewer `Schedule.cs:232-248`）
- 检查 `npc.followSchedule && !npc.ignoreScheduleToday` 后读取 `npc.Schedule`
- 日程条目转换为 `schedule_entry[n]` 格式: `schedule_entry[0]=time=1000,locationName=Town,label=Town fountain,x=47,y=56,endBehavior=sit_down`

#### 步骤 3.2: Agent 侧 — 日程事实与目的地并列
**文件:** `src/games/stardew/StardewQueryService.cs`
**改动:**
- 在 `BuildStatusFacts()` 中新增 `schedule_entry[n]` 事实
- `schedule_entry` 与 `destination` 作为同等可选候选源
- 若 `ignoreScheduleToday` 为 true，暴露 `schedule_ignored=true`

---

## 依赖关系

```
迭代 1:
  1.1 (Bridge目的地DTO) ─┬─> 1.3 (Agent工具契约)
                          └─> 1.4 (Agent观察格式)
  1.2a (路径计算) ─> 独立，与 1.1 可并行
  1.2b (路径执行) ─> 依赖 1.2a
  1.2c (中断通道) ─> 依赖 1.2b
  1.2d (邻居回退) ─> 依赖 1.2a（使用 ProbeRoute 结果）
  1.3 + 1.4 ─> 1.5 (Skill文档)
  1.3 + 1.4 + 1.1 ─> 1.6 (测试)

迭代 2:
  1.1 + 1.2a + 1.2b 完成 ─> 2.1 (取消封锁+warp)
  2.1 ─> 2.2 (日志)
  2.1 + 2.2 ─> 2.3 (真实游戏验证)

迭代 3:
  2.1 完成 ─> 3.1 (Bridge日程读取)
  3.1 ─> 3.2 (Agent日程事实)
```

---

## 验收检查点

### 迭代 1 验收
- [ ] `BuildMoveCandidates` 不再生成 12 方向近邻坐标（代码审查：方法不存在或重构为目的地生成）
- [ ] `stardew_status` HTTP 响应 JSON 中 `destinations` 字段存在且 `moveCandidates` 为 null
- [ ] `stardew_move` 工具参数 schema 为 `destination` (string) 而非 `locationName/x/y`
- [ ] 桥接层步进循环中不再调用 `npc.setTilePosition`
- [ ] 桥接层在执行移动前**不**设置 `npc.controller`
- [ ] 真实游戏: 目的地 tile 被家具遮挡时，桥接层自动搜索相邻 tile 作为站立位置，NPC 面朝原始目的地
- [ ] 真实游戏: 海莉能成功走到 "Bedroom mirror" 旁边（站相邻 tile 面朝镜子），日志记录 `task_completed`
- [ ] 真实游戏: 海莉能成功走到 "Living room" 旁边（站相邻 tile），日志记录 `task_completed`
- [ ] 自动化测试: `MoveTool_DestinationLevel_SendsCorrectAction` — mock bridge 含 `destination[0]=(label="Bedroom mirror",...)` → LLM 调用 `stardew_move(destination="Bedroom mirror")` → `GameAction.Target.Label == "Bedroom mirror"` 
- [ ] 自动化测试: `MoveTool_RejectsInventedDestination` — LLM 调用不存在的目的地 → 返回 `blocked:invalid_target`
- [ ] 自动化测试: `MoveTool_PollsUntilCompleted` — mock bridge 返回 running×2 → completed → 最终状态为 completed
- [ ] 自动化测试: `MoveTool_HandlesInterruptedStatus` — mock bridge 返回 interrupted(player_approached) → 状态码含 `interruption_reason=player_approached`
- [ ] 真实游戏: 海莉在 HaleyHouse 内从 (6,4) 附近走到 (10,12) 附近（客厅区域），日志记录 `task_completed`，步数不超过 30，时间不超过 60 tick

### 迭代 2 验收
- [ ] `BridgeCommandQueue` 中不再有 `"cross_location_unsupported"` 字符串
- [ ] `Game1.warpCharacter()` 在跨地图场景中被调用（日志验证）
- [ ] 真实游戏: 海莉从 HaleyHouse 走到 Town 喷泉 (47,56)，日志记录 `task_completed`，从开始到完成不超过 120 tick，3 次内成功

### 迭代 3 验收
- [ ] `stardew_status` 响应含 `scheduleEntries` 字段（当 `npc.followSchedule && !npc.ignoreScheduleToday` 时）
- [ ] 自动化测试: 观察含 `schedule_entry[0]` → LLM 可选跟日程或自由行动（mock 场景验证两种路径）
- [ ] 真实游戏: 在游戏时间 24 小时内，日志记录海莉至少执行 1 次 schedule_entry 驱动的移动和 1 次自主 destination 驱动的移动

---

## 风险与缓解

| 风险 | 概率 | 缓解 |
|------|------|------|
| `findPathForNPCSchedules` 在非日程场景下行为不稳定 | 中 | 备选: TheStardewSquad `AStarPathfinder.cs` 的自定义 A* 实现（已有 351 行完整代码） |
| 游戏原生步行动画 API 不够灵活 | 中 | 备选: 保留 `setTilePosition` + 增加步间插值/延迟；迭代 2 后再优化动画 |
| 跨地图 warp 后 NPC 状态延迟更新 | 低 | warp 后等待 1 tick 再读 `npc.currentLocation`，日志记录 warp 前后状态 |
| LLM 编造不在候选中的目的地 | 中 | 工具边界硬校验：`destination` label 在 `_currentObservedDestinations` 中**精确匹配**（大小写敏感） |
| 中断通道过于敏感（频繁触发导致 NPC 走不动） | 中 | 距离阈值可配（默认 4 tile）；增加中断冷却（同一 NPC 5 秒内不重复触发） |

---

## 审查记录

| 轮次 | 审查者 | 裁决 | 关键反馈 |
|------|--------|------|----------|
| 1 | Architect | APPROVE + 合成 | PathFindController 仅用于路径计算，不设 controller；需中断通道 |
| 1 | Critic | ITERATE | 步骤 1.2 需拆分；DTO 清单缺失；验收标准需量化；nearby 语义需明确 |
| 2 | (待 Architect) | — | v2 已吸收全部反馈 |
