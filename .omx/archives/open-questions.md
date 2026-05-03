当前无与“星露谷海莉 NPC 可见移动修复”计划相关的关键开放问题。

已吸收的决议：

- Phase 1 固定为同 location、短距离、多 tick 可见移动；跨 location 先显式 `blocked`。
- 候选目标默认走 observation-first 路径，先进入首轮 `GameObservation.Facts`；仅当该表达经验证无法低噪音稳定成立时，才评估同一个 `StardewNpcToolFactory` 下新增 `stardew_world_snapshot`。
