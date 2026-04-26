# Bright Nights调查纪要

更新时间：2026-04-17

## 1. 调查范围

本次调查围绕 `Cataclysm: Bright Nights`（后文简称 `BN`）展开，重点回答以下问题：

1. `BN` README 里官方强调的核心玩法是什么。
2. `BN` 社区 mod 生态里，是否已经存在 `势力经营`、`动态剧情`、`基地建设`、`NPC 社会模拟` 方向的积累。
3. `BN` 原版系统复杂度有多高，是否现实可改。
4. `BN` 是否有图形界面，还是只能终端运行。
5. `BN` 是否适合作为未来 AI 主导开发的新游戏底座。

---

## 2. README 里明确强调的玩法

只看官方 [README](./external/Cataclysm-BN/README.md)，`BN` 首页强调的不是“势力经营”或“剧情演出”，而是以下主循环：

1. 后启示录生存。
2. 在一个 `persistent`、`procedurally generated` 的世界里探索。
3. 搜刮文明废墟中的食物、装备、补给。
4. 找到和使用载具进行机动探索或逃生。
5. 对抗大量怪物与敌对幸存者。
6. 逐步变强，甚至“成为最强怪物之一”。

如果压成一句话：

`BN` 的官方产品定位是一个 `后启示录生存探索 roguelike`，核心体验是 `搜刮 + 生存 + 战斗 + 载具 + 变强`。

结论：

- `README` 没把 `势力经营` 当首页卖点。
- `README` 没把 `动态剧情` 当首页卖点。
- `README` 没把 `NPC 社会模拟` 当首页卖点。
- `README` 明确强调 `mod` 是正式能力，并有第三方 mod registry。

---

## 3. 社区 mod 调查结论

### 3.1 动态剧情

有明确积累。

最典型的例子是 `BL9`：

- 有大量地点、任务、日志与 lore。
- 有组织/派系背景。
- 有带 NPC 的剧情内容。
- 有商人 NPC 系统。

这类 mod 更像：

`内容型剧情扩展 + 地图/地点扩展 + 派系 lore`

而不是“系统层长期叙事模拟器”。

### 3.2 基地建设

有明确积累。

最值得看的例子是 `Sky Island`：

- 有 `home base`
- 可以囤积物资
- 可以种植
- 可以建立生产设施
- 可以扩建基地

这说明 `基地建设` 作为社区 mod 的主玩法方向是成立的，而且不是边角功能。

### 3.3 势力经营

没有发现成熟、公开、明确主打 `faction management / 势力经营` 的 `BN` 社区 mod。

我找到的情况更像：

- 有派系背景、派系 lore、派系 NPC。
- 有 faction camp / basecamp 语境。
- 但没有看到一个完成度高、公开可验证、明确把“经营多个势力 / 势力内政 / 势力战争”作为主玩法卖点的 mod。

结论：

`势力题材` 有，`成熟的势力经营玩法 mod` 没找到。

### 3.4 NPC 社会模拟

没有找到明确成熟代表。

我没有找到能证明 `BN` 社区已经有：

1. NPC 长期社交网络。
2. NPC 结盟/背叛/群体关系演化。
3. 类 `RimWorld / Dwarf Fortress` 的社会层模拟。

能找到的更多是：

- 任务型 NPC
- 商人 NPC
- 剧情型 NPC

这与“NPC 社会模拟”仍然差得很远。

### 3.5 社区 mod 生态一句话总结

从公开社区 mod 看：

- 最明确的是：`动态剧情 / 内容扩展`
- 第二明确的是：`基地建设`
- 明显偏弱的是：`势力经营`
- 基本没看到成熟案例的是：`NPC 社会模拟`

---

## 4. BN 原版系统复杂度

基于当前本地源码统计：

- `src` 文件数：`930`
- `data/json` 文件数：`1861`
- `data/mods` 文件数：`879`

较大的核心源文件包括：

- `src/game.cpp`：约 `604 KB`
- `src/character.cpp`：约 `438.8 KB`
- `src/item.cpp`：约 `407.3 KB`
- `src/map.cpp`：约 `375.1 KB`
- `src/iuse.cpp`：约 `354.4 KB`
- `src/mapgen.cpp`：约 `328.4 KB`
- `src/iexamine.cpp`：约 `309.1 KB`
- `src/vehicle.cpp`：约 `292.7 KB`
- `src/iuse_actor.cpp`：约 `266 KB`
- `src/cata_tiles.cpp`：约 `263 KB`

复杂度判断：

1. `BN` 不是轻量项目，而是一个大型、长期演化的 C++ 游戏工程。
2. 它的数据驱动比例很高，但不等于“核心逻辑简单”。
3. 真正复杂的地方是跨系统协议，而不只是文件大：
   - 地图与 overmap
   - 存档与世界流送
   - JSON 数据加载与 finalize/check 过程
   - NPC、任务、对话、活动系统
   - 载具系统
   - Lua 绑定层

一句话：

`BN` 适合做重度改造的“宿主”，但不适合当作“随便改改就会变成另一种游戏”的轻底座。

---

## 5. 改造现实吗

### 5.1 总判断

现实，但要分方向。

`现实可改` 不等于 `低成本可改`。

### 5.2 哪些方向现实

#### 方向一：动态剧情 + AI NPC + 数据驱动事件

这是相对最现实的改造方向。

原因：

1. 任务系统现成。
2. 对话系统本质上已经是 JSON 驱动状态机。
3. Lua hooks 现成。
4. mapgen / overmap / 任务目标投放能力现成。
5. 很多内容层工作可以继续放在 `JSON + Lua`。

但也要接受：

- 想做真正的 `AI-native NPC runtime`，仍然需要改 C++ 和绑定层。
- 现成系统更偏“规则 NPC + 任务图”，不是自由代理。

#### 方向二：势力经营 + 势力战争

有条件现实，但比上面难很多。

原因：

1. `faction` 数据对象是现成的。
2. overmap 层事件和任务系统可复用。
3. NPC 离屏推进有一定基础。

但关键缺口很大：

1. 阵营经济循环没有现成闭环。
2. 没有真正成熟的据点经营内核。
3. 没有 overmap 级人类势力战争系统。
4. 没有战略 AI、补给线、占点、损耗恢复、扩张决策。

一句话：

`A 路可做，但不是靠 mod 拼出来，而是要新增战略层 C++ 子系统。`

### 5.3 “改造成 C#”现实吗

如果意思是“全量重写成 C#”，不现实，不推荐作为主计划。

更现实的只有一条：

`保留 BN 的 C++ 核心，把 C# 用在外层工具、编辑器、AI 内容管线、验证工具、自动化和桥接层。`

理由：

1. `BN` 的 JSON 不是简单配置文件，而是和 C++ 运行时深绑定的协议。
2. 地图、存档、NPC、任务、载具、活动系统都高度耦合。
3. 全量 C# 化本质上就是重写一个大游戏。

结论：

- `C# 全量迁移`：不推荐
- `C# 工具层 / 桥接层`：可以考虑

---

## 6. BN 有无图形界面

有，而且不止一种形态。

根据源码与文档：

1. `BN` 使用 `ncurses` 作为基础 UI。
2. 同时有 `tiles` 构建形态，使用 `SDL` 与图块渲染。
3. 文档明确提到：
   - `cataclysm-bn-tiles`
   - `dist-tiles`
   - `dist-curses`
   - tiles 失败时自动回退 ASCII

可以这样理解：

- `有无图形界面？`
  - 有，存在 `tiles + SDL` 图形界面。
- `能不能先不要图形界面？`
  - 也可以，存在 curses/ASCII 路线。

所以对你来说，`BN` 在界面层是友好的：

1. 可以先走 `无图形 / 轻图形 / ASCII` 原型。
2. 以后再走 tiles 界面。
3. 不需要一开始就投入美术和完整 UI 重做。

---

## 7. 当前适合作为你项目底座吗

### 7.1 适合的地方

如果你的目标是：

1. 先做 `2D / 无图形优先`
2. 强调 `数据驱动`
3. 让 AI 大量参与内容生产
4. 希望先在现成宿主上验证玩法

那 `BN` 是值得继续看的。

### 7.2 不适合幻想的地方

如果你以为：

1. 它已经内建成熟势力经营
2. 它已经有成熟 NPC 社会模拟
3. 它只要装几个 mod 就能变成你的游戏

那这会高估现状。

### 7.3 最稳妥的判断

`BN` 更适合先做：动态剧情、事件系统、规则型 AI/NPC、基地建设扩展。`

`BN` 不适合一上来就把“势力经营/势力战争”当成低成本功能。`

---

## 8. 当前建议

如果继续沿着 `BN` 往下走，推荐顺序是：

1. 先验证 `动态剧情 + AI NPC + 数据驱动事件`
2. 再验证 `基地建设 / 据点循环`
3. 最后再决定要不要投入 `势力经营 / 势力战争` 的战略层改造

不建议的顺序是：

1. 先把 `BN` 整体改造成 `C#`
2. 或者先假设社区里已经有可直接复用的成熟 `势力经营` mod

---

## 9. 参考来源

### 本地文件

- [README.md](./external/Cataclysm-BN/README.md)
- [factions.md](./external/Cataclysm-BN/docs/en/mod/json/reference/creatures/factions.md)
- [missions_json.md](./external/Cataclysm-BN/docs/en/mod/json/reference/creatures/missions_json.md)
- [npcs.md](./external/Cataclysm-BN/docs/en/mod/json/reference/creatures/npcs.md)
- [modding.md](./external/Cataclysm-BN/docs/en/mod/json/tutorial/modding.md)
- [lua modding tutorial](./external/Cataclysm-BN/docs/en/mod/lua/tutorial/modding.md)
- [lua_integration.md](./external/Cataclysm-BN/docs/en/mod/lua/explanation/lua_integration.md)
- [user_interface.md](./external/Cataclysm-BN/docs/en/dev/explanation/user_interface.md)
- [mission.h](./external/Cataclysm-BN/src/mission.h)
- [mission.cpp](./external/Cataclysm-BN/src/mission.cpp)
- [npc.h](./external/Cataclysm-BN/src/npc.h)
- [npctalk.cpp](./external/Cataclysm-BN/src/npctalk.cpp)
- [catalua_hooks.cpp](./external/Cataclysm-BN/src/catalua_hooks.cpp)
- [faction.h](./external/Cataclysm-BN/src/faction.h)
- [overmap.cpp](./external/Cataclysm-BN/src/overmap.cpp)

### 外部链接

- BN 官方文档：https://docs.cataclysmbn.org/
- BN Mod Registry：https://mods.cataclysmbn.org/
- Sky Island：https://mods.cataclysmbn.org/mods/cbn_sky_island/
- Sky Island GitHub：https://github.com/TGWeaver/CDDA-Sky-Islands
- BL9 GitHub：https://github.com/Kenan2000/BL9

