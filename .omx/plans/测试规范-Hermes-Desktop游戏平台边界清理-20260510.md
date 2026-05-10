# 测试规范：Hermes-Desktop 游戏平台边界清理

## 1. 测试目标

验证本轮边界清理只删除 `Buddy` 和旧 `AutoDreamService`，不影响核心 NPC runtime、Wiki、Coordinator / AgentService、当前 Dreamer / Insights，也不把 `MixtureOfAgentsTool` 暴露为默认工具。

## 2. 必跑命令

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

建议补充：

```powershell
dotnet build .\src\Hermes.Core.csproj -c Debug
```

## 3. 运行时源码扫描

Buddy 扫描：

```powershell
rg -n "BuddyService|BuddyPage|BuddyPanel|BuddyNavItem|Hermes\\.Agent\\.Buddy" .\src .\Desktop\HermesDesktop .\Desktop\HermesDesktop.Tests
```

通过标准：

- `src`、`Desktop/HermesDesktop`、`Desktop/HermesDesktop.Tests` 中不应再有 Buddy 运行时引用。
- 历史文档不参与此条运行时验收。

AutoDream / Mixture 扫描：

```powershell
rg -n "AutoDreamService|MixtureOfAgentsTool|mixture_of_agents" .\src .\Desktop\HermesDesktop
```

通过标准：

- `AutoDreamService` 不应出现在 `src` 或 `Desktop/HermesDesktop` 当前运行时路径。
- `MixtureOfAgentsTool` 可以在 `src/Tools/MixtureOfAgentsTool.cs` 命中。
- `mixture_of_agents` 可以在该实现文件内命中。
- 不应在 `AgentCapabilityAssembler.BuiltInToolNames`、`RegisterAllTools`、`Desktop/HermesDesktop/App.xaml.cs` DI/工具注册路径中命中为已注册能力。

## 4. 文档扫描

```powershell
rg -n "Buddy|BuddyService|BuddyPage|BuddyPanel|AutoDreamService|AutoDream" .\AGENTS.md .\Desktop\HermesDesktop\AGENTS.md .\Desktop\HermesDesktop\docs .\docs .\wiki .\.omx .\external .\参考项目
```

通过标准：

- `AGENTS.md` 不得把 Buddy 写作当前组合根注册项、当前能力或当前导航项。
- `Desktop/HermesDesktop/AGENTS.md` 不得把 Buddy 写作当前主导航项或当前 UI 大图景。
- `Desktop/HermesDesktop/docs/LOCALIZATION-RECON.md` 不得把 `BuddyPage` / `BuddyPanel` 写作当前待本地化对象。
- `docs`、`wiki`、`.omx`、`external`、`参考项目` 中历史引用允许存在，但必须解释为历史资料或已移除实现，不作为当前产品面。

## 5. MemoryParityTests 新守卫

`Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs` 应包含当前 Dreamer 主线守卫。

测试语义必须满足：

- `src/dream/AutoDreamService.cs` 文件不存在。
- `Desktop/HermesDesktop/App.xaml.cs` 不包含 `AutoDreamService`。
- `Desktop/HermesDesktop/App.xaml.cs` 包含 `StartDreamerBackground`。
- 当前 Dreamer 主线文件包含 `DreamerService`。
- 不断言全仓无 `AutoDreamService`。

## 6. 禁止范围复核

运行：

```powershell
git diff --name-only
```

不得出现非预期行为改动：

- `src/runtime/**`
- `src/games/stardew/**`
- `src/wiki/**`
- `src/agents/**`
- `src/coordinator/**`
- `src/dreamer/**`
- `src/analytics/InsightsService.cs`
- `src/Tools/MixtureOfAgentsTool.cs`
- `src/runtime/AgentCapabilityAssembler.cs` 的工具注册链

如果出现以上路径，必须说明是否只是文档/测试扫描影响；否则应视为越界。

## 7. 手测建议

可选运行：

```powershell
.\run-desktop.ps1 -Rebuild
```

手测重点：

- Desktop 可启动。
- 主导航不再出现 Buddy。
- `Dashboard`、`Chat`、`Agent`、`Skills`、`Memory`、`Settings` 可打开。
- Dashboard 仍显示 Dreamer 与 NPC runtime 状态。
- AgentPage 中 Stardew/NPC runtime 工作台仍可打开。
- 日志中没有 Buddy 页面 XAML 加载异常。
- 日志中没有旧 `AutoDreamService` 启动痕迹。

## 8. 失败处理

- 构建失败优先查 Buddy 旧引用、XAML 页面生成引用、资源键残留。
- 测试失败优先查 `BuddyServiceTests`、`PanelHelperLogicTests`、`MemoryParityTests`。
- 如果扫描发现 `MixtureOfAgentsTool` 被注册，停止执行并重新评估，不要顺手注册或删除。
- 如果 `src/runtime/**` 或 `src/games/stardew/**` 出现改动，停止并复核是否越界。
