---
id: E-2026-0505-private-chat-input-menu-replaced-by-phone-plan
title: 玩家点击 NPC 的私聊输入窗口被错误替换成手机线程
updated_at: 2026-05-05
keywords:
  - stardew
  - private_chat
  - PrivateChatInputMenu
  - open_private_chat
  - phone overlay
  - activeClickableMenu
---

## symptoms

- 玩家点击 Haley 后没有弹出 Hermes 私聊输入窗口，而是打开右侧手机联系人/消息 UI。
- `/action/open_private_chat` 日志显示 completed 或 `thread_opened`，但玩家没有获得预期的私聊输入菜单。
- 测试和计划开始要求删除 `PrivateChatInputMenu`，导致原先已经实现的私聊窗口被当成旧链路清掉。
- 玩家通过私聊输入窗口发消息后，NPC 回复被 8 格近距离规则改成头顶气泡，而不是原版样式对话框。

## trigger_scope

- 修改 `/action/open_private_chat`、`BridgeCommandQueue.ExecuteOpenPrivateChat` 或 private chat 打开契约。
- 修改手机 overlay、远程消息、主动消息路由。
- 编写“手机消息”和“私聊输入”合并计划。
- 迁移 `activeClickableMenu`、`PrivateChatInputMenu`、`HermesPhoneOverlay` 生命周期。

## root_cause

- 计划把两条不同 UI 链路混为一谈：玩家点击 NPC 的私聊输入入口，和 NPC 主动/远程消息的手机 overlay。
- 旧正确实现已经存在：`PrivateChatInputMenu` 是真实 `IClickableMenu`，通过 `Game1.activeClickableMenu = inputMenu` 显示；这正是玩家点击 NPC 后的输入窗口。
- 后续计划错误地把“手机 overlay 不占 `activeClickableMenu`”套用到 `/action/open_private_chat`，并要求 `OpenPrivateChat` 返回 `thread_opened`。
- 回复展示又把“NPC 主动/远程消息的 8 格气泡规则”套用到玩家点击来源的私聊回复，缺少 `source` 区分。

## bad_fix_paths

- 把 `open_private_chat` 改成 `_phoneState.OpenThread(...)`。
- 删除或标记 `PrivateChatInputMenu` 为 retired production path。
- 让 `StardewCommandService` 接受 `thread_marked`、`thread_opened`、`focus_pending` 作为 `/action/open_private_chat` 成功。
- 用手机窗口一直打开不阻塞 autonomy 的需求，反推玩家点击 NPC 私聊也必须是手机 overlay。
- 用“8 格以内走头顶气泡”的主动消息规则，反推玩家点击 NPC 后的私聊回复也应该走气泡。
- 只改 phone close/cancel 事件，不恢复点击 NPC 私聊输入菜单。

## corrective_constraints

- `/action/open_private_chat` 必须创建 `PrivateChatInputMenu` 并设置 `Game1.activeClickableMenu = inputMenu`。
- 成功状态必须是 `input_menu_opened`；`thread_opened` 不得作为 `/action/open_private_chat` 成功。
- `PrivateChatInputMenu` 是生产路径，不是 retired compatibility shell。
- 手机 overlay 只用于 NPC 主动消息、远程消息、消息历史和手机内回复；它不能接管玩家点击 NPC 后的私聊入口。
- 如果已有 Stardew 菜单打开，`open_private_chat` 必须返回 retryable `menu_blocked`，不能覆盖当前菜单，也不能绕到手机线程。
- `PrivateChatInputMenu` 提交事件必须带 `source = "input_menu"`；手机提交事件必须带 `source = "phone_overlay"`。
- `source = "input_menu"` 的 private chat 回复必须用原版样式 `DialogueBox`，并由 `ModEntry.OnMenuChanged` 记录 `private_chat_reply_closed`。
- 8 格头顶气泡规则只适用于 NPC 主动消息、远程/手机来源 private chat 回复，不能覆盖玩家点击来源的回复展示。
- input-menu 回复显示前必须先登记待关闭的 reply dialogue，避免 `Game1.DrawDialogue` 触发 `MenuChanged` 时还没有 pending marker。
- 回归测试必须同时锁定正确路径存在和错误路径不存在。

## verification_evidence

- `git show 6c4058a5:Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs` 证明历史正确路径已经使用 `new PrivateChatInputMenu(...)` 和 `Game1.activeClickableMenu = inputMenu`。
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewCommandServiceTests.SubmitAsync_OpenPrivateChat_AcceptsInputMenuOpenState|FullyQualifiedName~StardewCommandServiceTests.SubmitAsync_OpenPrivateChat_RejectsPhoneThreadOpenState"` 先失败，证明旧服务层仍误收 phone thread 状态；修复后通过 2/2。
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~RawDialogueDisplayRegressionTests.PlayerClickedNpcPrivateChatUsesInputMenuNotPhoneOverlay"` 先失败，证明 Bridge 缺少 active menu 保护；修复后通过 1/1。
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~RawDialogueDisplayRegressionTests.PrivateChatReplyCloseIsRecordedBySourceSpecificUiOwner"` 先失败，证明 reply dialogue pending marker 在 display 后才登记；修复为 display 前预登记后通过 1/1。
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~RawDialogueDisplayRegressionTests"` 通过 18/18，覆盖 input-menu 原版回复、phone/bubble close、主动消息路由。
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewCommandServiceTests|FullyQualifiedName~StardewPrivateChatOrchestratorTests"` 通过 38/38，覆盖 source 从提交事件传到 speak DTO。

## related_files

- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Ui/PrivateChatInputMenu.cs`
- `Mods/StardewHermesBridge/Ui/HermesPhoneOverlay.cs`
- `src/games/stardew/StardewCommandService.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs`
- `.omx/plans/星露谷手机消息与OpenAI400修复计划.md`
