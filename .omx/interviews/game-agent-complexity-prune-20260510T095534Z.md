# Deep Interview Transcript: Game Agent Complexity Prune

- Profile: standard
- Context type: brownfield
- Final ambiguity: 19%
- Threshold: 20%
- Context snapshot: `.omx/context/game-agent-complexity-prune-20260510T095534Z.md`
- Source plan: `.omx/plans/Hermes-Desktop复杂度精简计划（详细审查版）.md`

## Clarified Intent

The project should be understood as a multi-game agent platform with Stardew Valley as the first concrete scenario, not as a strict Stardew-only minimum runtime. Cleanup should make the game-agent platform core clearer.

## Non-goal

Do not touch core NPC runtime.

This freezes the NPC runtime main path and its required substrate: NPC state, memory, soul/persona, transcript, session todo, skills, MCP tool surface, cron/schedule, Stardew autonomy/private-chat/bridge execution boundaries, and game-host execution responsibilities.

## Decision Boundary

For clearly irrelevant or unwired candidates, default to direct deletion. However, "currently unwired" is not sufficient for deletion if the module has a strong future fit as multi-game platform substrate.

If a module fits cross-game world knowledge, orchestration, cognition, observability, or game-authoring needs, the preferred action is to reshape it into a game-semantic platform module rather than delete it.

## Pressure Pass

The initial deletion rule was stress-tested against `Wiki`: although currently weakly wired, it could become a world-setting/location/rules knowledge layer for future games. User chose to keep such modules. Therefore deletion requires both weak current wiring and weak future platform fit.

## Agreed Review Format

Each item should be reviewed as:

- Decision: keep, delete, reshape as platform module, or defer
- Reason
- Core NPC runtime risk check

## Rounds

1. Boundary: user chose "multi-game agent platform with Stardew as first scenario."
2. Non-goal: user specified "do not touch core NPC runtime."
3. Outcome: user chose "make the game agent platform core clearer."
4. Decision priority: user chose direct deletion for clearly irrelevant or unwired candidates.
5. Pressure pass: user chose to keep currently unwired modules if they can become future platform substrate.
6. Output format: user accepted the fixed item review format.
