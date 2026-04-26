# 星露谷综合型 AI Mod 功能设计参考

副标题：基于 `GGBH_OpenAIWorld` 的真实功能、版本演进和代码节点，整理可借鉴能力与方案参考

> Current-architecture interpretation note:
>
> This attachment is still a valid borrowing/reference pool for feature scope and recovered chain shape.
> But current repo direction no longer assumes that rebuilt narrative orchestration must stay local.
> Read it as:
> - preserve the proven chain structure and feature semantics
> - allow hosted orchestration by default
> - keep local deterministic execution, UI binding, and authoritative host writeback

## 1. 文档目的

这份文档回答的不是“马上怎么落地”，而是：

- 如果参考 `GGBH_OpenAIWorld` 这个已经比较成熟的 mod，星露谷综合型 AI mod 应该把功能池写到多完整
- 哪些能力在 `GGBH_OpenAIWorld` 中已经有明确执行链
- 哪些能力只有 prompt / 配置证据，适合写入方案池，但不能当成已验证完整链
- 每个判断在 `GGBH_OpenAIWorld` 中对应哪些版本说明、功能来源与关键代码节点

本文综合使用：

- `recovered_mod/GGBH_OpenAIWorld_20260326/GGBH_OpenAIWorld_功能梳理_非UI.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/NPC_AI_FINDINGS.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/ACTIVE_WORLD_AI_CHAIN.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/PROMPT_STORAGE_ANALYSIS.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/artifacts/OpenAIWorld@updatelog.json`
- `参考项目/参考文档/星露谷物语/README.md`

## 2. 结论先行

星露谷最该借的不是“修仙世界演化壳”，而是这几层：

- `AI 路由与多方案配置`
- `世界规则 / Persona / Prompt 包`
- `短期消息记忆`
- `长期经历 / 月度传记记忆`
- `联系人群 / 社交圈层`
- `结构化叙事意图`
- `轻量事件流水线`

因此，星露谷的功能设计池应该写全为：

- NPC 会记住玩家、礼物、交易、承诺、失约、未归还物
- NPC 会基于关系、生日、节日、天气和最近经历主动互动
- 镇上存在联系人群和圈层传播，例如家庭、酒馆、商店、诊所
- 邮件、礼物、借还、协商、拒绝、邀约会面、来访串门都能进入 AI 语境
- 多 NPC 连续发言可以作为事件式演出能力
- 传闻和口信可以在镇上轻量流转

一句话概括：

星露谷版应该是 `AI 驱动的社交、礼物、交易协商、邮件、传闻、邀约、微事件系统`，而不是 `修仙世界演化系统换皮版`。

## 3. 借鉴分级

### 3.1 适合直接借鉴

- `多服务器、多方案、多模型配置`
- `Prompt 分层组织`
- `角色卡 / Persona 内容包`
- `消息记忆 + 长期经历记忆双层结构`
- `联系人群 / 社交圈层`
- `结构化意图白名单`
- `世界规则 prompt + 功能 prompt 分层`

### 3.2 适合重构后借鉴

- `事件式多人连续发言`
- `信息裂变 / 传闻传播`
- `交易 / 借还 / 拒绝 / 协商`
- `邀约会面 / 来访串门`
- `主动世界 AI`
- `世界事件数据结构`
- `动态礼物语境`
- `记忆压缩`

说明：

- `记忆压缩` 在鬼谷里已经有明确月度压缩链，但对星露谷仍更适合按“长期摘要机制”重构，而不是原样照搬。
- `信息裂变` 的 prompt 和功能证据很强，但对星露谷更稳妥的借法是轻量传闻网络，而不是高强度自动社会模拟。

### 3.3 不建议照搬

- `双修 / 身体交互`
- `战败 AI 对话`
- `宗门 / 守卫 / 义亲 / 师徒`
- `境界 / 气运 / 魔气`
- `大规模地图演化`

## 4. 这个成熟 mod 已经确认存在的功能成长线

从 `OpenAIWorld@updatelog.json` 可以直接看到：

- `4.1.0`
  - AI 对话已有送物、借物、交易
- `4.10.1`
  - AI 读取近期经历和生涯大事
- `4.12.0`
  - 互留传音符、邀请做客
- `5.4.0` 到 `5.6.0`
  - 群聊支持记录存储和互动
- `6.2.0`
  - AI 交易、多方案模型配置、主动联系
- `6.7.0`
  - 聊天记录 / 传记 / NPC 数据支持导入导出
- `7.2.0`
  - 私聊重构为个人记忆，增加地点、类别和更多行为记录
- `8.0.0`
  - 信息裂变和个人记忆系统
- `9.0.0` 到 `9.1.0`
  - 全套 prompt 和角色卡系统

这说明 `关系 / 记忆 / 联系 / 交易 / 给予 / 借还 / 邀约 / 传播` 才是成熟能力主线。

## 5. 星露谷综合型 AI Mod 的推荐功能池

## 5.1 NPC 记忆与关系引擎

建议功能：

- 最近对话记忆
- 最近送礼记忆
- 最近帮忙 / 冲突 / 拒绝记忆
- 承诺与失约记忆
- 借出未还 / 欠人情 / 待兑现约定
- 节日和生日记忆
- 长期印象摘要
- 月度传记摘要

鬼谷来源：

- `MessageData`
- `PrivateMessageData`
- `GroupMessageData`
- `ContactGroupMessageData`
- `ExperienceData`
- `记忆压缩.md`

关键代码节点：

- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod.data/PrivateMessageData.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod.data/GroupMessageData.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod.data/ContactGroupMessageData.cs`
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/ExperienceData.cs`
- `decompiled/GGBH_OpenAIWorld_src/.../g.cs:10557-10575`
  - 私聊消息会带日期、地点、天气
- `decompiled/GGBH_OpenAIWorld_src/.../g.cs:10625-10665`
  - 月度长期记忆读取
- `decompiled/GGBH_OpenAIWorld_src/.../g.cs:10687-10696`
  - 按月滚动压缩
- `decompiled/GGBH_OpenAIWorld_src/.../g.cs:10717-10815`
  - 调用 LLM 生成长期摘要
- `decompiled/GGBH_OpenAIWorld_src/.../l.cs:11261-11265`
  - 群聊链注入长期记忆
- `decompiled/GGBH_OpenAIWorld_src/.../Y.cs:12578-12581`
  - 联系人群链注入长期记忆
- `decoded_prompts/记忆压缩.md`
  - 要求保留关系、事件、承诺、交易

星露谷侧改造建议：

- 拆成：
  - `ConversationMemory`
  - `GiftMemory`
  - `PromiseMemory`
  - `DebtMemory`
  - `VisitMemory`
  - `FestivalMemory`
  - `RumorMemory`
  - `LongTermImpression`
- 记忆应绑定：
  - 日期
  - 地点
  - 天气
  - 节日状态
  - 好感阶段

## 5.2 联系人群、社交圈层、动态对话

建议功能：

- 家庭圈
- 酒馆圈
- 商店圈
- 诊所圈
- 镇政圈
- 普通动态对话
- 事件式多人连续发言
- 圈层内部消息历史

鬼谷来源：

- 联系人群
- 群聊
- 主动联系配置

关键代码节点：

- `decompiled/GGBH_OpenAIWorld_src/.../b.cs:12160-12186`
  - `AILM_ContactAIConfig` / `AILM_GroupAIConfig` / `AIGroupChat` / `AIContactGroup`
- `decompiled/GGBH_OpenAIWorld_src/.../Y.cs:12436-12501`
  - 联系人群请求组装
- `decompiled/GGBH_OpenAIWorld_src/.../Y.cs:12454-12469`
  - 联系人群历史注入
- `decompiled/GGBH_OpenAIWorld_src/.../Y.cs:12600-12645`
  - 联系人群历史按日期和说话人重建
- `decompiled/GGBH_OpenAIWorld_src/.../Y.cs:12681-12691`
  - 联系人群回复回写
- `decompiled/GGBH_OpenAIWorld_src/.../J.cs:10874-10899`
  - 群聊玩家消息入队
- `decompiled/GGBH_OpenAIWorld_src/.../J.cs:10905-10935`
  - 群聊按队列轮流发言
- `decompiled/GGBH_OpenAIWorld_src/.../l.cs:11224-11370`
  - 群聊上下文、长期记忆、结果回写

星露谷侧改造建议：

- 不做鬼谷式常驻聊天面板
- 群聊优先落成：
  - 事件中的多人连续发言
  - 头顶连续气泡
  - 节日 / 酒馆 / 家庭场景的局部演出
- `M1` 就可纳入 `事件式多人连续发言`
- `M2` 再扩展为更高频的圈层传播

## 5.3 主动赠礼、交易、借还、拒绝与协商

建议功能：

- NPC 因关系、生日、节日主动送礼
- NPC 可提出交易、借用、归还、催还
- 玩家可被拒绝、被协商条件、被延后
- 所有礼物和协商都带“为什么”

鬼谷来源：

- `行为指令.md` 中的 `Transaction` / `GiveItem` / `LendItem`
- `交易.md`
- AI 给予配置
- AI 交易配置

关键代码节点：

- `decompiled/GGBH_OpenAIWorld_src/.../b.cs:12196-12218`
  - `AI_Transaction`
  - `AI_TransactionAIServer`
  - `AI_TransactionScene`
  - `AI_Give`
  - `AI_GiveServer`
  - `AI_GiveScene`
  - `AI_GiveAction`
  - `AI_ActiveAction`
- `decoded_prompts/行为指令.md:269`
  - `Transaction`
- `decoded_prompts/行为指令.md:505`
  - `GiveItem`
- `decoded_prompts/行为指令.md:528`
  - `LendItem`
- `decoded_prompts/交易.md`
  - 交易中的接受 / 拒绝 / 协商语境

证据边界：

- `GiveItem / LendItem / Transaction` 在协议层和版本说明中都很明确
- 但 `O.cs:13663-13671` 更适合证明“世界事件里物品落地能力”，不适合直接当成“NPC 主动赠礼 / 邮件链”证据

星露谷侧改造建议：

- `M1` 最短路径：
  - `Data/Mail + AddMail` 负责投递邮件和礼物通知
  - 事件奖励 / 直接给物品负责真正发放
- 动态礼物语境：
  - `item.modData` 记录 AI 语境
  - 配合 `Harmony patch` 改写名称 / 描述 / tooltip
- 不做“AI 任意发明完整物品系统”

## 5.4 邀约会面、来访串门、微事件、邮件剧情

建议功能：

- 邀请见面
- 邀请到家里 / 酒馆 / 诊所 / 节日场地
- 来访串门
- 镇上小插曲
- 家庭请求
- 节日前准备事件
- 天气驱动邮件剧情

鬼谷来源：

- `Invite`
- 发送传音符
- 主动世界 AI

关键代码节点：

- `decoded_prompts/行为指令.md:433`
  - `Invite`
- `decoded_prompts/发送传音符.md`
  - 明确包含 `邀请会面`
- `OpenAIWorld@updatelog.json`
  - `4.12.0`、`3.1.2` 都有邀请做客 / 来访的版本记录
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:13204-13224`
  - 世界循环调度
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:13344`
  - 主动世界请求入口
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:13446-13527`
  - 世界规则、周边 NPC、历史事件、地点进入上下文
- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:13547-13674`
  - JSON 解析和世界落地

星露谷侧改造建议：

- 不做大规模镇子演化
- 做“轻量镇子导演”：
  - 每天 0 到 2 条口信或微事件
  - 每周 1 条委托、邀约或串门
  - 节日前置剧情
  - 关系阈值触发特殊来访

## 5.5 传闻传播与轻量世界模拟

建议功能：

- 谁知道某件事
- 谁会向谁传播
- 家庭 / 酒馆 / 商店 / 节日的不同传播路径
- 传闻失真、延迟、选择性传播

鬼谷来源：

- `信息裂变`
- 联系人群与主动联系配置

关键代码节点：

- `decoded_prompts/信息裂变.md`
  - `ConveyMessage`
- `decompiled/GGBH_OpenAIWorld_src/.../i.cs:11376-11410`
  - `ConveyMessage` 会转成新的真实通信链
- `decompiled/GGBH_OpenAIWorld_src/.../b.cs:12160-12186`
  - 联系和群聊相关配置

星露谷侧改造建议：

- 先做 `轻量传闻网络`
- 改动目标限制在：
  - 文本层
  - 事件触发层
  - 圈层知情状态

## 5.6 镇子规则 / Persona / 内容包系统

建议功能：

- 镇子世界规则
- 季节规则
- 节日规则
- NPC 公开 Persona
- NPC 私密 Persona
- 各圈层的社交规则

鬼谷来源：

- 世界定义
- 角色卡模板
- 本地 prompt 覆盖

关键代码节点：

- `decompiled/GGBH_OpenAIWorld_src/.../O.cs:12717-12840`
- `decoded_prompts/世界定义.md`
- `decoded_prompts/角色卡模板.md`
- `decoded_prompts/行为指令.md`
- `decoded_prompts/信息裂变.md`
- `decoded_prompts/交易.md`

星露谷侧改造建议：

- 单独建立：
  - `TownRules`
  - `NpcPersonaPack`
  - `Gift/Trade/Visit/Rumor/MicroEvent` 各功能 prompt

## 6. 对当前项目的直接建议

功能设计阶段可以把能力池写全，但方案说明要分层：

- `M1`
  - 记忆
  - Persona
  - 结构化意图
  - 联系人群 / 社交圈层
  - 送礼 / 交易 / 借还语境
  - 邮件 / 对话 / 气泡 / 邀约 / 来访 / 微事件
  - 事件式多人连续发言
- `M2`
  - 轻量传闻传播
  - 更高频的圈层互动
  - 周期性镇子导演
- `M3`
  - 更复杂的 NPC-NPC 社交网络
  - 更强的镇子级事件导演

这里必须保持一致：

- `M1` 已可包含事件式多人连续发言
- `M2` 才扩展成更强的自动传播与周期性轻量模拟

## 7. 最终判断

如果你想参考 `GGBH_OpenAIWorld` 做星露谷综合型 AI mod，最正确的借法是：

- 借它的系统分层
- 借它的联系人群设计
- 借它的月度长期记忆
- 借它的交易 / 借还 / 邀约 / 传播协议
- 借它的 Prompt / Persona / 内容包组织方式

不要借：

- 玄幻题材壳
- 高冲突玩法壳
- 强世界控制壳

一句话概括：

星露谷版应该是 `AI 驱动的社交、礼物、交易协商、邮件、传闻、邀约、微事件系统`，而不是 `修仙世界演化系统的农场换皮版`。
