## 1. Grounding And Regression Tests

- [x] 1.1 Re-read `Desktop/HermesDesktop/AGENTS.md` and required desktop instruction files before touching desktop test or runtime code.
- [x] 1.2 Re-read relevant `openspec/errors` entries for private-chat movement, hidden local executor fallback, reply action-chain guard, and runtime context-as-AI-field mistakes.
- [x] 1.3 Add a failing `StardewNpcAutonomyBackgroundServiceTests` regression proving a private-chat host task waiting for reply UI lifecycle remains recoverable after exceeding the generic stale/busy defer attempt budget.
- [x] 1.4 Add or update a regression proving generic action-slot/pending-work busy deferral still blocks after the configured generic budget.
- [x] 1.5 Add a failing test proving `stardew_task_status` returns an agent-readable `summary` while preserving `status` and `commandId`.
- [x] 1.6 Add failing harness coverage for `blocked/action_slot_busy`: a conflicting world action must not create another queued world-action item and must not auto-retry after the running action completes.
- [x] 1.7 Add or update real-asset prompt/skill boundary tests before editing prompt assets; assert guidance rejects `local_executor`, hidden host-inferred next actions, and broad hidden locks.
- [x] 1.8 Define the scoped status/result coverage matrix in tests: recoverable UI wait, `action_slot_busy`, `host_task_submission_deferred_exceeded`, timeout, and lease/menu conflict; map timeout and lease/menu reason codes from existing code before asserting them.

## 2. Runtime Classification Fix

- [x] 2.1 Separate private-chat reply UI lifecycle waiting from generic stale/busy ingress deferral in `StardewNpcAutonomyBackgroundService`.
- [x] 2.2 Keep waiting private-chat host task submissions queued or recoverable, with diagnostic logs and next wake scheduling, without incrementing or blocking on the generic defer budget.
- [x] 2.3 Preserve terminal blocking for malformed payloads, unsupported actions, hard blockers, and true generic busy/stale ingress loops.
- [x] 2.4 Verify the fix does not add a second host task queue, does not allow parallel body actions, and does not let a new conflicting action replace the current action slot.

## 3. Text-First Tool Result Summaries

- [x] 3.1 Add the smallest summary-shaping helper needed for Stardew action/status tool results; avoid a broad abstraction until multiple concrete call sites need it.
- [x] 3.2 Update `stardew_task_status` so its result includes a concise `summary` for running, waiting, completed, blocked, failed, cancelled, timeout, and unknown status cases.
- [x] 3.3 Add RED tests and implementation for `summary` on representative in-scope action results: queued host task submission, blocked `action_slot_busy`, and one completed or terminal action/status path.
- [x] 3.4 Ensure `summary` text remains factual and non-prescriptive: it may explain state and reason, but must not choose the agent's next action.

## 4. Prompt And Skill Guidance

- [x] 4.1 Run the prompt/skill boundary RED tests from 1.7 and confirm they fail for the missing non-blocking guidance before editing assets.
- [x] 4.2 Update `skills/gaming/stardew-core/SKILL.md` to state that windows, menus, animation, and events are game facts/status, not agent-flow locks.
- [x] 4.3 Update `skills/gaming/stardew-task-continuity/SKILL.md` to direct continuation through `stardew_task_status` for known long or waiting work and to treat terminal blocked/failed facts as agent decision points.
- [x] 4.4 Update `skills/system/stardew-npc-runtime/SYSTEM.md` so the runtime prompt keeps the host fact-only and makes the agent own the next decision.
- [x] 4.5 Re-run prompt/skill boundary tests and confirm the real repository assets contain non-blocking status guidance and no host-inferred next-action language.

## 5. Verification And Error Memory

- [x] 5.1 Run the new RED tests first and confirm they fail for the expected missing behavior before implementation.
- [x] 5.2 Run targeted `StardewNpcAutonomyBackgroundServiceTests` after the runtime fix. Evidence: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests" -p:UseSharedCompilation=false` passed 60/60, including durable reply-delivery while slot busy, undelivered reply while slot busy, retryable bridge failure after reply delivery, and old UI-wait attempt budget cases.
- [x] 5.3 Run targeted `StardewNpcToolFactoryTests` or equivalent tool-result tests after summary changes. Evidence: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests" -p:UseSharedCompilation=false` passed 39/39.
- [x] 5.4 Run broader Stardew test coverage with `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false`. Evidence: passed 275/275 with 2 skipped live-AI tests.
- [x] 5.5 If bridge code is touched, run `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug -p:UseSharedCompilation=false`. Evidence: passed 140/140 with an existing obsolete-warning in `BridgeMovementPathProbe.cs`.
- [x] 5.6 Update or create an `openspec/errors` entry recording that recoverable game UI/window lifecycle waits must not reuse generic agent/ingress retry budgets.
- [x] 5.7 Run `openspec status --change "stardew-agent-game-window-decoupling"` and confirm all artifacts are complete before implementation handoff. Evidence: 4/4 artifacts complete.
- [x] 5.8 Run `openspec validate "stardew-agent-game-window-decoupling" --strict` and fix any OpenSpec validation failures before implementation handoff. Evidence: change is valid.
