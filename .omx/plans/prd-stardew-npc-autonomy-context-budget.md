# PRD: Stardew NPC Autonomy Context Budget

Date: 2026-05-05
Status: Approved by `$ralplan` consensus review
Spec: `docs/superpowers/specs/2026-05-05-stardew-npc-autonomy-context-budget-design.md`
Context: `.omx/context/stardew-npc-autonomy-context-budget-20260505T111228Z.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Keep Stardew NPC autonomy first-call LLM context near a 5,000 character target without creating a second memory, persona, summary, or tool lane.

**Architecture:** Add an injectable first-call context budget policy. `Agent` only calls a narrow optional interface; `NpcAutonomyLoop` marks autonomy sessions explicitly; the Stardew policy no-ops unless that marker is true.

**Tech Stack:** C# / .NET 10, Hermes `Agent`, `ContextManager`, `TurnMemoryCoordinator`, `MemoryManager`, Stardew runtime, MSTest.

---

## RALPLAN-DR Summary

### Principles

1. Budget only Stardew NPC autonomy first-call context in this change.
2. Keep ordinary desktop chat, NPC private chat, and non-autonomy NPC turns unchanged unless a later spec broadens scope.
3. Reuse existing `ContextManager`, `TokenBudget`, `MemoryManager`, `TurnMemoryCoordinator`, `TranscriptRecallService`, `session_search`, and compaction concepts.
4. Preserve NPC decision ownership; host code may budget/prune/tool-gate but must not choose NPC intent.
5. Use deterministic delete/truncate/dedupe/placeholders only; do not add another summary/persona/memory lane.

### Decision Drivers

1. `Agent.ChatAsync` sends `preparedContext` on the first tool-capable LLM call, and this is the actual payload control point.
2. `ContextManager` is a generic archive/state/recall owner, so the 5,000 character Stardew autonomy target should not be pushed into it.
3. The NPC runtime has a clear construction chain for injecting a workload policy while preserving a no-op default.

### Options

1. **Inject a first-call budget policy into `Agent` and gate by autonomy marker.**
   - Pros: Directly controls first payload, default no-op is safe, avoids generic context pollution.
   - Cons: Adds one optional Agent dependency and a session marker contract.

2. **Put the 5,000 character rule in `ContextManager` / `TokenBudget`.**
   - Pros: Centralized context code.
   - Cons: Pollutes generic chat behavior and mixes Stardew character-budget policy with generic token-budget semantics.

3. **Rewrite the autonomy decision message in `NpcAutonomyLoop`.**
   - Pros: Very local to autonomy.
   - Cons: Does not control soul, skill, memory, recall, recent turns, or tool result payloads.

**Decision:** Use option 1. Options 2 and 3 are rejected because they either broaden scope or miss the actual bloated payload surface.

## Verified Code Facts

- `src/runtime/NpcAutonomyLoop.cs:125-146` creates the decision session, writes `traceId` / `npcId`, and calls `_agent.ChatAsync(...)`.
- `src/Core/Agent.cs:348-357` prepares optimized context through `AgentContextAssembler.PrepareOptimizedContextAsync(...)`.
- `src/Core/Agent.cs:402-415` sends `preparedContext` only on the first tool-capable iteration.
- `src/Core/Agent.cs:442-449` persists assistant tool request messages with `ToolCalls`.
- `src/runtime/NpcAutonomyLoop.cs:415-428` already groups tool result messages by `ToolCallId`.
- `src/runtime/NpcAgentFactory.cs:21-30` constructs NPC `Agent` instances from `NpcRuntimeContextBundle`.
- `src/runtime/NpcRuntimeContextFactory.cs:76-108` creates the NPC context bundle.
- `src/runtime/NpcRuntimeSupervisor.cs:307-339` wires `NpcRuntimeContextFactory`, `NpcAgentFactory`, and registered tools.
- `src/Core/Agent.cs:230-255` and `src/Core/Agent.cs:1396-1415` contain the existing per-tool-name Stardew status budget.

## Requirements

### Autonomy-Only Marker

- Add a single named marker contract, for example `StardewAutonomySessionKeys.IsAutonomyTurn`.
- `NpcAutonomyLoop` must set the marker on decision sessions, for example `session.State[IsAutonomyTurn] = true`.
- `traceId`, `npcId`, session ID shape, and platform name are observability fields only; they must not activate budgeting.
- First-call context budgeting and status-class budgeting must no-op when the marker is absent or false.

### Injectable First-Call Budget Policy

- Add a narrow optional interface, such as `IFirstCallContextBudgetPolicy`.
- `Agent` constructor accepts `IFirstCallContextBudgetPolicy? firstCallContextBudgetPolicy = null`.
- Default behavior is no-op for ordinary `Agent`.
- `NpcRuntimeContextFactory` creates the Stardew autonomy policy and stores it in `NpcRuntimeContextBundle`.
- `NpcAgentFactory` passes the bundle policy into `Agent`.
- `Agent.ChatAsync` invokes the policy only when `iterations == 1 && preparedContext is not null`, immediately before the first `CompleteWithToolsAsync(...)`.

### Budget Rule

- Default Stardew autonomy target is 5,000 characters.
- Protected content must survive even when final payload exceeds 5,000 chars.
- If protected content alone exceeds target, preserve it and log `budget_unmet`.
- First implementation keeps 5,000 as an internal constant. Do not add configuration unless evidence later shows repeated legitimate misses.

### Protected Content

The policy must preserve:

- system / soul / mandatory skills / Stardew supplement;
- current autonomy user message;
- existing recall or memory blocks produced by existing systems;
- active task context;
- latest continuation group.

### Latest Continuation Group

Protection must be structural, not keyword-based:

1. Find the latest assistant message in `preparedContext` where `Role == "assistant"` and `ToolCalls` is non-empty.
2. Collect all `ToolCall.Id` values from that assistant message.
3. Protect that assistant message.
4. Protect all tool messages where `Role == "tool"` and `ToolCallId` is in that ID set.
5. Do not split this protected group to satisfy 5,000 chars.

### Allowed Pruning

The policy may only:

- delete old, unprotected messages;
- truncate old unprotected assistant tool-call arguments;
- dedupe old repeated status/tool result payloads while keeping the latest relevant full result;
- replace unprotected old tool payloads with deterministic placeholders such as `[trimmed old tool result: stardew_status, 1234 chars removed]`.

The policy must not:

- create NPC recap blocks;
- create persona summaries;
- create memory summaries;
- call an LLM for summarization;
- add a parallel recall, memory, persona, or tool lane.

### Status-Class Budget

Upgrade same-turn Stardew status budgeting from per-tool-name to marker-gated status-class behavior.

`broad_status` tools:

- `stardew_status`
- `stardew_player_status`
- `stardew_progress_status`
- `stardew_social_status`
- `stardew_quest_status`
- `stardew_farm_status`
- `stardew_recent_activity`

`continuation_status` tools:

- `stardew_task_status`

Rules:

- If the autonomy marker is absent or false, do not apply the new status-class budget.
- If the marker is true, allow at most one `broad_status` call per turn.
- Return synthetic `status_tool_budget_exceeded` for additional `broad_status` calls in the same turn.
- `stardew_task_status` remains a continuation exception and must not be blocked by prior `broad_status` consumption.

### Skill Assets

Update existing assets only:

- `skills/gaming/stardew-core.md`
- `skills/gaming/stardew-social.md`
- `skills/gaming/stardew-navigation.md`

Required guidance:

- choose one turn purpose before tool calls;
- use short live context and on-demand tools/recall;
- use `session_search` for missing history;
- use `memory` only for durable updates;
- avoid repeated broad status scans;
- use `stardew_task_status` only for long-action continuation checks.

### Observability

Emit structured logs:

- `autonomy_context_budget_started`
  - `sessionId`, `traceId`, `npcId`, `messagesBefore`, `charsBefore`, `toolResultCharsBefore`, `recentTurnCharsBefore`, `budgetChars`
- `autonomy_context_budget_completed`
  - `messagesAfter`, `charsAfter`, `charsSaved`, `budgetMet`, `budgetUnmetReason`, `protectedTailMessages`, `prunedToolResults`, `prunedDuplicateStatusResults`, `truncatedToolCallArgs`, `replacedWithPlaceholders`

Minimum `budget_unmet_reason` values:

- `protected_tail`
- `recall_block`
- `active_task_context`
- `protected_content_over_budget`
- `unknown`

## Implementation Tasks

- [ ] Define the autonomy session marker constant and set it in `NpcAutonomyLoop` decision sessions.
- [ ] Define `IFirstCallContextBudgetPolicy`, no-op policy, budget context/result DTOs, and `BudgetUnmetReason`.
- [ ] Add optional first-call budget policy dependency to `Agent`.
- [ ] Add the policy to `NpcRuntimeContextBundle`, create it in `NpcRuntimeContextFactory`, and pass it through `NpcAgentFactory`.
- [ ] Invoke the policy before first `CompleteWithToolsAsync(...)`, gated by first iteration and prepared context.
- [ ] Implement Stardew autonomy deterministic pruning with structural latest continuation group protection.
- [ ] Add reason-coded structured logging and pruning counters.
- [ ] Upgrade `StardewStatusToolTurnBudget` to marker-gated status-class behavior.
- [ ] Update the three Stardew skill assets.
- [ ] Add the tests described in the test spec.

## Acceptance Criteria

1. Only sessions with the autonomy marker set to true activate first-call context budgeting.
2. Sessions without the marker, including NPC private chat and ordinary NPC/non-NPC chat, preserve existing first-call behavior.
3. `traceId`, `npcId`, session ID, and platform do not activate budgeting.
4. When protected content fits, Stardew autonomy sample first-call payload is `<= 5000` chars.
5. When protected content exceeds 5,000 chars, protected content is preserved and `budget_unmet_reason` is recorded.
6. Latest continuation group is protected by `ToolCallId` structure, not keywords.
7. Pruning uses only delete/truncate/dedupe/deterministic placeholders.
8. No new NPC recap/persona/memory summary lane exists.
9. Broad status budget applies only with autonomy marker true.
10. `stardew_task_status` remains allowed as a continuation exception.
11. Real repo skill asset tests prove the new tool-use guidance exists in `skills/gaming/*.md`.
12. Structured logs expose chars, counters, `budgetMet`, and reason codes.

## Risks

- Over-pruning could hide continuation facts. Mitigation: structural latest continuation group protection.
- Marker gating could be forgotten in a future autonomy path. Mitigation: tests require marker true/false/absent coverage.
- Status-class gating could affect private chat if not marker-gated. Mitigation: explicit private-chat no-op tests.
- 5,000 chars may be too low for protected states. Mitigation: preserve protected content and log reason-coded misses.

## ADR

### Decision

Implement a marker-gated, injectable first-call budget policy for Stardew NPC autonomy. `Agent` calls a narrow optional interface; `NpcAutonomyLoop` marks autonomy sessions; the policy no-ops without the marker.

### Drivers

- The first real payload control point is `Agent.ChatAsync` first tool-capable iteration.
- Generic `ContextManager` should remain generic.
- Existing tool-call metadata supports structural protection.
- The project already has memory, recall, session search, and compaction concepts; this change must reuse them.

### Alternatives Considered

- Put 5,000 chars in `ContextManager` / `TokenBudget`: rejected because it broadens scope and mixes generic token policy with Stardew autonomy workload policy.
- Compress only `NpcAutonomyLoop.BuildDecisionMessage(...)`: rejected because it misses soul/skills/recent turns/tool results.
- Add per-tick LLM summarization: rejected because it increases latency/cost and creates another summary lane.
- Use `traceId` / `npcId` as activators: rejected because they are observability fields and could appear in private chat later.

### Consequences

- `Agent` gains one optional dependency.
- NPC runtime bundle gains one policy dependency.
- `NpcAutonomyLoop` gains a session marker contract.
- Tests must cover marker scoping, structural continuation protection, status-class behavior, and no second lane.

### Follow-Ups

- Review `budget_unmet_reason` distribution after implementation.
- If private chat later needs similar budgeting, write a separate spec.
- If protected content frequently exceeds 5,000 chars, evaluate a config knob based on logs.

## Available Agent Types

- `executor`: implementation owner.
- `test-engineer`: test matrix and fixtures.
- `architect`: boundary review.
- `critic`: plan/diff quality review.
- `verifier`: final evidence and command verification.
- `explore`: focused repo lookup.

## Staffing Guidance

### Ralph Path

Use one `executor` owner with checkpoints after interface/injection, pruning/status budget, and tests/logging.

### Team Path

- Lane 1 `executor`: `Agent`, marker, runtime injection, policy implementation.
- Lane 2 `test-engineer`: budget, marker, status-class, logging, and skill asset tests.
- Lane 3 `executor` or `writer`: skill asset edits.
- Lane 4 `verifier`: run targeted and full tests, summarize evidence.

Suggested reasoning:

- executor: high
- test-engineer: medium
- architect/critic: high
- verifier: medium
- explore: low

## Launch Hints

Ralph:

```powershell
$ralph implement .omx/plans/prd-stardew-npc-autonomy-context-budget.md with .omx/plans/test-spec-stardew-npc-autonomy-context-budget.md
```

Team:

```powershell
$team implement Stardew NPC autonomy context budget per .omx/plans/prd-stardew-npc-autonomy-context-budget.md and .omx/plans/test-spec-stardew-npc-autonomy-context-budget.md
```

## Verification Path

Run targeted tests first:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~ContextBudget"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StatusBudget"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomy"
```

Then run the full desktop test project:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## Change Log

- Applied Architect feedback: policy is injectable and does not hardcode Stardew pruning into generic `Agent`.
- Applied Critic feedback: exact injection path, structural `ToolCallId` continuation protection, status-class table, forbidden summary lane, and reason-coded logs are explicit.
- Applied second Architect feedback: activation requires an autonomy-only session marker; `traceId`/`npcId` are observability only; private chat remains out of scope.
