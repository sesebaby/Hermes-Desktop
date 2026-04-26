# Manual Verification Record

legacy filename：

- `independent-review-record.md`

candidateRevision: 89492de8963fca73df21a42578c20c22975a8c39
schemaVersion: task10-manual-verification-record.v1
workflowMode: solo_manual_only
operator: solo repository owner
verificationTimestamp: 2026-03-29T16:24:50.7741394+08:00
verificationResult: working_baseline_verified_not_release_approved
manualVerificationOnly: true
externalApprovalPresent: false

verifiedArtifactList:

- docs/superpowers/governance/current-phase-boundary.md
- docs/superpowers/governance/evidence-review-index.md
- docs/superpowers/governance/evidence/launcher-player-visible-check.md
- docs/superpowers/governance/evidence/stardew-player-visible-check.md
- docs/superpowers/governance/evidence/stardew-m1-core-hand-check.md
- docs/superpowers/governance/evidence/stardew-implementation-only-channel-hand-check.md
- docs/superpowers/governance/evidence/prompt-asset-protection.md
- docs/superpowers/governance/evidence/client-package-check.md
- docs/superpowers/governance/evidence/degraded-window-proof.md
- docs/superpowers/governance/evidence/gate-time-runtime-degradation-recovery-evidence.md
- docs/superpowers/governance/evidence/release-governance-gate-record.md

repo-local verification baseline:

- `dotnet build src/Superpowers.sln`
- `dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj`
- `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj`
- `dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-launcher.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-runtime-local.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-cloud-control.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-stardew-mod.ps1 -ProjectPath .\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj -OutputDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-runtime-local.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-cloud-control.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\check-http-health.ps1 -Url http://127.0.0.1:5051/healthz`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\check-http-health.ps1 -Url http://127.0.0.1:7061/healthz`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\sync-stardew-mod.ps1 -RequireManifest -SourceDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod -TargetDir D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\Superpowers.Stardew.Mod`
- `powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-stardew-smapi.ps1 -SmapiPath D:\Stardew Valley\Stardew Valley.v1.6.15\StardewModdingAPI.exe -LogPath .\artifacts\logs\smapi-latest.log -TimeoutSec 120 -KillOnTimeout`
- `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~HostedNarrativePathTests"`

current conclusion:

- the current candidate is locally runnable and manually checked
- this record is not an approval artifact
- this record does not claim independent review
- this record does not upgrade the build to `RC`, `GA`, or external release readiness

known limitations:

- launcher visual proof was not freshly recaptured for the current candidate
- Stardew visual proof still depends on a controlled local override session
- implementation-only channel windows still lack fresh in-host screenshot proof
