# Hermes Desktop 更新版

<p align="center">
  <img src="docs/logo.png" alt="Hermes Desktop Logo" width="128" />
</p>

一个运行在你桌面上的 **Windows 原生 AI 代理**。与它对话，为它配置工具，让它了解你是谁。基于 WinUI 3 和 .NET 10 构建。

**v2.4.0** &mdash; [下载](https://github.com/RedWoodOG/Hermes-Desktop/releases/latest) | [更新日志](#changelog) | [讨论](https://github.com/RedWoodOG/Hermes-Desktop/discussions/10)

---

## 快速开始

**下载并运行** &mdash; 无需安装程序，无需 SDK，无需安装向导。

1. 从 [Releases](https://github.com/RedWoodOG/Hermes-Desktop/releases/latest) 下载 [`HermesDesktop-portable-x64.zip`](https://github.com/RedWoodOG/Hermes-Desktop/releases/latest)
2. 解压到任意位置
3. 运行 `HermesDesktop.exe`
4. 将你的 API 密钥添加到 `%LOCALAPPDATA%\hermes\config.yaml`

支持 Windows 10 (1809+) 和 Windows 11。便携版构建完全自包含 &mdash; 所有内容都打包在文件夹中。

<details>
<summary>最小化 config.yaml 配置（开始聊天）</summary>

```yaml
model:
  provider: anthropic
  default: claude-sonnet-4-6
  base_url: https://api.anthropic.com
  api_key: sk-ant-your-key-here

# 添加更多提供商以实现运行时切换（可选）
provider_keys:
  anthropic: sk-ant-your-key
  openai: sk-proj-your-key
  ollama_url: http://127.0.0.1:11434/v1
```

首次启动会在 `%LOCALAPPDATA%\hermes` 中创建配置文件、记忆、会话记录和日志。

</details>

### 更新

**便携版（从 Releases 下载的 zip）** &mdash; 你的数据存放在应用程序文件夹之外。

1. 退出 Hermes Desktop（系统托盘 → 退出，或关闭窗口）。
2. 下载最新的 [`HermesDesktop-portable-x64.zip`](https://github.com/RedWoodOG/Hermes-Desktop/releases/latest)。
3. **替换文件夹**（删除旧的解压文件夹，将新 zip 解压到相同路径）**或**解压到新文件夹并从那里运行 `HermesDesktop.exe` &mdash; 无论哪种方式，**不要删除** `%LOCALAPPDATA%\hermes`；你的 `config.yaml`、会话、记忆和维基都会保留在那里。
4. 启动新的 `HermesDesktop.exe`。

目前还没有应用内自动更新器；需要新版本时请查看 [Releases](https://github.com/RedWoodOG/Hermes-Desktop/releases)。

**从 git 构建（开发 / `dotnet run`）** &mdash; 拉取并再次运行：

```powershell
cd Hermes-Desktop
git pull
dotnet run --project Desktop/HermesDesktop/HermesDesktop.csproj -c Debug -p:Platform=x64 --launch-profile "HermesDesktop (Dev)"
```

**MSIX（`run-dev.ps1`）** &mdash; 拉取后重新注册：

```powershell
cd Hermes-Desktop
git pull
powershell -ExecutionPolicy Bypass -File .\Desktop\HermesDesktop\run-dev.ps1
```

---

## 功能特性

Hermes Desktop 是一个**进程内代理运行时**，具有原生 Windows UI &mdash; 不仅仅是一个聊天包装器。代理在本地运行，调用工具，跨会话记住上下文，并且可以代表你联系 Telegram 和 Discord。

| | |
|---|---|
| ![Chat](docs/screenshots/Screenshot%202026-04-12%20180315.png) | ![Agents](docs/screenshots/Screenshot%202026-04-12%20180348.png) |
| ![Soul Editor](docs/screenshots/Screenshot%202026-04-12%20180428.png) | ![Soul Templates](docs/screenshots/Screenshot%202026-04-12%20180445.png) |
| ![Skills](docs/screenshots/Screenshot%202026-04-12%20180517.png) | ![Memory](docs/screenshots/Screenshot%202026-04-12%20180529.png) |
| ![Integrations](docs/screenshots/Screenshot%202026-04-12%20180602.png) | ![Settings](docs/screenshots/Screenshot%202026-04-12%20180629.png) |

### 代理运行时

- **27+ 个工具** &mdash; 文件操作、Shell、网页抓取/搜索、代码沙箱、浏览器自动化、视觉识别、TTS 等
- **并行执行** &mdash; 8个工作线程的信号量用于只读工具，变更操作带有权限门控的串行执行
- **运行时模型切换** &mdash; 无需重启即可在 Claude、GPT、Ollama、Qwen、DeepSeek 等之间切换
- **子代理派生** &mdash; 5种配置模板用于委派和并行工作
- **94个技能** &mdash; 覆盖28个类别（代码审查、TDD、GitHub工作流、MLOps、研究、创意等）

### 记忆与身份

- **灵魂系统** &mdash; 持久化人格（SOUL.md）、用户画像（USER.md）、项目规则（AGENTS.md）、错误日志、习惯日志
- **12种灵魂模板** &mdash; 默认、创意、教师、研究者、结对编程、DevOps、安全等
- **Wiki 知识库** &mdash; 带 SQLite FTS5 全文搜索的 Markdown 文件，Obsidian 兼容，崩溃安全写入
- **编译记忆栈** &mdash; Wiki 内容自动注入代理上下文，可在 `config.yaml` 中配置
- **6层上下文运行时** &mdash; 灵魂上下文、系统提示、会话状态、检索到的知识、最近对话轮次、当前消息

### 生产级加固

基于 168+ 上游 PR 和 46+ 生产事故的经验教训构建：

- **压缩冷却**（600秒） &mdash; 防止无限 token 消耗循环
- **提供商故障转移** &mdash; 自动 5 分钟恢复
- **凭证池轮换** &mdash; 在 401/429 错误时轮换
- **原子写入**（WriteThrough + FlushAsync） &mdash; 崩溃安全
- **密钥扫描** &mdash; 所有工具输出都进行扫描
- **确定性工具调用 ID** &mdash; 提示缓存效率

### 桌面应用程序

八个页面：**Dashboard**（使用洞察、KPI、平台徽章）、**Chat**（工具调用、推理显示、模型切换器、侧边面板）、**Agent**（身份编辑器、灵魂浏览器）、**Skills**（带分类的可搜索库）、**Memory**（浏览器 + 项目规则编辑器）、**Buddy**（带 ASCII 艺术的伴侣）、**Integrations**（Telegram、Discord 等）、**Settings**（模型、内存、显示、执行、路径）。

### 消息传递

**Telegram 和 Discord 的原生 C# 适配器** &mdash; 无需 Python CLI。**Slack、WhatsApp、Matrix 和 Webhook** 通过 Integrations 中的同一 `config.yaml` 配置；当你希望这些机器人活跃时使用可选的 **Python 网关**（不只是为了节省 token）。

---

## 从源代码构建

适用于贡献者或任何想要修改代码的人。

**要求：** Windows 10+、[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)、[Windows App SDK 1.7](https://learn.microsoft.com/windows/apps/windows-app-sdk/)

### 开发模式（推荐）

```powershell
git clone https://github.com/RedWoodOG/Hermes-Desktop.git
cd Hermes-Desktop
dotnet run --project Desktop/HermesDesktop/HermesDesktop.csproj -c Debug -p:Platform=x64 --launch-profile "HermesDesktop (Dev)"
```

未打包运行，无需 MSIX 注册。开发配置文件启用 `HERMES_DESKTOP_SHOW_LOCAL_DETAILS`，因此路径和端点在 UI 中可见。在 Visual Studio 或 Cursor 中，选择 **HermesDesktop (Dev)** 启动配置文件并按 F5。

### 打包开发循环

```powershell
powershell -ExecutionPolicy Bypass -File .\Desktop\HermesDesktop\run-dev.ps1
```

构建、注册 MSIX 包并启动。使用 `-ShowLocalDetails` 在 UI 中显示路径。

### 构建便携版 zip

```powershell
.\scripts\publish-portable.ps1 -Zip
```

生成 `Desktop\HermesDesktop\bin\HermesDesktop-portable-x64.zip` &mdash; 自包含，可立即分发。对于 ARM64：添加 `-Platform ARM64`。

<details>
<summary>干净卸载、手动构建、故障排除</summary>

**从 git 更新** 在上面 [更新](#更新) 中已介绍（便携版 zip 与 `dotnet run` 与 `run-dev.ps1`）。

**干净卸载（MSIX）：**

```powershell
Get-AppxPackage *EDC29F63* | Remove-AppxPackage
Remove-Item -Recurse -Force Desktop\HermesDesktop\bin, Desktop\HermesDesktop\obj, src\bin, src\obj -ErrorAction SilentlyContinue
```

还要删除用户数据：`Remove-Item -Recurse -Force "$env:LOCALAPPDATA\hermes"`

**手动构建（如果脚本不起作用）：**

```powershell
dotnet build Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
cd Desktop\HermesDesktop\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64
Add-AppxPackage -Register AppxManifest.xml
Start-Process "shell:AppsFolder\EDC29F63-281C-4D34-8723-155C8122DEA2_1z32rh13vfry6!App"
```

**故障排除：**

- 应用窗口不出现？删除旧包（`Get-AppxPackage *EDC29F63* | Remove-AppxPackage`），清理 `bin/` 和 `obj/`，重新构建。
- 检查崩溃日志：`%LOCALAPPDATA%\hermes\hermes-cs\logs\desktop-startup.log`
- 检查 Windows 崩溃报告：`C:\ProgramData\Microsoft\Windows\WER\ReportArchive`
- 关闭叠加软件（MSI Afterburner、RTSS）&mdash; 这些可能干扰 WinUI 启动。
- 验证 SDK：`dotnet --version` 应显示 `10.x.x`
- `BriefService` 或 `DashboardPage` 构建错误？参见 [issue #25](https://github.com/RedWoodOG/Hermes-Desktop/issues/25)。
- 使用 `-p:Platform=x64`，而不是 `AMD64` &mdash; 参见 `Desktop/HermesDesktop/AGENTS.md` 了解详情。

**MSIX 签名：** 本地证书材料（`Desktop\HermesDesktop\packaging\dev-msix.pfx`）必须保留在 git 之外。使用 `scripts\new-msix-dev-cert.ps1` 生成开发证书。

</details>

---

## 项目结构

```
Hermes.CS/
├── src/                         # 核心代理库 (Hermes.Core)
│   ├── Core/                    #   代理循环、模型、工具接口
│   ├── Tools/                   #   27+ 工具实现
│   ├── LLM/                     #   提供商抽象、模型切换
│   ├── soul/                    #   身份系统、模板、配置
│   ├── wiki/                    #   WikiManager、FTS5 搜索
│   ├── Context/                 #   提示构建器、token 预算
│   ├── dreamer/                 #   后台自由联想工作器
│   ├── gateway/                 #   Telegram、Discord 适配器
│   └── ...                      #   memory、skills、security、plugins 等
├── Desktop/HermesDesktop/       # WinUI 3 桌面应用程序
│   ├── Views/                   #   8 个页面 + 侧边面板
│   ├── Services/                #   聊天桥接、环境、诊断
│   └── Strings/                 #   本地化 (en-us、zh-cn)
├── skills/                      # 94 个技能定义
├── scripts/                     # 构建、发布、安装脚本
└── HermesDesktop.slnx
```

## 技术栈

**.NET 10** / C# 13 &bull; **WinUI 3**（Windows App SDK 1.7、Mica 背景）&bull; **SQLite** FTS5 &bull; **Playwright** &bull; **System.Text.Json**

## 更新日志

| 版本 | 日期 | 亮点 |
|------|------|------|
| **v2.4.0** | 2026-04-19 | **Buddy：** 持久化到 `buddy/buddy.json`，物种孵化 UI，LLM-off 回退灵魂，对齐面板身份。**集成：** 原生 Telegram/Discord 适配器状态修复，更清晰的 Python 消息网关说明。**测试：** `BuddyServiceTests`。程序集/MSIX 清单 **2.4.0.0**。 |
| **v2.3.1** | 2026-04-13 | 修复 v2.3.0 源码 zip 中 `DreamerStatusSnapshot.LastLocalDigestHint` 编译错误，修复便携版启动时 `ReplayPanel` 的 `XamlParseException`（禁用 `PublishTrimmed`），添加 `ReplayPanel` 构造函数诊断捕获，刷新 readme 截图 |
| v2.3.0 | 2026-04-12 | 便携版发布（自包含 zip，无需 MSIX），编译记忆栈，Wiki 工具，开发启动配置文件，`publish-portable.ps1` |
| v2.2.1 | 2026-04-10 | 修复全新克隆的启动崩溃，安全文件操作，一键安装程序 |
| v2.2.0 | 2026-04-10 | 设置中的用户资料部分 |
| v2.1.0 | 2026-04-10 | 原生 C# 网关 &mdash; 无需 Python CLI 的 Telegram 和 Discord |
| v2.0.0 | 2026-04-09 | 运行时模型切换（Claude/OpenAI/Ollama/Qwen 会话中切换） |
| v1.9.0 | 2026-04-09 | 带 SQLite FTS5 搜索的 Wiki 知识库 |
| v1.8.0 | 2026-04-09 | 生产加固：冷却、故障转移、原子写入、密钥扫描 |
| v1.7.0 | 2026-04-09 | Anthropic 工具调用 |
| v1.5.0 | 2026-04-08 | 并行工具执行（8个工作线程） |

<details>
<summary>早期版本</summary>

| 版本 | 日期 | 亮点 |
|------|------|------|
| v2.1.1 | 2026-04-10 | 修复技能发现、模型下拉框、记忆路径 |
| v2.0.1 | 2026-04-09 | 修复深色主题、首次运行技能复制、网关通知 |
| v1.9.1 | 2026-04-09 | 代理工具循环测试（207项通过）、聊天用户体验 |
| v1.6.0 | 2026-04-09 | 执行后端、插件、分析仪表板 |
| v1.4.0 | 2026-04-08 | +7 个新工具（共21个） |
| v1.3.0 | 2026-04-08 | 通过完整代理管道路由聊天 |
| v1.2.0 | 2026-04-08 | 设置页面大修 |
| v1.1.0 | 2026-04-08 | 技能页面重新设计 |
</details>

## 致谢

基于 [NousResearch Hermes Agent](https://github.com/NousResearch/hermes-agent) 架构构建。本项目旨在表达对 NousResearch 团队的赞赏 &mdash; 请支持他们并使用他们创造的产品。

## 许可证

MIT
