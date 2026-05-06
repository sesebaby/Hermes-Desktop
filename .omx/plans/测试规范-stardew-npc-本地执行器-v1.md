# 测试规范：Stardew NPC 本地执行器 V1 加固

## 单元测试

- `NpcLocalExecutorRunnerTests.ExecuteAsync_WithMoveIntent_ExposesOnlyMoveToolAndLogsModelCalled`
  - 给定一个 `move` 意图，以及一个包含多个工具的 local executor tool surface。
  - 断言 delegation stream 只能接收到 `stardew_move`。
  - 断言结果是 `executorMode=model_called`。

- `NpcLocalExecutorRunnerTests.ExecuteAsync_WithTaskStatusIntent_ExposesOnlyTaskStatusTool`
  - 给定一个带 command id 的 `task_status` 意图。
  - 断言只暴露并执行 `stardew_task_status`。

- `NpcLocalExecutorRunnerTests.ExecuteAsync_WithObserveIntent_UsesReadOnlyObserveTool`
  - 给定一个 `observe` 意图。
  - 断言 runner 会调用本地模型，并且只暴露 `stardew_status` 或选定的只读 current-state 工具。
  - 断言不会暴露 move/speak/private-chat 工具。
  - 断言 `observeTarget` 只作为提示性上下文传入，而不是作为定向观察的工具参数。

- `NpcLocalExecutorRunnerTests.ExecuteAsync_WithWaitIntent_CompletesHostInterpretedWithoutModelCall`
  - 给定一个 `wait` 意图。
  - 断言 delegation stream 调用次数为 0，且 `executorMode=host_interpreted`。

- `NpcUnavailableLocalExecutorRunnerTests.ExecuteAsync_WithObserveIntent_BlocksWhenDelegationUnavailable`
  - 给定一个 `observe` 意图，并且 delegation client 缺失。
  - 断言 `executorMode=blocked`，而不是 `host_interpreted`。
  - 断言 `wait` 和 `escalate` 在没有 delegation 时仍然走 host-interpret。

- `NpcLocalExecutorRunnerTests.ExecuteAsync_WithNoToolCall_RetriesOnceThenBlocks`
  - 给定一个 model-called action，但本地模型没有产生 tool call。
  - 断言会发生两次 local stream 尝试，随后进入 `local_executor_blocked:no_tool_call`。
  - 断言证据形状固定为：诊断记录 `target=local_executor stage=attempt result=no_tool_call;attempt=1`、诊断记录 `target=local_executor stage=retry result=no_tool_call;attempt=2`，以及最终的 `local_executor stage=blocked result=no_tool_call`。
  - 断言不会发生宿主侧工具执行兜底。

- `NpcLocalExecutorRunnerTests.BuildUserMessage_OmitsIrrelevantOptionalIntentFieldsByAction`
  - 给定一个 `move` 意图。
  - 断言序列化后的意图不包含 `commandId`、`observeTarget`、`waitReason` 或 `escalate=false`。
  - 给定一个 `observe` 意图。
  - 断言序列化后的意图不包含 `destinationId`、`commandId` 或 `waitReason`。
  - 给定一个 `wait` 意图。
  - 断言序列化后的意图不包含 `destinationId`、`commandId` 或 `observeTarget`。
  - 给定一个 `task_status` 意图。
  - 断言序列化后的意图不包含 `destinationId`、`observeTarget`、`waitReason` 或 `escalate=false`。
  - 给定一个 `escalate` 意图。
  - 断言序列化后的意图不包含 `destinationId`、`commandId`、`observeTarget` 或 `waitReason`。

- `NpcRuntimeLogWriterTests.WriteAsync_WithExecutorMode_IsBackwardCompatible`
  - 断言新日志记录可以包含 `executorMode`。
  - 断言不带 `executorMode` 的旧 JSONL 记录仍可通过现有测试工具完成反序列化/读取。

## 集成测试

- `NpcAutonomyLoopTests.RunOneTickAsync_WithMoveIntent_LogsExecutorModeAndRestrictedTool`
  - 父层返回有效的 move contract。
  - 断言 runtime log 链包含 `parent_tool_surface`、`local_executor selected` 和 `local_executor executorMode=model_called`。

- `NpcAutonomyLoopTests.RunOneTickAsync_WithSpeechIntent_SubmitsHostSpeechButDoesNotTreatSpeakAsLocalExecutor`
  - 父层返回 `speech.shouldSpeak=true`。
  - 断言存在 `host_action target=stardew_speak`。
  - 断言不存在 `local_executor target=stardew_speak`。
  - 断言 `host_action target=stardew_speak` 可以与 blocked 的 local executor 结果同时出现，且不会被计为 local executor 成功。

- `NpcRuntimeSupervisorTests.GetOrCreateAutonomyHandleAsync_LocalExecutorToolSurfaceIncludesObserveReadOnlyTool`
  - 断言 local executor tools 包含 `stardew_move`、`stardew_task_status` 和选定的只读 observe/status 工具。
  - 断言 autonomy 父层仍然不会接收到任何已注册工具。

- `StardewNpcToolFactoryTests.CreateLocalExecutorTools_ContainsOnlyAllowedMechanicalReadOnlyTools`
  - 断言 local executor surface 不包含 `stardew_speak`、`stardew_open_private_chat`、gift、trade、memory、agent 或 todo 工具。

## 手工验证

1. Start LM Studio at `http://127.0.0.1:1234/v1` with the configured `delegation` model.
2. Start Hermes Desktop and Stardew.
3. Run one manual session with at least one `move`, one `wait`, one `observe`, and one parent-authored `speech`.
4. Inspect latest NPC `runtime.jsonl`.
5. Confirm:
   - parent tool surface is `registered_tools=0`
   - `move` has `executorMode=model_called` and `target=stardew_move`
   - `observe` has `executorMode=model_called` and a read-only target
   - `wait` has `executorMode=host_interpreted`
   - `stardew_speak` appears only as `host_action`
   - any no-tool-call retry is visible and counted
   - missing delegation blocks `observe` while `wait/escalate` remain host-interpreted
   - speech beside a blocked local executor result is counted as parent/host behavior, not local executor success
6. Missing delegation reproducible path: remove or blank the `delegation` section in `%LOCALAPPDATA%\hermes\config.yaml`, restart Desktop, trigger an `observe` intent, and confirm the mode split above.
7. No-tool-call reproducible path: use `NpcLocalExecutorRunnerTests.ExecuteAsync_WithNoToolCall_RetriesOnceThenBlocks`; do not require live LM Studio prompt manipulation.

## 验证命令

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcLocalExecutorRunnerTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcRuntimeSupervisorTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```
