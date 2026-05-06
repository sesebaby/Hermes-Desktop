# RALPLAN: Reference Memory / Session Search Boundary Parity

参考依据：
- `external/hermes-agent-main`
- `.omx/context/reference-memory-boundary-parity-20260428T052214Z.md`

任务：为 `D:\Projects\Hermes-Desktop` 制定一份与参考项目对齐的实施计划，收敛 `memory / session_search / transcript recall` 的架构边界；不实现代码。

## RALPLAN-DR Summary

### Principles
1. 参考优先：只有与 `external/hermes-agent-main` 的职责边界一致，才能称为 aligned；repo-local 近似实现不能自行宣称对齐。
2. 长期精选记忆与历史会话检索分层：`MEMORY.md` / `USER.md` 只承载 durable curated memory；历史 transcript recall 属于 `session_search` 检索域，不混入 memory provider 注入链路。
3. 修架构，不修问法：不要为“过去聊过什么 / 最早记录”之类提问做特例；测试应暴露和约束边界，而不是编码 prompt-specific 行为。
4. 测试必须说真话：任何声称 parity 的测试，都必须断言 reference 的实际边界，而不是断言当前实现的便利行为。

### Decision Drivers
1. 与参考项目的边界一致性：参考项目把 curated memory 与 transcript recall 分开，且 `session_search` 是显式工具。
2. 降低错误召回语义：当前 `TranscriptMemoryProvider -> IMemoryProvider -> TurnMemoryCoordinator` 会把 transcript recall 伪装成 memory 并隐式注入每轮用户消息，边界错误。
3. 可验证、可收缩的改造面：优先拆掉错误耦合，再让测试与工具语义收敛，避免继续在假边界上堆功能。

### Viable Options

#### Option A: 边界纠偏但保留通用编排骨架
- 做法：移除 transcript 作为 `IMemoryProvider` 的注册与使用；保留 `HermesMemoryOrchestrator` / `TurnMemoryCoordinator`，但仅服务未来真正的外部 memory provider；`session_search` 继续作为 transcript recall 的唯一 agent-facing 入口。
- Pros：改动面较可控；为未来外部 provider parity 预留扩展点；最容易分阶段落地。
- Cons：短期会留下一个更“空”的协调层；需要额外测试保证它不再接管 transcript recall。

#### Option B: 强收缩到参考边界最小集
- 做法：移除 transcript provider 注入链，同时把 `TurnMemoryCoordinator` 从 transcript 首调用路径中退出；当前版本只保留 curated memory + `session_search` 两条明确通路，等真实外部 provider 需求出现再恢复预取编排。
- Pros：边界最清晰；最接近参考项目当前事实；减少错误抽象继续扩散。
- Cons：重构幅度更大；若仓库近期要补外部 provider，会有一次回填成本。

### Recommended Option
- 推荐 `Option B`。
- 原因：这次问题不是“自动召回效果不够好”，而是“把 transcript recall 放进了错误架构层”。既然参考项目把 transcript recall 定位为 `session_search` 显式工具，就不应继续让 `TurnMemoryCoordinator` 在热路径里把它伪装成 memory。先把边界收干净，再决定是否需要为真正外部 provider 恢复预取编排，风险更小，也更符合“不要头疼医头”。

### Rejected Alternatives

#### Rejected 1: 保留 auto transcript injection，但缩窄触发条件
- 做法：保留 `TranscriptMemoryProvider -> IMemoryProvider -> TurnMemoryCoordinator` 链路，只在检测到“过去 / 之前 / 记得吗”等语义时自动注入 transcript recall。
- 拒绝理由：
  - 参考项目没有把 transcript recall 建模为 memory provider 预取层；缩窄触发条件仍然保留错误职责归属。
  - 触发判断会变成 prompt/语言/模型敏感的隐式行为面，后续很难证明没有把 transcript 内容偷偷塞进当前 turn。
  - 测试会继续围绕“哪些话术触发自动注入”展开，重新落回头疼医头。

#### Rejected 2: 增加 prompt-specific `oldest` / `when-did-we-meet` 模式
- 做法：给 `session_search` 增加专门模式或规则，服务“最早记录 / 第一次 / 相识日期”等问法。
- 拒绝理由：
  - 参考项目的 `session_search` 契约是 recent browsing + query search/summarization，不是按单个问法扩展工具协议。
  - 这会把本次暴露出的架构边界问题误修成某类问题的专用功能。
  - 特例越多，越难用测试证明 agent 是在正确使用显式 recall 工具，而不是依赖工具端猜测用户意图。

## Boundary Contract

该契约是执行阶段的硬约束：

1. `IMemoryProvider` 不承载 session transcript recall。它只允许承载 durable curated memory 或真正外部 memory provider 的静态/预取上下文。
2. 任何当前轮 prompt 构造路径都不得隐式读取 session transcript，包括 `TurnMemoryCoordinator`、provider adapter、`PromptBuilder`、`ContextManager` fallback、plugin/system prompt fallback。
3. transcript recall 的唯一 agent-facing 通路是显式工具 `session_search`。
4. `session_search` 保持参考契约：无 `query` 返回最近 sessions；有 `query` 走 FTS5/LIKE fallback 检索并对匹配 sessions 做摘要。
5. 未显式调用 `session_search` 时，发送给模型的当前 turn prompt 不得包含 transcript-derived 内容。
6. `MEMORY.md` / `USER.md` 与 `session_search` 可以共同存在，但不得在同一注入层混写来源。
7. 不引入 `oldest`、`when-did-we-meet`、`first conversation` 等 prompt-specific 工具分支。

## Acceptance Criteria

1. 组合根中不再把 `TranscriptMemoryProvider` 注册为默认 `IMemoryProvider`，也不再通过该路径进入 agent 首调用。
2. `Agent` 在 no-tools、tools、streaming tools 三条首调用路径中，都不会自动把历史 transcript 内容拼接到当前 user message。
3. `TurnMemoryCoordinator` 若保留，不能依赖 `TranscriptRecallService`，也不能负责 transcript recall 的 `<memory-context>` 注入。
4. `session_search` 仍能通过无 query 返回最近 sessions，通过 query 搜索并总结匹配 sessions，并继续排除 current session lineage 与 hidden tool-source sessions。
5. 所有“自动 transcript 注入 = Python parity”的测试被删除或改写为 reference boundary 测试。
6. 新增负向回归测试证明：未调用 `session_search` 时，模型请求中不出现 prior transcript marker。
7. 新增正向工具测试证明：调用 `session_search` 时，prior transcript marker 可以通过工具结果返回。
8. 不新增任何 prompt-specific `oldest` / `when-did-we-meet` 模式、参数、测试或文案承诺。
9. negative regression 是硬门禁：no-tools、tools、streaming tools 三条路径都必须证明未调用 `session_search` 时不会生成 transcript-derived prompt content。
10. 负向测试使用可识别 transcript sentinel 文本，并对 assembled prompt / injected context 做 absence assertion。

## 实施计划

### 子系统 A：边界与组合根修正
- 目标：停止把 transcript recall 装配为 memory provider。
- 关注点：
  - `Desktop/HermesDesktop/App.xaml.cs` 当前把 `TranscriptMemoryProvider` 注册为 `IMemoryProvider`，并把 `HermesMemoryOrchestrator` / `TurnMemoryCoordinator` 接入 `Agent` 首调用路径。
  - 需要把 DI 组合根改为 reference-aligned：curated memory 走 `MemoryManager` / built-in memory lane；transcript recall 不再经由 `IMemoryProvider` 进入每轮消息。
- 交付物：
  - 一份明确的依赖拆分方案：哪些服务保留，哪些退出热路径，哪些仅作为未来外部 provider 预留。
- 验收标准：
  - 从组合根可以直接看出：transcript recall 不再被注册为 memory provider，也不会默认参与 turn prefetch。

### 子系统 B：Prompt / Context 边界收敛
- 目标：让 prompt 组装只承载 reference 允许的稳定记忆层。
- 关注点：
  - `src/search/TurnMemoryCoordinator.cs` 当前会 `PrefetchAllAsync(userMessage)`，并把结果包装成 `<memory-context>` 追加到当前 user message。
  - `src/search/TranscriptMemoryProvider.cs` 当前把 transcript recall 适配成 `IMemoryProvider`，这是本次需要退出的桥接层。
  - `src/Core/AgentLoopScaffold.cs` 当前在首调用前优先走 `TurnMemoryCoordinator`，这条热路径需要改成不再负责 transcript recall。
- 交付物：
  - 一份消息分层规则：
    - stable memory：仅 `MEMORY.md` / `USER.md` 及其现有 built-in memory 机制；
    - transcript recall：不进入自动注入链；
    - cross-session recall：交由工具调用。
- 验收标准：
  - 首次 `CompleteAsync` / `CompleteWithToolsAsync` / streaming tool-loop 前，不再自动向当前 user message 拼接 transcript recall 块。
  - 不存在新的 repo-local prompt lane 继续模拟 `session_search`。

### 子系统 C：Transcript Recall 与 Session Search 单一职责
- 目标：让 transcript recall 回到 `session_search` 的显式工具职责内。
- 关注点：
  - `src/Tools/SessionSearchTool.cs` 已经是最接近参考项目的位置，应成为 transcript recall 的唯一 agent-facing 接口。
  - `src/Core/MemoryReferenceText.cs` 里的文案要与 reference 语义一致：memory 保存 durable facts；历史会话靠 `session_search`。
- 交付物：
  - 一份职责归并清单：哪些 transcript 检索逻辑归 `SessionSearchTool` / `TranscriptRecallService`；哪些 memory 文案或调用点需去掉“自动回忆”暗示。
- 验收标准：
  - 代码结构上只剩一条清晰的 transcript recall 主路径：`session_search` -> transcript search/summarize 服务。
  - memory 相关文案不再暗示 transcript recall 会像长期记忆一样自动注入。

### 子系统 D：测试面重构
- 目标：删除错误 parity 叙事，改成 reference boundary 断言。
- 关注点：
  - `Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs` 目前有多条测试把“自动把历史 transcript 拼进当前 user message”当作 parity。
- 交付物：
  - 一份测试迁移清单：保留什么、重写什么、删除什么。
- 验收标准：
  - 测试命名和断言都围绕 boundary parity，而不是围绕某句 prompt 是否能答出来。
  - 至少覆盖 unit、integration、negative regression 三类：
    - unit：`SessionSearchTool` / `TranscriptRecallService` 保持显式工具语义。
    - integration：`Agent` 首调用不会自动注入 prior transcript。
    - negative regression：no-tools、tools、streaming tools 三条路径中，任何未显式调用 `session_search` 的请求都不能出现 transcript-derived sentinel；断言对象包括 assembled prompt / injected context；此项为合并硬门禁。

### 子系统 E：计划落地后的文档与回归门槛
- 目标：让后续执行不会再回到“看起来能答题就算 aligned”的偏差。
- 关注点：
  - 现有 `.omx/plans/prd-hermes-desktop-memory-parity.md` 与 `.omx/plans/test-spec-hermes-desktop-memory-parity.md` 带有旧前提，需要以这次 boundary plan 为准进行修订或替代。
- 交付物：
  - 一份替换关系说明：哪些旧计划/测试规范失效，哪些新验收口径生效。
- 验收标准：
  - 执行者拿到计划后，不会再把“自动 transcript recall”当作目标能力恢复。

## 测试策略

### 需要删除或改写的现有测试
- 删除或反向改写以下“自动注入 transcript recall = parity”的测试，位置：`Desktop/HermesDesktop.Tests/Services/MemoryParityTests.cs`
  - `ChatAsync_NoTools_FirstCallAugmentsCurrentUserWithPriorTranscriptRecall`
  - `ChatAsync_WithTools_FirstToolLoopCallAugmentsCurrentUserWithPriorTranscriptRecall`
  - `StreamChatAsync_WithTools_FirstToolLoopCallAugmentsCurrentUserWithPriorTranscriptRecall`
  - `TurnMemoryCoordinator_BuildMemoryContextBlock_UsesPythonFenceShape`
- 改写 `PromptBuilder_DoesNotEmitRetrievedTranscriptRecallAsSystemLayer`
  - 从“transcript recall 不应走 system layer，但仍会走 user augmentation”改成“transcript recall 不属于 prompt builder / automatic memory lanes；prompt builder 只承载稳定记忆层”。
- 重新评估 `HermesMemoryOrchestrator_*` 用例
  - 若编排层保留但不再服务 transcript recall，则改为“仅覆盖真实 external memory provider 编排语义”。
  - 若编排层退出当前热路径，则把这些测试迁移到更窄的单元级，避免继续占据 parity 主叙事。

### 需要保留并强化的现有测试
- 保留 `SessionSearchTool_*` 相关测试，作为 reference parity 主测试群：
  - recent sessions 模式
  - query search 模式
  - current session exclusion
  - schema / description 与 reference 文案一致
- 保留 memory tool / built-in memory 的测试，但要明确它们验证的是 curated memory，不是 transcript recall。

### 需要新增的测试
- 组合根测试：
  - 断言 `App` 的服务注册中，transcript recall 不再经由 `IMemoryProvider` 装配。
- 首调用边界测试：
  - 断言 `Agent` 首次调用在无工具、有工具、streaming 路径下，都不会自动附加 transcript recall 块。
- 语义边界测试：
  - 断言 cross-session 问题的 recall 只能通过 `session_search` 工具路径到达 transcript corpus。
  - 断言 built-in memory 命中与 transcript search 命中不会在同一注入层混写。
- 文案/工具契约测试：
  - 断言 `MemoryReferenceText` 与 `SessionSearchTool.Description` 保持 reference 语义，不重新引入“自动回忆过去会话”的暗示。

### 不应新增的测试
- 不新增“oldest mode”“最早记录”“when did we meet”之类 prompt-specific 特例测试。
- 不新增任何通过 hardcode prompt 话术证明 recall 能力的测试。

## 风险与缓解

### 风险 1：用户体感上觉得“记忆变弱了”
- 原因：移除自动 transcript 注入后，跨会话回忆将更多依赖 agent 显式调用 `session_search`。
- 缓解：
  - 强化 `session_search` 描述与触发指引，确保工具是首选 recall 通路。
  - 以工具调用率和 manual scenario 验证“显式 recall 可用”，而不是偷塞 prompt。
  - 增加产品/测试层场景：用户提到“之前 / 上次 / 还记得”时，agent 应优先调用 `session_search`，但该行为由工具选择验证，不由 prompt 自动注入替代。

### 风险 2：旧测试/旧计划继续把错误边界当目标
- 原因：仓库内已存在一轮“自动 recall parity”叙事。
- 缓解：
  - 在计划与测试规范中明确标注旧假设失效。
  - 执行时先删/改旧测试，再做结构收敛，避免实现阶段被错误回归门槛绑架。
  - 明确迁移说明：依赖旧自动注入语义的测试、调用方、文案必须同步改，不允许通过保留兼容路径绕过契约。

### 风险 3：收缩协调层后影响未来外部 provider 扩展
- 原因：`HermesMemoryOrchestrator` / `TurnMemoryCoordinator` 可能被视为未来扩展点。
- 缓解：
  - 在实施时保留最小可复用接口，或明确记录 “先下线路径、后按真实 provider 需求重建”。
  - 用 Option A / B 的取舍记录为 ADR，避免未来重新发明“transcript provider”桥接。

### 风险 4：执行者只删注入，不补边界验证
- 原因：单纯移除注册容易，但容易漏掉描述、契约、测试口径同步。
- 缓解：
  - 把组合根、热路径、文案、测试四类验收标准绑定为同一批完成条件。
  - 增加负向保护测试：未调用 `session_search` 时 prior transcript marker 不得进入 prompt。

## ADR 草案

### Decision
将 Hermes Desktop 的长期记忆与历史会话召回边界，收敛为与 `external/hermes-agent-main` 一致的两条主路径：`MEMORY.md` / `USER.md` 继续承载 durable curated memory；transcript recall 退出 `IMemoryProvider` 与自动 turn injection 链，回归 `session_search` 的显式工具职责。

### Drivers
- 参考项目明确把 curated memory 与 transcript recall 分层。
- 当前 `TranscriptMemoryProvider -> HermesMemoryOrchestrator -> TurnMemoryCoordinator` 让 transcript recall 冒充 memory，造成错误边界。
- 需要一个可测试、可维护、不会继续围绕单个 prompt 打补丁的结构。

### Alternatives Considered
- Option A：仅移除 transcript provider 注册，但保留通用编排骨架，供未来外部 provider 使用。
- Option B：移除 transcript provider 注入链，并让当前热路径退出 `TurnMemoryCoordinator` 的 transcript 职责，只保留 curated memory + `session_search` 最小边界。
- Rejected：保留 auto transcript injection 但缩窄触发条件。拒绝原因是仍保留 reference 不存在的隐式 transcript memory lane，并扩大语言/提示词敏感的隐式行为面。
- Rejected：增加 `oldest` / `when-did-we-meet` prompt-specific 模式。拒绝原因是把架构边界问题误修为单问法功能，偏离 reference 工具契约。

### Why Chosen
- 选择 Option B，因为它最直接消除错误边界，且与参考项目事实最接近。
- 这次暴露的是设计错位，不是 recall 质量调参问题；继续保留 transcript auto-injection 只会让 repo-local 设计继续伪装成 aligned。

### Consequences
- 自动把历史 transcript 拼进当前 user message 的行为将被视为偏差，而不是能力目标。
- `session_search` 成为唯一的 transcript recall agent-facing 入口，相关文案、测试、回归标准都要围绕它收敛。
- 旧的 memory parity 计划和测试规范需要修订，否则会持续把错误行为当回归目标。
- 若未来引入真正外部 memory provider，需要基于 reference 事实重新定义它与 curated memory、session_search 的关系，而不是复用 transcript provider 桥接。

### Follow-ups
- 修订或替代现有 `.omx/plans/prd-hermes-desktop-memory-parity.md`。
- 修订或替代现有 `.omx/plans/test-spec-hermes-desktop-memory-parity.md`。
- 在执行前先完成测试迁移清单，确保回归门槛先与 reference 边界一致。
- 在执行 PR / commit 说明中记录本次 boundary contract，避免后续把 transcript recall 重新接回 `IMemoryProvider` 或 prompt injection 热路径。

## Consensus Review

### Architect Review
- Verdict: APPROVE WITH SYNTHESIS
- Steelman antithesis:
  - 如果目标是“用户跨会话回忆成功率最大化”，保留自动 transcript 注入看起来更直接，因为它绕过了模型是否主动调用 `session_search` 的不确定性。
- Real tradeoff tension:
  - `reference fidelity` 与 `answer-rate convenience` 存在直接张力。参考项目选择的是清晰边界和显式工具，不是把 transcript recall 混入 memory lane 来换取更高的表面命中率。
- Synthesis:
  - 本计划优先修正边界，再通过 `session_search` 文案、契约和测试保证显式 recall 可用；不在错误架构层继续优化“自动答出来”的体感。

### Architect Re-review
- Verdict: APPROVE
- Required execution guard:
  - 将 negative regression 提升为硬门禁：no-tools、tools、streaming tools 三条路径都必须证明未调用 `session_search` 时不会生成 transcript-derived prompt content。

### Critic Review
初次 Critic 结论为 `ITERATE`，要求补强：
- 独立的边界契约。
- 被拒绝替代方案及拒绝理由。
- 更可测试的验收标准。
- unit / integration / negative regression 三类回归防线。

本版已应用上述修改，待复审。

### Critic Re-review
- Verdict: APPROVE
- Checks passed:
  - Principle-option consistency: Option B 与 Boundary Contract 一致。
  - Alternative fairness: 缩窄自动注入与 prompt-specific 模式已被公平记录并拒绝。
  - Risk/test adequacy: no-tools、tools、streaming 的 negative regression 足以防止隐藏后门。
- Optional tightening applied:
  - 负向测试使用可识别 transcript sentinel，并对 assembled prompt / injected context 做 absence assertion。

## Applied Changes

- Added `Boundary Contract` as execution hard constraints.
- Added rejected alternatives for narrowed auto-injection and prompt-specific oldest/when-did-we-meet modes.
- Converted acceptance criteria into explicit testable statements.
- Strengthened test plan with unit, integration, and negative regression coverage.
- Strengthened risk mitigations for product feel, old-test migration, and hidden backdoors.
