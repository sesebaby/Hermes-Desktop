# 星露谷 NPC 移动 destinationId 契约收敛共识计划

日期：2026-05-05
模式：`$ralplan` 非交互共识规划
状态：Planner -> Architect -> Critic 已达成 `APPROVE`；2026-05-05 已执行完成公开契约收敛
上下文：`.omx/context/stardew-move-destinationid-contract-20260505T063700Z.md`

## 0. 执行完成记录

2026-05-05 已完成本计划的公开契约收敛部分：

- `StardewMoveTool` 只透传 `destinationId`，不再重新 observe 或解析 `destination[n]` 的 label/坐标。
- `StardewCommandService` destination-first 路径发送 `Target = null` 的 move envelope；legacy target-only 路径仅作为隐藏兼容层。
- `StardewQueryService` 只暴露带 `destinationId` 的 executable destination facts。
- `stardew-navigation` / `stardew-world` 已明确 label 只是地点语义 metadata，不能作为 `stardew_move` 输入。
- 相关 Desktop / Bridge 测试已覆盖 destinationId 成功、label 由 Bridge 拒绝、query facts 必带 destinationId、runtime claim 不伪装成坐标、Bridge 不走 `warpCharacter` 伪跨图。

未完成且留给下一阶段：Bridge 真实跨地图移动执行、destination registry 扩容、跨图终态状态链路和真实游戏手测。

## 1. 需求摘要

下一步应优先对齐参考项目的移动执行边界：`stardew_move` 在 Agent/Desktop 侧只表达“去哪个稳定目的地”，Bridge/runtime 侧负责 destination registry、路径、可达站位、执行控制、卡住/超时/失败回报。

当前系统已具备一部分基础：

- `StardewCommandService` 已能发送 `destinationId` 且 `Target = null` 的 move envelope。
- Bridge 已优先用 `DestinationId` 走 `BridgeDestinationRegistry`，未知 id 返回 `invalid_destination_id`。
- Bridge 已有 path probe 和 closest passable neighbor 方向。

当前主要差距在 `StardewMoveTool` 仍重新观察 `destination[n]` facts，用 `destinationId` 或 `label` 匹配后反解析 `locationName/x/y/facingDirection`，再构造坐标 target。这和参考项目的“高层提交目标、执行器持有控制权”相冲突。

## 2. 参考项目对齐原则

严格借鉴参考项目方案，借鉴的是边界和控制权，不照搬具体代码。

### HermesCraft 证据

- `external/hermescraft-main/bot/server.js:318-329`：runtime 加载并配置 pathfinder movement。
- `external/hermescraft-main/bot/server.js:1272-1283`：`goto({ x, y, z })` 只接收目标，底层 `pathfinder.goto(goal)` 执行，并处理 timeout/cleanup。
- `external/hermescraft-main/bot/server.js:2587-2640`：长任务通过 background task/status surface 暴露状态。
- `external/hermescraft-main/bot/server.js:2685-2697`：movement stuck detection 属于 runtime，而不是 LLM 途中补坐标。

### Stardew 参考 Mod 证据

- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/Wrappers/SquadMateStateHelper.cs:54-66`：接管 NPC 前清理 `controller`、`temporaryController`、`DirectionsToNewLocation` 和 `IsWalkingInSquare`。
- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/FollowerManager.cs:821-889`：执行器逐帧驱动 `faceDirection`、velocity、`Position`、animation，并在终态 `Halt()`。
- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Pathfinding/AStarPathfinder.cs:102-149`：路径层可选择最近可站邻格并验证 reachability。

本项目对应结论：

- Agent/skill 只选择 `destinationId` 和 reason。
- Query 只投影候选语义和稳定 id，不把坐标作为工具输入契约。
- Bridge registry 是唯一可执行真相。
- Bridge executor 拥有路径、站位、重规划、状态、卡住/失败回报。

## 3. RALPLAN-DR

### Principles

1. Agent/Desktop 不解析、不合成、不反查目的地坐标。
2. Bridge/runtime owns executable truth and movement control。
3. `destinationId` 是唯一公开可执行输入；`label` 只能是语义/可读元数据。
4. Prompt、schema、skill、tests 同批定义同一条契约。
5. 参考项目的 goal -> executor / background status / stuck-failure ownership 是硬对齐标准。

### Decision Drivers

1. 严格对齐参考项目的高层目标提交与底层执行器控制边界。
2. 消除 observation 字符串反解析造成的漂移和隐藏双轨。
3. 在预发布阶段收敛单一路径，同时控制 brownfield 兼容风险。

### Viable Options

#### Option A: 本批删除 Agent/Desktop 坐标解析和 Bridge target fallback

优点：

- 最干净，真正单路径。
- 任何旧调用立即暴露，不会留下隐藏兼容层。

缺点：

- 回归面更大。
- 如果仓库内仍有未发现 target-only caller，会打断调试链路。

#### Option B: 本批公开契约 destinationId-only，Bridge/CommandService target fallback 暂留为 hidden compatibility

优点：

- 对外主路径已经严格对齐参考项目。
- 执行风险较低，可在下一小步删除 fallback。
- 能用测试和 grep 把 fallback 锁定为内部过渡物。

缺点：

- 运行时代码短期仍有兼容分支。
- 如果文档/测试治理不严，可能重新固化成双轨。

#### Option C: 保留 label fallback，只更新说明

优点：

- 改动最小。
- 短期模型调用更宽容。

缺点：

- 不解决根因。
- Agent/Desktop 继续持有执行真相，违背参考项目边界。

### Decision

选择 Option B，但加硬约束：

- `StardewMoveTool`、schema、skills、autonomy prompt、主路径 tests 本批必须全部变成 destinationId-only。
- Bridge 和 `StardewCommandService` 的 `target` fallback 本批仅作为未广告内部兼容，不得出现在工具描述、schema、skill、prompt 或新主路径测试中。
- 下一小步删除 fallback，删除门槛是仓库内无 NPC/autonomy caller 发送 target-only move，并有 grep/test 证据。

## 4. 契约细化

### `stardew_move` 输入

- `destination` 参数暂保留字段名，但语义收敛为 exact `destinationId`。
- 空值在 Tool 层 fail-fast。
- 非空未知值、label 值、格式错误值不在 Tool 层解析或转换，原样作为 `payload.destinationId` 交给 Bridge。
- Bridge registry 负责可执行性判断；未知目标返回稳定 `invalid_destination_id`。

### `destination[n]` facts

- 供 `stardew_move` 选择的 executable `destination[n]` 必须始终包含 `destinationId`。
- 无 `destinationId` 的候选不得作为 executable move candidate；可改为非执行语义事实，或不暴露为 `destination[n]`。
- `label` 可继续保留为 observation、world skill、debug 的可读语义字段，但禁止作为 `stardew_move` executable input。

### Hidden Compatibility

- `StardewCommandService` 可暂时继续接受 target-based `GameAction`，但测试必须重命名或标注为 compatibility-only。
- Bridge 可暂时保留 `payload.Target` fallback，但不能被 Agent/Desktop 新主路径依赖。
- 不新增第二 tool lane、第二 schema 或并行提示路径。

## 5. 实施步骤

### Step 1: TDD 锁定 Tool destinationId-only 主路径

触点：

- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `src/games/stardew/StardewNpcTools.cs`

测试目标：

- `stardew_move(destination="town.fountain")` 不调用 `IGameQueryService.ObserveAsync`。
- 提交的 action 是 `new GameActionTarget("destination")`，`LocationName` 和 `Tile` 为空。
- `Payload["destinationId"] == "town.fountain"`。
- `label` 输入如 `"Town fountain"` 不再被本地解析成坐标；它会作为 `destinationId` 交给 fake command/Bridge，并得到 `invalid_destination_id` 类失败。
- 空 `destination` 仍在 Tool 层 fail-fast。

### Step 2: 删除 Tool 侧目的地反解析

触点：

- `src/games/stardew/StardewNpcTools.cs`

删除/调整：

- 删除 `ResolveDestinationAsync`。
- 删除 `TryReadDestinationFact`。
- 删除 `BuildDestinationId`。
- 删除 `NormalizeDestinationSegment`。
- 删除 move tool 对 `_queries.ObserveAsync` 的依赖路径。
- `ExecuteAsync` 只构造 destination-only action。

### Step 3: 收紧 schema、description 和 skill 文案

触点：

- `src/games/stardew/StardewNpcTools.cs`
- `skills/gaming/stardew-navigation.md`
- `skills/gaming/stardew-world/SKILL.md`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`

要求：

- 不再提示 `destination[n].label` fallback。
- 不再把 `nearby[n]` 描述为 `stardew_move` 的替代执行输入。
- world skill 仍可解释 `label`、`tags`、`reason`，但必须说明可执行调用只能复制 exact `destinationId`。
- autonomy prompt fixture/tool call 示例改为 `destination="town.fountain"` 这类 id。

### Step 4: 建立 executable destination invariant

触点：

- `src/games/stardew/StardewQueryService.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewQueryServiceTests.cs`

要求：

- 可执行 `destination[n]` facts 均包含 `destinationId`。
- 无 id 候选不作为 executable `destination[n]` 暴露。
- 测试更新无 id fixture，避免继续证明旧契约。

### Step 5: 明确 CommandService / Bridge fallback 边界

触点：

- `src/games/stardew/StardewCommandService.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`

要求：

- 加强 destinationId-only envelope test：`payload.target == null` 且 `payload.destinationId` 存在。
- 保留 target fallback 测试时重命名/标注为 compatibility-only。
- 不把 compatibility fallback 写入 Agent-facing schema 或 skill。
- 记录下一步删除条件：无 NPC/autonomy target-only caller，Bridge target fallback test 可删除或改成拒绝。

## 6. 验收标准

1. `StardewMoveTool` 不再调用 `ObserveAsync` 来解析目的地。
2. `StardewMoveTool` 不再解析 `destination[n]` fact，不再从 label/location 合成 `destinationId`。
3. `stardew_move` 主路径 action target 不含 location/tile，只含 kind 和 payload `destinationId`。
4. Tool/schema/skill/autonomy prompt 不再把 label 描述为可执行 fallback。
5. 可执行 `destination[n]` facts 都带 `destinationId`。
6. Bridge registry 仍对未知 id 返回稳定 `invalid_destination_id`。
7. Legacy target fallback 如保留，只能在 compatibility-only 测试中出现。
8. 计划执行后文档中的“下一步工作依据”应指向 Bridge 执行器可靠性和 fallback 删除，而不是回到 Agent 坐标解析。

## 7. 验证步骤

### Targeted Tests

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests|FullyQualifiedName~StardewCommandServiceTests|FullyQualifiedName~StardewAutonomyTickDebugServiceTests|FullyQualifiedName~StardewQueryServiceTests"
```

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMoveCommandQueueRegressionTests"
```

### Static Contract Checks

应无匹配：

```powershell
rg -n 'ResolveDestinationAsync|TryReadDestinationFact|BuildDestinationId|NormalizeDestinationSegment' src/games/stardew/StardewNpcTools.cs
```

应无 executable-input fallback 文案；`label` 作为 metadata 可保留，但不得作为 move 参数 fallback：

```powershell
rg -n 'destination\[n\]\.label|fall back to copying the exact label|只有没有 `destinationId` 时才退回|fallback to destination.*label' skills/gaming/stardew-navigation.md skills/gaming/stardew-world/SKILL.md Desktop/HermesDesktop.Tests/Stardew
```

### Broader Verification

Targeted tests 通过后运行：

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
```

## 8. 风险与缓解

| 风险 | 缓解 |
| --- | --- |
| 模型短期继续输出 label | skill/schema/autonomy prompt 同批删除 fallback，Bridge 返回 `invalid_destination_id` 暴露错误 |
| 无 id candidate 仍进入 prompt | `destination[n]` invariant + Query tests 固化 |
| hidden fallback 变成长久双轨 | compatibility-only 标注 + 下一小步删除门槛 + grep 验证 |
| 执行者误删 label 语义字段 | 明确 label 作为 metadata 可保留，只禁止 executable input |
| 只改 Tool 未严格借鉴参考项目 | 计划要求 Bridge/runtime 拥有 registry/path/status/stuck/failure，后续工作继续沿 Bridge executor 推进 |

## 9. ADR

### Decision

将 `stardew_move` 的公开执行契约收敛为 destinationId-only。Agent/Desktop 只提交稳定 `destinationId` 和 reason；Bridge/runtime 作为唯一执行真相负责 resolve、path、arrival fallback、status、stuck/failure。

### Drivers

- 严格借鉴参考项目 goal -> executor 的边界。
- 避免 LLM 和 Desktop 反解析 observation 字符串。
- 预发布阶段收敛单路径，减少兼容债。

### Alternatives Considered

- 同批删除所有 target fallback：架构纯度最高，但回归风险较大。
- 保留 label fallback：短期宽容，但继续违背执行器 ownership。

### Why Chosen

Option B 能在本批完成公开契约收敛，同时把 brownfield 兼容风险限制在 Bridge/CommandService 内部，不再向 Agent 暴露。

### Consequences

- 模型必须复制 exact `destinationId`。
- 无 id destination 不再是可执行候选。
- Bridge registry 成为移动失败边界；错误会更早、更明确暴露。

### Follow-ups

1. 删除 `StardewCommandService` / Bridge target fallback。
2. 继续推进 Bridge executor 可靠性：控制权接管、自然移动、bounded replan、stuck diagnostics。
3. 更新 `docs/Hermes参考项目功能模块效果差距对比.md` 的下一步状态。

## 10. Consensus Review Notes

### Architect

结论：第二轮 `APPROVE`。

关键意见：

- 当前根因是控制权漂移，Tool 层不应做 registry/label 解释器。
- 第二版已落实参考项目边界。
- 执行时要防止 hidden fallback 重新长成公开 contract。

### Critic

结论：第二轮 `APPROVE`。

关键意见：

- 第二版已补齐 label/未知 id 失败规则、executable destination invariant、fallback 边界、验证命令和 label metadata 区分。
- PowerShell 中带反引号的 `rg` 正则建议使用单引号。

## 11. Available Agent Types Roster

可用角色：

- `explore`：快速定位旧 caller、测试 fixture、skill 文案。
- `executor`：主实现。
- `test-engineer`：测试重写和新增。
- `architect`：执行中边界变更复核。
- `critic` / `code-reviewer`：完成后审阅双轨残留。
- `verifier`：运行验证命令和静态断言。
- `writer`：同步更新文档。

## 12. Follow-up Staffing Guidance

### `$ralph` 路径

适合单人顺序执行。建议顺序：

1. `explore` 快速确认所有 label fallback 和 target-only caller。
2. `executor` 按 Step 1-5 实施。
3. `verifier` 跑 targeted tests、rg 静态断言、broader tests。
4. `code-reviewer` 检查 hidden fallback 是否外泄。

建议启动提示：

```text
$ralph 执行 .omx/plans/星露谷NPC移动destinationId契约收敛共识计划.md，严格借鉴参考项目 goal->executor 边界，先 TDD，再改 Tool/schema/skills/query/tests，最后验证。
```

### `$team` 路径

适合并行拆分：

- Lane A `executor`：`StardewMoveTool` destinationId-only 和 Tool tests。
- Lane B `test-engineer`：Query/autonomy/skill prompt tests 和 fixture invariant。
- Lane C `executor`：CommandService/Bridge fallback compatibility 标注与 tests。
- Lane D `verifier`：最终整合后运行 targeted/broader tests 和 `rg` 断言。

建议启动提示：

```text
$team 按 .omx/plans/星露谷NPC移动destinationId契约收敛共识计划.md 执行。拆分 Tool 主路径、Query/prompt invariant、CommandService/Bridge compatibility、最终验证四条 lane；所有 lane 必须保持 destinationId-only 公开契约。
```

Team verification path：

1. Team 内每条 lane 汇报 touched files 和测试结果。
2. Leader 合并后跑本计划第 7 节全部验证。
3. 如 `rg` 仍发现旧 parser 或 executable label fallback，回到对应 lane 修复。
4. 关闭 team 前由 `verifier` 明确证明主路径无坐标/label fallback。
