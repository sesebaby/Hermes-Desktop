# Local Runtime Baseline

legacy filename：

- `degraded-window-proof.md`

candidateRevision: 89492de8963fca73df21a42578c20c22975a8c39
schemaVersion: task10-local-runtime-baseline.v1
workflowMode: solo_manual_only
verificationTimestamp: 2026-03-29T16:24:50.7741394+08:00
rcgaSemanticsUsed: false

current baseline:

- `dotnet build src/Superpowers.sln` passed
- launcher tests passed
- runtime tests passed
- Stardew mod tests passed
- runtime local health check passed on `http://127.0.0.1:5051/healthz`
- cloud control health check passed on `http://127.0.0.1:7061/healthz`
- Stardew mod publish/sync/load path passed
- hosted narrative path tests passed

current conclusion:

- during the local verification session, no obvious degraded startup state was observed in runtime/cloud startup, health checks, or hosted narrative path verification
- this file is not a gate-time proof document
- this file does not claim anything about `RC / GA`
