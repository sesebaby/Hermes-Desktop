# Client Package Check

candidateRevision: f42358ae808a1bfa3ed9a04ccf72429cd5d43d75
schemaVersion: task10-client-package-check.v1
requiredFieldSetRef: docs/superpowers/plans/2026-03-28-superpowers-m1-parallel-implementation-plan.md#task-10
status: working_evidence_refreshed_manual_baseline
packageArtifact: artifacts/launcher; artifacts/runtime-local; artifacts/cloud-control; artifacts/stardew-mod/Superpowers.Stardew.Mod
exposedFilesReviewed: launcher=[Superpowers.Launcher.deps.json, Superpowers.Launcher.dll, Superpowers.Launcher.exe, Superpowers.Launcher.pdb, Superpowers.Launcher.runtimeconfig.json, Superpowers.Launcher.Supervisor.dll, Superpowers.Launcher.Supervisor.pdb, launch-readiness-verdict.json]; runtime-local=[appsettings.Development.json, appsettings.json, Microsoft.AspNetCore.TestHost.dll, Microsoft.Data.Sqlite.dll, SQLitePCLRaw.batteries_v2.dll, SQLitePCLRaw.core.dll, SQLitePCLRaw.provider.e_sqlite3.dll, Superpowers.Runtime.Contracts.dll, Superpowers.Runtime.Contracts.pdb, Superpowers.Runtime.Local.deps.json, Superpowers.Runtime.Local.dll, Superpowers.Runtime.Local.exe, Superpowers.Runtime.Local.pdb, Superpowers.Runtime.Local.runtimeconfig.json, Superpowers.Runtime.Local.staticwebassets.endpoints.json, Superpowers.Runtime.Stardew.dll, Superpowers.Runtime.Stardew.pdb, web.config, runtimes/**/e_sqlite3 native assets]; cloud-control=[appsettings.Development.json, appsettings.json, Microsoft.AspNetCore.TestHost.dll, Superpowers.CloudControl.deps.json, Superpowers.CloudControl.dll, Superpowers.CloudControl.exe, Superpowers.CloudControl.pdb, Superpowers.CloudControl.runtimeconfig.json, Superpowers.CloudControl.staticwebassets.endpoints.json, Superpowers.Runtime.Contracts.dll, Superpowers.Runtime.Contracts.pdb, web.config]; stardew-mod=[manifest.json, Superpowers.Runtime.Contracts.dll, Superpowers.Runtime.Contracts.pdb, Superpowers.Runtime.Stardew.dll, Superpowers.Runtime.Stardew.pdb, Superpowers.Stardew.Mod.deps.json, Superpowers.Stardew.Mod.dll, Superpowers.Stardew.Mod.pdb]
defaultPlayerExposureSafe: true

状态：

- working evidence baseline

build revision:

- `f42358ae808a1bfa3ed9a04ccf72429cd5d43d75`

current baseline:

- `dotnet build src/Superpowers.sln` passed on current head
- launcher, runtime, cloud, and Stardew mod projects all build successfully
- `publish-launcher.ps1` passed
- `publish-runtime-local.ps1` passed after removing a stale duplicate-output suppression workaround that no longer matched actual `dotnet publish` behavior on current head
- `publish-cloud-control.ps1` passed
- `publish-stardew-mod.ps1` passed
- `sync-stardew-mod.ps1 -RequireManifest` passed after aligning the script to the repo manifest UniqueID
- Stardew mod publish and sync scripts produce the expected external deployment path:
  - `D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\Superpowers.Stardew.Mod`

artifact presence verified:

- launcher artifact:
  - `artifacts/launcher/Superpowers.Launcher.exe`
- runtime-local artifact:
  - `artifacts/runtime-local/Superpowers.Runtime.Local.dll`
- cloud-control artifact:
  - `artifacts/cloud-control/Superpowers.CloudControl.dll`
- stardew mod artifact:
  - `artifacts/stardew-mod/Superpowers.Stardew.Mod/manifest.json`

current risk note:

- `artifacts/runtime-local/` fresh publish inventory for this candidate still contains **no** `Superpowers.CloudControl.*` files, so the older package-exposure note is not reproduced on the current candidate.
- `publish-runtime-local.ps1` no longer relies on the older cross-project `appsettings*.json` identity guard or `/p:ErrorOnDuplicatePublishOutputFiles=false`; if a future duplicate publish output returns, `dotnet publish` will now fail directly and require renewed review.

next manual follow-up:

- if a future duplicate publish output resurfaces, treat the direct `dotnet publish` failure as the guardrail and re-audit before adding any new suppression
- if you later want distribution-grade packaging, re-audit the publish output list for that release candidate
