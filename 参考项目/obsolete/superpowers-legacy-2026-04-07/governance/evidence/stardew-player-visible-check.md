# Stardew 玩家可见检查

artifactPath: `games/stardew-valley/Superpowers.Stardew.Mod`
schemaVersion: task10-player-visible-check.v1
requiredFieldSetRef: docs/superpowers/plans/2026-03-28-superpowers-m1-parallel-implementation-plan.md#task-10
buildRevision: `f42358ae808a1bfa3ed9a04ccf72429cd5d43d75`
historicalVisualProofRevision: `923866c51002e9245fb69ff3eeedf774ff4db761`
surfaceId: `stardew:historical-controlled-shell-proof`
visibleHost: `Stardew Valley 1.6.15 running under SMAPI 4.1.10`
entryPath: `仅限历史受控证据路径：通过 SMAPI 启动 Stardew -> 进入真实存档 -> 当时部署配置中 AllowImplementationOnlyManualEntry=true -> 在该次受控运行中按一次 F10 -> 按 F9 打开 NpcInfoPanelMenu -> 按 F8 打开 AiDialogueMenu`
startupProof: Repo-local SMAPI log for the current-head candidate records `SUPERPOWERS_STARDew_VISIBLE_SHELL_READY`, hotkey help text, and `SUPERPOWERS_STARDew_RUNTIME_READY` before the manual interaction sequence.
visibleSurfaceProof: Historical user-supplied manual screenshot 1 shows `Superpowers NPC Panel [unknown]` rendered as an in-game clickable menu with `State: Ready`; historical screenshot 2 shows `Superpowers AI Dialogue` rendered as an in-game clickable menu with the local storyteller recovery copy visible. Both surfaces are visible in the host game window, not only in logs, but the PNG assets were captured during the earlier controlled override run rather than on current candidate `f42358ae808a1bfa3ed9a04ccf72429cd5d43d75`.
interactionProof: The controlled manual path (`F10` -> `F9` -> `F8`) is only proven by the earlier `2026-03-28` controlled override run; the current-head rerun refreshed startup/load markers only and did not produce a same-revision in-host interaction capture set.
visualEvidenceRef: `docs/superpowers/governance/evidence/assets/2026-03-28-stardew-current-head/stardew-player-visible-proof.md`; `docs/superpowers/governance/evidence/assets/2026-03-28-stardew-current-head/stardew-save-select-current-head.png`; `docs/superpowers/governance/evidence/assets/2026-03-28-stardew-current-head/stardew-save-loaded-current-head.png`; `docs/superpowers/governance/evidence/assets/2026-04-05-stardew-current-head/stardew-save-loaded-current-head.png`
reviewer: `Codex integrator with user-supplied manual screenshots`
reviewTimestamp: `2026-03-28T21:43:15.8766815+08:00`
result: visual_gate_pending

范围说明：

- 本文件保留为历史 controlled shell-proof 证据载体
- 当前 `buildRevision` 代表 repo-local code / load baseline 所对应的 candidate；`historicalVisualProofRevision` 表示现存 PNG 资产最初来源的受控运行 revision
- `2026-04-05` 的 current-head rerun 已再次进入真实存档，并新增 `stardew-save-loaded-current-head.png`，但自动化热键注入未能拿到同 revision 的 `NpcInfoPanelMenu / AiDialogueMenu` 截图，因此仍不得把它改写成 current-head shell-proof complete
- 它不得被当作当前默认的 M1 core 手动路径
- 当前面向测试的 M1 手动验证应使用 `docs/superpowers/plans/2026-03-29-superpowers-m1-tester-manual-verification-plan.md`
- 按当前代码现实，`F8` 与 `F9` 是 core shell 入口热键；在默认的 tester-facing M1 core 流程里不应要求先按 `F10`
