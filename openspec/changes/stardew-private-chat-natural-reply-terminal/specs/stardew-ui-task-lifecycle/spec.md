## MODIFIED Requirements

### Requirement: Private chat shall separate conversation from world action execution
Private chat SHALL use the main agent for natural reply, relationship judgment, memory, and commitment/todo decisions. If the agent accepts an immediate world action, it MUST call a visible host-task submission tool and that tool result MUST succeed before the host executes anything. If the private-chat agent returns only a natural reply without a successful world-action tool call, the host MUST show/send that reply as a valid private-chat terminal result and MUST NOT run an additional LLM self-check to infer whether the agent forgot an action.

#### Scenario: Agent accepts action request
- **WHEN** the player asks an NPC in private chat to go somewhere and the agent agrees
- **THEN** the agent records any needed todo itself, calls a visible Stardew action tool, and replies naturally; the host executes only the successfully created tool task

#### Scenario: Agent explicitly chooses no world action
- **WHEN** the private-chat agent successfully calls `npc_no_world_action`
- **THEN** the host records that the turn explicitly chose no immediate world action and shows/sends the agent's natural reply

#### Scenario: Agent only replies
- **WHEN** the private-chat response contains no successful world-action tool call and no successful `npc_no_world_action`
- **THEN** the host shows/sends the natural reply as the private-chat result and does not create movement, speech, todo, UI action, or a second LLM self-check from the reply text

#### Scenario: Text promise without tool call is not executed
- **WHEN** the private-chat reply says or implies an immediate action such as going somewhere, but no `stardew_submit_host_task` result succeeded
- **THEN** the host does not execute any game action and treats the response only as player-visible dialogue
