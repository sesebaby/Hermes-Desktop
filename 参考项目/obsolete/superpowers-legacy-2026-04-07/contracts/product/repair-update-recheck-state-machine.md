# Repair Update Recheck State Machine

状态：

- active design baseline

owner：

- launcher product owner
- launcher supervisor owner

用途：

- 用大白话写死：桌面程序里的修复、更新、重新检查到底怎么流转，玩家每一步看见什么。

固定回链：

- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
- `docs/superpowers/contracts/product/stardew-mod-package-install-update-rollback-contract.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-state-machine-catalog-appendix.md`

state owner：

- `Launcher.Supervisor` 负责状态推进
- `Launcher` 负责玩家可见面和操作入口

固定状态：

1. `idle`
2. `checking`
3. `repair_required`
4. `repairing`
5. `update_required`
6. `updating`
7. `rechecking`
8. `ready`
9. `failed`

状态迁移：

- `idle -> checking`
- `checking -> ready`
- `checking -> repair_required`
- `checking -> update_required`
- `repair_required -> repairing`
- `repairing -> rechecking`
- `update_required -> updating`
- `updating -> rechecking`
- `rechecking -> ready`
- `repairing -> failed`
- `updating -> failed`
- `rechecking -> failed`

每步玩家可见面：

1. `checking`
   - 游戏页 / 配置页显示“正在检查”
2. `repair_required`
   - 游戏页 / 配置页显示“需要修复”
3. `repairing`
   - 配置页显示当前修复中
4. `update_required`
   - 游戏页 / 配置页显示“需要更新”
5. `updating`
   - 游戏页 / 配置页显示当前更新中
6. `rechecking`
   - 配置页显示“正在重新检查”
7. `ready`
   - 首页主卡 / 游戏页显示“可以开始”
8. `failed`
   - 配置页 + 支持与帮助页显示失败和恢复入口

死规则：

1. 修复和更新完成后，必须自动进 `rechecking`
2. 不允许修完就直接显示“可以开始”，却没重新检查
3. 失败时必须保留：
   - `failureClass`
   - `recoveryEntryRef`
   - `operationReceiptRef`

绝对禁止：

1. 不允许 `Launcher` 自己推进状态机
2. 不允许某次修复失败后，还沿用上一次 `ready` 结果
3. 不允许更新失败却静默回到 `idle`

update trigger：

- 修复/更新/重检状态变化
- 自动重检规则变化
- 玩家可见失败面变化
