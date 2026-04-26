# AFW Boundary Note

状态：

- active design baseline

effective date：

- 2026-04-07

owner：

- runtime architecture owner

co-approval：

- product owner
- host-governance authority

优先级：

- `current-phase-boundary > 2026-03-27-superpowers-master-design.md > 2026-04-07-superpowers-afw-governance-overlay-appendix.md > AFW Boundary Note`

用途：

- 把 `Microsoft Agent Framework` 限定为 Cloud 侧可替换编排引擎
- 明确 AFW 可以进入哪些工作流
- 明确 AFW 绝不能拥有哪些治理权、真相源与 authoritative 边界

AFW 可以负责：

- `Cloud` 侧 `AgentSession`
- `Cloud` 侧 `AIContextProvider`
- Cloud 内的 workflow orchestration
- memory planning / candidate generation
- tool registry
- checkpoint management
- middleware / HITL / telemetry
- `group_chat / information_propagation / active_world` 的 candidate generation 与 workflow checkpoint

AFW 绝不能负责：

- `Host Governance Core`
- `Launcher` lifecycle
- entitlement decision
- launch readiness policy
- current launch readiness result
- runtime truth-source ownership
- prompt asset truth-source ownership
- chat truth-source ownership
- memory truth-source ownership
- audit plaintext truth-source ownership
- capability support declaration
- waiver truth-source ownership
- authoritative host writeback
- ship-gate decision
- commercial disclosure copy ownership

固定落位：

- AFW 主落位固定为 `Cloud` 内部的可替换编排子层
- AFW 不得成为 `Runtime.Local` 的 authority owner
- AFW 必须位于 deterministic validation 之前、deterministic execution 之外
- AFW 只能输出 narrative candidate / workflow state / diagnostic sidecar
- AFW 若接管 `Cloud` 侧 candidate generation，必须继续支持当前正式主线要求的 streaming / pseudo-streaming 玩家可见文本输出
- AFW 不得直接产出宿主 authoritative apply
- Cloud authoritative audit 可以保留 prompt / memory 明文，但它属于 Cloud 审计正本，不属于 AFW sidecar
- AFW checkpoint、telemetry、diagnostic sidecar 默认只允许保留 redacted context digest / policy-safe metadata，不得默认保留完整明文 prompt / persona / world-rule pack、完整记忆正本或可逆 rendered prompt

`M1` AFW-specific redlines：

- AFW session / checkpoint / memory provider 数据一律视为 `derived state`
- AFW 数据不得冒充 runtime truth、product truth 或 billing truth
- 不得把 “AFW workflow 可恢复” 写成产品承诺
- 不得把 “AFW checkpoint resume” 写成宿主恢复承诺
- 不得因为接入 AFW 扩大 `M1` 的 shared command classes、world/control profile burden 或托管账本负担
- 不得让 AFW 直接决定玩家可见 capability support state
- AFW 不得向 checkpoint / telemetry / sidecar / 诊断导出面写出完整 prompt asset 明文
- 不得因为迁到 AFW，就把当前已经定下来的文本 streaming / pseudo-streaming 快反馈退化成整包一次性回传

`M1` 允许的 AFW 工作流：

- dialogue candidate generation
- memory summarization / recall planning
- social transaction / commitment proposal generation
- 经批准的 `group_chat` / `information_propagation` / `active_world` experiment candidate generation

`M2+` 扩展前提：

- 已有稳定性证据
- 已有 phase-boundary 批准
- 已有 evidence review index 链接

更新触发：

- AFW 边界调整
- preview 风险策略调整
- shared narrative orchestration 范围变化

review rule：

- 任何边界放宽都必须同时得到 phase-boundary 更新与本 note 更新
- 本 note 只能操作化既有批准边界，不能自行扩权
