# Thought Request Contract

状态：

- active design baseline

owner：

- runtime architecture owner
- game integration owner

用途：

- 用大白话写死：`thought` 只是当前想法预览请求，不是另一套正式私聊主线。

固定回链：

- `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
- `docs/superpowers/contracts/runtime/private-dialogue-request-contract.md`
- `docs/superpowers/contracts/runtime/canonical-history-sourcing-contract.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

request family：

- `thought_preview`

base required fields：

- `requestId`
- `gameId`
- `npcId`
- `surfaceId`
- `sceneSnapshotRef`
- `memorySummaryRef`
- `hostSummaryRef`

conditional fields：

- `saveScopeId`
- `summarySelectionHint`
- `hostDialogueNormalizedRecord`
- `sceneContextDetails`
- `relationContextDetails`
- `recentPrivateHistoryDetails`
- `hostSummaryEnvelope`

字段语义死规则：

1. `npcId`
   - 当前要读心思的对象
2. `surfaceId`
   - 当前想法显示位
3. `memorySummaryRef`
   - 当前想法可引用的记忆摘要 ref
4. `hostSummaryRef`
   - 当前宿主摘要 ref

固定主线：

1. `thought` 复用 `private_dialogue` 的云端编排和 provider 通道
2. `dialogueMode` 固定走：
   - `inner_monologue`
3. 返回只允许是 preview 文本
4. 不允许带宿主动作

绝对禁止：

1. thought request 直接进入普通私聊 committed 历史
2. thought request 生成 host mutation action
3. thought request 长成独立 canonical chat family

update trigger：

- thought base fields 变化
- inner monologue 复用规则变化
- thought 与普通私聊边界变化
