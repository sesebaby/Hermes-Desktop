# Stardew 删除 Closure Lock 并对齐参考项目任务状态方案 PRD

## Requirements Summary

本次目标不是继续“缩小安全锁影响范围”，而是删除 `blocked_until_closure` 这条执行阻断锁。当前问题的根因是：本项目额外加了一层参考项目没有的自然语言收口锁，锁一旦因为私聊 UI、structured wait、todo 收口格式等误判进入 `blocked_until_closure`，后续玩家委托 move 在到达 bridge 前就被 runtime 拦住。

参考项目没有这种全局 closure lock。它用的是：

- `commandQueue` 保存玩家请求。
- `currentTask` 作为唯一正在执行的背景任务。
- `mc task` / brief state 暴露 running/done/error/stuck。
- `complete_command` 由 agent 在完成玩家请求后显式收尾。
- `actionHistory` / stuck watchdog 给出事实提示，不把后续动作锁死。

本项目应采用同样形状：

- 玩家私聊即时行动进入 `IngressWorkItems` 队列。
- 真实 Stardew 动作由 `ActionSlot` / `PendingWorkItem` 单槽保护。
- 进度用 `stardew_task_status` 查询。
- 完成/失败写 `LastTerminalCommandStatus` 和 recent activity。
- 玩家承诺状态仍用 `todo` 管。
- 重复失败/stuck/timeout 写事实和诊断，让 agent 自己观察、换方法、告知玩家或把 todo 标 blocked/failed。

## RALPLAN-DR Summary

### Principles

1. **队列和任务槽替代自然语言锁**：用机械状态控制执行，不用 `wait:` 文本解析控制行动许可。
2. **agent 决策，宿主反馈事实**：宿主不替 NPC 决定下一步，只返回 busy/running/done/error/stuck/action_loop 等事实。
3. **私聊请求不能被旧动作历史拦截**：玩家新委托是新的 queued command，不继承旧 closure debt。
4. **单身体单动作槽仍然保留**：删除 closure lock 不等于允许并发移动；busy 由 `ActionSlot` / `PendingWorkItem` 管。
5. **兼容旧 state，但不让旧 state 再有执行权**：历史 `action_chain_json` 可以读出来做诊断，不能拦截 bridge submission。

### Decision Drivers

1. 用户手测失败发生在 bridge 之前：没有 `task_move_enqueued`，说明执行入口被 runtime 锁挡住。
2. 参考项目证明正确抽象是 `commandQueue/currentTask/task status/complete_command`，不是 `blocked_until_closure`。
3. 当前已有 `IngressWorkItems`、`ActionSlot`、`PendingWorkItem`、`LastTerminalCommandStatus` 和 `todo`，无需新增系统。

### Viable Options

#### Option A：继续窄化 closure lock

Pros:
- 改动小。
- 能针对私聊 UI 和 structured wait 打补丁。

Cons:
- 仍保留参考项目没有的执行阻断锁。
- 还会继续出现新白名单：新的 terminal action、新工具状态、新私聊生命周期都可能误判。
- 旧 `blocked_until_closure` state 仍可能卡住玩家新请求。

Verdict: 拒绝。它就是这次用户反对的“头疼医头”。

#### Option B：删除执行阻断锁，保留 action facts

Pros:
- 对齐参考项目。
- 不再依赖自然语言 `wait:` / `no-action:` 文本格式。
- 仍保留 single slot、busy、defer、stuck、action_loop 等安全事实。
- 改动集中在现有 runtime，不新增第二系统。

Cons:
- 需要重写一批以 `blocked_until_closure` 为预期的测试。
- 需要小心避免删除掉真正需要的 busy/timeout/stuck 保护。

Verdict: 采用。

#### Option C：彻底删除 action-chain state 类型和持久化列

Pros:
- 最干净，避免命名继续误导。

Cons:
- 迁移风险更高，测试和旧 state 兼容工作更多。
- 当前已有 recent activity/action_loop 使用这些 counters，可分阶段清理。

Verdict: 本轮不采用。先删除执行阻断语义，保留兼容读取和诊断字段；后续单独重命名/瘦身为 action history。

## Functional Requirements

### FR1：删除 `blocked_until_closure` 的执行阻断能力

`StardewRuntimeActionController.TryBeginAsync` 不得再因为以下状态返回 blocked：

- `ActionChainGuard.BlockedUntilClosure == true`
- `ActionChainGuard.GuardStatus == "blocked_until_closure"`
- `ConsecutiveActions >= MaxActionsPerChain`
- `ConsecutiveFailures >= MaxConsecutiveFailures`
- `ClosureMissingCount > MaxClosureMissing`

这些状态最多形成 diagnostic/recent activity/action_loop fact，不能阻止 `stardew_navigate_to_tile`、`stardew_speak`、private chat reply、idle micro action 到达 bridge。

保留的硬保护只有：

- `ActionSlot` / `PendingWorkItem` 已存在时返回 `action_slot_busy`。
- world coordination claim 冲突时返回 command conflict。
- bridge / command service 自己返回的 path_blocked、running、failed、cancelled、stuck。

### FR2：废弃 closure_missing repair/block 流程

`NpcAutonomyLoop` 不应再执行：

- `RequiresClosureChoice` -> 没工具就写 `closure_missing` -> 计数 -> 升级 `blocked_until_closure`。
- `CloseActionChainAfterClosureAsync` 作为解锁必要路径。
- “当前动作链已被护栏阻断；你必须先...” 这种 prompt 文案。

替代行为：

- 如果 terminal action + active todo 存在，prompt 仍可以提示“你有 last_action_result 和 active todo，需要自己决定：完成 todo、继续动作、等待、或标 blocked/failed”。
- 如果 agent 没有工具、没有动作，最多写 `no_tool_decision` / `task_continuity` diagnostic，不改变动作许可。
- structured wait 是普通 no-action fact，不再承担“解锁”责任。

### FR3：旧 `action_chain_json` 兼容但无权阻断

读取旧 runtime state 时：

- `blocked_until_closure` 可以出现在 snapshot/recent activity 中用于诊断。
- 新动作开始时不得因旧状态被拒绝。
- 新动作被接受后，应把历史状态更新成非阻断的 observation/action history 状态，或清空阻断字段。

执行实现可二选一：

- 保留 `NpcRuntimeActionChainGuardSnapshot`，但 `BlockedUntilClosure` 和 `GuardStatus=blocked_until_closure` 只作为 legacy diagnostic。
- 或新增 helper `NormalizeLegacyActionChainGuardForExecution`，所有 TryBegin 路径先归一化。

### FR4：IngressWorkItems 对齐 commandQueue

私聊 `npc_delegate_action` 进入 `IngressWorkItems` 后，应像 hermescraft 的 `commandQueue`：

- player delegated move 是 queued command，不受旧 closure/history 拦截。
- `IngressWorkItems` 只负责“把玩家请求送进执行生命周期”，不是完整 command lifecycle 本身。
- 本项目的 `complete_command` 等价层是：private chat parent 先写入 `todo`，`npc_delegate_action` 带上 `traceId/rootTodoId/conversationId`，bridge terminal result 回到 `LastTerminalCommandStatus`，最后由 agent 显式把对应 `todo` 标 `completed` / `blocked` / `failed`。
- 因此，bridge accept 后可以移除 ingress，但玩家请求不能消失：它必须仍能通过 active todo、terminal result、recent activity 或 conversation/trace 事实被下一轮 agent 看见，直到 agent 显式更新 todo。
- 如果当前有 `ActionSlot` / `PendingWorkItem`，只 defer 并增加 `DeferredAttempts`，写 `delegated_ingress_deferred:action_slot_busy`，设置短 `NextWakeAtUtc`。
- 超过 defer 上限后，不能覆盖正在运行的 action terminal；只能把 ingress 自身标 blocked/diagnostic，或在无 active slot 时转成 terminal-like fact。
- 成功提交 bridge 后移除 ingress。

### FR5：ActionSlot/PendingWorkItem 对齐 currentTask

单任务槽继续是核心保护：

- 有 running/pending action 时，新真实动作返回 `action_slot_busy`，不提交 bridge。
- `stardew_task_status` 可以查进度。
- bridge terminal done/error/stuck 后，写 `LastTerminalCommandStatus`，清 slot/pending，唤醒 agent。
- timeout/stuck 进入 terminal fact，不自动换路线。
- 当前 Stardew contract 不新增 literal `stuck` terminal status；stuck 必须映射为现有 terminal status，例如 `blocked` 或 `failed`，并用 `ErrorCode/BlockedReason=command_stuck` 表达 stuck 原因。这样 `IsTerminalStatus` 不会漏掉 stuck 导致 slot 永久 busy。

### FR6：Action history/action_loop 保留为事实，不是锁

保留或重做 action history：

- 连续同 action/target failed/blocked/cancelled/stuck，写 `action_loop` fact。
- fact 告诉 agent“不要盲目第三次同动作同目标”，但不从 runtime 层禁止所有后续动作。
- 若 agent 选择另一个动作/目标，runtime 应允许提交。

### FR7：Skill/prompt 去掉锁语言

更新所有面向 agent 的文案：

- 删除 `blocked_until_closure`、`closure_missing` 作为必须先解锁的描述。
- 删除或改写 `action_chain_budget_exceeded` 作为“当前动作链被暂停/必须先解锁”的描述；它只能作为历史诊断或 repeated-action fact，不得表达执行锁。
- 改成参考项目语言：有玩家请求先处理；长动作运行时查 status；完成后告知玩家并用 todo 收尾；失败/stuck 时观察、换方法、或标 blocked/failed。
- 不要求 `wait:` / `no-action:` 固定文本格式。

## Non-goals

- 不新增 MCP runner。
- 不新增第二 task store。
- 不自动替 NPC 完成 todo。
- 不把“海莉去海边”写成特殊规则。
- 不删除 `ActionSlot` / `PendingWorkItem` busy 保护。
- 不改变父云子本地 lane 设计。

## Implementation Steps

### Step 1：先改测试预期，锁定“旧锁不得阻断”

Touchpoints:
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs`

Work:
- 把现有期待 `blocked_until_closure` 拦截动作的测试改成 RED：旧 blocked state 下，fresh delegated move/private chat reply 应提交或按 slot busy defer。
- 删除/改写 `ClosureMissing_SecondTimeBlocksFurtherWorldActionUntilClosure` 类测试。
- 增加 `OldBlockedUntilClosure_DoesNotBlockFreshWorldAction`。
- 增加 `ClosureMissing_DiagnosticOnly_DoesNotSetBlockedUntilClosure`。

### Step 2：移除 TryBegin 中 action-chain block 分支

Touchpoints:
- `src/games/stardew/StardewNpcTools.cs`

Work:
- 改 `PrepareActionChainGuard`：不返回 `BlockedResult`。
- 删除 `guard.BlockedUntilClosure`、`GuardStatus == blocked_until_closure`、max action/failure 导致 blocked 的逻辑。
- 删除 `RecordTerminalActionChainStatusAsync` 中把 terminal failure 升级成 `GuardStatus = "blocked_until_closure"` / `BlockedUntilClosure = true` 的分支。
- 保留 `ActionSlot/PendingWorkItem` busy 在最前面。
- 成功接受动作时更新 last action/target/counters 作为 history。
- terminal failure 只更新 failure counters/action_loop input，不设置 blocking state。

### Step 3：移除 autonomy closure lock 流程

Touchpoints:
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcAutonomyBudget.cs`

Work:
- 删除或停用 `_actionChainGuardOptions.MaxClosureMissing` 在 closure lock 中的使用。
- `WriteNoToolActionDiagnosticAsync` 不再调用 `RecordClosureMissingAsync`。
- 删除 `RecordClosureMissingAsync` 的 block 升级。
- `BuildActionChainFact` 不再输出“当前动作链已被护栏阻断”或“必须先收口才能继续世界动作”。
- `BuildActiveTodoClosureFact` 改成软提示：active todo + last result 是事实，不是 lock。

### Step 4：删除上一版 stale closure recovery 补丁

Touchpoints:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`

Work:
- 删除 `RecoverClosureMissingGuardBeforePlayerDelegatedMoveAsync`。
- 删除调用点。
- 原因：如果 TryBegin 不再因 closure block 拦截，这个恢复补丁没有存在意义，保留会制造第二条特殊路径。

### Step 5：更新 skills 和 debug prompt

Touchpoints:
- `skills/gaming/stardew-task-continuity/SKILL.md`
- `skills/gaming/stardew-world/SKILL.md`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`

Work:
- 删除“看到 blocked_until_closure 必须先...”文本。
- 替换为“看到 last_action_result/task_done/task_error/action_loop/stuck 后，自己决定 todo 收尾、继续新动作、等待、或告诉玩家”。
- 保留 `stardew_task_status` guidance。

### Step 6：保留/强化 ingress defer 和 task status 验证

Touchpoints:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`

Work:
- 确认 busy action slot 下 ingress 不丢、不 remove、计数、next wake。
- 超限不污染 active command terminal。
- 成功提交 bridge 后 remove ingress。

### Step 7：清理命名和错误文档

Touchpoints:
- `openspec/errors/E-2026-0511-stardew-private-chat-reply-action-chain-guard.md`
- 可选新增 error/decision doc。

Work:
- 更新错误教训：根因是“引入了参考项目没有的自然语言 closure lock”，修复方向是删除 execution block。
- 如保留 `NpcRuntimeActionChainGuardSnapshot`，在文档中标为 legacy/diagnostic，不再是 guard。

## Acceptance Criteria

- 旧 `ActionChainGuard.GuardStatus=blocked_until_closure` 不会阻止 fresh delegated move 到达 bridge command service。
- 旧 `ActionChainGuard.GuardStatus=blocked_until_closure` 不会阻止 private chat reply submit。
- `closure_missing` 不再升级成 `blocked_until_closure`。
- Prompt/skill 不再要求 `wait:` / `no-action:` 作为解锁文本。
- `ActionSlot` / `PendingWorkItem` busy 仍然阻止并发动作。
- Busy delegated ingress 会 defer/retry，而不是静默丢弃。
- Bridge accept 后 ingress 可以移除，但玩家请求仍必须通过 active todo/terminal/recent activity 可见，直到 agent 显式完成/阻塞/失败 todo。
- Stuck 使用现有 terminal status + `command_stuck` reason 表达，不引入未纳入 terminal set 的 literal `stuck` status。
- Repeated failure/action_loop/stuck 作为事实反馈存在，但不全局禁止新动作。
- Focused tests 通过。

## Risks and Mitigations

- Risk: 删除锁后 NPC 可能连续动作过多。
  - Mitigation: 仍有 `MaxToolIterations`、LLM turn timeout、single slot、bridge timeout、stuck/action_loop fact；连续行为由 agent 决策和事实反馈管理。

- Risk: 历史 tests 大量断言 `blocked_until_closure`。
  - Mitigation: 先按行为目标改 RED tests，再实现；不要为了过旧测试保留锁。

- Risk: 旧 state 中 `blocked_until_closure` 还在 recent activity 里吓到 agent。
  - Mitigation: prompt/skill 改成 legacy diagnostic 或不注入阻断文案；新动作接受后归一化状态。

- Risk: 删除太多导致 action_loop 事实丢失。
  - Mitigation: action history counters 可保留，但必须明确 diagnostic-only。

## Verification Steps

Focused tests:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewNpcToolFactoryTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewPrivateChatOrchestratorTests|FullyQualifiedName~StardewAutonomyTickDebugServiceTests" -p:UseSharedCompilation=false
```

Desktop build:

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false
```

Optional bridge tests:

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug -p:UseSharedCompilation=false
```

Manual log check after testing Haley private chat move:

```powershell
Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\hermes.log" -Tail 200
Get-Content "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" -Tail 200
Get-ChildItem "$env:LOCALAPPDATA\hermes\hermes-cs\runtime\stardew\games\stardew-valley" -Recurse -Filter runtime.jsonl | Sort-Object LastWriteTime -Descending | Select-Object -First 5 FullName,LastWriteTime
```

Expected manual evidence:

- Hermes log shows delegated move processed.
- SMAPI log shows `task_move_enqueued` / task running or terminal result.
- NPC runtime jsonl has ingress/task/terminal facts, but no new `blocked_until_closure` gate blocking the move.

## ADR

Decision:
删除 `blocked_until_closure` 作为执行阻断锁，改用参考项目同形态的 queue/current-task/status/complete/todo 状态机制。

Drivers:
- 参考项目没有同款全局 closure lock。
- 当前失败发生在 bridge 之前，说明 runtime lock 截断了玩家动作。
- 本项目已有 queue/task-slot/status/todo 基础设施。

Alternatives considered:
- 继续窄化锁：拒绝，仍会白名单式修补。
- 全删 action-chain state：暂缓，迁移和重命名可后续做。
- 新 MCP runner：拒绝，本问题不需要第二执行面。

Why chosen:
Option B 直接消除根因，同时保留真正必要的执行安全：single slot、busy/defer、bridge terminal、stuck/action_loop facts。

Consequences:
- 旧 guard 相关测试要反向改。
- 旧 action-chain 字段短期会成为 legacy/diagnostic。
- 后续可以单独把类型重命名为 action history。

Follow-ups:
- 实现后手测 Haley 私聊“去海边”，确认 bridge 收到 move。
- 稳定后清理 `blocked_until_closure` 字符串和 `BlockedUntilClosure` 字段。

## Available-Agent-Types Roster

- `executor`: 实现删除锁和测试更新。
- `test-engineer`: 设计 RED/GREEN focused tests。
- `debugger`: 跟日志确认 bridge 前后是否被截断。
- `architect`: 审查是否真正对齐 queue/currentTask/status，而不是换名保留锁。
- `code-reviewer`: 审查是否留下特殊恢复路径或硬编码 Haley/Beach。
- `verifier`: 跑测试、build、日志手测检查。

## Follow-up Staffing Guidance

Ralph path:
- 单个 `executor` 顺序修改源码和测试，避免 runtime shared files 冲突。
- 完成后交给 `verifier` 跑 focused tests/build。

Team path:
- Lane 1 `executor`: `StardewNpcTools.cs` 删除 TryBegin guard block。
- Lane 2 `executor`: `NpcAutonomyLoop.cs` 删除 closure_missing block/prompt。
- Lane 3 `test-engineer`: 更新 tests 和 skill prompt assertions。
- Lane 4 `code-reviewer`: 检查锁字符串、特殊恢复路径、硬编码。

Launch hints:

```text
$ralph .omx/plans/prd-stardew-remove-closure-lock-reference-task-state-alignment.md
$team .omx/plans/prd-stardew-remove-closure-lock-reference-task-state-alignment.md
```

Team verification path:
- Team 先跑 focused tests。
- Leader 再跑 Desktop build。
- 手测按固定日志顺序确认 move 到达 SMAPI bridge。
