# Stardew Player-Visible Proof

artifactPath: `games/stardew-valley/Superpowers.Stardew.Mod`
buildRevision: `923866c51002e9245fb69ff3eeedf774ff4db761`
visibleHost: `Stardew Valley 1.6.15 running under SMAPI 4.1.10`
recordedAt: `2026-03-28T21:43:15.8766815+08:00`
evidenceMode: `user-supplied manual screenshots + repo-local SMAPI log`
manualExposureOverrideUsed: true
manualExposureOverrideNote: `Deployed mod config was temporarily switched to AllowImplementationOnlyManualEntry=true for this controlled evidence run. Default packaged behavior remains blocked-by-default until governance closure explicitly enables local override.`
repoLocalImageAssetsStored: true

startup evidence:
- repo-local SMAPI log `artifacts/logs/smapi-debug-manual-entry.log` shows `SUPERPOWERS_STARDew_VISIBLE_SHELL_READY`, hotkey help text, and `SUPERPOWERS_STARDew_RUNTIME_READY` for the current-head candidate.

surface evidence:
- user-supplied manual screenshot 1 shows `Superpowers NPC Panel [unknown]` rendered in the loaded farmhouse save with `State: Ready`, `Selected tab: Memory`, and the tab strip `Memory / GroupHistory / Relation / Item / Thought`.
- user-supplied manual screenshot 2 shows `Superpowers AI Dialogue` rendered in the same loaded farmhouse save with `Status: Checking the local storyteller...`, `Recovery: Use the game config page to retry if the local storyteller is unavailable.`, and `Actions: Reply / Retry / Close`.

interaction log excerpt:
```text
[Superpowers.Stardew.Mod] SUPERPOWERS_STARDew_RUNTIME_READY
[Superpowers.Stardew.Mod] Implementation-only manual entry is now visible for the current session.
[Superpowers.Stardew.Mod] Manual test entry opened NpcInfoPanelMenu from F9.
[Superpowers.Stardew.Mod] Manual test entry opened AiDialogueMenu from F8.
```

limitations:
- This refresh proves current-head implementation-only shell visibility under a controlled local override, not RC/GA-approved default exposure.
- This refresh still does not prove rich playable M1 closure for dialogue / memory / item / thought beyond the current shell surfaces.
- Repo-local PNG assets for the two screenshots are now checked into `docs/superpowers/governance/evidence/assets/2026-03-28-stardew-current-head/`.
