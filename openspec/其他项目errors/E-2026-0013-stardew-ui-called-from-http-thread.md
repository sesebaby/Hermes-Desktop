# E-2026-0013-stardew-ui-called-from-http-thread

- id: E-2026-0013
- title: Stardew UI 从 bridge HTTP 后台线程执行导致 completed 但不弹框
- status: active
- updated_at: 2026-05-01
- keywords: [StardewValley, SMAPI, HttpListener, Game1.showTextEntry, NpcRawDialogueRenderer, PumpOneTick, private_chat]
- trigger_scope: [implementation, bugfix]

## Symptoms

- SMAPI 日志显示 `action_open_private_chat_completed`，但游戏里没有出现私聊输入框。
- 点击 Haley 后 bridge 可以记录 `npc_click_observed`、`vanilla_dialogue_completed_fact` 或 `vanilla_dialogue_unavailable_fact`。
- Desktop 认为 open-private-chat 成功，随后停在等待玩家输入状态，继续点击只产生新的 bridge fact 但没有新的可见 UI。

## Root Cause

- 直接根因：`BridgeHttpHost` 在 `Task.Run` 处理 HTTP 请求，`BridgeCommandQueue.OpenPrivateChat` 和 `Speak` 在该后台线程直接调用 `Game1.showTextEntry(...)` / `NpcRawDialogueRenderer.Display(...)`。
- Stardew/SMAPI UI 状态应从游戏主循环线程修改；后台线程可以完成日志和响应，但不能可靠显示游戏菜单。
- 诊断误导点：bridge 日志的 `completed` 只证明 HTTP 方法执行到末尾，不证明 Stardew UI 真正在游戏线程显示。

## Bad Fix Paths

- 继续调点击命中、`DialogueBox.transitioning` 或 `manual_original_start_failed` 分支；日志已经证明点击和 fact 记录链路可达。
- 把 `vanilla_dialogue_unavailable` 反复重试；如果 UI 仍在后台线程执行，重试只会重复记录 completed。
- 在 Desktop 侧延迟或轮询更多事件；真正失败点在 bridge 执行线程。

## Corrective Constraints

- 所有会改变 Stardew UI 的 bridge action 必须排队到 `UpdateTicked` / `PumpOneTick` 执行。
- HTTP handler 可以等待游戏线程执行结果，但不能直接调用 `Game1.showTextEntry`、`Game1.DrawDialogue`、`NpcRawDialogueRenderer.Display` 等 UI API。
- 回归测试必须锁住 `OpenPrivateChatAsync` / `SpeakAsync` 通过 `_pendingUi` 和 `TryPumpUiCommand()` 执行。
- `action_*_completed` 日志只能在 game-loop 执行成功后写出。

## Verification Evidence

- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~RawDialogueDisplayRegressionTests.BridgeUiActionsAreQueuedForGameLoopPump"` 通过，1/1。
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` 通过，45/45。
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:Platform=x64 --filter "FullyQualifiedName~Stardew"` 通过，38/38。
- `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64` 成功，只有既有 SMAPI AnyCPU warning。

## Related Files

- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/ModEntry.cs`
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs`

## Notes

- 这条经验与 `E-2026-0010`、`E-2026-0012` 相邻但不同：那些是输入/菜单状态路由问题，本条是跨线程 UI 执行问题。
