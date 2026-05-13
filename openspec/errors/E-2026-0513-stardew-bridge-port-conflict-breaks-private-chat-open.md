---
id: E-2026-0513-stardew-bridge-port-conflict-breaks-private-chat-open
title: Bridge 端口冲突导致点击 NPC 后私聊输入窗口无法打开
updated_at: 2026-05-13
keywords:
  - stardew
  - bridge_start_failed
  - bridge_stale_discovery
  - open_private_chat
  - PrivateChatInputMenu
  - stardew-bridge.json
  - HttpListener
---

## symptoms

- 玩家点击 Haley 后，SMAPI 日志只出现 `npc_click_observed` 和 `vanilla_dialogue_completed_fact` / `vanilla_dialogue_unavailable_fact`，没有后续 `action_open_private_chat_completed`。
- SMAPI 启动日志出现 `bridge_start_failed ... 另一个程序正在使用此文件，进程无法访问。`。
- Desktop 日志持续出现 `Stardew autonomy host iteration skipped; reason=discovery_unavailable; failureReason=bridge_stale_discovery`。
- `%LOCALAPPDATA%\hermes\hermes-cs\stardew-bridge.json` 仍指向旧进程/旧 token，当前 SMAPI bridge 没有写入可用 discovery。

## trigger_scope

- 修改或排查 `BridgeHttpHost.Start`、`ModEntry.WriteDiscoveryFile`、`FileStardewBridgeDiscovery`。
- 排查“点击 NPC 私聊窗口不打开”但代码中 `BridgeCommandQueue.ExecuteOpenPrivateChat` 仍创建 `PrivateChatInputMenu`。
- 同机重复启动 SMAPI/Stardew、旧进程残留、端口 8745 被占用。

## root_cause

- `BridgeHttpHost.Start` 只尝试绑定固定首选端口 `8745`。端口被旧 SMAPI/残留 listener 占用时，bridge 直接启动失败。
- `ModEntry.WriteDiscoveryFile` 正确地拒绝写入 discovery，因为 `_httpHost.IsRunning` 为 false；但结果是 Desktop 只能看到旧 discovery，并判定 `bridge_stale_discovery`。
- 点击 NPC 后 bridge 只记录本地事件；Desktop 需要通过 `/events/poll` 读取事件，再调用 `/action/open_private_chat`，因此 discovery 失效会让 `PrivateChatInputMenu` 永远无法被下发打开。

## bad_fix_paths

- 把点击入口改成 phone overlay 或接受 `thread_opened` 成功，重复 `E-2026-0505-private-chat-input-menu-replaced-by-phone-plan`。
- 删除 `_httpHost.IsRunning` discovery gate，把不可用 token/port 写进 `stardew-bridge.json`。
- 只要求用户手动杀进程或删 discovery 文件，而不让 bridge 在端口冲突时自动恢复到可发现端口。
- 只看 `BridgeCommandQueue.ExecuteOpenPrivateChat` 是否创建 `PrivateChatInputMenu`，不检查 SMAPI `bridge_start_failed` 和 Desktop `bridge_stale_discovery`。

## corrective_constraints

- `BridgeHttpHost.Start` 必须在首选端口冲突时尝试有界备用端口，并把实际绑定端口暴露给 discovery。
- fallback 成功必须写 `bridge_port_fallback` 日志，保留 `preferredPort`、`actualPort` 和上一次错误，方便手测定位。
- 所有端口绑定都失败时，仍必须清空 `BridgeToken` 和 `Port`，并写 `bridge_start_failed`；不得发布不可用 discovery。
- 玩家点击 NPC 的私聊生产路径仍必须是 `/action/open_private_chat -> PrivateChatInputMenu -> input_menu_opened`，不能被 bridge recovery 逻辑改成 phone overlay。

## verification_evidence

- RED: `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeDiscoveryRegistrationRegressionTests.PreferredPortConflictFallsBackToDiscoverableBridgePort"` failed because `BridgeHttpHost.cs` lacked `MaxPortBindAttempts`.
- GREEN: the same test passed after adding bounded fallback port binding and `bridge_port_fallback` logging.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeDiscoveryRegistrationRegressionTests|FullyQualifiedName~RawDialogueDisplayRegressionTests.PlayerClickedNpcPrivateChatUsesInputMenuNotPhoneOverlay|FullyQualifiedName~RawDialogueDisplayRegressionTests.PrivateChatReplyCloseIsRecordedBySourceSpecificUiOwner"` passed 5/5, proving discovery recovery and input-menu routing constraints.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewCommandServiceTests.SubmitAsync_OpenPrivateChat_AcceptsInputMenuOpenState|FullyQualifiedName~StardewCommandServiceTests.SubmitAsync_OpenPrivateChat_RejectsPhoneThreadOpenState|FullyQualifiedName~StardewManualActionServiceTests.TryReadLatest_WhenDiscoveryProcessIdIsDead_ReturnsStaleDiscovery"` passed 3/3.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` passed 141/141.

## related_files

- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `Mods/StardewHermesBridge/ModEntry.cs`
- `Mods/StardewHermesBridge.Tests/BridgeDiscoveryRegistrationRegressionTests.cs`
- `src/games/stardew/StardewBridgeDiscovery.cs`
- `openspec/errors/E-2026-0505-private-chat-input-menu-replaced-by-phone-plan.md`
