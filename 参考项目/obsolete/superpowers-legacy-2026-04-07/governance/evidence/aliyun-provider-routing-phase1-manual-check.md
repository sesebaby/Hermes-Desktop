# Aliyun Provider Routing Phase 1 Manual Check

状态：

- implementation complete, manually verified

effective date：

- 2026-03-29

scope：

- `private_dialogue` provider-backed candidate generation
- `Cloud Control` repo-local provider routing startup
- `Runtime.Local` deterministic gate after provider candidate generation

current note：

- 当前仓库已完成代码与自动化测试覆盖。
- 代码路径级别已移除 `Stardew Mod` `RuntimeClient` 对本地 shell `generation` 字段的构造与发送。
- 代码路径级别预期：如果 current-head 上出现 `private_dialogue` 玩家可见回复，它应经由 `Runtime.Local -> Cloud Control -> provider-backed candidate -> deterministic gate` 路径取得，而不是来自本地 shell 占位字段。
- `2026-03-29` 已用当前工作区开发配置中的有效 DashScope key 跑通受控 live-provider 闭环。
- 本记录当前同时包含：repo-local 自动化事实、受控 live-provider 交互事实、以及 player-visible 受控可见证据。
- 本次手动验证采用受控本地 harness：
  - `Cloud Control(TestServer)` 真实调用 DashScope
  - `Runtime.Local(TestServer)` 真实经过 provider-backed candidate generation、deterministic gate、hosted narrative create / finalize
  - `RuntimeClient -> AiDialogueMenu` 真实吃到 live-provider 回复并进入 player-visible `committed` 状态

startup proof：

- complete
- 受控运行时间：`2026-03-29T21:01:57+08:00`
- 证据：
  - [startup-proof.json](/D:/GitHubPro/AllGameInAI/docs/superpowers/governance/evidence/assets/2026-03-29-aliyun-provider-routing-phase1/startup-proof.json)
- 结果：
  - `Cloud Control /healthz = ok`
  - `Runtime.Local /healthz = ok`

visible-surface proof：

- complete
- 证据：
  - [player-visible-menu-proof.json](/D:/GitHubPro/AllGameInAI/docs/superpowers/governance/evidence/assets/2026-03-29-aliyun-provider-routing-phase1/player-visible-menu-proof.json)
- 关键事实：
  - `RuntimeClient -> AiDialogueMenu` 返回 `populated = true`
  - `AiDialogueMenu.CurrentState = Ready`
  - `Transcript[0].Body` 为真实 DashScope 回复
  - `ReplayMetadata.ReplayState = committed`

interaction proof：

- complete
- 证据：
  - [interaction-runtime-response.json](/D:/GitHubPro/AllGameInAI/docs/superpowers/governance/evidence/assets/2026-03-29-aliyun-provider-routing-phase1/interaction-runtime-response.json)
  - [interaction-finalize-response.json](/D:/GitHubPro/AllGameInAI/docs/superpowers/governance/evidence/assets/2026-03-29-aliyun-provider-routing-phase1/interaction-finalize-response.json)
- 关键事实：
  - 初次 runtime 响应 `replayState = pending_visible`
  - finalize 后响应 `replayState = committed`
  - canonical record id：`npc.abigail:pd-live-20260329-001`
  - provider 真实返回了内容；当前 deterministic gate 对 `dialogue_opportunity` 动作给出 `blocked / unsupported_action_kind`，但文本回复和 state 主链已成功提交

known limits：

- 当前已验证的是受控本地 harness 下的 live-provider 运行，不是实际启动 `Stardew Valley` 游戏进程后的屏幕录制。
- 当前 prompt-ready payload 已避免把 opaque ref 或“待解引用”措辞写成已存在的真实 scene / relation / recent-history 上下文；但这仍属于代码路径 truthfulness，不等于玩家侧已观测证据。
- 当前 phase 1 真实 provider 路由只承诺 `private_dialogue`；`thought / memory / social transaction / commitment` 不应在本记录里冒充已 provider-backed。
- 共享合同里的遗留 `Generation` 字段仍存在于范围外代码；本次已核实并修复的是 Stardew Mod runtime client 不再构造或序列化该字段。

visual evidence ref：

- complete
- 证据：
  - [player-visible-proof.png](/D:/GitHubPro/AllGameInAI/docs/superpowers/governance/evidence/assets/2026-03-29-aliyun-provider-routing-phase1/player-visible-proof.png)
  - [player-visible-proof.html](/D:/GitHubPro/AllGameInAI/docs/superpowers/governance/evidence/assets/2026-03-29-aliyun-provider-routing-phase1/player-visible-proof.html)

blocking note：

- 当前工作区 `appsettings.Development.json` 已含有效 DashScope 开发 key，支持当前受控 live-provider 运行。
- 若后续准备提交或归档到共享分支，需要先确认这把开发 key 是否允许进入版本库；否则应改回安全来源后重新补一次手动证据。
