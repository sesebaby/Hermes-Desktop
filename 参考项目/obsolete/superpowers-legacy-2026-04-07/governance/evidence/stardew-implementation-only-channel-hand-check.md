# Stardew Implementation-Only 频道手动检查

状态：

- 当前处于 working evidence baseline

schemaVersion: task10-stardew-implementation-only-hand-check.v1
requiredFieldSetRef: docs/superpowers/plans/2026-03-28-superpowers-m1-parallel-implementation-plan.md#task-10
visibleHost: Stardew Valley 1.6.15 running under SMAPI 4.1.10
failureExposure: unavailable_now -> availability_blocked; thread open fail -> render_failed; single turn submit fail -> submission_failed; surface refresh fail -> refresh_failed
recoveryEntry: 游戏 -> 帮助与修复
traceJoin: shared cross-channel join key remains historyOwnerActorId + canonicalRecordId; messageIndex is not used as a replay or audit key

build revision：

- `1825806dad6dcfe654ba24e394c9c315ccc98148`

historical adjacent shell-proof revision：

- `923866c51002e9245fb69ff3eeedf774ff4db761`

范围：

- `remote_direct_one_to_one`
- `group_chat`
- `PhoneDirectMessageMenu`
- `OnsiteGroupChatOverlay`
- `PhoneActiveGroupChatMenu`

当前有效证据：

- Task `6.1` runtime tests passed for persisted `group_chat` transcript/read-model reopen path
- Task `6.1` mod tests passed for `PhoneActiveGroupChatMenu` / `OnsiteGroupChatOverlay` transcript consumption
- Task `6.2` mod tests passed for `PhoneDirectMessageMenu` authoritative private-history reopen path and fail-closed follow-up history read behavior
- Task `6.2` runtime tests passed for `RemoteDirectEndpoint` + `PrivateHistoryEndpoint` actor-owned direct/private history truth
- current working tree still keeps implementation-only channel exposure fail-closed in code unless local config explicitly enables it
- current code path keeps local manual override off by default unless you explicitly enable it in local config
- current publish, sync, and SMAPI startup checks passed on the current working tree
- the currently deployed local mod config now contains `AllowImplementationOnlyManualEntry=false`, so this working tree is back on the default no-manual-entry posture for implementation-only channels
- `PhoneDirectMessageMenu` now reopens from actor-owned private/direct history truth, and `group_chat` surfaces now reopen from persisted Runtime.Local transcript rows; these are authority/read-model closeout facts, not player-visible window proof
- the adjacent controlled evidence run recorded in `stardew-player-visible-check.md` only proves `NpcInfoPanelMenu` / `AiDialogueMenu` shell visibility and is not treated as channel-window proof for this document's scope
- no fresh current-head screenshot or in-host proof has yet been recorded for `PhoneDirectMessageMenu`, `OnsiteGroupChatOverlay`, or `PhoneActiveGroupChatMenu`; current evidence for those surfaces remains test-backed and hidden-by-default only

后续手动跟进：

- 在默认 packaged path 下，本地 exposure override 应继续保持关闭，除非你明确为了受控检查而手动打开
- 若要把 `6.4` 从 `pending` 提升为真实可见证明，需要分别补：
  - `F6 -> PhoneDirectMessageMenu` 的 current-head 窗口截图或交互证据
  - `F7 / F11 -> group_chat` 的 current-head 窗口截图或交互证据
- 当前 candidate 仍然没有真实的玩家可见 `remote_direct_one_to_one` / `group_chat` 窗口级证明
- 当前这轮刷新尚未建立干净的 default-hidden 截图证明
- tester-facing 的 M1 手动验证应继续把这些频道排除在主可视通过标准之外，除非默认玩家构建意外暴露它们
