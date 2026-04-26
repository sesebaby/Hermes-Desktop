# Support Closure State Machine

状态：

- active design baseline

owner：

- support operations owner
- launcher product owner

用途：

- 用大白话写死：支持工单什么时候算结束，玩家桌面端看到什么状态。

固定回链：

- `docs/superpowers/contracts/product/support-ticket-and-diagnostic-bundle-contract.md`
- `docs/superpowers/contracts/product/launcher-notification-feed-contract.md`

固定状态：

1. `draft`
2. `submitted`
3. `waiting_for_response`
4. `needs_player_reply`
5. `resolved`
6. `closed`
7. `failed`

玩家可见死规则：

1. `submitted`
   - 必须有回执
2. `waiting_for_response`
   - 明确告诉玩家正在处理中
3. `needs_player_reply`
   - 必须有明确补充说明入口
4. `resolved`
   - 明确告诉玩家建议方案已给出
5. `closed`
   - 明确告诉玩家工单已结束

绝对禁止：

1. 不允许没有 `resolved` 或玩家确认就直接跳 `closed`
2. 不允许 `failed` 冒充 `submitted`
3. 不允许支持状态变化不进通知

update trigger：

- 支持闭环状态变化
- 玩家回复要求变化
- resolved / closed 区分变化
