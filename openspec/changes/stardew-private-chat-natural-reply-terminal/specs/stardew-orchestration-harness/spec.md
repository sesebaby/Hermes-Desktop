## MODIFIED Requirements

### Requirement: Harness shall gate removal of small-model gameplay execution
The harness SHALL include negative tests proving Stardew v1 gameplay does not call `NpcLocalExecutorRunner`, `local_executor`, small-model action prompts, hidden text parsing, fallback speech/move execution, or a second private-chat LLM turn that exists only to infer whether natural language meant action. Private-chat natural replies without successful world-action tool calls MUST be verified as player-visible dialogue, not as world-action input.

#### Scenario: Natural language movement is not executed
- **WHEN** the fake agent replies "I am going to the beach" without a successful movement or host-task tool call
- **THEN** no bridge command is submitted and the runtime records only dialogue/diagnostic state appropriate to that entry point

#### Scenario: Private chat natural reply stays single turn
- **WHEN** the private-chat fake agent returns a normal non-empty reply without `stardew_submit_host_task` and without `npc_no_world_action`
- **THEN** the runner returns that reply after one parent agent call, submits no host task, and does not run delegation self-check

#### Scenario: Explicit no-world action stays supported
- **WHEN** the private-chat fake agent successfully calls `npc_no_world_action` and then returns a natural reply
- **THEN** the runner records the successful no-world closure, returns the reply, and does not submit a host task

#### Scenario: Successful host task empty reply still self-checks
- **WHEN** the private-chat fake agent successfully calls `stardew_submit_host_task` but returns an empty final reply
- **THEN** the runner performs at most one bounded reply self-check, does not repeat host-task submission, and blocks queued ingress if no player-visible reply is produced

#### Scenario: Deprecated executor is not registered
- **WHEN** Stardew NPC autonomy and private chat runtime are constructed
- **THEN** no gameplay path has an active `INpcLocalExecutorRunner` dependency or equivalent model-in-the-middle executor
