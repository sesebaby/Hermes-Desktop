# Stardew Navigation Skill

Phase 1 navigation only covers the minimum real `move` loop.

Before moving:

- Confirm the target location and tile came from bridge facts, player command parsing, or a known safe test target.
- Send movement through the `move` task contract, not a direct HTTP call.
- Track `commandId`, status, failure reason, and `traceId`.
- If the target is blocked, unreachable, stale, or conflicts with another claim, report the reason and re-observe.

Do not invent advanced `goto`, `follow`, `interact`, collect, chest, craft, or farming behavior in Phase 1.
