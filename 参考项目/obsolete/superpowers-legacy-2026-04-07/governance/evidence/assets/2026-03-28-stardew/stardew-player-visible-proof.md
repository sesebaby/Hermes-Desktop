# Stardew Player-Visible Proof

artifactPath: `games/stardew-valley/Superpowers.Stardew.Mod`
buildRevision: `16e0abae1b0ed4bad61652bdd782ed956f252ebb`
visibleHost: `Stardew Valley 1.6.15 running under SMAPI 4.1.10`
recordedAt: `2026-03-28T17:56:05+08:00`

startup evidence:
- `docs/superpowers/governance/evidence/assets/2026-03-28-stardew/stardew-save-loaded-before-hotkeys.png`
- save file loaded into a real in-game room at `06:50`

surface evidence:
- `docs/superpowers/governance/evidence/assets/2026-03-28-stardew/stardew-after-f10-f8-ai-dialogue.png`
- `docs/superpowers/governance/evidence/assets/2026-03-28-stardew/stardew-after-f9-npc-panel.png`

log excerpt:
```text
[Superpowers.Stardew.Mod] SUPERPOWERS_STARDew_VISIBLE_SHELL_READY implementation_only surfaces default-hidden=True
[Superpowers.Stardew.Mod] Manual test hotkeys: F8 opens the AiDialogueMenu shell surface; F9 opens the NpcInfoPanelMenu shell surface; F10 toggles implementation_only surfaces for the current session.
[Superpowers.Stardew.Mod] Implementation-only manual entry is hidden. Press F10 to expose it for the current session.
[Superpowers.Stardew.Mod] Implementation-only manual entry is now visible for the current session.
[Superpowers.Stardew.Mod] Manual test entry opened AiDialogueMenu from F8.
[Superpowers.Stardew.Mod] Manual test entry opened NpcInfoPanelMenu from F9.
```
