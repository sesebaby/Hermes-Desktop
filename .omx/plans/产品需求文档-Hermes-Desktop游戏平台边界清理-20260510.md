# 产品需求文档：Hermes-Desktop 游戏平台边界清理

## 1. 背景

本项目定位为“以星露谷为第一个场景的多游戏 agent 平台”，不是只服务当前 Stardew MVP 的最小运行壳。

本轮目标是让平台核心边界更清楚：删除偏离主线的独立 companion/gacha 产品面和旧 Dream 实现，保留当前被确认有平台价值或仍需单独决策的能力。

## 2. 硬边界

- 不碰核心 NPC runtime。
- 不改 `src/runtime/**`。
- 不改 `src/games/stardew/**`。
- 不改造 `src/wiki/**`。
- 不改造 `src/agents/**`。
- 不改造 `src/coordinator/**`。
- 不改造当前 `src/dreamer/**` / `InsightsService` 主线。
- 不注册、不删除、不改造 `MixtureOfAgentsTool`。
- 不搬迁 `external/`、`参考项目/`、`其他资料/`、`.omx` 历史资料。

## 3. 范围

### 3.1 本轮要做

- 删除 `Buddy` 全链路。
- 删除旧 `AutoDreamService`。
- 修订 `MemoryParityTests`，把旧 dormant 叙事改为当前 Dreamer 主线守卫。
- 校准当前权威文档，不再把 Buddy 写成当前能力、当前导航项或组合根事实。
- 对历史文档只做必要“历史/已移除”标注，不做大规模搬家。

### 3.2 本轮不做

- 不把 `Wiki` 改造成世界知识库。
- 不改 `Coordinator / AgentService`。
- 不改 `Dreamer / Insights`。
- 不处理 `MixtureOfAgentsTool` 去留，只确认未注册。
- 不做参考资料治理。

## 4. 证据基线

- `Desktop/HermesDesktop/App.xaml.cs:12` 使用 `Hermes.Agent.Buddy`。
- `Desktop/HermesDesktop/App.xaml.cs:549-552` 注册 `BuddyService`。
- `Desktop/HermesDesktop/MainWindow.xaml:100-102` 暴露 `BuddyNavItem`。
- `Desktop/HermesDesktop/MainWindow.xaml.cs:22` 将 `buddy` 路由到 `BuddyPage`。
- `src/buddy/Buddy.cs` 是 Buddy 完整实现。
- `src/dream/AutoDreamService.cs` 是旧 Dream 服务。
- `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs:988-996` 当前测试表达“AutoDreamService 默认不注册”。
- `src/runtime/AgentCapabilityAssembler.cs:19` 的内建工具列表不包含 `mixture_of_agents`。
- `Desktop/HermesDesktop/App.xaml.cs` 仍注册 `WikiManager`、`InsightsService`、`DreamerStatus`、`AgentService`、`CoordinatorService` 并启动 `StartDreamerBackground`。

## 5. RALPLAN-DR 摘要

### 原则

- 以多游戏 agent 平台主线为准，删除偏离主线的独立 Buddy 产品面。
- 不触碰核心 NPC runtime。
- 保留 Wiki、Coordinator / AgentService、Dreamer / Insights 当前形态。
- 删除必须全链路完成：代码、DI、UI、导航、资源、测试、当前权威说明同步收敛。
- 验证分层：运行时源码按严格无残留验收，历史文档按历史标注验收。

### 决策驱动

- Buddy 已形成完整产品面，只隐藏 UI 不足以降低复杂度。
- AutoDreamService 是旧实现，继续保留会误导后续 agents 以为存在第二条 Dream 主线。
- MixtureOfAgentsTool 未注册，本轮保留未暴露状态即可。
- 文档和历史资料大搬家会制造无行为价值 diff，不适合作为本轮目标。

### 方案对比

- 方案 A：Buddy 全链路删除 + AutoDreamService 删除 + 当前事实文档校准。采用。
- 方案 B：只隐藏 Buddy UI，保留代码/DI/测试。拒绝，死代码和错误能力叙事仍在。
- 方案 C：把 Buddy 改造成游戏宠物 agent。拒绝，会触碰新产品设计和 NPC runtime 边界。
- 方案 D：同时改造 Wiki、Coordinator、Dreamer。拒绝，违反本轮明确保留约束。

## 6. 实施步骤

1. 基线检查：运行 `git status --short --branch`，确认已有用户改动。
2. 删除 `src/buddy/Buddy.cs`。
3. 从 `Desktop/HermesDesktop/App.xaml.cs` 删除 `using Hermes.Agent.Buddy;`、`buddyDir`、`buddyConfigPath`、`BuddyService` 注册块。
4. 从 `Desktop/HermesDesktop/MainWindow.xaml` 删除 `BuddyNavItem`。
5. 从 `Desktop/HermesDesktop/MainWindow.xaml.cs` 删除 `buddy` 路由。
6. 删除 `Desktop/HermesDesktop/Views/BuddyPage.xaml` 和 `.xaml.cs`。
7. 删除 `Desktop/HermesDesktop/Views/Panels/BuddyPanel.xaml` 和 `.xaml.cs`。
8. 从 `Desktop/HermesDesktop/Strings/en-us/Resources.resw` 与 `zh-cn/Resources.resw` 删除 Buddy 专属资源键。
9. 删除 `Desktop/HermesDesktop.Tests/Services/BuddyServiceTests.cs`。
10. 清理 `Desktop/HermesDesktop.Tests/Helpers/PanelHelperLogicTests.cs` 中 BuddyPanel 相关测试和注释。
11. 删除 `src/dream/AutoDreamService.cs`。
12. 修订 `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`，改为守卫当前 Dreamer 主线：
    - `src/dream/AutoDreamService.cs` 文件不存在。
    - `Desktop/HermesDesktop/App.xaml.cs` 不包含 `AutoDreamService`。
    - `Desktop/HermesDesktop/App.xaml.cs` 仍包含 `StartDreamerBackground`。
    - 当前 Dreamer 主线源码仍包含 `DreamerService`。
13. 校准当前权威文档：
    - `AGENTS.md`
    - `Desktop/HermesDesktop/AGENTS.md`
    - `Desktop/HermesDesktop/docs/LOCALIZATION-RECON.md`
14. 对历史文档只做必要“历史实现/已移除，不代表当前产品面”标注。
15. 运行 build、测试和分层扫描。
16. 用 `git diff --name-only` 复核没有改到禁止范围。

## 7. 文件清单

### 删除或修改

- `src/buddy/Buddy.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/MainWindow.xaml`
- `Desktop/HermesDesktop/MainWindow.xaml.cs`
- `Desktop/HermesDesktop/Views/BuddyPage.xaml`
- `Desktop/HermesDesktop/Views/BuddyPage.xaml.cs`
- `Desktop/HermesDesktop/Views/Panels/BuddyPanel.xaml`
- `Desktop/HermesDesktop/Views/Panels/BuddyPanel.xaml.cs`
- `Desktop/HermesDesktop/Strings/en-us/Resources.resw`
- `Desktop/HermesDesktop/Strings/zh-cn/Resources.resw`
- `Desktop/HermesDesktop.Tests/Services/BuddyServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Helpers/PanelHelperLogicTests.cs`
- `src/dream/AutoDreamService.cs`
- `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
- `AGENTS.md`
- `Desktop/HermesDesktop/AGENTS.md`
- `Desktop/HermesDesktop/docs/LOCALIZATION-RECON.md`

### 明确不改

- `src/runtime/**`
- `src/games/stardew/**`
- `src/wiki/**`
- `src/agents/**`
- `src/coordinator/**`
- `src/dreamer/**`
- `src/analytics/InsightsService.cs`
- `src/Tools/MixtureOfAgentsTool.cs`
- `src/runtime/AgentCapabilityAssembler.cs` 中的工具注册链

## 8. 验收标准

- Desktop build 成功。
- 主测试项目通过。
- Buddy 在运行时路径无代码、DI、导航、页面、面板、资源、测试残留。
- AutoDream 旧服务源码删除。
- `MemoryParityTests` 守卫当前 Dreamer 主线，不再表达旧服务 dormant 是当前事实。
- Mixture 仍未注册进默认工具面。
- 当前权威文档不再把 Buddy 写成当前能力。
- 历史文档若保留 Buddy/AutoDream 引用，必须解释为历史记录，不作为当前产品面。
- `git diff --name-only` 不包含禁止范围的行为改动。

## 9. ADR

### Decision

采用“Buddy 全链路删除 + AutoDreamService 旧实现删除 + 当前事实文档校准”的边界清理方案。

### Drivers

- 产品定位是多游戏 agent 平台，Buddy 是偏离主线的独立 companion/gacha 产品面。
- 旧 AutoDreamService 与当前 Dreamer 主线并存会误导架构判断。
- 用户明确要求 Wiki、Coordinator / AgentService、Dreamer / Insights 当前保留，先不要改造。
- 本轮目标是清理边界，不是资料治理或新功能设计。

### Buddy 替代方案

- 隐藏 UI 保留代码：拒绝。DI、测试、资源和文档仍会保留错误能力面。
- 改造成游戏宠物 agent：拒绝。未来如果做游戏宠物，应走 NPC runtime / game adapter 模式，不应复活 Buddy。
- 直接删除：采用。最清楚地消除旁支产品面和维护成本。

### AutoDream 替代方案

- 保留 dormant：拒绝。继续误导后续维护者。
- 并入 Dreamer：拒绝。属于 Dreamer 改造，违反本轮范围。
- 删除旧实现：采用。让当前 Dreamer 主线唯一化。

### Mixture 替代方案

- 删除：拒绝。用户已选择暂缓保留。
- 注册为正式工具：拒绝。会改变 agent 能力面。
- 保留未注册：采用。行为不变，只通过扫描确认未暴露。

### Consequences

- Buddy 用户数据 `buddy/buddy.json` 不再被读取。
- 主导航减少一个非游戏平台核心入口。
- AutoDreamService 不再作为源码候选存在。
- 历史资料中可能仍出现 Buddy/AutoDream，但权威文档会区分当前事实和历史记录。

### Follow-ups

- 单独评估 `MixtureOfAgentsTool` 去留。
- 单独建立“当前事实索引”文档。
- 如要整理 `external/`、`参考项目/`、`.omx`，先写资料治理 ADR。

## 10. 执行建议

推荐单人 `$ralph` 执行，不建议 `$team`。原因是共享文件集中在组合根、导航、资源和测试，并行修改容易冲突。

建议执行角色：

- `executor`：删除 Buddy/AutoDream 代码与 UI。
- `test-engineer`：修订 `MemoryParityTests` 和测试残留。
- `writer`：校准当前权威文档和必要历史标注。
- `verifier`：运行 build/test/扫描并复核禁止范围。
