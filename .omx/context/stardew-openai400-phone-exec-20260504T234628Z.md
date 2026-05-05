# Ralph 执行快照：星露谷手机消息与 OpenAI 400

## 任务

执行已确认的《星露谷手机消息与 OpenAI 400 修复计划》。

## 目标结果

- OpenAI reasoning/thinking 模型在 tool call 后续轮次不再因为缺少 reasoning 字段触发 `400 Bad Request`。
- Hermes/NPC 主动文本不再打开全局对话框打断玩家行动。
- 近距离主动消息走 NPC 头顶气泡，远距离主动消息走右侧手机消息。
- 玩家能在手机里回复 NPC，回复进入 Hermes Agent。
- 关键节点有日志，方便后续用手测日志定位问题。

## 已知证据

- `.omx/plans/星露谷手机消息与OpenAI400修复计划.md` 是当前批准计划。
- `src/Core/Models.cs` 的 `Message` 和 `ChatResponse` 还没有 reasoning 字段。
- `src/LLM/OpenAiClient.cs` 解析响应时只把 `reasoning` 兜底成 content，没有保存 reasoning 字段；构造 payload 时也不回放 `reasoning_content`。
- `src/search/SessionSearchIndex.cs` 的 schema 已有 reasoning 相关列，但读取和写入路径未完整使用。
- `src/Core/Agent.cs` 和 `src/Core/AgentLoopScaffold.cs` 有保存 assistant/tool-call 消息的路径，需要带上 reasoning。
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs` 仍有 `NpcRawDialogueRenderer.Display(...)` 和 `Game1.activeClickableMenu = inputMenu` 的旧路。
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs` 里有锁定旧菜单行为的测试，手机 overlay 实现时必须重写这些断言。

## 约束

- 先 RED 测试，再生产代码。
- 不回退用户已有改动。
- 手工编辑用 `apply_patch`。
- 不永久清 NPC 原版日程。
- 不用 `Game1.warpCharacter(...)` 或直接设置 `npc.controller` 伪造移动完成。
- 手机打开不设置 `Game1.activeClickableMenu`，不占键盘；只有输入框聚焦时接管键盘。
- 玩家主动点击 NPC 必须保留原版对话框。
- 优先复用授权参考项目 `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone` 的素材、布局、命中和震动思路。

## 历史错误经验

- 不绕过 bridge 移动控制器直接控制 NPC。
- 不用 warp 当作跨地图移动完成。
- 不用旧全局 UI shortcut 假装用户流程完成。
- 测试必须覆盖“不再出现坏生产路径”，例如不再主动打开 `DialogueBox` 或 `activeClickableMenu`。

## 未知点

- OpenAI 400 的真实响应 header 是否包含 provider request id，需要实现兼容多个常见 header。
- 手机 UI 首版需要复用多少 MobilePhone 素材，执行时按最小可用闭环选择。
- 气泡生命周期和私聊 lease 释放需要通过 tests 固定，避免再次依赖 `DialogueBox` close。

## 主要触点

- `src/Core/Models.cs`
- `src/LLM/OpenAiClient.cs`
- `src/Core/AgentLoopScaffold.cs`
- `src/Core/Agent.cs`
- `src/search/SessionSearchIndex.cs`
- `src/transcript/TranscriptStore.cs`
- `Desktop/HermesDesktop.Tests/LLM`
- `Desktop/HermesDesktop.Tests/Services`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Ui`
- `Mods/StardewHermesBridge.Tests`
