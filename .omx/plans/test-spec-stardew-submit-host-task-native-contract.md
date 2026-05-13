# Test Spec: Stardew Submit Host Task Native Contract

## RED Tests

- `StardewNpcToolFactoryTests.SubmitHostTaskTool_WithStringMoveTarget_ReturnsStructuredFailureWithoutIngress`
  Verifies a raw string `target` produces `ToolResult.Fail`, mentions object-shaped `target(locationName,x,y,source)`, and creates no ingress work item.

- `StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenSubmitHostTaskFailsValidation_AllowsNativeRetryWithoutCommitmentTodoSelfCheck`
  Verifies a first failed `stardew_submit_host_task` call returns to the same agent-native tool loop and lets the parent retry without entering the missing-todo repair path.

- `StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenSubmitHostTaskFailsValidationAndModelStops_RunsDelegationSelfCheckAndQueuesRetry`
  Verifies a failed `stardew_submit_host_task` followed by final text does not count as a successful submission; the runner sends the delegation self-check back to the parent agent and the retry queues one valid host-task ingress.

- `StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenNoWorldActionFailsValidation_RunsDelegationSelfCheck`
  Verifies a failed `npc_no_world_action` call does not count as closure and the runner sends one bounded self-check back to the parent agent.

## Regression Tests

- Existing `ReplyAsync_WhenImmediateActionDelegatedWithoutTodo_RetriesForCommitmentTodoWithoutDuplicatingDelegation` must still pass, proving successful submission still triggers only missing-todo repair.
- Existing `ReplyAsync_WhenNoWorldActionToolCalled_DoesNotSelfCheck` must still pass, proving explicit no-action remains a valid closure.
- Existing host-task tool tests must still pass for valid move and future unsupported action lifecycle skeletons.

## Verification Commands

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests.SubmitHostTaskTool_WithStringMoveTarget_ReturnsStructuredFailureWithoutIngress|FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenSubmitHostTaskFailsValidation_AllowsNativeRetryWithoutCommitmentTodoSelfCheck|FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenSubmitHostTaskFailsValidationAndModelStops_RunsDelegationSelfCheckAndQueuesRetry|FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenNoWorldActionFailsValidation_RunsDelegationSelfCheck" -p:UseSharedCompilation=false
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests|FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests" -p:UseSharedCompilation=false
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false
```

## Review Checklist

- No host natural-language target parsing.
- No hidden executor or local executor references in the new path.
- Failed tool attempts are model-visible and do not satisfy successful submission gates.
- Validation stays local to `stardew_submit_host_task`, not global Agent behavior.
