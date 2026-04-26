# Stardew Group Chat Session Contract

状态：

- active design baseline

owner：

- runtime architecture owner
- stardew integration owner

用途：

- 用大白话冻结现场群聊和手机群聊的 session / thread 边界，防止群聊实现继续飘。

固定分层：

1. `OnsiteGroupSession`
   - 现场群聊 session owner
2. `PhoneGroupThreadSession`
   - 手机固定群 thread owner

onsite session identity：

- `groupSessionKey`

phone group identity：

- `contactGroupId`

onsite create rules：

1. 只有玩家发出第一句后，才创建现场 `groupSession`
2. 现场 participant set 由宿主采样后冻结
3. 当前轮 participant set 冻结，加入/离开只影响下一轮

participant freeze algorithm minima：

- 当前 location 已加载
- 当前可见
- 当前可交互
- 非 cutscene / dialogue lock
- 与玩家距离不超过 title-local 距离阈值
- 按稳定 actorId 排序后冻结

onsite destroy rules：

- `Warped`
- 睡觉
- 切日
- 当前群聊 surface 被 authoritative 关闭

phone group thread rules：

1. 手机群线程固定由 `contactGroupId` 拥有
2. 同一群重复打开，必须回到同一线程
3. 后台群活动追加到同一线程 bucket

session minima：

- `sessionOrThreadId`
- `participantIds`
- `inputSequenceId`
- `unreadCount`
- `doNotDisturb`
- `connectionState`
- `failureState`

group history mirror rules：

1. committed group turn 先写群历史
2. 再按来源规则镜像进 group history tab / private projections
3. 不允许群 turn 还没 committed 就先加 unread

绝对禁止：

1. 不允许现场群聊和手机群聊共用一套临时内存 owner 就完事
2. 不允许 `UnreadCount` / `DoNotDisturb` 只活在 UI 字段里
3. 不允许 participant set 由 UI 自己临时决定

update trigger：

- participant freeze 规则变化
- group session 生命周期变化
- phone group thread 生命周期变化

