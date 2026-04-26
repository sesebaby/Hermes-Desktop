# E-2026-0005-player-visible-carrier-freeze-missing

- id: E-2026-0005
- title: 玩家可见承载面和验证路径没有提前冻结
- status: active
- updated_at: 2026-04-19
- keywords: [player-visible-surface, carrier-freeze, ui-validation, manual-validation, presentation-governance]
- trigger_scope: [proposal, design, tasks, review]

## Symptoms

- 文档写了功能目标，却没写玩家第一次从哪里看到、失败时在哪里暴露、主承载面和备选承载面分别是什么。
- UI 决策用“或”“兼容呈现面”“后续再看”这类模糊表述，把真正选择权推回 implementation AI。
- tasks 没写手动验证步骤，导致 review 只能看日志、截图或代码，不知道玩家实际体验怎么验。

## Root Cause

- 把前台承载面当成实现细节或最后润色，而不是 proposal/design 级硬约束。
- 没把 player-visible carrier、modal/non-modal 行为、失败暴露面、manual validation 写成合同。
- UI 决策依赖框架或宿主事实，却没有同步留下文档来源与验证路径。

## Bad Fix Paths

- 用临时调试面板、后台日志、trace 替代正式前台反馈。
- 只写一个“可能的 UI”草图，不冻结主承载面和备选承载面。
- 说“等实现时看看哪个更顺手”，把交互主链交给 implementation AI 现场选择。

## Corrective Constraints

- proposal/design 必须写清玩家首次可见承载面、备选承载面、阻塞性、失败暴露面、阶段归属。
- 任何会反向影响输入焦点、多人体验、动作表达或宿主边界的 UI 决策，都必须写出影响说明和来源文档。
- tasks 必须写出用户参与的手动验证步骤、预期现象和通过标准，不能只写“人工验证”。

## Verification Evidence

- `proposal.md` 中存在 `Presentation-Layer Surface Matrix`、`Deferred Expression Layer Direction`、`UI 不是实现期附属问题`、`Chinese-Only First Release Rule`。
- `design.md` 中存在 `Desktop Information Architecture`、`Desktop UI Principles`、`SMAPI-Grounded Input And UI Decision`、`Evidence And Manual Validation Rule`。
- `tasks.md` 中为 UI、输入、多人、分发相关任务写明 `Manual Validation` 内容。

## Related Files

- openspec/changes/hermescraft-stardew-replica-runtime/proposal.md
- openspec/changes/hermescraft-stardew-replica-runtime/design.md
- openspec/changes/hermescraft-stardew-replica-runtime/tasks.md
- openspec/changes/hermescraft-stardew-replica-runtime/specs/social-scene-routing/spec.md
- openspec/changes/hermescraft-stardew-replica-runtime/specs/desktop-multi-game-shell/spec.md

## Notes

- 关联通用卡：`frontend-surface-omission`、`frontend-ui-entry-chain-omission`。
- 本仓库特化点：强调主承载面/备选承载面冻结、玩家可见失败暴露面，以及任务卡必须附手动验证步骤。
