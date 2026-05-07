# E-2026-0507-route-probe-status-logged-as-error

- id: E-2026-0507-route-probe-status-logged-as-error
- title: Route-probe success was logged as a blocked error detail
- status: active
- updated_at: 2026-05-07
- keywords: [stardew, bridge, routeProbe, route_found, diagnostics, task_blocked, cross_location_unsupported]
- trigger_scope: [stardew, bridge, developer-page, manual-qa, diagnostics]

## Symptoms

- Manual QA after clicking `计算去海边路线` showed `task_move_enqueued`, followed by `task_blocked ... result=blocked error=route_found`.
- The terse SMAPI summary made a successful cross-location route probe look like an error.
- Full route details such as current location, Beach target, next warp segment, and route length were only available by knowing bridge internals or querying `/task/status` while the bridge was still online.

## Root Cause

- The cross-location branch correctly records `RouteProbeData` and intentionally blocks execution with `cross_location_unsupported`.
- The SMAPI monitor log reused the generic `error` field and wrote only `routeProbe.Status`, so a success value (`route_found`) appeared as an error string.
- The log did not include enough route-probe context for a manual tester to distinguish "route calculated but not executed yet" from "movement failed".

## Bad Fix Paths

- Do not treat `blocked` as failure for cross-location probes; the block is the current execution boundary.
- Do not rename `route_found` or `cross_location_unsupported` just to make the one-line log easier to read.
- Do not implement warp-based cross-location movement as a shortcut to make the status become `completed`.
- Do not require manual testers to hit `/task/status` before deciding whether the route probe worked.

## Corrective Constraints

- Cross-location route-probe logs must include a human-readable `routeProbeStatus`, mode, origin tile, target tile, next segment, route step count, and failure code.
- The route-probe summary must preserve the existing command status semantics: `blocked` still means "route found, execution not implemented" when the status is `route_found`.
- Keep route-probe details in task status data as the structured source of truth; SMAPI monitor logs are a short diagnostic summary.

## Verification Evidence

- Added a failing-then-passing bridge regression test for cross-location route-probe log detail formatting.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMoveCommandQueueRegressionTests.FormatRouteProbeLogDetail_WithCrossLocationRoute_IncludesHumanReadableProbeSummary"` passed.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~BridgeMoveCommandQueueRegressionTests|FullyQualifiedName~BridgeMovementPathProbeTests"` passed, 25/25.
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` passed, 94/94.
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug` passed with 0 warnings and 0 errors.

## Related Files

- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge.Tests/BridgeMoveCommandQueueRegressionTests.cs`
