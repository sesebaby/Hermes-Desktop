---
title: "Stardew NPC Navigation Reference Mods"
tags: ["stardew", "npc-navigation", "reference-mods", "pathfinding"]
created: 2026-05-06T22:46:51.601Z
updated: 2026-05-06T22:46:51.601Z
sources: ["参考项目/Mod参考/Stardew-GitHub-ncarigon-CeruleanStardewMods", "参考项目/Mod参考/Stardew-GitHub-andyruwruw-BotFramework", "参考项目/Mod参考/Stardew-GitHub-spacechase0-CustomNPCFixes", "参考项目/Mod参考/Stardew-GitHub-TamKungZ-NPCMaker-CS"]
links: []
category: reference
confidence: high
schemaVersion: 1
---

# Stardew NPC Navigation Reference Mods

已下载并初步分类 Stardew NPC 导航/日程参考 mod。Market Day (`Stardew-GitHub-ncarigon-CeruleanStardewMods/Market/MarketDay`) 是当前最强参考：`Patches.cs` patch `PathFindController.findPathForNPCSchedules`、`NPC.parseMasterSchedule`、`NPC.getMasterScheduleEntry`；`Utility/Schedule.cs` 中有 `parseMasterSchedule`、`PathfindToNextScheduleLocation`、`GetLocationRoute`、`ScheduleStringForMarketVisit`，体现“重组原版 schedule/path 链路 + 必要时 warp fallback”。BotFramework (`Stardew-GitHub-andyruwruw-BotFramework`) 是跨 GameLocation 目标访问/世界图参考：`WorldTour`/`WorldPath`/`WorldParser` 用 warps 建 location graph，并把到下一地点的 warp tile 作为 Navigate action；它更偏机器人框架，不是原版 NPC schedule API。CustomNPCFixes (`Stardew-GitHub-spacechase0-CustomNPCFixes`) 是 route/schedule refresh 参考：调用 `NPC.populateRoutesFromLocationToLocationList()`、`npc.getSchedule(...)`、`npc.checkSchedule(...)` 让自定义 NPC/地图恢复原版路径能力。NPCMaker-CS 只适合参考 schedule 数据生成 UI/格式，不适合作为运行时自动寻路参考。当前结论：社区没有稳定高层 API “NPC + 目标地图 + tile => 自然跨图走到”；可复用的是地图 route、schedule path 组装、单地图 `findPathForNPCSchedules`、warp fallback 这些零件。
