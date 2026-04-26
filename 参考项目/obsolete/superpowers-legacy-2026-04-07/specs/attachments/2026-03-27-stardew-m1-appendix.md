# Stardew M1 Master Design

> 状态说明：
> - 文件名中的 `M1` 保留为历史阶段名，用于说明 `Stardew` 首轮 source-faithful 基线。
> - 当前执行 phase 不再定义为 `M1`，而以 `docs/superpowers/governance/current-phase-boundary.md` 中的 `post-M1-reference-grade-hardening` 为准。
> - 本文中凡是“当前 M1 / M1-source-faithful”的说法，默认应理解为“上一阶段建立的基线”或“仍在当前阶段被继承的能力桶”，不能再当作当前 phase 名字。

## 1. 文档定位

本文档是 `Stardew Valley` 在 `superpowers` 体系下的：

- `Stardew` 首轮 source-faithful working-design 主入口
- `M1 core`、`M1 implementation_only` 与 `M2+ annex` 的历史分桶说明，以及它们在当前 phase 中的继承关系

以后阅读 `Stardew` 历史基线设计，默认先从本文进入；但当前 phase truth 仍以 `current-phase-boundary.md` 为准。

本文档负责：

- 用一份主文讲清楚整体结构
- 说明哪些是通用能力、哪些是 `Stardew` 宿主绑定
- 说明哪些能力按当前 working design 归入 `M1 core`
- 说明哪些能力属于当前 `M1 implementation_only`
- 说明哪些能力只是保留为 recovered `OpenAIWorld` 真相源的 `M2+ annex`
- 给出玩家前台、Runtime、Mod、Server 的总图
- 把细节下沉到附录

本文档不替代以下上位真相源：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-03-27-superpowers-platform-control-plane-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-superpowers-client-runtime-topology-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-superpowers-player-launcher-appendix.md`

当前 `Stardew` 的宿主级 profile 真相源为：

- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

补充说明：

- 本文档是 `Stardew` 的 working-design 收口入口
- 它不是独立的 ship-gate truth source
- 当前是否进入 `M1 core profile`、是否满足 ship-gate、是否可进入外部 support claim，仍以：
  - `current-phase-boundary`
  - 当前批准的 `M1 core profile`
  - `game-integration-profile`
  - 相关 claim / waiver / evidence artifact
  为准

## 2. 一句话总纲

`Stardew` 的当前路线固定为：

- 参考 recovered `OpenAIWorld` 已验证的 AI 主链
- `M1-source-faithful` 优先于 `post-M1-platformize`
- 在 `M1-source-faithful` 跑通前，不先改参考链的主语义、主落点与主存储边界
- 服务器端持有主要编排真相源，但这条平台落位不得覆盖 source-faithful 复现优先级
- 本地保留薄 Runtime 做 deterministic gate 与 trace glue；title-local adapter 以复现参考链为先
- Mod 负责 `Stardew` 宿主采集、宿主 surface 与宿主写回
- 第一阶段在当前 phase 已批准范围内优先保持与参考 mod 的主链一致，以便尽早暴露尚未识别的问题
- 当前 working design 下，玩家可能通过：
  - AI 私聊
  - 手机私信
  - 群聊
  - NPC 信息面板
  - 当前想法
  - 物品语境
  感知 NPC 是“活的”
- `group_chat` 与 `remote_direct_one_to_one`
  - 属于当前 `M1 implementation_only`
  - 必须实现、联调、review、留证据
  - 但当前不自动进入 exit criteria 或外部 support claim
- `information_propagation`、`active_world / world_event` 继续保留为 source-faithful design truth source，但当前仍不进入 `M1` ship-gate

## 3. 结构总图

```text
Server
  - Cloud Control [M1 authority]
  - Hosted Narrative Orchestration [M1 allowed]
  - Hosted Capability [post-M1 / approved-only]
  - Platform Control Plane [post-M1 blueprint]

Client
  - Launcher UI
  - Launcher Supervisor
  - Local Runtime Core
  - Stardew Runtime Adapter
  - Stardew Mod
```

其中：

- `Launcher`
  负责玩家产品入口、状态、支持与恢复
- `Runtime Core`
  负责 canonical input、server 调用、deterministic gate、trace glue
- `Stardew Runtime Adapter`
  负责 `Stardew` 宿主绑定与 source-faithful 输入归一化
- `Stardew Mod`
  负责宿主 hook、surface、宿主写回与本地事实采集

## 4. 通用 vs Stardew 特有

### 4.1 通用能力真相源

以下能力主链直接沿用参考 mod，但 phase status 不同：

- `M1 core`
  - `private dialogue`
  - `raw history + summary memory`
  - `month-bucketed summary memory`
  - `social transaction / commitment`
  - deterministic validation + apply
- `M1 implementation_only`
  - `group chat`
  - `remote_direct_one_to_one`
- `M2+ annex / experiment-only`
  - `information propagation`
  - `active world / world event generation`

补充原则：

- 当前第一阶段的默认态度是“先复现 recovered 主链，再讨论优化版重构”
- 只要某条能力已经进入当前阶段范围，默认优先保持 recovered 的行为语义、回放语义和持久化边界
- 任何主动偏离 recovered 主链的地方，都必须在对应附录中直接写成明确偏离，而不是只在实现时临时决定
- repo bridge、shared abstraction、hosted truth 重分配、跨游戏共用 taxonomy 统一归入 `post-M1-platformize`，不得反写成当前 `M1-source-faithful` 前置条件

`M1-source-faithful` 当前必须显式对齐参考 mod 的 6 层 skeleton：

1. `Trigger`
2. `Snapshot`
3. `Summary Builder`
4. `Intent Schema`
5. `Parser / Repair / Normalizer`
6. `Projector / Executor`

规则：

- 这 6 层在当前 `M1-source-faithful` 中都属于正式实现目标，不允许只把其中两三层写成硬要求、其余留给实现时自行理解
- `JsonRepair`、`ContactGroup`、`ExperienceData`、`PrivateMessageData`、source-style `actions[]`、mirrored writeback、canonical replay envelope 都应被视为这 6 层在当前 title 上的锚点组成部分

### 4.2 Stardew 特有绑定

以下内容属于 `Stardew game-integration-profile`，不上拉成 shared contract：

- 原版 / 扩展对话先说完，再进入 AI 对话的叠加模式
- 原版风格自定义私聊框
- 手机私信作为独立的 `remote_direct_one_to_one` 远程频道
- `NPC 信息面板`
- `NPC 当前想法` 独立 surface
- `item.modData` 驱动的实例级物品语境
- 农场 / 家庭 / 任务 / 事件等 `Stardew` 宿主摘要
- `group_chat` 在 `Stardew` 宿主上的：
  - 现场群聊气泡
  - 底部输入框
  - 手机主动群聊
  这些绑定当前属于 `M1 implementation_only`

## 5. 玩家可见价值面

当前 `Stardew` working design 的玩家价值面固定分为：

1. `M1 core`
   - `AI 私聊`
   - `NPC 信息面板`
   - `NPC 当前想法`
   - `AI 语境物品`
2. `M1 implementation_only`
   - `手机私信`
   - `群聊`

补充说明：

- `手机私信` 与 `群聊` 当前属于 `M1 implementation_only`
- 它们必须绑定 visible host、committed / failure / recovery / trace 证据
- 但它们不应被直接包装成当前 `supported` claim 或 pack-level shorthand
- 它们也不应被写成当前默认 launch-visible baseline；只有在当前 build / title 配置明确启用并满足 disclosure / evidence 条件时，才允许对玩家实际露出
- `NPC 主动拉群聊` 若进入试验，只能算 `per-game experimental`

`NPC 信息面板` 固定包含：

- `记忆`
- `群聊历史`
- `关系`
- `物品`
- `当前想法`

并且全部要求：

- 大白话
- 不出现术语
- 偏生活态 / 社交态

## 6. 主链状态图

### 6.1 Private Dialogue `[M1 core]`

- 原版 / SVE / 东斯卡普等宿主对话先跑
- 宿主对话实际显示后落成宿主记录
- 再次点击后进入 AI 私聊
- AI 私聊 committed 以自定义对话框可见文本显示完成为准
- 参考 mod 的 mirrored writeback、canonical replay envelope、recent history reinjection、accepted outcomes feeding memory compression 继续保留

### 6.2 Group Chat `[M1 implementation_only]`

- 分为现场群聊与主动群聊
- 底层继续沿用参考 mod 的：
  - `speaker selection`
  - frozen order
  - `per-speaker generation`
  - per-turn persistence
  - per-turn deterministic apply
  - history replay
  - mirrored projection into participant private history
 - 当前必须进入实现、联调、review 与留证据
 - 但当前不自动进入 `M1` exit criteria 或外部 support claim

### 6.2A Remote Direct One-to-One `[M1 implementation_only]`

- `手机私信` 是独立的 `remote_direct_one_to_one` 远程频道
- 它不并回本地 `private_dialogue` carrier，也不降格成 `information_propagation`
- 它必须保留独立 channel rules、committed 语义、failure copy 与 recovery 入口
- 它在产品族谱上属于 `dialogue family`
- 在 claim / waiver / sellability artifact 中，仍必须回链到 canonical capability key `dialogue`
- `remote_direct_one_to_one` 只作为 `dialogue` 的 channel implementation dimension 使用

### 6.3 Memory `[M1 core]`

- 继续沿用 raw history + summary memory 分层
- `M1` 玩家 UI 只看：
  - 当前 NPC 对玩家的时间桶摘要卡片

### 6.4 Item / Gift Context `[M1 core]`

- 模板实例化优先
- `item.modData` 优先
- 通过同一 accepted item/gift action bundle 先形成玩家可见文本 carrier，再实际发放
- 实例级名称 / 描述进入后续对话与记忆上下文
- 该链在 canonical capability / claim / runtime outcome 上固定回链到：
  - capability key `social transaction / commitment`
  - runtime outcome `transaction_state_committed`

### 6.4A Mod -> Runtime Contract Freeze

- `PrivateDialogueRequest` 当前固定包含 `hostSummaryRef`
- `RemoteDirectRequest` 当前固定包含：
  - `threadKey`
  - `hostSummaryRef`
  - `summarySelectionHint`
- `GroupChatTurnRequest` 当前固定包含：
  - `participantSetRef`
  - `inputSequenceId`
  - `surfaceId`
  - `hostSummaryRef`
  - `summarySelectionHint`
- `ThoughtRequest` 当前固定包含：
  - `surfaceId`
  - `hostSummaryRef`
  - `summarySelectionHint`
- `HostSummaryEnvelope` 当前固定包含：
  - `summaryEnvelopeId`
  - `snapshotCapturedAt`
  - 8 个 required summary buckets
- `groupHistoryDisclosureState` 当前固定只允许：
  - `open_for_player`
  - `not_open_for_player`

### 6.5 Information Propagation `[M2+ annex / experiment-only]`

- 保留 recovered reference mod 的真实消息投递语义
- 当前 `M1-source-faithful` 不引入 repo bridge；`propagation_intents[]` 只属于 `post-M1-platformize`
- `ConveyMessage` 后必须继续执行：
  - target validation
  - carrier selection
  - receiver-visible persisted delivery
  - receiver-side context reinjection
- 当前不进入 `M1` ship-gate

### 6.6 Active World / World Event `[M2+ annex / experiment-only]`

- 保留 recovered reference mod 的 event creation 语义
- 当前 `M1-source-faithful` 不引入 repo bridge；`world_event_intents[]` 只属于 `post-M1-platformize`
- 接受后的 proposal 必须落成 durable event object 或 rejection record，并保留 `lifecycleState / eventState`
- 当前不进入 `M1` ship-gate

## 7. 当前 M1 实现范围

当前 working design 下需要进入 `M1` 实现 / 联调 / 留证据范围的内容：

- 叠加式 AI 私聊
- `手机私信 / remote_direct_one_to_one`
- `group_chat`
- 宿主原对话记录接入 recent private history
- NPC 信息面板
- 当前想法 surface
- 模板实例化物品与文本语境
- 薄 Runtime + `Stardew Adapter`
- 只面向 `render_command` 与 `transactional_command` 的 deterministic lowering

`post-M1-platformize` 才允许进入的内容：

- `actions[] -> action_intents[] / diagnostic_sidecar[]` 的 repo bridge
- `propagation_intents[]`
- `world_event_intents[]`
- shared prompt / intent / command taxonomy 抽象
- hosted truth-source 重分配

`M1` 明确不做 ship-gate 的内容：

- `information_propagation`
- `active_world / world_event`
- 为 propagation 或 active world 新发明 shared command class
- 开放式完整新物品系统
- 重型本地 AI Runtime
- 把所有 NPC 关系 / 物品 / 思维都做成重交互操作台
- 在玩家前台暴露术语和后台感

`M1 implementation_only` 当前最小治理口径：

- 若当前 build 显式启用，则必须实现、联调、review、留证据
- 必须命名 visible host、failure exposure、recovery entry、trace join
- 未启用时不作为当前 `post-M1-reference-grade-hardening` closeout blocker
- 不得写成当前 exit criteria、外部 support claim 或 pack-level shorthand

`M2+ annex` 当前最小治理口径：

- 可以保留设计真相源
- 可以挂在 `experimental / annex` 下做受控实验
- 不得写成当前首发 `M1` 的强制完成项
- 不得把 annex 结果反向包装成“当前 M1 已完整闭环”

## 8. 附录体系

本主文以下沉附录方式组织细节。

### Appendix A

- `docs/superpowers/specs/attachments/2026-03-27-stardew-capability-flow-appendix.md`

用途：

- 讲 `private dialogue / memory / item` 的 `M1 core` 端到端流程
- 讲 `group_chat / remote_direct_one_to_one` 的 `M1 implementation_only` 真相源与 adapter 桥接
- 讲 `information_propagation / active_world` 的 annex 真相源与 adapter 桥接

### Appendix B

- `docs/superpowers/specs/attachments/2026-03-27-stardew-hook-mapping-appendix.md`

用途：

- 讲 `SMAPI / Data / modData / Harmony patch` 的宿主接入面
- 明确哪些 hook 只是当前候选，不是假装已经定稿的唯一落位

### Appendix C

- `docs/superpowers/specs/attachments/2026-03-27-stardew-context-summary-fields-appendix.md`

用途：

- 讲 `Stardew` 宿主摘要字段表
- 讲 `private dialogue` 输入装配除了宿主摘要之外还必须包含什么

### Appendix D

- `docs/superpowers/specs/attachments/2026-03-27-stardew-surface-commit-trace-failure-appendix.md`

用途：

- 讲 surface 的 committed、trace join、failure copy、recovery entry
- 明确哪些 surface 属于 `M1 implementation_only`

### Appendix E

- `docs/superpowers/specs/attachments/2026-03-27-stardew-first-wave-reference-mod-migration-appendix.md`

用途：

- 把第一批必须从 recovered `OpenAIWorld` 迁入的能力桶收成正式清单
- 对齐参考 mod 证据锚点、当前 `Stardew` 落位与 source-faithful 迁移口径

### Profile Truth Source

- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

用途：

- 讲 `Stardew` 的正式宿主级 profile 约束

## 9. 进入实现前先看什么

如果只是理解系统：

1. 先看本文
2. 再看 `game-integration-profile`
3. 需要细节时再翻附录

如果准备开始实现：

1. 先看 `game-integration-profile`
2. 再看 `first-wave reference-mod migration appendix`
3. 再看 `capability flow appendix`
4. 再看 `hook mapping appendix`
5. 再看 `context summary fields appendix`
6. 最后看 `surface commit trace failure appendix`

补充提醒：

- 当前实现前必须把 `M1 core`、`M1 implementation_only` 与 `M2+ annex` 分开看
- `group_chat` 与 `remote_direct_one_to_one` 当前属于 `M1 implementation_only`
- `information_propagation`、`active_world` 仍不得倒推成当前 `M1` 已经必须交付

## 10. 最终结论

以后阅读 `Stardew` 的历史 `M1-source-faithful` 基线设计，默认从本文进入；当前执行 phase 仍以 `docs/superpowers/governance/current-phase-boundary.md` 为准。

根目录不再继续堆放多个平级 `Stardew` 细化文档。

当前被继承到 `post-M1-reference-grade-hardening` 的历史 `M1 core` 基线主要包括：

- `private dialogue`
- `memory-visible evidence`
- `NPC 信息面板 / thought surface`
- `item context`

当前被继承的历史 `M1 implementation_only` 主要包括：

- `group_chat`
- `remote_direct_one_to_one`

当前 annex 真相源是：

- `information_propagation`
- `active_world / world_event`

细节统一下沉到附录，真正过时或被替代的内容再进入 `obsolete`。
