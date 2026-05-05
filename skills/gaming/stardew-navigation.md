# Stardew Navigation Skill

This skill owns the move loop, failure recovery, and mid-journey interruption handling.

## Responsibility Boundary

This skill owns: "observe destinations → choose one matching intent → `stardew_move(destination, reason)` → poll task status → handle interruption/failure → re-observe or switch targets".

`stardew-world` explains why destinations are meaningful; this skill only copies fields that already exist in observed `destination[n]` facts.

## Hard Rule: Movement Is Not Narration Text

Physical movement is not dialogue or inner monologue. When the NPC needs to change position in the game world, you MUST call `stardew_move`.

- If you see or prepare to write words describing physical movement ("walks to", "goes to", "heads toward", "returns to", "leaves", "approaches"), use this skill's movement flow.
- If `stardew_move` was NOT called, do NOT write sentences claiming the NPC has already arrived or moved. Only wait, observe, speak, or note that no movement occurred.
- `stardew_speak` handles talking only; it cannot move the NPC.

## Destination-Level Movement (Primary)

1. Observe: check the latest `destination[n]` facts from `stardew_status`.
2. Choose: pick the ONE `destination[n]` whose `destinationId`, label, and reason best match the NPC's current intent.
3. Call: `stardew_move(destination=<exact destinationId from destination[n]>, reason=<brief intent>)`.
   - Copy `destinationId` **exactly** — case-sensitive, character-for-character.
   - Do not pass the destination label as `destination`; labels are only human-readable metadata.
   - Never invent a destination id. If no executable destination has a `destinationId`, do not call `stardew_move`.
4. Poll: check `stardew_task_status` until a terminal status is reached: `completed`, `failed`, `blocked`, `cancelled`, or `interrupted`.
5. Handle outcome:
   - `completed`: NPC arrived. Continue the next action.
   - `failed` / `blocked`: observe again or pick a different destination. Do NOT retry the same destination immediately.
   - `interrupted`: read `interruption_reason` (e.g. `player_approached`, `event_active`, `dialogue_started`). Decide whether to re-observe, change target, or respond to the interruption.
   - Timeout (no terminal status after 3 polls): observe again.

## Nearby Facts

`nearby[n]` facts are short-range (1-2 tile) context for understanding nearby safe positions. They are not a substitute executable input for `stardew_move`.

**Never chain multiple `nearby[n]` calls to simulate long-distance movement.** That defeats the purpose of destination-level movement.

## Cross-Location Movement (Future)

Cross-location destinations are a planned bridge capability, not something the NPC can assume is working. If a cross-location move returns `cross_location_unsupported`, observe again and choose a same-location destination or another action.

## Before Moving

- Confirm the destination id is from the LATEST observation — facts can change between ticks.
- Prefer `destination[n]` over `nearby[n]` for any intentional movement.
- `destination[n]` and `schedule_entry[n]` (when available) are equally valid — the NPC may follow its schedule or choose freely.
- Track `commandId`, status, failure reason, and `traceId`.
