# Launcher 玩家可见检查

artifactPath: `artifacts/launcher/Superpowers.Launcher.exe`
buildRevision: `f42358ae808a1bfa3ed9a04ccf72429cd5d43d75`
surfaceId: `launcher:home-to-stardew-config`
visibleHost: `Windows desktop launcher main window`
entryPath: `启动 Superpowers.Launcher.exe -> 默认进入首页 surface -> 点击“星露谷物语”卡片上的“查看配置”`
startupProof: `2026-04-04T21:20:43.5723617+08:00` 启动当前发布产物 `artifacts/launcher/Superpowers.Launcher.exe` 成功，窗口标题为 `Superpowers Launcher`，默认落在首页 surface。
visibleSurfaceProof: 首页真实可见，截图中可见 `首页` 标题、`星露谷物语` 卡片、`打开配置` 主 CTA 和 `查看配置` 次 CTA；不是日志或后台状态成立。
interactionProof: 通过 UI Automation 实际触发 `查看配置` 按钮，成功进入 `星露谷物语配置` surface；交互后截图可见 `开始前检查` 区块和新的 `打开配置` CTA。
visualEvidenceRef: `docs/superpowers/governance/evidence/assets/2026-04-04-launcher-current-head/launcher-home-startup.png`; `docs/superpowers/governance/evidence/assets/2026-04-04-launcher-current-head/launcher-stardew-config-after-click.png`; `docs/superpowers/governance/evidence/assets/2026-04-04-launcher-current-head/launcher-player-visible-proof.json`
reviewer: `Codex main-worktree integrator`
reviewTimestamp: `2026-04-04T21:20:43.5723617+08:00`
result: passed

说明：

- 本文件现已刷新到当前 candidate revision `f42358ae808a1bfa3ed9a04ccf72429cd5d43d75`
- 该证明只覆盖 launcher startup / key navigation；`6.5 / 8.6` 所需的 Stardew in-host current-head 证明仍待补
- 当前面向测试的默认入口应使用 `docs/superpowers/plans/2026-03-29-superpowers-m1-tester-manual-verification-plan.md`
