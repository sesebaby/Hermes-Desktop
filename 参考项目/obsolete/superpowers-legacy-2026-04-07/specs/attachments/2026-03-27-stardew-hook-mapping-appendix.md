# Stardew Hook Mapping Design

## 1. 文档定位

本文档用于把 `Stardew Valley` 的宿主接入面进一步细化成：

- 哪些能力优先走 `SMAPI` 事件
- 哪些能力优先走 `Game Data / Content` 面
- 哪些能力必须落到 `Harmony patch`
- 哪些接入点当前只是推荐候选，而不是已证实唯一实现点

补充说明：

- 本文对 `group_chat` 的接入描述，承接当前 `M1 implementation_only` 口径：
  - 必须实现
  - 必须联调
  - 必须 review
  - 必须留证据
  - 但它不是当前 `M1` ship-gate
- 本文对 `information_propagation / active_world` 的接入描述，继续只保留为 annex / experiment-only 的宿主映射真相源

本文档承接：

- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-m1-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-capability-flow-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-26-openaiworld-ai-reproduction-appendix-host-porting.md`
- `docs/superpowers/specs/attachments/2026-03-26-stardew-ai-mod-feature-borrowing-from-ggbh-openaiworld.md`
- `docs/superpowers/specs/attachments/2026-03-26-stardew-seed-item-text-only-reference.md`

当前证据基线同时参考：

- `SMAPI` 文档类别
- `Content Patcher` 文档
- 本地 Stardew 参考项目中的 `DialogueBox`、`AddMail`、`modData`、`DisplayName`、`tooltip`、`ButtonPressed`、`Warped`、`DayStarted`、`SaveLoaded` 等使用方式

## 2. 总原则

### 2.1 接入优先级

Stardew 宿主接入优先级固定为：

1. `SMAPI` 公开事件与标准菜单/输入生命周期
2. `Game Data / Content` 面
3. `item.modData` 等宿主原生扩展位
4. `Harmony patch`

解释：

- 能用公开事件或标准数据面解决的，不先上 patch
- 只有当宿主没有足够的公开接入点，或者需要改写显示结果时，才落到 `Harmony patch`

### 2.2 不在本文件里假装写死不存在证据的唯一 API

本文件会区分：

- `推荐接入面`
- `推荐候选事件 / API`
- `必须依赖 patch 的显示面`

但不会把尚未验证的具体方法名伪装成唯一真相源。

### 2.2A 当前并行开发的 authoritative hook 合同

当前 `M1-source-faithful` 下，authoritative contract 固定冻结在 `semantic hook` 层，而不是冻结到具体方法名：

- `hostDialogueRenderedAt`
- `hostDialogueRecordedAt`
- `hostDialogueExhaustedAt`
- `aiDialogueOpenedAt`
- `aiDialogueRenderedAt`
- `aiDialogueClosedAt`
- `remoteThreadOpenedAt`
- `remoteSubmitQueuedAt`
- `remoteMessageRenderedAt`
- `groupParticipantSetFrozenAt`
- `groupPlayerInputQueuedAt`
- `groupTurnRenderedAt`
- `thoughtRequestedAt`
- `thoughtRenderedAt`
- `itemCarrierRenderedAt`
- `itemEventRecordedAt`
- `infoPanelRenderedAt`
- `memoryTabRenderedAt`
- `groupHistoryTabRenderedAt`
- `groupHistoryDisclosureResolvedAt`
- `relationTabRenderedAt`
- `itemTabRenderedAt`

规则：

- 这些 semantic hook 是当前并行开发正式合同
- 具体 API / patch 点可以因宿主证据继续调整
- 但任何实现都必须最终产出这些 semantic hook 事件，不能再自行改名或缺省

### 2.3 参考 mod 已证明机制不在这里重写

本文件不重写：

- 私聊编排
- 群聊 speaker selection
- 记忆压缩
- deterministic gate

本文件只回答：

- Stardew 里应该从哪里接
- 什么层负责接

## 3. 证据来源简述

### 3.1 已有 repo 文档中的 Stardew 接入基线

现有文档已经明确指出，Stardew 的工程底座优先参考：

- `SMAPI`
- Stardew Valley Wiki 的：
  - `Dialogue`
  - `Mail data`
  - `Event data`
  - `Trigger actions`
  - `Items`
  - `Data API`
  - `Harmony API`

同时明确提到值得复用的方向包括：

- `CustomGiftDialogue`
- `BirthdayMail`
- `HappyBirthday`
- `ImmersiveFestivalDialogue`
- `StardewGPT`

### 3.2 本地参考项目可直接支持的接入面

本地参考项目已提供足够证据支持以下宿主接入方向：

- `DialogueBox` / `Game1.activeClickableMenu`
- `CurrentDialogue`
- `Game1.drawDialogue(...)`
- `ButtonPressed`
- `SaveLoaded`
- `DayStarted`
- `Warped`
- `AddMail`
- `Data/Mail`
- `Data/Objects`
- `item.modData`
- `DisplayName`
- `getDescription()`
- tooltip 绘制 / hover 文本

## 4. 宿主接入分层

### 4.1 SMAPI 事件层

优先承接：

- 输入
- 存档加载
- 每日切换
- 地图切换
- 菜单切换

推荐候选：

- `ButtonPressed`
- `SaveLoaded`
- `DayStarted`
- `Warped`
- `MenuChanged`

### 4.2 Data / Content 层

优先承接：

- 邮件投递
- 任务/事件文本
- 少量受控物品条目
- 本地化显示文本

推荐候选：

- `Data/Mail`
- `Event data`
- `Trigger actions`
- `Data/Objects`
- `i18n`
- `Content Patcher EditData`

### 4.3 Item Instance 扩展层

优先承接：

- 实例级 AI 语境
- 实例名/描述覆写来源
- 物品与 NPC / 事件的关联

推荐候选：

- `item.modData`

### 4.4 Harmony Patch 层

优先只用于：

- 对话显示面改写
- tooltip / 名称 / 描述显示面改写
- 宿主无公开事件可用时的 UI 生命周期补钩

## 5. Private Dialogue Hook Mapping

### 5.1 目标

支撑以下宿主动作：

- 玩家点击 NPC
- 宿主原对话先说
- 记录宿主原对话
- 再次点击后进入 AI 对话框
- AI 文本显示、关闭、trace

### 5.2 推荐接入层

- 首选：
  - `SMAPI` 输入 / 菜单生命周期
- 补充：
  - `DialogueBox` / `Game1.drawDialogue(...)` 所在对话显示面
- 必要时：
  - `Harmony patch`

### 5.3 推荐映射

| 需求 | 推荐宿主接入面 | 类型 | 说明 |
| --- | --- | --- | --- |
| 玩家点击 NPC 触发交互 | `ButtonPressed` + NPC 交互判定 | 当前锁定 | 用于识别交互起点，不替代宿主原对话逻辑 |
| 识别当前是否进入对话菜单 | `MenuChanged` / `Game1.activeClickableMenu is DialogueBox` | 当前锁定 | 用于识别原对话框出现与退出 |
| 读取当前宿主对话是否仍有内容 | `CurrentDialogue` / 对话数据读取面 | 当前锁定 | 用于判断“原对话是否耗尽” |
| 打开 AI 自定义对话框 | 自定义 `IClickableMenu` / `DialogueBox` 风格菜单 | 宿主实现 | `Stardew` 自己补的可见面 |
| 识别 AI 文本已真正显示 | 自定义对话框内部的 `aiDialogueRenderedAt` 语义事件 | 宿主实现 | 用于 committed；具体 API 可变，但语义事件固定 |
| 原宿主对话被玩家看见后创建记录 | 原宿主对话的 `hostDialogueRenderedAt` 语义事件 | 宿主实现 | 不应早于玩家实际看见；具体 API 可变，但语义事件固定 |
| 原宿主对话记录创建 | `hostDialogueRecordedAt` 语义事件 | 宿主实现 | 与可见事件分离，供 recent history 与 trace 使用 |
| 头顶气泡补充投影 | 宿主自建气泡或轻量投影层 | 宿主实现 | 若保留，只是补充投影，不单独决定 private dialogue committed |

### 5.4 Patch 使用边界

只有当以下需求缺乏足够公开接入点时，才推荐 patch：

- 触发 `hostDialogueRenderedAt` 语义事件
- 拦截或包装原版风格对话框的显示结束
- 在不破坏原对话的前提下插入叠加 AI 对话框

## 5A. Remote Direct One-to-One Hook Mapping

### 5A.1 目标

支撑以下宿主动作：

- 从手机或联系人入口打开远程私信线程
- 远程消息渲染完成
- 线程关闭或切走
- remote availability 判定

### 5A.2 推荐接入层

- 首选：
  - 手机入口 / 联系人入口
  - 自定义 `IClickableMenu`
- 补充：
  - `MenuChanged`
  - `DayStarted`
- 必要时：
  - `Harmony patch`

### 5A.3 推荐映射

| 需求 | 推荐宿主接入面 | 类型 | 说明 |
| --- | --- | --- | --- |
| 打开手机私信线程 | 手机入口 / 联系人入口 -> 自定义菜单 | 当前锁定 | 不复用本地 NPC 点击对话入口 |
| 远程消息提交入队 | `remoteSubmitQueuedAt` 语义事件 | 宿主实现 | 用于 remote submit trace 与 `submission_failed` |
| 远程消息显示完成 | `remoteMessageRenderedAt` 语义事件 | 宿主实现 | 用于 committed |
| 线程关闭或切走 | `MenuChanged` + 自定义菜单关闭逻辑 | 当前锁定 + 宿主实现 | 用于 surface trace |
| availability 刷新 | `DayStarted` + 宿主状态检查 | 当前锁定 + 宿主实现 | 只重算 `available_now / unavailable_now`，不自动重发旧消息 |
| unavailable result 渲染 | 当前私信线程内的大白话 unavailable item | 宿主实现 | `unavailable_now` 时不创建 pending remote turn record |
| 同 NPC 线程复用 | `gameId + actorId + targetId + channelType` | 宿主实现 | 关闭重开同一 NPC 私信时复用同一线程 key |
| `remoteTurnId` 补充投影 | `historyOwnerActorId + canonicalRecordId + channelType` 导出/回链 | 宿主实现 | 只作线程内补充 id，不替代 authoritative join key |

## 6. Group Chat Hook Mapping

### 6.1 目标

支撑以下宿主动作：

- 现场 participant set 采样
- 群聊玩家输入
- 气泡显示
- 手机主动群聊 UI
- 切图 / 离场 / 超时导致的 surface 消失

当前 phase status：

- `M1 implementation_only`

### 6.2 推荐接入层

- 首选：
  - `SMAPI` 输入 / 地图切换 / 每日切换
- 宿主自建：
  - 现场群聊气泡系统
  - 底部输入框
  - 手机群聊菜单
- 补充：
  - 必要时 `Harmony patch` 做场景/UI 生命周期补钩

### 6.3 推荐映射

| 需求 | 推荐宿主接入面 | 类型 | 说明 |
| --- | --- | --- | --- |
| 现场 participant set 初始采样 | 当前位置 + 现场 NPC 可见/可交互采样逻辑 | 宿主实现 | 不要求由 SMAPI 单独提供高级社交集合 |
| 现场 participant set 固定算法 | 当前 location 已加载 + 可见 + 可交互 + 非 cutscene/dialogue lock + 距离玩家不超过 `8` tiles，按稳定 `actorId` 排序 | 宿主实现 | 当前轮 speaker selection 只对冻结后的 set 生效 |
| player message 提交 | `ButtonPressed` + 自定义输入框提交逻辑 | 当前锁定 + 宿主实现 | 玩家发送一句话时入队 |
| 现场 `groupSession` 创建 | 玩家首次发送 + 当前地点 participant set 冻结 | 宿主实现 | 不因单句自然气泡反向创建 session |
| 群聊气泡显示完成 | `groupTurnRenderedAt` 语义事件 | 宿主实现 | 用于每句 committed |
| 手机主动群聊打开 | 手机菜单入口 + `IClickableMenu` | 宿主实现 | 不建议依赖原版电话 UI 语义 |
| 手机主动群聊玩家输入提交 | `remoteSubmitQueuedAt` 语义事件 | 宿主实现 | 用于 remote group submit trace 与 `submission_failed` |
| 手机群聊消息显示完成 | `groupTurnRenderedAt` 语义事件 | 宿主实现 | 用于每条 committed |
| 后台远程群聊追加 | `contactGroupId` bucket 更新 + unread 增量 | 宿主实现 | 玩家未打开线程时也允许追加 recovered 风格远程群聊 turn |
| 远程多方消息结构 | `contactGroupId + groupTurnId + sequenceIndex + speakerActorId + visibleText + deliveryState + historyOwnerActorId + canonicalRecordId + sidecarRef` | 宿主实现 | 手机线程和后台追加都必须共用这组最小字段 |
| 地图切换导致群聊 surface 消失 | `Warped` | 推荐候选 | 作为 Stardew 宿主补充 trace |
| 每日切换导致会话状态刷新 | `DayStarted` | 推荐候选 | 用于重置/刷新现场上下文 |
| 现场 `groupSession` 结束 | `Warped` / 睡眠切日 | 推荐候选 + 宿主实现 | 气泡自然消失不回滚已 committed turn |

### 6.4 Patch 使用边界

只有在以下需求缺公开钩子时，才推荐 patch：

- 需要读取或协调某些宿主对话/事件中的多人可见状态
- 需要让群聊气泡更自然地绑定宿主角色渲染生命周期

## 7. Memory Hook Mapping

### 7.1 目标

支撑以下宿主动作：

- 收集 Stardew 宿主摘要
- 在请求前构建 snapshot/context
- 在时间桶边界或宿主定义周期触发记忆压缩
- 在 `NPC 信息面板` 中显示摘要记忆卡片

### 7.2 推荐接入层

- 首选：
  - `SaveLoaded`
  - `DayStarted`
  - `Warped`
  - 宿主事件驱动
- UI：
  - 自建 `NPC 信息面板`

### 7.3 推荐映射

| 需求 | 推荐宿主接入面 | 类型 | 说明 |
| --- | --- | --- | --- |
| 存档打开后初始化宿主摘要采样 | `SaveLoaded` | 推荐候选 | 建立当前存档的宿主摘要基线 |
| 每日开始刷新摘要快照 | `DayStarted` | 推荐候选 | 适合 Stardew 的日节奏 |
| 地图变更后刷新场景摘要 | `Warped` | 推荐候选 | 主要服务当前场景上下文 |
| 农场/任务/家庭状态变化时刷新 | 宿主事件驱动 + 请求前重新采样 | 宿主实现 | 不走高频轮询 |
| 记忆可见面 | `NPC 信息面板 -> 记忆 Tab` | 宿主实现 | `M1` 只显示 NPC 对玩家的时间桶摘要 |

### 7.4 Patch 使用边界

记忆链本身不优先依赖 patch。

只有在读取某类宿主状态缺乏干净读面时，才考虑 patch。

## 8. Item / Gift Hook Mapping

### 8.1 目标

支撑以下宿主动作：

- 文本先感知
- 实例物品创建
- 背包 / 奖励落地
- 实例名称 / 描述 / tooltip 覆写
- 物品与 NPC / 事件的关联记录

### 8.2 推荐接入层

- 首选：
  - `Data/Mail`
  - `AddMail`
  - 事件奖励 / 直接给物品
  - `item.modData`
- 受控例外：
  - `Content Patcher + Data/Objects + i18n`
- 必要时：
  - `Harmony patch` 改写名称 / 描述 / tooltip

### 8.3 推荐映射

| 需求 | 推荐宿主接入面 | 类型 | 说明 |
| --- | --- | --- | --- |
| 邮件/礼物通知 | `Data/Mail + AddMail` | 已有证据支持的推荐面 | 适合 `M1` 最短路径 |
| 奖励提示显示 | `itemCarrierRenderedAt` 语义事件 | 宿主实现 | `奖励提示物品文本 surface` 必须有独立 committed hook |
| 实际物品发放 | 事件奖励 / 直接背包落地 | 推荐接入面 | 真正发放不应只停留在邮件文本 |
| carrier 选择优先级 | 邮件 -> 奖励提示 -> tooltip/名称描述 | 宿主实现 | 同一 item/gift accepted bundle 只选择一个 authoritative first-perception carrier |
| 实例语境持久化 | `item.modData` | 明确推荐 | AI 名称/描述/关联元信息挂这里 |
| 实例显示名称/描述 | `DisplayName` / `getDescription()` 读取面 + patch | 推荐候选 + patch | 用于实例级覆写 |
| tooltip 可见面 | tooltip draw / hover text 面 + patch | 推荐候选 + patch | 用于玩家第一感知 |
| 极少量新条目受控例外 | `Content Patcher + Data/Objects + i18n` | 受控例外 | 不是默认路线 |

### 8.4 Patch 使用边界

物品链里最明确应该用 patch 的点是：

- 名称显示覆写
- 描述显示覆写
- tooltip 覆写

这些也是现有参考附件明确支持的方向。

## 9. NPC Info Panel / Thought Surface Mapping

### 9.1 目标

支撑以下宿主动作：

- 打开 `NPC 信息面板`
- 在一个固定面板里切换 NPC
- 展示 5 个 Tab
- 单独生成并显示 `当前想法`

### 9.2 推荐接入层

- 自建覆盖式 `IClickableMenu`
- `ButtonPressed` 作为打开动作之一
- 手机联系人入口作为菜单入口

### 9.3 推荐映射

| 需求 | 推荐宿主接入面 | 类型 | 说明 |
| --- | --- | --- | --- |
| 从 AI 对话框打开面板 | AI 对话框按钮 -> 自定义菜单 | 宿主实现 | 当前唯一默认近入口 |
| 从手机联系人打开面板 | 手机联系人列表 -> 自定义菜单 | 宿主实现 | 非聊天状态入口 |
| 信息面板显示完成 | `infoPanelRenderedAt` 语义事件 | 宿主实现 | 用于面板本体 committed |
| 切换 NPC | 下拉 / 头像点选 | 宿主实现 | 保留当前 Tab |
| 顶部 thought 入口或占位可见 | 面板顶部基础信息区渲染完成点 | 宿主实现 | 面板本体 committed 前必须先看到 thought 入口或占位位 |
| 当前想法生成 | `thoughtRequestedAt` 语义事件 | 宿主实现 | 允许短时间缓存防抖 |
| 切换 NPC 时 thought 失效 | 切换动作触发 stale / cancel / refetch | 宿主实现 | 旧 NPC thought 结果不得 commit 到新 NPC 面板 |
| 同 NPC thought 刷新失效 | 当前 NPC 手动刷新 / 关闭重开时触发 stale / refetch | 宿主实现 | 只有最新一次 thought 请求允许 commit |
| 当前想法显示完成 | `thoughtRenderedAt` 语义事件 | 宿主实现 | 独立 thought surface committed |
| thought replay key | `derivedThoughtRecordId + npcId + surfaceId` | 宿主实现 | surface-replayable thought projection record 必须具备唯一 derived id |
| 记忆 Tab 显示完成 | `memoryTabRenderedAt` 语义事件 | 宿主实现 | 对应记忆 Tab committed |
| 群聊历史 Tab 显示完成 | `groupHistoryTabRenderedAt` 语义事件 | 宿主实现 | 对应群聊历史 Tab committed |
| 群聊历史开放状态解析 | `groupHistoryDisclosureResolvedAt` 语义事件 | 宿主实现 | 必须产出 `groupHistoryDisclosureState` |
| 关系 Tab 显示完成 | `relationTabRenderedAt` 语义事件 | 宿主实现 | 对应关系 Tab committed |
| 物品 Tab 显示完成 | `itemTabRenderedAt` 语义事件 | 宿主实现 | 对应物品 Tab committed |

### 9.4 Patch 使用边界

该面板本身不优先依赖 patch。

只有在：

- 需要与宿主电话 / 菜单 / 头像显示面深度融合

而公开菜单接入不足时，才考虑 patch。

补充 UI/UX 依据：

- 本文件中的所有玩家可见入口与 surface mapping，统一回链到 `game-integration-profile` 中的 `UI/UX Basis`
- 具体宿主映射不得绕开该 basis 重新发明视觉方向、空态、失败态或交互反馈规则

## 10. 当前仍未写死的具体实现点

以下内容仍需要继续验证后再写成更细的实现映射表：

- `NPC 对话` 具体 patch 方法名
- 群聊气泡系统的宿主挂载细节
- 主动群聊 UI 的消息列表和滚动结构
- `NPC 信息面板` 与手机入口的最终菜单承载方式

## 11. 最终结论

本设计的最终结论如下：

1. Stardew 宿主接入优先顺序固定为：
   - `SMAPI 事件`
   - `Data / Content`
   - `item.modData`
   - `Harmony patch`
2. `NPC 对话` 主要依赖：
   - 输入 / 菜单生命周期
   - 对话显示面
   - 必要时的对话框 patch
3. `群聊` 主要依赖：
   - 输入 / 地图切换 / 宿主自建 UI
   - 现场群聊与主动群聊都以自建 surface 为主
   - 当前属于 `M1 implementation_only` 的宿主映射真相源：必须实现、联调、review、留证据，但不自动进入当前 exit criteria 或外部 support claim
4. `记忆` 主要依赖：
   - 宿主摘要采样
   - `SaveLoaded / DayStarted / Warped`
   - 自建 `NPC 信息面板`
5. `自定义物品` 主要依赖：
   - `Data/Mail + AddMail`
   - `item.modData`
   - 名称 / 描述 / tooltip 的 patch
   - 受控例外时的 `Content Patcher + Data/Objects + i18n`
6. 当前最需要继续补的是：
   - hook 类别到具体 API/patch 点的最终落位验证
