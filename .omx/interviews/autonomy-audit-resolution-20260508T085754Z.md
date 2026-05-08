# Deep Interview: Autonomy Audit Resolution

Metadata:
- Profile: standard
- Type: brownfield
- Date: 2026-05-08
- Context snapshot: `.omx/context/autonomy-audit-validation-20260508T084753Z.md`
- Output spec: `.omx/specs/deep-interview-autonomy-audit-resolution.md`

## User Intent

用户同意前一轮审查判断，但明确指出：不能把合理问题简单说成“中长期”然后放掉。因为上下文一旦中断，未写成持久化资产的问题会丢失。

## Pressure Pass

原先的“中长期/低优先级”说法风险很高：它描述了执行顺序，却没有把问题本身、判断依据和未来修复约束固化下来。修订后的处理方式是：

- 每个合理问题都必须进入持久化 issue ledger。
- 允许分执行顺序，但不允许省略问题。
- 对“不是 bug 但需要产品决策”的项，也要写清楚为什么不是当前 bug、将来如何决策、验收时怎么防止误改。
- 对“审查报告过时”的项，要写成测试/文档校准任务，而不是简单删除。

## Clarified Requirements

- 本轮仍属于 `$deep-interview`，不直接改源码。
- 要产出可被 `$ralplan`、`$ralph`、`$team` 或后续普通执行直接读取的规格文件。
- 规格文件必须覆盖审查报告中所有合理项：真越界、实现偏硬编码、测试滞后、以及应明确保留的工程边界。
- 不使用“中长期”作为丢弃理由；只允许标注“处理策略”和“依赖条件”。

## Readiness

Ambiguity score: 0.16 <= threshold 0.20

Readiness gates:
- Non-goals: 不在 deep-interview 内改源码；不推翻 parent-intent/local-executor 架构。
- Decision boundaries: Codex 可以整理、分类并持久化问题；具体代码执行需后续计划/执行 lane。
- Pressure pass: 已完成，把“中长期”改为持久化 ledger。

