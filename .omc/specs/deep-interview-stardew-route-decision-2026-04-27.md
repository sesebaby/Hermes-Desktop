# Deep Interview Spec: Stardew 双 NPC MVP 路线决策

## Metadata
- Interview ID: allgameinai-stack-decision-2026-04-27
- Rounds: 5
- Final Ambiguity Score: 10.7%
- Type: brownfield
- Generated: 2026-04-27
- Threshold: 0.2
- Status: PASSED

## Clarity Breakdown
| Dimension | Score | Weight | Weighted |
|-----------|-------|--------|----------|
| Goal Clarity | 0.93 | 0.35 | 0.3255 |
| Constraint Clarity | 0.90 | 0.25 | 0.2250 |
| Success Criteria Clarity | 0.88 | 0.25 | 0.2200 |
| Context Clarity | 0.82 | 0.15 | 0.1230 |
| **Total Clarity** |  |  | **0.8935** |
| **Ambiguity** |  |  | **0.1065** |

## Goal
以最快速度做出一个**真实桥接**的 Stardew Valley MVP，最低通过线是：
- 至少 **2 个 NPC** 接上真实游戏桥接链路
- 二者 **记忆不串、人格不串**
- 不是纯对话假象，而是可以证明 bridge / runtime / memory 隔离真实存在

在此目标下，路线结论是：**优先继续基于当前 Hermes Desktop 仓库实现，不切换到 `external/hermescraft-main` 作为主实现栈；后者只作为架构模式参考。**

## Constraints
- 速度优先，高于资产保留。
- 允许重写、允许大改、允许抛弃部分现有设计承诺。
- 不能接受“文档说有、实际没落地”的幻觉能力。
- MVP 必须触达真实 bridge / runtime / memory 边界，不能只是 UI 或 prompt 演示。
- 如果事实证明当前仓库更快达标，则继续押当前仓库。

## Non-Goals
- 不以保留 WinUI/UI 资产为第一优先级。
- 不把 `external/hermescraft-main` 的 Minecraft 代码直接迁移为 Stardew 主实现。
- 不先做复杂 UI 面板、完整社交图、完整经济系统。
- 不先追求多进程或分布式。
- 不把“全局 memory tool 存在”误当成“Stardew 双 NPC 记忆闭环已实现”。

## Acceptance Criteria
- [ ] 明确路线决定：当前仓库为主实现栈，`external/hermescraft-main` 仅作模式参考。
- [ ] 先验证并补齐当前仓库缺失的 Stardew 核心层，而不是先重平台迁移。
- [ ] MVP 的最低通过线固定为“双 NPC + 真实 bridge + 记忆/人格隔离”。
- [ ] 记忆能力验收以 NPC/runtime 级闭环为准，而不是仅凭 `MemoryTool` 与 `MemoryManager` 的存在。
- [ ] 后续实施计划必须优先覆盖 bridge、runtime、command service、identity isolation、trace 证据。

## Assumptions Exposed & Resolved
| Assumption | Challenge | Resolution |
|------------|-----------|------------|
| 当前项目“记忆功能根本没实现” | 核查源码 `src/memory/MemoryManager.cs`、`src/Tools/MemoryTool.cs`、`Desktop/HermesDesktop/App.xaml.cs:739` | 结论改为：**全局记忆基础件已实现，但 Stardew 双 NPC runtime 级记忆闭环未实现** |
| 要最快做出 MVP，可能必须换到 `hermescraft-main` | 对比当前仓库与 `external/hermescraft-main/README.md:34-43` 的架构和技术栈 | 结论改为：**`hermescraft-main` 更适合做架构参考，不适合作为 Stardew 直接主栈** |
| 当前仓库不值得继续投入 | 反向提问：若当前仓库其实更快，你是否仍愿意继续押这里？ | 用户确认：**如果当前仓库更快，就继续当前仓库** |
| MVP 可以先放宽到单 NPC 或纯对话演示 | 连续收窄成功标准 | 结论改为：**最低通过线必须是双 NPC 真实隔离** |

## Technical Context
### 已确认的当前仓库事实
- `src/memory/MemoryManager.cs:19`：记忆以目录为隔离边界，说明基础记忆系统真实存在。
- `Desktop/HermesDesktop/App.xaml.cs:739`：`MemoryTool` 已真实注册到 agent。
- `Desktop/HermesDesktop.Tests/Services/MemoryToolTests.cs:43` 起：已有记忆工具回归测试，证明至少修过保存/索引一致性问题。

### 已确认的当前仓库缺口
- 在 `src/` 中未找到 `NpcAutonomyLoop`、`NpcRuntimeFactory`、`StardewCommandService`、`ResourceClaimRegistry` 的实现。
- 这意味着当前仓库**缺的不是“有没有 memory tool”**，而是 Stardew 双 NPC MVP 所需的 runtime / bridge / command / isolation 核心层。

### 已确认的参考项目边界
- `external/hermescraft-main/README.md:34-43`：核心链路是 `Hermes Agent -> mc CLI -> bot/server.js HTTP API -> Mineflayer bot body -> Minecraft world`。
- 该项目可复用的是：独立 agent home、长会话 loop、任务状态轮询、聊天路由。
- 不可直接复用的是：Mineflayer / Minecraft 专用动作与世界模型。

### 路线判断
综合用户目标与现有证据，**继续修当前仓库**比**整体转向 `hermescraft-main`**更贴合“最快做出双 NPC 真实桥接 MVP”这一目标，因为：
1. 当前仓库与 SMAPI / C# 同构。
2. 当前仓库已有真实 memory 基础件，不必从零补记忆子系统。
3. `hermescraft-main` 虽有成熟模式，但换栈本身会引入 Node ↔ C# / SMAPI 的额外桥接成本。
4. 当前真正缺的是 Stardew runtime 层，而不是整个 agent 基础设施。

## Ontology (Key Entities)
| Entity | Type | Fields | Relationships |
|--------|------|--------|---------------|
| Hermes Desktop | brownfield system | .NET 10, WinUI 3, MemoryManager, Agent runtime | Contains Memory subsystem; candidate base for Stardew MVP |
| Memory subsystem | capability | MemoryManager, MemoryTool, desktop UI, implementation credibility | Part of Hermes Desktop; must be verified at runtime boundary |
| hermescraft-main | alternative stack | Node.js, Mineflayer, HTTP bridge, isolated HERMES_HOME | Alternative to Hermes Desktop; provides architecture patterns |
| Stardew MVP | target outcome | fastest validated prototype, real bridge loop, dual NPC isolation | Built primarily on Hermes Desktop; informed by hermescraft patterns |

## Ontology Convergence
| Round | Entity Count | New | Changed | Stable | Stability Ratio |
|-------|-------------|-----|---------|--------|----------------|
| 1 | 4 | 4 | 0 | 0 | - |
| 2 | 4 | 0 | 0 | 4 | 100% |
| 3 | 4 | 0 | 0 | 4 | 100% |
| 4 | 4 | 0 | 0 | 4 | 100% |
| 5 | 4 | 0 | 0 | 4 | 100% |

## Interview Transcript
<details>
<summary>Full Q&A (5 rounds)</summary>

### Round 1
**Q:** 你这次最想优先优化的是什么？
**A:** 最快做出MVP
**Ambiguity:** 41.25%

### Round 2
**Q:** 为了最快 MVP，你对路线切换的硬约束是哪一种？
**A:** 无硬约束
**Ambiguity:** 24.55%

### Round 3
**Q:** 下面哪一种最接近你会接受的 MVP？
**A:** 真实桥接闭环
**Ambiguity:** 21.00%

### Round 4
**Q:** 你认可的最低通过线是哪一种？
**A:** 双NPC真实隔离
**Ambiguity:** 20.45%

### Round 5
**Q:** 如果和你现在的直觉相反，当前仓库其实是更快的那条路，你会怎么选？
**A:** 快就继续当前仓库
**Ambiguity:** 10.65%
</details>
