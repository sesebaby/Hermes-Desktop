# Context Snapshot: Stardew NPC task continuity closure

UTC timestamp: 2026-05-05T08:00:10Z

## Task statement

Run `$ralplan` for: 借鉴参考项目，实现 `docs/Hermes参考项目功能模块效果差距对比.md` 中的 `P0：任务连续性闭环`.

The user explicitly emphasized: 严格借鉴参考项目方案.

## Desired outcome

Produce consensus planning artifacts that can be handed to `$ralph` for implementation:

- `.omx/plans/prd-stardew-npc-task-continuity-closure.md`
- `.omx/plans/test-spec-stardew-npc-task-continuity-closure.md`

The plan must focus on the missing closure proof and execution behavior, not repeat completed substrate work.

## Current document target

`docs/Hermes参考项目功能模块效果差距对比.md` currently defines P0 as:

`private chat 承诺 -> todo 写入长期 session -> autonomy 读取 active todo -> Stardew 工具推进 -> blocked/failed/completed -> 玩家反馈和 UI 留痕`

Its explicit P0 acceptance points are:

1. private chat commitment creates/updates `todo` under `descriptor.SessionId`, not the private-chat transcript session.
2. private chat transcript remains `${descriptor.SessionId}:private_chat:{conversationId}`.
3. autonomy tick sees active todo created from private chat.
4. autonomy progresses todo using Stardew tools, not narration only.
5. terminal `blocked` / `failed` updates todo reason and attempts player feedback via `stardew_speak` or private chat.

## Current Hermes Desktop facts

- `src/games/stardew/StardewPrivateChatOrchestrator.cs:226-233` runs private chat against transcript session `${descriptor.SessionId}:private_chat:{request.ConversationId}` with `ToolSessionId = descriptor.SessionId`.
- `src/games/stardew/StardewPrivateChatOrchestrator.cs:245-255` already gives Chinese continuity guidance: use `todo` for accepted commitments, `memory` for stable facts, `session_search` for old commitments, and mark `blocked`/`failed` with reason when stuck.
- `src/runtime/NpcAutonomyLoop.cs:121-125` runs autonomy on root `descriptor.SessionId`.
- `src/runtime/NpcAutonomyLoop.cs:201-215` instructs autonomy to inspect current facts and active todo, use `stardew_move`, poll `stardew_task_status`, mark `todo` as `blocked`/`failed` with reason, and tell the player via `stardew_speak` or private chat when possible.
- `src/runtime/AgentCapabilityAssembler.cs:40-48` registers Hermes-native `todo`, `todo_write`, `memory`, and `session_search` for NPC agents.
- `src/tasks/SessionTaskProjectionService.cs:21-31` projects todo tool results into the target task session; `src/tasks/SessionTaskProjectionService.cs:86-91` prefers explicit `TaskSessionId`.
- `src/Core/Agent.cs` now writes tool result `TaskSessionId = session.ToolSessionId ?? session.Id` in the tool-result and permission-denial paths.
- `src/search/SessionSearchIndex.cs:294-318` and `src/transcript/TranscriptStore.cs:91-100` provide `LoadTodoToolResultsByTaskSessionId`.
- `src/runtime/NpcRuntimeTaskHydrator.cs:23-52` replays persisted todo tool results from NPC transcript state DB into instance-level `SessionTodoStore`.
- `src/runtime/NpcRuntimeSupervisor.cs:47-80` hydrates tasks on runtime start / get-or-start; `src/runtime/NpcRuntimeSupervisor.cs:200-216` exposes task view by session id.
- `src/runtime/NpcRuntimeInstance.cs:406-419` returns task view from the instance-level `TodoStore`, so UI does not require a private chat handle first.
- `skills/gaming/stardew-task-continuity/SKILL.md` exists and teaches commitment capture, interruption recovery, active-todo-first autonomy, Stardew tool use, terminal failure marking, and player feedback.
- `skills/gaming/stardew-navigation.md` and `skills/gaming/stardew-world/SKILL.md` already enforce `destinationId`-only movement and prohibit narration-only movement.

## Current tests already covering substrate

- `Desktop/HermesDesktop.Tests/Services/HermesChatServiceTaskLoopTests.cs` covers `ToolSessionId` redirect and persisted `TaskSessionId`.
- `Desktop/HermesDesktop.Tests/Services/TranscriptStoreTests.cs` covers `task_session_id` schema, round-trip, explicit query, safe legacy prefix fallback, and mixed-era restore behavior.
- `Desktop/HermesDesktop.Tests/Services/SessionTaskProjectionServiceTests.cs` covers blocked/failed reasons and explicit task-session projection.
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs:127-141` covers private-chat promise writing to the long-term task session while leaving the private-chat transcript session task view empty.
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs:145-167` covers restart hydration before new private-chat message.
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs:78-87` covers hydration before any handle exists.
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs:486` asserts autonomy prompt must inject the NPC long-term active todo.
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs:164-190` covers Chinese task-continuity guidance inside autonomy decision messages.

## Reference-project evidence and how to apply it

### HermesCraft / hermes-agent principles to copy

- `external/hermescraft-main/README.md:24-32`: each character is a normal Hermes agent with its own home, memory/session history, prompt, body, standard tools, and game-specific interface.
- `external/hermescraft-main/README.md:123-130`: embodied gameplay includes movement/pathfinding and background tasks so agents can keep checking chat while acting.
- `external/hermescraft-main/README.md:140-151`: social systems and persistent identity are separate per agent.
- `external/hermes-agent-main/run_agent.py:1579-1581`: todo is a per-agent/session planning surface.
- `external/hermes-agent-main/run_agent.py:11948-11968` and `external/hermes-agent-main/run_agent.py:12118-12132`: `memory`, `todo`, `skill_manage`, and `session_search` are housekeeping/continuity tools; substantive action tools are different and must remain visible/progress-bearing.
- `external/hermes-agent-main/run_agent.py:3095-3120`: memory/skill learning is agent-led and skill-first, not host-side rewriting of persona or behavior.

Apply these by keeping Stardew NPCs as normal Hermes agents and using Hermes-native `todo` / `memory` / `session_search` plus Stardew action tools. Do not create a second NPC task system or host-side promise detector.

### VillagerAgent principles to borrow carefully

- `参考项目/Mod参考/VillagerAgent/pipeline/task_manager.py:19-27` manages task lists and task graphs with feedback.
- `参考项目/Mod参考/VillagerAgent/pipeline/task_manager.py:175-244` initializes tasks by querying current environment, decomposing into subtasks, and writing graph state.
- `参考项目/Mod参考/VillagerAgent/pipeline/task_manager.py:379-399` updates task status from execution feedback.
- `参考项目/Mod参考/VillagerAgent/pipeline/task_manager.py:462-493` traces success/failure and removes failed tasks from active trace.
- `参考项目/Mod参考/VillagerAgent/type_define/graph.py:205-244` distinguishes open, closed, failed, running, and prerequisite-blocked task nodes.
- `参考项目/Mod参考/VillagerAgent/type_define/graph.py:270-311` exposes current graph status in plain text.

Borrow the feedback-loop shape, not the DAG implementation: Hermes Desktop already uses `todo` as the single task surface. For P0, the equivalent is active todo snapshot + command terminal status + runtime JSONL / task view evidence.

## Missing closure to plan

The substrate is mostly present. The remaining P0 work should prove and harden the full lifecycle:

1. A private-chat commitment creates a long-term todo and transcript remains private-chat scoped.
2. A later autonomy turn receives the active todo as real prompt context, not just generic guidance.
3. The autonomy decision uses Stardew action tools (`stardew_move`, `stardew_task_status`, `stardew_speak` or private chat) to progress or explain the task.
4. Terminal `blocked` / `failed` status from Stardew tools is reflected back into `todo` with a short reason.
5. The NPC attempts player-visible feedback when a player commitment is blocked or failed.
6. The evidence appears in task view and `runtime.jsonl`.
7. Tests use real repository skill assets where prompt/skill boundaries matter.

## Constraints

- Do not reintroduce label/location/tile/facingDirection as public `stardew_move` inputs; keep `destinationId` as the public contract.
- Do not implement true cross-map movement in this P0; bridge should fail unsupported movement honestly.
- Do not add a second task store, NPC-specific promise detector, or host-owned task scheduler.
- Do not write or maintain `SOUL.md`, `MEMORY.md`, `USER.md`, or equivalent personality/memory snapshots.
- Keep game/bridge as fact + event + tool + result provider. The agent chooses intent and tool usage.
- Keep each NPC/save/profile namespace isolated.
- Current worktree has unrelated user changes:
  - `其他资料/ohmycodex使用说明.md`
  - `.omx/state/deep-interview-stardew-npc-task-continuity-runtime-debug.json`
  - `其他资料/test/__pycache__/`

## Likely touchpoints for execution

- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
- `skills/gaming/stardew-task-continuity/SKILL.md`
- `skills/gaming/stardew-social.md`
- `skills/gaming/stardew-navigation.md`

## Planning direction

Favor a narrow P0 closure plan:

- strengthen tests and runtime evidence first;
- only change prompt/tool descriptions or small loop behavior where tests prove a gap;
- preserve existing substrate;
- align with reference projects by reinforcing agent-led task maintenance and host-owned execution truth.
