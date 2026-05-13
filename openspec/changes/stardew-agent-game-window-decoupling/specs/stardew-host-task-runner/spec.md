## ADDED Requirements

### Requirement: Host tasks shall not block agent flow on recoverable game waits
The Stardew host task runner SHALL represent recoverable game UI, window, animation, menu, and event waits as task/status facts. It MUST NOT suspend the agent turn, terminally block a valid task, or consume a generic stale/busy ingress defer budget solely because the game is waiting on a recoverable lifecycle condition.

#### Scenario: Private-chat reply wait remains recoverable
- **WHEN** a private-chat `stardew_host_task_submission` has a matching `conversationId` and the private-chat reply UI lifecycle has not finished yet
- **THEN** the work remains recoverable as queued/running/waiting task state and is not terminally blocked by `MaxDeferredIngressAttempts`

#### Scenario: Generic busy defer remains bounded
- **WHEN** a host task submission is repeatedly deferred because the NPC action slot or pending work item is still busy
- **THEN** the generic stale/busy defer budget still applies and eventually produces an observable blocked fact with reason `host_task_submission_deferred_exceeded`

#### Scenario: Agent can continue while game waits
- **WHEN** a host task is waiting on a recoverable game condition such as a menu, dialogue, animation, or event transition
- **THEN** the next agent turn may still speak, update todo, or call status tools without the host inferring the next action

### Requirement: Host task results shall include agent-readable summaries
Stardew host task action and status results exposed to the agent SHALL include a concise text `summary` that explains the current state and what kind of decision remains with the agent. Machine-readable fields such as `status`, `commandId`, `reason`, `errorCode`, and correlation identifiers MUST remain available for testing, UI, logging, and follow-up status queries.

#### Scenario: Queued action returns summary
- **WHEN** an agent-visible Stardew action tool accepts or queues a host task
- **THEN** the tool result includes a short `summary` describing that the action has been queued or started and includes the relevant `commandId` or work identity when available

#### Scenario: Status query returns summary
- **WHEN** the agent calls `stardew_task_status` for a known command
- **THEN** the result includes a short `summary` describing whether the task is running, waiting, completed, blocked, failed, cancelled, or timed out

#### Scenario: Blocked action explains ownership
- **WHEN** a host task returns `blocked`
- **THEN** the `summary` explains the blocking reason without telling the agent which next action to choose

#### Scenario: Recoverable wait explains status
- **WHEN** a task is waiting on a recoverable UI/window/animation condition
- **THEN** the result includes a non-terminal status and a `summary` explaining the wait without using `host_task_submission_deferred_exceeded`

### Requirement: Conflicting world actions shall return facts instead of hidden queues
For a single NPC, the host task runner SHALL preserve one running world-action slot. When a new world action conflicts with an in-flight world action, the new request MUST return an observable blocked fact such as `action_slot_busy` instead of stealing the slot, replacing the current action, or entering an unbounded hidden queue.

#### Scenario: Running move blocks conflicting action
- **WHEN** an NPC already has a running move task and the agent submits another conflicting world action
- **THEN** the new action returns a `blocked/action_slot_busy` fact with an agent-readable `summary` and does not create another queued world-action work item

#### Scenario: Agent owns retry decision
- **WHEN** a conflicting world action is blocked because the action slot is busy
- **THEN** the host does not automatically retry the blocked action after the current task completes
