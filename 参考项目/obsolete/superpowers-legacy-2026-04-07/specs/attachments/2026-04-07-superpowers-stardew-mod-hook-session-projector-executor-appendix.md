# Superpowers Stardew Mod Hook、Session、Projector、Executor 落点附件

## 1. 文档定位

这份附件专门把 `Superpowers.Stardew.Mod` 里最容易跑偏的一层写死：

1. 每个功能到底从哪个 hook 进来
2. 哪个类负责读宿主事实
3. 哪个类负责本地 surface session
4. 哪个类负责调 Runtime
5. 哪个类负责把回包翻成 UI 或宿主执行输入
6. 哪个类负责最后真正改宿主

前面的附件已经回答了：

1. 功能是什么
2. 抄哪个参考 mod
3. 工程怎么大拆

本文只回答：

1. `类和职责怎么精确落`
2. `方法和 DTO 名字怎么固定`
3. `哪些旧入口从现在起必须断电`

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-feature-reference-implementation-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-rebuild-and-implementation-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-current-code-retirement-and-cutover-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-host-integration-map-appendix.md`
- `docs/superpowers/contracts/runtime/private-dialogue-request-contract.md`
- `docs/superpowers/contracts/runtime/remote-direct-request-contract.md`
- `docs/superpowers/contracts/runtime/group-chat-turn-request-contract.md`
- `docs/superpowers/contracts/runtime/thought-request-contract.md`
- `docs/superpowers/contracts/runtime/stardew-npc-panel-bundle-contract.md`
- `docs/superpowers/contracts/runtime/stardew-phone-contact-list-contract.md`
- `docs/superpowers/contracts/runtime/stardew-phone-contact-entry-contract.md`
- `docs/superpowers/contracts/runtime/stardew-remote-direct-thread-contract.md`
- `docs/superpowers/contracts/runtime/stardew-remote-direct-availability-state-machine-contract.md`
- `docs/superpowers/contracts/runtime/stardew-group-chat-session-contract.md`
- `docs/superpowers/contracts/runtime/stardew-onsite-group-overlay-contract.md`
- `docs/superpowers/contracts/runtime/stardew-phone-group-thread-contract.md`
- `docs/superpowers/contracts/runtime/stardew-item-instantiation-creator-contract.md`
- `docs/superpowers/contracts/runtime/stardew-item-gift-and-trade-host-executor-contract.md`
- `docs/superpowers/contracts/runtime/stardew-proactive-dialogue-trigger-state-machine-contract.md`
- `docs/superpowers/contracts/runtime/stardew-auto-action-and-schedule-restore-contract.md`

## 2. 大白话总规则

以后改 `Superpowers.Stardew.Mod`，固定按这 7 层说清楚：

1. `Hook`
2. `Snapshot Builder`
3. `Surface Session`
4. `Runtime Transport`
5. `Projector`
6. `Host Executor`
7. `Visible Surface`

死规则：

1. `Hook` 不准自己拼 request
2. `Snapshot Builder` 不准自己开 UI
3. `Surface Session` 不准自己做宿主写回
4. `RuntimeClient` 不准继续当 builder + projector + executor 大杂烩
5. `Executor` 不准自己生成 AI 语义

## 3. 未来目录与类落点

以后 `games/stardew-valley/Superpowers.Stardew.Mod` 固定按下面这套目录补齐：

```text
games/stardew-valley/Superpowers.Stardew.Mod/
  Application/Routes/
  HostSnapshots/
  SurfaceSessions/
  RuntimeTransport/
  Projectors/
  HostWriteback/
  HostModels/Requests/
  HostModels/Responses/
  HostModels/Receipts/
  UI/
  Hooks/
```

各层固定职责：

1. `Application/Routes`
   - 只管功能入口路由
2. `HostSnapshots`
   - 只管读 Stardew 事实
3. `SurfaceSessions`
   - 只管页面生命周期、本地线程壳、stale/loading/ready/failure
4. `RuntimeTransport`
   - 只管发请求、收回包、发 finalize
5. `Projectors`
   - 只管翻 UI model 或 executor input
6. `HostWriteback`
   - 只管最终宿主执行
7. `HostModels/*`
   - 放 Mod 内部 DTO，不允许继续塞进 `Controller` 和 `RuntimeClient`

## 4. 固定类清单

### 4.1 Route Coordinators

固定新增：

1. `PrivateDialogueRouteCoordinator`
   - `Application/Routes/PrivateDialogueRouteCoordinator.cs`
2. `PhoneContactBookRouteCoordinator`
   - `Application/Routes/PhoneContactBookRouteCoordinator.cs`
3. `PhoneDirectRouteCoordinator`
   - `Application/Routes/PhoneDirectRouteCoordinator.cs`
4. `GroupChatRouteCoordinator`
   - `Application/Routes/GroupChatRouteCoordinator.cs`
5. `ProactiveDialogueRouteCoordinator`
   - `Application/Routes/ProactiveDialogueRouteCoordinator.cs`

### 4.2 Snapshot Builders

固定新增：

1. `HostDialogueSnapshotBuilder`
2. `NpcPanelSnapshotBuilder`
3. `ScheduleSnapshotBuilder`
4. `TradeSnapshotBuilder`
5. `GroupParticipantSnapshotBuilder`
6. `ItemCreationSnapshotBuilder`
7. `AutoActionSnapshotBuilder`

### 4.3 Surface Sessions

固定新增：

1. `AiDialogueSurfaceSession`
2. `NpcInfoPanelSurfaceSession`
3. `RemoteDirectThreadSession`
4. `PhoneGroupThreadSession`
5. `OnsiteGroupSession`

### 4.4 Runtime Transport

固定新增：

1. `StardewRuntimeTransportClient`
   - 替代当前 `RuntimeClient` 的正式 transport 主线
2. `IRuntimeFinalizeClient`
   - 单独放 finalize 调用，不跟普通请求混在一起
3. `IRuntimeDialogueStreamClient`
   - 单独放文本 streaming 调用，不和 finalize、bundle 拉取混在一起

### 4.5 Projectors

固定新增：

1. `AiDialogueSurfaceProjector`
2. `NpcInfoPanelProjector`
3. `RemoteDirectThreadProjector`
4. `GroupChatProjector`
5. `ItemCarrierProjector`
6. `HostActionProjector`

### 4.6 Host Executors

固定新增：

1. `ItemGiftHostExecutor`
2. `TradeHostExecutor`
3. `StardewItemInstantiationExecutor`
4. `StardewEntityInstantiationExecutor`
5. `NpcAutoActionExecutor`
6. `NpcScheduleRestoreExecutor`

## 5. 每个功能的正式落点表

| 功能 | Hook 入口 | Snapshot Builder | Session Owner | Transport 方法 | Projector | Host Executor | Visible Surface |
| --- | --- | --- | --- | --- | --- | --- | --- |
| AI 私聊 | `NpcInteractionHooks`、`NpcNaturalInteractionController` | `HostDialogueSnapshotBuilder` | `AiDialogueSurfaceSession` | `SendPrivateDialogueAsync`、`StreamPrivateDialogueAsync` | `AiDialogueSurfaceProjector` | 无独立 host executor；提交 `FinalizeDialogueCommitAsync` 给 `Runtime.Local` | `AiDialogueMenu` |
| 信息面板基础资料 | `NpcInteractionHooks`、`MenuLifecycleHooks` | `NpcPanelSnapshotBuilder` | `NpcInfoPanelSurfaceSession` | `LoadNpcPanelBundleAsync` | `NpcInfoPanelProjector` | 无直接 executor | `NpcInfoPanelMenu` |
| 当前想法 | `NpcInteractionHooks` | `NpcPanelSnapshotBuilder` | `NpcInfoPanelSurfaceSession` | `SendThoughtAsync` | `NpcInfoPanelProjector` | 无直接 executor | `ThoughtTabView` |
| 手机联系人 | `WorldLifecycleHooks`、手机 app 入口 | `NpcPanelSnapshotBuilder` | `PhoneContactBookRouteCoordinator（列表加载态）` | `LoadPhoneContactsAsync` | `RemoteDirectThreadProjector` | 无直接 executor | `PhoneDirectMessageMenu` |
| 手机私信 | `WorldLifecycleHooks`、联系人点击 | `HostDialogueSnapshotBuilder` | `RemoteDirectThreadSession` | `SendRemoteDirectAsync` | `RemoteDirectThreadProjector` | 无独立 host executor；提交 `FinalizeDialogueCommitAsync` 给 `Runtime.Local` | `PhoneDirectMessageMenu` |
| 现场群聊 | `WorldLifecycleHooks` | `GroupParticipantSnapshotBuilder` | `OnsiteGroupSession` | `SendGroupChatTurnAsync` | `GroupChatProjector` | 无独立 host executor；提交 `FinalizeDialogueCommitAsync` 给 `Runtime.Local` | `OnsiteGroupChatOverlay` |
| 手机主动群聊 | `WorldLifecycleHooks`、手机群入口 | `GroupParticipantSnapshotBuilder` | `PhoneGroupThreadSession` | `SendGroupChatTurnAsync` | `GroupChatProjector` | 无独立 host executor；提交 `FinalizeDialogueCommitAsync` 给 `Runtime.Local` | `PhoneActiveGroupChatMenu` |
| 物品 carrier | `ItemCarrierHooks` | `TradeSnapshotBuilder` | 无独立 session | `FinalizeTransactionalActionsAsync` | `ItemCarrierProjector` | `ItemGiftHostExecutor` | `ItemTextCarrierBase` |
| 交易 / 给物 | `NpcInteractionHooks`、对话动作提交 | `TradeSnapshotBuilder` | `AiDialogueSurfaceSession` | `FinalizeTransactionalActionsAsync` | `HostActionProjector` | `TradeHostExecutor` | `AiDialogueMenu` + 宿主商店/文本 carrier |
| 自定义物品生成 | `ItemCarrierHooks`、交易/奖励动作 | `ItemCreationSnapshotBuilder` | 无独立 session | `FinalizeTransactionalActionsAsync` | `HostActionProjector` | `StardewItemInstantiationExecutor` | `ItemTextCarrierBase` |
| 主动对话 | `WorldLifecycleHooks`、接触触发 | `HostDialogueSnapshotBuilder` | `AiDialogueSurfaceSession` | `SendPrivateDialogueAsync` | `AiDialogueSurfaceProjector` | 无独立 host executor；提交 `FinalizeDialogueCommitAsync` 给 `Runtime.Local` | `AiDialogueMenu` |
| 自动行动 | `WorldLifecycleHooks` | `AutoActionSnapshotBuilder` | 无独立 session | `FinalizeTransactionalActionsAsync` | `HostActionProjector` | `NpcAutoActionExecutor` | 宿主实际行动 |
| 回日程 | `WorldLifecycleHooks` | `ScheduleSnapshotBuilder` | 无独立 session | 无单独 AI 请求 | 无单独 projector | `NpcScheduleRestoreExecutor` | 宿主实际位置恢复 |

## 6. 固定 Transport 方法名

以后 `StardewRuntimeTransportClient` 只允许有下面这些正式入口：

1. `SendPrivateDialogueAsync(StardewPrivateDialogueSnapshotDto request, CancellationToken ct)`
2. `StreamPrivateDialogueAsync(StardewPrivateDialogueSnapshotDto request, CancellationToken ct)`
3. `SendThoughtAsync(StardewThoughtSnapshotDto request, CancellationToken ct)`
4. `SendRemoteDirectAsync(StardewRemoteDirectSnapshotDto request, CancellationToken ct)`
5. `SendGroupChatTurnAsync(StardewGroupChatSnapshotDto request, CancellationToken ct)`
6. `LoadNpcPanelBundleAsync(StardewNpcPanelBundleRequestDto request, CancellationToken ct)`
7. `LoadPhoneContactsAsync(StardewPhoneContactListRequestDto request, CancellationToken ct)`
8. `FinalizeDialogueCommitAsync(StardewDialogueCommitCommandDto command, CancellationToken ct)`
9. `FinalizeTransactionalActionsAsync(StardewTransactionalCommitCommandDto command, CancellationToken ct)`
10. `ProbeRuntimeHealthAsync(CancellationToken ct)`

## 6A. 私聊流式显示固定方法名

`AiDialogueSurfaceSession` 固定新增：

1. `BeginThinking(string waitingCopy)`
2. `AppendStreamChunk(string visibleTextDelta, int sequence)`
3. `MarkStreamCompleted()`
4. `MarkExplicitFailure(string failureCopy)`

`AiDialogueSurfaceProjector` 固定新增：

1. `ProjectThinkingCopy(...)`
2. `ProjectStreamChunk(...)`
3. `ProjectExplicitFailure(...)`

固定规则：

1. `BeginThinking` 必须先于第一条文本 chunk。
2. `AppendStreamChunk` 只追加玩家可见文本，不追加系统术语。
3. `MarkExplicitFailure` 输出的最终 copy 必须带：
   - `(ai回复失败)`
4. `MarkStreamCompleted` 不等于 committed；committed 仍只在 finalize 成功后升级。

死规则：

1. 不允许继续保留旧 request builder 主线
2. 不允许继续保留旧 thought 上下文拼装主线
3. 不允许继续保留旧 remote direct request 拼装主线
4. 不允许继续保留旧 group chat request 拼装主线
5. 不允许继续保留 transport 里直接改 menu 的旧 projector 主线

## 7. 固定 DTO 名字清单

### 7.1 Snapshot DTO

固定名字：

1. `StardewPrivateDialogueSnapshotDto`
2. `StardewThoughtSnapshotDto`
3. `StardewRemoteDirectSnapshotDto`
4. `StardewGroupChatSnapshotDto`
5. `StardewNpcPanelBundleRequestDto`
6. `StardewPhoneContactListRequestDto`
7. `StardewTradeSnapshotDto`
8. `StardewItemCreationSnapshotDto`
9. `StardewAutoActionSnapshotDto`
10. `StardewScheduleSnapshotDto`

### 7.2 Response DTO

固定名字：

1. `StardewDialogueEnvelopeDto`
2. `StardewDialogueStreamChunkDto`
3. `StardewThoughtEnvelopeDto`
4. `StardewRemoteDirectEnvelopeDto`
5. `StardewGroupTurnEnvelopeDto`
6. `StardewNpcPanelBundleDto`
7. `StardewPhoneContactListDto`

### 7.3 Commit / Receipt DTO

固定名字：

1. `StardewDialogueCommitCommandDto`
2. `StardewTransactionalCommitCommandDto`
3. `StardewHostApplyReceiptDto`
4. `StardewItemEventReceiptDto`
5. `StardewTradeReceiptDto`
6. `StardewScheduleRestoreReceiptDto`

死规则：

1. 这些 DTO 以后不准继续定义在 `RuntimeClient.cs`
2. 这些 DTO 也不准继续定义在 `NpcNaturalInteractionController.cs`
3. `UI` 只允许消费 projector 后的 surface model，不直接吃 runtime envelope

## 8. 旧类断电清单

如果要看逐文件旧代码怎么退、哪些 grep 词必须清零、替代新类叫什么，只认下面这份专表：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-current-code-retirement-and-cutover-appendix.md`

本文只冻结未来分层、transport 方法名、DTO 名字和功能落点，不再正文里复制第二份旧类断电表。

## 9. 每个功能的完工判断

### 9.1 AI 私聊

只有同时满足下面条件才算做完：

1. request 由 `HostDialogueSnapshotBuilder` 产出
2. `RuntimeTransport` 只负责发请求
3. `AiDialogueSurfaceProjector` 只负责翻 UI
4. committed 只在 finalize 成功后升级

### 9.2 信息面板

只有同时满足下面条件才算做完：

1. `NpcInfoPanelSurfaceSession` 成为唯一 session owner
2. `GroupHistoryTab` 已区分空态/未开放/失败
3. `Thought` 不混进普通聊天历史

### 9.3 手机私信和群聊

只有同时满足下面条件才算做完：

1. 线程 key 已固定
2. `unread / dnd / availability` 不再靠 build 开关硬填
3. 正式入口不再依赖热键

### 9.4 交易、给物、物品生成

只有同时满足下面条件才算做完：

1. `Projector` 和 `Executor` 已分开
2. carrier 成功不再冒充 committed
3. 实例化和宿主写回走 executor

### 9.5 自动行动和回日程

只有同时满足下面条件才算做完：

1. `NpcAutoActionExecutor` 独立存在
2. `NpcScheduleRestoreExecutor` 独立存在
3. 自动行动结束后会显式尝试回日程

## 10. 大白话结论

以后谁再来改 `Superpowers.Stardew.Mod`，不能再说：

1. “先在 controller 里顺手拼一下 request”
2. “先在 runtime client 里顺手投影到菜单”
3. “先用热键顶着，后面再接正式入口”
4. “carrier 已经显示了，就算 committed”

固定施工口径就是：

1. 先把 hook 和 snapshot builder 分开
2. 再把 session owner 立住
3. 再把 transport、projector、executor 拆开
4. 最后才接 surface 和宿主写回
