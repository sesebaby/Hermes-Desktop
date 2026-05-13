## ADDED Requirements

### Requirement: Harness shall prove the agent/game non-blocking boundary
The Stardew orchestration harness SHALL prove that recoverable game UI/window/animation/event waits do not block the agent flow or terminally fail host task ingress through unrelated retry budgets. It MUST also prove that real busy/stale conditions remain bounded.

#### Scenario: Private-chat UI wait exceeds generic budget without terminal block
- **WHEN** a private-chat host task waits longer than the generic stale/busy defer attempt budget for reply UI lifecycle to finish
- **THEN** the task remains recoverable and is not removed or terminally blocked by `host_task_submission_deferred_exceeded`

#### Scenario: Generic busy ingress still blocks
- **WHEN** a host task submission is deferred repeatedly because an action slot or pending work item remains busy
- **THEN** the existing generic defer budget still produces a blocked fact with `host_task_submission_deferred_exceeded` after the configured threshold

#### Scenario: Conflicting action does not create hidden queued work
- **WHEN** a new world action is submitted while the same NPC already has a running world action slot
- **THEN** the harness observes `blocked/action_slot_busy`, no additional queued world-action work item, and no automatic retry after the running action completes

#### Scenario: Agent turn is not replaced by host decision
- **WHEN** a task reports running, waiting, blocked, failed, or completed status
- **THEN** the harness verifies the host records facts and does not auto-close todo, infer movement, or choose the next agent action

### Requirement: Harness shall verify text-first Stardew tool results
The harness SHALL verify that agent-visible Stardew action and continuation-status results include short readable summaries while preserving machine-readable fields for correlation and tests.

#### Scenario: Task status result includes summary and command identity
- **WHEN** a fake command service returns a running, waiting, completed, blocked, failed, cancelled, or timed-out task status
- **THEN** `stardew_task_status` returns an agent-readable `summary` and preserves `status` and `commandId`

#### Scenario: Action tool result includes summary and status
- **WHEN** a Stardew action tool returns queued, running, completed, or blocked status
- **THEN** the result includes an agent-readable `summary` and preserves machine-readable status fields

### Requirement: Harness shall verify prompt and skill non-blocking guidance
Prompt and skill boundary tests SHALL verify that Stardew runtime guidance tells the agent to treat windows, animation, menus, and events as status facts rather than hidden locks. The guidance MUST direct continuation through `stardew_task_status` and must not instruct the host or agent to rely on a small-model executor or host-inferred next action.

#### Scenario: Skills teach continuation status
- **WHEN** the harness scans Stardew core and task-continuity skill assets
- **THEN** the assets instruct the agent to use `stardew_task_status` for known long or waiting work

#### Scenario: Runtime prompt keeps host non-prescriptive
- **WHEN** the harness scans Stardew NPC runtime prompt assets
- **THEN** the assets state that the host provides facts/status and the agent owns the next decision
