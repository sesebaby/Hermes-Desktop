# Stardew NPC 工具编排与 Harness 长期架构计划

## Requirements Summary

目标是从编排层修复 Stardew NPC 工具链，而不是继续单点修 `move`。计划覆盖：

- `stardew_navigate_to_tile`
- `stardew_speak`
- `stardew_idle_micro_action`
- `stardew_open_private_chat`
- `stardew_task_status`
- MCP 包装调用
- private-chat delegated action
- scheduled private chat

期望闭环：

`agent 调工具` -> `runtime 记录 action/pending` -> `bridge 执行` -> `status/terminal event` -> `NPC runtime 写 terminal fact` -> `按 wake policy 唤醒 agent` -> `agent 看到 last_action_result 后继续决策`

## Grounding Evidence

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:475`：NPC worker 顺序是 pending action -> ingress -> private chat lease -> cooldown -> LLM turn；任何门不释放都会表现为“工具没反应”。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:817`：pending/action advance 负责 command lookup、status、timeout/cancel、pause。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:1073`：每轮只处理一个 ingress；`:1128` delegated action 遇到 busy slot 当前会短路。
- `src/games/stardew/StardewNpcTools.cs:1079`：`StardewRuntimeActionController` 是 action slot/pending 的入口；`:1211` terminal status 写 `LastTerminalCommandStatus` 并清理 slot/claim。
- `src/runtime/NpcAutonomyLoop.cs:303`：`LastTerminalCommandStatus` 会变成下一轮 `last_action_result`。
- `Desktop/HermesDesktop.Tests/Mcp/McpServerTests.cs:243`：MCP navigate 已有 terminal feedback 测试，其它动作 parity 还缺。
- `external/hermescraft-main/bot/server.js:703`：action response 带 brief state，让 agent 看到动作期间的新事实。
- `external/hermescraft-main/bot/server.js:2587`、`:2610`：长动作返回 task id，状态单独查询。
- `external/hermescraft-main/bot/server.js:2676`：watchdog 把 stuck 转 terminal 状态。
- `external/hermes-agent-main/acp_adapter/events.py:47`、`:132`：tool start/complete 用 id/FIFO 配对，避免同名工具结果错配。

## RALPLAN-DR Summary

### Principles

1. **Agent 决策，宿主执行和反馈**：宿主不替 NPC 选择地点、台词、目标或下一步，只提供工具、执行、状态和事实回执。
2. **所有真实动作共享一条事实合同**：不同入口可以存在，但 terminal fact、trace、wake、diagnostic 语义必须一致。
3. **不做巨型总控**：`StardewRuntimeActionController` 只管 action slot、pending、terminal status、claim cleanup，不接管 private chat session、cursor、lease、host batch。
4. **失败必须可见且可恢复**：pending、lease、ingress、cursor、bridge status 卡住时必须产生可关联诊断或 terminal fact。
5. **Harness 锁编排，再改 production**：先用 fake bridge + 真实 runtime/host tick harness 复现失败，再改代码。

### Decision Drivers

1. **闭环一致性**：工具完成后 parent agent 必须看见事实并能继续决策。
2. **编排可诊断性**：日志能分清“没调工具 / 被 slot 或 lease 挡住 / bridge 执行失败 / terminal feedback 丢失”。
3. **测试覆盖面**：必须覆盖卡住、超时、重启恢复、cursor replay、同名/重复工具和 MCP/native/private-chat parity。

### Viable Options

#### Option A：逐工具补丁

逐个修 `move/speak/idle/open_private_chat`。

Pros:
- 短期 diff 小。
- 能快速修某个显眼症状。

Cons:
- 继续制造工具语义分叉。
- 无法证明“所有工具都不会卡住编排”。
- 容易重复最近的失败模式：修完一个门，又漏另一个门。

Verdict: 拒绝。

#### Option B：统一 action lifecycle fact contract + host-level harness

所有真实动作共享 `prepare -> submit -> accepted/blocked -> status -> terminal -> fact -> wake` 的事实合同。保留现有 runtime/service 边界，通过 decorator/harness/parity tests 补齐。

Pros:
- 符合 agent-native 边界。
- 复用现有 `StardewRuntimeActionController`、runtime state、MCP move feedback。
- 能用一套 harness 覆盖所有工具入口。

Cons:
- 需要梳理 parent native、MCP、private chat、scheduled ingress、local executor 多入口。
- 初始测试矩阵大。
- 如果边界失守，controller 可能膨胀成总状态机。

Verdict: 采用。

#### Option C：全部改成外部 MCP server

把 Stardew 动作全部拆成外部 MCP server。

Pros:
- 工具协议统一。
- 长期可做更清晰的外部 capability surface。

Cons:
- Stardew 动作是异步的，MCP 同步返回不能替代 terminal event/wake。
- 仍要解决 action slot、lease、ingress、cursor。
- 当前阶段改动面过大，会形成第二 tool lane 风险。

Verdict: 暂不采用。本轮只保证 MCP wrapper 走同一 lifecycle。

## ADR

Decision:
采用统一 action lifecycle fact contract 与 host-level orchestration harness；不新增外部 MCP server，不新增第二 tool lane，不新增 runtime dependency。

Drivers:
- 需要从编排层覆盖所有工具，而不是继续单点修 `move`。
- Stardew 的真实动作异步完成，必须有 terminal event -> runtime fact -> wake agent。
- 当前代码已有 action slot、state store、MCP move feedback 基础，应该复用。

Alternatives considered:
- 逐工具补丁：短期快，但无法收敛编排语义。
- 全部外部 MCP server：长期可评估，但不能替代异步完成闭环。

Why chosen:
Option B 最贴近现有架构，也最符合“agent 决策、宿主执行和反馈”的边界。

Consequences:
- 会新增较多 orchestration harness 测试。
- local executor 必须退出真实写动作路径。
- private chat 需要 lifecycle-aware command service decorator，但 core `PrivateChatOrchestrator` 保持纯净。

Follow-ups:
- lifecycle 稳定后，再评估是否把 Stardew tool surface 包装成独立 MCP server。
- harness 可后续提炼成通用 game bridge contract simulator。

## Implementation Steps

### Step 1：定义 action lifecycle fact contract

Touchpoints:
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcRuntimeStateStore.cs`

Work:
- 统一状态词：`submitting`、`queued`、`running`、`completed`、`blocked`、`cancelled`、`failed`、`timeout`、`stuck`。
- terminal fact 字段固定：`traceId`、`workItemId`、`commandId`、`action`、`status`、`reason_code`、`errorCode`、`blockedReason`、`updatedAtUtc`。
- 可恢复且影响 agent 决策的 terminal/blocked 进入 `LastTerminalCommandStatus`。
- 纯工程诊断进入 runtime jsonl/log，不注入地点候选或宿主建议。

Acceptance:
- 每个 action type 都能从 submit 走到 terminal 或 blocked fact。
- 日志和 runtime fact 可用 `traceId/workItemId/commandId` 关联。

Verification:
- 新增/扩展 `StardewNpcToolFactoryTests`。
- 新增 harness helper 断言 terminal fact shape。

### Step 2：实现 wake policy

Touchpoints:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcAutonomyLoop.cs`

Policy:
- 必须立即 wake：长动作 terminal、failure、blocked、timeout、stuck、private chat reply completed/failed、玩家新输入。
- 可合并到自然 tick：短 `speak` completed、短 `idle_micro_action` completed。
- 不 wake 只记日志：重复 busy diagnostic、非决策所需工程诊断、已被 terminal fact 覆盖的 status query。

Acceptance:
- failure/blocked/timeout 不被 cooldown 吞掉。
- 短成功动作不会造成 LLM 高频空转。
- wake reason 可见：`terminal_failure`、`player_private_message`、`action_completed`、`none_log_only`。

Verification:
- `StardewNpcAutonomyBackgroundServiceTests` 增加 wake policy cases。

### Step 3：收紧 action slot / pending / claim 失败面

Touchpoints:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewNpcTools.cs`

Work:
- 覆盖无 commandId 的 `command_submitting` timeout。
- bridge status 永远 in-flight 时统一 cancel/terminal fallback。
- claim rekey/clear 失败要有可关联诊断。
- `action_slot_busy` 返回当前占用的 command/work item 信息，不静默。

Acceptance:
- 任意 action slot 不会无限占用 worker。
- 可区分 `slot_busy`、`command_submitting`、`command_running`、`timeout_cancelled`、`claim_conflict`。

Verification:
- `StardewNpcAutonomyBackgroundServiceTests`：submitting without commandId、running timeout、busy slot。
- `StardewNpcToolFactoryTests`：busy result 不覆盖已有 slot。

### Step 4：重做 ingress 编排语义

Touchpoints:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`

Work:
- delegated action 遇到 busy slot 时记录 deferred diagnostic，保留 work item，设置 next wake。
- ingress 保留“每轮处理一个”的节流，但增加 retry age / attempt diagnostic。
- malformed/unsupported ingress remove 前写 diagnostic。
- scheduled private chat 与 private-chat delegated action 共享 ack/defer 语义。

Acceptance:
- 被已有动作挡住的 ingress 有可见日志和 retry。
- supported ingress 最终进入 lifecycle 或 terminal failure。
- malformed/unsupported ingress 不会反复卡队头。

Verification:
- `StardewNpcAutonomyBackgroundServiceTests`：delegated ingress busy/deferred/retry、malformed remove。

### Step 5：Local executor exit contract

Touchpoints:
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcLocalActionIntentTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`

Contract:
- `move`：不允许 local executor 真实执行。
- `speak`：不允许 local executor 真实执行。
- `open_private_chat`：不允许 local executor 真实执行。
- `idle_micro_action`：提升为 parent-visible lifecycle action，不允许 local executor 真实执行。
- `task_status/status/skill_view`：允许 read-only/local helper，不占 action slot，不写真实世界。

Acceptance:
- local executor tool surface 不含 `stardew_navigate_to_tile` / `stardew_idle_micro_action` 写动作工具。
- parent surface 暴露 `stardew_idle_micro_action` 并走 lifecycle。
- 所有真实写动作都在 parent transcript 或 private-chat runtime fact 中可追踪。

Verification:
- `StardewNpcToolFactoryTests`：parent/local tool surface。
- `NpcAutonomyLoopTests`：local executor 不执行真实写动作。
- `NpcLocalActionIntentTests`：保留 read-only/status intent，移除真实写动作执行期待。

### Step 6：Private chat lifecycle decorator

Touchpoints:
- `src/game/core/PrivateChatOrchestrator.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`

Work:
- `PrivateChatOrchestrator` 保持 game-core 纯净，只依赖 `IGameCommandService`。
- Stardew adapter 注入 lifecycle-aware `IGameCommandService` decorator。
- decorator 在 private-chat open/speak submit 和 terminal status 处写 action slot / terminal fact / wake。

Acceptance:
- private-chat open/speak 不绕过 terminal feedback。
- core orchestrator 不引用 Stardew runtime 类型。

Verification:
- `StardewPrivateChatOrchestratorTests`：open/speak terminal fact。
- `PrivateChatOrchestratorTests`：core 行为不依赖 Stardew runtime。

### Step 7：Private chat lease 超时释放

Touchpoints:
- `src/runtime/NpcRuntimeInstance.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`

Work:
- lease 只覆盖 NPC 正在生成/提交私聊回复窗口。
- stale lease 在 timeout/restart 后释放并写 diagnostic。
- 玩家手机窗口闲置不阻塞 autonomy。

Acceptance:
- stale lease 不会永久阻塞 autonomy。
- private chat player idle 不等于 active lease。

Verification:
- `StardewNpcAutonomyBackgroundServiceTests`：stale lease release、open phone does not pause autonomy。

### Step 8：Shared batch 异常隔离与 cursor replay 幂等

Touchpoints:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewRuntimeHostStateStore.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`

Work:
- 单 event / 单 NPC private chat 处理异常不能拖垮整个 host iteration。
- staged batch replay 不重复提交已完成 open/speak。
- cursor replay 不丢失 click/private message，也不重复处理已经 ack 的 work。

Acceptance:
- unsupported/malformed NPC event 有 diagnostic，但其它 NPC 继续 dispatch。
- replay batch 幂等。

Verification:
- `StardewNpcAutonomyBackgroundServiceTests`：shared processor exception isolation、replay no duplicate。
- `StardewRuntimeHostStateStoreTests`：cursor/staged batch state。

### Step 9：MCP / native / private-chat parity

Touchpoints:
- `src/runtime/AgentCapabilityAssembler.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `Desktop/HermesDesktop.Tests/Mcp/McpServerTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`

Work:
- parent native：`navigate/speak/open_private_chat/idle_micro_action/task_status`。
- MCP wrapper：补齐 exposed action terminal feedback parity。
- private-chat command service：open/speak 走 decorator。
- tool result 只报告 submit/blocked，不伪造异步完成。

Acceptance:
- 同一 action type 从 native/MCP/private-chat 入口进入，terminal feedback 行为一致。
- 下一轮 agent 能看到上一轮 action result。

Verification:
- `McpServerTests`：speak/open/idle parity。
- `NpcAutonomyLoopTests`：last_action_result 注入。

### Step 10：建立 host-level orchestration harness

Touchpoints:
- `Desktop/HermesDesktop.Tests/Stardew/*`
- `Desktop/HermesDesktop.Tests/Mcp/McpServerTests.cs`
- new helper candidate: `Desktop/HermesDesktop.Tests/Stardew/StardewToolOrchestrationHarness.cs`

Subtasks:
- Fake bridge command service：submit/status/cancel/idempotency lookup，可脚本化状态序列。
- Host tick harness：尽量实例化真实 `StardewNpcAutonomyBackgroundService`，或抽出可测 dispatch core。
- Assertion helpers：slot cleared、terminal fact written、next wake set、ingress removed/deferred、LLM called/not called、tool result visible。
- Parity scenarios：native/MCP/private-chat/delegated/scheduled ingress。

Fake boundary:
- 可以 fake：bridge command service、event source、chat client、clock。
- 必须真实：runtime instance、controller、state snapshot/state store、host cursor/staged batch 或等价生产代码。

Acceptance:
- 不启动 Stardew/SMAPI 即可复现 slot stuck、status running、lease stale、ingress busy、cursor replay、MCP feedback。
- Harness 覆盖真实 worker 门禁顺序：pending action -> ingress -> lease -> cooldown -> LLM turn。

Verification:
- 新增 `StardewToolOrchestrationHarnessTests` 或拆入现有 `StardewNpcAutonomyBackgroundServiceTests`。

## Test Matrix

- `move/speak/idle/open_private_chat` submit accepted -> terminal -> `LastTerminalCommandStatus`。
- 下一轮 autonomy sees `last_action_result`。
- failure/blocked/timeout 立即 wake。
- short success 按 wake policy 合并或不唤醒。
- accepted 但 commandId empty -> idempotency lookup success/fail -> timeout fallback。
- status 永远 running -> timeout cancel -> terminal timeout fact。
- action slot busy -> later action diagnostic，不覆盖已有 slot。
- delegated ingress busy -> diagnostic deferred，不 remove，稍后处理。
- malformed/unsupported ingress -> diagnostic and remove。
- local executor 不能真实执行 `move/speak/open_private_chat/idle_micro_action`。
- stale private chat lease -> release and resume autonomy。
- shared private chat processor exception -> 当前 event/NPC 失败，host iteration 继续。
- MCP/native/private-chat parity for exposed actions。
- cursor replay 不重复 open_private_chat/speak。

## Hard Gates

- 不新增外部 MCP server。
- 不新增第二 tool lane。
- 不新增 runtime dependency。
- 不把 private chat / cursor / lease 塞进 `StardewRuntimeActionController`。
- 不让 local executor 执行真实写动作。
- 不硬编码 NPC 名、地点名、自然语言地点规则或剧情规则。
- 不把 `destination[n]`、`nearby[n]`、`moveCandidate[n]` 这类宿主生成候选注入 autonomy wake prompt。

Hardcode scan gate:

```powershell
rg -n "Haley|Willy|Beach|Town|海边|镇|destination\\[|nearby\\[|moveCandidate\\[" src Desktop/HermesDesktop.Tests Mods/StardewHermesBridge -g "*.cs"
```

执行时必须人工判读扫描结果：测试 fixture 中合理样例可以存在，production 新增 NPC/地点/自然语言规则不允许。

## Risks And Mitigations

- Risk: lifecycle abstraction 变成大重构。  
  Mitigation: controller 限定在 action slot / pending / terminal / claim；lease/cursor/private chat 保持原边界。

- Risk: wake storm 增加 LLM 调用。  
  Mitigation: 先写 wake policy tests；短成功不默认 wake，failure/blocked/timeout 必须 wake。

- Risk: 有日志但不可关联。  
  Mitigation: 所有编排日志和 fact 带稳定 `reason_code`、`traceId`、`workItemId`、`commandId`。

- Risk: harness mock 太多，测不到真实卡点。  
  Mitigation: fake 只限 bridge/event/chat/clock，runtime/controller/state/host tick 用真实生产路径。

- Risk: private chat decorator 污染 core。  
  Mitigation: core `PrivateChatOrchestrator` 只见 `IGameCommandService`，Stardew adapter 包装 service。

## Verification Commands

Focused tests first:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests|FullyQualifiedName~StardewPrivateChatOrchestratorTests|FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~McpServerTests"
```

Bridge regression:

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
```

Desktop build:

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
```

Full main tests if focused tests pass:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## Available-Agent-Types Roster

- `architect`：守住 lifecycle/controller/private-chat 边界。
- `debugger`：定位 slot/lease/cursor/ingress 卡死。
- `executor`：实现生产改动和测试。
- `test-engineer`：建设 harness 与测试矩阵。
- `code-reviewer`：审查 agent-native 边界和硬编码风险。
- `verifier`：运行测试、检查日志与验收门禁。

## Follow-up Staffing Guidance

Ralph path:
- 一个 `executor` 顺序执行。
- 一个 `verifier` 独立验收。
- 适合先做最小闭环，避免多 lane 冲突。

Team path:
- Lane 1 `test-engineer`：fake bridge + host tick harness。
- Lane 2 `executor`：action lifecycle/wake policy/local executor exit。
- Lane 3 `executor`：private chat decorator、lease、ingress、cursor。
- Lane 4 `code-reviewer`：持续审查 host-vs-agent 边界和硬编码。

Suggested reasoning:
- architecture/debug/harness/review lanes 用 high。
- narrow executor lanes 用 medium。
- verification 用 high。

Launch hints:

```text
$ralph .omx/plans/stardew-tool-orchestration-harness-plan.md
$team .omx/plans/stardew-tool-orchestration-harness-plan.md
```

Team verification path:
- Team 先证明 harness matrix 通过。
- Leader 再跑 focused Desktop tests、MCP tests、bridge tests。
- 最后执行 hardcode scan gate 并人工判读。

## Consensus Review Changelog

- Architect iteration 1 要求收紧 private chat core 边界、local executor exit、MCP parity 和 host-level harness。
- Architect iteration 2 要求 `idle_micro_action` 不留二选一，明确提升为 parent-visible lifecycle action。
- Architect final verdict: APPROVE。
- Critic verdict: APPROVE。
- 已采纳 Critic 建议：拆细 Step 4/6、补 verification commands、补 hardcode scan gate、补稳定 `reason_code/traceId/workItemId` 要求、补“不新增外部 MCP server / 第二 tool lane / runtime dependency”硬门禁。
