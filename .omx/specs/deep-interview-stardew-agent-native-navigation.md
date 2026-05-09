# Deep Interview Spec: Stardew Agent-Native Navigation

Metadata:
- Profile: standard
- Final ambiguity: ~0.12
- Threshold: 0.20
- Context type: brownfield
- Context snapshot: `.omx/context/stardew-agent-native-navigation-deep-interview-20260509T001401Z.md`
- Interview transcript: `.omx/interviews/stardew-agent-native-navigation-20260509T001401Z.md`

## Intent

Fix the Stardew NPC private-chat/action path so when an NPC agrees to an immediate action, such as going to the beach, the behavior is executed through agent-native delegation and local skill-grounded execution rather than host-side hardcoded parsing or `destination[n]` candidates.

## Desired Outcome

Private chat can produce a visible action:

1. Player asks NPC to do something now.
2. Private chat agent decides whether to accept.
3. If accepted, the agent calls `npc_delegate_action`.
4. Runtime queues a simple delegated action ingress item through existing infrastructure.
5. Local executor uses `skill_view` on `stardew-navigation` references to find the concrete target.
6. Local executor calls `stardew_navigate_to_tile` with coordinates and target source.
7. Logs make it clear whether the chain succeeded or where it stopped.

## In Scope

- Add an agent-facing private-chat delegation tool, tentatively `npc_delegate_action`.
- Make private chat prompt/tool surface instruct the agent to call that tool when it accepts an immediate action request.
- Use existing NPC runtime handle/tool infrastructure rather than parsing the final reply text.
- Queue delegated actions via existing ingress/action-slot infrastructure.
- Let local executor use `skill_view` on `stardew-navigation` and linked references.
- Use `stardew_navigate_to_tile` for mechanical movement execution.
- Remove the `destination` concept from the NPC movement product path.
- Convert or remove `stardew_move(destinationId)` usage for NPC movement.
- Return structured `blocked` / `escalate` for ambiguity, missing coordinates, or navigation failure.
- Add diagnostic-friendly logs for delegation, skill lookup, target selection, executor result, command id, and blocked reason.

## Out Of Scope

- Host-side natural-language place or intent parsing.
- `destination[n]`, `nearby[n]`, `moveCandidate[n]`, or destination-id movement candidates.
- Keeping `stardew_move(destinationId)` as a hidden compatibility route.
- A second structured assistant-message dispatcher protocol.
- Complex priority, numeric scheduling, or interruption policy fields for the agent.
- Main/cloud agent direct move authority.
- Solving full indoor navigation now; future indoor movement should be local-model micro-action based.

## Decision Boundaries

Allowed without further confirmation:
- Design and plan around `npc_delegate_action` as the only private-chat immediate action delegation path.
- Reuse existing runtime ingress/action-slot mechanics.
- Reuse `skill_view` and existing `stardew-navigation` skill assets.
- Remove destination-id movement from NPC runtime/tool/prompt/test surfaces.
- Add focused structured diagnostic logs.

Requires explicit user confirmation:
- Any fallback host parser.
- Any retained destination-id movement path.
- Any second movement/action dispatch protocol.
- Any complex scheduler or rich priority system.

## Constraints

- Hermes is an agent project; behavior should be skill/tool/delegation driven.
- Host executes explicit mechanical contracts and records facts, but does not decide NPC behavior.
- Reuse existing infrastructure before adding a new route.
- Avoid dual tracks during prerelease.
- Keep agent contracts simple so the model is less likely to fail.

## Testable Acceptance Criteria

- A private-chat agent can call `npc_delegate_action` when accepting "go now" style requests.
- The host does not parse the conversational reply text to infer movement.
- The delegated work appears as a runtime ingress item, not as a todo-only record.
- The local executor has access to `skill_view` for the Stardew navigation skill references.
- A beach request can resolve to `Beach`, `x=32`, `y=34`, `source=map-skill:stardew.navigation.poi.beach-shoreline` through skill content.
- Mechanical movement uses `stardew_navigate_to_tile`.
- If no unique skill target exists, the result is `blocked` or `escalate`; no host fallback parsing occurs.
- `destination[n]` and `stardew_move(destinationId)` are absent from NPC movement prompts/tool routes/tests after migration.
- Runtime logs show: `traceId`, private-chat delegation event/tool, work item id, skill files or skill source used, selected target, executor mode, tool name, command id when present, and blocked/failure reason when present.

## Brownfield Evidence

- Manual beach movement already works with explicit `Beach:32,34` and target source.
- Existing skill assets contain beach POI coordinates.
- Current deterministic executor can execute a concrete mechanical target.
- Current `stardew_move(destinationId)` path is tied to the destination system and should be removed from NPC movement.
- Existing runtime has ingress work item persistence and action-slot mechanics that should be reused.

## Assumptions

- `skill_view` can be exposed to the local executor in a sufficiently narrow tool surface.
- The local small model is already available on the delegation lane.
- Existing micro-action work can later absorb indoor movement without preserving destination IDs.

## Pressure-Pass Finding

A proposed priority/timing contract was simplified after user challenge. The final requirement is a direct delegated-action path, not a scheduler-heavy design.

## Handoff Recommendation

Recommended next step: `$ralplan` with this spec as input.

Reason: the desired behavior is now clear, but implementation touches prompt/tool/runtime/test surfaces and needs a careful migration plan to remove destination paths without creating a hidden second route.
