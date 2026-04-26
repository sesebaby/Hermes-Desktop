# Support Ticket And Diagnostic Bundle Contract

状态：

- active design baseline

owner：

- launcher product owner
- support operations owner

用途：

- 用大白话写死：支持与帮助页怎么提交问题，问题包怎么收，失败时怎么退回到“只提交文字说明”。

固定回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`
- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`

ticket owner：

- `Launcher` 负责前台收集和提交
- `Cloud` 负责工单正本和回执

submission states：

1. `Draft`
2. `CollectingBundle`
3. `Submitting`
4. `Submitted`
5. `Failed`
6. `TextOnlyFallback`

当前代码现实：

- `src/Superpowers.Launcher/ViewModels/SupportViewModel.cs`
  - 已经露出：
    - `Draft`
    - `Submitting`
    - `Submitted`
    - `Failed`

request minima：

- `playerHandle`
- `subject`
- `message`
- `diagnosticFailureClass`
- `recoveryEntryRef`
- `launcherSurfaceRef`

bundle minima：

- `ticketDraftRef`
- `launchReadinessVerdictRef`
- `runtimePreflightRef`（条件适用）
- `runtimeHealthRef`（条件适用）
- `failureClass`
- `recentTraceRefs`
- `redactionState`

提交死规则：

1. 玩家文字说明永远允许提交
2. 问题包是增强项，不是阻断项
3. 问题包整理失败时，必须自动退回：
   - `TextOnlyFallback`
4. 不允许因为问题包失败，就把整次求助吞掉

失败分类固定包括：

1. `submission_failed`
2. `diagnostic_export_failed`
3. `diagnostic_redaction_failed`
4. `ticket_receipt_missing`

玩家可见 copy 死规则：

1. 有问题包失败时，要明确告诉玩家：
   - “只提交文字说明也可以”
2. 有回执时必须显示：
   - `ticketReceiptId`
3. 没回执时不能假装提交成功

绝对禁止：

1. 不允许把内部 trace 明文全量直接暴露给玩家
2. 不允许没有清理就把敏感内容打包上传
3. 不允许支持提交失败却还显示“已提交”

update trigger：

- 提交状态变化
- bundle 最小字段变化
- 失败分类变化
