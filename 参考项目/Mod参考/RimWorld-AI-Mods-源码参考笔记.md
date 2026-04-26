# RimWorld AI Mods 源码参考笔记

整理日期：2026-03-25  
本目录中的 `RimTalk`、`RimChat`、`RimTalk-ExpandActions` 均为源码快照，已移除 Git 元数据，适合作为后续项目参考，不适合作为上游同步副本。

源码来源：
- RimTalk: `https://github.com/jlibrary/RimTalk`
- RimChat: `https://github.com/chapm250/RimChat`
- RimTalk-ExpandActions: `https://github.com/sanguodxj-byte/RimTalk-ExpandActions`

## 一眼结论

| 项目 | 核心定位 | 架构成熟度 | 最值得复用的部分 | 最大短板 |
| --- | --- | --- | --- | --- |
| RimTalk | AI 对话框架 | 高 | Prompt 系统、Provider 抽象、上下文拼装、扩展 API | 体系较大，接入成本高 |
| RimChat | 语音化对话成品 | 中低 | TTS 接入、语音分配、简单设置模型 | 核心逻辑耦合重，扩展点少 |
| RimTalk-ExpandActions | 对话触发行为扩展 | 中 | Intent 识别、延迟动作队列、行为执行映射 | 强依赖宿主和反射，当前集成链有风险点 |

建议把这三个项目理解成三层：
- `RimTalk` 负责“怎么组织上下文、怎么问模型、怎么把回答变成对话”
- `RimChat` 负责“怎么把一句 AI 台词播出来”
- `RimTalk-ExpandActions` 负责“怎么把 AI 对话继续翻译成游戏行为”

## 目录速记

- `RimTalk/Source/Service`: 核心服务层，主要看 `TalkService.cs`、`AIService.cs`、`ContextBuilder.cs`
- `RimTalk/Source/Prompt`: Prompt 预设、变量、模板解析
- `RimTalk/Source/Client`: 各模型供应商客户端
- `RimTalk/Source/API`: 给第三方 Mod 用的扩展接口
- `RimChat/Source/RimChat/Core`: 对话生成和 TTS 播放主逻辑
- `RimChat/Source/RimChat/Configuration`: 很轻量的设置封装
- `RimTalk-ExpandActions/Source/Memory/AI`: 意图识别、决策、延迟执行
- `RimTalk-ExpandActions/Source/Memory/Actions`: 真正落到 RimWorld 行为的执行器
- `RimTalk-ExpandActions/Source/Patches`: 对 RimTalk 的 Hook 和桥接层

## 1. RimTalk

### 1.1 定位

RimTalk 不是单纯“生成一句话”，而是一套完整的 AI 对话框架。它把对话拆成了：
- 触发
- 上下文收集
- Prompt 组装
- Provider 调用
- 流式解析
- 气泡展示
- 历史记录
- 扩展注入

这一套结构是三个项目里最像“可二开的基础设施”的。

### 1.2 关键入口文件

- `RimTalk/About/About.xml`: Mod 元信息、依赖、支持版本
- `RimTalk/Source/Settings.cs`: 主设置窗口和 Tab UI
- `RimTalk/Source/Patch/TickManagerPatch.cs`: 每 tick 调度入口
- `RimTalk/Source/Service/TalkService.cs`: 对话生成和展示主链路
- `RimTalk/Source/Service/AIService.cs`: 统一 AI 调用入口
- `RimTalk/Source/Prompt/PromptManager.cs`: Prompt 预设和消息构建
- `RimTalk/Source/Client/AIClientFactory.cs`: Provider 工厂
- `RimTalk/Source/API/ContextHookRegistry.cs`: 上下文扩展注册中心
- `RimTalk/Source/API/RimTalkPromptAPI.cs`: 给外部 Mod 的 Prompt API

### 1.3 运行链路

核心链路基本是：

1. `TickManagerPatch.Postfix()` 定时刷新缓存、检查用户请求、选择可说话 pawn。
2. `TalkService.GenerateTalk()` 做前置校验，选对话参与者，构建 `TalkRequest`。
3. `PromptManager.Instance.BuildMessages(...)` 生成最终消息列表。
4. `AIService.ChatStreaming(...)` 统一发起流式请求。
5. `AIClientFactory.GetAIClientAsync()` 根据配置返回 Gemini / OpenAI-compatible / Player2 / Local 客户端。
6. 流式解析得到 `TalkResponse` 后进入 `PawnState.TalkResponses` 队列。
7. `TalkService.DisplayTalk()` 消费队列，生成 `PlayLogEntry_RimTalkInteraction`，最后由气泡/UI 展示。

### 1.4 最值得复用的模块

#### Prompt 系统

`RimTalk/Source/Prompt` 这层是最有参考价值的部分：

- 支持 Simple / Advanced 两种模式
- Prompt 不是硬编码字符串，而是 `Preset -> Entry -> Role -> Position`
- 支持变量存储、导入导出、预设复制
- 用 `Scriban` 作为模板引擎，能力远强于简单字符串替换

如果你后面要做自己的“AI 行为系统”或“角色导演系统”，这一层可以直接借鉴，而不是重新拼 prompt。

#### Provider 抽象

`AIClientFactory.cs` + `IAIClient` 的思路很干净：

- 特殊供应商走单独客户端
- OpenAI-compatible 服务统一复用 `OpenAIClient`
- 本地模型和云模型共用一套上层调用

这意味着后续你做新项目时，完全可以把“模型供应商层”独立成一层基础设施，不绑死某一家。

#### Context 扩展 API

`ContextHookRegistry.cs` 和 `RimTalkPromptAPI.cs` 是它最像平台的地方：

- 可注册自定义 `pawn` 变量
- 可注册环境变量
- 可注册完整上下文变量
- 可插入 Prompt Entry
- 可按优先级排序和注入

如果你后面要做“记忆系统”“人格系统”“任务导演系统”，这套注册式扩展方式比直接改核心 prompt 更稳。

#### 调度与历史

`TickManagerPatch.cs`、`TalkHistory.cs`、`PawnState.cs`、`TalkRequestPool.cs` 这一套把“说话频率控制”“会话历史”“pending 队列”拆出来了。  
这比把一切塞进一个大类里更适合后续演化。

### 1.5 工程评价

优点：
- 分层清晰
- 可配置程度高
- 扩展点明确
- 对第三方 Mod 比较友好

风险：
- 体系偏大，想裁剪成“只保留行为 AI”时要做减法
- Prompt、Context、Settings、Patch 四层耦合较多，抽壳需要时间
- 依赖 RimWorld 的运行时对象较深，不适合原封不动搬去别的游戏

### 1.6 对你项目的参考价值

如果你的目标是做“AI 驱动 NPC 系统”，RimTalk 里最该抄的是：
- Prompt preset 体系
- Context 注入机制
- Provider 工厂
- 流式响应转游戏事件的管线

最不建议直接照搬的是：
- 过重的设置 UI
- 具体到 RimWorld 的大量 context 字段拼装

## 2. RimChat

### 2.1 定位

RimChat 更像“可玩的成品 Mod”，不是平台。  
它的核心目标很直接：抓住 RimWorld 已发生的社交互动，然后：

- 生成一段更自然的台词
- 调 TTS 合成声音
- 在游戏里播出来

它更像“语音层 + 简化对话层”。

### 2.2 关键入口文件

- `RimChat/About/About.xml`: 元信息
- `RimChat/Source/RimChat/Mod.cs`: 设置 UI 与 Harmony 初始化
- `RimChat/Source/RimChat/Settings.cs`: 全部设置项
- `RimChat/Source/RimChat/Core/Chatter.cs`: 互动捕捉、调度、语音分配
- `RimChat/Source/RimChat/Core/Chat.cs`: LLM 请求和 TTS 请求主实现
- `RimChat/Source/RimChat/Configuration/Setting.cs`: 轻量设置封装

### 2.3 运行链路

基本链路是：

1. Harmony patch 捕捉 `PlayLogEntry_Interaction`
2. `Chatter.Add(...)` 筛选发言者、接收者、互动类型和概率
3. 为 pawn 分配 voice，并创建 `Chat` 实例
4. `Chatter.StartTalk()` 异步调用 `chat.Talk(...)`
5. `Chat.cs` 按当前 LLM provider 分支调用 OpenAI / Claude / Gemini / Player2
6. 返回文本后再按 TTS provider 调 ElevenLabs / OpenAI TTS / Resemble / Player2
7. 生成 `AudioSource` 播放，并暂时压低背景音乐

### 2.4 最值得复用的模块

#### 语音分配和持久化

`VoiceWorldComp` 的思路是可以直接借鉴的：

- 按 provider 维护 voice pool
- 按 pawn 持久化 voice 分配
- 支持切换 provider 后重新分配
- 用反向索引尽量保证 voice 唯一

如果你未来做“每个角色固定音色”的系统，这块参考价值很高。

#### 轻量设置封装

`Configuration/Setting.cs` 很简单，但对小型 Mod 很实用：

- 一个泛型 `Setting<T>`
- 默认值、序列化、重置逻辑集中处理

如果项目不大，这种方案比上来就做复杂配置系统更合适。

#### TTS 接入范式

`Chat.cs` 里虽然写法偏集中，但把多家 TTS 的请求格式都走通了：

- ElevenLabs
- OpenAI `audio/speech`
- Resemble
- Player2 本地服务

如果你只是要打通“文本 -> 音频 -> Unity AudioClip”，这份实现可以直接作样例。

### 2.5 明显局限

RimChat 的核心问题不是功能少，而是工程结构偏紧：

- `Chat.cs` 同时处理 prompt 拼装、HTTP 请求、响应解析、音频生成
- `Chatter.cs` 同时承担调度、状态机、语音管理、Player2 健康检查
- 对话能力主要是模板替换，不是正式的 prompt framework
- 没有像 RimTalk 那样成熟的插件 API

换句话说，它更适合“参考实现”，不太适合当你自己的系统内核。

### 2.6 对你项目的参考价值

优先参考：
- 多 TTS provider 接入
- Voice assignment 持久化
- 用游戏现有 interaction 驱动 AI 台词

谨慎参考：
- 让一个类同时承担太多职责
- 把 provider 分支逻辑全部堆在一个文件里

## 3. RimTalk-ExpandActions

### 3.1 定位

这是一个典型的“宿主增强型扩展”。  
它不自己做完整对话框架，而是直接吃 `RimTalk` 的输出，再试图把对话转换成游戏动作。

它的核心问题不是“能不能聊天”，而是：
- 怎么识别 AI 回复里的行为意图
- 怎么做风险判断
- 怎么延迟执行
- 怎么把意图落成 RimWorld 行为

### 3.2 关键入口文件

- `RimTalk-ExpandActions/About/About.xml`: 功能清单、依赖、支持指令
- `RimTalk-ExpandActions/Source/RimTalkExpandActionsMod.cs`: Mod 初始化和设置 UI
- `RimTalk-ExpandActions/Source/Patches/RimTalkDialogPatch.cs`: Hook RimTalk 输出
- `RimTalk-ExpandActions/Source/Memory/AI/HybridIntentRecognizer.cs`: 混合意图识别
- `RimTalk-ExpandActions/Source/Memory/AI/LocalNLUAnalyzer.cs`: 本地规则分析器
- `RimTalk-ExpandActions/Source/Memory/AI/DelayedActionQueue.cs`: 延迟执行队列
- `RimTalk-ExpandActions/Source/Memory/AI/ActionExecutor.cs`: 意图到动作的映射
- `RimTalk-ExpandActions/Source/Memory/Actions/RimTalkActions.cs`: 真正的游戏行为实现

### 3.3 当前设计思路

它不是只靠固定 JSON 指令，而是想做三层识别：

1. 本地 NLU 规则优先  
   `LocalNLUAnalyzer` 注册了一组 intent rule，用关键词和模式匹配判断“招募、恋爱、休息、送礼、社交用餐”等意图。

2. LLM 标签增强  
   如果 AI 回复里刚好带了特定 tag，就提高置信度，直接走标签路径。

3. 决策矩阵 + 延迟执行  
   `HybridIntentRecognizer` 会给出置信度、风险等级、建议延迟，再由 `DelayedActionQueue` 自然延后执行。

这套思路比“让模型必须输出固定 JSON”更稳，也更适合自然语言驱动行为。

### 3.4 已实现的动作层

从 `ActionExecutor.cs` 和 `RimTalkActions.cs` 看，核心动作包括：

- `recruit_agree`
- `romance_accept`
- `romance_breakup`
- `force_rest`
- `inspire_fight`
- `inspire_work`
- `give_item`
- `social_dining`
- `social_relax`

实际行为落地包括：
- 改 faction 招募
- 建立或移除恋爱关系
- 强制休息
- 添加 inspiration
- 赠礼
- 社交用餐 Job

### 3.5 最值得复用的模块

#### Intent 规则层

`LocalNLUAnalyzer` + `IntentRules/*` 很适合你的项目参考，因为它把“自然语言行为判断”拆成了可维护的规则对象，而不是把所有判断写死在一个巨大 if/else 里。

如果你做自己的 AI NPC 系统，推荐保留这个结构：
- 一个统一分析器
- 多条独立规则
- 每条规则返回置信度
- 最终选最高分意图

#### 行为队列

`DelayedActionQueue` 的设计很实用：

- 不立刻执行
- 有随机化延迟
- 可取消
- 避免重复触发

这比“识别到就马上做”更自然，也更容易在游戏里调试。

#### 意图到动作的分发器

`ActionExecutor.Execute(intentName, speaker, listener)` 这种中心分发写法虽然朴素，但对行为系统很有效。  
适合把“识别层”和“游戏执行层”隔开。

### 3.6 明显风险点

这个项目最需要注意的不是功能，而是集成稳定性。

#### 风险 1：强依赖反射和宿主内部结构

`RimTalkDialogPatch.cs` 会动态搜索：
- `TalkService`
- `PlayLogEntry_RimTalkInteraction`
- 相关方法名和字段名

这意味着只要 RimTalk 改命名、改签名、改返回对象，它就可能失效。

#### 风险 2：主 Hook 当前传入的 listener 是空

`GetTalk_Postfix` 当前是：

- 从 `TalkService.GetTalk(Pawn pawn)` 拿到文本
- 然后调用 `ProcessResponse(originalText, pawn, null)`

而 `ProcessResponse(...)` 内部又要求：
- `speaker != null`
- `listener != null`

否则直接记录“缺少 speaker 或 listener，跳过行为触发”。

也就是说，按仓库当前代码看，主 `GetTalk` 路径本身更像“文本清洗 + 预留行为入口”，真正稳定触发行为还依赖别的上下文补齐方案。  
如果你以后参考它来做自己的项目，第一优先级应该是先把“行为触发时的 speaker / listener / userInput”数据链打通。

#### 风险 3：工程环境写死

`RimTalk-ExpandActions.csproj` 里有明显本地开发机路径，例如：
- 本地 RimWorld 安装目录
- 本地 RimTalk DLL 目录
- 本地 ExpandMemory DLL 目录

这说明它更像作者本机工程，不适合直接拿来编译。作为参考源码没问题，但要二开必须先清理工程依赖。

### 3.7 对你项目的参考价值

最该吸收的不是它的补丁写法，而是它的“行为 AI 结构”：

- 对话文本进入识别层
- 识别层给出 intent + confidence + risk
- 决策层判断是否执行
- 队列层安排时机
- 执行层落到具体游戏行为

如果你要做“AI 驱动 NPC 行为”，这套骨架很值得保留。

## 4. 如果你后面自己做项目，建议怎么取材

最推荐的组合不是直接选一个照抄，而是拆着用：

- 用 `RimTalk` 的 Prompt、Context、Provider 抽象做 AI 基础设施
- 用 `RimChat` 的 TTS 和 voice assignment 作为语音层
- 用 `RimTalk-ExpandActions` 的 intent / decision / delayed action 做行为层

一个更合理的新项目结构可以是：

1. `core-ai`
   负责模型供应商、Prompt preset、上下文拼装、流式输出
2. `dialogue-runtime`
   负责对话展示、会话状态、历史缓存
3. `voice-runtime`
   负责 TTS、音色分配、音频缓存
4. `intent-runtime`
   负责意图识别、置信度、风险评估
5. `action-runtime`
   负责行为队列和实际游戏执行

这样做的好处是：
- 以后要换模型供应商，不动行为层
- 以后要去掉语音，不动对话层
- 以后要从 RimWorld 迁移到别的游戏，也能保住大部分 AI 基础设施

## 5. 当前最值得优先读的文件

如果只看少量文件，推荐顺序如下：

1. `RimTalk/Source/Service/TalkService.cs`
2. `RimTalk/Source/Prompt/PromptManager.cs`
3. `RimTalk/Source/API/ContextHookRegistry.cs`
4. `RimChat/Source/RimChat/Core/Chatter.cs`
5. `RimChat/Source/RimChat/Core/Chat.cs`
6. `RimTalk-ExpandActions/Source/Patches/RimTalkDialogPatch.cs`
7. `RimTalk-ExpandActions/Source/Memory/AI/HybridIntentRecognizer.cs`
8. `RimTalk-ExpandActions/Source/Memory/AI/LocalNLUAnalyzer.cs`
9. `RimTalk-ExpandActions/Source/Memory/AI/DelayedActionQueue.cs`
10. `RimTalk-ExpandActions/Source/Memory/Actions/RimTalkActions.cs`

## 6. 最终判断

如果只允许留一个做“项目骨架参考”，优先级是：

1. `RimTalk`
2. `RimTalk-ExpandActions`
3. `RimChat`

原因很简单：

- `RimTalk` 最像平台
- `ExpandActions` 最像行为 AI 原型
- `RimChat` 最像语音能力样例

如果未来你的目标是“让 NPC 不只是说话，还会因为对话而行动”，真正值得组合参考的是：

- 用 `RimTalk` 做对话底座
- 借 `ExpandActions` 做行为闭环
- 从 `RimChat` 取语音层
