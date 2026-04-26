# Stardew Onsite Group Overlay Contract

状态：

- active design baseline

owner：

- stardew integration owner

用途：

- 用大白话冻结现场群聊 overlay 只负责显示什么，不负责拥有什么。

authoritative boundary：

- `OnsiteGroupChatOverlay` 只是现场群聊前台壳
- 它不拥有：
  - group session authority
  - participant freeze authority
  - committed truth

overlay minima：

- `isEnabledForPlayer`
- `frozenRoster`
- `visibleSpeakerRows`
- `inputEnabled`
- `failureClass`
- `recoveryEntryRef`

row kinds：

- `message`
- `action`
- `system`

input rules：

1. overlay 输入框只负责玩家输入
2. 真正的 input sequence owner 是 `OnsiteGroupSession`
3. `submission_failed` 只表示本轮输入没走通，不表示整个群聊已关闭

render rules：

1. overlay 上显示的 speaker rows 必须来自 committed 或 pending-visible 受控数据
2. 不允许 UI 自己决定发言顺序
3. 不允许 UI 自己补一条“差不多的 NPC 发言”

failure rules：

- `submission_failed`
- `render_failed`

绝对禁止：

1. 不允许 overlay 自己 freeze participants
2. 不允许 overlay 自己生成 speaker labels truth
3. 不允许 overlay 自己维护 group history

update trigger：

- onsite overlay row model 变化
- onsite input failure surface 变化

