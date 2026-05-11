# Stardew NPC 工具编排与 Harness RALPLAN-DR 初稿

## RALPLAN-DR Summary

### Principles

1. **Agent 决策，宿主执行和反馈**：宿主只负责 action slot、状态推进、bridge 执行、terminal fact、唤醒，不替 NPC 选择目标、地点或台词。
2. **所有真实动作共享一条事实合同，不做巨型总控**：`move`、`speak`、`idle_micro_action`、`open_private_chat`、delegated action、scheduled private chat、MCP 调用都必须产出一致的 submit/status/terminal/wake 事实；但 private-chat session、cursor、lease、host batch 仍保留在各自编排层。
3. **失败必须可见且可恢复**：任何 pending、lease、ingress、cursor、bridge status 卡住，都要有 terminal/diagnostic/cooldown，不允许静默挡住 LLM turn。
4. **Harness 先锁编排，不靠手测猜测**：用 fake bridge/runtime harness 覆盖门禁、超时、事件 replay、terminal feedback，再用少量 bridge contract tests 兜住 SMAPI 边界。
5. **参考项目借鉴模式，不照搬实现**：借鉴 HermesCraft 的 brief state、background task、watchdog，借鉴 Hermes Agent ACP 的 tool start/complete 配对；不引入 Python/Node runtime，不照搬 Minecraft 逻辑。

### Decision Drivers

1. **闭环一致性**：工具完成后必须让 parent agent 看见事实并能继续决策。
2. **编排可诊断性**：看日志能分清“没调工具 / 工具被 slot 或 lease 挡住 / bridge 执行失败 / terminal feedback 丢失”。
3. **测试覆盖面**：不只验证 happy path，还要验证卡住、超时、重启恢复、cursor replay、同名并发/重复工具。

### Viable Options

#### Option A：补丁式逐工具修复

逐个修 `move`、`speak`、`idle`、`open_private_chat` 的 timeout、日志和 feedback。

Pros:
- 短期 diff 小。
- 可以快速修一个显眼症状。

Cons:
- 继续产生工具之间的语义差异。
- 容易让 `move` 有完整闭环，而 `speak/open/idle` 仍走不同路径。
- harness 会碎片化，无法证明“所有工具都不会卡住编排”。

Verdict: 不推荐。它重复了这几天的失败模式：单点 patch 修完一个症状，又暴露另一个门禁。

#### Option B：统一动作生命周期控制器 + 编排 harness

把所有真实动作统一纳入一个 action lifecycle contract：prepare -> submit -> accepted/blocked -> status -> terminal -> fact -> wake。`StardewRuntimeActionController` 保留为 action slot / terminal fact 的核心，但不接管 private chat session、lease、cursor 或 host staged batch；这些编排层只通过统一事实合同汇合。

Pros:
- 保持 agent-native 边界，宿主不做目标决策。
- 能用一套测试覆盖所有工具。
- 直接对齐现有 `LastTerminalCommandStatus` / `last_action_result` 方向。
- 不需要新增运行时依赖。

Cons:
- 需要梳理多个入口：MCP、native tool、private chat delegated action、scheduled private chat。
- 初始测试矩阵较大。
- 如果边界没守住，可能把 controller 膨胀成跨 private chat、cursor、lease 的总状态机；实施时必须把 controller 限定在 action slot 与 terminal fact。

Verdict: 推荐。

#### Option C：把所有 Stardew 动作改成外部 MCP server

把 `move/speak/idle/open/status` 包成 MCP server，agent 只调 MCP。

Pros:
- 工具协议统一，调用方天然拿同步 tool result。
- 长期可以更清晰地区分 Stardew 能力面。

Cons:
- Stardew 动作本质异步，MCP 同步返回不能替代 terminal event/wake。
- 当前 in-process runtime 已有 action slot、state store、private chat、MCP wrapping，强行拆外部 server 会扩大改动面。
- 仍需解决 lease、ingress、cursor、terminal fact，因此不是最小闭环。

Verdict: 长期可作为包装层，但不作为本轮主方案。本轮先做统一 lifecycle，MCP 工具走同一 lifecycle 即可。

## Recommended Decision

采用 **Option B：统一动作生命周期事实合同 + 编排 harness**。

MCP 不是被否定，而是作为同一 lifecycle 的一个入口。关键不是“工具叫 MCP 还是 native”，而是所有入口最终都要产生一致的 runtime state、terminal fact 和 wake。

边界修正：`StardewRuntimeActionController` 不应成为私聊、cursor、lease 的总控。它只管理 action slot / pending work / terminal status / claim cleanup。`StardewNpcAutonomyBackgroundService` 继续拥有 worker 门禁顺序，`PrivateChatOrchestrator` 继续拥有私聊协议状态，Stardew adapter/command-service decorator 负责把私聊里的真实 open/speak action 接入 runtime fact。

## Reference Patterns To Borrow

- `external/hermescraft-main/bot/server.js:703`：每次 action response 都带 `briefState`，让 agent 看到动作期间发生的新事实。对应到本项目：terminal fact / diagnostic fact 要进入 NPC runtime，并在下一轮作为 `last_action_result` 或 `last_tool_blocker` 呈现。
- `external/hermescraft-main/bot/server.js:2587` 和 `:2610`：长动作返回 task id，状态单独查询。对应到本项目：bridge commandId 是 task id，action slot 是 runtime 侧 task lease，`stardew_task_status` 是读状态，不应另起执行路径。
- `external/hermescraft-main/bot/server.js:2676`：watchdog 把 stuck 转为 terminal 状态。对应到本项目：action slot timeout、bridge running timeout、stale lease recovery 都应产生 terminal/blocked fact，而不是无限 pause。
- `external/hermes-agent-main/acp_adapter/events.py:47` 与 `:132`：tool start/complete 用 FIFO 配对，避免同名工具并发结果错配。对应到本项目：action traceId/workItemId/commandId/idempotencyKey 必须可追踪，MCP/native/delegated 路径要能映射到同一 terminal fact。

不照搬边界：
- 不引入 Node/Mineflayer background task server。
- 不把 Minecraft 的 perception/action 规则搬到 Stardew。
- 不把 ACP 协议作为 NPC runtime 必需依赖。
- 不让宿主根据自然语言替 NPC 选择目标。

## Implementation Plan

### Step 1：定义 Stardew action lifecycle contract

Touchpoints:
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcRuntimeStateStore.cs`

Work:
- 明确所有真实动作状态：`submitting`、`queued`、`running`、`completed`、`blocked`、`cancelled`、`failed`、`stuck/timeout`。
- 规定所有 action type 的 terminal status 都可写 `LastTerminalCommandStatus`，包括 `speak`、`idle_micro_action`、`open_private_chat`；是否立即 wake 由 Step 1.1 的 wake policy 决定。
- blocked/invalid/unsupported 不提交 bridge 也要写可观察 diagnostic fact，避免用户只看到“没动”。
- 明确 diagnostic fact 的承载：可恢复且影响 agent 决策的 terminal/blocked 进入 `LastTerminalCommandStatus`；纯工程诊断进入 runtime jsonl/log；下一轮 prompt 只注入 `last_action_result` 和必要的 `last_tool_blocker`，不把 host 候选或地点建议注入给 agent。

Acceptance:
- 每个 action type 都能从 submit 走到 terminal 或 blocked fact。
- terminal fact 至少包含 `commandId/workItemId`、`action`、`status`、`errorCode/blockedReason`、`traceId`。

### Step 1.1：定义 wake policy，避免反馈闭环变成 LLM 风暴

Touchpoints:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcAutonomyLoop.cs`

Work:
- 必须立即 wake：真实长动作 terminal、失败、blocked、timeout、stuck、private chat reply completed/failed、玩家新输入。
- 可合并到下一次自然 tick：短 `speak` completed、短 `idle_micro_action` completed、只读 `task_status` result。
- 只写日志不唤醒：重复 busy diagnostic、非决策所需工程诊断、已被同一 terminal fact 覆盖的状态查询。
- wake reason 必须可见，例如 `wake=terminal_failure`、`wake=player_private_message`、`wake=action_completed`、`wake=none_log_only`。

Acceptance:
- 测试能断言某个 terminal fact 是否触发下一轮 autonomy，而不是所有 fact 一律唤醒。
- 失败和 blocked 不会被 cooldown 吞掉；短成功不会造成 LLM 高频空转。

### Step 2：修正 action slot / pending / claim 的编排失败面

Touchpoints:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewNpcTools.cs`

Work:
- 对 `command_submitting` 无 commandId 的场景补齐所有 action type 的 timeout 证据。
- 对 bridge status 永远 in-flight 的场景统一 cancel/terminal fallback。
- 对 claim rekey/clear 加强诊断，确保 move claim 泄漏不会静默挡住后续 move。
- action slot busy 时返回明确 diagnostic，说明被哪个 command/workItem 挡住。

Acceptance:
- 任意 action slot 不会无限占用 worker。
- 日志能区分 `slot_busy`、`command_submitting`、`command_running`、`timeout_cancelled`、`claim_conflict`。

### Step 3：重做 ingress 编排语义

Touchpoints:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`

Work:
- delegated action 遇到 busy slot 时不再静默 `return true`，必须记录 deferred diagnostic，保留 work item，并设置合理 next wake。
- ingress 每轮只处理一个可以保留，但要防止 starvation：记录重试次数/age，超时后 terminal failed 或 diagnostic。
- scheduled private chat 与 private-chat delegated action 走同一 ingress lease/ack 策略。
- non-move delegated action 不再走旧 local executor 黑盒执行；统一转为 parent-visible action lifecycle，或明确只允许 read/status/skill lookup 类本地辅助。

Acceptance:
- 被已有动作挡住的 ingress 有可见日志和 retry。
- malformed/unsupported ingress 被 remove 并写 diagnostic。
- supported ingress 最终进入 action lifecycle 或产生 terminal failure fact。

### Step 3.1：Local executor exit contract

Touchpoints:
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcLocalActionIntentTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`

Contract:
- `move`：不允许 local executor 真实执行。parent autonomy 必须自己用 `skill_view` 解析 target 后调用 parent-visible `stardew_navigate_to_tile` / MCP wrapper。
- `speak`：不允许 local executor 真实执行。parent 或 private-chat orchestrator 通过 lifecycle-aware command service 提交。
- `open_private_chat`：不允许 local executor 执行。只允许 private-chat ingress / player click / scheduled private chat 入口提交。
- `idle_micro_action`：提升为 parent-visible lifecycle action，并从 local executor 真实写动作能力中移出。它仍然可以是短动作，但必须像其他真实动作一样走 action slot、terminal fact、wake policy、harness parity。
- `task_status`：允许作为 read-only/local helper，但不得占 action slot，不得写真实动作。
- `status/observe/skill_view`：允许 read-only/local helper，不得产生真实写操作。

Acceptance:
- tool surface policy 测试明确 parent/local 各自工具清单。
- `NpcLocalExecutorRunner` 不能调用 `stardew_navigate_to_tile` 执行真实移动。
- 所有真实写动作都能在 parent transcript 或 private-chat runtime fact 中被追踪。

### Step 4：private chat lease 和 cursor 恢复

Touchpoints:
- `src/runtime/NpcRuntimeInstance.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewRuntimeHostStateStore.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/game/core/PrivateChatOrchestrator.cs`

Work:
- lease 只覆盖“NPC 正在生成/提交私聊回复”的窗口，不覆盖玩家手机窗口闲置。
- stale lease 在重启/超时后释放并写 diagnostic。
- shared batch 处理异常隔离到单 NPC/单 event，不让整个 host iteration 停摆。
- cursor/staged batch replay 必须幂等，避免重复 open_private_chat 或丢失 click/private message。
- `PrivateChatOrchestrator` 保持 game-core 纯净，不直接依赖 `NpcRuntimeDriver` 或 `StardewRuntimeActionController`。Stardew adapter 提供 lifecycle-aware `IGameCommandService` decorator，在 open/speak 提交和 terminal status 处写 action slot/terminal fact/wake。

Acceptance:
- stale lease 不会永久阻塞 autonomy。
- private chat processor 单个 unsupported NPC 或异常不会挡住其他 NPC。
- replay batch 不重复提交已完成 open/speak。
- private-chat open/speak 不再绕过 runtime terminal feedback，但 core orchestrator 不知道 Stardew runtime 细节。

### Step 5：MCP/native/delegated parity

Touchpoints:
- `src/runtime/AgentCapabilityAssembler.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `Desktop/HermesDesktop.Tests/Mcp/McpServerTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`

Work:
- 按 surface 分组做 parity，而不是假设所有工具都已暴露：
  - parent native：当前 `stardew_navigate_to_tile`、`stardew_speak`、`stardew_open_private_chat`、`stardew_task_status`。
- local executor：只保留 read-only/status/skill lookup；不保留 `move/speak/open_private_chat/idle_micro_action` 真实写动作能力。
  - MCP wrapper：当前已验证 `stardew_navigate_to_tile`，需补 exposed action 的 parity 测试。
  - private-chat command service：open/speak 通过 lifecycle-aware decorator 接入。
- tool result 只报告 submit/blocked，不伪造异步完成。
- terminal completion 通过 runtime fact 唤醒 agent，而不是依赖当前同步 tool return。
- prompt/skill 更新为“工具调用后等待真实 terminal fact”，不要求 agent 手写轮询循环。

Acceptance:
- MCP、native、private-chat command service 对同一 action type 的 terminal feedback 行为一致。
- agent 下一轮能看到上一轮 action 的 terminal result。

### Step 6：建立 orchestration harness

Touchpoints:
- `Desktop/HermesDesktop.Tests/Stardew/*`
- `Desktop/HermesDesktop.Tests/Mcp/McpServerTests.cs`
- optionally new `Desktop/HermesDesktop.Tests/Stardew/StardewToolOrchestrationHarness.cs`

Work:
- 建一个内存 fake bridge command service，支持 submit/status/cancel/idempotency lookup、可脚本化状态序列。
- 建一个 host-level orchestration harness，尽量实例化真实 `StardewNpcAutonomyBackgroundService` 或抽出可测 dispatch core，能驱动 pending/action/ingress/lease/cursor/staged batch 多轮 tick。
- 建 helper 断言：slot cleared、terminal fact written、next wake set、ingress removed/deferred、LLM turn called/not called、tool result visible。
- 复用真实 `NpcRuntimeInstance` / state snapshot，不做纯 mock 的假系统。
- fake 只放在 bridge command service、event source、chat client、clock；runtime instance、controller、state store、host cursor/staged batch 走真实实现或等价生产代码。

Acceptance:
- 不启动 Stardew/SMAPI 即可复现：slot stuck、status running、lease stale、ingress busy、cursor replay、MCP terminal feedback。
- Harness 覆盖所有 action type。
- Harness 覆盖真实 worker 门禁顺序：pending action 优先、ingress 次之、lease pause、cooldown、LLM turn。

### Step 7：测试矩阵

Tests:
- `move/speak/idle/open_private_chat` submit accepted -> terminal -> `LastTerminalCommandStatus` -> next autonomy sees `last_action_result`。
- wake policy：failure/blocked/timeout 立即 wake，短成功按策略合并或不唤醒。
- submit accepted 但 commandId empty -> idempotency lookup success/fail -> timeout fallback。
- status 永远 running -> timeout cancel -> terminal timeout fact。
- action slot busy -> later action returns diagnostic and does not overwrite existing slot。
- delegated ingress busy -> diagnostic deferred, not removed, later processed。
- malformed/unsupported ingress -> diagnostic and remove。
- local executor exit：`move/speak/open_private_chat/idle_micro_action` 不能由 local executor 真实执行；`task_status/status/skill_view` 保持 read-only。
- stale private chat lease -> release and resume autonomy。
- shared private chat processor exception -> one event/NPC fails but host iteration continues。
- MCP/native parity for all exposed Stardew actions。
- bridge cursor replay does not duplicate open_private_chat/speak。

## Risks And Mitigations

- Risk: lifecycle abstraction过大，变成重构泥潭。  
  Mitigation: 先只抽测试 helper 和状态合同，production 改动仍落在现有 `StardewRuntimeActionController` / background service。

- Risk: 过度唤醒 agent，导致 LLM 调用变多。  
  Mitigation: terminal wake 只对真实 terminal/failure/blocked fact；cooldown 与 private chat ingress 仍分层。

- Risk: MCP 化被误解成外部 server 大改。  
  Mitigation: 本轮只做 MCP/native parity，不新增外部 MCP server。

- Risk: harness 使用过多 mock，测不到真实 runtime。  
  Mitigation: fake bridge 可以 mock，但 runtime instance/state/controller 使用真实类。

## ADR

Decision:
采用统一 action lifecycle fact contract 与 orchestration harness，先不把 Stardew 动作拆成外部 MCP server，也不把 `StardewRuntimeActionController` 扩成私聊/cursor/lease 总控。

Drivers:
- 需要覆盖所有工具，而不是继续单点修 `move`。
- Stardew 动作是异步执行，必须有 terminal event -> runtime fact -> wake agent。
- 当前代码已有 action slot、state store、MCP move feedback 基础，应复用。

Alternatives considered:
- 逐工具补丁：短期快，但继续制造语义分叉。
- 全部外部 MCP server：长期可讨论，但不能替代异步 terminal/wake，当前改动面过大。

Why chosen:
统一 lifecycle 最贴近现有架构，也最符合 agent-native 原则：agent 决策，宿主执行和反馈。

Consequences:
- 会新增较多 harness 测试。
- 会要求真实写动作退出旧 local executor 黑盒路径。
- 所有 action type 都要补齐 terminal fact parity。
- private chat 需要 lifecycle-aware command service decorator，但 core `PrivateChatOrchestrator` 保持纯净。

Follow-ups:
- 若 lifecycle 稳定，再评估是否把 Stardew tool surface 包装为独立 MCP server。
- 后续可把 harness 提炼成 bridge contract simulator，服务更多游戏接入。

## Available-Agent-Types Roster

- `architect`：审查 lifecycle 边界、MCP/native parity、host-vs-agent 决策边界。
- `debugger`：定位 action slot、lease、cursor、ingress 卡死根因。
- `executor`：实现 production 改动和测试。
- `test-engineer`：设计 harness 和测试矩阵。
- `code-reviewer`：最终审查并防止宿主决策越界。
- `verifier`：运行测试、检查日志证据和验收条件。

## Staffing Guidance

Ralph path:
- 一个 `executor` 顺序执行，先 harness 后 production。
- 一个 `verifier` 最后独立检查测试与日志。

Team path:
- Lane 1 `test-engineer`：fake bridge harness 与测试 helper。
- Lane 2 `executor`：action lifecycle parity。
- Lane 3 `executor`：ingress/lease/cursor recovery。
- Lane 4 `code-reviewer`：审查 agent-native 边界和旧 local executor 残留。

Suggested reasoning:
- architecture/debug/harness lanes 用 high。
- narrow executor lanes 用 medium。
- verification 用 high。

Launch hints:
- `$ralph .omx/plans/stardew-tool-orchestration-harness-plan.md`
- `$team .omx/plans/stardew-tool-orchestration-harness-plan.md`

Team verification path:
- Team 先证明 harness matrix 通过。
- Ralph/leader 再跑 Stardew filtered tests、MCP tests、bridge tests。
- 最后检查 `rg`，确认没有新增 NPC/地点/自然语言硬编码规则。
