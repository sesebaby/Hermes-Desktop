# 手动验证索引

旧文件名：

- `evidence-review-index.md`

schemaVersion: task10-manual-verification-index.v1
requiredFieldSetRef: docs/superpowers/plans/2026-03-28-superpowers-m1-parallel-implementation-plan.md#task-10
workflowMode: solo_manual_only
candidateRevision: f42358ae808a1bfa3ed9a04ccf72429cd5d43d75
verifiedAt: 2026-04-04T21:27:58.5707057+08:00

状态：

- active manual verification baseline
- 当前手动验证基线已生效

用途：

- 汇总当前单人开发流程里真正有效的手动检查、测试、截图、日志和发布结果。
- 本文件不再承担审批、sign-off、`RC / GA` gate、商业 claim 或 waiver 放行职责。

当前收尾规则：

- 只记录 repo-local 可验证事实。
- 不要求独立审查人。
- 不输出 `approved release`、`RC`、`GA`、`commercially approved`。
- 若某项未手动检查或未拿到证据，直接写 `pending` 或 `not yet proven`。

## 当前手动验证索引

### 核心边界与说明

| 条目 | 路径 | 当前状态 |
| --- | --- | --- |
| 当前 phase boundary | `docs/superpowers/governance/current-phase-boundary.md` | 当前采用单人 / 手动验证流程 |
| 面向测试的 M1 手动计划 | `docs/superpowers/plans/2026-03-29-superpowers-m1-tester-manual-verification-plan.md` | 当前 M1 手动 UI 验证默认入口 |
| 玩家可见验证 schema | `docs/superpowers/governance/evidence/player-visible-verification-schema.md` | 仍作为可视证据形状参考 |
| 客户端暴露 threat model | `docs/superpowers/governance/client-exposure-threat-model.md` | 仅作参考；不作为 release gate |

### 代码与运行验证

| 条目 | 路径 | 当前状态 |
| --- | --- | --- |
| launcher/runtime/mod 测试基线 | repo-local command runs | 当前候选已通过 |
| launcher 发布基线 | `docs/superpowers/governance/evidence/client-package-check.md` | 已通过 |
| runtime/cloud 健康基线 | `docs/superpowers/governance/evidence/gate-time-runtime-degradation-recovery-evidence.md` | 作为本地 runtime 基线已通过 |
| hosted narrative 路径基线 | `docs/superpowers/governance/evidence/gate-time-runtime-degradation-recovery-evidence.md` | 已通过 |
| Stardew mod 发布 / 同步 / 加载基线 | `docs/superpowers/governance/evidence/client-package-check.md`; `docs/superpowers/governance/evidence/stardew-m1-core-hand-check.md` | 已通过 |

### 玩家可见证据

| 条目 | 路径 | 当前状态 |
| --- | --- | --- |
| launcher 可视证明 | `docs/superpowers/governance/evidence/launcher-player-visible-check.md` | 已刷新到当前 candidate `f42358ae808a1bfa3ed9a04ccf72429cd5d43d75` |
| Stardew 受控可视证明 | `docs/superpowers/governance/evidence/stardew-player-visible-check.md` | 历史 controlled-override shell proof 仍是唯一完整菜单证据；`2026-04-05` rerun 新增了 current-head real-save-loaded 截图，但同 revision 的 F8/F9 shell proof 仍待补 |
| Stardew NpcInfoPanel Chat / Item current-head proof | `docs/superpowers/governance/evidence/stardew-npc-info-panel-chat-item-check.md` | 已刷新到当前 working revision `86158d7fcbc4`；Haley 的 `NpcInfoPanel`、`聊天 / 物品` tabs 与 `Esc` 关闭交互均已拿到真实 in-host 证据 |
| Stardew M1 core 手检 | `docs/superpowers/governance/evidence/stardew-m1-core-hand-check.md` | 当前 working tree 已再次完成 publish/sync/load 并进入真实存档；`NpcInfoPanel Chat / Item` current-head shell 已补证，但 default-path rich-playable 与 current-head `F8 / AiDialogueMenu` 仍待补证 |
| Stardew implementation-only 频道手检 | `docs/superpowers/governance/evidence/stardew-implementation-only-channel-hand-check.md` | current-head `group / remote` authority/read-model reopen path 已闭合，默认 no-manual-entry 姿态已恢复；真实频道窗口级证明仍待补 |

### 打包与安全说明

| 条目 | 路径 | 当前状态 |
| --- | --- | --- |
| prompt 资产保护基线 | `docs/superpowers/governance/evidence/prompt-asset-protection.md` | 已记录 repo-local 发布扫描基线 |
| 客户端包清单 | `docs/superpowers/governance/evidence/client-package-check.md` | 已记录当前发布清单 |
| degraded/runtime 基线 | `docs/superpowers/governance/evidence/degraded-window-proof.md` | 仅为手动 runtime 基线；不含 RC 语义 |
| runtime recovery 基线 | `docs/superpowers/governance/evidence/gate-time-runtime-degradation-recovery-evidence.md` | 仅为手动 runtime 基线；不含 gate-time 语义 |
| 手动验证记录 | `docs/superpowers/governance/evidence/independent-review-record.md` | 已记录单人手动验证摘要 |
| 手动 release 记录 | `docs/superpowers/governance/evidence/release-governance-gate-record.md` | 已记录本地 working-build 收尾情况 |

## 当前结论

- 当前 candidate `f42358ae808a1bfa3ed9a04ccf72429cd5d43d75` 已完成：
  - supporting-runtime 路径的 repo-local 验证
  - launcher 可视证明刷新
  - Stardew build / publish / sync / load 的当前候选手检
- 当前 working tree 已完成：
  - post-implementation parity code-review closeout
  - `HttpHostedNarrativeGateway` transport-boundary traceability fail-close 修补与 focused regression verification
  - current-head Stardew rerun 到 `real save loaded`，并新增同日截图资产
  - current-head `NpcInfoPanel Chat / Item` 玩家可见证据刷新到 `86158d7fcbc4`
  - current-head final proof rollup 已明确写成：
    - `startup proof`: `pass`
    - `visible-surface proof`: `NpcInfoPanel Chat / Item` `pass`; `AiDialogueMenu / implementation-only channels` `pending`
    - `interaction proof`: `NpcInfoPanel Chat / Item -> Esc` `pass`; `AiDialogueMenu / implementation-only channels` `pending`
    - `visual evidence ref`: `NpcInfoPanel Chat / Item` `pass`; `AiDialogueMenu / implementation-only channels` `pending`
- 当前 candidate `f42358ae808a1bfa3ed9a04ccf72429cd5d43d75` 仍处于：
  - Stardew broader current-head in-host player-visible proof partially pending（`AiDialogueMenu/F8`、implementation-only channels、group/remote 仍未闭合）
- 当前候选不应被视为：
  - `implementation complete`
  - `manually verified locally`
  - `RC`
  - `GA`
  - `commercial release`
  - `independently reviewed`

## 当前已知限制

- Stardew 的 in-host 可视化证据仍依赖 `2026-03-28` 的 controlled override 运行。
- `2026-04-05` 的 parity closeout 只解决代码级 transport-boundary traceability blocker，不构成新的默认路径可视证明。
- `2026-04-06` 已补到 current-head `NpcInfoPanel Chat / Item` 的截图与 `Esc` 交互证据，但同 revision 的 `AiDialogueMenu / F8` 仍未拿到截图或交互证据。
- `remote_direct_one_to_one` / `group_chat` 已补 authority/read-model reopen path，但还没有真实窗口级截图证据。
- 当前本地 Stardew 配置已回到 `AllowImplementationOnlyManualEntry=false`，implementation-only 频道窗口级 current-head 证明仍未补齐。
- `8.1` 到 `8.5` 的 current-head supporting-runtime / launcher-visible 证据已刷新，但 `6.5` 与 `8.6` 的 Stardew current-head 玩家可见证据仍待补。
