# E-2026-0004-runtime-identity-isolation-faked-by-shared-state

- id: E-2026-0004
- title: 用共享状态假装多 NPC 运行时隔离
- status: active
- updated_at: 2026-04-19
- keywords: [identity-isolation, shared-state, npc-runtime, home-profile, lifecycle-owner]
- trigger_scope: [design, spec, implementation, review]

## Symptoms

- 多个 NPC 看起来有独立身份，实际共用 `home/profile`、session、memory 或 persona 注入根。
- 文档说支持多 NPC，但没有写清 `save_id / npc_id / home_id / profile_id / session_scope / memory_scope / runtime_instance_id` 的语义和 owner。
- 场景切换、休眠恢复后，NPC 身份连续性依赖共享状态兜底，导致串线或人格污染。

## Root Cause

- 没有把逐 NPC runtime identity isolation 写成宿主必须新增维护的契约。
- 没有给 identity、session、memory、runtime instance 指定稳定持久化键和 lifecycle owner。
- 把共享 `HERMES_HOME`、单 `identity_file` 或单 runtime 入口误当成现成的多 NPC 隔离能力。

## Bad Fix Paths

- 先用共享状态加标签区分 NPC，等后面再拆。
- 只写“支持多 NPC”，不写隔离键、恢复规则、owner、热冷态边界。
- 把统一宿主管理误写成统一人格装配或共享长期记忆。

## Corrective Constraints

- 每个 NPC 必须冻结独立 `home/profile`、`session_scope`、`memory_scope`、`persona` 装配和 `runtime_instance_id` 语义。
- `save_id + npc_id` 到 runtime identity 的映射必须由宿主维护，并写清创建、恢复、休眠、清理 owner。
- review 必须验证“单宿主统一托管”不等于“共享人格 / 共享记忆 / 共享运行态”。

## Verification Evidence

- `specs/npc-runtime-isolation/spec.md` 中存在逐 NPC 隔离、稳定持久化键、独立 prompt/soul/home/body binding、禁止共享记忆。
- `proposal.md` 中存在 `Current Hermes Fact Boundary` 与 `Multi-NPC Runtime Model`。
- `design.md` 中存在 `NpcRuntimeRegistry`、`ActivationManager`、逐 NPC 装配与热冷态说明。

## Related Files

- openspec/changes/hermescraft-stardew-replica-runtime/proposal.md
- openspec/changes/hermescraft-stardew-replica-runtime/design.md
- openspec/changes/hermescraft-stardew-replica-runtime/specs/npc-runtime-isolation/spec.md

## Notes

- 关联通用卡：`runtime-session-state-split`、`lifecycle-owner-missing`。
- 本仓库特化点：强调“一个宿主装配层 + 多逻辑 agent”前提下，仍必须保持逐 NPC 的稳定隔离和身份连续性。
