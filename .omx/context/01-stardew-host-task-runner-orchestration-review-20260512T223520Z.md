# 01-stardew-host-task-runner-orchestration review context

## Task statement

Use `$ralplan` requirements to re-review `openspec/changes/01-stardew-host-task-runner-orchestration`, then rename the proposal/change by adding the numeric prefix `01-`.

## Desired outcome

- Produce a grounded Planner -> Architect -> Critic style review of the OpenSpec change.
- Verify the proposal/design/tasks/spec deltas are coherent, testable, and aligned with the project constraints around Stardew host task runner orchestration.
- Rename the change directory to `openspec/changes/01-stardew-host-task-runner-orchestration`.
- Confirm no stale references remain to the old change id.

## Known facts / evidence

- The renamed change currently contains `proposal.md`, `design.md`, `tasks.md`, `.openspec.yaml`, and three spec deltas:
  - `specs/stardew-host-task-runner/spec.md`
  - `specs/stardew-ui-task-lifecycle/spec.md`
  - `specs/stardew-orchestration-harness/spec.md`
- The proposal explicitly breaks with small-model/local-executor gameplay execution and defines a single host task runner path.
- The design uses existing `PendingWorkItem`, `ActionSlot`, `IngressWorkItems`, `LastTerminalCommandStatus`, runtime jsonl, and bridge task/status semantics as the migration base.
- The tasks include discovery/retirement, host task contract, entry convergence, UI/window lifecycle, harness/tests, and documentation/verification.
- `rg` found no existing repository references to the old change id outside OpenSpec/context paths before rename.

## Constraints

- Follow `$ralplan` non-interactive consensus review shape: RALPLAN-DR summary, Architect review, then Critic review sequentially.
- Do not implement source-code behavior as part of this planning/review task.
- Preserve existing user changes; `openspec/changes/` is currently untracked.
- No new dependencies.
- Project rule: Stardew v1 must not preserve small-model gameplay execution, hidden fallback, shadow paths, or dual lanes.

## Unknowns / open questions

- Whether OpenSpec tooling accepts change ids with numeric prefixes; validate after rename.
- Whether implementation can acquire stable `toolCallId` from the C# tool pipeline; design already allows fallback correlation fields.
- Exact implementation shape for UI lease reuse vs a new generic lease snapshot remains intentionally deferred.

## Likely touchpoints

- OpenSpec artifacts under `openspec/changes/01-stardew-host-task-runner-orchestration`.
- Validation command: `openspec validate <change-id> --strict` if available.
- Rename target: `openspec/changes/01-stardew-host-task-runner-orchestration`.
