# Stardew 删除 Closure Lock 测试规格

## Scope

证明 `blocked_until_closure` 不再是执行锁；执行控制改由 `IngressWorkItems` 队列、`ActionSlot` / `PendingWorkItem` 单槽、bridge terminal status、`stardew_task_status` 和 `todo` 承担。

## Test Principles

1. 测旧锁状态下的新玩家请求是否能继续进入 bridge。
2. 保留 busy/single-slot 测试，避免把并发保护一起删掉。
3. 不用 Haley/Beach 做生产逻辑特例；测试 fixture 可以用样例 NPC/地点。
4. 断言 bridge submission，而不是只断言 runtime 状态。
5. 旧 `blocked_until_closure` 可以作为 legacy diagnostic 出现，但不能影响 action acceptance。

## Acceptance Criteria

### AC1：旧 closure block 不阻止 delegated move

Given:
- Runtime snapshot 有 `ActionChainGuard.GuardStatus=blocked_until_closure`。
- `BlockedReasonCode=closure_missing` 或其它旧 reason。
- 没有 active `ActionSlot` / `PendingWorkItem`。
- `IngressWorkItems` 有 player delegated move。

When:
- background service 处理 ingress。

Then:
- command service 收到 move。
- ingress 被移除。
- 没有 `action_chain_budget_exceeded` / `closure_missing` blocked result。

Suggested tests:
- `RunOneIterationAsync_WithDelegatedMoveAndLegacyBlockedClosure_SubmitsMove`
- `RunOneIterationAsync_WithDelegatedMoveAndLegacyPathBlockedClosure_SubmitsMoveWhenSlotFree`

### AC2：旧 closure block 不阻止 private chat reply

Given:
- private chat runtime driver 有旧 `blocked_until_closure` state。

When:
- 玩家在私聊里发消息，NPC 生成回复。

Then:
- `stardew_speak` / private chat reply command 被提交。
- `LastTerminalCommandStatus.Action=private_chat_reply`。
- reply 不需要先 close action chain。

Suggested test:
- `RuntimeAdapter_WithLegacyBlockedActionChain_SubmitsPrivateChatReply`

### AC3：closure_missing 只写诊断，不升级成锁

Given:
- terminal move completed。
- active todo 存在。
- agent 本轮没有工具，也没有结构化 action。

When:
- autonomy loop 运行。

Then:
- 可以写 `task_continuity` diagnostic。
- `ActionChainGuard.GuardStatus` 不变成 `blocked_until_closure`。
- `BlockedUntilClosure` 不被设 true。
- Next fresh world action 不被 closure diagnostic 阻断。

Suggested tests:
- `RunOneTickAsync_ClosureMissing_DiagnosticOnly_DoesNotBlock`
- `RunOneTickAsync_AfterClosureMissing_FreshMoveCanBeSubmitted`

### AC4：prompt/skill 不再要求解锁文本

Given:
- 构建 NPC autonomy prompt / skill prompt。

Then:
- 不包含“blocked_until_closure 时必须先...”。
- 不要求 `wait:` / `no-action:` 作为唯一收口格式。
- 包含参考项目式 guidance：长动作用 `stardew_task_status`，完成/失败后用 `todo` 或自然行动收尾。

Suggested tests:
- `StardewAutonomyTickDebugServiceTests.SystemPrompt_DoesNotMentionBlockedUntilClosureLock`
- `NpcAutonomyLoopTests.ActiveTodoTerminalFact_IsSoftContinuityPrompt`

### AC5：single slot busy 仍然保护并发动作

Given:
- active `ActionSlot` 或 `PendingWorkItem` 存在。

When:
- agent 或 ingress 尝试新 move/speak。

Then:
- 返回或记录 `action_slot_busy`。
- 不提交 bridge。
- ingress 不丢失，按 defer 计数和 next wake 处理。

Suggested tests:
- Existing busy tests should remain green.
- `RunOneIterationAsync_WithDelegatedMoveAndActionSlotBusy_DefersIngress`

### AC6：action budget/failure counters 不再 block

Given:
- action history counters 达到旧 `MaxActionsPerChain` 或 `MaxConsecutiveFailures`。
- 没有 active slot。

When:
- agent 选择新 world action。

Then:
- action 被接受并提交。
- counters 可更新为 diagnostic/history。
- 不返回 `action_chain_budget_exceeded`。

Suggested tests:
- `RuntimeActionController_WithLegacyMaxActionsExceeded_SubmitsNextAction`
- `RuntimeActionController_WithRepeatedFailures_SubmitsDifferentTargetAction`

### AC7：action_loop/stuck 是事实，不是全局锁

Given:
- recent history 显示同动作同目标连续失败。

When:
- 下一轮 prompt 构建。

Then:
- prompt/recent activity 可含 `action_loop`。
- agent 如果提交不同目标/不同动作，runtime 允许。

Suggested tests:
- `ActionLoopFact_DoesNotBlockDifferentWorldAction`
- `ActionLoopFact_RemainsDiagnosticOnly`
- `CommandStuck_MapsToExistingTerminalStatusWithCommandStuckReason`

### AC8：旧 stale recovery helper 被删除

Given:
- `RecoverClosureMissingGuardBeforePlayerDelegatedMoveAsync` 不再需要。

Then:
- 代码中没有该 helper 和调用点。
- 不再有只针对 `BlockedReasonCode=closure_missing` 的特殊绕过。

Suggested check:

```powershell
rg -n "RecoverClosureMissingGuardBeforePlayerDelegatedMove|recovered_stale_closure_missing_guard_before_player_delegated_move" src Desktop/HermesDesktop.Tests
```

应无生产代码匹配。

### AC9：`complete_command` 等价层不丢玩家请求

Given:
- private chat parent 写入 active todo。
- `npc_delegate_action` 创建 delegated move ingress。

When:
- bridge accept delegated move，background service 移除 ingress。

Then:
- 玩家请求仍以 active todo 可见。
- `LastTerminalCommandStatus` / recent activity 能把 bridge terminal result 关联到同一 trace/root todo/conversation。
- 只有 agent 显式把 todo 标 `completed` / `blocked` / `failed` 后，才算 reference 项目中 `complete_command` 的等价完成。

Suggested tests:
- `DelegatedMoveAccepted_RemovesIngressButKeepsActiveTodoUntilAgentCompletes`
- `DelegatedMoveTerminalCompleted_DoesNotAutoCompleteTodo`
- `TodoCompletedAfterTerminal_IsCommandCompletionEquivalent`

### AC10：生产代码不再赋值新的 blocked closure 状态

Given:
- implementation completed.

Then:
- 生产代码中不再出现 `GuardStatus = "blocked_until_closure"` 新赋值。
- 生产代码中不再出现 `BlockedUntilClosure = true` 新赋值。
- legacy DTO 字段和测试 fixture 可以存在，但不可作为 blocking 条件。

Suggested check:

```powershell
rg -n "GuardStatus = \"blocked_until_closure\"|BlockedUntilClosure = true" src
```

应无匹配。

## Verification Commands

Focused:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewNpcToolFactoryTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewPrivateChatOrchestratorTests|FullyQualifiedName~StardewAutonomyTickDebugServiceTests" -p:UseSharedCompilation=false
```

Build:

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false
```

String cleanup scan:

```powershell
rg -n "blocked_until_closure|ClosureMissing|MaxClosureMissing|BlockedUntilClosure|RecoverClosureMissingGuardBeforePlayerDelegatedMove|action_chain_budget_exceeded" src Desktop/HermesDesktop.Tests skills
```

Allowed after implementation:
- Historical error docs.
- Legacy DTO fields only if not used for blocking.
- Tests explicitly proving legacy blocked state does not block.
- String scan 中测试 fixture 可以保留 `blocked_until_closure`，但只用于证明 legacy state 不阻断。

Not allowed after implementation:
- TryBegin returning blocked because of `blocked_until_closure`.
- Prompt/skill telling agent it must unlock before any world action.
- Prompt/skill describing `action_chain_budget_exceeded` as a paused chain that must be unlocked.
- Background service special-casing closure_missing recovery.
- Production code assigning new `GuardStatus = "blocked_until_closure"` or `BlockedUntilClosure = true`.

## Manual Test

1. 打开 Stardew 和 Hermes。
2. 私聊 Haley：“我们去海边吧”。
3. 查看 Hermes log 是否处理 `npc_delegate_action` ingress。
4. 查看 SMAPI log 是否出现 `task_move_enqueued` / running / terminal。
5. 查看 Haley runtime jsonl 是否没有把 move 卡在 `blocked_until_closure`。

Logs:

```powershell
Get-Content "$env:LOCALAPPDATA\hermes\hermes-cs\logs\hermes.log" -Tail 200
Get-Content "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" -Tail 200
Get-ChildItem "$env:LOCALAPPDATA\hermes\hermes-cs\runtime\stardew\games\stardew-valley" -Recurse -Filter runtime.jsonl | Sort-Object LastWriteTime -Descending | Select-Object -First 5 FullName,LastWriteTime
```
