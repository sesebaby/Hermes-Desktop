# PRD: Stardew Autonomy System Context Budget Reduction

Date: 2026-05-06
Status: Approved for planning handoff by `$ralplan`
Context: `.omx/context/stardew-autonomy-system-context-budget-20260506T060825Z.md`

## Goal

Reduce Stardew autonomy first-call system context so the default 5K budget is realistic, while preserving the always-on NPC behavior contracts needed for movement, speaking, task continuity, and safe tool usage.

## Problem Statement

The previous active-task misclassification bug is already fixed. Current runtime evidence now shows the remaining blocker is `core_system_over_budget`:

- `systemChars=24025` for Haley
- `systemChars=22284` and `23520` for Penny
- `activeTaskChars=124-171`
- `currentUserChars=1901-2587`

This proves the dominant problem is the system layer itself, not active-task protection, not builtin memory, and not dynamic recall.

## Root Cause

Two prompt-assembly choices are overloading autonomy first calls:

1. `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
   - injects persona facts, voice, boundaries, and the full text of every required Stardew skill into `SystemPromptSupplement`
   - with the latest skill rewrites, the supplement is still about `6841-6844` chars per NPC

2. `src/skills/SkillManager.cs`
   - `BuildSkillsMandatoryPrompt()` still emits a repo-wide mandatory skills index
   - current rough size is about `5266` chars

These two layers alone are already incompatible with a 5K default target before adding base prompt, runtime guidance, soul context, or session state.

## Current Technical Facts

### Runtime evidence

- `budgetMet=False`
- `budgetUnmetReason=core_system_over_budget`
- `builtinMemoryChars≈2590-2604`
- `dynamicRecallCharsAfter=1000`

Builtin memory is not the main offender. It is within the expected scale.

### Current required Stardew skill payload

Current required skill files:

- `stardew-core` ≈ `618`
- `stardew-social` ≈ `1542`
- `stardew-navigation` ≈ `1913`
- `stardew-task-continuity` ≈ `912`
- `stardew-world` ≈ `1463`

Current persona blocks are small:

- facts ≈ `142-154`
- voice ≈ `53-62`
- boundaries ≈ `69`

The current skill content is already more compact than before and is now better source material for a short autonomy digest, but it is still too large to inject in full.

## Reference Alignment

The target architecture remains aligned with the reference project's tool-first, on-demand retrieval approach:

- do not widen the context budget as a workaround
- do not add a new recap/persona-summary lane
- keep long-lived knowledge in existing memory/session search systems
- keep first-call system context small and stable
- preserve only the active-task/tail-style protections already proven in the reference pattern

Reference-project behavior observed in `external/hermes-agent-main`:

- `tools/todo_tool.py`
  - only preserves active `pending` / `in_progress` tasks after compression
  - completed and cancelled work is intentionally not re-injected
- `agent/context_compressor.py`
  - protects head and tail
  - forces the latest user message to remain in tail
  - prunes old tool outputs before summarization
  - compacts middle turns instead of solving the problem by preloading more static prompt text

This is the behavior to align with: keep the persistent system layer small, keep only minimal active-task continuity, and retrieve depth on demand through tools.

## Decision

Adopt a single prompt-assembly framework with mode-aware block admission rules.

This framework will:

1. preserve a minimal always-on autonomy contract
2. stop injecting full required skill text into autonomy first-call system context
3. stop injecting the repo-wide `Skills (mandatory)` block into autonomy first calls
4. use the same skill/persona/memory sources as the rest of the product
5. rely on existing `session_search`, memory, and skill-loading mechanisms for on-demand depth
6. avoid any second prompt stack, shadow prompt path, or duplicated summary lane

## Principles

- Preserve execution correctness over prose richness.
- Keep autonomy first-call context small, explicit, and stable.
- Put on-demand knowledge behind existing tools instead of full-text preload.
- Do not duplicate memory, summary, or recap systems.
- Maintain one prompt governance model and one source of truth.
- Use runtime-mode block admission, not dual-track prompt architecture.

## Decision Drivers

1. Runtime `systemChars` is the dominant source of budget failure.
2. Required-skill full-text injection is structurally incompatible with a 5K target.
3. Repo-wide mandatory skills indexing is unnecessary noise for autonomy runtime.
4. User explicitly wants tool-first context acquisition, not giant same-turn context.

## Alternatives Considered

### Option A: Raise the budget

Rejected.

- Low implementation cost
- But it hides the design problem
- Conflicts with the explicit 5K target
- Moves farther away from reference-aligned tool-first behavior

### Option B: Keep full supplement, add another summary lane

Rejected.

- Could reduce first-call size
- But duplicates existing memory/compression systems
- Violates the user's constraint against extra recap/persona-summary lanes

### Option C: Single framework with compact autonomy block admission

Chosen.

- Directly attacks the actual blocker
- Keeps the behavior contract explicit
- Preserves existing on-demand systems
- Keeps blast radius bounded to first-call autonomy block admission
- Avoids creating a second long-term prompt architecture

## Required Behavior After Change

### Always-on autonomy contract must still cover

- one-turn purpose selection
- observation-first behavior
- tool-only world actions
- `stardew_move` required for physical movement
- `stardew_task_status` required for long-action polling
- task continuity via `todo`
- `session_search` for missing history
- visible player feedback via `stardew_speak` when appropriate
- blocked/failed task updates with short reasons
- no repeated broad status scanning

### Content that should move out of always-on system context

- full required skill prose
- expanded explanatory text
- example-heavy world interpretation text
- repo-wide skill catalog / mandatory index

## Scope

In scope:

- autonomy runtime first-call block admission within the shared prompt assembly framework
- Stardew autonomy supplement assembly
- mode-aware `Skills (mandatory)` admission behavior
- tests and diagnostics needed to prove system-layer shrinkage

Out of scope:

- redesigning the rest of prompt assembly outside the shared framework changes needed here
- new memory/summary infrastructure
- changing gameplay tools themselves
- changing persona assets unless required to keep a minimal digest

## Implementation Plan

1. Add instrumentation that logs autonomy supplement sub-block sizes:
   - persona facts
   - voice
   - boundaries
   - each required skill
   - total supplement chars

2. Replace full-text required skill injection with a compact autonomy digest:
   - preserve minimal behavior contracts only
   - keep persona facts/voice/boundaries in short form
   - preserve `stardew-world` as on-demand depth via `skill_view`, not full-time preload

3. Change shared prompt assembly admission rules for autonomy first calls so repo-wide `Skills (mandatory)` is not admitted there by default:
   - preferred: no global mandatory index for autonomy first call
   - acceptable fallback: a Stardew-only micro-index if needed, still inside the same framework

4. Keep all modes on the same assembly framework and source assets; only block admission differs by runtime mode.

5. Verify runtime logs:
   - `systemChars` drops dramatically
   - `budgetMet` becomes realistically achievable when protected content fits
   - behavior contracts remain intact

## Risks

- Over-trimming may remove a first-call rule that autonomy genuinely needs.
- A replacement micro-index may still be too descriptive and creep upward again.
- If mode-aware block rules are poorly defined, they can silently drift into real dual-track behavior.

## Mitigations

- Keep the compact digest derived directly from the latest Stardew skill content rather than inventing a new policy source.
- Add regression tests for behavior-critical rules, not for full prompt text.
- Add supplement-size diagnostics so future prompt growth is visible immediately.
- Test the admission rule boundary directly so "mode-aware" does not turn into shadow architecture.

## Acceptance Criteria

1. Autonomy runtime no longer injects full required skill text into the first-call system layer.
2. Autonomy first calls no longer inject the repo-wide global skills-mandatory index.
3. Runtime logs show a substantial reduction in `systemChars` from the current `22K-24K` range.
4. The compact autonomy contract still preserves:
   - movement contract
   - task continuity contract
   - visible feedback contract
   - on-demand history escalation contract
5. No new recap/persona-summary lane is added.
6. No second prompt architecture or shadow autonomy stack is introduced.

## ADR

### Decision

Keep one prompt assembly framework, but tighten first-call autonomy block admission so oversized static skill payloads are no longer preloaded.

### Drivers

- The real blocker is oversized core system context.
- The reference direction is on-demand retrieval and tool-first behavior.
- The user requires a 5K-first-context posture and no duplicated summary systems.
- The user explicitly rejects dual-track prompt architecture.

### Why Chosen

It directly removes the largest static prompt weights while preserving the minimal behavior contract required for autonomy correctness, without introducing a second prompt system.

### Consequences

- First-call autonomy prompt becomes smaller and more stable.
- Some background knowledge shifts from preload to on-demand retrieval.
- The assembly framework stays single-source; only block admission differs by runtime mode.

### Follow-ups

- Re-check runtime logs for `systemChars`, `budgetMet`, and `budgetUnmetReason`.
- If the compact path is still too large, shrink the Stardew micro-index before touching builtin memory.
- If any future change requires different source assets per mode, stop and re-review because that would violate the no-dual-track constraint.
- Add a guardrail test for supplement growth so the problem does not silently return.

## Available Agent Types Roster

- `planner`: plan refinement and sequencing
- `architect`: prompt-boundary and contract review
- `critic`: plan quality gate and regression skepticism
- `executor`: implementation
- `test-engineer`: regression test design and verification
- `verifier`: completion evidence and log-based validation

## Execution Handoff Guidance

### `ralph` staffing

- Primary lane: `executor`
- Verification lane: `test-engineer`
- Final sign-off: `verifier`

Reasoning guidance:

- implementation: `high`
- tests/verification: `medium` to `high`

### `team` staffing

- Lane 1: prompt assembly / supplement compaction
- Lane 2: mode-aware skills-mandatory admission reduction within shared assembly
- Lane 3: tests + runtime diagnostics

Suggested agent allocation:

- 1 `executor` for supplement builder + prompt path
- 1 `executor` or `build-fixer` for surrounding integration if needed
- 1 `test-engineer` for regression coverage
- 1 `verifier` for runtime/log confirmation

### Launch Hints

- Sequential delivery: `$ralph 执行计划`
- Parallel delivery: `$team 执行计划`

### Team Verification Path

1. targeted unit tests
2. full desktop test suite for touched area
3. live runtime log validation against known fields:
   - `systemChars`
   - `builtinMemoryChars`
   - `activeTaskChars`
   - `currentUserChars`
   - `budgetMet`
   - `budgetUnmetReason`
