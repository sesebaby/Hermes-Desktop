# Premium Media Pack Contract

状态：

- active design baseline

owner：

- product owner

当前能力范围：

- `AI voice`
- `AI image`
- `AI video`

硬边界：

- `Premium Media Pack` 只能提供附加媒体能力
- 不得作为 `Narrative Base Pack` 缺项的替代 entitlement
- 不得把基础包默认行为改成 premium-only hosted path
- 若基础包能力需要 hosted infrastructure，也必须仍然属于基础 entitlement

商业规则：

- premium claim 必须与 `sku-entitlement-claim-matrix.md` 一致
- premium 不得复写或覆盖基础包 support claim
- premium 成本治理、托管依赖与风控可独立，但不能污染基础包 claim truth
