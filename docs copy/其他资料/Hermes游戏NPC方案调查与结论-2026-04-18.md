# Hermes 游戏 NPC 方案调查与结论

日期：2026-04-18

## 1. 文档目的

这份文档用于固化当前对 `Hermes Agent` 作为游戏 NPC 内核的调查结论，避免后续讨论反复丢失上下文。

重点记录：

- 用户当前真实需求
- 已确认的 Hermes 源码事实
- 对参考图片中“作者架构”的判断
- Hermes 社区生态里值得参考的项目与方向
- 当前推荐路线
- 已发现的问题、风险和待验证项

---

## 2. 用户当前真实需求

用户的终极目标不是“把 Hermes 跑起来”本身，而是把它作为未来游戏 NPC 核心候选，接入类似星露谷的游戏场景，重点验证以下能力：

- 玩家与 NPC 私聊
- 多 NPC 群聊
- 交易
- 送礼
- 好感度变化
- 自主行动
- 社会模拟
- 群体关系和传闻传播
- 主动来找玩家
- 记忆连续性
- 活人感、主动感、非脚本感

用户特别强调：

- 测试重点是“效果”，不是只看能不能接通
- 但同时也必须知道“效果背后是怎么实现的”，以便评估 Hermes 是否适合作为长期内核
- 当前阶段尽量使用 Hermes 原生能力，不优先改造 Hermes 核心
- 文档、说明、后续沟通都使用中文

---

## 3. 已确认的 Hermes 源码事实

以下内容已通过本地仓库源码核对，不是主观猜测。

### 3.1 群聊能力存在，但核心在 gateway/session

Hermes 的群聊不是普通 CLI 对话，而是主要通过 `gateway` 的 session 机制实现。

关键文件：

- [session.py](D:\GitHubPro\AllGameInAI2\hermes-agent\gateway\session.py)
- [run.py](D:\GitHubPro\AllGameInAI2\hermes-agent\gateway\run.py)
- [config.py](D:\GitHubPro\AllGameInAI2\hermes-agent\gateway\config.py)

已确认点：

- `build_session_key()` 决定群聊是“按用户拆 session”还是“共享线程 session”
- 带 `thread_id` 且配置允许时，可以形成共享 thread 会话
- 共享线程会注入 `Multi-user thread` 语境提示
- 在共享线程里，用户消息会被改写成类似 `[user_name] message`

这说明：

- Hermes 有“多人共享对话空间”的原生基础
- 但“轮流发言”“谁该说话”“抢话怎么办”这类社交调度问题，并不是 Hermes 自动完整解决的

### 3.2 Hermes 的“记忆”分两层

#### 第一层：内建 `MemoryStore`

关键文件：

- [memory_tool.py](D:\GitHubPro\AllGameInAI2\hermes-agent\tools\memory_tool.py)
- [run_agent.py](D:\GitHubPro\AllGameInAI2\hermes-agent\run_agent.py)

已确认点：

- `get_memory_dir()` 返回 `get_hermes_home() / "memories"`
- `load_from_disk()` 直接读取 `MEMORY.md` 和 `USER.md`
- `memory_enabled` 打开的，是这套内建长期记忆能力
- 这些记忆会在 agent 初始化时加载，并注入 system prompt

结论：

- `memory_enabled` 只表示“启用当前 Hermes home 下的内建长期记忆”
- 它**不会自动按 NPC 分份**
- 如果多个 NPC 共用同一个 `HERMES_HOME`，这层默认不是天然隔离的

#### 第二层：外部 `Memory Provider`

关键文件：

- [run_agent.py](D:\GitHubPro\AllGameInAI2\hermes-agent\run_agent.py)
- [memory_manager.py](D:\GitHubPro\AllGameInAI2\hermes-agent\agent\memory_manager.py)

已确认点：

- provider 初始化时会收到这些 scope 信息：
  - `session_id`
  - `session_title`
  - `user_id`
  - `gateway_session_key`
  - `agent_identity`
  - `agent_workspace`
- 这些 scope 由不同 provider 自己决定如何使用

已确认的 provider 行为特征：

- `mem0`：明显按 `user_id` / `agent_id` 隔离
- `retaindb`：按 `user_id` / `session_id` / `agent_id` 隔离
- `honcho`：可利用 `session_title` / `gateway_session_key` / `user_id` / `identity`
- `supermemory`：更偏 `session_id` + `agent_identity`

结论：

- Hermes 真正细粒度的记忆隔离能力，主要在 provider 这一层
- 如果想做“每个 NPC 独立长期记忆”，不能只依赖内建 `memory_enabled`
- 更现实的路径是 `profile`、`provider scope`、或两者结合

### 3.3 `memory_enabled` 不是“按 NPC 一套”的自动开关

关键文件：

- [run_agent.py](D:\GitHubPro\AllGameInAI2\hermes-agent\run_agent.py)
- [hermes_cli/config.py](D:\GitHubPro\AllGameInAI2\hermes-agent\hermes_cli\config.py)
- [hermes_constants.py](D:\GitHubPro\AllGameInAI2\hermes-agent\hermes_constants.py)
- [AGENTS.md](D:\GitHubPro\AllGameInAI2\hermes-agent\AGENTS.md)

已确认点：

- `memory_enabled` 只是配置项，默认开启
- 内建记忆目录是 profile-scoped，不是 NPC-scoped
- Hermes 的 `profile` 才是官方定义的多实例隔离边界
- 每个 profile 有独立 `HERMES_HOME`、config、memory、sessions 等

结论：

- 如果有人说“Hermes 自带 memory_enabled，按 NPC 一套”
- 那么更可能的真实实现不是 `memory_enabled` 自动完成
- 而是“每个 NPC 一个 profile”，或者“每个 NPC 一个 provider identity / peer”

### 3.4 子 agent / 委托 agent 的记忆隔离

关键文件：

- [delegate_tool.py](D:\GitHubPro\AllGameInAI2\hermes-agent\tools\delegate_tool.py)
- [SECURITY.md](D:\GitHubPro\AllGameInAI2\hermes-agent\SECURITY.md)
- [memory_provider.py](D:\GitHubPro\AllGameInAI2\hermes-agent\agent\memory_provider.py)

已确认点：

- `delegate_task` 创建 child agent 时显式传入 `skip_memory=True`
- 官方安全文档明确写：subagents 不访问父 agent 的持久记忆 provider
- 子 agent 完成后，父 agent 只接收“委托任务 + 最终结果”的观察

结论：

- `delegate_task` 的子 agent / delegate agent 是“记忆隔离”的
- 它们更像临时外包工，不是长期人格容器
- 适合做“复杂时刻的临时分析”，不适合直接承载 NPC 本体

### 3.5 `review_agent` 和 `delegate_task` 的 child agent 不是一回事

关键文件：

- [run_agent.py](D:\GitHubPro\AllGameInAI2\hermes-agent\run_agent.py)

已确认点：

- Hermes 内部有 `review_agent`
- `review_agent` 会直接复用父 agent 的 `_memory_store` 和 `_memory_enabled`

结论：

- `delegate_task` 的 child agent：隔离
- 内部 `review_agent`：不隔离

这两者不能混为一谈

### 3.6 Hermes 更偏“少量常驻进程 + 多 session”，不是“一 NPC 一系统进程”

关键文件：

- [gateway/run.py](D:\GitHubPro\AllGameInAI2\hermes-agent\gateway\run.py)
- [run_agent.py](D:\GitHubPro\AllGameInAI2\hermes-agent\run_agent.py)

已确认点：

- gateway 内部有 `_agent_cache`
- 存在“按 session 复用 / 缓存 agent”的路径
- 官方和社区方向都更接近“一个 gateway 管很多 session”

结论：

- 把 Hermes 用成“一 NPC 一个系统进程”不是主流设计方向
- 真要做很多 NPC，更合理的是“少量常驻进程 + 多 session / 多 profile / 多状态对象”

---

## 4. 对参考图片架构的判断

参考图片：

- [image.png](D:\GitHubPro\AllGameInAI2\图片\image.png)

图片中的表述包括：

- 真送礼 / 加心
- 酒馆群聊
- cron 主动过来
- 跨 NPC 记忆
- delegate 链
- 情绪记忆
- 查天气 / 订外卖 / 新闻

### 4.1 这张图的真实结构不是“全靠 Hermes 自动完成”

更准确的分层应是：

#### 第一层：游戏端 / Mod / MCP

负责真实动作：

- `give_item`
- `modify_friendship`
- `teleport_npc_to_player`

这些不是 Hermes 原生自带的游戏能力，而是游戏宿主或 MCP bridge 提供的工具。

#### 第二层：Bridge / Orchestrator

负责场景编排：

- 酒馆群聊：同一 location 激活多个 session，控制轮次
- cron 主动来找玩家：定时评估 -> 触发动作 -> 自动发起对话
- 跨 NPC 记忆：用共享层传播“谁提到了谁”“谁知道什么”

这层是整张图里最关键的作者自建部分。

#### 第三层：Hermes 内核

负责：

- 人格 / system prompt
- 工具调用
- delegation
- 长期记忆能力
- web / MCP 使用

### 4.2 对图片各行的判断

#### 真送礼 / 加心

判断：`半原生`

- 工具调用这件事 Hermes 原生支持
- 但 `give_item` / `modify_friendship` 必须由游戏侧实现

#### 酒馆群聊

判断：`作者自建为主`

- Hermes 有 session / shared thread / group 会话基础
- 但“同一 location 激活三个 session 轮流出牌”更像 Bridge orchestrator

#### cron 主动过来

判断：`半原生偏自建`

- Hermes 自带 cron / 定时任务能力
- 但“评估 -> 命中 -> 调 mod -> 自动开对话”这一整段是桥接层逻辑

#### 跨 NPC 记忆

判断：`作者自建为主`

- Hermes 内建记忆默认不是“跨 NPC 传播系统”
- 图片描述更像作者显式做了一层共享记忆文件或公共记忆池

#### delegate 链

判断：`Hermes 原生`

- `delegate_tool` 是 Hermes 原生功能
- 但它更适合临时外包分析，不适合长期 NPC 人格驻留

#### 情绪记忆

判断：`原生能力 + 额外隔离手段`

- `memory_enabled` 确实是 Hermes 原生能力
- 但“按 NPC 一套”大概率不是 `memory_enabled` 自动做到
- 更可能依赖 `profile` 或 `provider identity`

#### 查天气 / 订外卖 / 新闻

判断：`Hermes 原生工具能力`

- Hermes 本身具备 web / MCP / 外部工具接入能力
- 这类外部信息查询属于它的原生强项

---

## 5. 外部调查结果摘要

本轮通过多 agent 并行调研 Hermes 官方文档、GitHub issue/PR、社区项目、外围生态，结论如下。

### 5.1 最有价值的不是某一个 fork，而是 4 类方案的组合

#### A. NPC 隔离层

推荐优先级最高：

- Hermes `profiles`
- `Honcho`

原因：

- 最接近“每个 NPC 独立人格 + 独立长期记忆”
- 同时还有机会保留共享 workspace / 公共记忆层

重点参考：

- [Hermes Agent v0.6.0: Profiles](https://github.com/NousResearch/hermes-agent/releases/tag/v2026.3.30)
- [PR #3681: add profiles](https://github.com/NousResearch/hermes-agent/pull/3681)
- [Honcho 文档](https://hermes-agent.nousresearch.com/docs/user-guide/features/honcho/)
- [PR #4616: Honcho profile scope](https://github.com/NousResearch/hermes-agent/pull/4616)
- [PR #4355: Honcho plugin parity + observationMode](https://github.com/NousResearch/hermes-agent/pull/4355)

#### B. 长期记忆层

高优先级候选：

- [agentmemory](https://github.com/rohitg00/agentmemory)
- [Hindsight](https://github.com/vectorize-io/hindsight)

原因：

- 比普通聊天日志更接近游戏所需的“礼物偏好 / 交易信誉 / 好感度变化 / 经历影响性格”

#### C. 镇级编排层

高优先级候选：

- [Mission Control](https://github.com/builderz-labs/mission-control)
- [hermes-plugins](https://github.com/42-evey/hermes-plugins)

原因：

- 这类项目更像“镇子 orchestrator”
- 适合定时任务、目标系统、事件总线、跨 agent 通信、日总结

#### D. 假宿主 / 测试桥接层

高优先级候选：

- [Hermes MCP 指南](https://hermes-agent.nousresearch.com/docs/guides/use-mcp-with-hermes/)
- [fastapi_mcp](https://github.com/tadata-org/fastapi_mcp)
- [MCP-Game](https://github.com/tadata-org/MCP-Game)
- [mcporter](https://github.com/steipete/mcporter)

原因：

- 最适合快速做“假星露谷服务器 + Hermes 控制台手测”

### 5.2 高参考价值，但不适合直接整套照搬

#### [HermesCraft](https://github.com/bigph00t/hermescraft)

优点：

- 最像“活 NPC”
- 有独立角色、公开聊天、私聊、偷听等机制

限制：

- 载体是 Minecraft，不是现成星露谷桥接

##### HermesCraft 专项源码分析

这部分已经结合本地克隆代码确认，不是只看 README。

关键文件：

- [civilization.sh](D:\GitHubPro\AllGameInAI2\external\hermescraft\civilization.sh)
- [hermescraft.sh](D:\GitHubPro\AllGameInAI2\external\hermescraft\hermescraft.sh)
- [server.js](D:\GitHubPro\AllGameInAI2\external\hermescraft\bot\server.js)
- [chat.js](D:\GitHubPro\AllGameInAI2\external\hermescraft\bot\lib\chat.js)

已确认点：

- 它不是重写了一套 Hermes runtime
- 它本质上是 `Hermes Agent -> mc CLI -> bot/server.js HTTP API -> Minecraft bot body`
- 单角色模式由 `hermescraft.sh` 启动
- 多角色模式由 `civilization.sh` 批量启动

它解决“角色隔离”的方式非常直接：

- 每个角色一个独立 `HERMES_HOME`
- 每个角色一个独立 `memories` 和 `sessions`
- 每个角色一个独立 `SOUL.md`
- 每个角色绑定一个独立 bot API 端口
- 每个角色各自跑一个 Hermes 进程

也就是说，它并不是“一个总 agent 在很多角色之间切换”。

它解决“群聊”的方式也不是直接依赖 Hermes 原生 gateway 群聊，而是桥接层自己做消息路由：

- `all: 消息` 视为广播
- `Name: 消息` 视为私聊
- `Name1,Name2: 消息` 视为小群聊

服务器端收到聊天后，会把消息分流成三类：

- 发给我的：进入 `chatLog`
- 明确叫我执行的：进入 `commandQueue`
- 不是发给我的：进入 `overheardLog`

这说明它的“社会感”来源于两层组合：

- Hermes 负责单角色的思考、记忆、工具调用
- bridge 负责谁听到什么、谁被点名、谁偷听到什么

对本项目的启发：

- `HermesCraft` 非常适合借鉴“单角色像活人”和“多角色独立记忆”的做法
- 但它采用的是“一角色一常驻 agent”路线
- 这对 Minecraft 小规模角色成立
- 对未来星露谷全镇 NPC，则更适合作为原型验证方案，而不是直接照搬成最终量产架构

#### [Gladiator](https://github.com/runtimenoteslabs/gladiator)

优点：

- 适合参考多组织、多 agent、自主博弈

限制：

- 更偏组织模拟，不是村民日常模拟

#### [Ankh.md](https://github.com/Abruptive/Ankh.md)

优点：

- 适合参考“一人格一容器”

限制：

- 不适合当前直接作为游戏 runtime

#### [Hermes-Desktop](https://github.com/RedWoodOG/Hermes-Desktop)

优点：

- 适合参考多 persona 与人工试聊体验

限制：

- 偏桌面伴侣，不是游戏 NPC 运行时

### 5.3 对群聊和多 NPC 社交模拟的调查结论

没有找到一个成熟的 Hermes 官方或高信号社区项目，已经完整实现了：

- 多 NPC 轮流发言
- 抢话仲裁
- 发言权公平分配
- 社交模拟调度器

目前 Hermes 更成熟的是：

- 会话隔离
- shared thread
- thread/topic 作为边界
- 新消息中断 / 合并

所以：

- Hermes 已有“群聊基础设施”
- 但没有“现成 NPC 社交导演”
- 真正的 tavern / 村庄群聊编排仍然需要上层 Bridge

### 5.4 对 delegation 的调查结论

外部资料与源码结论一致：

- Hermes 原生 `delegate_task` 适合临时外包分析
- 不适合直接充当长期 NPC 人格容器
- 适合作为叶子型 worker

结论：

- 应把它当“NPC 临时顾问脑”
- 不应把它当“长期 NPC 本体”

---

## 6. 当前推荐路线

这是基于源码事实、用户目标、外部生态三方面综合后的当前推荐。

### 6.1 顶层原则

不要把 Hermes 当作“游戏总控器”。

更适合的结构是：

- 游戏宿主 / Mod / MCP：提供真实世界动作和状态
- Bridge / Orchestrator：做场景编排、激活、轮次、记忆路由
- Hermes：作为 NPC 智能内核

### 6.2 推荐技术路线

#### 路线 1：核心 NPC 私有层

推荐：

- `Hermes profile` 或 `Honcho peer/identity`

用途：

- 固定人格
- 私有长期记忆
- 私有 session

#### 路线 2：共享公共记忆层

推荐：

- `Honcho workspace`
- 或单独的共享记忆池
- 或 `agentmemory` / `Hindsight` 的公共知识层

用途：

- 传闻传播
- 世界公共状态
- “Abigail 提到 Seb”的跨 NPC 信息流

#### 路线 3：手测宿主

推荐：

- `FastAPI + MCP`

用途：

- 提供假游戏世界状态
- 提供礼物、交易、关系变化、移动、时间推进、天气等工具
- 用于人工手测 Hermes 行为

#### 路线 4：群聊与主动行为编排

推荐：

- 单独 Bridge / orchestrator

职责：

- 判断哪些 NPC 当前激活
- 判断谁该说话
- 判断何时主动找玩家
- 判断哪些信息写入私有记忆，哪些写入共享记忆

#### 路线 5：delegate 只做临时分析

推荐：

- Hermes 原生 `delegate_task`

用途：

- 复杂礼物分析
- 群聊情绪评估
- 交易方案分析
- 冲突风险评估

不建议用途：

- 长期人格驻留
- 长期记忆容器

---

## 7. 明确不推荐的路线

### 7.1 不推荐“一 NPC 一系统进程”作为常态

原因：

- 资源开销大
- 并发管理复杂
- 不符合 Hermes 主流扩展方向

更推荐：

- 少量常驻进程
- 大量 session / profile / 休眠状态
- 只有活跃 NPC 进入热运行态

### 7.2 不推荐把 `memory_enabled` 误认为“天然按 NPC 隔离”

原因：

- 源码不支持这个结论
- 这样设计会导致后期严重串味风险

### 7.3 不推荐把 Hermes 原生群聊能力误认为“现成酒馆社交模拟器”

原因：

- Hermes 解决的是会话边界，不是完整社交调度
- 仍需 Bridge 负责谁说话、何时说、如何插话

### 7.4 不推荐把原生 delegation 当长期专家 NPC 体系

原因：

- 它擅长短任务外包
- 不擅长长期人格和长期工作区边界

---

## 8. 已发现的问题与风险

### 8.1 profile 隔离并不等于绝对强隔离

外部调查发现：

- 官方已有 profile 路线
- 但社区也指出 profile isolation 仍有补洞空间

风险：

- 目录隔离不等于文件访问级别完全隔离
- 插件和外部 provider 仍可能存在跨 profile 配置读取问题

### 8.2 内建记忆与多 NPC 设计天然冲突

风险：

- 如果多个 NPC 共用一个 Hermes home
- 内建 `MEMORY.md` 容易造成串味

### 8.3 delegation 的状态隔离仍不够“游戏级严格”

风险：

- 虽然记忆隔离做得不错
- 但工具、工作区、环境、并发隔离还不是完整游戏 runtime 级别

### 8.4 现成社区项目多数偏“陪伴 / 桌面 / Bot”

风险：

- 能直接迁到星露谷村民社会模拟的项目很少
- 大部分只能借思路，不能直接复用

---

## 9. 当前最值得继续推进的验证方向

### 9.1 第一优先级

验证 `Hermes profile + Honcho` 是否能稳定承载：

- 每 NPC 独立人格
- 每 NPC 独立长期记忆
- 可选共享公共记忆

### 9.2 第二优先级

搭建 `FastAPI + MCP` 假宿主，先手测以下效果：

- 私聊
- 送礼
- 交易
- 好感变化
- 主动接近玩家
- 简单群聊

### 9.3 第三优先级

设计 Bridge 编排规则，先不改 Hermes 核心：

- 激活规则
- 群聊轮次
- 公共记忆写入规则
- 私有记忆写入规则

### 9.4 第四优先级

验证 delegate 在游戏里的最佳使用边界：

- 哪些任务适合外包
- 哪些任务必须由主 NPC 直接判断

---

## 10. 当前一句话结论

`Hermes` 很适合做游戏 NPC 的“智能内核”，但它不是完整的“游戏 NPC 社会模拟框架”。

要想落到用户目标，最现实的路线不是直接改 Hermes 核心，而是：

- 用 `profile / provider` 解决 NPC 独立性
- 用 `memory provider` 解决长期记忆
- 用 `FastAPI + MCP` 做假宿主手测
- 用 `Bridge / orchestrator` 解决群聊、主动行为和跨 NPC 传播

后续继续推进时，应以这份文档为基线增量更新，而不是重新从零讨论。
