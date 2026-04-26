# 星露谷物语参考索引

更新时间：2026-03-26

## 这份资料解决什么问题

这份资料只服务当前 `M1` 基础版目标：

- `对话`
- `记忆`
- `创造物品`

并且重点回答四个落地问题：

- 官方文档和工具链在哪
- 哪些能力可以直接走数据层
- 哪些能力必须写 `C# / SMAPI`
- 哪些开源 mod 最值得复用

## 官方文档与工具链

- `SMAPI` 文档入口  
  https://smapi.io/docs
- `Modding:Dialogue`  
  https://stardewvalleywiki.com/Modding:Dialogue
- `Modding:Mail data`  
  https://stardewvalleywiki.com/Modding:Mail_data
- `Modding:Event data`  
  https://stardewvalleywiki.com/Modding:Event_data
- `Modding:Trigger actions`  
  https://stardewvalleywiki.com/Modding:Trigger_actions
- `Modding:Festival data`  
  https://stardewvalleywiki.com/Modding:Festival_data
- `Modding:Items`  
  https://stardewvalleywiki.com/Modding:Items
- `SMAPI Data API`  
  https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Data
- `SMAPI Harmony API`  
  https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Harmony
- `Content Patcher author guide`  
  https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide.md
- `Content Patcher EditData`  
  https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-editdata.md

## 当前最值得复用的开源轮子

### 对话与送礼

- `CustomGiftDialogue`  
  https://github.com/purrplingcat/StardewMods/blob/master/CustomGiftDialogue/README.md  
  价值：
  - 已经支持 `NPC -> 玩家送礼`
  - 直接定义了 `Mods/PurrplingCat.CustomGiftDialogue/NpcGiftData`
  - 很适合拿来做我们自己的 `NpcGiftRule`

- `HappyBirthday`  
  https://github.com/janavarro95/Stardew_Valley_Mods/tree/master/GeneralMods/HappyBirthday  
  价值：
  - 已经跑通 `生日 -> NPC 祝福 -> 给礼物 -> 邮件`
  - 明确证明“礼物会随好感变化”

- `BirthdayMail`  
  https://github.com/KathrynHazuka/StardewValley_BirthdayMail  
  价值：
  - 重点可借 `邮件调度`
  - 重点可借 `避免重复发信`

- `ImmersiveFestivalDialogue`  
  https://github.com/tangeriney/ImmersiveFestivalDialogue  
  价值：
  - 很适合借节日语境下的对白组织方式

- `Unique Gift Dialogues Expanded`  
  https://github.com/PrincessFelicie/Unique-Gift-Dialogues-Expanded  
  价值：
  - 适合借“指定礼物 -> 指定台词”的内容结构

### AI 对话与动态描述

- `StardewGPT`  
  https://github.com/henrischulte/stardewgpt  
  价值：
  - 可借 NPC AI 对话入口
  - 可借会话控制和 API 接入思路
  注意：
  - 它是 PoC，不是直接可上产品的架构

- `GoldPerEnergyDisplay`  
  https://github.com/AnthonySchneider2000/GoldPerEnergyDisplay  
  价值：
  - 明确证明可以 patch `StardewValley.Object.getDescription()`
  - 这是“动态礼物描述”最关键的实现证据

## 按能力拆解的 M1 落地结论

### 1. 对话

- 普通 NPC 对话：优先走 `Characters/Dialogue/<NPC>`
- 事件式多人对话：优先走 `Data/Events/<Location>`
- 节日对话：优先走 `Festival data`
- 头顶气泡：优先走 `Strings/SpeechBubbles`、事件命令 `speak / message / itemAboveHead`

结论：

- `M1` 的“原版对话框 + 气泡 + NPC 群聊”在星露谷里不需要先写复杂 UI
- `M1` 的“群聊”先收敛成“事件式多人连续发言”

### 2. 邮件发礼物

最短路径：

- 条件命中
- `AddMail`
- `Data/Mail`
- 邮件正文内 `%item` 或动作

结论：

- 星露谷最不该重造的轮子就是 `邮件发礼物`
- `Data/Mail + AddMail` 已经足够做 `M1`

### 3. 触发

当前最值得复用的是：

- 事件前置条件
- 节日数据
- `Trigger actions`
- `CustomGiftDialogue` 的 `NpcGiftData`

适合映射的触发源：

- 好感度
- 时间
- 生日
- 节日
- 活动
- 随机

### 4. 动态名称与描述

目前最稳的工程结论：

- `模板级改名`：可走 `Data/Objects`
- `实例级自定义状态`：走 `item.modData`
- `动态描述显示`：走 Harmony patch
- `动态名称显示`：也应按 `modData + patch` 方向做，不要指望只靠 Content Patcher

因此：

- 星露谷的“AI 名称 + AI 描述”应定义成 `SMAPI C# 能力`
- 不能把它定义成纯数据包能力

## M1 推荐主链路

最稳的首发链路如下：

1. NPC 气泡或原版对话框提示玩家
2. `C#` 负责判定触发条件
3. 礼物通过 `AddMail` 发到邮箱
4. 礼物实例把 AI 名称与描述写进 `item.modData`
5. 需要显示详情时通过 Harmony patch tooltip

## 当前不要高估的点

- 不要把“持续聊天室 UI”写成 `M1` 硬门槛
- 不要把“所有礼物逻辑都能只靠 Content Patcher”写成 `M1` 前提
- 不要把“动态名称与描述”误判成纯文本资产改动
