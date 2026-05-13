Task statement
- Supersede the previous small-model execution design. Align Stardew NPC orchestration with the reference project by removing the small-model execution lane from the v1 gameplay path.

Desired outcome
- A simpler architecture: main agent decides; host/bridge task runner executes; task/status/watchdog/facts return to the main agent.
- No small-model executor, no model-in-the-middle, no hidden fallback.

Known facts / evidence
- `external/hermescraft-main` uses command queues, background task IDs, `currentTask`, status polling, `briefState`, and watchdog stuck detection.
- `external/hermes-agent-main` emphasizes tool-call start/complete correlation by generated IDs.
- Current Hermes Stardew code already has `IngressWorkItems`, `ActionSlot`, `PendingWorkItem`, `LastTerminalCommandStatus`, runtime logs, state store, and host tick processing.

Constraints
- Agent decides; host executes and reports facts.
- Host must not parse free text into actions.
- No second tool/model lane in the Stardew v1 gameplay path.
- The framework must scale to movement, private chat, speech delivery, idle micro actions, crafting, trading, quest windows, and gathering.

Unknowns / open questions
- Exact naming of the unified host task contract.
- Whether existing `npc_delegate_action` becomes a thin host-task submission tool or is replaced by a generic host task tool.
- How much of existing `NpcLocalExecutorRunner` is deleted, bypassed, or left only for unrelated diagnostics.

Prompt-safe initial-context summary status
- not_needed
