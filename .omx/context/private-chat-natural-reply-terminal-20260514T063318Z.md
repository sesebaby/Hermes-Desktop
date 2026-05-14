# Context Snapshot: private chat natural reply terminal

## Task Statement

User complained that manual Stardew private-chat AI replies are especially slow, then provided another AI's proposed correction: treat "no-tool natural reply" as a legal private-chat terminal state instead of running an extra self-check or inferring actions from natural language.

## Desired Outcome

Decide whether Hermes-Desktop should remove the private-chat delegation self-check that runs when the parent NPC agent returns a natural reply without successful `stardew_submit_host_task` or `npc_no_world_action`, while preserving the project boundary that host code must not infer player/action intent from text.

## Stated Solution

Candidate direction: if the private-chat agent calls `stardew_submit_host_task`, the host executes the world action; if it calls `npc_no_world_action`, the host records no world action; if it calls neither and only returns natural language, the host displays that reply and does not run a second LLM self-check.

## Probable Intent Hypothesis

The user wants lower perceived private-chat latency without reintroducing hidden executor behavior. The underlying intent is not merely faster replies; it is to keep decision ownership inside the model-visible tool contract and stop host-side "helpful" recovery from becoming another decision layer.

## Known Facts / Evidence

- Project memory states that Stardew/NPC agent capability boundaries must be model-visible Hermes/MCP/Stardew tools; host code must not use free text or JSON-like text as a hidden action fallback.
- `openspec/specs/stardew-host-task-runner/spec.md` says natural language or JSON-like text without an assistant tool call must not execute gameplay.
- `openspec/specs/stardew-ui-task-lifecycle/spec.md` says private chat must not infer actions from reply text, and a response with no world-action tool call is shown/sent without creating movement, speech, todo, or UI action from the reply.
- Current code in `src/games/stardew/StardewPrivateChatOrchestrator.cs` runs `ShouldRunDelegationSelfCheck` when neither `stardew_submit_host_task` nor `npc_no_world_action` succeeded.
- That self-check is a second parent-agent LLM call and can explain the observed two-call latency pattern.
- Existing prior spec `.omx/specs/deep-interview-stardew-private-chat-reply-before-action.md` approved self-check for one narrower case: successful host-task submission with empty final reply, so the player still sees a reply before action.
- Existing error memory `E-2026-0513-stardew-private-chat-tool-attempt-vs-success.md` warns that tool-call presence is not enough; decisions must use successful tool results.

## Constraints

- Do not implement inside deep-interview.
- Do not reintroduce local executor, small-model gameplay runner, hidden fallback, text intent classifier, or host-side action inference.
- Do not make host synthesize NPC dialogue.
- Preserve successful-host-task empty-reply handling unless the user explicitly changes that boundary.
- Keep changes small, test-backed, and aligned with existing `StardewNpcPrivateChatAgentRunnerTests`.

## Unknowns / Open Questions

- Should `npc_no_world_action` remain mandatory in the prompt as a diagnostic/discipline tool even if absence no longer triggers self-check?
- Should no-tool natural replies record a diagnostic fact/log marker for observability, or remain just normal displayed dialogue?
- Should all existing delegation self-check tests be retired/rewritten, or should a narrower validation-failure path still keep self-check?

## Decision-Boundary Unknowns

- Whether OMX may remove `ShouldRunDelegationSelfCheck` and its associated prompt/tests without a separate ralplan artifact.
- Whether OMX may harden the prompt to say "no tool call means only speech; do not promise immediate action" as part of the same change.
- Whether the target success metric is "one LLM call for ordinary natural replies" or "single call unless there is successful host-task empty reply or normal agent tool-loop continuation".

## Likely Codebase Touchpoints

- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `openspec/specs/stardew-ui-task-lifecycle/spec.md`
- `.omx/specs/deep-interview-stardew-private-chat-reply-before-action.md`

## Current Ambiguity Estimate

- Profile: standard
- Context type: brownfield
- Ambiguity: 0.34
- Resolved: architectural direction is likely correct and source-backed.
- Unresolved: exact non-goals, observability policy, and whether to keep `npc_no_world_action` as a prompt-level expectation.
