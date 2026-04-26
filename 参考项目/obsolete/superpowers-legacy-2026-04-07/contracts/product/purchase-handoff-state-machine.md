# Purchase Handoff State Machine

状态：

- active design baseline

owner：

- launcher product owner
- commerce operations owner

用途：

- 用大白话写死：产品页从“想买”到“跳转购买 / 回来兑换 / 刷新权益”怎么走。

固定回链：

- `docs/superpowers/contracts/product/launcher-product-catalog-visibility-contract.md`
- `docs/superpowers/contracts/product/redeem-request-and-receipt-contract.md`
- `docs/superpowers/contracts/product/player-entitlement-visibility-contract.md`

固定状态：

1. `catalog_view`
2. `purchase_intent_created`
3. `handoff_outbound`
4. `awaiting_return`
5. `redeem_required`
6. `refreshing_entitlement`
7. `completed`
8. `failed`

状态迁移：

- `catalog_view -> purchase_intent_created`
- `purchase_intent_created -> handoff_outbound`
- `handoff_outbound -> awaiting_return`
- `awaiting_return -> redeem_required`
- `redeem_required -> refreshing_entitlement`
- `refreshing_entitlement -> completed`
- `handoff_outbound -> failed`
- `refreshing_entitlement -> failed`

玩家可见规则：

1. 跳出购买前，要先明确告诉玩家会离开启动器
2. 回来后，如果需要兑换码，就进入 `redeem_required`
3. 刷新权益中，当前页必须显示“正在刷新”
4. 完成后，产品卡和我的权益都要同步更新

绝对禁止：

1. 不允许外部购买回来后，权益没刷新却显示“已完成”
2. 不允许 handoff 失败后静默停在旧页面
3. 不允许没有明确动作就自动假装玩家买过了

update trigger：

- 购买移交流程变化
- 回流兑换流程变化
- entitlement 刷新规则变化
