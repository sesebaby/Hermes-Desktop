# 星露谷手机消息与 OpenAI 400 修复计划

> 给后续执行 agent：这个计划只处理两个当前阻断问题：OpenAI `400 Bad Request`，以及 Hermes/NPC 主动文本打断玩家行动。实现时优先复用 `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone` 的授权代码和素材，避免自己重造手机外壳、布局、点击命中和震动提示。

## 目标

把两条体验链路修顺：

1. OpenAI thinking/reasoning 模型返回工具调用后，下一轮请求不再因为缺少 `reasoning_content` 报 `400 Bad Request`。
2. NPC 主动消息不再弹全局对话框打断玩家，近距离显示头顶气泡，远距离进入右侧微信式手机。玩家可以在手机里回复 NPC，回复必须进入 Hermes Agent。

## 已确认规则

- 玩家主动点击 NPC：继续走原版星露谷 `DialogueBox`，不改。
- Hermes/NPC 主动说话：不能直接打开全局 `DialogueBox`。
- 同地图且距离不超过 8 格：走 NPC 头顶气泡。
- 不同地图，或同地图距离超过 8 格：走手机消息。
- 8 格判断不看 NPC 朝向。
- 手机打开时不占用键盘，不设置 `Game1.activeClickableMenu`。
- 只有玩家点进手机输入框打字时，才临时接管键盘；提交、取消、关闭、点外面后必须释放键盘。
- 手机回复必须进入 Hermes Agent，不能只在 SMAPI 本地显示。
- 关键节点必须打日志，后续手动测试要能从日志看出问题卡在哪里。
- 不永久清 NPC 原版日程。
- `hold/控制租约` 不能再被完全推迟：移动控制租约仍是后续问题，但手机私聊已经接入现有 session lease，必须在本计划里对齐手机状态和私聊 lease 生命周期。

## 当前证据

- `src/LLM/OpenAiClient.cs:43` 到 `src/LLM/OpenAiClient.cs:89`：`CompleteWithToolsAsync` 只把 `reasoning` 当成 `content` 的兜底，没有把 reasoning 字段保存到 `ChatResponse`。
- `src/LLM/OpenAiClient.cs:180` 到 `src/LLM/OpenAiClient.cs:225`：`BuildPayload` 只回放 `role/content/tool_calls/tool_call_id`，没有回传 `reasoning_content`。
- `src/Core/Models.cs:5` 到 `src/Core/Models.cs:14`：`Message` 没有 reasoning 字段。
- `src/Core/Models.cs:106` 到 `src/Core/Models.cs:116`：`ChatResponse` 没有 reasoning 字段。
- `src/Core/AgentLoopScaffold.cs:144` 到 `src/Core/AgentLoopScaffold.cs:177`：assistant 普通消息和 tool-call 消息保存时都没有 reasoning。
- `src/Core/Agent.cs:769`、`src/Core/Agent.cs:805`、`src/Core/Agent.cs:822`：`StreamChatAsync` 里仍有直接 `new Message` 并 `SaveMessageAsync` 的路径，会绕过 writer，必须一起处理。
- `src/search/SessionSearchIndex.cs:90` 到 `src/search/SessionSearchIndex.cs:106`：表结构已经有 `reasoning`、`reasoning_content`、`reasoning_details`、`codex_reasoning_items`。
- `src/search/SessionSearchIndex.cs:226` 到 `src/search/SessionSearchIndex.cs:255`：读取消息时没有读这些 reasoning 列。
- `src/search/SessionSearchIndex.cs:582` 到 `src/search/SessionSearchIndex.cs:613`：插入消息时没有写这些 reasoning 列。
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:180` 到 `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:224`：`ExecuteSpeak` 直接调用 `NpcRawDialogueRenderer.Display(...)`，会开全局对话框。
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:236` 到 `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:325`：`ExecuteOpenPrivateChat` 创建 `PrivateChatInputMenu` 并赋给 `Game1.activeClickableMenu`。
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:646` 到 `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs:654`：move 中断逻辑把 `DialogueBox` 判为 `dialogue_started`。
- `Mods/StardewHermesBridge/Ui/PrivateChatInputMenu.cs:39` 到 `Mods/StardewHermesBridge/Ui/PrivateChatInputMenu.cs:64`：旧私聊输入菜单构造时直接接管键盘。
- `Mods/StardewHermesBridge/Ui/BridgeStatusOverlay.cs:65` 到 `Mods/StardewHermesBridge/Ui/BridgeStatusOverlay.cs:80`：已有非菜单 overlay 绘制入口，可以作为手机/气泡 UI 的基础。
- `src/game/core/PrivateChatOrchestrator.cs:207` 到 `src/game/core/PrivateChatOrchestrator.cs:244`：私聊打开前会先拿 session lease，失败就不继续。
- `src/runtime/NpcRuntimeInstance.cs:106` 到 `src/runtime/NpcRuntimeInstance.cs:125`：私聊 lease 会暂停 NPC autonomy。
- `src/runtime/NpcRuntimeInstance.cs:379` 到 `src/runtime/NpcRuntimeInstance.cs:439`：释放 lease 时恢复 NPC autonomy。
- `src/transcript/TranscriptStore.cs:47` 到 `src/transcript/TranscriptStore.cs:55`：保存消息会进入 `SessionSearchIndex`，缓存里也要保留新字段。
- `src/transcript/TranscriptStore.cs:75` 到 `src/transcript/TranscriptStore.cs:88`：恢复 session 会从 `SessionSearchIndex.LoadMessages(...)` 读回消息。
- `src/transcript/TranscriptStore.cs:176` 到 `src/transcript/TranscriptStore.cs:206`：legacy JSONL import 会直接反序列化 `Message` 并 `ReplaceSessionMessages(...)`，新 reasoning 字段不能在迁移时丢。
- `src/games/stardew/StardewPrivateChatOrchestrator.cs:79` 到 `src/games/stardew/StardewPrivateChatOrchestrator.cs:82`：Stardew 私聊层还有 `IsRetryableOpenFailure(...)`，会影响 `open_private_chat` 成功/重试语义。
- `Mods/StardewHermesBridge/ModEntry.cs:192` 到 `Mods/StardewHermesBridge/ModEntry.cs:228`：玩家主动点 NPC 的原版对话观察链路已经存在，必须保留。
- `Mods/StardewHermesBridge/ModEntry.cs:281` 到 `Mods/StardewHermesBridge/ModEntry.cs:302`：旧的 `private_chat_reply_closed` 依赖 `DialogueBox` 关闭，需要改成手机状态事件。
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs:205` 到 `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs:234`：现有测试明确要求 `new PrivateChatInputMenu`、`Game1.activeClickableMenu = ...`、`PrivateChatInputMenu : IClickableMenu`，这些旧断言必须随手机 overlay 方案重写。
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs:236` 到 `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs:290`：现有测试锁定旧居中菜单的 portrait shell 和中文输入提示，手机 overlay 后这些断言要迁到 `HermesPhoneOverlay/HermesPhoneInput`。
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs:292` 到 `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs:309`：现有测试把 `private_chat_reply_closed` 绑定到 Stardew reply dialogue 离场，必须改成手机状态机或气泡 overlay 发 close 事件。

## RALPLAN-DR

### 原则

- 先修协议，再修体验：OpenAI 400 会阻断 Agent 回复，必须先处理。
- 主动文本只有一条路由：不要让 `ExecuteSpeak`、私聊回复、自动 replay 各走各的显示逻辑。
- 手机是 overlay，不是菜单：打开手机不等于暂停游戏。
- 输入焦点必须显式：谁占键盘、何时释放，都要有状态和日志。
- 授权参考项目优先复用：能用 MobilePhone 的外壳、素材、布局和输入命中，就不要自己重写。

### 决策驱动

- 稳定性：不能再因为主动文本开 `DialogueBox` 中断 Haley move。
- 可排查性：日志要能串起 OpenAI、Agent、bridge、UI、move 中断。
- 低重造：MobilePhone 已经有成熟手机视觉和交互基础，应改造使用。

### 可选方案

| 方案 | 做法 | 优点 | 问题 |
| --- | --- | --- | --- |
| A：继续用原版对话框 | 主动消息仍走 `DialogueBox` | 实现少 | 会继续打断玩家和 move，直接违背目标 |
| B：只做简单左上角文字 | 所有主动消息都走 HUD 文本 | 快 | 没有微信式线程和回复，交互体验不够 |
| C：复用 MobilePhone 外壳，改造成 Hermes 手机线程 | 手机外壳/素材/布局/点击/震动复用 MobilePhone，消息线程和 Agent 接入按 Hermes 重写 | 体验符合需求，少造轮子，可控 | 需要明确避开 MobilePhone 的电话/DialogueBox 链路 |

最终选 C。

## MobilePhone 复用审计表

| 类型 | 参考位置 | 本项目怎么用 |
| --- | --- | --- |
| 直接复用素材 | `MobilePhone/assets/phone_icon.png`、`assets/skins/*.png`、`assets/backgrounds/*.png` | 拷到 bridge 的资产目录，保留来源和授权说明 |
| 改造复用手机状态 | `MobilePhone/ModEntry.cs:25` 到 `MobilePhone/ModEntry.cs:53` | 参考 `phoneOpen`、`phoneRect`、`screenRect`、`phoneIconPosition`，落到唯一可变状态源 `HermesPhoneState` |
| 改造复用绘制 | `MobilePhone/PhoneVisuals.cs:28` 到 `MobilePhone/PhoneVisuals.cs:90` | 复用图标绘制、右侧手机外壳绘制、震动角度逻辑 |
| 改造复用输入命中 | `MobilePhone/PhoneInput.cs:29` 到 `MobilePhone/PhoneInput.cs:130` | 复用按钮监听、鼠标命中、`Helper.Input.Suppress(...)` 思路 |
| 改造复用布局 | `MobilePhone/PhoneUtils.cs:32` 到 `MobilePhone/PhoneUtils.cs:57`，`PhoneUtils.cs:92` 到 `PhoneUtils.cs:137` | 复用 `TogglePhone`、`RefreshPhoneLayout`、`GetPhonePosition` 思路 |
| 改造复用通知 | `MobilePhone/PhoneUtils.cs:207` 到 `MobilePhone/PhoneUtils.cs:220` | 新消息播放轻提示音，手机图标震动 |
| 禁止照搬 | `MobilePhone/MobilePhoneCall.cs:104` 到 `MobilePhone/MobilePhoneCall.cs:176` | 不用 `createQuestionDialogue` |
| 禁止照搬 | `MobilePhone/MobilePhoneCall.cs:210` 到 `MobilePhone/MobilePhoneCall.cs:257` | 不用 `DialogueBox`、`Game1.drawDialogue`、`activeClickableMenu` 通话链 |

执行时必须新增一份来源说明，例如 `Mods/StardewHermesBridge/assets/phone/NOTICE.md`，写清楚素材/代码来自授权参考项目。用户已说明取得作者授权；计划仍要求保留来源记录，方便以后维护。

## 新手机状态机

| 状态 | 屏幕表现 | 是否 `activeClickableMenu` | 是否占键盘 | 进入方式 | 退出方式 |
| --- | --- | --- | --- | --- | --- |
| `PhoneClosed` | 只显示右侧/角落手机图标；有新消息时震动/未读数 | 否 | 否 | 默认状态 | 点手机图标 |
| `PhoneIndicatorOnly` | 手机关闭但有未读提醒 | 否 | 否 | 收到远距离消息 | 点开手机或读完消息 |
| `PhoneThreadPassiveOpen` | 手机展开，能看联系人和聊天记录 | 否 | 否 | 点手机图标、远距离私聊打开 | 关闭手机、点输入框 |
| `PhoneReplyFocusActive` | 手机展开，输入框激活 | 否 | 是，只接管文字输入 | 点输入框 | 提交、Esc、关闭、点外部 |
| `VanillaDialogueOwnedByPlayer` | 原版 NPC 对话框 | 是，原版 | 原版控制 | 玩家主动点击 NPC | 原版对话结束 |

硬规则：

- `PhoneThreadPassiveOpen` 绝不能设置 `Game1.activeClickableMenu`。
- `PhoneThreadPassiveOpen` 绝不能设置 `Game1.keyboardDispatcher.Subscriber`。
- 只有 `PhoneReplyFocusActive` 能设置 `Game1.keyboardDispatcher.Subscriber`。
- 手机关闭或退出输入框时，如果 subscriber 是 Hermes 手机输入框，必须清空或恢复旧 subscriber。
- 鼠标点手机区域时，只 suppress 当前点击，避免点击穿透；不应冻结玩家移动。

### 单一权威状态源

手机 UI 必须只有一个共享状态对象作为唯一真值，命名为 `HermesPhoneState`。所有可变 UI 状态只存在这里。

如果实现时需要 `HermesPhoneController`，它只能是操作入口：读取/修改 `HermesPhoneState`，不能持有第二份 `UiOwner`、`FocusOwner`、线程可见性、未读数、输入焦点等可变状态。

`HermesPhoneState` 至少维护：

- `UiOwner`：`none`、`phone_overlay`、`vanilla_dialogue`。
- `FocusOwner`：`none`、`phone_text_input`、`vanilla_dialogue`。
- `VisibleThreadId` 和每个线程的未读数。
- `ReplyLifecycle`：`none`、`reply_displayed_phone`、`reply_displayed_bubble`、`reply_closed_pending_emit`。
- 当前输入框 subscriber 是否由 Hermes 手机持有。

`BridgeCommandQueue`、`ModEntry`、手机 overlay、气泡 overlay 都只能读写这个共享对象，不允许各自用 `Game1.activeClickableMenu` 或本地 bool 猜测 UI 状态。`CheckInterrupt(...)` 也读取这个对象判断 `UiOwner/FocusOwner`，手机 overlay 和手机输入框不应被伪装成 `dialogue_started`。

## 私聊 lease 和手机状态怎么对齐

这部分不能再当成“以后再说”，因为现有私聊后端已经依赖 session lease。

| 后端状态 | 手机状态 | lease 规则 |
| --- | --- | --- |
| `PendingOpen` | `PhoneThreadPassiveOpen` 可以被打开或标记线程 | `PrivateChatOrchestrator` 先拿 lease，拿不到不打开线程 |
| `AwaitingPlayerInput` | `PhoneThreadPassiveOpen` 或 `PhoneReplyFocusActive` | lease 保持，表示这轮私聊正在占用 NPC 对话会话 |
| 玩家点输入框 | `PhoneReplyFocusActive` | 不新拿 lease，只改变输入焦点 |
| 玩家提交回复 | `WaitingAgentReply` | lease 保持，直到 Agent 回复展示/关闭语义完成 |
| Agent 回复进手机或气泡 | `ShowingReply` / `WaitingReplyDismissal` | 不能靠 `DialogueBox` close 释放；由手机“已展示/已读/关闭回复”事件释放 |
| 玩家取消/关闭本轮私聊 | `PhoneThreadPassiveOpen` 或 `PhoneClosed` | 记录 cancelled 事件并释放 lease |

要新增的语义：

- `open_private_chat` 不再表示“菜单打开”，改成“手机线程已标记/打开”。
- `BridgeCommandModels.OpenPrivateChatData` 要从 `string NpcId, bool Opened` 扩展为至少包含 `ThreadId` 和 `OpenState`，例如 `thread_marked`、`thread_opened`、`focus_pending`。
- `src/games/stardew/StardewBridgeDtos.cs` 里的 `StardewOpenPrivateChatData` 必须同步扩展同样字段，不能让 bridge DTO 和 Stardew 客户端 DTO 不一致。
- bridge response 的 `Status` 或 metadata 也要写同一个状态，不能只有 `completed`。
- `StardewCommandService.SubmitOpenPrivateChatAsync(...)` 要把 `thread_marked`、`thread_opened`、`focus_pending` 都映射为 accepted success，不能只看旧的 `Opened == true`。
- `src/games/stardew/StardewPrivateChatOrchestrator.cs` 里的 `IsRetryableOpenFailure(...)` 要同步更新：`thread_marked`、`thread_opened`、`focus_pending` 绝不能被当作 retryable failure；`menu_blocked` 也要重新评估，因为手机 overlay 不再依赖菜单，旧的菜单阻塞不应成为常态重试理由。
- `PrivateChatOrchestrator.TrySubmitPendingOpenAsync(...)` 对 accepted-open 继续推进到 `AwaitingPlayerInput`；只有 world not ready、bridge down、lease failed 这类真实失败才重试或结束。
- `PrivateChatPolicy.ShouldRetryOpen` 需要更新：`thread_marked`、`thread_opened`、`focus_pending` 都不是失败，不应重试；只有 bridge 真的不可用或 world not ready 才重试。
- `private_chat_reply_closed` 事件不再从 `DialogueBox` 关闭推断，改由手机状态机在“回复已展示并被玩家关闭/已读”时发出。
- 近距离气泡 reply 的释放规则必须固定：气泡显示后进入 `reply_displayed_bubble`，气泡自然过期时由气泡 overlay 发出 `private_chat_reply_closed`，并记录 `reply_closed_source=bubble_expired`。如果本轮策略是 `ShouldEndAfterReply()`，过期时释放 lease；如果策略允许继续多轮，则过期事件驱动 reopen。
- 远距离手机 reply 的释放规则也要固定：消息进入线程后，如果线程当前打开并可见，标记为 read 后发 `private_chat_reply_closed`；如果线程没打开，首版不长期持有 lease，入队并记录通知后发 `private_chat_reply_closed`，字段 `reply_closed_source=phone_enqueued_unread`。以后若要“必须玩家已读后才继续”，另开持久会话计划。

## 主动文本入口路由矩阵

| 入口 | 当前问题 | 新路由 | 不允许 |
| --- | --- | --- | --- |
| `ExecuteSpeak` 普通主动说话 | 直接开 `DialogueBox` | 调用统一 `StardewMessageDisplayRouter` | 不允许 `NpcRawDialogueRenderer.Display` |
| `ExecuteSpeak` 的 `private_chat` 回复 | 回复展示会开 `DialogueBox` | 同样走 8 格路由：近距离气泡，远距离手机 | 不允许依赖 `DialogueBox` close |
| `ExecuteOpenPrivateChat` | 设置 `Game1.activeClickableMenu = inputMenu` | 更新手机线程状态，必要时打开 `PhoneThreadPassiveOpen` | 不允许创建 `PrivateChatInputMenu` |
| 手机输入提交 | 旧菜单本地记录事件 | 记录 `player_private_message_submitted`，继续进入 `PrivateChatOrchestrator -> ReplyAsync -> Agent` | 不允许只在 SMAPI 本地追加消息 |
| 玩家主动点击 NPC | 已有原版观察链路 | 保持 `ModEntry.OnButtonPressed` 原链路 | 不允许改成手机 |
| 自动 replay / follow-up | 可能走 speak | 统一经过显示路由 | 不允许绕过路由 |

## 8 格气泡怎么做

首版只走一个实现路径：**非菜单 overlay 气泡**。

- 不使用 `DialogueBox`。
- 不使用 `NpcRawDialogueRenderer.Display`。
- 不回退到 `Game1.drawDialogue`。
- 在 bridge UI 层新增类似 `NpcOverheadBubbleOverlay` 的组件，挂在已有 `RenderedWorld` / overlay 绘制链路里。
- 气泡位置按 NPC 世界坐标转屏幕坐标，显示在 NPC 头顶，过期时间首版 4 到 6 秒。
- 同一 NPC 短时间多条消息时，首版允许覆盖上一条；不要先做复杂队列。

## OpenAI 400 修复闭环

### 要保存哪些字段

在 `Message` 和 `ChatResponse` 中增加：

- `Reasoning`
- `ReasoningContent`
- `ReasoningDetails`
- `CodexReasoningItems`

字段可以先用 `string?` 保存原始 JSON 或原始文本。首版重点是“能原样回传 provider 要求字段”，不先做复杂结构化解析。

### OpenAI 响应解析

`OpenAiClient.CompleteWithToolsAsync` 要从 assistant message 解析：

- `reasoning_content`
- `reasoning`
- `reasoning_details`
- `codex_reasoning_items`

如果 content 为空，不要再简单把 reasoning 当 content 顶上去后丢掉原字段；要同时保存到 `ChatResponse`。

### Agent 保存路径

要改两条路径：

- assistant 最终文本：`AgentSessionWriter.AppendAssistantMessageAsync(...)` 支持传入 reasoning。
- assistant tool-call 请求：`AgentSessionWriter.AppendAssistantToolRequestMessageAsync(...)` 支持传入 reasoning。

`src/Core/Agent.cs` 里的普通 `ChatAsync` tool loop 和 `StreamChatAsync` tool loop 都要传，不只改一个分支。尤其是 `StreamChatAsync` 中直接 `new Message` 的 assistant final、assistant tool-call 保存点，必须收口到同一个 writer，或显式增加同样的 reasoning 字段赋值和测试。

### 落盘和加载

`SessionSearchIndex.InsertMessage(...)` 要写入已有列：

- `reasoning`
- `reasoning_content`
- `reasoning_details`
- `codex_reasoning_items`

`SessionSearchIndex.LoadMessages(...)` 要把这些列读回 `Message`。

`ReplaceSessionMessages(...)` 复用 `InsertMessage(...)`，但验收必须覆盖替换保存后仍能读回 reasoning。

`TranscriptStore.SaveMessageAsync(...)`、`LoadSessionAsync(...)`、legacy JSONL import、`ResumeManager` 恢复路径都必须纳入验收。不能只测 `SessionSearchIndex`，因为实际恢复会经过 transcript store 缓存和 resume 管理。

### 下一轮回放

`OpenAiClient.BuildPayload(...)` 在回放 assistant 消息时：

- 如果 `Message.Role == "assistant"` 且有 `ReasoningContent`，输出 `reasoning_content`。
- 如果 provider 需要 `reasoning` 或 `reasoning_details`，也原样输出。
- tool-call 消息尤其必须带回 reasoning，因为 OpenAI 报错发生在 thinking mode 的 tool-call 后续轮次。

### 400 日志

当 OpenAI 返回非成功状态时，日志必须有：

- status code
- provider/baseUrl/model
- 本地 `llmRequestId`
- provider request id，如果 response header 有 `x-request-id`、`openai-request-id`、`request-id` 或同类字段就记录
- response body 摘要
- 是否检测到 `reasoning_content` 相关错误

执行要求：

- `OpenAiClient.PostAsync(...)` 发请求前生成本地 `llmRequestId=req_llm_<guid>`，随 `reasoning_received`、`reasoning_replayed`、`openai_bad_request` 日志贯穿。
- 如果 provider 返回 request id header，就同时记录 `providerRequestId`。
- 没有上层 trace id 时，不要空着；用本地 `llmRequestId` 作为 LLM 层 correlation id。
- 不能只抛 `Bad Request`。

## 日志验收矩阵

| 日志事件 | 必填字段 | 说明 |
| --- | --- | --- |
| `reasoning_received` | `llmRequestId/providerRequestId/model/hasToolCalls/reasoningFieldNames` | 模型响应里收到了哪些 reasoning 字段；`providerRequestId` 来自 response headers，缺失允许为空 |
| `reasoning_persisted` | `sessionId/messageRole/hasToolCalls/reasoningFieldNames` | 已写入 transcript/search index |
| `reasoning_loaded_for_replay` | `sessionId/messageId/reasoningFieldNames` | 从历史消息加载回来 |
| `reasoning_replayed` | `llmRequestId/providerRequestId/reasoningFieldNames/hasToolCalls` | 下一轮 payload 已带回；请求尚未返回时 `providerRequestId` 可为空 |
| `openai_bad_request` | `llmRequestId/providerRequestId/statusCode/bodySnippet/model/baseUrl` | 400 排查入口；`providerRequestId` 来自 response headers，缺失允许为空 |
| `display_route_selected` | `traceId/requestId/conversationId/npcId/channel/sourceEntry/playerInitiated/sameLocation/distance/route` | 主动文本分流结果 |
| `overhead_bubble_shown` | `traceId/npcId/textLength/expiresMs` | 近距离气泡显示 |
| `phone_message_enqueued` | `traceId/conversationId/npcId/threadId/unreadCount` | 远距离消息入手机 |
| `phone_thread_opened` | `conversationId/npcId/threadId/sourceEntry` | 手机线程被打开或标记 |
| `ui_owner_changed` | `oldOwner/newOwner/reason` | 区分 `none/phone_overlay/vanilla_dialogue` |
| `focus_owner_changed` | `oldOwner/newOwner/reason` | 区分 `none/phone_text_input/vanilla_dialogue` |
| `input_focus_acquired` | `conversationId/npcId/threadId` | 玩家点进输入框 |
| `input_focus_released` | `conversationId/npcId/threadId/reason` | 提交、取消、关闭等 |
| `reply_submitted_to_agent` | `conversationId/npcId/textLength` | 手机回复进入 Agent |
| `reply_displayed_near` | `conversationId/npcId/distance` | Agent 回复走气泡 |
| `reply_displayed_phone` | `conversationId/npcId/threadId` | Agent 回复走手机 |
| `session_lease_acquired` | `conversationId/npcId/owner/reason/generation` | 私聊租约拿到 |
| `session_lease_released` | `conversationId/npcId/owner/reason/generation` | 私聊租约释放 |
| `session_lease_acquire_failed` | `conversationId/npcId/owner/reason` | 私聊租约失败 |
| `move_interrupt_reason` | `traceId/commandId/npcId/interruptedActionId/menuOwner/focusOwner/reason` | move 为什么中断 |

## 分阶段实施

### 阶段 1：锁住 OpenAI 400 回归测试

修改/新增测试：

- `Desktop/HermesDesktop.Tests` 或现有 LLM 测试目录中新增 OpenAI payload 测试。
- 覆盖“assistant 先返回 `tool_calls + reasoning_content`，保存后下一轮 payload 仍带 `reasoning_content`”。
- `SessionSearchIndex` 增加 reasoning roundtrip 测试：保存、加载、替换保存都不丢字段。
- `TranscriptStore` 增加 reasoning roundtrip 测试：`SaveMessageAsync -> LoadSessionAsync` 不丢字段，缓存命中也不丢。
- legacy JSONL import 增加新字段兼容测试。
- `ResumeManager` 或等价恢复路径增加包含 assistant tool-call reasoning 的恢复测试。

验收：

- 测试先能在当前代码上暴露缺口。
- 测试名写清楚是在防 OpenAI thinking mode 400。

### 阶段 2：实现 reasoning 保存和回放

修改文件：

- `src/Core/Models.cs`
- `src/LLM/OpenAiClient.cs`
- `src/Core/AgentLoopScaffold.cs`
- `src/Core/Agent.cs`
- `src/search/SessionSearchIndex.cs`
- `src/transcript/TranscriptStore.cs`
- `src/transcript/TranscriptStore.cs` 中的 `ResumeManager`
- 如 transcript store 还有 JSON 序列化路径，也一起确认不丢新字段。

验收：

- 单测通过。
- 构造一次带 tool call 的 assistant 历史消息，`BuildPayload` 输出里能看到 `reasoning_content`。
- 模拟 400 时日志能看到 body 摘要。
- `ChatAsync` 和 `StreamChatAsync` 两条路径都能保存 assistant final 和 assistant tool-call reasoning。
- 从 `TranscriptStore.LoadSessionAsync(...)` 恢复后再进入 `BuildPayload(...)`，reasoning 仍能回放。

### 阶段 3：抽出主动文本统一路由

新增或修改：

- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- 新增 `Mods/StardewHermesBridge/Ui/StardewMessageDisplayRouter.cs` 或同等职责类。
- 新增 `Mods/StardewHermesBridge/Ui/NpcOverheadBubbleOverlay.cs`。
- 修改 `Mods/StardewHermesBridge/ModEntry.cs` 的 overlay 绘制挂载。

规则：

- `ExecuteSpeak` 不再直接调用 `NpcRawDialogueRenderer.Display(...)`。
- `channel=private_chat` 的 Agent 回复也走同一分流。
- 8 格内用非菜单气泡。
- 8 格外或不同地图写入手机线程。
- 玩家主动点击 NPC 的原版链路不动。

验收：

- 单测覆盖 route 选择：同图 8 格内、同图 9 格、异图、NPC 不存在、world not ready。
- 手测 Haley move 时，如果 Agent 主动说话，不出现 `dialogue_started` 中断。

### 阶段 4：用 MobilePhone 改造 Hermes 手机 overlay

新增或改造：

- `Mods/StardewHermesBridge/Ui/HermesPhoneState.cs`
- `Mods/StardewHermesBridge/Ui/HermesPhoneOverlay.cs`
- `Mods/StardewHermesBridge/Ui/HermesPhoneInput.cs`
- `Mods/StardewHermesBridge/Ui/HermesPhoneThread.cs`
- 可选 `Mods/StardewHermesBridge/Ui/HermesPhoneController.cs`，但只能作为 `HermesPhoneState` 的操作入口，不能保存第二份状态
- `Mods/StardewHermesBridge/assets/phone/...`
- `Mods/StardewHermesBridge/assets/phone/NOTICE.md`

复用要求：

- 优先搬/改 MobilePhone 的手机皮肤、背景、图标。
- 优先搬/改 MobilePhone 的 `phoneRect/screenRect` 布局算法。
- 优先搬/改 MobilePhone 的图标震动和点击命中逻辑。
- 优先搬/改 MobilePhone 的 `Helper.Input.Suppress(...)` 使用方式。

禁止：

- 不创建新的 `IClickableMenu` 作为手机主 UI。
- 不设置 `Game1.activeClickableMenu`。
- 不使用 `MobilePhoneCall` 的电话状态机。
- 不使用 `createQuestionDialogue`、`Game1.DrawDialogue`、`Game1.drawDialogue`。

验收：

- 手机关闭有未读震动/未读数。
- 手机打开后玩家仍能移动。
- 手机打开但没点输入框时，键盘不被占用。
- 点输入框后能输入，提交/关闭后释放焦点。

### 阶段 5：改私聊打开、提交、回复关闭事件

修改：

- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/game/core/PrivateChatOrchestrator.cs`
- `src/game/core/PrivateChatContracts.cs`
- `Mods/StardewHermesBridge/ModEntry.cs`

关键点：

- `OpenPrivateChat` 改为“打开/标记手机线程”，不再创建 `PrivateChatInputMenu`。
- `BridgeCommandModels.OpenPrivateChatData` 和 `StardewBridgeDtos.StardewOpenPrivateChatData` 同步扩展 `ThreadId/OpenState`。
- bridge response 的 `Status` 或 metadata 同步返回同一个 `OpenState`，取值首版只允许 `thread_marked`、`thread_opened`、`focus_pending`。
- `StardewCommandService.SubmitOpenPrivateChatAsync(...)` 不再只用旧 `Opened` 判断，必须识别 `OpenState`，这三个状态都映射为 accepted success。
- `StardewPrivateChatOrchestrator.IsRetryableOpenFailure(...)` 必须同步识别新契约：`thread_marked/thread_opened/focus_pending` 不是失败，不重试；只有 world not ready、bridge 暂不可用等真实暂态失败才重试。
- `PrivateChatOrchestrator.TrySubmitPendingOpenAsync(...)` 对这些成功状态推进到 `AwaitingPlayerInput`。
- 手机提交回复时记录 `player_private_message_submitted`，字段兼容现有 `PrivateChatPolicy`。
- 手机取消时记录 `player_private_message_cancelled`。
- Agent 回复显示后，由手机状态机或气泡 overlay 发 `private_chat_reply_closed` 或新的等价事件，不能再依赖 `DialogueBox` 关闭。
- 近距离气泡 reply 在气泡自然过期时发 `private_chat_reply_closed`，并按当前私聊策略释放或 reopen lease。
- 远距离手机 reply 首版在入队/当前线程已读后发 `private_chat_reply_closed`，避免玩家不打开手机导致 lease 长期悬挂。
- `ShouldRetryOpen` 不要把 `thread_marked` 当失败。
- `IsRetryableOpenFailure(...)` 不要把新手机线程成功状态或旧 `menu_blocked` 惯性当成重试入口。
- lease 获取、释放、失败都要打日志。
- 重写 `RawDialogueDisplayRegressionTests` 里锁定旧行为的断言：
  - 删除/改写要求 `new PrivateChatInputMenu` 的断言，改成要求 `HermesPhoneState` / `HermesPhoneOverlay` / 手机线程状态。
  - 删除/改写要求 `Game1.activeClickableMenu = ...` 的断言，改成断言手机打开不设置 `activeClickableMenu`。
  - 删除/改写要求 `PrivateChatInputMenu : IClickableMenu` 的断言，改成断言手机 overlay 不是 `IClickableMenu` 主菜单。
  - 把旧菜单 portrait shell / wrapped input 断言迁移到 `HermesPhoneOverlay` 和 `HermesPhoneInput`。
  - 把 `private_chat_reply_closed` 断言改成来自手机状态机或气泡 overlay，而不是 Stardew `DialogueBox` 离场。

验收：

- 玩家手机回复能进入 `PrivateChatOrchestrator -> ReplyAsync -> StardewNpcPrivateChatAgentRunner`。
- Agent 回复回流后仍按 8 格规则显示。
- 关闭手机或取消本轮私聊时 lease 会释放。
- 近距离气泡回复过期后会释放 lease 或触发下一轮 reopen，不会让 NPC autonomy 长期暂停。

### 阶段 6：中断逻辑白名单和手动验收

修改：

- `BridgeCommandQueue.CheckInterrupt(...)` 或其调用处增加 UI/focus owner 判断。

规则：

- `VanillaDialogueOwnedByPlayer` 可以中断 move。
- `phone_overlay`、`phone_text_input`、`overhead_bubble` 不应被当成 `dialogue_started`。
- 如果未来确实需要“输入时暂停 NPC move”，必须另设明确 reason，不能伪装成 `dialogue_started`。

手动验收：

- Haley move 过程中收到远距离消息：手机震动，move 不因 `dialogue_started` 中断。
- Haley move 过程中同地图 8 格内说话：头顶气泡，move 不因 `dialogue_started` 中断。
- 玩家主动点击 Haley：原版对话框正常出现。
- 手机打开但未输入：玩家可以继续移动。
- 手机输入框激活：能打字提交；提交后键盘释放。
- 手机回复进入 Agent，Agent 回复按近/远规则显示。
- `RawDialogueDisplayRegressionTests` 不再锁定旧的 `PrivateChatInputMenu` / `activeClickableMenu` / `DialogueBox` close 行为，而是锁定新 overlay、输入焦点和 reply close 事件源。
- OpenAI thinking/tool-call 场景不再出现 `reasoning_content` 缺失 400。

## 自动化测试清单

- `OpenAiClient` payload 测试：assistant tool-call history 带 `reasoning_content` 时下一轮 payload 保留。
- `OpenAiClient` response parse 测试：`reasoning_content/reasoning/reasoning_details/codex_reasoning_items` 能进入 `ChatResponse`。
- `AgentSessionWriter` 测试：assistant 普通消息和 tool-call 消息都保存 reasoning。
- `SessionSearchIndex` 测试：reasoning 字段 save/load/replace roundtrip。
- bridge 路由测试：8 格内、8 格外、异图、玩家主动标记。
- phone 状态机测试：`PhoneThreadPassiveOpen` 不占键盘，`PhoneReplyFocusActive` 才占键盘，退出释放。
- private chat 测试：`open_private_chat` 的 `thread_marked/thread_opened` 被视为成功，不触发无意义重试。
- move 中断测试：phone overlay 和气泡不产生 `dialogue_started`。
- `StreamChatAsync` 测试：流式 assistant final 和流式 assistant tool-call 都能保存 reasoning，恢复后可回放。
- `OpenPrivateChat` 契约测试：bridge 返回 `thread_marked/thread_opened/focus_pending` 时，`StardewCommandService` 映射为 accepted，`PrivateChatOrchestrator` 进入 `AwaitingPlayerInput`。
- `StardewPrivateChatOrchestrator` 重试测试：`IsRetryableOpenFailure(...)` 不会对 `thread_marked/thread_opened/focus_pending` 触发重试。
- 气泡 reply lease 测试：近距离 private_chat 回复走气泡，气泡过期后发 close 事件，lease 不悬挂。
- 旧回归测试迁移测试：`RawDialogueDisplayRegressionTests` 明确禁止 `OpenPrivateChat` 创建 `PrivateChatInputMenu` 或写 `Game1.activeClickableMenu`，并要求 reply close 来源是 `HermesPhoneState` / 气泡 overlay。

建议命令：

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
```

## 不做范围

- 不做手机消息跨存档/重启持久化。
- 不做表情、图片、语音、群聊、搜索。
- 不做真实手机打开动画。
- 不把所有原版游戏对话改成手机。
- 不解决完整 NPC 移动控制租约。这里仅对齐“私聊 session lease 与手机 UI 状态”，移动控制租约继续留在 NPC 移动计划里。

## 历史旧代码退役清单

执行时不要在这些旧链路上继续加补丁；要么删除，要么改成只服务仍然合法的原版链路。

| 旧代码/旧行为 | 退役方式 | 原因 |
| --- | --- | --- |
| `PrivateChatInputMenu` | 手机 overlay 接管后删除；如果短期保留，只允许测试迁移期存在，不允许生产路径调用 | 它继承 `IClickableMenu`，会走菜单输入模型，和“手机打开不打断行动”冲突 |
| `ExecuteOpenPrivateChat` 里的 `new PrivateChatInputMenu` | 必须删除调用点 | 私聊打开应变成 `HermesPhoneState` 线程状态，不再打开菜单 |
| `ExecuteOpenPrivateChat` 里的 `Game1.activeClickableMenu = inputMenu` | 必须删除调用点 | 手机展开不能占用 `activeClickableMenu` |
| `PrivateChatInputMenu` 的构造时抢键盘 | 必须退役 | 新规则是打开手机不占键盘，只有 `PhoneReplyFocusActive` 才接管输入 |
| `private_chat_reply_closed` 依赖 `DialogueBox` 离场 | 必须退役 | 回复关闭事件改由手机状态机或气泡 overlay 产生 |
| `BridgeStatusOverlay` 中只服务旧私聊等待/思考 HUD 的文案状态 | 手机线程上线后合并或删除 | 等待、思考、未读都应进入手机线程/通知，不应再维护第二套私聊提示状态 |
| `NpcRawDialogueRenderer.Display(...)` 用于 Hermes/NPC 主动文本 | 必须退役 | 主动文本统一走 8 格路由，不能开全局对话框 |
| `NpcRawDialogueRenderer.Display(...)` 用于玩家主动点击原版对话 | 不适用，不应该接管 | 玩家主动点击继续由原版游戏链路负责，不由 Hermes raw renderer 代替 |
| `RawDialogueDisplayRegressionTests` 里锁定旧菜单的断言 | 必须重写 | 测试要锁定新行为：overlay、不占键盘、reply close 新事件源 |

退役验收：

- `OpenPrivateChat` 生产路径里搜不到 `new PrivateChatInputMenu`。
- `OpenPrivateChat` 生产路径里搜不到 `Game1.activeClickableMenu = inputMenu`。
- Hermes/NPC 主动文本路径里搜不到 `NpcRawDialogueRenderer.Display(...)`。
- 新手机 overlay 测试明确证明：手机打开不占键盘，点输入框才占键盘，关闭/提交后释放。
- 旧文件如果暂时没删，必须没有生产引用，并在计划执行记录里写明“待删原因”和“删除前置条件”。

## 风险和处理

| 风险 | 处理 |
| --- | --- |
| provider 字段名不止 `reasoning_content` | 同时保存 `reasoning/reasoning_content/reasoning_details/codex_reasoning_items` 原始值 |
| 手机 overlay 点击穿透到游戏 | 只在手机区域 suppress 当前鼠标事件 |
| 手机打开后误占键盘 | 状态机强制：只有 `PhoneReplyFocusActive` 能接管键盘 |
| 私聊 lease 不释放，NPC autonomy 长期暂停 | 手机 submit/cancel/reply closed 都要对应释放路径和日志 |
| MobilePhone 代码版本较旧 | 只复用稳定的绘制/布局/输入思路，避免照搬旧电话状态机 |
| 近距离气泡挡住 UI | 首版短时显示，可覆盖同 NPC 上一条消息，不做复杂堆叠 |

## ADR

### 决定

采用“OpenAI reasoning 闭环修复 + 主动文本统一路由 + MobilePhone 改造手机 overlay”的方案。

### 驱动

- OpenAI 400 是 Agent 回复链路的阻断问题。
- 全局 `DialogueBox` 会打断玩家行动和 NPC move。
- 用户已授权 MobilePhone 参考项目，且该项目已有手机视觉和输入基础。

### 放弃的方案

- 放弃继续用 `DialogueBox` 展示主动文本：会继续触发 `dialogue_started`。
- 放弃只做简单 HUD 文本：无法满足微信式联系人、线程和回复。
- 放弃从零写手机 UI：MobilePhone 已经有可复用资产和布局，重写没有收益。
- 放弃照搬 MobilePhone 电话链路：它依赖 `createQuestionDialogue`、`DialogueBox`、`activeClickableMenu`，与“不打断行动”冲突。

### 后果

- 需要一次性改到 OpenAI、Agent 保存、bridge UI、私聊编排多个边界。
- 手机 overlay 会比旧 `PrivateChatInputMenu` 稍复杂，但能保留玩家行动。
- 私聊 reply close 语义要从 `DialogueBox` 生命周期迁到手机状态机。

### 后续

- 本计划完成后，再回到 `hold/控制租约` 的完整移动控制设计。
- 手机消息持久化、搜索、表情等以后另开计划。

## 可用 agent 类型建议

| agent 类型 | 用途 |
| --- | --- |
| `explore` | 快速查文件、符号、现有测试位置 |
| `architect` | 复审 OpenAI 回放、手机状态机、lease 边界 |
| `executor` | 分阶段实现代码 |
| `test-engineer` | 设计和补自动化测试 |
| `verifier` | 对照日志和验收清单确认完成 |
| `code-reviewer` | 最终代码审查 |

## Ralph / Team 执行建议

如果走 `$ralph`：

- 一个主执行 agent 按阶段顺序推进。
- 每完成一个阶段先跑对应测试。
- OpenAI 400 阶段没过，不进入手机 UI 阶段。

如果走 `$team`：

- lane 1：OpenAI reasoning 闭环，负责人 `executor`。
- lane 2：Stardew 主动文本路由和气泡，负责人 `executor`。
- lane 3：MobilePhone 复用审计和手机 overlay，负责人 `executor` 或 `designer`。
- lane 4：测试和日志验收，负责人 `test-engineer`。
- 最后由 `verifier` 串联手动验收日志，确认没有 `dialogue_started` 误中断。

建议启动提示：

```text
$ralph 按 .omx/plans/星露谷手机消息与OpenAI400修复计划.md 执行，先修 OpenAI 400，再做主动文本路由，最后做手机 overlay。
```

```text
$team 按 .omx/plans/星露谷手机消息与OpenAI400修复计划.md 分四路执行：OpenAI reasoning、Stardew 路由、MobilePhone overlay、测试日志验收。
```

## 完成定义

- OpenAI thinking/tool-call 后续轮次不再因缺少 `reasoning_content` 报 400。
- 主动文本不再打开全局 `DialogueBox`。
- Haley move 不会被 Hermes 主动文本造成的 `dialogue_started` 中断。
- 玩家主动点 NPC 仍是原版对话。
- 8 格内头顶气泡，8 格外或异图手机消息。
- 手机打开不占键盘，只有输入框激活时占键盘。
- 手机回复能进入 Hermes Agent，并收到 Agent 回复。
- 日志能串起 OpenAI reasoning、显示路由、手机状态、私聊 lease、move 中断原因。

## 本轮计划改动记录

- 把用户新增要求“尽量使用 MobilePhone，避免重造轮子”升级为硬约束。
- 把“手机打开不占键盘，输入时才占键盘”写入状态机和验收。
- 把架构复审指出的 `reasoning_content` 保存/加载/回放闭环补完整。
- 把私聊 session lease 纳入本计划，不再简单丢到后续。
- 明确近距离气泡只走非菜单 overlay，不保留 `DialogueBox` 回退。
- 把历史旧代码退役清单补入计划，避免继续维护 `PrivateChatInputMenu`、`activeClickableMenu` 私聊打开、`DialogueBox` reply close 等旧链路。
