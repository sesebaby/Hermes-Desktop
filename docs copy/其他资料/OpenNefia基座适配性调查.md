# OpenNefia基座适配性调查

日期：2026-04-18

## 1. 本次判断基于什么目标

这次不是按“战略经营游戏”评估，而是按你当前已经明确的目标评估：

- 数据驱动
- NPC个体驱动
- 任务驱动
- 不是后台势力结算
- 不是生产链/岗位/资源模拟
- 世界结果来自玩家与NPC、NPC与NPC的实际互动
- 每个NPC尽量视为独立个体
- `Hermes` 是核心，不是外挂增强

用一句话概括：

`你要做的是“活世界沙盒RPG”，不是战略经营游戏。`

## 2. 结论

## 推荐结论

`OpenNefia 适合做这个方向的轻量基座，明显比 BN/CDDA 更合适。`

但要说清楚：

- `适合做基座`，不等于 `现成就能做成目标游戏`
- 它已经有很多你要的“个体驱动世界”拼装件
- 但最关键的那层 `NPC个人任务代理/长期目标/离屏继续行动` 还需要你自己补

我的最终判断是：

`适合做 Hermes 宿主基座，但 NPC行为运行时 应优先归到 Hermes 核心侧。`

## 3. 为什么它比BN/CDDA更适合你

### 3.1 它本来就是“个体视角”系统

OpenNefia 现成是围绕：

- 角色
- 同伴
- 队伍
- 任务
- 家园
- 地图切换
- 存档持久化

来组织的。

这和你的目标一致，因为你要的是：

`NPC自己接任务、自己行动、自己和世界互动，然后自然产生结果。`

而不是：

`势力每回合结算一次，数字对数字。`

相关源码：

- `external/OpenNefia/OpenNefia.Content/Parties/PartySystem.cs`
- `external/OpenNefia/OpenNefia.Content/Quests/QuestSystem.cs`
- `external/OpenNefia/OpenNefia.Content/Quests/MapImmediateQuestSystem.cs`
- `external/OpenNefia/OpenNefia.Content/Home/HomeSystem.cs`

### 3.2 它的底层结构比BN/CDDA更容易长期维护

OpenNefia 是 C#/.NET 8，核心是比较标准的 IoC + ECS + 事件系统。

相关源码：

- `external/OpenNefia/OpenNefia.Core/IoCSetup.cs`
- `external/OpenNefia/OpenNefia.Core/GameObjects/EntitySystem.cs`
- `external/OpenNefia/OpenNefia.Core/GameObjects/EntityManager.cs`
- `external/OpenNefia/OpenNefia.Core/GameObjects/EntityEventBus.Broadcast.cs`
- `external/OpenNefia/OpenNefia.Core/GameObjects/EntityEventBus.Directed.cs`

这意味着：

- 你后续加 `NPC任务代理系统`
- 加 `据点归属组件`
- 加 `任务调度器`
- 接 `Hermes`

都更容易切成独立系统，而不是去硬改巨型 C++ 遗产。

### 3.3 它本身就支持数据驱动和Mod扩展

OpenNefia 有完整的：

- Prototype/YAML 体系
- 变量加载体系
- Mod/Assembly 加载体系

相关源码：

- `external/OpenNefia/OpenNefia.Core/Prototypes/PrototypeManager.cs`
- `external/OpenNefia/OpenNefia.Core/EngineVariables/EngineVariablesManager.cs`
- `external/OpenNefia/OpenNefia.Core/ContentPack/ModLoader.cs`
- `external/OpenNefia/OpenNefia.Core/GameController/GameController.cs`

这和你要的“数据驱动”是同路的。

它不是所有行为都已经数据化，但它至少不是纯死写死绑的。

### 3.4 它有 headless / debug 基础

OpenNefia 不是只能手工点着玩。

相关源码：

- `external/OpenNefia/OpenNefia.EntryPoint/Program.cs`
- `external/OpenNefia/OpenNefia.Core/IoCSetup.cs`
- `external/OpenNefia/OpenNefia.Tests/FullGameSimulation.cs`
- `external/OpenNefia/OpenNefia.Core/Console/ConsoleHost.cs`

能看到：

- 有 `DisplayMode.Headless`
- 有控制台/REPL/debug server
- 有完整模拟测试入口

这对以后做：

- AI试玩
- 自动回归
- NPC行为验证
- 批量世界测试

很有价值。

## 4. 它现在已经能支撑到什么程度

## 4.1 能直接借力的部分

### A. NPC/同伴/队伍

现成有：

- 招募同伴
- 队伍成员管理
- 跨图跟随玩家
- 敌我关系同步

关键证据：

- `external/OpenNefia/OpenNefia.Content/Parties/PartySystem.cs`

其中 `TryRecruitAsAlly()` 已经能：

- 把 NPC 加入队伍
- 同步队伍归属关系
- 触发招募后的事件

这说明：

`“玩家带几个人去做事”` 这层不是从零开始。

### B. 家园/驻留

现成有：

- 家园系统
- 仆从/雇员
- 家园板
- 留守系统

关键证据：

- `external/OpenNefia/OpenNefia.Content/Home/HomeSystem.cs`
- `external/OpenNefia/OpenNefia.Content/Home/HomeSystem.Areas.cs`
- `external/OpenNefia/OpenNefia.Content/Home/ServantSystem.cs`
- `external/OpenNefia/OpenNefia.Content/Stayers/StayersSystem.cs`

尤其 `StayersSystem` 很重要：

- 留守 NPC 会从地图里移出
- 进入全局容器保存
- 玩家回到目标地图时再恢复出来

这证明 OpenNefia 已经接受一种思路：

`NPC不一定一直在玩家眼前，但可以持续“存在”。`

这和你要的 `离屏轻代理` 很接近。

### C. 任务系统

现成有：

- 普通任务
- 即时任务
- 护送、收集、讨伐、征服等任务类型

关键证据：

- `external/OpenNefia/OpenNefia.Content/Quests/QuestSystem.cs`
- `external/OpenNefia/OpenNefia.Content/Quests/MapImmediateQuestSystem.cs`
- `external/OpenNefia/OpenNefia.Content/Quests/Impl/EntitySystems/VanillaQuestsSystem.Conquer.cs`
- `external/OpenNefia/OpenNefia.Content/Quests/Impl/VanillaQuestsSystem.MapGen.cs`

注意：

这里的 `Conquer` 更接近：

`进入任务地图，杀一个高价值目标，任务完成`

而不是：

`某个真实据点被NPC队伍长期接管并改归属`

所以它说明有“攻目标”的味道，但还不是你要的据点占领系统。

### D. 地图/区域/存档

现成有：

- Map
- Area
- GlobalArea
- Map/Area ID
- 存档持久化
- 地图卸载与再加载

关键证据：

- `external/OpenNefia/OpenNefia.Core/Maps/MapManager.cs`
- `external/OpenNefia/OpenNefia.Core/Areas/AreaManager.cs`
- `external/OpenNefia/OpenNefia.Core/Areas/AreaManager.GlobalAreas.cs`
- `external/OpenNefia/OpenNefia.Core/SaveGames/SaveGameSerializer.cs`
- `external/OpenNefia/OpenNefia.Content/Maps/Entrances/MapTransferSystem.cs`
- `external/OpenNefia/OpenNefia.Content/Maps/MapCommonComponent.cs`

这说明：

`预设据点`
`归属到某个Area/Map`
`离开地图后保存`

这类东西都能落地。

## 4.2 能比较稳地做到的首版目标

如果按你现在的方向，我认为 OpenNefia 比较稳能做到：

1. 预设据点存在于世界中
2. 据点有归属
3. 玩家开局独立，后续可加入或自建势力
4. 玩家能招募NPC
5. 玩家能发布结构化任务
6. NPC 可驻守某据点
7. NPC 可在到点后执行任务
8. NPC 可带一个小队去攻击某个预设据点
9. 占领后据点直接改归属
10. 新势力可动态生成

但其中第 7 到 10 条，不是“开箱即用”，而是“有底子可搭”。

## 5. 它现在还缺什么

这部分最关键。

## 5.1 缺真正的“独立NPC长期任务运行时”

你要的是：

- 玩家下发任务
- NPC记住任务
- 到时间自己出发
- 不在玩家视野里也继续推进
- 根据遭遇自然形成结果

OpenNefia 现在没有现成完整框架把这条链跑通。

它有：

- 同伴
- 留守
- 即时任务
- 地图切换

但缺：

- NPC个人任务队列
- NPC长期计划/日程
- 离屏任务状态机
- NPC跨地图自主迁移与目标推进
- “任务成功后修改据点归属”的通用世界规则

所以核心缺口不是“图形”也不是“战斗”，而是：

`长期行为层`

## 5.2 势力系统太轻

现在的 `FactionComponent` / `FactionSystem` 更接近：

- 对玩家关系
- 个体之间临时仇恨/友好

关键证据：

- `external/OpenNefia/OpenNefia.Content/Factions/FactionComponent.cs`
- `external/OpenNefia/OpenNefia.Content/Factions/FactionSystem.cs`

当前没有看到现成的：

- 势力实体
- 势力资产
- 势力据点列表
- 势力扩张规则
- 势力之间长期外交

也就是说：

它有 `敌我关系`
但还没有你要的 `可成长的据点势力层`

## 5.3 征服任务不是据点占领系统

`VanillaQuestsSystem.Conquer.cs` 和对应 mapgen 说明：

- 它能生成征服任务地图
- 放一个核心敌人
- 杀掉就完成

但这不是：

- 真实世界据点
- 持续归属变化
- NPC接管
- 留守
- 再次被攻占

所以不能高估它这部分现成度。

## 5.4 没有现成“离屏个体代理”

目前能看到的离屏相关更像：

- 地图保存/卸载
- 留守 NPC 放进全局容器
- 玩家回来再恢复

这离你要的：

`NPC离屏后还像活人一样继续执行个人任务`

只差一层，但这层恰好是最重要的一层。

## 6. 如果拿它做基座，建议怎么切

## 推荐方案

`Hermes核心 + OpenNefia宿主层`

原因：

- 你的目标本质上是 `NPC驱动世界`
- 那么世界状态推进、NPC目标、任务解释、长期行为就不该放在宿主游戏里
- OpenNefia 更适合承载 `地图/角色/战斗/存档/UI/现成任务组件`
- Hermes 更适合承载 `NPC心智/任务运行时/世界交互决策`

建议拆成 4 层：

### 第1层：Hermes核心层

这是项目真正核心。

自己定义：

- NPC个体模型
- 记忆/目标/偏好
- 任务队列
- 到点执行
- 离屏轻代理
- 行为导致世界状态变化
- 任务解释与调度

这层决定“世界是不是活的”。

### 第2层：OpenNefia宿主层

直接复用：

- Map / Area / Save
- Chara / Party / Combat
- Home / Stayer
- Quest / ImmediateQuest

### 第3层：桥接层

负责把 Hermes 的决定映射到 OpenNefia 的现成能力：

- 据点定义
- 据点归属
- 势力实体
- 势力生成
- NPC可接受的结构化任务
- 行为结果落地到地图/角色/关系/归属

### 第4层：表现与工具层

- UI
- 调试
- Headless测试
- 自动试玩
- 数据验证

这层服务于 Hermes 核心，而不是反过来。

## 7. 适不适合做“活的世界”

## 我的真实判断

`适合，但只能做“轻量活世界”，不适合直接指望它长成超重型模拟世界。`

也就是：

它很适合做：

- 活的个体
- 小队行动
- 任务驱动结果
- 预设据点争夺
- 小规模势力变化

它不适合直接做：

- 大规模战争模拟
- 复杂生产链经营
- 全地图精细实时社会模拟

而你现在明确说：

`你不要战略经营，不要战争演算，要的是NPC驱动的活世界`

这反而正好落在它相对适合的区间里。

## 8. 最终建议

## 推荐

`继续把 OpenNefia 当主候选基座。`

理由：

1. 它比 BN/CDDA 更贴近“个体驱动世界”
2. 它比 BN/CDDA 更容易由我长期接手改造
3. 它有家园/同伴/任务/地图/存档这些现成地基
4. 它允许你把真正创新点集中到 `Hermes核心 + 行为桥接层`

## 不要误判的点

1. 不要以为现成 `Conquer` 就等于据点系统
2. 不要以为现成 `Faction` 就等于势力经营
3. 不要以为有 `Stayers` 就等于离屏活世界

真正的核心工作量，集中在：

`NPC独立个体任务与离屏行为代理`

## 9. 一句话总结

`OpenNefia 不是你的成品答案，但它是目前最像“活世界个体驱动RPG基座”的现成底座。`

后面如果继续推进，最该优先设计的不是图形，而是：

`Hermes核心行为模型 + 据点归属模型 + Hermes/OpenNefia桥接边界`
