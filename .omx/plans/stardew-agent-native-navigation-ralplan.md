# RALPLAN: Stardew Agent-Native Navigation Delegation

Status: approved by RALPLAN Architect/Critic
PRD: `.omx/plans/prd-stardew-agent-native-navigation.md`
Test spec: `.omx/plans/test-spec-stardew-agent-native-navigation.md`
Deep-interview spec: `.omx/specs/deep-interview-stardew-agent-native-navigation.md`

## Recommendation

Implement the single-path `npc_delegate_action` design:

Private chat agent -> `npc_delegate_action` -> durable ingress item -> existing action-slot/runtime processing -> local executor on delegation lane -> read-only `skill_view(stardew-navigation references)` -> `stardew_navigate_to_tile`.

This is the best fit because it keeps world meaning in skills, keeps host behavior mechanical, reuses existing NPC runtime infrastructure, and removes `destination` from the NPC movement product path instead of hiding it.

## Implementation Steps

1. Add the private-chat delegation tool.
   - Add a focused `npc_delegate_action` tool class near Stardew/NPC tool infrastructure, preferably where it can enqueue `NpcRuntimeIngressWorkItemSnapshot` through the current `NpcRuntimeDriver`.
   - Schema should stay simple: `action`, `reason`, optional `intentText`, optional `conversationId`.
   - It should not include numeric priority, rich timing, or natural-language parsing.
   - It should log a diagnostic record with trace id, work item id, NPC id, conversation id, and action.

2. Wire the tool only into private-chat agent surface.
   - Extend `StardewNpcPrivateChatAgentRunner` tool surface construction so private chat can see `npc_delegate_action`.
   - Update `BuildPrivateChatSystemPrompt` to say: when you accept an immediate player request that requires world action, call `npc_delegate_action` first, then reply naturally.
   - Keep the main/autonomy parent without direct move tools.

3. Generalize ingress processing for delegated actions.
   - Extend `TryProcessIngressWorkAsync` in `StardewNpcAutonomyBackgroundService` to handle `npc_delegated_action` in addition to `scheduled_private_chat`.
   - Convert delegated action payload into a local executor intent handoff, not a host movement command.
   - Preserve current action-slot behavior: if a command is in flight, leave work queued.
   - Remove work item only on accepted/completed terminal path or non-retryable failure according to existing patterns.

4. Teach local executor to resolve move targets through skills.
   - Add only the existing read-only `SkillViewTool` / `skill_view` to the local executor tool surface for this route.
   - Do not add `skill_manage`, `skill_invoke`, filesystem tools, memory tools, todo tools, or parent movement tools to the local executor surface.
   - Include local executor tool-surface fingerprint/rebind tests so adding `skill_view` is deterministic and visible to `NpcRuntimeSupervisor`.
   - Replace `SelectTools(Move => stardew_move)` with a model path that can use `skill_view` and `stardew_navigate_to_tile`.
   - Update system/user prompt so the local executor loads `stardew-navigation`, follows index -> region -> POI, and never invents coordinates.
   - Preserve the existing direct mechanical `Target` fast path to `stardew_navigate_to_tile`.

5. Remove destination-id movement contracts.
   - Remove `DestinationId` from `NpcLocalActionIntent` move validity and serialization.
   - Remove or retire `StardewMoveTool` from NPC tool factories/routes.
   - Update prompt text and tests that mention `destination[n]`, `destinationId`, or `stardew_move`.
   - Update `StardewQueryService` so NPC movement observations no longer emit `destination[n]`, `nearby[n]`, or `moveCandidate[n]` candidate facts for the product path.
   - Update bundled skills including `skills/gaming/stardew-navigation/SKILL.md`, `skills/gaming/stardew-world/SKILL.md`, `skills/gaming/stardew-task-continuity/SKILL.md`, and `skills/gaming/stardew-social/SKILL.md` so compact contracts no longer teach `stardew_move` or destination-id movement.
   - Update prompt-supplement/budget tests that currently preserve destination or `stardew_move` context.
   - If any manual/debug UI still needs movement, convert it to explicit coordinates plus target source.
   - Do not delete lower-level bridge DTO / command-service `destinationId` fields solely for cleanliness; classify remaining hits as legacy adapter surface unless they are reachable from NPC private-chat/autonomy movement.

6. Add diagnostic logging.
   - Log private-chat delegation tool call and queued ingress item.
   - Log local executor skill files/sources read, selected target source, executor mode, command id, and blocked reason.
   - Keep logs structured in existing `runtime.jsonl` style so developer inspector and manual tailing stay useful.

7. Update tests in migration order.
   - First write failing tests for `npc_delegate_action` ingress creation and private-chat tool-call path.
   - Then local executor skill-resolution tests.
   - Then destination-removal regression tests.
   - Finally focused integration tests around beach movement and blocked ambiguity.

## Risks And Mitigations

- Risk: `skill_view` is too broad for local executor.
  - Mitigation: expose only the existing tool surface needed for skills; do not add filesystem tools. Consider logging selected skill name/file path.

- Risk: private chat calls the tool but user sees no movement because ingress is not processed promptly.
  - Mitigation: reuse existing ingress wake/inbox depth and add tests that queued delegated action is processed before ordinary autonomy decision when no action slot is occupied.

- Risk: destination removal breaks existing manual/debug tests.
  - Mitigation: migrate manual/debug movement to explicit coordinate target, or delete destination-id debug surface if redundant.

- Risk: destination removal accidentally deletes lower-level bridge/adapter compatibility that is outside the product path.
  - Mitigation: define migration boundary as NPC prompt/tool/local-executor/query/skill product path. Leave bridge DTO cleanup to a separate approved adapter task unless directly required.

- Risk: local model fails to call tools correctly.
  - Mitigation: keep prompt narrow, provide only needed tools, retry once on no tool call, return `blocked` with diagnostic reason instead of fallback parser.

## Verification

- Focused test command from test spec.
- Full desktop test project.
- Bridge tests only if bridge DTO/action contracts change.
- Repo-wide migration guard search from test spec, with every remaining `destinationId` / `stardew_move` hit classified as removed, product-path bug, or lower-level legacy adapter surface.
- Manual test: ask Haley in private chat to go to the beach; verify `runtime.jsonl` shows `npc_delegate_action`, skill source `beach-shoreline`, `stardew_navigate_to_tile`, and command completion or blocked reason.

## Available Agent Types

- `explore`: fast codebase/file mapping.
- `architect`: architecture and boundary review.
- `critic`: plan/design challenge.
- `executor`: implementation.
- `test-engineer`: tests and regression coverage.
- `verifier`: completion evidence and test adequacy.
- `code-reviewer`: final review.

## Follow-Up Staffing Guidance

For `$ralph`:
- One `executor` lane is enough if implementing sequentially.
- Ask a `test-engineer` to focus on destination-removal and private-chat ingress tests if execution becomes broad.
- Finish with `verifier` or `code-reviewer`.

For `$team`:
- Executor 1: private chat tool and prompt surface.
- Executor 2: ingress processing and runtime logs.
- Executor 3: local executor skill-view movement and destination removal.
- Test engineer: migration/regression tests.
- Verifier: focused command matrix and manual-log checklist.

## Launch Hints

Sequential:

```text
$ralph .omx/plans/stardew-agent-native-navigation-ralplan.md
```

Coordinated:

```text
$team .omx/plans/stardew-agent-native-navigation-ralplan.md
```

## Team Verification Path

Team must prove:
- no destination-id movement remains in NPC product path;
- private chat can call `npc_delegate_action`;
- delegated action becomes ingress work;
- local executor uses skills then `stardew_navigate_to_tile`;
- logs are diagnostic-friendly.

Ralph/verifier should then run focused tests, full desktop tests, and check the manual beach flow log contract.

## Changelog

- Initial RALPLAN draft created from deep-interview spec and repository evidence.
- Applied Architect iteration feedback: clarified read-only `skill_view` injection, skill/prompt/query migration scope, destination removal boundary, and bridge DTO non-goal.
- Applied Critic approval notes: status marked approved and execution guardrails made explicit.

## Execution Guardrails

- `npc_delegate_action` belongs only on the private-chat agent surface; main/autonomy parent must not gain direct movement tools.
- Local executor may receive read-only `skill_view` and required mechanical navigation tools only; do not expose `skill_manage`, `skill_invoke`, filesystem, memory, todo, or parent movement tools.
- NPC product path must not retain `destinationId` / `stardew_move(destinationId)` fallback. Remaining search hits must be classified as lower-level legacy adapter surface and unreachable from private-chat/autonomy movement.
- Do not introduce host-side natural-language parsing. Missing tool calls, ambiguous skill targets, and navigation failures return `blocked` / `escalate`.
- Manual beach verification must show the full chain in `runtime.jsonl`: `npc_delegate_action` -> ingress work item -> skill source/file path -> selected target -> `stardew_navigate_to_tile` -> command completion or blocked reason.
