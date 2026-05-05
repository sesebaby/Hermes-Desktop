# 星露谷手机消息与 OpenAI 400 修复计划

更新时间：2026-05-05

## 2026-05-05 修正：玩家点击 NPC 的私聊入口不是手机

本计划早期版本把 `open_private_chat` 改成手机线程，这是错误需求理解，已经被代码历史和用户反馈否定。

必须严格执行下面的边界：

1. **玩家点击 NPC 后的私聊入口**：
   - 玩家点击 NPC 后，Bridge 先观察原版 `DialogueBox` 生命周期。
   - Desktop/core 若决定继续私聊，会调用 `/action/open_private_chat`。
   - Bridge 必须打开 Hermes 原有私聊输入窗口：`new PrivateChatInputMenu(...)`。
   - 该窗口是一个真实 `IClickableMenu`，必须通过 `Game1.activeClickableMenu = inputMenu` 显示。
   - 成功回执使用 `OpenState = "input_menu_opened"`。
   - 该输入窗口提交的事件必须带 `source = "input_menu"`；Agent 回复必须显示为原版样式 `DialogueBox`，即使 NPC 在 8 格以内也不能改成气泡。
2. **手机 overlay 的职责**：
   - 手机只处理 Hermes/NPC 主动消息、远距离消息、消息历史和手机内回复。
   - 手机 overlay 不设置 `Game1.activeClickableMenu`。
   - 手机打开但没有点输入框时不占键盘，不阻塞 NPC autonomy。
   - 手机输入提交的事件必须带 `source = "phone_overlay"`；这类回复如果同地图 8 格内才走头顶气泡，远程则进入手机消息。
3. **禁止再执行的旧结论**：
   - 不得删除 `PrivateChatInputMenu`。
   - 不得把 `/action/open_private_chat` 路由到 `_phoneState.OpenThread(...)`。
   - 不得让 `thread_marked`、`thread_opened`、`focus_pending` 代表 `/action/open_private_chat` 成功。
   - 不得把“手机不占 `activeClickableMenu`”误套到玩家点击 NPC 的私聊输入窗口上。

一句话：**点击 NPC 弹私聊输入窗口；手机是远程/主动消息窗口。**

## 目标

本计划只处理两个问题：

1. OpenAI thinking/reasoning 模型返回工具调用后，下一轮请求不再因为缺少 `reasoning_content` 报 `400 Bad Request`。
2. Hermes/NPC 主动文本不再弹全局 `DialogueBox` 打断玩家行动：近距离显示头顶气泡，远距离进入右侧手机消息。

## 已确认规则

- 玩家主动点击 NPC：原版对话观察链路保留；后续 Hermes 私聊输入必须打开 `PrivateChatInputMenu`。
- Hermes/NPC 主动说话：不能直接打开全局 `DialogueBox`。
- 同地图且距离不超过 8 格：走 NPC 头顶气泡。
- 不同地图，或同地图距离超过 8 格：走手机消息。
- 玩家点击 NPC 后经 `PrivateChatInputMenu` 发起的私聊回复：走原版样式 `DialogueBox`，不套用 8 格气泡规则。
- NPC 主动私聊、远程/手机来源回复：才套用 8 格气泡/手机规则。
- 手机打开时不占用键盘，不设置 `Game1.activeClickableMenu`。
- 只有玩家点进手机输入框打字时，手机才临时接管键盘；提交、取消、关闭、点外面后必须释放键盘。
- 玩家点击 NPC 的 `PrivateChatInputMenu` 是例外：它是显式输入窗口，可以接管键盘和 `activeClickableMenu`。
- 手机回复和 `PrivateChatInputMenu` 提交都必须进入 Hermes Agent，不能只在 SMAPI 本地显示。
- 关键节点必须打日志，后续手动测试要能从日志看出问题卡在哪里。
- 不永久清 NPC 原版日程。

## 当前证据

- `openspec/其他项目errors/E-2026-0014-show-text-entry-is-not-visible-menu.md` 已明确：私聊输入必须是 `IClickableMenu`，由 `Game1.activeClickableMenu = new PrivateChatInputMenu(...)` 打开。
- `git show 6c4058a5:Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs` 旧实现已经存在 `PrivateChatInputMenu` 正确路径。
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs` 当前应在 `ExecuteOpenPrivateChat` 中创建 `PrivateChatInputMenu`，并返回 `input_menu_opened`。
- `src/games/stardew/StardewCommandService.cs` 必须只把 `input_menu_opened` 识别为新契约成功；`thread_opened` 不能再掩盖错误路由。
- `Mods/StardewHermesBridge/Ui/HermesPhoneOverlay.cs` 保留手机 overlay，用于消息线程和远程回复，不作为点击 NPC 私聊入口。
- `Mods/StardewHermesBridge/Ui/StardewMessageDisplayRouter.cs` 负责按 `source` 分流 reply display：`input_menu` 走原版样式 `DialogueBox`，`phone_overlay` / 主动消息走 8 格气泡或手机。

## 方案

### A. OpenAI reasoning 400 闭环

需要保存并回放这些字段：

- `Reasoning`
- `ReasoningContent`
- `ReasoningDetails`
- `CodexReasoningItems`

涉及路径：

- `src/Core/Models.cs`
- `src/LLM/OpenAiClient.cs`
- `src/Core/AgentLoopScaffold.cs`
- `src/Core/Agent.cs`
- `src/search/SessionSearchIndex.cs`
- `src/transcript/TranscriptStore.cs`

验收：

- assistant tool-call history 带 `reasoning_content` 时，下一轮 payload 保留。
- `ChatAsync` 和 `StreamChatAsync` 都能保存 assistant final 与 assistant tool-call reasoning。
- 旧 `state.db` schema 升级后也能写入/读回 reasoning 字段。
- OpenAI 400 日志包含 status code、model、baseUrl、本地 request id 和 body 摘要。

### B. Hermes/NPC 主动文本显示路由

主动文本只能走统一显示路由：

| 场景 | 显示方式 | 禁止 |
| --- | --- | --- |
| 同地图且 8 格内 | 非菜单 NPC 头顶气泡 | `DialogueBox` |
| 异图或超过 8 格 | 手机 overlay 消息线程 | `DialogueBox` |
| 玩家点击 NPC 后 `PrivateChatInputMenu` 提交的 private_chat 回复 | 原版样式 `DialogueBox`，关闭后再进入下一轮 | 气泡、手机线程 |
| NPC 主动 / 手机来源 private_chat 回复 | 同样按 8 格路由；近处气泡，远处手机 | 误用玩家点击来源的 `DialogueBox` |
| 玩家点击 NPC 后打开私聊 | `PrivateChatInputMenu` | 手机线程 |

手机 overlay 可复用 `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone` 的外壳、素材、布局、点击命中和震动提示，但只复用手机 UI 能力，不照搬它的电话/`DialogueBox` 链路。

### C. 玩家点击 NPC 私聊输入窗口

`/action/open_private_chat` 的正式契约：

- world not ready：`world_not_ready`，retryable。
- NPC 不存在：`invalid_target`，non-retryable。
- 已有 Stardew 菜单：`menu_blocked`，retryable，不能覆盖当前菜单。
- 成功：打开 `PrivateChatInputMenu`，返回：

```json
{
  "opened": true,
  "threadId": "<conversationId>",
  "openState": "input_menu_opened"
}
```

输入窗口生命周期：

- 提交非空文本：记录 `player_private_message_submitted`，payload 带 `conversationId/text/submittedAtUtc`。
- 提交事件 payload 必须带 `source = "input_menu"`，后续 Agent reply 的 `stardew_speak` payload 也必须保留该 source。
- `source = "input_menu"` 的回复必须先登记待关闭的对话生命周期，再调用 `NpcRawDialogueRenderer.Display(...)`，防止 `MenuChanged` 竞态漏掉 `private_chat_reply_closed`。
- 空提交、ESC、关闭窗口：记录 `player_private_message_cancelled`。
- `ModEntry.OnMenuChanged` 只用于捕获这个真实菜单关闭后的取消，不用于手机 overlay 生命周期。

### D. 手机 overlay 生命周期

手机 overlay 只作为消息线程 UI：

- `PhoneThreadPassiveOpen`：可看消息，不占键盘，不暂停 NPC autonomy。
- `PhoneReplyFocusActive`：玩家点输入框后才占键盘。
- 关闭手机或提交/取消手机回复时，必须释放键盘。
- 手机可记录 `player_private_message_submitted` / `player_private_message_cancelled`，但这不改变 `/action/open_private_chat` 的入口语义。
- 手机提交事件 payload 必须带 `source = "phone_overlay"`；这类回复可以按 8 格规则走气泡，不得显示成玩家点击来源的原版对话框。

## 自动化测试清单

- `RawDialogueDisplayRegressionTests.PlayerClickedNpcPrivateChatUsesInputMenuNotPhoneOverlay`
  - 要求 `new PrivateChatInputMenu`。
  - 要求 `Game1.activeClickableMenu = inputMenu`。
  - 禁止 `_phoneState.OpenThread(npc.Name, conversationId)` 出现在 open private chat 生产路径。
  - 要求 `menu_blocked` 保护，不覆盖已有菜单。
- `RawDialogueDisplayRegressionTests.PrivateChatInputCloseWithoutEnterRecordsCancellation`
  - 关闭真实私聊输入菜单必须发取消事件。
- `StardewCommandServiceTests.SubmitAsync_OpenPrivateChat_AcceptsInputMenuOpenState`
  - `input_menu_opened` 映射为 accepted success。
- `StardewCommandServiceTests.SubmitAsync_OpenPrivateChat_RejectsPhoneThreadOpenState`
  - `thread_opened` 不能被当作 `/action/open_private_chat` 成功。
- 主动文本路由测试：
  - `source=input_menu` 的 private chat 回复走原版样式对话框。
  - `source=phone_overlay` 或 NPC 主动消息 8 格内气泡。
  - 8 格外或异图手机消息。
  - 主动消息不调用 `NpcRawDialogueRenderer.Display(...)`。
- `StardewPrivateChatOrchestratorTests`
  - `input_menu` 提交默认把 reply source 传到 speak payload。
  - `phone_overlay` 提交把 reply source 传到 speak payload。
- `StardewCommandServiceTests.SubmitAsync_Speak_PassesPrivateChatSourceToBridge`
  - Desktop/core 发给 bridge 的 speak DTO 保留 `source`。
- OpenAI 400 测试：
  - response parse、message save/load、payload replay、旧 schema migration。

建议命令：

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewCommandServiceTests|FullyQualifiedName~OpenAi|FullyQualifiedName~Reasoning"
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~RawDialogueDisplayRegressionTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug
```

## 不做范围

- 不做手机消息跨存档/重启持久化。
- 不做表情、图片、语音、群聊、搜索。
- 不把所有原版游戏对话改成手机。
- 不让 Bridge 或 Desktop 替 NPC 判断是否接受玩家请求。
- 不新增第二套 NPC task store。
- 不删除 `PrivateChatInputMenu`。

## 完成定义

- OpenAI thinking/tool-call 后续轮次不再因缺少 `reasoning_content` 报 400。
- Hermes/NPC 主动文本不再打开全局 `DialogueBox`。
- Haley move 不会被 Hermes 主动文本造成的 `dialogue_started` 中断。
- 玩家主动点击 NPC 后，Hermes 私聊入口打开 `PrivateChatInputMenu`。
- 玩家点击 NPC 后在 `PrivateChatInputMenu` 输入的私聊回复使用原版样式对话框，不使用头顶气泡。
- 只有 NPC 主动/手机来源的私聊回复才在 8 格内使用头顶气泡。
- `/action/open_private_chat` 成功状态为 `input_menu_opened`，不是 `thread_opened`。
- 8 格内头顶气泡，8 格外或异图手机消息。
- 手机打开不占键盘，只有输入框激活时占键盘。
- 手机消息和私聊输入窗口提交都能进入 Hermes Agent。
- 日志能串起 OpenAI reasoning、显示路由、手机状态、私聊输入生命周期、move 中断原因。
