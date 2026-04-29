# Cross-Game NPC Runtime Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在当前 Hermes Desktop 仓库中落地“跨游戏 NPC runtime core + 游戏 adapter + NPC 资料包”三层架构，并先支持 Stardew Valley 的双 NPC 真实隔离 MVP。

**Architecture:** 现有桌面聊天路径仍然围绕 `Desktop/HermesDesktop/Services/HermesChatService.cs:19` 的单会话 in-process agent 组织，而本方案会把它上提为“运行时宿主”而不是“唯一会话入口”。核心抽象只负责 NPC 生命周期、namespace、事件循环、trace 与 game contract；具体的 Stardew / SMAPI 语义全部落在 `src/games/stardew/`。NPC 本体通过资料包动态发现与装载，同游戏内新增 NPC 目标是不修改 core。

**Tech Stack:** .NET 10, WinUI 3, MSTest, Hermes.Core, SQLite transcript store, JSON manifest loading, SMAPI adapter boundary

---

## Final Architecture Decision

### Frozen boundaries
- `src/runtime/`：跨游戏 NPC runtime orchestration
- `src/game/core/`：跨游戏 game contract + NPC pack schema/loader
- `src/games/stardew/`：Stardew/SMAPI adapter only
- `content/npc-packs/<game>/<npc-id>/`：NPC content/config packs only

### Hard rules
- 一个 NPC = 一个独立 Agent 实例
- 每个 NPC 有独立 namespace：soul / memory / transcript / runtime state
- `core runtime` 必须拓扑无关，不与单进程或多进程强绑定
- 资料包只承载内容与配置，不承载任意可执行逻辑
- 同游戏新增 NPC 优先通过资料包完成，不改 core
- 最低验收线仍然是：双 NPC + 真实 bridge + 记忆/人格隔离可证明

---

## File Structure Map

### Existing files to keep and integrate
- Modify: `Desktop/HermesDesktop/App.xaml.cs` — DI 注册新 runtime host、pack loader、game adapter、debug view model
- Modify: `Desktop/HermesDesktop/Services/HermesChatService.cs` — 从“单聊天会话服务”转向“宿主 + 调试入口”协作
- Modify: `src/soul/SoulService.cs` — 支持基于 namespace 的路径装配
- Modify: `src/memory/MemoryManager.cs` — 支持基于 namespace 的实例化/路径隔离
- Modify: `src/transcript/TranscriptStore.cs` — 支持每 NPC 独立 transcript store / session source 标识
- Modify: `src/Core/Agent.cs` — 为 runtime host 暴露稳定的 agent 实例构建入口（避免仅靠 UI chat path）
- Modify: `src/soul/SoulRegistry.cs` — 复用其目录扫描经验，但不直接承担 NPC pack 发现
- Reference: `src/skills/BundledSkillCatalogService.cs` — 复用 manifest、校验、刷新思路

### New core runtime files
- Create: `src/runtime/NpcRuntimeHost.cs` — runtime host 顶层入口，拓扑无关
- Create: `src/runtime/NpcRuntimeSupervisor.cs` — 创建、启动、停止、恢复多个 NPC runtime
- Create: `src/runtime/NpcRuntimeInstance.cs` — 单 NPC 运行时聚合对象
- Create: `src/runtime/NpcRuntimeDescriptor.cs` — runtime 元数据与状态 DTO
- Create: `src/runtime/NpcNamespace.cs` — 每 NPC 的路径与隔离资源装配
- Create: `src/runtime/NpcAutonomyLoop.cs` — 感知→规划→执行循环
- Create: `src/runtime/NpcRuntimeTrace.cs` — action/event/memory trace 聚合
- Create: `src/runtime/WorldCoordinationService.cs` — 共享世界协调与仲裁入口
- Create: `src/runtime/RuntimeTopology.cs` — in-process / out-of-process host mode 描述

### New game-core files
- Create: `src/game/core/IGameAdapter.cs` — 组合式 adapter 根接口
- Create: `src/game/core/IGameCommandService.cs`
- Create: `src/game/core/IGameQueryService.cs`
- Create: `src/game/core/IGameEventSource.cs`
- Create: `src/game/core/GameAction.cs`
- Create: `src/game/core/GameObservation.cs`
- Create: `src/game/core/WorldSnapshot.cs`
- Create: `src/game/core/GameEntityBinding.cs`
- Create: `src/game/core/INpcPackLoader.cs`
- Create: `src/game/core/NpcPackManifest.cs`
- Create: `src/game/core/NpcPackValidationResult.cs`
- Create: `src/game/core/FileSystemNpcPackLoader.cs`

### New Stardew adapter files
- Create: `src/games/stardew/StardewAdapter.cs`
- Create: `src/games/stardew/StardewCommandService.cs`
- Create: `src/games/stardew/StardewQueryService.cs`
- Create: `src/games/stardew/StardewEventIngestor.cs`
- Create: `src/games/stardew/StardewWorldAdapter.cs`
- Create: `src/games/stardew/SmapiModApiClient.cs`
- Create: `src/games/stardew/StardewNpcBindingResolver.cs`

### New desktop debug files
- Create: `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- Create: `Desktop/HermesDesktop/Models/NpcRuntimeItem.cs`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcRuntimePanel.xaml`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcRuntimePanel.xaml.cs`
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml`

### New content/schema files
- Create: `content/npc-packs/stardew/abigail/manifest.json`
- Create: `content/npc-packs/stardew/abigail/soul.md`
- Create: `content/npc-packs/stardew/abigail/memory-seed.json`
- Create: `content/npc-packs/stardew/sebastian/manifest.json`
- Create: `content/npc-packs/stardew/sebastian/soul.md`
- Create: `content/npc-packs/stardew/sebastian/memory-seed.json`

### New tests
- Create: `Desktop/HermesDesktop.Tests/Runtime/NpcNamespaceTests.cs`
- Create: `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- Create: `Desktop/HermesDesktop.Tests/GameCore/NpcPackLoaderTests.cs`
- Create: `Desktop/HermesDesktop.Tests/GameCore/NpcPackManifestTests.cs`
- Create: `Desktop/HermesDesktop.Tests/Stardew/StardewNpcBindingResolverTests.cs`
- Create: `Desktop/HermesDesktop.Tests/Runtime/WorldCoordinationServiceTests.cs`

---

## Architecture Details

### A. Core runtime responsibilities
`src/runtime/` 只负责：
- 一个 NPC runtime 的生命周期
- 多 NPC registry 与 health
- namespace 装配
- 通用 autonomy loop
- trace 聚合
- 与 `IGameAdapter` 交互

它**不负责**：
- Stardew 世界语义
- SMAPI 协议细节
- NPC 具体角色内容

### B. Game adapter responsibilities
`src/games/stardew/` 只负责：
- 把通用 `GameAction` 映射到 SMAPI bridge
- 把 SMAPI 返回映射成 `GameObservation` / `WorldSnapshot`
- 解析现有 NPC 实体绑定
- 产出节日 / 剧情 / 对话等游戏事件

### C. NPC pack responsibilities
每个资料包负责声明：
- 我是谁（name / npcId / gameId）
- 我绑定谁（targetEntityId）
- 我有哪些初始人格与记忆
- 我有哪些默认策略 / policy

资料包**不负责**：
- 任意代码执行
- 调度循环
- adapter 行为实现

### D. Topology choice
当前不强制单进程。我的建议是：
- **Phase 1 先实现 topology-neutral in-process host**，因为与当前 `App.xaml.cs` DI、`HermesChatService`、`TranscriptStore` 的耦合最低
- 但 runtime host API 必须允许未来替换成 out-of-process worker host
- 所以 `NpcRuntimeSupervisor` 不直接暴露 WinUI 或 static path 依赖，只依赖 descriptor / namespace / adapter / agent factory

原因：这样最快落地，同时不给未来多进程封死路

---

## Recommended Build Order

### Task 1: Define game-core contracts and pack schema
**Files:**
- Create: `src/game/core/IGameAdapter.cs`
- Create: `src/game/core/IGameCommandService.cs`
- Create: `src/game/core/IGameQueryService.cs`
- Create: `src/game/core/IGameEventSource.cs`
- Create: `src/game/core/GameAction.cs`
- Create: `src/game/core/GameObservation.cs`
- Create: `src/game/core/WorldSnapshot.cs`
- Create: `src/game/core/GameEntityBinding.cs`
- Create: `src/game/core/NpcPackManifest.cs`
- Test: `Desktop/HermesDesktop.Tests/GameCore/NpcPackManifestTests.cs`

**Outcome:** 固定跨游戏 contract，防止 Stardew 语义进入 core。

### Task 2: Build pack loader and validation
**Files:**
- Create: `src/game/core/INpcPackLoader.cs`
- Create: `src/game/core/NpcPackValidationResult.cs`
- Create: `src/game/core/FileSystemNpcPackLoader.cs`
- Test: `Desktop/HermesDesktop.Tests/GameCore/NpcPackLoaderTests.cs`
- Content: `content/npc-packs/stardew/abigail/*`, `content/npc-packs/stardew/sebastian/*`

**Outcome:** 新增同游戏 NPC 可以通过资料包完成。

### Task 3: Introduce per-NPC namespace
**Files:**
- Create: `src/runtime/NpcNamespace.cs`
- Modify: `src/soul/SoulService.cs`
- Modify: `src/memory/MemoryManager.cs`
- Modify: `src/transcript/TranscriptStore.cs`
- Test: `Desktop/HermesDesktop.Tests/Runtime/NpcNamespaceTests.cs`

**Outcome:** 每个 NPC 独立 soul/memory/transcript/state。

### Task 4: Build runtime host and supervisor
**Files:**
- Create: `src/runtime/NpcRuntimeHost.cs`
- Create: `src/runtime/NpcRuntimeSupervisor.cs`
- Create: `src/runtime/NpcRuntimeInstance.cs`
- Create: `src/runtime/NpcRuntimeDescriptor.cs`
- Create: `src/runtime/RuntimeTopology.cs`
- Modify: `Desktop/HermesDesktop/App.xaml.cs`
- Test: `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`

**Outcome:** 当前仓库从单 `_currentSession` 走向多 NPC runtime registry。

### Task 5: Add autonomy loop and trace
**Files:**
- Create: `src/runtime/NpcAutonomyLoop.cs`
- Create: `src/runtime/NpcRuntimeTrace.cs`
- Modify: `src/Core/Agent.cs`
- Test: `Desktop/HermesDesktop.Tests/Services/AgentTests.cs`

**Outcome:** NPC 具备持续自治循环与可验证证据面。

### Task 6: Add world coordination layer
**Files:**
- Create: `src/runtime/WorldCoordinationService.cs`
- Test: `Desktop/HermesDesktop.Tests/Runtime/WorldCoordinationServiceTests.cs`

**Outcome:** 双 NPC 不抢同一资源，不互相踩状态，节日/剧情优先级有统一入口。

### Task 7: Implement Stardew adapter shell
**Files:**
- Create: `src/games/stardew/StardewAdapter.cs`
- Create: `src/games/stardew/StardewCommandService.cs`
- Create: `src/games/stardew/StardewQueryService.cs`
- Create: `src/games/stardew/StardewEventIngestor.cs`
- Create: `src/games/stardew/StardewWorldAdapter.cs`
- Create: `src/games/stardew/SmapiModApiClient.cs`
- Create: `src/games/stardew/StardewNpcBindingResolver.cs`
- Test: `Desktop/HermesDesktop.Tests/Stardew/StardewNpcBindingResolverTests.cs`

**Outcome:** 与 SMAPI 的边界清晰，Stardew 语义不污染 core。

### Task 8: Wire desktop debug surface
**Files:**
- Create: `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- Create: `Desktop/HermesDesktop/Models/NpcRuntimeItem.cs`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcRuntimePanel.xaml`
- Create: `Desktop/HermesDesktop/Views/Panels/NpcRuntimePanel.xaml.cs`
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml`
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`

**Outcome:** 有最小可观测面来验证运行时、资料包、adapter、trace。

---

## Suggested Initial Topology Decision

我建议**第一实现阶段先做 in-process host，但按 topology-neutral API 落地**。

### Why this is the best first move
1. 当前 DI、Agent、Memory、Transcript 都已经在 `App.xaml.cs:259-775` 里集中装配，先在同进程里拉起多实例成本最低。
2. 你的硬约束不是“必须多进程”，而是“不能被单进程绑死”。
3. 先把 `NpcRuntimeSupervisor + NpcNamespace + GameContract + NpcPackLoader` 抽象好，比过早引入 IPC 更能降低架构返工。
4. 如果后面 Stardew bridge 或隔离要求逼出多进程，只需要替换 host topology，不重写 core。

### When to switch to multi-process later
若出现以下任一情况，就应切多进程：
- SMAPI bridge 明显要求进程边界隔离
- 每 NPC 运行资源竞争严重
- 崩溃隔离成为核心需求
- 双 NPC MVP 很快验证通过，准备扩到更多 NPC

---

## NPC Pack Schema Draft

```json
{
  "schemaVersion": 1,
  "npcId": "abigail",
  "gameId": "stardew",
  "displayName": "Abigail",
  "targetEntityId": "Abigail",
  "adapterId": "stardew",
  "soulFile": "soul.md",
  "memorySeedFile": "memory-seed.json",
  "policies": {
    "schedulePriority": "agent_unless_story_event",
    "dialogStyle": "curious",
    "resourceBias": ["forage", "ore"]
  },
  "capabilities": ["move", "gather", "speak"]
}
```

### Required validation rules
- `npcId`, `gameId`, `targetEntityId`, `adapterId` 必填
- `adapterId` 必须存在已注册 adapter
- `soulFile` / `memorySeedFile` 路径必须位于 pack root 内
- `capabilities` 只能声明已知能力，不允许自由字符串驱动任意行为

---

## Testing Strategy

### Unit tests first
- `NpcPackManifestTests`：校验 manifest 必填项与路径规则
- `NpcPackLoaderTests`：校验目录发现、schema 验证、重复 NPC 拒绝
- `NpcNamespaceTests`：校验每 NPC 路径隔离
- `NpcRuntimeSupervisorTests`：校验多 runtime 注册、恢复、停止
- `WorldCoordinationServiceTests`：校验双 NPC 冲突仲裁
- `StardewNpcBindingResolverTests`：校验 pack 到现有 NPC 实体的绑定

### Integration tests later
- pack load -> namespace build -> runtime start -> adapter bind
- 双 NPC 启动后 transcript/state 分离
- 节日/剧情事件触发后 policy 生效

---

## Risks and counters

### Risk 1: 抽象过早
**Counter:** `GameAction` / `GameObservation` / `WorldSnapshot` 先只覆盖 Stardew MVP 所需字段，不为未来虚构能力预埋复杂层。

### Risk 2: Pack 变成脚本平台
**Counter:** pack schema 只允许声明式内容与策略值，不允许自由代码或表达式执行。

### Risk 3: 仍然被当前 App/ChatService 单会话路径绑死
**Counter:** `NpcRuntimeSupervisor` 不依赖 `_currentSession`，只依赖 agent factory 和 namespace builder。

### Risk 4: Stardew adapter 侵入 core
**Counter:** 所有带 `Stardew`, `SMAPI`, `Festival`, `Friendship`, `Dialogue` 等游戏术语的类型禁止进入 `src/runtime/` 与 `src/game/core/`。

---

## Spec coverage check
- 跨游戏边界：covered by `src/game/core/` vs `src/games/stardew/`
- NPC 独立实例：covered by runtime host/supervisor/namespace
- 资料包动态加载：covered by pack schema + loader
- 同游戏新增 NPC 不改 core：covered by pack path + validation strategy
- 不强制单进程：covered by topology-neutral host design
- 双 NPC 隔离验收：covered by namespace / coordination / trace / tests

---

## Immediate decision for implementation phase

如果现在进入实现，我建议先做这三个最小骨架：
1. `src/game/core/` contract + `NpcPackManifest`
2. `FileSystemNpcPackLoader`
3. `NpcNamespace`

这是最小且最不容易返工的起步面。

---

Plan complete and saved to `docs/superpowers/plans/2026-04-29-cross-game-npc-runtime-architecture.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
