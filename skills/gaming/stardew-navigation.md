# Stardew Navigation Skill

第一阶段导航只覆盖最小可行的 `move` 循环。

在移动前：

- 确认目标地点和坐标来自最新的 `moveCandidate[n]` 或 `placeCandidate[n]` 桥接事实，或者来自现有私聊例外路径中已解析好的目标，或者是已知的安全测试目标。
- 选择有意义的世界内目的地时，优先使用 `placeCandidate[n]`，因为它包含标签和与星露谷上下文相关的理由。
- 将 `placeCandidate[n]` 视为轻量级星露谷日程条目：语义化地点、精确位置/坐标、理由、可选朝向，以及可选的结束行为说明。
- 当没有合适的有意义地点候选时，再使用 `moveCandidate[n]` 做短距离重新定位。
- 当最新观察包含一个安全候选，并且移动符合 NPC 当前意图时，直接用该候选的 `locationName`、`x`、`y`、`reason` 和可选的 `facingDirection` 调用 `stardew_move`，不要只说自己要移动。
- 通过 `move` 任务契约发送移动，不要直接调用 HTTP。
- 跟踪 `commandId`、状态、失败原因和 `traceId`。
- 如果目标被阻塞、不可达、已经过时，或者与其他认领冲突，就报告原因并重新观察。

第一阶段不要发明高级的 `goto`、`follow`、`interact`、采集、箱子、制作或种田行为。
不要把普通 host 事件或玩家移动当成移动指令；除非是私聊，否则它们只是上下文。
