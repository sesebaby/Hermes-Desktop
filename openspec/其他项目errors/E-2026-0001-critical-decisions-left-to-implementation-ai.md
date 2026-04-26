# E-2026-0001-critical-decisions-left-to-implementation-ai

- id: E-2026-0001
- title: 关键决策被推迟到 implementation AI 临场拍板
- status: active
- updated_at: 2026-04-19
- keywords: [contract-freeze, docs-first, implementation-ai, canonical-contract, task-evidence]
- trigger_scope: [proposal, design, tasks, implementation, review]

## Symptoms

- proposal/design 写了方向和原则，但 implementation 仍要自己决定二进制拓扑、主协议名、资源名、字段语义、reason code、toolset 名称。
- tasks 看起来拆开了，但每张卡缺 `Sources / Evidence / Reproduce / Review / Manual Validation`，导致执行时还是靠猜。
- review 只能在代码写完后才发现“原来这里没冻结”。

## Root Cause

- 设计停留在原则层，没有进入合同层。
- 依赖外部框架或宿主事实的关键决策没有先查一手文档并留出处。
- task 没把关键证据和手动验证步骤写成完成条件，导致 implementation AI 获得过大的自由裁量。

## Bad Fix Paths

- 先写一版代码，再靠 review 逆向补 contract。
- 把 schema、reason code、resource name、carrier freeze 留给 implementation plan 或实现阶段再定。
- 只补“结论”，不补来源、证据、复现方法和手动验证步骤。

## Corrective Constraints

- proposal/design 必须冻结 canonical 二进制名、主协议名、资源名、共享字段语义、reason code、toolset 名称。
- 依赖外部框架、宿主 API、UI、打包、多人、输入、菜单的关键方案必须先查官方或一手文档，并在任务卡写明来源。
- 每张任务卡都必须写明 `Sources / Evidence / Reproduce / Review / Manual Validation`，缺任一项都不算可实施。

## Verification Evidence

- `openspec/changes/hermescraft-stardew-replica-runtime/proposal.md` 中存在 `Frozen Principles`、`Proposal Completeness Boundary`、`Docs-First Decision Rule`。
- `openspec/changes/hermescraft-stardew-replica-runtime/design.md` 中存在 `Docs-First Engineering Rule` 与 `Evidence And Manual Validation Rule`。
- `openspec/changes/hermescraft-stardew-replica-runtime/tasks.md` 顶部存在统一任务卡模板，且任务卡补齐固定字段。

## Related Files

- openspec/project.md
- openspec/changes/hermescraft-stardew-replica-runtime/proposal.md
- openspec/changes/hermescraft-stardew-replica-runtime/design.md
- openspec/changes/hermescraft-stardew-replica-runtime/tasks.md

## Notes

- 对应通用卡：`design-contract-under-specification`。
- 本仓库特化点：把“implementation AI 不能自由发挥的关键决策”明确写成 OpenSpec proposal/design/tasks 的冻结清单。
