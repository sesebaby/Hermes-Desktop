## Purpose

Define the fake-driven verification harness that proves Stardew host task lifecycle, entry-point convergence, ID correlation, prompt/tool boundaries, UI leases, replay behavior, and future action coverage without launching Stardew.

## Requirements

### Requirement: Harness shall prove the host task lifecycle without launching Stardew
The orchestration harness SHALL use fake agent, fake runtime driver, fake bridge command service, fake event source, fake UI/window service, and persisted state fixtures to verify host task lifecycle without requiring Stardew, SMAPI, or a live save.

#### Scenario: Move lifecycle in harness
- **WHEN** a fake agent issues a movement tool call
- **THEN** the harness observes host task creation, bridge command submission, status transition, terminal fact, runtime log, wake reason, and action slot cleanup

#### Scenario: Live game is not required
- **WHEN** the host task lifecycle tests run in CI
- **THEN** they complete using fakes and do not require SMAPI, game assets, or a running Stardew process

### Requirement: Harness shall cover every gameplay entry point
The harness SHALL cover autonomy, private chat, scheduled ingress, native Stardew tools, MCP Stardew wrappers, and debug/manual submission where applicable. Each entry point MUST converge on the same host task state and terminal fact semantics.

#### Scenario: Native and MCP parity
- **WHEN** the same action is submitted through native and MCP paths
- **THEN** both paths produce equivalent task identity, status transitions, terminal facts, and cleanup

#### Scenario: Scheduled ingress parity
- **WHEN** a scheduled ingress item triggers a world action
- **THEN** it enters the same host task lifecycle as an autonomy tool call

### Requirement: Harness shall assert stable ID correlation
The harness SHALL assert that each task records and propagates stable identifiers across tool call, trace, work item/task, bridge command, idempotency key, runtime log, and terminal fact. Tests MUST include repeated same-name tool calls to prove correlation is not based on tool name.

#### Scenario: Duplicate tool names
- **WHEN** two movement tool calls with the same tool name are submitted with different targets
- **THEN** each terminal fact is matched to the correct work item and bridge command

#### Scenario: Log correlation
- **WHEN** a task completes
- **THEN** runtime jsonl contains correlation fields that allow the task to be traced from submission to terminal fact

### Requirement: Harness shall gate removal of small-model gameplay execution
The harness SHALL include negative tests proving Stardew v1 gameplay does not call `NpcLocalExecutorRunner`, `local_executor`, small-model action prompts, hidden text parsing, or fallback speech/move execution.

#### Scenario: Natural language movement is not executed
- **WHEN** the fake agent replies "I am going to the beach" without a movement tool call
- **THEN** no bridge command is submitted and the runtime logs only a diagnostic fact

#### Scenario: Deprecated executor is not registered
- **WHEN** Stardew NPC autonomy and private chat runtime are constructed
- **THEN** no gameplay path has an active `INpcLocalExecutorRunner` dependency or equivalent model-in-the-middle executor

### Requirement: Harness shall verify terminal facts are agent-facing and non-prescriptive
The harness SHALL verify terminal facts are written to runtime state and injected into the next main agent turn as facts, not as host decisions. The fact MUST include status and reason codes where available.

#### Scenario: Completed action wakes agent
- **WHEN** a host task completes
- **THEN** the next fake agent prompt includes the completed action fact and does not auto-close related todo

#### Scenario: Blocked action wakes agent
- **WHEN** a host task is blocked
- **THEN** the next fake agent prompt includes the blocked reason and leaves the next decision to the agent

### Requirement: Harness shall verify UI lease lifecycle
The harness SHALL test UI/window tasks for lease acquisition, active menu conflict, bounded steps, validation, terminal status, timeout, cancellation, and cleanup. It MUST prove tasks release only their own leases and do not close unrelated menus.

#### Scenario: Active menu conflict
- **WHEN** a fake UI service reports an existing unrelated menu
- **THEN** the task returns blocked and no close operation is sent to that menu

#### Scenario: Lease cleanup after timeout
- **WHEN** a fake window task times out
- **THEN** its UI lease and action slot are released and a timeout fact is recorded

### Requirement: Harness shall verify replay and idempotency
The harness SHALL simulate persisted in-flight work, process restart, staged event replay, and duplicate idempotency keys. It MUST prove the runtime recovers or reports status without duplicate bridge submission.

#### Scenario: Restart recovery
- **WHEN** persisted state contains an in-flight task with a command id
- **THEN** recovery checks status and does not resubmit the command

#### Scenario: Replay duplicate ingress
- **WHEN** a staged event batch is replayed with the same idempotency key
- **THEN** the runtime reuses or ignores the existing work item instead of creating a duplicate

### Requirement: Harness shall cover supported action families
The harness SHALL include representative lifecycle tests for move, speak, idle micro action, open private chat, private chat reply delivery, crafting, trading, quest operation, and gathering. Future action families MUST add tests before implementation is considered complete.

#### Scenario: Speech delivery
- **WHEN** the main agent calls the speak tool with authored speech
- **THEN** the host submits the exact text and records a speech terminal fact

#### Scenario: Future crafting action
- **WHEN** crafting support is added
- **THEN** its harness covers UI lease, bounded operation, inventory validation, terminal fact, and cleanup

### Requirement: Harness shall use real repository prompt and skill assets for prompt boundary tests
Prompt and skill boundary tests SHALL load real repository assets through the production asset discovery path where feasible. They MUST NOT rely only on fixture text when verifying Stardew navigation, world, persona, or tool-boundary prompt behavior.

#### Scenario: Navigation prompt contract
- **WHEN** a test verifies that target resolution belongs to the main agent
- **THEN** it loads the real `stardew-navigation` skill/reference assets or the same production resolver used at runtime

#### Scenario: Deprecated prompt text stays retired
- **WHEN** prompt assets are scanned in tests
- **THEN** they do not instruct Stardew v1 gameplay to use a local executor or small-model action lane

### Requirement: Harness 必须证明 agent / game 的非阻塞边界成立
Stardew orchestration harness MUST 证明：可恢复的游戏 UI / 窗口 / 动画 / 事件等待，不会阻塞 agent 流程，也不会通过无关的重试预算把 host task ingress 打成终态失败。同时，它也 MUST 证明真正的 busy / stale 条件仍然是有边界的。

#### Scenario: 私聊 UI 等待超过通用预算时仍不能终态阻塞
- **WHEN** 一个 private-chat host task 为了等待回复 UI 生命周期结束，等待时间超过通用 stale / busy defer 次数预算
- **THEN** 该任务仍然必须保持可恢复，不能被 `host_task_submission_deferred_exceeded` 删除或打成终态 blocked

#### Scenario: 通用 busy ingress 仍然正确阻塞
- **WHEN** 一个 host task submission 因为 action slot 或 pending work item 持续 busy 而被重复 defer
- **THEN** 现有通用 defer 预算必须在达到配置阈值后，继续产生带 `host_task_submission_deferred_exceeded` 的 blocked 事实

#### Scenario: 冲突动作不能创建隐藏排队工作
- **WHEN** 同一个 NPC 已经有一个运行中的世界动作槽位，此时又提交了一个新的世界动作
- **THEN** harness 必须观察到 `blocked/action_slot_busy`，并确认没有额外创建排队世界动作 work item，也没有在运行中动作结束后自动重试

#### Scenario: Agent turn 不能被 host 决策替代
- **WHEN** 某个任务报告 running、waiting、blocked、failed 或 completed 状态
- **THEN** harness 必须验证 host 只是记录事实，不能自动关闭 todo、推断移动，或替 agent 选择下一步动作

### Requirement: Harness 必须验证 Stardew 工具结果是文本优先的
Harness MUST 验证：agent 可见的 Stardew 动作结果和续接状态结果都带有简短、可读的 summary，同时也保留用于关联和测试的机器字段。

#### Scenario: 任务状态结果包含 summary 和命令标识
- **WHEN** 一个 fake command service 返回 running、waiting、completed、blocked、failed、cancelled 或 timed-out 的任务状态
- **THEN** `stardew_task_status` 必须返回 agent 可读的 `summary`，并保留 `status` 和 `commandId`

#### Scenario: 动作工具结果包含 summary 和状态字段
- **WHEN** 一个 Stardew 动作工具返回 queued、running、completed 或 blocked 状态
- **THEN** 结果必须包含 agent 可读的 `summary`，并保留机器可读的状态字段

### Requirement: Harness 必须验证 prompt 和 skill 的非阻塞指引
Prompt 和 skill 边界测试 MUST 验证：Stardew runtime 指引明确告诉 agent，要把窗口、动画、菜单和事件看作状态事实，而不是隐藏锁。同时，这些指引 MUST 把续接路径指向 `stardew_task_status`，并且 MUST NOT 指示 host 或 agent 去依赖小模型 executor 或 host 代推断下一步动作。

#### Scenario: Skills 教会 agent 通过状态续接
- **WHEN** harness 扫描 Stardew core 和 task-continuity skill 资产
- **THEN** 这些资产必须明确指引 agent：对已知长时或等待中的工作，应使用 `stardew_task_status` 做续接

#### Scenario: Runtime prompt 保持 host 不带指挥性
- **WHEN** harness 扫描 Stardew NPC runtime prompt 资产
- **THEN** 这些资产必须明确写出：host 只提供 facts / status，下一步决策属于 agent 自己
