# Stardew NPC Autonomy Context Budget Design

Date: 2026-05-05

## Status

Approved for implementation planning.

This document captures the agreed design for reducing Stardew NPC autonomy prompt bloat. It is a design-only artifact; it does not implement the change.

## Problem

Stardew NPC autonomy currently runs a full Hermes agent turn for each NPC autonomy tick. The project already has context management, curated memory limits, session search, transcript recall, and compaction primitives, but the autonomy loop can still send oversized first-turn requests because recent session messages include full tool results and assistant tool-call arguments.

The optimization should not create a second memory system or a host-side NPC brain. It should make the existing architecture behave as intended:

- short live context for each autonomy turn;
- long-term or cross-session facts retrieved through tools;
- durable facts stored in existing memory;
- game state observed through Stardew tools;
- NPC decisions still made by the Agent.

## Verified Current Behavior

The following facts were checked in the repository before this design:

- `src/runtime/NpcAutonomyLoop.cs` runs autonomy through `IAgent.ChatAsync(...)`, so a tick is a full agent turn, not a lightweight rule pass.
- `src/Core/Agent.cs` uses the prepared context for the first tool-capable LLM request, then falls back to accumulated `session.Messages` on later iterations.
- `src/Context/ContextManager.cs` already aims to replace "send all messages" with transcript archive, active session state, recent window, and selective recall.
- `src/Context/TokenBudget.cs` defaults to `maxTokens=8000` and `recentTurnWindow=6`; the recent window can still contain large tool results.
- `src/runtime/NpcRuntimeContextFactory.cs` wires NPC runtime context with `ContextManager`, `TranscriptRecallService`, `HermesMemoryOrchestrator`, `TranscriptMemoryProvider`, and `TurnMemoryCoordinator`.
- `src/search/TurnMemoryCoordinator.cs` prefetches memory context before the first call and can inject recalled context into the current user message.
- `src/Tools/SessionSearchTool.cs` already exposes `session_search` for on-demand recall.
- `src/memory/MemoryManager.cs` already enforces curated memory and user profile character limits.
- `src/compaction/CompactionSystem.cs` contains compaction primitives, but repository search did not show it wired into the NPC autonomy first-request path.
- `src/runtime/AgentCapabilityAssembler.cs` registers built-in tools, including `memory` and `session_search`, for NPC agents.

## Design Goals

1. Keep the default Stardew NPC autonomy first LLM request at or below a 5,000 character target.
2. Reuse existing context, memory, transcript recall, session search, and compaction concepts instead of creating a parallel system.
3. Move long-lived facts out of the always-sent recent window and into existing on-demand recall tools.
4. Prevent repeated or unrelated status-tool calls inside the same autonomy turn.
5. Preserve NPC decision ownership: the host may budget, summarize, and expose tools, but must not decide NPC intent.
6. Keep ordinary desktop chat behavior unchanged unless a later design explicitly broadens the budget policy.

## Non-Goals

- No second memory, persona summary, or NPC-specific shadow memory lane.
- No host-side state machine that decides movement, speech, or social intent for the NPC.
- No transcript persistence rewrite.
- No provider/model/concurrency optimization in this first change.
- No continuation-session split in the first implementation.
- No broad replacement of `ContextManager`.

## Recommended Approach

Use a Stardew autonomy-scoped request budget layer that runs after `ContextManager` and `TurnMemoryCoordinator` prepare the first-call message list, but before the first `CompleteWithToolsAsync(...)`.

This is not a new context system. It is a final budget enforcement step for a specific workload that currently violates the intended "archive + state + recall" architecture.

## Data Flow

1. `NpcAutonomyLoop` builds the autonomy decision message from the latest observation and event facts.
2. `Agent.ChatAsync` persists the user message as it does today.
3. `TurnMemoryCoordinator.PrepareFirstCallAsync(...)` runs existing memory lifecycle and transcript recall.
4. `ContextManager.PrepareContextAsync(...)` builds the normal prepared message list.
5. A Stardew autonomy request budget step trims or summarizes non-protected material in that prepared first-call message list.
6. The first LLM request uses the budgeted message list.
7. Later tool iterations continue through the existing `session.Messages` path, with same-turn status-tool budgets preventing repeated blind status loops.

## 5K Budget Rule

The default target is 5,000 characters for the Stardew NPC autonomy first request.

Use characters, not tokens, for the first implementation because:

- existing memory settings are character based;
- UI already presents memory and user profile limits as character limits;
- current LLM request logs already record `chars`;
- this budget is meant to control payload size and latency symptoms directly.

The 5K limit is a target, not permission to drop critical context. Protected content must survive even if that means the final request exceeds 5K. In that case, log `budget_unmet` with the reason and resulting character count.

Protected content:

- soul/system prompt;
- Stardew system prompt supplement and mandatory skill guidance;
- current autonomy decision message;
- latest observation/event facts carried by the current user message;
- active task context;
- latest tool result group needed to continue a just-started action;
- minimal memory/session recall block already produced by existing recall.

Prunable content:

- old tool result content outside the protected tail;
- duplicate status/tool results from the same request or recent window;
- old assistant tool-call arguments when they are large;
- older recent turns that are not needed for the current autonomy purpose.

## Reuse Existing Features

The implementation should treat existing features as the source of truth:

- Keep `MemoryManager` as the only curated memory limit owner.
- Keep `session_search` and `TranscriptRecallService` as the recall path for older conversation facts.
- Keep `TurnMemoryCoordinator` as the pre-first-call memory lifecycle coordinator.
- Keep `ContextManager` as the context assembly owner.
- Reuse `TokenBudget` counting helpers where useful, but add character accounting because the accepted autonomy budget is character based.
- Reuse the existing Stardew status tool turn budget path in `Agent` rather than adding prompt-only policing.
- Reuse skill files for behavioral guidance instead of adding hidden host decisions.

The existing `CompactionManager` can inform the message-pruning behavior, especially old tool result removal and orphaned tool-result sanitization, but the first implementation should avoid wiring a full LLM summarization pass into every high-frequency NPC tick.

## Tool Use Policy

Stardew skills should explicitly guide the Agent to choose a theme before calling tools.

Each autonomy turn should first identify one purpose:

- respond to player;
- progress an existing todo;
- check long-action progress;
- choose a movement target;
- provide visible feedback;
- wait because the world is blocked or no useful action exists.

Then the Agent should call only tools relevant to that purpose. It should not repeatedly call broad status tools hoping the answer appears in the prompt.

When historical information is missing, the Agent should use the relevant existing tool:

- `session_search` for past sessions or cross-session recall;
- `memory` only for durable fact updates;
- `stardew_recent_activity` for recent in-game agent activity;
- `todo` / `todo_write` for active task state;
- `stardew_task_status` for long action progress.

## Same-Turn Status Budget

The existing `StardewStatusToolTurnBudget` in `Agent` should be strengthened from same-tool-name control to status-class control.

Recommended behavior:

- One ordinary autonomy turn may perform at most one extra broad status-class query after the initial observation.
- Duplicate status-class calls in the same turn should return a synthetic `status_tool_budget_exceeded` result without hitting the bridge.
- Action follow-up tools such as `stardew_task_status` may remain allowed when they are directly tied to an active command, because they are continuation checks rather than exploratory status refreshes.

This should be execution-layer enforcement, not only prompt wording.

## Skill Updates

Update existing Stardew skill assets rather than adding a new global skill lane.

Recommended files:

- `skills/gaming/stardew-core.md`: add the per-turn theme/purpose rule and "short context, tool recall on demand" principle.
- `skills/gaming/stardew-social.md`: strengthen status-tool choice rules and make repeated broad status calls explicitly wrong.
- `skills/gaming/stardew-navigation.md`: make movement flow prefer latest destination facts, then move, then task-status polling only; avoid unrelated background checks during movement.

## Observability

Add structured logs around the autonomy first-request budget step.

Suggested events:

- `autonomy_context_budget_started`
  - `sessionId`
  - `traceId`
  - `messagesBefore`
  - `charsBefore`
  - `toolResultCharsBefore`
  - `recentTurnCharsBefore`
  - `budgetChars`

- `autonomy_context_budget_completed`
  - `messagesAfter`
  - `charsAfter`
  - `prunedToolResults`
  - `prunedDuplicateStatusResults`
  - `truncatedToolCallArgs`
  - `protectedTailMessages`
  - `charsSaved`
  - `budgetMet`
  - `budgetUnmetReason`

These logs should make it possible to prove whether the change reduced first-request size before attributing latency to provider, model, or concurrency.

## Testing

Required tests:

1. A unit test where old long tool results are replaced by short summaries while the protected tail remains intact.
2. A unit test where duplicate status results keep the latest full result and summarize or mark older duplicates.
3. A unit test where old assistant tool-call arguments are truncated without corrupting current tool-call references.
4. A regression test proving the NPC autonomy first request is budgeted before the fake chat client receives it.
5. A same-turn status budget test proving duplicate status-class calls do not execute the real bridge tool.
6. Real asset tests for `skills/gaming/*.md` proving the focus/minimal-tool guidance exists in repo assets, not fixture text.

Acceptance criteria:

- Constructed autonomy sample drops to `<= 5000` chars after budget enforcement when protected content can fit.
- If protected content alone exceeds 5K, the request logs `budget_unmet` and preserves protected content.
- Old/repeated tool results no longer dominate the first request.
- The Agent can still use `session_search`, `memory`, `todo`, and Stardew tools on demand.
- No second memory/persona summary path is introduced.
- Existing curated memory char limits remain authoritative.

## Risks

- Over-pruning could hide the latest tool result needed for correct continuation. Mitigation: protect latest tool result group and active task context.
- Too-strict status budgets could block legitimate action continuation. Mitigation: classify exploratory status separately from command progress checks.
- Prompt guidance could grow the stable system prompt. Mitigation: add concise skill rules and verify prompt size in logs.
- 5K character budget may be too low for some protected states. Mitigation: treat 5K as default target, log budget misses, and only expose a config knob if evidence shows repeated legitimate misses.

## Open Questions For Implementation Planning

- Should the first implementation expose `stardew.npc_autonomy_context_char_budget`, or keep 5K as an internal constant until logs prove a need for configuration?
- Should status-class budgeting include all `stardew_*_status` tools immediately, or start with broad snapshot tools and leave `stardew_task_status` as a continuation exception?
- Should old tool result summaries be deterministic text only, or should they use an existing compaction helper for consistent wording?

The recommended first implementation is internal constant + deterministic pruning + task-status continuation exception.
