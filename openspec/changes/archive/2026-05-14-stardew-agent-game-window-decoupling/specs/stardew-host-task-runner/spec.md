## ADDED Requirements

### Requirement: 在可恢复的游戏等待上，host task 不能阻塞 agent 流程
Stardew host task runner MUST 把可恢复的游戏 UI、窗口、动画、菜单和事件等待表示成 task / status 事实。MUST NOT 仅仅因为游戏正在等待某个可恢复的生命周期条件，就暂停 agent turn、把合法任务打成终态 blocked，或者消耗通用 stale / busy ingress defer 预算。

#### Scenario: 私聊回复等待保持可恢复
- **WHEN** 一个 private-chat `stardew_host_task_submission` 带有匹配的 `conversationId`，且私聊回复 UI 生命周期还没有结束
- **THEN** 这项工作必须继续以 queued / running / waiting 等可恢复任务状态存在，不能被 `MaxDeferredIngressAttempts` 打成终态 blocked

#### Scenario: 通用 busy defer 仍然有边界
- **WHEN** 一个 host task submission 因为 NPC action slot 或 pending work item 仍然 busy，而被重复 defer
- **THEN** 通用 stale / busy defer 预算仍然适用，并最终产生带 `host_task_submission_deferred_exceeded` reason 的可观察 blocked 事实

#### Scenario: 游戏等待期间，agent 仍可继续
- **WHEN** 一个 host task 正在等待某个可恢复的游戏条件，例如菜单、对话、动画或事件切换
- **THEN** 下一次 agent turn 仍然可以说话、更新 todo 或调用状态工具，且 host 不得代推断下一步动作

### Requirement: Host task 结果必须带 agent 可读的摘要
暴露给 agent 的 Stardew host task 动作结果和状态结果 MUST 包含一个简洁文本 `summary`，说明当前状态，以及剩下的是什么类型的决策需要由 agent 自己做。`status`、`commandId`、`reason`、`errorCode` 以及各类关联标识等机器字段，仍然 MUST 保留，以便测试、UI、日志和后续状态查询使用。

#### Scenario: 排队动作返回 summary
- **WHEN** 一个 agent 可见的 Stardew 动作工具接受了或排队了一个 host task
- **THEN** 工具结果必须包含简短 `summary`，说明动作已经排队或开始执行；如果有对应 `commandId` 或工作标识，也必须一并带上

#### Scenario: 状态查询返回 summary
- **WHEN** agent 对某个已知命令调用 `stardew_task_status`
- **THEN** 返回结果必须包含简短 `summary`，说明任务当前是 running、waiting、completed、blocked、failed、cancelled 还是 timed out

#### Scenario: Blocked 动作解释清楚归因
- **WHEN** 一个 host task 返回 `blocked`
- **THEN** `summary` 必须解释阻塞原因，但不能替 agent 指定应该选哪一个下一步动作

#### Scenario: 可恢复等待要解释当前状态
- **WHEN** 一个任务正在等待某个可恢复的 UI / 窗口 / 动画条件
- **THEN** 返回结果必须带非终态状态和值得 agent 理解的 `summary`，并且不能使用 `host_task_submission_deferred_exceeded`

### Requirement: 冲突的世界动作必须返回事实，而不是进入隐藏队列
对于单个 NPC，host task runner MUST 保持只有一个运行中的世界动作槽位。当一个新世界动作和正在进行中的世界动作冲突时，新请求 MUST 返回像 `action_slot_busy` 这样的可观察 blocked 事实，MUST NOT 偷走当前槽位、替换当前动作，也不能进入一个无界的隐藏队列。

#### Scenario: 运行中的移动会阻止冲突动作
- **WHEN** 一个 NPC 已经有一个正在运行的移动任务，而 agent 又提交了另一个冲突世界动作
- **THEN** 新动作必须返回带 agent 可读 `summary` 的 `blocked/action_slot_busy` 事实，并且不能创建第二个排队世界动作 work item

#### Scenario: 重试决策必须归 agent 所有
- **WHEN** 一个冲突世界动作因为 action slot busy 而被 blocked
- **THEN** host 不能在当前任务完成后自动替这个 blocked 动作重试
