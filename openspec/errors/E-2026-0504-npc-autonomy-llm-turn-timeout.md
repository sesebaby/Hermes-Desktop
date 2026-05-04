# E-2026-0504-npc-autonomy-llm-turn-timeout

- id: E-2026-0504
- title: NPC autonomy LLM turn could hang before tool calls and starve movement
- status: active
- updated_at: 2026-05-04
- keywords: [npc-autonomy, stardew, llm-turn, timeout, worker, llm-slot, no-tool-call]
- trigger_scope: [stardew, runtime, bugfix, diagnostics]

## Symptoms

- Manual Stardew testing can run for many minutes with no visible NPC movement or speech.
- SMAPI bridge logs show no `task_move_enqueued`, no `task_running`, no `task_completed`, and no `action_speak_completed` for the current run.
- Desktop logs show `Agent: Processing message for session ...`, but no later `Agent: Executing tool stardew_move` or `stardew_speak`.
- Follow-up bridge ticks keep arriving, but the NPC worker does not make visible progress.

## Root Cause

- `StardewNpcAutonomyBackgroundService` acquired an `NpcAutonomyBudget` LLM slot and then awaited `NpcAutonomyLoop.RunOneTickAsync` without an autonomy-level timeout.
- If `Agent.ChatAsync` hung while waiting for `CompleteWithToolsAsync`, the worker and LLM slot stayed occupied until the lower HTTP/client layer returned or the app stopped.
- While that worker was busy, later dispatches only merged into the pending dispatch queue and could not actually retry the NPC decision.

## Bad Fix Paths

- Do not fix this by changing SMAPI movement/pathing when bridge logs show no movement or speak command was received.
- Do not make the host auto-send a movement or speech action to prove activity; that violates the NPC autonomous decision boundary.
- Do not rely on provider or `HttpClient.Timeout` as the only recovery path for NPC autonomy.
- Do not collapse this into `LlmConcurrencyLimit`; the slot was acquired and then held by a stuck turn.

## Corrective Constraints

- Every Stardew NPC autonomy LLM turn must have an explicit autonomy-level timeout.
- Timeout handling must pause/cool down the tracker, release the LLM slot, and allow a later dispatch to retry.
- Logs must distinguish LLM-turn start, completion, timeout, and bridge/tool execution.
- When diagnosing "NPC did nothing", first distinguish "Agent did not emit a tool call" from "bridge received a command and execution failed".

## Verification Evidence

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WhenLlmTurnTimesOut_ReleasesWorkerAndRetriesLater"` passed.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~TranscriptStoreTests.SessionSearchIndex_Search_TreatsColonLabelsAsPlainTerms"` passed.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests"` passed, 27/27.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug` passed, 813/814 with 1 skipped.
- `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64` passed with the existing SMAPI platform warning.

## Related Files

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcAutonomyBudget.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `src/search/SessionSearchIndex.cs`
- `Desktop/HermesDesktop.Tests/Services/TranscriptStoreTests.cs`
- `openspec/project.md`
