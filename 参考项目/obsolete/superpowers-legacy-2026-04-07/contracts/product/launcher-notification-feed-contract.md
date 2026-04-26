# Launcher Notification Feed Contract

状态：

- active design baseline

owner：

- launcher product owner

用途：

- 用大白话写死：通知页到底承接哪些消息，哪些必须显示，哪些不能乱写。

固定回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`
- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
- `docs/superpowers/contracts/product/stardew-mod-package-install-update-rollback-contract.md`

feed owner：

- `Launcher`

notification families：

1. `readiness_update`
2. `mod_install_update`
3. `redeem_result`
4. `support_receipt`
5. `service_notice`

entry minima：

- `notificationId`
- `family`
- `title`
- `summary`
- `issuedAt`
- `isUnread`
- `primaryActionLabel`
- `primaryActionTarget`

优先级死规则：

1. 启动受阻、修复失败、更新失败
   - 高优先级
2. 支持回执、兑换结果
   - 中优先级
3. 一般说明、版本更新记录
   - 普通优先级

当前代码现实：

- `src/Superpowers.Launcher/ViewModels/NotificationsViewModel.cs`
  - 现在更像静态提醒壳

正式要求：

1. 通知 feed 必须可区分：
   - 系统提醒
   - 我的结果
2. 必须保留 unread
3. 必须能跳到对应恢复页、游戏页、支持页、产品页

玩家可见宿主：

1. 通知页
2. 首页必要时可显示关键提醒摘录

绝对禁止：

1. 不允许把后台技术日志直接当通知标题
2. 不允许兑换失败、安装失败、支持失败没有通知回执
3. 不允许通知页自己推断另一套 readiness truth

update trigger：

- family 变化
- 优先级规则变化
- unread / action target 变化
