# Stardew Action Chain Guards Reference Alignment Context

## Task statement

为 Stardew NPC “动作完成 -> 收口 -> 新动作 -> 再收口”循环制定一个参考项目对齐的中文计划。用户明确要求不要自行臆造方案，必须对齐 `external/hermescraft-main` / `external/hermes-agent-main` 的真实做法，并作为既有 `.omx/plans/stardew-tool-orchestration-harness-plan.md` 的补充。

## Desired outcome

形成一个不直接改代码的共识计划，说明如何在当前 Hermes-Desktop NPC runtime 中补齐 action-chain guard：

- 保持 agent-native：NPC agent 自己决策，宿主只执行、记录、反馈、限流。
- 复用当前 `ActionSlot`、`PendingWorkItem`、`LastTerminalCommandStatus`、`SessionTodoStore`、runtime jsonl、skill prompt、autonomy loop。
- 从参考项目迁移“单任务槽、短状态回填、重复失败提示、stuck watchdog、tool id/result 配对、max turn/tool budget”的原则，而不是照搬 Minecraft 代码或引入第二套任务系统。

## Reference evidence

### hermescraft-main

- `external/hermescraft-main/bot/server.js:108` 有 `currentTask` 作为单一 background task state。
- `external/hermescraft-main/bot/server.js:116` 有 `actionHistory` 滚动窗口用于重复失败/循环检测。
- `external/hermescraft-main/bot/server.js:703` 的 `briefState()` 会在每次动作响应里带上简短世界状态、新聊天、pending command、task running/done/error/stuck。
- `external/hermescraft-main/bot/server.js:748` 在最近 3 次同 action 非 done 时给出 `action_loop` 提示。
- `external/hermescraft-main/bot/server.js:2610` 的 `/task/ACTION` 是异步长任务，返回 `task_id`，只允许一个 running task；已有 running task 时 409。
- `external/hermescraft-main/bot/server.js:2625` 长任务完成/失败后更新 `currentTask` 并写 `actionHistory`。
- `external/hermescraft-main/bot/server.js:2687` watchdog 检测 movement task 10 秒不移动，将任务标记为 `stuck` 并写错误。
- `external/hermescraft-main/SOUL-civilization.md:12` 指导每个小动作后读聊天；`:161` 指导动作失败两次后 stop/status/换方法。

### hermes-agent-main

- `external/hermes-agent-main/environments/agent_loop.py:138` 有 `max_turns`，`:204` 外层按最大 turn 控制 agent loop。
- `external/hermes-agent-main/environments/agent_loop.py:291` 保留 tool call id，`:466` 用 `tool_call_id` 回填 tool result。
- `external/hermes-agent-main/environments/agent_loop.py:338` 未知工具、非法 JSON、工具异常都作为 tool result/error 回给模型。
- `external/hermes-agent-main/environments/agent_loop.py:476` 对每轮 tool results 做 aggregate budget enforcement。
- `external/hermes-agent-main/acp_adapter/events.py:47` 的 tool progress callback 为 `tool.started` 分配 id；`:60` 明确使用 per-tool FIFO 让同名/并行 tool completion 对上正确 call。
- `external/hermes-agent-main/tools/todo_tool.py:21` todo 是 session/task-loop 结构，active items 会被注入上下文；失败不是另起宿主剧情系统。

## Current Hermes-Desktop evidence

- `src/runtime/NpcRuntimeDescriptor.cs:46` 当前 controller 已有 `PendingWorkItem`、`:54` `ActionSlot`、`:62` `IngressWorkItems`、`:103` `LastTerminalCommandStatus`。
- `src/games/stardew/StardewNpcTools.cs:1099` `StardewRuntimeActionController.TryBeginAsync` 已在 `ActionSlot` 或 `PendingWorkItem` 存在时返回 `action_slot_busy`。
- `src/games/stardew/StardewNpcTools.cs:1246` terminal status 写入 `LastTerminalCommandStatus`，`:1302` 清理 claim/slot/pending/next wake。
- `src/runtime/NpcAutonomyBudget.cs:24` 已有限制每轮工具次数，`:29` 已有限制 restart 次数，`:35` 默认最大工具 6、并发 LLM 1、重启冷却 5 秒。
- `src/runtime/NpcAutonomyLoop.cs:297` terminal action + active todo 会构造 closure required prompt；`:696` 没有工具/没有 explicit no-action 时写 `closure_missing`。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:1151` 每轮优先处理一个 ingress；`:1205` delegated action 遇到 busy slot 直接 return true，当前没有 attempt/defer 计数或显式 next wake。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:1555` 每 NPC tracker 合并 dispatch，避免 host poll 堆无限 worker backlog。
- `src/tasks/SessionTodoStore.cs:9` todo 已支持 `blocked` 和 `failed`，可承载任务收口与失败状态。

## Constraints

- 不新增外部 MCP server，不把 action chain guard 做成第二套 MCP-only 运行时。
- 不新增第二 task/runtime/memory/tool lane。
- 不硬编码 NPC、地点、自然语言地名、剧情规则。
- 不让 local executor 执行真实 move/speak/open/private/idle 写动作。
- 不把 `destination[n]`、`nearby[n]`、`moveCandidate[n]` 注入 autonomy wake prompt。
- 宿主可以限流、记录事实、诊断、释放资源，但不能替 NPC 选择下一步生活动作。

## Open questions

- action-chain 状态是否放入 `NpcRuntimeControllerSnapshot`，还是只做 runtime activity derived view。倾向：放 controller/state store，因为它需要跨重启恢复和测试可见。
- 是否把 chain guard 应用于所有 world-writing action，还是只应用 private-chat commitment actions。倾向：所有 world-writing action 共享 guard，但只有存在 active todo/commitment 时强制 closure。
- `closure_missing` 是否升级为一次 repair wake。倾向：先做 bounded self-check/repair，一次即可，避免无限追问。

## Likely touchpoints

- `src/runtime/NpcRuntimeDescriptor.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcRuntimeDriver.cs`
- `src/runtime/NpcRuntimeStateStore.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `skills/gaming/stardew-task-continuity/SKILL.md`
- `skills/gaming/stardew-world/SKILL.md`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
