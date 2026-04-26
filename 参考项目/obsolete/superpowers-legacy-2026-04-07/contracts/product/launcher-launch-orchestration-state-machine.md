# Launcher Launch Orchestration State Machine

状态：

- active design baseline

owner：

- launcher supervisor owner
- launcher product owner

用途：

- 用大白话写死：玩家在桌面程序里从“看到开始按钮”到“真的进游戏”，中间经过哪些状态。

固定回链：

- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
- `docs/superpowers/contracts/product/repair-update-recheck-state-machine.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

state owner：

- `Launcher.Supervisor` 推进正式状态
- `Launcher` 负责显示和按钮

固定状态：

1. `verdict_loading`
2. `ready_to_launch`
3. `open_repair_required`
4. `update_required`
5. `launch_blocked`
6. `launching_runtime`
7. `launching_game`
8. `running`
9. `launch_failed`

状态迁移：

- `verdict_loading -> ready_to_launch`
- `verdict_loading -> open_repair_required`
- `verdict_loading -> update_required`
- `verdict_loading -> launch_blocked`
- `ready_to_launch -> launching_runtime`
- `launching_runtime -> launching_game`
- `launching_game -> running`
- `launching_runtime -> launch_failed`
- `launching_game -> launch_failed`
- `open_repair_required -> verdict_loading`
- `update_required -> verdict_loading`

按钮文案绑定：

1. `ready_to_launch`
   - `启动游戏`
2. `open_repair_required`
   - `打开修复`
3. `update_required`
   - `立即更新`
4. `launch_blocked`
   - `联系支持` 或 `查看原因`
5. `running`
   - `继续运行`

当前代码现实绑定：

- `LaunchReadinessVerdict.Verdict`
  - `ready`
  - `needs_repair`
  - `needs_update`
  - `blocked`
  - `running`
- `LauncherActionViewModel.DisplayLabel`
- `LauncherShellViewModel.ExecutePrimaryAction`

死规则：

1. 没拿到正式 verdict 前，只能是 `verdict_loading`
2. `launch_blocked` 不允许直接启动游戏
3. 进入 `launch_failed` 后必须回到：
   - 游戏页
   - Stardew 配置页
   - 支持与帮助
4. `running` 不是“启动成功的猜测”，而是已有正式运行事实

绝对禁止：

1. 不允许 `Launcher` 只凭按钮点了就显示“启动成功”
2. 不允许 `launch_failed` 后还沿用旧的 `ready_to_launch` 玩家文案
3. 不允许 `continue` 和 `launch` 混成一个没有区别的状态

update trigger：

- 启动状态变化
- CTA 映射变化
- launch_failed 恢复入口变化
