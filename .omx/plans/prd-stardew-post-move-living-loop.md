# PRD：Stardew NPC 行动后生活闭环补充计划

## 目标摘要

本计划补充 `.omx/plans/stardew-tool-orchestration-harness-plan.md`，不替换已有 action lifecycle。

已有闭环解决的是：

`agent 调工具 -> runtime pending -> bridge 执行 -> terminal fact -> wake -> last_action_result`

本补充解决的是：NPC 接受玩家当前世界承诺后，不能只把移动当成一次孤立工具调用。它应该成为 NPC 自己的生活事件：

`接受承诺 -> 写入 NPC session todo -> 提交真实动作 -> terminal fact -> 下一轮看到 todo + last_action_result -> 显式收口`

显式收口只允许三类：

- 用 `todo` 把承诺标成 `completed` / `blocked` / `failed`。
- 自己发起新的真实世界动作，例如观察、说话、移动、短 idle 动作。
- 明确选择 wait/no-action，并给出短 reason。

宿主只执行和回报事实，不替 NPC 选择到达后的行为。

## 现有证据

- `src/runtime/NpcRuntimeContextFactory.cs:21` 已把 NPC 自主父层定义为“宿主只负责唤醒，不替你选择，也不预载世界事实”。
- `src/runtime/AgentCapabilityAssembler.cs:19` 已统一注册 `todo`、`session_search`、`skill_view` 等 Hermes-native 工具。
- `src/runtime/NpcAutonomyLoop.cs:267` 构造每轮 autonomy 决策提示；`:292` 会把 `LastTerminalCommandStatus` 注入成 `last_action_result`。
- `src/runtime/NpcAutonomyLoop.cs:715` 已能记录 active todo 观察证据；`:738` 后会从本轮工具结果写 `task_continuity` 证据。
- `src/games/stardew/StardewNpcTools.cs:501` 的 recent activity 工具会返回 `lastAction`；`:504` 会返回 active todo。
- `src/games/stardew/StardewNpcTools.cs:1222` 和 `:1270` 会把 terminal command status 写回 runtime driver。
- `src/games/stardew/StardewPrivateChatOrchestrator.cs:371` 已要求私聊中接受当前世界动作时调用 `npc_delegate_action`；`:378` 已有“接了承诺用 todo 记”的提示雏形。
- `skills/gaming/stardew-task-continuity/SKILL.md` 已定义承诺、打断、失败/阻塞的 todo 规则。
- `skills/gaming/stardew-world/SKILL.md` 已把地点意义和机械导航分开。
- `src/game/stardew/personas/haley/default/SOUL.md` 与 `facts.md` 已有海莉偏好，不需要生产代码硬编码 Haley/Beach。

## RALPLAN-DR

### Principles

1. **Agent 负责承诺和收口**：todo 是 NPC 自己维护的承诺状态，不是宿主替它决定下一步。
2. **宿主只执行真实动作和回报事实**：bridge terminal fact 与 `last_action_result` 是事实输入，不是后续行为脚本。
3. **复用现有 Hermes-native 能力**：使用 NPC session `todo`、`session_search`、skills、persona，不新增第二 task/runtime/memory/tool lane。
4. **不硬编码地点、NPC、剧情或自然语言规则**：地点坐标仍由父层通过 `skill_view` 读取 `stardew-navigation` 资料解析。
5. **测试锁住闭环，不靠提示词玄学**：必须能在 harness 中证明 private chat、todo、action terminal、autonomy 收口可以串起来。

### Decision Drivers

1. **生活连续性**：NPC 到达地点后要能理解这是自己刚答应过的事，而不是忘掉上下文。
2. **Agent-native 边界**：不能因为想让 NPC 更“活”就让宿主自动选择台词、后续动作或剧情。
3. **可观测与可测试**：日志和测试必须能串联 player request、todo、delegate action、commandId、terminal fact、下一轮 closure。

### Viable Options

#### Option A：只加强 autonomy prompt

做法：在 `NpcAutonomyLoop.BuildDecisionMessage` 里增加“看到 last_action_result 后继续行动”的文字。

Pros:

- 改动小。
- 能改善部分模型行为。

Cons:

- 没有强制承诺落到 todo，模型仍可能忘记任务。
- 测试只能断言提示词包含文字，不能证明闭环。
- 私聊接受动作和后续 autonomy 没有可追踪关联。

Verdict: 不采用为主方案，只作为辅助。

#### Option B：accepted commitment -> NPC session todo -> action lifecycle -> closure turn

做法：接受当前世界承诺时，父层先写/更新 NPC session todo，再提交真实动作；terminal completion/failure 后，下一轮 autonomy 同时看到 active/closing todo 和 `last_action_result`，并必须做 closure choice。

Pros:

- 复用现有 `todo`、runtime task hydration、task continuity 日志。
- 不新增第二系统，符合项目原则。
- 可用 harness 断言每个环节。
- 宿主只提供事实，不写死 post-arrival 行为。

Cons:

- 需要 private chat、autonomy、prompt supplement、skills、tests 多处小改。
- 需要防止每个普通 move 都被误升格成 todo。

Verdict: 采用。

#### Option C：把 post-move living loop 做成外部 MCP 工具

做法：新增外部 MCP server，提供“move and continue living”之类工具，由工具自然返回完成信息。

Pros:

- 工具调用体验上看起来统一。

Cons:

- Stardew 真实动作是异步执行，MCP 同步返回不能替代 terminal event -> runtime fact -> wake。
- 仍要解决 NPC todo、autonomy 收口和宿主 wake。
- 当前会引入第二 tool lane 风险，违背本轮约束。

Verdict: 本轮不采用。后续可以把已稳定的 host capability 包装成 MCP surface，但不能跳过 runtime 闭环。

## ADR

Decision:

采用 Option B：以现有 NPC session `todo` 承载 accepted commitment，以现有 action lifecycle 承载真实动作，以下一轮 autonomy closure choice 完成生活闭环。

Drivers:

- 手测证明 Haley 能到海边，但到达后缺少“这是我刚答应的事”的行动连续性。
- 项目已有 `todo`、task continuity、skills、persona、terminal fact 基础。
- 用户明确要求不硬编码、不让本地小模型执行真实移动、不新增第二套系统。

Alternatives considered:

- 只改 prompt：无法可靠证明闭环，也不能把承诺结构化。
- 外部 MCP：长期可评估，但当前不能替代异步 terminal/wake，且会扩大架构面。
- 新建 NPC task store：重复 `SessionTodoStore` 和 `NpcRuntimeTaskHydrator`，形成第二 lane。

Why chosen:

该方案把“生活感”放回 agent 自己的任务连续性里，而不是放进宿主脚本。它利用现有基础设施，同时保留所有真实世界写操作走 host executor。

Consequences:

- 私聊接受即时行动时要同时产生 todo 和 delegated action。
- 下一轮 autonomy 需要更明确的 closure contract。
- 测试要从单工具成功扩展到跨 private chat / runtime / todo / terminal / autonomy 的可追踪链路。

Follow-ups:

- action lifecycle 稳定后，再评估是否把 Stardew action surface 作为 MCP 包装暴露。
- 后续可把 living-event closure 抽成通用 game runtime pattern，但本轮不新增抽象层。

## 范围

### In Scope

- 私聊中玩家提出“现在就去/现在就做”的请求，NPC 接受后写入 session todo，并调用 `npc_delegate_action`。
- NPC 自主发起的长动作，如果它本身是承诺或有明确持续目标，也使用同一 todo closure pattern。
- terminal completed 后下一轮 autonomy 看到 active/closing todo 与 `last_action_result`。
- blocked/failed/timeout 映射到 todo `blocked` 或 `failed`，写短 reason。
- `stardew-world`、`stardew-task-continuity`、Haley persona 资产补充生活指导。
- Harness/单测证明链路，不依赖真实 Stardew/SMAPI。

### Out of Scope

- 不新增外部 MCP server。
- 不新增第二 task/runtime/memory/tool lane。
- 不把每次 move 自动变成 todo。
- 不让 local executor 执行 `move`、`speak`、`open_private_chat`、`idle_micro_action`。
- 不做 group task、经济系统、日程规划器、剧情导演。
- 不要求到达后必须说话或做可见动作。

## 需求

### R1：接受即时承诺必须结构化

当 private chat parent agent 决定接受玩家当前就要发生的世界动作时，必须先写入或更新 NPC session todo，再调用 `npc_delegate_action`。

约束：

- todo 内容写成短句，保留玩家请求意图。
- todo id 可以由实现选择，但必须稳定可在本轮工具结果和后续 autonomy 中定位。
- 不是所有 move 都写 todo，只有 accepted commitment / meaningful long action。

### R2：动作提交仍走现有 lifecycle

`npc_delegate_action` 只进入现有 ingress/action lifecycle，不直接代表动作完成。

约束：

- move target 仍由父层 `skill_view` 读取 `stardew-navigation` 后填入。
- 宿主不能根据“海边”等自然语言硬编码 locationName/x/y。
- local executor 仍不能执行真实写动作。

### R3：terminal fact 驱动 closure opportunity

bridge terminal completed/blocked/failed/timeout 写入 runtime terminal fact 后，下一轮 autonomy 必须能看到：

- `last_action_result`
- active 或 closing todo
- 可选 recent activity / task continuity 事实

### R4：下一轮 autonomy 必须显式 closure choice

当存在相关 active/closing todo 且上一真实动作已 terminal，下一轮 autonomy 不能无痕结束。它必须至少做一件：

- 调用 `todo` 更新状态。
- 调用 Stardew 世界动作工具。
- 返回明确 wait/no-action reason，并写 task continuity diagnostic。

注意：这里不是要求 NPC 必须说话或移动，而是要求它不再“忘记刚才发生的承诺”。

### R5：失败要变成可恢复任务状态

如果 action terminal 是 blocked/failed/timeout/stuck：

- 相关 todo 标成 `blocked` 或 `failed`。
- reason 使用短 factual reason。
- 根据已有 wake policy 给 agent 恢复机会。

### R6：生活指导走 skill/persona

通用原则写进：

- `skills/gaming/stardew-world/SKILL.md`
- `skills/gaming/stardew-task-continuity/SKILL.md`

NPC 倾向写进 persona 资产，例如 Haley：

- `src/game/stardew/personas/haley/default/SOUL.md`
- `src/game/stardew/personas/haley/default/facts.md`

这些指导只能影响倾向和风格，不能写死“到海边后说 X / 做 Y”。

## 实施步骤

### Step 1：定义 living-event todo contract

Touchpoints:

- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/Tools/TodoWriteTool.cs`

Work:

- 明确 private chat accepted immediate action 的工具顺序：`todo` -> `npc_delegate_action` -> natural reply。
- 对 autonomy long action 明确：只有承诺/持续目标需要 todo。
- 在 prompt/tool descriptions 中把 “commitment todo” 与 “ordinary movement” 分开。

Acceptance:

- 私聊接受“现在去海边”时，模型不能只调用 `npc_delegate_action` 而不写 todo。
- 拒绝或只是闲聊时，不强行写 todo。

### Step 2：补 closure contract 到 autonomy turn

Touchpoints:

- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`

Work:

- 在 `last_action_result` + active/closing todo 同时存在时，决策提示明确要求 closure choice。
- `stardew_recent_activity` / task status 继续作为事实查询，而不是宿主建议。
- 如果模型没有工具调用且没有 reason，写 diagnostic 作为测试可见失败面。

Acceptance:

- 到达后 autonomy turn 中能看到 `last_action_result` 和 active todo。
- 无工具调用必须有明确 wait/no-action reason，否则记录 diagnostic。

### Step 3：把 terminal failure 映射到 todo recovery

Touchpoints:

- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewNpcTools.cs`

Work:

- completed：给 agent closure opportunity，不由宿主自动完成 todo。
- blocked/failed/timeout/stuck：提示 agent 更新相关 todo，并记录 missing-feedback diagnostic。
- 保持 `reason_code/traceId/workItemId/commandId` 可关联。

Acceptance:

- failure/blocked/timeout 不会只留 bridge 日志。
- 相关 todo 能进入 blocked/failed，或留下可观测 diagnostic。

### Step 4：更新 skill 与 persona 指导

Touchpoints:

- `skills/gaming/stardew-task-continuity/SKILL.md`
- `skills/gaming/stardew-world/SKILL.md`
- `src/game/stardew/personas/haley/default/SOUL.md`
- `src/game/stardew/personas/haley/default/facts.md`

Work:

- 增加“承诺到达后要收口”的通用规则。
- 增加“完成承诺不等于必须说话”的边界。
- Haley 只补非硬编码倾向，例如喜欢明亮、干净、适合停留/拍照/社交的地点后会更愿意停留或轻松回应。

Acceptance:

- skill/persona 注入路径可被测试证明。
- production code 不新增 Haley/Beach/中文地点规则。

### Step 5：扩展 harness 和回归测试

Touchpoints:

- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`

Work:

- 私聊 fake LLM：接受当前行动时必须产生 todo + `npc_delegate_action`。
- runtime fake terminal：completed 后下一轮 message 含 active todo + `last_action_result`。
- closure fake LLM：分别覆盖 todo completed、新动作、wait reason。
- failure fake terminal：blocked/failed/timeout 对应 todo status 或 diagnostic。
- hardcode/local executor gates 保持。

Acceptance:

- 不启动 Stardew/SMAPI 即可证明 living loop。
- 真实 AI smoke 只作为补充，不作为唯一证据。

## 验收标准

1. 私聊请求“现在去海边”且 NPC 接受时，工具序列包含 todo/living-event record 和 `npc_delegate_action`。
2. delegated move 仍通过现有 lifecycle 进入 bridge command，不由 local executor 执行。
3. terminal completed 后，下一轮 autonomy 输入包含相关 active/closing todo 和 `last_action_result`。
4. 下一轮 autonomy 显式做 closure choice：todo completed、新 world action、或 wait/no-action reason。
5. terminal blocked/failed/timeout 会促使相关 todo 进入 blocked/failed，或留下明确 missing closure diagnostic。
6. 测试证明宿主没有注入 destination candidates、post-arrival choices 或 NPC/place-specific production rules。
7. 测试证明 common skill 和 Haley persona guidance 通过现有 asset 注入路径进入 prompt。
8. hardcode scan 无 production 新增硬编码。

## 风险与缓解

- Risk: 把普通移动都变成 todo，导致任务污染。  
  Mitigation: contract 限定 accepted commitment / meaningful long action。

- Risk: 宿主开始自动完成 todo，破坏 agent-native。  
  Mitigation: completed terminal 只触发 closure opportunity；todo completed 由 agent 调工具。

- Risk: 模型仍然到达后无动作。  
  Mitigation: 成功标准是显式 closure choice；无工具且无 reason 写 diagnostic 并测试。

- Risk: skill 写成隐藏剧情脚本。  
  Mitigation: skill 写通用生活指导；persona 写倾向，不写具体地点-台词规则。

- Risk: 测试只断 prompt 文本。  
  Mitigation: 必须有 fake LLM + runtime/tool-result/harness 链路测试。

## Verification Commands

Focused tests:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewAutonomyTickDebugServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests"
```

Bridge regression:

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
```

Desktop build:

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
```

Hardcode gate:

```powershell
rg -n "Haley|Willy|Beach|Town|海边|镇|destination\\[|nearby\\[|moveCandidate\\[" src Desktop/HermesDesktop.Tests Mods/StardewHermesBridge -g "*.cs"
```

人工判读：测试 fixture 可以出现 Haley/Beach；production 新增 NPC/地点/自然语言规则不允许。

## Available-Agent-Types Roster

- `architect`：审查 agent-native、todo、runtime、skill/persona 边界。
- `debugger`：定位 private chat -> ingress -> terminal -> autonomy 的断点。
- `executor`：实现 production 小改和测试。
- `test-engineer`：建设 fake LLM / harness / closure matrix。
- `code-reviewer`：审查硬编码、local executor 禁写、宿主越界。
- `verifier`：跑测试、hardcode scan、日志链路验收。

## Follow-up Staffing Guidance

Ralph path:

- 1 个 `executor` 顺序实现 Step 1-4。
- 1 个 `verifier` 独立跑 focused tests、hardcode scan 和日志证据。
- 适合本补充，因为 touchpoints 多但写入边界集中，能降低多人冲突。

Team path:

- Lane 1 `test-engineer`：先写 private chat + autonomy closure harness。
- Lane 2 `executor`：改 private chat prompt/tool contract 与 closure contract。
- Lane 3 `executor`：改 skills/persona 和 prompt injection tests。
- Lane 4 `code-reviewer`：持续审查硬编码与宿主越界。

Suggested reasoning:

- architecture/review/verification 用 high。
- executor 窄改用 medium。
- test-engineer 用 medium/high，取决于 harness 复杂度。

Launch hints:

```text
$ralph .omx/plans/prd-stardew-post-move-living-loop.md .omx/plans/test-spec-stardew-post-move-living-loop.md
$team .omx/plans/prd-stardew-post-move-living-loop.md .omx/plans/test-spec-stardew-post-move-living-loop.md
```

Team verification path:

- Team 先证明 test spec 的 focused matrix 通过。
- Leader 再跑 Desktop focused tests、bridge tests、Desktop build。
- 最后跑 hardcode scan 并人工判读 production hits。

## Consensus Review Changelog

- Planner draft 采用 existing `todo` + action lifecycle + closure turn，不新增 MCP/server/store。
- Architect review 强化了“terminal completed 只给 closure opportunity，不由宿主自动完成 todo”的边界。
- Critic review 强化了测试必须覆盖三种 closure choice，并把无工具无 reason 作为 diagnostic 失败面。
