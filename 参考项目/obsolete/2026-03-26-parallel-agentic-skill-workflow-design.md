# Parallel-Aware Superpowers Skill Workflow Design

> Historical design note:
>
> This obsolete workflow draft is kept for process lineage only.
> It should not be used to infer current superpowers runtime ownership, AFW placement, or hosted-vs-local narrative architecture.

## Context

The current superpowers workflow is inconsistent about parallel agent execution.

- `writing-plans` produces plans for agentic workers, but does not require an explicit parallelization assessment.
- `subagent-driven-development` assumes one fresh implementer subagent per task, but currently forbids parallel implementation dispatch entirely.
- `executing-plans` is positioned as a fallback executor, but does not clearly explain how to consume plans that include parallel-safe work.

This creates a planning and execution mismatch. A plan can contain independent tasks, but the downstream execution skills do not have a structured way to recognize and safely use that independence.

## Goal

Make the plan-writing and plan-execution workflow parallel-aware by default, while still failing closed when tasks are coupled, share write scope, or require sequential integration.

## Non-Goals

- Do not turn every plan into a parallel plan.
- Do not require parallel execution on platforms without subagent support.
- Do not permit parallel execution when tasks touch the same files, require shared mutable state, depend on unfinished upstream tasks, or cannot be isolated into separate worktrees or equivalent forked workspaces.
- Do not weaken TDD, review gates, or final verification.

## Design

### 1. `writing-plans` must assess parallelism explicitly

`writing-plans` should always perform a parallelization check before task decomposition is finalized.

The skill must require the plan writer to classify work into one of two modes:

- `Sequential only`
- `Parallel waves allowed`

The default is not "parallel everywhere." The default is "evaluate parallelism explicitly and document the result."

Parallel execution is allowed only when all of the following are true:

- Tasks have no unresolved dependency edge between them.
- Tasks do not share a write set.
- Tasks can be verified independently before integration.
- Integration risk is low enough to be bounded by a merge checkpoint plus wave-level verification.

If any of those conditions fail, the plan must mark the affected tasks or the full plan as sequential.

### 2. Plans must carry machine-usable parallel metadata

The plan header should include:

- `Execution Mode Recommendation`
- `Parallelization`
- `Parallel Waves`

Each task should include:

- `Wave`
- `Dependencies`
- `Parallel-safe`
- `Agent ownership`
- `Merge checkpoint`

`Execution Mode Recommendation` is advisory. Actual routing and safety checks depend on `Parallelization`, per-task `Wave`, exact write ownership, workspace isolation, and current repo state.

This keeps plan execution deterministic. Downstream skills should not infer concurrency from prose alone.

### 3. `subagent-driven-development` should support wave-based parallel execution

`subagent-driven-development` should remain the preferred execution mode when subagents are available, but it should no longer ban all parallel implementation dispatch.

Instead, it should allow concurrent implementer subagents only within a declared parallel wave and only when task ownership is disjoint.

Safety rules:

- One implementer per task.
- No overlapping write ownership or shared mutable resources inside a wave.
- Each parallel task runs in an isolated worktree or equivalent forked workspace, not a shared Git index.
- Each isolated workspace starts clean and from the expected base revision before dispatch.
- Each task still requires spec review, then code-quality review.
- A wave completes only after all task-level reviews pass and an integration verification step succeeds.
- If a wave shows unexpected overlap or merge conflict risk, execution falls back to sequential handling.
- If integration repair changes the merged result, rerun spec review and code-quality review on the repaired merged state before continuing.

This preserves the quality gates while allowing real concurrency where the plan already proved it is safe.

### 4. `executing-plans` should become an explicit fallback

`executing-plans` should stay simple and conservative.

When it reads a plan that declares parallel waves:

- If subagents are available, it should tell the user to use `subagent-driven-development` instead of pretending to execute in parallel itself.
- If subagents are not available, it should execute wave-by-wave in a sequential manner, preserving dependencies and merge checkpoints.

This keeps the fallback executor honest. It can understand parallel-aware plans without claiming capabilities it does not have.

## Skill-Specific Changes

### `writing-plans`

- Add a `Parallelization Check` section.
- Update the required plan header.
- Update the task template to include wave, dependency, and ownership fields.
- Update execution handoff text to prefer subagent-driven execution for plans that declare parallel waves.

### `subagent-driven-development`

- Update the "when to use" guidance to recognize plans with declared parallel waves.
- Replace the blanket ban on parallel implementers with a ban on overlapping write scopes.
- Add a wave-based process section.
- Require wave-level integration verification after task-level reviews.
- Cross-reference `dispatching-parallel-agents` for bounded parallel dispatch technique.

### `executing-plans`

- Add a plan review step that reads execution metadata and validates current repo state against declared ownership.
- State clearly that it is a fallback executor, not a real parallel dispatcher.
- Describe sequential handling of declared waves when subagents are unavailable.
- Redirect to `subagent-driven-development` when subagents are available and the plan requests parallel execution.

## Acceptance Criteria

- `writing-plans` no longer produces agentic plans without an explicit parallelization decision.
- `subagent-driven-development` can safely consume plans with independent parallel waves.
- `subagent-driven-development` still fails closed on shared write scope or unclear dependencies.
- Parallel tasks never write into the same active worktree or Git index concurrently.
- Integrated wave repairs cannot bypass the spec-review then code-quality-review order.
- `executing-plans` can read the new plan format without ambiguity.
- The three skill files no longer contradict each other about whether parallel implementation is allowed.

## Risks and Mitigations

### Risk: false-positive parallelization

Mitigation:

- Require explicit wave, dependency, and exact ownership fields.
- Require wave-level integration verification.
- Require sequential fallback when ownership is unclear.
- Require current worktree validation before execution starts.

### Risk: review order regression

Mitigation:

- Keep the existing review order per task: spec compliance first, code quality second.
- Add integration verification after all tasks in the wave are approved.
- If the merged wave changes during repair, rerun the review order on the repaired merged state.

### Risk: overcomplicating small plans

Mitigation:

- Allow `Sequential only` as the simplest valid outcome.
- Keep parallel metadata lightweight and standardized.

## Implementation Notes

- This is a coordinated documentation change across three skills, not a code feature.
- The update must preserve existing TDD and review discipline.
- The resulting workflow should treat parallelism as opt-in by proof, not opt-in by optimism.
