# Stardew 第一批参考 Mod 迁移清单

> 状态说明：
> - 文件名与正文中的“第一批 / 第一阶段”保留为历史 first-wave migration bucket 名称，不再等于当前执行 phase。
> - 当前执行 phase 统一以 `docs/superpowers/governance/current-phase-boundary.md` 中的 `post-M1-reference-grade-hardening` 为准。
> - 本文只回答哪些能力属于继承自历史 first-wave 的迁移桶，以及在当前收口阶段应如何读取这些桶。

## 1. 文档定位

本文档用于把 `recovered GGBH_OpenAIWorld` 中已经确认迁入 `Stardew` 的历史 first-wave 能力桶，收成一份可执行的迁移清单。

它回答的问题不是：

- 参考 mod 里还有哪些更远期能力
- 未来是否要进一步 `AFW-native`

它只回答：

- 历史 first-wave 计划纳入哪些能力
- 每项能力在参考 mod 里的主要证据锚点是什么
- 每项能力在当前 `Stardew` 设计体系里落到哪里
- 当前迁移应保留哪些 source-faithful 语义

补充说明：

- 本文档只负责历史 first-wave 能力桶的实现范围与迁移顺序
- 它不是 ship-gate truth source
- 它也不是 claim / waiver / sellability truth source
- 这里出现的“第一阶段 / 第一批计划纳入”，默认都表示 inherited first-wave migration bucket，而不是当前 phase 名称
- 这些 bucket 在当前收口阶段只表示：允许保留实现 / 联调 / review / 留证据的范围
- 其中 `M1 implementation_only` 能力若当前 build 显式启用，仍应补 visible host / failure / recovery / trace 证据；但它们不再是当前 `post-M1-reference-grade-hardening` 的默认 closeout blocker，也不得反写成当前 exit criteria 或外部 support claim
- `M1 implementation_only` 能力即使属于历史 first-wave bucket，也不等于当前默认 launch-visible baseline；若对玩家露出，仍必须满足 disclosure / evidence / waiver 约束

本文档承接：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-m1-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-capability-flow-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-hook-mapping-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-surface-commit-trace-failure-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-26-stardew-ai-mod-feature-borrowing-from-ggbh-openaiworld.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/GGBH_OpenAIWorld_功能梳理_非UI.md`

若与当前 `phase boundary`、claim artifact、runtime contract 或 `game-integration-profile` 冲突，仍以上位治理真相源为准。

## 2. 一句话结论

`Stardew` 当前继承的 historical first-wave 迁移路线固定为：

- 在当前 phase 批准范围内，尽量保持 recovered `OpenAIWorld` 的已验证能力语义
- 优先迁入已经被当前附件正式点名的能力桶
- 不先为了“架构更优雅”改写能力边界
- 不把 annex 能力混入当前收口阶段的 ship-gate 主链
- 若当前架构对 recovered 语义做了明确收缩或偏离，必须直接写明，不假装成完全 source-faithful

historical first-wave 额外原则固定为：

- 先保证与参考 mod 方案尽量一致，再考虑优化
- 在当前 phase 已批准的能力范围内，优先复现 recovered 链路、数据语义、持久化归属、回放方式与玩家可见行为
- 不因为“看起来更干净”就提前重写 recovered 主链
- 任何主动偏离 recovered 方案的地方，都必须显式标注为：
  - 当前阶段刻意收缩
  - 当前阶段宿主约束
  - 或当前治理边界要求
- 如果某个问题还没被完全理解，historical first-wave bucket 默认更偏向保留 recovered 行为，而不是先发明新语义

补充硬规则：

- `M1-source-faithful` 不等于“边复现边平台化”
- `action_intents[]`、`diagnostic_sidecar[]`、hosted truth-source 重分配、shared taxonomy 统一，统一归入 `post-M1-platformize`
- 若当前文档与上位主文冲突，以“先 source-faithful，后 platformize”的顺序优先

## 3. 历史 first-wave 计划纳入的能力桶

### 3.1 总表

| 能力桶 | historical capability bucket | 参考 mod 主要锚点 | 当前 Stardew 落位 | 当前迁移口径 |
| --- | --- | --- | --- | --- |
| `AI 私聊 / private dialogue` | `M1 core` | `PrivateMessageData`、私聊上下文组装、`对话.md` | `private dialogue` 主链、AI 私聊对话框 surface | 保留 recent history reinjection、mirrored writeback、canonical replay envelope、accepted outcomes feeding memory compression；宿主原对话只允许在归一化成 source-equivalent host-derived record 后进入 recent history |
| `记忆 / memory` | `M1 core` | `ExperienceData`、月度经历、`记忆压缩.md` | `raw history + summary memory + month-bucketed summary memory`、记忆 Tab | 保留 raw history 与 summary memory 分层，不先扩成更重画像系统 |
| `NPC 信息面板` | `M1 core` | 参考 mod 的记忆 / 群聊 / 关系 / 物品主线能力池 | `NPC 信息面板` 正式宿主 surface | 第一批就固定成正式承载面，不作为调试页 |
| `当前想法 / thought` | `M1 core` | `decoded_prompts/角色卡模板.md`、`decoded_prompts/对话.md`、`decoded_prompts/记忆压缩.md`、`OpenAIWorld.mod.data/PrivateMessageData.cs`、`OpenAIWorld.mod/ExperienceData.cs`、长期记忆/私聊分析链 | 独立 thought surface，生成主链并入 `private_dialogue + inner_monologue` | 必须作为独立展示面迁入，但生成口径按固定问题 + 内心独白回答理解，不再把它写成独立 provider 链路；同时不混入普通对话历史 |
| `物品语境 / 赠与 / item-gift context` | `M1 core` | `行为指令.md`、`交易.md`、物品落地分支、自定义物品 prompt | `自定义物品 / 赠与` 主链、物品 Tab、物品文本感知 surface | 保留“同一 accepted item/gift action bundle 先形成玩家可见文本 carrier，再实际发放，并进入记忆/对话”的顺序；在 canonical capability 上回链到 `social transaction / commitment`，在 runtime outcome 上回链到 `transaction_state_committed`；当前明确是对 recovered item chain 的收缩版，不宣称已覆盖开放式完整新物品系统 |
| `关系 / relation context` | `M1 core` | 关系参与目标选择与行动决策、关系相关行为指令 | relation snapshot 输入、关系 Tab 展示 | 第一批先保留“能展示、能参与生成”，不先扩成重交互关系系统 |
| `群聊 / group chat` | `M1 implementation_only` | `GroupMessageData`、`ContactGroup`、`群聊.md`、轮流发言主链 | 现场群聊、主动群聊、群聊历史 Tab | 保留 `speaker selection -> frozen order -> per-speaker generation -> per-turn persistence -> history replay` |
| `手机私信 / remote_direct_one_to_one` | `M1 implementation_only` | 参考 mod 的远程载体与远程一对一语义来源 | 手机私信 channel 与 surface | 作为独立 player-visible 远程频道迁入；但 accepted remote turn 仍必须写入与 `private dialogue` 共享的 actor-owned private/direct history truth |

### 3.2 `AI 私聊 / private dialogue`

参考 mod 当前最明确的成熟主链是：

- 玩家/NPC 对话进入 `PrivateMessageData`
- recent history 按日期、地点、天气回灌
- 当前轮 accepted outcome 回写到后续历史与记忆输入

当前 `Stardew` 迁移时必须保留：

- 宿主原对话先跑
- 原对话进入 recent private history
- 再次点击后进入 AI 私聊
- AI 文本成功显示后才 committed

这批 inherited first-wave 能力在当前收口阶段仍沿用的要求：

- 宿主原对话进入 recent private history 时，必须尽量贴近 recovered 私聊记录语义
- 如果宿主文本无法自然映射到 recovered message type/category，必须先补归一化规则，而不是直接把宿主文本原样塞进历史池

### 3.3 `记忆 / memory`

第一批记忆不是“以后补一下摘要 UI”，而是私聊与物品链的正式组成部分。

当前必须保留：

- raw history
- summary memory
- month-bucketed memory compression
- 只有 committed / replay-eligible 的 accepted outcome 才进入后续压缩

`M1` 玩家可见面当前只收敛到：

- `NPC 信息面板 -> 记忆`

### 3.4 `NPC 信息面板`

该面板不是参考 mod 的原样 UI 复刻，而是当前 `Stardew` 对参考 mod 能力主线的正式承载面。

第一批固定包含 5 个 Tab：

1. `记忆`
2. `群聊历史`
3. `关系`
4. `物品`
5. `当前想法`

要求：

- 全部算正式宿主 surface
- 全部有 committed / failure / trace / recovery 口径
- 不出现后台术语

### 3.5 `当前想法 / thought`

当前设计已经固定：

- `当前想法` 是独立 thought surface，不是独立 thought chain
- 它的生成主链并入 `private_dialogue + inner_monologue`
- 不冒充普通私聊 turn
- 不写入普通对话历史

当前 recovered anchor 固定为：

- `decoded_prompts/角色卡模板.md`
  - 提供 PUBLIC / PRIVATE persona 边界
- `decoded_prompts/对话.md`
  - 提供当前说话人的 persona / 关系 / 当前语境约束
- `decoded_prompts/记忆压缩.md`
  - 提供 owner-scoped 记忆保留与关系/事件/承诺摘要主线
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod.data/PrivateMessageData.cs`
  - 提供 actor-owned 私聊消息事实层
- `decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/ExperienceData.cs`
  - 提供月桶长期记忆事实层
- `LONG_TERM_MEMORY_ANALYSIS.md` 与私聊分析链
  - 提供当前 thought 所依赖的 actor-owned memory / relation 事实来源

因此第一批迁移不能只做一个“补文案”的 UI 壳，而必须保留：

- 独立 trigger
- 独立 context pack
- 独立 pending / committed 语义
- 固定问题 + 内心独白回答的 dialogue-mode 约束
- derived projection only，不得变成第二 durable history / memory authority

### 3.6 `物品语境 / 赠与`

这里当前要迁的是：

- 参考 mod 已验证的物品语境、赠与、交易/给予相关 narrative 主链

这里当前不迁的是：

- 开放式完整新物品系统

第一批固定口径：

- 模板实例化优先
- `item.modData` 优先
- 玩家先通过文本宿主感知
- 再做实际发放
- 实例级名称/描述/语境进入后续对话与记忆上下文

这批 inherited first-wave 能力在当前收口阶段仍沿用的要求：

- 在当前已批准范围内，优先保留 recovered 物品/赠与链的触发和回灌语义
- 不因为宿主实现方便，就把物品事件简化成“只显示一段文案”

### 3.7 `关系 / relation context`

当前继承的 historical first-wave 对 `关系` 的要求不是先做大而全的关系系统，而是先保留参考 mod 已经验证过的两类价值：

- 关系参与 AI 目标选择与生成
- 关系可以在 `NPC 信息面板` 中被玩家查看

因此第一批应先确保：

- relation snapshot 能进入 `private dialogue`、`group chat`、thought context
- `关系 Tab` 可以稳定展示

### 3.8 `群聊 / group chat`

`group_chat` 当前虽然不是 `M1 core`，但它已经被正式写成：

- 若当前 build 启用，则必须联调
- 若当前 build 启用，则必须补 review / 证据
- 若当前 build 未启用，不作为当前 reference-grade hardening closeout blocker

因此在当前收口阶段对这批 inherited first-wave 能力的处理里，它不是“以后再想”，而是：

- 治理上属于 `implementation_only`
- 实施上属于计划纳入历史 first-wave 的能力桶

当前应尽量保留参考 mod 的成熟语义：

- speaker selection
- frozen order
- per-speaker generation
- per-turn persistence
- per-turn deterministic apply
- history replay
- mirrored projection into participant private history

补充说明：

- historical `M1 implementation_only` bucket 只承诺保留 recovered `group_chat` 的核心轮次生成与持久化语义
- 当前收口阶段若保留远程多方链路，仍必须保留 recovered `ContactGroup` 的最小持久化状态：
  - `contactGroupId`
  - per-group message bucket
  - `unreadCount`
  - `doNotDisturb`
  - raw source-style payload sidecar 或等价保真 sidecar

这批 inherited first-wave 能力在当前收口阶段仍沿用的要求：

- 对已经纳入当前阶段的群聊主链，不先主动做“更简单但不一样”的重写
- recovered 里的非玩家自主远程群聊活动，在当前收口阶段不得被默认删除；若不对玩家主动打扰，也必须至少作为后台线程更新与 unread 增量保留

### 3.9 `手机私信 / remote_direct_one_to_one`

当前设计已经明确：

- `手机私信` 是独立的 `remote_direct_one_to_one` player-visible 频道
- 它不是点击 NPC 后本地 `private_dialogue` 的同一可见 carrier
- 但它的 accepted remote turn 仍必须回到与 `private dialogue` 共享的 actor-owned private/direct history truth
- 它也不是另一套独立 prompt family，而是同一 private/direct router 的远程 carrier 分支

这批 inherited first-wave 能力在当前收口阶段仍沿用的要求：

- 在远程一对一范围内，优先保留 recovered 远程私聊与私聊共享 actor-owned memory truth 的语义
- 不先把远程一对一做成另一套脱离 recovered 私聊体系的新历史系统

因此第一批迁移要保留的不是“手机版私聊 UI”，而是：

- 独立 channel rules
- 独立 committed 条件
- 独立 failure copy
- 独立 recovery 入口

## 4. 当前不纳入第一批的 recovered 能力

以下能力仍保留为 reference truth source，但当前不进入第一批主迁移清单：

- `information_propagation`
- `active_world / world_event`
- 开放式完整新物品系统
- 参考 mod 中更重的题材化玩法外壳

理由不是它们不重要，而是当前 `Stardew` 方案已经把它们收口为：

- `M2+ annex / experiment-only`
- 或当前阶段不建议直接照搬的能力

## 5. 建议阅读顺序

如果目的是按这份清单推进第一批迁移，建议按下面顺序看：

1. `docs/superpowers/specs/attachments/2026-03-27-stardew-m1-appendix.md`
2. 本文
3. `docs/superpowers/specs/attachments/2026-03-27-stardew-capability-flow-appendix.md`
4. `docs/superpowers/specs/attachments/2026-03-27-stardew-hook-mapping-appendix.md`
5. `docs/superpowers/specs/attachments/2026-03-27-stardew-surface-commit-trace-failure-appendix.md`
6. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/GGBH_OpenAIWorld_功能梳理_非UI.md`

## 6. 最终结论

当前 `Stardew` 第一批参考 mod 迁移清单固定收为：

- `AI 私聊`
- `记忆`
- `NPC 信息面板`
- `当前想法`
- `物品语境 / 赠与`
- `关系`
- `群聊`
- `手机私信`

其中：

- `AI 私聊 / 记忆 / NPC 信息面板 / 当前想法 / 物品语境 / 关系`
  共同构成当前继承的 historical first-wave 中最稳定的 `M1 core` 主体
- `群聊 / 手机私信`
  虽然治理上属于 `M1 implementation_only`，但当前只表示：若 build 显式启用，仍应进入实现、联调与留证据；未启用时不作为当前 reference-grade hardening closeout blocker

后续实现、review、留证据时，默认都应先回到这份迁移清单确认“该能力是不是当前 historical first-wave 正式范围内的 source-faithful 迁移对象”。

但这一步只用于确认当前实现范围，不得替代：

- ship-gate 判定
- 外部 support claim 判定
- waiver / disclosure 判定
