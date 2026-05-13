# Stardew Agent/Game Window Decoupling Deep Interview Context

## Task statement

用户要求先深度讨论 Stardew NPC 编排架构，不直接修代码。核心判断是：任何 agent 流程不应被游戏窗口、动画、事件阻塞；游戏归游戏，agent 归 agent。若当前游戏状态暂时不能控制 NPC 或 UI，bridge/host 应返回可观测状态，或由 agent 主动通过 status 类工具获取事实。

## Desired outcome

- 澄清是否要把 private-chat reply close、DialogueBox、动画、menu/window、cutscene/festival/day transition 等全部建模为游戏侧事实/lease/status，而不是阻塞 agent turn 或 ingress 队列。
- 澄清 host task runner、bridge、agent loop 的职责边界：host/bridge 执行机械动作并返回 terminal/progress/blocked facts；agent 保持推理与任务决策连续，不被游戏 UI 生命周期直接挂起。
- 形成可交给后续 `$ralplan` 的架构规格，而不是继续做单点补丁。

## Stated solution

用户提出的候选原则：

- agent 流程和游戏窗口/动画/事件并行。
- 游戏状态不能执行时，bridge/host 返回状态，不 block agent。
- agent 需要事实时主动使用 `status` 等工具读取当前游戏事实。

## Probable intent hypothesis

用户要避免同一类错误反复出现：私聊窗口、任务窗口、交易窗口、采集窗口、制造窗口等 UI/动画生命周期被错误地写成 agent/ingress 执行锁或短重试预算，导致 NPC 明明答应任务却不执行，或者未来每新增一种窗口又重新犯错。

## Known facts/evidence

- `openspec/specs/stardew-ui-task-lifecycle/spec.md` 已要求窗口任务通过 host task lifecycle：UI lease、active menu safety、bounded steps、observable validation、safe cleanup、terminal facts；未来 craft/trade/quest/gather 也必须走同一 host task handler。
- `openspec/specs/stardew-orchestration-harness/spec.md` 要求 harness 覆盖 host task lifecycle、entry-point convergence、UI lease、future action coverage、terminal facts agent-facing。
- 当前代码 `src/games/stardew/StardewNpcAutonomyBackgroundService.cs` 中，`stardew_host_task_submission` 在 `conversationId` 存在且未见 `private_chat_reply_closed` 时进入 `DeferPrivateChatReplyClosedIngressAsync(...)`。
- `DeferPrivateChatReplyClosedIngressAsync(...)` 当前会递增 `DeferredAttempts` 并复用 `ShouldBlockDeferredIngress(...)` 的通用短预算。
- `src/runtime/NpcAutonomyBudget.cs` 中 `MaxDeferredIngressAttempts = 3`，这适合 stale/busy guard，不适合人类读窗口的 UI 生命周期。
- 历史错误 `openspec/errors/E-2026-0510-stardew-private-chat-delegated-move-dialogue-and-target.md` 已记录：ingress work item 应在等待 reply close 时保持 queued；窗口/对话不能引入 local executor 或 broad dialogue interrupt。
- `.omx/context/stardew-remove-closure-lock-reference-task-state-alignment-20260511T111401Z.md` 已明确参考 hermescraft：玩家请求进入队列，真实动作由单一当前任务槽执行，进度/完成/失败作为事实反馈给 agent，agent 用工具或 todo 自己收尾，不靠自然语言 closure lock。
- `src/games/stardew/StardewNpcTools.cs` 当前父层工具面已包含 `stardew_task_status`，但工具描述仅为“读取此前 NPC 行动工具返回的 Stardew command 状态”，结果直接序列化 `GameCommandStatus`，偏字段化，不是面向 agent 的短文本状态说明。
- `src/Core/Agent.cs` 已把 broad Stardew status 工具限制为每轮一次，并把 `stardew_task_status` 分到 `continuation_status`，说明当前架构已经承认“长任务状态查询”不同于广义世界扫描。
- `skills/gaming/stardew-task-continuity/SKILL.md` 和 `skills/gaming/stardew-core/SKILL.md` 已要求长动作后用 `stardew_task_status` 查进度，但还没有明确规定“窗口/动画/事件只能成为文本事实或 task status，不能阻塞 agent 流程”。

## Constraints

- 不新增第二套任务系统；继续使用现有 IngressWorkItems、ActionSlot、PendingWorkItem、LastTerminalCommandStatus、host task runner/status surface。
- 不让宿主替 NPC 决策；宿主只执行工具请求、反馈状态、记录事实。
- 不让本地小模型或 ad hoc handler 操作 UI。
- 不用自然语言文本格式作为安全门。
- 不以增加通用 retry count 作为根修复。
- 不直接实施；本轮是 deep-interview，产出需求/边界规格。

## Unknowns/open questions

- “agent 不被 block”的严格含义：是 agent turn 永不等待任何游戏动作完成，还是只禁止等待人类 UI/动画，允许 tool call 同步返回 queued/blocked/running？
- 若 host task 正在运行或 UI lease busy，下一次 agent turn 应该收到 `running/busy/blocked` fact 后继续思考，还是应由后台 watchdog 唤醒 agent？
- private chat 的自然回复展示边界：回复被提交给游戏显示管线是否即为 agent 可继续行动，还是必须等玩家看到/关闭？用户当前倾向前者，但需要明确。
- status 工具的责任：应返回哪些最小事实才能支持 agent 自主决策，而不把宿主候选动作预载进 prompt？
- 状态工具的输出契约：是否要求每个结果必须有 agent-readable short text summary，并把字段降级为辅助证据？

## Decision-boundary unknowns

- 是否允许我后续把 `private_chat_reply_closed` 从 host task ingress gate 改成纯 UI fact/lease conflict，而不再阻塞 move/craft/trade 等非同一 UI lease动作？
- 是否允许后续引入/强化统一 task status surface，让 agent 主动 poll，而不是每个事件完成都自动驱动下一步？
- 哪些游戏状态属于 hard block 立即返回给 agent，哪些属于 background running 状态？

## Likely codebase touchpoints

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcRuntimeDescriptor.cs`
- `src/runtime/NpcAutonomyBudget.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewCommandContracts.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `openspec/specs/stardew-ui-task-lifecycle/spec.md`
- `openspec/specs/stardew-orchestration-harness/spec.md`
- `openspec/errors/E-2026-0510-stardew-private-chat-delegated-move-dialogue-and-target.md`

## Prompt-safe initial-context summary status

not_needed

## Interview rounds

### Round 1

Question: 是否立成硬规则：所有 Stardew 世界动作工具调用都必须立即返回 `queued/running/blocked/completed` 之一，绝不等待玩家关闭窗口、动画结束或长动作完成；后续进展只通过 `status` 工具、terminal fact、watchdog 唤醒进入下一轮 agent 判断？

User answer:

- 这个方向合理，但方案从简，越复杂越出错。
- 注意 AI 的特点：宿主要返回文本说明，而不是一堆字段。
- 需要提供 agent 能主动使用的状态查询工具；用户记得已有，但不确定是否完善。
- 对应 prompt、skill 也要完善。

Current interpretation:

- 执行动作可以被游戏状态暂时阻挡，但阻挡应表现为 host task status / readable fact，不应挂住 agent turn 或消耗 unrelated stale/busy retry budget。
- 可恢复阻挡进入 host-side queue/status；硬阻挡返回 terminal blocked 文本事实，由 agent 决定下一步。
- 状态工具需要对 LLM 友好：短文本结论优先，字段只作追踪证据。

### Round 2

Question: 状态工具返回的文本说明是统一放在所有 Stardew action/status 工具结果里的 `summary` 字段，还是工具结果直接以短自然语言为主？

User answer:

- 由实现方决定。

Decision:

- 采用文本优先、结构保留：Stardew action/status 工具结果保留 JSON 结构，但首要字段为面向 agent 的短自然语言 `summary`。
- `summary` 用一句话说明“现在发生了什么、是否需要 agent 下一步决策、如果 blocked 是暂时还是终态”。
- `status`、`commandId`、`reason/errorCode` 等字段继续保留，用于测试、UI、日志关联和 `stardew_task_status` 后续查询。
- 不采用纯自然语言工具结果，因为会削弱测试和宿主关联；也不采用字段优先结果，因为会让 LLM 过度依赖字段拼装。

### Round 3

Question: 第一版范围是否限制为 private-chat host task 不被 reply close 短预算阻断、action/status 工具增加短 `summary`、完善 `stardew_task_status`、更新 Stardew prompt/skill、补测试和 errors；明确不做 craft/trade/gather 真实窗口 handler、不重写任务系统、不新增复杂队列模型、不让宿主替 agent 自动决策、不把所有状态工具改成大而全世界扫描？

User answer:

- 认可。

Scope decision:

- In scope:
  - 修 private-chat host task 不被 `private_chat_reply_closed` 短预算阻断。
  - Stardew action/status 工具结果增加短 `summary`。
  - 完善 `stardew_task_status` 描述和返回内容。
  - 更新 `stardew-core`、`stardew-task-continuity`、runtime prompt，写清窗口/动画/事件是状态事实，不 block agent 流程。
  - 补回归测试和 `$errors` 记录。
- Out of scope:
  - 不实现 craft/trade/gather 真实窗口 handler。
  - 不重写整个任务系统。
  - 不新增复杂队列模型。
  - 不让宿主替 agent 自动决定下一步。
  - 不把所有状态工具改成大而全世界扫描。

### Round 4

Question: 是否接受边界：agent 流程并行，但 NPC 身体执行不并行；可恢复等待保留在当前 task/status 里，新冲突动作直接 blocked，让 agent 自己决定等待、取消、改计划或回复玩家？

User answer:

- 同意。

Decision:

- 一个 NPC 同时最多一个世界动作 running。
- agent 可以继续思考、说话、查询状态和更新 todo，不被游戏 UI/window/animation 直接挂起。
- 新冲突世界动作不排成长队，也不覆盖当前动作；host 立即返回 `summary + blocked/action_slot_busy`。
- 可恢复等待属于当前 task/status，例如 `running/waiting_for_menu_close`、`running/waiting_for_animation` 或 `blocked/menu_busy`，由 agent 通过事实决定下一步。
