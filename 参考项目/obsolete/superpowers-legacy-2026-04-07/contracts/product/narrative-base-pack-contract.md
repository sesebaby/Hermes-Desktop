# Narrative Base Pack Contract

状态：

- active design baseline

owner：

- product owner

approver：

- founder

能力集合：

- `dialogue`
- `memory`
- `social transaction / commitment`
- `group_chat`
- `information_propagation`
- `active_world`

能力键规范：

- `social transaction / commitment` 是产品、claim、waiver、sellability 的 canonical capability key
- “主动可确定性实体交互” 只是该能力的设计口径与玩家可见语义，不是独立 capability key
- `remote_direct_one_to_one` 固定作为 `dialogue` 的 channel implementation dimension，不是独立 capability key
- `remote_routed_communication` 固定作为 `group_chat` 的 channel implementation dimension，不是独立 capability key

主契约规则：

- 完整设计必须覆盖基础包全量能力，但当前 claim / listing / sellability 仍只认 `current-phase-boundary + capability-claim-matrix + sku-entitlement-claim-matrix`
- 每个游戏必须拥有自己独立的 prompt 资产；prompt 资产明文正本固定在 `Cloud`
- 大白话死规则：`billingSource` 只决定“谁出钱、谁提供 key、谁记账”，不决定“谁去直接连 provider”
- 不管是 `user_byok` 还是 `platform_hosted`，正式主线都固定为 `Cloud` 代表玩家或平台去连 provider；客户端不允许直连 provider
- `Runtime.Local`、`Runtime.<game> Adapter`、`Game Mod` 正式主线只允许持有结构化事实包、deterministic gate 输入、执行清单、trace ref、host evidence
- `Runtime.Local`、`Runtime.<game> Adapter`、`Game Mod` 不允许持有每个游戏的 prompt 资产明文、不允许拼最终 prompt、不允许把聊天/记忆/审计明文正本落在本地
- 任何基础能力若要成立，正式主线固定为：
  - `Game Mod facts -> Runtime.<game> Adapter fact package -> Runtime.Local gate -> Cloud orchestration/provider -> Runtime.<game> Adapter execution plan -> Game Mod host mutation`
- 所有能力都必须满足上位总纲定义的 portable semantics minimums
- `remote_communication` 固定归入 `information_propagation` 的必审实现维度；若文档讨论的是 `remote_direct_one_to_one` 对话载体，则仍归 `dialogue`
- pack-level shorthand 只允许用于所有基础能力都处于 `supported` 状态的 `skuId + gameId + billingSource`
- 任何能力若为 `supported_with_degraded_variant`、`phase_waived`、`partial_preview`、`implementation_only`、`not_supported` 或 `not-in-phase`，都不得使用 pack-level shorthand 对外宣称
- `implementation_only` 只表示当前 phase / title 要求把该 capability 或 channel 做到可实现、可 review、可留证据；它不是外部 support claim，也不能替代 waiver / disclosure / sellability judgement

support claim rules：

- capability support declaration 只能来自 `capability-claim-matrix.md`
- capability support declaration 的最小粒度为 `gameId + capability + billingSource`
- SKU 级 sellability、listing state 与 entitlement 只能来自 `sku-entitlement-claim-matrix.md`
- `billingSource` 变化不能把正式主线从 `Cloud orchestration/provider` 改成“本地直连 provider”
- `Game Integration Profile` 只拥有 evidence 与 binding，不拥有 support claim ownership
- `Game Integration Profile` 若使用 `remote_direct_one_to_one`、`remote_routed_communication` 等 channel 名称，必须回链到 canonical capability key，而不是自建第二套 claim key

waiver interaction rules：

- waiver 只允许“缺失 + 披露 + 恢复计划”
- waiver 不允许把基础能力转移到 `Premium Media Pack`
- waiver 必须精确到 `skuId + gameId + capability + billingSource`
