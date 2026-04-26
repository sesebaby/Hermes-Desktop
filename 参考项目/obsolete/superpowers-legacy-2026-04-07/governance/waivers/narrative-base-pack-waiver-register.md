# Narrative Base Pack Waiver Register

状态：

- active design baseline

owner：

- product operations owner

approval authority：

- founder or delegated commercial governance approver

独立审批规则：

- waiver owner 与 approval authority 必须是不同责任人
- 提交 waiver 的责任人不得审批自己的 waiver

审批实例主键：

- `waiverId`

续批链主键：

- `waiverLineageId`

业务作用域键：

- `skuId + gameId + capability + billingSource`

必须字段：

- `skuId`
- `gameId`
- `capability`
- `billingSource`
- `waiverLifecycleState`
- `reason`
- `player-visible disclosure`
- `fallback semantics`
- `owner`
- `approval authority`
- `approval date`
- `expiry phase`
- `expiry date`
- `reapproval cadence`
- `closure criteria`
- `expired blocking condition`
- `waiverId`
- `waiverLineageId`
- `supersedesWaiverId`
- `recovery impact`
- `claimStateRef`
- `evidenceReviewRef`
- `traceGroupId`
- `degradationStartedAt`

仅在 `waiverLifecycleState = closed` 时必填：

- `recoveredAt`
- `recoveryEvidenceRef`

硬规则：

- waiver 只允许批准“缺失 + 披露 + 恢复计划”
- waiver 不允许把基础包能力迁入 `Premium Media Pack` entitlement
- 每次续期或重批都必须生成新的 `waiverId`
- 同一未恢复 incident 的续批必须保持同一 `waiverLineageId + traceGroupId + degradationStartedAt`，并通过 `supersedesWaiverId` 串起审批链
- waiver 关闭时必须写入 `recoveredAt` 与 `recoveryEvidenceRef`
- active / renewed waiver 不要求 `recoveredAt` 或 `recoveryEvidenceRef`
- waiver 过期、缺字段、未链接 `claimStateRef`、未链接 `evidenceReviewRef`、未链接 `traceGroupId`、未链接 `degradationStartedAt` 或未链接 `waiverLineageId` 时，必须阻断 ship gate

## Current Working Register Rows

- 当前 repo working tree 下尚无已批准的 waiver instance row。
- 若后续把 `stardew-m1-working-user_byok` 推向 `sellable_with_disclosure` 或其他外部 claim 状态，必须先新增精确到 `skuId + gameId + capability + billingSource` 的 waiver row。
