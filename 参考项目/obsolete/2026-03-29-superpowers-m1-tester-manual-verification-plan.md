# Superpowers M1 测试手动验证计划

> 状态说明：
> - 文件名中的 `M1` 保留为历史测试计划名。
> - 当前执行 phase 已不再直接定义为 `M1`；当前手动验证口径以 `docs/superpowers/governance/current-phase-boundary.md` 为准。
> - 本文可继续作为上一阶段测试基线与 player-visible proof 参考，但不再单独定义“当前阶段名字”。

## 1. 目的

本计划是上一阶段 `Superpowers / M1` 测试基线的单一入口，也是当前阶段整理 player-visible proof 时的历史参考入口。

它只回答 4 个问题：

1. 你作为测试现在到底要人工检查什么。
2. 哪些只是开始前的前置条件，不算主测试项。
3. 你在 Launcher 和《星露谷》里到底能看到什么。
4. 什么结果算 `PASS`，什么只能记为 `PENDING EVIDENCE`、`OUT OF SCOPE` 或 `NOT PLAYER_VISIBLE`。

本计划优先级高于旧的实现任务计划，测试执行当前 `M1` 时默认从本文进入。

## 2. 当前边界

当前 `M1` 的 broader code scope 仍然是：

- `dialogue`
- `memory`
- `social transaction / commitment`

但当前**玩家可见手动测试**只覆盖你现在真的能看到、能操作、能截图留证的 UI。

当前真实可见的主手动测试项只有：

- Launcher 首页
- Launcher 《星露谷》配置页
- Stardew `AiDialogueMenu` shell
- Stardew `NpcInfoPanelMenu` shell

当前不进入主手动可视测试项的内容：

- `remote_direct_one_to_one`
- `group_chat`
- `information_propagation`
- `active_world`
- 当前没有稳定 in-host visible UI 的 `social transaction / commitment`
- 任何只能通过日志、hook、后台状态或自动化测试确认的内部行为

## 3. 结果术语

测试步骤只用以下状态：

- `PASS`
  - 当前步骤的人眼可见结果与预期一致，且已留图或等价可视证据
- `FAIL`
  - 当前步骤应当可见，但没有出现目标 UI，或出现了错误 UI
- `PENDING EVIDENCE`
  - 当前功能可能已有代码/日志/自动化测试支持，但本轮没有拿到人眼可见证据
- `OUT OF SCOPE`
  - 当前能力不在本轮 `M1` 玩家可见手动测试范围内
- `NOT PLAYER_VISIBLE`
  - 当前能力或语义存在于代码/测试/日志中，但当前玩家没有稳定可见 UI 可验证

与现有 player-visible evidence schema 的映射：

| 测试结果 | 证据结果 |
| --- | --- |
| `PASS` | `passed` |
| `FAIL` | `failed` |
| `PENDING EVIDENCE` | `visual_gate_pending` |
| `OUT OF SCOPE` | 不写入当前 player-visible evidence pass/fail |
| `NOT PLAYER_VISIBLE` | 不写入当前 player-visible evidence pass/fail |

## 4. 证明深度表

| 证明深度 | 证明了什么 | 没有证明什么 |
| --- | --- | --- |
| `startup only` | 程序或脚本能启动 | 不证明玩家看到目标 UI |
| `health only` | 服务健康检查通过 | 不证明 Launcher 或游戏内 surface 可见 |
| `mod loaded only` | SMAPI 已加载 mod | 不证明已进入真实存档 |
| `real save loaded` | 已进入真实存档 | 不证明特定 surface 已打开 |
| `surface shell visible` | 人眼可见 shell surface 已打开，关键文案可见 | 不等于 rich-playable 闭环 |

当前 `M1` 手动测试默认只要求到：

- Launcher：`surface shell visible`
- Stardew F8 / F9：`surface shell visible`

当前**不默认要求**：

- rich-playable dialogue 闭环
- rich-playable memory / thought / item 闭环

## 5. 环境前置条件

当前仓库默认按以下本地环境运行：

- Windows
- PowerShell
- `.NET 10 SDK`
- `.NET 6 SDK`
- `Stardew Valley 1.6.15`
- `SMAPI 4.1.10`

当前 repo 中出现的机器本地默认路径：

- Stardew / SMAPI:
  - `D:\Stardew Valley\Stardew Valley.v1.6.15\StardewModdingAPI.exe`
- Mod 部署目标:
  - `D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\Superpowers.Stardew.Mod`
- 当前手动测试配置文件:
  - `D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\Superpowers.Stardew.Mod\config.json`

当前代码中的相关假设来源：

- `scripts/dev/run-stardew-smapi.ps1`
- `scripts/dev/sync-stardew-mod.ps1`
- `games/stardew-valley/Superpowers.Stardew.Mod/Superpowers.Stardew.Mod.csproj`
- `src/Superpowers.Launcher/ViewModels/LauncherShellViewModel.cs`

如果你的本机路径不同，本计划开始前必须先改成你自己的可用路径。

Launcher 配置页里显示的 `启动器路径`，当前实际含义是：

- `SMAPI 可执行路径`

也就是你要填的是：

- `StardewModdingAPI.exe`

不是：

- `Superpowers.Launcher.exe`
- `Stardew Valley.exe`

在 Launcher 配置页里，测试应把该字段理解为：

- `SMAPI 路径输入框`

可见判定时，除了看输入框本身，还应看输入框下方的路径状态文本是否与当前机器路径一致。

## 6. 默认路径重置

开始做玩家可见测试前，先确认你不是在“脏测试机”状态：

1. 当前部署目录里的 `config.json` 不能保留人为打开的 implementation-only override。
2. `AllowImplementationOnlyManualEntry` 必须回到默认玩家路径。
3. 本轮默认主测试中，不应先按 `F10`。
4. 若你上一轮做过 implementation-only 受控检查，本轮必须先清理配置并重新启动游戏。

当前推荐的最小 reset 做法：

1. 打开：
   - `D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\Superpowers.Stardew.Mod\config.json`
2. 确认或改回：
   - `"AllowImplementationOnlyManualEntry": false`
3. 若文件里还有与 implementation-only 受控检查相关的显式开启项，也一并改回默认玩家路径
4. 保存文件
5. 重新启动 SMAPI 和游戏

如果你没先做这一步，后面的可见结果会被污染。

## 7. 前置条件

这些步骤是“开始前检查”，不是主测试项。

若任一前置条件失败：

- 当前轮主手动可视测试应停止继续推进
- 尚未执行的可见步骤统一记为 `PENDING EVIDENCE`
- 不要把这些未执行步骤误记成 `FAIL`

### 7.1 Build / Artifact 前置条件

运行：

```powershell
dotnet build src/Superpowers.sln
```

预期：

- 通过

证明深度：

- `startup only`

### 7.2 Publish / Sync 前置条件

运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-launcher.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-runtime-local.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-cloud-control.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-stardew-mod.ps1 -ProjectPath .\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj -OutputDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod
powershell -ExecutionPolicy Bypass -File .\scripts\dev\sync-stardew-mod.ps1 -RequireManifest -SourceDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod -TargetDir 'D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\Superpowers.Stardew.Mod'
```

预期：

- 通过
- 产物存在
- Mod 被镜像到目标 `Mods\Superpowers.Stardew.Mod`

证明深度：

- `startup only`

### 7.3 Runtime / Cloud 前置条件

运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-runtime-local.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-cloud-control.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\dev\check-http-health.ps1 -Url http://127.0.0.1:5051/healthz
powershell -ExecutionPolicy Bypass -File .\scripts\dev\check-http-health.ps1 -Url http://127.0.0.1:7061/healthz
```

预期：

- Runtime local 通过
- Cloud control 通过
- 两个 `/healthz` 返回 `200`

证明深度：

- `health only`

运行方式：

- `run-runtime-local.ps1` 与 `run-cloud-control.ps1` 会启动长驻进程
- 请在两个单独 PowerShell 窗口运行它们
- health check 通过后，不要关闭这两个窗口
- 在完成 8.1 到 8.4 全部可视检查之前，都保持这两个进程持续运行

### 7.4 Hosted Narrative 前置条件

运行：

```powershell
dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~HostedNarrativePathTests"
```

预期：

- 通过

证明深度：

- `health only`

### 7.5 SMAPI / Mod 加载前置条件

运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-stardew-smapi.ps1 -SmapiPath 'D:\Stardew Valley\Stardew Valley.v1.6.15\StardewModdingAPI.exe' -LogPath .\artifacts\logs\smapi-latest.log -TimeoutSec 120 -KillOnTimeout
```

预期：

- 检测到 `SUPERPOWERS_STARDew_VISIBLE_SHELL_READY`

证明深度：

- `mod loaded only`

注意：

- 这一步**不等于**你已经进入真实存档。
- 这一步**不等于**你已经看到 F8 / F9 目标 surface。

### 7.6 真实存档 / NPC 前置条件

实际做 Stardew 可见检查前，必须人工确认：

- 已进入真实存档
- 当前玩家已经站在游戏内场景中
- 当前 NPC 对话框已经打开
- 在按 `F8` / `F9` 的那个时刻，该 NPC 仍是当前有效 speaker

更直白地说：

- 不是“站在 NPC 旁边”就算通过
- 不是“刚刚看见过 NPC”就算通过
- 必须在按 `F8` / `F9` 的那个时刻，当前 NPC 对话窗口正开着并处于有效对话目标状态

如果这一步没满足：

- `F8`
- `F9`

只会 fail-close 提示：

- `Select an NPC in-game before opening F8. No runtime request was sent.`
- `Select an NPC in-game before opening F9. No runtime request was sent.`

证明深度：

- `real save loaded`

失败处理：

- 这类 fail-close 结果是**可见失败 UI**
- 可以截图
- 应记录为 `FAIL`
- 但根因应注明为“前置条件未满足：当前没有有效 NPC speaker”

## 8. 可见检查

以下才是当前 `M1` 主手动可视测试项。

### 8.1 Launcher 首页 Surface

进入路径：

- 启动 `artifacts/launcher/Superpowers.Launcher.exe`

预期可见 UI：

- 窗口标题 `Superpowers Launcher`
- 首页 surface
- `星露谷物语` 卡片
- 主 CTA
- `查看配置`

必需交互：

- 点击 `查看配置`

通过标准：

- 首页真实可见
- 《星露谷》卡片真实可见
- 点击 `查看配置` 后成功进入配置页
- 有截图或等价可视证据

证明深度：

- `surface shell visible`

### 8.2 Launcher Stardew 配置 Surface

进入路径：

- 从 Launcher 首页点击 `查看配置`

预期可见 UI：

- `星露谷物语配置`
- `开始前检查`
- `启动器路径`
- 运行状态 / 问题状态 / 恢复状态
- 主 CTA

必需交互：

- 通过首页按钮实际进入此页面

通过标准：

- `开始前检查` 区块真实可见
- 路径输入框真实可见
- 状态区块真实可见
- 有截图或等价可视证据

证明深度：

- `surface shell visible`

### 8.3 Stardew F8：AiDialogueMenu Shell

进入路径：

- 进入真实存档
- 确认当前 NPC 已被选中
- 按 `F8`

预期可见 UI：

- 标题 `Superpowers AI Dialogue`
- 若 Runtime 尚未返回，可见 loading / checking 文案
- 若 Runtime 返回失败，可见失败文案或恢复文案
- 若 Runtime 返回成功，可见：
  - `State: Ready`
  - `Replay:`
  - `Canonical:`
  - `Recovery:`
  - `Actions: Reply / Retry / Close`

必需交互：

- 按 `F8` 打开 shell
- 在 shell 打开后，按一次 `Esc` 关闭它

最小交互证明：

- `F8` 打开 shell
- `Esc` 关闭 shell

通过标准：

- `F8` 后真实弹出对话 shell
- shell 中能看到标题和状态文本
- 能完成一次 `打开 -> 关闭` 的真实交互
- 不是只在日志中出现
- 有截图或等价可视证据

证明深度：

- `surface shell visible`

当前限制：

- 当前只验证 shell surface 和状态文本
- 当前不把它记成 rich-playable 对话闭环

### 8.4 Stardew F9：NpcInfoPanelMenu Shell

进入路径：

- 进入真实存档
- 确认当前 NPC 已被选中
- 按 `F9`

预期可见 UI：

- 标题 `Superpowers NPC Panel [<npcId>]`
- 可见：
  - `State:`
  - `Selected tab:`
  - `Tab state:`
  - `Profile:`
  - `Relation:`
- 默认情况下当前 shell 主要显示 Memory tab 的状态或文本

必需交互：

- 按 `F9` 打开 shell
- 在 shell 打开后，按一次 `Esc` 关闭它
- 当前不要把 tab 切换当作必须交互，因为当前 shell 没有真实 tab 点击路径

最小交互证明：

- `F9` 打开 shell
- `Esc` 关闭 shell

通过标准：

- `F9` 后真实弹出 NPC 面板 shell
- shell 中能看到标题和状态文本
- 能完成一次 `打开 -> 关闭` 的真实交互
- 有截图或等价可视证据

证明深度：

- `surface shell visible`

当前限制：

- 当前 shell 不提供真实手动 tab 切换交互
- 当前测试只能验证默认显示出来的 Memory-shell 快照与状态文本
- 当前不把它记成 rich-playable memory / thought / item 面板闭环

## 9. 非阻塞 / 排除路径

### 9.1 Implementation-Only 受控路径

以下路径当前不属于 `M1 core` 主手动可视测试项：

- `F6` -> `PhoneDirectMessageMenu`
- `F7` -> `PhoneActiveGroupChatMenu`
- `F10` -> implementation-only visibility toggle
- `F11` -> `OnsiteGroupChatOverlay`

处理规则：

- 默认记为 `OUT OF SCOPE`
- 若默认玩家路径意外暴露这些 surface，记为 `FAIL`

### 9.2 当前非玩家可见项

以下内容当前不能作为主手动可视测试项：

- 当前没有稳定 in-host visible UI 的 `social transaction / commitment`
- 仅存在于 runtime / hook / carrier / log / automated test 中的内部语义

处理规则：

- 记为 `NOT PLAYER_VISIBLE`
- 可在 supporting evidence 中保留引用
- 不得写成当前玩家可见 `PASS`

### 9.3 M2+ 附录

以下能力当前记为：

- `OUT OF SCOPE`

包括：

- `information_propagation`
- `active_world`

## 10. 证据目标

执行本计划时，当前主要回链以下 evidence：

- `docs/superpowers/governance/evidence/launcher-player-visible-check.md`
- `docs/superpowers/governance/evidence/stardew-player-visible-check.md`
- `docs/superpowers/governance/evidence/stardew-m1-core-hand-check.md`
- `docs/superpowers/governance/evidence/stardew-implementation-only-channel-hand-check.md`
- `docs/superpowers/governance/evidence-review-index.md`

注意：

- Launcher 当前可视化证据仍绑定旧 revision，只能作为当前 UI 形态参考
- Stardew 当前可视化证据仍带有历史 controlled-run 痕迹，不能再被解释成当前 default player path

本轮测试对 evidence 的使用规则：

- 需要更新 / 需要新增本轮记录：
  - `launcher-player-visible-check.md`
  - `stardew-m1-core-hand-check.md`
  - `evidence-review-index.md`
- 只作历史参考，不应直接复用为本轮 default-path 结果：
  - `stardew-player-visible-check.md`
- 只作 implementation-only 参考，不属于本轮主可视测试记录：
  - `stardew-implementation-only-channel-hand-check.md`

不要在本轮里做的事情：

- 不要把 `stardew-player-visible-check.md` 直接改写成本轮 default-path 证明
- 不要把 implementation-only channel hand check 当成本轮主验收结果

## 11. 当前真实结论

如果只按“你现在真的能看到什么”来讲，当前 `M1` 手动可视测试的真实结论是：

- 你能看到 Launcher 首页
- 你能看到 Launcher 《星露谷》配置页
- 你能在真实存档里通过 `F8` 打开 `AiDialogueMenu` shell
- 你能在真实存档里通过 `F9` 打开 `NpcInfoPanelMenu` shell

你现在**还不能**把下列内容写成当前已完成的玩家可见 rich-playable 手动验证：

- rich-playable dialogue 闭环
- rich-playable memory / thought 闭环
- rich-playable item / social transaction 闭环
- implementation-only channel windows 的默认玩家路径可见证明
