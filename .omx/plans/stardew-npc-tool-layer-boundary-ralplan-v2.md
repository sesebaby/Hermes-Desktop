# Stardew NPC Tool 层分层边界修复 - RALPLAN-DR v2

## Plan Summary

**Plan saved to:** `.omx/plans/stardew-npc-tool-layer-boundary-ralplan-v2.md`

**Scope:**
- 约 8-10 个文件
- 预计复杂度：MEDIUM

**Key Deliverables:**
1. 将 `stardew_move` 的 owner 边界写死到 tool / world skill / navigation skill 三层，不再使用“stardew-world 或 stardew-navigation”这种模糊归属。
2. 以 repo-backed 测试和 bridge 回归清单锁定边界，确保文案收敛不破坏 `path_blocked` / `path_unreachable` / runtime replan 的既有可靠性修复。

## RALPLAN-DR

### Principles

1. `src/games/stardew/StardewNpcTools.cs` 只负责局部调用契约 owner：参数必须来自最新观察、不得编坐标、不承诺 route-guaranteed、runtime 自动绑定身份上下文。
2. `skills/gaming/stardew-world/SKILL.md` 只负责地点意义与候选解释 owner：`label/tags/reason/endBehavior` 的世界语义、为什么某地点值得去、`placeCandidate` / endpoint candidate 的意义。
3. `skills/gaming/stardew-navigation.md` 只负责移动循环与失败恢复 owner：必须基于最新观察调用 `stardew_move`，遇到 `path_blocked` / `path_unreachable` 后先重观察或换目标，不能直接 HTTP，非私聊事件不直接驱动移动。
4. schema 也是护栏，不只是 description：`src/games/stardew/StardewNpcTools.cs` 中 `x` / `y` / `reason` / `facingDirection` 的 provenance 描述不得因文案收缩而丢失。
5. 用真实仓库资产锁边界，而不是只用 fixture 伪造文本：涉及 `skills/gaming/*` owner 的断言必须至少有一层 repo-backed 测试。

### Decision Drivers

1. 保持最新移动可靠性修复完整，尤其是 stable `path_blocked`、stable `path_unreachable`、bounded replan、claim release。
2. 消除 tool/world/navigation 的 owner 漂移，让后续修改知道该改哪一层，而不是继续把世界语义塞回 `StardewNpcTools.cs`。
3. 让测试真正绑定仓库中的 skill/prompt 资源，而不是继续依赖临时 fixture 掩盖边界回退。

### Viable Options

#### Option A: 仅收缩 `StardewNpcTools.cs` description，并最小化测试改动

**Pros**
- 改动最小。
- 几乎不碰 prompt/skill 层文本。

**Cons**
- owner 仍不够明确，未来容易把地点意义或失败恢复再写回 tool。
- 无法满足 repo-backed 测试要求。
- schema provenance 护栏容易被遗漏，因为测试仍主要盯 description。

#### Option B: 写死三层 owner + 保留 schema provenance + 把测试锚点改为 repo-backed

**Pros**
- 与 Architect 反馈完全一致，边界清晰且可验证。
- 能同时保护 description 与 schema 两类护栏。
- 可直接把 `BridgeMoveFailureMapperTests.cs` 纳入冻结清单，保护 stable `path_unreachable`。

**Cons**
- 需要同步调整测试策略。
- 会触及 prompt supplement / skill root 读取路径，测试设计需要更谨慎。

#### Option C: 顺带重构 skill 目录结构或 prompt supplement 载入机制

**Pros**
- 理论上能进一步减少未来重复。

**Cons**
- 超出本次“边界修订计划”范围。
- 放大风险，且与“只修计划、不改源码”的当前目标不匹配。

**Invalidation rationale**
- Option C 不纳入本轮：它把边界修复扩大成架构整理，超出最小必要范围。

### Recommended Option

推荐 **Option B**。

理由：本轮需要的是“owner 写死 + 护栏补齐 + 测试落地”，不是只缩一句 description。Option B 可以把世界语义、移动循环、局部调用契约拆到固定 owner，同时显式保护 schema provenance 与 bridge 错误码回归，范围仍可控。

## Requirements Summary

1. `skills/gaming/stardew-world/SKILL.md` 必须被计划写成地点意义与候选解释 owner，不再与 navigation 共享此职责。
2. `skills/gaming/stardew-navigation.md` 必须被计划写成移动循环与失败恢复 owner，并显式写出“最新观察 -> `stardew_move` -> `path_blocked/path_unreachable` 后重观察或换目标 -> 不能直接 HTTP -> 非私聊事件不直接驱动移动”。
3. `src/games/stardew/StardewNpcTools.cs` 必须被计划写成局部调用契约 owner，并显式保留 `moveCandidate/placeCandidate` 来源、禁止编坐标、非 route-guaranteed、runtime 自动绑定。
4. `src/games/stardew/StardewNpcTools.cs` 的 move schema 中 `x/y/reason/facingDirection` provenance 描述必须被视为稳定护栏，不得因为 description 变短而丢掉。
5. `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs` 当前大量使用 fixture 写入临时 `skills/gaming/*`，不足以证明真实 repo owner 边界；计划必须新增或调整 repo-backed 测试，直接读取真实 `skills/gaming/*` 或 prompt supplement 的真实 skill root。
6. 冻结与验证清单必须显式包含 `Mods/StardewHermesBridge.Tests/BridgeMoveFailureMapperTests.cs`，保护 stable `path_unreachable`。
7. 文档步骤必须写成“创建或更新 `docs/星露谷NPC分层边界与HermesCraft对比说明.md`”；执行时若该文件已以 untracked 形式存在，则原位更新，不假设一定新建。

## Implementation Steps

1. 先冻结现有可靠性与边界护栏。
   文件：
   - `src/games/stardew/StardewNpcTools.cs`
   - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
   - `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
   - `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`
   - `Mods/StardewHermesBridge.Tests/BridgeMoveFailureMapperTests.cs`
   目标：
   - 列出当前必须保留的稳定项：`moveCandidate/placeCandidate` 来源、非 route-guaranteed、`path_blocked`、`path_unreachable`、claim release、schema provenance。
   - 明确 `BridgeMoveFailureMapperTests.cs` 是 stable `path_unreachable` 的冻结点，不能在边界收敛时被遗漏。

2. 把三层 owner 写死到计划执行目标中。
   文件：
   - `skills/gaming/stardew-world/SKILL.md`
   - `skills/gaming/stardew-navigation.md`
   - `src/games/stardew/StardewNpcTools.cs`
   调整方向：
   - `stardew-world` owner：`label/tags/reason/endBehavior`、地点价值解释、`placeCandidate` / endpoint candidate 的世界语义。
   - `stardew-navigation` owner：最新观察驱动的移动循环、`stardew_move` 调用、`path_blocked/path_unreachable` 后的重观察或换目标、禁止直接 HTTP、非私聊事件不直接驱动移动。
   - `StardewNpcTools.cs` owner：只保留局部调用契约，不再承担地点为何值得去、地点标签含义、世界层候选解释。

3. 收敛 tool description 时，同时保护 schema provenance。
   文件：
   - `src/games/stardew/StardewNpcTools.cs`
   调整方向：
   - description 保留：目标来自最新观察、不能编坐标、失败后重观察或换目标、不承诺 route-guaranteed、runtime 自动绑定。
   - schema 保留：`x` / `y` / `reason` / `facingDirection` 都继续明确“copied from current/latest observation candidate”的 provenance。
   - 禁止出现“description 缩短了，所以 schema provenance 也一并删掉”的回退。

4. 把测试从 fixture-only 调整为 fixture + repo-backed 双层验证。
   文件：
   - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
   - `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
   - 如需要，新增同目录下更窄职责测试文件用于 repo-backed prompt supplement 读取
   调整方向：
   - 保留现有 fixture 测试去验证载入机制和缺失文件报错。
   - 新增或调整至少一组 repo-backed 测试：直接读取真实 `skills/gaming/stardew-navigation.md`、`skills/gaming/stardew-world/SKILL.md`，或通过 `StardewNpcAutonomyPromptSupplementBuilder` 指向真实 `skills/gaming` root，验证 owner 边界实际存在于仓库资产中。
   - 让 `StardewNpcToolFactoryTests` 同时断言 description 与 schema provenance，而不是只断 description。
   - 让 `StardewAutonomyTickDebugServiceTests` 至少有一条真实 skill-root 测试验证 world/navigation owner 被实际注入 prompt supplement，而不仅是注入临时 fixture 文本。

5. 校准文档与人格层落点，但不扩大范围。
   文件：
   - `src/game/stardew/personas/haley/default/SOUL.md`
   - `src/game/stardew/personas/haley/default/facts.md`
   - `docs/星露谷NPC分层边界与HermesCraft对比说明.md`
   调整方向：
   - persona 只在与新边界冲突时轻量校准，继续承载 Haley 偏好，不把偏好拉回 tool。
   - 创建或更新 `docs/星露谷NPC分层边界与HermesCraft对比说明.md`，明确三层 owner、schema 护栏、repo-backed 测试策略。
   - 执行时若该文档已以 untracked 形式存在，则更新现有文件，不假设必须新建。

6. 执行 targeted regression，确认边界收敛未破坏可靠性。
   测试路径：
   - `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter StardewNpcToolFactoryTests`
   - `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter StardewAutonomyTickDebugServiceTests`
   - `dotnet test .\\Mods\\StardewHermesBridge.Tests\\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter BridgeMoveFailureMapperTests`
   - `dotnet test .\\Mods\\StardewHermesBridge.Tests\\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter BridgeMoveCommandQueueRegressionTests`
   - 如 prompt supplement / pack 绑定影响面扩大，再补跑：`dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug`

## Acceptance Criteria

1. 计划明确写死 owner：
   - `skills/gaming/stardew-world/SKILL.md` 是地点意义与候选解释 owner。
   - `skills/gaming/stardew-navigation.md` 是移动循环与失败恢复 owner。
   - `src/games/stardew/StardewNpcTools.cs` 是局部调用契约 owner。

2. `StardewNpcTools.cs` 的 `stardew_move` description 最终仍保留以下最小契约：
   - 参数来自最新观察的 `moveCandidate` 或 `placeCandidate`
   - 不允许编造坐标
   - 不承诺 route-guaranteed
   - `path_blocked` / `path_unreachable` 后重观察或换目标
   - runtime 自动绑定 `npcId/saveId/traceId/idempotency`

3. `StardewNpcTools.cs` 的 move schema 继续保留 `x` / `y` / `reason` / `facingDirection` 的 provenance 描述，且对应测试会在 description 之外单独断言这些字段来源于当前/最新 observation candidate。

4. 至少一条 repo-backed 测试直接读取真实 `skills/gaming/stardew-navigation.md` 和/或 `skills/gaming/stardew-world/SKILL.md`，或经真实 skill root 构建 prompt supplement，验证 owner 边界不是只存在于 fixture 文本。

5. `StardewAutonomyTickDebugServiceTests` 继续证明：
   - `placeCandidate` 对模型可见
   - 没有 tool call 时不会 host-side 自动移动
   - 非私聊事件不会直接驱动移动

6. `BridgeMoveFailureMapperTests.cs` 通过，并继续保护初始不可达路径映射为 stable `path_unreachable`，不被收敛为 `path_blocked` 或其他漂移词汇。

7. `BridgeMoveCommandQueueRegressionTests.cs` 继续保护 runtime step blockage 最终走 stable `path_blocked`，并释放 claim / action slot。

8. `docs/星露谷NPC分层边界与HermesCraft对比说明.md` 被创建或更新，且内容反映最终 owner 边界与测试策略。

## Risks And Mitigations

1. 风险：description 收缩时顺手删掉 schema provenance。
   缓解：把 schema provenance 升级为 Acceptance Criteria，并在 `StardewNpcToolFactoryTests` 中单独断言。

2. 风险：继续依赖 fixture 测试，导致真实 `skills/gaming/*` 被改坏却不报警。
   缓解：新增 repo-backed 测试，直接读取真实 skill 文件或真实 skill root。

3. 风险：world/navigation owner 写得太散，执行时再次交叉覆盖。
   缓解：在文档和测试中都用“写死 owner”措辞，不再使用“world 或 navigation”。

4. 风险：边界收敛误伤 bridge 错误码稳定性。
   缓解：显式补入 `BridgeMoveFailureMapperTests.cs` 与 `BridgeMoveCommandQueueRegressionTests.cs` 作为 targeted regression。

## Verification Steps

1. 人工核对三层 owner 是否唯一且无交叉主解释责任：
   - world 只管地点意义与候选解释
   - navigation 只管移动循环与恢复
   - tool 只管局部调用契约

2. 运行 `StardewNpcToolFactoryTests`，确认：
   - description 仍保留最小契约
   - schema 仍保留 `x/y/reason/facingDirection` provenance

3. 运行 `StardewAutonomyTickDebugServiceTests`，确认：
   - 真实 skill root 或真实 repo skill 文件可被 prompt supplement 读取
   - `placeCandidate` 可见性、无 tool call 不自动移动、非私聊事件不直接驱动移动均未回退

4. 运行 `BridgeMoveFailureMapperTests`，确认初始不可达仍是 stable `path_unreachable`。

5. 运行 `BridgeMoveCommandQueueRegressionTests`，确认运行时阻塞失败仍是 stable `path_blocked`，并保留 claim release / action slot release。

6. 若 prompt supplement / persona / docs 调整带来更广影响，再补跑 `Desktop/HermesDesktop.Tests` 全量。

## ADR

**Decision**
- 将 `stardew_move` 的边界改为固定三层 owner：`StardewNpcTools.cs` 负责局部调用契约，`stardew-world` 负责地点意义与候选解释，`stardew-navigation` 负责移动循环与失败恢复；同时把 move schema provenance 当作稳定护栏，并用 repo-backed 测试验证。

**Drivers**
- 防止世界语义、失败恢复和局部调用契约继续混写在同一层。
- 保护最新移动可靠性修复，尤其是 stable `path_blocked` / `path_unreachable`。
- 让测试真正绑定真实仓库 skill 资源，而不是只绑定 fixture 文本。

**Alternatives considered**
- 仅缩短 tool description，不动 skill/test：实现快，但 owner 仍漂移，且不满足 repo-backed 测试要求。
- 顺带重构 skill root / prompt supplement 体系：超出本次范围，风险过大。

**Why chosen**
- 该方案以最小必要范围满足 Architect 反馈，并把“谁拥有哪条规则”写死在计划、测试和文档里，后续执行可直接落地。

**Consequences**
- 以后新增地点语义时，默认先改 `stardew-world`，不是扩写 tool。
- 以后新增移动恢复策略时，默认先改 `stardew-navigation`，不是改 world/tool。
- 以后若想删减 move schema 文案，必须先通过 provenance 护栏测试。

**Follow-ups**
- 如果执行后仍发现 world/navigation 文案重叠，再单开一次低风险文案整理任务。
- 若其他 NPC persona 引入地点偏好，沿用同一边界：偏好进 persona，局部调用约束留在 tool。

## Execution Handoff

### Ralph 单人顺序执行

推荐作为默认执行路径。

建议 lane：
- `executor`，`reasoning_effort=high`
- 收尾验证加 `verifier`，`reasoning_effort=high`

顺序：
1. 冻结 description/schema/bridge 护栏
2. 收敛 `StardewNpcTools.cs`
3. 校准 `stardew-world` 与 `stardew-navigation`
4. 调整 repo-backed 测试
5. 创建或更新文档
6. 跑 targeted regression

Ralph handoff 要点：
- 先做测试锚点调整方案，再改文本，避免“改完才发现测不到真实 skill root”。
- 最终必须汇报 description 护栏、schema 护栏、repo-backed 测试、bridge 回归四类证据。

### Team 并行执行

仅在希望并行处理文本层与测试层时使用，建议 3 lane。

- Lane 1: `executor`，`reasoning_effort=high`
  负责 `src/games/stardew/StardewNpcTools.cs` 的 description/schema 边界。
- Lane 2: `writer` 或 `executor`，`reasoning_effort=medium`
  负责 `skills/gaming/stardew-world/SKILL.md`、`skills/gaming/stardew-navigation.md`、`docs/星露谷NPC分层边界与HermesCraft对比说明.md`。
- Lane 3: `test-engineer`，`reasoning_effort=high`
  负责 `Desktop/HermesDesktop.Tests/Stardew/*` 的 repo-backed 测试，以及 bridge 冻结清单校准。

共享冲突热点：
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `src/games/stardew/StardewNpcTools.cs`

### Team Verification Path

Team shutdown 前必须共同证明：
1. owner 边界在 world/navigation/tool 三层已写死，且文案无交叉主解释责任。
2. `StardewNpcToolFactoryTests` 能同时证明 description 护栏和 schema provenance 护栏。
3. `StardewAutonomyTickDebugServiceTests` 中至少一条 repo-backed 测试直接读取真实 skill 文件或真实 skill root。
4. `BridgeMoveFailureMapperTests` 仍保护 stable `path_unreachable`。
5. `BridgeMoveCommandQueueRegressionTests` 仍保护 stable `path_blocked` 和 claim release。

Ralph / leader 在 team handoff 后再统一验证：
1. targeted tests 的最终命令与结果被完整记录。
2. 文档文件是更新既有 untracked 副本还是新建文件，路径处理无重复。
3. 最终总结明确区分 owner、schema 护栏、repo-backed 测试、bridge 回归四类成果。

**Available agent types roster:**
- `planner`
- `architect`
- `critic`
- `executor`
- `writer`
- `test-engineer`
- `verifier`

**Launch hints:**
- Ralph：`$ralph 按 .omx/plans/stardew-npc-tool-layer-boundary-ralplan-v2.md 执行，先锁定 schema provenance 与 repo-backed 测试，再收敛文案`
- Team：`$team 按 .omx/plans/stardew-npc-tool-layer-boundary-ralplan-v2.md 执行，3 lane 并行：tool、skills/docs、tests`

## Applied Architect Feedback

1. 将 `stardew-world`、`stardew-navigation`、`StardewNpcTools.cs` 的 owner 直接写死，不再使用模糊二选一表述。
2. 将 move schema 的 `x/y/reason/facingDirection` provenance 提升为显式护栏和验收项。
3. 将 `StardewAutonomyTickDebugServiceTests` 从 fixture-only 升级为必须包含 repo-backed 测试。
4. 将 `Mods/StardewHermesBridge.Tests/BridgeMoveFailureMapperTests.cs` 补入冻结与验证清单，显式保护 stable `path_unreachable`。
5. 文档步骤改为“创建或更新 `docs/星露谷NPC分层边界与HermesCraft对比说明.md`”，并注明执行时优先更新已存在的 untracked 文件。

**Does this plan capture your intent?**
- `proceed` - 展示可执行的下一步命令
- `adjust [X]` - 按指定点继续修订
- `restart` - 丢弃这版重新起草
