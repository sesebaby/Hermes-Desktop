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

### Requirement: UI 生命周期等待必须表现为任务事实，而不是通用 ingress 失败
Stardew 的 UI / 窗口生命周期等待 MUST 表示为所属任务、交互、lease 或状态事实。当等待是可恢复的，并且属于游戏或玩家可见 UI 生命周期时，MUST NOT 把它归类成通用 ingress 重试失败。

#### Scenario: 私聊回复生命周期不能消耗 stale 预算
- **WHEN** 私聊回复对话框、电话回复，或同类回复 UI 生命周期仍然处于打开状态，或还在等待玩家交互
- **THEN** 相关的世界动作 host task 不能仅仅因为回复 UI 尚未关闭，就通过通用 stale / busy defer 预算被打成终态 blocked

#### Scenario: 菜单等待必须是可见状态
- **WHEN** 一个依赖 UI 的任务正在等待所属菜单、无关菜单，或某个玩家可见窗口条件结束
- **THEN** 这类等待必须以可读的 task / status 事实表现出来，例如 waiting、blocked 或 lease conflict，而不是隐藏的 host 锁

### Requirement: agent 流程继续推进时，UI 安全仍然必须留在游戏侧
把 agent 流程和 UI 生命周期解耦，MUST NOT 移除游戏侧的 UI 安全保护。任何会打开、关闭或操作菜单的任务，仍然 MUST 遵守 UI lease、当前激活菜单所有权、玩家自由状态、过场、节日、日切以及清理所有权。违反这些条件时，MUST 返回可观察的 blocked / failed 事实，并把下一步决策留给 agent。

#### Scenario: 无关的活动菜单阻止窗口操作
- **WHEN** 未来的交易、制作、采集、任务或私聊任务需要使用某个菜单，但当前存在一个无关的活动菜单
- **THEN** 任务必须返回带简短 `summary` 的 blocked 事实，并附带 `menu_blocked`、`ui_lease_busy` 或 `private_chat_active` 之类的 reason；同时不能关闭或覆盖这个无关菜单

#### Scenario: 所属 UI 等待在超时前保持可恢复
- **WHEN** 一个任务拥有 UI lease，并且正在等待某个可恢复的 UI 条件
- **THEN** 任务可以继续保持 running 或 waiting，直到条件改变，或由任务 timeout / watchdog 产生终态事实

#### Scenario: 超时会释放所属 UI 资源
- **WHEN** 一个依赖 UI 的任务在等待过程中超时
- **THEN** host 必须记录终态 timeout 事实，并使用 `action_slot_timeout` 或现有等价 timeout reason，同时只释放该任务自己拥有的 UI lease 和资源

### Requirement: 私聊回复投递必须与世界动作执行解耦
私聊回复的显示和关闭事件 MUST 被视为交互 / 窗口事实。它们 MUST NOT 再被当成通用理由，用来把一个由 agent 明确通过 host task 生命周期提交的世界动作任务直接打成终态 blocked。

#### Scenario: 回复显示期间，世界动作仍保持 pending
- **WHEN** agent 在私聊中自然回复，并且在同一个 private-chat turn 中提交了一个世界动作
- **THEN** 回复显示 / 关闭生命周期必须作为 UI 或交互状态跟踪，而世界动作则继续通过它自己的 host task status 表达

#### Scenario: 没有工具调用就仍然不能创建世界动作
- **WHEN** 私聊回复里没有任何世界动作工具调用
- **THEN** 即使回复文本提到了某种意图，也不能仅靠文本创建世界动作
