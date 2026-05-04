# E-2026-0504-stardew-move-invented-target-and-fts-punctuation

- id: E-2026-0504-stardew-move-invented-target-and-fts-punctuation
- title: Stardew move tool allowed invented targets and transcript FTS treated plain punctuation as syntax
- status: active
- updated_at: 2026-05-04
- keywords: [stardew, npc-autonomy, stardew_move, moveCandidate, placeCandidate, invalid_target, fts5, punctuation]
- trigger_scope: [stardew, runtime, search, bugfix, diagnostics]

## Symptoms

- Manual Stardew testing reports an NPC "move failed" or "did not move".
- SMAPI logs can show `task_failed` with `path_blocked` or `target_blocked` after an Agent emits `stardew_move`.
- Desktop logs can also show repeated `SessionSearchIndex: FTS5 search failed` with queries whose first line looks harmless, such as `NPC: Haley (haley)`, while the full query contains timestamps, sentences, dots, semicolons, equals signs, or observed facts.
- The visible NPC may be blamed even when the latest SMAPI log shows the failed move belongs to another NPC.

## Root Cause

- `stardew_move` documented that coordinates must come from current `moveCandidate` or `placeCandidate` facts, but the tool did not enforce that contract before submitting to the bridge.
- When the model invented coordinates or moved while no current candidates were available, the SMAPI bridge became the first hard validator and returned path or target failures.
- `SessionSearchIndex.SanitizeQuery` escaped only a narrow set of FTS5 syntax characters; ordinary punctuation such as sentence dots and `route=valid` style facts could still reach `MATCH` and raise FTS5 syntax errors.

## Bad Fix Paths

- Do not fix invented-coordinate movement by adding more prompt wording only; prompts are advisory and the tool boundary must enforce executable contracts.
- Do not treat every `path_blocked` as a SMAPI pathfinding bug; first confirm the target came from a current candidate.
- Do not trust the first line of a multi-line FTS warning; inspect the full query text and the sanitized shape.
- Do not patch candidate generation before proving whether the submitted coordinate was actually offered in the latest observation.

## Corrective Constraints

- `stardew_move` must re-observe current NPC facts and reject targets that do not match a current `moveCandidate` or `placeCandidate`.
- Rejected non-candidate moves should return an observable blocked result, using `invalid_target`, without submitting any bridge command.
- Search queries derived from arbitrary user or observation text must treat punctuation as plain separators unless deliberately constructing a vetted FTS expression.
- Diagnostics must distinguish the NPC named in the user's report from the NPC named in SMAPI `task_*` lines.

## Verification Evidence

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~TranscriptStoreTests.SessionSearchIndex_Search_TreatsSentencePunctuationAsPlainSeparators|FullyQualifiedName~StardewNpcToolFactoryTests.MoveTool_WhenTargetIsNotCurrentObservedCandidate_ReturnsBlockedWithoutSubmittingCommand"` passed.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --no-build --filter FullyQualifiedName~StardewNpcToolFactoryTests` passed, 10/10.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --no-build --filter FullyQualifiedName~TranscriptStoreTests` passed, 38/38.

## Related Files

- `src/games/stardew/StardewNpcTools.cs`
- `src/search/SessionSearchIndex.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Services/TranscriptStoreTests.cs`
