# 星露谷海莉 NPC 可见移动修复共识计划

## 计划摘要

- 计划类型：RALPLAN 共识计划修订版
- 计划来源：基于 `.omx/plans/stardew-haley-visible-move-fix-plan-20260503.md` 吸收 Architect 必改项后的正式中文版
- 范围：`autonomy observation/facts -> shared prompt/tool surface -> bridge move execution`
- 复杂度：中等偏高
- 预计触点：约 10-13 个文件，覆盖 `src/runtime`、`src/games/stardew`、`skills/gaming`、`Mods/StardewHermesBridge`、对应测试项目

## Architect 修订已落地

本次修订将以下内容从“建议”提升为“硬约束/明确决议”：

1. **首次 autonomy 观察事实必须包含安全候选目标。**
   - `ObserveAsync` 在首轮决策前返回的 `GameObservation.Facts`，必须包含 **1-3 个机器可读候选目标**。
   - 这些候选目标必须在 agent 首次决策消息里可见，不能只停留在 `stardew_status` 的工具返回里，更不能要求 agent 先额外拉一次工具才能获得最基本的 move 候选。
2. **普通 host/background service 不得通过事件/命令驱动 move。**
   - Phase 1 中“玩家命令解析结果”不作为普通 autonomy 候选来源。
   - 仅允许保留**既有私聊例外链路**里已经产生的候选；除此之外，host/background service 不得因事件或命令而替 NPC 触发 move。
3. **Phase 1 决策规则固定为 observation-first。**
   - 首选：在同一次 `ObserveAsync` 中输出紧凑、低噪音的候选目标 facts。
   - 仅当实践证明候选 facts 无法稳定表达、噪音不可控时，才允许通过同一个 `StardewNpcToolFactory` 增加 `stardew_world_snapshot`。
   - 本轮默认**不先落地**新工具。
4. **bridge Phase 1 边界写死。**
   - 仅支持：`同 location`、`短距离`、`多 tick`、复用现有 `queued/running/completed` 状态契约。
   - `跨 location` 一律先返回 `blocked`，不在本轮偷渡更大导航范围。
5. **反命题必须纳入主文。**
   - 计划必须正面回答：为什么不是 world snapshot tool 先行；为什么 bridge 分步移动不能推迟到下一轮。
6. **验收标准必须把“三段断点 -> 对应证据”一一映射。**
   - 不能只列泛化验收条目，必须明确每个断点对应哪类测试、日志、transcript、状态证据。

## 不可突破的硬约束

1. 不允许非私聊事件驱动 NPC 移动。
2. host/background service 不得替 NPC 决定目的地。
3. NPC 行为倾向只允许通过提示词、工具描述、观察事实影响 agent 自主选择。
4. 桌面 agent 与 NPC agent 必须保持同源装配，不新增第二套 agent 装配链。
5. Phase 1 的候选移动目标必须首先出现在 `GameObservation.Facts`，而不是先依赖新工具拉取。
6. 普通 autonomy tick 的目标来源不得来自新的 host 事件分支；私聊例外链路保留现状，但不扩展其权限面。
7. bridge Phase 1 不做跨地图导航，不做 schedule/follow/复杂 pathfinding。
8. autonomy 决策只能把**当前轮 observation**里的候选目标当作可执行候选；历史 observation 中的旧候选不得继续混入当前候选集。
9. bridge 只能做“本地、同 location、短距离、安全 tile 枚举”，不得做目的地偏好推断、任务解释或替 NPC 选择最终目的地。

## 目标与问题拆解

目标不是单点修 prompt 或单点修 bridge，而是闭合三个连续断点：

1. agent 为什么没有选择 `stardew_move`
2. agent 在选择 `stardew_move` 前是否看到了安全、机器可读、低噪音的候选目标
3. bridge 收到 `move` 后是否以可见、可诊断、非瞬移的方式执行

只有三段同时闭合，才能解释并修复“海莉会说话但不发生可见移动”。

## 现有证据

- `NpcAutonomyLoop` 当前以观察事实驱动 agent 决策，不直接替 NPC 下动作：`src/runtime/NpcAutonomyLoop.cs:95`, `src/runtime/NpcAutonomyLoop.cs:123`
- 私聊与 autonomy 当前共用同一条 agent 装配主链，而非两套 NPC/桌面分支：`src/runtime/AgentCapabilityAssembler.cs:14`, `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:392`, `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs:51`
- `stardew-navigation` 已经限制 move 目标必须来自 bridge facts、既有私聊候选或已知安全测试目标，不能让 agent 编坐标：`skills/gaming/stardew-navigation.md:5`
- `stardew_move` 工具存在，但当前描述不足以稳定把“要去某处”转成真实工具调用：`src/games/stardew/StardewNpcTools.cs:89`
- `stardew_status` 当前只暴露位置/移动状态/可控性，不足以让 autonomy 首轮决策看到候选目标：`src/games/stardew/StardewQueryService.cs:90`
- bridge 当前 move 在 `PumpOneTick` 中直接 `setTilePosition`，本质仍是瞬移：`Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:310`

## RALPLAN-DR Summary

### 原则

1. 决策权留在 agent，host 只提供事实、工具、状态推进。
2. 先修 observation 输入，再修工具语义，最后修 bridge 执行；不接受只修单层。
3. 优先走共享主链修复，不为 NPC 另造旁路。
4. Phase 1 必须是低风险可诊断闭环，而不是半成品占位。
5. 每个断点都要有直接证据，不靠“手感上应该可以”。

### 决策驱动 Top 3

1. 满足自治约束：不允许 host 决策目的地，不允许事件驱动 move。
2. 用最小范围打通真实闭环：首轮 facts 可见候选、agent 会选 move、bridge 可见执行。
3. 保持执行可验证：自动测试、transcript、状态流转、日志可以逐段归因。

### 可行方案

#### 方案 A：observation-first 候选 facts + 共享 prompt/tool 强化 + bridge 分步可见移动

- 做法：
  - 在 `ObserveAsync -> GameObservation.Facts` 首轮输出 1-3 个机器可读安全候选目标。
  - 强化共享 `stardew-navigation` required skill 与 `stardew_move` 工具描述，使 agent 在看到候选后优先调用工具而不是口头承诺；Phase 1 默认不改 Haley 专属 persona facts。
  - bridge 维持同一 `move` 命令契约，但改成同 location、短距离、多 tick 的运行态推进。
- 优点：
  - 最贴合当前 `ObserveAsync -> facts -> autonomy decision -> tool call` 主链。
  - 不要求 agent 先额外调用新工具才能做最基础 move 决策。
  - 最容易把三段断点映射到现有测试与日志体系。
- 代价：
  - 需要严格控制候选 facts 的格式和数量，避免观测噪音污染模型。

#### 方案 B：先新增 `stardew_world_snapshot` 工具，再让 agent 主动拉取候选目标

- 做法：
  - 保持默认 observation facts 轻量。
  - 通过同一个 `StardewNpcToolFactory` 增加 `stardew_world_snapshot`，让 agent 决策前或决策中主动拉取候选目标。
  - bridge 同样改为多 tick move。
- 优点：
  - 状态与世界候选目标分层清晰。
  - 对长期扩展友好。
- 代价：
  - Phase 1 里会把“是否能动”依赖成“两步推理”：先意识到要拉 world snapshot，再意识到要 move。
  - 在当前问题里先增加工具面，会降低首轮诊断确定性。
- 结论：
  - **不作为本轮默认方案。**
  - 仅当 observation-first 候选 facts 经验证无法低噪音稳定表达时，才升级到该方案。

#### 方案 C：只改 prompt/tool 描述，bridge 分步移动后再观察

- 优点：
  - 表面改动最小。
- 缺点：
  - 无法保证 agent 首轮看到安全目的地。
  - 不能满足“候选目标进入首轮 `GameObservation.Facts`”这一硬约束。
  - 极易把问题伪修成“偶尔动、无法复现、无法诊断”。
- 结论：
  - **淘汰。**

## 推荐方案

- 推荐采用：**方案 A**
- 升级条件：只有在 observation-first 候选 facts 经过测试后仍无法稳定做到低噪音、机器可读、可复现时，才进入同工厂新增 `stardew_world_snapshot` 的 Phase 1.5 / Phase 2 讨论。

## 反命题与结论

### 为什么不是 world snapshot tool 先行

1. 当前真正缺的是**首轮决策前**的最小候选目标，而不是更多世界数据。
2. 如果先上 `stardew_world_snapshot`，agent 会多一道工具选择分支，问题从“三段断点”变成“四段断点”，首轮排障更差。
3. 现有限制明确要求：安全候选目标必须进入首次 autonomy 观察事实；只把候选挂在新工具上，不满足 Architect 硬要求。
4. 本轮修复目标是让 Haley 在最短闭环内先“稳定可动”，不是先铺长期工具架构。

结论：**world snapshot 作为后备升级面保留，但不先行。**

### 为什么 bridge 分步移动不能推迟到下一轮

1. 当前 bridge 是瞬移语义，即使 agent 正确调用 `stardew_move`，也无法证明“可见移动”问题已经真正修复。
2. 如果本轮只修 agent 选择、不修 bridge 可见执行，会留下“命令发了但玩家仍看不到走动”的假完成状态。
3. 三段断点是串联问题；第三段若继续空缺，手测仍会失败，RALPLAN 不应把明显缺口外包给下一轮。
4. Phase 1 已经把边界压到同 location、短距离、多 tick，这个范围足够小，可以在本轮完成而不扩散成复杂 pathfinding 项目。

结论：**bridge 分步可见移动是本轮闭环的一部分，不允许后置。**

## Phase 1 边界决议

Phase 1 范围固定如下：

1. `move` 只支持 `同 location`。
2. 只支持 `短距离` 候选目标。
3. 执行模型必须是 `多 tick`，至少出现一个中间 `running` 态。
4. 复用现有 `queued/running/completed` 状态契约，必要时补 `blocked/failed` 原因，但不另造第二套命令生命周期。
5. `跨 location` 一律先 `blocked`，并带可诊断原因。
6. 普通 autonomy 不消费新的玩家命令/事件候选；若保留玩家命令解析，只能留在既有私聊例外链路内。

## 目标事实设计决议

### 首轮事实硬约束

`ObserveAsync` 返回的 `GameObservation.Facts` 在首轮决策前至少包含：

1. 当前 `location/tile`
2. 当前 `canMove/isBusy/menuOrEventBlocked` 类基础状态
3. **1-3 个机器可读候选目标**

候选目标格式要求：

- 机器可读
- 低噪音
- 固定键结构
- 直接可映射为 `locationName/x/y/reason` 类参数
- 数量严格限制为 1-3 个
- 默认只包含同 location、短距离、安全候选
- 带有当前 observation 的时间/轮次语义，且旧轮次候选不得在下一轮继续作为候选呈现

结论：

- `stardew_status` 可以继续存在，也可以复用为诊断工具；
- 但 **首轮 autonomy 候选目标不能只藏在 `stardew_status`**；
- 必须进入 `GameObservation.Facts`，让 agent 在首次决策就能看到。
- `NpcObservationFactStore` 可以继续保留历史 observation，供日志、debug、transcript 和测试使用；但 `NpcAutonomyLoop` 面向 agent 的决策消息里，候选 schema 只能来自最新 observation。
- 历史候选 facts 不得进入 `[Observed Facts]` 或任何等价 agent 决策输入；历史 observation 若保留给 agent，只能转成不含 `location/x/y/reason` 候选 payload 的摘要上下文。

### 候选来源决议

Phase 1 允许的候选来源只有：

1. bridge / query service 根据当前 location 动态发现的安全候选 tile
2. 测试专用安全候选目标
3. 既有私聊例外链路内已经解析出的候选

bridge / query service 的候选生成边界：

1. 只读取 NPC 当前 location、当前 tile、基础阻塞状态和本地可行走 tile 信息。
2. 只做短距离安全枚举，推荐用确定性顺序输出最多 1-3 个候选。
3. `reason` 只能表达安全枚举原因，例如 `same_location_safe_reposition` / `adjacent_walkable_tile`，不能表达“应该去哪里”“玩家希望去哪里”这类目的地偏好。
4. 不消费普通 host 事件、日程推断、玩家命令解析、LLM 结论或长期记忆来调整候选排序。
5. 最终选择哪个候选、是否移动，仍由 agent 在看到 observation facts 和工具描述后自主决定。

Phase 1 明确排除：

1. 普通 host/background service 基于事件替 NPC 产生命令式目标
2. 新增普通 autonomy 的玩家命令驱动 move
3. agent 自行编造 location/tile

## 实施步骤

### 步骤 1：先锁定三段断点测试与证据接口

工作内容：

- 增加/修订桌面侧测试，锁定“首轮 observation facts 是否带候选目标”。
- 增加/修订 runtime 测试，锁定“当前轮候选不会被历史 observation 候选污染”。
- 增加/修订 autonomy/debug 测试，锁定“看到候选后是否真实提交 `GameActionType.Move`”，不能只断言自然语言或工具名列表。
- 新增 `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueTests.cs`，锁定“move 是否经历 queued/running/completed 或 blocked，且中间 tick 没有到达最终 tile”，防止延迟后瞬移。

完成标准：

- 三段断点都有独立失败测试。
- 每段测试都能直接指向一个证据面，后续验收可复用。

### 步骤 2：把安全候选目标塞进首轮 `GameObservation.Facts`

工作内容：

- 默认沿 `query/status -> StardewNpcStatusData -> StardewQueryService.BuildStatusFacts -> GameObservation.Facts` 主链补充紧凑候选 facts，不改 `WorldSnapshot` 主链。
- 在 `src/games/stardew/StardewBridgeDtos.cs` 与 `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs` 扩展 status DTO，新增候选目标字段；**Phase 1 默认不修改** `StardewWorldSnapshotData` / `WorldSnapshotData`。
- 候选生成责任默认放在 bridge 侧 `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs` 的 `BuildStatusResponse`（必要时在同目录抽 helper，但仍属于 status query 路径），由 bridge 按“本地、同 location、短距离、安全枚举、无偏好推断”筛出 1-3 个候选。
- facts 序列化责任明确放在 `src/games/stardew/StardewQueryService.cs`：由 `BuildStatusFacts` 把 status DTO 中的候选序列化成固定键结构字符串 facts；`BuildStatusSummary` 仅做摘要，不承载机器可读候选主体。
- runtime 决策消息责任明确放在 `src/runtime/NpcAutonomyLoop.cs`：当前轮 observation 候选必须作为当前事实呈现；`NpcObservationFactStore.Snapshot` 中的旧候选不得被拼接进 agent 消息，避免全量累积导致旧候选继续参与当前决策。
- 只提供候选，不在 host/background service 里决定“现在应该去哪个”；`src/game/core/GameObservation.cs` 继续保持 `IReadOnlyList<string>` facts 形态，不在本轮引入新的 observation 结构。

完成标准：

- 首轮 autonomy 决策消息里可见候选目标。
- 候选格式稳定、低噪音、可被测试直接断言。
- 普通 autonomy 路径不依赖新 world snapshot 工具。

### 步骤 3：强化 shared prompt / skill / tool 描述，让 agent 把候选转成真实 move

工作内容：

- 更新 `skills/gaming/stardew-navigation.md`，把宽泛的 `player command parsing` 表述收紧为“仅既有私聊例外链路中已解析出的目标”；明确看到安全候选且适合 reposition 时，应直接调用 `stardew_move`。
- 通过 `StardewNpcAutonomyPromptSupplementBuilder` 验证 `stardew-navigation` required skill 确实注入 autonomy system prompt；Phase 1 默认不修改 `src/game/stardew/personas/haley/default/facts.md` 或 `skills.json`，避免把共享能力修复变成 Haley 专属 persona 补丁。
- 强化 `stardew_move` 描述，要求参数必须来自观察到的候选事实。
- 增加受控工具调用测试：构造唯一候选 fact（例如 `location=Town,x=42,y=17,reason=same_location_safe_reposition`），用会返回 `stardew_move` tool call 的假 chat client 驱动 autonomy/debug 链路，并断言 FakeCommandService/等价提交记录中出现 `GameActionType.Move`，且 `GameAction.Target.LocationName/Tile` 与唯一候选一一对应。
- 检查 `src/games/stardew/StardewNpcAutonomyBackgroundService.cs` 及其 ingress 路径，保持只有 `scheduled_private_chat` / 既有私聊例外链路能驱动 `open_private_chat`，**不得新增** 非私聊的 move ingress、事件分支或 host 代决策逻辑。

完成标准：

- 仍保持桌面 agent 与 NPC agent 同源装配。
- 不新增 NPC 专用工具注册支路。
- 测试中可稳定看到 `stardew_move` tool call 被执行并提交 `GameActionType.Move`，而非纯自然语言意图或仅工具名出现在 tool surface。
- `stardew-navigation.md` 不再允许把普通 `player command parsing` 当作 move 候选来源。

### 步骤 4：把 bridge move 改成 Phase 1 分步可见执行

工作内容：

- 保持现有命令入队与状态查询契约。
- 改为 `queued -> running -> completed` 的多 tick 推进。
- 同 location、短距离成功；跨 location 明确 `blocked`。
- 以 `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueTests.cs` 为主测试锚点，覆盖：
  - `queued -> running -> completed`
  - `cross-location -> blocked`
  - 非单 tick 瞬移，至少一次 `PumpOneTick` 后仍处于 `running`
  - 防“延迟后 teleport”：至少一个非终态 tick 中，NPC 世界内 tile 仍未到最终 tile；后续 tick 才逐步推进或完成到终点，不能先空等一拍再 `setTilePosition` 到终点

完成标准：

- 不再单 tick 直接完成。
- 至少一个中间 `running` 态可被自动测试与日志观察到。
- 中间 `running` 态不只是状态标签，必须对应 NPC 尚未到达最终 tile 的世界状态。
- 失败时带可关联原因。

## 实施触点 / 文件映射

### 步骤 1 对应文件

- `Desktop/HermesDesktop.Tests/Stardew/StardewQueryServiceTests.cs`
  - 断言 `ObserveAsync` 首轮 facts 已包含 1-3 个机器可读候选目标。
- `Desktop/HermesDesktop.Tests/Runtime/NpcObservationFactStoreTests.cs`
  - 断言历史 observation 可以被记录，但旧轮次候选不会作为当前可执行候选泄漏进下一轮决策输入。
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
  - 断言 `BuildDecisionMessage` 或等价决策输入中，当前候选与历史上下文分离；上一轮候选字符串在当前轮 agent 决策输入中完全不存在，而不是只带 `stale` 标签。
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
  - 断言受控 facts 输入下，agent 会把唯一候选转成 `stardew_move` tool call，并通过 FakeCommandService/等价提交记录落成 `GameActionType.Move`。
  - 断言提交的 `GameAction.Target.LocationName/Tile` 与唯一候选 fact 完全一致。
  - 增加一个非 Haley 但同样 required `stardew-navigation` 的 NPC 覆盖，用于证明共享 skill 文本不是 Haley 专属假设。
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
  - 断言 host/ingress 仍只保留既有私聊例外链路，不新增普通 move ingress。
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueTests.cs`
  - 断言 bridge move 生命周期、blocked 边界，以及中间 tick 的 NPC tile 尚未到达最终 tile。

### 步骤 2 对应文件

- `src/games/stardew/StardewQueryService.cs`
  - Phase 1 的事实序列化总入口。
  - `BuildStatusFacts` 负责把候选目标转成固定键 facts；必要时微调 `BuildStatusSummary`，但机器可读候选以 facts 为准。
- `src/runtime/NpcObservationFactStore.cs`
  - 保持历史 fact 存储职责，但要支持执行期区分“当前 observation 候选”和“历史上下文”，避免旧候选被当成当前候选。
- `src/runtime/NpcAutonomyLoop.cs`
  - 决策消息构造落点。
  - 必须确保当前轮候选来自本次 `ObserveAsync` 结果；历史 observation 中的候选 facts 不得出现在 agent 决策输入中。
- `src/games/stardew/StardewBridgeDtos.cs`
  - 桌面侧 status DTO 扩展点。
  - 默认在 `StardewNpcStatusData` 增加候选目标字段；本轮不改 `StardewWorldSnapshotData`。
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
  - bridge 侧 status DTO 对应扩展点。
  - 与桌面侧 status DTO 保持字段对齐；本轮不改 `WorldSnapshotData`。
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
  - `BuildStatusResponse` 负责按当前 NPC 所在 location 生成 1-3 个安全候选。
  - 候选生成只能做本地安全枚举，不做目的地偏好推断，不读取普通事件或玩家命令。
  - 若需要抽辅助函数，也应仍归属于 status query 路径，而不是新建普通事件驱动入口。
- `Desktop/HermesDesktop.Tests/Stardew/StardewQueryServiceTests.cs`
  - 验证 DTO -> facts 序列化结果、候选数量、候选结构。
- `Desktop/HermesDesktop.Tests/Runtime/NpcObservationFactStoreTests.cs`
  - 验证旧候选不会污染当前轮候选。
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
  - 验证决策消息只把最新 observation 候选作为当前候选。
- `Mods/StardewHermesBridge.Tests/BridgeNpcTargetResolutionRegressionTests.cs` 或新补 bridge status 测试
  - 验证 status query 返回的候选来源边界与同 location 限制。

### 步骤 3 对应文件

- `skills/gaming/stardew-navigation.md`
  - 必须把 `player command parsing` 改成“仅既有私聊例外链路”。
  - 明确普通 autonomy 只能消费 bridge facts、既有私聊例外链路已解析目标、或已知安全测试目标。
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
  - 不作为主要业务修改点；用于测试验证 `stardew-navigation` required skill 已注入 autonomy system prompt。
  - Phase 1 默认不修改 Haley 专属 `facts.md` / `skills.json`；如果执行期证据证明必须动 persona pack，必须另列理由并只改明确文件 `src/game/stardew/personas/haley/default/facts.md` 或 `src/game/stardew/personas/haley/default/skills.json`。
- `src/games/stardew/StardewNpcTools.cs`
  - `stardew_move` 描述和参数约束落点。
  - 明确 location/tile 必须来自观察到的候选事实，不允许 agent 编造。
- `src/games/stardew/StardewAutonomyTickDebugService.cs`
  - 共享调试/ transcript 入口，确保 prompt+tool 强化后的调用证据可抓取。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
  - 审计 host/ingress 路径，确认不新增非私聊 move 驱动。
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
  - 锁定 skill 文本注入与工具调用执行行为。
  - 使用可返回 `tool_calls` 的假 chat client，断言 FakeCommandService/等价提交记录中的 `GameActionType.Move` 和目标坐标。
  - 至少覆盖 Haley 与一个非 Haley 同 required skill NPC，确认共享 `stardew-navigation` 修改不会隐含 Haley-only 规则。
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
  - 锁定 ingress 仍只保留 `scheduled_private_chat` / 既有私聊例外链路。

### 步骤 4 对应文件

- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
  - move 命令状态机主落点。
  - 从单 tick 瞬移改成 `queued -> running -> completed` / `blocked` 的多 tick 推进。
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
  - 如需补 blocked reason / progress 细节，仍沿现有 `TaskStatusData` 契约扩展。
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueTests.cs`
  - 新测试锚点，覆盖 `queued -> running -> completed`、`cross-location blocked`、非单 tick 瞬移。
  - 必须断言至少一个 `running` tick 时 NPC 世界内 tile 尚未到最终 tile；后续 tick 才推进到终点，禁止“running 一拍后 teleport”的假分步。

### 步骤 5 对应文件

- `src/games/stardew/StardewAutonomyTickDebugService.cs`
  - transcript / debug 证据收口。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
  - autonomy tick 调用链证据收口。
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
  - `commandId/traceId/status transition` 日志证据收口。
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
  - transcript 侧自动证据。
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueTests.cs`
  - bridge 生命周期自动证据。

### 步骤 5：补齐 transcript / 日志 / 手测闭环

工作内容：

- 确保桌面侧能采集到 autonomy tick 中的工具调用证据。
- 确保 bridge 日志可关联 `commandId/traceId/status transition`。
- 编写最小手测路径：Haley idle、无 menu/event、候选事实可见、触发 autonomy、观察可见移动。

完成标准：

- 三段断点都有自动证据与手测证据。
- 手测不再停留在“她说想走，但画面不动”。

## 三段断点与证据映射

### 断点 1：agent 是否选择了 `stardew_move`

对应证据：

1. `StardewAutonomyTickDebugService` / 相关 autonomy 测试中，受控事实输入下出现 `stardew_move` 工具调用
2. `StardewAutonomyTickDebugServiceTests` 使用可返回 `tool_calls` 的假 chat client，并以 FakeCommandService/等价提交记录断言 `GameActionType.Move`
3. 提交的 `GameAction.Target.LocationName/Tile` 与受控唯一候选 fact 的 `location/x/y` 完全一致
4. autonomy transcript 或调试记录能看到工具名、参数来源、调用时机
5. 不再只是自然语言“我要过去”“我去看看”，也不只是 tool surface 中存在 `stardew_move`

### 断点 2：首轮 observation 是否提供了安全候选目标

对应证据：

1. `ObserveAsync` 返回的 `GameObservation.Facts` 在首轮包含 1-3 个机器可读候选目标
2. `Desktop/HermesDesktop.Tests/Stardew/StardewQueryServiceTests.cs` 直接断言候选格式、数量、来源边界
3. 首轮决策消息可见这些候选，而不是要求先调 `stardew_status` 或新工具

### 断点 3：bridge 是否以可见、多 tick 方式执行 move

对应证据：

1. `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueTests.cs` 观察到 `queued -> running -> completed` 或 `queued -> blocked`
2. 日志可关联 `commandId/traceId` 与状态转换
3. 同一测试文件断言“至少一个中间 tick 仍为 `running`”，避免单 tick 瞬移伪通过
4. 同一测试文件断言中间 `running` tick 时 NPC 世界内 tile 尚未到最终 tile，后续 tick 才推进到终点，避免“running 一拍后 teleport”伪通过
5. 手测能看到 Haley 发生可见位置变化，而非单帧瞬移或完全无变化

## 验收标准

1. 当 NPC 处于可控、无 menu/event/blocking 的 autonomy tick 中，且首轮 `GameObservation.Facts` 含 1-3 个安全候选目标时，agent 会产生 `stardew_move` tool call，并通过测试 FakeCommandService/等价提交记录落成 `GameActionType.Move`。
2. 上述候选目标在首轮决策前已经出现在 `GameObservation.Facts`，而不是仅出现在 `stardew_status` 返回体中。
3. 候选目标只来自同 location 的动态安全候选、测试安全候选，或既有私聊例外链路；普通 host/background service 没有新增事件/命令驱动 move 的权限。
4. `stardew_move` 参数来源可以追溯到观察事实中的候选：测试中使用唯一候选 fact，并断言最终 `GameAction.Target.LocationName/Tile` 与该候选的 `location/x/y` 一一对应，而不是只断言“有 move 调用”。
5. 当前轮决策输入仅包含当前轮候选；历史候选不出现在 agent 可见候选集中，也不能以 `stale/context-only` 形式出现在 agent 决策输入中。
6. bridge 候选生成只做本地安全枚举，不做目的地偏好推断或 host 代决策。
7. bridge 对同 location、短距离 move 呈现多 tick 运行态，至少一次 `running` 可观测，且该 `running` tick 时 NPC 世界内 tile 尚未到最终 tile。
8. bridge 对跨 location move 明确返回 `blocked`，不隐式瞬移，不偷偷扩展路径规划范围。
9. 桌面 agent 与 NPC agent 继续共用同一装配链，没有出现 NPC 专用旁路。
10. 手测场景中，Haley 不再只说话；至少一次 autonomy tick 能出现真实可见移动。

## 风险与缓解

- 风险：候选事实过多，模型噪音上升。
  - 缓解：强制 1-3 个、固定结构、短距离优先。

- 风险：候选事实过少，特定位置偶发无候选。
  - 缓解：允许测试安全候选目标；动态候选先收敛到 Haley 常见 location。

- 风险：历史 observation 中的旧候选污染当前决策。
  - 缓解：当前轮候选与历史上下文分离；旧候选不得进入 agent 决策消息，只能留在 debug/log/transcript artifact，并用 runtime 测试锁住。

- 风险：bridge 候选枚举逐步滑向 host 代决策。
  - 缓解：候选生成只读本地安全信息，输出确定性短距离安全枚举；不读取普通事件、玩家命令、日程推断、记忆或 LLM 结论。

- 风险：共享 `stardew-navigation` 文本修改影响所有引用该 required skill 的 NPC，而不只 Haley。
  - 缓解：保持文本为通用 Stardew NPC 导航规则，不写 Haley-only 假设；自动测试至少覆盖 Haley 与一个非 Haley 同 skill NPC。

- 风险：bridge 多 tick 推进与现有状态槽位冲突。
  - 缓解：复用既有 `queued/running/completed` 契约，先补状态回归测试再改实现。

- 风险：把玩家命令解析重新扩大成普通 autonomy 驱动。
  - 缓解：在计划与实现中明确冻结边界，只保留既有私聊例外链路。

- 风险：Observation facts 表达最终仍不稳定。
  - 缓解：只有在证据证明该路线无法低噪音稳定表达时，才通过同一个 `StardewNpcToolFactory` 增加 `stardew_world_snapshot`；这属于升级路径，不是本轮默认范围。

## 验证步骤

1. 运行桌面测试：
   - `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug`
2. 运行 bridge 测试：
   - `dotnet test .\\Mods\\StardewHermesBridge.Tests\\Mods.StardewHermesBridge.Tests.csproj -c Debug`
3. 定向验证 Stardew 相关用例：
   - `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter "StardewAutonomyTickDebugServiceTests|StardewNpcToolFactoryTests|StardewNpcAutonomyBackgroundServiceTests|StardewQueryServiceTests"`
4. 手测：
   - 启动桌面与 Stardew bridge
   - 让 Haley 处于 idle、可控、无 menu/event 的场景
   - 触发 autonomy tick
   - 核对首轮事实中是否出现 1-3 个候选目标
   - 核对 transcript 是否调用 `stardew_move`
   - 核对日志是否出现 `queued/running/completed` 或 `blocked`
   - 观察 Haley 是否发生可见移动

## ADR

### Decision

采用“**observation-first 候选 facts + shared prompt/tool 强化 + bridge Phase 1 分步可见 move**”作为本轮修复路径；默认不先新增 `stardew_world_snapshot`，也不允许普通 host/background service 通过事件/命令驱动 NPC move。

### Drivers

1. 必须把安全候选目标放进首轮 `GameObservation.Facts`。
2. 必须在不破坏自治约束的前提下闭合三段断点。
3. 必须保持桌面 agent 与 NPC agent 同源装配。

### Alternatives considered

1. 方案 A：observation-first 候选 facts + bridge 分步 move
2. 方案 B：先上 `stardew_world_snapshot` 工具，再让 agent 主动拉取候选
3. 方案 C：只调 prompt/tool，不补首轮候选事实

### Why chosen

- 方案 A 最符合当前主链，最容易做到“首轮可见候选 -> 实际 move -> 可见执行”的最小闭环。
- 方案 B 作为升级路径有价值，但本轮会把问题复杂度前置到工具编排，不利于稳定收敛。
- 方案 C 无法满足 Architect 对首轮候选事实的硬约束，也无法可靠排障。

### Consequences

- `ObserveAsync` / 候选 facts 设计将变成 Phase 1 的关键接口。
- bridge move 需要从瞬移改成多 tick 状态推进，测试面会增加。
- 跨 location 导航被明确延后，不在本轮隐式承诺。

### Follow-ups

1. 若 observation-first 候选事实无法稳定低噪音表达，再评估同工厂新增 `stardew_world_snapshot`。
2. Haley 修通后，再评估提炼为通用 Stardew NPC 候选移动策略。
3. Phase 2 再讨论跨 location 安全导航与更复杂可见行走。

## Ralph / Team 执行建议

### Ralph 路径

- 推荐 staffing：
  - `executor`：主实现，reasoning `high`
  - `test-engineer`：补测试与断点证据，reasoning `medium`
  - `verifier`：收口验证，reasoning `high`
- 执行顺序：
  1. 失败测试先行
  2. observation facts 落地
  3. prompt/tool 强化
  4. bridge 分步 move
  5. transcript/log/手测闭环
- Ralph 验证路径：
  - 每完成一段即跑定向测试
  - 最后跑项目级测试与一次手测

### Team 路径

- 可用 agent types roster：
  - `planner`
  - `architect`
  - `critic`
  - `executor`
  - `test-engineer`
  - `verifier`
  - `debugger`
  - `explore`
- 当前 Team 运行时一次 launch 共享一个 `agentType`，因此**不建议**在本计划里直接用混合 lane 启动；本计划优先走 Ralph。
- 如果确实要用 Team 执行实现阶段，推荐单一 `agentType=executor`，由 leader 在任务文本里拆 3 个共享实施分片：
  - 分片 1：status DTO + `StardewQueryService` 候选 facts 注入
  - 分片 2：`stardew-navigation.md` / `StardewNpcTools` 收紧与 ingress 审计
  - 分片 3：`BridgeCommandQueue` 多 tick move + `BridgeMoveCommandQueueTests`
- 可实际启动命令：
  - `omx team 3:executor "按 .omx/plans/星露谷海莉NPC可见移动修复共识计划.md 执行实现；worker 1 负责 status DTO 与首轮候选 facts，worker 2 负责 navigation/tool/ingress 收紧，worker 3 负责 bridge 多 tick move 与 BridgeMoveCommandQueueTests；完成后汇总验证证据"`
- 若实现完成后还需要独立验证，再单独启动后续验证 team，而不是同一次 launch 混角色：
  - `omx team 2:verifier "验证 .omx/plans/星露谷海莉NPC可见移动修复共识计划.md 的三段断点证据、测试结果与手测记录"`
- Team 验证路径保持与 Ralph 一致：
  1. 证明首轮 facts 已含 1-3 个机器可读候选目标
  2. 证明 agent 会把候选转成 `stardew_move`
  3. 证明 move 不再瞬移，且跨 location 明确 `blocked`
  4. 证明桌面与 bridge 自动测试覆盖三段断点
  5. 汇总 transcript、日志、手测证据后再收尾

## 结论

本计划已吸收 Architect 指定的六项必改项，并把原 open questions 中的两项关键分支正式收口为：

1. Phase 1 固定为 observation-first，默认不先上新 world snapshot 工具。
2. bridge Phase 1 固定为同 location、短距离、多 tick；跨 location 先 blocked。

因此，本计划可以直接作为下一步执行的共识基线。
