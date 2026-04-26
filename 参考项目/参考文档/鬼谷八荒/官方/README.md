# 鬼谷八荒官方资料摘要

更新时间：2026-03-26

## 1. 创意工坊总入口

- 原始页：`steam-workshop-about.html`
- 来源：https://steamcommunity.com/workshop/about/?appid=1468810

关键信息：

- 游戏主界面有 `Mod` 按钮，可创建模组并分享到 Steam 创意工坊。
- 创意工坊条目是 `Ready-To-Use Items`，订阅后下次启动即可使用。

对当前任务的价值：

- 这是官方确认的模组入口与分发方式。
- 可以作为我们后续参考“模组结构、分发、启停方式”的基础资料。

## 2. 官方示例：灵田秘境

- 原始页：`official-sample-farm-realm.html`
- 来源：https://steamcommunity.com/sharedfiles/filedetails/?id=2814885383&l=schinese

官方描述摘录要点：

- 标签含 `功能 / 道具 / 开源`
- 官方描述为：城镇和宗门新增灵田建筑，妖兽巢穴掉落灵种，前往城镇或宗门中的灵田建筑种植灵果

对当前任务的价值：

- 证明官方示例不是纯换皮，而是包含新增入口、道具掉落、建筑交互、种植循环的完整玩法。
- 适合重点借鉴：
  - 新增交互入口
  - 掉落到后续玩法的连接
  - 官方示例工程结构

## 3. 官方示例：八九玄功

- 原始页：`official-sample-bajiu-scroll.html`
- 来源：https://steamcommunity.com/sharedfiles/filedetails/?id=2814899983&l=schinese

官方描述摘录要点：

- 标签含 `战斗 / 功能 / 开源`
- 官方描述为：通过妖兽之血或天赐机缘解锁，在战斗中按 `X` 键暂时化形为随机妖兽

对当前任务的价值：

- 证明官方示例可做：
  - 通过条件解锁能力
  - 战斗内按键触发能力
  - UI / 状态 / 随机结果的结合

## 4. 官方公告：创意工坊上线，模组编辑器更新

- 原始页：`announcement-workshop-now-available.html`
- 来源：https://steamcommunity.com/games/1468810/announcements/detail/3316350753639131521

当前能稳定提取到的关键信息：

- 标题：`Workshop is now available; Mod maker updated!`
- 页面元信息显示这是一条 `2022-05-31` 的版本更新公告

对当前任务的价值：

- 可作为“官方从何时开始正式开放创意工坊和模组编辑器”的时间锚点。
- 后续若要整理鬼谷八荒 mod 能力演进，这条公告值得保留。

## 5. 官方公告：MOD Maker 1.0 Beta

- 原始页：`announcement-mod-maker-beta.html`
- 来源：https://steamcommunity.com/games/1468810/announcements/detail/6104075218000724675

当前能稳定提取到的关键信息：

- 标题：`[0.8.5018] MOD Maker(1.0 BETA) Now On!`

对当前任务的价值：

- 可作为模组编辑器 Beta 开放的时间锚点。
- 后续如果要倒查“哪些系统是编辑器原生支持、哪些是社区拓展支持”，这条公告有价值。

## 6. 官方资料对当前方案的直接帮助

- 对话：
  - 官方资料尚未直接给出“对话系统 API 细节”，但官方示例与创意工坊存在证明了可做带 UI 和逻辑的模组。
- 记忆：
  - 官方页未直接给出记忆系统说明，需要继续从社区实现找切入点。
- 创造物品：
  - `灵田秘境` 已经证明官方示例涉及道具掉落、种植、灵果等道具链路，说明“围绕现有物品做玩法”是现实路径。

## 7. 建议的后续阅读顺序

1. `official-sample-farm-realm.html`
2. `official-sample-bajiu-scroll.html`
3. `steam-workshop-about.html`
4. 两个公告页 HTML
