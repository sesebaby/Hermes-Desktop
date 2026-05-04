# Deep Interview Spec: NPC移动工具契约重构

## 元数据
- 访谈ID: 8a7b3c2d-1e4f-5a6b-9c8d-7e6f5a4b3c2d
- 轮次: 5
- 最终歧义度: 13%
- 类型: brownfield
- 生成时间: 2026-05-04
- 阈值: 20%
- 状态: PASSED

## 清晰度分解
| 维度 | 分数 | 权重 | 加权 |
|------|------|------|------|
| 目标清晰度 | 0.90 | 35% | 0.32 |
| 约束清晰度 | 0.90 | 25% | 0.23 |
| 成功标准 | 0.80 | 25% | 0.20 |
| 上下文清晰度 | 0.90 | 15% | 0.14 |
| **总清晰度** | | | **0.88** |
| **歧义度** | | | **13%** |

## 目标

**将 NPC 移动从"LLM 逐格微操"重构为"LLM 选择目的地，桥接层负责全路径执行"**，使海莉能够在星露谷中跨地图自主移动到有意义的语义目的地（如从 HaleyHouse 走到镇广场喷泉）。LLM 只做一次目的地级决策，桥接层利用游戏内置的 `PathFindController` + `Game1.warpCharacter()` 自动完成路径搜索、跨地图 warp 和失败恢复。

**核心策略：日程+自主混合。** 桥接层读取 NPC 的真实游戏日程（`NPC.Schedule`），作为观察事实暴露给 LLM。LLM 看到"我今天10点该去喷泉、1点该回家"，可以选择按日程走，也可以选择自由行动。游戏永远不驱动 Agent——日程只是事实，决策权在 LLM。

## 问题根因

当前移动系统存在设计层缺陷：**LLM 在做"每步踩哪块砖"的微操决策，而不是"我要去哪里"的目的地决策。**

具体表现为四个环节的连锁限制：

| 环节 | 文件:行号 | 当前行为 | 问题 |
|------|-----------|----------|------|
| 候选生成 | `BridgeHttpHost.cs:323-337` | 12方向近邻偏移中取3个可达的 | LLM只能看到身边2格内的瓷砖 |
| 参数约束 | `stardew-navigation.md:18` | 参数必须逐字来自 `moveCandidate[n]` | LLM被强制绑定到近邻坐标 |
| 跨地图封锁 | `BridgeCommandQueue.cs:365-369` | `block("cross_location_unsupported")` | 无法跨地图移动 |
| 移动执行 | `BridgeCommandQueue.cs:434-435` | `setTilePosition` 逐格瞬移 | 无走路动画，无路径优化 |

**核心矛盾：工具暴露给LLM的是"移动执行"接口（坐标级），而不是"移动意图"接口（目的地级）。**

## 关键发现：游戏已有成熟移动系统

通过分析 ScheduleViewer 和 TheStardewSquad 参考 mod，发现星露谷自带完整 NPC 移动基础设施，无需从零构建：

| 游戏机制 | 来源 | 用途 |
|----------|------|------|
| `NPC.Schedule` | `Dictionary<int, SchedulePathDescription>` | 每个NPC每天已解析好的日程 |
| `PathFindController` | 游戏内置 A* 路径搜索 | 按日程移动时自动使用 |
| `Game1.warpCharacter()` | 游戏内置跨地图 API | 在位置间传送 NPC |
| `npc.controller` / `npc.temporaryController` | 游戏移动控制器 | 管理 NPC 移动状态 |
| `SchedulePathDescription` | 日程条目结构 | 含目标位置、坐标、朝向、到达行为 |

TheStardewSquad 已验证了从游戏手中接管 NPC 移动控制的模式（`SquadMateStateHelper.PrepareForRecruitment()`：清掉 `npc.controller`、`npc.temporaryController`、`npc.DirectionsToNewLocation`，然后自己设新控制器）。

## 约束

- 遵循 HermesCraft 架构模式：Agent 自己通过工具理解世界、桥接层只暴露能力接口
- 遵循三层分离：tool 定义可执行契约、world skill 解释目的地语义（含日程含义）、navigation skill 负责移动方法和失败恢复
- 游戏日程**只作为观察事实**暴露，不替 NPC 做决策——NPC 可以选择不跟日程
- 不得让桥接层替 NPC 做目的地选择
- 移动执行优先使用游戏内置 `PathFindController`，不从头实现 A*
- 跨地图移动必须有可观测日志、超时和可重试路径
- 改动范围：`StardewNpcTools.cs`、`stardew-navigation.md`、`stardew-world` skill、`BridgeHttpHost.cs`、`BridgeCommandQueue.cs`、`BridgeMovementPathProbe.cs`
- 参考 hermescraft-main 的 goto 模式 + TheStardewSquad 的 NPC 控制权接管模式

## 非目标

- **不做** NPC 日程编辑/生成系统（只读日程，不改游戏数据）
- **不做** 编队、跟随、战斗等复杂移动模式
- **不做** 经济、种植、制作等扩展
- **不改变** NPC 人格/记忆系统
- **不做** UI 重设计

## 验收标准

### 第一迭代：单地图内语义目的地移动
- [ ] 观察事实新增 `schedule_entry[n]`（NPC当前日程条目）和 `destination[n]`（世界目的地）
- [ ] `stardew_move` 工具契约改为接收语义目的地标签（如 "Town fountain"），不再接收裸坐标
- [ ] 桥接层在收到目的地后使用 `PathFindController` 自主完成路径搜索和执行
- [ ] 桥接层接管 NPC 移动控制权并逐格执行路径（参考 `SquadMateStateHelper` 模式）
- [ ] 海莉能在 HaleyHouse 内从卧室走到客厅（自动化测试 mock + 真实游戏验证）
- [ ] LLM 一次 tick 只做一次目的地决策，不在移动途中重复决策
- [ ] 路径受阻时桥接层返回明确错误码（`path_blocked`、`path_unreachable`），不静默失败

### 第二迭代：跨地图移动
- [ ] 取消 `cross_location_unsupported` 封锁
- [ ] 桥接层使用 `Game1.warpCharacter()` 实现跨地图 warp
- [ ] 桥接层支持跨地图路径：当前位置 → warp点 → 传送 → 新地图 → 路径到目的地
- [ ] 海莉能从 HaleyHouse 走到 Town 的喷泉旁（真实游戏验证）
- [ ] 跨地图移动全程有可观测日志（进入/离开每个地图）

### 第三迭代：日程感知
- [ ] 桥接层读取 `NPC.Schedule` 并将当日日程条目作为 `schedule_entry[n]` 观察事实暴露
- [ ] LLM 在观察中看到"今天10:00该去Town喷泉"，可选择跟日程或自由行动
- [ ] 日程条目和世界目的地作为同等的可选候选项，LLM 二选一
- [ ] 海莉一日内既能跟日程（如按时去喷泉），也能自由行动（如中途拐去海滩）

### 通用
- [ ] 自动化测试覆盖：LLM sees schedule_entry + destinations → calls stardew_move(destination) → mock bridge returns path status → polls to completion
- [ ] 路径失败时 NPC 不无限重试，回到观察状态
- [ ] 日志清晰区分"Agent 没发工具命令"和"bridge 收到命令但执行失败"

## 暴露与化解的假设

| 假设 | 挑战 | 决议 |
|------|------|------|
| LLM需要看到坐标才能移动 | "hermescraft-main 的 LLM 调用 goto({x,y,z}) 时并不知道路径怎么走" | LLM只需知道目的地名称，坐标由桥接层从世界数据中解析 |
| 跨地图移动是架构限制 | "project.md 没有禁止跨地图，当前是实施选择" | 跨地图是技术问题，桥接层可用 `Game1.warpCharacter()` 解决 |
| 移动必须逐格由LLM确认 | "TheStardewSquad 的 NPC 一次决定目的地后自动走完全程" | 一次性目的地决策 + 桥接层 `PathFindController` 执行 |
| 需要自己写A*路径搜索 | ScheduleViewer揭示游戏已有`PathFindController` | 直接用游戏内置A*，无需重复造轮子 |
| 日程是Agent的对立面 | "日程只是事实，LLM可以选择不跟" | 日程+自主混合：观察暴露日程事实，决策权在LLM |

## 技术上下文

### 关键改动文件
- `src/games/stardew/StardewNpcTools.cs:64-238` — `StardewMoveTool` 改为接收语义目的地标签
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs:314-471` — 候选生成从近邻坐标改为目的地列表 + 日程条目
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:365-369` — 取消 `cross_location_unsupported`；使用 `PathFindController` + `warpCharacter`
- `Mods/StardewHermesBridge/Bridge/BridgeMovementPathProbe.cs` — 增加跨地图路径探测
- `skills/gaming/stardew-navigation.md` — 更新为"目的地级决策 + 桥接层自主执行"流程
- `skills/gaming/stardew-world/SKILL.md` — 增加目的地语义解释 + 日程条目含义
- `Mods/StardewHermesBridge/ModEntry.cs` — 可选：读取 `NPC.Schedule` 用于观察事实

### 参考项目可复用资产
| 资产 | 来源 | 用途 |
|------|------|------|
| `SchedulePathDescription` 结构 | 游戏原生 | 日程条目的目标位置、坐标、朝向、行为 |
| `Game1.warpCharacter()` | 游戏原生 | 跨地图传送 NPC |
| `PathFindController` | 游戏原生 | 同地图内 A* 路径搜索+执行 |
| `SquadMateStateHelper.PrepareForRecruitment()` | TheStardewSquad | 接管 NPC 移动控制权的模式 |
| `FollowerManager.ExecutePathMovement()` | TheStardewSquad | 逐格执行路径+路径优化参考 |
| `AStarPathfinder.cs` | TheStardewSquad | 备用：如果 `PathFindController` 不够用 |
| `goto({x,y,z})` API 设计 | hermescraft-main | 目的地级工具契约设计参考 |
| `NPC.Schedule` 读取方式 | ScheduleViewer | 安全读取日程字典并转换为观察事实 |

## 本体（关键实体）

| 实体 | 类型 | 字段 | 关系 |
|------|------|------|------|
| NPC | 核心领域 | npcId, location, personality, memory | NPC 调用 stardew_move, NPC 观察世界含日程 |
| Destination | 支撑 | label, locationName, x, y, tags, reason, facingDirection | 世界目的地，观察产生，LLM选择 |
| ScheduleEntry | 支撑 | time, locationName, x, y, facingDirection, endBehavior | 游戏日程条目，观察暴露，LLM可选跟或不跟 |
| Route | 支撑 | steps[], currentStep, totalSteps, crossLocationWarps[] | 桥接层用 PathFindController 从目的地计算 |
| Location | 核心领域 | locationName, tiles, warpPoints | NPC 位于 Location, Location 有 warp 点连接到其他 Location |
| stardew_move | 工具 | destination (标签), reason | LLM 调用一次，桥接层执行全路径+跨地图+到达 |
| Bridge | 基础设施 | PathFindController, warpCharacter, schedule reader | 执行路径搜索+跨地图传送+日程读取 |

## 本体收敛

| 轮次 | 实体数 | 新增 | 变更 | 稳定 | 稳定性 |
|------|--------|------|------|------|--------|
| 1 | 6 | 6 | — | — | N/A |
| 2 | 6 | 1 (Route) | 1 (PlaceCandidate→Destination) | 4 | 83% |
| 3 | 6 | 0 | 1 (stardew_move→stardew_goto) | 5 | 100% |
| 4 | 6 | 0 | 0 | 6 | 100% |
| 5 | 7 | 1 (ScheduleEntry) | 0 | 6 | 86% |

> 第5轮新增 ScheduleEntry 是日程混合策略的体现，与 Destination 形成互补——两者同属"可选目的地候选"，只是来源不同（游戏 vs 世界知识）。稳定性从100%降至86%是合理的，因为引入了新概念而非推翻旧概念。

## 访谈记录

<details>
<summary>完整问答 (5轮)</summary>

### Round 1
**Q:** 当你说海莉应该"像人一样真实地移动"时，你脑海中她应该能做到的 TOP 3 行为是什么？
**A:** 三者都需要：跨地图自主移动 + 有目的的移动 + 流畅的视觉移动
**歧义度:** 44% (目标:0.75, 约束:0.40, 标准:0.30, 上下文:0.80)

### Round 2
**Q:** 哪个场景最能让你说"对了，这就是我要的真实移动"？
**A:** 海莉从家里走到镇上：全程LLM只做一次目的地决策，桥接层自动处理跨地图路径和行走动画
**歧义度:** 28% (目标:0.90, 约束:0.45, 标准:0.65, 上下文:0.85)

### Round 3
**Q:** 四个改动方案选哪个？推荐选项1：改工具契约+桥接层
**A:** 同意，按这个方向：stardew_move从"(x,y)"改为语义目的地，桥接层负责全路径执行，小步验证
**歧义度:** 21% (目标:0.90, 约束:0.75, 标准:0.65, 上下文:0.85)

### Round 4
**Q:** 验证方式：自动化测试还是真实游戏？
**A:** 两者都要：自动化测试覆盖决策链路(mock桥接层) + 真实游戏手动端到端验证
**歧义度:** 16% (目标:0.90, 约束:0.80, 标准:0.80, 上下文:0.85)

### Round 5
**Q:** 利用游戏日程(ScheduleViewer揭示)还是LLM完全自主？
**A:** 方向A：日程+自主混合。桥接层读取NPC真实日程暴露给LLM，LLM可选择跟日程或自由行动。桥接层用PathFindController+warpCharacter执行
**歧义度:** 13% (目标:0.90, 约束:0.90, 标准:0.80, 上下文:0.90)

</details>
