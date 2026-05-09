# Stardew Agent-Native Navigation Interview

Timestamp: 2026-05-09T00:14:01Z
Profile: standard
Final ambiguity: ~0.12
Threshold: 0.20
Context snapshot: `.omx/context/stardew-agent-native-navigation-deep-interview-20260509T001401Z.md`

## Summary

The user wants Stardew NPC movement and private-chat action promises to follow an agent-native path. Host-side hardcoded natural-language parsing, `destination[n]`, and destination-id based movement should be removed from the NPC movement product path.

The agreed direction is:

1. Private chat agent accepts an immediate action request by calling a same-turn structured `npc_delegate_action` tool.
2. That tool queues a simple delegated action ingress item through existing runtime infrastructure.
3. Local executor uses the existing skill system, especially `skill_view` on `stardew-navigation` and linked references, to locate concrete coordinates.
4. Local executor calls executor-only mechanical tools such as `stardew_navigate_to_tile`.
5. Failure, ambiguity, or unreachable targets return structured `blocked` / `escalate`, never host fallback parsing.
6. Logs must be easy to inspect: trace the delegation, skill source, selected target, executor mode, command id, and blocked reason.

## Rounds

### Round 1

Question: Should private chat action delegation use same-turn `npc_delegate_action` or a structured assistant message consumed by a dispatcher?

Answer: Use same-turn `npc_delegate_action`.

Decision: Private chat agent explicitly delegates actions through a tool call. The host must not infer action intent by parsing reply text.

### Round 2

Question: May the local small model use `skill_view` on `stardew-navigation` and linked references as the first-pass location knowledge source?

Answer: Allowed.

Decision: Location resolution belongs in skills, not host resolver code.

### Round 3

Question: On ambiguous or missing skill-grounded coordinates, should the system return `blocked` / `escalate` instead of fallback parsing?

Answer: Agreed.

Decision: No fallback host parsing and no `destination[n]` fallback.

### Round 4

Question: Should `destination` be hidden from agent-facing paths only, or removed completely from the NPC movement product path?

Answer: Remove it completely. Indoor movement should later be handled by local small-model micro-actions.

Decision: Remove destination-id movement rather than preserving it as a compatibility/debug route.

### Round 5

Question: Should delegated private-chat actions carry rich priority/timing/interruption fields?

Answer: No. Keep agent work simple and direct.

Decision: `npc_delegate_action` should create a simple delegated action ingress item. Do not make the agent manage scheduler policy.

### Round 6

Question: Is minimal observability enough, or should logs also be optimized for troubleshooting?

Answer: Minimal observability is accepted, but logs must be easy to inspect for debugging.

Decision: Logs should include structured proof points: tool path, skill source, selected target, executor mode, command id, blocked reason, and trace id where available.

## Pressure Pass

The original priority idea was challenged because it risked pushing scheduling policy onto the agent. The user rejected complexity, and the design was simplified to a direct delegated action ingress path with existing action-slot mechanics.

## Non-Goals

- No host-side natural-language parsing for places or intent.
- No `destination[n]` movement candidate system.
- No `stardew_move(destinationId)` compatibility path for NPC movement.
- No rich priority scheduler or numeric priority fields for the agent.
- No second structured assistant-message dispatcher protocol.
- No main/cloud agent direct move authority.

## Decision Boundaries

Codex may design around the agreed `npc_delegate_action` path, local executor skill lookup, existing ingress/action-slot mechanics, and diagnostic logs.

Codex must not introduce a second movement route, fallback parser, destination-id compatibility path, or complex scheduler without explicit user approval.
