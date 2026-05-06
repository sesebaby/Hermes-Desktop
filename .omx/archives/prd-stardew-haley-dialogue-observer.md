# PRD: Stardew Haley Dialogue Observer

## Goal

When the player interacts with Haley in Stardew Valley, Hermes should preserve the vanilla NPC dialogue first and then show the Hermes custom NPC dialogue after the vanilla `DialogueBox` closes.

## Scope

- Replace the formal NPC click path's manual `npc.checkAction` replay with observation of Stardew's dialogue lifecycle.
- Use `Display.MenuChanged` and the active `DialogueBox` speaker as the authoritative signal that vanilla dialogue opened.
- Continue to render Hermes dynamic text through `new Dialogue(npc, null, text)`.
- Keep `/action/speak` as a debug path.

## Acceptance Criteria

- Accepted Haley clicks create a pending follow-up without suppressing the original input.
- Haley `DialogueBox` open/close is observed through menu state.
- Hermes custom dialogue appears only after no active menu remains.
- If no original dialogue appears after an accepted click, Hermes falls back instead of waiting forever.
- Mod tests and build pass.
