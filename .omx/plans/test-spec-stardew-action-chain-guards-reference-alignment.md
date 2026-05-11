# Stardew NPC 动作链护栏测试规格

## Scope

本测试规格覆盖 `.omx/plans/prd-stardew-action-chain-guards-reference-alignment.md` 的 action-chain guard。目标是证明：

- 连续动作可以自然发生。
- 连续动作不会无限推进。
- 重复失败、stuck、busy defer、closure missing 都会被 runtime 记录并反馈给 agent。
- 任务语义仍由 `todo` 管，不新增第二套任务系统。

## Test Principles

1. 测真实生产路径，fake 只用于 bridge command service、chat client、clock。
2. 断言必须用 `chainId/traceId/workItemId/commandId` 关联，不只看 action 名称。
3. 测试不依赖 Haley/Beach 等生产硬编码；测试 fixture 允许使用样例 NPC/地点。
4. prompt/skill 测试必须走真实 repo asset 注入路径。
5. 区分 agent-visible feedback 与 observability-only logs：前者断言 tool result / prompt / recent activity，后者断言 snapshot / activity sink / structured log。

## Acceptance Criteria

### AC1：Action chain state persistence

Given:
- NPC runtime 有一条 action chain。

When:
- state store 保存并重新加载。

Then:
- `ActionChainState` 保留 `ChainId`、计数、last action/status/reason。
- 旧 state 没有 action chain 字段时可正常加载为空。
- `HasPersistedControllerState` 在只有 action chain、没有 pending/actionSlot/ingress/nextWake 时仍认为需要恢复。
- 数据库迁移重复运行不报错。

Suggested tests:
- `NpcRuntimeRecoveryTests.ActionChainState_PersistsAcrossDriverReload`
- `NpcRuntimeRecoveryTests.LegacyControllerState_LoadsWithNullActionChain`

### AC2：single slot remains authoritative

Given:
- `ActionSlot` 或 `PendingWorkItem` 已存在。
- action chain 已接近或超过预算。

When:
- agent 再调用 world-writing tool。

Then:
- 返回 `action_slot_busy`。
- 不覆盖已有 slot/pending。
- 不返回 `action_chain_budget_exceeded`，因为参考项目 `currentTask` running 时先报告 busy。
- 不增加 `ConsecutiveActions`。
- 不覆盖 `LastTerminalCommandStatus`。
- action chain 写 busy/deferred observability diagnostic，带当前占用 command/work item。

Suggested tests:
- `StardewNpcToolFactoryTests.TryBegin_WhenSlotBusy_DoesNotOverwriteChainOrSlot`
- `StardewNpcToolFactoryTests.TryBegin_WhenSlotBusyAndChainBudgetExceeded_ReturnsBusyFirst`

### AC3：completed action updates chain but does not auto-complete todo

Given:
- active todo 存在，move terminal completed。

When:
- background service advance pending action。

Then:
- `LastTerminalCommandStatus` 是 completed。
- action chain 保留接受动作时已经增加的 `ConsecutiveActions`，terminal 阶段不得再次增加。
- completed 重置 failure counters。
- todo 仍由 agent 后续收口，host 不自动 completed。

Suggested tests:
- `StardewNpcAutonomyBackgroundServiceTests.CompletedAction_UpdatesChainWithoutMutatingTodo`
- `StardewNpcAutonomyBackgroundServiceTests.CompletedAction_DoesNotDoubleCountConsecutiveActions`

### AC4：terminal + active todo injects closure and chain facts

Given:
- `LastTerminalCommandStatus` terminal。
- active todo 存在。
- action chain 存在。

When:
- autonomy loop build decision message。

Then:
- prompt 含 `last_action_result`。
- prompt 含 `active todo closure required`。
- prompt 含短 `action_chain` fact。
- prompt 不含 `destination[n]`、`nearby[n]`、`moveCandidate[n]`。

Suggested tests:
- `NpcAutonomyLoopTests.RunOneTick_WithTerminalTodoAndChain_IncludesClosureAndChainFactsOnly`

### AC5：closure_missing repair is bounded

Given:
- terminal + active todo。

When:
- agent 第一轮没有工具也没有 `wait/no-action: reason`。

Then:
- 写 `closure_missing`。
- chain `ClosureMissingCount=1`。
- 允许一次 repair wake/self-check。

When:
- 第二次仍 missing。

Then:
- chain 标 blocked/guarded。
- 下一轮不允许继续真实动作，要求 todo/wait/no-action 收口。

Suggested tests:
- `NpcAutonomyLoopTests.ClosureMissing_FirstTimeSchedulesRepair`
- `NpcAutonomyLoopTests.ClosureMissing_SecondTimeBlocksFurtherWorldAction`

### AC6：repeated same action failure produces action_loop fact

Given:
- 同 action + 同 targetKey 连续 failed/blocked/cancelled 两次。

When:
- 下一轮 autonomy wake。

Then:
- prompt 或 recent activity 含 `action_loop` fact。
- fact 只描述事实和限制，不给地点候选。
- `action_slot_busy`、`action_chain_budget_exceeded` 等 guard rejection 不计入真实 bridge terminal failure counters，也不能单独触发 `action_loop`。

Suggested tests:
- `StardewNpcAutonomyBackgroundServiceTests.RepeatedSameTargetFailures_RecordActionLoopFact`
- `NpcAutonomyLoopTests.ActionLoopFact_DoesNotInjectMoveCandidates`
- `StardewNpcToolFactoryTests.GuardRejections_DoNotTriggerActionLoop`

### AC7：chain budget blocks next real action

Given:
- action chain 已达到 `MaxActionsPerChain`。

When:
- agent 尝试提交新的 world-writing action。

Then:
- tool result blocked，reason `action_chain_budget_exceeded`。
- 不提交 bridge command。
- agent 可继续调用 `todo`、read-only status、`wait/no-action`。
- 不创建 `PendingWorkItem`。
- 不创建 `ActionSlot`。
- 不申请或泄漏 world coordination claim。
- 不覆盖上一条真实 bridge command 的 `LastTerminalCommandStatus`。
- 如果写 terminal-like fact 给 agent，`commandId` 必须来自被拒绝 action/chain，不能复用上一条真实 bridge command id。
- agent-visible feedback 含 `action_chain_budget_exceeded`；observability-only 记录含完整 ids。

Suggested tests:
- `StardewNpcToolFactoryTests.ChainBudgetExceeded_BlocksWorldWritingAction`
- `NpcAutonomyLoopTests.ChainBudgetExceeded_AllowsTodoClosure`
- `StardewNpcToolFactoryTests.ChainBudgetExceeded_DoesNotCreateSlotOrClaim`
- `StardewNpcToolFactoryTests.ChainBudgetExceeded_DoesNotOverwriteLastTerminalCommandStatus`
- `StardewNpcToolFactoryTests.ChainBudgetExceeded_RecordsAgentVisibleAndObservableFacts`

### AC8：delegated ingress busy defer is visible and bounded

Given:
- delegated move ingress 排队。
- 当前 action slot busy。

When:
- background service 处理 ingress。

Then:
- 不 remove ingress。
- attempt/deferred count 增加。
- 写 `delegated_ingress_deferred` diagnostic。
- 设置 `NextWakeAtUtc`。
- 不增加 `ConsecutiveActions`。

When:
- attempts 超过上限。

Then:
- 如果没有 active slot：remove ingress，写 terminal-like blocked fact，下一轮 agent 看到结果并收口。
- 如果仍有 active slot：不覆盖 `LastTerminalCommandStatus`，写 ingress diagnostic 或 blocked ingress state。
- terminal-like fact 的 command/work id 指向 ingress 自身，不能冒充当前 bridge command。

Suggested tests:
- `StardewNpcAutonomyBackgroundServiceTests.DelegatedIngress_WhenSlotBusy_DefersWithAttemptCount`
- `StardewNpcAutonomyBackgroundServiceTests.DelegatedIngress_WhenDeferBudgetExceeded_BlocksAndWakesClosure`
- `StardewNpcAutonomyBackgroundServiceTests.DelegatedIngress_DeferBudgetExceeded_DoesNotOverwriteActiveActionTerminal`
- `StardewNpcAutonomyBackgroundServiceTests.DelegatedIngress_DeferDoesNotCountAsAcceptedChainAction`

### AC8.1：chain lifecycle avoids unrelated action accumulation

Given:
- 一个 action chain 已完成收口或过期。

When:
- agent 后续发起无关 world-writing action。

Then:
- 创建新的 `ChainId`。
- `ConsecutiveActions` 从 1 开始。
- 旧 chain 不参与 budget enforcement。
- chain 延续只依据 `RootTodoId`、`RootTraceId`、command/work item 关联或结构化工具上下文，不解析自然语言 reason/地点/台词。

Suggested tests:
- `StardewNpcToolFactoryTests.ClosedChain_NextUnrelatedActionStartsNewChain`
- `StardewNpcToolFactoryTests.ExpiredChain_NextActionStartsNewChain`
- `StardewNpcToolFactoryTests.ChainContinuation_DoesNotParseNaturalLanguageReason`

Given:
- open chain 存在。

When:
- 新 world-writing action 带相同 `RootTodoId`。
- 或带相同 `RootTraceId`。
- 或 command/work item 与上一条 pending/action slot/terminal 有明确关联。

Then:
- 延续同一个 `ChainId`。
- `ConsecutiveActions` 在 action 被接受时继续累计。
- 不因自然语言 reason 相似或地点文本相似而延续。
- 不同机械 id 的 action 创建新 chain。

Suggested tests:
- `StardewNpcToolFactoryTests.ChainContinuation_SameRootTodoId_ReusesOpenChain`
- `StardewNpcToolFactoryTests.ChainContinuation_SameRootTraceId_ReusesOpenChain`
- `StardewNpcToolFactoryTests.ChainContinuation_LinkedCommandOrWorkItem_ReusesOpenChain`
- `StardewNpcToolFactoryTests.ChainContinuation_DifferentMechanicalIds_StartsNewChain`

### AC9：stuck and timeout normalize into terminal path

Given:
- bridge 返回 `command_stuck` 或 running 超时。

When:
- background service advances pending action。

Then:
- `LastTerminalCommandStatus` status/reason 反映 stuck/timeout。
- slot/pending 清理。
- chain failure count 增加。
- active todo 不被宿主自动改状态。

Suggested tests:
- `StardewNpcAutonomyBackgroundServiceTests.CommandStuck_WritesTerminalChainFact`
- `StardewNpcAutonomyBackgroundServiceTests.RunningTimeout_IncrementsChainFailureAndClearsSlot`

### AC10：skill guidance uses repo assets and no hardcoding

Given:
- Prompt supplement / skill injection 使用真实仓库 assets。

When:
- 构建 Stardew NPC system prompt。

Then:
- 包含重复失败、stuck、chain budget 收口指导。
- 不包含地点/NPC/自然语言目的地硬编码规则。
- 不要求修改 Haley persona；若 persona 文件已有具体性格事实，不作为本轮验收失败，除非新增地点/台词/动作编排硬编码。

Suggested tests:
- `StardewAutonomyTickDebugServiceTests.SystemPrompt_IncludesActionChainGuardGuidance`
- hardcode scan 人工判读。

Hardcode scan 判定：

- 允许 false positives：测试 fixture、日志样例、Stardew 原始 id 数据适配层、外部参考项目、`.omx` 文档。
- 禁止新增：生产 wake prompt/controller/autonomy loop/tool guard/skill 中的具体 NPC、地点、自然语言目的地到坐标映射、固定剧情动作。

### AC11：feedback layering is explicit

Given:
- guard 事件发生，例如 chain budget exceeded、action loop、stuck、delegated ingress defer。

When:
- tool result / prompt / recent activity / runtime activity 被记录。

Then:
- agent-visible feedback 至少包含 `status`、`reasonCode`、短 action/chain 事实和可用收口方式。
- observability-only 记录至少包含 `chainId`、`traceId`、`workItemId`、`commandId`、`action`、`status`、`reasonCode`。
- prompt 不包含 observability-only 长日志、内部 JSON、候选地点列表。

Suggested tests:
- `NpcAutonomyLoopTests.ActionChainGuardFact_IsShortAgentVisibleFeedback`
- `StardewNpcToolFactoryTests.ActionChainGuardDiagnostic_IncludesTraceFields`
- `NpcAutonomyLoopTests.ActionChainGuardFact_DoesNotInjectDebugJsonOrCandidates`

### AC12：guard budgets are configurable options

Given:
- test runtime 使用非默认 action-chain guard options，例如 `MaxActionsPerChain=2`、`MaxConsecutiveFailures=1`、`MaxClosureMissing=0`、`MaxDeferredIngressAttempts=1`。

When:
- agent 触发对应 guard 条件。

Then:
- guard 按非默认 options 生效。
- controller 不依赖硬编码默认数值。
- hardcode scan/代码审查只允许默认数值出现在 options/defaults 层和测试 fixture。

Suggested tests:
- `StardewNpcToolFactoryTests.ChainBudget_UsesConfiguredMaxActions`
- `StardewNpcToolFactoryTests.RepeatedFailureGuard_UsesConfiguredMaxFailures`
- `NpcAutonomyLoopTests.ClosureMissingGuard_UsesConfiguredMaxClosureMissing`
- `StardewNpcAutonomyBackgroundServiceTests.DelegatedIngressDefer_UsesConfiguredMaxAttempts`

### AC13：world-writing classification is centralized

Given:
- Stardew tool surface 包含 world-writing action 和 read-only/status/todo/skill 工具。

When:
- 执行分类 helper/predicate 覆盖 move/speak/open private chat/idle micro action/read-only status。

Then:
- world-writing actions 进入 action-chain guard。
- read-only/status/todo/skill 工具不进入 guard，不消耗 action-chain budget。
- 新增 Stardew action 类型时必须通过同一个 helper/predicate 测试。

Suggested tests:
- `StardewNpcToolFactoryTests.WorldWritingClassification_WritingActionsEnterGuard`
- `StardewNpcToolFactoryTests.WorldWritingClassification_ReadOnlyToolsBypassGuard`

## Verification Commands

Focused tests:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests|FullyQualifiedName~NpcRuntimeRecoveryTests|FullyQualifiedName~StardewAutonomyTickDebugServiceTests"
```

Bridge tests:

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
```

Build:

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
```

Hardcode scan:

```powershell
rg -n "Haley|Willy|Beach|Town|海边|镇|destination\\[|nearby\\[|moveCandidate\\[" src Desktop/HermesDesktop.Tests Mods/StardewHermesBridge -g "*.cs"
```

## Manual Test Guidance

1. 私聊邀请 NPC 去一个地点。
2. 确认 NPC 到达。
3. 检查 NPC runtime jsonl：
   - 有 action terminal fact。
   - 有 chain fact 或 recent activity。
   - 有 closure turn。
4. 若 NPC 继续新动作，确认新动作完成后又进入下一轮 closure。
5. 连续失败/卡住时，确认 NPC 不无限重复同动作，而是观察、换方法、告知玩家、或标 todo blocked/failed。

固定日志读取顺序：

```powershell
Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\hermes.log" -Tail 200
Get-Content "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" -Tail 200
Get-ChildItem "$env:LOCALAPPDATA\hermes\hermes-cs\runtime\stardew\games\stardew-valley" -Recurse -Filter runtime.jsonl | Sort-Object LastWriteTime -Descending | Select-Object -First 5 FullName,LastWriteTime
```
