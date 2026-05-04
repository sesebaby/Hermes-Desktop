# Stardew Tool Layer Boundary Context

## Task statement

Plan a focused repair for the Stardew NPC prompt/tool layering issue raised in `docs/星露谷NPC分层边界与HermesCraft对比说明.md`.

## Desired outcome

Keep `StardewNpcTools.cs` at the minimum executable tool contract while preserving the movement reliability fix and moving durable world/persona guidance into Stardew skills or persona files where appropriate.

## Known facts / evidence

- `src/games/stardew/StardewNpcTools.cs` currently describes `stardew_move` as using observed `moveCandidate` or `placeCandidate` facts, treating `placeCandidate` as an endpoint candidate, refusing invented coordinates, and telling the model to re-observe or choose a different target after `path_blocked` / `path_unreachable`.
- `skills/gaming/stardew-world/SKILL.md` already carries world-level guidance for `placeCandidate`, endpoint candidates, location meaning, and blocked-path response.
- `skills/gaming/stardew-navigation.md` already carries movement-loop guidance and candidate usage rules.
- `src/game/stardew/personas/haley/default/SOUL.md` and `facts.md` already carry Haley-specific movement preferences and location bias.
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs` currently asserts that `stardew_move` description includes `moveCandidate`, `placeCandidate`, and `endpoint candidate`, and does not promise route-guaranteed movement.
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs` asserts that a `placeCandidate` fact is visible to the model but does not force host-side movement without a tool call.

## Constraints

- Do not undo the just-completed movement reliability fix: route-aware candidate filtering, runtime bounded replan, stable `path_blocked`, and prompt/tool contract changes must remain intact unless tests prove a safer equivalent.
- No new dependencies.
- Keep cross-location behavior unchanged: still unsupported by the bridge, no teleport/controller/warp model.
- Preserve current tests unless expectations are intentionally tightened.
- Use tests before cleanup edits when behavior is not already protected.

## Unknowns / open questions

- Whether the exact phrase "schedule-style destination details" in `StardewNpcTools.cs` is too semantic or still useful as a compact parameter contract.
- Whether duplicated guidance between `stardew-world` and `stardew-navigation` should be reduced now or left as intentional reinforcement.
- Whether docs should be updated to record the final boundary contract after repair.

## Likely codebase touchpoints

- `src/games/stardew/StardewNpcTools.cs`
- `skills/gaming/stardew-world/SKILL.md`
- `skills/gaming/stardew-navigation.md`
- `src/game/stardew/personas/haley/default/SOUL.md`
- `src/game/stardew/personas/haley/default/facts.md`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `docs/星露谷NPC分层边界与HermesCraft对比说明.md`
