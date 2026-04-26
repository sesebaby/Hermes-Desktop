# Superpowers Stardew 宿主接入总地图附件

## 1. 文档定位

本文只回答 4 件事：

1. `Stardew` 每个关键 surface 到底落在哪个类和目录
2. 哪个 semantic hook 对应哪个宿主面
3. 哪些现有代码继续保留当壳
4. 哪些旧本地 AI 主链代码必须退役

固定回链：

- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-runtime-module-interface-and-commit-contract-appendix.md`

## 2. 固定规则

1. 本文只收 `Stardew` 宿主接入，不替代总设计。
2. 本地 prompt builder / prompt catalog / prompt assets 不在本文的正式宿主地图里。
3. 它们已经被定性为：
   - `retired business mainline`
4. 本文只登记还能继续留在正式工程里的宿主壳、hook 壳、transport 壳。

## 3. 宿主接入总表

| 能力 / surface | semantic hook | 当前类 / 路径 | 当前处置 | committed / 成功点 | failureClass | recovery |
| --- | --- | --- | --- | --- | --- | --- |
| 宿主原对话显示 | `hostDialogueRenderedAt` / `hostDialogueRecordedAt` / `hostDialogueExhaustedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/NpcInteractionHooks.cs`、`games/stardew-valley/Superpowers.Stardew.Mod/Hooks/NpcNaturalInteractionController.cs` | `kept carrier shell` | 原对话真实显示，且记录已写入 | `render_failed` | `游戏 -> 帮助与修复` |
| AI 私聊对话框 | `aiDialogueOpenedAt` / `aiDialogueThinkingShownAt` / `aiDialogueStreamChunkRenderedAt` / `aiDialogueRenderedAt` / `aiDialogueClosedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/UI/AiDialogueMenu.cs` | `kept carrier shell` | AI 文本真实显示在对话框里；stream chunk 可渐进追加 | `render_failed` | `游戏 -> 帮助与修复` |
| NPC 信息面板 | `infoPanelRenderedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/UI/NpcInfoPanelMenu.cs` | `kept carrier shell` | 面板成功打开且基础信息可见 | `render_failed` | `游戏 -> 帮助与修复` |
| 记忆 Tab | `memoryTabRenderedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/UI/NpcInfoPanelMenu.cs` | `kept carrier shell` | 记忆卡片可见 | `render_failed` | `游戏 -> 帮助与修复` |
| 关系 Tab | `relationTabRenderedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/UI/NpcInfoPanelMenu.cs` | `kept carrier shell` | 关系分组可见 | `render_failed` | `游戏 -> 帮助与修复` |
| 物品 Tab | `itemTabRenderedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/UI/NpcInfoPanelMenu.cs` | `kept carrier shell` | 物品卡片与详情可见 | `render_failed` | `游戏 -> 帮助与修复` |
| 当前想法 | `thoughtRequestedAt` / `thoughtRenderedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/UI/NpcInfoPanelMenu.cs`、`games/stardew-valley/Superpowers.Stardew.Mod/Hooks/NpcInteractionHooks.cs` | `kept carrier shell` | thought 文本完整显示 | `render_failed` | `游戏 -> 帮助与修复` |
| 群聊历史 Tab | `groupHistoryDisclosureResolvedAt` / `groupHistoryTabRenderedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/GroupHistoryTabView.cs`、`games/stardew-valley/Superpowers.Stardew.Mod/Hooks/MenuLifecycleHooks.cs` | `kept carrier shell` | 记录、空态或开放状态其中之一成功显示 | `render_failed` | `游戏 -> 帮助与修复` |
| 手机私信 | `remoteThreadOpenedAt` / `remoteSubmitQueuedAt` / `remoteMessageRenderedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/UI/PhoneDirectMessageMenu.cs`、`games/stardew-valley/Superpowers.Stardew.Mod/Hooks/WorldLifecycleHooks.cs` | `kept carrier shell` | 远程线程打开且文本成功显示 | `availability_blocked` / `submission_failed` / `render_failed` | `游戏 -> 帮助与修复` |
| 现场群聊 | `groupParticipantSetFrozenAt` / `groupPlayerInputQueuedAt` / `groupTurnRenderedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/UI/OnsiteGroupChatOverlay.cs`、`games/stardew-valley/Superpowers.Stardew.Mod/Hooks/WorldLifecycleHooks.cs` | `kept carrier shell` | 当句 turn 已持久化且气泡已显示 | `submission_failed` / `render_failed` | `游戏 -> 帮助与修复` |
| 手机主动群聊 | `remoteSubmitQueuedAt` / `groupTurnRenderedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/UI/PhoneActiveGroupChatMenu.cs`、`games/stardew-valley/Superpowers.Stardew.Mod/Hooks/WorldLifecycleHooks.cs` | `kept carrier shell` | 当条 turn 已持久化且消息已显示 | `submission_failed` / `render_failed` | `游戏 -> 帮助与修复` |
| 物品文本 carrier | `itemCarrierRenderedAt` / `itemEventRecordedAt` | `games/stardew-valley/Superpowers.Stardew.Mod/UI/Carriers/ItemTextCarrierBase.cs`、`games/stardew-valley/Superpowers.Stardew.Mod/Hooks/ItemCarrierHooks.cs` | `kept carrier shell` | carrier 文本已显示、authoritative item-event 已记录、且实际发放或明确 no-delivery / rejected outcome 已成立 | `render_failed` | `游戏 -> 帮助与修复` |
| Mod -> Runtime transport | `runtimeStreamOpenedAt` / `runtimeStreamChunkReceivedAt` / `runtimeStreamClosedAt` / finalize 回链所有 request | `games/stardew-valley/Superpowers.Stardew.Mod/Runtime/RuntimeClient.cs` | `kept carrier shell` | 请求、stream、finalize 能正确往返 | 上层继承 | 上层继承 |

## 4. 与 Runtime / Cloud 的正式对接点

| 宿主壳 | 正式对接点 |
| --- | --- |
| `RuntimeClient` | `src/Superpowers.Runtime.Local/Endpoints/*` |
| `PrivateDialogueRequest` / `RemoteDirectRequest` / `GroupChatTurnRequest` / `ThoughtRequest` | `src/Superpowers.Runtime.Stardew/Contracts/*` |
| 群聊 / 私聊 / thought 的 deterministic gate | `src/Superpowers.Runtime.Local/Endpoints/PrivateDialogueEndpoint.cs`、`RemoteDirectEndpoint.cs`、`GroupChatEndpoint.cs`、`ThoughtEndpoint.cs` |
| canonical replay / pending / mirrored writeback | `src/Superpowers.CloudControl/History/CanonicalHistoryStore.cs` |
| canonical memory | `src/Superpowers.CloudControl/Memory/CanonicalMemoryStore.cs` |

## 4A. 流式快反馈绑定

`Stardew` 当前私聊快反馈固定绑定到以下类和接口：

1. `games/stardew-valley/Superpowers.Stardew.Mod/Runtime/RuntimeClient.cs`
   - 负责打开 `POST /runtime/{gameId}/private-dialogue/stream`
2. `games/stardew-valley/Superpowers.Stardew.Mod/UI/AiDialogueMenu.cs`
   - 负责显示角色化等待文案
   - 负责按 chunk 追加文本
   - 负责显示明确失败 copy
3. `src/Superpowers.Runtime.Local/Endpoints/PrivateDialogueEndpoint.cs`
   - 负责把本地请求接到 `Cloud candidate-stream`
4. `src/Superpowers.CloudControl/Narrative/HostedNarrativeController.cs`
   - 负责对外提供 `candidate-stream`

## 5. 必须退役的本地旧主链

下列代码不再属于 `Stardew` 正式宿主接入：

1. `src/Superpowers.Runtime.Stardew/Adapter/StardewPromptAssetCatalog.cs`
2. `src/Superpowers.Runtime.Stardew/Adapter/StardewPrivateDialoguePromptBuilder.cs`
3. `src/Superpowers.Runtime.Stardew/Adapter/StardewMemoryCompressionPromptBuilder.cs`
4. `src/Superpowers.Runtime.Stardew/PromptAssets/StardewValley/*`

固定原因：

1. 它们把 prompt 真源放在本地。
2. 它们把最终 prompt 编排放在本地。
3. 这和当前 `Cloud` 真源设计直接冲突。

## 6. review 必查点

1. 是否还在从 `StardewPromptAssetCatalog` 读取正式 prompt。
2. 是否还在本地 `PromptBuilder` 里拼最终 prompt。
3. 是否把宿主 UI 壳误当成业务 authority。
4. 是否把 semantic hook 漏成只剩 UI 状态，没有 trace 事件。
