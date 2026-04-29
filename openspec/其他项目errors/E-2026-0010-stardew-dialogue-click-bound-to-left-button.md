# E-2026-0010-stardew-dialogue-click-bound-to-left-button

- id: E-2026-0010
- title: Stardew NPC 对话入口误绑到左键导致只观察点击不触发原版对话
- status: active
- updated_at: 2026-04-29
- keywords: [StardewValley, SMAPI, IsActionButton, MouseLeft, npc_click, DialogueBox, Haley]
- trigger_scope: [implementation, bugfix]

## Symptoms

- SMAPI 日志出现 `npc_click_observed npc=Haley`，但没有后续 `original_dialogue_observed`、`original_dialogue_completed` 或 `custom_dialogue_displayed`。
- 玩家用测试命令传送到 Haley 身边后，点击 Haley 仍然没有进入原版对话或 Hermes 自定义对话。
- 右键/动作键相关输入被记录为 `npc_click_rejected ... error=unsupported_button`。

## Root Cause

- 直接根因：NPC 对话路由把有效入口写成 `SButton.MouseLeft`，但 Stardew 的 NPC 交互应使用 SMAPI 的 `SButton.IsActionButton()` 语义。
- 测试根因：`NpcDialogueClickRouterTests` 把“左键 Haley 接受”作为合同，导致错误输入语义被单元测试固化。
- 诊断根因：看到 `npc_click_observed` 后容易继续调 DialogueBox 状态机，但缺少 `original_dialogue_observed` 已经说明原版对话根本没有打开。

## Bad Fix Paths

- 继续修改 `DialogueBox.transitioning`、`dialogueFinished` 或 pending 状态清理顺序。
- 让左键也进入正式对话链路来“看起来有反应”，这会继续偏离 Stardew 原生交互语义。
- 在 `ModEntry` 里堆更多特殊分支，而不是把输入语义固定在路由合同和测试里。

## Corrective Constraints

- NPC 对话正式入口必须使用 `e.Button.IsActionButton()`，不要直接比较 `SButton.MouseLeft`。
- 测试名称和参数必须表达 action button，而不是 primary/left click。
- 看到 `npc_click_observed` 但没有 `original_dialogue_observed` 时，优先检查原版交互是否真的触发，而不是先改 DialogueBox 完成检测。

## Verification Evidence

- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj --filter FullyQualifiedName~NpcDialogueClickRouterTests --no-restore` 通过，5/5。
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj --no-restore` 通过，21/21。
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj --no-restore` 通过，0 warnings / 0 errors。

## Related Files

- `Mods/StardewHermesBridge/ModEntry.cs`
- `Mods/StardewHermesBridge/Dialogue/NpcDialogueClickRouter.cs`
- `Mods/StardewHermesBridge.Tests/NpcDialogueClickRouterTests.cs`
- `openspec/其他项目errors/E-2026-0009-dialogue-detection-debugged-by-guessing-instead-of-logging.md`

## Notes

- 这个错误与 `E-2026-0009` 相邻但不同：`E-2026-0009` 是 DialogueBox 完成检测猜错；本条是原版对话入口按钮语义猜错。
