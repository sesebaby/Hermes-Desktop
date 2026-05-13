## Context

Stardew NPC 当前已经有不少接近 host task runner 的基础：`PendingWorkItem`、`ActionSlot`、`IngressWorkItems`、`LastTerminalCommandStatus`、`NpcRuntimeStateStore`、runtime jsonl、MCP/native Stardew tools，以及 bridge 侧的 `/task/move`、`/task/status`、`/task/cancel` 等接口。问题不是完全没有任务系统，而是历史上叠过小模型执行层、`local_executor`、私聊委托动作、自检 prompt、MCP wrapper 等多条执行路径，导致“谁在决策、谁在执行、谁负责收口”变得不清楚。

本设计把 Stardew v1 的 gameplay 执行路径收敛成一条线：

`主 agent 调模型可见工具 -> runtime 创建 host task/work item -> host/bridge 机械执行 -> 状态/超时/watchdog/terminal fact 回主 agent -> 主 agent 继续决策`

这对齐 `external/hermescraft-main` 的 command queue、`currentTask`、`task_id`、`briefState()`、stuck watchdog 思路，也吸收 `external/hermes-agent-main` 对工具调用开始/完成 ID 关联的要求。不同点是 Stardew 不能照搬单个全局 `currentTask`：这里必须按 NPC、save、session、action slot、UI lease 和 resource claim 分桶。

关键约束：

- Stardew v1 不保留小模型 gameplay 执行层。
- 废弃能力必须同时退役，不允许双轨、影子实现、隐藏 fallback。
- host/bridge 不从自由文本推断动作。
- `todo` 是主 agent 的连续性层；host task 只是机械执行记录。
- 未来制造、交易、任务、采集等窗口能力必须扩展 host task runner，而不是新增模型窗口执行器。

## Goals / Non-Goals

**Goals:**

- 建立一个统一 host task contract，覆盖移动、说话、微动作、私聊窗口，以及后续制造、交易、任务、采集窗口。
- 让 native tool、MCP tool、private chat ingress、scheduled ingress、autonomy turn 都进入同一套 task lifecycle。
- 用稳定 ID 关联 `toolCallId`、`traceId`、`workItemId/taskId`、`commandId`、`idempotencyKey`。
- 明确状态机、terminal fact、wake policy、timeout、stuck、cancel、resource cleanup。
- 清退 `NpcLocalExecutorRunner` 作为 Stardew gameplay 执行层的职责和测试期望。
- 让 harness 在不启动 Stardew/SMAPI 的情况下证明编排契约成立。

**Non-Goals:**

- 不实现第二套 Hermes agent、第二 tool lane、第二 MCP lane。
- 不让小模型执行移动、说话、窗口点击、`todo` 收口或私聊即时行动。
- 不把自然语言回复、JSON 文本、prompt 自检文本解析成真实动作。
- 不在第一阶段做完整经济系统、复杂交易策略、写死剧情编排或自定义素材。
- 不要求一次性完成所有未来窗口动作的业务细节；本变更先统一生命周期和 harness，后续动作按同一 contract 扩展。

## Decisions

### 1. 以 Host Task / Work Item 作为唯一 gameplay 执行抽象

主 agent 只能通过模型可见工具提交意图。工具层把意图转换成 host task/work item，host/bridge 负责机械执行。状态回传给主 agent 后，由主 agent 自己判断下一步。

**理由：** 这和参考项目的 `/task/ACTION -> task_id -> /task/status -> briefState` 模式一致，也适合 Stardew 的长动作、跨地图移动、窗口生命周期和 watchdog。

**拒绝方案：** 保留小模型 executor 执行动作。拒绝原因是它会制造第二决策者：主 agent 以为自己说完了，小模型又在下面解释、选择、补动作，调试和责任边界都会崩。

### 2. 现有 `PendingWorkItem` / `ActionSlot` 先升级为 host task contract，而不是另造一套并行模型

当前代码已经有 `NpcRuntimePendingWorkItemSnapshot`、`NpcRuntimeActionSlotSnapshot`、`NpcRuntimeIngressWorkItemSnapshot`、`GameCommandStatus`、`StardewRuntimeActionController`。实施时优先扩展这些结构的语义和字段，必要时重命名或补字段，但不新建一条并行 task store。

**理由：** 现有结构已经进入持久化、MCP 测试和 autonomy wake 链路。复用它们能减少迁移风险，也符合“预发布阶段只保留一条路径”。

**拒绝方案：** 新建 `LocalExecutorTask` 或 `ModelActionTask`。拒绝原因是名字和职责都会把旧小模型执行层带回来。

### 3. 所有真实世界写操作必须由可见工具创建 task，不能由宿主解析文本

如果主 agent 没有 tool call，宿主只记录诊断，例如 `no_tool_decision`、`fallback_speak_retired`、`task_continuity_unresolved`，并在下一轮以事实提醒；宿主不得把文本、JSON、自然语言承诺转成 move/speak/todo/window action。

**理由：** 这是 agent-native 的关键：模型通过工具和工具结果闭环，不通过隐藏执行器闭环。

**拒绝方案：** 为了“体验顺滑”把可见自然语言自动投递成说话或移动。拒绝原因是这正是 hidden fallback，会导致 agent 以后更不愿意调用工具。

### 4. Private chat 仍由主 agent 说话和承诺，但即时行动通过 host task 延迟执行

私聊里玩家提出行动请求时，主 agent 自己决定是否答应、写不写 `todo`、说什么，并调用可见工具提交 host task。私聊窗口关闭、回复投递、open/reopen/lease 属于 UI task lifecycle。动作完成后的 `todo` 收口由主 agent 在看到 terminal fact 后显式处理。

**理由：** 这样保留大模型“活人感”和关系判断，同时避免小模型接管行动。

**拒绝方案：** 私聊回复后由小模型补动作或 host 从“我现在去海边”里解析移动。拒绝原因是会恢复双轨。

### 5. 窗口类任务统一走 UI Lease + bounded mechanical steps

制造、交易、任务、采集、私聊窗口都必须先拿 UI lease，检查 active menu，再执行有限步骤，验证可观察结果，最后关闭或释放 lease。失败必须返回 `blocked/failed/timeout/cancelled` 和原因码。

**理由：** Stardew 的 UI 是全局资源，窗口重入和错误关闭容易造成崩溃或误操作。窗口任务必须像资源一样租赁和释放。

**拒绝方案：** 给每类窗口写一套自己的 ad hoc runner。拒绝原因是后续能力会越堆越散，无法统一排查“菜单占用、超时、重复提交、关闭失败”。

### 6. Terminal fact 是主 agent 再决策的唯一自动回传形态

host task 结束后必须写入 `LastTerminalCommandStatus` 或等价 terminal fact，并在下一轮 prompt 中以 `last_action_result`、`interaction_session`、`action_slot_timeout` 等事实出现。事实只陈述结果，不指挥下一步。

**理由：** 主 agent 需要看到结果才能收口任务，但 host 不能替它做判断。

**拒绝方案：** host 看到 `completed` 后自动完成 `todo`，或看到 `blocked` 后自动换目标。拒绝原因是这把长期承诺和世界判断从 agent 手里拿走了。

### 7. ID 关联必须贯穿工具、host task、bridge command、runtime log

每个任务至少保留 `traceId`、`workItemId/taskId`、`commandId`、`idempotencyKey`、`source`、`action`。能拿到模型工具调用 ID 时也要纳入 correlation。重复同名工具调用不能靠工具名配对。

**理由：** 参考项目的工具事件和 Hermes agent 的 UI/progress 经验都说明，靠名称配对会在并发、重试、同名动作中错乱。

**拒绝方案：** 只记录 `action=move` 和最后一个 command。拒绝原因是无法证明哪次 tool call 对应哪次 bridge 结果。

### 8. Watchdog 和 replay/idempotency 是 host task runner 的内建职责

running/queued 任务必须有 timeout/stuck 转 terminal 的路径；重启恢复时必须基于 idempotency lookup 和 persisted state 继续或收口，不能重复提交已提交命令。

**理由：** NPC 自主循环是后台系统，不能无限占住 action slot、UI lease 或 LLM slot。

**拒绝方案：** 只靠下一轮 agent 自己查状态。拒绝原因是 bridge 卡死、UI 卡住、进程重启时 agent 可能永远等不到事实。

## Risks / Trade-offs

- [Risk] 清退 `NpcLocalExecutorRunner` 会触碰大量旧测试和诊断名。→ Mitigation：先加负向门禁和新 host task harness，再删除或重命名旧测试期望，避免“测试还在证明旧路径”。
- [Risk] 现有 `PendingWorkItem` 字段不够表达 UI lease、source、toolCallId。→ Mitigation：优先小步扩字段并保持持久化兼容；字段语义在 spec 中固定，迁移测试覆盖旧 state。
- [Risk] 主 agent 有时仍只说自然语言不调工具。→ Mitigation：宿主只记录诊断和下一轮事实提醒，不执行 fallback；prompt 和 harness 共同验证“无工具调用不会改变世界”。
- [Risk] 窗口任务种类增长后 runner 变成大 switch。→ Mitigation：统一外层 lifecycle，内部按 action handler 分发；handler 只能实现机械步骤和验证，不拥有 agent 判断。
- [Risk] 私聊体验可能因为必须显式 tool call 而变慢。→ Mitigation：保留主 agent 一次 turn 内“写 todo + 提交 host task + 自然回复”的能力，动作在窗口关闭后由 host 延迟执行。
- [Risk] MCP/native/private chat 入口语义不一致。→ Mitigation：入口只做参数适配，必须落到同一 `StardewRuntimeActionController` / host task submitter，并共享 terminal fact。

## Migration Plan

1. 盘点并标记 Stardew gameplay 路径中所有 `local_executor`、`NpcLocalExecutorRunner`、`npc_delegate_action`、私聊自检、fallback speak、测试期望和文档引用。
2. 为 host task contract 补齐状态、ID、source、terminal fact、watchdog、persistence/replay 的测试。
3. 将 native/MCP Stardew tools、private chat delegated action、scheduled ingress、autonomy action submission 收敛到同一 submit/status/terminal fact 路径。
4. 清退 `NpcLocalExecutorRunner` 在 Stardew gameplay 中的注册、prompt、harness 和测试期望；如果保留类名只能作为非当前路径的历史代码，不能被 DI 或工具面触达。优先删除。
5. 为 UI/window task lifecycle 建立 lease、active menu blocked、timeout、release 的 fake bridge harness。
6. 更新文档和项目记忆，使“不保留小模型 gameplay 执行层、废弃能力同步退役”成为后续修改门禁。

Rollback 策略：本变更是预发布架构收敛，不允许回滚到双轨。若某个新 host task handler 出问题，只回滚该 handler 或禁用该 action；不得恢复小模型 executor fallback。

## Open Questions

- `npc_delegate_action` 不作为长期 API 保留；实施时必须替换为更直白的 host task submitter/tool 名称，或在同一次迁移中删除。不得保留带 `delegate` 语义的 Stardew gameplay 当前路径入口。
- `toolCallId` 在当前 C# agent/tool pipeline 中是否能稳定取得；如果不能，第一阶段至少要用 `traceId + workItemId + commandId + idempotencyKey`，并预留字段。
- UI lease 是复用 private chat session lease，还是抽出通用 `NpcRuntimeUiLeaseSnapshot`？设计倾向抽通用 lease，但实施时要看现有 private chat wiring 的改动面。
- `timeout` 和 `stuck` 是否作为新的 command status 常量加入，还是先映射到 `Expired/Blocked + errorCode`？设计倾向在 host task contract 中语义化区分，bridge 兼容期可映射。
