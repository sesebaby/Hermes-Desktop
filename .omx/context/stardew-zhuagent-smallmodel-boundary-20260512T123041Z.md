Task statement
- 讨论 Stardew NPC autonomy 中主 agent（zhuagent）与小模型（delegation/local executor lane）的长期分工边界。

Desired outcome
- 明确什么决策必须由主 agent 负责，什么执行可以下放给小模型。
- 明确这套分工是否适用于后续更多窗口/动作类型，而不只是一条私聊链路。
- 为后续进入 `$ralplan` / `$autopilot` 提供稳定需求边界。

Stated solution
- 用户当前没有要求实现，只要求先做深访澄清，讨论是否“分工反了”。

Probable intent hypothesis
- 用户担心现在的设计把“连续性收口 / 微动作 / todo 更新”放回主 agent，导致真正应该薄执行的小模型反而不承担稳定执行职责，长远上会让更多窗口/动作编排失控。

Known facts / evidence
- `NpcAutonomyLoop` 当前明确要求父层自己决定下一步动作，并直接可见 `stardew_idle_micro_action`、`todo/todo_write`、`stardew_speak`、`stardew_task_status`。[src/runtime/NpcAutonomyLoop.cs]
- `StardewNpcToolSurfacePolicy` 当前 parent surface 包含 `stardew_idle_micro_action`，local executor surface 只保留 `stardew_status` 和 `stardew_task_status`。[src/games/stardew/StardewNpcTools.cs]
- `npc_delegate_action` 是私聊父 agent 用来把现实世界动作委托给宿主的工具，允许 `move/observe/wait/task_status/idle_micro_action/escalate`。[src/games/stardew/StardewNpcTools.cs]
- 项目记忆和 AGENTS 约束都强调：宿主不能替 agent 决策，`local executor` 不是隐藏 fallback，只能作为 agent-native 可见编排的一部分，不能偷偷吃自由文本。

Constraints
- 必须 agent-native。
- 宿主只提供事实、工具、确认、执行结果，不替 NPC 决策。
- 不能回退到隐藏 executor / closure lock / 文本解析 fallback。
- 方案必须可扩展到更多窗口和世界动作，而不是只修私聊。

Unknowns / open questions
- 用户心中的“主 agent 应负责到哪一层”为何。
- 用户是否希望把所有现实世界写动作统一下放到小模型执行层。
- `todo/todo_write`、`idle_micro_action`、`speak` 这类连续性动作是否也应收敛到统一 delegation contract。

Decision-boundary unknowns
- 主 agent 是否只负责“意图 + 计划 + 选择目标/策略”，小模型负责“已定意图的落地执行与状态收口”。
- 还是主 agent 继续直接调用所有 player-visible tool，小模型只做 move/observe 这种窄动作。

Likely codebase touchpoints
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`

Prompt-safe initial-context summary status
- not_needed
