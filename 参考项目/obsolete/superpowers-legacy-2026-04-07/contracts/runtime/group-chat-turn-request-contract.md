# Group Chat Turn Request Contract

状态：

- active design baseline

owner：

- runtime architecture owner
- game integration owner

用途：

- 用大白话写死：群聊单轮请求最少要带哪几块事实，避免实现时随手拿 UI 状态凑请求。

固定回链：

- `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
- `docs/superpowers/contracts/runtime/stardew-group-chat-session-contract.md`
- `docs/superpowers/contracts/runtime/canonical-history-sourcing-contract.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

request family：

- `group_chat_turn`

base required fields：

- `requestId`
- `gameId`
- `groupSessionKey`
- `speakerId`
- `participantSetRef`
- `currentSceneSnapshotRef`
- `inputSequenceId`
- `recentGroupHistoryRef`
- `topicSeedRef`
- `participantRelationsRef`
- `hostSummaryRef`
- `contactGroupId`
- `participantIds`

conditional fields：

- `surfaceId`
  - 需要绑定当前可见宿主面时必填
- `summarySelectionHint`
  - title-local 想提示云端优先取哪段摘要时可填
- `unreadCount`
  - 手机远程线程同步时可填
- `doNotDisturb`
  - 手机远程线程同步时可填

字段语义死规则：

1. `groupSessionKey`
   - 当前这轮属于哪个会话
2. `participantSetRef`
   - 当前冻结参与者集合的正式 ref
3. `inputSequenceId`
   - 当前轮次序号
4. `participantIds`
   - 当前轮冻结参与者列表
5. `recentGroupHistoryRef`
   - 当前群聊历史 ref
6. `contactGroupId`
   - 远程群聊线程容器 id

validation rule：

1. `participantIds` 至少 3 人
2. `speakerId` 必须属于 `participantIds`
3. `groupSessionKey / participantSetRef / inputSequenceId` 缺一直接 reject

绝对禁止：

1. 只靠当前 UI 文本内容拼请求，不带 participant facts
2. 把远程群聊 unread / DND 当成 canonical history owner
3. 用 transcript cache 代替 canonical group history

update trigger：

- 群聊最小请求字段变化
- participant freeze 规则变化
- 远程群聊线程同步字段变化
