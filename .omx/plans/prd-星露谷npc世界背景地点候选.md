# PRD：星露谷 NPC 世界背景与地点候选

## 需求摘要
为 Stardew NPC agent 增加通用世界背景、Haley 个人地点偏好和机器可读地点候选事实。目标是让 Haley 在自治 tick 中拥有足够世界知识和可执行地点选择，从而主动决定 move，而不是只在当前相邻格子之间小幅挪动。

## RALPLAN-DR
### 原则
- Agent 主动：host 只提供事实和工具，不替 NPC 决定行为。
- Observation-first：首轮 `ObserveAsync` facts 必须包含可用地点候选。
- 同源能力：继续使用 `NpcRuntimeSupervisor` 和 `AgentCapabilityAssembler`，不创建第二套 NPC agent。
- 渐进披露：skill 主体只放核心行动规则和索引；详细地点百科放 `references/`，由 agent 按需读取。

### 决策驱动
- Haley 当前缺少 Stardew 世界模型，不知道哪里有什么。
- 仅相邻 `moveCandidate` 会导致移动看起来像无意义小碎步。
- 成熟 Stardew Mod 的 NPC 行程不是随机挪格子，而是由时间/地点/坐标/朝向/结束行为组成的 schedule entry；ScheduleViewer 会读取 `SchedulePathDescription.targetLocationName/targetTile/facingDirection/endOfRouteBehavior`，SVE 的 NPC schedule 也是一天多个语义目的地。
- 用户明确禁止普通事件驱动 NPC move。

### 可选方案
- 方案 A：新增 `stardew-world` skill、Haley 偏好、`placeCandidate` observation facts，并让 `stardew_move` 接受最新候选。优点是首轮可见、风险小；缺点是地点候选表达需要保持克制。
- 方案 B：新增 `stardew_world_snapshot` 工具，让 agent 主动查询更多世界信息。优点是分层更清楚；缺点是首轮不一定会调用，不能稳定解决“不知道去哪”。
- 方案 C：给 Haley 长期生活 task。优点是目标明确；缺点是会引入结构约束，不符合本轮用户偏好。

## 决策
采用方案 A。世界背景进入 skill，个人偏好进入 Haley pack，地点候选进入首轮观察 facts。后续如需更大地图知识，再在同源 `StardewNpcToolFactory` 上追加 world snapshot 工具。

## 实施步骤
1. 新增目录型 skill：`skills/gaming/stardew-world/SKILL.md`。按 skill-creator 的渐进式披露原则，主文件只描述核心世界观、行动规则、候选使用方式和 reference 索引。
2. 新增 `skills/gaming/stardew-world/references/stardew-places.md`，承载更详细地点百科、地点语义、Haley 相关地点例子；不在每轮 prompt 中全文加载，只有 agent 需要深入理解地点时才通过 `skill_view(name="stardew-world", file_path="references/stardew-places.md")` 读取。
3. 更新 Haley pack：`skills.json` required 加入 `stardew-world`；`SOUL.md` / `facts.md` 增加地点偏好。
4. 扩展 bridge/core DTO：增加 `PlaceCandidate` 数据结构和 status payload 字段。
5. Bridge status 生成当前 location 内可执行 `placeCandidate`，例如 HaleyHouse 的镜子/客厅/门口等，Town/Beach 等当前地点内的安全目标。
6. 参考 `ScheduleViewer` / SVE，把 `placeCandidate` 表达成轻量 schedule entry：语义 label、location/tile、reason、可选 `facingDirection/endBehavior`。这些仍是候选事实，不是 host 指令。
7. `StardewQueryService` 将 `placeCandidate` 序列化成机器可读 facts，确保自治首轮决策可见。
8. 更新 `stardew-navigation.md` 和 `StardewNpcTools.cs` 描述，使 agent 可使用最新 `moveCandidate` 或 `placeCandidate` 调用 `stardew_move`。
9. Bridge move 执行参考成熟 Mod 的路径做法，使用 Stardew `PathFindController.findPathForNPCSchedules` 计算同地图路径，再逐 tick 可见推进；不再用简单直线步进穿墙试错。
10. 补充测试并运行核心与 bridge 测试。

## 验收标准
- Haley autonomy prompt 包含 `stardew-world` required skill 内容。
- `ObserveAsync` 可把 status payload 中的 `placeCandidates` 输出成最多 3 条 `placeCandidate[n]=...` facts。
- `stardew_move` 描述不再只允许 `moveCandidate`。
- bridge move 使用 Stardew schedule pathfinding，而不是 Manhattan 直线走格。
- 非私聊事件仍只是 context，不成为移动指令。
- bridge status 在可控时提供地点候选；阻塞时不提供候选。

## 风险与缓解
- 风险：世界背景和地点百科污染 prompt。缓解：采用 skill-creator 渐进式披露，主 `SKILL.md` 保持精简，只给世界观、决策方法和 reference 入口；详细地点资料放 `references/stardew-places.md`，由 agent 按需读取。`placeCandidate` facts 只承载当前可执行目标，不承载地点百科；最多 3 条只是 live affordance 的安全上限，不是世界知识的主要承载方式。
- 风险：跨 location 坐标当前 bridge 不一定能走。缓解：本轮候选优先当前 location。
- 风险：prompt 诱导变成强制行为。缓解：skill 文案只说明世界和选择方法，不规定固定日程。

## ADR
Decision: 使用 skill/persona/observation facts 增强 NPC 世界理解与地点选择。
Drivers: agent 主动、首轮可见、同源能力、最小可执行范围。
Alternatives considered: world snapshot 工具先行；长期任务结构先行；host 定时/事件驱动 move。
Why chosen: 方案 A 最贴近用户约束，并直接补齐 Haley 不知道去哪的根因。
Consequences: 本轮地点候选偏保守；跨 location 生活路线需要后续 pathfinding/world snapshot 扩展。
Follow-ups: 增加跨 location semantic destination、地图知识查询工具、更多 NPC 个人地点偏好。
