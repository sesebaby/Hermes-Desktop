---
name: pokemon-player
description: Play Pokemon games autonomously via headless emulation. Starts a game server, reads structured game state from RAM, makes strategic decisions, and sends button inputs — all from the terminal.
tags: [gaming, pokemon, emulator, pyboy, gameplay, gameboy]
---
# 宝可梦玩家

使用 `pokemon-agent` 包通过无头模拟来玩宝可梦游戏。

## 何时使用
- 用户说“play pokemon”、“start pokemon”、“pokemon game”
- 用户询问 Pokemon Red、Blue、Yellow、FireRed 等
- 用户想看 AI 玩宝可梦
- 用户提到 ROM 文件（.gb、.gbc、.gba）

## 启动流程

### 1. 首次设置（克隆、venv、安装）
仓库是 GitHub 上的 NousResearch/pokemon-agent。先克隆它，然后
用 Python 3.10+ 虚拟环境完成初始化。优先使用 uv
创建 venv，并以可编辑模式安装包，同时带上
pyboy extra；如果没有 uv，再退回到 python3 -m venv + pip。

这台机器上它已经配置好了，位于 /home/teknium/pokemon-agent，
并且 venv 已就绪——直接进入该目录并执行 source .venv/bin/activate。

你还需要一个 ROM 文件。向用户索要他们自己的 ROM。此机器上
该目录里已经有一个：roms/pokemon_red.gb。
绝不要下载或提供 ROM 文件——始终向用户索要。

### 2. 启动游戏服务
在 pokemon-agent 目录内并激活 venv 后，运行
pokemon-agent serve，并用 --rom 指向 ROM，用 --port 9876。
把它放到后台运行，使用 &。
如需从存档继续，额外添加 --load-state 并传入存档名。
等待 4 秒让它启动，然后通过 GET /health 验证。

### 3. 给用户搭好实时仪表盘
使用 localhost.run 的 SSH 反向隧道，让用户能在浏览器里查看
仪表盘。通过 ssh 连接，将本地 9876 端口转发到远端 80 端口，
目标为 nokey@localhost.run。把输出重定向到日志文件，
等待 10 秒，然后在日志里 grep 出 .lhr.life
URL。把带上 /dashboard/ 的完整地址发给用户。
每次重启后隧道 URL 都会变化——重启后要把新的地址发给用户。

## 存档与读档

### 何时存档
- 每 15-20 回合存一次
- 训练馆战斗、劲敌遭遇或高风险战斗前**必须**存档
- 进入新城镇或地牢前存档
- 在任何你不确定的动作之前先存档

### 如何存档
使用 POST /save，并给它一个描述性名称。好例子：
before_brock、route1_start、mt_moon_entrance、got_cut

### 如何读档
使用 POST /load，并传入存档名。

### 列出可用存档
GET /saves 会返回所有已保存的状态。

### 服务启动时自动读档
启动服务时使用 --load-state 标志自动加载存档。
这比启动后再通过 API 读档更快。

## 游戏循环

### 第 1 步：观察——检查状态并截图
GET /state 获取位置、HP、战斗和对话状态。
GET /screenshot 并保存到 /tmp/pokemon.png，然后用 vision_analyze。
两者都要做——RAM 状态给出数值，视觉给出空间感知。

### 第 2 步：定向
- 屏幕上有对话/文字 → 继续推进
- 正在战斗 → 打或跑
- 队伍受伤 → 去 Pokemon Center
- 接近目标 → 谨慎导航

### 第 3 步：决策
优先级：对话 > 战斗 > 治疗 > 剧情目标 > 训练 > 探索

### 第 4 步：行动——每次最多移动 2-4 步，然后重新检查
使用 POST /action，动作列表要短（2-4 个动作，不是 10-15 个）。

### 第 5 步：验证——每次移动序列后都截图
拍一张截图并用 vision_analyze 确认你是否真的移动到了
想去的地方。这是最重要的一步。没有视觉你一定会迷路。

### 第 6 步：用 PKM: 前缀把进度写入 memory

### 第 7 步：定期存档

## 操作参考
- press_a — 确认、对话、选择
- press_b — 取消、关闭菜单
- press_start — 打开游戏菜单
- walk_up/down/left/right — 移动一格
- hold_b_N — 按住 B N 帧（用于加速文字显示）
- wait_60 — 等待约 1 秒（60 帧）
- a_until_dialog_end — 反复按 A，直到对话结束

## 经验中的关键提示

### 始终使用视觉
- 每移动 2-4 步就截图一次
- RAM 状态只告诉你位置和 HP，但**不知道**周围有什么
- 台阶、栅栏、路牌、建筑门、NPC——只有截图能看到
- 向视觉模型提具体问题：“我正北边一格是什么？”
- 卡住时，先截图再尝试随机方向

### 穿门/楼梯传送需要额外等待
走过门或楼梯时，地图切换期间屏幕会变黑。你**必须**等待它完成。
在任何门/楼梯传送后都要再追加 2-3 个 wait_60 动作。否则
位置读数会滞后，你会以为自己还在旧地图。

### 出门陷阱
离开建筑时，你会直接出现在门口正前方。
如果你往北走，就会立刻回到屋里。一定要先横移：
先向左或向右走 2 格，再继续朝你的目标方向前进。

### 对话处理
第一世代的文字会逐字慢慢滚动。想加速对话时，
按住 B 120 帧，然后按 A。按需要重复。按住 B 会把
文字显示速度拉到最大。之后再按 A 推进到下一行。
a_until_dialog_end 动作会检查 RAM 里的对话标志，
但这个标志**不能**覆盖所有文字状态。如果对话看起来卡住，
就改用手动的 hold_b + press_a 模式，并通过截图验证。

### 窄崖是单向的
窄崖（小悬崖边）只能**向下**跳（向南），不能向上爬（向北）。
如果被向北的窄崖挡住，你必须向左或向右移动，找到绕过去的缺口。
用视觉判断缺口在哪个方向，并明确让视觉模型帮你确认。

### 导航策略
- 每次只移动 2-4 步，然后截图检查位置
- 进入新区域时立刻截图，先定向
- 问视觉模型“怎么去 [目的地]？”
- 如果连续卡住 3 次以上，截图并彻底重新评估
- 不要一口气狂按 10-15 次移动——你会冲过头或卡住

### 野外战斗逃跑
在战斗菜单里，RUN 在右下角。从默认光标位置（FIGHT，左上）
出发：先按下再按右，把光标移动到 RUN，然后按 A。
用 hold_b 包起来加速文字和动画。

### 战斗（FIGHT）
在战斗菜单里 FIGHT 在左上角（默认光标位置）。
按 A 进入招式选择，再按 A 使用第一招。
然后按住 B 加速攻击动画和文字。

## 战斗策略

### 决策树
1. 想抓？→ 先削弱，再丢 Poke Ball
2. 遇到不需要的野怪？→ RUN
3. 有属性克制？→ 用效果拔群的招式
4. 没有克制？→ 用最强的 STAB 招式
5. HP 低？→ 换人或使用 Potion

### 第一世代属性克制表（关键克制）
- 水克火、地面、岩石
- 火克草、虫、冰
- 草克水、地面、岩石
- 电克水、飞行
- 地面克火、电、岩石、毒
- 超能力克格斗、毒（第一世代非常强势！）

### 第一世代特殊机制
- 特攻数值同时影响特殊招式的攻击和防御
- 超能力系过强（幽灵招式有 bug）
- 暴击率与速度有关
- Wrap/Bind 会让对手无法行动
- Focus Energy bug：会**降低**暴击率，而不是提高

## 记忆约定
| 前缀 | 用途 | 示例 |
|--------|---------|---------|
| PKM:OBJECTIVE | 当前目标 | 从 Viridian Mart 取 Parcel |
| PKM:MAP | 导航知识 | Viridian：商店在东北方向 |
| PKM:STRATEGY | 战斗/队伍计划 | 打 Misty 前需要草系 |
| PKM:PROGRESS | 里程碑跟踪 | 打败劲敌，正在前往 Viridian |
| PKM:STUCK | 卡住的情况 | y=28 的窄崖，向右绕过去 |
| PKM:TEAM | 队伍备注 | Squirtle Lv6，Tackle + Tail Whip |

## 进度里程碑
- 选择初始宝可梦
- 从 Viridian Mart 交付 Parcel，获得 Pokedex
- Boulder Badge——Brock（岩石）→ 用水/草
- Cascade Badge——Misty（水）→ 用草/电
- Thunder Badge——Lt. Surge（电）→ 用地面
- Rainbow Badge——Erika（草）→ 用火/冰/飞行
- Soul Badge——Koga（毒）→ 用地面/超能力
- Marsh Badge——Sabrina（超能力）→ 最难的道馆
- Volcano Badge——Blaine（火）→ 用水/地面
- Earth Badge——Giovanni（地面）→ 用水/草/冰
- Elite Four → 冠军！

## 停止游玩
1. 通过 POST /save 用一个有描述性的名字保存游戏
2. 用 PKM:PROGRESS 更新 memory
3. 告诉用户：“游戏已保存为 [name]！说‘play pokemon’即可继续。”
4. 结束服务和隧道的后台进程

## 常见坑
- 绝不要下载或提供 ROM 文件
- 连续 4-5 次动作后就要检查视觉，不要更多
- 出建筑后要先横移，再向北走
- 穿门/楼梯后总是要追加 wait_60 x2-3
- 仅靠 RAM 判断对话不可靠——必须用截图验证
- 高风险遭遇前先存档
- 每次重启后，隧道 URL 都会变化
