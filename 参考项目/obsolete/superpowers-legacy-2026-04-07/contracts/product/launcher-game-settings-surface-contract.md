# Launcher Game Settings Surface Contract

状态：

- active design baseline

owner：

- launcher product owner

用途：

- 用大白话写死：桌面程序里的 `游戏设置` 区块到底展示什么、存什么、谁来检测路径，不允许再让页面自己拼一套本地规则。

固定回链：

- `docs/superpowers/contracts/product/stardew-launcher-workspace-ia.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-interface-and-class-landing-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-bridge-and-dto-contract-appendix.md`

authoritative boundary：

- `Launcher` 拥有 `游戏设置` 页面壳和玩家可见 copy
- `Launcher.Supervisor` 拥有路径检测、启动模式校验、保存回执
- `Cloud` 不拥有本地 SMAPI 路径和本地启动模式

固定区块字段：

1. `gameId`
2. `smapiPath`
3. `launchMode`
4. `pathState`
5. `pathFailureClass`
6. `lastValidatedAt`
7. `lastSavedAt`

固定动作：

1. `读取当前设置`
2. `自动检测路径`
3. `保存设置`
4. `重检路径状态`

玩家可见规则：

1. `游戏设置` 只展示路径、启动模式、当前路径状态
2. 不允许把 readiness verdict 混进这里
3. 不允许把 entitlement、prompt、provider 之类信息塞进这里

receipt rule：

1. 保存设置后必须返回正式 receipt
2. 自动检测后必须返回正式 receipt
3. 页面成功提示不得代替 receipt

绝对禁止：

1. 不允许 `StardewGameConfigViewModel` 自己决定路径是否有效
2. 不允许页面只存一个字符串路径，不存路径状态
3. 不允许把 `ResolveDefaultSmapiPath` 一类旧方法继续当正式主线

update trigger：

- 设置字段变化
- 动作变化
- 路径状态规则变化
