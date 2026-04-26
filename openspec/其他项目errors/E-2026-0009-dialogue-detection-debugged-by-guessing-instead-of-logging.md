# E-2026-0009-dialogue-detection-debugged-by-guessing-instead-of-logging

- id: E-2026-0009
- title: 对话续接流程反复修不生效——根因靠猜而非靠日志定位
- status: active
- updated_at: 2026-04-26
- keywords: [dialogue-detection, transitioning, MenuChanged, OnUpdateTicked, 日志先行, StardewValley, DialogueBox, SMAPI]
- trigger_scope: [implementation, bugfix]

## Symptoms

- 玩家与 Haley 对话后，原版对话正常显示（如"海莉不想理你"），但 mod 的续接问题"要继续和 Haley 聊聊吗？"从未弹出
- SMAPI 日志中 mod 加载正常、HostBridge 启动正常、存档加载正常，但对话结束后无任何 mod 日志
- 连续两轮修复均无效，每次都是"看起来逻辑对但实际不生效"

## Root Cause

**直接根因**：`IsCompletedHaleyDialogue` 中 `!dialogueBox.transitioning` 条件阻断了续接流程。DialogueBox 关闭后 `dialogueFinished=True` 但 `transitioning` 仍为 `True`（关闭动画尚未结束），导致完成检测失败，`pendingHaleyDialogue` 被静默丢弃。

**治理根因**：连续两轮修复都是"读代码 → 推测根因 → 改代码"，没有先加日志验证推测。第一轮加 `pendingHaleyDialogueConfirmed` 和第二轮计划重写为状态轮询，都是基于错误假设的无效修复。

**为什么推测错了**：
- 第一轮假设：关闭后的 DialogueBox 的 `characterDialogue` 被游戏引擎清除 → 实际日志显示 `charDialogue=Haley` 始终可用
- 第二轮假设：`OnMenuChanged` 第三个 if 块被其他菜单触发清除了状态 → 实际日志显示从未触发该分支
- 真正原因：`transitioning` 在对话内容完成后仍为 True，这是一个 Stardew Valley DialogueBox 的已知行为（关闭动画独立于内容完成状态）

## Bad Fix Paths

1. **不加日志直接改逻辑**：读代码推测根因 → 改条件/加字段 → 构建部署 → 手动测试 → 仍然不生效 → 重复。这是本次浪费两轮的根本原因。
2. **在 `MenuChanged` 里做复杂状态机**：事件快照不可靠，应该用 `Game1.activeClickableMenu` 实时状态。
3. **同时改多个条件**：如果第一轮同时改了 `pendingHaleyDialogueConfirmed` 和去掉 `transitioning`，就无法确定哪个是真正有效的修复。

## Corrective Constraints

1. **日志先行**：任何"看起来应该生效但不生效"的 bug，第一步永远是加诊断日志，不是改逻辑。日志要覆盖每个分支的进入/退出条件和关键变量值。
2. **一次只改一个条件**：日志定位到具体根因后，只修那一个条件，验证通过后再考虑其他优化。
3. **DialogueBox 完成检测只用 `dialogueFinished`**：`transitioning` 是动画状态，与对话内容是否完成无关。Stardew Valley 的 DialogueBox 在内容完成后 `transitioning` 可能仍为 True（关闭过渡动画）。
4. **`Monitor?.Log` 而非 `Monitor.Log`**：测试环境中 `Monitor` 为 null，用 `?.` 避免 NRE。

## Verification Evidence

- SMAPI 日志 `SMAPI-latest.txt` 第 203 行：`IsCompletedHaleyDialogue 返回 false, transitioning=True, dialogueFinished=True`
- 移除 `!dialogueBox.transitioning` 条件后，续接问题正常弹出
- StardewMod.Tests 全部通过（26/26 net6.0 + 26/26 net8.0）

## Related Files

- `mods/StardewMod/ModEntry.cs` — `IsCompletedHaleyDialogue` 方法（line 273-278），`OnMenuChanged`（line 62-83），`OnUpdateTicked`（line 85-104）
- `C:\Users\Administrator\AppData\Roaming\StardewValley\ErrorLogs\SMAPI-latest.txt` — 诊断日志输出
- `参考项目/参考文档/星露谷物语/星露谷自定义窗口实现参考.md` — 第 4.5 节生命周期规则（虽然本次问题方向与文档警告相反）

## Notes

- 参考文档警告"要确认 transition 已结束"，但本次问题是**不应该**检查 transition。文档警告的是"不要在 transition 期间抢菜单"，而我们的场景是"对话已完成，等 transition 结束再弹续接"——后者不需要等 transition。
- 这个 bug 从提案完成到真正可用，经历了 3 轮修复。如果第一轮就先加日志，一轮就够了。
