# Narrative Degradation Contract

状态：

- active design baseline

owner：

- runtime architecture owner

required fields：

- `status`
- `owner`
- `degradable modes`
- `base required degradation state fields`
- `lifecycle-conditioned required fields`
- `incident state machine`
- `missing-capability escalation rules`
- `waiver escalation trigger`
- `audit rules`

update trigger：

- 新增、删除或重定义 degradable mode
- 调整 base required field、conditional required field 或字段适用生命周期
- 调整 escalation trigger、phase threshold、waiver linkage 或 recovery evidence 要求
- `current-phase-boundary`、`trace-audit-contract`、`narrative-base-pack-waiver-register` 有相关真源变更

review rule：

- 任何语义变更都必须经过 `runtime architecture owner` 主审
- 任何 escalation threshold、waiver linkage、claim state 更新要求变更都必须增加 `release governance owner` 独立复审
- 审查结论必须登记到 `docs/superpowers/governance/evidence-review-index.md`

ship-gate linkage：

- 该文件属于 mandatory runtime governance artifact
- 若缺失、字段不完整、生命周期条件必填定义不明确、审查未在有效期内、或与 `current-phase-boundary` / waiver register / trace contract 不一致，不得进入实现批准、`RC` 或 `GA`

review freshness rule：

- 进入实现批准前，最近一次有效审查不得早于最近一次相关实质变更
- 进入 `RC` 或 `GA` 前，最近一次有效审查必须覆盖当前发布候选版本适用的 base fields 与 lifecycle-conditioned required fields
- 若 degradation field set、waiver escalation 规则、phase threshold、recovery evidence 要求任一发生实质变更，旧审查立即失效并必须重审

degradable modes：

- text preserved / action dropped
- action preserved / text dropped
- group chat degraded to single-speaker continuation
- propagation degraded to local-only result
- active world skipped for current turn

base required degradation state fields：

- `degradationWindowId`
- `degradationStartedAt`
- `degradedMode`
- `incidentState`
- `gameId`
- `launchSessionId`
- `skuId`
- `billingSource`
- `capability`
- `escalationReason`
- `claimStateRef`
- `traceGroupId`
- `recoveryEntryRef`

lifecycle-conditioned required fields：

- `escalationDeadlineAt`: 只要 incident 进入 degraded window 即必填
- `escalatedAt`: 一旦到达或超过 escalation threshold 即必填
- `waiverLineageId`: 一旦进入 waiver review 或 waiver renewal 链即必填
- `recoveredAt`: incident 关闭时必填
- `recoveryEvidenceRef`: incident 关闭时必填

incident state machine：

- `detected`
- `degraded`
- `escalated`
- `waiver_review`
- `waived`
- `recovered`
- `abandoned`

allowed transitions：

- `detected -> degraded`
- `degraded -> escalated`
- `degraded -> recovered`
- `degraded -> abandoned`
- `escalated -> waiver_review`
- `escalated -> recovered`
- `escalated -> abandoned`
- `waiver_review -> waived`
- `waiver_review -> recovered`
- `waiver_review -> abandoned`
- `waived -> escalated`
- `waived -> waiver_review`
- `waived -> recovered`
- `waived -> abandoned`

状态转移 authority 固定为：

- `Runtime.Local`
  - 负责 `detected -> degraded`
  - 负责整个 `degradation record` 的 authoritative incident state 持久化
  - 负责写入 `degradedMode`、`degradationStartedAt`、`recoveryEntryRef`
  - 负责在本地恢复成功时提交 `recoveredAt` 与 `recoveryEvidenceRef`
- `Cloud`
  - 负责产出 governance decision：
    - `escalate`
    - `enter_waiver_review`
    - `waive`
    - `abandon`
  - 负责更新 `claimStateRef`
- `Launcher / Launcher.Supervisor`
  - 只消费状态，不回写 incident state

单一真相源规则：

- `degradation record` 的唯一 owner 固定为 `Runtime.Local`
- `Cloud` 不直接回写 runtime incident state
- `Cloud` 只产出 `degradationGovernanceDecision`
- `Runtime.Local` 消费该 decision 后，把 `escalated / waiver_review / waived / abandoned` 写回同一条 authoritative degradation record

missing-capability escalation rules：

- 若长期失去 `writability`、`actor-to-actor propagation`、`cross-turn persistence` 或 `world-effect materialization`
- 则不得继续按可长期维持的 `degraded variant` 宣称
- 必须升级为 `missing capability`，进入 waiver 与商业披露
- `group_chat` 若长期退化为 `single-speaker continuation`，必须升级为 `missing capability`
- `information_propagation` 若长期退化为 `local-only result`，必须升级为 `missing capability`

waiver escalation trigger：

- incident duration 超过当前阶段允许阈值
- 或出现结构性缺失而非瞬时故障

审计规则：

- 当前阶段允许阈值必须可由 `escalationDeadlineAt` 直接审计
- 对当前阶段必需能力，degradation 不得跨过当前 release candidate / ship gate
- 对经批准的 `phase_waived` 或 `partial_preview` 能力，degradation 不得跨过下一次 `reapproval cadence`
- 一旦到达 `escalationDeadlineAt` 仍未恢复，必须写入 `escalatedAt`、更新 `claimStateRef`，并触发 waiver / claim review
- pre-waiver degraded segment 也必须保留同一 `traceGroupId + claimStateRef`
- 一旦进入 waiver review，必须分配 `waiverLineageId`
- degradation 关闭时必须写入 `recoveredAt` 与 `recoveryEvidenceRef`
