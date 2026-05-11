# Stardew NPC 动作链护栏参考项目对齐 PRD

## Requirements Summary

当前已经有“agent 调真实动作 -> 宿主执行 -> terminal fact -> 下一轮看到 `last_action_result`”的基础闭环，但缺少参考项目同类系统里的动作链护栏：

- 动作完成后如果 agent 发起新动作，后续仍会进入下一轮收口，这是正确方向。
- 但 runtime 还没有专门记录“这是一条连续 action chain”，也没有限制链长、重复失败、stuck、defer 尝试次数。
- 现有 `closure_missing` 只是诊断，不会 bounded repair。

本计划补齐的是 **action-chain guard**，不是改成宿主脚本编排。宿主只做三件事：

1. 记录动作链事实和计数。
2. 在明显循环/重复失败/卡死/队头阻塞时给 agent 事实反馈。
3. 到达预算上限时阻止继续提交真实动作，并要求 agent 用 `todo`/`wait:no-action`/`blocked` 收口。

## Reference Alignment

### 从 hermescraft-main 迁移的原则

- `currentTask` 单槽原则：一个身体同一时间只追踪一个真实长动作。
  - 参考：`external/hermescraft-main/bot/server.js:108`, `:2619`
  - 本项目对应：保留并强化 `ActionSlot` / `PendingWorkItem`。

- `briefState()` 反馈原则：每次动作响应都给 agent 简短新事实，不做长篇宿主剧情。
  - 参考：`external/hermescraft-main/bot/server.js:703`
  - 本项目对应：`LastTerminalCommandStatus` + `stardew_recent_activity` + runtime jsonl。

- `actionHistory` 循环检测原则：重复失败不是继续盲跑，而是提示换方法。
  - 参考：`external/hermescraft-main/bot/server.js:116`, `:748`
  - 本项目对应：新增 per NPC action-chain history / repeated failure summary。

- watchdog 原则：移动类长动作不能永久 running。
  - 参考：`external/hermescraft-main/bot/server.js:2687`
  - 本项目对应：现有 action slot timeout 已有基础，补齐 stuck/repeated-running 事实归一。

- SOUL 层生活指导原则：失败两次后 stop/status/换方法，这属于 agent 指导，不是宿主硬编码下一步。
  - 参考：`external/hermescraft-main/SOUL-civilization.md:161`
  - 本项目对应：更新 `stardew-task-continuity` / `stardew-world`，让 NPC 知道看到 loop/stuck 后该观察、换方法、告知玩家或标 blocked。

### 从 hermes-agent-main 迁移的原则

- tool call id/result 配对：同名工具结果不能靠自然顺序猜。
  - 参考：`external/hermes-agent-main/environments/agent_loop.py:291`, `:466`；`external/hermes-agent-main/acp_adapter/events.py:47`, `:60`
  - 本项目对应：action-chain 记录必须带 `traceId/workItemId/commandId/chainId`，测试不只按 action 名称断言。

- 工具错误回传给模型：未知工具、非法参数、执行异常都作为 tool result/error 进入下一步判断。
  - 参考：`external/hermes-agent-main/environments/agent_loop.py:338`
  - 本项目对应：busy/deferred/repeated-failure/stuck 都写 terminal/diagnostic fact，不能静默。

- turn/budget 原则：循环靠预算停止，而不是靠“希望模型听话”。
  - 参考：`external/hermes-agent-main/environments/agent_loop.py:138`, `:204`
  - 本项目对应：新增 action-chain 层预算，与现有 `NpcAutonomyBudget` 的每轮工具预算并存。

- todo 是 agent 自己维护的 task loop，不是宿主剧情系统。
  - 参考：`external/hermes-agent-main/tools/todo_tool.py:21`
  - 本项目对应：任务承诺继续使用 `SessionTodoStore`，不新增 NPC task store。

## RALPLAN-DR Summary

### Principles

1. **agent 决策，宿主设护栏**：宿主不决定下一步生活动作，只限制明显失控的执行链。
2. **单身体单动作槽**：每个 NPC body 同时最多一个真实 world-writing action。
3. **事实反馈短而可追踪**：所有 chain guard 事实必须带 `chainId/traceId/workItemId/commandId/action/status/reason`。
4. **任务连续性用 todo，不造第二套任务系统**：承诺状态仍由 agent 的 `todo` 管。
5. **参考项目原则化迁移，不照搬游戏实现**：Minecraft 的 `currentTask/briefState/actionHistory/watchdog` 映射到 Stardew runtime controller，而不是引入 HTTP task clone。

### Decision Drivers

1. **防止活人感变成死循环**：NPC 能连续生活，但不能无限移动/失败/重试。
2. **保持 agent-native 边界**：失败后是 agent 选择观察、换目标、告知玩家或收口，不是宿主替它写剧本。
3. **可测试、可诊断、可恢复**：每个护栏都能在 runtime snapshot/jsonl 和测试里看到。

### Viable Options

#### Option A：只靠 prompt/skill 指导

Pros:
- 改动最小。
- 完全不增加 runtime 状态。

Cons:
- 无法保证连续动作链不会无限推进。
- `closure_missing` 只能日志化，不能形成可靠恢复。
- 不能像参考项目 `actionHistory` / watchdog 一样从运行时发现循环。

Verdict: 拒绝。它不能解决用户问的“编排护栏是否实现”缺口。

#### Option B：在现有 NPC runtime controller 中补 action-chain guard

Pros:
- 复用 `ActionSlot`、`LastTerminalCommandStatus`、`SessionTodoStore`、state store。
- 对齐 hermescraft 的单槽、brief state、action history、watchdog 原则。
- 不新增第二 runtime/tool/task lane。

Cons:
- 需要扩展持久化 snapshot 和测试矩阵。
- 需要谨慎避免宿主代替 agent 决策。

Verdict: 采用。

#### Option C：做成新的外部 MCP action runner

Pros:
- 工具协议表面统一。
- 长期可以把 Stardew 能力外置。

Cons:
- MCP 同步工具返回不能天然解决异步动作完成后的 wake、runtime fact、todo 收口。
- 会与当前 in-process Stardew bridge/runtime 形成第二执行面。
- 与本阶段“不要新增 MCP server / 第二 tool lane”的约束冲突。

Verdict: 本轮拒绝。后续可以在现有 lifecycle 稳定后评估 MCP 包装，但不是 action-chain guard 的核心解。

## Functional Requirements

### FR0：反馈面分层

参考项目的 `briefState()` 是给 agent 的短事实，不等于调试日志。本计划必须分清两层：

- **agent-visible feedback**：工具直接返回的 `GameCommandResult`、下一轮 prompt 的 `last_action_result` / 单行 `action_chain` fact、`stardew_recent_activity` 中的短事实、以及 skill 指导。内容必须短、事实化、可追踪。
- **observability-only logs**：runtime activity jsonl、Hermes log、debug event、state snapshot。它们用于人工/测试诊断，不能被当作 prompt 注入通道，也不能塞入地点候选。

同一件事可以同时写两层，但写法不同：agent-visible 只告诉“发生了什么、为什么被限制、可用收口方式”；observability logs 额外带完整 ids 和内部状态。

### FR1：ActionChainGuardState

在 NPC runtime controller 增加最小 action-chain guard 状态，建议命名 `NpcRuntimeActionChainGuardSnapshot` 或在类型注释中明确 `guard-only`。建议字段：

- `ChainId`
- `GuardStatus`: `open` / `blocked_until_closure` / `closed`
- `BlockedReasonCode` 可空
- `BlockedUntilClosure`: bool
- `RootTodoId` 可空
- `RootTraceId`
- `StartedAtUtc`
- `UpdatedAtUtc`
- `LastAction`
- `LastTargetKey`
- `ConsecutiveActions`
- `ConsecutiveFailures`
- `ConsecutiveSameActionFailures`
- `LastTerminalStatus`
- `LastReasonCode`
- `ClosureMissingCount`
- `DeferredIngressAttempts`

状态只记录护栏事实，不记录宿主建议的下一步，不承载任务语义。

`LastTargetKey` 生成规则：

- move: `move:{locationName}:{x}:{y}`
- speak: `speak:{targetKind or targetId}`
- open private chat: `open_private_chat:{targetKind or targetId}`
- idle micro action: `idle_micro_action:{kind}`
- 其它 world-writing action: `{action}:{targetKind}:{targetId/locationName/tile if present}`
- 不包含自然语言目的地文本，不包含未解析地点名称。

### FR2：chain 起点

以下情况开启或延续 chain：

- private chat 接受当前行动请求并写入 active todo 后调用 `npc_delegate_action`。
- autonomy turn 看到 terminal result + active todo 后提交新的 world-writing action。
- autonomy 自己发起有明确连续目的的 move/speak/idle/open action。

普通短闲置动作可记录 action history，但不一定开启 commitment chain。

### FR2.1：chain 生命周期

新建 chain：

- 当前没有 open chain，且 agent 提交 world-writing action。
- private chat 接受当前行动请求并通过 `npc_delegate_action` 入队。
- 现有 chain 已 `closed` 或过期后，agent 提交新的 world-writing action。

延续 chain：

- 上一轮 terminal result 与当前 active todo 仍有关联，agent 继续提交 world-writing action。
- agent 在 closure turn 中明确继续当前承诺。
- delegated ingress 是同一个 `RootTraceId` / `RootTodoId` 的后续执行。

延续判定只能使用机械信号：

- `RootTodoId` 相同。
- `RootTraceId` 相同。
- pending/action slot/terminal command id 与当前 work item 有明确关联。
- agent 通过结构化 action payload 或工具调用上下文显式继续当前承诺。

不得解析自然语言 `reason`、玩家原话、地点文本或 NPC 台词来判断“明确连续目的”。这条是为了防止 controller 变成意图解释器。

关闭 chain：

- agent 调 `todo` 把相关 active todo 标为 `completed`、`blocked`、`failed` 或 `cancelled`。
- agent 明确输出 `wait:<reason>` / `no-action:<reason>`，且没有提交新的 world-writing action。
- chain 因预算超限进入 `blocked_until_closure` 后，agent 做了上述收口。

过期/清理：

- 没有 active todo 且 `UpdatedAtUtc` 超过一个短 TTL（建议 2 分钟）后清理 open chain。
- 新的一天/存档切换/bridge rebind 时不删除持久事实，但不让旧 chain 继续累计新动作；新动作开新 chain。
- closed chain 可保留最近一条供 `stardew_recent_activity` 展示，但不参与 budget enforcement。

必须避免：把互不相关的日常动作累计到同一条 chain。

### FR3：chain 预算

默认建议（可配置默认值，不是参考项目原样数值）：

- `MaxActionsPerChain = 4`
- `MaxConsecutiveFailures = 2`
- `MaxClosureMissing = 1`
- `MaxDeferredIngressAttempts = 3`

这些数值是本项目对 hermes-agent `max_turns` / tool budget 和 hermescraft 单任务护栏原则的 Stardew 映射；实现时应放在 `NpcAutonomyBudgetOptions` 或同级配置里，避免写死在 controller。

达到预算时：

- 真实动作工具返回 blocked/diagnostic fact。
- prompt 要求 agent 选择：`todo` 标 blocked/failed/completed、`wait:no-action`、或说话告知玩家。
- 宿主不得自动替 agent 标 todo，也不得自动换目的地。
- budget enforcement 的代码位置在 `StardewRuntimeActionController.TryBeginAsync`，但顺序必须唯一：
  1. 先读 controller snapshot。
  2. 若已有 `ActionSlot` / `PendingWorkItem`，立即返回 `action_slot_busy`，不启动/延续 chain，不增加 `ConsecutiveActions`，不覆盖 `LastTerminalCommandStatus`。这对齐 hermescraft `currentTask` running 时先 409 的规则。
  3. 只有没有活动真实动作时，才解析/延续 action chain 并检查 `blocked_until_closure` / chain budget。
  4. 若 chain guard 拦截，返回 blocked tool result + chain diagnostic，不创建 `PendingWorkItem` / `ActionSlot`，不提交 bridge command，不申请 claim。
  5. 只有通过 slot 和 chain guard 后，才申请 world coordination claim，并写 pending/action slot。
- 返回格式沿用 `GameCommandResult` blocked，`FailureReason=action_chain_budget_exceeded` 或具体 `BlockedReasonCode`，`Retryable=false`，并写 runtime diagnostic。
- 被拦截时不能创建 `PendingWorkItem`、不能创建 `ActionSlot`、不能提交 bridge command、不能申请 claim。
- `todo`、read-only status、`skill_view`、`session_search`、`memory`、显式 `wait/no-action` 不受 world-writing budget block 影响。
- `world-writing action` 判定必须集中在一个 helper/predicate，供 move/speak/open private chat/idle micro action 等工具共用；不要让每个工具各自判断是否进入 guard。

计数时机必须避免双计数：

- `ConsecutiveActions` 在 world-writing action 被 runtime 接受进 chain 且成功写入 pending/action slot 时加 1。
- terminal completed/failed/blocked/cancelled/stuck 只更新 `LastTerminalStatus`、`LastReasonCode`、failure counters，不再增加 `ConsecutiveActions`。
- `action_slot_busy`、`action_chain_budget_exceeded`、参数错误、read-only 工具调用不计入 `ConsecutiveActions`。
- delegated ingress defer 只更新 `DeferredIngressAttempts`，不计入 `ConsecutiveActions`，直到真正被接受为 world-writing action。
- guard rejection counters 与真实 bridge terminal failure counters 分开。`action_chain_budget_exceeded` / `action_slot_busy` 可形成 guard diagnostic，但不得增加 `ConsecutiveFailures` / `ConsecutiveSameActionFailures`，也不得触发 `action_loop`。

`LastTerminalCommandStatus` 污染边界：

- `LastTerminalCommandStatus` 只表示真实 bridge command 的 terminal，或“没有 active slot/pending 时”由 ingress 自身转出的 terminal-like blocked fact。
- `action_chain_budget_exceeded` 是当前工具调用结果和 chain diagnostic，默认不能覆盖上一条真实 bridge command terminal。
- 如果为了下一轮可见性写 terminal-like fact，`commandId` 必须来自被拒绝的 action/workItem/chain 派生命令，不能复用正在运行或上一条真实 bridge command 的 id。

### FR4：重复失败和 action loop

参考 hermescraft `actionHistory`：

- 最近 N 次 terminal 中同 action + 同 target key 连续失败/blocked/cancelled/stuck，写 `action_loop` fact。
- 该 fact 注入下一轮 `last_action_result` 附近或 `stardew_recent_activity`，但不注入移动候选。
- skill 指导 agent 失败两次后必须重新观察/查询状态/换方法/标 blocked，不能继续同动作同目标。

### FR5：stuck/timeout 统一为 terminal fact

现有 action slot timeout 保留；补齐：

- `running` 超过 timeout 归一成 `action_slot_timeout`。
- bridge 明确返回 `command_stuck` 时归一成 terminal blocked/cancelled fact。
- movement stuck/repeated no-progress 不由 host 换路线，只写事实和 reason。

### FR6：closure_missing bounded repair

当前 `closure_missing` 只写诊断。补齐：

- terminal action + active todo + 没工具 + 非 `wait/no-action: reason` 时，第一次 `closure_missing` 后允许一次 repair wake/self-check。
- 第二次仍 missing，则设置 chain blocked，要求下次只做 `todo` 收口或 `wait/no-action`，不允许继续真实动作。
- `closure_missing` 不直接修改 todo 状态。
- repair wake 的 prompt 只说明缺少收口和可选收口方式，不重复委托真实动作。

### FR7：delegated ingress defer 可见

当前 delegated ingress 遇到 busy slot 直接 return。补齐：

- 不 remove work item。
- 增加 attempt/deferred count。
- 写 runtime diagnostic：`delegated_ingress_deferred:action_slot_busy`。
- 设置短 `NextWakeAtUtc`。
- 超过 attempt 后，如果当前仍有 active `ActionSlot`，只能写 ingress diagnostic / recent activity，不允许覆盖 `LastTerminalCommandStatus`。
- 只有没有 active `ActionSlot` / `PendingWorkItem` 时，才可把该 ingress 转成 terminal-like blocked fact 并唤醒 agent 收口。
- ingress blocked fact 的 `commandId` 必须是 ingress `workItemId` 或 `traceId` 派生值，不能冒充正在运行的 bridge command。

### FR8：tool/result 可追踪

所有 action-chain guard 相关日志和状态必须至少含：

- `chainId`
- `traceId`
- `workItemId`
- `commandId` 可空
- `action`
- `targetKey` 可空
- `status`
- `reasonCode`

测试不得只按 action 名称判断，避免同名动作错配。

### FR8.1：自动/半自动可观测验收

每个 guard 事件至少写出一条可测试记录：

- agent-visible：tool result、`last_action_result`、单行 `action_chain` fact、或 `stardew_recent_activity`。
- observability-only：runtime activity jsonl / in-memory activity sink / Hermes structured log / controller state snapshot。

两层记录都必须能追踪到同一事件，字段至少包括 `chainId`、`traceId`、`workItemId`、`commandId`（可空）、`action`、`status`、`reasonCode`。测试可以使用 fake activity sink 或 snapshot 断言，不要求每个单测都读真实磁盘 jsonl；手测才按固定日志顺序查真实文件。

### FR9：skills/persona 指导

更新现有 Markdown skill，不新增硬编码：

- `stardew-task-continuity`：看到 `action_loop`、`stuck`、`chain_budget_exceeded`、重复失败时如何收口。
- `stardew-world`：到达地点后允许停留/说话/短动作/等待，但连续动作必须有目的，不能机械连走。

本轮不修改单个 NPC persona 作为实现前提。通用生活指导放在 world/task-continuity skill；单 NPC persona 只允许在后续独立任务里补充非地点、非台词、非动作编排的性格倾向。

## Non-goals

- 不新增外部 MCP server。
- 不把 host 写成剧情导演。
- 不自动替 NPC 完成 todo。
- 不做复杂日程系统。
- 不新增地点/NPC/自然语言硬编码。
- 不把 `destination[n]` / `nearby[n]` / `moveCandidate[n]` 注入 wake prompt。

## Implementation Steps

### Step 1：扩展 runtime controller snapshot

Touchpoints:
- `src/runtime/NpcRuntimeDescriptor.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcRuntimeDriver.cs`
- `src/runtime/NpcRuntimeStateStore.cs`

Work:
- 新增 `NpcRuntimeActionChainSnapshot`。
- 命名优先 `NpcRuntimeActionChainGuardSnapshot`；如果采用较短名称，类型注释必须写清楚这是 guard-only counters，不是 task state。
- 持久化到 state store。
- 提供 driver 方法：start/update/clear 或 set snapshot。
- `NpcRuntimeStateStore` 增加 nullable `action_chain_json` 列；load 时缺列/空值返回 null。
- save 时写 `action_chain_json`。
- `NpcRuntimeDriver.HasPersistedControllerState` 把 non-null action chain 视为 persisted controller state，避免 chain-only recovery 被丢掉。
- 旧数据库迁移必须 idempotent：启动时检测列是否存在，不存在则 `ALTER TABLE` 增加。

Acceptance:
- runtime restart 后 chain state 保留。
- 空状态不影响旧 snapshot。

### Step 2：在 `StardewRuntimeActionController` 更新 chain

Touchpoints:
- `src/games/stardew/StardewNpcTools.cs`

Work:
- `TryBeginAsync` 成功时记录 action/target/chain id。
- terminal 时更新 action count、failure count、same-action failure count。
- `ConsecutiveActions` 只在 action 被接受并写入 pending/action slot 时增加；terminal 阶段不得再次增加。
- blocked/busy 不覆盖已有 slot，但写可追踪 diagnostic。
- 检查顺序固定为：active slot/pending busy -> chain guard budget -> claim -> pending/action slot。
- slot busy 检查优先级：如果已有 action slot，返回 `action_slot_busy`，不把它计为新 chain action，也不遮盖当前 slot 的 terminal 归属；如果没有 slot 但 chain 已 `blocked_until_closure`，返回 `action_chain_budget_exceeded`。
- 对于 read-only/status 类工具，不进入 `StardewRuntimeActionController.TryBeginAsync` 的 world-writing guard。
- 增加集中式 `IsWorldWritingAction` / 等价 predicate，避免 read-only/status 工具误入 guard。

Acceptance:
- 成功动作增加 `ConsecutiveActions`。
- terminal failed/blocked/cancelled 增加 failure counters。
- completed 重置 failure counters，但不自动完成 todo。

### Step 3：在 autonomy prompt 注入 chain guard fact

Touchpoints:
- `src/runtime/NpcAutonomyLoop.cs`

Work:
- 在 `last_action_result` 附近加入短事实：`action_chain: chainId=... actions=... failures=... reason=...`。
- chain 超预算时明确要求收口，不允许继续同一真实动作。
- `closure_missing` 增加 bounded repair 语义。

Acceptance:
- prompt 只含事实和约束，不含宿主给的行动候选。
- 预算超限时不会默许继续 move/speak/open/idle。

### Step 4：delegated ingress defer attempts

Touchpoints:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcRuntimeDescriptor.cs` 或 ingress payload/status

Work:
- busy 时更新 ingress status/attempt。
- 写 diagnostic + next wake。
- attempt 超限且无 active slot 时转 blocked terminal-like fact 并 remove ingress。
- attempt 超限但仍有 active slot 时只写 diagnostic，保留或改成 blocked ingress 状态，不能覆盖当前真实动作的 `LastTerminalCommandStatus`。
- ingress attempt 计数优先作为 `NpcRuntimeIngressWorkItemSnapshot` 字段持久化；若先用 payload metadata，必须封装 helper，避免散落 JSON 字符串处理。

Acceptance:
- 忙时不会静默 return。
- 不会无限队头阻塞。
- 不会污染正在运行 command 的 terminal result。

### Step 5：stuck/loop 归一事实

Touchpoints:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewCommandContracts.cs`
- `src/games/stardew/StardewNpcTools.cs`

Work:
- 识别 bridge `command_stuck` / running timeout / repeated same target failures。
- 统一写 `action_loop` 或 `action_stuck` fact。
- 不由 host 自动重试或换路线。

Acceptance:
- 同动作同目标失败两次后，下一轮 agent 能看到 loop fact。
- stuck 和 timeout 都进入 terminal/closure path。

### Step 6：更新 skill guidance

Touchpoints:
- `skills/gaming/stardew-task-continuity/SKILL.md`
- `skills/gaming/stardew-world/SKILL.md`

Work:
- 明确失败两次后观察/换方法/标 blocked。
- 明确 chain budget 超限时收口。
- 明确到达地点后继续动作是角色选择，不是宿主循环。
- 不把单个 NPC persona 作为本轮改动点；如发现已有 persona 硬编码地点/台词/动作编排，只在后续清理任务中处理。

Acceptance:
- 真实 repo skill 注入测试能看到指导。
- 无地点/NPC/台词硬编码。

### Step 7：harness 与回归测试

Touchpoints:
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeRecoveryTests.cs`

Work:
- 补 action chain state persistence。
- 补 repeated failures -> action_loop fact。
- 补 chain budget exceeded blocks next real action。
- 补 closure_missing repair only once。
- 补 delegated ingress busy defer attempts。

Acceptance:
- 所有新增行为均有 focused tests。
- hardcode scan 通过人工判读。

## Risks and mitigations

- Risk: chain guard 变成第二套任务系统。
  - Mitigation: chain 只存执行护栏 counters；承诺语义仍在 `todo`。

- Risk: host 开始替 NPC 决策。
  - Mitigation: budget 超限只 blocked/diagnostic，不自动选择目的地、台词或 todo 状态。

- Risk: prompt 过重。
  - Mitigation: chain fact 单行，详细历史留 runtime jsonl/recent_activity。

- Risk: 持久化迁移破坏旧 state。
  - Mitigation: 新字段 nullable/optional，旧记录反序列化为空。

- Risk: busy/defer attempt 写入 payload 造成 JSON 兼容复杂。
  - Mitigation: 优先扩展 ingress snapshot 字段；如改动过大，再用 payload metadata 但封装 helper。

## Verification commands

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests|FullyQualifiedName~NpcRuntimeRecoveryTests"
```

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
```

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
```

Hardcode scan:

```powershell
rg -n "Haley|Willy|Beach|Town|海边|镇|destination\\[|nearby\\[|moveCandidate\\[" src Desktop/HermesDesktop.Tests Mods/StardewHermesBridge -g "*.cs"
```

Hardcode scan 判定标准：

- 允许：测试 fixture 的样例 NPC/地点、日志断言样例、外部参考项目、`.omx` 计划文档。
- 允许：Stardew 原始 location id / NPC id 的数据适配层常量，前提是它们不是自然语言目的地解析规则。
- 允许：action-chain guard 默认预算数值只出现在 options/defaults 层和测试 fixture 中；controller 只能读取 options，不能写死 magic number。
- 禁止：生产 wake prompt、controller、autonomy loop、tool guard、skill 中新增“某自然语言地点必须映射到某坐标/某 NPC 必须去某地”的规则。
- 禁止：用 `Haley/Beach/海边` 等具体文本修复通用导航或 action-chain 行为。

## ADR

Decision:
在现有 NPC runtime controller 内补 action-chain guard，采用参考项目的单任务槽、短状态反馈、重复失败检测、stuck watchdog 和 tool id/result 可追踪原则；不新增 MCP server、不新增第二任务系统。

Drivers:
- 用户明确要求参考项目对齐。
- 当前已具备 `ActionSlot`、`LastTerminalCommandStatus`、`todo`、runtime state store。
- 缺口是连续 action chain 的运行时预算和失败循环事实，而不是工具协议表面。

Alternatives considered:
- 只靠 prompt：无法运行时限流。
- 外部 MCP runner：不能替代异步 terminal wake，且引入第二执行面。
- 宿主自动完成 todo：违反 agent-native 边界。

Why chosen:
Option B 最小化新架构，最大化复用现有基础设施，并与 hermescraft/hermes-agent 的实际做法一致。

Consequences:
- 需要扩展 runtime persisted state。
- 需要新增测试矩阵。
- agent 会看到更多事实约束，但宿主仍不提供行动候选。

Follow-ups:
- 稳定后可评估把 action-chain fact 暴露到 developer UI。
- 稳定后再评估 Stardew tool surface 的 MCP 包装，不作为本轮前提。

## Available-Agent-Types Roster

- `architect`：审查 runtime controller 是否膨胀、agent-native 边界是否守住。
- `executor`：实现状态扩展、controller 计数、prompt/skill 更新。
- `test-engineer`：补 harness 和 focused tests。
- `debugger`：定位 action slot、ingress、terminal fact、wake 的卡点。
- `code-reviewer`：审查硬编码、第二系统、host 决策越界。
- `verifier`：跑 focused/full tests、hardcode scan、日志验收。

## Follow-up staffing guidance

Ralph path:
- 一个 `executor` 顺序实现，适合避免 runtime state store 冲突。
- 一个 `verifier` 在完成后独立跑测试和 hardcode scan。

Team path:
- Lane 1 `executor`：runtime snapshot/state store/driver。
- Lane 2 `executor`：Stardew action controller/ingress defer。
- Lane 3 `test-engineer`：harness + regression tests。
- Lane 4 `code-reviewer`：边界和硬编码持续审查。

Launch hints:

```text
$ralph .omx/plans/prd-stardew-action-chain-guards-reference-alignment.md
$team .omx/plans/prd-stardew-action-chain-guards-reference-alignment.md
```

Team verification path:
- Team 先证明 focused tests 和 hardcode scan。
- Leader 再跑 Desktop focused tests、Bridge tests、Desktop build。
- 手测后按固定日志顺序检查 `hermes.log`、SMAPI、NPC runtime jsonl。
