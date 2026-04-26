# Trace / Audit Contract

状态：

- active design baseline

owner：

- host-governance owner

required fields：

- `status`
- `owner`
- `record types`
- `base required ids`
- `lifecycle-conditioned required ids`
- `authoritative log paths`
- `trace correlation rules`

update trigger：

- 新增、删除或重定义 required id / conditional required id
- 调整 deterministic join key、claim linkage、waiver linkage 或 recovery evidence 要求
- 调整 authoritative log path 或审计归属边界
- `current-phase-boundary`、`narrative-degradation-contract`、`evidence-review-index`、`narrative-base-pack-waiver-register` 有相关真源变更

review rule：

- 任何语义变更都必须经过 `host-governance owner` 主审
- 任何 claim linkage、waiver linkage、release evidence linkage、authoritative log boundary 变更都必须增加 `release governance owner` 独立复审
- 审查结论必须登记到 `docs/superpowers/governance/evidence-review-index.md`

ship-gate linkage：

- 该文件属于 mandatory runtime governance artifact
- 若缺失、字段不完整、生命周期条件必填定义不明确、审查未在有效期内、或与 `current-phase-boundary` / degradation contract / waiver register / evidence review index 不一致，不得进入实现批准、`RC` 或 `GA`

review freshness rule：

- 进入实现批准前，最近一次有效审查不得早于最近一次相关实质变更
- 进入 `RC` 或 `GA` 前，最近一次有效审查必须覆盖当前发布候选版本适用的 base required ids 与 lifecycle-conditioned required ids
- 若 join key、required id set、authoritative log boundary、evidence linkage 或 recovery evidence 条件任一发生实质变更，旧审查立即失效并必须重审

base required IDs：

- `traceId`
- `requestId`
- `launchSessionId`
- `skuId`
- `gameId`
- `billingSource`
- `channelType`
- `capability`
- `narrativeTurnId`
- `executionResult`
- `degradedMode`
- `traceGroupId`
- `claimStateRef`
- `claimStateAtEvent`
- `degradationWindowId`
- `degradationStartedAt`
- `evidenceReviewRef`

record types：

- `server_authority_record`
- `cost_attribution_record`
- `runtime_preflight_record`
- `launch_readiness_record`
- `runtime_state_record`
- `deterministic_command_record`
- `host_writeback_record`
- `degradation_record`
- `player_visible_surface_record`

record-type scoped field rules：

- `server_authority_record`
  - 必填：`traceId`、`requestId`、`gameId`、`skuId`、`claimStateRef`、`claimStateAtEvent`、`evidenceReviewRef`、`decisionId`、`decision`、`decisionReasonCode`、`issuedAt`、`expiresAt`、`capability`、`billingSource`、`launchReadinessPolicySnapshotRef`、`policyVersion`、`policyHash`
- `cost_attribution_record`
  - 必填：`traceId`、`requestId`、`gameId`、`skuId`、`capability`、`billingSource`、`providerRef`、`modelRef`、`estimatedCost`
- `runtime_preflight_record`
  - 必填：`traceId`、`gameId`、`runtimePreflightRef`、`claimStateRef`、`claimStateAtEvent`、`evidenceReviewRef`、`preflightState`、`quarantineStateRef`、`recoveryEntryRef`
- `launch_readiness_record`
  - 必填：`traceId`、`gameId`、`claimStateRef`、`claimStateAtEvent`、`evidenceReviewRef`、`runtimeHealthRef` 或 `runtimePreflightRef`、`quarantineStateRef`、`readinessVerdictId`、`launchReadinessPolicySnapshotRef`、`capabilityAccessDecisionRef`、`recoveryEntryRef`、`verdict`、`primaryReasonCode`、`ctaKind`、`policyVersion`、`policyHash`
- `runtime_state_record`
  - 必填：`traceId`、`launchSessionId`、`gameId`、`executionResult`、`runtimeState`、`healthState`、`quarantineState`、`quarantineStateRef`、`recoveryEntryRef`、`traceGroupId`、`stateVersion`
- `deterministic_command_record`
  - 必填：`traceId`、`requestId`、`launchSessionId`、`gameId`、`executionResult`、`channelType`、`capability`、`commandId`、`candidateId`
  - 条件必填：
    - `rejectionStage`、`failureCode`、`retryDisposition`：仅当 `executionResult = rejected` 或 `executionResult = failed`
    - `rollbackState`：仅当 `executionResult = rolled_back`
- `host_writeback_record`
  - 必填：`traceId`、`requestId`、`gameId`、`executionResult`、`channelType`、`capability`、`commandId`、`hostEvidenceRef`
- `degradation_record`
  - 必填：`traceId`、`launchSessionId`、`gameId`、`skuId`、`billingSource`、`capability`、`degradedMode`、`incidentState`、`escalationReason`、`traceGroupId`、`claimStateRef`、`claimStateAtEvent`、`evidenceReviewRef`、`degradationWindowId`、`degradationStartedAt`、`recoveryEntryRef`
- `player_visible_surface_record`
  - 必填：`traceId`、`requestId`、`gameId`、`channelType`、`capability`、`commandId`、`surfaceId`、`commitOutcome`、`recoveryEntryRef`
  - 条件必填：
    - `failureClass`：仅失败时
    - `groupTurnId`、`sequenceIndex`、`canonicalRecordId`、`historyOwnerActorId`：适用于 group/private/direct/thought surface

规则：

- `base required IDs` 不再解释为“所有记录类型都必须同时具备全部字段”
- 每条日志必须先声明 `recordType`
- 只有该 `recordType` 对应字段才可视为强制必填

lifecycle-conditioned required IDs：

- `escalationDeadlineAt`: 一旦 incident 进入 degraded window 即必填
- `escalatedAt`: 一旦 incident 达到 escalation threshold 即必填
- `waiverId`: waiver 已批准后必填
- `waiverLineageId`: 一旦进入 waiver review 链或 waiver renewal 链即必填
- `recoveredAt`: incident 已关闭后必填
- `recoveryEvidenceRef`: incident 已关闭后必填
- `promptAuditRef`: 一旦本轮 Cloud 已生成并审计最终 prompt 即必填
- `chatCanonicalRef`: 一旦本轮 accepted turn 已进入 Cloud chat truth 即必填
- `memoryCanonicalRef`: 一旦本轮涉及记忆读写且已进入 Cloud memory truth 即必填
- `canonicalRecordId`: 一旦事件进入 replay-eligible / actor-owned history truth 即必填
- `historyOwnerActorId`: 一旦事件进入 actor-owned private/direct/group history truth 即必填
- `remoteTurnId`: 仅远程一对一线程内补充投影需要时必填；不得替代 `historyOwnerActorId + canonicalRecordId`

authoritative log paths：

- launcher-managed readiness / operational status log
- server authority decision log
- cost attribution audit log
- runtime state transition log
- deterministic host writeback audit log

owner boundary：

- `launcher-managed readiness / operational status log`
  - owner: `Launcher.Supervisor`
  - authoritative record types:
    - `runtime_preflight_record`
    - `launch_readiness_record`
- `cloud authority decision / plaintext audit log`
  - owner: `Cloud`
  - authoritative record types:
    - `server_authority_record`
- `cost attribution audit log`
  - owner: `Cloud`
  - authoritative record types:
    - `cost_attribution_record`
- `runtime state transition log`
  - owner: `Runtime.Local`
  - authoritative record types:
    - `runtime_state_record`
    - `degradation_record`
    - `deterministic_command_record`
- `deterministic host writeback audit log`
  - owner: `Game Mod`
  - authoritative record types:
    - `host_writeback_record`
    - `player_visible_surface_record`

补充规则：

- prompt / chat / memory 的明文审计正本固定在 `Cloud`
- `promptAuditRef` 固定指向 `Cloud` 基于结构化事实包编排出来的最终 prompt 审计记录，不允许指向任何本地 prompt builder、本地 prompt cache、或本地 provider 请求产物
- `providerRef`、`modelRef` 固定记录 `Cloud` 发起的 provider 调用；`billingSource = user_byok` 也只表示“Cloud 代表玩家发起调用”，不表示客户端直连 provider
- `Runtime.Local` 若需要回链上游输入，只能记录结构化事实包引用、gate 输入引用、trace ref，不得补落 prompt / chat / memory 明文
- `Launcher.Supervisor`、`Runtime.Local`、`Game Mod` 只保留 trace-linked ref、health fact、host evidence，不得各自长出第二套 prompt / memory 明文正本

trace correlation rules：

- 玩家可见 failure / degraded / recovery copy 必须能回链到 `traceId`
- capability claim review 必须能回链到 evidence review index
- `narrativeTurnId` 是当前一次玩家可见 narrative emission 的统一 join key
- 对 private dialogue、remote direct、group projected turn、thought record：
  - `narrativeTurnId` 必须与 `canonicalRecordId` 保持同值或 deterministic 一一映射
- 对 transaction-backed item / gift carrier：
  - `narrativeTurnId` 必须回链到产生该 item/gift 文本 carrier 的 accepted narrative record
  - `transactionId` 继续作为 transaction-backed authoritative join key
- pre-waiver degraded trace 也必须至少能按 `skuId + gameId + capability + billingSource + traceGroupId + claimStateRef` 做 deterministic join
- waiver 已批准后，runtime degradation evidence 必须能按 `skuId + gameId + capability + billingSource + traceGroupId + waiverId + claimStateRef` 与 waiver / claim matrix 做 deterministic join
- `waiverId` 仅在 waiver 已批准后必填，且必须指向 waiver register 中一次明确的审批实例，而不是泛化业务键
- `recoveredAt` 与 `recoveryEvidenceRef` 仅在 incident 已关闭后必填
- 连续 incident 在 waiver 续批后必须保持同一 `traceGroupId + waiverLineageId + degradationStartedAt`
- 跨 channel private/direct/group projection 的 authoritative join key 固定为 `historyOwnerActorId + canonicalRecordId`
- `messageIndex`、`remoteTurnId`、`groupTurnId`、`sequenceIndex` 只允许作为线程内或 carrier-local 补充关联键，不得替代 authoritative join key
- host writeback 级日志还必须能按 `commandId` 与 deterministic command log 做 deterministic join
- `transactionId`、`eventId`、`groupTurnId`、`sequenceIndex`、`surfaceId`、`commandId` 属于 writeback / player-visible trace 的条件必填关联键，不能只留在下游 surface 文档
- `degradationGovernanceDecision` 只允许存在于 `server_authority_record`，不得与 `degradation_record` 并列形成第二套 incident truth
