# E-2026-0002-baseline-and-authority-source-confusion

- id: E-2026-0002
- title: 实现基线、上游对照和产品参考被混写成同一 authority source
- status: active
- updated_at: 2026-04-19
- keywords: [baseline-roles, authority-source, reference-confusion, current-vs-target, source-of-truth]
- trigger_scope: [proposal, design, implementation, review]

## Symptoms

- implementation AI 把真实实现目录、上游只读对照、参考产品结构混成同一个“基线”。
- 文档把目标态宿主契约写得像当前源码已经具备的现状能力。
- review 难以判断某个字段、toolset、identity 装配到底是已有事实还是新增实现。

## Root Cause

- 没有显式区分 `真实实现基线 / 上游对照基线 / 产品结构参考基线`。
- 没有显式区分 `当前事实` 与 `目标态契约`。
- authority source 与 source of truth 的角色说明缺失，导致后续实现把参考材料误当运行时真源。

## Bad Fix Paths

- 用“大家都知道这是参考”代替文档冻结。
- 继续在 proposal/design 里使用含混词，例如“现有 Hermes 已支持逐 NPC 隔离”，但不给现状证据。
- 把只读归档、参考仓库或示例 mod 的结构直接当成本仓库的运行时 authority source。

## Corrective Constraints

- proposal/design 必须单独写清 `真实实现基线 / 上游对照基线 / 产品结构参考基线` 的角色、用途和禁止用途。
- 任何目标态字段、contract、capability 都必须标明“这是宿主新增装配契约，不是当前源码现状能力”。
- review 必须逐项核对 authority source、source of truth、current fact、target contract 是否混写。

## Verification Evidence

- `openspec/changes/hermescraft-stardew-replica-runtime/proposal.md` 中存在 `Baseline Roles`、`Current Hermes Fact Boundary`、`Authoritative NPC Content Sources`。
- `openspec/changes/hermescraft-stardew-replica-runtime/design.md` 中明确 `HermesRuntimeHost` 是职责概念，且说明当前 `hermes-agent` 的事实边界。
- `tasks.md` 中存在针对基线角色和路径来源的专门任务卡。

## Related Files

- openspec/changes/hermescraft-stardew-replica-runtime/proposal.md
- openspec/changes/hermescraft-stardew-replica-runtime/design.md
- openspec/changes/hermescraft-stardew-replica-runtime/tasks.md
- external/hermes-agent源码仅作对照.zip

## Notes

- 近似关联通用卡：`semantic-source-drift`。
- 本仓库特化点：必须同时区分真实实现基线、上游对照基线、产品结构参考基线，以及现状事实与目标态契约。
