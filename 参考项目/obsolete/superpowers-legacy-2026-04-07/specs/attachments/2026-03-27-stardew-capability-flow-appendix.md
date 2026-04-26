# Stardew Capability Flow Design

> 状态说明：
> - 本文保留 `M1 core / M1 implementation_only / M2+ annex` 作为能力来源分桶，不再把它们当作当前执行 phase 名称。
> - 当前执行 phase 以 `docs/superpowers/governance/current-phase-boundary.md` 为准；若它已切到 `post-M1-reference-grade-hardening`，则本文中的 `M1` 默认表示“上一阶段留下的基线能力桶”。
> - 因此本文可以继续说明哪些链源自 `M1 core`，但不能单独宣称“当前阶段仍然叫 M1”。

## 1. 文档定位

本文档用于把 `Stardew Valley` 的能力链写成可实现的端到端流程。

覆盖范围固定为：

1. `private dialogue`
2. `group chat`
3. `memory`
4. `自定义物品 / 赠与`
5. `information propagation`
6. `active world / world event`

它承接以下文档：

- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
- `docs/superpowers/specs/attachments/2026-03-27-superpowers-client-runtime-topology-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-m1-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-hook-mapping-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-context-summary-fields-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-surface-commit-trace-failure-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-26-openaiworld-ai-reproduction-master-manual.md`
- `docs/superpowers/specs/attachments/2026-03-26-stardew-ai-mod-feature-borrowing-from-ggbh-openaiworld.md`
- `docs/superpowers/specs/attachments/2026-03-26-stardew-seed-item-text-only-reference.md`

本文档不重新发明参考 mod 已经证明成熟的链路。

本文档只做三件事：

- 写清楚 `Mod -> Runtime -> Server -> Runtime -> Mod` 的实际顺序
- 写清楚 `Stardew` 必须自己补的宿主 binding、surface、committed 点、失败露出与 trace 点
- 明确哪些链源自上一阶段的 `M1 core`、哪些属于历史 `M1 implementation_only`、哪些只保留为 `M2+ annex / experiment-only`

## 2. 总原则

### 2.1 参考 mod 已证明的机制直接沿用

以下内容默认直接沿用 recovered reference mod：

- `private dialogue` 主链
- `group chat` 的 `speaker selection -> frozen order -> per-speaker generation` 主链
- `information propagation` 的真实消息投递语义
- `active world / world event generation` 的 event creation 语义
- `raw history + summary memory` 分层
- `month-bucketed summary memory`
- deterministic validation 先于宿主写回

### 2.2 Repo 集成 bridge 只属于 `post-M1-platformize`

模型侧继续保留 source-faithful 结果：

- `content`
- `actions[]`
- optional source-style propagation aliases
- optional parse diagnostics

`post-M1-platformize` 的 repo 集成侧才把 source-faithful 结果桥接为：

- `action_intents[]`
- `diagnostic_sidecar[]`

仅当对应 annex / experiment channel 已显式启用，且当前 turn 的 source-style alias 确实需要桥接时，才额外生成：

- `propagation_intents[]`
- `world_event_intents[]`

当前 `M1-source-faithful` 的固定规则是：

- 先跑通 source-faithful `actions[] + repair / normalize + projector / executor`
- 先验证 title-local host apply、canonical replay、mirrored writeback、memory 回灌
- 不把 repo bridge 作为 `M1` 主链前置条件

对 `post-M1-platformize` 而言，桥接完成后才允许进入 repo-governed deterministic validation。

当前 `M1` lowering 额外红线：

- 只允许落到 `render_command` 与 `transactional_command`
- 不得为了 `group_chat`、`information_propagation` 或 `active_world` 新发明 shared command class
- heavier lowering 只能属于 `M2+` 或批准过的 experiment annex

### 2.3 Stardew 只补宿主绑定

`Stardew` 这边只补：

- hook 点
- visible surface
- committed 条件
- failure exposure
- trace hooks
- 宿主摘要输入

### 2.4 相位状态

当前文档中的能力链分三类：

- `M1 core`
  - `private dialogue`
  - `memory`
  - `自定义物品 / 赠与`
- `M1 implementation_only`
  - `remote_direct_one_to_one`
  - `group chat`
- `M2+ annex / experiment-only truth source`
  - `information propagation`
  - `active world / world event`

`M1 implementation_only` 当前允许保留实现、联调、review 与证据；若当前 build 显式启用该能力，仍应按 visible host / failure / recovery / trace 收口。但它不再是当前 `post-M1-reference-grade-hardening` 的默认 closeout blocker，也不得自动写成当前 exit criteria 或外部 support claim。

`M2+ annex` 的设计真相源必须保留，但不得再写成当前 `M1` ship-gate。

补充分段：

- `M1-source-faithful`
  - 先跑 source-style 主链
- `post-M1-platformize`
  - 再把 source-style 主链桥接成 repo-facing abstraction

## 3. Flow A: Private Dialogue `[M1 core]`

### 3.1 通用链路

此能力沿用参考 mod 的 `private dialogue` 主链。

不再重定义：

- recent private history reinjection
- message record
- mirrored writeback
- canonical replay envelope
- sidecar JSON 或等价 diagnostic sidecar
- deterministic apply
- accepted outcomes -> future memory compression

### 3.2 Stardew 宿主绑定

- 原版 / SVE / 东斯卡普等宿主对话优先
- AI 对话采用叠加模式
- 原对话说完后，再次点击才进入 AI 对话
- AI 对话默认宿主是：
  - `原版风格的自定义对话框`
- `头顶气泡闲聊` 若保留，只能作为当前 `private dialogue` turn 的补充投影
- 它不单独决定当前 `private_dialogue` committed / failure / recovery
- `手机私信`
  - 不属于这条 `private dialogue` 本地主链
  - 它是独立的 `remote_direct_one_to_one` 远程频道

### 3.3 端到端流程

#### 阶段 1：宿主原对话接入

1. 玩家点击 NPC
2. Mod 让宿主先执行原版 / 扩展 Mod 对话
3. 宿主对话实际显示给玩家
4. Mod 将宿主对话归一化成 source-equivalent host-derived private-history record
5. 宿主对话记录进入 recent private history

说明：

- 宿主对话记录至少保留：
  - actor / target
  - sourceCategory 或等价 message type
  - 日期
  - 地点
  - 天气

#### 阶段 2：AI 对话触发

1. 玩家再次点击 NPC
2. Mod 判断当前宿主既有对话是否已耗尽
3. 若已耗尽，则打开原版风格自定义对话框
4. Mod 采集当前宿主上下文并交给 Runtime

#### 阶段 3：Runtime 组装

1. Runtime 读取：
   - actor snapshot
   - target snapshot
   - actor-relative relation snapshot from actor to target
   - current scene snapshot
   - recent private history
   - optional long-memory summary
   - optional player utterance / trigger text
   - Stardew 宿主摘要
2. Runtime 组装 canonical input
3. Runtime 绑定 prompt bundle：
   - world rules
   - private dialogue channel rules
   - behavior protocol
   - relationship / progression guidance
4. Runtime 调用 server-side private dialogue orchestration

#### 阶段 4：服务器编排

1. Server 使用参考 mod 私聊主链进行编排
2. 返回 source-faithful 结果：
   - rendered text
   - `actions[]`
   - optional propagation aliases
   - optional parse diagnostics

#### 阶段 5：Runtime source-faithful validation + lowering

1. Runtime 对结果做 parse / repair / normalization
2. 当前 `M1-source-faithful` 先保留 source-style：
   - `content`
   - `actions[]`
   - source-side parse diagnostics
3. Runtime 对 source-style accepted result 直接执行当前 title 的 deterministic validation
4. 当前 turn 的 source-style `actions[]` 必须完整保留在 accepted outcome / sidecar 中
5. 在 historical `M1-source-faithful` bucket 下：
   - host-apply allowlist 固定只包括 `render_command` 与 `transactional_command`
   - 对当前未批准或当前 title 尚未安全落地的 source-style action，必须形成显式 blocked / deferred deterministic outcome，不得静默丢弃

`post-M1-platformize` 才额外允许：

- `action_intents[]`
- `diagnostic_sidecar[]`
- `propagation_intents[]`
- `world_event_intents[]`

#### 阶段 6：服务器侧 pending write

1. Runtime 把 accepted turn envelope 回传到服务器侧 canonical history / memory store
2. 服务器侧先写入 `pending_visible` 的 canonical replay envelope
3. 服务器侧先写入 `pending_visible` 的 mirrored writeback truth
4. 服务器侧按 `historyOwnerActorId + canonicalRecordId` 写结构化 sidecar

补充规则：

- private dialogue / remote direct / group projected turn 的 `narrativeTurnId` 必须与 `canonicalRecordId` 保持同值或 deterministic 一一映射

#### 阶段 7：本地 projection / host display

1. Runtime 只保留 trace-linked local projection
2. Runtime 保留 host replay cache，保证宿主可做本地重放与证据回链
3. Mod 把 AI 文本显示到原版风格自定义对话框
4. 若存在可落地动作，则按 deterministic gate 已批准的结果执行
5. 对话结束后关闭当前轮 AI 对话 surface

#### 阶段 8：committed finalize / replay release

1. 只有在玩家可见文本显示成功后，服务器侧才把该 turn 从 `pending_visible` 升级为 committed / replay-eligible
2. 若 surface 失败，则该 turn 必须被标记为 `render_failed`、`not_replayable`
3. 未进入 committed / replay-eligible 的 turn：
   - 不得进入后续 recent history replay
   - 不得进入后续 memory compression
4. 后续 history replay 必须按 `historyOwnerActorId + canonicalRecordId` 同时回灌：
   - 可见消息文本
   - accepted deterministic outcomes
   - 结构化 sidecar
5. 只有 committed / replay-eligible 的 accepted deterministic outcomes 才进入后续 memory compression 输入

补充规则：

- `messageIndex` 只允许作为单线程内排序字段，不得作为跨 channel authoritative join key
- 本地私聊、远程一对一、群聊投影若进入同一 actor-owned history truth，必须共享：
  - `historyOwnerActorId`
  - `canonicalRecordId`

### 3.4 Committed 条件

- 只有在 AI 文本成功显示到原版风格自定义对话框后，才算当前 `dialogue` committed

### 3.5 Failure Exposure

- AI 私聊失败时，在当前自定义对话框中以大白话提示
- 不静默吞掉
- 不用术语

### 3.6 Trace Hooks

`Stardew` 私聊链至少记录：

1. NPC 交互开始
2. 宿主原对话显示完成
3. 宿主对话记录写入
4. 原对话耗尽判定
5. AI 对话框打开
6. AI 文本显示完成
7. canonical replay envelope 写入
8. mirrored private-history projection 写入
9. accepted deterministic outcome 落地
10. AI 对话关闭

### 3.7 当前待补而非已定稿

以下点仍待补，不得再和“完整实现链”混写：

- 失败文案与恢复入口的细分类映射

### 3.8 Flow A2: Remote Direct One-to-One `[M1 implementation_only]`

#### 3.8.1 通用链路

- `手机私信` 保持独立 `remote_direct_one_to_one` player-visible 频道
- 它不是本地点击 NPC 后 `private_dialogue` 的同一可见 carrier
- 它在 claim / sellability artifact 中仍回链到 canonical capability key `dialogue`
- 它不是另一套独立 prompt family；它是同一 private/direct router 的远程 carrier 分支

#### 3.8.2 端到端流程

1. 玩家或宿主入口打开手机私信线程
2. Mod 采集当前 target、可用性、最近 direct-message history 与必要宿主摘要
3. Runtime 组装 remote direct canonical input，并绑定“private dialogue base rules + remote carrier overlay”
4. Server 返回 source-faithful 结果与 `actions[]`
5. Runtime 先保留 source-style 结果并直接做当前 title 的 deterministic validation
6. 只有 availability = `available_now` 时，accepted turn 才先写入服务器侧 canonical replay envelope 与 remote turn record
7. 只有 remote turn record 已存在且手机私信 UI 成功显示文本后，当前 turn 才 committed
8. committed 之后，receiver-visible direct-message / private-message projection 才允许回写到与 `private dialogue` 共享的 actor-owned private/direct history truth

#### 3.8.3 Failure / Recovery / Pending

- 当前收口口径下，`remote_direct_one_to_one` 只允许：
  - `available_now`
  - `unavailable_now`
- 若 remote availability 不满足，必须形成显式 `unavailable_now` 结果，不得冒充本地私聊 committed
- 当前收口口径下不做 deferred queue，不做 delayed delivery
- `unavailable_now` 时：
  - 不创建 `pending_visible` remote turn record
  - 不进入待投递队列
  - 只保留输入侧 trace 与当前线程内 unavailable result
- surface-local 重试只属于 convenience action
- authoritative recovery path 仍回到：
  - `游戏 -> 帮助与修复`
  - `支持与帮助`
- 线程 key 固定为：
  - `gameId + actorId + targetId + channelType`
- 关闭并重新打开同一 NPC 的手机私信时，必须复用同一线程 key
- `DayStarted` 只重算 availability，不自动重发上一条 unavailable message
- `remoteTurnId`
  - 只允许作为远程私信线程内的补充投影 id
  - 必须由 `historyOwnerActorId + canonicalRecordId + channelType` 可 deterministic 导出或回链
  - 不得替代 `historyOwnerActorId + canonicalRecordId` 的 authoritative join 语义

## 4. Flow B: Group Chat `[M1 implementation_only]`

### 4.1 通用链路

此能力沿用参考 mod 的 `group_chat` 主链：

- `speaker selection`
- 冻结本轮 speaker order
- `per-speaker generation`
- 每句单独持久化
- 每句效果单独 apply
- history replay from persisted records
- delivered turn mirrored into each participant's private history projection

### 4.2 Historical Capability Bucket

这里的 historical capability bucket 固定为：

- `M1 implementation_only`
- `allowed for current title implementation / review / evidence when enabled`
- `not current reference-grade hardening closeout criteria`
- `not current external support claim`

### 4.3 Stardew 宿主绑定

`Stardew` 里分两类群聊：

1. `现场群聊`
2. `主动群聊`

#### 4.3.1 现场群聊

- 玩家 + NPC 总数 `>= 3`
- NPC 用头顶气泡发言
- 玩家用底部输入框发言
- participant set 动态变化：
  - 新来的 NPC 从下一轮加入
  - 离场 NPC 从下一轮退出
- 气泡会消失，但群聊会话可以继续保留

#### 4.3.2 主动群聊

- 通过手机群聊板块进入
- 当前属于 `M1 implementation_only`
- 允许拉入不在场 NPC

#### 4.3.3 NPC 主动拉群聊

- 该能力做成 `Stardew per-game experimental` 开关
- 关闭时只关闭 NPC 主动发起
- 不影响：
  - 当前实现范围内的现场自然群聊
  - 当前实现范围内的玩家主动群聊

### 4.4 端到端流程

#### 阶段 1：群聊创建

1. 现场群聊：
   - Mod 采样当前现场 participant set
   - 只有在玩家首次发送后，才创建或继续使用当前 `groupSession`
2. 主动群聊：
   - 玩家从手机群聊板块选择参与者
   - Mod 打开或复用对应 `contactGroupId` 线程
3. recovered 风格的后台远程群聊活动：
   - 即使玩家未主动打开手机群聊，也允许把新 turn 追加到既有 `contactGroupId` 线程并增加 unread

#### 阶段 2：玩家输入

1. 玩家输入一句群聊消息
2. Mod 将玩家消息提交给 Runtime
3. Runtime 将该消息写入群聊输入队列，并生成稳定的 `inputSequenceId`

#### 阶段 3：speaker selection

1. Runtime 读取：
   - current scene snapshot
   - participant set
   - recent group history
   - topic seed
   - participant relations
   - relevant Stardew 宿主摘要
2. Runtime 调用 server 进行 speaker selection / order planning
3. 返回本轮 ordered speaker list
4. Runtime 冻结当前轮顺序

补充规则：

- 现场 `groupSession` 只在玩家主动发送后创建或延续
- 现场 participant set 算法固定为：
  - 当前 location 已加载
  - 当前可见
  - 当前可交互
  - 非 cutscene / dialogue lock
  - 与玩家距离不超过 `8` tiles
  - 按稳定 `actorId` 排序后冻结
- participant set 在当前轮冻结；新来的 NPC 与离场 NPC 只影响下一轮
- 手机主动群聊线程 key 固定为：
  - `gameId + contactGroupId`

#### 阶段 4：逐句生成

1. Runtime 按冻结顺序逐个请求 server 生成一条 NPC 发言
2. 每个 speaker 的请求都必须是 speaker-centric 的，至少附带：
   - 当前 speaker snapshot
   - 当前 speaker 对其他参与者的关系视角
   - 当前 speaker 自己的 private-dialogue context
   - 本轮已 accepted 的 earlier turns
   - 本轮已 accepted 的 deterministic effects
3. 每句返回 source-faithful 结果：
   - content
   - `actions[]`
   - optional diagnostics
4. Runtime 对每句先保留 source-style 结果，再做 deterministic validation
5. Runtime 把 accepted group turn envelope 回传到服务器侧 canonical history store
6. 服务器侧先生成并写入带：
   - `groupTurnId`
   - `sequenceIndex`
   - `surfaceId`
   的 pending turn record / canonical turn envelope
7. 只有服务器侧记录已存在后，才把已批准结果交给 Mod

#### 阶段 5：宿主落地

1. 现场群聊：
   - Mod 把句子显示为对应 NPC 头顶气泡
2. 远程多方频道：
   - Mod 把句子显示到手机群聊消息流
3. 每句只有在：
   - 对应 turn record 已持久化
   - 对应句子显示成功
   后，才形成单独 committed

#### 阶段 6：结果回写

1. 每句结构化效果单独落地
2. 每句 delivered turn 再镜像进相关 private history projection
3. 群聊历史回放只读取已 committed 的 persisted turn
4. authoritative committed turn truth 固定归服务器侧 canonical history store 所有
5. 远程多方线程还必须保留：
   - `contactGroupId`
   - per-group message bucket
   - `unreadCount`
   - `doNotDisturb`
   - raw source-style payload sidecar 或等价保真 sidecar
6. 远程多方消息结构最小字段固定为：
   - `contactGroupId`
   - `groupTurnId`
   - `sequenceIndex`
   - `speakerActorId`
   - `visibleText`
   - `deliveryState`
   - `historyOwnerActorId`
   - `canonicalRecordId`
   - `sidecarRef`

补充规则：

- `Warped` 或睡眠切日会结束现场 `groupSession`
- 气泡自然消失不回滚已 committed turn，只结束当前局部可见面
- 手机主动群聊线程在玩家显式关闭前持续；关闭重开同一 `contactGroupId` 时必须复用同一线程
- `DayStarted` 只做线程状态重建，不丢失同一 `contactGroupId` 的 unread 与 persisted bucket

### 4.5 Committed 条件

- 现场群聊：
  - 每句 turn record 已持久化且气泡显示成功后 committed
- 主动群聊：
  - 每条 turn record 已持久化且消息显示成功后 committed

### 4.6 Failure Exposure

- 现场群聊某句失败：
  - 按单句失败处理
  - 不让整场群聊直接崩掉
- 主动群聊某条失败：
  - 按单条失败占位处理
  - 不清空整个群聊窗口
- turn render fail：
  - 写入同一 `groupTurnId + sequenceIndex` 的 failed placeholder
  - 线程持续
- thread open fail：
  - 不新增 turn
  - 只露出 open failure copy
  - 既有线程状态保持

### 4.7 Trace Hooks

群聊链至少记录：

1. 现场 participant set 采样
2. participant 变化
3. 玩家消息入队
4. speaker selection / order freeze
5. per-speaker generation
6. 每句显示完成
7. 每句持久化
8. 每句效果落地
9. 群聊历史重放

补充说明：

- `surface 消失 / UI 关闭 / 切图`
  属于 `Stardew` 宿主补充 trace
  不替代参考 mod 的群聊主记录主链

### 4.8 当前待补而非已定稿

以下内容仍需要后续专项继续细化：

- 现场群聊范围 / 可见 / 可交互采样证据
- 主动群聊 UI 的具体消息结构
- 现场群聊与主动群聊的细粒度 failure / recovery 映射

## 5. Flow C: Memory `[M1 core]`

### 5.1 通用链路

底层机制沿用参考 mod：

- raw history 与 summary memory 分层
- 长期记忆使用按时间桶组织的 summary memory
- 记忆压缩由独立 compression prompt 驱动
- accepted private-dialogue / item outcomes 继续反馈到后续压缩

### 5.2 Stardew 宿主补充

`Stardew` 不重写记忆机制，只补宿主摘要输入：

- 玩家农场信息摘要
- 玩家物品摘要
- 玩家任务摘要
- 事件摘要
- 婚姻 / 恋爱 / 同居情况摘要
- 孩子摘要
- 宠物摘要
- 养殖动物摘要

规则：

- 由 Mod 采集
- 由 Runtime 归一化并组装
- 事件驱动刷新 + 请求前重新取当前快照

### 5.3 端到端流程

#### 阶段 1：raw history 产生

1. 宿主原对话、AI 私聊、物品事件、关键宿主事件不断产生 raw records
2. 这些 raw records 进入各自 history store
3. 若当前 build 启用了 `group_chat`，其 committed delivered turn 也会继续镜像进 private history projection

#### 阶段 2：压缩触发

1. 在 host-defined interval 触发记忆压缩
2. Runtime / Server 收集：
   - 选定聊天历史
   - 重要事件记录
   - 旧 memory summaries
   - Stardew 宿主摘要

#### 阶段 3：记忆压缩

1. Server 调用 memory compression prompt
2. 生成当前时间桶的摘要记忆
3. 写入 summary memory store

补充规则：

- `summary memory store` 的 authoritative key 固定为：
  - `memoryOwnerActorId + memoryKind + timeBucket`
- 记忆压缩不得混入其他 `memoryOwnerActorId` 的经历、关系或世界视角

#### 阶段 4：玩家可见面

1. 玩家打开 `NPC 信息面板 -> 记忆`
2. 系统展示当前 NPC 对玩家的时间桶摘要
3. 每个时间桶显示为一张记忆卡片

### 5.4 Historical M1 UI 收敛

在 historical `M1` bucket 下：

- 只展示 NPC 对玩家的记忆摘要
- 不展示原始会话流水
- 不把 NPC 对其他人的完整 actor-owned memory 摊给玩家看

## 6. Flow D: 自定义物品 / 赠与 `[M1 core]`

### 6.1 通用链路

平台大方向固定为：

- 该链在 canonical capability / claim / waiver / sellability 上固定回链到：
  - `social transaction / commitment`
- accepted `GiveItem` / `LendItem` / `Transaction` 的 authoritative runtime outcome 固定回链到：
  - `transaction_state_committed`
- 模板实例化优先
- 受限文本 / 语境覆写
- 不做开放式新物品系统作为默认路线

### 6.2 Stardew 宿主绑定

- 默认优先使用 `item.modData`
- 玩家 first-perception carrier 固定按以下顺序选择：
  - 邮件
  - 奖励提示
  - tooltip / 名称描述
- `对话` 可作为补充叙事宿主，但不作为 authoritative first-perception carrier
- 默认发放路径：
  - 通过同一 accepted item/gift action bundle 先形成玩家可见文本 carrier
  - 再进入实际发放路径
- AI 名称 / 描述只绑定当前实例
- 文案不得与真实用途完全不符
- recovered 的 `GiveItem / LendItem / Transaction` 仍应被视为同一 accepted `actions[]` bundle 中的行为结果
- 文本感知不是脱离动作的独立前传，而是同一 item/gift 行为结果的玩家可见 carrier

### 6.3 端到端流程

#### 阶段 1：触发

1. 私聊 / 任务 / 承诺 / 礼物事件产生命中条件
2. Mod 把触发事实交给 Runtime

#### 阶段 2：server 生成语境

1. Runtime 将当前对话上下文、记忆上下文、宿主摘要送到 server
2. Server 生成该次物品事件的文本语境与 source-style `actions[]`

#### 阶段 3：source-faithful validation + authoritative pending write

1. Runtime 保留 source-faithful `actions[]`
2. Runtime 通过 deterministic gate 验证该次物品事件
3. 服务器侧先写入 authoritative item-event record 或 rejection record
4. 只有 authoritative item-event record 已成立后，Runtime 才生成模板实例化结果

#### 阶段 4：玩家先感知

1. Mod 先选择一个明确的文本 carrier：
   - 邮件
   - 奖励提示
   - tooltip / 名称描述
2. carrier 选择优先级固定为：
   - 邮件
   - 奖励提示
   - tooltip / 名称描述
3. Mod 通过该 carrier 让玩家明确感知：
   - 给了什么
   - 为什么给
   - 这个物品的 AI 语境是什么

#### 阶段 5：实际落地

1. Mod 创建物品实例并写入 `item.modData`
2. 物品落到背包或对应奖励落点
3. 相关实例描述、tooltip、关联记录可被后续 UI 使用

#### 阶段 6：authoritative finalize

1. authoritative item-event record 必须保持为本次 item/gift 的唯一 authoritative join 源
2. authoritative item-event record 至少要能回链：
   - `traceId`
   - `requestId`
   - `narrativeTurnId`
   - `itemRef`
   - `transactionId`
   - actor / target refs
   - `transactionId` 或等价 `transaction_state_committed` join key
3. `itemRef` 的 authoritative join 定义固定为：
   - 若已创建实例：`gameId + itemInstanceId`
   - 若尚未创建实例或为 no-delivery / rejected：`authoritativeItemEventRecordId`
4. 只有 authoritative item-event record 成立后，才允许把该物品事件释放为 replay-eligible / memory-eligible

#### 阶段 6.5：committed finalize

1. 文本宿主成功显示只代表玩家已经 `perceived`
2. 只有以下三项同时成立时，item / gift 才进入 committed / replay-eligible：
   - 文本宿主成功显示
   - authoritative item-event record 已成立
   - 实际发放或明确 no-delivery / rejected outcome 已成立

#### 阶段 7：后续链路

1. 该实例级名称 / 描述进入后续对话与记忆上下文
2. `NPC 信息面板 -> 物品`
   可展示这件与该 NPC 关联的物品
3. memory flow 只消费 authoritative item-event record，不消费临时 surface 结果

补充规则：

- `奖励提示` 属于独立 carrier，必须拥有自己的 committed hook 和失败 copy

### 6.4 Trace Hooks

至少记录：

1. 赠与结果显示
2. 实例物品创建
3. 背包 / 奖励落地
4. 实例文本覆写读取
5. 物品关联记录

## 7. Flow E: Current Thought `[M1 core]`

### 7.1 通用链路

`当前想法` 是独立的 thought surface，但它的生成主链并入 `private_dialogue + inner_monologue`，不再单独长成 thought provider lane。

它至少要保留：

- 独立 trigger
- 独立 context pack
- 固定问题 + 内心独白回答的 prompt profile
- 独立 surface-local persistence / trace contract
- 不写入普通对话历史
- 不得成为第二 replay history truth 或 memory compression authority

### 7.2 端到端流程

#### 阶段 1：触发

1. 玩家打开 `NPC 信息面板`
2. 系统定位当前 NPC
3. Mod 触发当前 thought 请求

补充规则：

- 若玩家在当前 thought 请求未完成时切换 NPC，原请求必须标记为 stale
- stale thought 结果不得 commit 到新 NPC 面板
- 面板必须对新 NPC 立即重新请求或显示 thought 占位位
- 若玩家对同一 NPC 手动刷新或关闭后立即重开 thought 面，旧请求同样必须标记为 stale；只有最新请求允许 commit
- thought projection record 一旦进入 `surface-replayable`，必须同时拥有：
  - `derivedThoughtRecordId`
  - `npcId`
  - `surfaceId`

#### 阶段 2：上下文组装

1. Runtime 读取：
   - 当前 NPC snapshot
   - current scene snapshot
   - relevant Stardew 宿主摘要
   - current memory summary
   - recent accepted private / item / event outcomes
2. Runtime 构造固定问题，并绑定 `private_dialogue + inner_monologue` prompt profile

#### 阶段 3：编排与验证

1. Server 返回 thought text 与最小 diagnostics
2. Runtime 直接做 deterministic validation
3. 当前 thought 结果默认不落普通 host action，且 `actions` 必须为空

#### 阶段 4：pending write 与显示

1. 服务器侧先写入 `pending_visible` 的 thought projection record
2. Mod 在信息面板 thought 区显示当前一段内心独白

补充规则：

- `thought projection record` 只允许作为当前 surface 的 derived projection / trace evidence
- 它不得回写成第二条 actor-owned canonical history
- 它不得进入 memory compression authoritative input

#### 阶段 5：committed finalize

1. 只有 thought 文本完整显示成功后，thought projection record 才进入 committed / surface-replayable
2. 若显示失败，则该记录必须标记为 `render_failed`
3. 该链不得写入普通对话历史
4. 这里的 `surface-replayable` 只表示 surface-local replay / trace 可回看，不得解释成第二份 canonical replay authority

### 7.3 Trace Hooks

至少记录：

1. thought request trigger
2. context pack frozen
3. thought projection record pending write
4. thought render success / failure
5. thought committed finalize

## 8. Annex-Derived Chains

### 8.1 Information Propagation `[M2+ annex / experiment-only]`

最小治理口径：

- 保留 reference-faithful “真实消息投递”语义
- 当前 `M1-source-faithful` 不引入 repo bridge；`propagation_intents[]` 只属于 `post-M1-platformize`
- 只有在显式 committed payload 落盘后，才允许对外声称 `propagation_committed`
- `ConveyMessage` 后必须继续执行：
  - target validation
  - 按可达性 / 媒介可用性选择本地私聊或远程 carrier
  - receiver-visible persisted record 写入
  - receiver-side 后续 AI 语境变化回灌
- lineage / anti-explosion contract 至少保留：
  - `originTraceId`
  - `hopCount`
  - `maxPropagationHops`
- committed propagation payload 至少要带：
  - `propagationId`
  - `sourceFactId` 或 `sourceEventId`
  - `deliveryMode`
  - `deliveryState`
  - `targetScope`
- 当前 `M1` 不为它扩展新的 shared command class

### 8.2 Active World / World Event `[M2+ annex / experiment-only]`

最小治理口径：

- 保留 reference-faithful `WORLD_EVENT_PROPOSAL` / event creation 语义
- 当前 `M1-source-faithful` 不引入 repo bridge；`world_event_intents[]` 只属于 `post-M1-platformize`
- 只有在显式 world-event payload 落盘后，才允许对外声称 `world_event_committed`
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
- 当前 `M1` 不为它扩展新的 shared command class

## 9. Cross-Capability UI Map

### 9.1 手机联系人入口

- 可查看全部原版 NPC、宠物、孩子
- 默认按与玩家关系 / 熟悉度排序
- 暂不覆盖其他内容扩展 Mod 新 NPC

### 9.2 NPC 信息面板

固定为：

- 顶部基础信息区
- 下方 5 个 Tab

Tab 固定为：

1. `记忆`
2. `群聊历史`
3. `关系`
4. `物品`
5. `当前想法`

在 historical `M1` bucket 下：

- `关系` 只做展示
- `物品` 只做展示
- `当前想法` 只做单向流式 / 伪流式展示
- `群聊历史` 可保留空态，但空态只表示当前窗口无 committed group turn，不得用来替代 `group_chat` 未实现

## 10. 当前仍未细化到实现点的内容

以下内容仍需要后续专项继续细化：

- hook 类别对应的具体 `SMAPI / Harmony / UI patch` 最终落位
- 记忆卡片、物品卡片、当前想法 prompt 的专项结构
- annex / debug 扩展用的更细粒度 join / replay 表
- annex / debug 扩展用的 recovery / failure 文案细表

## 11. 最终结论

本设计的最终结论如下：

1. `Stardew` 的 `private dialogue`、`memory`、`item context` 继续沿用参考 mod 的成熟机制，并作为当前 `M1 core`。
2. `Stardew` 只补宿主绑定、surface、hook、committed 与 trace。
3. `private dialogue` 必须保留 mirrored writeback、canonical replay envelope、sidecar JSON、recent history reinjection、accepted outcomes feeding memory compression。
4. 当前 `M1-source-faithful` 先保留 `actions[]` 主链并直接做当前 title 的 deterministic validation；`actions[] -> action_intents[] / diagnostic_sidecar[]` bridge 以及 `propagation_intents[] / world_event_intents[]` 只属于 `post-M1-platformize`。
5. `remote_direct_one_to_one` 与 `group_chat` 当前属于 `M1 implementation_only`：若当前 build 显式启用，仍应实现、联调、review、留证据；但它们不再是当前 `post-M1-reference-grade-hardening` 的默认 closeout blocker，也不自动进入当前 exit criteria 或外部 support claim。
6. `当前想法` 作为 `M1 core` 必须保留独立展示面与 committed 语义，但生成主链按 `private_dialogue + inner_monologue` 收口，而不是继续长成独立 thought chain。
7. `information_propagation` 与 `active_world / world_event` 也必须继续保留，但当前同样只属于 annex。
8. 仍未定稿的 hook 具体 API 落位与更细粒度 failure / recovery 文案映射，都必须被视为待补项。
