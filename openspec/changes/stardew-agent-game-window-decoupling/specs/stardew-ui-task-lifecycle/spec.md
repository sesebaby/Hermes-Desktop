## ADDED Requirements

### Requirement: UI lifecycle waits shall be task facts, not generic ingress failures
Stardew UI/window lifecycle waits SHALL be represented as owned task, interaction, lease, or status facts. They MUST NOT be classified as generic ingress retry failures when the wait is recoverable and belongs to the game or player-visible UI lifecycle.

#### Scenario: Private-chat reply lifecycle does not consume stale budget
- **WHEN** a private-chat reply dialogue, phone reply, or equivalent reply UI lifecycle is still open or waiting for player interaction
- **THEN** a related world-action host task is not terminally blocked through the generic stale/busy defer budget solely because the reply UI has not closed

#### Scenario: Menu wait is visible status
- **WHEN** a UI-backed task is waiting for an owned menu, unrelated menu, or player-visible window condition
- **THEN** the wait is exposed as a readable task/status fact such as waiting, blocked, or lease conflict rather than a hidden host lock

### Requirement: UI safety shall remain game-side while agent flow continues
Decoupling agent flow from UI lifecycle SHALL NOT remove game-side UI safety. A task that opens, closes, or manipulates a menu MUST still respect UI leases, active menu ownership, player-free state, cutscenes, festivals, day transitions, and cleanup ownership. Violations MUST return observable blocked/failed facts while leaving the next decision to the agent.

#### Scenario: Unrelated active menu blocks window operation
- **WHEN** a future trade, craft, gather, quest, or private-chat task requires a menu but an unrelated active menu is present
- **THEN** the task returns a blocked fact with a short `summary` and a reason such as `menu_blocked`, `ui_lease_busy`, or `private_chat_active`, and does not close or overwrite the unrelated menu

#### Scenario: Owned UI wait remains recoverable until timeout
- **WHEN** a task owns a UI lease and is waiting on a recoverable UI condition
- **THEN** the task may remain running or waiting until the condition changes or the task timeout/watchdog produces a terminal fact

#### Scenario: Timeout releases owned UI resources
- **WHEN** a UI-backed task times out while waiting
- **THEN** the host records a terminal timeout fact with `action_slot_timeout` or an equivalent existing timeout reason and releases only the UI lease and resources owned by that task

### Requirement: Private-chat reply delivery shall be separated from world-action execution
Private-chat reply display and close events SHALL be treated as interaction/window facts. They MUST NOT be used as a generic reason to terminally block a world-action task that was explicitly submitted by the agent through the host task lifecycle.

#### Scenario: Reply is displayed while world action remains pending
- **WHEN** the agent replies naturally and submits a world action from the same private-chat turn
- **THEN** the reply display/close lifecycle is tracked as UI or interaction state while the world action remains represented by its own host task status

#### Scenario: No tool call still creates no world action
- **WHEN** the private-chat response contains no world-action tool call
- **THEN** no world action is created from reply text even if the reply mentions an intention
