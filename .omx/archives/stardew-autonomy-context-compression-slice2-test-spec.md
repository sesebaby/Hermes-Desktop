# Test Spec: Stardew Autonomy Context Compression Slice 2

Date: 2026-05-06
PRD: `.omx/plans/stardew-autonomy-context-compression-slice2-prd.md`

## Test Goals

1. Prove first-call behavior remains covered by `IFirstCallContextBudgetPolicy`.
2. Prove later autonomy tool-loop requests are compacted through `IOutboundContextCompactionPolicy`.
3. Prove non-autonomy sessions remain no-op.
4. Prove surviving tool call/result pairs are protocol-safe after pruning.
5. Prove autonomy `SessionState` is shaped before `PromptBuilder` serializes JSON.
6. Prove usage/cost telemetry works with both provider usage and estimated fallback.

## Test Cases

- `Agent_FirstToolIteration_AppliesPolicyBeforeClientCall`
- `Agent_LaterToolIteration_AppliesOutboundCompactionBeforeClientCall`
- `BudgetPolicy_MarkerMissing_NoOps`
- `BudgetPolicy_SanitizesOrphanAndMissingToolPairs`
- `BudgetPolicy_CompletedLogIncludesEstimatedTokenAndDeepSeekCostFields`
- `PrepareContextAsync_AutonomySessionCompactsSessionStateBeforePromptBuild`
- `Agent_LlmCompletionLogUsesProviderUsageWhenAvailable`

## Acceptance Mapping

- First-call contract preserved -> `Agent_FirstToolIteration_AppliesPolicyBeforeClientCall`.
- Every outbound autonomy request compacted -> `Agent_LaterToolIteration_AppliesOutboundCompactionBeforeClientCall`.
- Non-autonomy no-op -> existing marker-missing and status-budget no-marker tests.
- Tool protocol safety -> `BudgetPolicy_SanitizesOrphanAndMissingToolPairs`.
- SessionState structural shaping -> `PrepareContextAsync_AutonomySessionCompactsSessionStateBeforePromptBuild`.
- Provider/estimated telemetry -> `BudgetPolicy_CompletedLogIncludesEstimatedTokenAndDeepSeekCostFields` and `Agent_LlmCompletionLogUsesProviderUsageWhenAvailable`.

## Verification Commands

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextBudgetTests|FullyQualifiedName~MemoryParityTests.PrepareContextAsync_AutonomySessionCompactsSessionStateBeforePromptBuild"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentTests|FullyQualifiedName~NpcAgentFactoryTests|FullyQualifiedName~NpcRuntimeContextFactoryTests"
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
```

## Out Of Scope

- Transcript rewrite.
- Session split.
- Generic LLM summary main path.
- Host-side NPC decisions.
- Hard 30-minute cost budget enforcement.
