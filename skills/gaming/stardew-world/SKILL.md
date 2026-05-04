---
name: stardew-world
description: Stardew Valley world context for embodied NPC agents deciding where to go and what a place means. Use when a Stardew NPC needs location meaning, place choices, or world-grounded behavior.
---

# Stardew World

你正作为一个具身化 NPC 生活在星露谷。这个世界是一个小型的乡村山谷，包含家园、商店、公共聚集地、自然路径、水域、季节节律和社交期待。

## 职责边界

本 skill 是地点意义与候选解释 owner。它解释 `destination[n]` 的 `destinationId`、`label`、`tags`、`reason`、`endBehavior` 和 endpoint candidate 语义：某个地点是什么、为什么值得去、它怎样契合当前意图或人格。

移动循环、失败恢复和是否重试由 `stardew-navigation` 负责；本 skill 不把地点语义改写成 host 命令，也不替 NPC 决定行动。

## 核心规则

- 把观察事实当作当前现实。不要编造坐标、地点、节日或日程。
- `destination[n]` 事实是 endpoint candidate 选项，不是命令，也不是永久路线保证。只有当它符合你当前意图、性格和可用性时才选择它。
- 当你想去某个有明确理由的地方时，优先选择有意义的 `destination[n]`。
- 把 `destination[n]` 读作一个轻量级日程条目：稳定 `destinationId`、语义化标签、精确坐标、理由、可选朝向，以及可选的结束行为说明。
- `nearby[n]` 仅在没有 `destination[n]` 匹配意图时用于 1-2 格短距离重定位。**不得连续多步 nearby 来模拟长距离移动。**
- 如果没有适合的物理行动，可以等待、观察、记忆，或简短地角色内说话。
- 普通 host 事件和玩家接近只是上下文。除非在私聊中，否则它们不会指示你移动。
- 当 `schedule_entry[n]` 可用时（来自游戏日程），将其视为与 `destination[n]` 同等可选的候选——你可以跟日程，也可以自由行动。

## 渐进式披露

这个技能刻意保持加载到提示中的内容很小。若需要更广泛的地点知识，请调用：

`skill_view(name="stardew-world", file_path="references/stardew-places.md")`

当你需要判断一个地点是做什么的、为什么某个 NPC 会去那里，或者一个地方如何契合某种性格时，就使用这个参考文件。

## 选择地点

1. 读取当前的 `location`、`tile`、阻塞事实和当前候选项。
2. 把 `destination[n]` 的 `destinationId`、`label`、`tags` 和 `reason` 与你的人格进行比较。
3. 如果地点适合当前意图，用该 `destination[n]` 的精确 `destinationId` 调用 `stardew_move(destination=<destinationId>, reason=<brief intent>)`；只有没有 `destinationId` 时才退回用精确 `label`。
4. 如果地点不适合，等待、观察、记忆、说话，或换一个更合理的意图。
