# Manual Release Record

legacy filename：

- `release-governance-gate-record.md`

candidateRevision: f42358ae808a1bfa3ed9a04ccf72429cd5d43d75
schemaVersion: task10-manual-release-record.v1
workflowMode: solo_manual_only
operator: solo repository owner
decision: local_working_build_verified
decisionTimestamp: 2026-04-04T21:27:58.5707057+08:00
distributionScope: local machine and development workflow only
rcgaApproved: false
externalReleaseApproved: false

current conclusion:

- treat candidate `f42358ae808a1bfa3ed9a04ccf72429cd5d43d75` as a locally verified working build
- do not treat it as `RC`
- do not treat it as `GA`
- do not treat it as commercially approved

working baseline achieved:

- current working tree passed `dotnet build src/Superpowers.sln`
- launcher/runtime/stardew test suites passed
- launcher/runtime/cloud/stardew publish commands passed
- runtime and cloud health checks passed
- `sync-stardew-mod.ps1 -RequireManifest` passed
- `run-stardew-smapi.ps1` passed by detecting the actual load marker emitted during startup
- hosted narrative path tests passed
- fresh `runtime-local` publish inventory still does not reproduce the older `Superpowers.CloudControl.*` package-carryover note
- `publish-runtime-local.ps1` was refreshed to remove a stale duplicate-output suppression assumption that no longer matched current-head `dotnet publish`

remaining manual follow-up items:

- launcher visual proof is already refreshed on current candidate `f42358ae808a1bfa3ed9a04ccf72429cd5d43d75`; remaining same-revision visible-proof work is now Stardew in-host only
- capture real in-host proof for `remote_direct_one_to_one` / `group_chat` windows if those surfaces matter to your next iteration
- reset local Stardew config if you want a clean default-hidden verification pass
