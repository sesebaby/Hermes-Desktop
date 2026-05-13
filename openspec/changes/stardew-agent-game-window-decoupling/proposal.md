## Why

Stardew NPC world actions are still vulnerable to a repeated orchestration mistake: game UI/window/animation lifecycle can block or terminally fail agent-visible work instead of returning status facts for the agent to reason over. The latest private-chat move regression shows this directly: waiting for a reply dialogue lifecycle reused a short generic defer budget, so a valid host task could be blocked while the player was simply reading the game UI.

This change is needed now because the same boundary will otherwise fail again for task windows, trading, gathering, crafting, quest menus, and other future UI-backed actions.

## What Changes

- Stardew agent flow and game execution flow become explicitly decoupled:
  - agent turns may continue to think, speak, query status, and update todo;
  - game-side waits become host task/status facts, not agent-flow locks.
- Stardew world action tools and status tools return text-first, structure-retained results:
  - a short `summary` explains the current state in agent-readable language;
  - `status`, `commandId`, `reason`/`errorCode`, and correlation fields remain for tests, logs, UI, and follow-up status queries.
- Private-chat host task submission waiting is narrowed:
  - waiting for private-chat reply UI lifecycle must not consume generic stale/busy `MaxDeferredIngressAttempts`;
  - recoverable UI waits remain recoverable task/status state;
  - hard blockers still produce terminal blocked facts.
- The single-body execution boundary is made explicit:
  - one NPC has at most one running world action slot;
  - conflicting new world actions return readable `blocked/action_slot_busy` facts immediately;
  - the host does not queue unbounded conflicting work or replace the current action.
- Stardew runtime prompt and skills are updated so the agent knows:
  - windows, animation, menus, and events are status facts, not hidden execution locks;
  - `stardew_task_status` is the continuation tool for long or waiting work;
  - terminal blocked/failed facts require a new agent decision instead of host inference.
- Regression coverage is added for the repeated failure mode:
  - private-chat UI wait does not terminal-block a host task through the generic defer budget;
  - generic busy/stale ingress behavior still blocks according to its configured budget;
  - status/action outputs include an agent-readable summary where required.

Anti-goals for this change:

- Do not implement real craft, trade, gather, or quest window handlers.
- Do not rewrite `IngressWorkItems`, `ActionSlot`, `PendingWorkItem`, or the task system.
- Do not introduce a second queue model.
- Do not let the host infer or choose the agent's next action.
- Do not expand broad status tools into a large all-in-one world scan.
- Do not solve unrelated movement/pathing issues.

## Capabilities

### New Capabilities

None. This change tightens existing Stardew host-task and UI lifecycle contracts instead of introducing a parallel capability.

### Modified Capabilities

- `stardew-host-task-runner`: Host task and status results must be agent-readable facts with short summaries; recoverable game waits must not become agent-flow locks or unrelated defer-budget failures; conflicting new world actions must return observable blocked facts while preserving the single running action slot.
- `stardew-ui-task-lifecycle`: UI/window lifecycle waits must be represented as owned task/status facts or UI lease conflicts, not as generic ingress failures; private-chat reply display/close must not be treated as a reason to terminally block unrelated world-action submission.
- `stardew-orchestration-harness`: Harness coverage must prove the non-blocking agent/game boundary, text-first status summaries, private-chat UI wait regression, and preservation of the existing generic busy/stale blocking behavior.

Capability reuse matrix:

- Agent-visible host task execution already belongs to `stardew-host-task-runner`; this change tightens result shape and wait classification.
- UI lease/menu safety already belongs to `stardew-ui-task-lifecycle`; this change clarifies that recoverable UI waits are task facts, not generic ingress failures.
- Regression proof already belongs to `stardew-orchestration-harness`; this change adds required cases rather than creating a new harness capability.

## Impact

- Code:
  - `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
  - `src/games/stardew/StardewNpcTools.cs`
  - `src/runtime/NpcAutonomyBudget.cs` only if needed for naming or separation, not for increasing generic retry limits
  - `src/Core/Agent.cs` only if status-tool budget or continuation semantics need clarification
- Prompt and skill assets:
  - `skills/gaming/stardew-core/SKILL.md`
  - `skills/gaming/stardew-task-continuity/SKILL.md`
  - `skills/system/stardew-npc-runtime/SYSTEM.md`
- Tests:
  - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
  - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
  - prompt/skill boundary tests where existing harnesses support them
- Specs:
  - delta specs for `stardew-host-task-runner`
  - delta specs for `stardew-ui-task-lifecycle`
  - delta specs for `stardew-orchestration-harness`
- Error memory:
  - update or create an `openspec/errors` entry recording that game UI/window lifecycle waits must not reuse generic agent/ingress retry budgets.
- Dependencies:
  - No new runtime dependencies.
  - No schema-breaking external API dependency is intended.
