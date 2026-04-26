# SKU Entitlement & Claim Matrix

owner: product owner
schemaVersion: task10-sku-entitlement-claim-matrix.v1
requiredFieldSetRef: docs/superpowers/plans/2026-03-28-superpowers-m1-parallel-implementation-plan.md#task-10

状态：

- active design baseline

主键：

- `skuId + gameId + capability + billingSource`

必须字段：

- `skuId`
- `gameId`
- `capability`
- `billingSource`
- `entitlement state`
- `support claim`
- `sellability state`
- `claim copy`
- `linked waiver`
- `linked evidence`
- `approval authority`

硬规则：

- SKU 级 support claim 不得宽于 `capability-claim-matrix.md`
- 只有 `supported` 才允许 `sellability state = sellable`
- 只有 `phase_waived` 或 `partial_preview` 才允许 `sellability state = sellable_with_disclosure`
- `supported_with_degraded_variant`、`not_supported` 必须进入 `blocked`
- `implementation_only` 只能进入 `not_listed`；若它在 shipped build 中对玩家可见，`claim copy` 只能写精确 non-claim disclosure，不得写成 support promise
- `not-in-phase` 只能进入 `not_listed`，不得作为已销售 SKU 的缺项例外
- 若 `support claim` 为 `phase_waived` 或 `partial_preview`，则必须链接有效 waiver
- 不得通过 bundle / edition / region / billingSource 变体绕开基础包缺项披露
- `billingSource` 不得被解释成“本地 provider 通路”；无论是 `user_byok` 还是 `platform_hosted`，provider 通信都仍由 `Cloud` 代表发起
- 不得把基础包能力迁移为 premium entitlement 满足缺项
- `capability` 字段必须使用 canonical capability key；“主动可确定性实体交互” 在 SKU 级 artifact 中必须写成 `social transaction / commitment`

## Current Working Rows

> 当前 working rows 使用临时 `skuId = stardew-m1-working-user_byok` 作为 repo-local 治理锚点，不代表最终商业 listing id。

| skuId | gameId | capability | billingSource | entitlement state | support claim | sellability state | claim copy | linked waiver | linked evidence | approval authority |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `stardew-m1-working-user_byok` | `stardew-valley` | `dialogue` | `user_byok` | `enabled_for_working_build` | `implementation_only` | `not_listed` | `当前 working row 对应的对话主链代码已实现并通过本地验证；current-head 只证明私聊/面板 shell 可见，不足以形成 rich playable 外部 listing。` | `not_applicable` | `docs/superpowers/governance/evidence/stardew-m1-core-hand-check.md` | `commercial governance approver pending RC review` |
| `stardew-m1-working-user_byok` | `stardew-valley` | `memory` | `user_byok` | `enabled_for_working_build` | `implementation_only` | `not_listed` | `当前 working row 对应的记忆主链代码与 tab 骨架已实现并通过本地验证；current-head 尚未证明 rich playable 记忆内容闭环，因此不得形成外部 listing。` | `not_applicable` | `docs/superpowers/governance/evidence/stardew-m1-core-hand-check.md` | `commercial governance approver pending RC review` |
| `stardew-m1-working-user_byok` | `stardew-valley` | `social transaction / commitment` | `user_byok` | `enabled_for_working_build` | `implementation_only` | `not_listed` | `当前 working row 对应的物品 / 赠与主链代码与测试已实现，但 current-head 尚未证明 rich playable item / carrier 可见闭环，因此不得形成外部 listing。` | `not_applicable` | `docs/superpowers/governance/evidence/stardew-m1-core-hand-check.md` | `commercial governance approver pending RC review` |
| `stardew-m1-working-user_byok` | `stardew-valley` | `group_chat` | `user_byok` | `enabled_for_working_build` | `implementation_only` | `not_listed` | `当前 build 已实现 implementation_only 群聊频道并保留 hidden-by-default evidence；current-head 仍未证明真实群聊窗口可见，因此不得写成当前 support promise。` | `not_applicable` | `docs/superpowers/governance/evidence/stardew-implementation-only-channel-hand-check.md` | `commercial governance approver pending RC review` |
| `stardew-m1-working-user_byok` | `stardew-valley` | `information_propagation` | `user_byok` | `not_enabled_for_current_phase` | `not-in-phase` | `not_listed` | `当前 phase 不进入 support claim，也不进入 listing。` | `not_applicable` | `docs/superpowers/governance/current-phase-boundary.md` | `commercial governance approver pending RC review` |
| `stardew-m1-working-user_byok` | `stardew-valley` | `active_world` | `user_byok` | `not_enabled_for_current_phase` | `not-in-phase` | `not_listed` | `当前 phase 不进入 support claim，也不进入 listing。` | `not_applicable` | `docs/superpowers/governance/current-phase-boundary.md` | `commercial governance approver pending RC review` |
