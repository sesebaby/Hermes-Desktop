# Prompt Asset Protection Evidence

candidateRevision: 89492de8963fca73df21a42578c20c22975a8c39
schemaVersion: task10-prompt-asset-protection.v1
requiredFieldSetRef: docs/superpowers/plans/2026-03-28-superpowers-m1-parallel-implementation-plan.md#task-10
status: working_evidence_refreshed_manual_baseline
anchorDocs: docs/superpowers/governance/client-exposure-threat-model.md; docs/superpowers/plans/2026-03-28-superpowers-m1-parallel-implementation-plan.md
sixLayerMapping: layer1 source reference dirs remain repo-only (`参考项目/Mod参考/recovered_mod/**`, `temp_decompile/**`) -> layer2 fresh publish inventory recorded for launcher/runtime-local/cloud-control/stardew-mod -> layer3 keyword scan over artifact roots found no sensitive prompt/reference keywords -> layer4 filename scan found no checkpoint/telemetry/sidecar/trace/export/dump files -> layer5 repo-local logs scan found no prompt/persona/world-rule/orchestration keywords -> layer6 current workflow stops at local manual verification
redactionCheck: fresh artifact keyword scan and filename scan passed for candidate `89492de8963fca73df21a42578c20c22975a8c39`; no extra approval flow is attached in the current solo/manual workflow
defaultExposureBlocked: true

状态：

- working evidence baseline

build revision:

- `89492de8963fca73df21a42578c20c22975a8c39`

repo-local baseline:

- `docs/superpowers/governance/client-exposure-threat-model.md`
- recovered prompt/reference assets remain in research/reference directories:
  - `参考项目/Mod参考/recovered_mod/**`
  - `temp_decompile/**`
- they are not part of normal launcher/runtime/cloud/mod publish outputs in the current working tree

next manual follow-up:

- if you later add checkpoint / telemetry / sidecar / diagnostic export paths, rerun this scan
- if you later move to external distribution, rebuild a stricter export/redaction checklist
