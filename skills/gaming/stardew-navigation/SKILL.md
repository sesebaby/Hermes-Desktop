---
name: stardew-navigation
description: 星露谷 NPC host task 导航：当 NPC 需要把自然语言地点意图解析成地图 tile、跨地图移动、查询移动状态或处理移动失败时使用。
---

# 星露谷导航技能

## Compact Contract

- compact-contract-owner: stardew-navigation
- 移动是 agent-native：父层 agent 通过 skill 资料解析地点意义并调用 `stardew_navigate_to_tile`；实际移动仍由宿主和 bridge 执行。
- 私聊父 agent 答应“现在去某地”时，用 `stardew_submit_host_task` 提交 `action=move` host task；父层不要绕过 host task lifecycle。
- 父层 agent 收到自然语言地点后，先加载本技能，再加载 `references/index.md`，再加载相关 region 和 POI 文件。
- 只有已经加载的 POI/reference 文件可以提供最终 `target(locationName,x,y,source)`。
- 绝对不要编造坐标。目标缺失、有歧义或未加载时，返回 blocked/escalate，不要猜。
- 长动作进度只用 `stardew_task_status` 查询。

本技能负责地点解析、移动循环、失败恢复和中途打断处理。它不是硬编码地点脚本；它要求 agent 通过 skill 资料理解世界，再把明确坐标交给宿主执行器。

## 职责边界

私聊或自主父 agent 决定“要不要移动、为什么移动”，并负责读取地图 skill、解析地点和调用导航工具。宿主不替 agent 解析自然语言地点，只负责执行已明确的机械 target。

`stardew-world` 解释地点的社会和世界意义；本技能只在需要真实移动时读取地图参考，并只使用已披露 reference 里的坐标。

每轮先确定本轮目标。优先用最新短期上下文；缺少历史承诺或路线记录时按需用 `session_search`。`memory` 只用于持久更新，不用于临时移动摘要。移动意图明确后，不要重新查询广泛状态工具。

## 硬规则：移动不是叙述文本

物理移动不会因为说“我现在过去”就发生。只要要改变 NPC 在游戏世界中的位置，就必须调用导航工具链。

- 如果你看到或准备写描述物理移动的词（“走到”“去”“前往”“返回”“离开”“接近”），使用本技能的移动流程。
- 父 agent 答应“现在去某地”时，应先用 `stardew_submit_host_task` 提交 `action=move`，不要只回复。
- 父层 agent 决定 move 后，必须用 `skill_view` 读取本技能和地图参考文件。
- 如果没有调用 `stardew_navigate_to_tile`，不要声称 NPC 已到达或正在移动。
- `stardew_speak` 只负责说话，不能移动 NPC。
- 不使用 `destinationId`；地点解析走 skill 资料和 `stardew_navigate_to_tile` 单轨。

## Host Task 提交流程

1. 读取 `skill_view(name="stardew-navigation")`。
2. 读取 `skill_view(name="stardew-navigation", file_path="references/index.md")`。
3. 按 index 选择最小相关 region 文件，再读取最具体的 POI 文件。
4. 当已加载参考文件明确给出 `locationName`、`x`、`y`、`source` 后，调用 `stardew_navigate_to_tile`。
5. 如果多个 POI 都匹配，或没有 POI 给完整 target，不要导航；返回 blocked，并说明缺少什么或哪里有歧义。

## 机械目标规则

`stardew_navigate_to_tile` 是父层 agent 可调用的机械动作工具。它需要：

- `locationName`
- `x`
- `y`
- `source`
- 可选 `facingDirection`
- 简短 `reason`

`source` 必须标识披露坐标的已加载地图 skill reference。不要从未加载的 region/POI 猜坐标。

## 坐标资料披露

地图坐标按需分层披露。需要把自然语言地点（如“去海边”“去广场”）转换成机械目标时，先加载索引：

`skill_view(name="stardew-navigation", file_path="references/index.md")`

索引只告诉你有哪些 region；region 文件只告诉你少量出口和 POI 入口；只有 POI 文件可以给出最终 `target(locationName,x,y,source)`。

## 跨地点移动

跨地点移动可以由 bridge 分段执行：当前地图 segment、warp transition、切图后重规划、最终 target segment。发起后必须用 `stardew_task_status` 轮询，重点看 `crossMapPhase`、`currentSegment`、`finalTarget` 和 `lastFailureCode`。

## 移动前确认

- 确认地点坐标来自已经加载的 reference 文件。
- 确认目标不是普通寒暄或只需要说话的社交回应。
- 确认父层已经决定“现在执行”，并且当前是在处理 active todo 或当前轮行动。
- 跟踪 `commandId`、状态、失败原因和 `traceId`。

## 失败处理

- `completed`：已到达，继续下一步动作。
- `failed` / `blocked`：读取当前状态或换一个相关参考文件后再判断，不要盲目重复。
- `interrupted`：查看打断原因，决定重试、等待或报告 blocked。
- 超时或没有终态：查询一次 `stardew_task_status`，然后带证据 blocked 或 escalate。
