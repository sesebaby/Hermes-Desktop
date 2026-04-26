# Superpowers Launcher 与功能包附件

## 1. 文档定位

本文是当前桌面前台和产品包设计的正式附件。  
它覆盖并取代旧的“只把 Launcher 当启动器”的窄口径。

固定回链：

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
- `docs/superpowers/contracts/product/launcher-game-settings-surface-contract.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-current-code-retirement-and-rebuild-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-implementation-order-and-service-split-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-interface-and-class-landing-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-bridge-and-dto-contract-appendix.md`

## 2. Launcher 一级导航

固定为：

1. `首页`
2. `游戏`
3. `产品与兑换`
4. `通知`
5. `支持与帮助`
6. `设置`

## 3. Launcher 的完整职责

### 3.1 账号面

- 注册
- 登录
- 账号状态
- 退出登录
- 会话过期提示
- 我的权益入口

### 3.2 游戏面

- 最近游戏主卡
- 游戏列表
- 工作区
- 概览 / 运行 / 帮助与修复 / 游戏设置
- 当前状态、主 CTA、问题摘要

### 3.3 产品与兑换面

- 产品介绍
- 外部购买跳转
- Key 兑换
- 我的权益
- 购买回流状态

### 3.4 通知面

- 重要通知
- 我的消息
- 更新记录

### 3.5 支持与帮助面

- 一键修复
- 重新检查
- 提交问题
- 问题记录
- 玩家补充说明
- 常见问题
- 只提交文字说明

### 3.6 设置面

- 账号
- 启动器
- 游戏管理
- 隐私与数据

## 4. Launcher 的 4 个闭环

1. `启动闭环`
2. `兑换闭环`
3. `通知闭环`
4. `支持闭环`

## 4.1 二级页面和 route

固定 route：

1. `home`
2. `games`
3. `games/stardew`
4. `products`
5. `products/redeem`
6. `products/entitlements`
7. `notifications`
8. `support`
9. `settings`
10. `account`
11. `account/login`
12. `account/register`

固定二级页面：

1. `账号面`
   - `账号入口`
   - `登录`
   - `注册`
   - `账号主页`
   - `会话过期提示`
2. `游戏面`
   - `Stardew 工作区`
   - `概览`
   - `运行状态`
   - `Mod 管理`
   - `帮助与修复`
   - `游戏设置`
3. `产品与兑换面`
   - `产品目录`
   - `购买回流状态`
   - `兑换`
   - `我的权益`
4. `通知面`
   - `通知列表`
5. `支持与帮助面`
   - `问题摘要`
   - `提交问题`
   - `回执`
   - `问题记录`
   - `玩家补充说明`
   - `常见问题`
   - `只提交文字说明`

## 5. 产品包固定集合

当前固定产品线：

1. `试用包`
2. `基础包-BYOK`
3. `基础包-托管`
4. `高级包-绘画`
5. `高级包-视频`
6. `高级包-语音`

固定规则：

1. `基础包-BYOK` 与 `基础包-托管` 必须分开展示。
2. 玩家只看次数，不看 provider 和内部成本。
3. `Launcher` 不自己推断 sellability、entitlement、claim。
4. 权益和展示必须回链：
   - `narrative-base-pack-contract`
   - `capability-claim-matrix`
   - `sku-entitlement-claim-matrix`

## 6. Launcher 与 Launcher.Supervisor 的边界

### 6.1 Launcher

负责：

- 玩家前台
- 产品前台
- 通知和支持前台

### 6.2 Launcher.Supervisor

负责：

- 启动前检查
- 拉起 / 停止 `Runtime.Local`
- 汇总本地运行事实
- 生成 `launchReadinessVerdict`
- 执行更新、修复、重启

固定不允许：

1. `Launcher` 自己重算 readiness truth
2. `Launcher` 自己推断产品 claim
3. `Runtime.Local` 自己长出一套产品前台

## 7. 首次可见宿主与失败暴露

桌面能力的首次可见宿主固定为：

- `Launcher`

失败暴露点固定为：

1. 首页主卡
2. 游戏页
3. 支持与帮助
4. 通知页

额外固定规则：

1. 账号过期优先露在：
   - 顶部状态条
   - `account`
   - 当前受影响页面 CTA
2. 购买回流优先露在：
   - `products`
   - `products/redeem`
   - `products/entitlements`

## 8. review 必查点

1. 是否把 Launcher 又收窄成“启动壳”
2. 是否漏写产品与兑换、支持闭环、通知闭环
3. 是否把 `BYOK` 和 `托管` 混成一个基础包
4. 是否让 Launcher 自己生成第二套 readiness / entitlement 真相
5. 是否漏掉账号 session、mod 包安装更新回滚、问题包与通知 feed 的正式合同

## 9. 类落点回链

桌面程序真正进入重构时，不能只看页面和 service 名字。  
还必须同时回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-interface-and-class-landing-appendix.md`

固定原因：

1. 这里面写死了未来目录
2. 写死了 bridge 清单
3. 写死了页面状态模型
4. 写死了旧类什么时候退役
