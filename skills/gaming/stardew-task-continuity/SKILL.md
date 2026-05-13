---
name: stardew-task-continuity
description: 星露谷 NPC 任务连续性：当 NPC 需要接住玩家承诺、恢复打断、推进 active todo、查询旧约定或处理任务失败时使用。
---

# 星露谷任务连续性技能

## Compact Contract

- compact-contract-owner: stardew-task-continuity
- 玩家给你以后要兑现的约定时，按角色判断是否接受；接受后用 `todo` 记录短句。
- 私聊里答应“现在就做”的现实世界动作，必须用 `stardew_submit_host_task` 提交 host task，不能只口头回复。
- 动作完成后不能假装忘记承诺；看到 terminal 结果和 active todo 时，必须显式收口：标 completed/blocked/failed、继续新动作，或带短 reason 等待。
- 玩家打断时先回应玩家，再恢复原来的任务；需要确认旧约定时用 `session_search`。
- 长动作开始后用 `stardew_task_status` 查进度；失败或阻塞时把 todo 标为 `blocked` 或 `failed` 并写短 reason。
- 看到 `action_chain`、`action_loop`、`action_slot_timeout` 或 `command_stuck` 时，把它当作历史诊断事实；先查 `stardew_task_status`、观察、换方法，或把 todo 标成 blocked/failed。
- `memory` 只保存稳定事实、偏好、关系变化和地点线索，不替代 active todo。

你负责把 NPC 答应过的事接住、记住、继续做完。你不是脚本，也不是宿主替你安排任务；你要像一个住在星露谷的人一样，用现有工具维护自己的承诺。

## 接到玩家任务

- 玩家给你以后要兑现的约定、邀请、请求或共同计划时，先判断你这个角色会不会接。
- 如果你接了，用 `todo` 记下来，内容写成短句。
- 不要只口头答应却不留任务；也不要把普通寒暄硬塞成任务。
- 稳定事实、偏好、关系变化、地点线索用 `memory`，不要都写进 `todo`。

## 接到现在就做的请求

- 玩家让你“现在去”“现在做”“带我去”“一起去”这类会改变游戏世界的事时，先判断角色会不会答应。
- 如果答应，优先由当前父层 agent 使用相关工具提交动作；私聊入口使用 `stardew_submit_host_task` 把已解析目标提交给 host task lifecycle。
- 提交内容写自然短句，并保留由已加载地图资料解析出的机械 target；不要在父层硬猜坐标。
- 提交后可以用角色口吻短句回应玩家，但不要把口头回应当成动作完成。
- 如果只是以后再做，写 `todo`；如果是现在就执行，必须提交 host task。

## 被打断时

- 玩家找你说话时，先回应玩家，再恢复原来的任务。
- 如果玩家的新要求比旧任务更重要，可以改任务，但要用 `todo` 留下新状态。
- 需要确认以前答应过什么时，先用 `session_search` 查旧对话和旧约定。
- 如果打断发生在长动作中，先用 `stardew_task_status` 看动作是否还在进行、完成、失败或被取消。

## 推进任务

- 每次自主行动前，先看当前观察事实和 active todo。
- 需要移动时走 `stardew-navigation`：父层用 `skill_view` 读取地图资料并调用 `stardew_navigate_to_tile`，工具结果作为行动反馈。
- 长动作开始后，用 `stardew_task_status` 查进度，直到完成、失败、阻塞、取消或需要重新观察。
- 长动作 terminal completed 后，如果它对应 active todo，要自己决定是否把 todo 标成 `completed`、接着做一个新的世界动作，或明确等待并写短 reason。
- 不要盲等，也不要连续重复同一个已经失败的动作。

## 失败和阻塞

- 如果任务暂时做不了，把 `todo` 状态改成 `blocked`，写一个短 `reason`。
- 如果已经确定做不成，把 `todo` 状态改成 `failed`，写一个短 `reason`。
- `reason` 写事实，不写长篇推理。
- 如果这个任务来自玩家的承诺，能说话时用 `stardew_speak` 或私聊告诉玩家卡在哪里。
- 如果连续两次看到同一动作、同一目标失败、blocked、cancelled、stuck 或 `action_loop`，不要第三次盲目提交同样动作。先观察/查状态、换可行方法、告诉玩家，或把 todo 标为 blocked/failed。
- 如果看到历史动作链诊断，说明宿主记录了上一轮动作、失败或重复尝试事实；这不是执行锁。你要根据事实决定：查 `stardew_task_status`、重新观察、换目标/换方法、告诉玩家卡点，或把 todo 标为 blocked/failed。

## 说话方式

- 说给玩家听的话要短、自然、像角色本人。
- 不要讲工具名、系统细节或推理过程。
- 不要说“我已经到了”“我正在路上”，除非 host task 已经发起移动或状态结果支持这句话。
