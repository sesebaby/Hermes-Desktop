# Stardew Launcher Workspace IA

状态：

- active design baseline

owner：

- launcher product owner
- stardew integration owner

用途：

- 用大白话写死：桌面程序里 Stardew 工作区到底有哪些区块，玩家打开后先看什么、再看什么。

固定回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
- `docs/superpowers/contracts/product/launcher-game-settings-surface-contract.md`

workspace 一级结构：

1. `概览`
2. `运行状态`
3. `Mod 管理`
4. `帮助与修复`
5. `游戏设置`

玩家进入 Stardew 工作区后的阅读顺序：

1. 先看 `概览`
2. 再看 `运行状态`
3. 有安装问题去 `Mod 管理`
4. 有启动问题去 `帮助与修复`
5. 有路径和启动器问题去 `游戏设置`

各区块最小内容：

### 概览

- 当前 readiness 文案
- 主 CTA
- 最近问题摘要
- 最近一次成功进入状态

### 运行状态

- runtime health
- preflight 状态
- recoveryEntryRef
- 当前失败类

### Mod 管理

- 当前已装版本
- 可更新版本
- 安装 / 更新 / 回滚入口
- 最近一次包操作结果

### 帮助与修复

- 一键修复
- 重新检查
- 提交问题
- 最近回执
- 常见问题

### 游戏设置

- SMAPI 路径
- 启动模式
- 当前路径状态
- 正式字段、保存回执、路径检测动作只认：
  - `launcher-game-settings-surface-contract.md`

死规则：

1. `概览` 不得塞成配置页
2. `帮助与修复` 不得承担 mod 包管理
3. `Mod 管理` 不得显示 prompt 资产
4. `运行状态` 只能展示单一 readiness 相关真相，不重算

update trigger：

- workspace 一级结构变化
- 各区块最小内容变化
- Stardew 页面阅读顺序变化
