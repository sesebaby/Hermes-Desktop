# E-2026-0012-active-dialogue-menu-misrouted-as-menu-open

- id: E-2026-0012
- title: 已打开的 NPC DialogueBox 被误判成 menu_open 导致后续自定义对话丢失
- status: active
- updated_at: 2026-04-30
- keywords: [StardewValley, SMAPI, ButtonPressed, DialogueBox, menu_open, currentSpeaker, Haley, original_start_failed]
- trigger_scope: [implementation, bugfix]

## Symptoms

- 点击 Haley 后能看到或曾经看到原版对话，但没有出现 Hermes 自定义对话。
- 日志出现 `npc_click_rejected ... error=menu_open;button=MouseLeft;...`，随后再次点击 Haley 出现 `original_start_failed`。
- 玩家当天的原版 NPC 对话被消费后，后续点击不再能进入 `original_dialogue_observed -> custom_dialogue_displayed` 链路。

## Root Cause

- 直接根因：路由层把任何 `Game1.activeClickableMenu != null` 都作为 `menu_open` 拒绝；但 SMAPI `ButtonPressed` 处理时，原版 Haley `DialogueBox` 可能已经是当前 active menu。
- 状态机根因：已经打开的 Haley `DialogueBox` 没有被登记为“原版对话已观察到”，所以关闭后没有 pending flow 可推进到自定义对话。
- 体验根因：`original_start_failed` 曾经是终止路径；如果原版当天对话已经被消费，玩家会一直看不到 Hermes 自定义对话。

## Bad Fix Paths

- 继续扩大 `TryResolveClickedNpc` 的命中范围；这不能修复 active menu 已经是 `DialogueBox` 的情况。
- 把 `menu_open` 一律放行；这会在背包、商店、问答菜单等非目标菜单上误触发 NPC 对话链路。
- 只处理 `original_start_failed`，不处理已打开的原版 `DialogueBox`；这样仍会错过最关键的“原版已启动”证据。

## Corrective Constraints

- `NpcDialogueClickRouteRequest` 必须携带 `IsDialogueBoxOpen` 与 `ActiveDialogueNpcName`，不能只传 `HasActiveMenu`。
- 如果 active menu 是 Haley 的 `DialogueBox`，路由应接受并返回 `accepted_active_dialogue`。
- `ModEntry` 必须把已打开的 Haley `DialogueBox` 转成 `BeginObservedOriginal(...)`，等待关闭后显示自定义对话。
- 如果手动启动原版对话失败，必须记录 `original_start_failed;fallback_custom=true` 并直接显示 Hermes 自定义对话，避免当天对话已被消费后静默失败。

## Verification Evidence

- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj --filter "FullyQualifiedName~NpcDialogueClickRouterTests|FullyQualifiedName~NpcDialogueFlowServiceTests|FullyQualifiedName~RawDialogueDisplayRegressionTests" --no-restore` 通过，14/14。

## Related Files

- `Mods/StardewHermesBridge/ModEntry.cs`
- `Mods/StardewHermesBridge/Dialogue/NpcDialogueClickRouter.cs`
- `Mods/StardewHermesBridge/Dialogue/NpcDialogueFlowService.cs`
- `Mods/StardewHermesBridge.Tests/NpcDialogueClickRouterTests.cs`
- `Mods/StardewHermesBridge.Tests/NpcDialogueFlowServiceTests.cs`
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs`
- `openspec/其他项目errors/E-2026-0010-stardew-dialogue-click-bound-to-left-button.md`

## Notes

- 这条经验依赖 SMAPI 输入文档里的 `SButton`/`Suppress` 语义，以及事件文档里 `ButtonPressed` 暴露当前 cursor/input 状态的事实；不要凭直觉假定事件到达时 active menu 一定还没变。
- 看到 `menu_open` 时必须记录或检查 active menu 类型与 `Game1.currentSpeaker`；仅凭 `menu_open` 不足以判断是无关菜单还是原版 NPC 对话框。
