# 星露谷 status 事实扩展上下文快照

## 任务

用户认为现在给 NPC agent 的 `status` 事实还不够，希望规划在星露谷场景里还应该提供哪些事实。用户点名了地点、玩家衣着、金钱、任务、最近行动、游戏进度、手里东西，并提醒如果事实太多，应考虑创建新工具。

## 想要结果

形成一个中文白话计划，指导后续实现“更完整但不臃肿”的 NPC 观察事实和按需查询工具。当前阶段只规划，不实现。

## 已知源码证据

- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs` 的 `/query/status` 由 `BuildStatusResponse` 生成，现在返回 NPC 名字、地点、格子、是否移动、是否对话、是否可控、游戏时间、季节、日期、天气、目的地和附近可走格。
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs` 的 `NpcStatusData` 是 bridge 侧 status DTO。
- `src/games/stardew/StardewBridgeDtos.cs` 的 `StardewNpcStatusData` 是 Hermes 侧 status DTO。
- `src/games/stardew/StardewQueryService.cs` 的 `BuildStatusFacts` 把 status DTO 映射成 prompt facts，目前包含 `displayName`、`smapiName`、`location`、`tile`、`isMoving`、`isInDialogue`、`isAvailableForControl`、`gameTime`、`gameClock`、`season`、`dayOfMonth`、`weather`、`blockedReason`、`currentCommandId`、`lastTraceId`、`destination[n]`、`nearby[n]`。
- `src/games/stardew/StardewNpcTools.cs` 现在默认工具是 `stardew_status`、`stardew_move`、`stardew_speak`、`stardew_open_private_chat`、`stardew_task_status`。
- `src/runtime/NpcAutonomyLoop.cs` 每轮会先观察，再把当前 observation 和事件拼成 `[Observed Facts]` 交给 agent；这里已经有 fact count、message chars、gameTime/location/tile、LLM duration 日志。
- `src/runtime/NpcObservationFactStore.cs` 只在内存里记录 observation/event 快照，当前没有暴露成 agent 可主动调用的“最近行动”工具。
- `src/runtime/NpcRuntimeLogWriter.cs` 会写 `runtime.jsonl`，当前主要是 tick/诊断记录。
- `src/tasks/SessionTodoStore.cs` 和 `todo` 工具支持 active todo 注入，但这属于 agent 自己维护的任务，不等于星露谷游戏内任务/日志。

## 约束

- 用户要求中文白话。
- 除私聊外，不允许把 agent 改成事件驱动；agent 必须保持独立自主循环。
- 历史错误经验不一定正确，必须依据当前源码和日志。
- 不要动其他 AI 的工作；当前工作区很脏，规划阶段只写 `.omx/context` 和 `.omx/plans`。
- 关键节点必须加日志，方便后续检查问题。
- 事实不能无限塞进每轮 prompt，否则会拖慢本来已经被用户质疑的 agent 反应速度。

## 未决问题

- 玩家衣着要细到装备/服装名，还是首版只给“帽子、上衣、裤子、鞋子、饰品”摘要。
- 游戏进度首版是只给轻量进度，如 year/season/day/money/locations visited，还是包括社区中心、技能、关系、矿洞层数等较重信息。
- 星露谷原版任务日志的 API 字段需要实现时以 SDV 1.6.15 源码/反编译结果确认。

## 可能触点

- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `src/games/stardew/StardewCommandContracts.cs`
- `src/games/stardew/StardewQueryService.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcObservationFactStore.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewQueryServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Mods/StardewHermesBridge.Tests/*`
