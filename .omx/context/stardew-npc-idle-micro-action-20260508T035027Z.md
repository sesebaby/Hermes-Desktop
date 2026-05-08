## Task Statement

Execute `.omx/plans/星露谷NPC闲置微动作短动画实施计划-20260507.md` under the `$ralph` workflow, implement the full single-path idle micro action pipeline for Stardew NPCs, and commit progress at meaningful verified checkpoints.

## Desired Outcome

- Add `idle_micro_action` support from parent intent through local executor, command service, bridge route, bridge queue, and UI/overlay execution.
- Preserve the explicit single-path contract:
  `NpcLocalActionIntent.IdleMicroAction -> NpcLocalExecutorRunner -> stardew_idle_micro_action -> GameActionType.IdleMicroAction -> StardewCommandService -> StardewBridgeRoutes.ActionIdleMicroAction -> BridgeHttpHost -> BridgeCommandQueue -> displayed/skipped/blocked/interrupted`.
- Keep `private_chat` boundaries intact.
- Add focused regression tests across desktop runtime and bridge projects.
- Build/tests pass, then commit with Lore trailers.

## Known Facts / Evidence

- The user supplied a detailed implementation plan at `.omx/plans/星露谷NPC闲置微动作短动画实施计划-20260507.md`.
- Repository rules require `git status` inspection before edits; branch is `allgameinai`, ahead of `origin/allgameinai` by 2 commits.
- `$ralph` requires context snapshot and canonical `prd-*.md` plus `test-spec-*.md` artifacts before implementation.
- Root and `Desktop/HermesDesktop` AGENTS require Chinese progress updates, `apply_patch` for manual edits, no revert of unrelated work, and verification before completion.
- The desktop subtree has additional code-quality, testing, and security instruction files that apply to the expected C# changes.

## Constraints

- No new dependencies without explicit request.
- Keep the bridge as executor/reporter only; no fallback random action selection.
- Do not introduce a second execution path for idle actions.
- Follow TDD: tests must fail before production code for new behavior.
- Need timely commits, but only after meaningful verification.

## Unknowns / Open Questions

- Which parts of the idle action contract already exist in runtime, command, or bridge layers.
- Whether overlay/bubble abstractions already expose enough channel/kind separation for idle micro actions.
- Which existing tests can be extended versus where new focused test classes are needed.
- Whether current repo state contains user edits in touched files that require merge-aware handling.

## Likely Codebase Touchpoints

- `src/runtime/NpcLocalActionIntent.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/game/core/GameAction.cs`
- `src/games/stardew/StardewCommandContracts.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `src/games/stardew/StardewCommandService.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Ui/NpcOverheadBubbleOverlay.cs`
- `Mods/StardewHermesBridge/Ui/StardewMessageDisplayRouter.cs`
- `Desktop/HermesDesktop.Tests/**`
- `Mods/StardewHermesBridge.Tests/**`
