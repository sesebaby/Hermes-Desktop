# 项目级 OpenSpec 约束

## 项目定位
本方案目标是在 Hermes Desktop 现有能力基础上，接入 Stardew Valley（星露谷物语），实现多 NPC 村庄模式。

项目目标来自现有深度访谈规格：
- 改造 Hermes Desktop 项目，接入星露谷物语
- 实现多 NPC 村庄模式
- 每个 NPC 拥有独立人格、独立记忆、独立自主决策能力
- MVP 先实现 1-3 个 NPC 并发运行

本方案明确参考 `D:\GitHubPro\Hermes-Desktop\external\hermescraft-main` 的核心架构思想，但不盲目复制 Minecraft 特有实现，而是优先复刻其“Agent 自己通过工具理解世界、桥接层只暴露能力接口”的结构。

## Git 远程仓库注意事项

- 本项目 fork 自 `RedWoodOG/Hermes-Desktop`，**严禁直接推送到源仓库**。
- `origin` 指向自己的仓库 `sesebaby/Hermes-Desktop`，所有修改推送到这里。
- `upstream` 指向源仓库 `RedWoodOG/Hermes-Desktop`，**push URL 已设为 `no_push`**，仅用于 fetch 拉取上游更新。
- 日常操作：
  - `git push origin <分支名>` — 推送到自己仓库（安全）
  - `git fetch upstream` — 拉取源仓库最新代码（安全）
  - `git push upstream` — 会直接报错，防止误操作
- 如需向源仓库贡献，通过 GitHub 网页端从 fork 发起 Pull Request，不要直接 push。


## 文档与提案规则

- 全部文档优先使用中文。
- 能用白话的地方必须用白话。
- 任务拆分必须让非代码读者也能看懂：至少写清楚“做什么 / 不做什么 / 产出 / 怎么验收”。
- 提案必须明确契约边界，防止宿主层、Hermes 层、游戏层职责混淆。
- 提案必须优先复用现有 Hermes 和 HermesCraft 的成熟能力，不得为了“看起来更通用”先重写核心。

## 架构硬约束

- 预发布阶段只允许一条实现路径，禁止双轨、兼容分叉、影子实现。
- 游戏侧和桥接层只负责把世界接到 `Hermes` 上，禁止接管、替代或干涉任何 `Hermes` 原生能力。
- 禁止宿主/桥接层代写或维护 `SOUL.md`、`MEMORY.md`、`USER.md`，也禁止维护任何等价的人格摘要、记忆摘要、身份快照、第二 skill lane、第二 MCP/tool lane。
- 宿主只提供事实、事件、工具、确认和执行结果，不替 NPC 决策。
- 任何真实世界写操作都必须走宿主执行器，不能让 Agent 直接改游戏状态。
- Agent 可见 tool schema 必须尽量少：AI 只填完成业务决策所必需的字段；`conversationId`、session、trace、root todo 等宿主能确定的运行时上下文，由宿主在工具绑定或执行边界补入，不能作为可填可不填字段推给模型。
- 每个原版 NPC 必须有独立 `home`、独立会话、独立记忆边界, 必须和桌面agent具备全部同等能力。
- 群聊、私聊、偷听、送礼、交易都必须保持“Agent 自主提意图，宿主负责许可和执行”的边界。
- 第一阶段不做复杂经济系统，不做自定义素材，不做写死剧情编排。
- 本项目的能力应当参D:\GitHubPro\Hermes-Desktop\external\hermes-agent-main, 本项目可以视为是这个参考项目的c#实现并做了基于游戏的改造

### NPC 工具 / 世界 / 导航 / 人格分层原则

- tool / action schema 只定义可执行契约、参数来源和运行时绑定，不承载地点意义、角色偏好或长期行为法则。
- 世界语义写入 skill，例如 `skills/gaming/stardew-world` 解释地点、候选、标签、理由和 endpoint candidate 含义。
- 移动方法和失败恢复写入 navigation skill，例如 `skills/gaming/stardew-navigation` 负责“最新观察 -> 工具调用 -> 状态查询 -> 失败后重新观察或换目标”。
- NPC 偏好写入 persona / `SOUL.md` / `facts.md`；memory 只保存耐久事实和经历，不替代 skill 或 persona。
- runtime / host 只提供事实、事件、工具、执行结果、必要许可、安全门控、路径探测和状态编排，不替 NPC 选择世界内目标、不把观察事实自动转换成行动。
- 涉及 prompt / skill 边界的测试，必须优先走真实仓库资产注入路径；定位 repo 资产时从 `AppContext.BaseDirectory` 向上查找，不能依赖当前工作目录或只用 fixture 文本。

## 任务执行规则

- 小步快跑：一次只完成一个小任务块。
- 一个任务完成后，先检查，再提交，再进入下一个任务。
- 能并行的任务默认并行，但必须先写清楚边界和集成点。
- 以后任何"看起来逻辑对但实际不生效"的 bug，第一步永远是加诊断日志，不要靠读代码推测根因。
- 跨 LLM / Agent / 游戏桥接边界的后台自主任务，必须有可观测日志、明确超时、资源释放和可重试路径；不能让一次模型请求或工具等待无限占住 worker、LLM slot、action slot 或事件推进。
- 排查 NPC 没有动作时，先用日志分清“Agent 没发工具命令”和“bridge 收到命令但执行失败”；前者查 LLM turn / prompt / tool-call，后者才查 SMAPI 路径、碰撞和 UI 线程。
- 模型可见工具契约不能只靠 prompt 约束；凡是“必须来自最新观察 / 候选 / 权限”的参数，都要在工具边界做硬校验，失败时返回可观察的 blocked/invalid_target，而不是把编造参数继续下发到游戏桥接层。
- openspec\其他项目errors中保存了之前失败项目的历史错误信息,无论是实施功能还是解决问题,都应该先检索对应问题是否出现过.
- 遇到问题先看external\hermescraft中方案, 尽可能的对齐
