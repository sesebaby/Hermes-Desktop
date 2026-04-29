# Deep Interview Spec: Stardew 多 NPC 改造面梳理

## Metadata
- Interview ID: allgameinai-stardew-modification-map-2026-04-29
- Profile: standard, continued manually after `omx question` was unavailable in this shell
- Rounds: 5
- Final Ambiguity Score: ~14%
- Type: brownfield
- Generated: 2026-04-29
- Revised: 2026-04-29
- Threshold: 0.2
- Status: PASSED
- Prior Specs:
  - `.omc/specs/deep-interview-stardew-allgameinai.md`
  - `.omc/specs/deep-interview-stardew-route-decision-2026-04-27.md`
- Evidence context:
  - `.omx/context/stardew-modification-map-review-20260429T032420Z.md`
  - `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md`
  - `docs/superpowers/plans/2026-04-29-cross-game-npc-runtime-architecture.md`

## Executive Verdict

方案主方向正确，但必须按“平衡推荐”理解：

**先做真实 Stardew 双 NPC MVP，同时切清楚 core / Stardew 边界；不要在第一阶段做完整跨游戏平台。**

对小白最重要的判断是：
- 当前 Hermes Desktop 是合适主栈，不需要切到 `external/hermescraft-main` 当主实现。
- 当前仓库已有 Agent、memory、soul、transcript 等基础件，但**还没有**多 NPC runtime、game contract、Stardew adapter、SMAPI bridge、NPC pack loader。
- 所以第一阶段不是“调用现成能力拼起来”，而是先补最小闭环：双 NPC、真实 bridge、独立实例、独立 memory/soul/session、trace 证据。
- 跨游戏方向不是错，但第一阶段只做最小通用边界，不做完整跨游戏框架。

## Clarity Breakdown

| Dimension | Score | Weight | Weighted |
|-----------|-------|--------|----------|
| Goal Clarity | 0.95 | 0.25 | 0.2375 |
| Outcome Clarity | 0.92 | 0.20 | 0.1840 |
| Scope Clarity | 0.90 | 0.20 | 0.1800 |
| Constraint Clarity | 0.87 | 0.15 | 0.1305 |
| Success Criteria | 0.93 | 0.10 | 0.0930 |
| Context Clarity | 0.88 | 0.10 | 0.0880 |
| **Total Clarity** |  |  | **0.9130** |
| **Ambiguity** |  |  | **0.0870-0.14 residual** |

Residual ambiguity remains because exact NPC cast, transport details, and SMAPI command surface still need implementation-time verification.

## Decision Locks

### 1. Review Standard
Use the balanced standard:

> First prove a real Stardew two-NPC MVP, while keeping the minimum clean boundary between generic NPC runtime and Stardew-specific adapter code.

### 2. Phase 1 Must Not Become a Big Platform
Earlier material said “MVP 不做跨游戏通用框架”; this document now clarifies the non-contradictory meaning:

- Do create a small `core` boundary so Stardew logic does not enter generic runtime code.
- Do not pre-design a full multi-game platform before Stardew works.
- Expand the core only when Stardew MVP needs the field or method.

### 3. Direct Review Authority
When evidence shows a section is misleading, over-abstracted, or in the wrong order, future planning agents may rewrite or reorder it. Do not preserve the old plan out of politeness.

### 4. Full Roadmap Preservation
This document must keep the full roadmap. Phase 1 can be MVP-only, but later goals must stay visible so they are not forgotten.

## Goal

基于当前 Hermes Desktop 仓库，而不是换栈，做出 Stardew Valley 多 NPC 村庄 MVP 的改造地图。

MVP 的核心目标：
- 至少 2 个 Stardew NPC 接入真实 SMAPI bridge。
- 一个 NPC = 一个独立 Agent 实例。
- 每个 NPC 拥有独立 session、memory、soul、runtime state。
- 能证明两个 NPC 的人格和记忆不会串线。
- 能通过 trace 证明 bridge / command / memory / session 是真实闭环，不是文档或 prompt 假象。

长期目标：
- 扩展到 3+、10+ NPC。
- 增加更强自主规划、世界协调、社交网络、经济系统。
- 在必要时演进到多进程或分布式。
- 在 Stardew 跑通后再复用最小 core 边界接入其他游戏。

## Constraints

- 继续以当前 Hermes Desktop 仓库为主实现栈，不切到 `external/hermescraft-main` 作为主栈。
- `external/hermescraft-main` 只作为架构参考：独立 home、长会话 agent、HTTP bridge、工具观察世界、task status、后台任务与冲突处理。
- 每个 NPC 必须是真独立实例，不接受“一个总 Agent + 多人格 prompt 切换”的伪隔离。
- 允许对当前项目做结构性改造，不再把“尽量少改项目”作为前置约束。
- 第一阶段采用 in-process host 更现实，但 `core runtime` API 不能被单进程写死。
- `core` 只放最小通用 NPC runtime、namespace、scheduler、trace、game contract。
- `Stardew` 只能实现 bridge / world adapter / action mapping / SMAPI semantics。
- NPC 资料必须独立，并可通过资料包动态加载。
- 在同一游戏内新增受支持 NPC 时，目标是不改 core。
- 节日、剧情、玩家不可控等重大原版状态必须优先于 Agent 计划。
- 所有“已有能力”表述必须有代码证据；否则标为新增设计约束。

## Non-Goals

### Phase 1 Non-Goals
第一阶段明确后置以下内容：

- 不做完整漂亮的桌面调试 UI。
- 不做复杂世界协调层、社交网络、经济系统。
- 不支持很多 NPC；第一阶段只管 2 个。
- 不做多进程 / 分布式；一 NPC 一进程以后再说。
- 不做完整跨游戏框架；只保留清晰边界，不做过度泛化。
- 不做高级自主规划；先做简单 tick loop：观察、决策、执行、记录。

### Global Non-Goals
- 不把当前 agent profile 功能误当成多 NPC runtime。
- 不把全局 `MemoryTool` / `MemoryManager` 的存在误当成 NPC 级记忆闭环。
- 不把 UI 演示或 prompt 演示当成真实游戏桥接。
- 不把 Stardew 语义写进 `core runtime`。
- 不把 hermescraft 的 Minecraft 动作原样搬到 Stardew。

## Acceptance Criteria

### Phase 1 MVP: 双 NPC 真实隔离演示
第一阶段验收以“真实可证明”为准：

- [ ] 至少两个 NPC runtime 同时存在，例如 Haley / Penny，或资料包指定的两个首发 NPC。
- [ ] 每个 NPC 是独立 `Agent` 实例，而不是共享单 `_currentSession`。
- [ ] 每个 NPC 都有独立 `sessionId`、memory namespace、soul namespace、transcript namespace、runtime state。
- [ ] 每个 NPC 的 persona 来自固定 seed pack / soul 文件，不允许实现阶段临场生成。
- [ ] 两个 NPC 都能通过真实 SMAPI bridge 执行至少一个简单命令或对话闭环。
- [ ] 告诉 NPC A 一个秘密，NPC B 不知道；日志能证明读写发生在不同 namespace。
- [ ] 每个 bridge command 都能串起 `traceId` 或等价证据：NPC -> command -> SMAPI bridge -> result -> transcript/activity。
- [ ] Stardew adapter 与 core runtime 有明确边界，Stardew 地图、SMAPI、节日、NPC 名字、动作细节不进入 core。
- [ ] 文档或 UI 不能替代验收；必须有可运行验证脚本、日志或 replay 证据。

### Full Roadmap Acceptance
后续阶段仍要覆盖：

- [ ] 同一游戏内新增 NPC 时，若复用既有能力集，优先通过资料包完成，不改 core。
- [ ] `NpcRuntimeSupervisor` 支持生命周期、tick、调度、暂停、恢复、关闭。
- [ ] `NpcAutonomyLoop` 支持持续观察、决策、执行、观察结果记录。
- [ ] 世界协调层避免多个 NPC 抢同一资源、同一目标 tile、同一互动目标。
- [ ] 节日、剧情、玩家不可控、跨天状态有统一 override policy。
- [ ] 最小调试面板可查看每 NPC 状态、memory、transcript、action、bridge health。
- [ ] 后续可切到多进程 topology 而不重写 core runtime / game contract。

## Technical Context

### 当前仓库事实
- `Desktop/HermesDesktop/Services/HermesChatService.cs`：桌面层是 in-process agent execution，适合作为首个宿主基础。
- `HermesChatService` 当前只有一个 `_currentSession`，说明桌面聊天主路径仍是单活跃会话模型。
- `src/Core/Agent.cs`：Agent 支持注入 `MemoryManager`、`TranscriptStore`、`ContextManager`、`SoulService`，且具备工具调用循环。
- `src/Core/Agent.cs` 默认有 `MaxToolIterations`，说明游戏里的持续自治 loop 必须单独设计，不能假设一次 `ChatAsync` 就能长期生活。
- `src/transcript/TranscriptStore.cs`：SQLite transcript store 支持多 session 和 `sessionSource`，可作为 NPC transcript 隔离底座。
- `src/memory/MemoryManager.cs`：memory 以目录为边界，具备隔离潜力；但当前 Desktop DI 注册的是全局 singleton。
- `src/soul/SoulService.cs`：默认读取全局 `SOUL.md`、`USER.md`、journal、project rules；需要 NPC namespace 化。
- `src/soul/AgentProfile.cs`：激活 profile 会写回全局 `SOUL.md`，这是切换，不是并存。
- `src/agents/AgentService.cs`：subagent 基础设施偏短生命周期任务代理，不是长期 NPC supervisor。
- 当前仓库未发现已落地的 `src/runtime/`、`src/game/core/`、`src/games/stardew/`、`content/npc-packs/`、`NpcRuntimeSupervisor`、`IGameAdapter`、`StardewCommandService`、`NpcAutonomyLoop`、`WorldCoordinationService`。

### Recent Git Direction
- `8434bd10 Retire non-game surfaces for the NPC runtime`：仓库已经从通用桌面 agent 收缩到 NPC runtime 方向。
- `15e2b6bf Retire non-shipping bundled skills behind a manifest-driven reconcile`：裁掉非核心 bundled skills，减轻非游戏面包袱。
- `48f1a406 Separate skills from souls to match reference discovery boundaries`：soul 与 skills 边界拆开，有利于 per-NPC soul namespace。
- `4dae1e42 Constrain game-runtime subtraction around NPC life`：近期决策已围绕 NPC life / game runtime 最小减法收敛。

### Evidence Boundary
不要混淆以下三类东西：

- 当前已有：Agent、tool loop、memory 基础件、soul 基础件、transcript store、activity 基础。
- 参考证明：hermescraft 的 long-running agent + game bridge 模式，Stardew mod 的 SMAPI 生命周期和游戏状态门控。
- 新增设计：NPC runtime supervisor、NPC namespace builder、Stardew adapter、SMAPI bridge contract、trace id 串联、resource claim、NPC pack loader。

## Required Modifications

### P0: Phase 1 MVP 必做

#### 1. 多 NPC Runtime Supervisor
新增长期存活宿主管理层，替代桌面聊天路径的单 `_currentSession` 模型。

职责：
- 维护 `NpcRuntimeRegistry`。
- 启停 2 个 NPC Agent 实例。
- 为每个 NPC 维护独立 session id、turn loop、health state。
- 支持暂停、恢复、关闭和基础恢复。

落点：
- `src/runtime/`
- `Desktop/HermesDesktop/Services/` 的宿主接入层

#### 2. NPC Identity / Memory / Soul Namespace
把当前全局 soul/memory 体系拆成“公共规则 + NPC 私有层”。

建议：
- 每个 NPC 一个 runtime home，例如 `npcs/{gameId}/{saveId}/{npcId}/{profileId}/`。
- 私有文件包括 `SOUL.md`、`MEMORY.md`、`USER.md`、`state.db`、trace/activity。
- `SoulService`、`MemoryManager`、`TranscriptStore` 必须由 namespace 装配，不能直接复用全局 singleton。
- 私人记忆默认不可被其他 NPC 读取；公共世界事实另设共享层。

#### 3. 最小 Game Contract + Stardew Adapter
先切两层，不做完整平台：

- `src/game/core/`：只放最小通用接口、DTO、NPC pack schema。
- `src/games/stardew/`：只放 Stardew / SMAPI 实现。

P0 最小接口：
- `IGameCommandService`
- `IGameQueryService`
- `IGameEventSource`
- `GameAction`
- `GameObservation`
- `WorldSnapshot`
- `NpcPackManifest`
- `INpcPackLoader`

P0 Stardew 实现：
- `StardewCommandService`
- `StardewQueryService`
- `StardewEventIngestor`
- `SmapiModApiClient`
- `StardewNpcBindingResolver`

#### 4. 真实 SMAPI Bridge 闭环
SMAPI mod 或 bridge 层必须提供真实命令 / 查询 / 事件闭环。

第一阶段可很小：
- health
- world/time/status query
- NPC status query
- speak/dialogue or simple action command
- command status/result

硬规则：
- HTTP handler 只能读缓存、入队命令、返回 command id。
- 真正读写 `Game1` / NPC / map 对象必须在 SMAPI game loop event 中执行。
- 除 health 外，本机 bridge 需要 token 或等价本机安全约束。

#### 5. Trace / Evidence Layer
MVP 验收必需，不是锦上添花。

需要：
- 每 NPC 独立 action log / memory log / event log。
- bridge command trace id。
- 隔离验证命令或测试：A 的秘密不会被 B 读到。
- 验证脚本或 replay 记录，能证明 bridge 真实调用。

### P1: Phase 2 稳定性与可调试性

#### 6. 简单 NpcAutonomyLoop
第一阶段可只做最小 loop，第二阶段增强：
- 观察 -> 决策 -> 执行 -> 记录结果。
- 支持 cancellation、cooldown、max turn budget。
- 支持节日 / 剧情 / 玩家不可控打断。
- 支持路径失败后的简单重试或停止。

#### 7. 最小 Desktop 调试面板
不是完整 UI，但要能调试：
- 查看每个 NPC 的 runtime 状态。
- 查看最近 memory / transcript / action。
- 手动暂停、恢复、单步。
- 查看 bridge health。

### P2: Phase 3 世界协调与自治增强

#### 8. World State 与冲突协调
多个 NPC 同时运行后需要共享世界协调。

建议新增：
- `WorldSnapshotProvider`
- `ResourceClaimRegistry`
- `NpcActionArbiter`
- `ScheduleOverridePolicy`

用途：
- 避免两个 NPC 抢同一资源。
- 避免覆盖位置 / 对话状态。
- 定义 Agent 计划与原版日程、剧情事件优先级。

#### 9. 高级自主规划
后续再做：
- 多步计划。
- 长期目标。
- NPC 间社交关系。
- 经济 / 生产行为。
- 玩家不在场的 NPC 群聊。

### P3: Phase 4 拓扑与跨游戏扩展

后续再考虑：
- 一 NPC 一进程。
- 多进程 worker host。
- 多游戏 adapter。
- 更完整 game contract。
- 分布式或 10+ NPC 资源调度。

## Recommended Build Order

### Phase 1: 双 NPC 真实隔离 MVP
1. 定义最小 `NpcRuntimeSupervisor` / `NpcRuntimeInstance` / `NpcNamespace`。
2. 让两个 NPC 各自拥有独立 `Agent + session + memory + soul + transcript`。
3. 定义最小 `game core contract`，只包含 MVP 需要的 command/query/event。
4. 定义 `NpcPackManifest` 和两个首发 NPC seed pack。
5. 实现 `StardewAdapter` shell 与最小 SMAPI bridge。
6. 注册 Stardew typed tools，让 NPC 通过工具发出真实 bridge 命令。
7. 做双 NPC 隔离验证：A 的秘密不会被 B 读到。
8. 做 trace 验证：NPC -> command -> SMAPI -> result -> transcript/activity。

### Phase 2: 稳定与调试
1. 强化 `NpcAutonomyLoop`。
2. 增加最小 Desktop runtime panel。
3. 增加 bridge health、command retry、failure reason。
4. 增加节日 / 剧情 / 玩家不可控状态门控。

### Phase 3: 协调与自治
1. 增加 `WorldCoordinationService`。
2. 增加 resource claim / action arbiter。
3. 增加 NPC 间对话和玩家在场群聊。
4. 增加更复杂任务计划。

### Phase 4: 扩展
1. 支持 3+ NPC。
2. 支持更多 Stardew 行为能力。
3. 支持多进程 host topology。
4. 在 Stardew 稳定后再提取可复用 multi-game core。

## Correctness Review Notes

### Direction Correct
- 用 Hermes Desktop 作为主栈是正确的：C# / .NET 与 SMAPI 更同构，且已有 Agent/memory/soul/transcript 基础。
- 一 NPC 一 Agent 实例是正确的：否则无法证明人格和记忆隔离。
- NPC pack 是正确方向：新增 NPC 不应回头改 core。
- game contract + Stardew adapter 分层是正确方向：避免 Stardew 逻辑污染 core。
- trace/evidence 必须一等公民：否则会回到“文档说有、实际没闭环”。

### Needs Correction
- 不能把“core runtime 通用化”理解成第一阶段完整跨游戏框架。
- 不能把 `AgentProfile` 误认为可并发 NPC 人格系统。
- 不能把全局 `MemoryManager` 误认为 per-NPC memory 隔离。
- 不能第一阶段就做完整 UI、复杂协调、多进程、社交经济系统。
- 不能让 CLI、typed tools、UI 各自调用 SMAPI，必须经同一个 `StardewCommandService` / bridge contract。

## Assumptions Exposed & Resolved

| Assumption | Challenge | Resolution |
|------------|-----------|------------|
| 当前“多 agent”能力可直接承载多 NPC 并存 | 检查 `HermesChatService` 与 `AgentProfile` | 当前更像单会话 + profile 切换，不是多 NPC 并存 runtime |
| 共享一个 Agent 只靠 prompt/memory 标签也能过 MVP | 追问 NPC 在系统里到底是什么实例 | MVP 必须是一 NPC 一独立 Agent 实例 |
| 现有 memory/soul 子系统已经天然支持 NPC 隔离 | 检查 `MemoryManager`、`SoulService`、Desktop DI | 现有能力偏全局用户 / 项目导向，需要 NPC namespace 装配 |
| Stardew 的 world / action 语义可以直接写进 core | 对照未来多游戏目标 | core 只能定义最小 game contract，Stardew 只能作为 adapter 实现 |
| 第一阶段必须做完整跨游戏框架 | 对照“最快真实双 NPC MVP”目标 | 第一阶段只切边界，不做大平台 |
| 文档只写第一阶段就够 | 用户要求不要忘记后续 | 文档保留全量 roadmap，但 Phase 1 明确只做 MVP |

## Ontology (Key Entities)

| Entity | Type | Fields | Relationships |
|--------|------|--------|---------------|
| NpcAgentRuntime | core runtime | npcId, sessionId, memoryNamespace, soulNamespace, status | Hosted by supervisor; drives one NPC |
| NpcRuntimeSupervisor | host/runtime | registry, scheduler, recovery, health | Owns many NpcAgentRuntime instances |
| NpcNamespace | storage boundary | soulPath, memoryPath, transcriptDb, config | Belongs to one NpcAgentRuntime |
| NpcPackManifest | content contract | gameId, npcId, profileId, soulFile, memorySeed, capabilities | Creates NPC runtime input |
| GameRuntimeContract | core boundary | commandService, queryService, eventSource, DTOs | Implemented by concrete game adapters |
| StardewAdapter | game adapter | commands, queries, events, transport | Implements GameRuntimeContract via SMAPI |
| SmapiBridge | game bridge | health, commands, queries, events | Executes real Stardew side effects |
| TraceEvidenceLayer | verification | actionLog, memoryLog, eventLog, replay | Proves isolation and real bridge execution |
| WorldCoordinationLayer | shared world service | resourceClaims, arbitration, schedulePolicy | Later coordinates multiple NPC runtimes |

## Interview Transcript

<details>
<summary>Full Q&A</summary>

### Round 1
**Q:** MVP 里的“一个 NPC”在系统里到底是什么？  
**A:** 独立 Agent 实例。  
**Resolution:** 一 NPC 一 Agent 实例。

### Round 2
**Q:** 跨游戏通用边界应该收敛到哪一层？  
**A:** 运行时通用化。  
**Resolution:** core 做最小 runtime/game contract，Stardew 做 adapter。

### Round 3
**Q:** 审查方案时优先按哪种标准判断正确？  
**A:** 按推荐。  
**Resolution:** 平衡推荐：真实 Stardew 双 NPC MVP + 清晰 core/Stardew 边界。

### Round 4
**Q:** 第一版愿意后置哪些内容？  
**A:** 7，以上都可以后置。  
**Resolution:** UI、复杂协调、很多 NPC、多进程、完整跨游戏框架、高级规划全部后置。

### Round 5
**Q:** 可以如何处理发现的问题？  
**A:** 直接给出修正版判断，并可以直接修改文档；内容要全，但可以先只做 MVP。  
**Resolution:** 本文档保留全量路线，明确 Phase 1 只做真实双 NPC MVP。

</details>
