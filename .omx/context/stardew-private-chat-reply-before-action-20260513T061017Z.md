# Stardew Private Chat Reply Before Action Context

## Task statement
User reports that after private chat with an NPC, sometimes the NPC does not reply and directly starts acting. Desired behavior: the NPC should first reply through the vanilla-style dialogue/private-chat reply UI, then start the action.

## Desired outcome
Clarify requirements before planning or implementation. Preserve agent-native Stardew v1 path: parent/main agent visible tools, host/bridge task runner execution, terminal facts back to agent. No hidden local executor and no host-side gameplay intent parsing.

## Stated solution
Require NPC to reply first using the original dialogue/private-chat display path, then begin movement/action.

## Probable intent hypothesis
Private chat should feel conversational and responsive. Even when the NPC accepts an immediate world action, the player needs visible acknowledgement before the NPC starts acting.

## Known facts/evidence
- `src/game/core/PrivateChatOrchestrator.cs` submits `Speak` only after `ReplyAsync` returns non-empty text.
- If `ReplyAsync` returns empty text, `PrivateChatOrchestrator.TryReplyToPlayerMessageAsync` calls `EndSession()` and does not call `SubmitSpeakAsync`.
- `src/games/stardew/StardewPrivateChatOrchestrator.cs` requires accepted immediate world actions to use `todo` then `stardew_submit_host_task`, then final natural reply.
- `StardewPrivateChatOrchestrator.ReplyAsync` returns `response.Trim()` from the parent agent. It does not currently appear to synthesize or enforce a non-empty player-visible final reply after successful host-task submission.
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs` delays private-chat move ingress until `private_chat_reply_displayed` or `private_chat_reply_closed` appears.
- Because `private_chat_reply_displayed` is sufficient, a move may begin while the reply UI is visible, not necessarily after player dismissal.
- Existing tests assert host task ingress is queued for accepted immediate action, and one regression explicitly says delegated move after private chat reply displayed submits move without waiting for close.

## Constraints
- Stardew v1 has one execution path only: visible parent tool call -> host/bridge task runner -> terminal fact.
- Do not introduce hardcoded destination parsing or host-side natural-language gameplay intent parsing.
- Do not use `local_executor`, hidden fallback, JSON-text action parsing, or small-model gameplay execution.
- `todo` remains agent-owned; host must not infer or auto-close task state.
- Private-chat invalid or failed tool attempts must return to the parent agent-native loop as visible tool results.

## Unknowns/open questions
- Resolved: private-chat accepted world actions must begin only after the NPC reply is displayed and the player dismisses/closes the reply box.
- Resolved: if the parent agent successfully submits `stardew_submit_host_task` but final reply text is empty, the fix must stay agent-native: run a bounded parent self-check that asks the same visible parent agent to produce a natural player-facing reply, without repeating host-task submission. The host must not synthesize NPC dialogue.
- Resolved: every private-chat-triggered `stardew_submit_host_task` must require a non-empty reply and wait for `private_chat_reply_closed` before host-task execution, not only `action=move`.
- Resolved: if reply display fails or no `private_chat_reply_closed` arrives within the bounded wait, the host task must not execute. The host should record deferred/blocked terminal facts for the parent agent to handle/retry/explain, without synthesizing NPC dialogue.

## Decision-boundary unknowns
- Resolved: OMX may change the existing test expectation that move starts after `private_chat_reply_displayed` without waiting for close.
- Resolved: OMX may add a hard gate that host-task ingress for private-chat accepted world actions waits for `private_chat_reply_closed`.
- Resolved: OMX may add agent-native bounded self-check for empty final reply without changing tool contracts.

## Interview rounds
- Round 1 answer: choose B. NPC must first show a non-empty private-chat reply and the host task must not start until the player closes/dismisses that reply.
- Round 2 answer: choose A. Empty final reply after successful host-task submission must be repaired by an agent-native parent self-check. Host must not hardcode/synthesize NPC dialogue or add non-native fallback.
- Round 3 answer: choose A. Scope applies to all private-chat-triggered `stardew_submit_host_task` actions, not only movement.
- Round 4 answer: reference-project alignment accepted. HermesCraft has no dialogue-close UI concept, but its equivalent principle is: player commands are queued, agent replies through visible chat/whisper tools, long actions are background tasks, and completion/status facts return to the agent. For Stardew, the equivalent is visible parent agent reply first, then `private_chat_reply_closed` gate before host-task execution; failures become facts, not host-side dialogue or hidden execution.

## Likely codebase touchpoints
- `src/game/core/PrivateChatOrchestrator.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs`

## Prompt-safe initial-context summary status
not_needed
