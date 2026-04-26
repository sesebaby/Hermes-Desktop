# Superpowers Launcher 接口与类落点附件

## 1. 文档定位

这份附件专门解决桌面程序现在还缺的最后一层：

1. 合同已经有了
2. 页面也大概有了
3. 但以后代码到底落到哪些目录、哪些类、哪些桥接接口，还不够死

所以本文固定回答 5 个问题：

1. `Launcher` 未来代码目录怎么拆
2. `Launcher.Supervisor` 未来给桌面的桥怎么落
3. 每个 `Application Service` 具体放哪里
4. 每个页面、`ViewModel`、页面状态模型怎么落
5. 现有类什么时候还能留，什么时候必须退役

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-current-code-retirement-and-rebuild-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-implementation-order-and-service-split-appendix.md`
- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
- `docs/superpowers/contracts/product/launcher-auth-session-contract.md`
- `docs/superpowers/contracts/product/stardew-launcher-workspace-ia.md`
- `docs/superpowers/contracts/product/stardew-mod-package-install-update-rollback-contract.md`
- `docs/superpowers/contracts/product/repair-update-recheck-state-machine.md`
- `docs/superpowers/contracts/product/support-ticket-and-diagnostic-bundle-contract.md`
- `docs/superpowers/contracts/product/launcher-notification-feed-contract.md`
- `docs/superpowers/contracts/product/redeem-request-and-receipt-contract.md`
- `docs/superpowers/contracts/product/launcher-product-catalog-visibility-contract.md`
- `docs/superpowers/contracts/product/player-entitlement-visibility-contract.md`
- `docs/superpowers/contracts/product/purchase-handoff-state-machine.md`
- `docs/superpowers/contracts/product/support-closure-state-machine.md`
- `docs/superpowers/contracts/product/launcher-support-surface-flow.md`
- `docs/superpowers/contracts/product/launcher-game-settings-surface-contract.md`

## 2. 大白话总原则

以后桌面程序这条线，固定按 6 层落代码：

1. `View`
   - XAML
   - 只管显示
2. `Shell ViewModel`
   - 只管导航、当前页、顶层状态条
3. `Page ViewModel`
   - 只管某一页的状态和按钮
4. `Application Service`
   - 真正吃合同
   - 真正调桥接接口
5. `Bridge`
   - 连接 `Launcher.Supervisor` 或 `Cloud`
6. `Authority Core`
   - 真正的 readiness、修复、产品、回执真相

死规则：

1. `ViewModel` 不准直接 `HttpClient`
2. `ViewModel` 不准直接读本地 `json`
3. `ViewModel` 不准直接 `Process.Start`
4. `XAML` 不准绑原始合同 DTO
5. 页面文案不准散落在构造函数里硬编码

## 3. 未来代码目录总图

### 3.1 `Launcher`

未来正式目录固定改成下面这套：

```text
src/Superpowers.Launcher/
  Application/
    Account/
    Launch/
    Games/Stardew/
    Packages/
    Support/
    Notifications/
    Commerce/
    Shared/
  Bridges/
    Supervisor/
    Cloud/
  Presentation/
    Shell/
    Home/
    Games/Stardew/
    Commerce/
    Notifications/
    Support/
    Settings/
    Shared/
  Copy/
  Views/
```

各层职责固定为：

1. `Application/*`
   - 放 `Application Service`
   - 放页面状态模型
   - 放 service 返回 DTO
2. `Bridges/Supervisor/*`
   - 只负责和 `Launcher.Supervisor` 通话
3. `Bridges/Cloud/*`
   - 只负责和云端产品、账号、通知、支持接口通话
4. `Presentation/*`
   - 放 `Shell ViewModel`、`Page ViewModel`、页面级 command model
5. `Copy/*`
   - 放玩家文案映射
   - 不能再放在 `LauncherUiModels.cs`

### 3.2 `Launcher.Supervisor`

未来正式目录固定补成下面这套：

```text
src/Superpowers.Launcher.Supervisor/
  Readiness/
  State/
  Facades/
  Operations/
  Packages/
  Diagnostics/
```

各层职责固定为：

1. `Readiness/*`
   - 单一 readiness truth
2. `State/*`
   - `RuntimePreflightFact`
   - `RuntimeHealthFact`
   - `RecoveryEntryRef`
3. `Facades/*`
   - 专门给 `Launcher` 调用
4. `Operations/*`
   - 启动、重检、修复、更新动作
5. `Packages/*`
   - 包安装、更新、回滚的执行入口
6. `Diagnostics/*`
   - 问题包采集、操作回执、诊断输出

## 4. 固定 Application Service 清单与落点

### 4.1 `AccountSessionApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Account/AccountSessionApplicationService.cs`
- 直接吃：
  - `launcher-auth-session-contract.md`
- 直接依赖桥：
  - `IAccountSessionBridge`
- 直接产出：
  - `LauncherSessionState`
  - `SessionBannerState`
- 替代旧做法：
  - 页面各自猜登录态

### 4.2 `LaunchReadinessApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Launch/LaunchReadinessApplicationService.cs`
- 直接吃：
  - `launcher-supervisor-boundary-contract.md`
  - `supervisor-preflight-input-matrix.md`
- 直接依赖桥：
  - `ILaunchReadinessBridge`
- 直接产出：
  - `LaunchPrimaryCardState`
  - `LaunchReadinessSummaryState`
- 替代旧做法：
  - `LauncherShellViewModel` 直接读 `launch-readiness-verdict.json`

### 4.3 `RuntimeStatusApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Launch/RuntimeStatusApplicationService.cs`
- 直接吃：
  - `launcher-supervisor-boundary-contract.md`
  - `stardew-launcher-workspace-ia.md`
- 直接依赖桥：
  - `IRuntimeStatusBridge`
- 直接产出：
  - `RuntimeStatusPanelState`
- 理由：
  - `运行状态` 是工作区一级区块，不能再混在 readiness 里顺手带出来

### 4.4 `LaunchExecutionApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Launch/LaunchExecutionApplicationService.cs`
- 直接吃：
  - `launcher-launch-orchestration-state-machine.md`
- 直接依赖桥：
  - `ILaunchExecutionBridge`
- 直接产出：
  - `LaunchExecutionState`
  - `LaunchExecutionReceiptState`
- 替代旧做法：
  - `LauncherShellViewModel.ExecuteLaunchAsync`

### 4.5 `StardewWorkspaceApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Games/Stardew/StardewWorkspaceApplicationService.cs`
- 直接吃：
  - `stardew-launcher-workspace-ia.md`
- 直接依赖：
  - `LaunchReadinessApplicationService`
  - `RuntimeStatusApplicationService`
  - `ModPackageManagementApplicationService`
  - `RepairUpdateRecheckApplicationService`
  - `SupportTicketApplicationService`
  - `GameSettingsApplicationService`
- 直接产出：
  - `StardewWorkspaceState`
- 理由：
  - 这个 service 只负责把 5 个区块组装成一个工作区，不自己拥有底层真相

### 4.6 `ModPackageManagementApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Packages/ModPackageManagementApplicationService.cs`
- 直接吃：
  - `stardew-mod-package-install-update-rollback-contract.md`
- 直接依赖桥：
  - `IModPackageOperationBridge`
- 直接产出：
  - `ModPackagePanelState`
  - `ModPackageOperationReceiptState`
- 替代旧做法：
  - 页面自己解释“已装/未装/可回滚”

### 4.7 `RepairUpdateRecheckApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Packages/RepairUpdateRecheckApplicationService.cs`
- 直接吃：
  - `repair-update-recheck-state-machine.md`
- 直接依赖桥：
  - `IRepairUpdateRecheckBridge`
- 直接产出：
  - `RepairPanelState`
  - `RepairActionReceiptState`
- 理由：
  - `修复/更新/重检` 不等于 `mod 包管理`
  - 这块必须单列，不然后面实现一定会混

### 4.8 `SupportTicketApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Support/SupportTicketApplicationService.cs`
- 直接吃：
  - `support-ticket-and-diagnostic-bundle-contract.md`
  - `launcher-support-surface-flow.md`
  - `support-closure-state-machine.md`
- 直接依赖桥：
  - `ISupportTicketBridge`
- 直接产出：
  - `SupportPageState`
  - `SupportDraftState`
  - `DiagnosticBundleState`
  - `SupportReceiptState`
  - `SupportHistoryState`
- 页面能力补充：
  - 当工单进入 `needs_player_reply` 时，固定走同一 `SupportTicketApplicationService` 的补充说明入口，不再让 `SupportViewModel` 自己拼第二套提交流
- 替代旧做法：
  - `SupportViewModel` 自己混合提交、失败文案、回执解释

### 4.9 `NotificationFeedApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Notifications/NotificationFeedApplicationService.cs`
- 直接吃：
  - `launcher-notification-feed-contract.md`
- 直接依赖桥：
  - `INotificationFeedBridge`
- 直接产出：
  - `NotificationFeedState`
  - `NotificationItemState`
  - `HomeAlertExcerptState`
  - `NotificationActionTarget`
- 替代旧做法：
  - `NotificationsViewModel` 构造函数里写死两条通知

### 4.10 `ProductCatalogApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Commerce/ProductCatalogApplicationService.cs`
- 直接吃：
  - `launcher-product-catalog-visibility-contract.md`
  - `narrative-base-pack-contract.md`
  - `sku-entitlement-claim-matrix.md`
- 直接依赖桥：
  - `IProductCatalogBridge`
- 直接产出：
  - `ProductCatalogPageState`
  - `CatalogItemCardState`

### 4.11 `EntitlementVisibilityApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Commerce/EntitlementVisibilityApplicationService.cs`
- 直接吃：
  - `player-entitlement-visibility-contract.md`
- 直接依赖桥：
  - `IEntitlementBridge`
- 直接产出：
  - `EntitlementSummaryState`
  - `OwnedProductState`

### 4.12 `RedeemApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Commerce/RedeemApplicationService.cs`
- 直接吃：
  - `redeem-request-and-receipt-contract.md`
  - `purchase-handoff-state-machine.md`
- 直接依赖桥：
  - `IRedeemBridge`
  - `IEntitlementBridge`
- 直接产出：
  - `RedeemSubmissionState`
  - `RedeemReceiptState`
- 替代旧做法：
  - `ProductRedeemViewModel` 本地假成功

### 4.13 `PurchaseHandoffApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Commerce/PurchaseHandoffApplicationService.cs`
- 直接吃：
  - `purchase-handoff-state-machine.md`
- 直接依赖桥：
  - `IPurchaseHandoffBridge`
  - `IEntitlementBridge`
- 直接产出：
  - `PurchaseHandoffState`
  - `PurchaseReturnState`
- 理由：
  - 外部购买跳出、回来、等待兑换、刷新权益，这一整段必须有单独 owner
  - 不能把这条链散在产品页、兑换页、权益页里各做一点

### 4.14 `GameSettingsApplicationService`

- 目标文件：
  - `src/Superpowers.Launcher/Application/Games/Stardew/GameSettingsApplicationService.cs`
- 直接吃：
  - `stardew-launcher-workspace-ia.md`
  - `launcher-game-settings-surface-contract.md`
- 直接依赖桥：
  - `IGameSettingsBridge`
- 直接产出：
  - `GameSettingsPanelState`
  - `GameSettingsReceiptState`
- 理由：
  - `游戏设置` 是工作区正式一级区块，不能继续只靠 `StardewGameConfigViewModel` 本地拼文案和路径状态

## 5. 固定 Bridge 清单与落点

### 5.1 `Launcher -> Supervisor`

固定新增下面 7 个桥接口：

1. `ILaunchReadinessBridge`
   - 目标文件：
     - `src/Superpowers.Launcher/Bridges/Supervisor/ILaunchReadinessBridge.cs`
2. `IRuntimeStatusBridge`
   - 目标文件：
     - `src/Superpowers.Launcher/Bridges/Supervisor/IRuntimeStatusBridge.cs`
3. `ILaunchExecutionBridge`
   - 目标文件：
     - `src/Superpowers.Launcher/Bridges/Supervisor/ILaunchExecutionBridge.cs`
4. `IModPackageOperationBridge`
   - 目标文件：
     - `src/Superpowers.Launcher/Bridges/Supervisor/IModPackageOperationBridge.cs`
5. `IRepairUpdateRecheckBridge`
   - 目标文件：
     - `src/Superpowers.Launcher/Bridges/Supervisor/IRepairUpdateRecheckBridge.cs`
6. `IGameSettingsBridge`
   - 目标文件：
     - `src/Superpowers.Launcher/Bridges/Supervisor/IGameSettingsBridge.cs`

`Launcher.Supervisor` 端固定对应 6 个 façade：

1. `LaunchReadinessFacade`
   - `src/Superpowers.Launcher.Supervisor/Facades/LaunchReadinessFacade.cs`
2. `RuntimeStatusFacade`
   - `src/Superpowers.Launcher.Supervisor/Facades/RuntimeStatusFacade.cs`
3. `LaunchExecutionFacade`
   - `src/Superpowers.Launcher.Supervisor/Facades/LaunchExecutionFacade.cs`
4. `ModPackageOperationFacade`
   - `src/Superpowers.Launcher.Supervisor/Facades/ModPackageOperationFacade.cs`
5. `RepairUpdateRecheckFacade`
   - `src/Superpowers.Launcher.Supervisor/Facades/RepairUpdateRecheckFacade.cs`
6. `GameSettingsFacade`
   - `src/Superpowers.Launcher.Supervisor/Facades/GameSettingsFacade.cs`

### 5.2 `Launcher -> Cloud`

固定新增下面 7 个桥接口：

1. `IAccountSessionBridge`
2. `IProductCatalogBridge`
3. `IEntitlementBridge`
4. `IPurchaseHandoffBridge`
5. `IRedeemBridge`
6. `INotificationFeedBridge`
7. `ISupportTicketBridge`

固定目录：

- `src/Superpowers.Launcher/Bridges/Cloud/`

死规则：

1. 所有桥都只做通信和协议翻译
2. 桥里不准拼玩家页面文案
3. 桥里不准自己决定最终业务状态

## 6. 页面、ViewModel、状态模型的正式落点

### 6.1 顶层壳

#### `LauncherShellViewModel`

- 未来保留文件：
  - `src/Superpowers.Launcher/Presentation/Shell/LauncherShellViewModel.cs`
- 只保留：
  - 一级导航
  - 当前页面切换
  - 顶部会话条
  - 全局通知角标
- 必须迁出：
  - `LoadLocalReadinessAsync`
  - `ExecuteLaunchAsync`
  - `SubmitSupportAsync`
  - `GetReadinessArtifactCandidates`
  - `ResolveDefaultSmapiPath`
- 以后直接依赖：
  - `AccountSessionApplicationService`
  - `NotificationFeedApplicationService`
- 固定配套状态模型：
  - `LauncherNavigationState`
  - `SessionBannerState`

### 6.2 首页

#### `HomeViewModel`

- 未来文件：
  - `src/Superpowers.Launcher/Presentation/Home/HomeViewModel.cs`
- 只保留：
  - 首页展示
- 不再保留：
  - 预置假 verdict
- 固定依赖：
  - `LaunchReadinessApplicationService`
- 固定状态模型：
  - `HomePageState`
  - `LaunchPrimaryCardState`

### 6.3 游戏页

#### `GameLibraryViewModel`

- 未来文件：
  - `src/Superpowers.Launcher/Presentation/Games/Stardew/GameLibraryViewModel.cs`
- 只负责：
  - 游戏列表
  - 进入工作区
- 固定依赖：
  - `LaunchReadinessApplicationService`
  - `EntitlementVisibilityApplicationService`
- 固定状态模型：
  - `GameLibraryState`
  - `GameCardState`

#### `StardewGameConfigViewModel`

- 现状归类：
  - `rebuild around kept shell`
- 未来改名为：
  - `StardewWorkspaceViewModel`
- 未来文件：
  - `src/Superpowers.Launcher/Presentation/Games/Stardew/StardewWorkspaceViewModel.cs`
- 只负责：
  - 承接 `概览 / 运行状态 / Mod 管理 / 帮助与修复 / 游戏设置`
- 必须迁出：
  - 失败文案硬编码
  - 固定配置说明数组
  - 直接解释 verdict 的页面业务
- 固定依赖：
  - `StardewWorkspaceApplicationService`
- 固定状态模型：
  - `StardewWorkspaceState`
  - `OverviewPanelState`
  - `RuntimeStatusPanelState`
  - `ModPackagePanelState`
  - `RepairPanelState`
  - `GameSettingsPanelState`

### 6.4 产品与兑换页

#### `ProductRedeemViewModel`

- 现状归类：
  - `retired business mainline`
- 未来拆成 3 个 ViewModel：
  1. `ProductCatalogViewModel`
     - `src/Superpowers.Launcher/Presentation/Commerce/ProductCatalogViewModel.cs`
  2. `RedeemViewModel`
     - `src/Superpowers.Launcher/Presentation/Commerce/RedeemViewModel.cs`
  3. `MyEntitlementsViewModel`
     - `src/Superpowers.Launcher/Presentation/Commerce/MyEntitlementsViewModel.cs`
- 固定依赖：
  - `ProductCatalogApplicationService`
  - `PurchaseHandoffApplicationService`
  - `EntitlementVisibilityApplicationService`
  - `RedeemApplicationService`
  - `AccountSessionApplicationService`
- 固定状态模型：
  - `ProductCatalogPageState`
  - `CatalogItemCardState`
  - `EntitlementSummaryState`
  - `PurchaseHandoffState`
  - `RedeemSubmissionState`
  - `RedeemReceiptState`

### 6.5 通知页

#### `NotificationsViewModel`

- 未来文件：
  - `src/Superpowers.Launcher/Presentation/Notifications/NotificationsViewModel.cs`
- 只负责：
  - 列表显示
  - 已读未读动作
- 固定依赖：
  - `NotificationFeedApplicationService`
- 固定状态模型：
  - `NotificationFeedState`
  - `NotificationItemState`

### 6.6 支持与帮助页

#### `SupportViewModel`

- 未来文件：
  - `src/Superpowers.Launcher/Presentation/Support/SupportViewModel.cs`
- 只负责：
  - 页面区块状态
  - 提交按钮状态
  - 回执显示
- 不再负责：
  - 直接提交流程
  - 失败类型解释真相
- 固定依赖：
  - `SupportTicketApplicationService`
  - `RepairUpdateRecheckApplicationService`
- 固定状态模型：
  - `SupportPageState`
  - `SupportDraftState`
  - `DiagnosticBundleState`
  - `SupportReceiptState`
  - `SupportHistoryState`
  - `SupportFaqState`
  - `TextOnlyFallbackState`

### 6.7 设置页

#### `SettingsViewModel`

- 未来文件：
  - `src/Superpowers.Launcher/Presentation/Settings/SettingsViewModel.cs`
- 只保留：
  - 桌面体验设置
  - 本地偏好
- 不允许进入：
  - readiness 真相
  - entitlement 真相
  - prompt / provider 真相
- 固定状态模型：
  - `LauncherSettingsState`

### 6.8 拒绝原因页

#### `DeniedReasonViewModel`

- 未来文件：
  - `src/Superpowers.Launcher/Presentation/Shared/DeniedReasonViewModel.cs`
- 只负责：
  - 展示已被映射好的拒绝原因
- 固定依赖：
  - `LaunchReadinessApplicationService`
- 固定状态模型：
  - `DeniedReasonState`

### 6.9 账号面

账号面不能再只算“设置里的一个按钮”。

未来固定拆成下面这些 ViewModel：

1. `AccountEntryViewModel`
   - `src/Superpowers.Launcher/Presentation/Shared/AccountEntryViewModel.cs`
2. `LoginViewModel`
   - `src/Superpowers.Launcher/Presentation/Shared/LoginViewModel.cs`
3. `RegisterViewModel`
   - `src/Superpowers.Launcher/Presentation/Shared/RegisterViewModel.cs`
4. `AccountHomeViewModel`
   - `src/Superpowers.Launcher/Presentation/Shared/AccountHomeViewModel.cs`
5. `SessionExpiredBannerViewModel`
   - `src/Superpowers.Launcher/Presentation/Shared/SessionExpiredBannerViewModel.cs`

固定依赖：

1. `AccountSessionApplicationService`
2. `EntitlementVisibilityApplicationService`

固定状态模型：

1. `AccountSurfaceState`
2. `LauncherSessionState`
3. `SessionExpiredBannerState`

### 6.10 固定 route id 和 action target

以后桌面页面跳转，不允许再靠字符串文案临时拼。

固定 route id：

1. `home`
2. `games`
3. `games/stardew`
4. `games/stardew/runtime-status`
5. `games/stardew/mod-packages`
6. `games/stardew/help-repair`
7. `games/stardew/settings`
8. `products`
9. `products/redeem`
10. `products/entitlements`
11. `notifications`
12. `support`
13. `settings`
14. `account`
15. `account/login`
16. `account/register`

固定 action target：

1. `go_home`
2. `open_stardew_workspace`
3. `open_runtime_status`
4. `open_mod_packages`
5. `open_help_repair`
6. `open_redeem`
7. `open_entitlements`
8. `open_notifications`
9. `open_support`
10. `open_login`
11. `open_register`

## 7. `LauncherUiModels.cs` 必须拆文件

当前这个文件太杂，以后固定拆成下面这些文件：

1. `Presentation/Shared/LauncherSurfaceViewModel.cs`
2. `Presentation/Shared/LauncherActionViewModel.cs`
3. `Application/Launch/LaunchPrimaryCardState.cs`
4. `Application/Games/Stardew/StardewWorkspaceState.cs`
5. `Application/Notifications/NotificationItemState.cs`
6. `Application/Settings/LauncherSettingsState.cs`
7. `Presentation/Shared/DeniedReasonViewModel.cs`

固定规则：

1. 页面状态模型放 `Application/*`
2. 只跟显示强绑定的壳放 `Presentation/*`
3. 不准再把 7 种不同页面的东西塞回一个文件

## 8. 固定 DTO 名字清单

以后实现桌面程序，下面这些 DTO 名字不允许临时乱起：

1. `GameWorkspaceSummaryDto`
2. `LaunchReadinessSummaryDto`
3. `LaunchExecutionReceiptDto`
4. `RuntimeStatusDto`
5. `ModPackageOperationReceiptDto`
6. `RepairActionReceiptDto`
7. `SupportTicketDraftDto`
8. `SupportTicketReceiptDto`
9. `SupportTicketHistoryItemDto`
10. `NotificationFeedItemDto`
11. `AccountSessionDto`
12. `EntitlementSummaryDto`
13. `RedeemReceiptDto`
14. `PurchaseReturnDto`

死规则：

1. `Request / Response / Receipt / Summary` 不准继续写进 `ViewModel.cs`
2. 页面状态模型不等于 Cloud DTO
3. 页面状态模型也不等于 Supervisor DTO

## 9. 玩家文案落点

当前 `LauncherPlayerCopy` 只能算临时过渡。

未来固定拆成：

1. `src/Superpowers.Launcher/Copy/FailureCopyMapper.cs`
2. `src/Superpowers.Launcher/Copy/LauncherSurfaceCopyCatalog.cs`

死规则：

1. `FailureCopyMapper` 只做 `failureClass -> 玩家文案`
2. `LauncherSurfaceCopyCatalog` 只做固定页面标题、副标题、空态文案
3. 文案不能再散落在：
   - `HomeViewModel`
   - `StardewGameConfigViewModel`
   - `SupportViewModel`
   - `LauncherShellViewModel`

## 10. 旧类保留与退役判定

### 10.1 `kept shell`

固定保留为壳的文件：

1. `App.xaml`
2. `App.xaml.cs`
3. `MainWindow.xaml`
4. `MainWindow.xaml.cs`
5. 全部 `Views/*.xaml`
6. `ViewModelBase.cs`
7. `Commands.cs`

### 10.2 `rebuild around kept shell`

固定重建但允许保留壳的文件：

1. `LauncherShellViewModel.cs`
2. `HomeViewModel.cs`
3. `StardewGameConfigViewModel.cs`
4. `NotificationsViewModel.cs`
5. `SupportViewModel.cs`
6. `SettingsViewModel.cs`
7. `LauncherUiModels.cs`

### 10.3 `retired business mainline`

逐文件旧类、旧方法、断电清单只认下面这份专表：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-current-code-retirement-and-rebuild-appendix.md`

本文只保留“未来该落到哪”的设计，不再在正文里重复抄第二份退役名单。

### 10.4 `kept authority core`

固定保留的 authority core：

1. `LaunchReadinessVerdict.cs`
2. `CapabilityAccessDecision.cs`
3. `LaunchReadinessPolicySnapshot.cs`
4. `StardewReadinessEvaluator.cs`
5. `RuntimePreflightFact.cs`
6. `RuntimeHealthFact.cs`
7. `RuntimePreflightRef.cs`
8. `RecoveryEntryRef.cs`

## 11. 完工判断

只有同时满足下面条件，桌面设计才算真正细化到可开工：

1. 每个页面都能说清：
   - 页面 owner 是谁
   - service 是谁
   - bridge 是谁
   - 页面状态模型是谁
2. 每个旧类都能说清：
   - 保留壳
   - 重建
   - 退役
3. `Launcher` 不再直接碰：
   - 本地 readiness 文件
   - 本地启动命令
   - 本地假回执
4. `Launcher.Supervisor` 的 façade 清单已经固定
5. 页面文案的正式落点已经固定，不再让实现时自由发挥
6. `账号面` 和 `购买回流` 都已经有单独页面状态和 owner
7. 固定 route id 和 action target 已经登记
8. DTO 名字清单已经登记

## 12. 大白话结论

以后谁再来改桌面程序，不能再说：

- “先在 ViewModel 里写一下”
- “先读个本地文件顶着”
- “先假装有兑换成功”
- “支持提交先返回一个 local-ticket”

固定施工口径就是：

1. 先把目录和类落点按本文立住
2. 再按 service 和 bridge 一层层接真相
3. 最后才改页面壳和按钮绑定
