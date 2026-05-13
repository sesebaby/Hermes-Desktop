## ADDED Requirements

### Requirement: Stardew gameplay shall use one host task execution path
Stardew v1 gameplay actions SHALL be executed only through model-visible tools that create host task/work item records. The host and bridge SHALL mechanically execute those tasks and return facts to the main agent. The system MUST NOT route movement, speech delivery, idle micro actions, private-chat immediate actions, UI operations, or `todo` closure through a small-model executor, hidden executor, or second tool lane.

#### Scenario: Tool call creates a host task
- **WHEN** the main NPC agent calls a Stardew movement, speech, idle micro action, private chat, or window action tool
- **THEN** the runtime creates or updates a host task/work item with stable identity and the host/bridge executes the mechanical action

#### Scenario: No tool call does not execute gameplay
- **WHEN** the main NPC agent returns only natural language or JSON-like text without an assistant tool call
- **THEN** the host records a diagnostic fact and MUST NOT infer or execute any Stardew gameplay action from that text

#### Scenario: Small-model execution lane is not reachable
- **WHEN** a Stardew v1 gameplay action is submitted through autonomy, private chat, scheduled ingress, native tool, or MCP tool
- **THEN** no `local_executor`, small-model executor, model-in-the-middle runner, or hidden fallback is invoked

### Requirement: Host tasks shall carry stable correlation identity
Each host task SHALL preserve correlation fields sufficient to connect the model-visible tool call, runtime state, bridge command, terminal fact, and runtime log. The minimum identity fields are `traceId`, `workItemId` or `taskId`, `commandId` when available, `idempotencyKey`, `npcId`, `gameId`, `saveId`, `sessionId`, `source`, and `action`. If the agent/tool pipeline exposes `toolCallId`, the system MUST store it with the task.

#### Scenario: Repeated same-name tool calls are distinguishable
- **WHEN** the same NPC submits two `stardew_navigate_to_tile` calls in separate turns or retries
- **THEN** each call has distinct correlation identity and the terminal result for one call is not attributed to the other

#### Scenario: Bridge command id arrives after submission
- **WHEN** the runtime creates a work item before the bridge returns a `commandId`
- **THEN** the runtime updates the existing work item/action slot with the returned `commandId` instead of creating a second task

### Requirement: Host tasks shall follow a terminal lifecycle
Host tasks SHALL use a bounded lifecycle with non-terminal states such as `queued`, `submitting`, and `running`, and terminal states such as `completed`, `blocked`, `failed`, `cancelled`, `timeout`, and `stuck`. Once a task reaches a terminal state, the runtime MUST clear the action slot/resource claims that belong to that task and preserve a terminal fact for the next agent turn.

#### Scenario: Completed task records terminal fact
- **WHEN** the bridge reports a task as `completed`
- **THEN** the runtime clears the in-flight slot and records a terminal fact containing the action, status, command id, and relevant final state

#### Scenario: Blocked task remains visible to agent
- **WHEN** the bridge or host reports `blocked` with a reason code
- **THEN** the runtime clears the in-flight slot, records the blocked reason, and wakes the main agent with that fact

#### Scenario: Running task is not overwritten
- **WHEN** a host task is still `queued`, `submitting`, or `running`
- **THEN** another gameplay action for the same NPC/resource slot is rejected or deferred with an observable conflict reason

### Requirement: Host task facts shall return to the main agent without making decisions
After a host task reaches a terminal state, the runtime SHALL expose the result to the main agent as a fact such as `last_action_result`, `interaction_session`, `action_slot_timeout`, `task_stuck`, or equivalent structured content. The fact MUST describe what happened and MUST NOT instruct the agent to choose a specific next action.

#### Scenario: Movement completes
- **WHEN** a move task reaches `completed`
- **THEN** the next autonomy wake includes a factual result with command id, action `move`, and status `completed`

#### Scenario: Private chat UI closes
- **WHEN** a private chat UI lifecycle task reaches a terminal state
- **THEN** the next wake describes it as an interaction/window fact, not as a completed world movement or social decision

#### Scenario: Agent owns todo closure
- **WHEN** a terminal host task is related to an active `todo`
- **THEN** the runtime presents both the terminal fact and active todo to the main agent, and MUST NOT automatically mark the todo completed, blocked, or failed

### Requirement: Host task submission shall be idempotent across replay and restart
The runtime SHALL persist enough host task state to recover after process restart or staged batch replay. If a command was already submitted, replay MUST NOT submit a duplicate bridge command for the same idempotency key. The runtime SHALL query or reconstruct status and then return a terminal or in-flight fact.

#### Scenario: Restart with submitted command
- **WHEN** the process restarts after a bridge command was submitted but before terminal status was recorded
- **THEN** the runtime uses the persisted command id or idempotency key to recover status instead of submitting the action again

#### Scenario: Duplicate idempotency key
- **WHEN** the same source retries with an idempotency key already associated with an in-flight or terminal task
- **THEN** the runtime returns the existing task/status rather than creating a new command

### Requirement: Watchdog shall terminate stuck or expired host tasks
The host task runner SHALL detect tasks that exceed their timeout, lose progress, or are reported stuck by the bridge. Such tasks MUST become terminal facts with reason codes and MUST release their action slot, UI lease, and resource claims.

#### Scenario: Action slot timeout
- **WHEN** an action slot passes `timeoutAtUtc` before completion
- **THEN** the runtime records a terminal timeout fact and releases the slot

#### Scenario: Bridge reports stuck
- **WHEN** the bridge reports a movement task as stuck
- **THEN** the runtime records a `stuck` terminal fact with the bridge reason and wakes the main agent

### Requirement: Entry points shall converge on the same task lifecycle
Autonomy, private chat, scheduled ingress, native Stardew tools, MCP Stardew tools, and debug/manual submissions SHALL adapt parameters into the same host task lifecycle. They MUST NOT maintain separate completion, failure, timeout, or terminal fact semantics for the same action type.

#### Scenario: MCP and native move have equivalent lifecycle
- **WHEN** the same movement target is submitted through native tool and MCP wrapper in separate tests
- **THEN** both paths create equivalent host task state and terminal facts

#### Scenario: Private chat action uses host task
- **WHEN** the main agent accepts an immediate private-chat action request
- **THEN** the resulting world action is represented as a host task with source `private_chat`, not as a small-model delegation

### Requirement: Resource ownership shall be explicit and cleaned up
Host tasks SHALL declare and manage owned resources such as NPC action slot, movement claim, UI lease, target resource claim, or bridge command. Terminal, cancelled, blocked, timeout, and stuck outcomes MUST release resources owned by the task.

#### Scenario: Move claim is released
- **WHEN** a movement task reaches any terminal state
- **THEN** the movement claim associated with the work item or command id is released

#### Scenario: Conflict is observable
- **WHEN** a second task requests a resource already owned by an in-flight task
- **THEN** the second request returns `blocked` or deferred status with a conflict reason rather than stealing the resource
