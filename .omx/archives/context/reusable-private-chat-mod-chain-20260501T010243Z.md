# Context Snapshot: reusable private chat mod chain

## Task statement

用户希望将当前 Stardew 私聊链路推广为每个游戏 mod 都可复用的链路，并要求按已讨论的思路实现。

## Desired outcome

- 桌面/core 侧拥有可跨游戏复用的私聊状态机。
- Stardew 保留薄适配层，现有 Haley 私聊行为不回退。
- 各游戏 mod 仍只负责游戏宿主特有的 UI、输入、事件上报和回复展示。
- 现有 Stardew 测试继续通过，并新增测试证明非 Stardew 事件名/游戏 id 也能驱动同一私聊状态机。

## Known facts / evidence

- `src/game/core/IGameAdapter.cs` 已有跨游戏 `Commands / Queries / Events` 接口。
- `src/game/core/GameAction.cs` 已有通用 `GameActionType.OpenPrivateChat`。
- `src/game/core/GameObservation.cs` 的 `GameEventRecord` 已支持 `EventType`、`NpcId`、`CorrelationId`、`Payload`。
- `src/games/stardew/StardewPrivateChatOrchestrator.cs` 当前包含通用状态机逻辑，但类名、选项、触发事件、`gameId`、默认 NPC 与 prompt 均绑定 Stardew/Haley。
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs` 和 `Mods/StardewHermesBridge/Ui/PrivateChatInputMenu.cs` 是 Stardew/SMAPI 专用实现，不能直接跨游戏复用。
- `.omx/plans/星露谷NPC私聊闭环-共识计划-20260430.md` 明确要求 SMAPI 只做桥接/UI，Desktop/core 拥有私聊编排。
- `openspec/其他项目errors/E-2026-0013-stardew-ui-called-from-http-thread.md` 要求所有 Stardew UI 改动通过 game loop pump。
- `openspec/其他项目errors/E-2026-0014-show-text-entry-is-not-visible-menu.md` 要求私聊输入必须是真实 `IClickableMenu`。

## Constraints

- 不破坏当前已手动验证的 Stardew 输入框弹出行为。
- 不把 LLM、人格、记忆或会话状态移入游戏 mod。
- 不为跨游戏复用新增外部依赖。
- 先写失败测试，再写生产代码。
- 保留 Stardew public wrapper，降低对 `App.xaml.cs` 和现有测试的冲击。

## Unknowns / open questions

- 未来不同游戏是否都能支持“回复窗口关闭后继续下一轮”的关闭事件；当前实现先保留事件名可配置能力。
- 各游戏 mod 的实际 UI 质量和输入焦点行为需要逐游戏手动验证；本次只抽象 Desktop/core 状态机。

## Likely codebase touchpoints

- `src/game/core/`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs`
- 新增 `Desktop/HermesDesktop.Tests/Game/` 或相邻测试文件
- 可能更新 `.omx/plans/`
