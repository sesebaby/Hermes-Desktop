# E-2026-0513-stardew-ui-wait-generic-defer-budget

- id: E-2026-0513-stardew-ui-wait-generic-defer-budget
- title: Stardew UI/window waits must not consume generic host-task defer budget
- status: active
- updated_at: 2026-05-13
- keywords: [stardew, private_chat, ui_wait, window_lifecycle, host_task_submission, deferred_ingress, action_slot_busy, stardew_task_status]
- trigger_scope: [stardew, host-task, private-chat, ui-lifecycle, orchestration, bugfix]

## symptoms

- 玩家和 NPC 私聊后，NPC 自然回复里答应了现在行动，但游戏里没有移动。
- Runtime 里 host-task ingress 仍在等待 `private_chat_reply_closed` 这类 UI 生命周期事件。
- 这个等待会被计入 `MaxDeferredIngressAttempts`，最后变成 `host_task_submission_deferred_exceeded`。
- 只认 `private_chat_reply_closed` 会让已经 `private_chat_reply_displayed` 的回复继续阻塞后续 world action，导致“原版对话框已弹出但 NPC 不 move”。
- 如果 `private_chat_reply_displayed` 在 action slot 忙时到达并被 cursor 消费，而 delivery 事实没有持久化，slot 清空后 queued ingress 会重新等待并最终假 timeout。
- 如果 action slot 忙且 reply delivery 还没到，有 `conversationId` 的 private-chat ingress 仍不能先走 generic busy defer，否则会在玩家可见回复递送前耗尽 stale/busy budget。
- 如果 slot free 且当前 batch 已经看到 delivery，但 bridge submit 返回 retryable failure，保留的 ingress 也必须带着 delivery 事实；否则下一轮会丢失已 ack 的 delivery event。
- 私聊 prompt 若继续写“私聊回复关闭后才进入 host task submission lifecycle”，会把旧 hidden lock 模型教回 agent。
- 看起来像 bridge 没动、寻路失败或 agent 没决定，实际是玩家读 UI 的时间被错误当成 stale/busy ingress。

## root_cause

- `StardewNpcAutonomyBackgroundService.DeferPrivateChatReplyClosedIngressAsync` 复用了 generic stale/busy deferred ingress 路径。
- generic 路径的短预算适合真正的 action slot / pending work busy 或 stale ingress loop，但不适合玩家可见 UI/window lifecycle wait。
- 等待 reply delivery/display/close 是游戏事实和任务状态，不是 agent flow lock，也不是 stale work item。
- reply delivery 事实只扫当前 batch 不够；跨 action-slot busy 或 cursor ack 后必须能持久保存到 host-task ingress 或等价事实源。

## bad_fix_paths

- 不要提高 `MaxDeferredIngressAttempts` 掩盖分类错误。
- 不要给 private-chat move 增加隐藏队列、自动重试或 host 侧下一步推理。
- 不要从玩家文本或 NPC 回复文本里推断是否应该移动。
- 不要恢复 `local_executor`、JSON 文本解析或自然语言 fallback。
- 不要绕过 UI/menu/lease 安全；可恢复等待不是无限执行许可。

## corrective_constraints

- Recoverable game UI/window/menu/animation/event waits must be task/status facts.
- Private-chat reply lifecycle waits must keep the host task ingress recoverable and must not increment or block on the generic stale/busy defer budget.
- `private_chat_reply_displayed` and `private_chat_reply_closed` both count as reply delivery for the world-action submission gate; do not wait for close before submitting the agent-requested action.
- If matching delivery arrives while the body action slot is busy, persist that delivery fact before acknowledging the event cursor so later dispatches do not lose it.
- If delivery has not arrived yet, classify the ingress as `waiting_private_chat_reply_delivery` before action-slot generic busy deferral, even while the body action slot is busy.
- Persist reply delivery before any submit path that may retain ingress and acknowledge the cursor, including retryable bridge submission failures.
- If reply delivery never arrives, block with an independent `private_chat_reply_delivery_timeout` fact, not `host_task_submission_deferred_exceeded`.
- True action-slot/pending-work busy still uses the generic defer budget and may produce `host_task_submission_deferred_exceeded`.
- Agent-visible Stardew status/action results should include a short `summary` plus stable machine fields such as `status`, `commandId`, `reason`/`errorCode`.
- Conflicting physical world actions must return `blocked/action_slot_busy` facts without hidden queueing or host auto-retry.

## verification_evidence

- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithPrivateChatReplyWaitAtGenericBudget_RemainsRecoverable"` failed because a private-chat UI wait produced a terminal blocked status.
- RED: reviewer-added `RunOneIterationAsync_WithDelegatedMoveAfterPrivateChatReplyDisplayed_SubmitsMoveWithoutWaitingForClose`, `RunOneIterationAsync_WithPrivateChatReplyWaitAtGenericBudget_RemainsRecoverable`, and `RunOneIterationAsync_WithPrivateChatReplyWaitPastUiTimeout_BlocksWithDeliveryTimeout` failed because the runtime still waited for close, consumed the UI wait as a whole turn, and lacked an independent delivery timeout.
- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests.TaskStatusTool_WithRunningStatus_ReturnsSummaryAndCommandIdentity|FullyQualifiedName~StardewNpcToolFactoryTests.SubmitHostTaskTool_WhenQueued_ReturnsSummaryAndIdentity|FullyQualifiedName~StardewNpcToolFactoryTests.NavigateToTileTool_WhenActionSlotBusy_ReturnsBlockedSummaryWithoutQueuedWork" -p:UseSharedCompilation=false` failed because summary fields were absent.
- RED: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyTickDebugServiceTests.RepositoryStardewAssets_TeachUiLifecycleAsStatusFactsWithoutHostInferredNextAction" -p:UseSharedCompilation=false` failed because repository skills/runtime prompt did not state the non-blocking UI lifecycle boundary.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithPrivateChatReplyWaitAtGenericBudget_RemainsRecoverable" -p:UseSharedCompilation=false` passed.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithDelegatedMoveAfterPrivateChatReplyDisplayed_SubmitsMoveWithoutWaitingForClose|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithPrivateChatReplyWaitAtGenericBudget_RemainsRecoverable|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithPrivateChatReplyWaitPastUiTimeout_BlocksWithDeliveryTimeout" -p:UseSharedCompilation=false` passed, 3/3.
- RED/GREEN: `RunOneIterationAsync_WithPrivateChatReplyDisplayedAfterUiWaitAtGenericBudget_SubmitsMove` failed before the delivered UI wait bypassed stale generic attempts, then passed after delivered private-chat ingress no longer blocks on old UI-wait attempts.
- GREEN: `RunOneIterationAsync_WithPrivateChatReplyDisplayedWhileSlotBusy_PersistsDeliveryAndSubmitsAfterSlotClears` passed after matching reply delivery is persisted onto the ingress payload while the action slot is busy.
- GREEN: `RunOneIterationAsync_WithPrivateChatReplyNotDeliveredWhileSlotBusy_DoesNotConsumeGenericDeferBudget` passed after undelivered private-chat waits were classified before generic action-slot busy deferral.
- GREEN: `RunOneIterationAsync_WithPrivateChatReplyDisplayedAndRetryableSubmitFailure_PersistsDeliveryForRetry` passed after reply delivery is persisted before slot-free submit attempts that may retain ingress on retryable bridge failure.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~StardewNpcToolFactoryTests.TaskStatusTool_WithRunningStatus_ReturnsSummaryAndCommandIdentity|FullyQualifiedName~StardewNpcToolFactoryTests.SubmitHostTaskTool_WhenQueued_ReturnsSummaryAndIdentity|FullyQualifiedName~StardewNpcToolFactoryTests.NavigateToTileTool_WhenActionSlotBusy_ReturnsBlockedSummaryWithoutQueuedWork"` passed.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~StardewAutonomyTickDebugServiceTests.RepositoryStardewAssets_TeachUiLifecycleAsStatusFactsWithoutHostInferredNextAction"` passed.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests" -p:UseSharedCompilation=false` passed, 60/60.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests" -p:UseSharedCompilation=false` passed, 39/39.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyTickDebugServiceTests" -p:UseSharedCompilation=false` passed, 18/18.
- GREEN: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false` passed, 275/275 with 2 skipped live-AI tests.
- GREEN: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug -p:UseSharedCompilation=false` passed, 140/140 with an existing obsolete-warning in `BridgeMovementPathProbe.cs`.
- GREEN: `openspec status --change "stardew-agent-game-window-decoupling"` reported 4/4 artifacts complete.
- GREEN: `openspec validate "stardew-agent-game-window-decoupling" --strict` reported the change is valid.

## related_files

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `skills/gaming/stardew-core/SKILL.md`
- `skills/gaming/stardew-task-continuity/SKILL.md`
- `skills/system/stardew-npc-runtime/SYSTEM.md`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
