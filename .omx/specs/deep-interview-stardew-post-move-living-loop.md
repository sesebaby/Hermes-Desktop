# Deep Interview Spec: Stardew Post-Move Living Loop

## Metadata

- Profile: standard
- Rounds: 6
- Final ambiguity: 11%
- Threshold: 20%
- Context type: brownfield
- Context snapshot: .omx/context/stardew-post-move-living-loop-20260511T052547Z.md
- Supplements: .omx/plans/stardew-tool-orchestration-harness-plan.md

## Intent

Extend the completed Stardew tool orchestration harness from raw action lifecycle closure into a generic NPC living-event loop. The existing harness proves agent tool -> host execution -> terminal fact -> wake -> last_action_result; this supplement makes accepted commitments meaningful to the NPC by tying them to session todo/task continuity and skill/persona guidance.

## Desired Outcome

When an NPC accepts a current-world action request, such as Haley agreeing to go to the beach, the system should treat it as a living event:

1. The agent records the commitment in the NPC session todo/task-continuity surface.
2. The agent submits the real action through the existing lifecycle path.
3. Terminal completion/failure becomes runtime fact through the existing controller/status path.
4. The next autonomy turn sees both the living event and last_action_result.
5. The agent makes an explicit closure choice: mark todo completed, do a new world action, or explicitly wait/no-action with a reason.

The host must not choose the follow-up behavior.

## In Scope

- Private-chat immediate delegated action: accepted player request creates/updates an NPC session todo and submits npc_delegate_action.
- Autonomy-initiated long actions: NPC-created long actions can use the same living-event/todo closure pattern.
- Failure/blocked/timeout: existing todo is marked blocked or failed with a short reason and the agent gets a recovery opportunity.
- Common life guidance in stardew-world and stardew-task-continuity.
- NPC-specific non-hardcoded inclinations in persona assets, first concrete target likely Haley.
- Harness/tests proving the loop without requiring live Stardew/SMAPI.

## Out of Scope / Non-goals

- No second task system or NPC-specific parallel task store.
- No host-hardcoded behavior such as "when Haley reaches beach, speak X".
- Not every move automatically becomes a todo; only accepted commitments / meaningful long actions.
- No local small model real write actions.
- No new external MCP server in this pass.
- No broad persona architecture replacement.
- No forced visible post-arrival action as the success criterion.
- No group NPC task system, economy, schedule planner, or scripted story director.

## Decision Boundaries

Implementation may decide without further approval:

- Concrete todo id/content/status wording.
- Exact wording in stardew-world and stardew-task-continuity.
- Haley persona non-hardcoded life inclination wording.
- Which existing test classes or harness helpers receive coverage.
- Whether closure status uses completed, in_progress, or a minimal compatible state sequence, as long as it remains within existing todo statuses.

Implementation must ask before:

- Adding an external MCP server.
- Introducing a second task/runtime/memory/tool lane.
- Letting the host auto-select post-arrival actions.
- Changing broad persona pack architecture.
- Making every move action create a todo by default.

## Constraints

- Preserve the existing action lifecycle contract from .omx/plans/stardew-tool-orchestration-harness-plan.md.
- Agent decides; host executes and reports facts.
- Real world write actions go through existing host tools/lifecycle.
- Local executor/delegation lane may only do read-only/helper work, not real writes.
- Skills encode behavior/domain guidance; tools carry executable contracts.
- Persona guidance must influence likelihood and style, not hardcode exact actions or dialogue.
- Runtime/log evidence must remain traceable by trace/session/command/work item.

## Acceptance Criteria

1. A private-chat request like "Haley, let's go to the beach now" that the agent accepts produces both:
   - a session todo/living-event record for the commitment, and
   - a delegated move action through the existing lifecycle.
2. When the move reaches terminal completed, Haley's next autonomy turn receives both the relevant active/closing todo context and last_action_result.
3. That next autonomy turn makes an explicit closure choice:
   - calls todo to mark the living event completed, or
   - submits a new world action tool, or
   - explicitly waits/no-actions with a short reason.
4. Failure/blocked/timeout marks the relevant todo blocked or failed with a short factual reason and wakes or schedules the agent according to existing wake policy.
5. Harness/log assertions prove the loop is traceable across private-chat/autonomy/action terminal/runtime fact without starting Stardew.
6. Tests prove the host did not inject destination candidates or hardcoded post-arrival choices.
7. Tests prove local executor still cannot perform real write actions.
8. Skill/persona prompt tests prove common guidance and Haley-specific guidance are injected through existing asset paths.

## Assumptions Resolved

- Move terminal fact alone is insufficient for credible life continuity.
- The right abstraction is a living event backed by existing NPC session todo, not a new task store.
- Visible post-arrival action is allowed but not mandatory.
- Common world guidance and per-NPC persona guidance should both exist.

## Brownfield Evidence

- NpcAutonomyLoop.BuildDecisionMessage already injects last_action_result from LastTerminalCommandStatus.
- TodoTool and SessionTodoStore already support pending/in_progress/completed/blocked/failed/cancelled.
- NpcRuntimeTaskHydrator hydrates NPC todos from transcript tool results.
- stardew-world already provides world-background guidance and compact contract.
- stardew-task-continuity already covers commitments, interruption, status checks, and blocked/failed todo updates.
- Haley persona assets already contain non-hardcoded preferences such as bright/clean/photogenic places.
- Manual logs showed action lifecycle closure works, but living-event closure does not yet guide meaningful post-move behavior.

## Recommended Handoff

Use $ralplan next with this spec as the requirements source of truth:

$plan --consensus --direct .omx/specs/deep-interview-stardew-post-move-living-loop.md

The planning pass should produce a PRD/test spec that supplements, rather than replaces, .omx/plans/stardew-tool-orchestration-harness-plan.md.
