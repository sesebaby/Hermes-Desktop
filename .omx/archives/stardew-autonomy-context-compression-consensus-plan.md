# Stardew NPC Autonomy First-Phase Context Compression / Cost-Control Plan

## Requirements Summary

目标：为 Stardew NPC autonomy 落第一阶段上下文压缩与成本控制，优先深度对齐 `external/hermes-agent-main` 的上下文压缩思路，但做 title-local adaptation，不照搬 Python 运行时。

已确认约束与证据：

- 用户已批准以“确定性的游戏帧压缩作为主 pre-LLM snapshot”作为首选实现方向，再叠加参考仓库里的 token/telemetry、tool repair、writeback 思路。
- 成本目标按用户给定的 DeepSeek-V4-Flash 定价约束执行：默认 30 分钟玩家会话的累计估算成本要能被控制并证明 `<= 1 RMB`。
- 当前仓库已有两段相关但未统一的机制：
  - `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs` 只覆盖 autonomy 首轮字符预算裁剪。
  - `src/Context/ContextManager.cs` 只做通用 recent-window + evicted-summary，且 `SummarizeEvictedAsync()` 直接调用主 LLM，总体不适合作为 autonomy 的首个成本闸口。
- 当前 autonomy prompt 仍带有较泛化的 agent 指令痕迹：
  - `src/Core/SystemPrompts.cs`
  - `src/runtime/NpcRuntimeContextFactory.cs`
- 参考链路已核对：
  - `external/hermes-agent-main/agent/context_engine.py`
  - `external/hermes-agent-main/agent/context_compressor.py`
  - `external/hermes-agent-main/run_agent.py::_compress_context`
  - `external/hermes-agent-main/acp_adapter/server.py::_cmd_compact`
- 当前测试基座已存在，适合 test-first 扩展：
  - `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`
  - `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeContextFactoryTests.cs`
  - `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`

本计划刻意收窄为“本轮即可执行的第一切片”：

1. 先抽出可复用的 autonomy context compression / telemetry contract。
2. 先接入 autonomy 首轮与多轮/tool-loop 的可行点，不引入新依赖。
3. 先用 deterministic compaction + 估算 telemetry 建闭环，保留后续再补 provider 真实 usage。
4. 同步去掉 NPC autonomy 上的 generic agent prompt 噪音，但不牺牲现有游戏效果。

## RALPLAN-DR Summary

### Principles

- 先保游戏效果：压缩只能减少无效上下文，不能破坏 NPC 当前决策连续性、工具续跑和 persona/runtime contract。
- 先做自治通道的最小闭环：首轮和 tool-loop 共用一套 contract，避免再长出第二套 budget 逻辑。
- 先 deterministic、后生成式：优先本地可预测的帧压缩；生成式总结只作为后续补充，不作为第一闸。
- 参考优先但本地适配：借鉴 `hermes-agent-main` 的 prune/protect-tail/sanitize/telemetry 思想，不强行复制其 Python session split 和全局 context engine 架构。
- 测试先行：先锁住首轮 budget、tool-call/result 完整性、autonomy prompt 缩减与 loop 行为，再做实现。

### Decision Drivers

- 成本压力明确：必须让 autonomy 能证明自己在给定会话长度和计费前提下可控。
- 当前问题集中在首轮和工具循环：只有首呼裁剪，没有多轮统一压缩，也没有可核查的 autonomy 成本遥测。
- 架构现实：`Agent.ChatAsync()` 在首轮与后续 tool-loop 分叉，`ContextManager` 不是 autonomy 专用，直接全局重构风险过大。

### Viable Options

#### Option A: 以 Stardew autonomy 专用 deterministic compactor 为首切片，并抽成可复用 contract

优点：

- 最贴合用户已批准方向。
- 可以直接复用现有 `StardewAutonomyFirstCallContextBudgetPolicy` 经验和测试资产。
- 能小步接入 `Agent.ChatAsync()` 的首轮与后续 tool-loop。
- 能顺手把 prompt slimming 和 telemetry 一并纳入 autonomy 专线。

缺点：

- 第一阶段仍是 autonomy 专用，不会立刻变成全局 context engine。
- provider 真实 usage 可能先是 nullable / 后补齐。

#### Option B: 先把 `ContextManager` 改造成通用 compression engine，再让 autonomy 复用

优点：

- 架构更统一。
- 长期可服务桌面 agent / NPC / 其他 runtime。

缺点：

- 这轮范围过大。
- 会把通用 summary、memory recall、desktop prompt 语义一起卷进去，30 分钟内不适合。

#### Option C: 先引入 local small model 做 autonomy pre-compression，再考虑 deterministic compaction

优点：

- 看起来更“智能”。
- 对长历史的抽象能力更强。

缺点：

- 与用户要求相反，且首步成本/复杂度都更高。
- 增加新的失败面，不利于先建立稳定成本基线。

### Chosen Option

选择 Option A。

Option B 暂不选，因为这轮目标是“先把 autonomy 成本闸口立住”，不是重做全站上下文架构。Option C 暂不选，因为用户已明确要求“context compression before local small model”，且第一阶段应优先 deterministic compaction。

## ADR

### Decision

新增一条 Stardew autonomy 专用的“上下文压缩与成本遥测 contract”，由 deterministic compactor 驱动，并在 `Agent.ChatAsync()` 首轮与后续 tool-loop 共用；同时把 autonomy system prompt 收窄为 game/runtime-specific 版本，去掉 generic agent 噪音。

### Drivers

- autonomy 首轮已有裁剪逻辑，可演进而不是推倒重来；
- tool-loop 是当前缺口，必须补；
- 成本目标要求每轮都能度量；
- 用户要求 reference-first，但允许 title-local adaptation。

### Alternatives Considered

- 全局 `ContextManager` 先行重构：拒绝，范围过大。
- 先上 small model summary：拒绝，违背首步策略。
- 只做 prompt 缩减不做 loop compaction：拒绝，无法形成真实成本闭环。

### Why Chosen

因为它最小、最可验证、最接近现有实现，也最容易用测试证明“效果未退化 + 成本可控”。

### Consequences

- 第一阶段会出现一条 autonomy-specific contract，而不是立即全局通用。
- 部分 telemetry 先是 estimated，不是 provider-billed exact usage。
- 后续若验证收益稳定，可把 contract 向 `ContextManager` 或更广 runtime 抽象。

### Follow-ups

- 第二阶段再考虑接真实 provider usage 到 `ChatResponse` / `IChatClient`。
- 第二阶段再考虑把 deterministic snapshot 与生成式 summary 组合。
- 第二阶段再评估是否把通用 `ContextManager` 的 evicted-summary 替换为统一压缩接口。

## Acceptance Criteria

- 新增一套 autonomy 专用压缩 contract，明确输入、输出、原因码、遥测字段，且首轮与后续 tool-loop 至少各有一个调用点复用它。
- 首轮 autonomy 仍满足现有 `5000 chars` 级别保护目标，但实现从“首呼专用 policy”升级为“可复用 compaction pipeline”，不回归现有 `StardewAutonomyContextBudgetTests`。
- 新增测试证明后续 tool-loop 在发送 `session.Messages` 前也能进行 autonomy compaction，而不是只有首轮有预算控制。
- 新增测试证明压缩后不会破坏 assistant tool_call / tool result 配对完整性；若边界切分造成不完整，必须像参考实现那样修复或补 stub。
- 新增测试证明 autonomy prompt 不再包含 generic agent guidance 噪音；保留 Stardew runtime、persona、required skill contract、memory/task continuity 所需内容。
- 新增 telemetry 能按会话/轮次输出至少这些字段：`preChars/preTokensEst`、`postChars/postTokensEst`、`savedChars/savedTokensEst`、`reason`、`iteration`、`isFirstCall`、`statusToolCount`、`estimatedCostRmb`、`sessionEstimatedCostRmb`、`budgetTargetRmb`。
- 成本验证路径明确：能够通过测试或 debug harness 日志证明 30 分钟默认会话可被持续估算，并可判断是否超过 `1 RMB` 阈值；本切片不要求先做全量真实计费对账。

## Implementation Steps

1. 先补失败测试，锁定首轮外的缺口与 prompt 目标。

目标文件：

- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeContextFactoryTests.cs`
- 视测试组织选择新增：
  - `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextCompressionLoopTests.cs`
  - 或扩展 `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`

要补的断言：

- 首轮 contract 仍保护 system / active task / protected tail / current user。
- 非首轮 tool-loop 也会在 `CompleteWithToolsAsync()` 前执行 autonomy compaction。
- compaction 后 assistant tool_calls 与 tool results 仍成对。
- autonomy channel 的 system prompt 不再出现 `session_search` / `todo` / `memory` 这类 generic agent guidance，除非是 runtime contract 明确保留的内容。
- telemetry log/record 至少带出 iteration、reason、pre/post tokens est、estimated RMB。

2. 抽出 autonomy compression / telemetry contract，先服务 deterministic game-frame compaction。

目标文件：

- 新增 `src/runtime/StardewAutonomyContextCompression.cs`
  - 包含 request/result/reason/telemetry DTO。
- 新增 `src/runtime/StardewAutonomyContextCompactor.cs`
  - 将当前 `StardewAutonomyFirstCallContextBudgetPolicy` 的核心裁剪逻辑迁入可复用 compactor。
- `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs`
  - 收窄成对新 compactor 的首轮适配器，避免重复实现。

设计要求：

- contract 明确区分：
  - `FirstCallPreparedContext`
  - `ToolLoopContinuation`
  - `OverflowRepair`
- 保留现有 dynamic recall cap / protected tail / duplicate broad-status trimming 思路。
- 新 contract 内建 rough token estimator，先用 chars->tokens 的稳定估算。
- telemetry 先走 logger + 结构化结果，不引入新服务依赖。

3. 把 compactor 接进 `Agent.ChatAsync()` 的首轮和多轮/tool-loop。

目标文件：

- `src/Core/Agent.cs`
- 可能新增一个小 helper：
  - `src/runtime/StardewAutonomyAgentCompactionAdapter.cs`

接入策略：

- iteration 1 保持现有 preparedContext 路径，但改为调用新 contract。
- iteration > 1 在 `messagesToUse = session.Messages` 后、`CompleteWithToolsAsync()` 前增加 autonomy compaction 钩子。
- 只对 `StardewAutonomySessionKeys.IsAutonomyTurnSession(session)` 生效，避免影响桌面通用 agent。
- 参考 `hermes-agent-main`：
  - 加入 tool-call/result pair sanitize；
  - 必要时对旧 tool results 做 placeholder/summary 化；
  - 不做 Python 的 SQLite session split/writeback，当前仓库没有同等需求。

4. 收窄 autonomy prompt，去掉 generic agent prompt 噪音，但保留 NPC runtime 所需 contract。

目标文件：

- `src/Core/SystemPrompts.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- 如需要，可能补一个 autonomy 专用 prompt 常量/构造器。

具体方向：

- autonomy 基础 prompt 只保留：
  - Stardew NPC runtime 身份；
  - 事实优先；
  - 工具执行边界；
  - continuity/memory/task 使用规则；
  - 不伪造行动结果。
- 从 autonomy channel 中移除泛化桌面 agent 指导，如：
  - `session_search` 的通用宣传式 guidance；
  - `todo` / `memory` / `skills` 的 generic onboarding 语气；
  - 非游戏场景的 runtime facts 说明。
- 不动 `StardewNpcAutonomyPromptSupplementBuilder.cs` 的 persona/skill contract 主体，只减少上层噪音。

5. 做最小成本闭环验证，并为执行阶段留出第二切片接口。

目标文件：

- `src/runtime/StardewAutonomyContextCompression.cs`
- 若需要轻量持久化或聚合，再评估：
  - `src/analytics/InsightsService.cs`
  - 但本切片优先不强绑全局 analytics。

本步输出：

- 结构化日志能累计单会话估算成本。
- 若 `sessionEstimatedCostRmb > 1`，日志给出明确 reason/code，而不是沉默超限。
- 为第二切片预留 nullable 实际 usage 字段，后续再决定是否把 `ChatResponse` / provider clients 扩成真实 usage 透传。

## Concrete File Map

本轮预计涉及 8-11 个文件，核心落点如下：

- `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/Core/Agent.cs`
- `src/Core/SystemPrompts.cs`
- `src/runtime/StardewAutonomyContextCompression.cs`（new）
- `src/runtime/StardewAutonomyContextCompactor.cs`（new）
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeContextFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextCompressionLoopTests.cs`（new, if cleaner）

明确暂不纳入首切片：

- `src/Context/ContextManager.cs` 的通用 summary 架构重做；
- provider 真实 usage 全链路透传；
- local small model pre-compression；
- 新依赖或新存储层。

## Risks

- tool-loop 压缩边界最容易破坏 tool_call/result 完整性；这是首要风险，必须用测试锁住。
- autonomy prompt 去泛化后，若把 memory/task continuity 指令删得过头，可能损伤 NPC 连续性。
- 估算 token/cost 与真实账单会有偏差；因此第一阶段只把它作为“控制阈值和回归比较”的工程指标，不宣称账单精确值。
- `ContextManager` 与 autonomy compactor 会暂时并存，两者职责边界要写清，避免后续再次双轨化。

## Verification

至少执行并记录这些验证：

1. `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextBudgetTests"`
2. `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcRuntimeContextFactoryTests"`
3. `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextCompressionLoopTests"`
   如果不单独建测试类，则替换成实际落点测试名。
4. `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~MemoryParityTests"`
   只在 compaction hook 触及共享 memory/plugin 行为时跑对应子集。
5. `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64`

手工/日志验证建议：

- 用 `StardewAutonomyTickDebugService` 跑单个 NPC tick，检查 `autonomy_*compression*` / `estimatedCostRmb` 日志是否连续输出。
- 观察是否仍存在重复 broad status tool 调用、长 tool result 反复回灌、或 tool continuation 丢失。

## Execution Notes For The Next Turn

推荐执行顺序：

1. 先写/改测试，故意让“非首轮无压缩”这类断言失败。
2. 再抽 contract + compactor。
3. 再改 `Agent.ChatAsync()` 接入。
4. 最后做 autonomy prompt slimming。
5. 跑测试和构建，必要时再回补 telemetry 字段。

这轮的最小成功标准不是“做完整个通用 context engine”，而是：

- autonomy 首轮与 tool-loop 共用一套压缩 contract；
- prompt 去掉 generic agent 噪音；
- 有可核查的估算成本遥测；
- tests 证明没有破坏游戏效果。

## Executable Handoff

建议按下面顺序直接执行：

1. 先跑现有相关测试，确认基线：
   `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextBudgetTests|FullyQualifiedName~NpcRuntimeContextFactoryTests"`
2. 先写失败测试，优先补：
   - tool-loop compaction
   - autonomy prompt 去泛化
   - tool-call/result pair repair
3. 实现新文件：
   - `src/runtime/StardewAutonomyContextCompression.cs`
   - `src/runtime/StardewAutonomyContextCompactor.cs`
4. 改接入点：
   - `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs`
   - `src/Core/Agent.cs`
   - `src/runtime/NpcRuntimeContextFactory.cs`
   - `src/Core/SystemPrompts.cs`
5. 回归验证：
   `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomy"`
6. 最后做桌面项目构建验证：
   `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64`

完成判定：

- 新增测试先失败后转绿；
- autonomy 首轮与 tool-loop 都有压缩 telemetry；
- autonomy prompt 不再带 generic agent prompt 噪音；
- 未引入新依赖；
- 无已知 tool continuation 回归。
