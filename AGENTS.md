# Hermes-Desktop Agent 指南

本文件作用于整个 `D:\Projects\Hermes-Desktop` 仓库。更深层目录里的 `AGENTS.md` 优先级更高；尤其是修改 `Desktop/HermesDesktop/**` 时，必须同时遵守 `Desktop/HermesDesktop/AGENTS.md`。

## 沟通与执行

- 默认用中文向用户汇报进展、风险和结果；代码标识符、命令、路径、API 名称保持原文。
- 在明确、低风险、可逆的任务上直接执行到完成，不把普通检查、构建、测试交回给用户。
- 工作区可能已有用户改动；不要回退、覆盖或清理自己没造成的变更。开始改动前先看 `git status --short --branch`。
- 搜索文件和符号优先用 `rg` / `rg --files`。
- 手工改文件使用 `apply_patch`；不要用 shell 拼接重写文件。

## 仓库结构

- `HermesDesktop.sln` / `HermesDesktop.slnx`：主解决方案。
- `Desktop/HermesDesktop/`：WinUI 3 / Windows App SDK 桌面壳，当前项目文件为 `HermesDesktop.csproj`。
- `Desktop/HermesDesktop.Tests/`：面向 `src/Hermes.Core.csproj` 的 MSTest 测试。
- `Desktop/HermesDesktop.Package/`：桌面打包项目。
- `src/`：Hermes 核心库与 CLI 入口，包含 LLM、memory、skills、mcp、runtime、dreamer、game/stardew 等模块。
- `Mods/StardewHermesBridge/`：星露谷 SMAPI bridge。
- `Mods/StardewHermesBridge.Tests/`：bridge 相关测试。
- `tools/Hermes.SmokeProbe/`：烟测工具。
- `scripts/`：发布、部署、临时目录与证书脚本。
- `external/`、`参考项目/`、`其他资料/`：参考资料和上游/样例代码。除非任务明确要求，默认只读。

## 当前技术事实

这些事实来自初始化时的项目文件读取；后续若有差异，以 `.csproj` 为准。

- 桌面应用：`Desktop/HermesDesktop/HermesDesktop.csproj`
  - `TargetFramework`: `net10.0-windows10.0.26100.0`
  - `RootNamespace`: `HermesDesktop`
  - `Platforms`: `x64`
  - `Microsoft.WindowsAppSDK`: `1.7.250310001`
  - `WindowsPackageType`: `None`，常规开发走 unpackaged `dotnet run`
- 核心库：`src/Hermes.Core.csproj`
  - `TargetFramework`: `net10.0`
  - 主要依赖：`JsonSchema.Net`、`Microsoft.Data.Sqlite`、`Cronos`、`Microsoft.Extensions.*`
- CLI：`src/Hermes.Agent.csproj`
  - `TargetFramework`: `net10.0`
  - 依赖 `Hermes.Core.csproj`
- 桌面测试：`Desktop/HermesDesktop.Tests/HermesDesktop.Tests.csproj`
  - `TargetFramework`: `net10.0`
  - MSTest + Moq + coverlet

## 修改前检查

- 先读用户目标，再定位既有实现，避免重复造轮子。
- 对 `Desktop/HermesDesktop/**` 做任何代码改动前，打开并遵守：
  - `Desktop/HermesDesktop/AGENTS.md`
  - 它要求的 `.github/instructions/*.instructions.md`
- UI/XAML 改动要检查可访问性、性能、字符串资源和 WinUI 模式。
- 涉及用户输入、HTTP、权限、密钥、文件系统访问时，先检查安全规则，不写硬编码密钥。
- 不新增依赖，除非用户明确要求或已有项目模式强烈支持；新增前说明理由和替代方案。

## 构建与测试

优先运行与改动范围匹配的最小验证，再按风险扩大范围。

- 查看解决方案项目：
  ```powershell
  dotnet sln HermesDesktop.sln list
  ```
- 核心/服务层测试：
  ```powershell
  dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
  ```
- Stardew bridge 测试：
  ```powershell
  dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
  ```
- 桌面应用构建、运行和 MSIX 相关流程，以 `Desktop/HermesDesktop/AGENTS.md` 和实际 `.csproj` 为准。不要凭记忆硬编码 TFM、WinAppSDK 版本或包路径。
- 仓库根目录的快捷启动脚本：
  ```powershell
  .\run-desktop.ps1
  .\run-desktop.ps1 -Rebuild
  ```

## 代码风格

- 保持改动小、可审查、可回退。
- 优先删除无用代码、复用现有 helper 和服务边界，再考虑新增抽象。
- 新增公共方法/类时补测试；窄改动配窄测试，跨模块行为配更广验证。
- 保持 nullable 语义，不用 `!` 掩盖真实空值问题。
- 避免把 UI 逻辑、持久化、网络调用和领域状态混在一个方法里；跟随现有 Services / Models / Views 分层。

## 提交信息

如果用户要求提交，commit message 使用 Lore 风格：第一行写“为什么改”，正文说明约束和取舍，并用 git trailer 记录有价值的信息，例如：

```text
Prevent silent session loss during replay restore

The replay loader now rejects partial state before it can overwrite the active session.

Constraint: Existing session files may be partially written after a crash
Rejected: Ignore malformed replay files | hides recoverable data issues
Confidence: high
Scope-risk: narrow
Tested: dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
Not-tested: Full packaged MSIX launch
```

## 完成标准

- 说明改了哪些文件、做了哪些简化或行为修复。
- 报告实际运行过的验证命令和结果。
- 明确剩余风险、未测范围或环境限制。
- 如果验证失败，继续定位和修复；不要在失败状态下声称完成。
