# Superpowers Launcher 实施顺序与服务拆分附件

## 1. 文档定位

这份附件回答 4 个问题：

1. `Launcher` 真正开改时先做什么
2. 哪些 service 要先立
3. 旧 `ViewModel` 各自迁到哪里
4. 每一步什么叫“做完”

这份文档不是讲愿景。  
它是桌面程序进入真实重构前的施工顺序表。

固定回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-current-code-retirement-and-rebuild-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-interface-and-class-landing-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-bridge-and-dto-contract-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-phase-backlog-and-delivery-appendix.md`
- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
- `docs/superpowers/contracts/product/launcher-launch-orchestration-state-machine.md`
- `docs/superpowers/contracts/product/launcher-account-surface-state-machine.md`
- `docs/superpowers/contracts/product/stardew-launcher-workspace-ia.md`
- `docs/superpowers/contracts/product/stardew-mod-package-install-update-rollback-contract.md`
- `docs/superpowers/contracts/product/support-ticket-and-diagnostic-bundle-contract.md`
- `docs/superpowers/contracts/product/launcher-notification-feed-contract.md`
- `docs/superpowers/contracts/product/redeem-request-and-receipt-contract.md`
- `docs/superpowers/contracts/product/launcher-product-catalog-visibility-contract.md`
- `docs/superpowers/contracts/product/player-entitlement-visibility-contract.md`
- `docs/superpowers/contracts/product/purchase-handoff-state-machine.md`
- `docs/superpowers/contracts/product/support-closure-state-machine.md`
- `docs/superpowers/contracts/product/launcher-support-surface-flow.md`
- `docs/superpowers/contracts/product/launcher-game-settings-surface-contract.md`
- `docs/superpowers/contracts/product/repair-update-recheck-state-machine.md`
- `docs/superpowers/contracts/product/supervisor-preflight-input-matrix.md`

## 2. 总体施工原则

桌面程序这块，固定按 3 层拆：

1. `View`
   - XAML
   - 只管显示
2. `ViewModel`
   - 只管页面状态和按钮动作
   - 不直接读文件、不直接拼业务真相
3. `Application Service`
   - 真正吃合同
   - 真正调 Supervisor / Cloud / 本地桥接

大白话死规则：

1. `ViewModel` 不允许自己读 json 文件算 readiness
2. `ViewModel` 不允许自己决定兑换成功
3. `ViewModel` 不允许自己决定支持提交成功
4. `ViewModel` 不允许自己决定通知列表内容

## 3. 固定 service 列表

当前桌面程序正式 service 固定为：

1. `LaunchReadinessApplicationService`
2. `LaunchExecutionApplicationService`
3. `RuntimeStatusApplicationService`
4. `StardewWorkspaceApplicationService`
5. `ModPackageManagementApplicationService`
6. `RepairUpdateRecheckApplicationService`
7. `GameSettingsApplicationService`
8. `SupportTicketApplicationService`
9. `NotificationFeedApplicationService`
10. `RedeemApplicationService`
11. `AccountSessionApplicationService`
12. `EntitlementVisibilityApplicationService`
13. `ProductCatalogApplicationService`
14. `PurchaseHandoffApplicationService`

## 4. 每个 service 的职责

### 4.1 LaunchReadinessApplicationService

owner：

- `Launcher.Supervisor`

只负责：

1. 读单一 `LaunchReadinessVerdict`
2. 读 `RuntimePreflightFact`
3. 读 `RuntimeHealthFact`
4. 组首页主卡 / 游戏概览可见状态

绝对不负责：

1. 启动游戏
2. 修复
3. 更新

直接服务页面：

1. 首页
2. 游戏页概览
3. Stardew 配置页顶部状态

### 4.2 LaunchExecutionApplicationService

owner：

- `Launcher.Supervisor`

只负责：

1. 启动前最后检查
2. 调用 Runtime.Local 启动链
3. 调用游戏启动链
4. 回 `launching / running / failed`

直接服务页面：

1. 首页主 CTA
2. 游戏页主 CTA

### 4.3 StardewWorkspaceApplicationService

owner：

- `Launcher`
 - `Stardew` title-local workspace owner

只负责：

1. 组装 `概览 / 运行状态 / Mod 管理 / 帮助与修复 / 游戏设置`
2. 汇总别的 service 已经准备好的区块状态
3. 让 `StardewGameConfigViewModel` 不再自己拼内容

绝对不负责：

1. 自己重算 readiness
2. 自己执行 repair/update/recheck

### 4.4 RuntimeStatusApplicationService

owner：

- `Launcher.Supervisor`

只负责：

1. 读取 `RuntimeHealthFact`
2. 读取 `RuntimePreflightFact`
3. 组 `运行状态` 区块

直接服务页面：

1. Stardew 工作区 `运行状态`

### 4.5 ModPackageManagementApplicationService

owner：

- `Launcher.Supervisor`

只负责：

1. 读取 mod 包状态
2. 安装 / 更新 / 回滚
3. 回包操作 receipt

直接服务页面：

1. 游戏页
2. Stardew 工作区 `Mod 管理`

### 4.6 RepairUpdateRecheckApplicationService

owner：

- `Launcher.Supervisor`

只负责：

1. 修复
2. 更新
3. 重新检查
4. 回动作 receipt

直接服务页面：

1. Stardew 工作区 `帮助与修复`
2. 支持与帮助页

### 4.7 SupportTicketApplicationService

owner：

- `Launcher`
 - `Cloud support`

只负责：

1. 收集表单
2. 收集问题包
3. 提交工单
4. 读回执和工单状态
5. 当工单进入 `needs_player_reply` 时提交玩家补充说明

直接服务页面：

1. 支持与帮助页

### 4.8 GameSettingsApplicationService

owner：

- `Launcher`
 - `Launcher.Supervisor`

只负责：

1. 读取当前游戏设置
2. 检测 SMAPI 路径和启动模式状态
3. 保存设置
4. 回设置保存 / 检测回执

直接服务页面：

1. Stardew 工作区 `游戏设置`

### 4.9 NotificationFeedApplicationService

owner：

- `Launcher`

只负责：

1. 拉通知 feed
2. 标记已读未读
3. 给页面 action target

### 4.10 RedeemApplicationService

owner：

- `Launcher`
 - `Cloud commerce`

只负责：

1. 提交兑换请求
2. 读兑换回执
3. 触发 entitlement refresh

### 4.11 AccountSessionApplicationService

owner：

- `Launcher`

只负责：

1. 注册
2. 登录
3. 会话续期 / 过期
4. 退出登录

### 4.12 EntitlementVisibilityApplicationService

owner：

- `Launcher`

只负责：

1. 读取玩家当前拥有内容
2. 给产品页 / 我的权益 / 游戏页供数

### 4.13 ProductCatalogApplicationService

owner：

- `Launcher`
 - `Cloud commerce`

只负责：

1. 拉产品目录
2. 区分：
   - `基础包-BYOK`
   - `基础包-托管`
   - 高级包

### 4.14 PurchaseHandoffApplicationService

owner：

- `Launcher`
 - `Cloud commerce`

只负责：

1. 创建购买意图
2. 管理外部购买跳转
3. 回来后进入 `awaiting_return / redeem_required / refreshing_entitlement`
4. 给产品页和我的权益页同步 handoff 状态

## 5. 旧 ViewModel 迁移表

### `LauncherShellViewModel`

保留：

1. 导航
2. 页面切换
3. surface 容器

迁出：

1. readiness 读取 -> `LaunchReadinessApplicationService`
2. 启动执行 -> `LaunchExecutionApplicationService`
3. 支持提交桥接 -> `SupportTicketApplicationService`
4. 顶层登录态 -> `AccountSessionApplicationService`

### `HomeViewModel`

保留：

1. 首页展示壳

迁出：

1. 假 verdict 预览 -> `LaunchReadinessApplicationService`

### `StardewGameConfigViewModel`

保留：

1. 页面状态壳

迁出：

1. readiness 解释 -> `LaunchReadinessApplicationService`
2. 运行状态 -> `RuntimeStatusApplicationService`
3. mod 包状态 -> `ModPackageManagementApplicationService`
4. repair/update/recheck -> `RepairUpdateRecheckApplicationService`
5. 页面组合 -> `StardewWorkspaceApplicationService`

### `ProductRedeemViewModel`

重建为：

1. `RedeemViewModel`

直接依赖：

1. `RedeemApplicationService`
2. `EntitlementVisibilityApplicationService`

### `NotificationsViewModel`

重建为：

1. 只消费 `NotificationFeedApplicationService`

### `SupportViewModel`

重建为：

1. 只消费 `SupportTicketApplicationService`

### `SettingsViewModel`

保留：

1. 桌面体验设置壳

不进入：

1. 游戏业务真相
2. 产品真相

## 6. 固定施工顺序

### 第 1 步：先立 readiness 和启动服务

先做：

1. `LaunchReadinessApplicationService`
2. `LaunchExecutionApplicationService`

原因：

1. 这是桌面程序主入口
2. 当前 `LauncherShellViewModel` 最大越界点就在这里

完成标准：

1. `LauncherShellViewModel` 不再直接读 readiness 文件
2. `LauncherShellViewModel` 不再直接启动 SMAPI

### 第 2 步：再立 Stardew 工作区服务

先做：

1. `RuntimeStatusApplicationService`
2. `StardewWorkspaceApplicationService`

原因：

1. 先把游戏页和配置页统一供数
2. `运行状态` 是工作区一级区块，必须先单列
3. 这样后面 mod 管理、修复、帮助都能挂在统一工作区上

完成标准：

1. `HomeViewModel` 和 `StardewGameConfigViewModel` 不再自带业务拼装

### 第 3 步：接 mod 包和 repair/update/recheck

先做：

1. `ModPackageManagementApplicationService`
2. `RepairUpdateRecheckApplicationService`

完成标准：

1. 安装 / 更新 / 回滚 走正式 package service
2. 修复 / 更新 / 重检动作走正式 repair service
3. 页面不再自己解释 package 状态

### 第 4 步：接支持与通知

先做：

1. `SupportTicketApplicationService`
2. `NotificationFeedApplicationService`

完成标准：

1. `SupportViewModel` 不再自己混合提交和回执语义
2. `NotificationsViewModel` 不再使用静态条目

### 第 5 步：接兑换、产品目录、权益

先做：

1. `RedeemApplicationService`
2. `ProductCatalogApplicationService`
3. `EntitlementVisibilityApplicationService`
4. `AccountSessionApplicationService`
5. `PurchaseHandoffApplicationService`

完成标准：

1. `ProductRedeemViewModel` 旧业务主线退役
2. 产品页、兑换页、我的权益口径统一
3. 外部购买回来后，不允许“权益没刷新却显示已完成”

## 7. 每一步禁止事项

### 第 1 步禁止

1. 不允许边接服务边保留旧本地读取主线继续跑成功路径

### 第 2 步禁止

1. 不允许 `StardewGameConfigViewModel` 继续长出新业务判断

### 第 3 步禁止

1. 不允许安装状态和 readiness 状态混用
2. 不允许把 repair/update/recheck 又塞回 mod 包 service

### 第 4 步禁止

1. 不允许支持和通知各自维护一套 receipt 语义

### 第 5 步禁止

1. 不允许兑换、权益、产品页各自一套产品状态

## 8. 完工判断

桌面程序这条线，只有同时满足下面条件，才算真正完成首轮收口：

1. 页面壳保留
2. 越界业务主线断电
3. 所有业务状态从正式 service 进入 ViewModel
4. `Launcher` 不再自己长出第二套 readiness、entitlement、support、notification 真相

## 9. 大白话结论

以后真开始改桌面代码，不准再说：

- “先把 ViewModel 改一改再看”
- “先本地读个文件顶着”
- “先写个假通知”

固定顺序就是：

1. 先 readiness / 启动
2. 再工作区
3. 再 mod / repair
4. 再支持 / 通知
5. 最后兑换 / 产品 / 权益 / 账号
