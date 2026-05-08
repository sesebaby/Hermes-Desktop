# Hermes-Desktop 复杂度精简计划（详细审查版）

**生成时间**: 2026-05-08  
**目标**: 降低项目复杂度，聚焦游戏产品核心能力  
**审查要求**: 每个删除项附完整证据链和判断理由

---

## 一、背景

当前 Hermes-Desktop 主线是 **Stardew Valley / 多 NPC 村庄模式**，但仓库中仍保留大量通用 AI 平台能力。本计划基于对组合根、导航入口、服务注册链和 NPC runtime 依赖的全链路扫描，识别可删除的非核心模块。

**扫描范围**:
- `Desktop/HermesDesktop/App.xaml.cs` (组合根，所有服务注册)
- `Desktop/HermesDesktop/MainWindow.xaml` (主导航入口)
- `src/runtime/` (NPC runtime 核心)
- `src/games/stardew/` (Stardew 游戏链路)
- `AgentCapabilityAssembler.cs` (工具注册)

---

## 二、立即可删除（零依赖，游戏链路无关）

### 1. Buddy 系统

#### 删除位置
- `src/buddy/` (完整目录，包含 `Buddy.cs`、`BuddyService.cs`、`BuddyRenderer.cs` 等)
- `Desktop/HermesDesktop/Views/BuddyPage.xaml` + `.xaml.cs`
- `Desktop/HermesDesktop/Views/Panels/BuddyPanel.xaml` + `.xaml.cs`
- `App.xaml.cs:548-552` (DI 注册)
- `MainWindow.xaml:99-106` (主导航项)

#### 证据链

**1. 功能定位**:
- `src/buddy/Buddy.cs:1-50` - Buddy 是"deterministic gacha + AI soul 生成 + ASCII renderer"
- 用途：生成伙伴形象和 AI 人格，纯 UI 趣味功能

**2. 注册位置**:
```csharp
// App.xaml.cs:548-552
services.AddSingleton<BuddyService>();
```

**3. UI 入口**:
```xml
<!-- MainWindow.xaml:99-106 -->
<NavigationViewItem x:Uid="BuddyNavItem" Tag="buddy">
    <NavigationViewItem.Icon>
        <FontIcon Glyph="&#xE77B;"/>
    </NavigationViewItem.Icon>
</NavigationViewItem>
```

**4. 游戏链路依赖检查**:
```bash
# 搜索 NPC runtime 是否引用 BuddyService
grep -r "BuddyService\|IBuddy" src/runtime/ src/games/stardew/
# 结果：零引用
```

**5. 后台服务检查**:
- `App.xaml.cs` 中无 `BuddyService.Start*()` 调用
- 仅在用户打开 `BuddyPage` 时按需加载

#### 删除理由
- **与游戏无关**: Buddy 是通用 AI 平台的趣味功能，不是 Stardew/NPC 核心能力
- **零依赖**: NPC runtime、自主循环、私聊、工具面均不依赖 Buddy
- **独立模块**: 删除后不影响任何游戏链路

#### 影响
- 删除约 800 行代码
- 移除 1 个主导航项
- 减少 1 个 DI 注册

#### 风险
**无**

---

### 2. Wiki 系统

#### 删除位置
- `src/wiki/` (9 个文件)
  - `WikiManager.cs`
  - `LocalWikiStorage.cs`
  - `WikiSearchIndex.cs`
  - `WikiConfig.cs`
  - `WikiEntry.cs`
  - `IWikiStorage.cs`
  - `IWikiSearchIndex.cs`
  - 等
- `App.xaml.cs:555-566` (DI 注册)

#### 证据链

**1. 功能定位**:
- Wiki 是本地知识库系统，用于存储和搜索 Markdown 文档
- 设计用途：agent 可查询项目文档、API 参考等

**2. 注册位置**:
```csharp
// App.xaml.cs:555-566
services.AddSingleton<WikiConfig>(sp => new WikiConfig
{
    WikiDirectory = Path.Combine(hermesHome, "wiki")
});
services.AddSingleton<IWikiStorage, LocalWikiStorage>();
services.AddSingleton<IWikiSearchIndex, WikiSearchIndex>();
services.AddSingleton<WikiManager>();
```

**3. UI 入口检查**:
```bash
# 搜索 MainWindow.xaml 是否有 Wiki 导航项
grep -i "wiki" Desktop/HermesDesktop/MainWindow.xaml
# 结果：无匹配
```
- `MainWindow.xaml` 当前导航项：Dashboard、Chat、Agent、Skills、Memory、Buddy、Settings
- **没有 Wiki 导航项**

**4. 游戏链路依赖检查**:
```bash
# 搜索 NPC runtime 是否引用 WikiManager
grep -r "WikiManager\|IWikiStorage\|IWikiSearchIndex" src/runtime/ src/games/stardew/ Desktop/HermesDesktop/Views/
# 结果：零引用
```

**5. 工具注册检查**:
- `AgentCapabilityAssembler.cs` 中无 `wiki_*` 工具注册
- agent 无法通过工具调用 Wiki

#### 删除理由
- **已注册但未接线**: Wiki 服务已注册到 DI，但无任何 UI 入口或工具调用
- **孤立模块**: 既无用户可见功能，也无 agent 可用工具
- **与游戏无关**: Wiki 是通用文档管理能力，不是 Stardew/NPC 核心需求

#### 影响
- 删除约 600 行代码
- 减少 4 个 DI 注册
- 清理 `%LOCALAPPDATA%\hermes\wiki\` 目录（如果存在）

#### 风险
**无**

---

### 3. Coordinator / AgentService 多 worker 编排

#### 删除位置
- `src/coordinator/CoordinatorService.cs` (完整文件)
- `src/agents/AgentService.cs` (完整文件，包含 `TeamManager` 和 `MailboxService`)
- `App.xaml.cs:701-716` (DI 注册)

#### 证据链

**1. 功能定位**:
- `CoordinatorService`: 多 worker 任务分解与编排引擎，支持 brief-driven orchestration
- `AgentService`: 子 agent spawn 能力，支持 worktree/remote isolation
- `TeamManager` / `MailboxService`: agent 间消息系统（作为 `AgentService.cs` 的内部类存在）

**2. 注册位置**:
```csharp
// App.xaml.cs:701-716
services.AddSingleton<AgentService>(sp => new AgentService(
    sp.GetRequiredService<ILogger<AgentService>>(),
    sp.GetRequiredService<IConfiguration>(),
    hermesHome
));

services.AddSingleton<CoordinatorService>(sp => new CoordinatorService(
    sp.GetRequiredService<ILogger<CoordinatorService>>(),
    sp.GetRequiredService<AgentService>(),
    sp.GetRequiredService<TaskManager>(),
    sp.GetRequiredService<IChatClientProvider>()
));
```

**3. UI 入口检查**:
```bash
# 搜索桌面 UI 是否有 Coordinator/Agent 相关页面
grep -r "CoordinatorService\|AgentService" Desktop/HermesDesktop/Views/
# 结果：零引用
```
- `MainWindow.xaml` 无相关导航项
- 无任何 UI 页面调用这些服务

**4. 游戏链路依赖检查**:
```bash
# 搜索 NPC runtime 是否引用
grep -r "CoordinatorService\|AgentService\|TeamManager\|MailboxService" src/runtime/ src/games/stardew/
# 结果：零引用
```

**5. 工具注册检查**:
- `AgentCapabilityAssembler.cs` 中有 `agent` 工具注册
- 但 `agent` 工具的实现在 `src/Tools/AgentTool.cs`，是"模型内���具式子 agent"
- **不依赖** `AgentService` 的 worktree/remote isolation 能力

**6. AGENTS.md 明确**:
> "CoordinatorService 是后端编排引擎；不要写成桌面里已有完整 team mailbox / inbox UI"
> "AgentService 支持 remote isolation，但是否可用依赖本机 ssh/scp 与环境，不要写成默认可用路径"

#### 删除理由
- **已注册但未接线**: 虽然注册到 DI，但无任何 UI 入口或实际调用
- **与游戏无关**: 多 worker 编排是通用 AI 平台高级能力，不是 Stardew/NPC 核心需求
- **独立模块**: `agent` 工具不依赖这些服务，删除后不影响游戏链路

#### 影响
- 删除约 900 行代码
- 减少 2 个 DI 注册
- 同时删除 `TeamManager` 和 `MailboxService`（作为 `AgentService.cs` 的一部分）

#### 风险
**无**

---

### 4. AutoDreamService（旧 Dream 实现）

#### 删除位置
- `src/dream/AutoDreamService.cs` (514 行)

#### 证据链

**1. 功能定位**:
- 旧的 transcript consolidation 系统
- 10 分钟周期扫描 session，提取重要信息并写入 memory 文件
- 设计用途：自动维护 agent 长期记忆

**2. 当前主链对比**:
```csharp
// App.xaml.cs:783-859 - 当前启动的是 DreamerService
private async Task StartDreamerBackground(...)
{
    var dreamerService = new DreamerService(...);
    await dreamerService.RunForeverAsync(cancellationToken);
}
```
- 当前主链：`DreamerService` + `RssFetcher` + `SignalScorer` + `BuildSprint`
- **不是** `AutoDreamService`

**3. 注册检查**:
```bash
# 搜索 App.xaml.cs 是否注册 AutoDreamService
grep "AutoDreamService" Desktop/HermesDesktop/App.xaml.cs
# 结果：无匹配
```

**4. 测试证据**:
```csharp
// Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs:988-996
[TestMethod]
public void AutoDreamService_NotRegistered_ByDefault()
{
    // 专门测试确认 AutoDreamService 默认不注册
    var autoDreamService = _serviceProvider.GetService<AutoDreamService>();
    Assert.IsNull(autoDreamService);
}
```

**5. AGENTS.md 明确**:
> "AutoDreamService 虽然代码存在，但**不是当前 Desktop 启动主链路**"
> "src/dream/AutoDreamService.cs 有代码，但当前桌面默认不走它；不要把它误判成现在线上主链路"

**6. 设计文档冲突**:
- `docs/all_9_pillars_complete.md` 声称 `AutoDreamService` 已完成
- 但实际代码和测试证明它未启动

#### 删除理由
- **明确休眠**: 代码完整但从未启动，有测试保证不注册
- **已被替代**: 当前 Dreamer 主链已改为 `DreamerService`
- **避免混淆**: 保留会误导开发者认为有两套 Dream 系统

#### 影响
- 删除约 500 行代码
- 清理一个历史实现路径

#### 风险
**无**（有测试保证不启动）

---

### 5. MixtureOfAgentsTool（未注册工具）

#### 删除位置
- `src/Tools/MixtureOfAgentsTool.cs` (140+ 行)

#### 证据链

**1. 功能定位**:
- 多模型综合工具，同一问题发给多个 LLM，综合答案
- 上游 Python 参考：`external/hermes-agent-main/tools/mixture_of_agents_tool.py`

**2. 工具注册检查**:
```csharp
// AgentCapabilityAssembler.cs:19-32
public static readonly HashSet<string> BuiltInToolNames = new()
{
    "todo",
    "todo_write",
    "schedule_cron",
    "agent",
    "memory",
    "session_search",
    "skills_list",
    "skill_view",
    "skill_manage",
    "skill_invoke",
    "checkpoint"
};
// 不含 "mixture_of_agents"
```

**3. DI 注册检查**:
```bash
# 搜索 App.xaml.cs 是否注册 MixtureOfAgentsTool
grep "MixtureOfAgentsTool" Desktop/HermesDesktop/App.xaml.cs
# 结果：无匹配
```

**4. 游戏链路依赖检查**:
```bash
# 搜索 NPC runtime 是否引用
grep -r "MixtureOfAgentsTool\|mixture_of_agents" src/runtime/ src/games/stardew/
# 结果：零引用
```

**5. AGENTS.md 明确**:
> "mixture_of_agents 在仓库中有独立工具实现时，也先按'存在代码'看待；除非确认进入当前桌面主注册链，否则不要写成默认能力面"

#### 删除理由
- **未注册**: 完整实现但从未注册到工具面
- **需要多 provider**: 需要配置多个 LLM provider，当前产品未暴露此能力
- **与游戏无关**: 多模型综合是通用 AI 能力，不是 Stardew/NPC 核心需求

#### 影响
- 删除约 140 行代码
- 清理一个未接线的工具实现

#### 风险
**无**

---

## 三、第二批（需配合删除）

### 6. Dreamer 背景系统

#### 删除位置
- `src/dreamer/` (10 个文件)
  - `DreamerService.cs` (核心服务)
  - `DreamerRoom.cs` (工作区管理)
  - `BuildSprint.cs` (构建冲刺)
  - `RssFetcher.cs` (RSS 抓取)
  - `SignalScorer.cs` (信号评分)
  - `DreamerStatus.cs` (状态模型)
  - 等
- `App.xaml.cs:676` (`DreamerStatus` 注册)
- `App.xaml.cs:738` (`StartDreamerBackground()` 调用)
- `App.xaml.cs:783-859` (`StartDreamerBackground()` 方法实现)
- `DashboardPage.xaml:68-92` (Dreamer 状态卡片)
- `SettingsPage.xaml.cs:641-694` (Dreamer 配置区)

#### 证据链

**1. 功能定位**:
- Dreamer 是后台智能系统，周期性执行以下任务：
  - 读取 transcript / inbox / RSS
  - 运行 local-model walk（本地模型遍历）
  - Signal scoring（信号评分）
  - 触发 build sprint（构建冲刺）
  - 写本地 digest（摘要）
- 工作区：`%LOCALAPPDATA%\hermes\dreamer\`

**2. 启动位置**:
```csharp
// App.xaml.cs:738
await StartDreamerBackground(serviceProvider, cancellationToken);

// App.xaml.cs:783-859
private async Task StartDreamerBackground(...)
{
    var dreamerService = new DreamerService(...);
    _ = Task.Run(async () =>
    {
        await dreamerService.RunForeverAsync(cancellationToken);
    }, cancellationToken);
}
```

**3. UI 入口**:
- `DashboardPage.xaml:68-92` - Dreamer 状态卡片（Phase、Last Walk、Signal Count）
- `SettingsPage.xaml.cs:641-694` - Dreamer 配置区（启用/禁用、周期设置）

**4. 游戏链路依赖检查**:
```bash
# 搜索 NPC runtime 是否引用 DreamerService
grep -r "DreamerService\|DreamerStatus\|DreamerRoom" src/runtime/ src/games/stardew/
# 结果：零引用
```

**5. InsightsService 依赖**:
```csharp
// DreamerService.cs 调用 InsightsService
_insightsService.RecordDreamerStartupFailure(ex);
```
- Dreamer 是 InsightsService 的主要调用方

**6. 与游戏产品的关系**:
- Dreamer 是"通用 AI 平台背景能力"，设计用途：
  - 自动发现项目中的改进机会
  - 周期性整理 transcript 和 inbox
  - 生成项目健康报告
- **不是** Stardew/NPC 自主循环的一部分
- NPC 自主循环走 `StardewNpcAutonomyBackgroundService`，完全独立

#### 删除理由
- **与游戏无关**: Dreamer 是通用 AI 平台能力，不是 Stardew/NPC 核心需求
- **零依赖**: NPC runtime、自主循环、私聊均不依赖 Dreamer
- **可配置禁用**: 已有 `dreamer.enabled` 配置项，说明设计上就是可选模块

#### 影响
- 删除约 1500 行代码
- 移除 Dashboard 中的 Dreamer 状态卡片
- 移除 Settings 中的 Dreamer 配置区
- 清理 `%LOCALAPPDATA%\hermes\dreamer\` 工作区（如果存在）

#### 风险
**低**（可先配置 `dreamer.enabled=false` 验证无副作用）

---

### 7. InsightsService（分析服务）

#### 删除位置
- `src/analytics/InsightsService.cs`
- `App.xaml.cs:670-672` (DI 注册)
- `DashboardPage.xaml:254-293` (Usage Insights 面板，默认 `Visibility="Collapsed"`)

#### 证据链

**1. 功能定位**:
- 记录 tool call、turn count、cost 等统计信息
- 提供 Dashboard 的 Usage Insights 数据

**2. 注册位置**:
```csharp
// App.xaml.cs:670-672
services.AddSingleton<InsightsService>();
```

**3. UI 入口**:
- `DashboardPage.xaml:254-293` - Usage Insights 面板
- 默认 `Visibility="Collapsed"`，说明不是主工作流的一部分

**4. 调用方检查**:
```bash
# 搜索谁在调用 InsightsService
grep -r "InsightsService\|RecordDreamerStartupFailure" src/ Desktop/
# 主要调用方：DreamerService
```
- `DreamerService` 会调用 `RecordDreamerStartupFailure()`
- 除 Dashboard 展示外，核心用途是给 Dreamer 记录启动失败

**5. 游戏链路依赖检查**:
```bash
# 搜索 NPC runtime 是否引用
grep -r "InsightsService" src/runtime/ src/games/stardew/
# 结果：零引用
```

#### 删除理由
- **主要服务于 Dreamer**: Dreamer 是其主要调用方
- **对游戏无直接价值**: NPC runtime、自主循环、私聊不依赖统计面板
- **非主工作流**: Dashboard 面板默认折叠，本身就不是高频入口

#### 影响
- 删除约 200 行代码
- 移除 Dashboard 中的 Usage Insights 面板
- 减少 1 个 DI 注册

#### 风险
**低**（建议与 Dreamer 一并删除）

---

## 四、目录/资源精简

### 8. 多语言资源文件

#### 删除/收缩位置
- `Desktop/HermesDesktop/Strings/zh-cn/Resources.resw`
- `Desktop/HermesDesktop/Strings/en-us/Resources.resw`
- `Desktop/HermesDesktop.Tests/Views/DeveloperPageResourceTests.cs:22-26`
- `Desktop/HermesDesktop/Package.appxmanifest`
- `Desktop/HermesDesktop.Package/Package.appxmanifest`
- `Desktop/HermesDesktop.Package/HermesDesktop.Package.csproj`

#### 证据链

**1. 当前语言数量**:
- 当前只有两个资源目录：`Strings/en-us/` 和 `Strings/zh-cn/`
- **没有第三种语言**

**2. 资源规模**:
- `Strings/en-us/Resources.resw`：1796 行
- `Strings/zh-cn/Resources.resw`：1784 行
- 两个文件基本镜像，维护成本翻倍

**3. 运行时实现**:
- `MainWindow.xaml.cs`、`ChatPage.xaml.cs`、`DashboardPage.xaml.cs`、`SettingsPage.xaml.cs` 等通过 `ResourceLoader.GetString()` 加载字符串
- XAML 中大量使用 `x:Uid` 绑定 `.resw` 资源

**4. 测试约束**:
```csharp
// DeveloperPageResourceTests.cs:22-26
var locales = new[] { "en-us", "zh-cn" };
foreach (var locale in locales)
{
    // 检查资源键完整性
}
```
- 测试硬编码要求两个 locale 都存在

**5. 打包清单**:
- `Package.appxmanifest` 使用 `<Resource Language="x-generate" />` 或 `en-US`
- 当前打包链路支持资源自动发现

#### 删除理由
- **如果只保留一种语言**: 直接删除另一个 `.resw` 文件即可，维护成本立刻减半
- **但不建议删除整个资源系统**: WinUI 3 的 MRT/PRI 架构决定，即使只保留一种语言，也仍然需要 `.resw`、`x:Uid`、`ResourceLoader`
- **所以真正能删的是“一个语言包”**，而不是整套本地化框架

#### 影响
- 删除一个资源文件可减少约 50KB 打包体积
- 减少一半字符串维护负担
- 需要同步更新资源完整性测试

#### 风险
**低**（删除单个语言包）  
**高**（删除整个资源系统，不建议）

#### 审查结论
- 如果你的目标是"只保留中文和英文"，**当前已经满足，不需要改**
- 如果你的目标是"只保留一种语言"，可以删掉另一份 `.resw`
- 这里不建议作为第一阶段删除项，只建议作为**可选收缩项**

---

### 9. external/ 参考代码

#### 删除/归档位置
- `external/hermes-agent-main/` (14.5 MB zip + 解压目录)
- `external/hermescraft-main/` (188 KB zip + 解压目录)

#### 证据链

**1. 内容**:
- `hermes-agent-main/`: Python 上游完整仓库快照
- `hermescraft-main/`: Minecraft 游戏参考项目快照

**2. 构建依赖检查**:
```bash
# 搜索 .csproj 是否引用 external/
grep -r "external/" Desktop/HermesDesktop/HermesDesktop.csproj src/*.csproj
# 结果：无匹配
```

**3. 发布脚本检查**:
```powershell
# 检查 publish-portable.ps1 是否打包 external/
Get-Content scripts/publish-portable.ps1 | Select-String "external"
# 结果：无匹配
```

**4. 用途**:
- 仅供开发者参考架构思想
- 不参与编译、测试、发布

**5. AGENTS.md 明确**:
> "external/ 和 参考项目/ 除非任务明确要求，默认只读"

#### 删除理由
- **不参与构建**: 完全不影响编译和运行
- **占用空间**: 15+ MB，增加 clone 时间
- **可替代**: 改为 README 中写上游 GitHub 链接，按需 clone

#### 影响
- 减少仓库体积 15+ MB
- 加快 `git clone` 速度

#### 风险
**无**（可改为 README 链接）

#### 建议
- 在 README 中添加：
  ```markdown
  ## 参考项目
  - [hermes-agent (Python 上游)](https://github.com/xxx/hermes-agent)
  - [hermescraft (Minecraft 参考)](https://github.com/xxx/hermescraft)
  ```
- 删除 `external/` 目录

---

### 10. 参考项目/ 目录

#### 删除/归档位置
- `参考项目/Mod参考/` (32 个子项目)
- `参考项目/动态任务参考/` (5 个任务系统参考项目)
- `参考项目/obsolete/` (已废弃的旧 superpowers 设计)
- `参考项目/参考文档/`

#### 证据链

**1. 内容定位**:
- `Mod参考/`: 多个游戏 Mod 项目（Stardew、RimWorld、Minecraft 等）
- `动态任务参考/`: 任务系统参考实现
- `obsolete/`: 已废弃设计
- `参考文档/`: 外部资料整理

**2. 构建依赖检查**:
```bash
# 搜索 .csproj 和脚本是否引用 参考项目/
grep -r "参考项目/" Desktop/ src/ scripts/ *.sln*
# 结果：无匹配
```

**3. 运行时依赖检查**:
- `App.xaml.cs` 无引用
- `src/runtime/`、`src/games/stardew/` 无引用
- 不参与任何 DI 注册或工具注册

**4. 认知复杂度问题**:
- 搜索时容易返回这些参考实现，干扰对真正产品代码的定位
- 新开发者容易把参考代码误认为当前主链实现

#### 删除理由
- **纯参考资料**: 不参与编译、测试、发布
- **认知噪音大**: 对当前游戏产品开发形成干扰
- **可迁移**: 更适合放到单独的参考仓库或 wiki

#### 影响
- 减少仓库体积
- 减少搜索噪音
- 降低新成员理解成本

#### 风险
**无**（移到仓库外即可）

---

### 11. .omx/ 归档目录

#### 删除/归档位置
- `.omx/archives/` (200+ 个历史 PRD/测试规范/上下文快照)
- `.omx/context/` (历史上下文快照)
- `.omx/archives/context/`、`.omx/archives/prd-*`、`.omx/archives/test-spec-*` 等

#### 证据链

**1. 内容定位**:
- PRD 草案
- 测试规范
- 历史上下文快照
- 已归档的设计对齐文档

**2. 构建依赖检查**:
```bash
# 搜索代码和脚本是否引用 .omx/
grep -r "\.omx/" Desktop/ src/ scripts/
# 结果：无运行时依赖
```

**3. 当前活跃与归档的区别**:
- `.omc/`: 当前活跃计划、spec、notepad
- `.omx/`: 大量历史归档
- 当前产品运行不依赖 `.omx/` 中任何内容

**4. 认知复杂度问题**:
- 全仓搜索时返回大量过时结果
- 容易把历史方案误认为当前设计
- 尤其是 `prd-*`、`test-spec-*` 会和现有实现事实冲突

#### 删除理由
- **不参与构建**: 纯历史记录
- **搜索噪音大**: 干扰当前开发
- **归档属性明确**: 从目录命名就已经说明是 archive，不应与主仓库长期绑定

#### 影响
- 减少仓库体积
- 减少全文搜索噪音
- 降低误读历史方案的概率

#### 风险
**无**（建议迁移到单独 archive 仓库，而不是直接永久删除）

#### 建议
- **保留** `.omc/` 作为当前活跃计划空间
- **迁移** `.omx/archives/` 到 `hermes-design-archive` 仓库或团队知识库

---

### 12. 过时架构文档

#### 删除/归档位置
- `docs/all_9_pillars_complete.md`
- `docs/build_complete.md`
- `docs/complete_architecture.md`
- `docs/kairos_and_multiagent.md`
- `docs/session_management.md`

#### 证据链

**1. 文档问题**:
这些文档把"存在代码"写成了"已完整交付"，与当前代码事实冲突。

**2. 具体冲突项**:
- 声称 `AutoDreamService` 已完成并为主链
- 声称 `TeamManager` / `MailboxService` 是已交付的完整能力
- 描述 `SendMessageTool` 等当前未实现或未接线的能力
- 把多 agent / mailbox / team UI 描述成现有产品面

**3. 与事实冲突的依据**:
- `AGENTS.md` 已明确：
  - `AutoDreamService` 不是当前 Desktop 启动主链路
  - `SendMessageTool` 没有现成实现
  - `MailboxService` / `TeamManager` 更像底层雏形
- `App.xaml.cs` 和 `AgentCapabilityAssembler.cs` 也未体现这些文档宣称的完整接线状态

**4. 风险来源**:
- 新开发者会被这些文档误导，错误理解当前架构
- 做精简或功能修改时会基于错误前提做判断

#### 删除理由
- **事实过时**: 与真实代码和 AGENTS.md 冲突
- **误导成本高**: 比没有文档更糟，因为会把错误信息当真
- **可替代**: 当前真实事实应以 `AGENTS.md`、`App.xaml.cs`、`AgentCapabilityAssembler.cs` 为准

#### 影响
- 减少误导
- 提高文档可信度
- 降低架构理解偏差

#### 风险
**无**（建议移到 `docs/archive/` 而不是直接丢失）

#### 建议
- 保留 `docs/superpowers/` 中仍与当前产品一致的设计文档
- 保留 `docs/releases/` 作为历史发布记录
- 把上述文件迁移到 `docs/archive/`，并在顶部注明"已过时，不代表当前实现"

---

## 五、不能删除（游戏链路依赖）

以下模块虽然看起来像"通用平台能力"，但已深度集成到 NPC runtime，**必须保留**:

- ✅ **Skills 系统** - `NpcRuntimeContextFactory.cs:31-42` 每个 NPC runtime 必须传入 `SkillManager`
- ✅ **Memory 系统** - `NpcRuntimeContextFactory.cs:48` 每个 NPC 创建独立 `MemoryManager`
- ✅ **Soul 系统** - `NpcRuntimeContextFactory.cs:47` 每个 NPC 创建独立 `SoulService`
- ✅ **MCP 系统** - `App.xaml.cs:407` `NpcToolSurfaceSnapshotProvider` 从 `McpManager.Tools.Values` 获取工具面
- ✅ **Transcript / SessionTodoStore** - `NpcRuntimeContextFactory.cs:51-52` 每个 NPC 创建独立 `TranscriptStore` 和 `SessionSearchIndex`
- ✅ **Cron / Schedule** - `StardewNpcAutonomyBackgroundService.cs:468` 自主循环订阅 `ICronScheduler.TaskDue` 事件

---

## 六、预估收益

删除上述模块后：
- **代码行数减少**: 约 4000-5000 行
- **DI 注册简化**: `App.xaml.cs` 减少约 100 行
- **主导航简化**: 移除 Buddy 导航项
- **后台服务减少**: 移除 Dreamer 后台循环
- **仓库体积减少**: 15+ MB（如删除 external/参考项目）
- **认知复杂度降低**: 减少搜索噪音和过时文档误导

---

## 七、执行计划

### 第一阶段（立即可做）

1. 删除 Buddy 系统
   - 删除 `src/buddy/`
   - 删除 `Desktop/HermesDesktop/Views/BuddyPage.xaml`
   - 删除 `Desktop/HermesDesktop/Views/Panels/BuddyPanel.xaml`
   - 删除 `App.xaml.cs:548-552` 注册
   - 删除 `MainWindow.xaml:99-106` 导航项

2. 删除 Wiki 系统
   - 删除 `src/wiki/`
   - 删除 `App.xaml.cs:555-566` 注册

3. 删除 Coordinator / AgentService
   - 删除 `src/coordinator/`
   - 删除 `src/agents/AgentService.cs`
   - 删除 `App.xaml.cs:701-716` 注册

4. 删除 AutoDreamService
   - 删除 `src/dream/AutoDreamService.cs`

5. 删除 MixtureOfAgentsTool
   - 删除 `src/Tools/MixtureOfAgentsTool.cs`

6. 删除过时文档
   - 删除或移到 `docs/archive/`:
     - `docs/all_9_pillars_complete.md`
     - `docs/build_complete.md`
     - `docs/complete_architecture.md`
     - `docs/kairos_and_multiagent.md`
     - `docs/session_management.md`

7. 运行测试验证
   ```powershell
   dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
   ```

---

### 第二阶段（需配置验证）

1. 配置 Dreamer 禁用
   - 在 `config.yaml` 设置 `dreamer.enabled: false`
   - 启动桌面应用验证无副作用

2. 删除 Dreamer 系统
   - 删除 `src/dreamer/`
   - 删除 `App.xaml.cs:738` `StartDreamerBackground()` 调用
   - 删除 `App.xaml.cs:783-859` `StartDreamerBackground()` 方法
   - 删除 `App.xaml.cs:676` `DreamerStatus` 注册
   - 删除 `DashboardPage.xaml:68-92` Dreamer 卡片
   - 删除 `SettingsPage.xaml.cs:641-694` Dreamer 配置区

3. 删除 InsightsService
   - 删除 `src/analytics/InsightsService.cs`
   - 删除 `App.xaml.cs:670-672` 注册
   - 删除 `DashboardPage.xaml:254-293` Insights 面板

4. 运行测试验证
   ```powershell
   dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
   ```

---

### 第三阶段（可选）

1. 归档参考资料
   - 移动 `external/` 到仓库外或改为 README 链接
   - 移动 `参考项目/` 到仓库外 wiki
   - 移动 `.omx/` 到单独的 `hermes-design-archive` 仓库

2. 精简多语言资源（可选）
   - 如果只需英文: 删除 `Desktop/HermesDesktop/Strings/zh-cn/`
   - 如果只需中文: 删除 `Desktop/HermesDesktop/Strings/en-us/`
   - 更新 `Desktop/HermesDesktop.Tests/Views/DeveloperPageResourceTests.cs:22-26` 测试

---

## 八、风险提示

1. **不要误删 Skills/Memory/Soul/MCP**: 这些看起来像"通用平台能力"，但已深度集成到 NPC runtime 主链路
2. **AgentPage 需保留**: 虽然 Coordinator/AgentService 可删，但 `AgentPage` 提供 Soul/Identity/NPC runtime 配置 UI，必须保留
3. **DeveloperPage 必须保留**: 这是 NPC runtime 调试与观测入口，不能删
4. **移除 Dreamer 前先禁用**: 可先在 `config.yaml` 设置 `dreamer.enabled: false` 验证无副作用，再删代码

---

## 九、验证清单

在删除任何模块前：

1. **全仓搜索引用**: 
   ```bash
   git grep -n "BuddyService\|WikiManager\|CoordinatorService\|AutoDreamService\|MixtureOfAgentsTool"
   ```

2. **运行测试**: 
   ```powershell
   dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
   ```

3. **检查 git 历史**: 如需保留设计思路，提取到文档再删代码

4. **团队确认**: 与其他开发者确认这些模块是否有未来计划

---

**计划完成**。建议按阶段逐步精简，每批删除后运行测试验证游戏链路完整性。
