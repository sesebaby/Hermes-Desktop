# Stardew Phone Contact List Contract

状态：

- active design baseline

owner：

- stardew integration owner

用途：

- 用大白话冻结手机联系人列表怎么加载、列表里最少要有什么、列表加载和线程 owner 怎么分开，不允许把联系人页和私信线程混成一坨。

固定回链：

- `docs/superpowers/contracts/runtime/stardew-phone-contact-entry-contract.md`
- `docs/superpowers/contracts/runtime/stardew-remote-direct-thread-contract.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-hook-session-projector-executor-appendix.md`

authoritative boundary：

- `PhoneContactBookRouteCoordinator` 拥有联系人页加载流程
- `RemoteDirectAvailabilityResolver` 拥有每个联系人当前可不可联系的宿主事实判断
- `RemoteDirectThreadSession` 只在玩家点进某个联系人后才成为线程 owner

request minima：

1. `requestId`
2. `traceId`
3. `gameId`
4. `actorId`
5. `surfaceId`

response minima：

1. `contacts`
2. `failureClass`
3. `recoveryEntryRef`
4. `loadedAt`

固定规则：

1. 联系人列表必须先加载列表，再允许进入某个私信线程
2. 联系人是否显示，和当前能不能联系是两回事
3. 列表项里的 `availabilityState` 只能来自宿主事实，不得来自 build 开关
4. 点击联系人后，才允许计算并固定该联系人的 `threadKey`

绝对禁止：

1. 不允许联系人列表直接复用 `RemoteDirectThreadSession` 当列表 owner
2. 不允许用热键或 debug 入口冒充正式联系人列表入口
3. 不允许按 NPC 名字在 UI 层硬编码列表

update trigger：

- 联系人列表字段变化
- 列表 owner 变化
- 列表和线程边界变化
