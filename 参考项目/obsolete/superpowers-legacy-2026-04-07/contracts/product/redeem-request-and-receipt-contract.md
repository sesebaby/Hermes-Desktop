# Redeem Request And Receipt Contract

状态：

- active design baseline

owner：

- launcher product owner
- commerce operations owner

用途：

- 用大白话写死：兑换码请求和兑换回执最少要有哪些字段，哪些结果必须通知玩家。

固定回链：

- `docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md`
- `docs/superpowers/contracts/product/launcher-notification-feed-contract.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`

request owner：

- `Launcher` 发起
- `Cloud` 产出兑换回执正本

request minima：

- `requestId`
- `accountId`
- `redeemCode`
- `submittedAt`
- `launcherSurfaceRef`

receipt minima：

- `receiptId`
- `requestId`
- `accountId`
- `redeemOutcome`
- `grantedSkuIds`
- `failureClass`
- `issuedAt`
- `notificationRef`

固定结果：

1. `accepted`
2. `rejected_invalid_code`
3. `rejected_already_used`
4. `rejected_not_eligible`
5. `failed`

玩家可见规则：

1. 输入兑换码后，当前页必须先进入提交中
2. 成功后必须显示：
   - 回执结果
   - 已获得内容摘要
3. 失败后必须显示：
   - 大白话原因
4. 最终结果还必须进入通知 feed

当前代码现实绑定：

- `src/Superpowers.Launcher/ViewModels/ProductRedeemViewModel.cs`
  - 现在只是本地记录壳

绝对禁止：

1. 不允许“本地先记成已兑换”，再以后补云端
2. 不允许没有 receiptId 却显示成功
3. 不允许兑换结果只留在当前页，不进通知

update trigger：

- 兑换请求字段变化
- 兑换结果枚举变化
- 通知绑定规则变化
