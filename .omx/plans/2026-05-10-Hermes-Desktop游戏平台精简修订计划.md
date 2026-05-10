# Hermes-Desktop 游戏平台精简修订计划（已被共识计划取代）

> 本文件是深访后的临时整理稿。最终执行以以下文件为准：
>
> - `.omx/plans/产品需求文档-Hermes-Desktop游戏平台边界清理-20260510.md`
> - `.omx/plans/测试规范-Hermes-Desktop游戏平台边界清理-20260510.md`

## 定位

Hermes-Desktop 不是只服务当前 Stardew MVP 的最小壳，而是“以星露谷为第一个场景的多游戏 agent 平台”。

本轮精简目标不是把仓库删到最小，而是让游戏 agent 平台内核更清楚：核心 NPC runtime 不动，外围噪音删除，可复用底座保留并改造成游戏语义。

## 硬边界

- 不碰核心 NPC runtime。
- 不碰 NPC 的 `Soul`、`Memory`、`Transcript`、`SessionTodoStore`、`Skill`、`MCP`、`Cron/Schedule` 装配边界。
- 不碰 Stardew bridge/autonomy/private chat 的主链路。
- 不因为“当前未接线”就删除模块；如果能明确成为多游戏底座，保留并改造。
- 删除项必须同时满足：当前弱接线，并且未来平台适配弱。

## 决策表

| 项目 | 决策 | 原因 | 核心 NPC runtime 风险 |
| --- | --- | --- | --- |
| `Buddy` | 删除 | 桌面趣味/伙伴功能，未进入游戏链路，也不构成多游戏底座 | 无 |
| `Wiki` | 保留并改造 | 改成游戏世界知识、规则、设定、地点语义底座 | 低 |
| `Coordinator / AgentService` | 保留 | 可作为多 NPC、长任务、跨游戏编排底座候选 | 低 |
| `AutoDreamService` | 删除 | 旧 Dream 实现，默认不注册，保留会制造双 Dream 系统误导 | 无 |
| `MixtureOfAgentsTool` | 暂缓 | 当前是通用多模型合成工具，但未来可能变成多 NPC 共识/议事会 | 无 |
| `Dreamer / Insights` | 保留并改造 | 从通用项目反思改成游戏世界/NPC 运行总结与观测 | 中 |
| `external/` | 迁出/归档 | 参考价值保留，但不应干扰主仓搜索 | 无 |
| `参考项目/` | 迁出/归档或隔离搜索 | 多游戏参考有价值，但不应混入当前实现搜索 | 无 |
| `.omx/archives` | 迁出/归档 | 历史计划不应作为当前事实来源 | 无 |
| 过时架构文档 | 归档并标注过时 | 防止误导当前架构判断 | 无 |
| 多语言资源 | 保留中英文 | 当前只维护中英文，不扩第三语言，不拆 WinUI 资源系统 | 无 |

## 第一批：可直接删除

### 1. 删除 `Buddy`

删除范围：

- `src/buddy/`
- `Desktop/HermesDesktop/Views/BuddyPage.xaml`
- `Desktop/HermesDesktop/Views/BuddyPage.xaml.cs`
- `Desktop/HermesDesktop/Views/Panels/BuddyPanel.xaml`
- `Desktop/HermesDesktop/Views/Panels/BuddyPanel.xaml.cs`
- `Desktop/HermesDesktop/App.xaml.cs` 中 `BuddyService` 注册
- `Desktop/HermesDesktop/MainWindow.xaml` 中 `Buddy` 导航项
- `Desktop/HermesDesktop/MainWindow.xaml.cs` 中 `buddy` 路由
- `Desktop/HermesDesktop/Strings/**/Resources.resw` 中 `BuddyNavItem` 等 Buddy UI 字符串
- `Desktop/HermesDesktop.Tests/Services/BuddyServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Helpers/PanelHelperLogicTests.cs` 中 BuddyPanel 相关测试

验证重点：

- 桌面构建通过。
- 主导航不再出现 Buddy。
- 不影响 Chat、Agent、Skills、Memory、Settings、Dashboard。

### 2. 删除 `AutoDreamService`

删除范围：

- `src/dream/AutoDreamService.cs`
- `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs` 中“默认不注册 AutoDreamService”的测试需要改成“源码中不再存在旧 AutoDreamService”或删除对应断言。
- 清理任何旧 `using Hermes.Agent.Dream` 引用。

验证重点：

- 当前 `src/dreamer/` 主链仍存在。
- 桌面启动路径仍只走 `DreamerService`。
- 不影响 NPC runtime。

## 第二批：保留但改造成游戏平台模块

### 3. `Wiki` 改造成游戏世界知识底座

目标方向：

- 从通用 `Wiki` 改成游戏世界知识、地点语义、规则说明、角色常识、剧情线索、物品/地图/活动知识底座。
- 后续命名可考虑中文概念“世界知识库”或代码标识符 `WorldKnowledge` / `GameLore`。
- 不要隐式注入核心 NPC prompt；应通过明确工具、skill 或裁剪后的上下文包接入。

第一步建议：

- 暂不删代码。
- 先写设计说明，明确它未来服务哪些游戏 agent 场景。
- 后续再决定是否重命名命名空间、目录和工具面。

### 4. `Coordinator / AgentService` 保留为编排底座候选

目标方向：

- 从通用代码 agent 编排，收敛为多 NPC 协作、长任务拆分、跨游戏任务编排、隔离执行 lane。
- 明确它不是第二套 NPC runtime，也不是第二套 NPC 记忆/任务系统。
- 未来接入时必须经过 NPC runtime 边界审查。

第一步建议：

- 暂不删。
- 后续单独审查 `worktree`、`remote`、`mailbox`、`team` 这些能力是否仍适合游戏 agent。

### 5. `Dreamer / Insights` 改造成游戏后台认知与观测

目标方向：

- 从通用项目反思改成游戏世界事件回顾、NPC 行为总结、村庄状态摘要、异常行为诊断、长期剧情线索沉淀。
- 保持后台系统不能替 NPC 决策，不能绕过 NPC 工具执行边界。
- `Insights` 更像运行观测与诊断，不应只服务 Dreamer。

第一步建议：

- 暂不删。
- 后续先改文档和 UI 命名，让它不再看起来像泛 AI 项目管理功能。
- 默认启动策略可单独讨论：保留默认启动、改成配置开关、或只在游戏会话存在时启动。

## 第三批：暂缓

### 6. `MixtureOfAgentsTool`

当前结论：

- 暂时保留。
- 当前通用“多个模型回答再合成”形态不是产品就绪能力。
- 未来如果要用，应改造成游戏语义，例如“多 NPC 共识”“议事会”“世界规划器”。

约束：

- 暂不注册进默认工具面。
- 暂不接入 NPC runtime。
- 后续若启用，必须先设计游戏语义和边界。

## 第四批：迁出/归档/隔离搜索

### 7. 参考资料和历史资料

目标：

- 保留参考价值。
- 降低主仓搜索噪音。
- 防止历史计划和过时文档被误当成当前事实。

建议处理：

- `external/`：保留索引文档，原始快照迁出主仓或压缩归档。
- `参考项目/`：保留参考，但隔离出默认搜索路径。
- `.omx/archives`：迁出或明确标注为历史归档。
- 过时架构文档：移动到 `docs/archive/`，文件头标注“已过时，不代表当前实现”。

## 第五批：保持现状

### 8. 多语言资源

结论：

- 只保留中文和英文。
- 不新增第三语言维护范围。
- 不拆 WinUI 资源系统。

## 建议执行顺序

1. 删除 `Buddy`，运行桌面测试和构建。
2. 删除 `AutoDreamService`，运行桌面测试和构建。
3. 归档或标注过时文档，先不移动大量参考代码。
4. 为 `Wiki`、`Coordinator / AgentService`、`Dreamer / Insights` 写游戏平台化改造说明。
5. 再决定是否进入代码级重命名、UI 调整、默认启动策略调整。

## 验证命令

```powershell
git status --short --branch
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## 剩余风险

- 删除 `Buddy` 会牵涉 UI 资源和测试，不是只删一个目录。
- 删除 `AutoDreamService` 需要同步调整“确认旧 Dream 未注册”的测试。
- `Dreamer / Insights` 是已启动系统，不能作为第一批删除项。
- `Wiki` 当前未接线，但按产品定位应保留为游戏知识底座候选。
- 参考资料迁出前要确认是否还有近期开发依赖本地快照。
