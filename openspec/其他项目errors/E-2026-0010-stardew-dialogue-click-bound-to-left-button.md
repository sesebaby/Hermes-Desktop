# E-2026-0010-stardew-dialogue-click-input-semantics-drift

- id: E-2026-0010
- title: Stardew NPC 对话入口输入语义漂移导致 Haley 点击被拒绝
- status: active
- updated_at: 2026-04-30
- keywords: [StardewValley, SMAPI, IsActionButton, IsUseToolButton, MouseLeft, npc_click, DialogueBox, Haley]
- trigger_scope: [implementation, bugfix]

## Symptoms

- SMAPI 日志出现 `npc_click_observed npc=Haley`，但没有后续 `original_dialogue_observed`、`original_dialogue_completed` 或 `custom_dialogue_displayed`。
- 玩家用测试命令传送到 Haley 身边后，点击 Haley 仍然没有进入原版对话或 Hermes 自定义对话。
- 右键/动作键相关输入被记录为 `npc_click_rejected ... error=unsupported_button`。
- 当前失败日志可表现为 `button=MouseLeft;is_action=False;is_use_tool=True;action_bindings=X,右键点击;use_tool_bindings=C,左键点击`，即鼠标点击被 Stardew 归类到 use-tool 绑定。

## Root Cause

- 直接根因：NPC 对话路由曾经在 `MouseLeft` 与 `IsActionButton()` 之间来回收窄，最终只接受 action button；但玩家当前键位下，实际到达桥接层的 Haley 点击是鼠标 use-tool 输入。
- 测试根因：`NpcDialogueClickRouterTests` 没有同时覆盖 `IsActionButton()`、鼠标 use-tool 接受、键盘 use-tool 拒绝三种合同，导致输入语义漂移。
- 诊断根因：看到 `npc_click_observed` 后容易继续调 DialogueBox 状态机，但缺少 `original_dialogue_observed` 已经说明原版对话根本没有打开。

## Bad Fix Paths

- 继续修改 `DialogueBox.transitioning`、`dialogueFinished` 或 pending 状态清理顺序。
- 只比较 `SButton.MouseLeft` 或只接受 `IsActionButton()`，都会把输入语义写死到某一套键位配置。
- 直接接受所有 `IsUseToolButton()` 输入；这会让键盘 `C` 这类工具键也进入 NPC 对话链路。
- 在 `ModEntry` 里堆更多特殊分支，而不是把输入语义固定在路由合同和测试里。

## Corrective Constraints

- NPC 对话正式入口接受 `e.Button.IsActionButton()` 或“鼠标按钮且 `e.Button.IsUseToolButton()`”；不要直接比较单一实体按键，也不要接受非鼠标 use-tool。
- `NpcDialogueClickRouteRequest` 必须携带 `IsActionButton`、`IsUseToolButton`、`IsMouseButton` 三个语义，路由层统一决定是否接受。
- 测试必须覆盖 action button 接受、mouse use-tool 接受、keyboard use-tool 拒绝。
- 看到 `npc_click_observed` 但没有 `original_dialogue_observed` 时，优先检查原版交互是否真的触发，而不是先改 DialogueBox 完成检测。

## Verification Evidence

- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj --filter FullyQualifiedName~NpcDialogueClickRouterTests --no-restore` 通过，7/7。
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj --no-restore` 通过，23/23。
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj --no-restore` 通过，0 warnings / 0 errors。
- `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj --no-restore` 通过，0 warnings / 0 errors。

## Related Files

- `Mods/StardewHermesBridge/ModEntry.cs`
- `Mods/StardewHermesBridge/Dialogue/NpcDialogueClickRouter.cs`
- `Mods/StardewHermesBridge.Tests/NpcDialogueClickRouterTests.cs`
- `openspec/其他项目errors/E-2026-0009-dialogue-detection-debugged-by-guessing-instead-of-logging.md`

## Notes

- 这个错误与 `E-2026-0009` 相邻但不同：`E-2026-0009` 是 DialogueBox 完成检测猜错；本条是原版对话入口按钮语义随键位配置漂移。
- `NpcOriginalDialogueStarter` 会 suppress 触发按钮、收起物品并调用 `npc.checkAction(...)`，因此这类日志优先查路由是否把有效输入放行到 starter。
