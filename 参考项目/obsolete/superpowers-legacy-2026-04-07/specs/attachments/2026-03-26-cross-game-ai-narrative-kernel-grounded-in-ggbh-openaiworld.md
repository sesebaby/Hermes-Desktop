# 跨宿主 AI 叙事交互通用方案

副标题：以 `GGBH_OpenAIWorld` 的真实实现、版本演进和代码节点为锚点，提炼星露谷 / 环世界可落地、太吾可研究的一套通用内核

> Superseded for reproduction-contract work on 2026-03-26.
>
> The authoritative implementation contract is now:
> - `2026-03-26-openaiworld-ai-reproduction-master-manual.md`
> - `2026-03-26-openaiworld-ai-reproduction-appendix-code-evidence.md`
> - `2026-03-26-openaiworld-ai-reproduction-appendix-source-validation.md`
> - `2026-03-26-openaiworld-ai-reproduction-appendix-unresolved.md`
>
> If this document conflicts with those files, follow the reproduction manual set. The `A级/B级/C级` labels below are a legacy synthesis scheme, not the repo's current source-truth arbitration scheme.
>
> Additional current-architecture note:
> - this attachment is still useful for cross-host synthesis
> - but any wording that sounds like the orchestration owner must remain local should be read through the current framework direction: preserve chain structure, allow hosted orchestration, keep local deterministic execution

## 1. 文档定位

这份文档不做空泛抽象，只回答三件事：

1. `GGBH_OpenAIWorld` 这个成熟 mod 在代码里到底怎么实现
2. 这些实现里，哪些能提炼成跨宿主共享内核
3. 这些共享内核怎么映射到不同宿主

本文的证据分三级：

- `A级`
  - 已确认执行链
- `B级`
  - 已确认 prompt / 协议层，执行入口部分可见
- `C级`
  - 宿主映射假设

## 2. 先确认源宿主样本：鬼谷八荒版 `GGBH_OpenAIWorld`

这里必须先强调：

- `GGBH_OpenAIWorld` 不是“参考对象之一”
- 它本身就是 `已经成熟的源宿主样本`

从 `artifacts/OpenAIWorld@updatelog.json` 能看到明显成长线：

- `4.1.0`
  - AI 对话已有行为反馈，明确提到送物、借物、交易
- `4.10.1`
  - AI 读取近期经历和生涯大事
- `4.12.0`
  - 互留传音符、邀请做客
- `5.0.0`
  - 主动式 AI
- `5.4.0` 到 `5.6.0`
  - 群聊开始支持记录、互动、与私聊打通
- `6.2.0`
  - 多方案模型配置、AI 交易、主动联系
- `6.7.0` 到 `6.8.0`
  - AI 能创建并送出不存在的物品
- `7.2.0`
  - 私聊被重构为“个人记忆”，分类为对话、传音、交易、行为、物品、同意、拒绝
- `8.0.0`
  - 信息裂变和个人记忆系统
- `9.0.0` 到 `9.1.0`
  - 全套 prompt、角色卡模板、角色卡 / 世界演变创建 NPC

因此，真正成熟的不是修仙题材壳，而是：

- AI 路由
- Prompt / Persona / 世界规则包
- 消息记忆和长期经历
- 多通道会话编排
- 结构化行为协议
- 事件流水线

## 3. `GGBH_OpenAIWorld` 在代码里怎么实现

## 3.1 AI 运行与配置层 `A级`

关键代码：

- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/AIAgents.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/AIServer.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/AIServerScheme.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.ailm/LLMSession.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.ailm/LLMMessage.cs`

已确认事实：

- 不是单模型单 prompt
- 不同功能域可以走不同服务器和不同模型
- 会话和消息结构被抽成独立层

可提炼：

- `AI routing protocol`

## 3.2 Prompt 仓、角色卡和世界规则层 `A级/B级`

关键代码：

- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:12717-12740`
  - 解密读取内置 prompt
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:12753-12767`
  - 角色卡拆分公开 / 私密段
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:12770-12789`
  - 扫描角色卡目录
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:12791-12810`
  - 导出 prompt 到本地
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:12816-12824`
  - 只有在设置开关允许时才检查本地覆盖，否则直接走内置 prompt
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:12824-12840`
  - 回退内置模板并注入变量

关键 prompt：

- `decoded_prompts/世界定义.md`
- `decoded_prompts/世界推演.md`
- `decoded_prompts/行为指令.md`
- `decoded_prompts/信息裂变.md`
- `decoded_prompts/交易.md`
- `decoded_prompts/群聊.md`
- `decoded_prompts/传音群聊.md`
- `decoded_prompts/角色卡模板.md`

可提炼：

- `prompt bundle`
- `persona pack`
- `world-rule pack`
- `local override mechanism`

## 3.3 短期消息记忆与长期记忆层 `A级`

消息与经历载体：

- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod.data/MessageData.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod.data/PrivateMessageData.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod.data/GroupMessageData.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod.data/ContactGroupMessageData.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/ExperienceData.cs`

私聊历史回灌：

- `decompiled/GGBH_OpenAIWorld_src/.../g.cs:10557-10575`
  - 消息格式带日期、地点、天气
- `decompiled/GGBH_OpenAIWorld_src/.../g.cs:10717-10807`
  - 历史消息和日志重新拼进 prompt
- `decompiled/GGBH_OpenAIWorld_src/.../g.cs:10896-10910`
  - 回复回写成新的 `PrivateMessageData`

长期记忆存储与疑似回灌候选：

- `decompiled/GGBH_OpenAIWorld_src/.../g.cs:10625-10665`
  - `ExperienceData` 的月度存储读取
- `decompiled/GGBH_OpenAIWorld_src/.../g.cs:10687-10696`
  - 按月滚动压缩
- `decompiled/GGBH_OpenAIWorld_src/.../g.cs:10717-10815`
  - 调用 LLM 做月度长期记忆压缩
- `decoded_prompts/记忆压缩.md`
  - 明确要求保留关键关系、事件、情报、承诺、交易
- `decompiled/GGBH_OpenAIWorld_src/.../l.cs:11261-11265`
  - 可能涉及群聊链中的长期记忆读取候选点，仍需以 reproduction manual 的 `optional reconstruction` 结论为准
- `decompiled/GGBH_OpenAIWorld_src/.../Y.cs:12578-12581`
  - 可能涉及联系人群链中的长期记忆读取候选点，仍需以 reproduction manual 的 `optional reconstruction` 结论为准

补充成熟度证据：

- `OpenAIWorld@updatelog.json`
  - `4.10.1`、`6.3.2`、`6.7.0`、`7.2.0` 已经明确了近期经历、传记、导入导出、个人记忆

可提炼：

- `recent-message memory`
- `long-term bucketed summary memory`
- `memory-compression contract`

## 3.4 多通道会话编排层 `A级`

这不是单纯 prompt 变体，而是成熟的通道拓扑能力。

联系人群 / 社交圈层：

- `decompiled/GGBH_OpenAIWorld_src/.../b.cs:12160-12186`
  - 主动联系、群聊、联系人群配置
- `decompiled/GGBH_OpenAIWorld_src/.../Y.cs:12436-12501`
  - 联系人群请求组装
- `decompiled/GGBH_OpenAIWorld_src/.../Y.cs:12454-12469`
  - 注入联系人群历史
- `decompiled/GGBH_OpenAIWorld_src/.../Y.cs:12600-12645`
  - 按日期和说话人重建联系人群历史
- `decompiled/GGBH_OpenAIWorld_src/.../Y.cs:12681-12691`
  - 联系人群回复回写

群聊排队 / 轮流发言：

- `decompiled/GGBH_OpenAIWorld_src/.../J.cs:10874-10899`
  - 玩家消息先写成 `GroupMessageData`，再启动群聊队列
- `decompiled/GGBH_OpenAIWorld_src/.../J.cs:10905-10935`
  - 出队一个 NPC，逐个生成并回写
- `decompiled/GGBH_OpenAIWorld_src/.../l.cs:11224-11370`
  - 群聊上下文构造、疑似长期记忆读取候选、结果解析和 `GroupMessageData` 回写；长期记忆回灌仍以 reproduction manual 的 `optional reconstruction` 结论为准

远程 / 传音链：

- `decompiled/GGBH_OpenAIWorld_src/.../Y.cs:12999-13020`
  - 远程消息会写入 `PrivateMessageData`

可提炼：

- `channel orchestration protocol`
- `social-circle memory`
- `multi-speaker sequencing`

## 3.5 结构化行为协议与统一动作分发层 `A级/B级`

协议层证据：

- `decoded_prompts/行为指令.md`
  - `Transaction`
  - `Invite`
  - `GiveItem`
  - `LendItem`
  - `AddContact`
- `decoded_prompts/信息裂变.md`
  - `ConveyMessage`
- `decoded_prompts/交易.md`

真正执行链：

- `decompiled/GGBH_OpenAIWorld_src/.../F.cs:13827-13839`
  - 解析响应 JSON，提取 `actions`
- `decompiled/GGBH_OpenAIWorld_src/.../i.cs:11062-11084`
  - 把 `actions[]` 解析成统一动作列表
- `decompiled/GGBH_OpenAIWorld_src/.../i.cs:11087-11352`
  - 按 action 名称统一分发
- `decompiled/GGBH_OpenAIWorld_src/.../i.cs:11376-11410`
  - `ConveyMessage` 等动作会触发新的真实通信链
- `decompiled/GGBH_OpenAIWorld_src/.../l.cs:11345-11370`
  - 群聊链解析并回写结构化结果
- `decompiled/GGBH_OpenAIWorld_src/.../J.cs:10918-10924`
  - 群聊链执行动作分发

这里的正确结论不是“共享最终动作”，而是：

- 共享的是 `结构化叙事意图`
- 最终 authoritative write 仍由宿主 adapter 决定

建议抽象成：

- `Speak`
- `SpeakMulti`
- `RecordMemory`
- `PropagateRumor`
- `ProposeGift`
- `ProposeTrade`
- `ProposeLoan`
- `ProposeVisit`
- `ScheduleNarrativeBeat`
- `ProposeRelationshipUpdate`

## 3.6 主动世界事件流水线 `A级`

关键代码：

- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld/PatchClass.cs`
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:13204-13224`
  - 世界循环调度
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:13344`
  - 世界事件请求入口
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:13446-13527`
  - 世界规则、周围 NPC、历史事件、地点信息进入上下文
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:13547-13674`
  - JSON 解析与事件 / NPC / 物品落地
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/WorldEventData.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/MapEventData.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/MapEventIcon.cs`

可提炼：

- `trigger -> context -> inference -> normalize -> host commit`
- `micro-event scheduler`
- `narrative event record`

## 4. 因此，真正可跨宿主提炼的共享内核

共享内核只保留协议与中间层，不保留宿主最终提交动作：

- `AI routing protocol`
- `prompt / persona / world-rule protocol`
- `message-memory protocol`
- `long-term-summary protocol`
- `channel orchestration protocol`
- `narrative-intent protocol`
- `event-pipeline contract`

宿主层单独定义：

- `host profile`
- `host writable targets`
- `host adapter allowlist`
- `host commit / rollback / idempotency policy`

## 5. 最小共享数据模型

本节只保留跨宿主抽象讨论价值。

如果要实际复现 `GGBH_OpenAIWorld`，应以 reproduction manual 的 canonical snapshot contract 为准，而不是以这里的抽象最小模型替代它。

实现级最小模型仍必须保留可重放的身份、位置、场景和时间锚点。

### Core Envelope

- `gameId`
- `saveId`
- `actorId`
- `subjectIds`
- `channelType`
- `timeKey`
- `locationId`
- `sceneId`
- `surfaceId`

### Prompt Context

- `worldRuleRefs[]`
- `personaRefs[]`
- `contextTags[]`
- `recentMessages[]`
- `longTermSummary`
- `worldClock`
- `sceneSnapshot`

### Narrative Intent

- `intentType`
- `reason`
- `arguments`
- `confidence`
- `hostHints`
- `preconditions`
- `traceRefs`

### Host Extension

- `hostContext`
- `hostWritableTargets`
- `hostConstraints`

仍可放入宿主扩展的，是更强宿主特有字段，例如 `worldId`、`season`、`dayPart`、更细的地图层级语义、宿主内部对象句柄等。

## 6. 这套内核如何映射到不同宿主

## 6.1 源宿主样本：鬼谷八荒 `A级`

它已经证明：

- 多功能域模型路由可行
- 联系人群 / 多通道会话可行
- 长期记忆按月压缩和存储可行；是否存在运行时回灌仍以 reproduction manual 的 `optional reconstruction` 结论为准
- 结构化行为 + 统一动作分发可行
- 世界事件流水线可行

所以它是 `已验证样本`，不是“待讨论宿主”。

## 6.2 星露谷物语 `A级/B级映射`

最适合承接：

- Prompt / Persona / 世界规则包
- 消息记忆
- 联系人群 / 社交圈层
- 事件式多人连续发言
- 轻量传闻传播
- 邀约 / 来访 / 微事件

典型落点：

- 对话框
- 头顶气泡
- 邮件
- 事件脚本
- 家庭圈 / 酒馆圈 / 商店圈 / 诊所圈

## 6.3 环世界 `A级/B级映射`

最适合承接：

- AI 路由
- 消息记忆
- 多通道编排
- 叙事意图
- 轻量事件流水线

典型落点：

- 头顶气泡
- 殖民地连续发言
- 来访者互动
- 小型殖民地社交事件
- `WorldComponent` / `GameComponent` / `ThingComp`

## 6.4 太吾绘卷 `C级映射假设`

当前仓库里没有像星露谷、环世界那样完整的太吾参考索引。

因此这里不能写成“已验证宿主”，只能写成：

- `高匹配度宿主假设`
- `后续研究 backlog`

保留它的原因只是：

- 从宿主气质上看，太吾大概率适合关系、记忆、传闻、来访、门派圈层互动

但现阶段不应把它和鬼谷、星露谷、环世界并列成同强度结论。

## 7. 当前最有价值的统一口径

最值得采用的统一口径是：

**一套以 `Prompt/Persona/世界规则`、`消息记忆`、`长期分桶摘要`、`多通道会话编排`、`结构化叙事意图`、`轻量事件流水线` 为核心的跨宿主 AI 叙事交互内核；各游戏只负责把这些共享协议映射到自己的原生表现层和提交层。**

这句话在 `GGBH_OpenAIWorld` 里都有真实落点：

- 配置路由：
  - `AIAgents.cs`
  - `AIServer.cs`
- 世界规则 / Persona：
  - `O.cs:12717-12840`
  - `世界定义.md`
  - `角色卡模板.md`
- 记忆：
  - `PrivateMessageData`
  - `GroupMessageData`
  - `ContactGroupMessageData`
  - `ExperienceData`
  - `g.cs`
- 多通道编排：
  - `J.cs`
  - `l.cs`
  - `Y.cs`
- 结构化意图和动作分发：
  - `F.cs`
  - `i.cs`
  - `行为指令.md`
  - `信息裂变.md`
- 事件流水线：
  - `PatchClass.cs`
  - `O.cs:13204-13674`

## 8. 最终判断

最正确的通用方案不是：

- 从一个游戏里抽出完整玩法壳

而是：

- 从 `GGBH_OpenAIWorld` 中抽出它已经证明成熟的 `叙事交互内核`

这个内核最稳的组成是：

- 可路由的 AI 配置
- 可覆盖的 Prompt / Persona / 世界规则包
- 分层记忆
- 多通道会话编排
- 结构化叙事意图
- 轻量事件流水线

而邮件、背包、好感值、任务、地图对象写回，这些都应继续留在宿主 profile 和 adapter 中。
