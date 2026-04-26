# Deep Interview Spec: 星露谷多 NPC 村庄 - AllGameInAI

## Metadata
- Interview ID: allgameinai-stardew-2026-04-26
- Rounds: 6
- Final Ambiguity Score: 17.5%
- Type: brownfield
- Generated: 2026-04-26
- Threshold: 20%
- Status: PASSED

## Clarity Breakdown
| Dimension | Score | Weight | Weighted |
|-----------|-------|--------|----------|
| Goal Clarity | 0.85 | 0.35 | 0.298 |
| Constraint Clarity | 0.80 | 0.25 | 0.200 |
| Success Criteria | 0.90 | 0.25 | 0.225 |
| Context Clarity | 0.75 | 0.15 | 0.113 |
| **Total Clarity** | | | **0.836** |
| **Ambiguity** | | | **0.164 (16.4%)** |

## Goal

**改造 Hermes Desktop 项目，接入星露谷物语（Stardew Valley）游戏，实现多 NPC 村庄模式。**

核心目标：让 AI Agent 像真实玩家一样生活在星露谷世界中，每个 NPC 拥有独立的人格、记忆和自主决策能力。Agent 完全自主运行，无需人类持续下达指令。

**MVP 范围**：
- 1-3 个 NPC Agent 同时运行
- 基础能力：移动、采集资源、对话
- 人格系统：每个 NPC 有独立记忆和人格
- 技术验证：SMAPI mod + Hermes Desktop 单进程架构

**长期目标**
- 扩展到 10+ NPC 的大规模村庄
- 复杂社交网络和经济系统
- 多进程/分布式架构支持

## Constraints

### 技术约束
1. **技术栈**：SMAPI mod（原生 C#）+ Hermes Desktop（.NET 10 + WinUI 3）
2. **架构模式**：MVP 阶段采用单进程多 NPC 架构，在进程内做记忆/人格/会话逻辑隔离
3. **NPC 形式**：接管星露谷现有 NPC（如 Abigail、Sebastian），不创建新 NPC
4. **参考架构**：借鉴 hermescraft-main 的三层架构思想（Agent → CLI → HTTP API → Bot），但适配到 Hermes Desktop 的单宿主架构

### 行为约束
1. **日程冲突处理**：Agent 计划优先，但不能破坏重大事件（节日、剧情触发点）
2. **自主运行**：Agent 完全自主决策，无需人类持续干预
3. **记忆隔离**：每个 NPC 的记忆和人格完全独立，不共享私人记忆

### 性能约束（待细化）
1. MVP 阶段支持 1-3 个 NPC 并发
2. 后续扩展需考虑 10+ NPC 时的资源限制和降级策略
3. 对话响应延迟、tick rate 等运行时指标待定

## Non-Goals

明确不做的事情：
1. **不接管玩家控制**：Agent 只控制 NPC，不干预玩家角色
2. **不修改游戏核心机制**：不改变星露谷的时间系统、经济系统、战斗系统等核心玩法
3. **不创建新 NPC**：MVP 阶段只接管现有 NPC，不涉及美术资源、动画、新对话树的创建
4. **不做实时多人联机**：当前只考虑单机模式，不涉及多人服务器
5. **不做跨游戏通用框架**：MVP 专注星露谷，通用化留待后续迭代

## Acceptance Criteria

### MVP 必须实现（P0）
- [ ] **基础能力 - 移动**：NPC 可以在星露谷地图上自主寻路到任意可达地点
- [ ] **基础能力 - 采集**：NPC 可以采集基础资源（如野果、木材、矿石）
- [ ] **基础能力 - 对话**：NPC 可以与玩家和其他 NPC 进行上下文连贯的对话
- [ ] **人格系统 - 独立记忆**：每个 NPC 有独立的记忆存储，记住与玩家/其他 NPC 的互动历史
- [ ] **人格系统 - 独立人格**：每个 NPC 基于 SOUL.md 模板有不同的性格特征，对话风格符合人设
- [ ] **系统稳定性**：单进程运行 1-3 个 NPC 时，系统不崩溃、不卡死，可持续运行 1 小时以上
- [ ] **日程冲突处理**：Agent 可以覆盖 NPC 日常日程，但在节日/剧情事件时自动让位给原版逻辑

### 验证方法（P0）
- [ ] **移动验证**：手动指定目标地点，观察 NPC 是否能正确寻路到达
- [ ] **采集验证**：观察 NPC 是否能自主发现并采集资源，资源是否正确存入 NPC 背包
- [ ] **对话验证**：与 NPC 进行 3 轮以上对话，检查是否记住上一轮内容，是否符合人设
- [ ] **记忆隔离验证**：告诉 NPC A 一个秘密，检查 NPC B 是否不知道这个秘密
- [ ] **人格一致性验证**：同一个问题问 NPC A 和 NPC B，检查回答是否符合各自人设
- [ ] **稳定性验证**：运行 1 小时，检查内存占用、CPU 占用是否稳定，是否有崩溃/卡死

### 后续迭代（P1）
- [ ] NPC 间社交互动（聊天、送礼、建立关系）
- [ ] 自主目标规划（NPC 自己决定今天做什么）
- [ ] 经济系统参与（买卖物品、赚钱）
- [ ] 多进程架构支持（10+ NPC 时拆分为独立进程）

## Assumptions Exposed & Resolved

| Assumption | Challenge | Resolution |
|------------|-----------|------------|
| "模仿 hermescraft 架构" | hermescraft 是多进程独立 HERMES_HOME，但 Hermes Desktop 是单宿主架构 | MVP 先单进程逻辑隔离，后续扩展再拆分多进程 |
| "Agent 完全自主" | 与 NPC 原版日程冲突怎么办？ | Agent 计划优先，但节日/剧情事件时让位给原版逻辑 |
| "独立记忆和人格" | 如何在单进程内隔离？ | 每个 NPC 独立 MemoryManager 实例 + 独立 SOUL.md 配置 |
| "接管现有 NPC" | 是否需要美术资源？ | 不需要，复用原版 NPC 的模型、动画、对话树 |
| "SMAPI mod" | 与 .NET 10 WinUI 3 如何集成？ | SMAPI mod 作为桥接层，通过进程间通信或共享库与 Hermes Desktop 交互 |

## Technical Context

### 现有代码库（Hermes Desktop）

**架构**：.NET 10 + WinUI 3 桌面应用

**核心组件**：
- `src/Core/Agent.cs`：Agent 运行时，工具调用循环、并行执行（8 工作线程）、权限管理
- `src/memory/MemoryManager.cs`：持久化记忆系统，LLM 相关性选择、新鲜度警告
- `src/soul/SoulService.cs`：灵魂/人格系统，12 种模板（Default、Creative、Teacher、Researcher 等）
- `src/Tools/*.cs`：27+ 工具实现（bash、文件、web、浏览器、LSP、vision、TTS）
- `src/wiki/WikiManager.cs`：Wiki 知识库，SQLite FTS5 全文搜索
- `src/gateway/GatewayService.cs`：Telegram/Discord 原生 C# 适配器
- `src/skills/SkillManager.cs`：94 个技能系统

**扩展点**：
- `ITool` 接口：工具扩展
- `IPlatformAdapter` 接口：网关扩展
- `skills/` 目录：技能扩展
- `SOUL.md` 模板：人格扩展

**当前限制**：
- Agent 运行时更像单宿主架构，需要验证是否支持多实例并发
- 记忆系统需要适配 NPC 独立记忆的隔离策略
- 27+ 工具中大部分是通用工具，需要新增游戏专用工具（如 `GameActionTool`、`GameQueryTool`）

### 参考架构（HermesCraft）

**架构**：Hermes Agent → mc CLI → HTTP API → Mineflayer Bot → Minecraft

**关键模式**：
- 三层架构：Agent 层（决策）、CLI 层（命令接口）、Bot 层（游戏执行）
- 每个 NPC 独立 HERMES_HOME + 独立端口（如 3001、3002、3003）
- 公平感知系统：LOS（视线）、声音范围、场景总结
- 社交系统：聊天路由、私聊、窃听
- 提示词模板：`SOUL-minecraft.md`、`SOUL-civilization.md`

**启动流程**：
```bash
# 单 agent
hermescraft.sh → 启动 bot/server.js (端口 3001) → 连接 Minecraft
# 多 agent
civilization.sh → 启动多个 bot/server.js (端口 3001, 3002, ...) → 共享 Minecraft 世界
```

**数据流**：
```
Hermes Agent 执行 `mc status`
  → CLI 发送 GET /status
  → server.js 返回 bot 状态
  → Agent 决策
  → `mc collect oak_log 5`
  → POST /action/collect
  → Mineflayer 执行采集
```

### 星露谷技术接口（待调研）

**SMAPI 能力**（需要验证）：
- NPC 行为钩子（Schedule、Elevation、Dialogue）
- 地图事件监听
- 物品/背包操作
- 时间系统集成
- 节日/剧情事件检测

**集成方案（待设计）**：
1. **方案 A**：SMAPI mod 作为 HTTP API 服务器，Hermes Desktop 通过 HTTP 调用
2. **方案 B**：SMAPI mod 与 Hermes Desktop 共享 .NET 库，通过进程间通信（Named Pipe、Shared Memory）
3. **方案 C**：SMAPI mod 直接引用 Hermes.Core.dll，在游戏进程内运行 Agent

**推荐方案**：方案 A（HTTP API），理由：
- 隔离性好，游戏崩溃不影响 Hermes Desktop
- 调试方便，可以独立测试 SMAPI mod 和 Hermes Desktop
- 参考 hermescraft 的成熟模式

## Ontology (Key Entities)

| Entity | Type | Fields | Relationships |
|--------|------|--------|---------------|
| HermesAgent | core/agent | id, personality, memory, capabilities, world_position | HAS_MEMORY (MemoryManager), HAS_PERSONALITY (PersonalityProfile), EXECUTES (Action), OBSERVES (GameState) |
| StardewNPCBridge | adapter/bridge | npc_id, vanilla_script, schedule, override_behavior | WRAPS (NPC), PROXIES (HermesAgent), RECEIVES (GameEvent) |
| GameWorld | simulation/world | map_state, npc_registry, time_tick | CONTAINS (NPC), EMITS (GameEvent) |
| PersonalityProfile | agent/personality | traits, goals, biases | INFLUENCES (Decision), STORED_IN (MemoryManager) |
| MemoryManager | memory/manager | episodic_buffer, semantic_store, retention_policy | HOLDS (Memory), BELONGS_TO (HermesAgent) |
| NPC | game/npc | name, schedule, dialogue_tree, friendship_points | WRAPPED_BY (StardewNPCBridge), IN (GameWorld) |
| Action | agent/action | type (MOVE/GATHER/SPEAK), target, parameters | EXECUTED_BY (HermesAgent), VALIDATED_BY (GameWorld) |

## Ontology Convergence

| Round | Entity Count | New | Changed | Stable | Stability Ratio |
|-------|-------------|-----|---------|--------|----------------|
| 1 | 10 | 10 | - | - | N/A |
| 2 | 10 | 0 | 0 | 10 | 100% |

**收敛分析**：
- 第 1 轮识别出 10 个核心实体
- 第 2 轮实体完全稳定，无新增、无变更
- 实体模型在第 2 轮即达到 100% 稳定，说明需求边界清晰

## Interview Transcript

<details>
<summary>Full Q&A (6 rounds)</summary>

### Round 1
**Q:** 你希望 Agent 在星露谷游戏中扮演什么角色？
**A:** 多 NPC 村庄
**Ambiguity:** 100% → 75% (Goal: 0.75, Constraints: 0.45, Criteria: 0.60, Context: 0.55)
**Weakest:** Constraint Clarity

### Round 2
**Q:** 你计划在星露谷中同时运行多少个 NPC Agent？
**A:** 初步 MVP 考虑实现 1-3 个，然后看情况，目标是大规模
**Ambiguity:** 75% → 55% (Goal: 0.80, Constraints: 0.50, Criteria: 0.65, Context: 0.60)
**Weakest:** Constraint Clarity

### Round 3
**Q:** 你打算如何让 Hermes Agent 与星露谷游戏通信？
**A:** SMAPI mod（原生 C#）
**Ambiguity:** 55% → 40% (Goal: 0.82, Constraints: 0.60, Criteria: 0.70, Context: 0.65)
**Weakest:** Constraint Clarity

### Round 4
**Q:** Agent 控制的 NPC 在游戏中以什么形式存在？
**A:** 接管现有 NPC
**Ambiguity:** 40% → 30% (Goal: 0.83, Constraints: 0.65, Criteria: 0.75, Context: 0.68)
**Weakest:** Constraint Clarity

### Round 5
**Q:** 你（用户）与 NPC 村庄如何交互？
**A:** 我打算和 hermescraft-main 方案保持一致，agent 就像生活在星露谷世界的人一样
**Ambiguity:** 30% → 22% (Goal: 0.85, Constraints: 0.70, Criteria: 0.80, Context: 0.70)
**Weakest:** Context Clarity

### Round 6
**Q:** 对于星露谷多 NPC，你更倾向哪种隔离方式？（单进程 vs 多进程）
**推荐:** MVP 单进程，后续拆分
**A:** 接受推荐
**Ambiguity:** 22% → 17.5% (Goal: 0.85, Constraints: 0.80, Criteria: 0.85, Context: 0.75)
**Weakest:** Context Clarity

### Round 7
**Q:** 当 Agent 计划与 NPC 原版日程冲突时，怎么处理？
**A:** Agent 计划优先，但不能破坏重大事件（节日、剧情）
**Ambiguity:** 17.5% → 16.4% (Goal: 0.85, Constraints: 0.80, Criteria: 0.90, Context: 0.75)
**Status:** PASSED (低于 20% 阈值)

</details>

## Architecture Recommendations

### MVP 架构（推荐）

```
┌─────────────────────────────────────────────────────────────┐
│ Hermes Desktop (单进程)                                      │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Agent 运行时                                             │ │
│ │ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐    │ │
│ │ │ NPC Agent 1  │ │ NPC Agent 2  │ │ NPC Agent 3  │    │ │
│ │ │ (Abigail)    │ │ (Sebastian)  │ │ (Penny)      │    │ │
│ │ │              │ │              │ │              │    │ │
│ │ │ Memory 1     │ │ Memory 2     │ │ Memory 3     │    │ │
│ │ │ SOUL 1       │ │ SOUL 2       │ │ SOUL 3       │    │ │
│ │ └──────────────┘ └──────────────┘ └──────────────┘    │ │
│ └─────────────────────────────────────────────────────────┘ │
│                           ↕ HTTP API                         │
└─────────────────────────────────────────────────────────────┘
                            ↕
┌─────────────────────────────────────────────────────────────┐
│ SMAPI Mod (游戏进程内)                                       │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ HTTP Server (端口 3000)                                  │ │
│ │ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐    │ │
│ │ │ NPC Bridge 1 │ │ NPC Bridge 2 │ │ NPC Bridge 3 │    │ │
│ │ │ (Abigail)    │ │ (Sebastian)  │ │ (Penny)      │    │ │
│ │ └──────────────┘ └──────────────┘ └──────────────┘    │ │
│ └─────────────────────────────────────────────────────────┘ │
│                           ↕ SMAPI API                        │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Stardew Valley Game                                      │ │
│ │ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐    │ │
│ │ │ NPC Abigail  │ │ NPC Sebastian│ │ NPC Penny    │    │ │
│ │ └──────────────┘ └──────────────┘ └──────────────┘    │ │
│ └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

**关键设计点**：
1. **单进程多 Agent**：Hermes Desktop 在一个进程内运行 1-3 个 Agent 实例
2. **逻辑隔离**：每个 Agent 有独立的 MemoryManager、PersonalityProfile、会话状态
3. **HTTP 桥接**：SMAPI mod 提供 HTTP API，Hermes Desktop 通过 HTTP 调用游戏操作
4. **NPC Bridge**：SMAPI mod 内部为每个被接管的 NPC 创建 Bridge 对象，拦截原版行为
5. **事件驱动**：游戏事件（如玩家对话、时间流逝��通过 HTTP 推送到 Hermes Desktop

### 后续扩展架构（10+ NPC）

```
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ Hermes       │ │ Hermes       │ │ Hermes       │
│ Desktop 1    │ │ Desktop 2    │ │ Desktop 3    │
│ (NPC 1-3)    │ │ (NPC 4-6)    │ │ (NPC 7-9)    │
└──────────────┘ └──────────────┘ └──────────────┘
       ↕                ↕                ↕
┌─────────────────────────────────────────────────┐
│ SMAPI Mod (负载均衡 + 路由)                      │
└─────────────────────────────────────────────────┘
       ↕
┌─────────────────────────────────────────────────┐
│ Stardew Valley Game                              │
└─────────────────────────────────────────────────┘
```

**迁移路径**：
1. MVP 验证单进程架构可行性
2. 当 NPC 数量超过 3 个时，评估性能瓶颈
3. 如果单进程无法支撑，拆分为多进程架构
4. SMAPI mod 增加负载均衡和路由逻辑，将不同 NPC 的请求路由到不同的 Hermes Desktop 实例

## Next Steps

### 立即行动（Phase 0）
1. **调研 SMAPI API**：
   - 阅读 SMAPI 官方文档，了解 NPC 行为钩子
   - 查找现有 mod（如 ChatCommands、AutomateAPI）的实现
   - 确认 Schedule、Dialogue、Pathfinding 的可��性
2. **设计 HTTP API 接口**：
   - 定义 Agent → SMAPI 的命令接口（如 `/npc/move`、`/npc/gather`、`/npc/speak`）
   - 定义 SMAPI → Agent 的事件推送接口（如 `/event/dialogue`、`/event/time_tick`）
3. **验证 Hermes Desktop 多实例支持**：
   - 阅读 `src/Core/Agent.cs`，确认是否支持同一进程内多个 Agent 实例
   - 测试 MemoryManager 的隔离性（不同 Agent 的记忆是否会混淆）

### MVP 开发（Phase 1）
1. **创建 SMAPI mod 骨架**：
   - 初始化 SMAPI mod 项目
   - 实现 HTTP Server（使用 ASP.NET Core Minimal API）
   - 实现 NPC Bridge 基础类
2. **扩展 Hermes Desktop**：
   - 新增 `src/game/` 目录
   - 实现 `StardewActionTool`（调用 SMAPI HTTP API）
   - 实现 `StardewQueryTool`（查询游戏状态）
3. **集成测试**：
   - 启动星露谷 + SMAPI mod
   - 启动 Hermes Desktop，创建 1 个 NPC Agent
   - 验证 Agent 可以控制 NPC 移动

### 验证与迭代（Phase 2）
1. 逐步增加 NPC 数量（1 → 2 → 3）
2. 验证记忆隔离、人格一致性
3. 测试日程冲突处理（节日事件）
4. 性能测试（内存、CPU、响应延迟）

### 扩展准备（Phase 3）
1. 评估单进程性能瓶颈
2. 设计多进程架构方案
3. 实现 SMAPI mod 的负载均衡和路由

## Risk & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| SMAPI API 能力不足（无法完全控制 NPC 行为） | High | Medium | 提前调研 SMAPI 文档和现有 mod，必要时使用 Harmony 补丁直接修改游戏代码 |
| 单进程性能瓶颈（3 个 NPC 已卡顿） | Medium | Low | MVP 先验证 1 个 NPC，逐步增加；提前设计多进程迁移路径 |
| 记忆隔离失败（NPC 之间记忆混淆） | High | Low | 单元测试验证 MemoryManager 隔离性；每个 Agent 使用独立的内存命名空间 |
| 日程冲突处理复杂（节日/剧情事件检测困难） | Medium | Medium | 先实现简单规则（硬编码节日日期），后续优化为事件监听 |
| HTTP 通信延迟（Agent 决策慢） | Low | Medium | 使用异步 HTTP 调用；必要时改为 Named Pipe 或 Shared Memory |

## Success Metrics

### MVP 成功指标
- [ ] 1 个 NPC Agent 可以在星露谷中自主移动、采集、对话
- [ ] 3 个 NPC Agent 同时运行，记忆和人格完全隔离
- [ ] 系统稳定运行 1 小时，无崩溃、无卡死
- [ ] 节日事件时，Agent 自动让位给原版逻辑

### 长期成功指标
- [ ] 10+ NPC Agent 同时运行，性能稳定
- [ ] NPC 之间形成复杂社交网络（友谊、恋爱、冲突）
- [ ] Agent 可以自主制定长期目标（如"赚 10000 金币"、"与 Abigail 结婚"）
- [ ] 玩家无法区分 Agent 控制的 NPC 和原版 NPC

## References

- [Hermes Desktop GitHub](https://github.com/RedWoodOG/Hermes-Desktop)
- [HermesCraft Reference](D:\GitHubPro\Hermes-Desktop\external\hermescraft-main)
- [SMAPI Documentation](https://stardewvalleywiki.com/Modding:Index)
- [Stardew Valley Wiki](https://stardewvalleywiki.com/)
