# PRD: Stardew Autonomy Tool Closure Harness Reference Alignment

## Problem

After a visible Stardew action completes while an NPC still has an active todo, the next autonomy turn can answer with ordinary text that looks like a tool request, for example `{"tool":"stardew_status"}`. The core agent treats that as final text because the model did not emit a real tool call. The host then only records diagnostics, so the NPC appears to know tools at first but stops using them later.

## Product Intent

Autonomy should behave like an agent-native loop:
- The model must call visible tools for observations, task updates, speech, movement, and idle actions.
- Text JSON is not executable authority.
- Host feedback should be short, factual, and lifecycle-oriented, similar to the reference project's `briefState()` fields.

## Required Behavior

1. On normal autonomy wake-up, keep the existing wake-only decision prompt.
2. If all are true:
   - the previous terminal command is a real world-action terminal,
   - there is at least one active todo,
   - the agent's response produced no Stardew action tool call,
   - the agent produced no todo/todo_write tool call,
   - the text response did not contain an explicit `wait:` / `no-action:` reason,
   then run one extra self-check turn in the same session.
3. The self-check must state:
   - the previous reply had no real tool call,
   - JSON text is not a tool call,
   - the agent should now use a visible tool such as `todo`, `stardew_status`, `stardew_speak`, `stardew_idle_micro_action`, or `stardew_task_status`, or give an explicit no-action reason.
4. The harness must not parse or execute the first text response.
5. The harness must be bounded to one retry.

## Non-Goals

- No hidden execution of text JSON.
- No global model/provider `tool_choice` change.
- No task runner port from the Minecraft reference project.
- No new task state system.

## Reference Alignment

Borrow:
- short lifecycle feedback,
- explicit task/action completion facts,
- bounded retry/closure harness shape.

Do not borrow:
- Minecraft-specific command queue semantics,
- a separate bot task runner,
- host-side interpretation of arbitrary model text as execution intent.

