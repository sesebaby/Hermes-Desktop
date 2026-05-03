# 任务陈述

为星露谷 NPC 运行时制定并审查一套更完善的方案，把 NPC persona / pack 根目录与通用编程 workspace 彻底解耦，避免桌面 exe 启动位置或错误环境变量导致 NPC 常驻自治失效。

## 期望结果

- NPC persona 根目录不再依赖 `HERMES_DESKTOP_WORKSPACE`。
- 桌面端无论从仓库、构建输出还是独立 exe 启动，都能稳定找到并使用星露谷 NPC personas。
- 当前 `C:\src\game\stardew\personas` 这类错误路径不再导致自治循环整体失效。
- 计划与测试规格使用中文，便于后续直接执行。

## 已确认事实

- SMAPI 实际从 `D:\Stardew Valley\Stardew Valley.v1.6.15\Mods` 加载 `StardewHermesBridge`。
- `C:\Users\Administrator\AppData\Local\hermes\hermes-cs\stardew-bridge.json` 显示当前 saveId 为 `1_435026555`。
- 桌面日志 `C:\Users\Administrator\AppData\Local\hermes\hermes-cs\logs\hermes.log` 持续报错：
  - `No Stardew NPC packs were found under 'C:\src\game\stardew\personas'.`
- 用户级和进程级 `HERMES_DESKTOP_WORKSPACE` 当前都是 `C:\`。
- 仓库里的真实 personas 根目录存在：
  - `D:\GitHubPro\Hermes-Desktop\src\game\stardew\personas`
- 当前这次手测的 SMAPI / bridge 日志中，没有出现真正的 `task_move_*`、`task_completed`、`action_speak_*`、`action_open_private_chat_*` 成功链路；最新时段主要是点击追踪。
- 用户明确表示：这个项目是专门为游戏改造的，编程 workspace 对该需求没有意义。

## 约束

- 计划名称与内容必须使用中文。
- 方案要保持当前游戏定制方向，不把 NPC 资源发现再绑定回通用 workspace 语义。
- 尽量收窄改动面，只动 NPC persona / pack 发现与同步链路，不破坏桌面 agent 的其它行为。
- 后续执行必须可测试、可回归。

## 仍需注意的未知项

- 是否需要把 personas 在启动时复制到本地托管目录，还是直接从“已发现源目录”读取即可。
- 是否需要增加显式配置项，作为极端场景下的人工兜底。
- 是否需要在 Dashboard / 设置页增加明确的诊断显示，提示当前 NPC pack 根目录来自哪里。

## 可能触达的代码位置

- `Desktop/HermesDesktop/Services/HermesEnvironment.cs`
- `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `src/games/stardew/StardewNpcRuntimeBindingResolver.cs`
- `Desktop/HermesDesktop.Tests/Services/*`
- `Desktop/HermesDesktop.Tests/Stardew/*`
