# Stardew NPC 本地执行器 V1 共识方案

## 需求摘要

当前 `a5ab5ee1` 已证明 Stardew local executor 可以让 `move` 经由 `delegation` lane 使用本地 LM Studio 模型，但现有日志仍会让人误判：`stardew_speak` 是 parent speech contract + host action，不是本地模型发言；`observe/wait/escalate` 目前是 host-interpreted，不是本地模型调用。下一阶段目标是把本地执行层做成真实、可审计、可扩展的 v1，而不是扩大到人格、记忆或对话。

## 已确认事实

- `src/runtime/NpcAutonomyLoop.cs:367` 解析 parent intent contract；无效 JSON 会升级而不进入 executor。
- `src/runtime/NpcAutonomyLoop.cs:405` 已写 `parent_tool_surface registered_tools=0` 诊断。
- `src/runtime/NpcAutonomyLoop.cs:410` 已写 `local_executor selected action=<x>;lane=delegation`。
- `src/runtime/NpcAutonomyLoop.cs:423` 把 `speech` contract 提交为 `host_action stardew_speak`。
- `src/runtime/NpcLocalExecutorRunner.cs:80` 目前把 `observe/wait/escalate` 直接 host-interpreted。
- `src/runtime/NpcLocalExecutorRunner.cs:93` 是真正调用 delegation model + tools 的路径。
- `src/games/stardew/StardewNpcTools.cs:12` 目前 local executor 只开放 `stardew_move` 和 `stardew_task_status`。
- `src/runtime/NpcRuntimeSupervisor.cs:314` 在 delegation client 缺失时创建 `NpcUnavailableLocalExecutorRunner`，不回退父模型。
- `src/runtime/NpcLocalExecutorRunner.cs:27` 当前 missing delegation 时只对 `move/task_status` 阻塞，对 `observe/wait/escalate` host-interpreted；v1 若把 `observe` 升为 model-called，missing delegation 的 `observe` 也必须改为 blocked。

## RALPLAN-DR 摘要

### 原则

1. 证据优先：日志必须能区分 `model_called`、`host_interpreted`、`blocked`。
2. 短上下文优先：parent 只输出必要 intent 字段，不被工具 schema 和低层参数污染。
3. 低风险边界：local executor 只能做机械、读状态、查命令进度，不做人格/关系/记忆/承诺决策。
4. 不夸大本地模型作用：`stardew_speak` 和 `wait` 不得被报告成本地模型产物。
5. 先稳后扩：先把 `move/task_status/observe` 证明清楚，再考虑 gift/use/private-chat-open。

### 决策驱动因素

1. 用户需要知道本地小模型到底承担了多少真实工作，而不是看见 `speak/move` 就误判。
2. 长期运行成本目标要求云端 parent 少调用、少输出、少看工具 schema。
3. 当前实现已接近目标，但 `observe` 还未真正进入 model-called local path，日志也缺少显式执行模式。

### 可行选项

#### 方案 A：只增强日志，不改行为

Pros: 最小改动，风险最低，可以快速解决“谁调用了工具”的误判。

Cons: `observe` 仍不是本地模型执行，不能满足 local executor v1 spec 对 `observe` 的最小范围；本地模型贡献仍主要只有 `move/task_status`。

#### 方案 B：以证据优先的 hardening + current-state observe 本地化（推荐）

Pros: 保持低风险；让 `observe` 成为第二类可证明的本地模型 read-only 工作；日志能清晰回答本地模型承担了什么；不把 `wait/speak` 过度本地化。

Cons: 需要改 `NpcLocalExecutorRunner` 的 action-specific tool filtering 和日志结构；要更新一批测试。

#### 方案 C：所有低风险动作都调用本地模型

Pros: 表面上最符合“低风险动作交给本地小模型”的直觉。

Cons: `wait` 调模型是浪费；`speak` 本地化会削弱人格一致性；过度委派会让 NPC 像脚本执行器而不是主 agent 心智驱动。

### 选定方案

选择 Option B。

## 范围

### 范围内

1. 给 runtime log 增加显式 `executorMode` 证据。
   - 建议在 `src/runtime/NpcRuntimeLogWriter.cs` 的 `NpcRuntimeLogRecord` 末尾增加可选字段，避免破坏旧调用。
   - `NpcAutonomyLoop.WriteLocalExecutorResultAsync` 写入 `model_called|host_interpreted|blocked`。

2. 改造 `NpcLocalExecutorRunner` 为 action-specific local executor。
   - `move` 调本地模型，只暴露 `stardew_move`。
   - `task_status` 调本地模型，只暴露 `stardew_task_status`。
   - `observe` 调本地模型，只暴露现有 read-only current-state 工具。
   - `wait/escalate` 不调模型，直接 host-interpreted。
   - 若 delegation client 不可用，`move/task_status/observe` 都必须 blocked；只有 `wait/escalate` 可 host-interpreted。

3. 把 read-only observe/status 工具加入 local executor tool surface。
   - 优先复用现有 `StardewStatusTool`，不要新增等价工具。
   - 更新 `StardewNpcToolFactory.CreateLocalExecutorTools` 和 fingerprint。
   - 确保不包含 `stardew_speak`、`stardew_open_private_chat`、gift/trade/memory/todo/agent 类工具。

4. 减少无关字段和无关工具。
   - Parent prompt 要求只输出 action 相关字段，非相关 optional 字段直接省略。
   - Local executor `SerializeIntent` 忽略 null/empty/default optional 字段。
   - 本地模型每次只看到该 action 需要的一种工具 schema。

5. 增加 no-tool-call 一次重试。
   - 仅对 `move/task_status/observe` 这些 model-called action 生效。
   - 第二次仍无 tool call 时，记录 `local_executor_blocked:no_tool_call`。
   - 禁止 host 直接代替本地模型执行 `move`，否则会破坏证据链。

6. 增加手测统计脚本或说明。
   - 从 `runtime.jsonl` 聚合 parent intent 数、local model call 数、host interpreted 数、blocked 数、`move/task_status/observe` 成功数、`stardew_speak` host action 数。
   - 明确该统计不是 billing-grade token usage。

### 范围外

1. 不让 local model 生成 `stardew_speak` 文本。
2. 不加入 gift/trade/use item/relationship/memory/durable task creation。
3. 不做 Settings UI。
4. 不做 provider billing-grade token accounting。
5. 不把 local executor 换成通用 `AgentTool` child-agent flow。

## 实施步骤

1. 更新 local executor 结果证据。
   - Files: `src/runtime/NpcLocalExecutorRunner.cs`, `src/runtime/NpcAutonomyLoop.cs`, `src/runtime/NpcRuntimeLogWriter.cs`.
   - Add `ExecutorMode` to `NpcLocalExecutorResult`.
   - Add optional `ExecutorMode` to `NpcRuntimeLogRecord`.
   - Keep JSONL backward-compatible: old records without `executorMode` must still deserialize/read normally.
   - Set `model_called` only after the runner actually invokes the delegation stream path.
   - Set `host_interpreted` only for `wait/escalate`.
   - Set `blocked` for unavailable, no-tool-call, unknown tool, invalid args, tool failure, and stream errors.

2. 增加 action-specific 的工具过滤。
   - Files: `src/runtime/NpcLocalExecutorRunner.cs`, especially both `NpcLocalExecutorRunner` and `NpcUnavailableLocalExecutorRunner`.
   - Select tools by intent action before calling `_chatClient.StreamAsync`.
   - If required tool is unavailable, block with `local_executor_blocked:required_tool_unavailable`.
   - In `NpcUnavailableLocalExecutorRunner`, block `move/task_status/observe` and host-interpret only `wait/escalate`.
   - Pass only the selected action tool definitions to the local model.

3. 让 observe 变为 current-state 只读且走 model-called。
   - Files: `src/games/stardew/StardewNpcTools.cs`, `src/runtime/NpcLocalExecutorRunner.cs`.
   - Include existing `stardew_status` in the local executor allowed set.
   - Route `NpcLocalActionKind.Observe` through model-called path with only `stardew_status`.
   - Treat `observeTarget` as advisory context only. It does not create targeted observation in v1 because `stardew_status` re-reads current NPC/world state.
   - Keep observe result summary compact.

4. 去掉无关的可选意图噪音字段。
   - Files: `src/runtime/NpcLocalExecutorRunner.cs`, `src/runtime/NpcAutonomyLoop.cs`.
   - `NpcAutonomyLoop.BuildDecisionMessage` is the owner of the parent raw JSON contract. Only touch `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs` if skill-facing wording also needs synchronization.
   - Do not rely only on `WhenWritingNull`; build per-action intent JSON so default values like `escalate=false` are not emitted when irrelevant.
   - Required absence assertions:
     - `move` must omit `commandId`, `observeTarget`, `waitReason`, and `escalate=false`.
     - `observe` must omit `destinationId`, `commandId`, and `waitReason`.
     - `wait` must omit `destinationId`, `commandId`, and `observeTarget`.
     - `task_status` must omit `destinationId`, `observeTarget`, `waitReason`, and `escalate=false`.
     - `escalate` must omit `destinationId`, `commandId`, `observeTarget`, and `waitReason`.
   - Tighten parent prompt wording: output raw JSON and omit fields that do not apply to the chosen action.

5. 增加 no-tool-call 重试。
   - File: `src/runtime/NpcLocalExecutorRunner.cs`.
   - Retry once for model-called actions with a shorter corrective instruction.
   - Preserve attempt-level evidence through fixed runtime diagnostic records, not a new log schema field:
     - first failed attempt: `actionType=diagnostic`, `target=local_executor`, `stage=attempt`, `result=no_tool_call;attempt=1`
     - retry start: `actionType=diagnostic`, `target=local_executor`, `stage=retry`, `result=no_tool_call;attempt=2`
     - final unresolved result: normal `local_executor` record with `stage=blocked`, `result=no_tool_call`, `error=no_tool_call`
   - Do not retry host-interpreted actions.

6. 扩展测试并更新现有断言。
   - Files:
     - `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs`
     - `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
     - `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
     - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
   - Cover move/tool filtering, task_status/tool filtering, observe/read-only local model path, wait/no model path, no-tool-call retry, speech host action separation, and log `executorMode`.

7. 增加或记录 runtime log 聚合方式。
   - Preferred lightweight option: add a PowerShell snippet to the plan/manual verification notes or existing Stardew README, not a new product feature.
   - Only add code if an existing diagnostics/helper location already exists.

## 风险与缓解措施

1. 风险：通过 `stardew_status` 实现的 `observe` 可能返回过多或噪音状态，提升 no-tool-call 或 wrong-tool-call 的概率。
   缓解：将 `observe` 的工具暴露严格限制为一个只读工具，紧凑汇总返回证据，并增加测试证明本地模型只接收到 `stardew_status`。

2. 风险：向 `NpcRuntimeLogRecord` 增加 `executorMode` 可能破坏依赖旧 JSONL 结构的 consumer 或测试。
   缓解：补充向后兼容的序列化/反序列化覆盖：没有 `executorMode` 的旧记录仍能读取，新记录只在相关时包含该字段。

3. 风险：no-tool-call 重试可能掩盖第一次失败，让日志看起来比实际情况更“干净”。
   缓解：输出 attempt 级别诊断或结果细节，明确展示第一次 `no_tool_call`、重试，以及重试后仍失败时的最终 `blocked`。

4. 风险：如果意图字段清理只依赖 null omission，仍可能泄露像 `escalate=false` 这样的默认字段。
   缓解：构建 action-specific JSON，并在测试中对全部五类动作（`move`、`task_status`、`observe`、`wait`、`escalate`）断言相关字段缺失。

5. 风险：`host_action target=stardew_speak` 与 `local_executor blocked` 并列出现时，可能被误统计为本地执行器成功。
   缓解：手工和脚本统计都必须把 `host_action` speech 与 `local_executor` 结果分开计数。

## 验收标准

1. Parent autonomy turn remains `_agent.ChatAsync(...)` with zero registered tools and log evidence `registered_tools=0`.
2. `move` runtime evidence includes `executorMode=model_called`, `lane=delegation`, `target=stardew_move`, and command id when submitted.
3. `task_status` local executor call exposes only `stardew_task_status`.
4. `observe` local executor call exposes only read-only `stardew_status` or equivalent existing current-state tool.
5. `wait` produces zero delegation stream calls and logs `executorMode=host_interpreted`.
6. `speech.shouldSpeak=true` creates `host_action target=stardew_speak` and never `local_executor target=stardew_speak`.
7. `host_action target=stardew_speak` may coexist with `local_executor stage=blocked`; that is expected and must not be counted as local executor success.
8. Missing delegation blocks `move/task_status/observe`, and host-interprets only `wait/escalate`.
9. No-tool-call failures retry once and then remain visible as blocked; no host fallback hides the failure.
10. No-tool-call retry evidence includes first attempt, retry attempt, and final result.
11. No-tool-call retry evidence uses fixed diagnostic records: `target=local_executor stage=attempt result=no_tool_call;attempt=1`, `target=local_executor stage=retry result=no_tool_call;attempt=2`, then the final blocked local executor record.
12. Local executor prompt/intent serialization omits irrelevant optional fields with explicit absence assertions for `move`, `task_status`, `observe`, `wait`, and `escalate`.
13. `executorMode` JSONL addition is backward-compatible with old records.
14. Focused tests and full `HermesDesktop.Tests` pass.

## 验证步骤

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalExecutorRunnerTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcRuntimeSupervisorTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

手工验证：

1. Start LM Studio and the configured local `delegation` model.
2. Start Hermes Desktop and Stardew.
3. Trigger at least `move`, current-state `observe`, `wait`, and parent speech.
4. Inspect NPC `runtime.jsonl`.
5. Confirm the evidence chain and mode split in the acceptance criteria.
6. If `host_action target=stardew_speak` appears beside a blocked local executor record, count speech as parent/host behavior, not local executor success.
7. Missing delegation reproducible path: remove or blank the `delegation` section in `%LOCALAPPDATA%\hermes\config.yaml`, restart Desktop, trigger an `observe` intent, and confirm `observe` is blocked while `wait/escalate` remain host-interpreted.
8. No-tool-call reproducible path: use the focused fake-client test path, not live LM Studio prompting. The accepted proof is `NpcLocalExecutorRunnerTests.ExecuteAsync_WithNoToolCall_RetriesOnceThenBlocks` asserting first diagnostic, retry diagnostic, and final blocked record shape.

## 可用 Agent 类型

- `explore`: fast read-only code lookup.
- `planner`: task sequencing and scope refinement.
- `architect`: boundary and design review.
- `critic`: plan/design challenge and consistency review.
- `executor`: implementation and refactoring.
- `test-engineer`: test design and test repair.
- `verifier`: completion evidence and test adequacy.
- `code-reviewer`: final code review.

## 后续人员配置建议

Ralph 路径：

- Recommended for this slice because implementation touches shared runtime files.
- Use one `executor` lane for code changes and one `test-engineer` or `verifier` pass after implementation.
- Suggested reasoning: `executor=medium`, `test-engineer=medium`, `verifier=high`.

Team 路径：

- Lane 1 `executor`: `NpcLocalExecutorRunner` action filtering, retry, result mode.
- Lane 2 `executor`: Stardew tool surface/fingerprint and log record plumbing.
- Lane 3 `test-engineer`: focused tests for runner, supervisor, autonomy loop, tool factory.
- Final `verifier` or `code-reviewer`: ensure no `stardew_speak` local executor regression and no host fallback for move.
- Suggested reasoning: executors `medium`, test-engineer `medium`, verifier/code-reviewer `high`.

启动提示：

```text
$ralph .omx/plans/stardew-npc-local-executor-v1-consensus-plan.md
$team .omx/plans/stardew-npc-local-executor-v1-consensus-plan.md
```

Team 验证路径：

- Team must not stop until focused tests pass or exact blockers are documented.
- Ralph/final verifier then runs full `HermesDesktop.Tests` and summarizes manual-test evidence requirements.

## 评审变更记录

- 初始方案明确拆分了 `speak`、`wait` 与 model-called 的机械工作。
- 将 current-state 的只读 observe 增加为当前 move/task_status local executor 之外唯一的行为扩展。
- 增加 no-tool-call 重试，同时禁止隐藏式 host fallback。
- 增加对 `executorMode` 的 runtime 证据要求。
- 应用 Architect 迭代：修正 `observe` 在 missing-delegation 下的语义；将父层 JSON contract 归属纠正为 `NpcAutonomyLoop.BuildDecisionMessage`；澄清 `stardew_status` 是 current-state 的重新观察而不是 targeted observe；并将 host speech 记录为并行证据，而不是 local executor 成功。
- 应用 Architect 复审：将验收文字从 `CompleteAsync` 修正为实际的 `_agent.ChatAsync(...)` 零工具 autonomy turn。
- 应用 Critic 迭代：增加显式风险/缓解、action-specific 缺失字段断言、`NpcUnavailableLocalExecutorRunner` 归属、attempt 级重试证据、向后兼容的 `executorMode` 日志，以及手工失败边界检查。
- 应用 Critic 复审：将 no-tool-call 证据修正为具体诊断记录形状，补全 `task_status` 和 `escalate` 的缺失字段断言，并让失败边界验证可复现。
