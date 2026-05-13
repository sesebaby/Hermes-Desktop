## Purpose

Define UI/window-backed Stardew host task lifecycle requirements for leases, active menu safety, bounded mechanical steps, observable validation, safe cleanup, private chat boundaries, and future window action extension.

## Requirements

### Requirement: Window tasks shall acquire a UI lease before operating
Every Stardew task that opens, closes, or manipulates a game UI/window SHALL acquire an explicit UI lease before performing mechanical steps. This applies to private chat, crafting, trading, quest, gathering, and any future menu-backed action. The system MUST NOT let a small model or ad hoc handler operate UI outside the host task lifecycle.

#### Scenario: Private chat opens with lease
- **WHEN** the agent calls the private chat open tool
- **THEN** the host task acquires a UI lease, submits the open request, and records the lease owner in runtime state

#### Scenario: Crafting opens with lease
- **WHEN** a future crafting task needs to open the crafting menu
- **THEN** it first obtains a UI lease and records the lease with the host task identity

### Requirement: Active menu conflicts shall block instead of overwrite
Before a window task opens or manipulates UI, the host SHALL check whether another active menu or UI lease is present. If the current active menu is not owned by the task, the task MUST return `blocked` with a reason such as `menu_blocked`, `ui_lease_busy`, or `private_chat_active`.

#### Scenario: Existing private chat blocks crafting
- **WHEN** a crafting task starts while a private chat lease is active
- **THEN** the crafting task returns blocked and does not close or overwrite the private chat menu

#### Scenario: Unexpected active menu blocks operation
- **WHEN** a trade task expects the shop menu but the active menu is a different menu
- **THEN** the task stops with a blocked/failed fact and releases only resources it owns

### Requirement: Window tasks shall use bounded mechanical steps
Window task handlers SHALL execute finite, explicit mechanical steps such as open menu, select item, click/confirm, validate state, and close/release. They MUST NOT ask another model to decide clicks, parse screenshots, or continue indefinitely.

#### Scenario: Bounded trade purchase
- **WHEN** a trade task buys an item
- **THEN** the handler performs a bounded sequence for locating the item, validating price/inventory, confirming quantity, and verifying the result

#### Scenario: Missing item stops task
- **WHEN** a requested shop item, recipe, quest option, or resource target is not present
- **THEN** the handler returns `blocked` or `failed` with a specific reason instead of searching indefinitely

### Requirement: Window tasks shall validate observable results
After a UI task performs a mechanical operation, it SHALL validate an observable game state change before returning `completed`. Validation MAY use bridge-provided inventory, money, quest state, menu state, world object state, or equivalent facts. If validation fails, the task MUST return a non-completed terminal state.

#### Scenario: Crafting validates inventory
- **WHEN** a crafting task claims completion
- **THEN** the host has verified the expected crafted item count, material consumption, or bridge-confirmed result

#### Scenario: Gathering validates result
- **WHEN** a gathering task interacts with a resource
- **THEN** the host verifies an inventory or world-state change before returning `completed`

### Requirement: Window tasks shall close or release safely
When a window task reaches any terminal state, it SHALL close only UI it owns or release its lease without touching unrelated menus. Cleanup MUST be safe if the menu has already been closed by the game, player, bridge, or another lifecycle event.

#### Scenario: Menu already closed
- **WHEN** a private chat or other UI task reaches cleanup but the expected menu is already gone
- **THEN** cleanup treats the lease as releasable and does not call unsafe close logic

#### Scenario: Timeout releases lease
- **WHEN** a window task times out
- **THEN** the task records timeout, releases its UI lease, and leaves an observable terminal fact

### Requirement: Private chat shall separate conversation from world action execution
Private chat SHALL use the main agent for natural reply, relationship judgment, and commitment/todo decisions. If the agent accepts an immediate world action, it MUST call a visible host-task submission tool. The private chat orchestrator MUST NOT delegate gameplay execution to a small model or infer actions from reply text.

#### Scenario: Agent accepts action request
- **WHEN** the player asks an NPC in private chat to go somewhere and the agent agrees
- **THEN** the agent records any needed todo itself, calls a visible Stardew action tool, and replies naturally; the host executes only the tool-created task

#### Scenario: Agent only replies
- **WHEN** the private chat response contains no world-action tool call
- **THEN** the host shows/sends the reply but does not create movement, speech, todo, or UI action from the reply text

### Requirement: Window tasks shall preserve player safety and game state
Window tasks SHALL respect player-free, festival, cutscene, day-transition, menu-blocked, inventory-space, money, material, and permission constraints reported by the bridge. Violations MUST become observable blocked/failed facts.

#### Scenario: Player is not free
- **WHEN** a task requires UI input while the player is in a cutscene or day transition
- **THEN** the task returns blocked with the bridge reason and does not attempt UI manipulation

#### Scenario: Not enough money
- **WHEN** a trade task lacks required money
- **THEN** the task returns blocked/failed with a money reason and does not partially buy unrelated items

### Requirement: Window action types shall extend the host task contract
New window capabilities such as crafting, trading, quest operations, and gathering SHALL be added as host task action types with schemas, validation, lifecycle, terminal facts, and harness coverage. They MUST NOT introduce a separate model-controlled UI runner.

#### Scenario: Adding a new quest action
- **WHEN** a quest window action is introduced
- **THEN** it is implemented as a host task handler using the common UI lease, status, timeout, validation, and cleanup lifecycle

#### Scenario: Unsupported window action
- **WHEN** the agent or MCP requests an unsupported window action
- **THEN** the system returns a blocked/unsupported fact and does not fall back to free-form model execution
