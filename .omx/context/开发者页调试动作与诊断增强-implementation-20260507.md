# 开发者页调试动作与诊断增强实现上下文快照

## 任务陈述

按 `.omx/plans/产品需求文档-开发者页调试动作与诊断增强-20260507.md` 实现开发者页后续阶段，包括 bridge 调试放置、开发者页受控调试、追踪筛选、诊断包导出与上下文注入差异视图。

## 期望结果

- SMAPI 新增 `npc_town` 短命令，支持 Haley/Penny 中英文别名并把目标 NPC 放到 `Town` 固定可站立点。
- bridge HTTP 调试端点与 SMAPI 命令复用同一套受限 debug reposition 核心。
- DeveloperPage 新增中文 `受控调试` 区域，复用现有说话、单步唤醒，并新增放到镇上。
- inspector 支持 runtime/transcript 投影筛选、诊断包导出与上下文注入块元数据投影。
- 验证覆盖 bridge tests、desktop tests 与桌面构建。

## 已知事实与证据

- `$team` runtime 在当前 PowerShell leader 环境不可直接启动：`tmux` 不在 Windows PATH，`$TMUX` 未设置；WSL 有 `tmux 3.4`，但 WSL 内 `omx` 缺少 Linux `node`。
- 产品需求与测试规范均已读取，推荐 lane 是 bridge、desktop、diagnostics、test/review。
- 桌面子树 `AGENTS.md` 与相关 `.github/instructions/*.instructions.md` 已读取；UI、字符串、HTTP/文件访问、测试都需要遵守对应规则。
- 历史错误中与本任务强相关的约束：
  - 不用 `NPC.controller` 或 `Game1.warpCharacter` 伪装自然移动完成。
  - status/diagnostics 要区分当前 NPC 状态与 later preflight failure。
  - 任意诊断文本和查询要处理标点、错误码和 NPC 名称，不把日志首行当完整证据。

## 约束

- 不新增第二套 NPC runtime、todo、memory、transcript、trace 数据库或工具 lane。
- 调试 reposition 是显式 debug teleport，日志和 UI 必须与自然移动验收隔离。
- 诊断导出默认本地 zip，必须脱敏 bridge token、Authorization、apiKey/apikey、secret、password、token、connectionString 等字段。
- 用户可见 UI 文案走 `.resw`；普通标签中文，原始日志/JSON/路径/工具名保留原文。
- 保持改动小、复用现有 service/inspector 模式，不新增依赖。

## 未知与风险

- 真实 Stardew `Town` 的 `town.square` 附近 tile 是否总可站立，需手测验证。
- 当前运行环境不能执行 tmux-based OMX team；需要在本线程完成实现，并在最终报告中明确 team runtime blocker。
- UI 运行验证可能受本机 WinUI/Stardew 环境限制，至少需要跑相关 tests 和 desktop build。

## 可能代码触点

- `Mods/StardewHermesBridge/Commands/TestTeleportCommand.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeDestinationRegistry.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
- `Mods/StardewHermesBridge.Tests/*`
- `src/games/stardew/StardewBridgeDiscovery.cs`
- `src/games/stardew/StardewAutonomyTickDebugService.cs`
- `src/runtime/NpcDeveloperInspectorService.cs`
- `Desktop/HermesDesktop/Views/DeveloperPage.xaml`
- `Desktop/HermesDesktop/Views/DeveloperPage.xaml.cs`
- `Desktop/HermesDesktop/Views/AgentPage.xaml`
- `Desktop/HermesDesktop/Views/AgentPage.xaml.cs`
- `Desktop/HermesDesktop/Strings/zh-cn/Resources.resw`
- `Desktop/HermesDesktop/Strings/en-us/Resources.resw`
