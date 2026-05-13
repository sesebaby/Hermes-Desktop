# PRD: Stardew Private Chat Reply Before Action

## Decision

Repair the Stardew private-chat immediate-action flow so the parent NPC agent must produce a non-empty in-character reply before any private-chat host task can execute, and the host task runner may consume that ingress only after the matching `private_chat_reply_closed` event.

## Drivers

- Agent-native contract: the parent agent owns the reply, visible tool calls, and follow-up decisions; the host only executes and returns facts.
- Player experience: a private-chat request must never feel like a silent action acknowledgment.
- Reference alignment: match HermesCraft’s pattern of player request -> agent reply -> background task -> terminal fact, mapped onto Stardew’s blocking reply dialog.
- Single path: preserve one visible-tool host-task lane with no local executor, no host-authored dialogue, and no hidden fallback.

## Alternatives Considered

- Let the host synthesize a fallback reply when `stardew_submit_host_task` succeeded but final text is empty.
  Rejected because it creates a second dialogue author and breaks the agent-native contract.
- Allow host-task ingress to execute once `private_chat_reply_displayed` is seen.
  Rejected because the player can still be trapped in the blocking reply UI while the action already starts.
- Add a separate reply-await state store outside the existing ingress defer/block path.
  Rejected because the current deferred-ingress lifecycle already models bounded waiting and terminal blocking.

## Scope

- Add a bounded parent self-check when a successful `stardew_submit_host_task` exists but the final private-chat reply is empty.
- The self-check may only ask the same parent agent to add a natural reply; it must not duplicate host-task submission.
- If the self-check still yields an empty reply, convert the already queued private-chat host-task ingress into a blocked terminal fact for the agent to resolve later.
- Gate every private-chat `stardew_host_task_submission` carrying `conversationId` on `private_chat_reply_closed`, not `private_chat_reply_displayed`.
- Reuse the existing deferred-ingress and terminal blocked fact mechanisms so “reply never closes” becomes observable and bounded.
- Cover all private-chat host-task actions with `conversationId`, not only `move`.

## Out Of Scope

- No private-chat UI redesign.
- No natural-language intent parsing or host-side target inference.
- No automatic `todo` completion/closure after terminal facts.
- No revival of `local_executor`, sidecar executors, or second tool lanes.

## Acceptance Criteria

- A successful private-chat `stardew_submit_host_task` followed by a non-empty reply keeps ingress queued/deferred until `private_chat_reply_closed`, then executes normally.
- `private_chat_reply_displayed` alone never authorizes execution of the matching private-chat host task.
- The wait gate applies to all private-chat `stardew_host_task_submission` ingress with `conversationId`, including currently unsupported future action skeletons such as `craft`.
- A successful host-task submission followed by an empty final reply triggers one bounded parent self-check that asks only for a natural reply and explicitly forbids duplicate submission.
- If the reply remains empty after that self-check, the ingress is blocked and exposed through existing terminal-fact machinery; the host does not silently execute it.
- If `private_chat_reply_closed` never arrives, the queued ingress eventually reaches the existing deferred/block limit and becomes an observable blocked terminal fact.
- No host-authored NPC reply text, hidden fallback, or second execution path is introduced.
