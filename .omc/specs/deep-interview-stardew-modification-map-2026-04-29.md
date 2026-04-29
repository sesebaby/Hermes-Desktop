# Deep Interview Spec: Stardew 多 NPC 改造面梳理

## Metadata
- Interview ID: allgameinai-stardew-modification-map-2026-04-29
- Profile: standard, continued manually after `omx question` was unavailable in this shell and user waived the tmux renderer requirement
- Rounds: 6
- Final Ambiguity Score: ~14%
- Type: brownfield
- Generated: 2026-04-29
- Revised: 2026-04-29
- Threshold: 0.2
- Status: PASSED WITH CURRENT-CODE CORRECTIONS
- Prior Specs:
  - `.omc/specs/deep-interview-stardew-allgameinai.md`
  - `.omc/specs/deep-interview-stardew-route-decision-2026-04-27.md`
- Evidence context:
  - `.omx/context/stardew-modification-map-review-20260429T032420Z.md`
  - `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md`
  - `docs/superpowers/plans/2026-04-29-cross-game-npc-runtime-architecture.md`
- Important source rule:
  - `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md` is now a reference and constraint library, not a current-code fact source. Its design constraints remain valuable, but its code evidence must be rechecked against the current repository before implementation.

## Executive Verdict

方案主方向正确，但必须按“平衡推荐”理解：

**模仿D:\Projects\Hermes-Desktop\external\hermescraft-main，先做真实 Stardew 双 NPC MVP，同时切清楚 core / Stardew 边界；不要在第一阶段做完整跨游戏平台。**

对小白最重要的判断是：
- 当前 Hermes Desktop 是合适主栈，不需要切到 `external/hermescraft-main` 当主实现。
- 当前仓库已有 Agent、memory、soul、transcript 等基础件，但**还没有**多 NPC runtime、game contract、Stardew adapter、SMAPI bridge、NPC pack loader。
- 所以第一阶段不是“调用现成能力拼起来”，而是先补最小闭环：双 NPC、真实 bridge、独立实例、独立 memory/soul/session、trace 证据。
- 跨游戏方向不是错，但第一阶段只做最小通用边界，不做完整跨游戏框架。

### How to Read the 2026-04-27 Design Safely

`docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md` 仍然值得借鉴，但必须这样读：

- 继续保留：一 NPC 一 runtime、统一 `StardewCommandService`、SMAPI 线程边界、bridge token、本机绑定、persona seed pack、NPC 身份规范化、prompt 组装顺序、trace 证据、资源互斥这些硬约束。
- 谨慎使用：里面对当前仓库文件和数据落盘方式的描述。当前代码已经继续变化，例如 transcript 相关实现已更接近 SQLite-first，不应照搬旧文档里的 JSONL 说法。
- 不要照搬：完整目录树、全量 UI、完整 SocialRouter、完整跨游戏平台、复杂经济系统。这些是后续路线，不是 Phase 1 开局任务。

给新手的直白解释：

- “当前已有基础件”只表示有砖头，不表示房子已经盖好。
- “旧大方案仍有价值”表示里面有安全规范和边界提醒，不表示里面每个路径、每个类、每个行号都还准确。
- “先做 MVP”不是砍掉长期目标，而是先证明最小真实闭环，后续再扩。

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

Residual ambiguity remains because exact NPC cast, transport details, current-code integration points, and SMAPI command surface still need implementation-time verification.

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

### 5. Current Code Beats Old Evidence
When the 2026-04-27 design and current repository disagree, current repository evidence wins. The older document may still supply constraints, but implementation tasks must re-verify file paths, constructor signatures, persistence behavior, and DI registration before editing code.

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
- CLI、Hermes typed tools、未来 UI 都必须经同一个 `StardewCommandService`，不能各自直接调用 SMAPI HTTP。
- Prompt 组装必须沿用 `ContextManager` / `PromptBuilder` 方向，禁止为了 Stardew 新增独立 `StardewPromptAssembler`。
- Stardew 配置必须进入现有 `HERMES_HOME/config.yaml` 的 `stardew` section，不能新增 `.claude/stardew.json` 或第二配置源。
- Bridge MVP 默认只绑定 `127.0.0.1`；除 `/health` 外必须有启动时生成的 token 或等价本机安全约束。
- NPC 身份必须有规范化层：代码内部用稳定 `npcId`，SMAPI 侧用游戏名 / `smapiName`，玩家输入别名只能经 catalog 归一化。
- Persona 只能来自固定 seed pack 初始化出的 NPC 专属 `SOUL.md`，不能在初始化 prompt、工具 prompt、命令 prompt 里临时塞角色人设。
- Stardew 必选 skill prompt 应来自可审查的 bundled markdown 源，并由运行时显式绑定；不能依赖当前 `SkillManager` 的触发词能力，因为当前触发词能力并不能保证必选注入。
- Bridge request / response / error / status DTO 必须固定。不能只规定一个 envelope，然后让各工具内部的 `data` 随意变化。
- 如果 Phase 1 纳入移动、交互、采集等物理动作命令，这些命令必须先有资源互斥设计；观察类查询可以并发，物理动作不能自由并发。
- 活跃 NPC 必须由 `NpcAutonomyLoop` 主动观察、决策、执行和轮询任务；游戏事件只能作为事实输入、唤醒、暂停、阻塞或优先级信号，不能代替 NPC 决策。
- 禁止把 MVP 做成“游戏事件来了才临时问一次 Agent”的事件驱动问答系统；本项目参考 hermescraft 的 agent -> tools/bridge -> 游戏身体 -> 游戏世界链路。

## Non-Goals

### Phase 1 Non-Goals
第一阶段明确后置以下内容：

- 不做完整漂亮的桌面调试 UI。
- 不做复杂世界协调层、社交网络、经济系统。
- 不支持很多 NPC；第一阶段只管 2 个。
- 不做多进程 / 分布式；一 NPC 一进程以后再说。
- 不做完整跨游戏框架；只保留清晰边界，不做过度泛化。
- 不做高级自主规划；先做简单 tick loop：观察、决策、执行、记录。
- 不做完整 NPC 社交图、经济系统、世界资源调度 UI。
- 不做 LAN / 远程联机 bridge 暴露。
- 不做采集、生产、箱子存取等复杂物理动作。
- Phase 1 必须包含一个最小 `move` 真实闭环，但必须同时包含 command status、失败原因、资源互斥和 trace 验证。
- Phase 1 必须包含最小 SMAPI overlay/debug menu 与最小 Desktop 调试面；完整漂亮 UI、复杂过滤和可视化 replay 后置。

### Global Non-Goals
- 不把当前 agent profile 功能误当成多 NPC runtime。
- 不把全局 `MemoryTool` / `MemoryManager` 的存在误当成 NPC 级记忆闭环。
- 不把 UI 演示或 prompt 演示当成真实游戏桥接。
- 不把 Stardew 语义写进 `core runtime`。
- 不把 hermescraft 的 Minecraft 动作原样搬到 Stardew。
- 不绕开 `StardewCommandService` 做第二套 CLI、typed tools 或 UI 命令逻辑。
- 不把旧大方案中的过期代码事实继续当作当前事实。
- 不用“事件触发一次 agent 回答”替代活跃 NPC 的常驻主动循环。

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
- [ ] `StardewCommandService` 是唯一命令服务源；typed tools、CLI、未来 UI 都只作为 adapter 调它。
- [ ] `move` 命令从 NPC runtime 穿过 typed tool / `StardewCommandService` / SMAPI bridge / `UpdateTicked` 执行，并回写 `commandId`、status、failure reason 和 trace。
- [ ] Phase 1 的 `move` 有最小 resource claim：同一 NPC、target tile、interaction tile、目标对象不能被多个运行中物理命令同时占用。
- [ ] Bridge 默认 loopback-only；无 token 的非 `/health` 请求会被拒绝，不能默认开放到 LAN。
- [ ] Stardew 配置只从 `HERMES_HOME/config.yaml` 的 `stardew` section 读取。
- [ ] Prompt preview 或等价日志能证明每个 NPC 加载的是自己的 `SOUL.md`、共享 Stardew skill、稳定 system prompt，且没有把两个 NPC 人格混在一起。
- [ ] 如果 seed pack 缺必要文件或 NPC catalog 不能解析身份，runtime 创建必须失败并给出明确错误，不能临场生成替代内容。
- [ ] SMAPI mod 最小 overlay/debug menu 能显示 bridge 是否在线、最近 `move` 请求、`traceId`、失败原因和 block reason。
- [ ] Desktop 最小调试面能查看 NPC runtime 状态、bridge health、last error、recent trace，并能打开或导出对应日志。

### Full Roadmap Acceptance
后续阶段仍要覆盖：

- [ ] 同一游戏内新增 NPC 时，若复用既有能力集，优先通过资料包完成，不改 core。
- [ ] `NpcRuntimeSupervisor` 支持生命周期、tick、调度、暂停、恢复、关闭。
- [ ] `NpcAutonomyLoop` 支持持续观察、决策、执行、观察结果记录。
- [ ] 世界协调层避免多个 NPC 抢同一资源、同一目标 tile、同一互动目标。
- [ ] 节日、剧情、玩家不可控、跨天状态有统一 override policy。
- [ ] 后续完整调试面板可查看每 NPC 状态、memory、transcript、action、bridge health，并提供更完整过滤和 replay。
- [ ] 后续可切到多进程 topology 而不重写 core runtime / game contract。
- [ ] 更复杂的 movement / interact / collect / craft / economy 命令都经过 command contract、resource claim、trace、failure reason，不允许各自散落实现。

## Technical Context

### 当前仓库事实
- `Desktop/HermesDesktop/Services/HermesChatService.cs`：桌面层是 in-process agent execution，适合作为首个宿主基础。
- `HermesChatService` 当前只有一个 `_currentSession`，说明桌面聊天主路径仍是单活跃会话模型。
- `src/Core/Agent.cs`：Agent 支持注入 `MemoryManager`、`TranscriptStore`、`ContextManager`、`SoulService`，且具备工具调用循环。
- `src/Core/Agent.cs` 默认有 `MaxToolIterations`，说明游戏里的持续自治 loop 必须单独设计，不能假设一次 `ChatAsync` 就能长期生活。
- `src/transcript/TranscriptStore.cs`：当前是 SQLite-first session persistence，支持多 session 和 `sessionSource`，可作为 NPC transcript 隔离底座。旧设计文档中的 JSONL-first 描述已经不能直接当事实。
- `src/memory/MemoryManager.cs`：memory 以目录为边界，具备隔离潜力；但当前 Desktop DI 注册的是全局 singleton。
- `src/soul/SoulService.cs`：默认读取全局 `SOUL.md`、`USER.md`、journal、project rules；需要 NPC namespace 化。
- `src/soul/AgentProfile.cs`：激活 profile 会写回全局 `SOUL.md`，这是切换，不是并存。
- `src/agents/AgentService.cs`：subagent 基础设施偏短生命周期任务代理，不是长期 NPC supervisor。
- 当前仓库未发现已落地的 `src/runtime/`、`src/game/core/`、`src/games/stardew/`、`src/game/stardew/personas/`、`NpcRuntimeSupervisor`、`IGameAdapter`、`StardewCommandService`、`NpcAutonomyLoop`、`WorldCoordinationService`。

### 2026-04-27 Design Still Useful For
以下内容即使旧文档代码事实过期，也仍然是正确方向：

- 证据分层：当前已有、参考实现已证明、新增设计约束必须分开写。
- 不保留“主 Agent 语义导演”；调度器只调度，不替 NPC 理解世界。
- 桥接层只暴露事实和能力，NPC Agent 自己通过工具观察 / 决策。
- CLI / typed tools / UI 统一走 `StardewCommandService`。
- Prompt 组装绑定现有 `ContextManager` / `PromptBuilder`，不新建独立 Stardew prompt assembler。
- NPC 私有身份、记忆、人格、transcript、runtime state 以 `gameId + saveId + npcId + profileId` 或等价键隔离。
- Bridge 默认本机、带 token、固定 DTO、固定 error/status 语义。
- SMAPI HTTP handler 不直接改 `Game1`；真正改游戏对象的操作在 SMAPI 游戏循环事件中执行。
- NPC seed pack 缺文件时失败，不能临场生成。
- MVP 也必须有 trace / prompt preview / bridge command evidence，不把 UI 截图或文档当验收。

### 2026-04-27 Design Must Be Rechecked Before Use
以下内容不能直接照搬：

- 具体代码行号、目录是否存在、类名是否已实现。
- transcript / activity 的落盘方式。当前代码已偏 SQLite-first，旧 JSONL 表述应视为历史背景。
- 完整目录树。它是目标布局参考，不是 Phase 1 必须一次创建的清单。
- Bearer token、SocialRouter、commandQueue、ResourceClaimRegistry 等对象在旧文档里是新增设计约束，不是当前已经存在的实现。
- 旧文档中对 UI 面板、SocialGraph、经济系统、跨游戏平台的完整设想不应压进 MVP。

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

审查规则：
- 写“当前已有”时必须能在当前代码中找到证据。
- 写“参考证明”时只能证明模式可行，不能证明 Stardew 侧已经实现。
- 写“新增设计”时要进入任务清单和验收标准，不能伪装成现成能力。

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

边界规则：
- `src/game/core/` 不允许出现 `Stardew`、`SMAPI`、`Festival`、`Friendship`、`Dialogue` 等具体游戏术语。
- `src/games/stardew/` 可以知道 Stardew 地图、NPC 名、SMAPI lifecycle、节日 / 剧情状态。
- `core` 字段只因 Phase 1 真实需要而增加，不为了未来游戏幻想预埋复杂模型。

#### 4. 统一命令服务与 DTO 契约
这是从 2026-04-27 设计中保留下来的强约束。

必须做：
- `StardewCommandService` 是唯一命令服务源。
- Hermes typed tools 只负责把 Agent tool call 适配到 `StardewCommandService`。
- 未来 `sdv` CLI 和 Desktop UI 也只能调用 `StardewCommandService`。
- request `payload`、response `data`、error code、status enum、`commandId`、`traceId` 必须固定。

不能做：
- 不能让 typed tools 直接调 SMAPI HTTP。
- 不能让 CLI 自己拼 HTTP JSON。
- 不能让 UI 用另一套命令对象。
- 不能只固定 envelope，而让每个工具的 `data` 自由发挥。

#### 5. 真实 SMAPI Bridge 闭环
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
- 除 `/health` 外，本机 bridge 必须有启动时生成的 token 或等价本机安全约束。
- MVP 默认只绑定 `127.0.0.1`，不得默认绑定 `0.0.0.0`。
- 发现文件 / 端口缓存只能是运行时发现材料，不是 Hermes Desktop 主配置源。

#### 6. Prompt / Skill / Persona 边界
这是防止多 NPC 人格串线的核心。

必须做：
- NPC persona 从 seed pack 初始化到该 NPC runtime 的 `SOUL.md`。
- Stardew 通用规则和工具使用规范来自共享 Stardew skill prompt 或稳定 system context。
- skill prompt 正文应放在可审查 markdown 源中，不塞进 C# 字符串、JSON 或 persona 文件。
- Prompt preview 或等价日志必须记录：启用 skill、`SOUL.md` 来源、最终 messages 顺序、NPC identity。

不能做：
- 不能把 Haley / Penny 的人格同时写进共享 prompt。
- 不能让 `AgentProfile` 的“激活并覆盖全局 SOUL.md”路径承载并发 NPC。
- 不能依赖 `SkillManager` 触发词来保证 Stardew 必选规则被注入。

#### 7. Trace / Evidence Layer
MVP 验收必需，不是锦上添花。

需要：
- 每 NPC 独立 action log / memory log / event log。
- bridge command trace id。
- 隔离验证命令或测试：A 的秘密不会被 B 读到。
- 验证脚本或 replay 记录，能证明 bridge 真实调用。
- prompt preview / message preview，能证明人格和共享规则没有重复注入或串 NPC。

### P1: Phase 2 稳定性与可调试性

#### 8. 简单 NpcAutonomyLoop
第一阶段可只做最小 loop，第二阶段增强：
- 观察 -> 决策 -> 执行 -> 记录结果。
- 支持 cancellation、cooldown、max turn budget。
- 支持节日 / 剧情 / 玩家不可控打断。
- 支持路径失败后的简单重试或停止。

#### 9. 最小 UI / Overlay 调试面
Phase 1 必须有最小 UI / overlay 调试面，但不是完整漂亮 UI：
- SMAPI mod 提供最小 overlay / debug menu。
- 查看每个 NPC 的 runtime 状态。
- 查看最近 memory / transcript / action。
- 手动暂停、恢复、单步。
- 查看 bridge health。
- 查看最近一次 `move` 请求是否到达 mod、当前 status、失败原因和 `traceId`。

### P2: Phase 3 世界协调与自治增强

#### 10. World State 与冲突协调
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

Phase 1 规则：
- Phase 1 必须包含 `move` 真实闭环，因此必须至少实现最小 resource claim，否则会出现两个 NPC 抢同一 tile / object 的不确定行为。
- 完整 `WorldCoordinationService` 可以后置，但 `move` 所需的 claim、idempotency、status、failure reason、claim release 不能后置。

#### 11. 高级自主规划
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
3. 定义 `NpcIdentity` / `NpcCatalog` / `NpcPackManifest`，先固定 Haley / Penny 或资料包指定的两个首发 NPC。
4. 定义最小 `game core contract`，只包含 MVP 需要的 command/query/event。
5. 定义 `StardewCommandService` 和固定 request / response / error / status DTO。
6. 实现 `StardewAdapter` shell 与最小 SMAPI bridge，跑通 health/status/chat 和最小 `move` 真实闭环。
7. 注册 Stardew typed tools，让 NPC 通过工具调用 `StardewCommandService`，再由它访问 bridge。
8. 接入 seed pack -> NPC runtime `SOUL.md` 初始化，并记录 prompt preview。
9. 做双 NPC 隔离验证：A 的秘密不会被 B 读到。
10. 做 trace 验证：NPC -> tool -> `StardewCommandService` -> SMAPI bridge -> result -> transcript/activity。
11. 验证 `move` 的 resource claim、command status、failure reason、claim release。
12. 验证 bridge loopback/token/config.yaml 约束。
13. 验证最小 SMAPI overlay 和 Desktop 调试面能定位最近一次 `move` 的 trace 与失败原因。

### Phase 2: 稳定与调试
1. 强化 `NpcAutonomyLoop`。
2. 增加最小 Desktop runtime panel。
3. 增加 bridge health、command retry、failure reason。
4. 增加节日 / 剧情 / 玩家不可控状态门控。
5. 增强 Phase 1 最小 resource claim；后续扩展 interact/collect/craft 前必须补对应 claim 类型。

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
- 2026-04-27 大方案里的安全和边界约束仍应保留：统一命令服务、loopback/token、config.yaml、prompt preview、SMAPI game-loop 执行、seed pack。

### Needs Correction
- 不能把“core runtime 通用化”理解成第一阶段完整跨游戏框架。
- 不能把 `AgentProfile` 误认为可并发 NPC 人格系统。
- 不能把全局 `MemoryManager` 误认为 per-NPC memory 隔离。
- 不能第一阶段就做完整漂亮 UI、复杂协调、多进程、社交经济系统；但最小 SMAPI overlay 和 Desktop 调试面是 Phase 1 必做。
- 不能让 CLI、typed tools、UI 各自调用 SMAPI，必须经同一个 `StardewCommandService` / bridge contract。
- 不能照搬旧大方案里的 current-code 证据；当前实现已变化，尤其 transcript persistence 相关描述要以现代码为准。
- 不能把旧大方案里的完整目录树当作 Phase 1 文件清单；它只是一张长期地图。

## Assumptions Exposed & Resolved

| Assumption | Challenge | Resolution |
|------------|-----------|------------|
| 当前“多 agent”能力可直接承载多 NPC 并存 | 检查 `HermesChatService` 与 `AgentProfile` | 当前更像单会话 + profile 切换，不是多 NPC 并存 runtime |
| 共享一个 Agent 只靠 prompt/memory 标签也能过 MVP | 追问 NPC 在系统里到底是什么实例 | MVP 必须是一 NPC 一独立 Agent 实例 |
| 现有 memory/soul 子系统已经天然支持 NPC 隔离 | 检查 `MemoryManager`、`SoulService`、Desktop DI | 现有能力偏全局用户 / 项目导向，需要 NPC namespace 装配 |
| Stardew 的 world / action 语义可以直接写进 core | 对照未来多游戏目标 | core 只能定义最小 game contract，Stardew 只能作为 adapter 实现 |
| 第一阶段必须做完整跨游戏框架 | 对照“最快真实双 NPC MVP”目标 | 第一阶段只切边界，不做大平台 |
| 文档只写第一阶段就够 | 用户要求不要忘记后续 | 文档保留全量 roadmap，但 Phase 1 明确只做 MVP |
| 旧大方案的所有证据都还能直接用 | 用户指出旧方案已落后；检查 transcript 当前实现 | 旧方案当参考约束库，当前代码事实必须重新核查 |
| 聊天命令足以代表完整 Stardew 行为闭环 | 用户要求 Phase 1 必须包含 move；对照物理动作、tile、对象占用风险 | MVP 必须包含最小 move 闭环，并同步实现 resource claim、status、failure reason 和 trace |

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

## Detailed Design Addendum Imported From 2026-04-27

本节吸收 `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md` 中仍然适用的详细设计。阅读规则：

- 这里的内容是目标架构与实施约束，不代表所有类和路径当前已经存在。
- 涉及当前代码事实、行号、落盘方式、DI 注册方式时，实施前必须重新核查当前仓库。
- Phase 1 仍以双 NPC 真实隔离 MVP 为边界；最小 `move` 真实闭环、最小 SMAPI overlay、最小 Desktop 调试面是用户明确要求的 Phase 1 硬要求。完整目录、完整漂亮 UI、复杂社交、经济系统、多进程和多游戏扩展仍按后续阶段推进。

### Imported Design Principles

1. NPC 身份由系统绑定，不由提示词决定。
2. 调度器只调度，不做语义导演。
3. 桥接层只提供能力接口，不提供语义摘要。
4. NPC Agent 自己使用工具感知和理解场景。
5. 每个 NPC 自己维护并写入自己的长期记忆。
6. 玩家在场时优先保证近景真实感。
7. 完整设计写全，MVP 只截断实现范围，不截断愿景。
8. 每个关键结论必须能追溯到当前仓库证据、参考 mod 证据，或被明确标注为新增设计约束。
9. 一个应用可以托管多个 NPC runtime，但每个 runtime 的 home、SOUL、memory、transcript、social、marks 必须硬隔离。
10. 同一条 Stardew 命令只能有一个服务源；CLI、typed tools、UI 只能调用同一个命令服务。
11. Prompt 组装顺序固定，不允许实现阶段自行发明新顺序。
12. SocialRouter 只是“邮递员”，不是导演。
13. Stardew 行为工具必须按 Stardew 任务语义定义，不能把 Minecraft 的 `collect` 等动作照搬进 MVP。
14. 参考实现对齐必须同时考虑当前 Hermes 代码接入点，不能只抄参考 mod 的表面流程。
15. Bridge 契约、命令状态、错误码、幂等规则必须是共享契约，不允许 CLI、typed tools、UI 各写一份。
16. 跨天续做必须重新观察和重新下发命令；旧的物理动作 command 不能默认跨天继续跑。
17. 多 NPC 运行必须可追踪、可回放、可调试；没有结构化 trace 的实现不能算通过 MVP。
18. Haley / Penny 人格必须来自固定 seed pack，不能由实现 AI 临场编写、临场改写或重复注入。
19. NPC 名字不能散落匹配；所有显示名、中文名、别名、SMAPI 名都必须先映射成稳定 `npcId`。
20. MVP 每个工具的 payload / data 都必须有命名 DTO 与 golden JSON，不能用自由 JSON 字典补实现。
21. 多 NPC 可以并发思考，但物理动作不能并发抢同一资源；资源占用必须由 bridge / command 层统一裁决。
22. 活跃 NPC 必须有自己的常驻自主循环；调度器负责启动、暂停、恢复、停止，不替 NPC 做一次性决策。

### Detailed Overall Architecture

```text
SMAPI / Stardew
    ^
    |
桥接层（HTTP / 后续可替换为进程通信）
    ^
    |
Hermes 工具接口（Stardew typed tools）
    ^
    |
平行 NPC Agent（Haley / Penny / ...）
    ^
    |
非智能调度器服务
```

运行形态：
- MVP 采用一个 Hermes Desktop 应用内托管多个 NPC runtime。
- 这不是把 Haley、Penny 塞进同一个 Agent，而是在同一进程内创建多个边界清楚的 `Agent + SoulService + ContextManager + MemoryManager + TranscriptStore` 组合。
- `external/hermescraft-main` 的 landfolk 是每个角色一个独立 `HERMES_HOME` 进程；Hermes Desktop 的等价映射是每个 NPC 一个 runtime home / profile。
- 长期可以演进到多进程 / 分布式，但 MVP 不以多进程为默认形态。

架构分工：
- 非智能调度器服务：检测视野变化、查注册表、创建 / 回收 NPC Agent、维护运行表、处理节日 / 剧情让位。
- 平行 NPC Agent：各自独立使用工具理解场景、执行近景行为、维护自己的会话与长期记忆。
- Hermes 工具接口：给 Agent 暴露 Stardew 专用能力。
- 桥接层：接收工具调用，转成 SMAPI 侧操作。
- SMAPI / Stardew：游戏实际执行层。

### Reference Mapping Matrix

| 链条层 | hermescraft 参考实现 | Hermes Desktop 当前接入点 | Stardew 映射规则 |
| --- | --- | --- | --- |
| Trigger | 每个角色独立 home、提高迭代预算、启动长期 Hermes chat 进程 | `Agent` 支持注入依赖；当前 Desktop 主路径仍偏单会话 / 单例服务 | `NpcSceneScheduler` 只负责拉起 / 暂停 / 恢复 / 回收；`NpcRuntimeFactory` 按 binding 创建 runtime；`NpcAutonomyLoop` 是活跃 NPC 的受控常驻脑子 |
| Snapshot | `/status`、`/nearby`、`/look`、`/chat`、`/commands` 等观察端点 | Hermes typed tool 可提供参数 schema，Agent 自己调用工具 | Stardew MVP 工具只暴露观察、社交、命令队列、生命周期能力；bridge 返回事实，不返回导演式摘要 |
| Prompt / Summary Assembly | 角色 prompt 文件 + `SOUL.md` + skill prompt | `PromptBuilder` 方向固定为 soul / stable system / session state / retrieved context / recent turns / current user | 不新增独立 `StardewPromptAssembler`；NPC 人格只进各自 `SOUL.md`；Stardew 通用规则进入稳定 system context 或共享 skill prompt |
| Parse / Repair / Normalize | CLI / server 把命令转 HTTP action 或 task | typed tool 参数 schema 由工具类型生成；当前 skill 触发能力不能保证必选注入 | `StardewCommandService` 统一做参数规范化、错误码、结果渲染；必选 skill 由 `EnabledSkillNames` 显式注入 |
| Projector / Executor | bot server 执行动作或启动后台 task，并维护 task 状态 | Agent 工具循环执行 tool call，并写 activity；并行白名单只解决单 Agent 内并行 | HTTP handler 只读缓存 / 入队 / 返回 `commandId`；实际执行在 SMAPI `UpdateTicked` 内完成；物理动作先通过 `ResourceClaimRegistry` 占用资源 |
| Authoritative Writeback | 写 chat log、social graph、locations、task state | transcript / activity / memory / soul 具备基础能力，但隔离边界需 namespace 化 | NPC runtime 的 memory、transcript、marks、social、runtime state 必须落到 `gameId/saveId/npcId/profileId` 边界下 |
| Player-visible Surface | Minecraft chat、bot 行为、CLI 输出、task status | Desktop 已有 chat / activity / session 相关基础 | MVP 至少提供 CLI / 文件化调试；模式 B 再做管理面板；玩家可见聊天和命令结果必须来自同一 `StardewCommandService` 契约 |

硬规则：
- 任何新增模块都必须能放进上表某一层；放不进去的模块默认视为架构漂移。
- 不允许只复刻工具名，而忽略 authority owner、canonical truth、writeback 顺序和 prompt 边界。
- 若参考实现和当前 Hermes 代码边界不一致，以当前代码可安全承载的边界为准，但必须记录偏离理由。

### Detailed Core Components

#### NpcRegistry

职责：
- 保存 `npcId -> 绑定信息`。
- 作为 NPC 身份、记忆路径、人格作用域的系统真相表。
- 只接受已经由 `StardewNpcCatalog` 规范化后的 `npcId`。

#### StardewNpcCatalog

职责：
- 作为 `npcId`、SMAPI 内部名、玩家显示名、中文名、别名、persona seed pack 的唯一映射源。
- 为 SocialRouter、StardewCommandService、NpcRuntimeFactory、SMAPI bridge client 提供统一身份解析。

MVP 最小字段：

```json
{
  "npcId": "haley",
  "smapiName": "Haley",
  "displayNames": ["Haley", "海莉"],
  "aliases": ["haley", "Haley", "海莉"],
  "profileIds": ["default"],
  "defaultProfileId": "default",
  "personaSeedPack": "src/game/stardew/personas/haley/default"
}
```

硬规则：
- 路径、runtime state、trace、memory、transcript 只使用稳定小写 `npcId`，例如 `haley`、`penny`。
- SMAPI 查询、NPC 查找、友谊 / 行为条件只使用 `smapiName`，例如 `Haley`、`Penny`。
- 玩家输入、点名、群聊 target 必须先经 `aliases` 归一化为 `npcId`。
- `displayNames` 只用于 UI / prompt 可读文本，不能作为路径 key 或 SMAPI 查询 key。
- SocialRouter、typed tools、CLI、UI、bridge client 禁止各自维护名字映射表。
- 未知或歧义别名必须返回 `invalid_target`，不能让模型猜最像的 NPC。
- `npc_default_cast` 只能写 `npcId` 列表，不能写 `Haley`、`海莉` 或 `smapiName`。

#### NpcRuntimeBinding

职责：
- 表达单个 NPC 在运行时必须绑定的依赖集合。
- 包括 `gameId`、`saveId`、`npcId`、`profileId`。
- 包括 `runtimeHome`、`memoryDir`、`transcriptDir`、`soulScope`、`sessionNamespace`。
- 包括 `enabledSkillNames`。

硬规则：
- `npcId` 不能单独作为数据隔离 key。
- MVP 即使只有一个人格配置，也必须使用 `profileId = "default"`。
- 任一 runtime 的 `SoulService`、`ContextManager`、`PromptBuilder`、`MemoryManager`、`TranscriptStore` 必须由 `NpcRuntimeBinding` 创建 / 注入，不能复用全局单例依赖。

#### NpcRuntimeFactory

职责：
- 根据 `NpcRuntimeBinding` 创建已经绑定好的 NPC Agent。
- 构造并注入专属 `MemoryManager`、`TranscriptStore`、`ContextManager`、`SoulService`。
- 把 NPC 专属 `SOUL.md` 放在该 runtime 的 `SoulContext` 来源中。
- 把 Stardew 必选规则、命令契约、观察-思考-行动循环绑定进稳定系统上下文。

#### NpcSceneScheduler

职责：
- 维护视野集合。
- 决定进入视野 / 持续停留 / 离开视野时的创建与回收。
- 维护每个 NPC 同时只有一个近景运行体。
- 启动、暂停、恢复、停止对应 NPC 的 `NpcAutonomyLoop`。

硬规则：
- `NpcSceneScheduler` 不调用 LLM，不替 NPC 组装“下一步应该做什么”。
- 进入近景时创建 runtime 并启动常驻自主循环。
- 持续停留时只保活 / 投递事件，不重复启动第二个循环。
- 离开近景、切存档、返回标题时发出停止信号，由 `NpcAutonomyLoop` 收尾。

#### NpcRuntimeHandle

职责：
- 保存运行中 NPC 实例的元数据：
  - `npcId`
  - `agentId`
  - `sessionId`
  - `backgroundTaskId`
  - `cancelTokenSource`
  - `status`
  - `lastSeenAt`
  - `autonomyLoopTask`
  - `autonomySessionId`
  - `lastAutonomyHeartbeatAt`
  - `lastAutonomyExitReason`

#### NpcAutonomyLoop

职责：
- 作为活跃 NPC 的“脑子”常驻运行。
- 持有该 NPC 的 `Agent`、`Session`、`NpcRuntimeBinding`、enabled skills、工具集合和 cancellation token。
- 进入循环后让 NPC 自己反复观察、思考、行动、读聊天、读命令、轮询任务。
- 接收 SocialRouter / bridge / scheduler 投递的事件，但事件只影响优先级，不替 NPC 决策。

启动 prompt 只允许描述循环任务，不允许塞人格：

```text
You are now active in Stardew Valley.
Repeat your Stardew loop while active:
1. observe game status, self state, nearby, chat, commands
2. decide what matters
3. speak, remember, mark, complete command, or request a task when needed
4. while a task runs, poll task_status plus read_chat / commands
Stop cleanly if runtime state becomes paused, stopping, or bridge unavailable.
```

硬规则：
- 一个 `npcId + saveId + profileId` 同时只能有一个 `NpcAutonomyLoop`。
- `NpcAutonomyLoop` 使用同一个 `sessionId` 持续写 transcript，不为每次事件创建新 session。
- `NpcAutonomyLoop` 可以在内部调用一次长 `Agent.ChatAsync(...)`，并把 `MaxToolIterations` 显式设置为 Stardew autonomy 预算。
- 如果 `ChatAsync(...)` 因达到预算或模型自然停止而返回，但 runtime 仍为 `active`，loop supervisor 必须记录原因、短暂 cooldown，然后用同一 session 续跑。
- `NpcAutonomyLoop` 不能在 SMAPI `UpdateTicked` 线程内执行；它只能在 Hermes Desktop 后台任务中运行。
- `paused` / `stopping` / `ReturnedToTitle` / `DayEnding` / `Saving` 必须能取消或收尾 loop，不能让后台 LLM 继续向旧存档发命令。
- 长任务期间禁止让模型空等；必须轮询 `task_status`、`read_chat`、`commands`。

#### NpcAutonomyBudget

职责：
- 控制多个常驻 NPC loop 对 LLM 的并发和总量。
- 在 ChatClient 或 Agent 调用层做全局门闸，而不是锁住整个 `NpcAutonomyLoop`。

MVP 默认：
- `npc_autonomy_max_tool_iterations = 100`
- `npc_autonomy_max_concurrent_llm_requests = 1`
- `npc_autonomy_restart_cooldown_seconds = 5`
- `npc_autonomy_max_restarts_per_scene = 3`
- `npc_autonomy_start_stagger_seconds = 4`

硬规则：
- 多个 NPC loop 可以同时存活、等待工具、轮询 bridge；但 LLM 请求必须经过 `NpcAutonomyBudget`。
- MVP 默认同一时间只允许一个 LLM request，防止多 NPC 同时烧模型和触发 provider 限流。
- 预算限制不能由 prompt 表达，必须由代码 enforce。
- 超预算时 NPC runtime 进入 `paused` 或 `blocked`，并写入 trace；不能静默停止。

#### StardewBridgeServer

职责：
- 在 SMAPI mod 中暴露 HTTP / 进程通信接口。
- 只负责能力接口，不负责语义摘要。
- HTTP 请求线程只允许读取已缓存状态、写入命令队列、返回 `commandId`。
- 实际读写 Stardew `Game1` / NPC / 地图对象必须在 SMAPI 游戏循环事件内执行。
- 统一记录 bridge request / response / command 状态，供 Hermes 侧按 `traceId` 查询。

#### StardewCommandService

职责：
- 作为 Stardew 命令的唯一业务入口。
- 负责命令注册、参数规范化、调用 bridge client、结果渲染。
- 负责生成或传递 `traceId` / `requestId` / `commandId`。
- 负责把 bridge error code 映射为 typed tool / CLI / UI 一致的结果模型。

硬规则：
- Hermes typed tools 必须调用 `StardewCommandService`。
- `sdv` CLI 必须调用 `StardewCommandService`。
- 未来 UI 管理面板必须调用 `StardewCommandService`。
- 禁止 typed tool、CLI、UI 各自直接拼 HTTP 请求。
- 禁止 typed tool、CLI、UI 各自维护一套 request / response DTO、错误码或命令状态枚举。

#### StardewSkillBinding

职责：
- 映射参考 mod 的 `skills/*.md` 思路，为 Stardew 建立游戏专有 skill prompt。
- 在 `NpcRuntimeBinding.EnabledSkillNames` 中显式声明本 NPC 启用哪些 skill。
- 在 runtime 创建时把启用 skill 的 prompt 注入稳定系统上下文。
- 维护 Stardew skill prompt 的固定清单、职责边界、启用条件和测试快照。

MVP 必选 skill：

| Skill | 启用范围 | 职责 | 禁止内容 |
| --- | --- | --- | --- |
| `stardew-core` | 所有 Stardew NPC runtime | 固定观察 -> 决策 -> 单动作 / 短任务 -> 再观察循环；要求从 bridge observation 读取 `gameState`、位置、可见 NPC、时间、节日 / 菜单 / 过场 / 存档状态；规定 `commandId` 查询、取消、跨天收束、memory 写入边界 | 禁止写 Haley / Penny 人格；禁止定义 HTTP DTO / 错误码；禁止列出未实现工具；禁止让模型凭常识猜地图、作物、NPC 位置或节日状态 |
| `stardew-social` | 所有 Stardew NPC runtime | 固定 `read_chat` / `commands` / `social_snapshot` 的读取优先级；玩家直接命令优先；完成命令后必须写回完成状态；MVP 只允许玩家在场群聊；`SocialRouter` 只做 envelope 投递 | 禁止自行计算好感、情绪、剧情结果、礼物效果；禁止离屏 NPC 私聊扩散；禁止把社交路由写成剧情导演 |

扩展 skill：

| Skill | 阶段 | 启用条件 | 边界 |
| --- | --- | --- | --- |
| `stardew-navigation` | Phase 1 minimum + later expansion | Phase 1 只覆盖最小 `move` 闭环、tile / location 目标、不可达失败和 task status；后续再扩展路径 / 跟随 / 交互点 | 未实现前不能在 prompt 里声明 `goto / follow` 为可用工具；Phase 1 也不能声明超出 `move` 的导航能力 |
| `stardew-farming` | Phase 4+ | 已有 `Watering / Harvesting / Foraging` 等 Stardew task DTO、资源占用和状态回写 | 不能照搬 Minecraft `collect / craft`；必须使用 Stardew crop / tile 等权威 observation |
| `stardew-town-schedule` | Phase 5+ | bridge 能提供地点、节日、商店、NPC 日程与可见性快照 | 不能让模型凭 Stardew 常识猜 NPC 日程 |

skill prompt 源文件布局：

```text
skills/
└─ gaming/
   ├─ stardew-core.md
   ├─ stardew-social.md
   ├─ stardew-navigation.md
   ├─ stardew-farming.md
   └─ stardew-town-schedule.md
```

硬规则：
- `skills/gaming/stardew-*.md` 是 Stardew 专有 skill prompt 的唯一源文件。
- `StardewSkillBinding` 负责从 bundled markdown 源文件加载、校验并按 `EnabledSkillNames` 注入稳定系统上下文。
- 用户 skills 目录副本只能用于 Skills UI 展示和普通 `skill_invoke`，不能作为 Stardew runtime 必选 skill 的唯一来源。
- `skill_invoke` 只能作为可选查询工具，不能作为核心规则唯一来源。
- 当前触发词 / `triggers` 不应成为运行时逻辑依据。
- Haley / Penny 的人格写在各自 `SOUL.md`；Stardew 通用规则、工具契约、游戏循环写在稳定系统上下文或共享 Stardew skill prompt。
- skill prompt 只能引用 `StardewCommandContracts.cs` / `StardewBridgeDtos.cs` 的 typed contract，不能在 markdown 中另造 request / response JSON。
- skill prompt 不允许包含 NPC 人格、口癖、关系设定或角色背景。
- 禁止把 `stardew-core` / `stardew-social` prompt 正文写进 C# 字符串、JSON 配置、persona seed pack 或初始化 prompt。
- 必选 skill 源文件缺失、frontmatter 缺字段、`tools` 格式不兼容当前 parser、或 `name` 与 `EnabledSkillNames` 不一致时，runtime 创建必须失败并给出明确错误。

#### StardewPersonaSeedPack

职责：
- 为 Haley / Penny 提供固定、可审查、可测试的人格源文件。
- 作为 `NpcRuntimeFactory` 创建 runtime profile 时复制 / 初始化 `SOUL.md` 的唯一来源。
- 记录每个 NPC 的事实摘要、说话风格边界、禁止事项、可用 skill 列表。

MVP seed pack 最小结构：

```text
src/game/stardew/personas/
├─ haley/
│  └─ default/
│     ├─ SOUL.md
│     ├─ facts.md
│     ├─ voice.md
│     ├─ boundaries.md
│     └─ skills.json
└─ penny/
   └─ default/
      ├─ SOUL.md
      ├─ facts.md
      ├─ voice.md
      ├─ boundaries.md
      └─ skills.json
```

硬规则：
- MVP 必须至少提交 `haley/default` 与 `penny/default` 两套 seed pack。
- 核心人格正文只能进入对应 runtime 的 `SOUL.md`，禁止再写进初始化 prompt、工具 prompt 或命令 prompt。
- 禁止实现 AI 在运行时临场生成 Haley / Penny 核心人格。
- 禁止把两个 NPC 的人格合写到一个共享 prompt 或共享 `SOUL.md`。
- 禁止直接复制大段原版台词；seed pack 使用手写事实摘要、风格边界和行为约束，不把 prompt 做成台词库。
- `skills.json` 只能声明该 NPC 启用的 Stardew skill 名称；skill prompt 本体仍归 `StardewSkillBinding` 管。
- `NpcRuntimeFactory` 首次创建 profile 时从 seed pack 初始化 runtime `SOUL.md`；后续 NPC 长期记忆写入 memory，不回写 seed pack。

#### SocialRouter

职责：
- 按“谁能听见 / 看见 / 被点名”投递消息。
- 维护每个 NPC 的 `inbox`、`commandQueue`、`overhear`、`socialSummary`。
- 生成可审计的消息 envelope。

硬规则：
- SocialRouter 不判断情绪，不推断意图，不替 NPC 做关系决策。
- 对 `Haley: ...` 这类点名消息，只有 Haley 收到 command；附近 NPC 只能进入 overhear。
- 对 `Haley,Penny: ...` 这类群组消息，只有被点名 NPC 收到 direct / group command。
- 广播消息进入可听见 NPC 的 inbox，但是否回应由各 NPC Agent 自己决定。

### Detailed Lifecycle Design

Hermes Desktop NPC runtime 三态：
1. 未激活：不在玩家视野内，没有近景 Agent 在运行。
2. 近景活跃：在玩家视野内，近景 Agent 正在运行。
3. 回收中：已离开玩家视野，正在停止 Agent、等待收尾。

核心事件：
- 进入视野：未激活 -> 近景活跃。
- 持续停留：近景活跃 -> 近景活跃。
- 离开视野：近景活跃 -> 回收中 -> 未激活。

实现状态还应覆盖：
- `starting`：正在创建 runtime、绑定依赖、加载 `SOUL.md` 与 skill。
- `active`：自主 loop 正常运行。
- `paused`：节日、剧情、菜单、玩家不可控、bridge unavailable 或预算限制导致暂停。
- `draining`：跨天、保存、返回标题或离开视野时正在收尾。
- `stopped`：已停止，runtime 可释放。
- `failed`：出现不可自动恢复错误。

SMAPI bridge 生命周期要求：
- `GameLaunched`：启动 bridge，绑定 loopback 端口，写发现文件。
- `SaveLoaded` / `DayStarted`：建立 save 级缓存，更新 `saveId`，允许查询当前世界。
- `UpdateTicked`：处理 command queue，执行真正的 `Game1` / NPC / map 对象读写。
- `DayEnding` / `Saving`：停止接收新长动作，drain 或取消物理 command，flush 状态。
- `ReturnedToTitle`：清空 save runtime 状态，拒绝旧 save 命令。

跨天与存档边界：
- 跨天保留记忆、承诺、意图，但不默认保留未验证的物理动作现场。
- 第二天继续任务必须重新观察当前世界，再重新下发命令。
- 旧 `commandId` 不应跨天继续跑；应转为 `expired`、`cancelled` 或 `blocked`。

多 NPC 命令并发与资源占用：
- 观察类工具可以并发。
- 物理动作命令必须领取资源 claim。
- claim 至少覆盖 `npcId`、目标 tile、交互 tile、目标对象 / 地形。
- 同一 NPC 同时只能有一个运行中物理 command，除非明确幂等复用原 `commandId`。
- 冲突返回 `command_conflict`，不能让两个 NPC 抢同一 tile / object。
- `cancel_task`、`failed`、`stuck`、`DayEnding`、`Saving` 后必须释放 claim。

### Binding And Prompt Assembly

绑定目标：
- 每个 NPC 有固定身份、固定 runtime home、固定 profile、固定 memory / soul / transcript / trace 边界。
- Prompt 中的人格来自该 NPC 的 `SOUL.md`，不是临场 prompt。
- Stardew 通用行为规则来自稳定 system context 或共享 skill prompt。

Prompt 组装固定顺序必须遵循当前 Hermes 方向：
1. NPC 专属 `SoulContext`。
2. 稳定 `SystemPrompt`。
3. `[Session State]`。
4. `[Retrieved Context]`。
5. recent turns。
6. current user / loop instruction。

硬规则：
- 不新增独立 `StardewPromptAssembler`。
- 不把两个 NPC 的人格放进同一个共享 prompt。
- 不把 tool contract、HTTP DTO、错误码写散到 prompt 文本中；prompt 只能引用契约。
- 每次 prompt 组装要产出 prompt preview 或等价日志，记录最终 messages 顺序、启用 skill、`SOUL.md` 来源、NPC identity。

### Tool Interface Design

工具原则：
- 桥接层只暴露能力接口。
- 不提供 `scene_summary` 之类的语义摘要接口。
- NPC Agent 自己调用工具理解场景。
- MVP 工具优先覆盖观察、社交、命令队列、生命周期，不优先覆盖采集 / 制作 / 存取箱子。

桥接层可以返回事实压缩，例如位置、时间、天气、可见对象、NPC 状态；但禁止返回导演式内容，例如：
- “Haley 现在应该安慰 Penny”。
- “Penny 对玩家有点失望”。
- “此时应该触发三人剧情”。
- “推荐 NPC 下一步去做什么”。

MVP 第一批工具：
- `game_status`
- `self_state`
- `nearby`
- `look`
- `read_chat`
- `chat`
- `commands`
- `complete_command`
- `overhear`
- `social`
- `task_status`
- `cancel_task`
- `mark`
- `marks`

MVP 工具 DTO 固定契约：

| 工具 | request payload DTO | response data DTO |
| --- | --- | --- |
| `game_status` | `{}` | `{ gameId, saveId, day, season, year, timeOfDay, weather, locationName, isWorldReady, isPlayerFree, isFestival, isEventUp }` |
| `self_state` | `{ npcId }` | `{ npcId, smapiName, locationName, tile, facingDirection, isVisibleToPlayer, currentActivity, blockedReason }` |
| `nearby` | `{ npcId, radius }` | `{ originNpcId, radius, npcs, objects, terrain }` |
| `look` | `{ npcId, range }` | `{ npcId, locationName, visibleNpcs, visibleObjects, exits, hazards, rawFacts }` |
| `read_chat` | `{ npcId, sinceMessageId, limit }` | `{ messages }` |
| `chat` | `{ npcId, channel, text, targetNpcIds }` | `{ messageId, deliveredTo, overheardBy }` |
| `commands` | `{ npcId, status, limit }` | `{ commands }` |
| `complete_command` | `{ npcId, commandId, resultSummary }` | `{ commandId, status }` |
| `overhear` | `{ npcId, sinceMessageId, limit }` | `{ messages }` |
| `social` | `{ npcId }` | `{ npcId, recentEvents, relationshipFacts }` |
| `task_status` | `{ npcId, commandId }` | `{ commandId, status, progress, blockedReason, errorCode }` |
| `cancel_task` | `{ npcId, commandId, reason }` | `{ commandId, status }` |
| `mark` | `{ npcId, name, locationName, tile, note }` | `{ markId, npcId, name, locationName, tile }` |
| `marks` | `{ npcId }` | `{ marks }` |

嵌套 DTO 必须固定：
- `tile`: `{ x, y }`
- `npcs[]`: `{ npcId, smapiName, displayName, locationName, tile, distance, relationshipHint }`
- `objects[]`: `{ objectId, displayName, locationName, tile, category, state }`
- `terrain[]`: `{ kind, locationName, tile, state }`
- `messages[]`: `{ messageId, from, channel, text, createdAt, commandId, targets, hearers }`
- `commands[]`: `{ commandId, text, from, targets, status, createdAt }`
- `recentEvents[]`: `{ eventId, kind, participants, summary, createdAt }`
- `relationshipFacts[]`: `{ subjectNpcId, objectId, relationType, value, source, updatedAt }`
- `marks[]`: `{ markId, npcId, name, locationName, tile, note, createdAt }`

硬规则：
- MVP 生产代码禁止用 `Dictionary<string, object>`、匿名对象或自由 JSON 拼出工具 `payload` / `data`。
- 工具可以把 DTO 渲染成模型可读文本，但文本必须由 DTO 派生。
- 任何出现 NPC 身份的 DTO 字段必须包含稳定 `npcId`；涉及 SMAPI 对象时同时保留 `smapiName`。
- `data` 禁止包含 `recommendedNextAction`、情绪判断、关系推理、剧情导演结论。
- 新增 MVP 工具必须先补 DTO、golden JSON、CLI/tool 同源测试，再写工具实现。

扩展工具后置：
- `goto` 的通用导航能力后置；Phase 1 只允许最小 `move` 真实闭环。
- `interact`
- `collect`
- `deposit`
- `withdraw`
- `craft`
- `eat`

`move` 进入 Phase 1 的条件：
- 使用统一 `StardewCommandService` 和 bridge envelope。
- 具备 `commandId`、status、failure reason、idempotency 和 trace。
- 由 SMAPI HTTP handler 入队，由 `UpdateTicked` 执行实际游戏对象操作。
- 具备最小 resource claim 与 claim release。
- SMAPI overlay 和 Desktop 调试面可看到最近 move 请求、失败原因和 trace。

其它行为工具进入条件：
- 先定义目标类型、目标 tile、交互 tile。
- 先定义路径可达性与占位冲突。
- 先定义节日、剧情、菜单、玩家不可控状态下的让位。
- 先定义动作动画、结果确认、失败原因。
- 先定义任务状态轮询与取消。

### Communication And Bridge Contract

MVP 通信方式选 HTTP，理由：
- hermescraft 已验证 loopback HTTP bridge 可行。
- HTTP 隔离性好，易调试。
- 命名管道 / 进程通信和进程内共享库可作为后续优化，不作为 MVP 起点。

CLI / tool / UI 单一命令源：
- `sdv` CLI 只做参数解析、展示结果、返回退出码。
- Hermes typed tools 只做 tool 参数 schema 与模型可读结果适配。
- 未来 UI 面板只做界面交互。
- 三者都调用同一个 `StardewCommandService`。

Bridge request envelope：

```json
{
  "traceId": "string",
  "requestId": "string",
  "idempotencyKey": "string",
  "gameId": "string",
  "saveId": "string",
  "npcId": "string",
  "profileId": "string",
  "commandType": "string",
  "payload": {}
}
```

HTTP 认证 header：

```http
Authorization: Bearer <bridgeToken>
```

Bridge response envelope：

```json
{
  "ok": true,
  "traceId": "string",
  "requestId": "string",
  "commandId": "string",
  "status": "queued | running | succeeded | failed | cancelled | blocked | expired | stuck",
  "data": {},
  "error": null,
  "state": {}
}
```

Bridge error envelope：

```json
{
  "ok": false,
  "traceId": "string",
  "requestId": "string",
  "commandId": "string",
  "status": "failed | blocked | expired | stuck",
  "data": null,
  "error": {
    "code": "string",
    "message": "string",
    "retryable": false
  },
  "state": {}
}
```

固定错误码：
- `bridge_unavailable`
- `world_not_ready`
- `player_not_free`
- `festival_blocked`
- `cutscene_blocked`
- `menu_blocked`
- `day_transition`
- `invalid_target`
- `invalid_state`
- `command_conflict`
- `command_not_found`
- `command_expired`
- `command_stuck`
- `idempotency_conflict`
- `bridge_unauthorized`
- `bridge_stale_discovery`

硬规则：
- `StardewBridgeDtos.cs` 与 `StardewCommandContracts.cs` 是契约源。
- typed tools、`sdv` CLI、未来 UI 只能引用契约源，不能复制 DTO。
- `commandId` 由 `StardewCommandService` / bridge contract 统一生成或登记，不能由各 adapter 自己生成。
- 同一个 `idempotencyKey` 重放时不得创建第二条物理动作命令。
- observation 类查询可以无 `commandId`，但仍必须有 `traceId` / `requestId`。
- 除 `/health` 外，Bridge API 必须校验 `Authorization: Bearer <bridgeToken>`。
- `/health` 允许无 token，但只能返回 alive / version / minimal readiness，不返回 saveId、NPC 状态、命令状态或其他敏感运行时细节。

端口与本机安全：
- 支持默认端口、端口范围扫描、自动向后寻找可用端口。
- SMAPI mod 把当前绑定端口写入发现文件。
- Hermes Desktop 启动时读取发现文件连接。
- MVP 默认绑定 `127.0.0.1`，禁止默认绑定 `0.0.0.0`。
- LAN 暴露不进入 MVP；后续如需 LAN，必须单独配置开关、单独验收。
- SMAPI bridge 每次启动生成随机 `bridgeToken`，写入 `bridge-port.json`。
- `bridge-port.json` 至少包含 `host`、`port`、`bridgeToken`、`pid`、`saveId`、`startedAt`、`modVersion`。
- Hermes Desktop 读取发现文件后必须校验 host 是 loopback、token 存在、端口可连、发现文件不是陈旧状态。
- 发现文件缺 token、host 非 loopback、进入存档后 saveId 与当前目标存档不匹配、端口 health 不匹配时，必须拒绝连接并返回 `bridge_stale_discovery` 或 `bridge_unauthorized`。
- token 只能防误调用、旧进程串线和普通网页 / 脚本误打端口；不把它描述成本机恶意程序防护。

### Observability And Debugging

MVP 即使不做完整 UI，也必须留下可查询的结构化审计和可观测证据。

定义：
- `trace` 回答“一件事从 Agent 到 SMAPI 是怎么跑完的”。
- `audit log` 回答“哪个 NPC / runtime 在什么时候基于什么原因做了什么，结果是什么，是否被拒绝或取消”。
- `transcript` 保存模型对话和上下文证据；`activity` 保存工具调用证据；`prompt preview` 保存 prompt 组装证据。

必须记录：
- 每个 NPC runtime 独立 transcript：模型对话、system messages 来源、当前 user message。
- 每个 NPC runtime 独立 activity：tool call、参数摘要、结果摘要、耗时、状态。
- 每个 NPC runtime 独立 audit log：生命周期事件、调度原因、预算命中、拒绝原因、暂停 / 恢复 / 停止原因。
- 每个 bridge request：`traceId`、`requestId`、HTTP method、endpoint、status code、error code。
- 每个 command：`commandId`、`npcId`、`saveId`、`profileId`、状态变更、执行 tick、失败原因。
- 每次 prompt 组装：最终 messages 顺序、启用 skill 列表、`SOUL.md` 来源路径。
- 每个安全 / 边界事件：token 缺失或错误、stale discovery、saveId 不匹配、命令被 bridge 或 resource claim 拒绝。
- 每个跨天 / 存档事件：`DayEnding`、`Saving`、`SaveLoaded` 时 runtime 暂停、命令收尾、命令取消、第二天重新观察并新建 command 的关联关系。

最小 audit event 形状：

```json
{
  "timestamp": "iso-8601",
  "eventType": "runtime_started | prompt_built | tool_requested | command_state_changed | bridge_request | security_rejected | day_boundary",
  "severity": "debug | info | warn | error",
  "gameId": "stardew",
  "saveId": "string",
  "npcId": "haley",
  "profileId": "default",
  "traceId": "string",
  "commandId": "string?",
  "requestId": "string?",
  "summary": "human readable short summary",
  "details": {}
}
```

MVP 调试入口：
- `sdv debug runtime <npcId>`：查看 runtime 状态、路径、最近 activity。
- `sdv debug command <commandId>`：查看 command 状态、bridge 结果、SMAPI 执行记录。
- `sdv debug prompt <npcId>`：导出该 NPC 最近一次最终 messages，用于验证 prompt 顺序和 skill 注入。
- `sdv debug trace <traceId>`：串起 typed tool / command service / bridge / SMAPI 执行记录。
- `sdv debug audit --npc <npcId>`：查看该 NPC 最近审计事件，至少支持按 `traceId` / `commandId` 过滤。
- SMAPI overlay / debug menu：显示 bridge 是否在线、最近 `move` 请求、`traceId`、status、failure reason、block reason。
- Desktop 最小调试面：显示 NPC Runtime、Bridge Health、Last Error、Recent Trace，并提供打开日志目录、复制 traceId、打开 replay/verification 输出的入口。

硬规则：
- 不允许只依赖普通文本日志判断多 NPC 正确性。
- 审计日志不能只写给人看的字符串；必须是 JSONL、SQLite 表或等价结构化格式，便于测试断言和未来 UI 读取。
- trace 必须是 JSONL、SQLite 表或等价结构化格式，便于测试断言。
- `traceId` 必须能跨越 Hermes tool call、`StardewCommandService`、bridge HTTP、SMAPI command queue、`UpdateTicked` 执行结果。
- audit event 必须包含 `npcId`、`saveId`、`profileId`、`traceId`；涉及物理动作或社交命令时必须包含 `commandId`。
- 完整模式 B UI 可以后置，但 MVP 的 CLI / 文件化调试能力、SMAPI overlay 和 Desktop 最小调试面不能后置。

### Social And Group Chat Design

hermescraft 可参考的场景能力：
1. 场景感知。
2. 社交感知。
3. 群聊路由。
4. 命令队列。
5. 后台任务。
6. 地标记忆。

Stardew 对应设计：
- 完整目标：玩家在场 / 不在场都允许 NPC 群聊。
- MVP：先只实现玩家在场群聊。
- 群聊建立在“感知范围内 NPC 可互相看见 / 听见 / 交互”的基础上。
- 不通过调度器转述场景，由 NPC 自己感知和决定是否参与群聊。

SocialRouter message envelope：

```json
{
  "messageId": "string",
  "channel": "public | direct | group | overheard | system_event",
  "from": "player | npcId | system",
  "targets": ["npcId"],
  "hearers": ["npcId"],
  "locationId": "string",
  "saveId": "string",
  "text": "string",
  "createdAt": "iso-8601"
}
```

路由规则：
- `public`：投递给可听见 NPC 的 inbox。
- `direct`：只投递给目标 NPC 的 commandQueue；附近 NPC 可进入 overhear。
- `group`：只投递给被点名 NPC 的 commandQueue；附近非目标 NPC 可进入 overhear。
- `overheard`：只作为旁听事实，不可被当作对自己的命令。
- `system_event`：用于节日、剧情、日夜切换、bridge 状态等事实通知。

硬规则：
- SocialRouter 不负责“让谁应该说话”。
- SocialRouter 不负责“判断谁喜欢谁”。
- SocialRouter 不负责“决定 NPC 应不应该吃醋 / 生气 / 解释”。
- 每个 commandQueue 项必须有 `commandId`。
- NPC 完成点名命令后必须调用 `complete_command(commandId)`，避免下轮重复处理同一命令。

### First NPC Cast

MVP 首批 NPC：
- Haley（海莉）
- Penny（潘妮）

选择原因：
- Haley 人格外放，近景对话价值高，更容易验证“像不像活人”。
- Penny 性格温和、稳定，适合测试持续对话、情绪和关系连续性。
- 两人差异大，更容易验证人格隔离。
- 两人都属于村庄核心 NPC，利于在公共场景测试群聊。

Seed pack 必须包含：
- `SOUL.md`：该 NPC 的核心身份与行为边界。
- `facts.md`：可审查事实摘要，不写成长篇台词库。
- `voice.md`：说话风格边界，包括可用语气、不可用语气、不要过度现代化。
- `boundaries.md`：禁止事项，包括不要越权控制玩家、不要代替其他 NPC 表态、不要编造未观察到的事件。
- `skills.json`：该 NPC 启用的 Stardew skill 名称列表。

### Run Modes

模式 A：后台独立运行（MVP 先做）
- 不修改现有 Hermes Desktop 主 UI。
- NPC 系统作为后台模块运行。
- 用户正常使用现有 UI，不受打断。

模式 B：UI 集成运行（后续阶段）
- Hermes Desktop 增加 NPC 管理面板。
- 显示在线 NPC、生命周期状态、日志、最近活动。
- 允许手动暂停 / 重启 / 查看某个 NPC 状态。

### Detailed Phasing

Phase 0：运行时基础设施
- 提交 `skills/gaming/stardew-core.md`、`skills/gaming/stardew-social.md`，并为 future skill 放置受门控占位文件。
- `StardewSkillBinding` 读取 / 校验 bundled markdown，缺必选 skill 时 fail fast。
- golden prompt preview 测试固定 `ContextManager` / `PromptBuilder` 顺序、skill 注入位置和人格边界。
- `NpcRegistry`、`NpcRuntimeBinding`、`NpcRuntimeFactory`、`NpcSceneScheduler`。
- `StardewCommandService`。
- 最小 `SocialRouter` envelope 投递，不做社交图推理。
- SMAPI 桥接层 HTTP 服务。
- 端口发现与冲突处理。
- 近景三态生命周期。
- `gameId + saveId + npcId + profileId` 数据隔离布局。
- 第一批观察 / 社交 / 生命周期工具 DTO 与 fake bridge golden JSON。
- 最小文件化 trace / prompt preview。
- 最小 SMAPI overlay / debug menu 和 Desktop 调试面骨架。

Phase 1：单 NPC 近景闭环
- 先用 Haley 跑通单 NPC 全链路。
- 进入视野唤醒。
- 启动 Haley 的 `NpcAutonomyLoop`。
- 自己调用工具理解场景。
- 连续对话。
- 最小 `move` 真实闭环：NPC -> tool -> `StardewCommandService` -> bridge -> SMAPI `UpdateTicked` -> status/result -> trace。
- 自己写入长期记忆。
- 离开视野后正确回收。

Phase 2：双 NPC MVP
- Haley + Penny 各自常驻 `NpcAutonomyLoop`。
- 双 NPC 独立记忆 / 独立人格。
- LLM request 经过 `NpcAutonomyBudget` 受控并发。
- 玩家在场群聊。
- 点名命令与旁听 / 插话。
- 地标记忆。
- 命令队列与 `complete_command(commandId)`。

Phase 3：扩展社交
- 玩家不在场时 NPC 自然群聊。
- 更完整的社交图。
- 旁听、群聊、关系演化。
- 监督器 / 恢复机制增强。
- 更完整 `sdv debug *` 与 UI 调试面板。

Phase 4：行为扩展
- Stardew-native `goto`。
- Stardew-native `interact`。
- Stardew-native `collect`。
- 存取箱子。
- 制作。
- 恢复体力。
- 节日 / 剧情让位规则完善。
- 更丰富交互对象。

Phase 5：完整村庄
- 3 个以上 NPC 扩展。
- 复杂社交网络。
- 更强自治。
- 必要时多进程 / 分布式演进。
- 模式 B UI 集成。

### Detailed Test Plan

绑定正确性测试：
- Haley 只能读写 Haley 的记忆目录。
- Penny 只能读写 Penny 的记忆目录。
- 人格作用域不串。
- 同一 NPC 不会同时存在两个近景运行体。
- `gameId + saveId + npcId + profileId` 任一字段不同，都不能读写到同一个 runtime profile 目录。
- `profileId = default` 的 MVP 路径也必须存在，不能省略 profile 层。

生命周期测试：
- 进入视野唤醒。
- 进入视野后启动该 NPC 唯一的 `NpcAutonomyLoop`。
- 持续停留不重建。
- 持续停留不会启动第二个 `NpcAutonomyLoop`。
- 离开视野会取消 / 收尾 `NpcAutonomyLoop`。
- 再次进入视野恢复。
- 剧情 / 节日 / 菜单 / 玩家不可控时 runtime 进入 `paused` 或命令进入 `blocked`，不能反复重启 Agent。
- `DayEnding` / `Saving` 进入 `draining`，停止接收新长动作并 flush 状态。
- 跨天后旧物理命令不会沿用原 `commandId` 继续执行；Agent 必须重新观察后创建新命令。
- `GameLaunched` 写入 bridge port 发现文件。
- `DayStarted` / `SaveLoaded` 建立 save 级缓存。
- `UpdateTicked` 处理命令队列。
- `ReturnedToTitle` 清空 save runtime 状态。

工具桥接测试：
- 覆盖 `game_status`、`self_state`、`nearby`、`look`、`read_chat`、`chat`、`commands`、`complete_command`、`overhear`、`social`、`task_status`、`cancel_task`、`mark`、`marks`。
- `sdv` CLI 与 Hermes typed tools 必须命中同一个 `StardewCommandService`。
- CLI 与 typed tools 对同一 fake bridge 输入应产生一致请求与一致结果模型。
- 每个 MVP 工具都必须有 fake bridge golden JSON，覆盖 request `payload`、response `data`、error envelope。
- 缺必填字段必须失败；未知额外字段是忽略还是拒绝必须在测试里固定。
- MVP 生产代码不得用 `Dictionary<string, object>`、匿名对象或自由 JSON 作为工具 `payload` / `data` 契约。
- 两个 NPC 同时请求同一目标 tile / interaction tile / object 时，只有一个 command 进入 `running`，另一个必须返回 `command_conflict`。
- `cancel_task`、`stuck`、`failed`、`DayEnding`、`Saving` 后必须释放 claim。
- Stardew 动作工具不得进入 Hermes `ParallelSafeTools` 白名单；只读观察工具进入白名单前必须有只读缓存测试。
- Bridge 默认绑定 `127.0.0.1`，测试必须证明不会绑定 `0.0.0.0`。
- 除 `/health` 外，无 `Authorization: Bearer <bridgeToken>` 的请求必须返回 `bridge_unauthorized`。
- `/health` 无 token 时只能返回最小健康信息，不能泄露 command / NPC / save runtime 状态。

双 NPC 并行与群聊测试：
- Haley 与 Penny 同时在场稳定存在。
- Haley 与 Penny 各自有一个 `NpcAutonomyLoop`，且同一 `npcId + saveId + profileId` 不会启动第二个 loop。
- 两个 loop 同时存活时，LLM request 经过 `NpcAutonomyBudget`；MVP 默认同一时间只有一个 LLM request 在 flight。
- 一个 NPC 的 `Agent.ChatAsync(...)` 达到预算后，supervisor 会记录 exit reason，并在 cooldown 后用同一 session 续跑；不能静默死亡。
- 剧情 / 节日 / 菜单 / `DayEnding` / `Saving` 触发时，loop 能进入 `paused` / `stopping`，不再下发新物理命令。
- 记忆不串、人设不串。
- 玩家在场时可触发群聊。
- 玩家点名某个 NPC 时，对应 NPC 正确响应。
- 玩家点名 Haley 时，Penny 只能旁听，不能把命令当成自己的命令。
- 玩家点名 `Haley`、`haley`、`海莉` 都必须归一化到 `npcId = haley`。
- 未知或歧义名字必须返回 `invalid_target`，不能由模型猜测目标 NPC。
- NPC 完成点名命令后调用 `complete_command(commandId)`，下轮不重复处理。

Prompt 与 skill 注入测试：
- 单 NPC runtime 的最终 messages 顺序符合当前 Hermes `PromptBuilder` 方向。
- `SOUL.md` 中的人格不会被初始化 prompt 重复覆盖。
- `EnabledSkillNames` 中的 Stardew skill prompt 会进入稳定系统上下文。
- `skills_required` 的 `stardew-core`、`stardew-social` 与 `stardew-navigation` 必须同时出现在 Haley / Penny runtime 的 prompt preview，因为 Phase 1 已把最小 `move` 作为硬要求。
- 未启用 skill 不会进入该 NPC runtime。
- 不依赖 `triggers` 自动触发核心规则。
- 未满足能力门控时，`stardew-farming`、`stardew-town-schedule` 不能进入稳定系统上下文；`stardew-navigation` 在 Phase 1 只能注入最小 `move` 相关规则，不能提前暴露 `goto / follow / interact`。
- skill prompt 不得出现 Haley / Penny 的人格正文、口癖、关系设定或角色背景。
- skill prompt 不得出现未注册工具名，也不得内联自造 HTTP path、DTO、错误码或 command status enum。
- `StardewSkillBinding` 必须从 `skills/gaming/stardew-*.md` 源文件读取 prompt。
- seed pack 缺 `SOUL.md`、`facts.md`、`voice.md`、`boundaries.md` 或 `skills.json` 时，runtime factory 必须失败并给出明确错误。
- prompt preview 必须证明人格只出现在 `SoulContext` 层，不出现在初始化 prompt / tool prompt / command prompt 的重复副本中。

可观测性与调试测试：
- 每个 NPC runtime 有独立 transcript 与 activity。
- 每个 NPC runtime 有独立 audit log，且为 JSONL、SQLite 表或等价结构化格式。
- activity / audit 记录包含 `npcId`、`saveId`、`profileId`、`traceId`、涉及命令时包含 `commandId`。
- `sdv debug prompt <npcId>` 能导出最近一次最终 messages。
- `sdv debug trace <traceId>` 能串起 tool call、command service、bridge request、SMAPI command 状态。
- `sdv debug audit --npc <npcId>` 能查看该 NPC 最近审计事件，并按 `traceId` / `commandId` 定位。
- MVP 验收要求文件化 / CLI 可查询证据、SMAPI overlay 和 Desktop 最小调试面；完整 UI 面板、复杂过滤和可视化回放属于 Phase 3+。

MVP 通过条件：
1. Haley 单 NPC 闭环通过。
2. Penny 单 NPC 闭环通过。
3. 双 NPC 并行通过。
4. 生命周期通过。
5. 稳定性通过。
6. Bridge 契约与 trace 调试通过。
7. `stardew-core` / `stardew-social` / `stardew-navigation` bundled markdown、`StardewSkillBinding`、golden prompt preview 通过。
8. 最小 `move` 闭环、resource claim、status、failure reason、SMAPI overlay、Desktop 最小调试面通过。
9. 当前 Hermes 已有能力与 Stardew 新增约束的测试边界清楚，不把未实现能力当作现成依赖。

### Detailed Risks And Mitigations

| Risk | Mitigation |
| --- | --- |
| 端口冲突 | 端口范围 + 自动回退 + 发现文件 |
| 身份绑定错误 | 注册表 + 工厂注入，禁止靠提示词绑定 |
| 多 NPC 记忆 / 人格串写 | 每 NPC 独立 memory / soul / session / transcript / runtime home |
| 桥接层做太多语义转述 | 桥接层只暴露能力接口，不提供语义摘要 |
| 群聊复杂度过早膨胀 | 完整设计保留，MVP 只实现玩家在场群聊 |
| UI 集成过早导致范围失控 | 模式 A 先行，模式 B 后置 |
| CLI / tool / UI 各写一套命令逻辑 | `StardewCommandService` 为唯一命令服务源 |
| Prompt 顺序被实现阶段重写 | 沿用 `ContextManager` / `PromptBuilder` 方向，用 golden prompt preview 固定 |
| SocialRouter 演变成剧情导演 | SocialRouter 只投递 envelope，关系、情绪、是否回应由各 NPC Agent 决定 |
| 跨存档 / 跨人格污染 | runtime 路径包含 `gameId + saveId + npcId + profileId` |
| HTTP 线程直接改 Stardew 游戏对象 | HTTP handler 只入队，`UpdateTicked` 执行 |
| 照搬 Minecraft 动作工具 | MVP 不引入 Minecraft 风格动作；扩展阶段定义 Stardew-native task |
| Bridge API 契约自由发挥 | `StardewBridgeDtos.cs` 与 `StardewCommandContracts.cs` 是唯一契约源 |
| 跨天任务盲目续跑 | 物理命令跨天转 `blocked` / `expired` / `cancelled`，第二天重新观察 |
| 多 NPC 出错后无法定位 | per-NPC transcript / activity / audit / trace / prompt preview |
| 新增第二配置源 | Stardew 配置只进入 `HERMES_HOME/config.yaml` 的 `stardew` section |
| 本机 HTTP bridge 被误调用 | loopback + runtime token + stale discovery 校验 |
| 临场生成 Haley / Penny 人格 | 固定 persona seed pack，prompt preview 证明人格未重复注入 |
| 名字混用导致路由错乱 | `StardewNpcCatalog` 是唯一身份映射源 |
| MVP 工具 data 自由发挥 | 每个工具固定 request payload DTO 与 response data DTO |
| 多 NPC 抢同一 tile / object | `ResourceClaimRegistry` 统一裁决，冲突返回 `command_conflict` |
| 自主性退化成事件触发问答 | 活跃 NPC 必须运行受控 `NpcAutonomyLoop` |
| 常驻循环无预算 | `NpcAutonomyBudget` 限制 max tool iterations、并发 LLM request、重启次数 |
| skill prompt 混入人格或自造协议 | `stardew-core` / `stardew-social` 固定职责，DTO / 错误码只引用契约 |
| 必选 skill 来源双轨 | `StardewSkillBinding` 从 bundled `skills/gaming/stardew-*.md` 加载并校验 |

### Expanded Non-Goals

MVP 不做：
- 不接管玩家角色。
- 不改变 Stardew 核心玩法。
- 不实现玩家不在场自然群聊。
- 不实现多进程。
- 不实现复杂经济系统。
- 不实现完整 UI 集成模式 B。
- 不实现通用 `goto` / `interact` / `collect` 这类 Stardew 动作执行工具；Phase 1 只实现最小 `move` 真实闭环，并补完整任务语义、资源互斥和 trace。
- 不实现采集、制作、箱子存取、恢复体力等行为扩展。
- 不实现独立于 `StardewCommandService` 的 CLI 命令逻辑。
- 不新增 `.claude/stardew.json` 或任何绕开 `HERMES_HOME/config.yaml` 的第二配置源。
- 不实现 LAN bridge 暴露、账号系统或复杂权限模型；MVP 只做 loopback + runtime token。
- 不由实现 AI 临场生成 Haley / Penny 核心人格，不把原版大段台词复制进 prompt。
- 不实现模型猜名、模糊匹配或各层自带 alias 表；名字解析必须走 `StardewNpcCatalog`。
- 不用“事件来了才临时问一次 AI”代替 NPC 常驻自主循环。
- 不把 hermescraft 的长会话循环照搬成无预算、无限重启的模型调用。
- 不实现完整 `NpcSocialGraph`、复杂关系演化或离屏 NPC 私聊扩散。
- 不实现完整 `NpcAutonomySupervisor`、复杂自动恢复策略；MVP 只要求 loop 有预算、退出原因、cooldown 与可查 trace。
- 不实现完整可视化调试面板；MVP 要求 CLI / 文件化 prompt preview / trace 查询 / SMAPI overlay / Desktop 最小调试面。

### Target Code Layout

本布局是完整目标地图，不代表 Phase 0 / Phase 1 必须一次性创建所有文件。MVP 只创建闭环所需的最小文件；Phase 3+ 再补完整社交图、监督器增强和 UI 调试面板。

Hermes Desktop 侧：

```text
src/
├─ game/
│  ├─ npc/
│  │  ├─ NpcRegistry.cs
│  │  ├─ NpcIdentity.cs
│  │  ├─ NpcRuntimeBinding.cs
│  │  ├─ NpcRuntimeFactory.cs
│  │  ├─ NpcRuntimeHandle.cs
│  │  ├─ NpcSceneScheduler.cs
│  │  ├─ NpcRuntimeState.cs
│  │  ├─ NpcVisibilitySet.cs
│  │  ├─ NpcCommandQueue.cs
│  │  ├─ NpcSocialGraph.cs
│  │  └─ NpcRuntimeSupervisor.cs
│  └─ stardew/
│     ├─ StardewBridgeOptions.cs
│     ├─ BridgePortDiscovery.cs
│     ├─ StardewNpcCatalog.cs
│     ├─ commands/
│     │  ├─ StardewCommandService.cs
│     │  ├─ StardewCommandRegistry.cs
│     │  ├─ StardewCommandResultRenderer.cs
│     │  └─ StardewCommandContracts.cs
│     ├─ bridge/
│     │  ├─ StardewBridgeClient.cs
│     │  ├─ StardewBridgeHealth.cs
│     │  ├─ StardewBridgeDtos.cs
│     │  └─ StardewBridgeExceptions.cs
│     ├─ personas/
│     │  ├─ haley/default/{SOUL.md,facts.md,voice.md,boundaries.md,skills.json}
│     │  └─ penny/default/{SOUL.md,facts.md,voice.md,boundaries.md,skills.json}
│     ├─ runtime/
│     │  ├─ NpcAutonomyLoop.cs
│     │  ├─ NpcAutonomyBudget.cs
│     │  ├─ StardewSkillBinding.cs
│     │  ├─ StardewRuntimeTrace.cs
│     │  ├─ StardewPromptPreviewStore.cs
│     │  ├─ FestivalOverrideRules.cs
│     │  └─ CutsceneOverrideRules.cs
│     └─ social/
│        ├─ SocialRouter.cs
│        ├─ MessageEnvelope.cs
│        ├─ SocialEventLog.cs
│        └─ OverheardMessageStore.cs
├─ Tools/
│  └─ Stardew/
│     ├─ StardewGameStatusTool.cs
│     ├─ StardewSelfStateTool.cs
│     ├─ StardewNearbyTool.cs
│     ├─ StardewLookTool.cs
│     ├─ StardewReadChatTool.cs
│     ├─ StardewChatTool.cs
│     ├─ StardewCommandsTool.cs
│     ├─ StardewCompleteCommandTool.cs
│     ├─ StardewOverhearTool.cs
│     ├─ StardewSocialTool.cs
│     ├─ StardewTaskStatusTool.cs
│     ├─ StardewCancelTaskTool.cs
│     ├─ StardewMarkTool.cs
│     └─ StardewMarksTool.cs
└─ cli/
   └─ sdv/
      ├─ SdvCliAdapter.cs
      └─ SdvDebugCommands.cs
```

目录硬规则：
- `Tools/Stardew/*Tool.cs` 与 `cli/sdv/SdvCliAdapter.cs` 只能调用 `StardewCommandService`。
- `goto`、`interact`、`collect`、`deposit`、`withdraw`、`craft`、`eat` 只能放入后续 `ExperimentalActions` / 扩展阶段，不能混入 MVP 工具目录。
- `StardewSkillBinding.cs` 只能负责读取、校验、拼接和注入 skill prompt，不得内联 `stardew-core` / `stardew-social` 正文。
- `personas/**/skills.json` 只能列 skill 名称，不能包含 skill prompt 正文。
- `NpcSocialGraph.cs`、完整 `NpcAutonomySupervisor.cs`、完整 `StardewPromptPreviewStore.cs` 可在目录中预留，但 MVP 不得因此实现复杂社交演化、自动恢复策略或 UI 面板双轨。

SMAPI mod 侧：

```text
mods/
└─ StardewHermesBridge/
   ├─ StardewHermesBridge.csproj
   ├─ ModEntry.cs
   ├─ manifest.json
   ├─ config.json
   ├─ Config/BridgeConfig.cs
   ├─ Server/{BridgeServer.cs,RouteTable.cs,JsonHelpers.cs,PortBinder.cs}
   ├─ Queries/{StatusQuery.cs,SelfStateQuery.cs,NearbyQuery.cs,LookQuery.cs,ReadChatQuery.cs,OverhearQuery.cs,SocialQuery.cs,CommandsQuery.cs,TaskStatusQuery.cs,MarkQuery.cs,MarksQuery.cs}
   ├─ Actions/{ChatAction.cs,CompleteCommandAction.cs,CancelTaskAction.cs}
   ├─ ExperimentalActions/{GotoAction.cs,InteractAction.cs,CollectAction.cs}
   ├─ Runtime/{NpcLocator.cs,VisibilityScanner.cs,TaskTracker.cs,CommandQueue.cs,ResourceClaimRegistry.cs,BridgeTraceLog.cs,PromptPreviewRecorder.cs,NearbyNpcTracker.cs,GroupConversationTracker.cs,FestivalHooks.cs,CutsceneHooks.cs}
   └─ Social/{ChatRouter.cs,SocialEventRecorder.cs,OverheardRouter.cs}
```

### Configuration And Data Layout

Hermes Desktop 侧配置必须写入现有 Hermes Desktop 配置文件：

```text
<HERMES_HOME>/config.yaml
```

示例 section：

```yaml
stardew:
  bridge_default_host: 127.0.0.1
  bridge_default_port: 8745
  bridge_port_range: 8745-8765
  bridge_allow_lan: false
  bridge_token_source: generated_runtime
  bridge_discovery_max_age_seconds: 30
  bridge_discovery_file: mods/StardewHermesBridge/runtime/bridge-port.json
  npc_catalog_source: bundled
  npc_default_cast: haley,penny
  npc_max_active_scene_agents: 3
  npc_default_profile_id: default
  npc_autonomy_max_tool_iterations: 100
  npc_autonomy_max_concurrent_llm_requests: 1
  npc_autonomy_restart_cooldown_seconds: 5
  npc_autonomy_max_restarts_per_scene: 3
  npc_autonomy_start_stagger_seconds: 4
  skills_required: stardew-core,stardew-social,stardew-navigation
  skills_optional: stardew-farming,stardew-town-schedule
  mvp_enable_player_present_group_chat_only: true
  mvp_enable_offscreen_npc_chat: false
```

配置硬规则：
- 禁止新增 `.claude/stardew.json`。
- 禁止新增项目目录内 Stardew 专用配置文件作为主配置源。
- `StardewOptions` / `StardewConfig` 如需新增，只能从 `HERMES_HOME/config.yaml` 的 `stardew` section 读取。
- `bridge-port.json` 是 SMAPI bridge 的运行时发现文件，不是 Hermes 配置源。
- `bridge_allow_lan` MVP 必须保持 `false`。
- `bridge_token_source` MVP 只能是 `generated_runtime`，不读取用户手写固定 token。
- `npc_default_cast` 只能写 `npcId`，不能写显示名、中文名或 SMAPI 内部名。
- 配置放在 `config.yaml`；运行时状态、trace、prompt preview、NPC profile 数据放在 `HermesHomePath/runtime/stardew/`。

Hermes Desktop 侧运行时数据目录：

```text
runtime/stardew/
├─ bridge/
│  ├─ bridge-port-cache.json
│  ├─ bridge-health.json
│  └─ last-connection.json
├─ games/
│  └─ stardew-valley/
│     └─ saves/
│        └─ <saveId>/
│           ├─ npc/
│           │  ├─ haley/profiles/default/
│           │  │  ├─ SOUL.md
│           │  │  ├─ USER.md
│           │  │  ├─ runtime.json
│           │  │  ├─ marks.json
│           │  │  ├─ social.json
│           │  │  ├─ scene-session.json
│           │  │  ├─ memory/
│           │  │  ├─ transcripts/
│           │  │  ├─ activity/
│           │  │  ├─ traces/
│           │  │  └─ prompt-preview/
│           │  └─ penny/profiles/default/
│           │     ├─ SOUL.md
│           │     ├─ USER.md
│           │     ├─ runtime.json
│           │     ├─ marks.json
│           │     ├─ social.json
│           │     ├─ scene-session.json
│           │     ├─ memory/
│           │     ├─ transcripts/
│           │     ├─ activity/
│           │     ├─ traces/
│           │     └─ prompt-preview/
│           └─ registry-cache.json
└─ scheduler/
   ├─ active-handles.json
   ├─ visibility-state.json
   └─ recovery-state.json
```

路径硬规则：
- `stardew-valley` 是 `gameId`。
- `<saveId>` 必须来自当前 Stardew 存档身份。
- `haley` / `penny` 是 `npcId`。
- `default` 是 MVP 的 `profileId`，不可省略。
- 任何记忆、transcript、marks、social、runtime 状态都必须落在 profile 目录下。
- 上述 `transcripts/`、`activity/` 可以是目录边界；内部实际用 JSONL、SQLite 或其他结构化形式必须以当前实现和测试为准。

SMAPI mod 侧运行时目录：

```text
mods/StardewHermesBridge/
├─ config.json
├─ manifest.json
├─ runtime/
│  ├─ bridge-port.json
│  ├─ health.json
│  ├─ task-state.json
│  ├─ visibility-cache.json
│  └─ bridge-trace.jsonl
└─ data/
   ├─ npc-catalog.json
   ├─ map-cache.json
   └─ event-cache.json
```

布局原则：
- 静态配置与运行时状态分离。
- 桥接状态与调度状态分离。
- 每个 NPC profile 拥有自己的 `SOUL / USER / memory / transcripts / marks / social / runtime` 文件边界。
- 优先用文件化 / 目录化边界表达隔离；具体 transcript / activity 后端以当前代码和测试为准。

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

### Round 6
**Q:** 2026-04-27 大方案已经落后，是否仍要纳入本次审查？
**A:** 确实已经落后，因为最近修改了记忆功能等，但是里面很多内容仍然值得借鉴；豁免 `omx question` 的 tmux pane 要求，吸收 deep-interview 精神即可。
**Resolution:** 旧大方案降级为“参考约束库”，不作为当前代码事实源；保留其中仍正确的硬约束，并用当前代码事实修正 transcript / memory 等已变化内容。

</details>
