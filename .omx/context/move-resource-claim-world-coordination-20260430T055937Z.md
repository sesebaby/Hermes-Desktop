# Ralplan Context: Move Resource Claim / World Coordination

## Task Statement

Plan `move resource claim / world coordination` for Hermes Desktop's Stardew integration. The user explicitly wants reference-first work against `external/hermescraft-main`, while another AI continues agent loop and private-chat/event work.

## Desired Outcome

Create a source-grounded implementation plan for making Stardew `move` actions safe under autonomous multi-NPC execution:

- prevent overlapping move commands for the same NPC;
- prevent concurrent target/interact tile conflicts;
- preserve idempotency replay behavior;
- release claims when commands reach terminal status or are cancelled/drained;
- keep the work separate from current private-chat/event-loop WIP.

## Reference Evidence

Primary file: `external/hermescraft-main/bot/server.js`.

Reference chain reconstructed:

1. Trigger: `POST /task/ACTION` starts background actions; `POST /task/cancel` cancels the current task.
2. Snapshot: `GET /task` and `briefState()` expose current task state to the agent loop.
3. Prompt / Summary Assembly: `briefState()` includes `task`, `task_done`, `task_error`, and `task_stuck` so the agent can react.
4. Parse / Repair / Normalize: request bodies are parsed centrally by `parseBody`; action parameters are handled by `ACTIONS[actionName]`.
5. Projector / Executor: `currentTask` is the authoritative single active background task; if one is running, a new `/task/ACTION` returns HTTP 409.
6. Authoritative Writeback: the fire-and-forget action updates `currentTask.status` to `done` or `error`; `/task/cancel` sets `cancelled`; watchdog can set `stuck`.
7. Player-visible Surface: `/task`, `/status`, and `briefState()` show active, terminal, or stuck tasks.

Important reference behaviors:

- Single bot body has one `currentTask`.
- New background task is rejected while `currentTask.status === 'running'`.
- Cancel stops pathfinder / digging and marks task cancelled.
- Stuck watchdog clears controls/pathfinder and marks task stuck.
- Reference docs call out old behavior of silently overwriting background tasks as a bug and recommend reject-or-explicit-cancel.

## Current Hermes Evidence

Current files:

- `src/runtime/ResourceClaimRegistry.cs`
- `src/runtime/WorldCoordinationService.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewGameAdapter.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`

Observed current state:

- `ResourceClaimRegistry` already supports per-command claims, same-NPC conflicts, target/interact tile conflicts, idempotency replay, release, and snapshot.
- `WorldCoordinationService` wraps `TryClaimMove(...)` and `ReleaseCommand(...)`.
- `StardewCommandService.SubmitAsync(Move)` currently sends `/task/move` directly to the SMAPI bridge without consulting `WorldCoordinationService`.
- `StardewCommandService.GetStatusAsync(...)` and `CancelAsync(...)` map bridge status, but do not release Desktop-side claims.
- `StardewGameAdapter` constructs `StardewCommandService` without a coordinator.
- The SMAPI bridge `BridgeCommandQueue` has command idempotency and task status, but does not solve cross-NPC tile/resource conflicts at Desktop command-submission time.

## Constraints

- Do not modify private-chat/event-loop WIP in this planning lane.
- No new dependencies.
- Keep behavior reference-faithful in principle: reject overlapping work rather than silently overwriting it.
- Adapt from HermesCraft's single `currentTask` to Stardew's multi-NPC world using resource claims.
- Preserve existing `Speak` and `OpenPrivateChat` paths.
- Worktree is dirty with user/other-AI changes; implementation plan must avoid reverting or overwriting them.

## Unknowns / Decisions

- Whether claim ownership should be Desktop-only, bridge-only, or both. Preliminary recommendation: Desktop-side first, bridge-side optional later as defense-in-depth.
- How to handle bridge accepted command id when Desktop must claim before sending. Preliminary recommendation: use a local pending command id derived from idempotency/trace before bridge send, then update/release via returned bridge command id mapping if necessary.
- Whether idempotency replay after claim release should resend or return previous command status. Preliminary recommendation: preserve existing `ResourceClaimRegistry` behavior while active; after release, allow a new command.

## Likely Touchpoints

- `src/runtime/ResourceClaimRegistry.cs`
- `src/runtime/WorldCoordinationService.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewGameAdapter.cs`
- `src/games/stardew/StardewBridgeDiscovery.cs`
- `Desktop/HermesDesktop.Tests/Runtime/ResourceClaimRegistryTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/WorldCoordinationServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
