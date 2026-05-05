# E-2026-0505-stardew-bridge-dll-lock-during-desktop-deploy

- id: E-2026-0505-stardew-bridge-dll-lock-during-desktop-deploy
- title: SMAPI 运行时桌面部署会被 StardewHermesBridge.dll 文件锁打断
- updated_at: 2026-05-05
- keywords: [desktop-deploy, stardew, smapi, bridge, file-lock, StardewHermesBridge.dll]

## symptoms

- 执行 `scripts\deploy-desktop.ps1 -Configuration Debug` 时，桌面端 publish 还没装完就失败。
- 错误来自 `Pathoschild.Stardew.ModBuildConfig`，提示无法复制 `D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\StardewHermesBridge\StardewHermesBridge.dll`，因为文件正被另一个进程使用。
- 同时 `StardewModdingAPI.exe` 正在运行并加载了 `Stardew Hermes Bridge`。

## trigger_scope

- 桌面端小修复部署。
- SMAPI/星露谷正在运行。
- `Desktop\HermesDesktop\HermesDesktop.csproj` 默认引用 `Mods\StardewHermesBridge\StardewHermesBridge.csproj`，且 `HermesAutoPublishStardewBridge` 没设为 `false`。

## root_cause

桌面项目默认会顺带构建并部署 Stardew bridge。SMAPI 运行中会锁住已加载的 `StardewHermesBridge.dll`，所以 ModBuildConfig 复制 bridge DLL 到游戏 Mods 目录时失败，导致整个桌面部署脚本提前退出。

## bad_fix_paths

- 把这类失败误判成桌面端代码构建失败。
- 为了桌面端核心库小修强行关闭/重启游戏，打断手测现场。
- 重复运行同一个部署脚本但不处理 DLL 锁，仍会失败。

## corrective_constraints

- 只部署桌面端且不需要更新 bridge 时，用 `dotnet publish ... -p:HermesAutoPublishStardewBridge=false`，再复制 publish 输出到安装目录。
- 需要更新 bridge 时，必须先关闭 SMAPI/游戏，再运行完整部署或 bridge 构建部署。
- 日志判断时要区分“桌面发布失败”和“bridge DLL 被运行中的 SMAPI 锁住”。

## verification_evidence

- 用 `-p:HermesAutoPublishStardewBridge=false` 重新发布桌面端成功。
- 安装目录 `C:\Users\Administrator\AppData\Local\Programs\HermesDesktop\Hermes.Core.dll` 更新到 2026-05-05 11:05:56。
- 重启后的 HermesDesktop 进程正常接上当前 SMAPI bridge，并出现新的 `Stardew autonomy LLM turn started; ... maxToolIterations=6` 日志。
