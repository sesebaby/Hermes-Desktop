# Game Profiles

本目录用于承载：

- `docs/superpowers/profiles/games/<gameId>/game-integration-profile.md`

每个支持游戏都应有独立目录与 profile 文档。

每个 game profile 至少必须回答：

- 这个游戏在 7 层结构里各层分别干什么
- 这个游戏的 prompt 资产、聊天正本、记忆正本、审计正本在哪里
- 这个游戏的 `Launcher` 游戏页、支持页、修复页、mod 下载/更新页分别承接什么
- 这个游戏的 `Runtime.<game> Adapter` 负责哪些事实冻结与执行翻译
- 这个游戏的结构化事实包最小字段是什么，哪些 raw 输入允许进包，哪些绝对不许混进包
- 这个游戏的 `Game Mod` 负责哪些宿主 UI 与最终宿主写回
- 哪些能力已进入当前实现 / 当前证据 / 当前 claim

profile 必须按 phase 或实施批次拆分：

- `M1 core profile`
- `M2+ annex`

规则：

- profile 必须先回链：
  - `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
  - `docs/superpowers/specs/attachments/2026-04-07-superpowers-reference-mod-capability-mapping-appendix.md`
- `M1` ship gate 只强制检查 `M1 core profile`
- 只有当某能力进入当前阶段、进入当前 title 的 claim scope、或被批准为 experiment / preview 时，才要求对应 annex
- profile 不得重写 prompt 真源、memory 真源、readiness truth、entitlement truth
- profile 不得把 `billingSource` 误写成“本地 provider 通路选择”；provider 通信固定仍在 `Cloud`
