---
id: E-2026-0505-published-stardew-runtime-resource-gap
title: 发布版漏带 Stardew 运行时资源会让 NPC 自主循环看似不触发
updated_at: 2026-05-05
keywords:
  - stardew
  - publish
  - personas
  - skills/gaming
  - autonomy
  - desktop deploy
---

## symptoms

- 从源码目录运行时 NPC autonomy 正常，但发布安装到 `%LOCALAPPDATA%\Programs\HermesDesktop` 后，手测数分钟没有任何 agent 行为。
- Hermes 日志反复出现 `No valid Stardew persona source directory was found`。
- 补上 persona 后，错误继续前进到 `required skill 'stardew-core' was not found at any configured Stardew gaming skill root`。

## trigger_scope

- 修改 Stardew persona、技能、NPC autonomy prompt 资源。
- 修改桌面端 publish/deploy 脚本或 `.csproj` 内容项。
- 通过安装目录启动 Hermes，而不是 `dotnet run` 从仓库启动。

## root_cause

发布版只带了可执行文件和常规资产，没有把 `src/game/stardew/personas` 与 `skills/gaming` 复制到安装目录。运行时定位器在安装目录下查 `personas` 与 `skills/gaming`，资源缺失导致 NPC autonomy 每轮都在组装 prompt 前失败，表现为“agent 没触发”。

## bad_fix_paths

- 只重启 Hermes 或游戏，不检查 Hermes 日志中的资源路径错误。
- 只发布 persona，不发布 `skills/gaming`，会把错误推到下一层。
- 只让开发机依赖仓库路径或 `HERMES_DESKTOP_WORKSPACE`，发布版仍会坏。
- 把问题误判成 LLM 没响应或 NPC 决策保守。

## corrective_constraints

- 桌面发布必须把 Stardew persona 包复制到 app-local `personas`。
- 桌面发布必须把 Stardew 必需技能复制到 app-local `skills/gaming`。
- 发布资源测试要同时断言 persona 和 gaming skills 都进 `.csproj` 的 `CopyToPublishDirectory`。
- 手测“agent 不触发”时，先查 `StardewNpcAutonomyBackgroundService` 的资源错误，再判断 LLM 或行为策略问题。

## verification_evidence

- `DesktopProject_PublishesBundledStardewPersonasNextToExe` 覆盖发布版 persona 与 `skills/gaming` 内容项。
- 安装目录确认存在 `C:\Users\Administrator\AppData\Local\Programs\HermesDesktop\personas\...`。
- 安装目录确认存在 `C:\Users\Administrator\AppData\Local\Programs\HermesDesktop\skills\gaming\stardew-core.md`。
- 重启 Hermes 后日志出现 `Stardew autonomy LLM turn started/completed for haley`，不再停在 persona 或 skill 资源缺失。
