# PRD: Stardew Autonomy Intent Contract Tool Boundary

## Goal

Make the high-frequency Stardew autonomy tick cheap and stable without taking away NPC agency in player-facing conversations.

## Requirements

- Background autonomy parent agent must make high-level NPC decisions through a short JSON intent contract, not direct tool calls.
- Private chat and player task intake keep the normal Hermes tool surface, including `todo`, `memory`, `session_search`, skills, and speech/game tools.
- Autonomy intent contract must support:
  - mechanical action: `move`, `observe`, `wait`, `task_status`, `escalate`
  - optional NPC-authored speech intent
  - optional NPC-authored task status update
- Host/runtime owns real side effects:
  - local executor handles mechanical game actions such as move and command status
  - host submits speech actions from the contract
  - host updates existing todos from the contract
- Autonomy task updates must not create new tasks in MVP. They may update an existing task by id only.

## Tool Boundary

- Player/private chat channel: wide agent tool surface, because this is where the NPC understands requests, writes todos, remembers user facts, and speaks naturally.
- Background autonomy parent: no direct tool surface. The model sees context, soul, memory, active todo, and current game facts, then returns one contract.
- Local executor: restricted game-action tools only, currently `stardew_move` and `stardew_task_status`.
- Host-interpreted contract fields: `speech` becomes a `GameActionType.Speak`; `taskUpdate` may update an existing session todo by id.
- Other Hermes tools such as `memory`, `session_search`, `skills_*`, `skill_invoke`, `agent`, `schedule_cron`, MCP tools, and `todo_write` stay outside the high-frequency autonomy parent. They remain available to interactive/private channels, or their results are converted by the host into facts and active todo context before the next autonomy tick.
- Future local-executor candidates must be low-risk, bounded, and externally verifiable game actions. Examples: observe a specific scope, wait, move, check command status, use item, gift, emote, open/continue private chat. Tools that create long-term memory, change identity, spawn subagents, browse arbitrary external state, or schedule durable tasks should not be delegated from the autonomy tick without a new contract and tests.
- Future contract extensions can add `open_private_chat`, gift/use-item, or richer observe scopes after the minimal contract proves stable.

## Acceptance Criteria

- Autonomy parent LLM calls use `CompleteAsync`, not `CompleteWithToolsAsync`.
- Autonomy parent tool list is empty even when game, MCP, and built-in tools exist.
- Autonomy parent system prompt does not tell the model to use registered tools; it tells the model to return a JSON intent contract only.
- Private chat still exposes normal tools.
- A valid autonomy contract with `speech.shouldSpeak=true` submits one `GameActionType.Speak`.
- A valid autonomy contract with `taskUpdate` updates an existing todo in the NPC runtime store.
- Missing or unknown task ids do not create new todos.
- Logs show contract acceptance, parent tool surface verification, local executor selection, and host-side speech/task update outcomes.

## ADR

Decision: Use a contract-only parent autonomy lane plus restricted local executor.

Drivers: low cost, short parent context, fewer tool loops, preservation of NPC agency.

Alternatives considered:
- Keep wide tools on autonomy parent: rejected because live logs showed parent tool loops and max iteration failures.
- Remove todo/speech from NPCs globally: rejected because player task intake and NPC expressiveness depend on them.
- Let host decide tasks directly: rejected because host must execute, not author NPC intent.

Consequences: More explicit schema and host execution code, but lower tick cost and stronger observability.

Follow-ups: Add `open_private_chat` and item-use/gift contract fields only after this minimal contract passes live testing.
