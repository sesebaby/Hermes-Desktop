## 背景

Stardew v1 的总体架构方向其实已经是对的：模型可见工具负责创建 host task / work item 记录，host / bridge 代码机械执行这些任务，终态事实再返回给主 NPC agent。现有 spec 也已经要求 UI lease、受限窗口操作，以及 fake 驱动的 orchestration harness 覆盖。

真正出错的是更细的一层编排边界。当前私聊 host task 提交流程，会把“等待回复 UI 生命周期结束”当成普通 ingress defer。因为这个通用预算本来就故意设得很短，所以玩家只是还在看对话，一个本来合法的移动任务就可能被打成终态 blocked。这正是 host task runner 本来要避免的问题类型：游戏生命周期状态泄露进了 agent 流程，被当成执行锁使用了。

这个变更涉及的相关方包括：NPC agent、host task runner、Stardew bridge、prompt / skill 编写者，以及必须在交易、制作、采集、任务窗口等更多 UI 动作加入前就能兜住这类问题的测试体系。

## 目标 / 非目标

**目标：**

- 保证 agent runtime 不会被 Stardew 的窗口、菜单、动画和事件阻塞。
- 保持每个 NPC 只有一个正在运行的世界动作槽位。
- 把可恢复的游戏等待表示成 agent 可读、可查的 task / status 事实。
- 工具结果采用文本优先格式，提供简洁 `summary`，同时保留最小必要的关联 / 状态字段。
- 对真正 stale 的工作，保留现有通用 stale / busy ingress 阻塞行为。
- 更新 prompt 和 skill 指引，让 agent 通过 `stardew_task_status` 做续接，而不是依赖隐藏的 host 锁。
- 为私聊回复生命周期 bug 和状态 summary 补上回归覆盖。
- 在实现时把这次反复犯错的教训记录进 `openspec/errors`。

**非目标：**

- 不做真实的制作、交易、采集、任务窗口处理器。
- 不重写任务系统。
- 不引入第二套队列模型。
- 不让 host 侧推断 agent 的下一步动作。
- 不增加一个大而全的状态 mega-tool。
- 不把提高通用重试次数当成修复方案。

## 设计决策

### 决策 1：保持 agent 流程非阻塞，同时维持单槽位身体执行

当一个游戏动作处于排队、运行或等待 UI 状态时，agent 仍然可以继续思考、说话、查状态、更新 todo。NPC 身体侧仍然只有一个运行中的世界动作槽位。新的冲突世界动作不能排在当前动作后面，而是要直接返回可读的 `blocked/action_slot_busy` 事实。

原因：

- 这样能保持架构简单，也符合现有 `ActionSlot` / `PendingWorkItem` 设计。
- 可以避免冲突实体动作无限排队。
- 下一步决策仍然留给 agent，而不是让 host 接管。

考虑过的替代方案：

- 允许多个身体动作并行。否决，因为 Stardew NPC 控制、UI 所有权和 bridge 命令语义天然就是单身体。
- 所有后续身体动作都排到当前动作后面。否决，因为这样会形成隐藏意图排序，也会让过时的玩家请求在上下文已经变化后继续执行。

### 决策 2：把可恢复等待和通用 stale / busy defer 预算分开

等待私聊回复 UI 生命周期、菜单关闭、动画结束或类似游戏条件时，必须表示成可恢复的 task / status 状态，不能消耗那套专门用于 stale 或 busy ingress 保护的短 defer 预算。

原因：

- 人类阅读 UI 的时间，不是 stale work item。
- 通用 defer 预算应该继续只拦真正的循环和忙槽位。
- 这能直接打中根因，而不是靠放大全局重试上限掩盖问题。

考虑过的替代方案：

- 提高 `MaxDeferredIngressAttempts`。否决，因为这会掩盖错误分类，并削弱 stale loop 防护。
- 在做任何 UI 安全检查前就先启动世界动作。否决，因为 UI lease / 菜单所有权仍然是保护游戏状态所必需的。
- 回复 UI 没有快速关闭就直接打终态 blocked。否决，因为这会让玩家可见的 UI 时机破坏本来合法的 agent 决策。

### 决策 3：采用文本优先、结构保留的工具结果

Stardew 动作和状态工具结果应该带一个写给 agent 的简短 `summary`，同时保留 `status`、`commandId`、`reason` / `errorCode` 以及关联字段。

原因：

- LLM 处理简洁文本事实，通常比处理纯字段袋更稳。
- 测试、UI、日志，以及后续 `stardew_task_status` 查询，仍然需要稳定的机器字段。
- 这能避开两个极端：既不走只有自然语言、没有验证面的纯 prose，也不走只有字段、逼模型自己解码运行时内部状态的纯 JSON。

考虑过的替代方案：

- 纯自然语言工具结果。否决，因为可测试性和关联性都会变差。
- 纯字段 JSON 结果。否决，因为这会重复当前弱点：模型看到了状态，但没有明显的下一步解释线索。

### 决策 4：在 prompt 和 skill 资产里把边界讲清楚

`stardew-core`、`stardew-task-continuity` 以及 runtime system prompt 都要明确说明：窗口、菜单、动画、事件是游戏事实 / 状态，不是 agent 进程上的隐藏锁。同时要明确指引 agent 用 `stardew_task_status` 做续接，并且根据 running / blocked / completed 事实自行做下一步决策。

原因：

- 只改工具行为还不够，agent 自己也必须知道应该怎么反应。
- 这样可以避免 host 为了弥补模型误解而不断长出决策逻辑。

考虑过的替代方案：

- 只靠实现变更，不改 prompt / skill。否决，因为下一种故障很可能会变成 agent 持续重复提交冲突动作，而不是主动查状态。
- 给 host 加启发式逻辑，替 agent 挑下一步动作。否决，因为这会破坏既有 host / agent 边界。

### 决策 5：让 harness 证明的是架构边界，而不是单个症状

测试应该同时证明这条拆分边界两侧都成立：

- 私聊 UI 等待不会再因为通用 defer 预算而把合法 host task ingress 打成终态 blocked；
- 通用 busy / stale ingress 仍然会按配置预算正确阻塞；
- 状态 / 动作工具结果包含 agent 可读的 summary；
- 冲突世界动作返回 `blocked/action_slot_busy`，不会偷偷创建隐藏排队工作，也不会由 host 擅自安排重试；
- prompt / skill 指引明确保留“游戏状态非阻塞 agent”这条边界。

原因：

- 这是一类会重复出现的 bug，单独补一个移动回归用例不够。
- harness 本来就是用来守住 host task 生命周期和 prompt 边界回归的。

考虑过的替代方案：

- 只补一个 Haley / beach 回归。否决，因为它抓不住下一次交易 / 制作 / 窗口类变体问题。

### 决策 6：为测试定义一个小而明确的状态码契约

实现层应继续和当前代码字段名保持一致，但测试必须断言最小状态词汇表：

| 条件 | 预期状态形状 | 必需的 reason / code 证据 | summary 预期 |
| --- | --- | --- | --- |
| 可恢复 UI 等待 | 非终态 `queued`、`running` 或现有等价 waiting 状态 | 绝不能使用 `host_task_submission_deferred_exceeded` | 明确说明任务正在等待游戏 UI / 窗口生命周期 |
| 动作槽位冲突 | 终态或即时 `blocked` | `action_slot_busy` | 明确说明已有另一个世界动作在进行中 |
| 通用 defer 用尽 | 终态 `blocked` | `host_task_submission_deferred_exceeded` | 明确说明提交超出了通用 busy / stale 重试预算 |
| Timeout / watchdog | 使用现有 runtime 状态表示终态 timeout / blocked / failed | `action_slot_timeout` 或映射到现有 timeout code | 明确说明任务超时或被 watchdog 停止 |
| Lease / 菜单冲突 | 终态或即时 `blocked` | `menu_blocked`、`ui_lease_busy`、`private_chat_active` 或映射到现有 bridge reason | 明确说明菜单 / UI lease 当前不可用，但不替 agent 选下一步动作 |

原因：

- 没有这个小矩阵，测试很容易只验证“发生了某种错误”就过掉。
- 这个矩阵能把实现边界收住，不需要为此发明一套新状态机。

考虑过的替代方案：

- 彻底重设计状态枚举。否决，因为超出本次范围，而且会增加破坏 bridge / runtime 契约的风险。
- 让状态码继续隐式存在。否决，因为当前回归正是躲在泛化 blocked 事实后面才漏掉的。

## 风险 / 取舍

- [风险] 身体还在等待时，agent 可能继续思考并提交冲突动作。  
  缓解：保留单一运行动作槽位，并返回 `summary + blocked/action_slot_busy`；同时教 agent 去查状态或改计划。

- [风险] 如果游戏一直不发出预期事件，可恢复等待可能会无限持续。  
  缓解：保留 task / action timeout 和 watchdog 终态事实；“可恢复等待”只表示不使用通用 ingress 预算，不表示无限执行。

- [风险] 新增 `summary` 字段可能和状态字段漂移，形成两套真相。  
  缓解：尽量从同一个状态 / 动作结果对象生成 summary，并在测试里断言代表性 summary。

- [风险] prompt / skill 改动可能无意中鼓励宽泛状态扫描。  
  缓解：保持现有广义状态预算，并把指引限定在“已知命令续接使用 `stardew_task_status`”。

- [风险] 把私聊回复关闭视为 UI 事实后，可能让不安全的菜单重叠漏过去。  
  缓解：继续在窗口处理器里保留 UI lease / 菜单冲突检查；解耦 agent 流程不等于移除游戏侧安全保护。

- [风险] 可恢复等待可能唤醒或轮询过于积极，制造噪音 agent loop。  
  缓解：复用现有 next-wake / backoff 行为，并把 `stardew_task_status` 保持为已知命令的续接路径。

## 迁移计划

1. 先补会失败的回归测试，证明私聊 UI 生命周期等待在超过通用 defer 预算后，不应把 host task 打成终态 blocked。
2. 为 `stardew_task_status` 和代表性动作工具结果补 summary 输出测试，必要时调整旧测试。
3. 只做最小运行时改动：把私聊 UI 生命周期等待从通用 stale / busy ingress retry 中分离出来。
4. 为状态 / 动作结果补“文本优先”的 summary 形状。
5. 更新 Stardew prompt 和 skill 资产。
6. 先跑定向 Stardew 测试，再跑更宽的 Stardew 测试过滤。
7. 更新或新增对应 `openspec/errors` 记录，把这次反复犯错的教训沉淀下来。

因为改动只落在运行时分类、工具结果形状以及 prompt / skill 文案上，回滚比较直接。如果后续发现某种 summary 形状引发模型异常行为，也可以单独回滚 summary 相关改动，而不用连带撤销 defer 预算分类修复。

## 未决问题

- `summary` 的具体措辞，应在实现阶段结合真实 `GameCommandStatus` 和工具结果形状最终确定。
- 所有动作工具是否共用一个 summary helper，还是先为 `stardew_task_status` 和 host task submission 用窄 helper 起步，可以在实现时再定；在至少两个调用点真实需要前，不要急着抽象。
- 超时和菜单 / lease reason code 的精确接受集合，应在写 RED 测试时从当前代码映射出来；除非证明现有代码完全没有对应值，否则不要发明新 code。
