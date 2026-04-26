# Launcher Auth Session Contract

状态：

- active design baseline

owner：

- launcher product owner

用途：

- 用大白话写死：桌面程序的账号会话要有哪些状态，哪些页面能看，哪些动作要拦。

固定回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`
- `docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md`

session owner：

- `Launcher`

固定状态：

1. `anonymous`
2. `registering`
3. `logging_in`
4. `authenticated`
5. `expired`
6. `logging_out`
7. `auth_failed`

状态说明：

1. `anonymous`
   - 还没登录
2. `registering`
   - 正在注册
3. `logging_in`
   - 正在登录
4. `authenticated`
   - 已登录，可看我的权益、通知、支持记录
5. `expired`
   - 会话过期，要重新登录
6. `logging_out`
   - 正在退出
7. `auth_failed`
   - 登录或注册失败

页面权限死规则：

1. `首页`
   - 全部可见
2. `游戏`
   - 未登录可看游戏介绍和安装状态
   - 已登录可看权益、兑换结果、支持状态
3. `产品与兑换`
   - 未登录可看产品介绍
   - 输入兑换码前必须登录
4. `通知`
   - 必须登录
5. `支持与帮助`
   - 未登录也允许提交基础文字说明
   - 登录后才绑定账号历史和工单记录
6. `设置`
   - 全部可见

session minima：

- `sessionId`
- `accountId`
- `displayName`
- `sessionState`
- `expiresAt`
- `entitlementSnapshotRef`

失败暴露：

1. `auth_failed`
   - 当前登录面直接提示
2. `expired`
   - 顶部状态条 + 当前受影响页面 CTA
3. 不允许静默把玩家踢回匿名态

绝对禁止：

1. 不允许把 entitlement 当 auth session 的 owner
2. 不允许没登录却在本地伪造“已拥有产品”
3. 不允许登录态只存在某个页面，不存在全局 session

update trigger：

- auth 状态变化
- 页面权限变化
- entitlement 与 auth 的关系变化
