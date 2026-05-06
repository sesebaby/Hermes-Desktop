# Stardew OpenAI 400 和手机消息 UI 白话规格

## 元信息

- 类型：brownfield，基于现有 Hermes Desktop + Stardew SMAPI bridge。
- 上下文快照：`.omx/context/stardew-openai400-phone-ui-20260504T222112Z.md`
- 访谈记录：`.omx/interviews/stardew-openai400-phone-ui-20260504T223158Z.md`
- 最终模糊度：约 18%，已满足继续形成计划的条件。

## 为什么要做

现在 NPC 私聊或主动说话会打开全局对话框。这个体验不好：玩家正在行动时会被打断，NPC move 也可能因为 `DialogueBox` 被判定为 `dialogue_started` 而中断。

同时，OpenAI `400 Bad Request` 已经出现在 Hermes 日志里，错误指向 `reasoning_content` 没有按 thinking mode 要求传回 API。这个问题会阻断 Agent 回复链路，所以必须先修。

## 想要的结果

玩家主动找 NPC 聊天时，还是原版星露谷对话体验。

NPC 或 Hermes 主动发文本时，不再强行弹全局对话框：

- 近距离：NPC 头顶冒气泡。
- 远距离：手机收到微信式消息。
- 玩家可以打开手机，选 NPC，在聊天页回复。
- 玩家在手机里发出的回复必须进入 Hermes Agent，而不是只存在 SMAPI 本地。

## 范围内

1. OpenAI `400 Bad Request` 修复
   - 检查 Hermes OpenAI 请求/响应链路。
   - 保存并回传 API 要求的 thinking/reasoning 内容。
   - 让后续工具调用轮次不会因为缺少 `reasoning_content` 失败。

2. NPC 文本展示分流
   - 玩家主动点击 NPC：保留原版 `DialogueBox`。
   - Hermes/NPC 主动发文本：不直接打开全局 `DialogueBox`。
   - 同地图且距离不超过 8 格：显示 NPC 头顶气泡。
   - 不同地图或距离超过 8 格：进入手机消息。

3. 右侧手机/微信式 UI
   - 屏幕右侧有手机入口。
   - 有新消息时轻微震动/闪烁，显示未读数。
   - 点开后显示联系人列表、当前 NPC 聊天线程、底部输入框。
   - 手机关闭时不打断玩家行动。
   - 手机打开并输入时，可以短暂占用键盘；关闭后恢复正常游玩。
   - 玩家发送的消息进入 Hermes Agent，按“给对应 NPC 的私聊消息”处理。

4. 关键日志
   - 方案中的关键节点必须打日志，方便后续根据 Hermes 日志、SMAPI 日志、bridge JSONL 日志检查问题。
   - OpenAI 请求失败时必须保留状态码和 response body。
   - NPC 文本展示分流时必须记录 NPC、距离、同地图判断、最终展示通道。
   - 手机消息收取、打开、发送、投递到 Agent、Agent 回复回流时都要有可关联的日志字段。
   - move 被中断时要能看出是否由全局对话、手机 UI、气泡展示或其他原因触发。

## 不做什么

- 首版不做消息跨存档/重启后的持久保存。
- 首版不做表情、图片、语音、群聊、搜索。
- 首版不做真实手机打开动画，只做入口轻微震动/闪烁。
- 不改变玩家主动点击 NPC 的原版对话逻辑。
- 不直接永久清 NPC 日程；`hold/控制租约` 是移动可靠性的后续问题。
- 不把所有原版游戏对话都改成手机消息，只处理 Hermes/NPC 主动触发的文本。

## 已确认规则

- “视野内”首版只按距离判断：同地图 8 格内算近距离。
- 不考虑 NPC 朝向。
- 手机回复必须进入 Hermes Agent。
- Agent 后续回复仍按同一套 8 格规则显示：近距离气泡，远距离手机。
- 手机 UI 实现前必须参考 `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone`。
- 用户已取得作者授权，可以使用该参考项目的代码和素材；实施计划仍要记录来源/授权。
- 手机 UI 尽量使用 MobilePhone 的现成实现，避免自己造轮子：优先复用/改造手机状态、绘制、布局、图标/皮肤素材、点击命中、通知/震动等部分。
- MobilePhone 中会打开 `DialogueBox` 或依赖 `activeClickableMenu` 的通话逻辑与本项目“不打断行动”的目标冲突，不能照搬；应改造成非模态手机线程和短暂输入焦点。

## 接受标准

- OpenAI 400 的错误 body 不再出现同类 `reasoning_content` 缺失错误。
- Agent 发生工具调用后的下一轮请求能正常继续。
- Hermes/NPC 主动说话不会打开全局 `DialogueBox`。
- Haley 这类 NPC move 不会因为 Hermes 主动文本展示而被 `dialogue_started` 打断。
- 玩家主动点击 NPC 时仍看到原版对话框。
- 同地图 8 格内的主动文本以头顶气泡展示。
- 8 格外或不同地图的主动文本进入手机消息。
- 手机 UI 有联系人、聊天线程、未读提示和输入发送。
- 手机发送的玩家回复能进入 Hermes Agent，并能触发 NPC 回复。
- 关键链路日志足够定位问题：OpenAI 400、文本分流、手机收发、Agent 投递、move 中断原因都能从日志看出来。

## 主要代码触点

- `src/LLM/OpenAiClient.cs`
- `src/Core/Models.cs`
- `src/Core/Agent.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Ui/PrivateChatInputMenu.cs`
- `Mods/StardewHermesBridge/Ui/BridgeStatusOverlay.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`

## 证据和推断

- 证据：bridge 日志中 Haley move 被 `dialogue_started` 中断。
- 证据：同一长路线早前成功完成，说明路线本身不是主要问题。
- 证据：当前 speak 路径会打开全局对话框。
- 证据：OpenAI 日志中明确要求 thinking mode 回传 `reasoning_content`。
- 推断：只要 Hermes 主动文本继续走全局 `DialogueBox`，move 仍可能被文本展示打断。
- 推断：手机 UI 如果只做 SMAPI 本地消息，不进入 Agent，会破坏用户要的 NPC 交互闭环。

## 后续建议

下一步进入计划阶段，按已确认优先级拆成三段：

1. 修 OpenAI 400。
2. 做 NPC 主动文本分流。
3. 做右侧微信式手机 UI，并把回复接入 Hermes Agent。

每一段都要同步设计日志点，不能等出问题后再补日志。
