# Stardew Mod Package Install Update Rollback Contract

状态：

- active design baseline

owner：

- launcher product owner
- stardew integration owner

用途：

- 用大白话写死：桌面程序怎么管 Stardew 的 mod 包下载、安装、更新、回滚。

固定回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`

authoritative split：

- `Launcher`
  - 负责包列表、版本说明、安装结果、回滚入口前台
- `Launcher.Supervisor`
  - 负责检测环境、执行安装/更新/回滚、产出结果

包类型：

1. `superpowers_stardew_mod`
2. `runtime_local_bundle`
3. `stardew_dependency_bundle`

固定不包含：

1. 游戏 prompt 资产下载
2. prompt 明文包

package action states：

1. `not_installed`
2. `downloading`
3. `downloaded`
4. `installing`
5. `installed`
6. `updating`
7. `rollback_available`
8. `rolling_back`
9. `failed`

action minima：

- `packageId`
- `gameId`
- `installedVersion`
- `targetVersion`
- `actionState`
- `failureClass`
- `recoveryEntryRef`
- `operationReceiptRef`

安装死规则：

1. 安装前必须先做前置检测
2. SMAPI / 路径 / 依赖不满足，直接阻断安装
3. 安装失败必须给：
   - `failureClass`
   - `recoveryEntryRef`
   - `operationReceiptRef`

更新死规则：

1. 更新前必须确认当前已安装版本
2. 更新失败时必须保留上一可用版本
3. 不允许更新失败后留半安装状态却还显示“已完成”

回滚死规则：

1. 只有存在上一可用版本时才给回滚入口
2. 回滚成功后必须刷新安装状态
3. 回滚失败也必须保留明确 receipt

玩家可见宿主：

1. 游戏页
2. Stardew 游戏配置页
3. 通知页
4. 支持与帮助页

绝对禁止：

1. 不允许把 mod 下载结果和 readiness verdict 混成一个状态
2. 不允许安装失败却显示“可以开始”
3. 不允许把 prompt 资产暴露成可下载文件

update trigger：

- 包类型变化
- 安装/更新/回滚状态变化
- 前置检测阻断规则变化
