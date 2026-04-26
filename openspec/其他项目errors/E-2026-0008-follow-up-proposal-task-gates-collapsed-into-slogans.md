# E-2026-0008-follow-up-proposal-task-gates-collapsed-into-slogans

- id: E-2026-0008
- title: 后续修正提案的任务门禁退化成口号, 重新打开 fake-completion 缺口
- status: active
- updated_at: 2026-04-20
- keywords: [task-gates, fake-completion, evidence-path, review-evidence, slogan-tasks]
- trigger_scope: [proposal, design, tasks, review]

## Symptoms

- follow-up proposal 明明是为了补治理洞, 结果自己的 `tasks.md` 却退化成几条口号式任务。
- 任务卡缺 `Sources / Evidence / Reproduce / Review / Manual Validation` 和固定证据落点。
- 文档写了“要退役旧入口”“要补 fake-completion 检查”, 但没有把验证命令、证据目录、失败条件写死。
- implementation AI 仍然可以口头宣称“已完成”, 而不是拿固定证据过门。

## Root Cause

- 把“方向正确”误当成“治理已经足够硬”。
- 没有承接项目级和 P01 已冻结的任务卡结构要求。
- 没有把旧入口退役、替代入口冻结、负向测试、证据落点做成统一 gate。

## Bad Fix Paths

- 继续用“后面实现时再补证据”安慰自己。
- 继续写“怎么验收”一句话, 但不给固定命令和固定目录。
- 只补 proposal/design, 不补 tasks。

## Corrective Constraints

- follow-up proposal 的 `tasks.md` 也必须继承项目级任务卡硬字段, 不能退化成口号。
- 每张关键任务卡都必须固定 review evidence 和 verification evidence 目录。
- fake-completion gate 必须同时检查:
  - 旧路径是否已退役
  - 新路径是否已命名并冻结
  - 是否出现未命名替代旁路

## Verification Evidence

- `openspec/changes/p02-enforce-hermes-native-single-path/tasks.md` 中每张关键任务卡都有 `Sources / Evidence / Reproduce / Review / Manual Validation`。
- `openspec/changes/p02-enforce-hermes-native-single-path/tasks.md` 中存在固定 `review-evidence/<task-id>/` 与 `verification/<task-id>/` 路径。
- `openspec/changes/p02-enforce-hermes-native-single-path/tasks.md` 中明确写出“未刷新 current-fact inventory 前不得开工”“未冻结唯一替代入口前不得删除旧入口”“任一残留未清零不得勾选完成”。

## Related Files

- openspec/project.md
- openspec/changes/p01-di-yi-jie-duan-ke-shi-shi-ban/tasks.md
- openspec/changes/p02-enforce-hermes-native-single-path/tasks.md

## Notes

- 本卡与 `E-2026-0001` 的区别是:
  - `E-2026-0001` 处理“关键决策被留给 implementation AI”
  - 本卡处理“明明是在补治理洞, 任务门禁却自己先塌了”
