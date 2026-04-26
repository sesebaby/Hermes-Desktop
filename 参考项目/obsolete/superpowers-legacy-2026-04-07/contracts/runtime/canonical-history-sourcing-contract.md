# Canonical History Sourcing Contract

状态：

- active design baseline

owner：

- cloud canonical-history owner
- runtime architecture owner

用途：

- 用大白话写死：聊天正本、记忆正本、投影历史，到底谁说了算，谁只是镜像壳。

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
- `docs/superpowers/contracts/runtime/cloud-prompt-assembly-contract.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`
- `docs/superpowers/contracts/runtime/deterministic-command-event-contract.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

authoritative boundary：

- `Cloud`
  - 拥有 canonical chat truth
  - 拥有 canonical memory truth
  - 拥有 candidate / pending / committed 状态正本
- `Runtime.Local`
  - 拥有 finalize 仲裁结果
  - 不拥有 canonical chat 正本
- `Game Mod`
  - 拥有宿主显示证据
  - 可以保留 mirrored projection
  - 不拥有 canonical chat 正本

record families：

1. `candidate record`
   - provider 已返回
   - 还没过宿主 finalize
2. `pending_visible record`
   - `Runtime.Local` 已通过 gate
   - 等宿主显示 / 写回
   - 允许玩家先看到纯文本可见面
   - 不代表已 committed
3. `committed canonical record`
   - 宿主 finalize 成功
   - 允许进入正式聊天 / 记忆主线
4. `mirrored projection record`
   - 只给本地 UI、线程、宿主面板用
   - 不是 authority

canonical record minima：

- `canonicalRecordId`
- `narrativeTurnId`
- `historyOwnerActorId`
- `gameId`
- `channelType`
- `capability`
- `recordState`
- `content`
- `acceptedActions`
- `acceptedDeterministicOutcomes`
- `promptAuditRef`
- `chatCanonicalRef`
- `memoryCanonicalRef`（条件适用）

authoritative join key：

- 跨 private/direct/group projection 的正式 join key 固定为：
  - `historyOwnerActorId + canonicalRecordId`

补充 join key：

- `threadKey`
- `groupSessionKey`
- `contactGroupId`
- `messageIndex`
- `sequenceIndex`

这些只能做线程内排序和 UI 回放，不能替代 authoritative join key。

history sourcing rules：

1. `private_dialogue`
   - 正式历史 owner 固定为 `Cloud canonical private/direct history`
2. `remote_direct_one_to_one`
   - 与 private dialogue 共用同一 actor-owned private/direct history 正本
   - 手机线程只是 carrier，不是第二套正本
3. `group_chat`
   - committed group turn 进入 canonical group history
   - 再镜像投影到参与者 private history projection
4. `thought_preview`
   - 默认不进入普通私聊历史
   - 若未来进入正式 thought record，也必须有独立 canonical record family

commit promotion rule：

1. `Cloud` 可以先写 `candidate`
2. `Cloud` 可以先写 `pending_visible`
3. 只有收到 `Runtime.Local` 的 finalize 成功结果，才能升为：
   - `committed canonical record`
4. 没 commit 之前：
   - 不得进入正式聊天回放
   - 不得进入正式记忆压缩
   - 不得冒充“已经发生过”

projection rule：

1. `Runtime.Local` 可以保留 local projection cache
2. `Game Mod` 可以保留 UI thread projection
3. `streaming / pseudo-streaming` 只允许驱动玩家可见文本增量与等待态，不改 canonical committed 规则
3. `projection record` 必须带：
   - `historyOwnerActorId`
   - `canonicalRecordId`
   - `projectionKind`
   - `sourceChannelType`
4. projection 丢了可以重建
5. canonical 正本丢了不允许靠 projection 反推重建 authority

memory sourcing rule：

1. committed canonical record 才能喂给 memory
2. candidate / pending_visible 不得进长期记忆
3. memory summary 的 owner 仍然固定在 `Cloud`
4. 本地面板看到的记忆摘要，只能来自 `Cloud canonical memory`

pending-visible special rule：

1. `pending_visible` 只允许用于纯文本玩家可见面
2. 不允许借 `pending_visible` 冒充：
   - item committed
   - relation committed
   - world event committed

remote / group special rule：

1. `remote_direct unavailable_now`
   - 不创建 canonical chat record
   - 只保留 trace 和 thread-local unavailable result
2. `group_chat queued / draft / render_failed`
   - 不得冒充 committed group history
3. `group_chat` 投影到参与者私聊面板时，必须保持 `canonicalRecordId` 不变

forbidden paths：

1. 不允许 `LocalProjectionStore` 长成正式历史 owner
2. 不允许手机私信线程单独存一套 authoritative direct history
3. 不允许群聊 transcript cache 单独存一套 authoritative group history
4. 不允许 thought preview 写进普通私聊历史冒充真实发言

update trigger：

- canonical record state 变化
- authoritative join key 变化
- projection 与 canonical 的关系变化
- private / direct / group / thought 的 history owner 变化
