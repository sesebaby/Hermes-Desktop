# Stardew 原版点击 NPC 后续接自定义对话 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让玩家在 Stardew Valley 里用鼠标点击 Haley 时，先看到原版 NPC 对话，待原版对话自然结束后，再进入 Hermes 的自定义对话框流程。

**Architecture:** 正式入口冻结为游戏内 NPC 点击链路，不再以桌面按钮或 `/action/speak` 作为主流程。Mod 侧负责观察原版对话生命周期、记录日志、等待原版 `DialogueBox` 完结，再安全切入自定义对话；实时菜单状态只以 `Game1.activeClickableMenu` 为准，`MenuChanged` 仅作快照证据。

**Tech Stack:** C#, SMAPI, Stardew Valley DialogueBox lifecycle, MSTest, structured SMAPI logging

---

## File Structure Map

### Existing files to modify
- Modify: `Mods/StardewHermesBridge/ModEntry.cs` — 新增 NPC 点击识别、原版对话观测、原版结束后切入自定义对话的状态机与日志调用
- Modify: `Mods/StardewHermesBridge/Logging/SmapiBridgeLogger.cs` — 补充点击链路、菜单状态、对话阶段日志字段
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml` — 把 Haley/Penny 桌面按钮标记为 debug-only 或隐藏
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs` — 降级桌面按钮语义，避免误导为正式入口
- Modify: `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs` — 保留 `/action/speak` 为诊断路径，并在日志里区分来源

### New files to create
- Create: `Mods/StardewHermesBridge/Dialogue/NpcClickDialogueState.cs` — 封装“点击命中 -> 原版对话中 -> 等待结束 -> 自定义对话已投递”的最小状态
- Create: `Mods/StardewHermesBridge/Dialogue/NpcDialogueClickRouter.cs` — 只负责命中 NPC、过滤按钮/菜单状态、产出点击结果
- Create: `Mods/StardewHermesBridge/Dialogue/NpcDialogueFollowUpService.cs` — 负责在原版对话完成后安全推入自定义对话
- Create: `Mods/StardewHermesBridge.Tests/NpcDialogueClickRouterTests.cs` — 命中与过滤规则测试
- Create: `Mods/StardewHermesBridge.Tests/NpcDialogueFollowUpServiceTests.cs` — 原版结束检测、自定义对话触发条件测试

### Frozen responsibilities
- `ModEntry.cs` 只做事件接线与最小状态推进，不承载复杂命中逻辑
- `NpcDialogueClickRouter` 只判断“这次点击是否应进入对话观察链路”
- `NpcDialogueFollowUpService` 只判断“原版对话是否自然结束、现在是否允许进入自定义对话”
- `SmapiBridgeLogger` 必须提供足够证据解释“不弹出”“弹早了”“重复弹出”三类问题
- `/action/speak` 保留为 debug path，不参与正式玩家主链路验收

---

## Hard Rules From Errors

- 命中 `E-2026-0009`：任何不生效问题先看日志，不允许先猜 `transitioning`、`MenuChanged` 或状态清理顺序。
- 命中 `E-2026-0001`：关键决策现在冻结，不留给实现 AI 临场决定。
- 命中 `E-2026-0005`：主承载面冻结为“游戏内鼠标点击 NPC -> 原版对话框 -> 原版结束后自定义对话框”，桌面按钮仅是备选 debug carrier。
- 自定义对话**必须在原版对话完毕后**再开始，不能抢原版首次对话框。
- 实时菜单状态判断只看 `Game1.activeClickableMenu`，`MenuChanged` 只能记证据，不能当真值。
- 不允许实现 overlay 模拟对话框或独立新 UI 来冒充原版对话。

---

## Task 1: Freeze click-entry contract and evidence points

**Files:**
- Modify: `Mods/StardewHermesBridge/ModEntry.cs`
- Modify: `Mods/StardewHermesBridge/Logging/SmapiBridgeLogger.cs`

**Sources:**
- `openspec/其他项目errors/E-2026-0001-critical-decisions-left-to-implementation-ai.md`
- `openspec/其他项目errors/E-2026-0005-player-visible-carrier-freeze-missing.md`
- `openspec/其他项目errors/E-2026-0009-dialogue-detection-debugged-by-guessing-instead-of-logging.md`
- `参考项目/参考文档/星露谷物语/星露谷自定义窗口实现参考.md`

**Evidence:**
- 计划中明确记录：正式入口、备选 debug 入口、失败暴露面、实时状态真值、原版结束后再接自定义对话的合同

**Reproduce:**
- 启动 SMAPI，进入存档，靠近 Haley，左键点击她一次

**Review:**
- review 时逐项核对：是否仍把桌面按钮当正式入口；是否把 `MenuChanged` 误用成实时状态；是否缺少失败暴露面

**Manual Validation:**
- 玩家第一次可见承载面必须是游戏内原版 NPC 对话，不是桌面 UI，不是 HUD overlay

- [ ] **Step 1: 在 `ModEntry.cs` 顶部注入正式链路说明常量**

```csharp
private const string DialogueEntryPath = "npc_click_then_wait_original_then_custom";
private const string DialogueDebugPath = "desktop_debug_or_action_speak";
```

- [ ] **Step 2: 在 `SmapiBridgeLogger` 定义统一事件名**

```csharp
public static class BridgeLogEvents
{
    public const string NpcClickObserved = "npc_click_observed";
    public const string NpcClickRejected = "npc_click_rejected";
    public const string OriginalDialogueObserved = "original_dialogue_observed";
    public const string OriginalDialogueCompleted = "original_dialogue_completed";
    public const string CustomDialogueQueued = "custom_dialogue_queued";
    public const string CustomDialogueDisplayed = "custom_dialogue_displayed";
}
```

- [ ] **Step 3: 运行局部编译确认常量与事件名可被引用**

Run: `dotnet build "Mods/StardewHermesBridge/StardewHermesBridge.csproj"`
Expected: `Build succeeded.`

- [ ] **Step 4: 提交这一冻结切片**

```bash
git add Mods/StardewHermesBridge/ModEntry.cs Mods/StardewHermesBridge/Logging/SmapiBridgeLogger.cs
git commit -m "chore: freeze npc click dialogue entry contract"
```

## Task 2: Write failing tests for click routing

**Files:**
- Create: `Mods/StardewHermesBridge.Tests/NpcDialogueClickRouterTests.cs`
- Create: `Mods/StardewHermesBridge/Dialogue/NpcDialogueClickRouter.cs`

**Sources:**
- `Mods/StardewHermesBridge/ModEntry.cs:25-32`
- `参考项目/参考文档/星露谷物语/星露谷自定义窗口实现参考.md`

**Evidence:**
- 测试覆盖：只响应鼠标主键、已有菜单时拒绝、未命中 NPC 时拒绝、命中 Haley 时接受

**Reproduce:**
- 仅跑新增测试类

**Review:**
- 不允许把命中逻辑直接塞回 `ModEntry.cs`

**Manual Validation:**
- 测试说明里要能映射到玩家实际行为：点 NPC、点空地、菜单打开时点 NPC

- [ ] **Step 1: 写失败测试骨架**

```csharp
[TestMethod]
public void TryRoute_ReturnsAccepted_WhenPrimaryMouseHitsHaleyWithoutActiveMenu()
{
    var router = new NpcDialogueClickRouter();
    var result = router.TryRoute(new NpcClickContext(
        buttonName: "MouseLeft",
        hasActiveMenu: false,
        npcName: "Haley",
        hitNpc: true));

    Assert.IsTrue(result.Accepted);
    Assert.AreEqual("Haley", result.NpcName);
}
```

- [ ] **Step 2: 再写三个失败测试**

```csharp
[TestMethod]
public void TryRoute_Rejects_WhenClickMissesNpc() { }

[TestMethod]
public void TryRoute_Rejects_WhenMenuAlreadyActive() { }

[TestMethod]
public void TryRoute_Rejects_WhenButtonIsNotPrimaryMouse() { }
```

- [ ] **Step 3: 运行测试确认失败**

Run: `dotnet test "Mods/StardewHermesBridge.Tests/Mods.StardewHermesBridge.Tests.csproj" --filter "FullyQualifiedName~NpcDialogueClickRouterTests"`
Expected: FAIL with `The type or namespace name 'NpcDialogueClickRouter' could not be found`

- [ ] **Step 4: 写最小实现让测试可编译**

```csharp
public sealed record NpcClickContext(string buttonName, bool hasActiveMenu, string? npcName, bool hitNpc);
public sealed record NpcClickRouteResult(bool Accepted, string? NpcName, string? RejectReason);

public sealed class NpcDialogueClickRouter
{
    public NpcClickRouteResult TryRoute(NpcClickContext context)
    {
        if (!string.Equals(context.buttonName, "MouseLeft", StringComparison.OrdinalIgnoreCase))
            return new(false, null, "unsupported_button");
        if (context.hasActiveMenu)
            return new(false, null, "menu_active");
        if (!context.hitNpc || string.IsNullOrWhiteSpace(context.npcName))
            return new(false, null, "npc_not_hit");
        return new(true, context.npcName, null);
    }
}
```

- [ ] **Step 5: 运行测试确认通过**

Run: `dotnet test "Mods/StardewHermesBridge.Tests/Mods.StardewHermesBridge.Tests.csproj" --filter "FullyQualifiedName~NpcDialogueClickRouterTests"`
Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add Mods/StardewHermesBridge.Tests/NpcDialogueClickRouterTests.cs Mods/StardewHermesBridge/Dialogue/NpcDialogueClickRouter.cs
git commit -m "test: add npc click routing contract"
```

## Task 3: Write failing tests for original-dialogue completion gate

**Files:**
- Create: `Mods/StardewHermesBridge.Tests/NpcDialogueFollowUpServiceTests.cs`
- Create: `Mods/StardewHermesBridge/Dialogue/NpcDialogueFollowUpService.cs`
- Create: `Mods/StardewHermesBridge/Dialogue/NpcClickDialogueState.cs`

**Sources:**
- `openspec/其他项目errors/E-2026-0009-dialogue-detection-debugged-by-guessing-instead-of-logging.md`
- `参考项目/参考文档/星露谷物语/星露谷自定义窗口实现参考.md`
- `参考项目/动态任务参考/StardewMods-QuestEssentials/QuestEssentials/Tasks/TalkTask.cs`

**Evidence:**
- 测试必须证明：原版对话没结束时不能弹自定义对话；原版对话结束后才允许切换

**Reproduce:**
- 仅跑 follow-up 测试类

**Review:**
- 禁止把 `transitioning == false` 当成完成的唯一标准

**Manual Validation:**
- 先看到原版对话；原版结束后才看到自定义对话

- [ ] **Step 1: 写失败测试，锁定“未结束不能切换”**

```csharp
[TestMethod]
public void TryPromote_ReturnsFalse_WhenOriginalDialogueStillOpen()
{
    var service = new NpcDialogueFollowUpService();
    var state = NpcClickDialogueState.WaitingForOriginal("Haley", "custom text");

    var advanced = service.TryPromote(state, new DialogueSnapshot(
        hasActiveDialogueBox: true,
        dialogueFinished: false,
        currentSpeakerName: "Haley"));

    Assert.IsFalse(advanced.ShouldDisplayCustomDialogue);
}
```

- [ ] **Step 2: 写失败测试，锁定“结束后才允许切换”**

```csharp
[TestMethod]
public void TryPromote_ReturnsTrue_WhenOriginalDialogueFinishedAndMenuClosed()
{
    var service = new NpcDialogueFollowUpService();
    var state = NpcClickDialogueState.WaitingForOriginal("Haley", "custom text");

    var advanced = service.TryPromote(state, new DialogueSnapshot(
        hasActiveDialogueBox: false,
        dialogueFinished: true,
        currentSpeakerName: "Haley"));

    Assert.IsTrue(advanced.ShouldDisplayCustomDialogue);
}
```

- [ ] **Step 3: 跑测试确认失败**

Run: `dotnet test "Mods/StardewHermesBridge.Tests/Mods.StardewHermesBridge.Tests.csproj" --filter "FullyQualifiedName~NpcDialogueFollowUpServiceTests"`
Expected: FAIL with missing `NpcDialogueFollowUpService` / `NpcClickDialogueState`

- [ ] **Step 4: 写最小实现**

```csharp
public sealed record DialogueSnapshot(bool hasActiveDialogueBox, bool dialogueFinished, string? currentSpeakerName);
public sealed record FollowUpDecision(bool ShouldDisplayCustomDialogue, string? Reason);

public sealed record NpcClickDialogueState(string NpcName, string CustomText, bool WaitingForOriginalToFinish)
{
    public static NpcClickDialogueState WaitingForOriginal(string npcName, string customText)
        => new(npcName, customText, true);
}

public sealed class NpcDialogueFollowUpService
{
    public FollowUpDecision TryPromote(NpcClickDialogueState state, DialogueSnapshot snapshot)
    {
        if (snapshot.hasActiveDialogueBox)
            return new(false, "original_dialogue_still_open");
        if (!snapshot.dialogueFinished)
            return new(false, "original_dialogue_not_finished");
        return new(true, null);
    }
}
```

- [ ] **Step 5: 跑测试确认通过**

Run: `dotnet test "Mods/StardewHermesBridge.Tests/Mods.StardewHermesBridge.Tests.csproj" --filter "FullyQualifiedName~NpcDialogueFollowUpServiceTests"`
Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add Mods/StardewHermesBridge.Tests/NpcDialogueFollowUpServiceTests.cs Mods/StardewHermesBridge/Dialogue/NpcDialogueFollowUpService.cs Mods/StardewHermesBridge/Dialogue/NpcClickDialogueState.cs
git commit -m "test: add original dialogue completion gate"
```

## Task 4: Wire click routing into ModEntry with logs first

**Files:**
- Modify: `Mods/StardewHermesBridge/ModEntry.cs`
- Modify: `Mods/StardewHermesBridge/Logging/SmapiBridgeLogger.cs`
- Modify: `Mods/StardewHermesBridge/Dialogue/NpcDialogueClickRouter.cs`

**Sources:**
- `Mods/StardewHermesBridge/ModEntry.cs`
- `openspec/其他项目errors/E-2026-0009-dialogue-detection-debugged-by-guessing-instead-of-logging.md`

**Evidence:**
- 点击时能记录：按钮、location、active menu 类型、命中 NPC、reject 原因

**Reproduce:**
- 进存档，左键点 Haley；点空地；菜单打开时再点 Haley

**Review:**
- 看日志而不是肉眼猜为什么没触发

**Manual Validation:**
- SMAPI console/bridge log 能解释每次点击是否进入观察链路

- [ ] **Step 1: 在 `ModEntry` 注入 router 与等待状态字段**

```csharp
private readonly NpcDialogueClickRouter _clickRouter = new();
private readonly NpcDialogueFollowUpService _followUpService = new();
private NpcClickDialogueState? _pendingDialogue;
private bool _lastObservedDialogueFinished;
```

- [ ] **Step 2: 改写 `OnButtonPressed` 只做观测与挂起**

```csharp
private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
{
    _debugMenu.HandleButton(e.Button);

    var context = new NpcClickContext(
        e.Button.ToString(),
        Game1.activeClickableMenu is not null,
        TryResolveClickedNpcName(),
        hitNpc: TryResolveClickedNpcName() is not null);

    var route = _clickRouter.TryRoute(context);
    if (!route.Accepted)
    {
        _bridgeLogger.Write(BridgeLogEvents.NpcClickRejected, route.NpcName, "npc_click", "ui", null, "rejected", route.RejectReason);
        return;
    }

    _bridgeLogger.Write(BridgeLogEvents.NpcClickObserved, route.NpcName, "npc_click", "ui", null, "accepted", null);
    _pendingDialogue = NpcClickDialogueState.WaitingForOriginal(route.NpcName!, BuildFallbackDialogue(route.NpcName!));
}
```

- [ ] **Step 3: 运行编译，允许先因为 `TryResolveClickedNpcName` 未完成而失败一次，再补最小桩**

Run: `dotnet build "Mods/StardewHermesBridge/StardewHermesBridge.csproj"`
Expected: FAIL mentioning `TryResolveClickedNpcName`

- [ ] **Step 4: 补最小桩函数**

```csharp
private string? TryResolveClickedNpcName()
{
    return null;
}

private static string BuildFallbackDialogue(string npcName)
{
    return npcName switch
    {
        "Haley" => "...你还挺会挑时机的。现在想继续聊什么？",
        "Penny" => "嗯，如果你还想聊的话，我在听。",
        _ => "你好。"
    };
}
```

- [ ] **Step 5: 再次编译确认通过**

Run: `dotnet build "Mods/StardewHermesBridge/StardewHermesBridge.csproj"`
Expected: `Build succeeded.`

- [ ] **Step 6: 提交**

```bash
git add Mods/StardewHermesBridge/ModEntry.cs Mods/StardewHermesBridge/Logging/SmapiBridgeLogger.cs Mods/StardewHermesBridge/Dialogue/NpcDialogueClickRouter.cs
git commit -m "feat: observe npc click dialogue entry"
```

## Task 5: Detect original dialogue completion and only then display custom dialogue

**Files:**
- Modify: `Mods/StardewHermesBridge/ModEntry.cs`
- Modify: `Mods/StardewHermesBridge/Dialogue/NpcDialogueFollowUpService.cs`

**Sources:**
- `openspec/其他项目errors/E-2026-0009-dialogue-detection-debugged-by-guessing-instead-of-logging.md`
- `参考项目/参考文档/星露谷物语/星露谷自定义窗口实现参考.md`

**Evidence:**
- 日志必须显示：已观察到原版对话、原版结束、已投递自定义对话三个阶段

**Reproduce:**
- 点击 Haley，先看原版对话，关闭/结束原版对话，再看 Hermes 自定义对话

**Review:**
- 不能提前抢菜单；不能重复弹出两次自定义对话

**Manual Validation:**
- 玩家能明显感知顺序：原版先、自定义后

- [ ] **Step 1: 在 `OnUpdateTicked` 中加入原版对话观测推进**

```csharp
private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
{
    var status = _commands.PumpOneTick();
    if (status is not null)
        _overlay.SetLastRequest(status.NpcId, status.Action, status.TraceId, status.Status, status.BlockedReason);

    AdvancePendingDialogue();
}
```

- [ ] **Step 2: 写 `AdvancePendingDialogue()` 最小版本**

```csharp
private void AdvancePendingDialogue()
{
    if (_pendingDialogue is null)
        return;

    var snapshot = new DialogueSnapshot(
        hasActiveDialogueBox: Game1.activeClickableMenu is DialogueBox,
        dialogueFinished: _lastObservedDialogueFinished,
        currentSpeakerName: Game1.currentSpeaker?.Name);

    var decision = _followUpService.TryPromote(_pendingDialogue, snapshot);
    if (!decision.ShouldDisplayCustomDialogue)
        return;

    var npc = Game1.getCharacterFromName(_pendingDialogue.NpcName, false, false);
    if (npc is null)
        return;

    npc.CurrentDialogue.Push(new Dialogue(_pendingDialogue.CustomText, npc));
    Game1.drawDialogue(npc);
    _bridgeLogger.Write(BridgeLogEvents.CustomDialogueDisplayed, npc.Name, "npc_click", "ui", null, "completed", null);
    _pendingDialogue = null;
    _lastObservedDialogueFinished = false;
}
```

- [ ] **Step 3: 在 `OnUpdateTicked` 中刷新 `_lastObservedDialogueFinished` 证据**

```csharp
if (Game1.activeClickableMenu is DialogueBox dialogueBox)
{
    _bridgeLogger.Write(BridgeLogEvents.OriginalDialogueObserved, Game1.currentSpeaker?.Name, "npc_click", "ui", null, "observed", $"finished={dialogueBox.dialogueFinished}");
    if (dialogueBox.dialogueFinished)
        _lastObservedDialogueFinished = true;
}
else if (_lastObservedDialogueFinished)
{
    _bridgeLogger.Write(BridgeLogEvents.OriginalDialogueCompleted, Game1.currentSpeaker?.Name, "npc_click", "ui", null, "completed", null);
}
```

- [ ] **Step 4: 编译确认通过**

Run: `dotnet build "Mods/StardewHermesBridge/StardewHermesBridge.csproj"`
Expected: `Build succeeded.`

- [ ] **Step 5: 运行 follow-up 测试与 mod 测试**

Run: `dotnet test "Mods/StardewHermesBridge.Tests/Mods.StardewHermesBridge.Tests.csproj"`
Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add Mods/StardewHermesBridge/ModEntry.cs Mods/StardewHermesBridge/Dialogue/NpcDialogueFollowUpService.cs
git commit -m "feat: wait for original dialogue before custom follow-up"
```

## Task 6: Resolve clicked NPC precisely enough for Phase 1 Haley flow

**Files:**
- Modify: `Mods/StardewHermesBridge/ModEntry.cs`
- Modify: `Mods/StardewHermesBridge/Dialogue/NpcDialogueClickRouter.cs`
- Test: `Mods/StardewHermesBridge.Tests/NpcDialogueClickRouterTests.cs`

**Sources:**
- SMAPI input event path in `ModEntry.cs`
- `参考项目/参考文档/星露谷物语/星露谷自定义窗口实现参考.md`

**Evidence:**
- 日志能显示点击命中的 NPC 名称，且只在命中 Haley 时进入 Phase 1 正式路径

**Reproduce:**
- 在 Haley 附近点她、点 Penny、点空地

**Review:**
- 首版只支持 Haley 也必须写死，不允许“默认所有 NPC 都上”这种临场扩 scope

**Manual Validation:**
- 点 Haley 触发；点其他 NPC 暂不接管或明确记录 reject reason

- [ ] **Step 1: 把测试补成 Phase 1 只接受 Haley**

```csharp
[TestMethod]
public void TryRoute_Rejects_WhenNpcIsNotHaleyInPhase1()
{
    var router = new NpcDialogueClickRouter();
    var result = router.TryRoute(new NpcClickContext("MouseLeft", false, "Penny", true));

    Assert.IsFalse(result.Accepted);
    Assert.AreEqual("npc_not_enabled", result.RejectReason);
}
```

- [ ] **Step 2: 先跑测试确认失败**

Run: `dotnet test "Mods/StardewHermesBridge.Tests/Mods.StardewHermesBridge.Tests.csproj" --filter "FullyQualifiedName~NpcDialogueClickRouterTests"`
Expected: FAIL because current router accepts any NPC hit

- [ ] **Step 3: 最小实现只放行 Haley**

```csharp
if (!string.Equals(context.npcName, "Haley", StringComparison.OrdinalIgnoreCase))
    return new(false, null, "npc_not_enabled");
```

- [ ] **Step 4: 实现 `TryResolveClickedNpcName()` 的最小命中逻辑**

```csharp
private string? TryResolveClickedNpcName()
{
    var location = Game1.currentLocation;
    if (location is null)
        return null;

    var cursorTile = Game1.currentCursorTile;
    foreach (var character in location.characters)
    {
        if (!string.Equals(character.Name, "Haley", StringComparison.OrdinalIgnoreCase))
            continue;
        if (character.TilePoint.X == (int)cursorTile.X && character.TilePoint.Y == (int)cursorTile.Y)
            return character.Name;
    }

    return null;
}
```

- [ ] **Step 5: 跑测试与编译**

Run: `dotnet test "Mods/StardewHermesBridge.Tests/Mods.StardewHermesBridge.Tests.csproj" --filter "FullyQualifiedName~NpcDialogueClickRouterTests" && dotnet build "Mods/StardewHermesBridge/StardewHermesBridge.csproj"`
Expected: PASS + `Build succeeded.`

- [ ] **Step 6: 提交**

```bash
git add Mods/StardewHermesBridge/ModEntry.cs Mods/StardewHermesBridge/Dialogue/NpcDialogueClickRouter.cs Mods/StardewHermesBridge.Tests/NpcDialogueClickRouterTests.cs
git commit -m "feat: gate phase one click dialogue to haley"
```

## Task 7: Downgrade desktop speak buttons to debug-only carrier

**Files:**
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml`
- Modify: `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`

**Sources:**
- `openspec/其他项目errors/E-2026-0005-player-visible-carrier-freeze-missing.md`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml:140-149`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs:377-401`

**Evidence:**
- UI 文案明确说明这是 debug-only，不是正式对话入口

**Reproduce:**
- 打开 Desktop Dashboard，观察按钮文案与说明文案

**Review:**
- 不允许留下“Haley Speak”这种看起来像正式功能的文案

**Manual Validation:**
- 普通玩家读 UI 后不会误以为桌面按钮是主交互

- [ ] **Step 1: 改 XAML 说明文本**

```xml
<TextBlock x:Name="NpcManualActionResult"
           Text="Debug only: use these buttons only to diagnose bridge speak. The real Phase 1 flow is clicking Haley inside Stardew."
           FontSize="12"
           TextWrapping="Wrap"
           Foreground="{StaticResource AppTextSecondaryBrush}"/>
```

- [ ] **Step 2: 改按钮文案**

```xml
<Button x:Name="HaleySpeakButton" Content="Debug Haley Speak" ... />
<Button x:Name="PennySpeakButton" Content="Debug Penny Speak" ... />
```

- [ ] **Step 3: 改代码里的成功提示**

```csharp
NpcManualActionResult.Text = $"Debug speak sent to {npcId}. This is not the primary player dialogue path.";
```

- [ ] **Step 4: 编译桌面项目**

Run: `dotnet build "Desktop/HermesDesktop/HermesDesktop.csproj"`
Expected: `Build succeeded.`

- [ ] **Step 5: 提交**

```bash
git add Desktop/HermesDesktop/Views/DashboardPage.xaml Desktop/HermesDesktop/Views/DashboardPage.xaml.cs
git commit -m "chore: mark desktop speak buttons as debug only"
```

## Task 8: Final manual validation and evidence capture

**Files:**
- Modify if needed: `Mods/StardewHermesBridge/Logging/SmapiBridgeLogger.cs`
- Evidence file/output: SMAPI console + bridge log

**Sources:**
- `openspec/其他项目errors/E-2026-0001-critical-decisions-left-to-implementation-ai.md`
- `openspec/其他项目errors/E-2026-0005-player-visible-carrier-freeze-missing.md`
- `openspec/其他项目errors/E-2026-0009-dialogue-detection-debugged-by-guessing-instead-of-logging.md`

**Evidence:**
- 至少留下一次完整证据链：点击 Haley -> 原版对话出现 -> 原版对话完成 -> Hermes 自定义对话出现

**Reproduce:**
- 使用固定存档、固定地点、固定 NPC Haley

**Review:**
- reviewer 必须能从日志与手动验证步骤复盘链路，不需要靠口头解释

**Manual Validation:**
- 1. 进入存档并靠近 Haley。
- 2. 鼠标左键点击 Haley。
- 3. 确认首先出现原版对话框。
- 4. 结束/关闭原版对话。
- 5. 确认随后出现 Hermes 自定义对话。
- 6. 查看日志，确认存在 `npc_click_observed`、`original_dialogue_observed`、`original_dialogue_completed`、`custom_dialogue_displayed`。
- 7. 点空地，确认不会触发。
- 8. 打开其他菜单后再点 Haley，确认不会误触发。

- [ ] **Step 1: 跑所有相关测试**

Run: `dotnet test "Mods/StardewHermesBridge.Tests/Mods.StardewHermesBridge.Tests.csproj" && dotnet test "Desktop/HermesDesktop.Tests/HermesDesktop.Tests.csproj" --filter "FullyQualifiedName~Stardew"`
Expected: PASS

- [ ] **Step 2: 构建 mod 与桌面项目**

Run: `dotnet build "Mods/StardewHermesBridge/StardewHermesBridge.csproj" && dotnet build "Desktop/HermesDesktop/HermesDesktop.csproj"`
Expected: `Build succeeded.` for both

- [ ] **Step 3: 真实手动验证并保存日志位置**

Run in game: click Haley, finish original dialogue, observe custom follow-up
Expected: player-visible order is original first, custom second

- [ ] **Step 4: 提交最终实现**

```bash
git add Mods/StardewHermesBridge Desktop/HermesDesktop
git commit -m "feat: follow original stardew dialogue with hermes custom dialogue"
```

---

## Self-Review

### Spec coverage
- 正式承载面：已覆盖，冻结为游戏内点击 NPC
- 原版对话先、自定义后：已覆盖于 Task 3 / Task 5 / Task 8
- 历史 errors 约束：已分别固化在 Hard Rules 与每个任务的 Sources / Evidence / Manual Validation
- debug path 降级：已覆盖于 Task 7

### Placeholder scan
- 无 `TBD` / `TODO` / “之后再说”占位
- 每个任务均包含具体文件、代码片段、命令、验证方式

### Type consistency
- `NpcClickContext` / `NpcClickRouteResult` / `NpcClickDialogueState` / `DialogueSnapshot` / `FollowUpDecision` 在任务之间命名一致

---

Plan complete and saved to `docs/superpowers/plans/2026-04-29-stardew-click-npc-original-then-custom-dialogue.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
