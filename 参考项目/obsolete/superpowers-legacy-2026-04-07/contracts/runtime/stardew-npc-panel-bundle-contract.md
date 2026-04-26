# Stardew Npc Panel Bundle Contract

状态：

- active design baseline

owner：

- stardew integration owner

用途：

- 用大白话冻结 NPC 信息面板一次正式加载到底要拿哪些数据、谁来组包、谁来决定 disclosure，不允许后面实现时再各拼各的。

固定回链：

- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-hook-session-projector-executor-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-rebuild-and-implementation-appendix.md`

authoritative boundary：

- `NpcInfoPanelSurfaceSession` 拥有面板 session 壳
- `Runtime.Local` 拥有 panel bundle response 的统一返回壳
- `Runtime.Stardew` adapter 拥有 title-local snapshot 组包
- `BuildExposurePolicy` 只能提供 build 露出规则，不直接冒充 panel disclosure 真相

request minima：

1. `requestId`
2. `traceId`
3. `gameId`
4. `actorId`
5. `targetNpcId`
6. `requestedTabs`
7. `historyWindow`

bundle minima：

1. `npcProfile`
2. `relationSummary`
3. `memoryPreview`
4. `itemRecords`
5. `groupHistoryDisclosureState`
6. `groupHistoryPreview`
7. `failureClass`
8. `recoveryEntryRef`

固定规则：

1. 面板基础资料、关系、记忆、物品、群聊历史 disclosure 必须走同一次正式 bundle
2. `Thought` 预览不混进这个 bundle，仍走独立 `thought-preview`
3. `groupHistoryDisclosureState` 只能由 `NpcInfoPanelSurfaceSession` 基于 policy 结果解析后消费
4. 不允许控制器直接往 menu 里一项项手塞 profile / relation / item / group history

绝对禁止：

1. 不允许 `BuildExposureConfig` 直接生成 `groupHistoryDisclosureState`
2. 不允许 `NpcNaturalInteractionController` 自己并行拼 panel data loader
3. 不允许 `NpcInfoPanelMenu` 直接吃 runtime envelope 原始字段

update trigger：

- panel bundle 字段变化
- disclosure 规则变化
- requestedTabs 规则变化
