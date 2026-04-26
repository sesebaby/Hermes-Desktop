# Superpowers Launcher 当前代码退役与重建附件

## 1. 文档定位

这份附件专门回答 3 个问题：

1. `Launcher` 和 `Launcher.Supervisor` 当前代码里，哪些还能留
2. 哪些只能当壳
3. 哪些必须断电重建

这不是“代码审美建议”。  
这是后面真正开改时的施工图。

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-current-implementation-divergence-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-interface-and-class-landing-appendix.md`
- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
- `docs/superpowers/contracts/product/launcher-auth-session-contract.md`
- `docs/superpowers/contracts/product/stardew-mod-package-install-update-rollback-contract.md`
- `docs/superpowers/contracts/product/support-ticket-and-diagnostic-bundle-contract.md`
- `docs/superpowers/contracts/product/launcher-notification-feed-contract.md`
- `docs/superpowers/contracts/product/repair-update-recheck-state-machine.md`
- `docs/superpowers/contracts/product/launcher-launch-orchestration-state-machine.md`
- `docs/superpowers/contracts/product/launcher-account-surface-state-machine.md`
- `docs/superpowers/contracts/product/stardew-launcher-workspace-ia.md`
- `docs/superpowers/contracts/product/supervisor-preflight-input-matrix.md`
- `docs/superpowers/contracts/product/redeem-request-and-receipt-contract.md`
- `docs/superpowers/contracts/product/launcher-product-catalog-visibility-contract.md`
- `docs/superpowers/contracts/product/player-entitlement-visibility-contract.md`
- `docs/superpowers/contracts/product/purchase-handoff-state-machine.md`
- `docs/superpowers/contracts/product/support-closure-state-machine.md`
- `docs/superpowers/contracts/product/launcher-support-surface-flow.md`

## 2. 总结论

当前桌面程序代码不是全废。  
但也绝对不能直接沿着现在的业务语义继续长。

固定分 3 桶：

1. `kept shell`
   - 页面
   - 基础 ViewModel 外壳
   - 命令壳
2. `kept authority core`
   - `LaunchReadinessVerdict`
   - `RuntimePreflightFact`
   - `RuntimeHealthFact`
   - `CapabilityAccessDecision`
   - `LaunchReadinessPolicySnapshot`
   - `StardewReadinessEvaluator`
3. `retired business mainline`
   - 本地读 json 拼 readiness 主线
   - 本地假兑换成功
   - 本地静态通知主线
   - 把支持提交、工单回执、问题包当“先有个壳再说”的业务语义

## 3. 当前文件处置表

### 3.1 Launcher 应用壳

#### `src/Superpowers.Launcher/App.xaml`

- 处置：
  - `kept shell`
- 理由：
  - 只是桌面应用入口壳
- 后续要求：
  - 不得在这里长业务判断

#### `src/Superpowers.Launcher/App.xaml.cs`

- 处置：
  - `kept shell`
- 理由：
  - 只适合放应用启动壳和依赖装配
- 后续要求：
  - 不得自己读 readiness 文件、产品文件、工单文件

#### `src/Superpowers.Launcher/MainWindow.xaml`

- 处置：
  - `kept shell`
- 理由：
  - 主窗口壳有价值
- 后续要求：
  - 只承接导航和容器结构

#### `src/Superpowers.Launcher/MainWindow.xaml.cs`

- 处置：
  - `kept shell`
- 后续要求：
  - 不得长出产品业务判断
  - 不得长出登录、兑换、支持、启动的 code-behind 主线

### 3.2 Launcher 共享 ViewModel 壳

#### `src/Superpowers.Launcher/ViewModels/ViewModelBase.cs`

- 处置：
  - `kept shell`

#### `src/Superpowers.Launcher/ViewModels/Commands.cs`

- 处置：
  - `kept shell`

#### `src/Superpowers.Launcher/ViewModels/LauncherUiModels.cs`

- 处置：
  - `partial keep`
- 保留：
  - `LauncherSurfaceViewModel`
  - `LauncherActionViewModel`
  - `StardewStatusViewModel`
  - `GameCardViewModel`
  - `GameLibraryViewModel`
  - `NotificationEntryViewModel`
  - `StardewLaunchSettingsViewModel`
  - `DeniedReasonViewModel`
  - `ToggleSettingViewModel`
- 退役语义：
  - `LauncherPlayerCopy` 里硬编码的 issue 文案主语义
- 后续要求：
  - 文案映射要迁到正式 copy / contract 驱动层

### 3.3 Launcher 页面级 ViewModel

#### `src/Superpowers.Launcher/ViewModels/LauncherShellViewModel.cs`

- 处置：
  - `rebuild around kept shell`
- 可保留：
  - 一级导航壳
  - surface 切换壳
  - 页面组合壳
- 必须退役：
  - `LoadLocalReadinessAsync`
  - `GetReadinessArtifactCandidates`
  - 直接读本地 `launch-readiness-verdict.json`
  - `ExecuteLaunchAsync` 里直接本地起 SMAPI 的临时主线
- 原因：
  - 它现在同时做：
    - 导航
    - readiness 读取
    - 启动判断
    - 启动执行
    - 支持提交桥接
  - 已经越界
- 重建落点：
  - 导航留在 `LauncherShellViewModel`
  - readiness 消费改为只吃 `Launcher.Supervisor` 标准结果
  - 启动动作改为调用正式 launch orchestration service
  - 支持提交改为 support application service

#### `src/Superpowers.Launcher/ViewModels/HomeViewModel.cs`

- 处置：
  - `kept shell`
- 可保留：
  - 首页主卡结构
- 必须退役：
  - 用假 `LaunchReadinessVerdict` 造预览主线
- 重建落点：
  - 只消费正式 `GameWorkspaceSummary`

#### `src/Superpowers.Launcher/ViewModels/StardewGameConfigViewModel.cs`

- 处置：
  - `rebuild around kept shell`
- 可保留：
  - 配置页壳
  - `ApplyVerdict` 入口壳
- 必须退役：
  - 把配置说明、失败文案、恢复文案固定死在本类
- 重建落点：
  - 只消费：
    - readiness
    - package state
    - repair/update/recheck state
    - game settings state

#### `src/Superpowers.Launcher/ViewModels/ProductRedeemViewModel.cs`

- 处置：
  - `retired business mainline`
- 原因：
  - 当前只是本地记一句“已记录”
  - 没有正式请求
  - 没有 receipt
  - 没有 entitlement refresh
- 重建落点：
  - 按：
    - `redeem-request-and-receipt-contract`
    - `purchase-handoff-state-machine`
    重做

#### `src/Superpowers.Launcher/ViewModels/NotificationsViewModel.cs`

- 处置：
  - `rebuild around kept shell`
- 可保留：
  - 列表壳
- 必须退役：
  - 当前静态通知数据
- 重建落点：
  - 改成消费正式 notification feed

#### `src/Superpowers.Launcher/ViewModels/SupportViewModel.cs`

- 处置：
  - `rebuild around kept shell`
- 可保留：
  - `Draft / Submitting / Submitted / Failed` 壳
  - 提交表单壳
- 必须退役：
  - 当前把提交服务和结果语义混在本类里
  - 当前没有正式 bundle flow owner
- 重建落点：
  - support application service
  - bundle collector
  - receipt reader

#### `src/Superpowers.Launcher/ViewModels/SettingsViewModel.cs`

- 处置：
  - `kept shell`
- 后续要求：
  - 只承接桌面体验设置
  - 不承接游戏业务状态真相

### 3.4 Launcher 视图

以下视图全部默认：

- `kept shell`

文件：

1. `src/Superpowers.Launcher/Views/HomeView.xaml`
2. `src/Superpowers.Launcher/Views/GameLibraryView.xaml`
3. `src/Superpowers.Launcher/Views/StardewGameConfigView.xaml`
4. `src/Superpowers.Launcher/Views/ProductRedeemView.xaml`
5. `src/Superpowers.Launcher/Views/NotificationsView.xaml`
6. `src/Superpowers.Launcher/Views/SupportView.xaml`
7. `src/Superpowers.Launcher/Views/SettingsView.xaml`
8. `src/Superpowers.Launcher/Views/DeniedReasonView.xaml`

死规则：

- 可以保留布局壳
- 不允许把现在临时业务语义继续绑死在 XAML 文案上
- 不允许在任何 `xaml.cs` 里补业务主线

### 3.5 Launcher.Supervisor

#### `src/Superpowers.Launcher.Supervisor/Readiness/LaunchReadinessVerdict.cs`

- 处置：
  - `kept authority core`
- 理由：
  - 这是正式单一 readiness truth 的好骨架
- 后续要求：
  - 继续按合同扩，不要并行再长第二套 verdict DTO

#### `src/Superpowers.Launcher.Supervisor/Readiness/CapabilityAccessDecision.cs`

- 处置：
  - `kept authority core`

#### `src/Superpowers.Launcher.Supervisor/Readiness/LaunchReadinessPolicySnapshot.cs`

- 处置：
  - `kept authority core`

#### `src/Superpowers.Launcher.Supervisor/Readiness/StardewReadinessEvaluator.cs`

- 处置：
  - `kept authority core`
- 原因：
  - 已经有比较清晰的单一 verdict 计算入口
- 后续要求：
  - 继续收口到：
    - `supervisor-preflight-input-matrix`
    - `launcher-launch-orchestration-state-machine`
  - 不允许别处再复制一套 verdict 判断

#### `src/Superpowers.Launcher.Supervisor/State/RuntimePreflightFact.cs`

- 处置：
  - `kept authority core`

#### `src/Superpowers.Launcher.Supervisor/State/RuntimeHealthFact.cs`

- 处置：
  - `kept authority core`

#### `src/Superpowers.Launcher.Supervisor/State/RecoveryEntryRef.cs`

- 处置：
  - `kept authority core`

#### `src/Superpowers.Launcher.Supervisor/State/RuntimePreflightRef.cs`

- 处置：
  - `kept authority core`

## 4. 第一阶段重建顺序

### 4.1 先保住的

1. `LaunchReadinessVerdict`
2. `StardewReadinessEvaluator`
3. `RuntimePreflightFact`
4. `RuntimeHealthFact`
5. 全部 XAML 页面壳

### 4.2 先断电的

1. `LauncherShellViewModel` 本地读 readiness 文件主线
2. `ProductRedeemViewModel` 本地假兑换成功主线
3. `NotificationsViewModel` 静态通知主线
4. `SupportViewModel` 本地混合工单业务主线

### 4.3 再接正式服务

1. `LaunchReadinessApplicationService`
2. `LaunchExecutionApplicationService`
3. `RuntimeStatusApplicationService`
4. `StardewWorkspaceApplicationService`
5. `ModPackageManagementApplicationService`
6. `RepairUpdateRecheckApplicationService`
7. `SupportTicketApplicationService`
8. `NotificationFeedApplicationService`
9. `RedeemApplicationService`
10. `PurchaseHandoffApplicationService`

## 5. 大白话死规则

以后改桌面程序时，任何一个文件都先判断它属于哪一桶：

1. `kept shell`
2. `kept authority core`
3. `retired business mainline`

不允许边改边猜。  
先归桶，再开改。
