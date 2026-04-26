# Launcher Supervisor Boundary Contract

状态：

- active design baseline

owner：

- launcher product owner
- runtime architecture owner

用途：

- 用大白话写死：`Launcher` 和 `Launcher.Supervisor` 各自管什么，谁能给玩家下启动结论，谁不能越权。

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`
- `docs/superpowers/governance/current-phase-boundary.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`

authoritative split：

- `Launcher`
  - 负责玩家前台
  - 负责账号、游戏页、产品与兑换、通知、支持与帮助、设置
  - 负责把单一真相翻成玩家看得懂的大白话
- `Launcher.Supervisor`
  - 负责启动前检查
  - 负责本地修复、更新、重检动作
  - 负责拉起 / 停止 `Runtime.Local`
  - 负责生成单一 `launchReadinessVerdict`

单一真相死规则：

1. 启动 readiness 只认 `Launcher.Supervisor`
2. `Launcher` 只消费：
   - `launchReadinessVerdict`
   - `runtimeHealthFact`
   - `runtimePreflightFact`
   - `recoveryEntryRef`
3. `Launcher` 不允许自己重算第二套 verdict
4. `Runtime.Local` 也不允许直接对玩家宣称“可以启动”

`Launcher` 只准做：

1. 展示首页主卡
2. 展示游戏页和游戏工作区
3. 展示产品与兑换
4. 展示通知 feed
5. 展示支持与帮助
6. 接玩家按钮操作，再把动作转给对应 owner

`Launcher` 绝对不准做：

1. 直接读取一堆本地碎文件后自己拼启动结论
2. 自己推断 entitlement
3. 自己决定 provider / prompt / canonical history 真相
4. 自己执行宿主修复脚本

`Launcher.Supervisor` 只准做：

1. 采集 `RuntimePreflightFact`
2. 采集 `RuntimeHealthFact`
3. 结合 policy 和 access decision 算出 `LaunchReadinessVerdict`
4. 执行：
   - 修复
   - 更新
   - 重检
   - 启动 Runtime.Local
5. 给 `Launcher` 回标准化结果

`Launcher.Supervisor` 绝对不准做：

1. 长成产品页
2. 自己管理通知 feed
3. 自己管理支持工单前台
4. 自己长出第二套产品 claim

玩家可见宿主：

1. 首页主卡
2. 游戏页
3. Stardew 游戏配置页
4. 支持与帮助页
5. 通知页

失败暴露规则：

1. 启动失败
   - 先露在首页主卡和游戏页
2. 修复失败
   - 露在 Stardew 游戏配置页和支持与帮助页
3. 更新失败
   - 露在游戏页和通知页
4. 重检失败
   - 露在 Stardew 游戏配置页

当前代码现实绑定：

- `src/Superpowers.Launcher/ViewModels/LauncherShellViewModel.cs`
- `src/Superpowers.Launcher/ViewModels/StardewGameConfigViewModel.cs`
- `src/Superpowers.Launcher.Supervisor/Readiness/StardewReadinessEvaluator.cs`

retirement rule：

1. `LauncherShellViewModel` 里直接读本地 readiness 文件，只能视为开发阶段临时壳
2. 正式主线必须迁到 `Launcher.Supervisor` 统一给 verdict

update trigger：

- Launcher / Supervisor 职责变更
- readiness 单一真相规则变更
- 玩家可见失败面变更
