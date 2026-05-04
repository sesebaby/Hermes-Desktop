# Stardew Navigation Skill

第一阶段导航只覆盖最小可行的 `move` 循环。

## 职责边界

本 skill 是移动循环与失败恢复 owner。它负责“最新观察 -> `stardew_move` -> 查询任务状态 -> 失败后重新观察或换目标”的方法。

地点标签、地点理由、`endBehavior` 和 `placeCandidate[n]` 为什么有意义由 `stardew-world` 解释；本 skill 只复制候选里已经存在的字段，不重新发明世界语义。

## 硬规则：移动不是叙事文本

物理位移不是台词，也不是内心独白。只要你想让 NPC 在游戏世界里真实改变位置，就必须调用 `stardew_move`。

- 看到或准备写出“走向、走到、回房、出门、靠近、移动到、上楼、下楼、离开、前往”等实际位移动作时，先使用本 skill 的移动流程。
- 如果没有调用 `stardew_move`，不要写“已经走向/走到/回到/离开/上楼/下楼”这类看起来完成了物理移动的句子；只能等待、观察、说话，或说明没有移动。
- `stardew_speak` 只负责说话；它不能替代 `stardew_move`，也不能让 NPC 真实换位置。
- `stardew_move` 的参数必须逐字来自最新观察里的 `moveCandidate[n]` 或 `placeCandidate[n]`：`locationName`、`x`、`y`、`reason`，以及可选的 `facingDirection`。

在移动前：

- 确认目标地点和坐标来自最新的 `moveCandidate[n]` 或 `placeCandidate[n]` 桥接事实，或者来自现有私聊例外路径中已解析好的目标。
- 选择有意义的世界内目的地时，可以使用 `placeCandidate[n]`，但只复制它给出的 `locationName`、`x`、`y`、`reason` 和可选 `facingDirection`。
- 将 `placeCandidate[n]` 视为 endpoint candidate；它不是 host 命令，也不是永久路线保证。
- 如果移动以 `path_blocked` 或 `path_unreachable` 结束，先重新观察或换目标，不要原样重试同一目的地。
- 当没有合适的有意义地点候选时，再使用 `moveCandidate[n]` 做短距离重新定位。
- 当最新观察包含一个安全候选，并且移动符合 NPC 当前意图时，直接用该候选的 `locationName`、`x`、`y`、`reason` 和可选的 `facingDirection` 调用 `stardew_move`，不要只说自己要移动。
- 通过 `move` 任务契约发送移动，不要直接调用 HTTP。
- 跟踪 `commandId`、状态、失败原因和 `traceId`。
- 如果目标被阻塞、不可达、已经过时，或者与其他认领冲突，就报告原因并重新观察。

第一阶段不要发明高级的 `goto`、`follow`、`interact`、采集、箱子、制作或种田行为。
不要把普通 host 事件或玩家移动当成移动指令；除非是私聊，否则它们只是上下文。
