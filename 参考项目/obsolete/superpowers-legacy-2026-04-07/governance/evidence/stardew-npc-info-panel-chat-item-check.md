# Stardew NpcInfoPanel Chat / Item 检查

artifactPath: `games/stardew-valley/Superpowers.Stardew.Mod`
schemaVersion: task10-player-visible-check.v1
buildRevision: `86158d7fcbc4`
surfaceId: `stardew:npc-info-panel:chat-item-current-head`
visibleHost: `Stardew Valley 1.6.15 running under SMAPI 4.1.10`
entryPath: `进入真实存档 -> 面向 Haley -> 打开当前 head 的 NpcInfoPanel -> 切换到 聊天 / 物品 -> 按 Esc 关闭`
startupProof: User-supplied current-thread screenshots prove the current deployed mod is running inside a real loaded save; the in-host panel proof was collected after the repo-local current-head build deployed `Superpowers.Stardew.Mod.dll` / runtime dependencies into `D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\Superpowers.Stardew.Mod` on `2026-04-06 09:01-09:02`, with `AllowImplementationOnlyManualEntry=false` in the deployed config.
visibleSurfaceProof: Current-thread screenshots show the current-head `NpcInfoPanel` rendered in-host for Haley, with the shared tab strip visible and both `聊天` and `物品` tabs successfully opened. The `聊天` tab shows current chat-history rows and disclosure copy (`[查看更多]`), while the `物品` tab shows the item grid shell and `物品详情` pane.
interactionProof: User manually confirmed `Esc 可以关闭`, establishing a real `open -> tab switch -> Esc close` interaction path for the current-head `NpcInfoPanel`.
visualEvidenceRef: `docs/superpowers/governance/evidence/assets/2026-04-05-stardew-current-head/stardew-save-loaded-current-head.png`; `docs/superpowers/governance/evidence/assets/2026-04-06-stardew-current-head/stardew-npc-info-panel-chat-item-proof.md`
reviewer: `Codex integrator using user-supplied current-thread screenshots and user-reported close interaction`
reviewTimestamp: `2026-04-06T09:24:00+08:00`
result: passed

范围说明：

- 本记录只证明 current-head `NpcInfoPanel Chat / Item` 的玩家可见 shell、tab 切换和关闭交互。
- 本记录不证明 current-head `AiDialogueMenu` (`F8`) shell。
- 本记录不把 `聊天 / 物品` 记成 rich-playable 全闭环，只证明 player-visible shell 已在 current-head 真正出现。
