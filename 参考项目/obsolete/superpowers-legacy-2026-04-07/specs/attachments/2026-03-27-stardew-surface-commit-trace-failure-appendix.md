# Stardew Surface Commit Trace Failure Design

> 状态说明：
> - 本文表格中的 `M1 core / M1 implementation_only / M2+ annex` 是历史 capability bucket，用于说明 surface 来源和继承关系。
> - 当前执行 phase 已不再直接定义为 `M1`；当前是否属于强制收口范围，以 `docs/superpowers/governance/current-phase-boundary.md` 为准。
> - 因此这里可以继续保留 `M1` bucket 名称，但不能再把它解释成“当前阶段仍然叫 M1”。

## 1. 文档定位

本文档用于把 `Stardew Valley` 已确认的宿主 surface，统一落成一张表：

- `surface`
- `phase status`
- `committed condition`
- `trace join`
- `failure copy`
- `recovery entry`

它承接：

- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-m1-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-capability-flow-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-hook-mapping-appendix.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`
- `docs/superpowers/contracts/runtime/deterministic-command-event-contract.md`

## 2. 目标

本文件的目标不是解释能力逻辑，而是让下面这些问题不再含糊：

- 哪个 surface 什么时候才算真的成功显示
- 当前 surface 属于 `M1 core` 还是 annex
- 失败时玩家到底在哪看到、看到什么
- trace 至少要能通过哪些 key 串起来
- 桌面前台和平台控制面如何对齐这些宿主可见面

## 3. 统一规则

### 3.1 大白话规则

所有玩家可见失败文案必须：

- 使用大白话
- 不出现术语
- 不要求玩家理解内部层次

### 3.2 Trace 规则

所有玩家可见 surface 的失败 / committed / 恢复，都必须至少能回链到：

- `traceId`

同时遵守 runtime trace contract 中的基础关联键要求。

### 3.3 Recovery 规则

默认恢复入口固定为：

- 玩家前台：
  - `游戏 -> 帮助与修复`
  - `支持与帮助`
- 桌面管理端：
  - `Stardew 游戏配置页`

annex 能力若单独启用，也必须沿用同一恢复入口体系，不得自创第二套玩家恢复口径。

surface 内的重试 / 刷新 / 重开按钮只属于 convenience action，不构成第二套 player-visible recovery authority。

### 3.3A Failure Class 规则

当前玩家可见 failure class 固定为：

- `render_failed`
- `submission_failed`
- `availability_blocked`
- `refresh_failed`
- `diagnostic_export_failed`
- `diagnostic_redaction_failed`

规则：

- 每个 surface 至少命中其中一个 failure class
- player-visible copy 必须能回链到 `traceId + failureClass`
- `failureClass` 由 Runtime / Launcher / Mod 共同消费，不得各自发明同义词

当前固定映射如下：

- `unavailable_now` -> `availability_blocked`
- `thread open fail` -> `render_failed`
- `single turn submit fail` -> `submission_failed`
- `surface refresh fail` -> `refresh_failed`
- `diagnostic export fail` -> `diagnostic_export_failed`
- `diagnostic redaction fail` -> `diagnostic_redaction_failed`
- `stale thought`
  - 不进入 committed
  - 不对玩家露出独立 failure copy
  - 只记录 trace 与 stale discard evidence

### 3.4 主结果与补充 trace 分层

以下区分必须明确：

- `主结果 trace`
  - 对应能力链真正的 committed / apply / history write
- `宿主补充 trace`
  - 例如切图、气泡消失、菜单关闭

宿主补充 trace 不得替代主结果 trace。

### 3.5 相位规则

本文件里的 surface 分三类：

- `M1 core`
- `M1 implementation_only`
- `M2+ annex / experiment-only`

`M1 implementation_only` surface 若当前 build 显式启用，仍应实现、联调、review、留证据；但它们不再是当前 `post-M1-reference-grade-hardening` 的默认 closeout blocker，也不得因此反推：

- 它已经进入当前 `M1` exit criteria
- 它已经自动进入当前 release claim scope

annex surface 可以保留完整设计真相源，但不得因此反推：

- 它已经进入当前 `M1` ship-gate
- 它已经自动进入当前 release claim scope

## 4. Surface 总表

| Surface | Historical capability bucket | Canonical capability key | Committed condition | 最小 trace join | Failure class | 玩家失败 copy | Authoritative recovery entry |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `宿主原对话 surface` | `M1 core` | `dialogue` | 原版/扩展 Mod 对话实际显示给玩家，且宿主对话记录已创建 | `traceId + gameId + channelType + commandId + historyOwnerActorId + canonicalRecordId + narrativeTurnId + surfaceId` | `render_failed` | 默认不单独露出 AI 失败文案；若宿主对话本身失败，按宿主错误面处理 | `游戏 -> 帮助与修复` |
| `AI 私聊对话框 surface` | `M1 core` | `dialogue` | AI 文本成功显示到原版风格自定义对话框后 committed | `traceId + requestId + commandId + historyOwnerActorId + canonicalRecordId + narrativeTurnId + surfaceId` | `render_failed` | “现在没法继续聊。” 之类的大白话提示，不出现术语 | `游戏 -> 帮助与修复` |
| `手机私信 surface` | `M1 implementation_only` | `dialogue` | 远程一对一频道的 committed record 已存在，且远程私聊 UI 成功显示文本后 committed | `traceId + requestId + commandId + historyOwnerActorId + canonicalRecordId + narrativeTurnId + remoteTurnId + surfaceId` | `availability_blocked` 或 `submission_failed` 或 `render_failed` | 大白话提示这次没发出去 | `游戏 -> 帮助与修复` |
| `现场群聊气泡 surface` | `M1 implementation_only` | `group_chat` | 对应 turn record 已存在且带 `groupTurnId + sequenceIndex + surfaceId`，并且该句气泡显示成功后 committed | `traceId + requestId + commandId + groupTurnId + sequenceIndex + historyOwnerActorId + canonicalRecordId + narrativeTurnId + surfaceId` | `render_failed` | 单句失败占位或大白话提示，不让整场群聊一起崩 | `游戏 -> 帮助与修复` |
| `现场群聊玩家输入 surface` | `M1 implementation_only` | `group_chat` | 玩家输入提交并成功入队后形成输入侧 trace；不单独构成 NPC 输出 committed | `traceId + requestId + commandId + inputSequenceId + surfaceId` | `submission_failed` | “这句话没发出去。” 之类大白话 | `游戏 -> 帮助与修复` |
| `手机主动群聊 surface` | `M1 implementation_only` | `group_chat` | 对应 turn record 已存在且带 `groupTurnId + sequenceIndex + surfaceId`，并且该条消息显示成功后 committed | `traceId + requestId + commandId + groupTurnId + sequenceIndex + historyOwnerActorId + canonicalRecordId + narrativeTurnId + surfaceId` | `submission_failed` 或 `render_failed` | 单条失败占位，不清空整个窗口 | `游戏 -> 帮助与修复` |
| `NPC 信息面板 surface` | `M1 core` | `dialogue` | 面板成功打开、当前 NPC 基础信息成功加载，且顶部 `当前想法` 入口或占位位已经可见 | `traceId + gameId + commandId + surfaceId + npcId` | `render_failed` | 大白话说明“信息暂时打不开” | `游戏 -> 帮助与修复` |
| `NPC 当前想法 surface` | `M1 core` | `dialogue` | 在信息面板中完整显示当前一段内心独白后 committed | `traceId + requestId + commandId + derivedThoughtRecordId + npcId + surfaceId` | `render_failed` | 大白话说明“现在看不清他在想什么” | `游戏 -> 帮助与修复` |
| `记忆 Tab surface` | `M1 core` | `memory` | 当前 NPC 对玩家的时间桶记忆卡片成功显示 | `traceId + commandId + npcId + memoryOwnerActorId + memoryKey + timeBucket + sourceSpanRef + surfaceId` | `render_failed` | 大白话说明“这部分记忆暂时读不出来” | `游戏 -> 帮助与修复` |
| `群聊历史 Tab surface` | `M1 core` 容器面 / `M1 implementation_only` 数据源 | `group_chat` | 最近时间窗口内存在 committed group turn 时：成功回放到面板；当前窗口无记录时：明确空态成功显示；当前 build 未对玩家开放群聊历史时：明确开放状态提示成功显示 | 有记录时：`traceId + commandId + npcId + groupTurnId + sequenceIndex + historyOwnerActorId + canonicalRecordId + narrativeTurnId + surfaceId`；空态时：`traceId + commandId + npcId + surfaceId`；开放状态提示时：`traceId + commandId + npcId + surfaceId + groupHistoryDisclosureState` | `render_failed` | 大白话说明“群聊记录暂时看不了”或“当前没有可回放的群聊历史”或“这类多人聊天记录会逐步开放” | `游戏 -> 帮助与修复` |
| `关系 Tab surface` | `M1 core` | `dialogue` | 关系图和右侧详情卡成功显示 | `traceId + commandId + npcId + relatedActorId + surfaceId` | `render_failed` | 大白话说明“关系信息暂时读不出来” | `游戏 -> 帮助与修复` |
| `物品 Tab surface` | `M1 core` | `social transaction / commitment` | 关联物品卡片墙成功显示 | `traceId + commandId + npcId + itemRef + surfaceId` | `render_failed` | 大白话说明“相关物品暂时看不了” | `游戏 -> 帮助与修复` |
| `邮件物品文本 surface` | `M1 core` | `social transaction / commitment` | 邮件文本成功显示，形成 `perceived` carrier success | `traceId + requestId + commandId + narrativeTurnId + transactionId + actorId + targetId + itemRef + surfaceId` | `render_failed` | 大白话说明“这封物品邮件暂时没送到” | `游戏 -> 帮助与修复` |
| `奖励提示物品文本 surface` | `M1 core` | `social transaction / commitment` | 奖励提示成功显示，形成 `perceived` carrier success | `traceId + requestId + commandId + narrativeTurnId + transactionId + actorId + targetId + itemRef + surfaceId` | `render_failed` | 大白话说明“这次奖励暂时没发到你手里” | `游戏 -> 帮助与修复` |
| `tooltip / 名称描述物品文本 surface` | `M1 core` | `social transaction / commitment` | tooltip 或实例级名称/描述已成功向玩家显示，形成 `perceived` carrier success | `traceId + requestId + commandId + narrativeTurnId + transactionId + actorId + targetId + itemRef + surfaceId` | `render_failed` | 大白话说明“这件东西的说明暂时显示不出来” | `游戏 -> 帮助与修复` |
| `Launcher startup surface` | `M1 core` | `launcher` | Launcher 主窗口成功打开，且 `Stardew` 入口卡片或主操作区已经可见，并已加载当前 `launchReadinessVerdict` | `traceId + gameId + surfaceId + launchReadinessVerdict + runtimeHealthRef` | `render_failed` 或 `refresh_failed` | 大白话说明“启动器现在没完全打开”或“当前状态暂时刷不出来” | `支持与帮助` |
| `Launcher key navigation surface` | `M1 core` | `launcher` | 从 Launcher 关键入口导航到目标页成功完成，且目标页主标题/主操作区已可见 | `traceId + gameId + surfaceId + navigationKey + targetRoute + launchReadinessVerdict` | `submission_failed` 或 `render_failed` | 大白话说明“这一步没打开成功” | `支持与帮助` |
| `Stardew 游戏配置页状态 surface` | `M1 core` | `dialogue` | 当前运行状态与最近问题摘要成功显示，且已回链当前 `launchReadinessVerdict` | `traceId + gameId + commandId + launchReadinessVerdict + runtimeHealthRef + surfaceId` | `refresh_failed` | 大白话说明“当前状态暂时刷不出来” | `Stardew 游戏配置页` |
| `问题包提交 surface` | `M1 core` | `dialogue` | 问题提交结果成功显示 | `traceId + requestId + commandId + gameId + surfaceId` | `diagnostic_export_failed` 或 `diagnostic_redaction_failed` | 大白话说明“当前无法附带问题包，只能提交文字说明” | `支持与帮助` |

## 5. 逐项补充说明

### 5.1 宿主原对话 surface

这是：

- 宿主对话记录的来源 surface
- AI 私聊 recent history 的上游 surface

它不是 AI surface，但必须进入 trace。

### 5.2 AI 私聊对话框 surface

它是 `Stardew` 私聊主宿主。

关键点：

- 只有显示完成后才 committed
- 失败时必须直接在当前对话框大白话提示
- 不静默失败
- 若存在 `头顶气泡闲聊` 之类补充投影，它也不单独决定当前 `private_dialogue` committed

### 5.3 `group_chat` implementation-only surfaces

参考 mod 的核心语义继续保持：

- 每句 / 每条只有在持久化记录已存在且显示成功后才 committed
- 失败也按单句 / 单条处理
- delivered turn 进入持久化与 private-history projection

但当前它们全部属于：

- `M1 implementation_only`

不能再被主文或 profile 反写成当前 `supported` claim 或当前 exit criteria。

### 5.4 当前想法 surface

`当前想法` 是独立 thought surface，但不是独立 thought provider chain。

因此：

- 它显示的是 `private_dialogue + inner_monologue` 模式返回的结果
- 不算普通 `dialogue_emitted`
- 不写入普通对话历史
- 失败时也必须用大白话，不冒充正常发言

### 5.5 NPC 信息面板及其各 Tab

信息面板本身是一个宿主 surface。

下属各 Tab 也都应有自己的：

- 显示完成点
- 最小 trace 关联
- 大白话失败 copy
- canonical semantic hook

当前固定 semantic hook 绑定如下：

- `NPC 信息面板 surface` -> `infoPanelRenderedAt`
- `记忆 Tab surface` -> `memoryTabRenderedAt`
- `群聊历史 Tab surface` -> `groupHistoryTabRenderedAt`
- `关系 Tab surface` -> `relationTabRenderedAt`
- `物品 Tab surface` -> `itemTabRenderedAt`

其中：

- `群聊历史 Tab` 本身可作为 `M1` 展示面存在
- 有记录时，按 persisted `groupTurnId + sequenceIndex + surfaceId` 回放
- 无记录时，合法结果是明确空态成功显示，而不是失败
- 空态只表示当前窗口无记录，不得替代 `group_chat` 未实现
- 当前 build 未对玩家开放该类记录时，必须显示单独的大白话开放状态提示，不得伪装成失败或普通空态

### 5.6 物品文本感知 surface

`M1` 下，物品链的第一感知优先是文本宿主，但不同文本宿主必须分开治理。

因此：

- 文本宿主成功显示
  只算玩家已真正感知到这次 AI 物品语境
- `邮件`、`奖励提示`、`tooltip / 名称描述`
  是三类不同 carrier，必须分别拥有 committed / failure / recovery 口径
- item / gift committed 仍需同时满足：
  - authoritative item-event record 已成立
  - 实际发放或明确 no-delivery / rejected outcome 已成立
- 不能只以“背包里出现了实例”就视为玩家感知成功
- 文本 carrier surface 的 committed 只表示 `perceived`
- `item / gift committed` 属于后续 transaction / authoritative item-event 条件，不与 carrier surface committed 混同

### 5.7 Launcher startup / key navigation surfaces

`Launcher startup and key navigation` 不是任意截图要求，而是正式 closeout 合同的一部分。

因此：

- `Launcher startup surface`
  - 必须证明主窗口、`Stardew` 入口和当前 readiness 状态已经真实可见
- `Launcher key navigation surface`
  - 必须证明关键导航动作确实到达目标页，而不是只拍到半路状态
- 两者都必须能回链：
  - committed 条件
  - failure class
  - trace join
  - recovery entry

## 6. 与 trace / audit contract 的衔接

本文件里的每条 surface 仍需遵守：

- `traceId`
- `requestId`
- `gameId`
- `channelType`
- `capability`
- `claimStateRef`
- `traceGroupId`

等基础关联要求。

玩家可见失败 copy 与 recovery copy 都必须能：

- 回链到 `traceId`

## 7. 当前仍未细化的点

以下内容仍可在后续专项继续细化：

- 每个 surface 的具体文案文本库
- Tab surface 的更细粒度 trace 字段
- `手机私信 surface` 的更细远程失败分类
- `邮件 / 奖励提示 / tooltip` 三类物品文本宿主的优先级细表
- `recovery_entry` 到桌面管理端动作的细映射

## 8. 最终结论

本设计的最终结论如下：

1. `Stardew` 的所有关键玩家可见面都必须有明确 committed 条件。
2. 玩家可见失败 copy 必须全部是大白话。
3. 所有失败 / committed / 恢复都必须能回链到 `traceId`。
4. `现场群聊`、`主动群聊`、`手机私信` 当前都必须明确标记为 `M1 implementation_only`，不能再和 `annex` 或 `M1 core` 混写。
5. `当前想法` 是独立 thought surface，不混入普通对话历史。
6. `NPC 信息面板` 及其各 Tab 都被视为正式宿主可见面，而不是附属调试页。
