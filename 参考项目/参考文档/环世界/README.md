# 环世界参考索引

更新时间：2026-03-26

## 这份资料解决什么问题

这份资料只服务当前 `M1` 基础版目标：

- `对话`
- `记忆`
- `创造物品`

并且重点回答四个落地问题：

- 环世界的官方 / 社区主文档和工具链在哪
- 头顶气泡和群聊该借什么轮子
- “创造物品并掉地上”该用什么原生 API
- 动态名称 / 描述应该挂在哪一层

## 主文档与工具链

环世界没有像 `SMAPI` 那样统一的官方 mod API 门户，当前最实用的主文档组合是：

- `RimWorld Modding Tutorials` 总入口  
  https://rimworldwiki.com/wiki/Modding_Tutorials
- `ThingComp` 教程  
  https://rimworldwiki.com/wiki/Modding_Tutorials/ThingComp
- `Harmony` 教程  
  https://rimworldwiki.com/wiki/Modding_Tutorials/Harmony
- `BigAssListOfUsefulClasses`  
  https://rimworldwiki.com/wiki/Modding_Tutorials/BigAssListOfUsefulClasses
- `Writing custom code`  
  https://rimworldwiki.com/wiki/Modding_Tutorials/Writing_custom_code

常用前置 / 框架：

- `Harmony`  
  https://github.com/pardeike/Harmony
- `HugsLib`  
  https://github.com/UnlimitedHugs/RimworldHugsLib
- `HugsLib Wiki`  
  https://github.com/UnlimitedHugs/RimworldHugsLib/wiki
- `HugsLib ModBase reference`  
  https://github.com/UnlimitedHugs/RimworldHugsLib/wiki/ModBase-reference
- `HugsLib Introduction to Patching`  
  https://github.com/UnlimitedHugs/RimworldHugsLib/wiki/Introduction-to-Patching

## 当前最值得复用的开源轮子

### 对话气泡

- `Bubbles / Interaction Bubbles`  
  https://github.com/Owlchemist/Bubbles  
  价值：
  - 已经实现“社交记录 -> 头顶气泡”
  - README 里直接写了 Harmony patch 点
  - 很适合借 UI 表现和 patch 入口

- `SpeakUp`  
  https://github.com/jptrrs/SpeakUp  
  价值：
  - 是现成的 conversation mod
  - 适合借“社交文本如何出现在世界里”的整体思路

## 按能力拆解的 M1 落地结论

### 1. 对话与群聊

当前最稳的实现策略：

- 单句互动：借 `Bubbles`
- 多人连续互动：做成“多 NPC 连续气泡”
- 不把 `M1` 的“群聊”定义成完整聊天软件 UI

结论：

- 环世界的“头顶气泡 + 气泡群聊”是可落地的
- 最该复用的是 `Bubbles` 的表现层和 patch 点，不是重做一套对话系统

### 2. 物品掉地

当前最关键的原生 API 方向：

- `ThingMaker.MakeThing`
- `GenPlace.TryPlaceThing`

参考依据：

- `BigAssListOfUsefulClasses` 明确点名了所有 `Maker` / `Gen` 类是常用入口
- `RimWorldModGuide` 的基础说明直接写到 `ThingMaker.MakeThing(ThingDef def, ThingDef Stuff = null)`

结论：

- 环世界“NPC 创造物品 -> 掉落地上 -> 玩家拾取”不需要先找一个完整成品 mod
- 原生 API 就够强，重点是把触发和表现层串起来

### 3. 动态名称与描述

当前最稳的落点是 `ThingComp`。

原因：

- `ThingComp` 是 RimWorld 最常见的实例态扩展方式
- 教程明确列出了这些可覆写入口：
  - `TransformLabel`
  - `CompInspectStringExtra`
  - `GetDescriptionPart`
  - `AllowStackWith`
  - `PostSplitOff`
  - `PreAbsorbStack`

工程判断：

- 如果礼物要有“这一次的 AI 名称 / AI 描述”，优先挂在 `ThingComp`
- 如果需要彻底改显示名或检查面板，再补 Harmony patch

### 4. 记忆

`M1` 不需要把环世界记忆做成完整社交脑模型。

当前更稳的实现是：

- 把“NPC 最近一次送礼语境 / 最近一次送礼文案 / 触发来源”记成轻量状态
- 状态落在 `ThingComp`、`GameComponent`、`WorldComponent` 或 mod 自己的数据层
- 不要一开始就追求全社交图谱

## M1 推荐主链路

最稳的首发链路如下：

1. 条件命中后，NPC 触发一段社交气泡
2. 用 `ThingMaker.MakeThing` 创建礼物
3. 用 `GenPlace.TryPlaceThing` 掉落到地上
4. 礼物实例状态挂在 `ThingComp`
5. 如需展示 AI 描述，用 `CompInspectStringExtra` 或 inspect patch

## 当前不要高估的点

- 不要把 `M1` 写成“要先做完整对话树系统”
- 不要把“动态名称 / 描述”误写成必须新建一整套物品系统
- 不要让 `HugsLib` 变成不必要依赖；只在需要它的日志、设置、调度等能力时再依赖
