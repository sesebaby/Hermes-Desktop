# Player Entitlement Visibility Contract

状态：

- active design baseline

owner：

- commerce operations owner
- launcher product owner

用途：

- 用大白话写死：玩家在桌面程序里能看到哪些“我已经拥有”的信息，哪些不能乱显示。

固定回链：

- `docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md`
- `docs/superpowers/contracts/product/launcher-product-catalog-visibility-contract.md`

visibility owner：

- `Launcher`

entitlement minima：

- `accountId`
- `skuId`
- `entitlementState`
- `effectiveFrom`
- `effectiveTo`
- `billingSource`
- `usageSummary`

玩家可见内容：

1. 我有没有这个包
2. 当前是试用、已激活、已过期还是不可用
3. 剩余次数或当前权益摘要
4. 如果失效了，下一步该去哪里

不向玩家露出的内容：

1. claim 审批内部字段
2. waiver 内部流程字段
3. provider 内部路由
4. 成本归因明细

死规则：

1. entitlement 可见，不等于 capability claim 已经对外放开
2. entitlement 过期时，必须明确露出
3. entitlement 缺失时，不能靠本地缓存冒充“已拥有”

绝对禁止：

1. 不允许把登录态当 entitlement
2. 不允许把兑换码刚提交就直接显示“已拥有”
3. 不允许 entitlement 过期后仍显示旧可用状态

update trigger：

- entitlement 可见字段变化
- 过期处理变化
- usage summary 展示变化
