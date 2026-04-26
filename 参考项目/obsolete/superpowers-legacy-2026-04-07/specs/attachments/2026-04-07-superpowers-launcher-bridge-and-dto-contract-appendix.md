# Superpowers Launcher 桥接与 DTO 合同附件

## 1. 文档定位

这份附件专门解决最后一个容易跑偏的问题：

1. 现在已经知道有哪些页面
2. 也知道有哪些 service
3. 但还没有把 `Launcher` 调 `Launcher.Supervisor / Cloud` 的方法名、请求 DTO、回执 DTO 写死

所以本文固定回答：

1. `Launcher -> Launcher.Supervisor` 具体调哪些桥
2. `Launcher -> Cloud` 具体调哪些桥
3. 每个桥的方法名叫什么
4. 每个方法最少带哪些字段
5. 哪些旧代码入口从现在起绝对不准再用

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-interface-and-class-landing-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-implementation-order-and-service-split-appendix.md`
- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
- `docs/superpowers/contracts/product/launcher-auth-session-contract.md`
- `docs/superpowers/contracts/product/launcher-launch-orchestration-state-machine.md`
- `docs/superpowers/contracts/product/stardew-mod-package-install-update-rollback-contract.md`
- `docs/superpowers/contracts/product/repair-update-recheck-state-machine.md`
- `docs/superpowers/contracts/product/launcher-notification-feed-contract.md`
- `docs/superpowers/contracts/product/redeem-request-and-receipt-contract.md`
- `docs/superpowers/contracts/product/launcher-product-catalog-visibility-contract.md`
- `docs/superpowers/contracts/product/player-entitlement-visibility-contract.md`
- `docs/superpowers/contracts/product/purchase-handoff-state-machine.md`
- `docs/superpowers/contracts/product/support-ticket-and-diagnostic-bundle-contract.md`
- `docs/superpowers/contracts/product/support-closure-state-machine.md`
- `docs/superpowers/contracts/product/launcher-game-settings-surface-contract.md`

## 2. 大白话死规则

以后桌面程序和 authority owner 对接，固定只允许走桥接口。

不允许：

1. `ViewModel` 直接发 `HttpClient`
2. `ViewModel` 直接读本地 readiness 文件
3. `ViewModel` 直接 `Process.Start`
4. `Launcher` 直接摸 `Launcher.Supervisor` 内部类
5. `Launcher` 直接猜 DTO 结构

## 3. `Launcher -> Launcher.Supervisor` 固定桥

### 3.1 `ILaunchReadinessBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Supervisor/ILaunchReadinessBridge.cs`

固定方法：

1. `GetLaunchReadinessAsync(GetLaunchReadinessRequestDto request, CancellationToken ct)`
   - 返回：
     - `LaunchReadinessSummaryDto`

`GetLaunchReadinessRequestDto` 最少字段：

1. `gameId`
2. `surfaceId`
3. `launcherSessionId`

`LaunchReadinessSummaryDto` 最少字段：

1. `verdictId`
2. `gameId`
3. `verdict`
4. `primaryReasonCode`
5. `displayLabel`
6. `ctaKind`
7. `recoveryEntryRef`
8. `runtimePreflightRef`
9. `runtimeHealthRef`
10. `generatedAt`

### 3.2 `IRuntimeStatusBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Supervisor/IRuntimeStatusBridge.cs`

固定方法：

1. `GetRuntimeStatusAsync(GetRuntimeStatusRequestDto request, CancellationToken ct)`
   - 返回：
     - `RuntimeStatusDto`

`RuntimeStatusDto` 最少字段：

1. `gameId`
2. `runtimeHealthState`
3. `runtimePreflightState`
4. `failureClass`
5. `recoveryEntryRef`
6. `lastCheckedAt`

### 3.3 `ILaunchExecutionBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Supervisor/ILaunchExecutionBridge.cs`

固定方法：

1. `StartLaunchAsync(StartLaunchCommandDto command, CancellationToken ct)`
   - 返回：
     - `LaunchExecutionReceiptDto`
2. `GetLaunchExecutionAsync(GetLaunchExecutionRequestDto request, CancellationToken ct)`
   - 返回：
     - `LaunchExecutionReceiptDto`

`StartLaunchCommandDto` 最少字段：

1. `gameId`
2. `launcherSessionId`
3. `requestId`
4. `requestedBySurface`

`LaunchExecutionReceiptDto` 最少字段：

1. `operationReceiptId`
2. `gameId`
3. `launchState`
4. `failureClass`
5. `recoveryEntryRef`
6. `startedAt`
7. `finishedAt`

### 3.4 `IModPackageOperationBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Supervisor/IModPackageOperationBridge.cs`

固定方法：

1. `GetPackageStateAsync(GetPackageStateRequestDto request, CancellationToken ct)`
   - 返回：
     - `ModPackageStateDto`
2. `InstallPackageAsync(InstallPackageCommandDto command, CancellationToken ct)`
   - 返回：
     - `ModPackageOperationReceiptDto`
3. `UpdatePackageAsync(UpdatePackageCommandDto command, CancellationToken ct)`
   - 返回：
     - `ModPackageOperationReceiptDto`
4. `RollbackPackageAsync(RollbackPackageCommandDto command, CancellationToken ct)`
   - 返回：
     - `ModPackageOperationReceiptDto`

`ModPackageStateDto` 最少字段：

1. `packageId`
2. `gameId`
3. `installedVersion`
4. `targetVersion`
5. `actionState`
6. `failureClass`
7. `recoveryEntryRef`
8. `operationReceiptRef`

### 3.5 `IRepairUpdateRecheckBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Supervisor/IRepairUpdateRecheckBridge.cs`

固定方法：

1. `RunRepairAsync(RepairCommandDto command, CancellationToken ct)`
   - 返回：
     - `RepairActionReceiptDto`
2. `RunUpdateAsync(UpdateRuntimeCommandDto command, CancellationToken ct)`
   - 返回：
     - `RepairActionReceiptDto`
3. `RunRecheckAsync(RecheckCommandDto command, CancellationToken ct)`
   - 返回：
     - `RepairActionReceiptDto`
4. `GetRepairStateAsync(GetRepairStateRequestDto request, CancellationToken ct)`
   - 返回：
     - `RepairActionStateDto`

`RepairActionReceiptDto` 最少字段：

1. `operationReceiptId`
2. `gameId`
3. `actionKind`
4. `actionState`
5. `failureClass`
6. `recoveryEntryRef`
7. `finishedAt`

### 3.6 `IGameSettingsBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Supervisor/IGameSettingsBridge.cs`

固定方法：

1. `GetGameSettingsAsync(GetGameSettingsRequestDto request, CancellationToken ct)`
   - 返回：
     - `GameSettingsDto`
2. `SaveGameSettingsAsync(SaveGameSettingsCommandDto command, CancellationToken ct)`
   - 返回：
     - `GameSettingsReceiptDto`
3. `DetectGameSettingsAsync(DetectGameSettingsCommandDto command, CancellationToken ct)`
   - 返回：
     - `GameSettingsReceiptDto`

`GameSettingsDto` 最少字段：

1. `gameId`
2. `smapiPath`
3. `launchMode`
4. `pathState`
5. `pathFailureClass`
6. `lastValidatedAt`

`GameSettingsReceiptDto` 最少字段：

1. `operationReceiptId`
2. `gameId`
3. `settingsState`
4. `pathState`
5. `failureClass`
6. `issuedAt`

## 4. `Launcher -> Cloud` 固定桥

### 4.1 `IAccountSessionBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Cloud/IAccountSessionBridge.cs`

固定方法：

1. `RegisterAsync(RegisterCommandDto command, CancellationToken ct)`
   - 返回：
     - `AccountSessionDto`
2. `LoginAsync(LoginCommandDto command, CancellationToken ct)`
   - 返回：
     - `AccountSessionDto`
3. `RefreshSessionAsync(RefreshSessionCommandDto command, CancellationToken ct)`
   - 返回：
     - `AccountSessionDto`
4. `LogoutAsync(LogoutCommandDto command, CancellationToken ct)`
   - 返回：
     - `AccountSessionDto`

`AccountSessionDto` 最少字段：

1. `sessionId`
2. `accountId`
3. `displayName`
4. `sessionState`
5. `expiresAt`
6. `entitlementSnapshotRef`

### 4.2 `IProductCatalogBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Cloud/IProductCatalogBridge.cs`

固定方法：

1. `ListCatalogAsync(ListCatalogRequestDto request, CancellationToken ct)`
   - 返回：
     - `ProductCatalogPageDto`

`ProductCatalogPageDto` 最少字段：

1. `catalogVersion`
2. `items`

`CatalogItemDto` 最少字段：

1. `catalogItemId`
2. `skuId`
3. `displayName`
4. `family`
5. `listingState`
6. `entitlementState`
7. `supportClaimCopy`
8. `primaryActionLabel`
9. `primaryActionTarget`

### 4.3 `IEntitlementBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Cloud/IEntitlementBridge.cs`

固定方法：

1. `GetEntitlementsAsync(GetEntitlementsRequestDto request, CancellationToken ct)`
   - 返回：
     - `EntitlementSummaryDto`
2. `RefreshEntitlementsAsync(RefreshEntitlementsCommandDto command, CancellationToken ct)`
   - 返回：
     - `EntitlementSummaryDto`

### 4.4 `IPurchaseHandoffBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Cloud/IPurchaseHandoffBridge.cs`

固定方法：

1. `CreatePurchaseIntentAsync(CreatePurchaseIntentCommandDto command, CancellationToken ct)`
   - 返回：
     - `PurchaseHandoffStateDto`
2. `ConfirmPurchaseReturnAsync(ConfirmPurchaseReturnCommandDto command, CancellationToken ct)`
   - 返回：
     - `PurchaseReturnDto`

`PurchaseHandoffStateDto` 最少字段：

1. `purchaseIntentId`
2. `skuId`
3. `handoffState`
4. `externalTargetUrl`
5. `returnToken`

`PurchaseReturnDto` 最少字段：

1. `purchaseIntentId`
2. `returnState`
3. `redeemRequired`
4. `entitlementRefreshState`
5. `failureClass`

### 4.5 `IRedeemBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Cloud/IRedeemBridge.cs`

固定方法：

1. `SubmitRedeemAsync(SubmitRedeemCommandDto command, CancellationToken ct)`
   - 返回：
     - `RedeemReceiptDto`

`RedeemReceiptDto` 最少字段：

1. `receiptId`
2. `codeRef`
3. `redeemState`
4. `failureClass`
5. `entitlementRefreshRequested`
6. `issuedAt`

### 4.6 `INotificationFeedBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Cloud/INotificationFeedBridge.cs`

固定方法：

1. `ListNotificationsAsync(ListNotificationsRequestDto request, CancellationToken ct)`
   - 返回：
     - `NotificationFeedDto`
2. `MarkNotificationReadAsync(MarkNotificationReadCommandDto command, CancellationToken ct)`
   - 返回：
     - `NotificationFeedItemDto`

`NotificationFeedItemDto` 最少字段：

1. `notificationId`
2. `family`
3. `title`
4. `summary`
5. `issuedAt`
6. `isUnread`
7. `primaryActionLabel`
8. `primaryActionTarget`

### 4.7 `ISupportTicketBridge`

目标文件：

- `src/Superpowers.Launcher/Bridges/Cloud/ISupportTicketBridge.cs`

固定方法：

1. `SubmitSupportTicketAsync(SubmitSupportTicketCommandDto command, CancellationToken ct)`
   - 返回：
     - `SupportTicketReceiptDto`
2. `ListSupportTicketsAsync(ListSupportTicketsRequestDto request, CancellationToken ct)`
   - 返回：
     - `SupportTicketHistoryDto`
3. `ReplySupportTicketAsync(ReplySupportTicketCommandDto command, CancellationToken ct)`
   - 返回：
     - `SupportTicketReceiptDto`

`SupportTicketReceiptDto` 最少字段：

1. `ticketReceiptId`
2. `ticketId`
3. `ticketState`
4. `failureClass`
5. `recoveryEntryRef`
6. `bundleState`
7. `issuedAt`

## 5. service 和桥的固定绑定

固定绑定表：

1. `LaunchReadinessApplicationService`
   - 只能调：
     - `ILaunchReadinessBridge`
2. `RuntimeStatusApplicationService`
   - 只能调：
     - `IRuntimeStatusBridge`
3. `LaunchExecutionApplicationService`
   - 只能调：
     - `ILaunchExecutionBridge`
4. `ModPackageManagementApplicationService`
   - 只能调：
     - `IModPackageOperationBridge`
5. `RepairUpdateRecheckApplicationService`
   - 只能调：
     - `IRepairUpdateRecheckBridge`
6. `GameSettingsApplicationService`
   - 只能调：
     - `IGameSettingsBridge`
7. `AccountSessionApplicationService`
   - 只能调：
     - `IAccountSessionBridge`
8. `ProductCatalogApplicationService`
   - 只能调：
     - `IProductCatalogBridge`
9. `PurchaseHandoffApplicationService`
   - 只能调：
     - `IPurchaseHandoffBridge`
     - `IEntitlementBridge`
10. `EntitlementVisibilityApplicationService`
   - 只能调：
     - `IEntitlementBridge`
11. `RedeemApplicationService`
    - 只能调：
      - `IRedeemBridge`
      - `IEntitlementBridge`
12. `NotificationFeedApplicationService`
    - 只能调：
      - `INotificationFeedBridge`
13. `SupportTicketApplicationService`
    - 只能调：
      - `ISupportTicketBridge`

## 6. 旧入口断电清单

逐文件旧入口、旧方法、旧 DTO 断电清单，只认下面这份专表：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-current-code-retirement-and-rebuild-appendix.md`

本文只冻结新桥接口、新方法名和 DTO 名字，不再正文里复制第二份断电名单。

## 7. 完工判断

只有同时满足下面条件，这份桥接设计才算真的能指导实现：

1. 每个 service 都有唯一 bridge
2. 每个 bridge 都有固定方法名
3. 每个方法都有最少 DTO 字段
4. 旧入口断电清单已经写入退役文档
5. 任何新实现都不需要自己发明新的临时 DTO 名字

## 8. 大白话结论

以后实现桌面程序，不能再出现这种情况：

1. “我先在 ViewModel 里调一下接口”
2. “DTO 先临时起个名字”
3. “登录和兑换先共用一个返回结构”
4. “购买跳转回来先假装完成”

固定规则就是：

1. 先按 bridge 调
2. 再按 DTO 落
3. 最后才绑到页面
