# Stardew Navigation Skill

Phase 1 navigation only covers the minimum real `move` loop.

Before moving:

- Confirm the target location and tile came from the latest `moveCandidate[n]` or `placeCandidate[n]` bridge facts, an already-resolved target from the existing private-chat exception path, or a known safe test target.
- Prefer `placeCandidate[n]` when choosing a meaningful in-world destination, because it includes a label, tags, and a reason tied to Stardew context.
- Treat `placeCandidate[n]` like a lightweight Stardew schedule entry: a semantic place, exact location/tile, reason, optional facing direction, and optional end behavior note.
- Use `moveCandidate[n]` for short repositioning when no meaningful place candidate fits the NPC's current intent.
- When the latest observation includes a safe candidate and moving fits the NPC's current intent, call `stardew_move` with that candidate's `locationName`, `x`, `y`, `reason`, and optional `facingDirection` instead of only saying you will move.
- Send movement through the `move` task contract, not a direct HTTP call.
- Track `commandId`, status, failure reason, and `traceId`.
- If the target is blocked, unreachable, stale, or conflicts with another claim, report the reason and re-observe.

Do not invent advanced `goto`, `follow`, `interact`, collect, chest, craft, or farming behavior in Phase 1.
Do not treat ordinary host events or player movement as movement instructions; outside private chat, they are context only.
