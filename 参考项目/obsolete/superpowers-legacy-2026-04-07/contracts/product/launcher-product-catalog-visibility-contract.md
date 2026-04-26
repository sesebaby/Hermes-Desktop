# Launcher Product Catalog Visibility Contract

状态：

- active design baseline

owner：

- launcher product owner
- commerce operations owner

用途：

- 用大白话写死：产品与兑换页到底展示什么，不展示什么，哪些信息必须从产品真相源来。

固定回链：

- `docs/superpowers/contracts/product/narrative-base-pack-contract.md`
- `docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`

catalog owner：

- `Cloud` 拥有产品目录和 entitlement 真相
- `Launcher` 只负责展示

catalog families：

1. `trial`
2. `base_byok`
3. `base_hosted`
4. `premium_image`
5. `premium_video`
6. `premium_voice`

entry minima：

- `catalogItemId`
- `skuId`
- `displayName`
- `family`
- `listingState`
- `entitlementState`
- `supportClaimCopy`
- `primaryActionLabel`
- `primaryActionTarget`

展示死规则：

1. `基础包-BYOK` 和 `基础包-托管` 必须分开卡片
2. 只向玩家展示：
   - 产品名
   - 当前是否可买 / 可兑 / 已拥有
   - 权益摘要
3. 不展示：
   - provider 内部路由
   - prompt 资产
   - 内部成本账

来源真相死规则：

1. `listingState` 只来自产品真相源
2. `entitlementState` 只来自 entitlement 真相源
3. `supportClaimCopy` 只来自 claim 相关正式口径
4. `Launcher` 不允许自己猜“这个现在应该能卖”

绝对禁止：

1. 不允许 working build 状态直接冒充对外可售
2. 不允许 `implementation_only` 产品卡写成“已完整支持”
3. 不允许把媒体高级包拿来掩盖基础包能力缺项

update trigger：

- 产品家族变化
- catalog 展示字段变化
- listing / entitlement / claim 映射变化
