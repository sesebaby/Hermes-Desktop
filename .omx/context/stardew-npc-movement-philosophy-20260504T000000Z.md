# 星露谷 NPC 移动哲学深访上下文快照

## Task Statement

参考 `external/hermescraft-main`，深入澄清“一个真正生活在星露谷中的 NPC 应该怎么移动”：应该以局部几格挪动为主，还是应该围绕明确目的地进行持续移动。

## Desired Outcome

- 明确 Stardew NPC 的移动哲学，而不是继续在“挪一格也算动”与“必须去远处目的地”之间摇摆。
- 为后续 `$ralplan` 或执行阶段提供可测试的方向：移动到底是“微 reposition”还是“目的地驱动”。
- 保持用户已确认的原则不变：
  - 除私聊外，不允许事件驱动 NPC move。
  - host/bridge 只能提供观察事实、工具和执行能力，不能替 NPC 决策。
  - 如果要调整行为，优先通过提示词、工具描述、观察 facts 来影响 agent 决策。

## Stated Solution

用户明确要求参考 `external/hermescraft-main` 的实现思路，重新思考 Stardew NPC 的“真实移动”应该长什么样，而不是只盯当前一格 move 的局部修补。

## Probable Intent Hypothesis

用户要解决的不是单一 bug，而是移动建模方向错误：
- 当前系统也许“技术上会 move”，但玩家体感不像一个真正 living NPC。
- 用户希望先钉死“移动是什么”这个上位设计，再决定后面怎么修候选目标、prompt 和 bridge。

## Known Facts / Evidence

### 当前仓库中的移动现实

1. 当前手测日志证明海莉不是完全不动，而是只做过一次很短的 move：
   - 2026-05-03 22:01:15 观察事实：海莉在 `HaleyHouse (8,7)`，候选只有
     - `(7,7)`
     - `(8,6)`
     - `(7,8)`
   - 2026-05-03 22:01:29 agent 选择了 `stardew_move` 到 `(8,6)`
   - 2026-05-03 22:01:30 `stardew_task_status` 已完成
   - 之后没有第二次 move，只有 speak
2. 当前候选点来自 `BridgeHttpHost.BuildMoveCandidates(...)`，本质是“围绕当前 tile 的局部安全枚举”，不是“目的地导航”：
   - `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
3. 当前 bridge move 是同地图、短距离、逐 tick 执行，但目标本身依然是局部 tile：
   - `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
4. 当前 agent 输入的是 observation facts 中的 `moveCandidate[n]`，而不是“去某个语义目的地”的 world action：
   - `src/games/stardew/StardewQueryService.cs`
   - `src/runtime/NpcAutonomyLoop.cs`
5. 当前 prompt 约束是：
   - 可用 `moveCandidate[n]` 就用 `stardew_move`
   - 但并没有定义“一个 living NPC 什么时候应该明确去某个地方”
   - `skills/gaming/stardew-navigation.md`

### HermesCraft 参考实现中的移动现实

1. HermesCraft 的 embodied movement 不是“随便挪几个格子”，而是围绕明确动作意图调用：
   - `mc goto X Y Z`
   - `mc goto_near X Y Z [range]`
   - `mc follow PLAYER`
2. 它的本质是：
   - agent 先决定“我要去哪里/跟谁走”
   - body 层再用 pathfinding 持续推进直到到达或接近目标
3. 参考证据：
   - `external/hermescraft-main/README.md`
   - `external/hermescraft-main/SOUL-minecraft.md`
   - `external/hermescraft-main/bot/server.js`

## Constraints

- 不允许把普通 NPC move 改成事件驱动或 host 代决策。
- 不允许为了移动而另造第二套 NPC agent。
- 仍保持“共享 agent + 不同 persona / session / body”的同源原则。
- Phase 1 现有 bridge 还没有跨地图导航能力；如果未来要上目的地驱动，需要明确是否允许跨 location。

## Unknowns / Open Questions

1. 对 Stardew 这种 2D 村庄游戏来说，“living NPC 的移动”最小正确单位应该是什么：
   - 一次走 1-3 格的局部 reposition
   - 还是一次选择一个语义目的地，再持续移动到附近
2. 如果采用“目的地驱动”，目的地应来自哪里：
   - prompt 自主生成的意图
   - world facts / scene facts 中暴露的地点
   - 某种可枚举的 POI 候选
3. 如果仍保留“候选 facts”模式，候选应当是：
   - tile 候选
   - 语义地点候选
   - 二者并存
4. 在用户要求的自治边界下，host 可以安全提供到什么层级：
   - 只给 tile
   - 给地点名 + 可达范围
   - 给 POI 但不做最终选择

## Decision-Boundary Unknowns

- OMX / 后续执行阶段是否可以默认把“living NPC”解释成“目的地驱动移动”。
- 如果不能默认，需要用户亲自决定：
  - `局部生活感移动` 路线
  - `语义目的地移动` 路线
- 一旦选错路线，后续 prompt、候选 facts、bridge 能力都会跟着错。

## Likely Codebase Touchpoints

- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `src/games/stardew/StardewQueryService.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `skills/gaming/stardew-navigation.md`
- `external/hermescraft-main/README.md`
- `external/hermescraft-main/SOUL-minecraft.md`
- `external/hermescraft-main/bot/server.js`
