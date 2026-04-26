# Stardew Phone Group Thread Contract

状态：

- active design baseline

owner：

- stardew integration owner

用途：

- 用大白话冻结手机主动群聊线程的 owner、unread、DND、message bucket，不允许继续只当一次性菜单。

固定回链：

- `docs/superpowers/contracts/runtime/stardew-group-chat-session-contract.md`
- `docs/superpowers/contracts/runtime/private-dialogue-commit-state-machine-contract.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-hook-session-projector-executor-appendix.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

thread owner：

- `PhoneGroupThreadSession`

identity：

- `contactGroupId`

required fields：

- `contactGroupId`
- `threadKey`
- `unreadCount`
- `doNotDisturb`
- `messageBucketRef`
- `lastCommittedTurnRef`
- `lastFinalizeReceiptRef`
- `lastHostRenderReceiptRef`
- `connectionState`
- `failureClass`

authoritative boundary：

- `Cloud` 拥有 canonical group turn 正本
- `Runtime.Local` 拥有 finalize verdict 与 finalize receipt
- `PhoneGroupThreadSession` 只拥有：
  - 群线程壳
  - unread / dnd / bucket 本地状态
  - 最后一次已加载 turn ref

固定规则：

1. 一个 `contactGroupId` 只能对应一条固定线程
2. 关闭再打开同一群，必须回同一线程
3. 后台远程群活动必须继续累计 unread
4. `DayStarted` 只能重建状态，不能清空 bucket

thread states：

- `not_open`
- `opening`
- `ready`
- `submission_failed`
- `render_failed`
- `closed`

unread rules：

1. 只有 committed group turn 才能增加 unread
2. 玩家打开线程并看到对应 turn 后，才能扣 unread
3. 不允许用 UI 打开动作直接清空全部 unread

dnd rules：

1. DND 是 thread state，不是 build 开关
2. DND 开启后仍可追加后台 bucket，只是不主动打扰

receipt / writeback rules：

1. 只有 `Runtime.Local` finalize 成功后，才允许把 turn 标成已 committed
2. `lastFinalizeReceiptRef` 只引用正式 finalize receipt，不允许引用 UI 本地成功提示
3. `lastHostRenderReceiptRef` 只记录宿主消息已显示证据，不替代 canonical turn 正本
4. 玩家打开线程、看到该条 turn、且 finalize 已成功后，才允许扣减对应 unread

绝对禁止：

1. 不允许 `UnreadCount` / `DoNotDisturb` 只存在 `PhoneActiveGroupChatMenu`
2. 不允许 `RuntimeClient` 请求时默认硬填 `UnreadCount = 0`
3. 不允许线程关闭时直接丢失 message bucket

update trigger：

- unread 规则变化
- DND 规则变化
- phone group thread state 变化
