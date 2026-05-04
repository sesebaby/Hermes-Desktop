# Stardew NPC move 架构重构可执行实现计划

## RALPLAN-DR 摘要

### Principles
1. 高层命令只选择 `destinationId`，不再把 `locationName/x/y/tile` 作为最终执行真相上传。
2. move 一旦 accepted，Bridge 独占执行直到 `completed/failed/interrupted/cancelled` 终态。
3. Bridge 只维护 destination registry 与执行状态真相；语义标签、可读说明、候选投影由 Query/skill 负责。
4. 兼容迁移必须显式分阶段，避免公共命令契约、Bridge DTO、status 消费方在同一轮改动中失配。
5. `player_approached` 不再作为 interrupt 条件；临时挡路属于执行层重规划或失败语义。

### Decision Drivers
1. 提升 NPC 到达可靠性，消除 label -> 坐标反解和运行中被错误中断的问题。
2. 收紧公共命令契约、Bridge DTO、Query 投影三层边界，避免“外层说 destination、内层仍按 tile 执行”的伪抽象。
3. 在已有探路和测试基线上渐进演进，优先形成可验证、可回滚、可观测的闭环。

### Viable Options
- 方案 A：渐进式 registry-first 重构。先建立 registry、phase、claim 一致性，再逐步收口公共命令与 DTO 到纯 `destinationId`。优点是回归面可控、兼容期清晰；缺点是需要数个阶段维护双写/双读。
- 方案 B：温和中间态方案。保留公共 `GameActionTarget` 结构不立即删 `locationName/tile`，但把它们降级为兼容影子字段，先让 `Payload.destinationId` 与 Bridge status 成为主读路径，待消费方稳定后再收缩公共 target。优点是对上层调用方冲击更小；缺点是公共契约会有一段时间处于“语义主字段与影子字段并存”的过渡态。
- 方案 C：一次性重写 move pipeline。同步替换命令契约、Bridge DTO、状态机、跨图执行和动画步进。优点是最终形态最干净；缺点是联调面过大、回归难定位，不适合当前已有测试与在制分支状态。

建议采用方案 A，并吸收方案 B 的“公共契约缓冲”做法：先让内部真相和状态契约稳定，再退役旧执行读法。方案 C 不推荐。

## 计划上下文与范围修正

### 本次计划必须覆盖的关键边界
- `D:\GitHubPro\Hermes-Desktop\src\game\core\GameAction.cs`
- `D:\GitHubPro\Hermes-Desktop\src\game\core\GameAction.cs` 内的公共状态抽象：`GameCommandResult`、`GameCommandStatus`
- `D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewCommandService.cs`
- `D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewBridgeDtos.cs`
- `D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewQueryService.cs`
- `D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewNpcTools.cs`
- `D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeHttpHost.cs`
- `D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeCommandQueue.cs`
- `D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeMovementPathProbe.cs`
- `D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeMoveFailureMapper.cs`
- `D:\GitHubPro\Hermes-Desktop\Desktop\HermesDesktop.Tests\Stardew\*.cs`

### 范围修正原因
Critic 指出的缺口成立：当前公共命令契约与序列化入口仍以 `GameActionTarget.LocationName/Tile` 为中心，而 `StardewCommandService.SubmitAsync()` 也仍然以 `action.Target.LocationName + action.Target.Tile` 组装 `StardewMoveRequest`。同时，`GameAction.cs` 中的 `GameCommandResult` 与 `GameCommandStatus` 仍是公共提交回执/状态抽象；如果不把这一层显式纳入迁移，`destinationId/initialPhase` 只会停留在 Bridge accepted DTO，`destinationId/phase/currentLocationName/resolvedStandTile/routeRevision` 也只会停留在 Bridge status DTO、Query 投影或设计文档里，公共返回面仍然只能暴露旧 `Status/ErrorCode` 语义，Architect 指出的“公共返回面断口”会被原样保留。因此本计划不仅要迁移 `Payload.destinationId` 提交路径，还必须同步扩展 `GameCommandResult` / `GameCommandStatus`，把 move 的受理回执和执行状态正式收口到公共抽象层。

## 运行时一致性约束

### 1. claim 的绑定对象
本计划采用以下一致性规则：
- Desktop 侧 `WorldCoordinationService` 继续参与 Stardew move 的并发/冲突控制，不在本次设计中退役；退役 tile claim 会把 Bridge 外的 action slot 与跨工具互斥保护直接掏空，因此本计划明确选择“保留协调服务、迁移 claim 真相”。
- claim 的规范主键从 `locationName + tile` 迁移为语义身份键：提交前临时使用 `workItemId + destinationId` 占位，命令 accepted 后升级/重绑定为 `commandId + destinationId`；如果 `WorldCoordinationService` 无法原地 rekey，则允许使用等价实现（例如保留内部 claimId，但其冲突判定主身份必须基于 `commandId/workItemId + destinationId`，不能再基于 tile）。
- `resolvedStandTile` 不是 claim 主键，而是当前执行轮次的解析结果快照；它可以参与调试、可观测性与局部探测，但不能继续作为 Desktop claim 的身份依据。
- status 中需要区分：
  - 稳定身份：`commandId`、`destinationId`
  - 易变执行快照：`phase`、`currentLocationName`、`resolvedStandTile`、`routeRevision`
- `StardewNpcTools.TryClaimMove(...)` / `RequiresMoveClaim(...)` 对 Stardew move 继续保留，但判定条件与入参必须一起迁移：
  - `RequiresMoveClaim(...)` 改为“`action.Type == Move` 且 `Payload.destinationId` 非空”即触发，不再要求 `Target.LocationName` 与 `Target.Tile` 非空。
  - `TryClaimMove(...)` 或其等价协调入口改为接收 `destinationId` 主身份；若仍保留 tile 形态入参，也只能把 tile 作为调试快照或局部提示，不能作为冲突键。

这样做的原因是：跨图 transition、同图 replan、相邻落点回退都可能改变 `resolvedStandTile`，但不应改变“这条命令仍在前往哪个目的地”的身份；同时 Desktop runtime 仍需要 claim 来保护 action slot/pending work item 生命周期内的互斥与节流。

**可测试验收标准**
- 仅含 `Payload.destinationId` 的 `stardew_move` 在 `Target.LocationName/Tile` 为空时，仍会触发 Desktop move claim，而不会被 `RequiresMoveClaim(...)` 绕过。
- submit 前创建的是 `workItemId + destinationId` 占位 claim；该占位 claim 由 `StardewRuntimeActionController.PrepareActionAsync(...)` 创建并暂持，submit accepted 后必须由同一控制器的 `RecordSubmitResultAsync(...)` 负责升级/重绑定为 `commandId + destinationId` 主 claim；如果 submit 未 accepted、accepted 回写前抛异常，或后续进入 terminal，则由 `RecordSubmitResultAsync(...)` / `ClearAsync(...)` 按同一责任链回滚或释放占位 claim，不能把回滚责任留给 Bridge 或调用方猜测。
- 同一 `destinationId` 在 replan/transition 导致 `resolvedStandTile` 变化时，不会因为 tile 变化而丢失原 claim，也不会生成第二份并发 claim。
- Desktop 侧不存在再以 `action.Target.LocationName + action.Target.Tile` 作为 Stardew move claim 身份键的主路径；若兼容层仍读旧字段，只允许用于 legacy 适配日志，不得参与正式冲突判定。

### 2. replan / transition 与 claim 一致性
- replan 允许改变 `resolvedStandTile`，前提是 `destinationId` 不变，且新站位仍满足该 registry entry 的 `arrivalPolicy/fallbackPolicy`。
- 跨图 transition 允许把执行上下文从“当前 location 的 segment”切到“目标 location 的 segment”，但不能改写 claim 的 `destinationId`。
- 每次 stand tile 重新解析后，Bridge 都应递增 `routeRevision` 或等价版本号，并在 status 中暴露，供调试与测试判断“这是同一命令下的重规划，不是新 claim”。
- 只有以下事件允许 claim 终止或失效：显式 cancel、外部系统夺回控制权导致 interrupt、registry entry 失效且无法继续解析、命令进入任一 terminal phase。

### 3. phase 与 claim 的最小一致性要求
- 同一 `commandId` 在终态前不得切换 `destinationId`。
- 同一 `commandId` 允许多次进入 `planning_route/executing_segment/replanning/transitioning_location`，但这些 phase 变化都属于同一 claim 生命周期。
- `completed` 判定必须基于“满足当前 registry entry 的 arrivalPolicy”，而不是“曾经到过某个旧 stand tile”。

## 阶段兼容矩阵

| 阶段 | 写入字段 | 读取字段 | 双写责任 | 双读消费者 | 旧 `label/x/y` 执行读法状态 |
| --- | --- | --- | --- | --- | --- |
| 阶段 1 | 公共 `GameCommandResult` accepted 回执新增 `destinationId/initialPhase`；公共 `GameCommandStatus` 新增 `destinationId/phase/currentLocationName/resolvedStandTile/routeRevision`；内部 registry 解析 `resolvedStandTile` | 现有消费者继续读 legacy `status/errorCode/locationName/tile/label`；新测试开始读公共扩展字段 | `GameCommandResult` / `GameCommandStatus` 与 Bridge DTO 双写新旧字段 | `StardewCommandService`、`StardewQueryService`、调试/测试消费者双读 | 保留；仍可作为兼容执行输入 |
| 阶段 2 | `StardewNpcTools` 与 `StardewCommandService` 开始写 `Payload.destinationId` 主路径；兼容影子字段仍可写 | Bridge 和 Query 双读 `destinationId` 与旧 target 字段 | 工具层、命令服务层双写 | `StardewCommandService`、`BridgeHttpHost`、`StardewQueryService` 双读 | 保留但降级，不再推荐作为主执行读法 |
| 阶段 3 | move active state 固定写 `commandId/destinationId/phase/currentLocationName/routeRevision/resolvedStandTile`，并稳定映射到公共 `GameCommandStatus` | 执行器只按 `destinationId + registry` 驱动；兼容读仅留在入口适配层 | 执行状态对象双写兼容 status 字段 | status/query/debug 消费者双读 phase 与兼容字段 | 退役 Bridge 内部按 `label/x/y` 直接执行的读法 |
| 阶段 4 | 公共契约保留最小兼容字段或正式收缩；测试只写 `destinationId` | 业务消费者只读公共 `GameCommandStatus` 暴露的 `commandId/destinationId/phase/status/errorCode/currentLocationName/resolvedStandTile/routeRevision`；`locationName/tile/label` 仅调试可见或删除 | 仅对外兼容层保留必要双写 | 少量遗留调试面板若存在则受控双读 | 彻底退役；旧读法不得参与命令执行判定 |

### 兼容矩阵执行约束
- “双写”只允许作为迁移策略存在，不能让旧字段继续驱动新设计的真实执行判断。
- 退役点以阶段 3 为界：Bridge 执行器内部必须不再根据 `label/x/y` 直接决定落点；阶段 4 仅负责清理剩余消费者和测试。

## 执行进度（2026-05-04）

| 阶段 | 状态 | 关键完成项 |
|------|------|-----------|
| 阶段 1 | ✅ 完成 | `GameCommandResult(DestinationId/InitialPhase)`, `GameCommandStatus(Phase/...)`, `Payload.destinationId`, `StardewMoveRequest.destinationId`, Bridge `MovePayload/DestinationData/TaskStatusData` 扩展, Query `destinationId` 投影, Bridge destination ID 生成 |
| 阶段 2 | ✅ 完成 | `StardewCommandService` destination-first 提交, `MoveTool` 写 `destinationId` 到 payload, `RequiresMoveClaim` 按 `destinationId` 触发, claim `workId→cmdId` rekey, destination-only move Desktop claim, claim 释放回退链 `commandId→workItemId` |
| 阶段 3 | ✅ 完成 | ✅ `interrupted` 纳入终态, `player_approached` 从 `CheckInterrupt` 移除, phase/currentLocationName/routeRevision 回写, terminal 快照保留, `IsTerminalStatus` 扩到 `interrupted`, tool schema/description 优先 `destinationId` ✅ `cross_location_unsupported` 已移除 → `Game1.warpCharacter` 跨地图, `ignoreScheduleToday` 接管控制 ✅ `setTilePosition` → `PathFindController` 自然移动, replan 在 controller 异常结束时自动触发 ✅ failure mapper 升级为 phase-aware (`preflight_blocked`/`arrival_unresolvable`/`route_failed`/`path_unreachable`) ⚠️ `BridgeDestinationRegistry.cs` 未新增（destinations 定义在 `BridgeHttpHost.BuildPlaceCandidateDefinitions` 内联，够用）
| 阶段 4 | ❌ 待开始 | legacy 兼容层清理, 真实游戏验证 |

### 本轮改动文件清单
- `src/game/core/GameAction.cs` — `GameCommandResult`, `GameCommandStatus` 扩展
- `src/games/stardew/StardewCommandContracts.cs` — 新增 `Interrupted` 常量
- `src/games/stardew/StardewCommandService.cs` — destination-first 提交, rekey, 新字段映射
- `src/games/stardew/StardewBridgeDtos.cs` — `StardewMoveRequest`, `StardewDestinationData`, `StardewTaskStatusData` 扩展
- `src/games/stardew/StardewNpcTools.cs` — tool schema/description, `BuildDestinationId`, `RequiresMoveClaim`, claim 迁移, `interrupted` 终态, terminal 快照
- `src/games/stardew/StardewQueryService.cs` — `destinationId` 投影
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs` — claim 释放走 `commandId` 优先
- `src/runtime/WorldCoordinationService.cs` — `RekeyClaim`, `TryClaimMove` 支持 null tile
- `src/runtime/ResourceClaimRegistry.cs` — `Rekey` 方法
- `src/runtime/NpcRuntimeDescriptor.cs` — `NpcRuntimeControllerSnapshot.LastTerminalCommandStatus`
- `src/runtime/NpcRuntimeInstance.cs` — terminal 快照存取
- `src/runtime/NpcRuntimeDriver.cs` — `SetLastTerminalCommandStatusAsync`
- `src/runtime/NpcRuntimeStateStore.cs` — terminal 快照持久化
- `Mods/.../BridgeCommandModels.cs` — `MovePayload`, `DestinationData`, `TaskStatusData` 扩展
- `Mods/.../BridgeMoveCandidateSelector.cs` — `BridgePlaceCandidateDefinition.DestinationId`
- `Mods/.../BridgeHttpHost.cs` — 目的地 ID 生成
- `Mods/.../BridgeCommandQueue.cs` — `CheckInterrupt` 精简, phase 回写, `ToStatusData` 扩展, **跨地图 warp (P0)**, **PathFindController 替换 setTilePosition (P1)**, **preflight 错误码 phase-aware (P2)**
- `Mods/.../BridgeMoveFailureMapper.cs` — 错误码升级为 phase-aware (`arrival_unresolvable` / `route_failed` / `path_unreachable`)
- 测试文件 — `StardewCommandServiceTests`, `StardewCommandContractTests`, `StardewNpcToolFactoryTests`, `StardewQueryServiceTests`

---

## 实现阶段

### 阶段 1：建立 registry 真相、phase 语义与公共回执/状态承载位

**主要修改文件**
- 修改：`D:\GitHubPro\Hermes-Desktop\src\game\core\GameAction.cs`（扩展 `GameCommandResult`、`GameCommandStatus`）
- 修改：`D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewBridgeDtos.cs`
- 修改：`D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewQueryService.cs`
- 修改：`D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeHttpHost.cs`
- 建议新增：`D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeDestinationRegistry.cs`
- 建议新增：`D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeDestinationModels.cs`

**阶段目标**
1. 在 Bridge 内建立唯一 destination registry 真相，定义 `destinationId -> locationName/anchorTile/arrivalPolicy/fallbackPolicy/transitionPolicy`。
2. 在公共命令契约中写死 `Payload.destinationId` 为 `stardew_move` 的唯一正式承载位，避免后续只能继续靠 `LocationName/Tile` 串行推进。
3. 在公共回执/状态抽象中收口 move 语义：`GameCommandResult` 承载 `accepted/commandId/destinationId/initialPhase`，`GameCommandStatus` 承载 `destinationId/phase/currentLocationName/resolvedStandTile/routeRevision`，避免这些字段只存在于 Bridge DTO、Query 或文档层。

**关键决策**
- 本计划明确二选一结论：`stardew_move` 的公共命令契约唯一正式承载位是 `Payload.destinationId`，不是 `GameActionTarget`。`GameActionTarget.LocationName/Tile` 在 move 语义中自阶段 2 起降级为 legacy 兼容影子字段，不再承担规范真相，也不得继续作为新调用方的文档化入口。
- 本计划对“提交回执语义封口”明确选择第一条路径：扩展公共 `GameCommandResult` 承载 `destinationId/initialPhase`，不再另起一套 Stardew 专用受控回执面。原因是 `GameCommandResult` 已经是提交命令后所有调用方都会经过的公共回执抽象；如果另建 move 专用回执 DTO，`destinationId/initialPhase` 仍会被困在游戏适配层，公共调用方依旧只能看见旧 `Accepted/CommandId/Status` 组合，无法真正封住公共返回面断口。
- 与提交回执配套，公共 `GameCommandStatus` 必须同步扩展 `destinationId/phase/currentLocationName/resolvedStandTile/routeRevision`。对非 move 命令这些字段可以为空、默认或不适用，但字段承载位必须位于公共状态抽象本身，而不是只附着在 Stardew Bridge status DTO 上。
- 过渡期兼容方式固定为：`StardewCommandService` 与 Bridge 入口先主读 `Payload.destinationId`；仅当 `Payload.destinationId` 缺失时，才允许从旧 `LocationName/Tile` 适配生成 legacy request，并在测试与日志中显式标记为 legacy path。
- Query 侧开始把 `destinationId` 当作主识别键，把 `label/tags/reason` 明确为投影语义而非执行真相。

**可测试验收标准**
- 单测可构造一个 move accepted 响应，并断言公共 `GameCommandResult` 直接暴露 `accepted/commandId/destinationId/initialPhase`，不需要读取 Stardew 私有 DTO 才能识别受理结果。
- 单测可构造一个 move status 响应，并断言公共 `GameCommandStatus` 直接暴露 `destinationId/phase/currentLocationName/resolvedStandTile/routeRevision`；非 move 命令在这些字段为空或默认时仍保持兼容。
- Query 输出的目的地事实中，`destinationId` 是稳定主键；`label/locationName/x/y` 即使存在，也不再被标记为执行真相。
- registry 能按 `destinationId` 解析出唯一 entry；传入未知 `destinationId` 时返回稳定错误而非回退到模糊字符串匹配。

### 阶段 2：收口 `GameAction`/`StardewCommandService` 到 destination-first 提交路径

**主要修改文件**
- 修改：`D:\GitHubPro\Hermes-Desktop\src\game\core\GameAction.cs`
- 修改：`D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewCommandService.cs`
- 修改：`D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewNpcTools.cs`（显式纳入 `StardewRuntimeActionController`、`RecordSubmitResultAsync(...)`、`RecordStatusAsync(...)`、`ClearAsync(...)`、terminal 判定相关 runtime 控制逻辑）
- 修改：`D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewBridgeDtos.cs`
- 修改：`D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeHttpHost.cs`

**阶段目标**
1. 让 `stardew_move` 对外只要求 `destinationId`，满足硬约束“最终只提交 destinationId”。
2. 让 `StardewCommandService.SubmitAsync()` 不再把 `action.Target.LocationName/Tile` 当作 move 的唯一入口，而是建立 destination-first 的序列化路径。
3. 保留兼容影子字段仅用于过渡，不再作为主执行输入。

**关键决策**
- `stardew_move` 的唯一规范提交路径固定为 `Payload.destinationId`。`Target.Kind` 仅保留动作分类语义，不承载目的地身份；`GameActionTarget.LocationName/Tile` 只允许作为 legacy 输入被动兼容，不能再被任何新逻辑、新测试、新文档描述为正式 move 参数。
- `StardewCommandService` 必须明确“先读 `Payload.destinationId`，缺失时才走 legacy tile 兼容”，并在进入 legacy 兼容时产生日志/调试标记，供阶段 4 退出判定使用。
- `StardewNpcTools` 只提交 `destinationId`，不再做 label -> 坐标反解，也不再构造新的 tile 真相。
- Desktop runtime 对 Stardew move 的 claim 路线在本阶段同步收口，不允许留到实现时再决定：
  - 继续使用 `WorldCoordinationService` 保护 move 的互斥、节流和 action slot 生命周期。
  - `StardewRuntimeActionController.PrepareActionAsync(...)` 负责在 submit 前创建 `workItemId + destinationId` 占位 claim；`StardewRuntimeActionController.RecordSubmitResultAsync(...)` 负责在 accepted 后把该占位 claim 升级/重绑定为 `commandId + destinationId` 主 claim。
  - 若 submit 未 accepted、accepted 回写前抛异常，或 runtime 在进入 running 前就结束，则由 `RecordSubmitResultAsync(...)` 与 `ClearAsync(...)` 负责按同一责任链回滚/释放占位 claim；Bridge 不承担 Desktop claim 的补偿责任，调用方也不得绕过 runtime 直接清理。
  - `TryClaimMove(...)` 的规范身份键迁移为 `workItemId/commandId + destinationId`；`action.Target.LocationName/Tile` 不再是 claim 身份输入。
  - `RequiresMoveClaim(...)` 对 Stardew move 改为检查 `Payload.destinationId`，而不是要求 `Target.Tile` 与 `Target.LocationName` 非空。
  - 若存在 legacy `locationName/tile` 提交入口，其 tile 只允许用于兼容适配或调试展示，不得重新成为 claim 冲突主键。

**可测试验收标准**
- `StardewCommandService` 单测能覆盖三类输入：仅 `destinationId`、旧 `locationName/tile`、两者同时存在；断言优先级为 `destinationId` 主读，旧字段只作为兼容回退。
- `StardewNpcTools` 单测断言生成的 move action 不再依赖候选 tile 坐标作为必填输入。
- 当提交仅含 `destinationId` 的命令时，Bridge request 成功序列化并被接受；当仅含旧字段时，仍可在兼容期工作，但测试要标记为 legacy path。
- `StardewRuntimeActionController` 相关测试必须断言：仅含 `destinationId` 的 move 也会建立 Desktop claim，且 claim 身份不再取自 `action.Target.LocationName + action.Target.Tile`。

### 阶段 3：Bridge 独占执行，落实 phase/claim/replan/transition 一致性，并补齐 `interrupted` 终态清理闭环

**主要修改文件**
- 修改：`D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeCommandQueue.cs`
- 修改：`D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeMovementPathProbe.cs`
- 修改：`D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeMoveFailureMapper.cs`
- 修改：`D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewNpcTools.cs`（显式纳入 `StardewRuntimeActionController`、`RecordSubmitResultAsync(...)`、`RecordStatusAsync(...)`、`ClearAsync(...)`、`IsTerminalStatus(...)` / `IsInFlightStatus(...)` 与终态快照保留逻辑）
- 修改：`D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewAutonomyTickDebugService.cs`（纳入终态快照读取与 debug 展示 scope）
- 修改：`D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewQueryService.cs`（纳入终态快照投影与 Query 读取 scope）
- 建议新增：`D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeMoveExecutionState.cs`

**阶段目标**
1. 把执行循环收敛为 `resolve destination -> preflight -> resolving_arrival -> planning_route -> executing_segment -> replanning/transitioning_location -> arriving -> terminal`。
2. 明确 claim 绑定 `commandId + destinationId`，把 `resolvedStandTile` 降级为可变执行快照。
3. 移除 `player_approached` interrupt，把普通 proximity/挡路纳入 replan 或失败语义。

**关键决策**
- replan 可以更新 `resolvedStandTile`，但不得改写 `destinationId`。
- transition 改变的是 segment 和当前 location，不是命令所声称的目标身份。
- failure mapper 必须 phase-aware，至少能区分：`invalid_destination_id`、`preflight_blocked`、`arrival_unresolvable`、`route_failed`、`transition_failed`、`interrupted_by_external_control`、`cancelled`。
- `interrupted` 被正式纳入 Desktop runtime terminal 集：一旦 Bridge status 进入 `interrupted`，`StardewRuntimeActionController` 必须把它当作与 `completed/failed/cancelled` 同级的终态处理，而不是继续把 runtime 留在 in-flight。
- `interrupted` 的清理路径在本计划中明确固定：
  - `StardewRuntimeActionController.RecordStatusAsync(...)` 在识别到 `interrupted/failed/completed/cancelled` 等 terminal status 时，必须先把最后一份终态 `GameCommandStatus` 连同 `commandId/destinationId/phase/currentLocationName/resolvedStandTile/routeRevision/errorCode/retryAfterUtc` 写入 runtime 持久快照，再进入清理；该终态快照的保留责任在 Desktop runtime，不在 Bridge。
  - 终态快照的落点固定为 NPC runtime controller 持久状态，由 `D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewNpcTools.cs` 所在 runtime/controller 写入，并由 `D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewQueryService.cs` 与 `D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewAutonomyTickDebugService.cs` 读取；Query/debug 在 `ActionSlot`、`PendingWorkItem` 已清空后，仍必须能读到最近一次 terminal move 的终态事实。
  - 清空 `ActionSlot`，避免 runtime 误以为仍有活跃 move 占槽。
  - 清空 `PendingWorkItem`，避免后续 tick/工具把已终止命令继续当作待完成 work item。
  - 释放与该命令绑定的 move claim（按 `workItemId/commandId + destinationId` 身份释放），避免并发控制残留把后续 move 永久挡住。
  - `nextWake` 采用“仅在 Bridge status 显式带 `RetryAfterUtc` 时跟随；否则默认置空”的策略，不把 `interrupted` 当作自动冷却重试信号。原因是 `interrupted` 在该设计里代表外部接管或控制上下文丢失，应交由上层重新观察并重新决策，而不是 runtime 悄悄续跑旧命令。
- 与 `interrupted` 相关的范围不只限于 terminal 判定函数，还必须把相关 runtime/controller 清理逻辑纳入同一阶段：`RecordSubmitResultAsync(...)`、`RecordStatusAsync(...)`、`ClearAsync(...)`、状态轮询停止条件、终态快照持久化，以及任何依赖 `IsTerminalStatus(...)` / `IsInFlightStatus(...)` 的 action slot 释放判断都要一起调整。

**可测试验收标准**
- 同一命令在 replan 前后，status 中 `destinationId` 不变，`routeRevision` 递增，`resolvedStandTile` 允许变化。
- 跨图 transition 前后，status phase 依次出现 `transitioning_location` 再回到 `planning_route/executing_segment`，但 claim 不丢失。
- 玩家靠近或短暂挡路不会直接产出 `interrupted/player_approached`；测试应验证进入 `replanning`、最终 `completed` 或 phase-aware `failed`。
- event lock、强制对话、显式 cancel 等真正外部接管情形，仍可产出 `interrupted/cancelled` 终态。
- `interrupted` 自动化测试必须覆盖 Desktop runtime 清理闭环：收到 `interrupted` status 后，终态快照已先被持久保留且可被 Query/debug 读取，随后 `ActionSlot` 被清空、`PendingWorkItem` 被清空、对应 claim 被释放、状态轮询停止，且 `nextWake` 仅在带 `RetryAfterUtc` 时设置，否则保持为空。
- `interrupted` 回归测试必须证明：同一 NPC 在一次 `interrupted` 终态后，可以立即重新提交新的 `destinationId` move，不会因残留 slot、pending work item 或 claim 被错误判定为 `action_slot_busy`/`command_conflict`。
- Query/debug 验收必须显式覆盖“空运行时 + 有最近终态快照”场景：`ActionSlot` 与 `PendingWorkItem` 均为空时，`StardewQueryService` 与 `StardewAutonomyTickDebugService` 仍能读出最近一次 terminal move 的 `commandId/destinationId/phase/status/errorCode/currentLocationName/resolvedStandTile/routeRevision`，且不会把该快照误判为 in-flight。

### 阶段 4：退役旧执行读法，完成兼容收束、测试与真实游戏验证

**主要修改文件**
- 修改：`D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewQueryService.cs`
- 修改：`D:\GitHubPro\Hermes-Desktop\src\games\stardew\StardewBridgeDtos.cs`
- 修改：`D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeHttpHost.cs`
- 修改：`D:\GitHubPro\Hermes-Desktop\Mods\StardewHermesBridge\Bridge\BridgeCommandQueue.cs`
- 测试修改：`D:\GitHubPro\Hermes-Desktop\Desktop\HermesDesktop.Tests\Stardew\StardewNpcToolFactoryTests.cs`
- 测试修改：`D:\GitHubPro\Hermes-Desktop\Desktop\HermesDesktop.Tests\Stardew\StardewCommandServiceTests.cs`
- 测试修改：`D:\GitHubPro\Hermes-Desktop\Desktop\HermesDesktop.Tests\Stardew\StardewQueryServiceTests.cs`
- 测试修改：`D:\GitHubPro\Hermes-Desktop\Desktop\HermesDesktop.Tests\Stardew\StardewAutonomyTickDebugServiceTests.cs`

**阶段目标**
1. 在 Bridge 执行器内部正式退役按 `label/x/y` 直接执行的旧读法，并建立拒绝 legacy 提交入口的可执行退出条件。
2. 让主消费方只依赖 `commandId/destinationId/phase/status/errorCode/currentLocationName/resolvedStandTile/routeRevision`；旧字段如保留，仅用于调试展示或受控兼容。
3. 完成自动化与真实游戏验证，证明 phase、claim、compat 以及 `interrupted` 清理闭环都可测且可回归。

**阶段 4 退出条件（满足后即可拒绝 legacy 提交入口）**
- `StardewNpcTools`、`StardewCommandServiceTests`、相关集成测试与真实游戏验证样例，全部已改为只提交 `Payload.destinationId`，不再依赖 `LocationName/Tile` 组装 move。
- 仓内主消费方已完成切换：status/query/debug 的业务读取只使用 `commandId/destinationId/phase/status/errorCode/currentLocationName/resolvedStandTile/routeRevision` 这组主读字段；`locationName/tile/label` 不再被用作执行真相判定。
- 连续一轮回归中不存在任何必须依赖 legacy 入口才能通过的自动化测试；保留的 legacy 用例仅允许验证“显式拒绝”或受控兼容告警。
- Bridge/命令服务已具备 legacy 命中观测；当该观测在约定验收窗口内降为 0，或仅剩明确豁免的调试入口时，即可把 legacy 提交入口从“兼容接受”切换为“稳定拒绝并返回明确错误”。

**可测试验收标准**
- 代码层面不存在“按 `label` 或原始 `x/y` 直接决定执行落点”的主路径；若仍保留，只允许出现在兼容适配或调试投影中。
- Query 与 debug 服务的主展示字段集合固定为 `commandId/destinationId/phase/status/errorCode/currentLocationName/resolvedStandTile/routeRevision`；消费者禁止再把 `locationName/tile/label` 当执行真相偷读，旧字段若保留只能作说明性展示。
- 回归测试至少覆盖以下矩阵：
  - compat：新命令仅 `Payload.destinationId` 可通过；legacy 命令在兼容期可通过；阶段 4 退出条件达成后，未迁移旧入口会被显式拒绝
  - claim：replan/transition 下 `commandId + destinationId` 保持稳定
  - phase：`queued -> resolving_destination -> ... -> terminal` 顺序可观察
  - arrival：`StandOnAnchor` 与 `StandAdjacentAndFaceAnchor` 均可验证
  - interrupt：`player_approached` 不再触发 interrupt，外部接管仍会触发
  - failure：错误码能反映 phase，而不是统一塌缩成模糊失败

## 测试与验证策略

### 自动化测试优先级
1. **契约测试**：覆盖 `GameAction`、`GameCommandResult`、`GameCommandStatus`、`StardewCommandService`、Bridge DTO 的新旧字段兼容。
2. **状态测试**：覆盖 accepted 回执中的 `destinationId/initialPhase`，以及 status 中 `destinationId/phase/currentLocationName/resolvedStandTile/routeRevision` 的演进。
3. **执行测试**：覆盖 replan、arrival fallback、transition policy、interrupt 语义。
4. **投影测试**：覆盖 Query/skill 不再把 tile 坐标当执行真相。

### 真实游戏验证清单
- 同图直达目标：完成并返回正确 phase 演进。
- 相邻站位目标（如镜子类点位）：最终站在邻格并朝向 anchor。
- 玩家靠近或临时挡路：触发 replan 或有限失败，不触发 `player_approached` interrupt。
- 事件/对话接管：返回 `interrupted` 或 `cancelled`，错误码与 phase 可解释。
- 跨图目标：至少验证一次 `WarpThenRoute` 路径中的 phase 流转与 claim 稳定性。

## ADR
- **Decision**：采用“registry-first + 公共契约与公共回执/状态抽象同步收口 + 执行器独占终态 + 分阶段退役旧读法”的实现路径，并明确通过扩展 `GameCommandResult` 封住 `accepted/commandId/destinationId/initialPhase` 回执面。
- **Drivers**：可靠到达优先；`destinationId` 成为唯一稳定身份；公共命令契约、公共返回面、Bridge DTO、Query 投影必须同步收口；兼容迁移可测试可回归。
- **Alternatives considered**：
  1. 一次性重写整个 move pipeline。
  2. 温和中间态方案：先保持公共 target 结构不收缩，只把 `destinationId` 提升为主字段，延后删除 legacy 字段。
  3. 仅删除 `player_approached` interrupt，而不调整 claim/phase/compat 契约。
  4. 另建 Stardew 专用提交回执面，在适配层单独暴露 `destinationId/initialPhase`，不扩展 `GameCommandResult`。
- **Why chosen**：方案 1 风险最高；方案 3 只修表象不修边界；方案 4 会把 move 受理语义锁死在游戏适配层，公共调用方仍看不到 `destinationId/initialPhase`，断口依旧存在；最终选择以方案 A 为主、吸收方案 B 的缓冲策略，并把公共回执/状态抽象一并纳入迁移，既能尽快稳定内部真相，又能真正封住公共返回面。
- **Consequences**：短期内必须维护有限双写/双读与阶段矩阵；需要把 `GameAction`、`GameCommandResult`、`GameCommandStatus` 与 `StardewCommandService` 一并纳入 move 架构计划；测试会增加 receipt/phase/compat/claim 四类断言。
- **Follow-ups**：清理遗留 legacy 消费者；补全跨图目的地配置；完善调试面板对 `routeRevision/phase/errorCode` 的可观测性；在兼容结束后收紧公共 move target 结构。
