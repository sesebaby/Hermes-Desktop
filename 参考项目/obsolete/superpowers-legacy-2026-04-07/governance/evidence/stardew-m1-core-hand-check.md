# Stardew M1 Core 手动检查

状态：

- 当前处于 working evidence baseline

schemaVersion: task10-stardew-m1-core-hand-check.v1
requiredFieldSetRef: docs/superpowers/plans/2026-03-28-superpowers-m1-parallel-implementation-plan.md#task-10
visibleHost: Stardew Valley 1.6.15 running under SMAPI 4.1.10
failureExposure: ai-dialogue shell shows storyteller/runtime failure copy with retry guidance; rich playable memory/item/thought closure still not proven on the default packaged path
recoveryEntry: 游戏 -> 帮助与修复
traceJoin: current controlled-override proof joins through stardew-player-visible-check.md and repo-local SMAPI markers; rich playable per-turn trace closure remains pending

build revision：

- `86158d7fcbc4`

historical visual proof revision：

- `923866c51002e9245fb69ff3eeedf774ff4db761`

范围：

- `dialogue`
- `memory`
- `social transaction / commitment`
- `NPC 信息面板`
- `NPC 当前想法`

当前有效证据：

- `docs/superpowers/plans/2026-03-29-superpowers-m1-tester-manual-verification-plan.md`
- `docs/superpowers/governance/evidence/launcher-player-visible-check.md`
- `docs/superpowers/governance/evidence/stardew-player-visible-check.md`
- `docs/superpowers/governance/evidence/stardew-npc-info-panel-chat-item-check.md`
- current-head full build 已通过
- current-head launcher/runtime/stardew test suites 已通过
- `2026-04-05` working-tree parity closeout 已补齐 `HttpHostedNarrativeGateway` transport-boundary traceability fail-close：
  - provider-candidate / hosted-create 的 `200 OK` success payload 若缺 `rawText + normalizedObject`，现在会在 runtime gateway 直接拒绝
  - focused transport regression tests 与 fresh `gpt-5.4 high` reviewer approvals 已记录在当前 change evidence
- current-head 已检测到 SMAPI load marker
- `2026-04-05` current-head 手检 rerun 已再次进入真实存档，并生成：
  - `docs/superpowers/governance/evidence/assets/2026-04-05-stardew-current-head/stardew-save-loaded-current-head.png`
  它作为 current-head startup proof 继续有效
- `2026-04-06` current-head `NpcInfoPanel Chat / Item` 手检已补到：
  - `docs/superpowers/governance/evidence/stardew-npc-info-panel-chat-item-check.md`
  - `docs/superpowers/governance/evidence/assets/2026-04-06-stardew-current-head/stardew-npc-info-panel-chat-item-proof.md`
  - 该组证据证明 Haley 的 `NpcInfoPanel` 在真实已加载存档中可见，`聊天 / 物品` tabs 可切换，且 `Esc` 可关闭
- `2026-04-06` implementation-only 频道 current-head 状态已补到：
  - `docs/superpowers/governance/evidence/stardew-implementation-only-channel-hand-check.md`
  - 该文档现已明确写出：`group_chat / remote_direct_one_to_one` 的 authority/read-model reopen path 已落地，但窗口级 player-visible proof 仍为 `pending`
- 最近一次同时覆盖 `NpcInfoPanelMenu + AiDialogueMenu` 的 in-host 可视证明仍是 `2026-03-28` 的 controlled local override 运行；它不是当前 `86158d7fcbc4` revision 的同 revision 双 surface 资产

当前证据限制：

- `docs/superpowers/governance/evidence/stardew-player-visible-check.md` now proves current-head implementation-only shell visibility under a controlled local override, but it still does not prove rich playable M1 dialogue / memory / item / thought closure on the default packaged path.
- the current closeout now includes a fresh current-head `NpcInfoPanel Chat / Item` shell proof, but it still does not produce a new default-path rich-playable visual proof set for candidate `86158d7fcbc4`.
- 本次 `2026-04-05` parity closeout 只补代码级 transport-boundary fail-close，不构成新的 player-visible 证明，也不抵消 `6.5 / 8.6` 的待补项。
- 当前 revision 已补齐 `F9 -> NpcInfoPanel Chat / Item -> Esc` 的 visible-surface / interaction proof，但 `F8 -> AiDialogueMenu` current-head shell proof 仍未闭合。
- `remote_direct_one_to_one` / `group_chat` 已补 authority/read-model reopen path，但还没有真实窗口级截图证据。

当前 final proof 状态：

- `startup proof`
  - `pass`: current-head real-save-loaded screenshot (`docs/superpowers/governance/evidence/assets/2026-04-05-stardew-current-head/stardew-save-loaded-current-head.png`) + fresh SMAPI load marker
- `visible-surface proof`
  - `pass`: `F9 -> NpcInfoPanel Chat / Item`
  - `pending`: `F8 -> AiDialogueMenu`
  - `pending`: implementation-only `F6 / F7 / F11` channel windows
- `interaction proof`
  - `pass`: `NpcInfoPanel Chat / Item -> Esc`
  - `pending`: `AiDialogueMenu / F8`
  - `pending`: `PhoneDirectMessageMenu / PhoneActiveGroupChatMenu / OnsiteGroupChatOverlay`
- `visual evidence ref`
  - `pass`: `docs/superpowers/governance/evidence/assets/2026-04-06-stardew-current-head/stardew-npc-info-panel-chat-item-proof.md`
  - `pending`: current-head `AiDialogueMenu` visual ref
  - `pending`: current-head `group / remote` visual refs

后续手动跟进：

- 在默认 packaged path 下补证 rich-playable dialogue / memory / item / thought 闭环
- 如果想拿到同一 candidate revision 的统一证据包，需要刷新 launcher 与 Stardew 的可视证明
- 在更丰富的 in-host UI 证明真正存在之前，tester-facing 的 M1 手动验证应继续限制在当前可见的 launcher surface 以及 Stardew `F8` / `F9` shell proof
