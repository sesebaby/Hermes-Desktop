## ADDED Requirements

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
