# E-2026-0014-show-text-entry-is-not-visible-menu

- id: E-2026-0014
- title: Game1.showTextEntry 被误当作可见输入菜单导致 completed 但不弹框
- status: active
- updated_at: 2026-05-01
- keywords: [StardewValley, SMAPI, Game1.showTextEntry, IClickableMenu, TextBox, keyboardDispatcher, private_chat]
- trigger_scope: [implementation, bugfix]

## Symptoms

- SMAPI 日志显示 `action_open_private_chat_completed`，但游戏里没有出现私聊输入框。
- bridge JSONL 已记录 `private_chat_opened`，Desktop 进入等待玩家输入状态。
- 继续点击 Haley 只产生新的 `vanilla_dialogue_unavailable_fact` / `action_open_private_chat_completed`，没有 `private_chat_message_submitted` 或可见输入 UI。

## Root Cause

- 直接根因：`BridgeCommandQueue.ExecuteOpenPrivateChat` 创建 `StardewValley.Menus.TextBox` 后只调用 `Game1.showTextEntry(textBox)`，没有创建并设置任何会被 Stardew 绘制的 `IClickableMenu`。
- 参考实现中，`showTextEntry` 只作为 gamepad/on-screen keyboard 辅助；真正可见的文本框由菜单自己的 `draw(...)` 调用 `TextBox.Draw(...)` 绘制，并通过 `Game1.keyboardDispatcher.Subscriber` 接收键盘输入。
- 诊断误导点：`action_open_private_chat_completed` 只说明 bridge action 执行到了末尾，不证明屏幕上存在可见菜单。

## Bad Fix Paths

- 继续调 HTTP/game-loop 线程排队；日志已经证明当前执行路径能在 game loop 写出 `completed`。
- 继续修改 NPC 点击路由或原版 DialogueBox 检测；点击链路与 Desktop open command 均已可达。
- 只给 `showTextEntry` 增加延迟、重试或日志；缺少 `activeClickableMenu` 时 Stardew 仍没有菜单可画。

## Corrective Constraints

- 私聊输入必须是一个真实的 `IClickableMenu`，由 `Game1.activeClickableMenu = new PrivateChatInputMenu(...)` 打开。
- 菜单必须自己绘制 `TextBox`，并设置 `Game1.keyboardDispatcher.Subscriber`，不能只调用 `Game1.showTextEntry`。
- 回归测试必须拒绝 `Game1.showTextEntry(textBox)` 作为 private-chat 打开路径，并要求 `PrivateChatInputMenu` 继承 `IClickableMenu`。
- 取消/关闭路径仍需记录 `player_private_message_cancelled`，并保留 `private_chat_input_closed_without_submit` 诊断日志。

## Verification Evidence

- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~PrivateChatInputUsesVisibleClickableMenuInsteadOfTextEntryHelper"` 先失败，原因是缺少 `Ui\PrivateChatInputMenu.cs`。
- 同一过滤测试修复后通过，1/1。
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` 通过，46/46。
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug` 通过，0 warnings / 0 errors，并部署到 `D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\StardewHermesBridge`。
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:Platform=x64 --filter "FullyQualifiedName~Stardew"` 通过，38/38。
- 2026-05-01 用户手动测试确认点击 Haley 后输入框已能弹出；剩余问题是自定义输入框视觉质量差，属于后续 UI polish，不是“不弹出”的行为故障。

## Related Files

- `Mods/StardewHermesBridge/Ui/PrivateChatInputMenu.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs`
- `openspec/其他项目errors/E-2026-0013-stardew-ui-called-from-http-thread.md`

## Notes

- 本条与 `E-2026-0013` 相邻但不同：`E-2026-0013` 是 UI API 在 HTTP 后台线程执行；本条是即使已经回到 game loop，也不能把 `showTextEntry` 当成可见菜单。
