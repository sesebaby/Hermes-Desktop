# Cloud Orchestration Fact Package Contract

状态：

- active design baseline

owner：

- runtime architecture owner
- cloud orchestration owner

用途：

- 用大白话写死：`Runtime.Local` 往 `Cloud` 发的只能是结构化事实包，不能再偷偷夹带最终 prompt。

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/governance/current-phase-boundary.md`
- `docs/superpowers/governance/afw-boundary-note.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

authoritative boundary：

- `Superpowers.<game>.Mod`
  - 负责从宿主取原始事实
  - 只允许交出：
    - 宿主原文
    - 宿主对象 id
    - 宿主快照 ref
    - 宿主可见 surface ref
- `Runtime.<game> Adapter`
  - 负责把宿主事实整理成 title-local 结构化字段
  - 负责给出：
    - request family
    - field mapping
    - title-local refs
- `Runtime.Local`
  - 负责统一包壳、校验、trace、fail-closed
  - 不负责拼最终 prompt
- `Cloud`
  - 负责吃事实包
  - 负责把事实包和云端 prompt 资产、canonical history、memory 组合成最终 prompt

fact package identity：

- `requestId`
- `traceId`
- `launchSessionId`
- `gameId`
- `channelType`
- `capability`
- `requestFamily`
- `submittedAt`
- `factPackageVersion`

`requestFamily` 固定枚举：

- `private_dialogue`
- `remote_direct`
- `group_chat_turn`
- `thought_preview`

base package minima：

- `request envelope`
  - `requestId`
  - `traceId`
  - `launchSessionId`
  - `gameId`
  - `channelType`
  - `capability`
  - `requestFamily`
  - `submittedAt`
- `actor envelope`
  - `actorId`
  - `targetActorId` 或 `participantSetRef`
  - `historyOwnerActorId`
- `host fact envelope`
  - `hostSummaryRef`
  - `sceneSnapshotRef`
  - `surfaceId` 或 `hostSurfaceRef`
  - `hostDialogueRecordRef` 或当前等价宿主记录 ref
- `history hint envelope`
  - `recentHistoryRef`
  - `summarySelectionHint`
  - `memorySummaryRef` 或 `memoryBucketHint`
- `guard envelope`
  - `supportStateRef`
  - `readinessVerdictRef`
  - `recoveryEntryRef`

按能力固定最小字段：

- `private_dialogue`
  - `actorId`
  - `targetActorId`
  - `triggerKind`
  - `playerText`
  - `hostDialogueRecordRef`
  - `sceneSnapshotRef`
  - `relationSnapshotRef`
  - `recentHistoryRef`
  - `hostSummaryRef`
- `remote_direct`
  - `actorId`
  - `targetActorId`
  - `threadKey`
  - `availabilityState`
  - `recentHistoryRef`
  - `hostSummaryRef`
  - `contactGroupId`
- `group_chat_turn`
  - `groupSessionKey`
  - `speakerId`
  - `participantSetRef`
  - `participantIds`
  - `currentSceneSnapshotRef`
  - `inputSequenceId`
  - `recentHistoryRef`
  - `topicSeedRef`
  - `participantRelationsRef`
  - `hostSummaryRef`
- `thought_preview`
  - `npcId`
  - `surfaceId`
  - `sceneSnapshotRef`
  - `memorySummaryRef`
  - `hostSummaryRef`
  - `hostDialogueNormalizedRecord`
  - `sceneContextDetails`
  - `relationContextDetails`
  - `recentPrivateHistoryDetails`

允许进包的 raw 内容：

1. 玩家本轮刚输入的原文
2. 当前宿主原对话原文
3. 为 repair / normalize 必需的宿主原始文本片段
4. 当前 title-local channel 元数据

绝对不许进包的内容：

1. prompt 模板正文
2. persona 正文
3. world rules 正文
4. provider 参数正文
5. 已渲染完成的历史 prompt
6. 聊天正本文本整包
7. 记忆正本文本整包
8. 审计明文正本

fact package 组装规则：

1. 先由 `Mod` 产出宿主事实
2. 再由 `Runtime.<game> Adapter` 做 title-local 归一化
3. 最后由 `Runtime.Local` 补统一 envelope、trace、guard refs
4. 到 `Cloud` 前必须已经是完整结构化对象
5. 到 `Cloud` 后不允许再回头向本地索要 prompt 正文

ref-only rule：

1. `hostSummaryRef`
2. `sceneSnapshotRef`
3. `relationSnapshotRef`
4. `recentHistoryRef`
5. `memorySummaryRef`
6. `supportStateRef`
7. `readinessVerdictRef`

以上字段默认都按 `ref` 传，不按“把明文抄一份”传。

明文展开规则：

1. 展开 prompt 资产明文，只能在 `Cloud`
2. 展开 canonical history 明文，只能在 `Cloud`
3. 展开 canonical memory 明文，只能在 `Cloud`
4. `Runtime.Local` 若为了校验需要看内容，只能看结构化字段和值是否合法，不得顺手生成最终 prompt

fail-closed rule：

1. 任一 base package minima 缺失，直接拒绝
2. `requestFamily` 与 payload 不匹配，直接拒绝
3. title-local adapter 没给出必需 ref，直接拒绝
4. 不允许“缺字段也先拼一个简化 prompt 试试”

audit linkage：

- 事实包一旦被 `Cloud` 接收，必须留下：
  - `factPackageRef`
  - `requestId`
  - `traceId`
  - `requestFamily`
  - `gameId`
  - `channelType`
  - `capability`

update trigger：

- 新增 request family
- 调整事实包最小字段
- 调整 raw 内容准入边界
- 调整 ref-only rule 或明文展开 rule
