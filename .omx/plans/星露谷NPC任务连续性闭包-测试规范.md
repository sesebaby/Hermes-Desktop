# 星露谷 NPC 任务连续性闭包测试规范

## 测试策略

只针对闭包缺口。现有底座测试已经证明了 `ToolSessionId` / `TaskSessionId`、todo 投影、hydration 和必需 skill 注入。本规范新增以下端到端断言：

1. autonomy 确实会通过 Stardew 工具推进玩家承诺；
2. 终态 `blocked` / `failed` 命令事实会先由 runtime 暴露，然后只有在 agent 随后调用 `todo` / `todo_write` 时，才流入 todo 的 `status` + `reason`；
3. 对已承诺任务在 `blocked`/`failed` 场景下会尝试玩家反馈；
4. `runtime.jsonl` 与 task-view/UI 证据保持一致。

## 拟议测试

### 1. `StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_PlayerPromiseCreatesLongTermTodo_AndAutonomyLaterConsumesSameTodo`

- 意图：
  - 扩展当前的私聊承诺覆盖，使其不再停留在“todo 已存在”。
  - 证明后续被 autonomy 消费的就是同一个 NPC 长期 session todo。
- 断言：
  - 根 session 包含被承诺的 todo；
  - 私聊 transcript session 不拥有该 todo；
  - 后续 autonomy handle 能看到同样的 active todo 内容。

### 2. `NpcRuntimeSupervisorTests.RunOneTickAsync_AfterPrivateChatPromise_AutonomyUsesStardewActionToolsForContinuation`

- 意图：
  - 把当前“active task 被注入 prompt”测试升级为行为测试。
- 断言：
  - autonomy 工具表面包含 `stardew_move`、`stardew_task_status`、`stardew_speak`、`stardew_open_private_chat`；
  - 当存在可执行 todo 时，chat/tool loop 会至少使用一个 Stardew 动作工具；
  - 如果只产生叙述文本，则结果不能被接受为闭包。

### 3. `NpcAutonomyLoopTests.RunOneTickAsync_WhenActiveTodoExists_NarrationOnlyDecisionWritesNoToolClosureDiagnostic`

- 意图：
  - 保留一条强回归保护，防止把叙述伪装成任务推进。
- 断言：
  - 如果没有发生动作工具调用，`runtime.jsonl` 会记录一条与该 tick 绑定的 warning 条目；
  - 该诊断是可机检的，并会指向缺失的可见动作/反馈。

### 4. `StardewNpcAutonomyBackgroundServiceTests.ProcessAsync_WhenMoveOrSpeakCommandEndsBlocked_SurfacesTerminalStatusAndWritesRuntimeEvidence`

- 意图：
  - 证明 runtime/background 路径只负责暴露终态命令事实和 append-only 证据。
- 断言：
  - runtime/controller snapshot 或 recent-activity 表面会暴露终态 `blocked` 状态；
  - `runtime.jsonl` 包含 `command_terminal`；
  - 不应有任何断言期待 background service 直接 mutation 根 session todo。

### 5. `NpcAutonomyLoopTests.RunOneTickAsync_AfterTerminalBlockedOrFailedStatus_AgentWritesTodoReasonAndAttemptsPlayerFeedback`

- 意图：
  - 覆盖闭包的后半段：runtime 暴露 `blocked`/`failed` 命令事实之后，稍后的 agent/autonomy turn 会编写任务事实并尝试反馈。
  - “稍后”指的是由 agent 编写，而不是由 background 编写；它可以发生在同一条 Agent chat/tool loop 中，即 Stardew tool result 之后，也可以发生在后续 autonomy tick 中，即命令事实已经暴露之后。
- 断言：
  - agent 能通过 prompt facts / `stardew_recent_activity` / runtime context 看见已暴露的终态状态；
  - agent 会调用 `todo` / `todo_write`，使根 session todo 变为 `blocked` 或 `failed`，并附带非空的简短 `reason`；
  - agent 会尝试 `stardew_speak` 或私聊反馈路径；
  - host 不会自行合成 NPC 发言。

### 6. `NpcRuntimeSupervisorTests.TryGetTaskView_AfterBlockedPromise_ReturnsUpdatedStatusAndReasonWithoutFreshChatHandle`

- 意图：
  - 锁定面向 UI 的需求。
- 断言：
  - `TryGetTaskView(descriptor.SessionId, ...)` 会从 transcript-backed 的 tool-result projection 返回更新后的 `blocked`/`failed` todo；
  - 该行为在 runtime start/hydration 之后、且在任何新的私聊 handle 创建之前就成立。

### 7. `NpcAutonomyLoopTests.RunOneTickAsync_WhenPromisedTaskBlocks_RuntimeJsonlIncludesTaskOutcomeAndFeedbackAttempt`

- 意图：
  - 确保运行时证据足以支撑 operator/debug 审查。
- 断言：
  - `runtime.jsonl` 中会出现 `ActionType = "task_continuity"` 的记录，其 `Target` 值至少包含 `observed_active_todo`、`action_submitted`、`command_terminal`，然后是 `feedback_attempted`；
  - 对于向玩家承诺的 `blocked`/`failed` 任务，`feedback_missing` 不能满足该测试，主验收路径必须失败；
  - 当 agent 更新任务事实时，`runtime.jsonl` 还会出现 `Target = "todo_update_tool_result"`；
  - 测试必须先断言结构化的 `ActionType` / `Target` / `Stage` / `Result` 字段，再考虑自由格式的 `Error` 细节；
  - 该日志记录引用的是 NPC runtime 下同一条 session/task 生命周期。

### 8. `StardewAutonomyTickDebugServiceTests.RunOneTickAsync_WithRepositoryGamingSkillRoot_PreservesTaskContinuityAndVisibleFeedbackGuidance`

- 意图：
  - 保住以仓库资产为后盾的 prompt 保证。
- 断言：
  - 必需 skill 文本仍包含：
    - `todo` / `memory` / `session_search` 分工，
    - `stardew_task_status`，
    - `blocked`/`failed` 的简短原因，
    - 对玩家可见反馈的预期；
  - 不应出现 Minecraft / DAG / 第二任务系统渗漏。

### 9. `StardewNpcToolFactoryTests.StardewMove_PublicContract_RemainsDestinationIdOnly`

- 意图：
  - 防止语义化目的地契约回退到 label / 坐标 / 朝向输入。
- 断言：
  - `stardew_move` 的 schema/description 要求或描述 `destinationId` 作为公开目的地输入；
  - 公开 schema/description 不暴露 `label`、`x`、`y`、`tile`、`facingDirection` 或任何坐标/朝向替代项；
  - 仓库中的 navigation/world skill 指导仍要求 agent 使用规范化 destination ID，而不是 label 或原始坐标。

## 所需测试夹具 / 替身

1. 一个 chat client 替身，能够：
   - 在私聊中写入/更新 todo，
   - 在后续 autonomy 中选择 Stardew 动作工具，
   - 模拟 `blocked`/`failed` 后续决策。
2. 一个 command service 替身，能够：
  - 为 `stardew_move` 或 `stardew_speak` 返回终态 `blocked` / `failed` 状态，
  - 把这些结果暴露给 runtime/background service 以及后续 autonomy prompt context。
3. 一个围绕 `runtime.jsonl` 的运行时日志捕获夹具。
4. 一个使用真实仓库 skill-root 的 prompt 边界断言夹具。

## 日志事件映射

所有闭包证据事件都必须映射到现有 `NpcRuntimeLogRecord` 结构：

- `ActionType = "task_continuity"`。
- `Target` 为以下值之一：
  - `observed_active_todo`
  - `action_submitted`
  - `command_terminal`
  - `todo_update_tool_result`
  - `feedback_attempted`
  - `feedback_missing`
- `Stage` 标识生命周期阶段。
- `Result` 承载机器状态。
- 可用时，命令相关事件必须包含 `CommandId`。
- `Error` 是可选细节，不能成为唯一断言表面。
- 对于向玩家承诺的 `blocked`/`failed` 任务闭包，必须要求 `feedback_attempted`。`feedback_missing` 只保留给负向诊断覆盖或非玩家承诺/工具不可用场景。

## 命令

### 主命令

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~NpcRuntimeSupervisorTests|FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewAutonomyTickDebugServiceTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests"
```

### 全项目兜底

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## 审查清单

1. 每条新增行为断言是否都回绑到现有根 NPC session，而不是影子 session？
2. 测试是否证明了动作工具使用，而不只是 prompt 文案？
3. 是否把 `blocked`/`failed` 的 `reason` 断言为简短事实输出，而不是冗长自由格式说明？
4. 测试是否保住了这个边界：runtime 只暴露命令事实，而只有 agent 的 `todo` / `todo_write` 才会更新任务事实？
5. 在连续性边界重要的地方，prompt/skill 测试是否使用了真实仓库 skill 资产？
6. 每条面向玩家承诺的 `blocked`/`failed` 成功路径，是否都要求 `feedback_attempted`，同时把 `feedback_missing` 限制在负向诊断或非玩家承诺/工具不可用场景？
7. 是否有机械性的工具契约测试，防止 `stardew_move` 把 `label`、原始坐标、tile 字段或 `facingDirection` 暴露为公开输入？
