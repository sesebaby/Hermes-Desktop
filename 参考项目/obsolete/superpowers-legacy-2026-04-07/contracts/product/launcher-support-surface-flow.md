# Launcher Support Surface Flow

状态：

- active design baseline

owner：

- launcher product owner

用途：

- 用大白话写死：`支持与帮助` 页里，玩家从看到问题、提交、回看回执，到继续恢复的页面流。

固定回链：

- `docs/superpowers/contracts/product/support-ticket-and-diagnostic-bundle-contract.md`
- `docs/superpowers/contracts/product/support-closure-state-machine.md`
- `docs/superpowers/contracts/product/repair-update-recheck-state-machine.md`

页面流：

1. `问题摘要区`
   - 最近 failureClass
   - recoveryEntryRef
   - 主修复按钮
2. `提交问题区`
   - 玩家描述
   - 问题包状态
   - 提交按钮
3. `回执区`
   - ticketReceiptId
   - 当前工单状态
4. `常见问题区`
   - 常见恢复路径
5. `只提交文字说明区`
   - 问题包失败时的退路

阅读顺序死规则：

1. 先看问题摘要
2. 再看能不能先修
3. 修不掉再去提交问题
4. 提交后优先看回执

当前代码现实绑定：

- `src/Superpowers.Launcher/ViewModels/SupportViewModel.cs`
  - 现在已经有提交和失败文案壳

绝对禁止：

1. 不允许把 FAQ 放在最前面，盖住当前问题
2. 不允许回执区只有“成功”两个字，没有 receipt
3. 不允许问题包失败后没有“只提交文字说明”的退路

update trigger：

- 支持页区块变化
- 阅读顺序变化
- 回执/FAQ/退路位置变化
