---
id: E-2026-0505-private-chat-lease-and-status-tool-budget
title: 私聊窗口打开态和状态工具轮询缺少硬边界会阻塞 NPC autonomy
updated_at: 2026-05-05
keywords:
  - stardew
  - private_chat
  - session lease
  - npc-autonomy
  - status_tool_budget_exceeded
  - stardew_status
  - query_status
  - phone overlay
---

## symptoms

- 玩家打开 Haley 的手机私聊线程后，即使没有继续输入，Hermes 日志仍反复出现 `reason=private_chat_session_active`，Haley autonomy 每轮被跳过。
- SMAPI/Bridge 已记录 `action_open_private_chat_completed ... thread_opened`，但 Desktop 侧把等待玩家输入的 UI 状态当成 active private-chat lease。
- 玩家可能长期不关闭手机窗口，这会让对应 NPC 像“被锁死”一样不再自主行动。
- Agent 在 Stardew autonomy turn 中可能反复调用 `stardew_status` / 细分 status 工具；SMAPI/Bridge 日志表现为多次 `query_status`；原实现只记录 `status_tool_budget_exceeded` warning，没有阻止继续真实查询，最终可能消耗 `MaxToolIterations`。

## trigger_scope

- 改动 private chat open / submit / cancel / close 生命周期。
- 给 NPC autonomy 增加 pause gate、lease、worker dispatch 或 runtime lock。
- 改动 Stardew status / observation 工具、Agent tool loop、tool budget 或 `MaxToolIterations` 行为。
- 从阻塞菜单迁移到可长期停留的 overlay / phone UI。

## root_cause

- `PrivateChatOrchestrator` 在提交 `OpenPrivateChat` 时就 acquire `IPrivateChatSessionLease`，而不是等玩家真正提交消息、NPC 正在生成私聊回复时才 acquire。`StardewNpcAutonomyBackgroundService` 只要看到 active lease 就暂停 NPC autonomy，因此被动打开的手机线程也会长期阻塞 agent 进程。
- `HermesPhoneOverlay.ClosePhone` 只在输入框聚焦时取消当前线程，导致被动可见线程关闭时不一定发出明确取消事件。
- `Agent` 的 status 工具预算只有日志诊断，没有执行层硬约束；模型重复请求同一个 Stardew status 工具时，系统仍会继续调用 bridge。

## bad_fix_paths

- 只在关闭按钮路径补取消事件，但继续在 `OpenPrivateChat` 阶段 acquire lease；这无法覆盖“手机窗口一直开着不关闭”的场景。
- 把 `private_chat_session_active` pause gate 从 autonomy 中删除；真正的私聊回复生成期仍需要短暂互斥，避免 private chat 和 autonomy 同时驱动同一个 NPC agent。
- 让 bridge/host 根据手机是否打开来决定 NPC 是否继续任务；手机打开是 UI 状态，不是 agent 决策状态。
- 只记录 `status_tool_budget_exceeded` warning，不返回工具结果约束模型行为；日志不会阻止真实 bridge 查询。
- 用提高 `MaxToolIterations` 掩盖重复状态查询；这会放大成本和延迟。

## corrective_constraints

- 打开手机私聊线程只表示 UI 正在等待玩家输入，不得 acquire private-chat session lease，也不得触发 autonomy pause。
- private-chat lease 只允许覆盖玩家已提交消息后、NPC 正在生成/提交私聊回复的短窗口，并且必须在回复路径结束时释放。
- overlay 关闭必须对当前可见线程发出明确取消事件，不能依赖输入框焦点判断业务生命周期。
- Stardew status 工具预算必须有执行层硬边界；同一 Agent turn 内重复调用同一个 status 工具时，应返回工具失败/提示结果，让模型使用已有事实继续行动，而不是继续查询 bridge。
- 回归测试必须同时覆盖核心状态机、Stardew wrapper、background service pause gate、bridge overlay 生命周期和 Agent tool loop。

## verification_evidence

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~PrivateChatOrchestratorTests|FullyQualifiedName~StardewPrivateChatOrchestratorTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_OpenPrivateChatWithoutPlayerMessageDoesNotPauseAutonomy|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_ActivePrivateChatLeasePausesAutonomy"` passed.
- `PrivateChatOrchestratorTests.ProcessNextAsync_OpenAccepted_DoesNotAcquireSessionLeaseAndCancelEndsSession` proves passive open has zero active lease.
- `StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_OpenPrivateChatWithoutPlayerMessageDoesNotPauseAutonomy` proves passive phone/private thread does not pause autonomy.
- `AgentTests.ChatAsync_WhenStardewStatusToolRepeatsAcrossIterations_ReturnsBudgetResultWithoutExecutingAgain` proves repeated `stardew_status` in one turn does not execute the real tool a second time.
- `RawDialogueDisplayRegressionTests.PassivePhoneCloseRecordsCancellationForVisiblePrivateThread` proves overlay close is not gated by text input focus.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug` passed: 896 passed, 1 skipped.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` passed: 86 passed.
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug` passed with 0 warnings and 0 errors.
