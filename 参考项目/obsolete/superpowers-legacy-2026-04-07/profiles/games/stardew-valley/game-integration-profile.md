# Stardew Valley Game Integration Profile

状态：

- active design baseline

gameId：

- `stardew-valley`

owner：

- game integration owner

同步说明：

- 当前 `NPC 信息面板` 的前台 tab 结构与交互口径，已按 active change
  - `openspec/changes/maximize-stardew-reference-mod-parity`
  同步到本文件
- 本次同步覆盖旧的：
  - `关系图谱 + 右侧详情卡`
  - `物品卡片墙 + 点开详情`
  - `5 tab` 口径

phase split：

- `M1 core profile`
- `M1 implementation_only capability / channel / surface`
- `M2+ annex / experiment-only truth source`

当前正式 authority：

- `docs/superpowers/specs/README.md`
- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-reference-mod-capability-mapping-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-core-dialogue-memory-social-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-group-propagation-expansion-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-world-generation-and-tooling-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-afw-governance-overlay-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-runtime-module-interface-and-commit-contract-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-phase-backlog-and-delivery-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-host-integration-map-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-feature-reference-implementation-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-rebuild-and-implementation-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-hook-session-projector-executor-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-current-code-retirement-and-cutover-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-reference-unit-to-repo-landing-map-appendix.md`
- `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
- `docs/superpowers/contracts/runtime/cloud-prompt-assembly-contract.md`
- `docs/superpowers/contracts/runtime/canonical-history-sourcing-contract.md`
- `docs/superpowers/contracts/runtime/runtime-local-vs-title-adapter-boundary-contract.md`
- `docs/superpowers/contracts/runtime/runtime-local-endpoint-catalog-contract.md`
- `docs/superpowers/contracts/runtime/hosted-narrative-endpoint-contract.md`
- `docs/superpowers/contracts/runtime/private-dialogue-request-contract.md`
- `docs/superpowers/contracts/runtime/remote-direct-request-contract.md`
- `docs/superpowers/contracts/runtime/group-chat-turn-request-contract.md`
- `docs/superpowers/contracts/runtime/thought-request-contract.md`
- `docs/superpowers/contracts/runtime/private-dialogue-commit-state-machine-contract.md`
- `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
- `docs/superpowers/contracts/product/launcher-auth-session-contract.md`
- `docs/superpowers/contracts/product/stardew-mod-package-install-update-rollback-contract.md`
- `docs/superpowers/contracts/product/support-ticket-and-diagnostic-bundle-contract.md`
- `docs/superpowers/contracts/product/launcher-notification-feed-contract.md`
- `docs/superpowers/contracts/product/repair-update-recheck-state-machine.md`
- `docs/superpowers/contracts/product/launcher-launch-orchestration-state-machine.md`
- `docs/superpowers/contracts/product/launcher-account-surface-state-machine.md`
- `docs/superpowers/contracts/product/stardew-launcher-workspace-ia.md`
- `docs/superpowers/contracts/product/supervisor-preflight-input-matrix.md`
- `docs/superpowers/contracts/product/redeem-request-and-receipt-contract.md`
- `docs/superpowers/contracts/product/launcher-product-catalog-visibility-contract.md`
- `docs/superpowers/contracts/product/player-entitlement-visibility-contract.md`
- `docs/superpowers/contracts/product/purchase-handoff-state-machine.md`
- `docs/superpowers/contracts/product/support-closure-state-machine.md`
- `docs/superpowers/contracts/product/launcher-support-surface-flow.md`
- `docs/superpowers/contracts/product/launcher-game-settings-surface-contract.md`
- `docs/superpowers/contracts/runtime/stardew-phone-contact-entry-contract.md`
- `docs/superpowers/contracts/runtime/stardew-phone-contact-list-contract.md`
- `docs/superpowers/contracts/runtime/stardew-npc-panel-bundle-contract.md`
- `docs/superpowers/contracts/runtime/stardew-remote-direct-thread-contract.md`
- `docs/superpowers/contracts/runtime/stardew-remote-direct-availability-state-machine-contract.md`
- `docs/superpowers/contracts/runtime/stardew-group-chat-session-contract.md`
- `docs/superpowers/contracts/runtime/stardew-onsite-group-overlay-contract.md`
- `docs/superpowers/contracts/runtime/stardew-phone-group-thread-contract.md`
- `docs/superpowers/contracts/runtime/stardew-item-instantiation-creator-contract.md`
- `docs/superpowers/contracts/runtime/stardew-item-gift-and-trade-host-executor-contract.md`
- `docs/superpowers/contracts/runtime/stardew-proactive-dialogue-trigger-state-machine-contract.md`
- `docs/superpowers/contracts/runtime/stardew-auto-action-and-schedule-restore-contract.md`

辅助参考：

- `docs/superpowers/specs/attachments/2026-03-27-superpowers-platform-control-plane-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-superpowers-client-runtime-topology-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-m1-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-capability-flow-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-hook-mapping-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-context-summary-fields-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-surface-commit-trace-failure-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-26-openaiworld-cross-host-ai-reproduction-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-26-openaiworld-ai-reproduction-master-manual.md`
- `docs/superpowers/specs/attachments/2026-03-26-stardew-ai-mod-feature-borrowing-from-ggbh-openaiworld.md`
- `docs/superpowers/specs/attachments/2026-03-26-stardew-seed-item-text-only-reference.md`

固定规则：

1. 辅助参考不得再被当成当前上位 authority。
2. 若辅助参考与当前正式 authority 冲突，以当前正式 authority 为准。
3. 当前 title 下的本地 prompt builder、本地 prompt catalog、本地嵌入 prompt 资产，只视为：
   - `retired implementation`
   不再作为正式 authority。

实现 / review / gate 前置提醒：

- 当前 title 的实现、review、`RC / GA` 仍必须先回链：
  - `docs/superpowers/governance/current-phase-boundary.md`
  - `docs/superpowers/contracts/product/narrative-base-pack-contract.md`
  - `docs/superpowers/contracts/product/capability-claim-matrix.md`
  - `docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md`
  - `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
  - `docs/superpowers/contracts/product/launcher-auth-session-contract.md`
  - `docs/superpowers/contracts/product/stardew-mod-package-install-update-rollback-contract.md`
  - `docs/superpowers/contracts/product/support-ticket-and-diagnostic-bundle-contract.md`
  - `docs/superpowers/contracts/product/launcher-notification-feed-contract.md`
  - `docs/superpowers/contracts/product/repair-update-recheck-state-machine.md`
  - `docs/superpowers/contracts/product/launcher-launch-orchestration-state-machine.md`
  - `docs/superpowers/contracts/product/launcher-account-surface-state-machine.md`
  - `docs/superpowers/contracts/product/stardew-launcher-workspace-ia.md`
  - `docs/superpowers/contracts/product/supervisor-preflight-input-matrix.md`
  - `docs/superpowers/contracts/product/redeem-request-and-receipt-contract.md`
  - `docs/superpowers/contracts/product/launcher-product-catalog-visibility-contract.md`
  - `docs/superpowers/contracts/product/player-entitlement-visibility-contract.md`
  - `docs/superpowers/contracts/product/purchase-handoff-state-machine.md`
  - `docs/superpowers/contracts/product/support-closure-state-machine.md`
  - `docs/superpowers/contracts/product/launcher-support-surface-flow.md`
  - `docs/superpowers/contracts/product/launcher-game-settings-surface-contract.md`
  - `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
  - `docs/superpowers/contracts/runtime/cloud-prompt-assembly-contract.md`
  - `docs/superpowers/contracts/runtime/canonical-history-sourcing-contract.md`
  - `docs/superpowers/contracts/runtime/runtime-local-vs-title-adapter-boundary-contract.md`
  - `docs/superpowers/contracts/runtime/runtime-local-endpoint-catalog-contract.md`
  - `docs/superpowers/contracts/runtime/hosted-narrative-endpoint-contract.md`
  - `docs/superpowers/contracts/runtime/private-dialogue-request-contract.md`
  - `docs/superpowers/contracts/runtime/remote-direct-request-contract.md`
  - `docs/superpowers/contracts/runtime/group-chat-turn-request-contract.md`
  - `docs/superpowers/contracts/runtime/thought-request-contract.md`
  - `docs/superpowers/contracts/runtime/private-dialogue-commit-state-machine-contract.md`
  - `docs/superpowers/contracts/runtime/deterministic-command-event-contract.md`
  - `docs/superpowers/contracts/runtime/narrative-degradation-contract.md`
  - `docs/superpowers/contracts/runtime/trace-audit-contract.md`
  - `docs/superpowers/contracts/runtime/stardew-phone-contact-list-contract.md`
  - `docs/superpowers/contracts/runtime/stardew-npc-panel-bundle-contract.md`
  - `docs/superpowers/governance/evidence-review-index.md`
  - `docs/superpowers/governance/client-exposure-threat-model.md`
  - `docs/superpowers/governance/waivers/narrative-base-pack-waiver-register.md`

---

## 0. Stardew 固定 7 层落位

`Stardew` 当前固定按这条链工作：

- `Cloud`
  - 持有 `stardew-valley` 自己的 prompt 资产明文
  - 持有聊天正本、记忆正本、审计明文
  - 负责 prompt 编排与 provider 通信
- `Launcher`
  - 承接账号、产品与兑换、通知、支持与帮助、游戏工作区
  - 承接 Stardew 的 mod 下载、安装、更新、反馈与帮助入口
- `Launcher.Supervisor`
  - 负责启动前检查、SMAPI / 前置框架检测、readiness verdict、修复、更新、Runtime.Local 拉起
- `Runtime.Local`
  - 负责统一入口检查、deterministic gate、trace、health、recovery
- `Runtime.Stardew Adapter`
  - 负责 Stardew 事实冻结、字段映射、执行清单翻译
- `Superpowers.Stardew.Mod`
  - 负责宿主取数、宿主 UI、最终宿主写回
- `Stardew Valley`
  - 作为最终真实世界状态

固定不允许：

- `Superpowers.Stardew.Mod` 本地拼最终 prompt
- `Runtime.Local` 直接懂宿主全部对象细节
- `Launcher` 自己生成第二套 readiness 或 entitlement 真相
- `Cloud` 直接跳过 Mod 改宿主

### 0.1 Stardew 当前代码处置规则

`Stardew` 当前固定采用：

1. 宿主 UI / hook / transport 壳保留
2. 本地 prompt 资产 / prompt builder / memory prompt builder 退役
3. 新正式主链固定改为：
   - `Cloud 编排`
   - `Runtime.Local gate`
   - `Runtime.Stardew Adapter 翻译`
   - `Superpowers.Stardew.Mod 最终写回`

正式总地图见：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-host-integration-map-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-current-code-retirement-and-cutover-appendix.md`

## 1. M1 Core Profile

### 0.1 Sellability Blocking Rule

当前文件集只定义 `Stardew` 的 working design / implementation scope，不定义可销售基础包闭环。

固定规则：

- 当前 profile 默认不得被解释为：
  - Narrative Base Pack 完整支持证明
  - pack-level shorthand 依据
  - sellable SKU 依据
- 若 `group_chat` 仍为 `implementation_only`，或 `information_propagation / active_world` 仍为 `not-in-phase / annex-only`，则当前 title 默认只能被视为：
  - `not_listed`
  - 或必须等待 `sku-entitlement-claim-matrix + capability-claim-matrix + waiver register` 闭口后再讨论 `sellable_with_disclosure`
- 在上述产品 artifacts 与 waiver/disclosure 未落地前，本 profile 只能作为：
  - design truth source
  - implementation truth source
  - evidence binding truth source

### 1.0 UI/UX Basis

当前 `Stardew` 玩家可见 surface 固定采用 `ui-ux-pro-max` 作为 UI hard gate 依据。

本 title 的明确 basis 如下：

- visual direction：
  - 原版风格优先
  - 叠加式 AI surface，不破坏原版宿主阅读节奏
  - 生活态 / 社交态优先，不做后台术语面
- accessibility：
  - 对话框、信息面板、联系人入口、手机私信、群聊入口都必须有可读标签
  - 失败/开放状态/空态不得只用颜色表达
  - 主要按钮与入口必须可键盘/手柄聚焦
- responsive / layout：
  - PC 常规分辨率与窄窗口都不得遮挡主 CTA、主状态、主恢复入口
  - 面板切换 NPC、切换 Tab、打开手机线程时不得丢失当前主要上下文
- interaction / feedback：
  - 私聊、群聊、当前想法、面板加载、手机私信、问题提交都必须有显式 loading / success / failure feedback
  - convenience action 与 authoritative recovery path 必须可区分
- empty / failure / delayed / recovery surfaces：
  - `空态`
  - `未开放`
  - `失败`
  - `检查中`
  - `只提交文字说明`
  都属于 title 级必须显式设计的玩家可见 surface

### 1.1 First Visible Host

`Stardew` 的当前 `M1` 玩家可见宿主按两层固定：

1. `M1 core profile` 默认 baseline visible surface
   - `宿主原对话 surface`
   - `AI 私聊对话框 surface`
   - `NPC 信息面板 surface`
   - `NPC 当前想法 surface`
   - `邮件 / 奖励提示 / tooltip 物品文本 surface`
2. `M1 implementation_only` 条件 visible surface
   - `手机私信 surface`
   - `现场群聊气泡 / 输入 surface`
   - `手机主动群聊 surface`

补充说明：

- 前一层属于当前 `M1 core profile` 的强制 visible surface
- 后一层属于当前 `M1 implementation_only` visible surface
- `M1 implementation_only` 表示当前 phase / 当前 title 要求实现、联调、review、留证据，但它本身不自动变成当前 exit criteria、sellability 或 pack-level shorthand
- `M1 implementation_only` visible surface 只有在当前 build / title 配置明确启用并满足 disclosure / evidence 条件时，才允许实际对玩家露出
- 若当前该露出同时触发 `current-phase-boundary` 下的 waiver gate，则还必须存在有效 waiver
- `information_propagation`、`active_world / world_event` 仍不属于当前 `M1` 的必做 visible surface
- `NPC 信息面板 surface` 与 `NPC 当前想法 surface` 在 Stardew 中是一等玩家可见面
- 这些 surface 属于 Stardew 宿主绑定，不上拉成 shared UI schema

### 1.2 Failure Surface

`M1` 默认失败露出规则如下：

- `AI 私聊` 失败时，在当前原版风格自定义对话框中用角色内大白话明确提示，并且句尾必须带：
  - `(ai回复失败)`
- `手机私信` 若频道已启用但本轮失败，必须沿用该远程频道自己的失败 copy，不得冒充本地 `private_dialogue` committed
- `现场群聊` 与 `主动群聊` 失败时，必须按单句 / 单条失败处理，不得整场静默吞掉
- `NPC 当前想法` 失败时，不回写到普通对话历史，也不冒充正常发言
- `NPC 信息面板` 或其展示型 Tab 失败时，只在当前面板内大白话说明，不静默吞掉
- `M1 implementation_only` surface 也必须绑定 committed / failure / recovery / trace evidence，但不得倒推出它已经进入当前外部 support claim
- `M2+ annex` 能力若单独启用，必须沿用 annex 中的失败与恢复规则，但不得倒推出“它已经是 `M1` ship-gate”

### 1.3 Recovery Entry

`Stardew` 的恢复入口在当前阶段固定为：

- 玩家前台：
  - `游戏 -> 帮助与修复`
  - `支持与帮助`
- 桌面管理端：
  - `Stardew 游戏配置页`

其中：

- `AI 私聊` enable/disable 属于 per-game 配置
- `群聊` 与 `手机私信` 若提供局部重试入口，只属于 surface convenience action；authoritative recovery path 仍回到：
  - `游戏 -> 帮助与修复`
  - `支持与帮助`
- `NPC 主动拉群聊`、`information_propagation`、`active_world / world_event` 若进入试验，只能放在 `experimental / annex` 区，不得计入 launch readiness gate
- `当前想法` 与 `NPC 信息面板` 不作为配置开关

### 1.3A 逐 Surface 失败 / 恢复矩阵

| Surface | failureClass | 玩家先看到哪里 | authoritative recovery |
| --- | --- | --- | --- |
| `AI 私聊对话框` | `render_failed` | 当前 AI 对话框 | `游戏 -> 帮助与修复` |
| `NPC 信息面板` | `render_failed` | 当前信息面板 | `游戏 -> 帮助与修复` |
| `当前想法` | `render_failed` | 当前想法区域 | `游戏 -> 帮助与修复` |
| `手机私信` | `availability_blocked` / `submission_failed` / `render_failed` | 当前私信线程 | `游戏 -> 帮助与修复` |
| `现场群聊` | `submission_failed` / `render_failed` | 当前群聊输入框或当前句气泡位 | `游戏 -> 帮助与修复` |
| `手机主动群聊` | `submission_failed` / `render_failed` | 当前手机群聊线程 | `游戏 -> 帮助与修复` |
| `群聊历史 Tab` | `render_failed` | 当前信息面板 Tab | `游戏 -> 帮助与修复` |
| `Stardew 游戏配置页状态` | `refresh_failed` | Launcher 的 Stardew 配置页 | `Stardew 游戏配置页` |
| `问题包提交` | `diagnostic_export_failed` / `diagnostic_redaction_failed` | Launcher 的支持与帮助页 | `支持与帮助` |

固定规则：

1. surface 内局部重试只算 convenience action。
2. authoritative recovery 只认上表。
3. 更细的 hook、类、路径、trace join 统一回链：
   - `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-host-integration-map-appendix.md`

### 1.3B 等待文案与失败文案规则

当前 `Stardew` 的等待与失败 copy 固定采用：

1. 等待文案必须是：
   - 角色内
   - 世界内
   - 大白话
2. 等待文案不允许直接出现：
   - `正在回应`
   - `处理中`
   - `pending_visible`
   - `committed`
3. 等待文案必须采用模板池随机轮换，例如：
   - `海莉正在想该怎么回答你，你先别催她……`
   - `海莉像是在组织语言，正琢磨怎么把话说出口……`
4. 失败文案必须同时满足：
   - 角色内表达
   - 明确告诉玩家这轮失败
   - 句尾固定带 `(ai回复失败)`
5. `(ai回复失败)` 固定放在句尾，不随机改写，不翻译成别的写法。

示例：

- `糟糕，海莉大惊失色，哎呀，牛哥，你快检查一下，我的脑子掉啦！(ai回复失败)`
- `海莉拍了拍自己的脸，像是忽然断线了。牛哥，你快帮我看看！(ai回复失败)`

### 1.4 Visible Surface Bindings

#### 1.4.1 Host Dialogue Surface

- 原版 / SVE / 东斯卡普等宿主对话优先于 AI 对话
- 只要当前还有宿主既有对话可说，AI 对话必须完全让路
- 宿主对话在玩家实际看见后，立即创建宿主对话记录
- 宿主对话记录按参考链至少保留：
  - 日期
  - 地点
  - 天气

#### 1.4.2 AI Private Dialogue Surface

- 采用叠加模式，不替代原版或扩展 Mod 对话
- 默认宿主是：
  - `原版风格的自定义对话框`
- 玩家提交后，若云端结果尚未回来，必须先进入角色化等待态，而不是显示系统 loading 术语
- `头顶气泡闲聊` 若保留，只能作为当前 `private dialogue` turn 的补充投影视图
- `头顶气泡闲聊` 不是独立 committed carrier，也不单独决定 `private_dialogue` 的 success / failure / recovery
- 进入 AI 对话前，刚刚那轮宿主对话记录进入 recent private history
- 纯文本 reply 可以先显示为玩家可见结果，再在后台完成 committed 升级
- 这不允许倒推出：
  - 关系已经变化
  - 物品已经给出
  - 事件已经发生
- committed 条件：
  - 只有在玩家可见对话框完成渲染后，才算 committed

#### 1.4.3 Remote Direct One-to-One Surface

- `手机私信 surface` 属于独立的 `remote_direct_one_to_one` 远程一对一频道
- 它在产品族谱上属于 `dialogue family`
- 但在 claim / waiver / sellability artifact 中，仍必须回链到 canonical capability key `dialogue`
- `remote_direct_one_to_one` 只作为 `dialogue` 的 channel implementation dimension 使用
- 它不是点击 NPC 后本地 `private_dialogue` 的同一可见 carrier
- 它也不是手机主动群聊那类多方远程房间
- 它必须保留独立的 channel rules、carrier routing 与 committed 语义
- 它在编排层不是另一套独立 prompt family，而是同一 private/direct router 的远程 carrier 分支
- 但 accepted remote turn 仍必须写入与 `private dialogue` 共享的 actor-owned direct-message / private-message history truth，而不是另起一套脱离参考链的独立记忆真源
- first-stage `remote_direct_one_to_one` 的 availability 结果固定只有两种：
  - `available_now`
  - `unavailable_now`
- 以下情况统一归为 `unavailable_now`：
  - 睡觉
  - 节日 / 剧情锁定
  - 电话占用或当前宿主不允许远程接入
- 第一阶段不做 delayed delivery，不做 deferred send queue
- 若 `unavailable_now`：
  - 不创建 `pending_visible` remote turn record
  - 不创建待投递队列
  - 只保留输入侧 trace 与当前线程内的显式 unavailable result
- 手机私信线程 key 固定为：
  - `gameId + actorId + targetId + channelType`
- 关闭并重新打开同一 NPC 的手机私信时，必须复用同一线程 key
- `DayStarted` 只重算 availability，不自动重发上一条 unavailable message
- 它仍属于当前 `M1 implementation_only`，必须实现 committed / failure / recovery / trace contract，但不得被包装成当前 `supported` support claim

#### 1.4.4 NPC Info Panel Surface

- 以覆盖在当前游戏画面上的独立面板形式展示
- 默认入口：
  - AI 对话框中的 `查看信息`
- 额外入口：
  - 手机联系人 / 角色列表
- `M1` 范围先覆盖：
  - 全部原版 NPC
  - 玩家的宠物
  - 玩家的孩子
- 暂不覆盖其他内容扩展 Mod 的新增 NPC
- 只有一个固定面板，不支持多面板并行
- 可通过下拉或头像点选动态切换 NPC
- 切换 NPC 时保留当前 Tab

#### 1.4.5 NPC Thought Surface

- `当前想法` 是独立的 thought surface
- 不是普通对话 surface
- 不写入普通对话历史
- 默认打开面板时生成
- 允许短时间缓存防抖
- 默认以流式或伪流式方式显示
- 只显示当前一段内心独白
- 不允许玩家在 thought surface 中直接输入打断为双向对话
- 若玩家在 thought 请求未完成时切换 NPC：
  - 原请求必须标记为 stale
  - 原结果不得 commit 到新 NPC 面板
  - 当前面板必须立即对新 NPC 重新请求或显示占位
- committed 条件：
  - 在信息面板中完整显示完成后 committed

### 1.5 NPC Info Panel Binding

`NPC 信息面板` 固定为：

- 上方基础信息区
- 下方 Tab 区

顶部基础信息区必须：

- 偏生活态 / 社交态
- 全部使用大白话
- 不出现术语

顶部至少展示：

- 头像 / 立绘
- 名字
- 当前地点
- 当前状态摘要
- 与玩家关系摘要
- 当前想法入口或简短可见位

下方 Tab 固定为：

1. `记忆`
2. `关系`
3. `当前想法`
4. `物品`
5. `聊天`
6. `群聊历史`

#### 1.5.1 记忆 Tab

- `M1` 只展示当前 NPC 对玩家的记忆摘要
- 不展示原始会话流水
- 仍按时间桶展示
- 每个时间桶显示为一张记忆卡片

#### 1.5.2 关系 Tab

- 不再采用关系图谱
- 采用关系标题分组 + 头像/名字列表
- 分组标题必须是玩家可理解的生活化文案
- 默认优先展示：
  - `最亲近的人`
  - `家里人`
  - `朋友`
  - `最近常来往`
  - `不太来往的人`
- 每组只展示当前最重要的一批人物
- `M1` 只做展示，不提供快捷互动

#### 1.5.3 当前想法 Tab

- 只显示当前一段内心独白
- 不做历史想法记录
- 不显示 `pending / stale / committed` 这类术语
- 玩家可见文案固定为：
  - `loading`：角色内等待文案模板池
  - `failure`：角色内失败文案，且句尾固定带 `(ai回复失败)`
  - `stale`：不把旧结果冒充当前 NPC 的内容

#### 1.5.4 物品 Tab

- 只展示当前 NPC 与玩家之间有关联的物品
- 固定包括：
  - 送给玩家的
  - 从玩家收到的
  - 借出 / 借入的
  - 在任务 / 承诺中提到过的
- 优先采用原版风格物品格子
- 图标沿用原版素材
- 名字 / 描述允许按当前实例的 AI 语境自定义
- 下方详情区只展示当前选中物品的名字、描述和与该 NPC 的关联说明
- 必须支持鼠标 hover 和左键选中
- `M1` 只做展示，不提供快捷互动

#### 1.5.5 聊天 Tab

- `聊天` 只用于回看玩家与当前 NPC 的最近私聊记录
- authoritative 数据来源固定为当前 NPC 与玩家之间的 actor-owned private/direct history
- 按天分组展示
- 默认每一天只显示前几条
- 超出显示 `……还有 N 条` 或等价提示
- 鼠标左键点击当天标题区或 `查看更多` 行可展开 / 收起
- 展开后只允许在内容区内部滚动，不得增高整窗
- 空态表示当前没有可展示的私聊记录，不得拿它替代失败
- `聊天 Tab` 不绑定 disclosure 枚举；它只受私聊历史存在与否控制

#### 1.5.6 群聊历史 Tab

- `群聊历史` 默认只承接已有持久化群聊记录
- authoritative 数据来源固定为 committed group turn records
- 若当前时间窗口没有 committed group turn，Tab 可为空态
- 空态只表示当前窗口无记录，不得拿它替代 `group_chat` 未实现
- 若当前 build 中 `group_chat` 对玩家仍未开放或当前角色没有进入已开放范围，Tab 必须显示单独的大白话开放状态提示
- 该开放状态提示不等同于空态，也不等同于失败
- 有记录时，按天分组展示
- 默认每一天只显示前几条
- 超出显示 `……还有 N 条` 或等价提示
- 鼠标左键点击当天标题区或 `查看更多` 行可展开 / 收起
- 展开后只允许在内容区内部滚动，不得增高整窗

`groupHistoryDisclosureState` 固定为当前 `群聊历史 Tab` 的 authoritative disclosure truth：

- `open_for_player`
- `not_open_for_player`

规则：

- `groupHistoryDisclosureState` 由 `NpcInfoPanelSurfaceSession` 基于当前 `BuildExposurePolicy` 与 title exposure rule 解析
- `群聊历史 Tab surface` 中的 `disclosureState` 固定回链到 `groupHistoryDisclosureState`
- 不允许 UI 层、Runtime 层、Mod 层各自发明不同 disclosure 枚举

### 1.6 Private Dialogue Binding

`Stardew` 的 `private dialogue` 绑定固定如下：

- 采用 recovered `OpenAIWorld` 私聊主链作为真相源
- 宿主对话与 AI 私聊是同一条能力链的不同宿主入口
- 第一阶段在当前 phase 已批准范围内，优先保持 recovered 私聊链的记录、回放与记忆回灌语义，避免过早发明新语义
- 宿主原对话先被归一化成 source-equivalent host-derived private-history record，再进入 AI 私聊链和记忆压缩链
- 上述 host-derived record 至少要保留：
  - actor / target
  - sourceCategory 或等价 message type
  - date
  - location
  - weather
- canonical input 组装至少包含：
  - actor snapshot
  - target snapshot
  - actor-relative relation snapshot from actor to target
  - current scene snapshot
  - recent private history
  - optional long-memory summary
  - optional player utterance / trigger text
  - Stardew 宿主摘要
  - behavior / progression prompt bundle
- `behavior / progression prompt bundle` 至少包含：
  - world rules
  - private dialogue channel rules
  - behavior protocol
  - relationship / progression guidance
- 模型侧保留 source-faithful `actions[]` 语义
- 第一阶段必须先完整保留 source-style `actions[]` 作为 authoritative accepted outcome 的一部分
- 当前 `M1` host-apply allowlist 固定只包括：
  - `render_command`
  - `transactional_command`
- 对当前未批准或当前 title 尚未安全落地的 source-style action，必须形成显式 blocked / deferred deterministic outcome，不得静默丢弃
- 每轮私聊必须形成：
  - one canonical replay envelope
  - mirrored writeback
  - actor-local projections
  - sidecar JSON 或等价 `diagnostic_sidecar`
- orchestration-side canonical replay envelope 与 memory coordination copy 固定归服务器侧 canonical history / memory store 所有
- actor-owned private/direct history identity 与回放键必须在本地私聊、远程一对一、群聊投影之间保持 source-equivalent 稳定性
- cross-channel actor-owned private/direct history 的 authoritative join key 固定为：
  - `historyOwnerActorId + canonicalRecordId`
- `messageIndex`
  - 只允许作为单线程内排序字段
  - 不允许作为跨 channel dedupe / replay / audit 的 authoritative join key
- 任一 projection record 若存在自己的局部 id，必须同时携带：
  - `historyOwnerActorId`
  - `canonicalRecordId`
  - `sourceChannelType`
  - `projectionKind`
- `Runtime` 只保留：
  - trace-linked local projection
  - host replay cache
  - deterministic apply evidence
- history replay 时，必须按 `historyOwnerActorId + canonicalRecordId` 同时回灌：
  - 可见消息文本
  - accepted deterministic outcomes
  - 结构化 sidecar
- accepted deterministic outcomes 必须继续喂给 future memory compression

`M1-source-faithful` 与 `post-M1-platformize` 的边界固定为：

- `M1-source-faithful`
  - 输入、输出、repair / normalize、projector / executor、canonical replay、mirrored writeback 先按 source-style 主链跑通
  - 允许 title-local normalization
  - 不要求 framework-facing `action_intents[]` 成为当前必做前置
- `post-M1-platformize`
  - 才允许把 source-faithful 结果桥接为：
    - `action_intents[]`
    - `diagnostic_sidecar[]`
    - `propagation_intents[]`
    - `world_event_intents[]`

### 1.6A Minimal Mod -> Runtime Contract

当前 `M1-source-faithful` 下，`Mod -> Runtime` 的最小请求包固定为语义合同，不再等待后续补完：

- `privateDialogueRequest`
  - `requestId`
  - `gameId`
  - `actorId`
  - `targetId`
  - `triggerKind`
  - `hostDialogueRecordRef`
  - `sceneSnapshotRef`
  - `relationSnapshotRef`
  - `recentPrivateHistoryRef`
  - `hostSummaryRef`
- `remoteDirectRequest`
  - `requestId`
  - `gameId`
  - `actorId`
  - `targetId`
  - `channelType`
  - `threadKey`
  - `availabilityState`
  - `recentDirectHistoryRef`
  - `hostSummaryRef`
- `groupChatTurnRequest`
  - `requestId`
  - `gameId`
  - `groupSessionKey`
  - `contactGroupId`（条件适用）
  - `participantSetRef`
  - `currentSceneSnapshotRef`
  - `inputSequenceId`
  - `recentGroupHistoryRef`
  - `topicSeedRef`
  - `participantRelationsRef`
  - `hostSummaryRef`
- `thoughtRequest`
  - `requestId`
  - `gameId`
  - `npcId`
  - `surfaceId`
  - `sceneSnapshotRef`
  - `memorySummaryRef`
  - `hostSummaryRef`

这些字段是并行开发合同；底层 JSON / DTO 可以重命名，但不得减少语义位点。

大白话死规则：

- 上述请求包进入 `Runtime.Local` 后，只能继续作为结构化事实包与 gate 输入向上游流转
- `Cloud` 必须基于这些结构化事实包自己编排最终 prompt；`Runtime.Local`、`Runtime.Stardew Adapter`、`Superpowers.Stardew.Mod` 不允许拼最终 prompt
- `billingSource` 不管是 `user_byok` 还是 `platform_hosted`，都不改变这条主线；provider 通信仍固定由 `Cloud` 代表发起

允许进包的 raw 输入只限：

- 玩家本轮刚提交的原始文本
- 当前宿主原对话文本或宿主线程里本轮必需的原始文本
- 为 repair / normalize 必需的 title-local channel 元数据

绝对不许混进包的内容：

- prompt 模板正文
- 角色卡正文
- 世界规则正文
- provider 参数正文
- 过去某轮已经渲染好的完整 prompt
- 聊天 / 记忆 / 审计明文正本

绑定规则固定为：

- `hostSummaryRef` 固定指向 `hostSummaryEnvelope.summaryEnvelopeId`
- `summarySelectionHint` 固定与请求包同传，或可由同一请求包字段 deterministic 导出
- 不允许一端按“引用 envelope”，另一端按“内联 envelope”各自实现不同 wire shape

必需宿主 hook 类别：

1. NPC 交互开始
2. 宿主对话显示完成
3. 原对话耗尽判定
4. AI 对话框打开
5. AI 文本显示完成
6. AI 对话关闭

补充说明：

- 上述 hook 类别是正式需求
- 当前并行开发合同不再等待“具体 API 名字完全冻结”
- authoritative contract 固定为：
  - 先冻结语义 hook
  - 再由 hook-mapping appendix 给出 `SMAPI / menu lifecycle / Harmony patch` 的最终承载点

### 1.7 Memory Visible Evidence

底层记忆机制沿用参考 mod：

- raw history 与 summary memory 分层
- 长期记忆按时间桶 summary memory 组织

`Stardew` 的玩家可见记忆面在 `M1` 固定为：

- 只展示当前 NPC 对玩家的记忆摘要
- 不展示 NPC 对其他人的完整 actor-owned memory
- memory recall 必须可按：
  - `memoryKey`
  - `sourceSpanRef`
  - `timeBucket`
  回链到 durable memory identity
- 对当前月桶聚合记忆，`sourceSpanRef` 固定表示：
  - `memoryOwnerActorId + timeBucket`
  而不是单个 turn id
- owner-scoped summary memory key 固定为：
  - `memoryOwnerActorId + memoryKind + timeBucket`
- 记忆压缩与读取都不得跨越 `memoryOwnerActorId` 混入其他 actor 的经历、关系或世界视角

### 1.8 Item Delivery Binding

`Stardew` 的自定义物品 / 赠与链固定如下：

- 该链在 canonical capability / claim / waiver / sellability 上固定回链到：
  - `social transaction / commitment`
- 若 accepted `GiveItem` / `LendItem` / `Transaction` 进入当前阶段范围，其 authoritative runtime outcome 固定回链到：
  - `transaction_state_committed`
- 默认模板实例化优先
- 默认优先使用 `item.modData` 挂 AI 语境
- 玩家 first-perception carrier 固定按以下顺序选择：
  - `邮件`
  - `奖励提示`
  - `tooltip / 名称描述`
- `对话` 可作为补充叙事宿主，但不作为 authoritative first-perception carrier
- 默认发放路径：
  - 通过同一 accepted item/gift action bundle 先形成玩家可见文本 carrier
  - 再进入实际发放路径
- 实例级 AI 名称 / 描述默认只绑定当前实例，不修改全局模板
- 实例级名称 / 描述进入后续对话与记忆上下文
- 文本宿主成功显示只代表 `perceived`
- 只有以下三项同时成立时，才允许视为 item / gift committed：
  - 文本宿主成功显示
  - authoritative item-event record 已成立
  - 实际发放或明确 no-delivery / rejected outcome 已成立
- 只有 authoritative item-event record 已成立的实例，才允许进入后续对话 / 记忆 replay
- 文案不得与真实用途完全不符
- `邮件`、`奖励提示`、`tooltip / 名称描述` 属于不同文本宿主 carrier
- 它们的 committed / failure / recovery 语义不得在 surface contract 中被合并成一条未区分的统一口径
- `GiveItem`、`LendItem`、`Transaction` 若进入当前阶段范围，默认仍属于同一 accepted action bundle 的行为结果，不应被改写成“先讲一段文本、以后再决定是否真正给物”
- `itemRef` 的 authoritative join 定义固定为：
  - 若已创建实例：`gameId + itemInstanceId`
  - 若尚未创建实例或为 no-delivery / rejected：`authoritativeItemEventRecordId`

必需宿主 hook 类别：

1. 赠与结果显示
2. 实例物品创建
3. 背包 / 奖励落地
4. 实例文本覆写读取
5. 物品关联记录

### 1.9 Stardew-Specific Context Summary

以下内容作为 `Stardew` 宿主额外上下文输入：

- 玩家农场信息摘要
- 玩家物品摘要
- 玩家任务摘要
- 事件摘要
- 婚姻 / 恋爱 / 同居情况摘要
- 孩子摘要
- 宠物摘要
- 养殖动物摘要

规则：

- 这些摘要由 Mod 采集
- 由 Runtime 归一化并组装为 canonical input
- 采用事件驱动刷新 + 请求前重新取当前快照
- 不采用后台高频轮询

补充说明：

- 婚姻 / 恋爱 / 同居情况按当前宿主事实组织
- 不硬编码成原版单配偶假设
- 允许多婚 Mod 等宿主现实存在
- 孩子 / 宠物 / 养殖动物先按摘要事实处理
- 不在 `M1` 引入重型 actor graph

### 1.10 Runtime / Launcher Configuration Exposure

`Stardew` 游戏配置页在当前 `M1` 固定为少量关键开关 + 状态显示。

至少包含：

- `AI 私聊` 总开关
- 当前运行状态
- 最近问题摘要

不包含：

- `当前想法` 开关
- `NPC 信息面板` 开关
- 把 `group_chat` 写成 launch readiness 必填项的开关

补充规则：

- `AI 私聊` 关闭时，只关闭 AI 叠加层，不影响原版 / 扩展对话
- `NPC 主动拉群聊`、`information_propagation`、`active_world / world_event` 若启用，只能挂到 `experimental / annex` 配置区
- `Launcher` 对当前 title 的 authoritative 消费合同固定为：
  - 单一 readiness truth source:
    - `launchReadinessVerdict`
  - 派生消费输入:
    - `runtimeHealthFact`
    - `failureClass`
    - `recoveryEntryRef`
    - `最近问题摘要`
- `Launcher` 不自行拼接第二套 issue / recovery truth
- `Stardew 游戏配置页状态 surface` 也必须回链到同一 `launchReadinessVerdict`，不得只靠 `runtimeHealthFact` 独立出 verdict

### 1.10A Launcher 页面职责

当前 `Stardew` 在 `Launcher` 的页面职责固定为：

- `游戏页`
  - 显示 Stardew 安装状态、版本、当前支持状态、进入游戏入口
  - 显示 mod 下载、安装、更新、卸载入口
  - 显示最近一次 readiness 结果和一键修复入口
- `支持与帮助页`
  - 显示当前 title 的问题摘要、trace 回执、问题包导出/提交入口
  - 显示玩家能看懂的失败说明、恢复说明、反馈入口
- `修复页`
  - 承接 SMAPI / 前置框架缺失、版本不符、路径异常、Runtime.Local 未就绪等修复动作
  - 只消费 `Launcher.Supervisor` 给出的单一 readiness / recovery truth，不自己重算
- `mod 下载 / 更新页`
  - 承接 Stardew mod 包、依赖包、版本说明、安装结果、回滚入口
  - 不承接 prompt 资产下载，不暴露游戏 prompt 明文

固定不允许：

- `Launcher` 页面直接显示或缓存 Stardew prompt 资产明文
- `Launcher` 页面把 `billingSource` 写成“本地模型模式”
- `Launcher` 页面自己推断另一套支持状态、readiness、entitlement 真相

### 1.11 Trace Hooks And Committed Conditions

当前 `M1 core` 已定 committed 规则：

- `宿主原对话 surface`
  - 玩家实际看见且宿主对话记录已创建后 committed
- `AI 私聊对话框 surface`
  - 文本成功显示到原版风格自定义对话框后 committed
- `当前想法 surface`
  - 在信息面板中完整显示完成后 committed

当前 `M1 core` 已定 trace / record 关键点：

- 宿主原对话显示完成
- 宿主对话记录创建
- AI 对话框打开
- AI 文本显示完成
- canonical replay envelope 写入
- mirrored private-history projection 写入
- deterministic accepted outcome 落地
- accepted outcome 回灌记忆压缩

当前仍待补全但不得假装已定稿的点：

- hook 最终落位
- failure / recovery 细映射
- annex / debug 扩展用的更细粒度 join 补表

当前已冻结而不得再漂移的 failure / recovery 合同：

- `unavailable_now` -> `availability_blocked`
- `thread open fail` -> `render_failed`
- `single turn submit fail` -> `submission_failed`
- `stale thought` -> 不进入 committed，不得冒充 failure copy
- `surface refresh fail` -> `refresh_failed`
- `diagnostic export fail` -> `diagnostic_export_failed`
- `diagnostic redaction fail` -> `diagnostic_redaction_failed`

这些 `failureClass` 规则必须逐 surface 绑定到 surface contract，不得只停留在全局说明。

---

## 2. M1 Implementation-Only Channels And M2+ Annex Truth Sources

### 2.1 Group Chat / Remote Multi-Speaker Threads

`Stardew` 的 `group_chat` 设计事实继续保留，但当前 phase status 固定为：

- `M1 implementation_only`
- `required for current title implementation / review / evidence`
- `not current M1 exit criteria`
- `not current external support claim`

必须保留的参考 mod 主链真相源：

- `speaker selection`
- `order freeze`
- `per-speaker generation`
- `per-turn persistence`
- per-turn deterministic apply
- history replay from persisted records
- delivered turn mirrored into each participant's private history projection
- per-speaker generation 必须是 speaker-centric 的：
  - 带当前 speaker 对其他参与者的关系视角
  - 带当前 speaker 自己的 private-dialogue context
  - 带本轮已 accepted 的 earlier turns
  - 带本轮已 accepted 的 deterministic effects
- local group chat 与手机主动群聊只复用同一套两阶段生成核心；它们的 carrier、thread/session 规则与状态语义不得被默认视为完全同构
- `手机主动群聊` 当前只承诺复用 recovered group-chat 的 speaker-selection / per-speaker-generation 核心
- 第一阶段远程多方链路必须保留以下 recovered `ContactGroup` 持久化状态：
  - `contactGroupId`
  - per-group message bucket
  - `unreadCount`
  - `doNotDisturb`
  - raw source-style payload sidecar 或等价保真 sidecar

第一阶段额外原则：

- 对已经进入当前阶段的群聊链，优先保持 recovered 的轮次生成、持久化和历史投影语义
- 对尚未覆盖的 recovered 状态模型，必须明确标成“未覆盖”，而不是改写成另一套看起来更简单的新语义
- recovered 里的非玩家自主远程群聊活动，第一阶段不得被默认删除；若不对玩家主动打扰，也必须至少作为后台线程更新与 unread 增量保留

`Stardew` 宿主侧保留两类 surface：

1. `现场群聊`
2. `远程多方频道`

宿主绑定规则保留如下：

- `现场群聊`
  - 玩家 + NPC 总人数 `>= 3`
  - NPC 发言宿主：
    - `头顶气泡`
  - 玩家输入宿主：
    - `屏幕下方输入框`
- `主动群聊`
  - 通过右下角手机图标进入
  - 当前属于 `M1 implementation_only`，但不计入当前 exit criteria
  - 允许拉入不在现场的 NPC
- `NPC 主动拉群聊`
  - 只作为 per-game experimental 的主动打扰 / 前台弹出开关
  - 不得写成当前 `M1 core profile` 的强制项
  - 它不关闭 recovered 风格的后台远程群聊线程更新与 unread 增量

群聊 hook 类别仍保留为需求：

1. 现场 participant set 采样
2. participant 变化
3. 现场群聊气泡显示完成
4. 玩家输入提交
5. 手机主动群聊打开
6. 手机群聊消息显示完成
7. 群聊 surface 消失

当前最小 session 规则先固定为：

- 现场 `groupSession` 只在玩家主动发送后创建，不因单个气泡自然出现而反向创建
- 现场 participant set 算法固定为：
  - 当前 location 已加载
  - 当前可见
  - 当前可交互
  - 非 cutscene / dialogue lock
  - 与玩家距离不超过 `8` tiles
  - 按稳定 `actorId` 排序后冻结
- participant set 在当前轮冻结；加入 / 离开只影响下一轮
- `Warped` 或睡眠切日会结束现场 `groupSession`
- 气泡自然消失不回滚已 committed turn，只结束该句的局部可见面
- 手机主动群聊线程 key 固定为：
  - `gameId + contactGroupId`
- 后台 recovered 风格远程群聊活动可在玩家未打开线程时继续追加到同一 `contactGroupId`
- 玩家关闭并重新打开同一 `contactGroupId` 时，必须复用同一线程 key
- `DayStarted` 只做线程状态重建，不丢失同一 `contactGroupId` 的 unread 与 message bucket

当前明确待补而非已定稿的点：

- 现场范围 / 可见 / 可交互采样证据
- 手机群聊消息结构
- 群聊 failure / recovery 细映射

### 2.2 Information Propagation

`information_propagation` 当前必须明确为：

- `M2+ annex`
- `experiment-only`
- `not ship-gate for current M1`

最小治理口径：

- 它保留的是 source-faithful “真实消息投递”语义，而不是一段普通叙述文案
- 当前 `M1-source-faithful` 只保留 source-style `actions[] + deterministic validation`
- `propagation_intents[]` 只属于 `post-M1-platformize`
- 在显式 committed payload 落盘前，不得冒充 `propagation_committed`
- `ConveyMessage` 后必须继续执行：
  - target validation
  - 按可达性 / 媒介可用性选择本地私聊或远程 carrier
  - receiver-visible persisted record 写入
  - receiver-side 后续 AI 语境变化回灌
- committed propagation payload 至少要带：
  - `propagationId`
  - `sourceFactId` 或 `sourceEventId`
  - `deliveryMode`
  - `deliveryState`
  - `targetScope`
  - `originTraceId`
  - `hopCount`
  - `maxPropagationHops`
- 当前 `M1` 不得为了补 propagation 语义而新发明 shared command class

### 2.3 Active World / World Event

`active_world / world_event` 当前必须明确为：

- `M2+ annex`
- `experiment-only`
- `not ship-gate for current M1`

canonical capability 规则：

- `active_world` 是 claim / waiver / sellability 的 canonical capability key
- `world_event`、`world_event_intents[]`、`world_event_committed` 只允许作为 `active_world` 下的 event-generation / runtime outcome 术语
- 不得把 `world_event` 重新当成第二套 capability key

最小治理口径：

- 保留 recovered reference mod 的 “event creation, not just narration” 主语义
- 当前 `M1-source-faithful` 只保留 source-style `actions[] + deterministic validation`
- `world_event_intents[]` 只属于 `post-M1-platformize`
- 在显式 world-event payload 写入前，不得冒充 `world_event_committed`
- 接受后的 `WORLD_EVENT_PROPOSAL` 必须落成 durable event object 或 rejection record
- durable event object 至少要带：
  - `eventId`
  - `eventTypeId`
  - `affectedScope`
  - `lifecycleState`
  - `eventState`
- rejection / skip path 至少要带：
  - `rollbackHandle`
  - `skipOrFailureReason`
- 它必须通过宿主合法事件面暴露，并在玩家真正打开 / 交互后才能进入 `triggered`
- 当前 `M1` 不得为了 world-event 语义引入新的 shared command class

### 2.4 Explicit Pending Items

以下内容当前必须保留为待补，不得再与“已经完整设计链”混写：

- failure / recovery 细映射
- annex 与 debug 扩展用的更细粒度 join / replay 扩展表
- 极少量 `game-local custom item implementation` 的受控例外 annex

但以下内容不再属于待补，而属于当前并行开发正式合同：

- semantic hook 名称与语义
- `Mod -> Runtime` 最小请求包字段
- `Launcher` 只消费单一 `launchReadinessVerdict` 作为 readiness truth source；其余 `runtimeHealthFact / failureClass / recoveryEntryRef / 最近问题摘要` 只作为该 truth source 的派生消费输入
