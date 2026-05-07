---
name: stardew-navigation
description: 星露谷 NPC 目的地级移动——观察候选、选择目的地、调用 stardew_move、轮询状态、处理打断与失败。当 NPC 需要移动时必须使用。
---

# 星露谷导航技能

本技能负责移动循环、失败恢复和中途打断处理。

## 职责边界

本技能负责："观察目的地 → 选择匹配意图的目的地 → `stardew_move(destination, reason)` → 轮询任务状态 → 处理打断/失败 → 重新观察或切换目标"。

`stardew-world` 解释目的地的含义；本技能只复制观察到的 `destination[n]` 字段，或在已披露地图 skill 给出机械目标时保留完整 `target(locationName,x,y,source)`。

每轮先确定本轮目标。优先用最新短期上下文；缺少历史承诺或路线记录时按需用 `session_search`。`memory` 只用于持久更新，不用于临时移动摘要。移动意图明确后，不要重新查询广泛状态工具。

## 硬规则：移动不是叙述文本

物理移动不是对话或内心独白。当 NPC 需要在游戏世界中改变位置时，你必须调用 `stardew_move`。

- 如果你看到或准备写描述物理移动的词（"走到"、"去"、"前往"、"返回"、"离开"、"接近"），使用本技能的移动流程。
- 如果没有调用 `stardew_move`，不要写声称 NPC 已经到达或移动的句子。只能等待、观察、说话，或注明没有发生移动。
- `stardew_speak` 只处理说话；它不能移动 NPC。
- `destinationId` 只能复制自最新观察的 `destination[n]`，不得发明。`label` 只用于理解地点含义，不能作为 `stardew_move` 的 `destination` 参数。

## 目的地级移动流程

1. 观察：读取 `stardew_status` 中最新 `destination[n]` 事实。
2. 选择：选一个 `destinationId`、label 和 reason 最匹配当前意图的 `destination[n]`。
3. 调用：`stardew_move(destination=<destinationId 精确值>, reason=<简短意图>)`。
   - `destinationId` 必须逐字复制，区分大小写。
   - 不要传入 label 作为 destination 参数。
   - 如果没有可执行的 `destinationId`，不要调用 `stardew_move`。
4. 轮询：用 `stardew_task_status` 查进度，直到到达终态：`completed`、`failed`、`blocked`、`cancelled` 或 `interrupted`。
   - `stardew_task_status` 只用于长动作进度查询；不要用作广泛世界扫描。
5. 处理结果：
   - `completed`：已到达，继续下一步动作。
   - `failed` / `blocked`：重新观察或换一个目的地，不要立即重试同一个。
   - `interrupted`：读取 `interruption_reason`（如 `player_approached`、`event_active`、`dialogue_started`），决定是重新观察、换目标还是响应打断。
   - 超时（3 次轮询无终态）：重新观察。

## 机械坐标目标（executor-only）

当已披露的地图 skill 明确给出地点和坐标时，父层可以输出 mechanical `target(locationName,x,y,source)`，其中 `source` 必须是坐标来源 skill id。

mechanical `target(locationName,x,y,source)` 由本地 executor-only 的 `stardew_navigate_to_tile` 执行；父层不要调用或编写 `stardew_navigate_to_tile` 工具参数，本地小模型也不要改写坐标。

如果没有完整的 `target.locationName/x/y/source`，不要让本地 executor 猜坐标，改用 `destinationId`、观察或等待。

地图坐标按需分层披露。需要把自然语言地点（如"去海边"、"去广场"）转换成机械目标时，先加载索引：

`skill_view(name="stardew-navigation", file_path="references/index.md")`

索引只告诉你有哪些 region；region 文件只告诉你少量出口和 POI 入口；只有 POI 文件可以给出最终 `target(locationName,x,y,source)`。不要从未加载的 region/poi 猜坐标。

## nearby[n] 约束

`nearby[n]` 事实是短距离（1-2 格）的附近安全位置上下文，不是 `stardew_move` 的替代输入。

**不得连续多步 nearby 来模拟长距离移动。** 这会破坏目的地级移动的设计。

## 跨地点移动

跨地点移动可以由 bridge 分段执行：当前地图 segment、warp transition、切图后重规划、最终 target segment。发起后必须用 `stardew_task_status` 轮询，重点看 `crossMapPhase`、`currentSegment`、`finalTarget` 和 `lastFailureCode`。

## 移动前确认

- 确认 destinationId 来自最新观察——事实可能在两次 tick 之间变化。
- 有意图的移动优先选 `destination[n]` 而非 `nearby[n]`。
- `destination[n]` 和 `schedule_entry[n]`（可用时）同等可选——NPC 可以跟日程，也可以自由选择。
- 跟踪 `commandId`、状态、失败原因和 `traceId`。
