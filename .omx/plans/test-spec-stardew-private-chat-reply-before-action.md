# Test Spec: Stardew Private Chat Reply Before Action

## RED Tests

- `StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenSuccessfulHostTaskSubmissionEndsWithEmptyReply_RunsReplySelfCheckWithoutDuplicatingDelegation`
  Verifies the runner detects a successful `stardew_submit_host_task` plus empty final reply, sends one bounded self-check asking only for a natural reply, returns the agent-authored reply, and does not enqueue a duplicate ingress.

- `StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenSuccessfulHostTaskSubmissionStillHasEmptyReply_BlocksQueuedIngress`
  Verifies a second empty reply after the bounded self-check blocks the queued ingress via terminal runtime state instead of allowing silent execution.

- `StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithDelegatedMoveAfterPrivateChatReplyDisplayed_StaysDeferredUntilReplyClosed`
  Verifies `private_chat_reply_displayed` alone does not submit the move, preserves ingress, and records a deferred diagnostic.

- `StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithDelegatedMoveAfterPrivateChatReplyClosed_SubmitsMove`
  Verifies the matching `private_chat_reply_closed` event allows the previously queued delegated move to execute.

- `StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithFutureWindowHostTaskSubmissionBeforeReplyClosed_StaysDeferred`
  Verifies the reply-closed gate applies to private-chat `conversationId` ingress for non-move actions such as `craft`, even though the action later resolves to blocked/unsupported.

## Regression Tests

- `ReplyAsync_WhenImmediateActionDelegatedWithoutTodo_RetriesForCommitmentTodoWithoutDuplicatingDelegation` must keep passing, proving successful submission still uses the missing-todo repair path when the reply is present.
- `ReplyAsync_WhenSubmitHostTaskFailsValidation_AllowsNativeRetryWithoutCommitmentTodoSelfCheck` must keep passing, proving failed tool attempts are still not treated as successful submission.
- Existing defer-budget tests must keep passing, proving reply-closed waiting reuses the bounded deferred/block lifecycle instead of adding a second state machine.

## Verification Commands

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenSuccessfulHostTaskSubmissionEndsWithEmptyReply_RunsReplySelfCheckWithoutDuplicatingDelegation|FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests.ReplyAsync_WhenSuccessfulHostTaskSubmissionStillHasEmptyReply_BlocksQueuedIngress|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithDelegatedMoveAfterPrivateChatReplyDisplayed_StaysDeferredUntilReplyClosed|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithDelegatedMoveAfterPrivateChatReplyClosed_SubmitsMove|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WithFutureWindowHostTaskSubmissionBeforeReplyClosed_StaysDeferred" -p:UseSharedCompilation=false
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests" -p:UseSharedCompilation=false
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false
```

## Review Checklist

- The only new reply repair path is a bounded parent self-check; no host-authored dialogue exists.
- `private_chat_reply_closed` is the sole execution gate for private-chat host-task ingress with `conversationId`.
- Reply-wait failures become existing deferred/block diagnostics and terminal facts, not silent execution.
- Non-move private-chat host-task actions are gated consistently before they later become unsupported/blocked facts.
