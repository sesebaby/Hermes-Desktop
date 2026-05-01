# E-2026-0015-stardew-tinyfont-chinese-renders-stars

- id: E-2026-0015
- title: Stardew `Game1.tinyFont` 渲染中文 footer 为星号
- status: active
- updated_at: 2026-05-01
- keywords: [StardewValley, SMAPI, Game1.tinyFont, Game1.smallFont, Chinese, glyph, footer, private_chat]
- trigger_scope: [implementation, bugfix, ui-polish]

## Symptoms

- 私聊窗口右下角 footer 文案 `回车发送    ESC取消` 在游戏中显示为星号。
- 同一窗口里的标题、prompt、placeholder 等中文正常显示。
- 代码层面 footer 使用 `Game1.tinyFont`，其他中文区域使用 `Game1.smallFont`。

## Root Cause

- `Game1.tinyFont` 对中文 glyph 支持不完整，会把中文字符渲染为 `*`。
- 该 footer 虽然只是辅助提示，但包含中文字符，不能使用 tiny font。
- 其他中文正常是因为它们通过 `Game1.smallFont` 绘制。

## Bad Fix Paths

- 改快捷键提交/取消语义来避开中文显示问题。
- 把文案改回英文；这违背用户“窗口里不要出现英文”的要求。
- 继续使用 `Game1.tinyFont`，只调整位置、颜色或透明度。
- 使用特殊分隔符或全角符号；这可能引入新的 glyph 缺失。

## Corrective Constraints

- Stardew 中文 UI 文案优先使用 `Game1.smallFont` 或已实机验证支持中文的字体。
- 含中文的 footer / hint / helper text 不要使用 `Game1.tinyFont`。
- 回归测试应锁住中文 footer 使用 `Game1.smallFont.MeasureString(...)` 和 `Game1.smallFont` 绘制路径。
- 保持 UI 字体修复在菜单绘制层，不改变 bridge 事件、提交/取消行为或 Desktop/core 状态机。

## Verification Evidence

- 先新增 `PrivateChatInputUsesLocalizedPolishedTextAndInsetCloseButton` 断言 footer 使用 `Game1.smallFont.MeasureString(hint)`，测试失败，失败点为当前源码仍使用 `Game1.tinyFont.MeasureString(hint)`。
- 修复后同一过滤测试通过，1/1。
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug` 通过，48/48。
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug -p:EnableModDeploy=false -p:EnableModZip=false` 通过，0 warnings / 0 errors。

## Related Files

- `Mods/StardewHermesBridge/Ui/PrivateChatInputMenu.cs`
- `Mods/StardewHermesBridge.Tests/RawDialogueDisplayRegressionTests.cs`

## Notes

- 这个问题和 `E-2026-0014` 不同：`E-2026-0014` 是可见菜单承载问题，本条是已可见菜单内部的字体 glyph 支持问题。
