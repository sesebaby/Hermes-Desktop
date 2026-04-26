# Deterministic Command / Event Contract

状态：

- active design baseline

owner：

- runtime architecture owner

required fields：

- `status`
- `owner`
- `shared outcomes`
- `candidate / command envelope minima`
- `per-outcome payload minima`
- `completion conditions`
- `deterministic execution state machine`
- `phase-scoped command lowering`
- `M1 required outcome-to-command mapping`
- `authoritative boundary`

update trigger：

- 新增或删除共享 narrative outcome
- 调整某个 outcome 的 payload minima 或 completion condition
- 调整 `M1` lowering 边界或 outcome-to-command mapping
- 调整 authoritative apply boundary 或宿主写回限制
- `current-phase-boundary`、`narrative-base-pack-contract`、`trace-audit-contract` 有相关真源变更

review rule：

- 任何语义变更都必须经过 `runtime architecture owner` 主审
- 任何 authoritative boundary、lowering 边界、宿主写回限制变更都必须增加 `release governance owner` 独立复审
- 审查结论必须登记到 `docs/superpowers/governance/evidence-review-index.md`

ship-gate linkage：

- 该文件属于 mandatory runtime governance artifact
- 若缺失、字段不完整、审查未过期内有效、或与 `current-phase-boundary` / 主框架不一致，不得进入实现批准、`RC` 或 `GA`

review freshness rule：

- 进入实现批准前，最近一次有效审查不得早于最近一次相关实质变更
- 进入 `RC` 或 `GA` 前，最近一次有效审查必须覆盖当前发布候选版本的最终文本
- 若 authoritative boundary、phase boundary、outcome minima、completion condition 任一发生实质变更，旧审查立即失效并必须重审

共享语义 outcome：

- `dialogue_emitted`
- `memory_recorded`
- `memory_recalled`
- `transaction_state_committed`
- `group_turn_committed`
- `thought_record_committed`
- `propagation_committed`
- `world_event_committed`
- `recovery_instruction`

candidate / command envelope minima：

- `candidate envelope`
  - `candidateId`
  - `outcomeType`
  - `traceId`
  - `requestId`
  - `gameId`
  - `launchSessionId`
  - `preconditionSnapshotRef`
  - `issuedAt`
- `command envelope`
- `commandId`
- `commandClass`
- `traceId`
- `requestId`
- `gameId`
- `launchSessionId`
- `targetSurfaceId` 或 `targetHostObjectRef`
- `preconditionSnapshotRef`
- `issuedAt`
- `executionState`
- `executionResult`
- `hostEvidenceRef`

`commandClass` 枚举固定为：

- `render_command`
- `transactional_command`

`executionState` 枚举固定为：

- `candidate_received`
- `validated`
- `lowered`
- `dispatched`
- `applied`
- `rejected`
- `rolled_back`
- `failed`

`executionResult` 枚举固定为：

- `success`
- `rejected`
- `failed`
- `rolled_back`

deterministic execution state machine：

- `candidate_received -> validated -> lowered -> dispatched -> applied`
- `candidate_received -> validated -> rejected`
- `dispatched -> failed`
- `applied -> rolled_back`

补充规则：

- 不是所有 `candidate` 都会生成 `command`
- 每个跨端可执行结果都必须有唯一 `commandId`
- `candidateId` 只用于回链 server-side orchestration candidate
- `Runtime` lowering 后必须保留 `candidateId -> commandId` 的 deterministic 映射
- `Mod` 执行与 host writeback 证据必须回链到 `commandId`
- `candidate envelope` owner 固定为 `Cloud / Hosted Narrative Orchestration`
- `command envelope` owner 固定为 `Runtime.Local`
- `Runtime.<game> Adapter` 负责 title-local 字段翻译与执行清单映射，但不拥有 `commandId`、`executionState` 或 `executionResult`

per-outcome payload minima：

- `dialogue_emitted`: `turnId`、`narrativeTurnId`、`speakerId`、`listenerSet`、`surfaceId`、`canonicalRecordId`、`historyOwnerActorId`
- `memory_recorded`: `memoryKey`、`sourceSpanRef`、`timeBucket`、`memoryOwnerActorId`
- `memory_recalled`: `memoryKey`、`sourceSpanRef`、`timeBucket`、`memoryOwnerActorId`、`recallSurfaceId`
- `transaction_state_committed`: `transactionId`、`offererId`、`counterpartyId`、可选 `brokerId`、`state`、`resourceOrServiceKey`、`targetScope`
- `group_turn_committed`: `groupTurnId`、`sequenceIndex`、`narrativeTurnId`、`speakerId`、`surfaceId`、`canonicalRecordId`、`historyOwnerActorId`
- `thought_record_committed`: `narrativeTurnId`、`canonicalRecordId`、`npcId`、`surfaceId`
- `propagation_committed`: `propagationId`、`sourceFactId / sourceEventId`、`channelType`、`deliveryMode`、`deliveryState`、`targetScope`
- `world_event_committed`: `eventId`、`eventType`、`eventState`、`affectedScope`、`rollbackHandle`、`skipOrFailureReason`
- `recovery_instruction`: `traceId`、`recoveryEntry`、`reasonCode`

completion conditions：

- `dialogue_emitted` 只有在玩家可见 surface 完成渲染确认后才算 committed
- `memory_recorded` 只有在持久化 ledger 写入后才算 committed
- `memory_recalled` 只有在 recall 已绑定到当前可见 surface 且能回链到 `sourceSpanRef + timeBucket` 后才算 committed
- `transaction_state_committed` 只有在 transactional persistence 完成且 actor roles 已明确写入后才算 committed
- `group_turn_committed` 只有在持久化 group turn 已写入、且对应 player-visible surface 已成功渲染并具有 `groupTurnId + sequenceIndex + surfaceId` 后才算 committed
- `thought_record_committed` 只有在独立 thought record 已持久化、并且对应 thought surface 已成功渲染后才算 committed
- `propagation_committed` 与 `world_event_committed` 必须带显式 state，不得把 `pending / applied / skipped / rolled back / delayed / failed` 混成同一结果
- `recovery_instruction` 只有在 `recoveryEntry` 已持久化、可回链到 `traceId`、且已绑定到一个可执行或可见的恢复处理入口后才算 committed

deterministic reject / failure fields：

- `rejectionStage`
- `failureCode`
- `rollbackState`
- `retryDisposition`

`rejectionStage` 枚举固定为：

- `validation`
- `lowering`
- `dispatch`
- `host_apply`

`rollbackState` 枚举固定为：

- `not_needed`
- `pending`
- `completed`
- `failed`

`retryDisposition` 枚举固定为：

- `no_retry`
- `surface_retry_allowed`
- `authoritative_recovery_required`

phase-scoped command lowering：

- `M1` 默认只允许 lowering 到 `render_command` 与 `transactional_command`
- `M1` 不得因为总纲设计引入新的 shared command classes
- `group_chat`、`information_propagation`、`active_world` 的更重 lowering 只允许在 `M2+` 或经批准的 experiment annex 中出现

`M1` required outcome-to-command mapping：

- `dialogue_emitted` -> `render_command` required
- `memory_recorded` -> `transactional_command` required
- `memory_recalled` -> `render_command` required
- `transaction_state_committed` -> `transactional_command` required, `render_command` optional as confirmation surface
- `group_turn_committed` -> persisted group turn required, `render_command` required for the committed player-visible carrier
- `thought_record_committed` -> persisted thought record required, `render_command` required for the committed thought surface
- `propagation_committed` -> `M1 forbidden / annex-only`, 不得要求 `Runtime` 在当前 `M1` 生成 shared command lowering
- `world_event_committed` -> `M1 forbidden / annex-only`, 不得要求 `Runtime` 在当前 `M1` 生成 shared command lowering
- `recovery_instruction` -> 不进入 host apply command；只允许绑定到 `recoveryEntry` 与 player-visible recovery path

fail-closed response envelope freeze：

- `CommittedOutcomeEnvelope` 当前固定包含：
  - `requestId`
  - `traceId`
  - `gameId`
  - `channelType`
  - `capability`
  - `commandId`
  - `surfaceId`
  - `commitOutcome`
  - `reason_code`
  - `statusCode`
  - `failureClass`
  - `recoveryEntryRef`
- `reason_code` 是 canonical fail-closed reason field
- `statusCode` 必须与 `reason_code` 维持 deterministic status mapping

authoritative boundary：

- `Deterministic Game Execution Layer` 是唯一 authoritative apply owner
- AFW / LLM / prompt chain 只能提出 candidate，不得直接写回宿主
- `Runtime.Local` 是 `commandId`、`executionState`、`rejectionStage` 的唯一 owner
- `Mod` 只负责 host apply 与 `hostEvidenceRef` 回写，不得篡改 `candidateId -> commandId` 映射
