---
id: E-2026-0508-stardew-autonomy-host-fed-facts
title: 星露谷 NPC 自主循环中宿主把观察事实和行动候选灌给父层决策
updated_at: 2026-05-08
keywords:
  - stardew
  - npc-autonomy
  - host-boundary
  - wake-only
  - destination-candidates
  - Haley
  - Penny
---

## 症状

- 玩家在海利身边等待几分钟，看不到预期的闲置微动作。
- 潘妮和海利会莫名其妙移动，表现像被宿主脚本推着走，而不是角色自己决定。
- 海利在自己屋里时，桥接层注册表会把 `mirror`、`downstairs`、`front door` 这类候选地点带进自主决策链路。
- 运行表现与“角色主动通过工具理解世界，再提出意图”相反，像宿主先替角色看了世界、挑了方向，再让角色补一段理由。

## 根因

- 自动自主轮询在唤醒角色前先调用观察接口，并把观察结果、事件记录和桥接层生成的移动候选放进父层提示。
- `destination[n]`、`nearby[n]`、`moveCandidate[n]` 被当成普通世界事实使用，但它们本质上是宿主生成的行动候选。
- 事件记录也被当成每轮上下文灌入父层，导致宿主每轮都在替角色建立“当前该关注什么”的判断。
- 这破坏了核心边界：宿主和桥接层只能提供工具、执行、确认和工具结果，不能成为“半个大脑”。

## 错误修法

- 不要只过滤 `mirror`、`downstairs`、`front door` 这几个候选；问题不是候选名字，而是宿主不该预载候选。
- 不要把唤醒改成“唤醒后必须先观察”；这仍然是宿主替角色决定第一步。
- 不要把“每轮自动观察”包装成被动事实注入；只要不是角色主动调用工具，它就不是这一轮的角色所得事实。
- 不要把移动、等待、闲置微动作或观察策略写成桥接层规则；桥接层只能执行明确意图。
- 不要靠增加提示词去压住错误候选；应该从链路上禁止自动灌入。

## 修正约束

- 自主轮询每轮只做唤醒：提示角色“作为生活在星露谷的人，自己行动”。
- 唤醒提示不得自动附带观察事实、事件文本、地点候选、附近候选、移动候选或宿主建议。
- 观察、状态查询、移动、等待、闲置微动作都只能来自角色输出的意图，不能由宿主预先要求。
- 显式观察或状态工具仍可返回世界事实和候选，但这是工具结果，不是唤醒时的父层预载上下文。
- 本地执行层只能接收父层意图和自己的工具结果；不得把宿主自动观察结果作为隐藏事实塞给执行层。
- 回归测试必须断言父层提示不包含 `location=...`、`gameTime=...`、事件文本、`destination[n]`、`nearby[n]`、`moveCandidate[n]`。

## 验证证据

- 更新 `NpcAutonomyLoopTests`，锁定核心循环不自动观察、不注入事件或地点候选。
- 更新 `StardewAutonomyTickDebugServiceTests`，锁定调试入口不会把候选目的地暴露给父层提示。
- 更新 `StardewNpcAutonomyBackgroundServiceTests`，锁定后台自动轮询只唤醒，不把事件记录灌进父层提示。
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcAutonomyLoopTests" -p:UseSharedCompilation=false` passed, 30/30.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Stardew.StardewAutonomyTickDebugServiceTests" -p:UseSharedCompilation=false` passed, 15/15.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Stardew.StardewNpcAutonomyBackgroundServiceTests" -p:UseSharedCompilation=false` passed, 34/34.
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false` passed, 199/199.

## 相关文件

- `src/runtime/NpcAutonomyLoop.cs`
- `src/Core/SystemPrompts.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
