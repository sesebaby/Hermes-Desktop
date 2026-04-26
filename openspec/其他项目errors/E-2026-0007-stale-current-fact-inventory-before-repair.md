# E-2026-0007-stale-current-fact-inventory-before-repair

- id: E-2026-0007
- title: 修正提案没有先刷新当前事实清单, 导致文档继续写错现状
- status: active
- updated_at: 2026-04-20
- keywords: [current-fact-inventory, stale-baseline, repair-proposal, outdated-assertion, review-drift]
- trigger_scope: [proposal, design, tasks, review]

## Symptoms

- review 指出的问题方向对, 但提案仍把已经变化的代码现实写成旧状态。
- 文档继续写“共享单一 `HERMES_HOME`”, 但代码实际上已经落了每 NPC 独立目录。
- 文档继续写“生产链直接读取 `NPC_IDENTITY.md` / `MEMORY_SUMMARY.md`”, 但当前 runner 实际消费的已经是 `HERMES_HOME` 下的 `SOUL.md` / `MEMORY.md` / `USER.md`。

## Root Cause

- 修正提案启动前没有强制刷新 current-fact inventory。
- 评审结论沿用了上一轮检查的旧事实, 没把“哪些已经修正、哪些仍未退役”拆开。
- proposal/design/tasks 没把“先刷新事实清单再开工”写成 blocking gate。

## Bad Fix Paths

- 直接沿用上一轮 review 结论改提案, 不重新读当前代码。
- 只改结论态度, 不改现状事实描述。
- 把“方向对”误当成“现状描述也自动对”。

## Corrective Constraints

- 任何修正型 proposal/design/tasks 开工前, 必须先生成 current-fact inventory。
- current-fact inventory 必须逐项写明当前入口、当前调用方、当前文件位置、替代入口、退役命令和负向测试。
- review 必须先判断“哪些旧问题仍成立, 哪些已部分修正”, 再给提案下结论。

## Verification Evidence

- `openspec/changes/p02-enforce-hermes-native-single-path/verification/0.1/current-fact-inventory.md`
- `openspec/changes/p02-enforce-hermes-native-single-path/design.md` 中不再把“共享单一 `HERMES_HOME`”写成现状。
- `openspec/changes/p02-enforce-hermes-native-single-path/proposal.md` 与 `specs/hermes-native-runtime-governance/spec.md` 中不再把 `NPC_IDENTITY.md` / `MEMORY_SUMMARY.md` 写成当前 production runner 直接读取的权威输入。
- `openspec/changes/p01-di-yi-jie-duan-ke-shi-shi-ban/design.md` 已把当前正式结构收口到 `runtime/SaveState` 与 per-NPC `profile/hermes-home/`，并移除 `runtime/PersonaPacks` / `enabled_mcp_toolsets` 这类陈旧口径。
- `openspec/changes/p01-di-yi-jie-duan-ke-shi-shi-ban/verification/8/手动验证清单.md` 已把“当前代码现状 smoke check”和“未来 `8.7 / MVP-1` gate”分开，避免继续把 reject-only skeleton 写成已可通过的当前事实。
- `docs/superpowers/specs/2026-04-19-hermes-p01-structure-mvp-design.md` 与 `docs/superpowers/plans/2026-04-19-hermes-p01-mvp-proof-implementation-plan.md` 顶部已追加历史文档告警，明确当前实施以 `p01` 正式 OpenSpec 文档为准。

## Related Files

- openspec/changes/p02-enforce-hermes-native-single-path/proposal.md
- openspec/changes/p02-enforce-hermes-native-single-path/design.md
- openspec/changes/p02-enforce-hermes-native-single-path/tasks.md
- adapters/HermesAgentAdapter/HermesAgentClient.cs
- adapters/HermesAgentAdapter/scripts/private_chat_runner.py

## Notes

- 本卡与 `E-2026-0002` 的区别是:
  - `E-2026-0002` 处理“authority source 角色混写”
  - 本卡处理“修正文档时没有先刷新当前事实清单, 继续把旧现状写到新提案里”
