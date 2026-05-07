# Stardew NPC Local Executor Minimal Test Consensus Plan

## Requirements Summary

目标是在当前 Stardew NPC autonomy 链路上落一个最小 v1，本轮只验证把低风险动作 `move / observe / wait / task_status` 通过本地/委派小模型执行层路由出去，而不是继续让主 NPC agent 直接执行这些细粒度动作。

已核对的当前代码事实：

- `src/runtime/NpcAutonomyLoop.cs:133-147` 当前构造 `decisionMessage` 后直接调用 `_agent.ChatAsync(...)`。
- `src/runtime/NpcAutonomyLoop.cs:212-221` 当前 autonomy prompt 明确告诉主 agent “需要移动就用 `stardew_move`，长动作开始后用 `stardew_task_status` 查进度”。
- `src/Core/Agent.cs` 的 `ChatAsync(...)` 会在返回前完成 tool loop；如果主 autonomy agent 仍能看到 `stardew_move`，它可能在 `NpcAutonomyLoop` 接回结果前就已经直接执行低风险工具。因此 hard routing 必须发生在主 agent 工具暴露之前，而不是 `_agent.ChatAsync(...)` 之后。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:520-525` 当前通过 `NpcRuntimeCompositionServices` 同时传入 `_chatClient` 和 `_delegationChatClient`。
- `src/runtime/NpcRuntimeSupervisor.cs:307-342` 创建 NPC agent handle 时，父 agent 使用 `services.ChatClient`，并把 `DelegationChatClient` 传入 capability 注册。
- `src/runtime/AgentCapabilityAssembler.cs:44-51` 当前 `AgentTool` 已用 `services.DelegationChatClient ?? services.ChatClient` 注册，说明 subagent lane 已可走 delegation client。
- `src/games/stardew/StardewNpcTools.cs:35-45` 当前已注册 `StardewMoveTool`、`StardewTaskStatusTool` 等真实工具。
- 现有测试基座已覆盖 `NpcAutonomyLoop`、`NpcRuntimeSupervisor`、`StardewNpcAutonomyBackgroundService` 和 `AgentTool` delegation wiring，可直接扩展，而不必新造测试 harness。

## RALPLAN-DR Summary

### Principles

1. 主 agent 保持高层决策，只输出短意图，不再直接承担低层动作工具参数。
2. 低风险动作用代码层硬路由，不依赖提示词“希望模型自觉委派”。
3. v1 只验证最小闭环，不把本地执行层扩张成第二个 NPC 大脑。
4. 复用现有 delegation wiring、Stardew 工具和 runtime log 路径，不新增依赖、不重做架构。
5. 先用测试和日志证明“路由发生了”，再谈更广动作面或人格层收敛。

### Top Decision Drivers

1. 成本与上下文：高频动作级推理不能持续占用主 agent 上下文和云模型调用。
2. 架构现实：当前仓库已经具备 `_delegationChatClient`、`AgentTool` delegation lane、`stardew_move`/`stardew_task_status` 工具，可在现有链路上做最小切入。
3. 风险控制：用户要求的是 minimal v1 test，必须把范围收窄到低风险动作和可观测证据链。

### Viable Options

#### Option A: 轻量双工具面 hard-routing seam

Pros:

- 仍保持 minimal v1：不先做完整执行框架，只建立必要的工具面边界和本地 executor runner。
- 从结构上剥夺主 autonomy agent 对 `stardew_move` / `stardew_task_status` 的直接访问，满足 hard routing。
- 可以利用现有 `NpcAutonomyLoopTests`、`NpcRuntimeSupervisorTests` 和 Stardew autonomy tests 做验证。

Cons:

- 比“在 `_agent.ChatAsync(...)` 返回后插分支”多一点 wiring，需要拆分 parent tool surface 与 local executor tool surface。
- `NpcAutonomyLoop` 仍会承担一部分 v1 编排职责，后续成功后应抽成独立服务。

#### Option B: 新增独立 `LocalNpcExecutor` / `NpcActionExecutor` 服务，并由 `NpcAutonomyLoop` 调用

Pros:

- 结构更清晰，职责边界更好。
- 后续扩展 `observe/wait/retry/escalate` 更自然。

Cons:

- v1 就要引入新类型、新接口、新 wiring，范围比最小验证更大。
- 更容易把“最小验证”做成新的半套架构。

#### Option C: 只通过 prompt 约束主 agent 改为调用现有 `agent` 工具或 delegation lane

Pros:

- 表面代码改动最少。

Cons:

- 不满足“硬路由”目标。
- 主 agent 仍可能继续直接调 `stardew_move`，结果不稳定。
- 无法可靠缩短主上下文与控制成本。

### Recommended Option

选择修正后的 Option A：轻量双工具面 hard-routing seam，并在实现时保留向 Option B 演进的接口边界。

原因：

- 它最符合“minimal v1 to test routing”。
- 当前关键事实都集中在 `NpcRuntimeSupervisor` 的工具面组合、`NpcAutonomyLoop` 的决策入口和现有 delegation wiring 上。
- 先证明 `intent -> local executor -> stardew_move/task_status` 证据链成立，再决定是否抽独立服务，能避免 v1 过早设计。
- 它修正了关键 seam：hard routing 必须在主 agent 工具暴露之前完成，不能等 `Agent.ChatAsync(...)` 返回后再接管。

## Implementation Plan

### Step 1: 先锁定现状与目标行为的回归测试

目标文件：

- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`（参考现有 delegation fake；本轮主要断言仍放在 autonomy/runtime 测试）
- 可选新增 `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRoutingTests.cs`，若把新断言塞进现有文件会过于噪音。

实施内容：

- 为 autonomy tick 增加测试，锁定当前事实：主 agent 产生高层响应后，由本地执行层而不是主 agent 直接触发 `stardew_move`。
- 增加 parent intent contract 测试：parent final response 只接受单个 JSON object；非 JSON、缺少 required field、越权 action 都进入 rejected/escalated path。
- 增加测试验证低风险动作集仅包含 `move / observe / wait / task_status`。
- 增加测试验证高风险动作如 `gift / trade / use item / relationship-impacting` 不会走本地执行层。
- 增加测试验证本地执行层调用使用 delegation client，而不是主 `_chatClient`。
- 增加日志断言，按固定 `NpcRuntimeLogRecord` 取值验证 parent negative evidence 与 local executor positive evidence。

### Step 2: 在 runtime 内定义最小本地执行合同和工具面策略

目标文件：

- 新增 `src/runtime/NpcLocalActionIntent.cs`
- 新增 `src/runtime/NpcLocalExecutorRouting.cs`
- 如需封装，可新增 `src/runtime/NpcLocalExecutorPolicy.cs`
- 可选新增 `src/runtime/NpcToolSurfacePolicy.cs`，若现有工具过滤逻辑需要单独承载。
- 修改 `src/runtime/NpcRuntimeBindings.cs`

实施内容：

- 定义最小 action contract：动作类型、允许参数、升级条件、失败重试计数、摘要输出。
- 明确 v1 支持的动作枚举：`move`、`observe`、`wait`、`task_status`。
- 明确禁止本地执行层做人格/长期目标/关系类决策。
- 让 contract 天然支持“失败两次升级回主 agent”，但 v1 不做复杂 plan rewrite。
- 定义两个工具面：
  - parent autonomy tool surface：不包含 `stardew_move` / `stardew_task_status` 这类本地执行层所有的低风险动作工具。
  - local executor tool surface：只包含 allowlisted 低风险动作工具，v1 至少包含 `stardew_move` / `stardew_task_status`。
- 明确注入 contract，避免执行时自由猜：
  - 在 `NpcRuntimeAutonomyBindingRequest` 中新增等价于 `LocalExecutorGameToolFactory` 的字段，签名建议为 `Func<IGameAdapter, NpcObservationFactStore, IEnumerable<ITool>>? LocalExecutorGameToolFactory`。
  - 保留现有 `GameToolFactory` 作为 parent game tools 来源，但它的输出必须经过 `NpcToolSurfacePolicy` 过滤，确保 parent 不含 local-executor-owned tools。
  - `LocalExecutorGameToolFactory` 由 Stardew autonomy wiring 传入，v1 可复用 `StardewNpcToolFactory.CreateDefault(...)` 后过滤出 `stardew_move` / `stardew_task_status`。
  - rebind key / fingerprint 必须同时包含 parent tool fingerprint 与 local executor tool fingerprint，避免工具面变化后复用旧 handle。

### Step 3: 在主 autonomy agent 暴露工具前建立 hard-routing seam

目标文件：

- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewNpcTools.cs`（如需支持按用途构造工具面）

实施内容：

- 调整 autonomy handle 创建路径：主 autonomy agent 的 registered game tools 不再包含 `stardew_move` / `stardew_task_status`。
- 注意 `stardew_move` / `stardew_task_status` 当前来自 `GameToolFactory`，不是 discovered `ToolSurface`；只过滤 `request.ToolSurface.Tools` 不足以达成 hard routing，必须过滤 `gameTools`。
- 同步收窄 `BuildDecisionMessage(...)` 和 autonomy prompt：主 agent 产出短 `intent contract` 或可解析高层意图，不再被提示直接使用 `stardew_move` / `stardew_task_status`。
- parent -> local executor 的 v1 contract 固定为单个 JSON object，不接受自由文本混排。最小 schema：

```json
{
  "action": "move|observe|wait|task_status|escalate",
  "reason": "short natural-language reason",
  "destinationId": "optional for move",
  "commandId": "optional for task_status",
  "observeTarget": "optional for observe",
  "waitReason": "optional for wait",
  "allowedActions": ["move", "observe", "wait", "task_status"],
  "escalate": false
}
```

- `action=move` 必须有 `destinationId`；`action=task_status` 必须有 `commandId` 或可从 runtime driver action slot 推断；`action=escalate` 或 `escalate=true` 不进入 local executor tool execution，只记录升级。
- parent response 不是单个 JSON object、字段缺失、action 不在 allowlist、或包含高风险动作时，视为 parent intent contract invalid，记录诊断并升级，不调用 local executor。
- 在主 agent 得到高层 intent 后，由 `NpcAutonomyLoop` 调用本地 executor runner；这里是 intent 后置执行，不是低层工具后置接管。
- 本地 executor runner 使用 `DelegationChatClient` 和 local executor tool surface；只有它能触发 `stardew_move` / `stardew_task_status`。
- 保留升级策略：非法动作、超出允许动作、重试超阈值、重要事件发现时，回退给主 agent 或记录升级原因。
- 测试必须证明主 autonomy agent 即使想直接调 `stardew_move` 也拿不到该工具定义。

### Step 4: 接入本地 executor runner 并复用 delegation lane 与 Stardew 工具执行

目标文件：

- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/AgentCapabilityAssembler.cs`
- `src/runtime/NpcRuntimeBindings.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- 新增 `src/runtime/NpcLocalExecutorRunner.cs`

实施内容：

- 不重做 root client/lane resolver；继续复用现有 `DelegationChatClient` 注入链。
- 注意当前 `DelegationChatClient` 只是被携带到 composition services，并明确用于 `AgentTool`；本轮必须新增 `NpcAutonomyLoop` / local executor runner 对它的消费路径。
- local executor runner 形态必须是专用 helper，不是完整第二 NPC agent：
  - 直接基于 `DelegationChatClient.StreamAsync(systemPrompt, messages, tools, ct)` 调用本地模型。
  - 使用单独的短 system prompt，说明它只是动作执行层，不拥有 NPC 人格/长期目标。
  - 输入只包含 intent contract、短 observation facts、allowed actions 和 retry/escalation policy。
  - 输出必须解析为 `NpcLocalActionIntent`，schema 非法即失败并升级。
  - 它使用一个受限临时 `ToolRegistry` / tool list，只包含 local executor tool surface，不共享 parent agent 的完整 tool registry。
- 由 `NpcRuntimeSupervisor.CreateAutonomyHandle(...)` 构造 `NpcLocalExecutorRunner`：
  - 用 `request.Services.DelegationChatClient` 作为 chat client；如果为空，则 runner 不可用并记录 `local_executor_unavailable`。
  - 用 `LocalExecutorGameToolFactory(adapter, factStore)` 生成 executor-only tools。
  - 将 runner 作为可选依赖注入 `NpcAutonomyLoop` 构造函数。
- 保证本地执行层最终仍通过真实 Stardew 工具执行，不直接拼接宿主命令。
- 如果 `observe / wait` 当前没有独立工具，v1 明确限定其行为：
  - `observe` 是 executor 判定后的宿主解释执行，复用现有 `ObserveAsync` / 当前 observation 路径，不宣称它是 tool-backed Stardew action。
  - `wait` 是 executor 判定后的宿主解释执行，记录为无命令动作/短等待摘要，不宣称它有真实 Stardew tool。
  - 不为了 v1 凭空新增游戏侧复杂命令面。
- delegation lane 失败时的硬边界：
  - `DelegationChatClient` 超时、不可达、stream error、schema 非法、返回越权 action 时，只能记录诊断并升级/blocked。
  - 绝不能把 `stardew_move` / `stardew_task_status` 临时还给 parent autonomy agent 作为兜底。
  - 失败结果要写入 runtime log，便于判断是 local executor 不可用，而不是 NPC 不想行动。
- delegation failure 的可断言 surface：
  - `NpcAutonomyTickResult.DecisionResponse` 返回短摘要，例如 `local_executor_blocked:<reason>` 或 `local_executor_escalated:<reason>`。
  - `runtime.jsonl` 写一条 diagnostic 记录，`ActionType=diagnostic`，`Target=local_executor`，`Stage=blocked|escalated`，`Result=<reason>`，`Error=<machine-readable error code>`。
  - v1 不要求自动改写 todo 为 blocked；如果后续需要 todo 状态变化，必须走单独任务，避免本轮把执行层失败直接等同长期任务失败。

### Step 5: 把运行时证据链补全到 log、诊断和 memory 边界

目标文件：

- `src/runtime/NpcAutonomyLoop.cs`
- 如需要，`src/runtime/NpcRuntimeLogWriter.cs` 或对应 log record 类型

实施内容：

- 在 `runtime.jsonl` 中增加最小证据字段或诊断记录，至少能看出：
  - `intent_contract`
  - `local_executor_selected`
  - `delegation_lane_used`
  - `action=move|observe|wait|task_status`
  - `tool=stardew_move|stardew_task_status` 或 `no_tool_wait`
  - `escalated_to_main_agent`（如发生）
- 证据必须是负向 + 正向双证据：
  - parent decision session / tool log 中 `stardew_move` 和 `stardew_task_status` 调用次数为 0。
  - local executor trace / tool log 中对应低风险 tool 调用次数为 1+ 或记录明确的 executor-side refusal/escalation。
- 保留现有 narrative/no-tool 诊断，但新增“主 agent 未直接调 `stardew_move`、而本地执行层触发工具”的正向证据。
- 将 parent 高层 intent 与 local executor trace 分开记录。
- v1 不扩展 `NpcRuntimeLogRecord` schema；用多条诊断记录编码证据，固定取值如下：
  - parent contract accepted: `ActionType=diagnostic`, `Target=intent_contract`, `Stage=accepted`, `Result=<compact action/reason>`
  - parent contract rejected: `ActionType=diagnostic`, `Target=intent_contract`, `Stage=rejected`, `Result=<reason>`, `Error=intent_contract_invalid`
  - parent low-risk tool negative evidence: `ActionType=diagnostic`, `Target=parent_tool_surface`, `Stage=verified`, `Result=stardew_move=0;stardew_task_status=0`
  - local executor selected: `ActionType=diagnostic`, `Target=local_executor`, `Stage=selected`, `Result=action=<action>;lane=delegation`
  - local executor tool result: `ActionType=local_executor`, `Target=stardew_move|stardew_task_status|observe|wait`, `Stage=completed|blocked|escalated`, `Result=<short result>`, `CommandId=<command id when available>`, `Error=<error code when blocked>`
- Memory 写入时机和来源固定：
  - 不再把 parent raw JSON intent contract 直接传给 `WriteMemoryAsync(...)`。
  - 如果 local executor 完成，`WriteMemoryAsync(...)` 只写压缩后的自然语言结果摘要，例如 `Autonomy tick <trace>: tried moving to PierreShop; command queued; reason: ...`。
  - 如果 local executor blocked/escalated，只写短自然语言失败/升级摘要，不写原始 executor prompt、schema、tool arguments 或完整 trace。
  - 如果 parent contract invalid，默认不写 memory，只写 diagnostic，避免把格式错误的协议文本沉淀进 NPC 记忆。

### Step 6: 最小手测与回归验证

目标文件：

- `Desktop/HermesDesktop/README.md` 不在本轮修改范围内；仅在计划中列出验证命令。

实施内容：

- 用现有本地 OpenAI-compatible/delegation lane 配置跑单 NPC autonomy tick。
- 检查 delegation endpoint 是否收到 executor 请求。
- 检查 `runtime.jsonl`、Hermes log、SMAPI/bridge log 是否能串起闭环。
- 触发一次单 NPC move 意图后，手测预期：
  - LM Studio 请求日志看到一次 `/v1/chat/completions` 请求，模型为 `qwen3.5-2b-gpt-5.1-highiq-instruct-i1`，请求内容是 local executor prompt / intent contract，而不是完整 NPC persona prompt。
  - Hermes log 看到 lane 解析或 executor 使用记录，包含 `lane=delegation` 或等价字段。
  - `runtime.jsonl` 看到 parent tool count 中 `stardew_move=0`、local executor selected、action=move、tool=stardew_move、result/status。
  - SMAPI/bridge log 看到真实 move command/status。

## Testable Acceptance Criteria

1. 主 autonomy agent 的工具面不再包含 `stardew_move` / `stardew_task_status`；测试能证明它无法直接调用这些低风险动作工具。
2. 当高层意图对应 `move` 时，最终触发 `stardew_move` 的执行证据来自本地执行层/委派 lane，而不是主 agent 直接工具调用。
3. 本地执行层仅允许 `move / observe / wait / task_status`；遇到 `gift / trade / use item / relationship-impacting` 等动作时会拒绝本地执行并升级。
4. 本地执行层使用 `DelegationChatClient`；相关测试能区分主 `_chatClient` 与 delegation client 的调用次数，并证明 `NpcAutonomyLoop` / executor runner 是 delegation client 的实际消费者。
5. `runtime.jsonl` 或等价运行时诊断中可看到 `intent_contract -> local_executor -> stardew_move/task_status -> result/status` 证据链。
6. 当移动失败达到阈值时，本地执行层不会无限重试；会记录升级原因并回主 agent。
7. 不新增 NuGet 依赖，不新增第二条平行 NPC 心智/记忆系统，不绕过现有 Stardew 工具层。
8. `observe` / `wait` 在 v1 中明确是 executor 判定、宿主解释执行；不把它们误报为已有真实 Stardew tool-backed action。
9. 原始 local executor trace 不进入 NPC 长期 memory；长期 memory 只写高层意图或压缩后的执行结果摘要。
10. 当 delegation lane 不可用、超时、返回非法 schema 或越权 action 时，系统只能记录并升级/blocked；测试证明不会通过重新暴露 `stardew_move` / `stardew_task_status` 给 parent agent 来兜底。
11. Parent response contract 有固定 JSON schema；测试覆盖 valid move、valid wait、invalid JSON、missing destinationId、high-risk action。
12. 日志证据使用现有 `NpcRuntimeLogRecord` 字段编码，不要求本轮扩展 log schema；测试按固定 `ActionType/Target/Stage/Result/Error` 取值断言。
13. `WriteMemoryAsync(...)` 不接收 parent raw JSON intent 或 local executor raw trace；测试验证 memory 内容为短自然语言摘要或在 invalid contract 时不写入。

## Risks And Mitigations

- 风险：v1 在 `NpcAutonomyLoop` 和 `NpcRuntimeSupervisor` 内加 seam，可能让 loop/supervisor 职责继续膨胀。
  - 缓解：把本地动作 contract、tool surface policy 与 routing policy 抽成小类型，loop 只做编排，supervisor 只做工具面组装。
- 风险：`observe`、`wait` 当前没有与 `move` 同等成熟的 Stardew 工具面，容易为了“动作集合完整”而过度设计。
  - 缓解：v1 允许这两类动作走最小实现，重点先证明 `move/task_status` 的真实 delegating 闭环。
- 风险：如果仍保留主 agent 直接 `stardew_move` 的旧 prompt 语义，可能与新路由冲突。
  - 缓解：同步收窄 autonomy prompt，让主 agent 输出高层意图而非低层工具细节。
- 风险：如果只新增本地 executor 但不移除 parent 工具面，主 agent 仍能在 `Agent.ChatAsync(...)` 内部直接执行低风险工具，形成伪成功。
  - 缓解：把 parent autonomy tool surface 过滤作为第一验收条件，并用测试证明主 agent 拿不到 `stardew_move` / `stardew_task_status`。
- 风险：日志只记录失败不记录正向路由，验证时会误判“其实没走本地执行层”。
  - 缓解：新增正向 routing 诊断记录，而不只依赖现有 warning diagnostics。
- 风险：delegation lane 调用稳定，但 executor 输出非法 action。
  - 缓解：在 routing policy 中加 schema 校验和 allowlist，非法输出直接拒绝并记录。
- 风险：执行者只过滤 discovered tool surface，漏掉来自 `GameToolFactory` 的 `stardew_move` / `stardew_task_status`。
  - 缓解：计划要求在 `NpcRuntimeAutonomyBindingRequest` / `NpcRuntimeSupervisor.CreateAutonomyHandle(...)` 层 partition `GameToolFactory` 输出，并用 parent 0-call 负向测试验证。
- 风险：delegation lane 不可用时，为了“保持功能”，有人把低风险工具重新暴露给 parent。
  - 缓解：把该行为列为验收失败；正确行为是 local executor unavailable diagnostic + blocked/escalation。
- 风险：parent intent contract 格式不固定，执行者临场设计 parser，导致测试和 prompt 对不上。
  - 缓解：把 v1 parent response 固定为单 JSON object，并在 tests 中覆盖 valid/invalid contract。
- 风险：为了记录证据而扩展 runtime log schema，扩大 diff。
  - 缓解：v1 默认不扩 schema，用固定 `ActionType/Target/Stage/Result/Error` 组合编码证据；只有现有 schema 无法满足时才另开小改动。

## Verification Commands

先跑定向测试：

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcRuntimeSupervisorTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests"
```

再跑完整桌面测试与构建：

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

手测/日志验证：

本地执行层手测使用用户已启动的 LM Studio OpenAI-compatible endpoint：

```yaml
delegation:
  provider: openai
  base_url: http://127.0.0.1:1234/v1
  default: qwen3.5-2b-gpt-5.1-highiq-instruct-i1
  api_key: lm-studio
```

注意：OpenAiClient 当前会请求 `{base_url}/chat/completions`，所以 `base_url` 必须包含 `/v1`，不要只写 `http://127.0.0.1:1234`。

```powershell
.\run-desktop.ps1
Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\hermes.log" -Tail 200 -Wait
Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\runtime\stardew\games\stardew-valley\saves\<saveId>\npc\<npcId>\profiles\<profileId>\activity\runtime.jsonl" -Tail 200 -Wait
Get-Content "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" -Tail 200 -Wait
```

## ADR

### Decision

在 Stardew autonomy runtime 中建立轻量双工具面 hard-routing seam：主 NPC agent 只产出高层 intent contract，不再持有 `stardew_move` / `stardew_task_status`；`NpcRuntimeSupervisor` 构造一个专用 `NpcLocalExecutorRunner`，使用 `DelegationChatClient` 和 allowlisted local executor tool surface 执行 `move / task_status`，并由宿主解释执行 `observe / wait`。

### Drivers

- 主 agent 上下文和云成本必须收缩。
- 当前仓库已具备 delegation lane 和真实 Stardew 工具，可支撑最小验证，但需要新增 `NpcAutonomyLoop` / executor runner 对 delegation lane 的消费路径。
- 用户目标是“代码层硬路由”，不是“提示词建议委派”。
- `stardew_move` / `stardew_task_status` 当前来自 `GameToolFactory`，所以必须 partition game tools，而不是只过滤 discovered tools。

### Alternatives Considered

- 独立新建完整 `LocalNpcExecutor` 服务层后再接入：本轮拒绝，范围偏大；保留后续抽离方向。
- 只改 prompt，让主 agent 自己学会调用 delegation：拒绝，不稳定也不满足硬路由。
- 在 `_agent.ChatAsync(...)` 返回后插入本地执行层分支：拒绝，低层 tool loop 已在 `ChatAsync(...)` 内部完成，接管时机太晚。
- 先做全量动作面：拒绝，不符合 minimal v1。

### Why Chosen

因为它是对当前代码事实最短、最可验证、最符合成本目标的路径，而且能先回答核心问题：低风险动作是否真的已经脱离主 agent 直接执行。

### Consequences

- `NpcAutonomyLoop` 会暂时承担更多编排职责，`NpcRuntimeSupervisor` 需要负责双工具面组装。
- 需要新增小的本地 action contract、tool surface policy、routing policy 与专用 `NpcLocalExecutorRunner`。
- `move/task_status` 会先成为 tool-backed 闭环，`observe/wait` 在 v1 中采用宿主解释执行。
- 主 agent prompt 和工具可见性必须同步收窄，否则硬路由不成立。
- delegation lane 失败不再允许 fallback 到 parent low-risk tools；短期可能出现更多 blocked/escalated，但这比伪 hard-routing 更符合目标。

### Follow-Ups

- 若 v1 成功，再把 routing/executor 从 loop 中进一步抽离成独立服务。
- 再扩到简单 todo step 推进与低风险 retry policy。
- 最后再评估更复杂动作是否应进入本地执行层。
- 如果日志查询体验不够好，再单独设计结构化 `NpcRuntimeLogRecord` 扩展；不要把它塞进 v1 hard-routing 验证。

## Execution Handoff

### Available Agent Types

- `planner`
- `architect`
- `executor`
- `debugger`
- `test-engineer`
- `verifier`
- `critic`
- `explore`

### Recommended Staffing: Ralph

适合本任务的默认路径。原因是共享文件集中在 `NpcAutonomyLoop`、runtime wiring 和同一批测试文件，串行 owner 更容易控制回归。

建议 lane：

- `ralph` 主 owner：`executor` 心智，`high` reasoning
- 辅助审查：`verifier`，`high` reasoning
- 如中途需要结构挑战，再引入一次 `critic` / `architect` 只读复核

执行顺序：

1. 先补测试并让新断言失败。
2. 再做最小 routing contract。
3. 再接入 `NpcAutonomyLoop`。
4. 最后补日志与回归验证。

### Recommended Staffing: Team

仅当你想并行推进时使用，建议 3 lanes：

1. `executor` lane，`high` reasoning
   - 负责 `src/runtime/NpcAutonomyLoop.cs` 与新的 local action contract/routing policy。
2. `executor` 或 `debugger` lane，`medium` reasoning
   - 负责 runtime wiring / delegation client 使用点核对与最小接线调整。
3. `test-engineer` lane，`high` reasoning
   - 负责 `NpcAutonomyLoopTests`、`StardewNpcAutonomyBackgroundServiceTests`、delegation usage 断言。

团队收口要求：

- 所有 lanes 不要各自定义第二套 executor contract。
- 共享事实基线以 `NpcAutonomyLoop` 当前调用 `_agent.ChatAsync` 的入口为准。
- 合并前由 `verifier` 统一跑定向测试、全量桌面测试和构建。

### Launch Hints

`ralph`：

```text
$ralph implement .omx/plans/stardew-npc-local-executor-minimal-test-consensus-plan.md
```

`team`：

```text
$team implement .omx/plans/stardew-npc-local-executor-minimal-test-consensus-plan.md
```

如果走 OMX shell：

```powershell
omx ralph ".omx/plans/stardew-npc-local-executor-minimal-test-consensus-plan.md"
omx team ".omx/plans/stardew-npc-local-executor-minimal-test-consensus-plan.md"
```

### Team Verification Path

1. `test-engineer` 先确认新增断言能在实现前失败。
2. `executor` 合流后由 `verifier` 跑定向测试。
3. 定向测试通过后跑 `dotnet build` 与完整 `HermesDesktop.Tests`。
4. 如需手测，检查 delegation endpoint、`runtime.jsonl`、Hermes log、SMAPI log 三处证据一致。
5. 只有在“主 agent 未直接触发 `stardew_move`、本地执行层触发了真实工具、失败可升级”三件事都被证实后，才算完成。

## Consensus Changelog

- Architect 复核结论：已接受 hard-routing seam 必须在 parent agent 工具暴露之前建立，不能在 `_agent.ChatAsync(...)` 返回后再接管。
- Critic 反馈已合入：固定 parent 单 JSON contract、使用现有 `NpcRuntimeLogRecord` 字段编码证据、明确 memory 写入边界、明确 delegation 失败不能把低风险工具重新暴露给 parent。
- 最终 critic 子 agent 在最后一轮等待中卡住并已关闭；本地只读复核未发现新的阻塞问题。唯一发现的文档不一致已同步修正到 `.omx/specs/stardew-npc-local-executor-minimal-test.md`：早期宽泛 `goal/persona` 合同示例改为本计划采用的固定 `action/destinationId/allowedActions` contract。

## Final Review Verdict

APPROVE for execution.

Execution cautions:

- 实现阶段第一条红线是 parent autonomy tool surface：`stardew_move` / `stardew_task_status` 必须在 `_agent.ChatAsync(...)` 前不可见。
- 第二条红线是失败兜底：delegation/local executor 不可用只能 blocked/escalated，不能临时把低风险工具还给 parent。
- 第三条红线是 memory：不要把 parent raw JSON contract 或 executor raw trace 写进 NPC 长期 memory。
