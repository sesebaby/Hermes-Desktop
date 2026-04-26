# Stardew Auto Action And Schedule Restore Contract

状态：

- active design baseline

owner：

- stardew integration owner

用途：

- 用大白话冻结自动行动怎么执行、执行完怎么回当前日程。

executor split：

1. `NpcAutoActionExecutor`
2. `NpcScheduleRestoreExecutor`

reference boundary：

1. 第一阶段直接抄 `TheStardewSquad` 的任务骨架
2. `ScheduleViewer` 只抄日程读取事实层
3. 不允许 AI 直接控制逐帧路径

auto action inputs：

- accepted action intent
- 当前 scene snapshot
- 当前 NPC 位置
- 当前 schedule state
- 可交互目标

auto action flow：

1. 读当前宿主事实
2. 选 title-local 可执行任务
3. 计算目标点 / 交互点
4. 寻路
5. 执行动作
6. 产出结果
7. 进入 schedule restore

result states：

- `executed`
- `blocked`
- `deferred`
- `failed`

schedule restore flow：

1. 查询当前应在 schedule entry
2. 若可恢复，按宿主恢复链回去
3. 若不可恢复，返回结构化 blocked / deferred

schedule restore minima：

- `npcId`
- `scheduleEntryRef`
- `targetLocation`
- `targetTile`
- `restoreOutcome`
- `failureClass`

固定规则：

1. 自动行动结束后默认必须尝试回日程
2. 不能恢复时，必须给出明确 blocked / deferred
3. 不允许执行完临时任务后把 NPC 永久留在错误地点

绝对禁止：

1. 不允许 AI 直接给逐帧移动指令
2. 不允许 auto action executor 跳过 schedule restore
3. 不允许把 `RecruitmentManager` 招募玩法语义直接冒充正式 NPC AI

update trigger：

- auto action 任务骨架变化
- schedule restore 规则变化

