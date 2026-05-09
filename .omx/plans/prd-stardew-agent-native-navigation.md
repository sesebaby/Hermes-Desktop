# PRD: Stardew Agent-Native Navigation Delegation

Status: draft for RALPLAN review
Source spec: `.omx/specs/deep-interview-stardew-agent-native-navigation.md`
Context snapshot: `.omx/context/stardew-agent-native-navigation-deep-interview-20260509T001401Z.md`

## Problem

Manual Stardew movement to the beach can work because the host/debug path already submits a concrete `Beach:32,34` mechanical target. Private chat, however, can accept a player request such as "now go to the beach" and still only reply in text, because the accepted intent is not converted into an agent-native delegated action.

The fix must preserve the project boundary: agent decides intent through tools/skills; host queues and executes explicit contracts; no host natural-language parsing, no `destination[n]`, and no second movement route.

## Product Goal

When an NPC accepts an immediate action request in private chat, the NPC agent should explicitly delegate that action through a tool. The runtime should queue it, the local executor should resolve mechanical targets through Stardew navigation skills, and the host should execute `stardew_navigate_to_tile` or return structured blocked/escalated evidence.

## Non-Goals

- No host parsing of conversational text to infer "go to beach" or "now".
- No `destination[n]`, `nearby[n]`, `moveCandidate[n]`, or destination-id based movement.
- No retained `stardew_move(destinationId)` compatibility path for NPC movement.
- No complex priority, numeric priority, or interruption policy fields.
- No separate structured assistant-message dispatcher protocol.
- No main/cloud agent direct move authority.
- No full indoor navigation in this pass; later indoor movement should be local small-model micro-actions.
- No attempt to make bridge/adapter DTOs part of the new movement contract unless a separate adapter migration is approved.

## RALPLAN-DR Summary

### Principles

1. Agent-native decisions: NPC/private-chat agents decide and delegate; the host only executes explicit contracts.
2. One path only: remove destination-id movement instead of preserving hidden compatibility.
3. Infrastructure first: reuse existing runtime handles, tool registration, ingress work items, action slots, skills, and local executor.
4. Simple contracts: keep `npc_delegate_action` small enough for reliable model use.
5. Debuggable evidence: every handoff should leave structured breadcrumbs.

### Decision Drivers

1. Prevent host-side rule scripts from replacing agent behavior.
2. Make private-chat promises become executable without giving cloud parent direct movement power.
3. Remove destination-id movement cleanly while preserving enough logs/tests to diagnose failures.

### Viable Options

Option A: `npc_delegate_action` -> ingress -> local executor -> `skill_view` -> `stardew_navigate_to_tile` (recommended)
- Pros: matches user constraints; reuses existing private chat handle/tool surface, ingress persistence, local delegation lane, skill tools, and mechanical navigation tool.
- Pros: single visible route with strong logging and tests.
- Cons: requires coordinated edits across prompt/tool/runtime/tests and a skill-asset migration away from destination guidance.

Option B: Private chat writes a todo, autonomy later interprets it
- Pros: reuses existing todo/task visibility.
- Cons: too indirect for "now"; private chat can still look like it accepted but not act; risks making todo a second dispatcher.
- Rejected because the requested behavior is immediate delegated action, not long-term memory.

Option C: Host parser detects "go to beach" from private-chat text
- Pros: superficially fast.
- Cons: violates project principle, hardcodes semantics, breaks agent boundary.
- Rejected by user and by architecture constraints.

## Functional Requirements

- Private chat prompt/tool surface must expose an `npc_delegate_action` tool to the private-chat agent.
- `npc_delegate_action` should accept a simple action contract, not rich scheduler policy.
- The tool should create a durable ingress work item using existing `NpcRuntimeIngressWorkItemSnapshot`.
- Delegated action ingress should be processed by existing runtime/action-slot flow when the executor is free.
- Local executor should use a read-only `skill_view` tool surface to load `stardew-navigation` and linked reference files for target resolution.
- Movement must end in `stardew_navigate_to_tile` with `locationName`, `x`, `y`, and `source`.
- Ambiguous/missing/unreachable targets must return structured `blocked` or `escalate`.
- `destinationId` should be removed from NPC movement contracts, prompts, tools, and tests.
- Skill assets and compact prompt supplements must stop teaching `destination[n]` / `stardew_move` for NPC movement.

## Acceptance Criteria

- Private-chat tests prove the agent can call `npc_delegate_action` while still replying naturally.
- No test relies on host parsing of private-chat reply text to trigger movement.
- Runtime tests prove delegated private-chat action becomes an ingress work item.
- Local executor tests prove move without concrete target uses the local model with `skill_view` and then `stardew_navigate_to_tile`, not `stardew_move`.
- Beach target resolution test proves skill path can load `references/index.md`, `references/regions/beach.md`, and `references/poi/beach-shoreline.md`, producing `Beach:32,34` and `map-skill:stardew.navigation.poi.beach-shoreline`.
- Destination migration tests fail if `destination[n]`, `destinationId`, or `stardew_move(destinationId)` reappear in NPC movement prompt/tool descriptions.
- Prompt supplement tests fail if `StardewNpcAutonomyPromptSupplementBuilder` still injects `destination[n]`, `nearby[n]`, or `moveCandidate[n]` into NPC autonomy prompts.
- Skill asset tests fail if bundled Stardew navigation/world/task/social skill files still instruct `stardew_move` or destination-id movement as the product path.
- Query-service tests fail if new NPC movement observations still emit destination candidate facts for the product path.
- Failure tests prove ambiguous/missing skill targets produce `blocked` / `escalate` with no fallback parser.
- Logs include trace id, work item id, tool name, skill source/file path, selected target, executor mode, command id when present, and blocked reason when present.

## Code Evidence

- Private chat already creates an NPC runtime handle with tool surface: `src/games/stardew/StardewPrivateChatOrchestrator.cs:212`, `src/games/stardew/StardewPrivateChatOrchestrator.cs:228`.
- Private-chat prompt currently handles todo/memory but no action delegation: `src/games/stardew/StardewPrivateChatOrchestrator.cs:251`.
- Autonomy parent intentionally has an empty combined tool surface: `src/runtime/NpcRuntimeSupervisor.cs:264`.
- Local executor tool surface is built separately and uses delegation chat client: `src/runtime/NpcRuntimeSupervisor.cs:298`, `src/runtime/NpcRuntimeSupervisor.cs:315`.
- Existing ingress work item shape is durable: `src/runtime/NpcRuntimeDescriptor.cs:62`, `src/runtime/NpcRuntimeStateStore.cs:50`.
- Ingress processing currently handles only `scheduled_private_chat`: `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:1003`, `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:1011`.
- Local executor can directly execute concrete mechanical move: `src/runtime/NpcLocalExecutorRunner.cs:92`, `src/runtime/NpcLocalExecutorRunner.cs:137`.
- Local executor still falls back to `stardew_move` for move without target: `src/runtime/NpcLocalExecutorRunner.cs:214`.
- `stardew_move` description is explicitly tied to `destination[n].destinationId`: `src/games/stardew/StardewNpcTools.cs:461`, `src/games/stardew/StardewNpcTools.cs:463`.
- Navigation skill already defines target layering and beach target: `skills/gaming/stardew-navigation/SKILL.md:63`, `skills/gaming/stardew-navigation/references/poi/beach-shoreline.md:7`.
- Skill compact contracts still teach destination-style movement and are injected into runtime prompts: `skills/gaming/stardew-navigation/SKILL.md:3`, `skills/gaming/stardew-world/SKILL.md:11`, `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs:83`.
- Query service still emits destination/nearby candidate facts: `src/games/stardew/StardewQueryService.cs:223`, `src/games/stardew/StardewQueryService.cs:248`.

## ADR

Decision: Use `npc_delegate_action` as the only private-chat immediate action path, queue it through existing ingress/action-slot infrastructure, and have the local executor resolve targets via skills before calling mechanical host tools.

Drivers: preserve agent boundary; remove destination dual-track; keep model contracts simple; use existing infrastructure.

Alternatives considered: todo-only delegation; structured assistant-message dispatcher; host text parser; retained destination-id fallback.

Why chosen: the recommended path is the only one that satisfies all user constraints while reusing runtime infrastructure.

Consequences: implementation must touch several existing tests and remove old destination assumptions. Debug/manual tooling may need conversion to explicit coordinate/skill-source targets if it currently depends on destination IDs. Lower-level bridge DTO / command-service destinationId handling is intentionally left for a separate adapter decision unless a specific edit is required by the product-path migration.

Follow-ups: later indoor movement should extend local-model micro-actions rather than reviving destination candidates.
