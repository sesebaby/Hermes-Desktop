# E-2026-0011-raw-dialogue-text-passed-as-translation-key

- id: E-2026-0011
- title: 原始 NPC 对话文本误传给 translation-key overload 导致 ContentLoadException
- status: active
- updated_at: 2026-04-30
- keywords: [StardewValley, SMAPI, Dialogue, DrawDialogue, translationKey, ContentLoadException, raw-dialogue-text]
- trigger_scope: [implementation, bugfix]

## Symptoms

- Haley 原版对话完成后，Hermes 自定义对话触发时，SMAPI 在 `GameLoop.UpdateTicked` 事件中报错。
- 堆栈显示 `LocalizedContentManager.parseStringPath(...)` 尝试解析 `Oh... you're still here? Fine. Just don't make this weird.`。
- 崩溃点为 `Game1.DrawDialogue(NPC npc, String translationKey)`，说明原始文本被当成 content translation key 处理。

## Root Cause

- 直接根因：代码调用 `Game1.DrawDialogue(npc, rawText)` 显示 Hermes 生成/硬编码的原始文本；该 overload 的第二个参数是 translation key，不是 raw dialogue text。
- 参考根因：Stardew 1.6 C# 对话创建方式区分 translation key 与 custom text。官方 modding docs 示例使用 `new Dialogue(npc, null, "Some arbitrary text to show as-is")` 表示原始文本。
- 复用根因：debug `/action/speak` 路径也使用了同一错误 overload，虽然本次堆栈发生在正式点击续接路径。

## Bad Fix Paths

- 修改 `DialogueBox.transitioning`、`dialogueFinished` 或 pending 状态机。这次堆栈已经证明对话完成检测进入了自定义显示阶段，问题不在完成检测。
- 把原始文本包装成伪 translation key 或写入某个临时 asset key；这会把动态对话路径重新耦合到 content asset。
- 只修 `ModEntry`，漏掉 `/action/speak` debug path，导致同类 crash 以后从 debug 入口重现。

## Corrective Constraints

- 原始 NPC 对话文本必须创建 `Dialogue` 对象再显示，不得调用 `Game1.DrawDialogue(NPC,string)`。
- 共享一个 raw dialogue renderer，正式点击续接和 debug speak 都走同一入口。
- 回归测试必须扫描并拒绝 `Game1.DrawDialogue(npc,` 这种 raw text 易误用形式。
- 修复后重新跑 mod 测试和 mod build；真实游戏里需要再验证 Haley 原版对话结束后自定义对话能显示。

## Verification Evidence

- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj --filter FullyQualifiedName~RawDialogueDisplayRegressionTests --no-restore` 通过，1/1。
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj --no-restore` 通过，0 warnings / 0 errors。

## Related Files

- `Mods/StardewHermesBridge/ModEntry.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Dialogue/NpcRawDialogueRenderer.cs`
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs`
- `openspec/其他项目errors/E-2026-0009-dialogue-detection-debugged-by-guessing-instead-of-logging.md`
- `openspec/其他项目errors/E-2026-0010-stardew-dialogue-click-bound-to-left-button.md`

## Notes

- `E-2026-0009` 和 `E-2026-0010` 都提示不要把这类问题重新归因到 DialogueBox 状态机或输入路由；本条发生在更后面的“显示自定义文本”阶段。
- 官方 Stardew modding docs 的 1.6 迁移说明明确区分 translation key 与 custom text：translation key 用 `new Dialogue(npc, "Strings\\...")`，原始文本用 `new Dialogue(npc, null, "...")`。
