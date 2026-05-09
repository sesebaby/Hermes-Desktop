# Stardew Autonomy Candidate Boundary Short Plan

## Scope

Fix the autonomy boundary so `destination[n]` / `nearby[n]` movement candidates are not presented to the parent autonomous decision agent as general observed facts. Keep candidates available to the local executor / Stardew tools where they are execution affordances.

## Minimal Reversible Change

1. Add a tiny filter/helper in `src/runtime/NpcAutonomyLoop.cs` near `BuildDecisionMessage` to omit movement-candidate facts from the parent decision message only:
   - omit facts starting with `destination[` or `nearby[`
   - preserve normal facts such as `location=`, `tile=`, `gameTime=`, events, todos, and action status
   - do not mutate `NpcObservationFactStore` or `GameObservation.Facts`
2. Leave `src/games/stardew/StardewQueryService.cs` unchanged so Bridge/query diagnostics and executor affordances still produce candidates.
3. Update failing autonomy tests to assert parent prompt exclusion while execution path can still consume exact `destinationId` through the local executor/tool lane.

## Files

- `src/runtime/NpcAutonomyLoop.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`

## Risks

- Existing tests currently assert `destinationId` is visible in the parent autonomy prompt; those must be rewritten to match the corrected boundary.
- Over-filtering could hide legitimate non-movement facts if implemented as broad substring matching; use prefix checks only.
- If the local executor currently depends on parent-visible candidates, add/adjust a narrow test before changing behavior, because the intended boundary is parent does not see candidates, executor/tool lane still can.

## Verification

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcAutonomyLoopTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Stardew.StardewAutonomyTickDebugServiceTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Stardew.StardewNpcToolFactoryTests"
```
