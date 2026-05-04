# Stardew NPC Tool 层分层边界修复 - RALPLAN-DR Short-Mode 初稿

## Plan Summary

**Plan saved to:** `.omx/plans/stardew-npc-tool-layer-boundary-ralplan-short-draft.md`

**Scope:**
- 约 6-8 个文件
- 预计复杂度：MEDIUM

**Key Deliverables:**
1. 收敛 `stardew_move` 为最小可执行 tool 契约，不再承载可迁出的世界/人格语义。
2. 用 skill / persona / tests 明确并锁定新的分层边界，同时不破坏已完成的移动可靠性修复。

## RALPLAN-DR

### Principles

1. Tool 层只保留最小可执行契约：参数边界、禁止编造、失败后处理约束、runtime 自动绑定。
2. 世界语义归 skill，角色偏好归 persona；同一规则不在 tool 中重复承担主解释责任。
3. 任何边界收敛都不能回退已完成的移动可靠性修复，尤其是 `moveCandidate` / `placeCandidate` 使用、`path_blocked` / `path_unreachable` 契约和 runtime replan 保护。
4. 以测试锁边界而不是靠口头约定：tool 描述、autonomy prompt 可见性、host 不自动移动都要有可验证证据。

### Decision Drivers

1. 保持移动可靠性修复完整，不引入行为回退。
2. 让 `StardewNpcTools.cs` 与 HermesCraft 风格一致，只承担动作接口而不是领域解释。
3. 把长期可维护的世界/人格语义放到文本资产层，减少后续重复维护和边界漂移。

### Viable Options

#### Option A: 仅微调 `StardewNpcTools.cs` 文案，其他层基本不动

**Pros**
- 改动最小，最不容易碰到现有可靠性行为。
- 可以快速去掉最明显的越界措辞。

**Cons**
- skill / persona / docs 的边界声明仍然不够显式，未来容易反弹回 tool。
- 现有测试仍可能继续把世界语义绑死在 tool 字符串上。
- 不能系统性回答“哪些语义必须在 skill / persona”的治理问题。

#### Option B: Tool 收敛 + skill/persona 明确补位 + 测试重定锚点

**Pros**
- 与现有分层目标一致，tool / skill / persona 各自职责更清楚。
- 可以把测试从“检查 tool 是否复述世界语义”改成“检查边界是否正确分布”。
- 能在不改 bridge 行为的前提下，长期稳定约束 prompt/tool 责任。

**Cons**
- 涉及文件更多，需要同步调整文案和测试锚点。
- 如果测试拆分不够谨慎，容易误伤刚完成的移动可靠性修复。

#### Option C: 进一步把 world / navigation 重复内容合并重写

**Pros**
- 理论上可以减少 skill 间重复。
- 最终文案可能更整洁。

**Cons**
- 当前任务核心是修复边界，不是重构 skill 体系。
- 会扩大变更面，增加误伤移动可靠性修复的概率。
- 对短期收益不成比例，且会引入新的 review 成本。

**Invalidation rationale**
- Option C 当前不推荐：它把“边界修复”扩大成“技能体系整理”，超出本次最小必要范围。

### Recommended Option

推荐 **Option B**。

理由：它在不动 bridge 行为、不新增依赖、不改变 cross-location unsupported 和失败契约的前提下，把最小 tool 契约、世界语义、角色偏好分别锚定到正确层，并通过测试防止回退。相比 Option A，它不是只改一句文案，而是把边界治理做完整；相比 Option C，它又控制住了范围。

## Implementation Steps

1. 先冻结现有可靠性边界与回归面。
   文件：
   - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
   - `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
   - `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`
   目标：
   - 确认现有测试继续覆盖 `moveCandidate` / `placeCandidate` 可用性、`path_blocked` / `path_unreachable` 后续策略、host 不自动移动。

2. 收敛 tool 描述到最小契约，不移除关键安全/可靠性约束。
   文件：
   - `src/games/stardew/StardewNpcTools.cs`
   调整方向：
   - 保留：只能用最新观察候选、不能编坐标、失败后重新观察/换目标、runtime 自动绑定。
   - 移出或压缩：`placeCandidate` 的长篇世界语义、schedule-style 展开、角色化解释。
   - 明确保留：不承诺 route-guaranteed，不改变 cross-location unsupported，不移除 `path_blocked` / `path_unreachable` 契约。

3. 把世界层语义固定到 skill 层，而不是由 tool 主解释。
   文件：
   - `skills/gaming/stardew-world/SKILL.md`
   - `skills/gaming/stardew-navigation.md`
   调整方向：
   - `stardew-world` 作为 `placeCandidate` / endpoint candidate 的主解释位置。
   - `stardew-navigation` 聚焦移动循环、失败恢复、调用方式。
   - 只做必要去重或引用式收敛，不做大重写。

4. 维持 Haley 偏好在 persona 层，必要时只做轻量校准。
   文件：
   - `src/game/stardew/personas/haley/default/SOUL.md`
   - `src/game/stardew/personas/haley/default/facts.md`
   调整方向：
   - 仅当与新的边界表述不一致时微调。
   - 明确不把 Haley 偏好拉回 `StardewNpcTools.cs`。

5. 重定测试锚点，让测试验证“边界分布”而不是“tool 复述语义”。
   文件：
   - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
   - `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
   调整方向：
   - tool 测试继续要求 `moveCandidate` / `placeCandidate` 可用、禁止 route-guaranteed 承诺、保留失败后恢复约束。
   - 如果移除了 `endpoint candidate` 或 schedule-style 的细节措辞，则把对应语义断言转移到 skill/prompt 相关测试，而不是继续强绑 tool 描述。
   - autonomy 测试继续证明 `placeCandidate` 对模型可见，但没有 tool call 就不会触发 host-side move。

6. 同步文档并记录最终边界，避免后续回摆。
   文件：
   - `docs/星露谷NPC分层边界与HermesCraft对比说明.md`
   目标：
   - 把最终保留在 tool 的最小契约、放到 skill 的世界语义、放到 persona 的偏好边界写清楚。

7. 运行回归验证并检查未破坏移动可靠性修复。
   测试路径：
   - `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter StardewNpcToolFactoryTests`
   - `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter StardewAutonomyTickDebugServiceTests`
   - `dotnet test .\\Mods\\StardewHermesBridge.Tests\\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter BridgeMove`
   - 视影响面补跑：`dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug`

## Acceptance Criteria

1. `src/games/stardew/StardewNpcTools.cs` 中的 `stardew_move` 描述仍保留以下最小契约：
   - 目标来自当前观察的 `moveCandidate` 或 `placeCandidate`
   - 不允许编造坐标
   - `path_blocked` / `path_unreachable` 后重新观察或换目标
   - runtime 自动绑定身份上下文

2. `stardew_move` 描述不再承担 Haley 偏好、地点人格化解释或冗长的 schedule-style 世界语义主说明。

3. `skills/gaming/stardew-world/SKILL.md` 或 `skills/gaming/stardew-navigation.md` 明确承载 `placeCandidate` / endpoint candidate 的世界语义和失败后策略。

4. `src/game/stardew/personas/haley/default/SOUL.md` / `facts.md` 继续承载 Haley 的地点偏好，且 tool 层不再复述这些偏好。

5. `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs` 通过，且不再把可迁移的世界语义错误地锁在 tool 描述里。

6. `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs` 通过，并继续证明 `placeCandidate` 可见但不会自动触发移动。

7. 与 bridge move 可靠性相关测试通过，证明本次修复未破坏已完成的移动可靠性修复。

## Risks

1. 过度收缩 tool 文案，导致模型失去必要的最小执行护栏。
2. 测试改错层，导致“分层变清楚”但可靠性约束被意外放松。
3. 对 `stardew-world` 和 `stardew-navigation` 的收敛过大，扩大任务范围并引入无关回归。

## Verification Steps

1. 先逐个阅读最终改动后的 `stardew_move` 描述、world/navigation skill、Haley persona，核对每条规则只在正确层承担主职责。
2. 运行 `StardewNpcToolFactoryTests`，确认 tool 契约与 route-guarantee 禁止仍在。
3. 运行 `StardewAutonomyTickDebugServiceTests`，确认候选可见性与“无 tool call 不自动移动”不变。
4. 运行 bridge move 相关测试，确认 `path_blocked` / `path_unreachable`、claim release、bounded replan 等可靠性修复未回退。
5. 如文案变动影响更广，再补跑 `Desktop/HermesDesktop.Tests` 全量。

## ADR Draft

**Decision**
- 将 `StardewNpcTools.cs` 收敛为最小可执行 tool 契约；把 `placeCandidate` / endpoint candidate 的世界语义放回 Stardew skills，把 Haley 偏好保留在 persona 文件。

**Drivers**
- 避免 tool 层继续承担 world/persona 解释责任。
- 保护刚完成的移动可靠性修复不被边界清理误伤。
- 降低后续维护时在代码和 prompt 资产间重复修改同一规则的成本。

**Alternatives considered**
- 仅缩短 tool 文案，不动 skill/test：改动最小，但边界治理不足。
- 一次性重构 world/navigation 技能体系：收益有限，超出本次范围。

**Why chosen**
- 该方案在保持现有运行时和 bridge 行为不变的前提下，最小化 tool 责任、最大化边界清晰度，并可由现有测试体系承接验证。

**Consequences**
- 未来新增 Stardew 地点语义时，应优先改 skill / persona，而不是继续扩写 tool 描述。
- 测试将更多验证“规则在哪一层”，而不是只验证某个字符串是否仍在 tool 文案里。

**Follow-ups**
- 如后续仍观察到 world/navigation 文案重复，再单开一个低风险整理任务。
- 若新增其他 NPC persona，沿用同样边界：偏好进 persona，不进 tool。

## Execution Recommendation

### Ralph 单人顺序执行

适合本次任务的默认路径。原因：
- 改动是同一条边界链路，文件之间高度相关。
- 需要先收敛 tool，再调整 skill/persona，再校准测试，串行更容易避免交叉覆盖。

建议 lane：
- `executor`，`reasoning_effort=high`
- 需要补充验证时加 `verifier`，`reasoning_effort=high`

执行顺序：
1. 调整 `StardewNpcTools.cs`
2. 调整 `stardew-world` / `stardew-navigation`
3. 必要时微调 Haley persona
4. 校准 Stardew 测试
5. 跑 targeted tests，再决定是否补跑全量

### Team 并行执行

仅在希望加快文案与测试分离时使用。建议最多 3 lane：
- Lane 1: `executor` 负责 `src/games/stardew/StardewNpcTools.cs`
- Lane 2: `writer` 或 `executor` 负责 `skills/gaming/*` 与 `docs/*`
- Lane 3: `test-engineer` 负责 `Desktop/HermesDesktop.Tests/Stardew/*` 与 bridge 回归清单

并行前提：
- 先冻结共享边界清单：tool 保留项、skill 承载项、persona 禁止回流项。
- 明确 `StardewNpcTools.cs` 与 `StardewNpcToolFactoryTests.cs` 是共享冲突热点，需要最后由 leader 合并。

团队验证路径：
1. 各 lane 先自检本 lane 文件是否遵守边界清单。
2. leader 合并后统一跑：
   - `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter StardewNpcToolFactoryTests`
   - `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter StardewAutonomyTickDebugServiceTests`
   - `dotnet test .\\Mods\\StardewHermesBridge.Tests\\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter BridgeMove`
3. 若任一失败，回到 leader 统一收敛，不让 lane 各自发散修补。

**Available agent types roster:**
- `planner`
- `architect`
- `critic`
- `executor`
- `writer`
- `test-engineer`
- `verifier`

**Launch hints:**
- 顺序执行：`$ralph Stardew NPC tool 层分层边界修复，按已批准计划执行`
- 团队执行：`$team Stardew NPC tool 层分层边界修复，按已批准计划执行`

**Does this plan capture your intent?**
- `proceed` - 我再把它收敛成可执行 handoff 版
- `adjust [X]` - 我按指定点修改计划
- `restart` - 丢弃这版重新起草
