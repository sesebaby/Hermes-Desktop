# Stardew NPC Local Executor V1 Context

## Task Statement

Advance the next Stardew NPC local-executor phase after the provider-routing and intent-contract work has passed manual smoke testing.

## Desired Outcome

Produce an execution-ready plan that makes the local small-model executor contribution clear, measurable, and safely expandable without moving NPC personality, speech authorship, memory, or long-term decisions out of the parent agent.

## Known Facts / Evidence

- The latest relevant commit is `a5ab5ee1 Keep Stardew local work on the delegation lane`.
- The existing design spec is `.omx/specs/stardew-npc-local-executor-minimal-test.md`.
- Existing provider-routing plans are complete and should not be treated as the next implementation scope:
  - `.omx/plans/stardew-npc-delegation-provider-routing-consensus-plan.md`
  - `.omx/plans/stardew-npc-delegation-provider-routing-initial-plan.md`
  - `.omx/plans/stardew-private-chat-open-decoupling-plan.md`
- `src/runtime/NpcAutonomyLoop.cs:367` parses the parent response as a local action intent and rejects invalid intent contracts.
- `src/runtime/NpcAutonomyLoop.cs:405` logs parent tool surface verification as `registered_tools=0`.
- `src/runtime/NpcAutonomyLoop.cs:410` logs local executor selection with `lane=delegation`.
- `src/runtime/NpcAutonomyLoop.cs:423` submits speech from the parent `speech` contract as a host action, so `stardew_speak` is not evidence that the local model authored speech.
- `src/runtime/NpcLocalExecutorRunner.cs:80` currently host-interprets `observe`, `wait`, and `escalate`, so those actions do not prove a local model call.
- `src/runtime/NpcLocalExecutorRunner.cs:93` calls the delegation chat client with tool definitions for model-called local execution.
- `src/runtime/NpcLocalExecutorRunner.cs:160` records host-interpreted actions through `CompleteHostInterpreted`.
- `src/games/stardew/StardewNpcTools.cs:12` currently restricts local executor tools to `stardew_move` and `stardew_task_status`.
- `src/games/stardew/StardewNpcTools.cs:55` creates the local executor tool surface from the default Stardew tools.
- `src/runtime/NpcRuntimeSupervisor.cs:264` builds an empty autonomy parent tool surface, and `src/runtime/NpcRuntimeSupervisor.cs:277` disables capability registration for the autonomy parent.
- `src/runtime/NpcRuntimeSupervisor.cs:314` creates `NpcUnavailableLocalExecutorRunner` if `DelegationChatClient` is missing.
- `src/runtime/NpcLocalExecutorRunner.cs:27` currently blocks missing-delegation `move/task_status`, but host-interprets `observe/wait/escalate`; if v1 promotes `observe` to `model_called`, unavailable `observe` must become blocked too.
- `src/runtime/NpcRuntimeLogWriter.cs:45` has no explicit field for `executorMode`, so current logs force humans to infer whether a local executor result came from a model call or host interpretation.
- Tests already cover parent contract-only behavior and local executor move routing:
  - `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs:400`
  - `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs:525`
  - `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs:53`

## Constraints

- Parent/autonomy/private-chat lanes must remain cloud/high-quality capable; only the local executor/delegation lane should use the LM Studio local model route.
- Do not claim `stardew_speak` is local-model speech unless logs prove a local executor path authored it. Current design says it is parent-authored speech submitted by host.
- The host validates and executes real Stardew side effects; it must not author NPC intent.
- Local executor must not make relationship, gift, trade, memory, personality, or long-term planning decisions.
- Keep diffs small and testable. No new dependencies.
- Avoid asking models for irrelevant fields. Optional intent fields should be omitted unless action-specific.
- Worktree currently has unrelated dirty files: `.omx/state/session.json` and `其他资料/ohmycodex使用说明.md`; do not touch them unless separately requested.

## Unknowns / Open Questions

- Whether losing the current host-interpreted `observe` fallback is acceptable. The v1 plan chooses audit purity: if `observe` is model-called, missing delegation blocks it.
- Whether `wait` should ever call the local model. Current evidence suggests no, because it adds latency with no useful tool work.
- Whether local executor no-tool-call failures should retry once or immediately block.
- Whether token/cost observability should be billing-grade now or remain approximate until provider usage plumbing exists.

## Likely Codebase Touchpoints

- `src/runtime/NpcLocalExecutorRunner.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcRuntimeLogWriter.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs` only if skill-facing wording must be synchronized; the parent raw JSON contract owner is `NpcAutonomyLoop.BuildDecisionMessage`.
- `Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
