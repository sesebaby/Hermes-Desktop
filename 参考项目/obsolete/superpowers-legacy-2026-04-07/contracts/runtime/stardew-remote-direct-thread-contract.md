# Stardew Remote Direct Thread Contract

状态：

- active design baseline

owner：

- runtime architecture owner
- stardew integration owner

用途：

- 用大白话冻结 Stardew 手机私信线程谁拥有、thread key 怎么算、打开线程后读什么、提交消息后如何回到正式主线。

固定回链：

- `docs/superpowers/contracts/runtime/stardew-phone-contact-entry-contract.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-rebuild-and-implementation-appendix.md`

authoritative boundary：

- `Cloud` 拥有 canonical chat truth
- `Runtime.Local` 拥有 deterministic gate 与 finalize outcome
- `Superpowers.Stardew.Mod` 只拥有：
  - 线程宿主壳
  - thread session 壳
  - player-visible failure / recovery surface

thread identity：

- `threadKey = gameId + actorId + targetId + channelType`

固定规则：

1. 同一联系人重复打开，必须复用同一 `threadKey`
2. 不允许按时间、请求 id、surface id 重新造线程
3. `threadKey` 只标识线程，不替代 canonical history key

thread session owner：

- `RemoteDirectThreadSession`

它固定拥有：

- `threadKey`
- `targetId`
- `availabilityState`
- `openState`
- `failureState`
- `composerState`
- `lastLoadedHistoryRef`

thread states：

- `not_open`
- `opening`
- `ready`
- `unavailable_now`
- `submission_failed`
- `render_failed`
- `closed`

open-thread flow：

1. 联系人入口给出 `threadKey`
2. Mod 创建或恢复 `RemoteDirectThreadSession`
3. Mod 发 `RemoteDirectRequest`
4. Runtime 回 `availabilityState + historyOwnerActorId`
5. Mod 读取 recent direct history
6. Projector 填充线程 UI

submit flow：

1. 玩家在手机线程发消息
2. 先走同一 `threadKey`
3. 走 `remote_direct_one_to_one` request
4. accepted turn 仍写入 actor-owned private/direct history
5. finalize 成功后才算 committed

history source：

1. authoritative source 固定为：
   - actor-owned private/direct history
2. 不允许手机私信另做一套本地聊天正本

player-visible minima：

- `participants`
- `transcript`
- `availabilityState`
- `failureClass`
- `recoveryEntryRef`
- `draftText`
- `inputEnabled`

failure rules：

1. `availability_blocked`
   - 只显示当前不可联系
2. `submission_failed`
   - 只表示当前这次发失败
3. `render_failed`
   - 只表示线程显示失败

绝对禁止：

1. 不允许 `PhoneDirectMessageMenu` 自己判断可不可联系
2. 不允许 `RuntimeClient` 硬填 `available_now`
3. 不允许本地创建 deferred send queue
4. 不允许 remote direct 另建独立 history truth

update trigger：

- thread key 规则变化
- 手机线程状态机变化
- canonical history 接法变化

