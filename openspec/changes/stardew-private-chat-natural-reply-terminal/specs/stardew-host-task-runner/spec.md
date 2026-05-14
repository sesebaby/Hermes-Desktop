## MODIFIED Requirements

### Requirement: Stardew gameplay shall use one host task execution path
Stardew v1 gameplay actions SHALL be executed only through model-visible tools that create host task/work item records. The host and bridge SHALL mechanically execute those tasks and return facts to the main agent. The system MUST NOT route movement, speech delivery, idle micro actions, private-chat immediate actions, UI operations, or `todo` closure through a small-model executor, hidden executor, text-intent classifier, or second tool lane. In private chat, natural language without a successful host-task tool call SHALL be treated as dialogue only, not as an implicit action request.

#### Scenario: Tool call creates a host task
- **WHEN** the main NPC agent calls a Stardew movement, speech, idle micro action, private chat, or window action tool and the tool result succeeds
- **THEN** the runtime creates or updates a host task/work item with stable identity and the host/bridge executes the mechanical action

#### Scenario: Failed tool call does not create a host task
- **WHEN** the main NPC agent attempts `stardew_submit_host_task` but the tool result fails validation or does not enqueue work
- **THEN** the host treats the action as not submitted and MUST NOT execute gameplay from the attempted call or from surrounding text

#### Scenario: No tool call does not execute gameplay
- **WHEN** the main NPC agent returns only natural language or JSON-like text without a successful assistant tool call
- **THEN** the host MAY record a diagnostic fact or log entry and MUST NOT infer or execute any Stardew gameplay action from that text

#### Scenario: Private chat natural reply is terminal dialogue
- **WHEN** a private-chat turn returns a non-empty natural reply without a successful `stardew_submit_host_task`
- **THEN** the host shows/sends the reply to the player and does not run a second LLM turn merely to decide whether world action was intended

#### Scenario: Small-model execution lane is not reachable
- **WHEN** a Stardew v1 gameplay action is submitted through autonomy, private chat, scheduled ingress, native tool, or MCP tool
- **THEN** no `local_executor`, small-model executor, model-in-the-middle runner, hidden fallback, or host-side text parser is invoked
