# PRD: Stardew NPC Idle Micro Action

## Problem

Stardew NPCs currently lack lightweight, semantically meaningful idle micro actions that can surface while the player is watching. The new behavior must not create a second execution path, must not let bridge/UI layers make decisions, and must not bleed into speech or private chat contracts.

## Goal

Implement a single-path `idle_micro_action` feature that lets the parent decide whether to try an idle micro action, the local executor choose from a fixed whitelist, and the host/bridge safely execute or reject it with consistent result reporting.

## Non-Goals

- No speech bubbles with free text.
- No private chat/open chat routing.
- No walking, destination changes, or cross-map navigation.
- No raw frame IDs, raw animation IDs, or other low-level animation control in model-facing contracts.
- No bridge-side random fallback behavior.

## Functional Requirements

1. Add `NpcLocalActionKind.IdleMicroAction` and a whitelist-only payload in `NpcLocalActionIntent`.
2. Parent intent may only emit the allowed idle micro action fields.
3. Local executor must expose only `stardew_idle_micro_action` for this action type.
4. Missing delegation/local executor capability must block rather than fallback.
5. Add `GameActionType.IdleMicroAction`.
6. Add a dedicated Stardew route and DTOs for idle micro actions.
7. `BridgeHttpHost` must deserialize and enqueue idle micro actions through one route only.
8. `BridgeCommandQueue` must enforce visibility, idle-state, cooldown, scene/menu/event, movement, warp/remove, and animation allowlist gates.
9. Results must normalize to:
   - `completed/displayed`
   - `completed/skipped`
   - `blocked/blocked`
   - `interrupted/interrupted`
10. Overlay/UI execution must use explicit channel/kind separation so idle micro actions never emit `private_chat_reply_closed`.

## Acceptance Criteria

1. The end-to-end path is the only implemented path for idle micro actions.
2. Unsupported or disallowed idle actions are blocked, not silently remapped.
3. `private_chat` behaviors remain intact and isolated.
4. Focused runtime/command/bridge/overlay regression tests exist and pass.
5. Relevant builds/tests pass before completion and before each commit claim.

## Risks

- Overlay/channel changes may regress private chat display behavior.
- Bridge lifecycle changes may conflict with existing move command interruption logic.
- Existing parser or executor abstractions may require broader updates than the plan initially suggests.

## Validation

- Focused MSTest runs for runtime intent/executor/command service coverage.
- Focused bridge tests for route handling, queue lifecycle, overlay regression, and interruption cleanup.
- At minimum one build plus related full project test runs before final completion.
