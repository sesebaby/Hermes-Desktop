# 测试规范：Stardew NPC 行动后生活闭环

## 测试目标

证明 “accepted commitment -> session todo -> action lifecycle -> terminal fact -> autonomy closure choice” 是可测试闭环，而不是只靠 prompt 期待模型更聪明。

本测试规范补充：

- `.omx/plans/stardew-tool-orchestration-harness-plan.md`
- `.omx/plans/prd-stardew-post-move-living-loop.md`

## 关键不变量

1. 真实世界写动作只走 parent/host action lifecycle。
2. local executor 不执行 `move`、`speak`、`open_private_chat`、`idle_micro_action`。
3. 私聊接受当前世界动作时，必须产生 todo 和 `npc_delegate_action`。
4. terminal completed 不自动完成 todo，只给 agent 下一轮收口机会。
5. 收口必须是显式的：todo update、新 action、或 wait/no-action reason。
6. skill/persona 只注入生活指导和倾向，不写 post-arrival 脚本。

## Test Matrix

### A. Private chat accepted immediate action

Target:

- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewNpcTools.cs`

Cases:

1. `ReplyAsync_WhenAcceptingImmediateMove_WritesTodoBeforeDelegatingAction`
   - Fake chat client emits tool calls: `todo`, `skill_view`, `npc_delegate_action`, final reply.
   - Assert tool surface contains `todo` and `npc_delegate_action`.
   - Assert todo content is commitment-shaped, not a mechanical coordinate record.
   - Assert `npc_delegate_action.action == move` and target comes from loaded POI fields.

2. `ReplyAsync_WhenAcceptingImmediateMoveWithoutTodo_RunsSelfCheckOrRejectsAsIncomplete`
   - Fake first response only calls `npc_delegate_action`.
   - Expected: self-check asks parent to repair missing commitment todo, or test asserts current prompt contract fails until implementation repairs.
   - This locks the new invariant: accepted immediate commitment cannot be action-only.

3. `ReplyAsync_WhenNoCurrentWorldAction_DoesNotForceTodo`
   - Player sends ordinary chat / future vague idea.
   - Fake client calls `npc_no_world_action`.
   - Assert no mandatory delegated action and no forced todo.

4. `ReplyAsync_WhenFuturePromiseAccepted_WritesTodoButDoesNotDelegateNow`
   - Player asks future plan.
   - Fake client calls `todo` and `npc_no_world_action`.
   - Assert no `npc_delegate_action`.

### B. Delegated action lifecycle remains host-owned

Target:

- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`

Cases:

1. `DelegatedMoveIngress_WhenAcceptedCommitment_SubmitsThroughRuntimeActionController`
   - Seed ingress work item with action `move`, reason, target.
   - Fake bridge returns accepted commandId.
   - Assert action slot/pending is set and ingress removed only according to existing lifecycle rules.

2. `DelegatedMoveIngress_DoesNotUseLocalExecutorForMove`
   - Local executor fake records invocations.
   - Seed move ingress.
   - Assert local executor not called.

3. `LocalExecutorSurface_StillExcludesRealWriteActions`
   - Assert local executor tool names exclude `stardew_navigate_to_tile`, `stardew_speak`, `stardew_open_private_chat`, `stardew_idle_micro_action`.

### C. Terminal completed creates closure opportunity

Target:

- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewNpcTools.cs`

Cases:

1. `RunOneTickAsync_WithCompletedLastActionAndActiveCommitmentTodo_IncludesBothInDecisionMessage`
   - Seed session todo as `in_progress`.
   - Set `LastTerminalCommandStatus` to completed move.
   - Run one autonomy tick with fake agent.
   - Assert fake agent last message contains `last_action_result` and active todo context.

2. `RecentActivityTool_WithCompletedLastActionAndTodo_ReturnsLastActionAndTodoFacts`
   - Seed runtime driver with terminal status and active todo.
   - Call recent activity tool.
   - Assert facts contain `lastAction=` and `todo[0]=`.

3. `TerminalCompleted_DoesNotHostAutoCompleteTodo`
   - Seed active todo and terminal completed.
   - Run background status advance.
   - Assert todo remains active until agent calls `todo`.

### D. Closure choices

Target:

- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`

Cases:

1. `RunOneTickAsync_AfterMoveCompleted_WhenAgentMarksTodoCompleted_RecordsTodoClosure`
   - Fake agent sees last action and calls `todo` with status `completed`.
   - Assert runtime jsonl has `todo_update_tool_result`, status `completed`.

2. `RunOneTickAsync_AfterMoveCompleted_WhenAgentStartsNewAction_RecordsActionSubmitted`
   - Fake agent calls `stardew_speak` or `stardew_idle_micro_action`.
   - Assert runtime jsonl has `action_submitted` and command evidence.

3. `RunOneTickAsync_AfterMoveCompleted_WhenAgentWaitsWithReason_RecordsExplicitNoAction`
   - Fake agent returns explicit wait/no-action shape with reason.
   - Assert diagnostic records reason.

4. `RunOneTickAsync_AfterMoveCompleted_WhenAgentDoesNothingWithoutReason_RecordsMissingClosureDiagnostic`
   - Fake agent returns no tool call and no reason.
   - Assert runtime jsonl records diagnostic such as `closure_missing`.

### E. Failure/blocked/timeout recovery

Target:

- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`

Cases:

1. `RunOneTickAsync_WithBlockedLastActionAndActiveTodo_AgentMarksTodoBlocked`
   - Seed terminal status blocked with reason.
   - Fake agent calls `todo` status `blocked`.
   - Assert reason is preserved and short.

2. `RunOneTickAsync_WithFailedLastActionAndActiveTodo_AgentMarksTodoFailed`
   - Same for failed.

3. `RunOneTickAsync_WithTimeoutLastActionAndActiveTodo_DoesNotRepeatSameActionBlindly`
   - Fake agent must either update todo blocked/failed, observe/status, or wait with reason.
   - Assert no duplicate same command submission without new fact.

4. `BackgroundService_WhenTerminalFailureOccurs_WakesOrSchedulesByExistingPolicy`
   - Assert failure/blocked/timeout are not swallowed by cooldown.

### F. Skill/persona injection

Target:

- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `skills/gaming/stardew-task-continuity/SKILL.md`
- `skills/gaming/stardew-world/SKILL.md`
- `src/game/stardew/personas/haley/default/SOUL.md`
- `src/game/stardew/personas/haley/default/facts.md`

Cases:

1. `RunOneTickAsync_HaleyPromptInjectsLivingClosureGuidanceThroughSkills`
   - Assert system prompt contains task-continuity guidance for commitment closure.
   - Assert it comes through required skill asset path, not fixture-only string.

2. `RunOneTickAsync_HaleyPromptInjectsPersonaInclinationsWithoutHardcodedPostArrivalRule`
   - Assert Haley persona contains bright/clean/photogenic preference.
   - Assert prompt does not contain fixed beach-after-arrival script.

3. `SkillAssets_DoNotContainCoordinateOrDestinationHardcoding`
   - Scan edited skill/persona files for production-like coordinate or route hardcoding.

### G. Hardcode and boundary gates

Commands:

```powershell
rg -n "Haley|Willy|Beach|Town|海边|镇|destination\\[|nearby\\[|moveCandidate\\[" src Desktop/HermesDesktop.Tests Mods/StardewHermesBridge -g "*.cs"
```

Expected:

- Test fixture hits are allowed.
- Production code must not add NPC/place/natural-language routing rules.
- No host-injected `destination[n]` / `nearby[n]` / `moveCandidate[n]` in autonomy wake prompts.

Local executor gate:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~NpcLocalExecutorRunnerTests|FullyQualifiedName~StardewNpcToolFactoryTests"
```

Expected:

- Existing and new tests prove local executor cannot execute real write actions.

## Focused Verification Command

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewAutonomyTickDebugServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests"
```

## Broader Regression

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## Live AI Smoke Policy

Live AI smoke 只能作为补充证据：

- 先跑 deterministic fake-client tests。
- 再跑已有 live smoke，确认真实父层模型能在私聊请求中产生 todo + `npc_delegate_action`。
- 不恢复 obsolete local-executor live move test，因为 local executor 不允许真实移动。

Recommended live filters:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewLiveAiSmokeTests"
```

## Completion Evidence

执行实现后，最终报告必须包含：

- 修改文件列表。
- 新增/修改测试名称。
- focused verification 输出摘要。
- bridge regression/build/full tests 是否运行。
- hardcode scan 人工判读结果。
- 未测范围，例如真实长时间游戏手测、不同 NPC persona、跨 NPC group tasks。
