# 测试规格：单个 NPC 驱动的 Stardew 自主循环

## 策略

采用先测试后交付。测试必须在不需要 Stardew Valley、SMAPI 或真实桥接的前提下，证明自主性、隔离性和 typed bridge 行为。

最重要的负向测试是：事件只是事实。任何事件摄取路径都不能调用 LLM，也不能发出 Stardew 命令。

## 必需测试

1. `NpcAutonomyLoop_RunOneTick_ObservesBeforeDecision`
   - 组装假的观察器、假的 LLM/agent、假的命令服务。
   - 断言 observation 在任何 decision/tool 动作之前被调用。

2. `NpcAutonomyLoop_EventFact_DoesNotDriveAgent`
   - 注入一个假的桥接/游戏事件。
   - 断言不会发生 LLM completion、不会调用 `Agent.ChatAsync`、不会调用 `StardewCommandService.SubmitAsync`、不会移动，也不会说话。
   - 断言该事件会作为下一 tick 的 observation fact 可用。

3. `NpcAutonomyLoop_UsesRuntimeLocalContextManagerAndPromptBuilder`
   - 构建一个 Haley 运行时命名空间。
   - 断言上下文准备通过运行时本地的 `ContextManager` / `PromptBuilder` 完成。
   - 断言不需要也不会使用自定义的 Stardew prompt assembler。

4. `NpcAutonomyLoop_LoadsHaleyPersonaFromPackAndNamespace`
   - 使用 Haley 默认 persona pack。
   - 断言 `SOUL.md`、facts、voice/boundaries 和 skills 会被解析进 NPC 本地上下文路径。

5. `NpcAutonomyLoop_RegistersOnlyNpcSafeTools`
   - 检查 NPC agent 可用的工具定义。
   - 断言全局桌面工具不存在。
   - 断言只存在允许的 Stardew / NPC 本地工具。

6. `NpcAutonomyLoop_MoveDecision_UsesStardewCommandService`
   - 模拟模型请求移动。
   - 断言 `GameActionType.Move` 通过 `StardewCommandService` 提交。
   - 断言记录了 `commandId`、`traceId` 和幂等性上下文。

7. `NpcAutonomyLoop_SpeakDecision_UsesStardewCommandService`
   - 模拟模型请求说话。
   - 断言 `GameActionType.Speak` 通过 `StardewCommandService` 提交。
   - 断言结果被记录。

8. `NpcAutonomyLoop_PollsLongRunningCommandUntilTerminalOrLimit`
   - 模拟状态序列：queued -> running -> completed。
   - 断言轮询会在终态停止。
   - 如果实现允许，在同一切片里再补一个 failure/blocked 变体。

9. `NpcAutonomyLoop_BridgeUnavailable_WritesNoOpTrace`
   - 模拟 discovery/observer 不可用。
   - 断言不会提交任何 action command。
   - 断言会写入带失败原因的 no-op/paused trace。

10. `NpcAutonomyLoop_CompletedTick_WritesTraceActivityAndMemory`
   - 模拟一次成功且有意义的动作。
   - 断言 trace/log 条目位于 NPC 命名空间下。
   - 断言 `LastTraceId` 被更新。
   - 断言 NPC 本地记忆写入被尝试或完成。

11. `NpcAutonomyLoop_EnforcesBudgetAndToolIterationLimit`
   - 使用 `NpcAutonomyBudget`。
   - 断言迭代/并发限制可以阻止失控行为。

12. `NpcRuntimeSupervisor_StartStop_PreservesSnapshotSemantics`
   - 扩展现有 supervisor 测试。
   - 断言生命周期状态和 trace id 仍能通过快照看到。

## 回归测试

- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyBudgetTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/ResourceClaimRegistryTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandContractTests.cs`
- `Desktop/HermesDesktop.Tests/GameCore/NpcPackLoaderTests.cs`
- `Desktop/HermesDesktop.Tests/GameCore/NpcPackManifestTests.cs`

## 验证命令

目标命令：

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:Platform=x64 --filter "FullyQualifiedName~NpcAutonomyLoop|FullyQualifiedName~AutonomyBoundary|FullyQualifiedName~EventFact"
```

运行时和 Stardew 回归：

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:Platform=x64 --filter "FullyQualifiedName~Runtime|FullyQualifiedName~Stardew|FullyQualifiedName~GameCore"
```

最终：

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:Platform=x64
```
