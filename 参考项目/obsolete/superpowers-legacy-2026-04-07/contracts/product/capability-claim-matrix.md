# Capability Claim Matrix

owner: product owner
schemaVersion: task10-capability-claim-matrix.v1
requiredFieldSetRef: docs/superpowers/plans/2026-03-28-superpowers-m1-parallel-implementation-plan.md#task-10

状态：

- active design baseline

用途：

- 作为 capability support declaration 的单一产品真相源

主键：

- `gameId + capability + billingSource`

允许的 support state：

- `supported`
- `supported_with_degraded_variant`
- `phase_waived`
- `partial_preview`
- `implementation_only`
- `not_supported`
- `not-in-phase`

必须字段：

- `gameId`
- `capability`
- `billingSource`
- `support state`
- `claim label`
- `visible-host claim`
- `degraded classification`
- `disclosure copy`
- `linked evidence`
- `waiver policy reference`
- `approval authority`

fail-closed 规则：

- 只有 `supported` 才允许进入 pack-level shorthand
- 其他任何状态都必须使用精确 disclosure copy，不得自行简写为“已含基础包”
- `implementation_only` 只允许表达“当前 phase / title 已进入实现、review、evidence 范围，但尚未进入外部 support claim”；它不得被写成 sellable promise、不得替代 waiver，也不得冒充当前 exit criteria
- `billingSource` 只表达商业与审计归属，不表达 provider 拓扑；不管写 `user_byok` 还是 `platform_hosted`，正式主线都仍是 `Cloud orchestration/provider`
- SKU 级精确 waiver linkage 只能由 `sku-entitlement-claim-matrix.md` 承载
- `capability` 字段必须使用 canonical capability key；“主动可确定性实体交互” 在 claim artifact 中必须写成 `social transaction / commitment`

## Current Working Rows

| gameId | capability | billingSource | support state | claim label | visible-host claim | degraded classification | disclosure copy | linked evidence | waiver policy reference | approval authority |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `stardew-valley` | `dialogue` | `user_byok` | `implementation_only` | `Stardew M1 对话主链 working row` | `AI 私聊对话框 shell + NPC 信息面板 shell` | `current_head_shell_visibility_recorded_review_pending` | `当前 working row 对应的代码路径已实现并在本地验证；current-head controlled-override 只证明了私聊/面板 shell 可见，不足以形成 rich playable support claim，因此仍不得形成当前外部 support claim。` | `docs/superpowers/governance/evidence/stardew-m1-core-hand-check.md` | `not_applicable_for_current_working_row` | `commercial governance approver pending RC review` |
| `stardew-valley` | `memory` | `user_byok` | `implementation_only` | `Stardew M1 记忆主链 working row` | `NPC 信息面板 shell -> 记忆 Tab shell` | `current_head_shell_visibility_recorded_review_pending` | `当前 working row 的代码与 tab 骨架已实现并在本地验证，但 current-head evidence 尚未证明 rich playable 记忆内容闭环，因此仍不得形成当前外部 support claim。` | `docs/superpowers/governance/evidence/stardew-m1-core-hand-check.md` | `not_applicable_for_current_working_row` | `commercial governance approver pending RC review` |
| `stardew-valley` | `social transaction / commitment` | `user_byok` | `implementation_only` | `Stardew M1 物品 / 赠与主链 working row` | `no current rich player-visible proof on current candidate` | `code_and_test_only_visibility_not_yet_proven` | `当前 working row 的代码与测试已实现，但 current-head evidence 尚未证明 item / carrier rich playable 可见闭环，因此仍不得形成当前外部 support claim。` | `docs/superpowers/governance/evidence/stardew-m1-core-hand-check.md` | `not_applicable_for_current_working_row` | `commercial governance approver pending RC review` |
| `stardew-valley` | `group_chat` | `user_byok` | `implementation_only` | `Stardew 实现中的群聊频道` | `no fresh current-head player-visible group-chat window proof` | `implementation_only_hidden_by_default_visual_proof_pending` | `当前 phase / title 已进入实现、review 与 hidden-by-default evidence 范围，但 current-head 仍未证明真实群聊窗口可见，因此不得写成当前外部 support claim。` | `docs/superpowers/governance/evidence/stardew-implementation-only-channel-hand-check.md` | `not_applicable_for_current_working_row` | `commercial governance approver pending RC review` |
| `stardew-valley` | `information_propagation` | `user_byok` | `not-in-phase` | `Stardew 信息传播 annex` | `no current player-visible host` | `not-in-phase` | `当前 phase 不进入 support claim，也不进入当前 ship gate。` | `docs/superpowers/governance/current-phase-boundary.md` | `not_applicable_for_current_working_row` | `commercial governance approver pending RC review` |
| `stardew-valley` | `active_world` | `user_byok` | `not-in-phase` | `Stardew 主动世界 annex` | `no current player-visible host` | `not-in-phase` | `当前 phase 不进入 support claim，也不进入当前 ship gate。` | `docs/superpowers/governance/current-phase-boundary.md` | `not_applicable_for_current_working_row` | `commercial governance approver pending RC review` |
