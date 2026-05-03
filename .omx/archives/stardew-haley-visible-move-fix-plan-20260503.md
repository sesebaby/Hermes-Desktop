# 星露谷海莉 NPC 可见移动修复计划草案

## 计划类型

- 类型：RALPLAN 共识流程 - Planner 草案
- 范围级别：中等，跨 `autonomy prompt/facts/tool surface` 与 `SMAPI bridge move execution`
- 目标问题：修复“海莉可以私聊、也能主动说话，但始终没有可见移动”

## 需求摘要

- 必须继续由 `NpcAutonomyLoop` 把观察事实交给 agent 决策，不能引入非私聊事件驱动的 NPC 移动执行分支。
- 如果需要提升移动概率，只能通过提示词、工具描述、观察事实、候选事实来影响 agent 的自主选择。
- host/background service 不得代 NPC 决定去哪，只能维护 runtime、工具装配、命令状态推进与资源协调。
- 桌面 agent 与 NPC agent 保持同源，不新增第二套 NPC 专用 agent 装配链。
- 计划必须同时覆盖三类问题：
  1. agent 不选 `move`
  2. `move` 缺少安全目标事实
  3. bridge `move` 当前是瞬移/不可见

## 现有证据

- `NpcAutonomyLoop` 当前只把 observation/event facts 组装成决策消息，不直接发动作；消息明确要求“facts only / events are context only”：`src/runtime/NpcAutonomyLoop.cs:95`, `src/runtime/NpcAutonomyLoop.cs:123`
- NPC autonomy 与 private chat 都走同一套 agent 装配：共享 `AgentCapabilityAssembler`、同一 `NpcRuntimeAutonomyBindingRequest` / `Services` 注入路径，而非第二套 NPC 专用 agent：`src/runtime/AgentCapabilityAssembler.cs:14`, `src/games/stardew/StardewAutonomyTickDebugService.cs:196`, `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:392`, `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs:51`
- autonomy prompt supplement 会把 persona facts / voice / boundaries 和必需游戏技能注入系统提示，因此允许通过 persona 与 skill 文本影响 agent 决策：`src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs:64`, `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs:111`
- Haley 当前 Phase 1 只有 `move`、`speak` 两个能力，且必需技能包含 `stardew-core`、`stardew-social`、`stardew-navigation`：`src/game/stardew/personas/haley/default/facts.md:5`, `src/game/stardew/personas/haley/default/manifest.json:21`, `src/game/stardew/personas/haley/default/skills.json:2`
- `stardew-navigation` 已规定目标必须来自 bridge facts、玩家命令解析或已知安全测试目标，不能让 agent 瞎编导航：`skills/gaming/stardew-navigation.md:5`
- `stardew_move` 目前的工具描述较弱，只要求 tile 参数，未强调“当存在安全候选目标时优先移动而不是口头承诺要移动”：`src/games/stardew/StardewNpcTools.cs:89`
- `stardew_status` 当前只暴露位置、是否在移动、是否可控、当前 commandId 等基础 facts，没有候选目标或 move affordance：`src/games/stardew/StardewQueryService.cs:90`
- world snapshot 通路已经存在，但当前只返回很薄的 world facts（location/menu/event），未向 autonomy 工具面暴露：`src/games/stardew/StardewQueryService.cs:51`, `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs:186`, `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs:310`
- background service 会在动作 in-flight 时暂停 autonomy 并维护 claim / command 状态，但它不负责决定新的移动目标：`src/games/stardew/StardewNpcAutonomyBackgroundService.cs:363`, `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:583`
- bridge 收到 move 后当前仅入队，然后在 `PumpOneTick` 中直接 `setTilePosition` + `addCharacter` 完成，属于瞬移，没有逐步可见路径：`Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:39`, `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:310`
- 现有测试只覆盖 move 合约绑定、状态轮询和 claim 生命周期，没有覆盖“agent 选择 move”或“bridge 可见行走”：`Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs:34`

## RALPLAN-DR Short Summary

### 原则

1. 保持 NPC 自主性：host 只能提供事实、工具和状态推进，不能替 agent 做目的地决策。
2. 同源装配优先：桌面 agent 与 NPC agent 继续共用能力装配、prompt 注入和工具表面。
3. 先给可选的安全目标，再要求 agent 选择 `move`；不能只靠“更强的提示词”赌模型自己编坐标。
4. `move` 必须是可诊断的长任务语义，而不是一次性瞬移，至少要能观察到 queued/running/completed 或 blocked。
5. Phase 1 只做最小真实闭环：短距离、可见、低风险移动，不扩展到复杂 town schedule / follow / farming。

### 决策驱动 Top 3

1. 严格满足约束：不新增事件驱动替 NPC 决策的移动逻辑，不拆出 NPC 专用 agent 链。
2. 端到端修复真实断点：同时解决“为什么不选 move”“选了 move 去哪”“bridge 如何可见执行”。
3. 测试与手测可证明：日志、命令状态、位置变化、autonomy transcript 都能说明问题发生在哪一层。

### 可行方案

#### 方案 A：扩展现有 `stardew_status`/观察事实 + 强化提示/工具描述 + bridge 分步可见移动

- 做法：
  - 在现有 observation/status facts 中加入安全候选目标与 move affordance。
  - 调整 `stardew-navigation`、persona facts、`stardew_move` 描述，明确“有安全目标且适合 reposition 时，应调用 `stardew_move`，不要只说要走”。
  - bridge 把 move 从瞬移改为可见分步执行，保留同一 `move` 命令契约和状态轮询。
- 优点：
  - 改动路径最短，保持 agent 工具表面简单。
  - 与现有 autonomy loop 和 `stardew_status` 认知模型最一致。
  - 更容易在现有 debug/autonomy 测试框架中锁定回归。
- 缺点：
  - `stardew_status` 语义会变重，需要控制候选目标数量与格式，避免提示噪音。
  - 如果后续需要更丰富世界上下文，可能还要再引入独立 world snapshot 工具。

#### 方案 B：新增显式 `stardew_world_snapshot` 或候选目标工具 + 轻量状态 facts + bridge 分步可见移动

- 做法：
  - 保持 `stardew_status` 轻量，仅报告当前状态。
  - 新增 NPC-safe 世界快照/候选目标工具，让 agent 需要时主动拉取安全目的地。
  - 同样强化 `stardew_move` 描述，并把 bridge 改成可见分步移动。
- 优点：
  - 事实分层更清晰，候选目标和当前状态分离。
  - 为后续更多 NPC/世界交互留出更干净的扩展面。
- 缺点：
  - agent 需要多一步工具调用，Phase 1 里可能反而降低 move 被选中的概率。
  - 需要新增 NPC-safe 工具与测试面，范围略大。

#### 方案 C：只调提示词/工具描述 + bridge 可见移动

- 做法：不补安全目标事实，只靠 prompt/tool description 诱导 agent 选 `move`。
- 优点：实现表面最小。
- 缺点：
  - 违反当前导航技能对目标来源的要求。
  - 不能系统性修复“有 move 但无安全目标”的根因。
  - 高概率把问题从“不移动”变成“瞎猜坐标”或重复 `speak`。
- 结论：不推荐，作为无效备选明确淘汰。

## 推荐方案

- 推荐：**方案 A，必要时预留向方案 B 演进的接口，但本轮不先新增独立 world tool。**

### 推荐理由

- 它最符合现有 autonomy loop 的认知路径：先 `ObserveAsync` 获取当前 facts，再由同源 agent 在同一次 tick 内决定是否调用 `stardew_move`。
- 它能在不新增第二套 agent 装配链的前提下，同时处理三类缺陷：
  - 用 prompt/persona/tool description 修正“只说要走，不实际调 move”
  - 用 status/observation facts 提供“安全可选目标”
  - 用 bridge 分步执行修正“move 看起来像瞬移/没发生”
- world snapshot 管道已存在，可以作为 status facts 的数据来源或后续演进面，而不必本轮就把 autonomy 工具面复杂化：`src/games/stardew/StardewQueryService.cs:51`, `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs:186`

## 范围内 / 范围外

### 范围内

- 调整 autonomy prompt supplement 所注入的 persona/skills/tool 描述，使 `move` 成为被鼓励但仍由 agent 自主决定的动作。
- 为 Haley 或通用 Stardew NPC status/world facts 增加“安全候选目标”表达。
- 把 bridge move 从瞬移改成最小可见、可诊断的分步移动执行。
- 增补自动测试与手测脚本，分别锁定：
  - agent 选择 `move`
  - facts 提供安全目的地
  - bridge move 产生 running/completed/blocked 以及可见位置变化

### 范围外

- 新增非私聊事件驱动的 host 决策器、schedule engine 或 follow/goto 自动机。
- 跨地图长路径、高级 pathfinding、复杂 town 巡逻、耕作/交互/采集行为。
- 为 NPC 单独维护一套与桌面 agent 分离的 prompt、tool registry、runtime 进程。
- 修改私聊主流程本身；私聊只作为现有允许的例外入口继续存在。

## 涉及文件

- `src/runtime/NpcAutonomyLoop.cs`
  - 决策消息格式与事实注入边界：`src/runtime/NpcAutonomyLoop.cs:95`, `src/runtime/NpcAutonomyLoop.cs:123`
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
  - persona/required skills 注入位置：`src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs:64`
- `skills/gaming/stardew-core.md`
  - observe-decide-act 基线：`skills/gaming/stardew-core.md:3`
- `skills/gaming/stardew-navigation.md`
  - move 目标来源约束：`skills/gaming/stardew-navigation.md:5`
- `src/game/stardew/personas/haley/default/facts.md`
  - Haley 能力与 persona facts：`src/game/stardew/personas/haley/default/facts.md:5`
- `src/game/stardew/personas/haley/default/skills.json`
  - 必需技能集合：`src/game/stardew/personas/haley/default/skills.json:2`
- `src/games/stardew/StardewNpcTools.cs`
  - `stardew_status` / `stardew_move` 工具描述与返回面：`src/games/stardew/StardewNpcTools.cs:32`, `src/games/stardew/StardewNpcTools.cs:64`, `src/games/stardew/StardewNpcTools.cs:91`
- `src/games/stardew/StardewQueryService.cs`
  - observation/world snapshot facts 构造：`src/games/stardew/StardewQueryService.cs:24`, `src/games/stardew/StardewQueryService.cs:51`, `src/games/stardew/StardewQueryService.cs:95`
- `src/games/stardew/StardewBridgeDtos.cs`
  - world/status DTO 能力边界：`src/games/stardew/StardewBridgeDtos.cs:98`, `src/games/stardew/StardewBridgeDtos.cs:117`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
  - world snapshot 当前事实过薄：`Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs:186`, `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs:310`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
  - move 入队、状态与瞬移实现：`Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:39`, `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:310`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
  - host 负责状态推进而不负责目的地决策：`src/games/stardew/StardewNpcAutonomyBackgroundService.cs:363`, `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:392`, `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:583`
- 测试锚点
  - `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs:56`
  - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs:34`
  - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs:530`
  - `Desktop/HermesDesktop.Tests/Stardew/StardewQueryServiceTests.cs:91`
  - `Mods/StardewHermesBridge.Tests/*`（新增 bridge 可见移动测试）

## 可测试验收标准

1. 在 deterministic autonomy/debug 测试中，当 observation/status facts 含有至少一个安全候选目标，且无 menu/event/blocking 时，Haley 的 autonomy tick 会产生一次 `stardew_move` 工具调用，而不是仅输出“要去某处”的自然语言。
2. `stardew_move` 的可用目标必须来自观察事实、world snapshot facts 或测试安全目标；测试中不存在 agent 自行编造 location/tile 的通过路径。
3. `stardew_status`（或本轮选定的事实来源）返回的 facts 中，至少包含当前 location/tile、可控状态，以及一个机器可读的候选移动目标表达。
4. bridge move 在日志与状态上至少表现为：`queued -> running -> completed` 或 `queued -> blocked/failed`；`commandId`、`traceId`、失败原因在日志可关联。
5. bridge move 执行不再是单 tick 瞬移完成；自动测试或集成测试能观测到至少一个中间运行态，手测能看到海莉发生可见位置变化。
6. autonomy host/background service 仍不直接决定 `locationName/x/y`；代码审查可追溯到目的地只从 agent 工具调用参数进入 bridge。
7. 桌面 private chat agent 与 autonomy agent 继续共用相同的 built-in tool registration / prompt assembly 主链，未出现 NPC 专用旁路装配。

## 实施步骤

1. 先补失败测试，锁定三段断点。
   - 在 autonomy/debug 测试中构造“Haley 有安全候选目标但当前 idle”的事实，断言会选 `stardew_move`。
   - 在 query/status 测试中断言候选目标 facts 会被输出。
   - 在 bridge tests 中断言 move 不是单步 `completed`，而会进入可见 running 阶段。

2. 设计并实现“安全候选目标事实”最小格式。
   - 优先复用现有 `stardew_status` / `ObserveAsync` 路径，把候选目标压缩成少量、稳定、可解析的 facts。
   - 目标来源优先级：
     - bridge 动态发现的当前 location 内安全 tile
     - 玩家命令解析结果
     - 测试专用安全目标
   - 明确禁止 host 在此阶段自行挑“现在就去哪里”；host 只暴露候选列表或 affordance。

3. 调整 prompt/skills/tool description，提升 `move` 被选中的概率。
   - 更新 `stardew-navigation` 与相关 persona facts，明确“当存在安全候选目标且有 reposition 意图时，直接调用 `stardew_move`，不要只 verbalize intent”。
   - 强化 `stardew_move` 描述，要求参数必须来自观察到的候选目标，并在合适时优先行动而非空谈。
   - 保持 shared prompt assembly，不引入 NPC 专用 agent 分支。

4. 把 bridge move 改成最小可见的分步执行状态机。
   - 保留现有 command queue / command status 契约。
   - 将 `PumpOneTick` 中的 move 处理拆成“入队后 running，多 tick 推进，结束时 completed/blocked/failed”。
   - Phase 1 只保证短距离、同 location、可见挪动；跨 location 可先定义为受限或显式 blocked，避免一次计划同时吞掉复杂 pathfinding。

5. 补齐日志、状态和人工验证闭环。
   - Desktop/core transcript 需能看出 agent 是否调用了 `stardew_move`。
   - bridge 日志需能看出命令是否入队、开始、阻塞、完成。
   - 手测脚本要覆盖“海莉 idle -> 看到安全目标 -> 触发 autonomy tick -> 可见移动”。

## 风险与缓解

- 风险：只改 prompt 仍可能不选 `move`。
  - 缓解：把“安全候选目标存在”做成明确可见 facts，并在测试里用可控 chat client / transcript 断言工具调用。

- 风险：候选目标 facts 太啰嗦，反而污染模型决策。
  - 缓解：限制为 1-3 个近距离、命名稳定的候选；采用固定键格式，而不是大段自然语言。

- 风险：bridge 可见移动需要 pathfinding，范围失控。
  - 缓解：Phase 1 先限定同 location 短距离分步移动；跨 location 明确 blocked 或留到后续。

- 风险：分步移动与现有 claim / pending action 状态不一致。
  - 缓解：复用现有 `PendingWorkItem`、`ActionSlot`、`TryAdvancePendingActionAsync` 轮询模型，先对 running/completed/timeout 补测试再动实现。

- 风险：动态安全 tile 发现不稳定，手测地点偶发 blocked。
  - 缓解：先为 Haley 当前常见 location 定义 bridge 侧动态筛选规则和少量测试安全目标；避免硬编码跨地图复杂导航。

## 验证步骤

1. 运行桌面核心测试：
   - `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug`
2. 运行 bridge 测试：
   - `dotnet test .\\Mods\\StardewHermesBridge.Tests\\Mods.StardewHermesBridge.Tests.csproj -c Debug`
3. 运行定向测试筛选：
   - `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter "StardewAutonomyTickDebugServiceTests|StardewNpcToolFactoryTests|StardewNpcAutonomyBackgroundServiceTests|StardewQueryServiceTests"`
4. 手测：
   - 启动桌面与 Stardew bridge
   - 让 Haley 处于 idle、可控、无 menu/event 的场景
   - 触发 autonomy tick
   - 核对 transcript 是否调用 `stardew_move`
   - 核对 SMAPI/bridge 日志是否出现 move queued/running/completed
   - 观察 Haley 是否发生可见移动，而非仅说话或瞬移

## ADR 草案

### Decision

采用“**现有 status/observation facts 扩展 + shared prompt/tool 强化 + bridge 分步可见 move**”作为 Phase 1 修复路径，不引入 host 决策移动，也不拆分 NPC 专用 agent 装配链。

### Drivers

- 必须遵守 autonomy/self-decision 约束。
- 必须端到端修复 agent 选择、目标事实、bridge 执行三个层面的断点。
- 必须在当前 shared runtime/tool architecture 内完成，且可通过测试和日志证明。

### Alternatives considered

- 方案 A：扩展现有 `stardew_status`/观察 facts 并强化 prompt/tool，再把 bridge move 改成分步可见。
- 方案 B：新增独立 `stardew_world_snapshot`/候选目标工具，再把 bridge move 改成分步可见。
- 方案 C：仅改 prompt/tool description 与 bridge move，不补安全候选目标事实。

### Why chosen

- 方案 A 最贴合现有 `ObserveAsync -> facts -> autonomy tick -> shared tool call` 主链。
- 方案 A 对用户约束最稳：host 只提供事实，不决定目标；agent 仍在同源工具表面里自主选择。
- 方案 B 可作为后续演进，但本轮会扩大 agent 工具复杂度，不利于先把“海莉不动”修透。
- 方案 C 无法解决“安全目标从哪里来”的根因，风险过高。

### Consequences

- `stardew_status` 或 observation facts 的职责会略微增重，需要控制格式与体积。
- bridge move 从单 tick 完成变成 in-flight 命令，相关状态测试会增多。
- Phase 1 可能只承诺同 location 短距离可见移动，不直接覆盖跨地图复杂导航。

### Follow-ups

- 如果方案 A 的 facts 体积或表达受限，再引入独立 `stardew_world_snapshot` 工具作为 Phase 2。
- 在 Haley 验证通过后，抽象为通用 Stardew NPC 候选移动目标策略。
- 评估跨 location move 的最小安全实现边界。

## 后续执行建议

### Ralph 路径

- 推荐角色：
  - `executor`：主实现，reasoning `high`
  - `test-engineer`：补测试与验收脚本，reasoning `medium`
  - `verifier`：串行收尾验证，reasoning `high`
- 执行方式：
  - 由 Ralph 单 owner 顺序推进：先失败测试，再 facts/prompt，再 bridge move，再回归验证。
- 验证路径：
  - 每完成一层先跑定向测试，再跑对应项目全量测试。
  - 最后进行一次手测与日志核对，确保三个断点都闭合。

### Team 路径

- 可用 agent types roster：
  - `planner`, `architect`, `critic`, `executor`, `test-engineer`, `verifier`, `debugger`, `explore`
- 建议分工：
  - Lane 1 `executor`：autonomy facts/prompt/tool surface，reasoning `high`
  - Lane 2 `executor` / `debugger`：bridge 分步 move 状态机与日志，reasoning `high`
  - Lane 3 `test-engineer`：桌面 + bridge 测试补齐，reasoning `medium`
  - Lane 4 `verifier`：整合回归、手测脚本、日志证据，reasoning `high`
- 团队验证路径：
  - Lane 1 证明 agent 在受控事实上会选 `stardew_move`
  - Lane 2 证明 move 不再瞬移且有 running/completed/blocked 诊断
  - Lane 3 证明桌面/bridge 自动测试全绿
  - Lane 4 汇总 transcript、日志、手测结果后再关停团队
- 启动提示：
  - `omx team "执行 .omx/plans/stardew-haley-visible-move-fix-plan-20260503.md"`
  - 或 `$team 执行 .omx/plans/stardew-haley-visible-move-fix-plan-20260503.md`

## 开放问题

- Phase 1 是否把跨 location move 明确判为 blocked，只先承诺同 location 短距离可见移动。
- “安全候选目标”是否优先塞进 `stardew_status` facts，还是需要在实现阶段证据表明必须拆为独立 world snapshot 工具。

