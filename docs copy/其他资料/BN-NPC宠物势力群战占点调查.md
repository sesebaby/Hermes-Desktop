# BN NPC/宠物/势力群战/占点调查

更新时间：2026-04-18

补充说明：这份文档只补充 `Bright Nights调查纪要.md` 里没展开的部分，聚焦：

1. `NPC/同伴` 能不能打群架
2. `宠物/坐骑` 能不能协同战斗
3. `势力` 有没有系统化对抗
4. 能不能 `占领基地/据点`

## 1. 先说结论

- `NPC/同伴打群架`：`有`
- `宠物/坐骑协同战斗`：`有`
- `势力间自动长期对抗/战争`：`只有基础，不是成熟主玩法`
- `占领基地/据点`：`基本没有现成系统`

如果你要的是：

- `玩家带几个伙伴、几只宠物，和敌人打一团`：`BN 能做`
- `不同派系在地图上长期互殴、扩张、占点、占领基地`：`BN 现成不行`

## 2. NPC/同伴群战

原版是有的。

- 跟随 NPC 会在靠近玩家时，如果周围有敌人，直接切到攻击逻辑，而不是只傻跟随。
- NPC AI 会评估敌我，选择目标并执行近战/远程行为。
- 玩家同伴会加入 `your_followers` 阵营，敌我判定会受 faction 关系影响。

本地证据：

- [npcmove.cpp#L981](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/npcmove.cpp#L981)
- [npcmove.cpp#L991](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/npcmove.cpp#L991)
- [npctalk_funcs.cpp#L677](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/npctalk_funcs.cpp#L677)
- [avatar.cpp#L1249](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/avatar.cpp#L1249)
- [npc.cpp#L2211](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/npc.cpp#L2211)

结论：

- `小队战`、`护卫战`、`跟随者混战` 没问题。
- 但这还是 `战术层`，不是 `战略层派系战争`。

## 3. 宠物/坐骑协同

这块也是原生支持。

- 宠物/友方怪物有 `friendly` 状态。
- 宠物可以跟随。
- 一部分宠物可以被下令 `忽略敌人/主动接敌`。
- 有 `PET_ARMOR`、骑乘、套具、牵引这些体系。

本地证据：

- [monster.cpp#L1398](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/monster.cpp#L1398)
- [monster.cpp#L3422](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/monster.cpp#L3422)
- [monmove.cpp#L871](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/monmove.cpp#L871)
- [monexamine.cpp#L418](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/monexamine.cpp#L418)
- [mtype.h#L173](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/mtype.h#L173)
- [mtype.h#L188](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/mtype.h#L188)
- [item_creation.md#L209](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/docs/en/mod/json/reference/items/item_creation.md#L209)
- [pets_medium_quadruped_armor.json:4](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/json/items/armor/pets_medium_quadruped_armor.json:4)

结论：

- `宠物参战`、`坐骑参战`、`宠物装备` 都能用。
- 但更像 `单体宠物系统`，不是 `宠物军团管理`。

## 4. 势力对抗

### 4.1 有什么

BN 有 faction 数据和关系表。

- 可以定义 `kill on sight`
- 可以定义 `watch your back`
- 可以定义 `knows your voice`

怪物 faction 这边支持更完整，能自然形成多方混战。  
NPC faction 这边有基础，但实现面比较窄。

本地证据：

- [faction.h#L29](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/faction.h#L29)
- [factions.md#L74](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/docs/en/mod/json/reference/creatures/factions.md#L74)
- [factions.md#L86](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/docs/en/mod/json/reference/creatures/factions.md#L86)
- [monfaction.h#L15](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/monfaction.h#L15)
- [monster.cpp#L1413](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/monster.cpp#L1413)
- [data/json/npcs/factions.json:466](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/json/npcs/factions.json:466)

### 4.2 没有什么

没有看到成熟的：

- overmap 级势力扩张
- 自动抢地盘
- 补给线
- 据点归属争夺循环
- AI 势力长期战争

源码里甚至直接把这件事写成了未来 TODO：

- NPC 以后“应该”能攻击 rival faction 的 base
- 现在只是去一个附近地点当据点，然后待着

本地证据：

- [npcmove.cpp#L4200](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/npcmove.cpp#L4200)
- [npcmove.cpp#L4238](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/src/npcmove.cpp#L4238)

这基本可以视为官方源码层面的直接结论：

`有势力数据，不等于有势力战争玩法。`

## 5. 基地/据点/占领

### 5.1 本地仓库里能看到什么

能看到的主要是两类：

1. `营地/基地任务接口`
2. `剧情据点建设链`

比如：

- 文档里还有 `basecamp_mission`
- 还有 `FACTION_CAMP_ANY`
- Tacoma Ranch 这类剧情据点会随着任务 `update_mapgen`

本地证据：

- [npcs.md#L673](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/docs/en/mod/json/reference/creatures/npcs.md#L673)
- [npcs.md#L692](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/docs/en/mod/json/reference/creatures/npcs.md#L692)
- [npcs.md#L779](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/docs/en/mod/json/reference/creatures/npcs.md#L779)
- [NPC_ranch_foreman.json:54](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/json/npcs/tacoma_ranch/NPC_ranch_foreman.json:54)
- [NPC_ranch_foreman.json:773](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/json/npcs/tacoma_ranch/NPC_ranch_foreman.json:773)
- [group_camp_missions.json:74](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/json/npcs/homeless/group_camp_missions.json:74)

### 5.2 真正缺的是什么

缺的是：

- 占领后归属切换
- 敌我据点控制权
- 据点防守/反攻循环
- 势力 AI 抢点
- “基地被谁占了” 的持续推进系统

所以它不是 `占点系统`，更像：

`剧情驱动的据点改造 / 建设任务`

而不是：

`可重复运行的战略据点玩法`

### 5.3 外部公开资料补充

外部公开资料还给了一个更强的信号：

- BN 在公开讨论里后来把 `faction camp` 移除了
- 所以“通用基地营地框架”至少不是当前官方主推方向

公开链接：

- https://github.com/cataclysmbn/Cataclysm-BN/discussions/3866
- https://github.com/cataclysmbn/Cataclysm-BN/pull/4369
- https://github.com/cataclysmbn/Cataclysm-BN/pull/5147
- https://github.com/cataclysmbn/Cataclysm-BN/issues/6781

结论：

- `剧情据点`：有
- `建设任务链`：有
- `可占领、可争夺、可换手的基地系统`：没有现成成熟方案

## 6. 自带 mod 里最值得借的

### 6.1 Aftershock

最接近 `势力 + NPC任务 + 局部地盘控制`。

- 有 faction
- 有任务链
- 有可招募 NPC
- 有 `faction_owner`

证据：

- [Aftershock/README.md:5](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/Aftershock/README.md:5)
- [Aftershock/npcs/factions.json:3](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/Aftershock/npcs/factions.json:3)
- [Aftershock/npcs/prepnet_dialogue.json:138](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/Aftershock/npcs/prepnet_dialogue.json:138)
- [Aftershock/maps/mapgen/prepnet_orchard.json:8](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/Aftershock/maps/mapgen/prepnet_orchard.json:8)

但它也没有把 `据点争夺战` 做成通用框架。

### 6.2 DinoMod

最接近 `宠物/坐骑/阵营生态 + NPC剧情`。

- 有驯化
- 有骑乘
- 有宠物食物
- 有宠物装备

证据：

- [DinoMod/README.md:7](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/DinoMod/README.md:7)
- [DinoMod/monsters/dinosaur.json:533](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/DinoMod/monsters/dinosaur.json:533)
- [DinoMod/items/petfoods.json:7](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/DinoMod/items/petfoods.json:7)

### 6.3 MagicalNights

偏 `剧情/NPC/任务/阵营`，不偏占点。

证据：

- [MagicalNights/npc/npc.json:3](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/MagicalNights/npc/npc.json:3)
- [MagicalNights/npc/missiondef.json:4](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/MagicalNights/npc/missiondef.json:4)
- [MagicalNights/npc/factions.json:3](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/MagicalNights/npc/factions.json:3)

### 6.4 Civilians

更像 `世界氛围 / 骚乱模拟`，不是伙伴经营。

证据：

- [Civilians/modinfo.json:8](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/Civilians/modinfo.json:8)
- [Civilians/monstergroup.json:4](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/Civilians/monstergroup.json:4)

## 7. 对你最有用的判断

如果你想做的是：

### 路线 A：伙伴 + 宠物 + 小规模群战

可行，而且是 `低于平均难度` 的改法。

因为：

- 原版已有同伴
- 原版已有宠物
- 原版已有敌我判定
- 原版已有小队战斗

### 路线 B：势力冲突 + 任务驱动据点建设

可行，但会比 A 难一截。

可以复用：

- faction 数据
- NPC 任务链
- update_mapgen
- 故事据点模板

### 路线 C：派系战争 + 自动扩张 + 占点 + 占领基地

现成不行，属于 `重做一整层战略系统`。

## 8. 内置 mod 和社区 mod 的区别

### 8.1 内置 mod

仓库里 `data/mods` 下面这些，是随游戏一起发的 `in-repo mods`。

对你这次评估最相关的，还是：

- `Aftershock`
- `DinoMod`
- `MagicalNights`
- `Civilians`

本地证据：

- [Cataclysm-BN/data/mods](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods)

### 8.2 社区 mod

BN 现在明确把第三方 mod 和内置 mod 分开了。

- 第三方 mod 不应该放进 `data/mods`
- 手动安装时，应该放到游戏根目录下的 `mods`
- BN 官方也提供了单独的社区 mod registry

本地证据：

- [Where-to-put-3rd-party-mods.txt](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/data/mods/!ONLY%20MODS%20INCLUDED%20WITH%20THE%20GAME%20GO%20HERE!/Where-to-put-3rd-party-mods.txt)
- [README.md](/D:/GitHubPro/AllGameInAI2/external/Cataclysm-BN/README.md)

公开链接：

- https://mods.cataclysmbn.org/
- https://github.com/cataclysmbn/registry
- https://docs.cataclysmbn.org/mod/json/explanation/in_repo_mods/

一句话就是：

`内置 mod 适合拿来拆源码和抄结构，社区 mod 适合拿来补内容和找现成点子。`

## 9. 社区 mod 里和你方向最相关的

先说总判断：

- 社区 mod `有很多`
- 但大多强在 `内容/NPC/宠物/据点玩法`
- 目前没看到一个成熟现成包，直接给你 `势力自动开战 + 占点换手 + 地图扩张`

### 9.1 Sky Island

偏 `基地/撤离/raid 循环`。

- 有自己的玩法闭环
- 更像独立生存模式
- 适合参考 `基地玩法`
- 不适合直接当 `派系占领系统`

公开链接：

- https://mods.cataclysmbn.org/mods/cbn_sky_island/

### 9.2 NPC Recruitment Options

偏 `伙伴系统增强`。

- 主要是扩展 NPC 招募条件
- 对“多同伴队伍玩法”有帮助
- 但不解决势力战争

公开链接：

- https://mods.cataclysmbn.org/mods/recruitment_options/

### 9.3 Draco's Dog Mod

偏 `宠物战斗`。

- 重点是攻击犬/训练犬
- 对“宠物参战”方向有参考价值
- 不是军团管理

公开链接：

- https://mods.cataclysmbn.org/mods/dracodogmod/

### 9.4 Salvaged Robots

偏 `可控单位扩展`。

- 把部分机器人变成可回收、可改造、可带着跑的单位
- 如果你想做“非人同伴小队”，这个方向值得参考

公开链接：

- https://mods.cataclysmbn.org/mods/salvaged_robots/

### 9.5 Add Bandits Extended+

偏 `人类敌对团伙`。

- 更适合做 `遭遇战/群架`
- 能补“敌对人类目标”这块
- 不是自动势力战争

公开链接：

- https://mods.cataclysmbn.org/mods/gov_bandits_kai_r/

### 9.6 BL9

偏 `超大内容包`。

- 有 NPC、地点、商人、巡逻、阵营味道
- 很适合看“怎么堆内容和世界感”
- 但它也不是成熟的地图级占点系统

公开链接：

- https://mods.cataclysmbn.org/mods/bl9_100monres/
- https://github.com/Kenan2000/BL9

## 10. 对改造方向的实际建议

如果你要尽快做一个 `能玩` 的原型，优先级建议是：

### 10.1 最省事的原型

`伙伴 + 宠物 + 人类敌对团伙`

可以重点参考：

- `原版 NPC/同伴逻辑`
- `原版宠物/坐骑逻辑`
- `DinoMod`
- `NPC Recruitment Options`
- `Add Bandits Extended+`

这条线能最快做出：

- 玩家带队
- 宠物参战
- 多目标混战
- 局部据点清剿

### 10.2 中间难度的原型

`伙伴 + 宠物 + 任务驱动据点建设`

可以重点参考：

- `Aftershock`
- `MagicalNights`
- `Sky Island`
- `update_mapgen`
- `故事据点任务链`

这条线能做出：

- 招人
- 养宠
- 清点
- 建点
- 任务推进后据点变化

### 10.3 最难的原型

`势力自动扩张 + AI 抢点 + 基地换手`

这个目前没现成 mod 可直接抄。

你基本要自己补：

- overmap 级据点归属
- 势力资源和刷新
- AI 攻点/守点
- 占领后地图状态更新
- 长期推进循环

一句话结论：

`BN 适合做“战术层队伍生存 + 任务据点变化”，不适合直接拿来做“战略层派系战争模拟”。`
