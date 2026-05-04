# 星露谷 NPC 移动执行可靠性修复共识计划

## 状态

- Workflow: `$ralplan` / `$plan --consensus`
- Scope: 规划，不实施业务代码
- Consensus status: Architect 多轮复审后仅剩定义口径问题；已逐条收口。Critic 子代理两次卡住，按 workflow fallback 在主线程完成最终 Critic 检查。
- Final verdict: APPROVE for execution handoff

## 白话结论

这次 Haley 不是“不会寻路”，也不是 HTTP 请求错了。她确实开始移动了，日志里的 `pathSteps=38` 说明桥接层已经算出一条路；失败点是执行到下一步 `HaleyHouse:7,7` 时，这一格被当前安全检查拒绝，于是命令失败。

核心问题是：系统把 `Front door (15,8)` 这种 `placeCandidate` 暴露给 agent 时，只检查了终点看起来安全；但真正执行时才计算整条 schedule path，并且每一步再检查一次安全。也就是“候选看起来能去”和“整条路现在真的能走”不是同一套判断。

## 证据

### 当前 Hermes 链路

- `BridgeHttpHost.BuildMoveCandidates` / `BuildPlaceCandidates` 目前只检查候选终点，安全判断是 `isTileLocationOpen && CanSpawnCharacterHere`: `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs:314-344`, `347-393`, `501-510`.
- HaleyHouse 的 curated endpoints 包括 `Bedroom mirror (6,4)`, `Living room (10,12)`, `Front door (15,8)`: `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs:395-420`.
- `StardewQueryService` 最多把 3 条 `moveCandidate` / `placeCandidate` 展开成 observation facts: `src/games/stardew/StardewQueryService.cs:117-157`.
- `stardew-navigation.md`, `stardew-world/SKILL.md`, `StardewMoveTool.Description` 都鼓励把 `placeCandidate` 当成有意义的 schedule-style endpoint: `skills/gaming/stardew-navigation.md:7-11`, `skills/gaming/stardew-world/SKILL.md:12-16`, `src/games/stardew/StardewNpcTools.cs:91`.
- `BridgeCommandQueue` 执行时用 `PathFindController.findPathForNPCSchedules` 准备路线，然后每一步动态检查 `IsTileSafeForMove`; `step_blocked` 来自 next step 被拒: `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:372-418`, `703-731`.
- 当前 `Fail(string)` 会把同一个字符串同时写到 `ErrorCode` 和 `BlockedReason`; 新的动态错误详情不能继续塞进单参数 `Fail`: `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:674-679`.
- runtime 对 terminal status 会清理 action slot；但 cooldown 主要跟 `blocked` / `expired` 相关，所以坏目标最终应走 `failed/path_blocked`，避免自动重试同一目标: `src/games/stardew/StardewNpcTools.cs:484-519`, `536-547`.

### 参考 mod 与官方资料

- ScheduleViewer 把 `SchedulePathDescription` 读成 endpoint 模型：`time + targetTile + targetLocationName + facingDirection + endOfRouteBehavior`，不执行中间路径: `参考项目/Mod参考/Stardew-Nexus-19305-ScheduleViewer/ScheduleViewer/Schedule.cs:91-98`.
- TheStardewSquad 借鉴点是分层恢复：目标不可走找邻格、直线可行就单节点、否则 A*、卡住后纠偏；但它的持续 controller/warp 接管不适合 Hermes Phase 1 普通自主 NPC: `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/FollowerManager.cs:756-905`, `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/Wrappers/SquadMateStateHelper.cs:57-101`.
- Stardew schedule data 是目标时间/location/tile 语义，不是逐步路线脚本: https://stardewvalleywiki.com/Modding%3ASchedule_data
- Stardew map pathing 相关 tile 属性包括 `NoPath`, `NPCBarrier`, `NPCPassable`, `TemporaryBarrier`; NPC pathing 与玩家 passability 不能简单等价: https://stardewvalleywiki.com/Modding%3AMaps
- Stardew 1.6 migration 提到 `TryLoadSchedule`, `ScheduleKey`, `WarpPathfindingCache`; Hermes 不应复刻底层 pathfinder，而应复用游戏 pathing 能力并做好诊断边界: https://stardewvalleywiki.com/Modding%3AMigrate_to_Stardew_Valley_1.6
- SMAPI release notes 也强调 map warp/pathfinding cache 会影响 NPC pathfinding: https://github.com/Pathoschild/SMAPI/blob/develop/docs/release-notes-archived.md

## RALPLAN-DR

### Principles

1. Bridge 只提供候选 affordance、执行结果和诊断，不替 agent 做长期意图决策。
2. Phase 1 继续限定 same-location move；跨图仍是 `cross_location_unsupported`，不静默 warp。
3. `placeCandidate` 是 schedule-style endpoint 候选，不是 route-guaranteed command。
4. 候选暴露与执行前准备共享静态 route probe；执行中仍保留动态 step check 与有限重算。
5. `step_blocked` 是内部诊断；bounded replan 耗尽后以 terminal `failed/path_blocked` 收尾，让 agent 下一轮重新观察或换目标。

### Decision Drivers

1. 消除 endpoint-safe 但 route-unsafe 的 false affordance。
2. 保留 Haley 的语义地点移动，不退回只走相邻碎步。
3. 控制 blast radius：不新增依赖、不扩 DTO/fact 字段、不引入持续 controller、不改变跨 location 语义。

### Options

| Option | 方案 | Pros | Cons | 结论 |
| --- | --- | --- | --- | --- |
| A | Prompt-only 收紧 | 改动最小 | bridge 仍会暴露 route-unsafe 候选，执行仍可能中途失败 | 不选 |
| B | Shared static route probe + runtime bounded replan + prompt/tool contract clarification | 修复候选与执行准备模型不一致；保留 endpoint 语义；不扩协议 | status 热路径更重；候选可能更保守 | 采用 |
| C | Runtime-only 放宽/移除 step safety | 可能绕过 `7,7` | 可能把 NPC 放到不可生成/不可站位置，风险高 | 不选 |
| D | TheStardewSquad-style 持续 controller/warp | 恢复能力强 | 侵入大，改变普通 NPC 语义，和 command queue 冲突 | 不选 |
| E | 只删/降级 `Front door` | 快速避开本次目标 | 修表象，不修模型分裂，其他 endpoint 仍会失败 | 不选 |

## ADR

Decision: 采用 Option B：shared static route probe + runtime bounded replan + stable `failed/path_blocked` terminal mapping + prompt/tool contract clarification。

Drivers:
- 当前 failure 是候选模型和执行模型分裂。
- Haley 仍需要语义地点移动。
- Phase 1 需要小步、可测、可回退。

Alternatives considered:
- Prompt-only。
- Runtime-only 放宽 step safety。
- TheStardewSquad-style controller/warp。
- 只删/降级 `Front door`。

Why chosen:
- 直接修“看起来能去，实际中途失败”的根因。
- 保留 `placeCandidate` 作为 endpoint 候选，不把 agent 降级成只会走附近格子。
- 不扩 DTO/fact 字段，不改变跨图边界。

Consequences:
- status probe 会更重，但候选数量 bounded。
- 候选会更保守，但失败率下降。
- 需要新增 stable error code 和更细日志。

Follow-ups:
- `endBehavior` 端到端打通。
- 未来跨 location travel / door transition 单独设计。
- 如果 split detail 证明 `CanSpawnCharacterHere` 在中间步误杀，再单独调整 intermediate safety。

## 实施计划

### 1. 测试与诊断先行

新增 helper 行为测试，覆盖：
- route order: `Route[0]` 是 first step。
- `RouteValid`, `TargetUnsafe`, `PathEmpty`, `StepUnsafe`。
- `FailingTile` 与 `FailureKind`。
- target unsafe 与 step unsafe 分离。
- `ErrorCode` / `BlockedReason` 分离。
- replan attempts 计数。

如果 SMAPI 类型难以实例化，helper 的 pathfinder/safety 通过小接口或 delegate 注入；生产实现接 Stardew API，测试实现提供 deterministic route/safety。

同时补 Desktop contract tests：
- `placeCandidate` 文案是 endpoint candidate，不声明 route-guaranteed。
- `path_blocked` 是稳定错误码 vocabulary。

### 2. 新增 shared static route probe

在 `Mods/StardewHermesBridge/Bridge/` 新增内部 helper，例如 `BridgeMovementPathProbe`。

结果对象建议：

```csharp
internal sealed record BridgeRouteProbeResult(
    BridgeRouteProbeStatus Status,
    IReadOnlyList<Point> Route,
    int PathLength,
    Point? FailingTile,
    string? FailureKind,
    string? FailureDetail);
```

Status:
- `RouteValid`
- `TargetUnsafe`
- `PathEmpty`
- `StepUnsafe`

字段口径：
- `Route[0]` 必须是从 current tile 出发的 first step。
- `Route` 不包含 current tile。
- 转换到现有 `Stack<Point>` 时必须 reverse push，保证 `Peek()` / `Pop()` 先拿到 `Route[0]`。
- `FailingTile`: target unsafe 时为 target tile；step unsafe 时为 route 顺序里的第一个 unsafe step；path empty 时为 null。
- `FailureKind`: `target_tile_open_false`, `target_can_spawn_false`, `path_empty`, `step_tile_open_false`, `step_can_spawn_false`。

### 3. 拆清 target 与 step safety

概念上拆成：
- `TargetAffordanceCheck`: endpoint 是否可作为候选目标。
- `RouteStepSafetyCheck`: route 中间 step 当前是否可走。

Phase 1 可以由同一个 helper 实现，但结果和测试必须分别覆盖两类失败，避免把“语义 endpoint”与“中间步当前可踩”永久绑死。

### 4. Path-aware candidate filtering

`BuildMoveCandidates`:
- 仍从近邻 deltas 生成候选。
- 只暴露 `RouteValid` 的 top 3。

`BuildPlaceCandidates`:
- 先 probe 全部 curated endpoints。
- 如果至少 1 个 curated `RouteValid`，只暴露这些 valid curated，不用 nearby 补足。
- 如果 0 个 curated valid，fallback 到 route-valid `moveCandidate` 衍生 nearby place，最多 3。

本轮不新增 `route=verified,pathSteps=N` facts；过滤原因只写 bridge 日志。

### 5. Runtime bounded replan

`MaxReplanAttempts = 2`:
- 含义是初始 path prepare 之后，运行中遇到 unsafe next step 最多额外重算 2 次。
- 初始 prepare 不计入。
- 成功重算不清零，防止同一命令无限卡住。

执行规则：
- next step unsafe 时，先记录内部 `step_blocked` 诊断。
- 若 attempts < 2，调用 probe 从当前 tile 到原 target 重算。
- replan valid: 替换 route，保持 `running`，日志记录 `route_replanned;blockedStep=x,y;attempt=n`。
- replan invalid 或 attempts exhausted: terminal `failed/path_blocked`。

### 6. Error mapping

必须保持 stable error code 与动态 detail 分离。

需要新增/调整 fail API，例如：

```csharp
command.Fail("path_blocked", "path_blocked:HaleyHouse:7,7;step_tile_open_false");
```

映射表：
- 初始 target unsafe: `Status=failed`, `ErrorCode=invalid_target`, `BlockedReason=target_blocked:<loc>:x,y;<FailureKind>`.
- 初始 path empty: `Status=failed`, `ErrorCode=path_unreachable`, `BlockedReason=path_unreachable:<loc>:x,y;path_empty`.
- 运行中 step unsafe 且 replan 成功: `Status=running`, log detail `route_replanned;blockedStep=x,y;attempt=n`.
- 运行中 step unsafe 且 replan 耗尽或 replan returns `StepUnsafe` / `PathEmpty`: `Status=failed`, `ErrorCode=path_blocked`, `BlockedReason=path_blocked:<loc>:x,y;<FailureKind>`.
- `path_exhausted` 保留现有码；除非 helper 行为测试证明需要调整，否则不纳入首修重命名。

`path_blocked` 必须加入共享稳定错误码常量面：
- `src/games/stardew/StardewCommandContracts.cs`
- bridge/core 对应常量位置
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandContractTests.cs`

### 7. Prompt/tool contract clarification

更新：
- `skills/gaming/stardew-navigation.md`
- `skills/gaming/stardew-world/SKILL.md`
- `src/games/stardew/StardewNpcTools.cs` description/schema

规则：
- `placeCandidate` 是 endpoint candidate，不是 host 命令，也不是 route guarantee。
- 短距调整可以优先 `moveCandidate`。
- terminal failure 为 `path_blocked` / `path_unreachable` 时，下一轮必须 reobserve 或换目标，不原样重试同一目的地。

### 8. 验证 action slot

运行/补充 runtime action tests，证明 terminal `failed/path_blocked` 经 `RecordStatusAsync` 会释放 action slot，后续工具调用不再因旧命令卡住而 `action_slot_busy`。

除非测试暴露缺陷，本次不重构 slot 逻辑。

## Acceptance Criteria

1. HaleyHouse 的 `Front door (15,8)` 只有在 curated route probe `RouteValid` 时才出现在 `placeCandidate` facts。
2. 如果所有 curated endpoints invalid，才 fallback 到 route-valid nearby candidate；只要有至少一个 valid curated，就不回填 nearby。
3. 执行中遇到 `HaleyHouse:7,7` unsafe 时，初始 path 后最多额外 2 次 replan；第一次不直接 terminal。
4. replan 成功时 command 保持 `running`，日志含 `route_replanned;blockedStep=7,7;attempt=n`。
5. replan 耗尽失败时 `Status=failed`, `ErrorCode=path_blocked`, `BlockedReason` 含 failing tile 和 `FailureKind`。
6. `ErrorCode` 不包含动态坐标/detail；动态内容只在 `BlockedReason` 和日志中。
7. terminal `failed/path_blocked` 后 action slot 被清理，后续工具调用不再因旧命令卡住而 `action_slot_busy`。
8. Cross-location move 仍是 `cross_location_unsupported`，无 teleport。
9. Prompt/tool 文案不再暗示 `placeCandidate` route-guaranteed，同时仍允许语义 endpoint movement。
10. 验证命令通过：
    - `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug`
    - `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug`

## Risks And Mitigations

- Status 热路径成本升高：curated endpoint 数量 bounded；move deltas 最多 12，最终暴露最多 3；本轮固定 probe 全部 curated，再 probe move fallback，不留运行时策略分叉。
- `CanSpawnCharacterHere` 过严导致候选过少：先通过 split detail 日志确认；不在首修盲目放宽。
- 真实 `7,7` 阻塞原因未知：用 `FailureKind` 和人工复现消除黑箱。
- Dirty files 已存在：执行前读 `git diff`，只做最小 patch，不回退用户改动。
- 子代理不稳定：已记录 Critic 子代理卡住；执行阶段可以用 native agent，但不要让卡住的 agent 阻塞主线验证。

## Available Agent Types Roster

- `executor`: 实现 bridge helper、candidate filtering、runtime replan、prompt/tool contract。
- `test-engineer`: 补 helper 行为测试、desktop contract tests、runtime slot tests。
- `debugger`: 若复现仍出现 `step_blocked` 或 `action_slot_busy`，定位具体 state transition。
- `verifier`: 跑两个 test projects，核对日志和 acceptance criteria。
- `build-fixer`: 处理编译/测试环境问题。
- `architect`: 若实现中发现 `CanSpawnCharacterHere` 与 schedule pathfinder 语义冲突，重新评估 boundary。
- `critic` / `code-reviewer`: 执行后做质量门禁。

## Follow-up Staffing Guidance

### Ralph Path

Use when one owner should complete the fix sequentially.

- `executor` high reasoning: implement `BridgeMovementPathProbe`, candidate filtering, bounded replan, error mapping.
- `test-engineer` medium reasoning: add helper behavior tests and desktop/runtime contract tests.
- `verifier` high reasoning: run tests and inspect logs.

Launch hint:

```text
$ralph implement .omx/plans/星露谷NPC移动执行可靠性修复共识计划.md
```

### Team Path

Use when faster parallel execution is needed.

- Lane A, `executor`: bridge helper + command queue replan + stable error mapping.
- Lane B, `test-engineer`: helper tests + contract tests + action slot test.
- Lane C, `executor` or `writer`: prompt/tool contract updates.
- Lane D, `verifier`: test execution and evidence summary.

Launch hint:

```text
$team implement .omx/plans/星露谷NPC移动执行可靠性修复共识计划.md with lanes A bridge, B tests, C prompt-tool, D verification
```

Team verification path:
- Team must prove helper route order, error code/detail split, fallback behavior, and replan attempts with tests.
- Team must run both test projects.
- Ralph or verifier should do final log checklist for `path_blocked` and `action_slot_busy`.

## Review Changelog

- Architect iteration 1: clarified `path_blocked` should be `failed`, not `blocked`; rewrote Principle 4 to separate static probe from dynamic runtime checks.
- Critic iteration 1: added explicit probe contract, failing tile semantics, `MaxReplanAttempts` counting, fixed `SquadMateStateHelper` path, aligned alternatives.
- Architect iteration 2: fixed stable `ErrorCode` vs dynamic `BlockedReason`; split target affordance from route step safety; fixed placeCandidate fallback rule.
- Architect iteration 3: fixed route ordering, probe status to error mapping, and shared `path_blocked` constant/test requirement.
- Critic fallback: subagent Critic hung twice; main-thread review found no remaining blocking ambiguity after the above fixes.
