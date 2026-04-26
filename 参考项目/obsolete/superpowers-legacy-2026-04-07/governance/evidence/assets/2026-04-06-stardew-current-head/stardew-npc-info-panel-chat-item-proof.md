# Stardew NpcInfoPanel Chat / Item Current-Head Proof

artifactPath: `games/stardew-valley/Superpowers.Stardew.Mod`
buildRevision: `86158d7fcbc4`
recordedAt: `2026-04-06T09:20:00+08:00`
evidenceMode: `user-supplied inline screenshots + in-thread manual interaction confirmation`
visibleHost: `Stardew Valley 1.6.15 running under SMAPI 4.1.10`
surfaceId: `stardew:npc-info-panel:chat-item-current-head`

entry path:

- current deployed local mod config kept `AllowImplementationOnlyManualEntry=false`
- entered a real loaded save
- faced Haley in `HaleyHouse`
- opened the current-head `NpcInfoPanel` shell
- switched to `聊天` and `物品`
- pressed `Esc` to close the panel

visual evidence:

- inline screenshot A shows `NpcInfoPanel` with Haley selected and the `聊天` tab visible in-host, including current-head chat history rows, collapsed history disclosure (`[查看更多]`), and the shared tab strip `记忆 / 关系 / 想法 / 物品 / 聊天 / 群聊历史`
- inline screenshot B shows the same `NpcInfoPanel` session with the `物品` tab visible in-host, including the current item grid shell and the `物品详情` pane
- inline screenshot C shows the same shell can switch tabs while staying in-host, proving the shared panel shell remains visible under current-head when moving across the tab strip

interaction witness:

- user manually confirmed: `Esc 可以关闭`

limitations:

- this proof is scoped to current-head `NpcInfoPanel Chat / Item` shell visibility and interaction only
- it does not prove current-head `AiDialogueMenu` (`F8`) shell visibility
- it does not claim rich playable dialogue / item lifecycle closure beyond the visible shell, tab switch, and close interaction
