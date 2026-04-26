# Supervisor Preflight Input Matrix

状态：

- active design baseline

owner：

- launcher supervisor owner

用途：

- 用大白话写死：`Launcher.Supervisor` 在算启动前检查时，最少吃哪些输入，哪些输入缺了就必须 fail-closed。

固定回链：

- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
- `docs/superpowers/contracts/product/repair-update-recheck-state-machine.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`

preflight owner：

- `Launcher.Supervisor`

必查输入矩阵：

1. `game installation`
   - 游戏路径
   - 游戏主程序存在
2. `SMAPI readiness`
   - `StardewModdingAPI.exe` 路径
   - 版本可用性
3. `mod package readiness`
   - `superpowers_stardew_mod` 是否存在
   - 当前版本
4. `runtime local readiness`
   - `Runtime.Local` 是否可启动
   - 当前健康状态 ref
5. `access / entitlement`
   - 当前账号状态
   - capability access decision ref

输入最小字段：

- `runtimePreflightRef`
- `gameId`
- `gameInstallPath`
- `smapiPath`
- `smapiDetected`
- `modPackageState`
- `runtimeLocalState`
- `capabilityAccessDecisionRef`
- `quarantineStateRef`

状态判定死规则：

1. 游戏路径缺失
   - `needs_repair`
2. SMAPI 缺失或不可用
   - `needs_repair`
3. mod 包缺失
   - `needs_repair`
4. Runtime.Local 不可达
   - `needs_repair`
5. 访问被拒
   - `blocked`
6. 版本不符但可升级
   - `needs_update`

fail-closed rule：

1. 关键输入缺失时，不允许默认算 `ready`
2. 任一输入拿不到 authoritative ref 时，要给明确失败类
3. 不允许只因为本地上一次成功过，就复用旧结论

当前代码现实绑定：

- `RuntimePreflightFact`
- `RuntimeHealthFact`
- `CapabilityAccessDecision`
- `LaunchReadinessPolicySnapshot`
- `StardewReadinessEvaluator`

update trigger：

- preflight 输入项变化
- 输入缺失时的 fail-closed 规则变化
- `needs_repair / needs_update / blocked` 判定变化
