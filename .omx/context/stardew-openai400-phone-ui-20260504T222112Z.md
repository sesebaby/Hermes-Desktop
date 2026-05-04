# Stardew OpenAI 400 和手机消息 UI 上下文快照

## 任务

用户手动测试后发现两个问题：

1. OpenAI `400 Bad Request` 需要彻底检查。
2. NPC 私聊和主动说话不应打断玩家行动。用户倾向在屏幕右侧做一个类似手机/微信的消息 UI：来消息时手机提示或振动，玩家主动点开后看各个 NPC 的消息。

## 期望结果

先通过 deep-interview 把产品边界问清楚，再进入计划或实施。

## 当前证据

- `bridge.jsonl` 最新 Haley 长距离 move 有一条被 `dialogue_started` 打断。
- 同一路线早前成功完成过，因此最新失败更像全局对话框打断，不是路线不可达，也不是 Haley 原版日程必然抢控制。
- `BridgeCommandQueue.ExecuteSpeak` 当前走 `NpcRawDialogueRenderer.Display(...)`，底层会打开 `Game1.DrawDialogue(...)`。
- `BridgeCommandQueue.CheckInterrupt()` 会把 `Game1.activeClickableMenu is DialogueBox` 判为 `dialogue_started`，从而中断 move。
- `PrivateChatInputMenu` 当前是 `IClickableMenu` 居中输入菜单，会占用 `Game1.activeClickableMenu`，不适合“不打断玩家行动”的目标。
- `BridgeStatusOverlay` 已有非菜单 overlay 的绘制模式，可作为右侧手机 UI 的参考方向。
- Hermes 日志中 OpenAI 400 body 已可见：`reasoning_content in the thinking mode must be passed back to the API`。
- `OpenAiClient.BuildPayload(...)` 目前只回传 `role/content/tool_calls/tool_call_id`，没有承载或回传 `reasoning_content`。

## 已知约束

- 用户要求中文白话，不要假设历史经验一定正确。
- 优先依照当前源码证据和 TheStardewSquad 参考实现。
- 不直接永久清 NPC 原版日程；控制租约/hold 是待解决设计问题。
- 开始改动前需看 `git status`，不要回退用户已有改动。

## 待澄清

- 手机 UI 首版要替代哪些行为：私聊回复、NPC 主动搭话、自动 speak、还是所有非玩家主动触发的对话？
- 哪些行为仍必须保留原版全局 `DialogueBox`？
- OpenAI 400 与手机 UI 是同一计划内一起修，还是先把 400 当阻断 bug 单独修？
- 手机 UI 是否只做 SMAPI bridge 层，还是也要改 Hermes 工具语义、prompt、日志、测试？
- 首版是否需要持久历史、联系人列表、未读数、震动动画、输入框、消息分类等。

## 已澄清边界

- 玩家主动点击 NPC 时，继续走原版对话框。
- Hermes/NPC 主动发文本时，如果玩家和 NPC 在同一地图且距离不超过 8 格，走 NPC 头顶气泡，不打开全局对话框。
- Hermes/NPC 主动发文本时，如果玩家和 NPC 不在同一地图，或同地图距离超过 8 格，走右侧手机/微信式消息。
- 首版“视野内”不考虑 NPC 朝向，只按 8 格距离判断。
- 手机/微信式消息首版必须允许玩家在手机里回复 NPC；只做收件箱不够。
- 手机 UI 采用更贴近微信的版本：联系人列表、聊天线程、底部输入框。
- 手机关闭时不打断玩家行动；手机打开并输入回复时，可以短暂占用键盘输入，关闭后恢复正常游玩。
- 玩家在手机聊天页发送的回复，必须进入 Hermes Agent，按“给对应 NPC 的私聊消息”处理。
- NPC/Agent 对手机回复产生的后续文本，仍按 8 格规则分流：近距离头顶气泡，远距离手机消息。
- 方案中的关键节点必须添加日志，方便后续手动测试后定位问题。
- 实施优先级已确认：
  1. 先彻底修 OpenAI `400 Bad Request`，因为它会阻断 Agent 回复链路。
  2. 再改 NPC 文本展示分流：主动点击走原版对话框，NPC 主动文本按 8 格规则走头顶气泡或手机消息。
  3. 最后做右侧微信式手机 UI，并让玩家手机回复进入 Hermes Agent。
- 手机 UI 实现前必须参考 `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone`。
- 用户已说明取得作者授权，可以使用 MobilePhone 参考项目的代码和素材；实施时仍需保留来源/授权记录。
- 手机 UI 尽量使用 MobilePhone 的现成实现，避免自己造轮子：优先复用/改造手机状态、绘制、布局、图标/皮肤素材、点击命中、通知/震动等部分。
- MobilePhone 中会打开 `DialogueBox` / `activeClickableMenu` 的通话逻辑与本项目“不打断行动”的目标冲突，不能照搬；应改造成非模态手机线程和短暂输入焦点。

## 已确认不做

- 首版不做手机消息跨存档/重启后的持久保存，先只保留本局运行内消息。
- 首版不做复杂微信功能，例如表情、图片、语音、群聊、搜索。
- 首版不做真实手机打开动画，只做右侧入口轻微震动/闪烁。
- 不改变玩家主动点击 NPC 的原版对话逻辑。
- 不直接永久清 NPC 日程；`hold/控制租约` 仍作为移动可靠性的待解决问题单独处理。
- 不把所有原版游戏对话都改成手机消息，只处理 Hermes/NPC 主动触发的文本。
