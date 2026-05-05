# 星露谷 NPC 任务连续性闭包 PRD

## RALPLAN-DR 摘要

### 原则

1. 保留唯一的 Hermes 原生连续性表面：`todo` + `memory` + `session_search` + 按 NPC 划分的 session/runtime 状态。不引入第二套任务系统。
2. 保持 NPC agent 作为决策者。host/bridge 只暴露事实、工具、命令事实和日志。
3. 用行动证据而不是叙述来证明闭包：玩家承诺必须能延续到 autonomy、驱动 Stardew 工具调用、暴露终态命令事实、让 agent 更新 todo 状态，并留下运行时/UI 可见痕迹。
4. 复制参考项目的结构，不复制参考项目的包袱：继承 HermesCraft/Hermes Agent 的连续性模式，只借用 VillagerAgent 的反馈回路可见性，不引入 DAG/任务图机制。

### 决策驱动因素

1. `P0` 需要从私聊承诺到 autonomy 执行、终端反馈和可见证据的端到端闭包证明。
2. 仓库已经具备基础底座（`ToolSessionId` / `TaskSessionId`、todo 投影、重启 hydration、`stardew-task-continuity` skill），所以计划应聚焦闭包缺口，而不是重建底层。
3. OpenSpec 约束禁止第二任务存储、host 侧承诺检测、跨地图移动、公开移动坐标输入，以及 host 主导 NPC 决策。

### 可行方案

#### 方案 A：以 prompt/skill 为先的加固，辅以定向运行时诊断

- 形态：收紧 autonomy/private-chat prompt 和 Stardew skill 文案，增加/调整少量运行时证据钩子与窄范围循环行为，再用集成式测试锁定闭包。
- 优点：
  - 最符合 HermesCraft/Hermes Agent：由 agent 主导 todo 连续性，而不是由 host 编排。
  - 架构风险最低，因为复用了现有 `todo`、任务投影、autonomy loop 和运行时日志表面。
  - 实现路径最窄，也最容易回退。
- 缺点：
  - 依赖 prompt/工具契约质量；如果断言不够强，模型仍可能只叙述不行动而导致回归。
  - 可能需要一到两个严格收口的运行时钩子，才能让失败/反馈证据可被机器校验。

#### 方案 B：在 Stardew runtime 内加入 host 中介式连续性控制器

- 形态：新增运行时控制器，用来解释私聊承诺、决定何时更新 todo、注入玩家反馈，并把终态命令状态转换成任务状态迁移。
- 优点：
  - 闭包行为更确定。
  - 更容易在测试中做机械化断言。
- 缺点：
  - 会偏离当前架构和参考方向，把意图所有权从 agent 转移到 host。
  - 有演变成事实上的第二任务系统/承诺检测器的风险。
  - 会鼓励脆弱的游戏特化逻辑，而不是 Hermes 原生的连续性习惯。

#### 方案 C：叠加 VillagerAgent 风格的任务图/状态机

- 形态：保留现有 todo 底座，但增加图结构/状态机来拆解和追踪 NPC 工作。
- 优点：
  - 状态可见性更丰富。
  - `blocked` / `running` / `failed` 语义更显式。
- 缺点：
  - 对 `P0` 来说范围过大。
  - 与“唯一真相 = todo”规则冲突，并会通过间接方式重新引入第二层任务系统。
  - 会带入用户已明确拒绝的参考项目包袱。

### 推荐方案

选择 **方案 A**。

它最符合仓库的真实现状：系统已经会把私聊工具写入路由到长期 session，能在重启时 hydrate todo 状态，会把 active todo 注入 autonomy，并暴露运行时任务视图和 `runtime.jsonl`。当前缺失的不是新的调度器或控制器，而是补齐这条证明链：agent 使用 Stardew 工具推进或解释任务，看到终态 `blocked` / `failed` 命令事实，通过 `todo` / `todo_write` 自行写入简短 todo 原因，并在同一 NPC runtime 表面留下连贯、对玩家/UI 可见的证据。

## ADR 草案

### 决策

通过加固现有按 NPC 划分的 Hermes agent 循环来实现 `P0` 任务连续性闭包：私聊将承诺写入长期 `todo`，autonomy 消费同一个 active todo，Stardew 动作工具执行或失败，runtime 暴露终态命令事实，agent 通过 `todo` / `todo_write` 给同一个 todo 写入简短原因更新，而 runtime/UI 表面暴露这条生命周期，不增加第二控制器或第二任务存储。

### 驱动因素

- 现有底座已经支持私聊、autonomy、重启 hydration 和任务视图投影之间的长期任务连续性。
- 参考项目偏向标准 Hermes 工具加游戏接口，而不是游戏自有的任务编排。
- 所要求的 `P0` 验收标准以证据为导向，可以通过窄范围的 loop/prompt/log 改动加测试满足。

### 考虑过的备选方案

- 在 Stardew runtime 内加入 host 管理的连续性控制器：拒绝，因为它会把 agent 的自主性移走，并逐渐演变成承诺检测器。
- 基于 DAG/图的任务管理器叠层：拒绝，因为它重复了 `todo` 语义，且超出了 `P0` 范围。

### 选择原因

这是在保留当前已向 HermesCraft/Hermes Agent 收敛架构的前提下，满足闭包证明所需改动最小的一条路径。

### 影响

- Prompt、skill、tool-result 和 logging 契约会变得更关键，必须用真实仓库 skill 资产做回归测试。
- 当 agent 只叙述动作而不调用 Stardew 工具时，系统将有意大声失败。
- 可能需要扩展部分运行时证据，以便 UI/debug 消费者区分“已观察”“已执行动作”“已解释终态失败”。

### 后续

1. 如果 `P0` 稳定，`P1` 可以在不改变连续性架构的前提下深化世界/社交/导航知识。
2. 如果反复回归暴露出可观测性不足，应先增加结构化运行时事件，再考虑任何新的控制层。

## 范围

- 只聚焦闭包缺口。
- 保留已完成底座：
  - `ToolSessionId` / `TaskSessionId`
  - 投影进长期 NPC session 的 `todo`
  - restart hydration
  - `skills/gaming/stardew-task-continuity/SKILL.md`
- 不新增：
  - 第二任务存储
  - host 承诺检测器
  - 公开的 `stardew_move` 坐标/label 输入
  - 跨地图移动实现
  - host 代写的 NPC 决策

## 需求

### 功能需求

1. NPC 接受的私聊承诺必须继续把 `todo` 写入/更新到 `descriptor.SessionId`，而 transcript session 仍保持为 `${descriptor.SessionId}:private_chat:{conversationId}`。
2. 后续 autonomy tick 必须把现有 active todo 作为具体 prompt/runtime 上下文接收，并在自由发呆之前把它视为连续性候选项。
3. 当 autonomy 推进任务时，成功/进展必须锚定在 `stardew_move`、`stardew_task_status`、`stardew_speak` 或 `stardew_open_private_chat` 等 Stardew 动作工具上，而不是只靠叙述式最终文本。
4. 当 Stardew 命令进入终态 `blocked` 或 `failed` 时，runtime 表面可以通过 controller snapshot、`stardew_recent_activity`、prompt facts 和 append-only logs 持久化并暴露该命令事实；随后 agent/autonomy 的某一轮必须调用 `todo` / `todo_write`，用简短、事实化的 `reason` 更新同一个长期 todo。
5. 当 `blocked`/`failed` 的 todo 来自玩家承诺时，NPC 必须尝试通过 `stardew_speak` 或私聊路径提供玩家可见的反馈，并且仍由 agent 自主决定。
6. 产出的连续性证据必须能从两个运行时表面读取：
  - `NpcRuntimeSupervisor.TryGetTaskView(...)`
  - `runtime.jsonl`

### 非功能需求

1. 涉及 prompt/skill 边界的测试，在适用时必须使用仓库中的真实 gaming skill 资产。
2. 变更必须保持收口：prompt 文案、运行时证据钩子以及少量 autonomy/tool 处理调整在范围内；新编排层不在范围内。
3. 行为必须继续按 NPC / save / profile 隔离。

## 验收标准

1. 回归测试证明：私聊创建的 todo 存在于根 NPC session，并在 supervisor 重启后仍可见，而私聊 transcript session 的 task view 仍然为空。
2. 回归测试证明：看到 active todo 的 autonomy turn 至少执行一次 Stardew 动作工具调用，而不是只返回叙述。
3. 回归测试证明：终态为 `blocked` 或 `failed` 的 Stardew 命令，先作为运行时命令事实暴露，随后在稍后的一次 agent/autonomy turn 中触发：
   - 在同一个长期 session 中进行一次 `todo` / `todo_write` 更新，
   - 存储一个非空且简短的 `reason`，
   - 当任务源自玩家承诺时，尝试一次玩家反馈动作（`stardew_speak` 或私聊路径）。
4. 回归测试证明：同一条 `blocked`/`failed` 生命周期会在 `runtime.jsonl` 中以可机检的条目形式可见，而不只是出现在自由格式最终聊天文本里。
5. 回归测试证明：面向 task-view/UI 的快照能从 transcript-backed 的 `todo` tool-result projection 中暴露更新后的 `blocked`/`failed` todo 状态与原因，而不依赖直接运行时控制器 mutation，也不需要新的私聊 handle。
6. Prompt/skill 测试证明：autonomy/private-chat prompt 继续传达以下约束：
   - `todo` / `memory` / `session_search` 的职责分工，
   - 优先使用 Stardew 工具而不是叙述，
   - `blocked` / `failed` 的原因纪律，
   - 对玩家反馈的预期。
7. 一项 schema/description 回归测试证明 `stardew_move` 仍保持只接受 `destinationId` 的公开工具契约：
   - 允许的公开输入：语义化 `destinationId`；
   - 禁止的公开输入：`label`、`x`、`y`、`tile`、`facingDirection` 或任何坐标/朝向替代项。

## 实施步骤

### 步骤 1：用端到端测试基线化闭包缺口

文件：
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`

意图：
- 新增或收紧当前只停留在底座证明的测试，使其断言缺失的闭包：真实工具推进、终态 todo 更新、玩家反馈尝试，以及运行时/UI 证据。

验收：
- 新的失败测试能隔离剩余缺口，而不引入新的基础设施假设。

### 步骤 2：在 agent 边界收紧 autonomy/private-chat 连续性契约

文件：
- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `skills/gaming/stardew-task-continuity/SKILL.md`
- `skills/gaming/stardew-social.md`
- `skills/gaming/stardew-navigation.md`

意图：
- 保留现有中文连续性指导，同时强化以下最小行为契约：
  - 从 active todo 恢复玩家承诺，
  - 使用 Stardew 工具而不是叙述，
  - 用简短原因更新 `blocked`/`failed`，
  - 当 `blocked`/`failed` 的任务是对玩家的承诺时，尝试提供可见的玩家反馈。
- 增加一条机械性保护：确保 `stardew_move` 的公开 schema/description 仍只指导使用语义化 `destinationId`，不暴露 label、tile、坐标或朝向字段。

验收：
- Prompt/skill 测试表明所需文案仍然存在，且由仓库资产支撑。
- 一旦 `stardew_move` 从语义化 destination ID 回退到 label、坐标、tile 或朝向输入，工具契约测试就会失败。

### 步骤 3：补上终态命令事实与 todo 事实汇合处的 runtime loop

文件：
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcRuntimeBindings.cs`
- `src/runtime/NpcRuntimeInstance.cs`

意图：
- 确保权威性的 Stardew 命令结果路径只作为运行时命令事实被捕获。
- 再通过 controller snapshot、prompt facts 和 `stardew_recent_activity`，把这些事实反馈给后续 agent/autonomy 的决策表面，让 agent 自己决定是否调用 `todo` / `todo_write`、`stardew_speak` 或私聊。
- 保持“任务事实”由 agent 编写并以 transcript 为后盾；runtime/background 代码不得直接因为终态命令状态而 mutation todo 状态。

验收：
- `blocked`/`failed` 动作会先作为运行时命令事实可见，然后只有在 agent 通过现有 tool-result projection 写入 todo 更新之后，才产生匹配的 task-view 状态。

### 步骤 4：让运行时证据成为一等且可机器校验的对象

文件：
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcRuntimeLogWriter.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`

意图：
- 扩展或标准化 `runtime.jsonl` 条目，使审查者可以追踪：
  - tick 观察到 active task
  - 已提交动作工具
  - 终态 `blocked`/`failed` 命令结果
  - 后续 todo 更新工具结果
  - 是否发出了反馈尝试
- 保持它只是日志证据，而不是第二份任务记录。

最小 append-only 事件词汇表：
- `observed_active_todo`
- `action_submitted`
- `command_terminal`
- `todo_update_tool_result`
- `feedback_attempted`
- `feedback_missing`

事件含义：
- `observed_active_todo`：当前 tick/prompt 消费了一个 active 的根 session todo。
- `action_submitted`：NPC 提交了一条 Stardew 动作命令，例如 move/speak/open-chat。
- `command_terminal`：runtime 观察到了终态命令事实，例如 `blocked`、`failed`、`completed` 或 `cancelled`。
- `todo_update_tool_result`：agent 随后使用 `todo` / `todo_write`，产出了以 transcript 为后盾的任务事实更新。
- `feedback_attempted`：在面向玩家的 `blocked`/`failed` 结果后，agent 尝试了 `stardew_speak` 或私聊反馈。
- `feedback_missing`：仅用于非玩家承诺任务、工具不可用负向测试或显式失败观察测试的诊断事件；它不能满足主线的玩家承诺 `blocked`/`failed` 验收路径。

最小 `NpcRuntimeLogRecord` 映射：
- 对这些闭包证据事件，`ActionType = "task_continuity"`。
- `Target` 为上述词汇表值之一。
- `Stage` 承载生命周期阶段：`observed`、`submitted`、`terminal`、`task_written`、`feedback` 或 `diagnostic`。
- `Result` 承载机器状态：`active`、`submitted`、`completed`、`blocked`、`failed`、`attempted` 或 `missing`。
- 可用时，动作/命令事件需填充 `CommandId`。
- `Error` 可以包含简短原因/细节字符串，但测试必须先断言结构化字段。

轮次边界规则：
- “稍后的 agent/autonomy turn” 意味着“不是 background service，也不是任何 host/runtime 代码直接 mutation todo”。
- todo 更新可以发生在同一条 Agent chat/tool loop 中，即在 Stardew tool result 之后；也可以发生在后续 autonomy tick 中，即在 `LastTerminalCommandStatus` / `stardew_recent_activity` 暴露命令事实之后。
- 被禁止的路径是 runtime/controller/background 代码直接写 `SessionTodoStore` 或伪造一条 `todo` tool result。

验收：
- 测试可以对 `P0` 闭包路径的精确运行时日志记录做断言。

### 步骤 5：对齐面向 UI 的任务快照与运行时证据

文件：
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`

意图：
- 验证 task-view 快照能暴露与运行时动作/日志中一致的 `blocked`/`failed` 状态和原因，包括重启之后，以及在尚未创建任何新私聊 handle 之前。

验收：
- UI 消费方只通过现有 `TryGetTaskView` API 就能读取闭包状态。

## 风险与缓解

### 风险：模型仍然只叙述而不行动

- 缓解：
  - 强化 prompt/skill 文案，
  - 为“未使用动作工具”和“未调用 `stardew_move` 却叙述移动”增加诊断，
  - 要求测试断言工具调用，而不只是最终文本。

### 风险：todo 原因更新偏离真实命令失败

- 缓解：
  - 将命令结果路径视为权威来源，
  - 直接测试从 tool result 到任务快照的 `blocked`/`failed` 映射。

### 风险：玩家反馈退化为 host 代写的兜底行为

- 缓解：
  - 保持反馈是对 agent 工具使用的预期；
  - 对于向玩家承诺过的 `blocked`/`failed` 任务，只有 agent 编写的 `feedback_attempted` 才能构成通过路径；
  - `feedback_missing` 只用于诊断/负向测试事件，不能被接受为玩家承诺闭包；
  - host 不得伪造 NPC 发言。

### 风险：可观测性增强悄悄演变成新的状态系统

- 缓解：
  - 运行时证据仍然只是 append-only 日志加现有任务快照；
  - 不新增持久化模型或调度器。

### 风险：参考借鉴漂移成 DAG/任务图扩张

- 缓解：
  - 明确将对 VillagerAgent 的借鉴限制为“只借用反馈回路/状态可见性”。

## 验证

### 定向测试命令

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~NpcRuntimeSupervisorTests|FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewAutonomyTickDebugServiceTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests"
```

### 次级兜底

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

### 证据审查

1. 确认 todo 落在根 session，而不是私聊 transcript session。
2. 确认 autonomy tick 会为 active todo 连续性发起 Stardew 动作工具调用。
3. 确认 `blocked`/`failed` 命令事实会在任何 todo 更新之前先出现在运行时证据中。
4. 确认稍后的 agent/autonomy turn 会通过 `todo` / `todo_write` 更新 todo 的 `status` + `reason`。
5. 确认面向玩家承诺的 `blocked`/`failed` 任务会在工具调用或运行时日志证据中包含 `feedback_attempted`；主路径上不接受 `feedback_missing`。
6. 确认 `runtime.jsonl` 与 `TryGetTaskView` 对闭包结果达成一致，且 task-view 状态来源于 transcript-backed 的 todo 投影。
7. 确认 `stardew_move` schema/description 仍只暴露语义化 `destinationId`，且不暴露 `label`、坐标、tile 字段或 `facingDirection`。

## 执行分工

### 可用 Agent 类型

- `planner`
- `architect`
- `critic`
- `executor`
- `debugger`
- `test-engineer`
- `verifier`
- `explore`

### Ralph 串行路径

推荐场景：
- 团队希望由单一 owner 保持 prompts、tools、runtime logs 和 tests 之间的连续性模型；
- 像 `NpcAutonomyLoop.cs` 和 `StardewNpcTools.cs` 这样的共享文件使并行编辑容易冲突。

建议分道顺序：
1. 高推理 `executor`：补上失败的闭包测试。
2. 高推理 `executor`：实现窄范围 runtime/prompt/tool 调整。
3. 中推理 `test-engineer`：加固测试矩阵和真实 skill 资产覆盖。
4. 高推理 `verifier`：运行定向测试集，检查日志/任务快照，然后跑完整 desktop 测试集。

启动提示：
- `$ralph implement .omx/plans/prd-stardew-npc-task-continuity-closure.md with .omx/plans/test-spec-stardew-npc-task-continuity-closure.md`

### Team 并行路径

推荐场景：
- 你想通过拆分测试设计、运行时证据映射和 prompt/skill 契约审查来缩短周期。

建议编组：
1. Lane A：高推理 `executor`
   - 负责 `NpcAutonomyLoop.cs`、`StardewNpcTools.cs`、`StardewNpcAutonomyBackgroundService.cs`
2. Lane B：中推理 `test-engineer`
   - 负责 `NpcAutonomyLoopTests`、`NpcRuntimeSupervisorTests`、`StardewNpcAutonomyBackgroundServiceTests`
3. Lane C：中推理 `executor` / `writer`
   - 负责 `stardew-task-continuity`、`stardew-social`、`stardew-navigation`、private-chat 文案检查
4. Lane D：高推理 `verifier`
   - 负责定向命令执行、运行时证据检查和合并前验证摘要

启动提示：
- `omx team run .omx/plans/prd-stardew-npc-task-continuity-closure.md`
- 或 `$team execute .omx/plans/prd-stardew-npc-task-continuity-closure.md`

### Team 验证路径

1. 由 Lane B 先落失败测试。
2. 由 Lane A 针对这些测试落 runtime/tool 修复。
3. 由 Lane C 确认 prompt/skill 断言仍使用仓库资产，且没有出现被禁止的架构漂移。
4. 由 Lane D 跑定向测试集，然后跑完整 `HermesDesktop.Tests`，并对以下内容做证据审计：
   - 任务快照，
   - 工具调用，
   - `runtime.jsonl` 记录。
