# Private Dialogue Request Contract

状态：

- active design baseline

owner：

- runtime architecture owner
- game integration owner

用途：

- 用大白话写死：私聊请求最少要带什么，哪些字段是事实，哪些字段绝对不能偷偷塞 prompt。

固定回链：

- `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
- `docs/superpowers/contracts/runtime/runtime-local-vs-title-adapter-boundary-contract.md`
- `docs/superpowers/contracts/runtime/private-dialogue-commit-state-machine-contract.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

request family：

- `private_dialogue`

authoritative boundary：

- `Mod`
  - 负责宿主触发与宿主事实
- `Runtime.<game> Adapter`
  - 负责 title-local 字段归一化
- `Runtime.Local`
  - 负责共享校验
- `Cloud`
  - 负责 prompt 编排

base required fields：

- `requestId`
- `gameId`
- `actorId`
- `targetId`
- `triggerKind`
- `channelType`
- `hostDialogueRecordRef`
- `sceneSnapshotRef`
- `relationSnapshotRef`
- `recentPrivateHistoryRef`
- `hostSummaryRef`

conditional fields：

- `saveScopeId`
  - 宿主需要分档位存档时必填
- `playerText`
  - 玩家本轮真有输入时必填
- `dialogueMode`
  - 非标准私聊模式时必填
- `resolvedContextBuilderId`
  - title-local 已做结构化上下文归一时必填
- `resolvedContextMode`
  - title-local 已做结构化上下文归一时必填
- `hostDialogueNormalizedRecord`
  - 已有宿主对话归一文本时必填
- `sceneContextDetails`
  - 已有场景细节文本时必填
- `relationContextDetails`
  - 已有关系统一文本时必填
- `recentPrivateHistoryDetails`
  - 已有最近私聊归一文本时必填

扩展字段：

- `recentPrivateHistoryTurns`
- `hostSummaryEnvelope`
- `generation`

这些可以有，但不能替代 base required fields。

字段语义死规则：

1. `actorId`
   - 当前发起人
2. `targetId`
   - 当前被说话对象
3. `triggerKind`
   - 为什么触发这次私聊
4. `channelType`
   - 当前 carrier 类型
5. `hostDialogueRecordRef`
   - 当前宿主对话记录的正式 ref
6. `recentPrivateHistoryRef`
   - 最近私聊历史的正式 ref
7. `hostSummaryRef`
   - 当前宿主摘要的正式 ref

禁止混入：

1. 最终 prompt 文本
2. prompt asset 正文
3. world rules 正文
4. persona 正文
5. provider model 参数正文

validation rule：

1. `requestId / gameId / actorId / targetId / triggerKind` 缺一直接拒绝
2. `hostDialogueRecordRef / sceneSnapshotRef / relationSnapshotRef / recentPrivateHistoryRef / hostSummaryRef` 属于正式 facts，缺一直接 reject
3. `resolvedContext*` 系字段如果出现，必须整组自洽，不能只传半套

stardew 首发映射：

- `actorId`
  - 默认 `player`
- `targetId`
  - 当前 NPC
- `channelType`
  - 默认 `dialogue`
- `triggerKind`
  - 例：
    - `host_dialogue_exhausted`
    - `npc_info_panel_chat`
    - `remote_direct_open`
    - `inner_monologue`

update trigger：

- base required fields 变化
- conditional fields 变化
- 私聊 request family 语义变化
