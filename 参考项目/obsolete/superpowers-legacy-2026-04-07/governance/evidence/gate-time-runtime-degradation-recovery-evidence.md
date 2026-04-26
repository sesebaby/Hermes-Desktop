# Local Runtime Verification Baseline

legacy filename：

- `gate-time-runtime-degradation-recovery-evidence.md`

candidateRevision: f42358ae808a1bfa3ed9a04ccf72429cd5d43d75
schemaVersion: task10-local-runtime-verification-baseline.v1
workflowMode: solo_manual_only
verifiedAt: 2026-04-04T21:27:58.5707057+08:00

current checks:

- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-runtime-local.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-runtime-local.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-cloud-control.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\check-http-health.ps1 -Url http://127.0.0.1:5051/healthz`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\check-http-health.ps1 -Url http://127.0.0.1:7061/healthz`
- `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~HostedNarrativePathTests"`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-stardew-smapi.ps1 -SmapiPath D:\Stardew Valley\Stardew Valley.v1.6.15\StardewModdingAPI.exe -LogPath .\artifacts\logs\smapi-latest.log -TimeoutSec 120 -KillOnTimeout`

observed results:

- runtime local startup passed
- cloud control startup passed
- both `/healthz` endpoints returned `200`
- hosted narrative path tests passed
- SMAPI startup detected the `Superpowers.Stardew.Mod` load marker
- current-head `publish-runtime-local.ps1` passed after removing a stale script-side duplicate-output suppression workaround that no longer matched actual `dotnet publish` behavior

note:

- this file records local runtime verification only
- it does not record gate-time evidence
- it does not require or imply recovery sign-off
