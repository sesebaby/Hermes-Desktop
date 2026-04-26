# Stardew Item Gift And Trade Host Executor Contract

状态：

- active design baseline

owner：

- runtime architecture owner
- stardew integration owner

用途：

- 用大白话冻结给物、借物、交易这些 accepted action 到底怎么在 Stardew 宿主执行。

authoritative boundary：

- `Runtime.Local` 决定 accepted / rejected
- `ItemGiftHostExecutor` / `TradeHostExecutor` 决定宿主是否真正执行成功
- `ItemTextCarrierBase` 只是文本 carrier，不是交易 authority

executor split：

1. `ItemGiftHostExecutor`
   - give / lend / receive
2. `TradeHostExecutor`
   - barter / shop / paid exchange

executor minima：

- `actionId`
- `canonicalRecordId`
- `transactionId`
- `resourceOrServiceKey`
- `counterpartyId`
- `hostApplyInput`

execution flow：

1. 验证宿主前置条件
2. 需要开商店时，走 title-local 商店入口
3. 需要直接给物时，走 item creator
4. 真正写回宿主
5. 产出 receipt
6. 再回 finalize

required receipts：

- `writebackReceiptId`
- `commitOutcome`
- `deliveryOutcome`
- `authoritativeItemEventRecordId`
- `carrierEvidenceRef`
- `failureClass`

rollback rules：

1. 宿主 apply 失败，必须给失败码
2. 若已部分落地，必须给 rollback state
3. 不允许只在日志里写“失败了”，但没有结构化结果

shop boundary：

1. `ShopTileFramework` 只负责宿主开店入口
2. `LivestockBazaar` 只负责宿主购买 / 生成 / 放置路径参考
3. 不允许把参考 mod 的玩法语义直接搬成正式主链

绝对禁止：

1. carrier 显示成功就当交易成功
2. `RuntimeClient` 继续兼任 host executor
3. `Cloud` 直接宣布交易已完成

update trigger：

- give/trade host path 变化
- receipt 字段变化
- rollback 规则变化

