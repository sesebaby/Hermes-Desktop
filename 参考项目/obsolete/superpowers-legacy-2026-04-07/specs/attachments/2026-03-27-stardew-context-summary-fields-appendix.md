# Stardew Context Summary Fields Design

## 1. 文档定位

本文档用于把 `Stardew Valley` 的宿主摘要输入落成最小字段表。

本文档只回答以下问题：

- `Stardew` 额外上下文到底分哪几类摘要
- 每类摘要在 `M1` 至少要有哪些字段
- 哪些字段是可选补充而不是硬要求
- 这些摘要由谁采集、何时刷新、主要服务哪些能力链
- 除宿主摘要外，`private dialogue` 还必须带哪些非摘要输入合同

本文档承接：

- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-m1-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-capability-flow-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-hook-mapping-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-26-stardew-ai-mod-feature-borrowing-from-ggbh-openaiworld.md`
- `docs/superpowers/specs/attachments/2026-03-26-openaiworld-ai-reproduction-master-manual.md`

## 2. 总规则

### 2.1 宿主摘要只补 Stardew 特有事实

这些摘要不是为了重写参考 mod 的记忆机制，而是为了把 `Stardew` 宿主特有事实送进同一条成熟链路。

### 2.2 采集与归一化分工

分工固定如下：

- `Mod`
  - 负责采集 Stardew 宿主事实
- `Runtime`
  - 负责归一化与组装 canonical input

### 2.2A 最小 DTO 合同

当前 `M1-source-faithful` 下，摘要相关的 `Mod -> Runtime` 传输不再等待后续补完；最小 DTO 语义固定为：

- `hostSummaryEnvelope`
  - `summaryEnvelopeId`
  - `gameId`
  - `snapshotCapturedAt`
  - `farmSummary`
  - `inventorySummary`
  - `taskSummary`
  - `eventSummary`
  - `relationshipSummary`
  - `childrenSummary`
  - `petSummary`
  - `animalSummary`
- `summarySelectionHint`
  - `capability`
  - `actorId`
  - `targetId`
  - `participantSetRef`
  - `sceneRef`

底层 JSON / DTO 字段名可在实现中调整，但不得减少上述语义位点。

### 2.3 刷新方式

刷新方式固定为：

- 事件驱动刷新
- 请求前重新取当前快照

不采用后台高频轮询。

### 2.4 M1 收敛原则

`M1` 只保留：

- 能明显影响对话 / 群聊 / 记忆 / 物品语境的字段

不在 `M1` 引入：

- 重型 actor graph
- 过细的世界模拟状态
- 为了“以后可能用到”而先塞进输入的大量边缘字段

补充说明：

- `group_chat` 当前属于 `M1 implementation_only`，其设计真相源与实现链都可引用这里的相关摘要分类
- 这不等于它已经进入当前 `M1` ship-gate

## 3. 摘要分类总览

`Stardew` 额外宿主摘要固定分为：

1. `玩家农场信息摘要`
2. `玩家物品摘要`
3. `玩家任务摘要`
4. `事件摘要`
5. `婚姻 / 恋爱 / 同居情况摘要`
6. `孩子摘要`
7. `宠物摘要`
8. `养殖动物摘要`

## 4. 字段表

### 4.1 玩家农场信息摘要

用途：

- 服务对话语境
- 服务 NPC 当前想法
- 服务事件与来访、农场相关礼物/评论

M1 必需字段：

| 字段 | 说明 |
| --- | --- |
| `farmType` | 当前农场类型 |
| `farmName` | 农场名称 |
| `season` | 当前季节 |
| `dayOfMonth` | 当前日期 |
| `majorAreaStates[]` | 农场关键区域摘要，如作物区、畜棚区、果园区是否有显著状态 |
| `notableRecentChanges[]` | 最近显著变化，如新建筑、收获、荒废、布置变化 |

可选字段：

- `farmStyleTags[]`
- `notableDecorations[]`
- `recentFarmVisitorEvents[]`

刷新时机：

- 存档加载
- 每日开始
- 明显农场状态变化后
- 请求前重新取快照

### 4.2 玩家物品摘要

用途：

- 服务礼物、借还、协商、任务、物品相关对话
- 服务 NPC 信息面板中的 `物品 Tab`

M1 必需字段：

| 字段 | 说明 |
| --- | --- |
| `notableCarriedItems[]` | 当前玩家随身最重要物品摘要 |
| `recentReceivedFromNpc[]` | 最近从 NPC 收到的重要物品 |
| `recentGivenToNpc[]` | 最近送给 NPC 的重要物品 |
| `openBorrowedItems[]` | 借出未还 / 借入未还摘要 |
| `aiContextItems[]` | 带 AI 语境的实例物品摘要 |

可选字段：

- `favoriteOrFrequentItems[]`
- `questBoundItems[]`
- `storageHighlights[]`

刷新时机：

- 物品获得 / 失去
- 礼物事件
- 借还状态变化
- 请求前重新取快照

### 4.3 玩家任务摘要

用途：

- 服务任务相关对话
- 服务承诺、帮忙、协商与物品语境

M1 必需字段：

| 字段 | 说明 |
| --- | --- |
| `activeTasks[]` | 当前活跃任务摘要 |
| `recentCompletedTasks[]` | 最近完成任务摘要 |
| `npcBoundTasks[]` | 与当前 NPC 直接有关的任务摘要 |
| `outstandingPromises[]` | 仍待兑现的承诺或约定 |

可选字段：

- `failedOrExpiredTasks[]`
- `townBoardTasks[]`

刷新时机：

- 任务接取
- 任务完成/失败/过期
- 承诺状态变化
- 请求前重新取快照

### 4.4 事件摘要

用途：

- 服务“刚刚发生了什么”的即时语境
- 服务记忆压缩输入
- 服务节日、剧情、偶遇、来访、小插曲

M1 必需字段：

| 字段 | 说明 |
| --- | --- |
| `recentStoryEvents[]` | 最近剧情/事件摘要 |
| `recentNpcRelatedEvents[]` | 与当前 NPC 相关的重要事件 |
| `festivalState` | 当前是否处于节日或特殊日 |
| `recentSocialIncidents[]` | 最近社交小事件、冲突、见面、来访摘要 |

可选字段：

- `weatherDrivenEvents[]`
- `townIncidentSeeds[]`

刷新时机：

- 宿主事件完成
- 节日状态变化
- 地图切换后如有事件上下文变化
- 请求前重新取快照

### 4.5 婚姻 / 恋爱 / 同居情况摘要

用途：

- 服务关系、亲密语气、家庭语境、嫉妒/亲密/承诺类反应

规则：

- 按当前宿主事实组织
- 不硬编码为原版单配偶模型

M1 必需字段：

| 字段 | 说明 |
| --- | --- |
| `currentPartners[]` | 当前伴侣/恋爱/同居对象摘要 |
| `relationshipStatusSummary` | 当前整体亲密状态摘要 |
| `recentRelationshipChanges[]` | 最近关系状态变化 |
| `supportsMultiPartner` | 当前宿主事实是否处于多伴侣状态 |

可选字段：

- `cohabitationNotes[]`
- `jealousyOrConflictFlags[]`

刷新时机：

- 关系状态变化
- 宿主家庭结构变化
- 请求前重新取快照

### 4.6 孩子摘要

用途：

- 服务家庭语境
- 服务 NPC 当前想法与来访、送礼、家庭话题

M1 必需字段：

| 字段 | 说明 |
| --- | --- |
| `childrenPresent` | 是否有孩子 |
| `childrenSummary[]` | 每个孩子的简要摘要 |
| `recentChildRelatedEvents[]` | 最近和孩子相关的重要事件 |

可选字段：

- `childMoodHints[]`
- `childGrowthStageSummary[]`

刷新时机：

- 每日开始
- 家庭状态变化
- 请求前重新取快照

### 4.7 宠物摘要

用途：

- 服务日常生活感、亲近话题、 NPC 对玩家生活状态的感知

M1 必需字段：

| 字段 | 说明 |
| --- | --- |
| `petPresent` | 是否有宠物 |
| `petSummary[]` | 宠物摘要 |
| `recentPetRelatedEvents[]` | 最近宠物相关事件 |

可选字段：

- `petBehaviorHints[]`

刷新时机：

- 每日开始
- 相关事件发生
- 请求前重新取快照

### 4.8 养殖动物摘要

用途：

- 服务农场生活语境
- 服务礼物、任务、来访、农场评论

M1 必需字段：

| 字段 | 说明 |
| --- | --- |
| `animalCategoryCounts[]` | 各类养殖动物数量摘要 |
| `notableAnimalStates[]` | 显著状态摘要，如新出生、缺料、产出高峰 |
| `recentAnimalRelatedEvents[]` | 最近养殖动物相关事件 |

可选字段：

- `favoriteNamedAnimals[]`
- `seasonalAnimalNotes[]`

刷新时机：

- 每日开始
- 畜棚关键状态变化
- 请求前重新取快照

## 5. 输入裁剪规则

### 5.1 请求前裁剪

每次真正送进编排链前，应只保留：

- 与当前 NPC 直接相关
- 与当前地点、当前事件直接相关
- 与当前能力链直接相关

的摘要内容。

### 5.2 不应一次性把全部摘要原文塞满

即使这些摘要都存在，也不应每次都全量注入。

最小原则：

- `private dialogue`
  优先当前 NPC、当前地点、当前事件相关摘要
- `group_chat`
  优先 participant set、当前 scene、当前 topic 相关摘要
- `current thought`
  优先当前 NPC 视角最贴近的即时摘要
- `item / gift`
  优先物品、任务、关系、近期事件相关摘要

### 5.3 `private dialogue` 的非摘要输入合同

除宿主摘要外，`private dialogue` 仍必须按参考链保留以下非摘要输入：

- `recent private history`
  - 每条至少带：
    - 日期
    - 地点
    - 天气
    - role / content
- relation snapshot
- current scene snapshot
- optional long-memory summary
- accepted deterministic outcomes replay
- sidecar replay

其中：

- sidecar replay 必须按 `historyOwnerActorId + canonicalRecordId` 取回结构化 sidecar
- `messageIndex` 只允许作为单线程内排序字段，不得替代 authoritative replay key
- 后续 prompt replay 不得只回灌纯文本最近几句
- 宿主摘要只负责补 `Stardew` 特有事实，不替代以上非摘要输入合同

## 6. 与参考 mod 的边界

本文件不改变参考 mod 的以下主事实：

- raw history 与 summary memory 分层
- summary memory 按时间桶组织
- 记忆压缩由独立 prompt 生成
- actor-owned long memory 仍是底层主事实
- relation snapshot 必须是 actor-relative view，不得退化成对称 / 全局 merged relation object

`M1` 的 UI 收敛只影响：

- 玩家看到什么

不影响：

- 底层存储与压缩机制

## 7. 当前未写死的后续细化

以下内容仍可在后续专项继续细化：

- 每类摘要的字段长度与裁剪上限
- 每类摘要的优先级排序
- 哪些摘要字段默认只用于 `thought surface`
- 哪些摘要字段也进入 `group chat` 或 `item generation` 链

## 8. 最终结论

本设计的最终结论如下：

1. `Stardew` 额外宿主摘要固定分为 8 类：
   - 农场
   - 物品
   - 任务
   - 事件
   - 婚姻/恋爱/同居
   - 孩子
   - 宠物
   - 养殖动物
2. 这些摘要由 `Mod` 采集、由 `Runtime` 归一化。
3. 刷新方式固定为：
   - 事件驱动
   - 请求前重新取当前快照
4. `M1` 只保留对当前能力链真正有用的最小字段。
5. 这些摘要的作用是：
   - 补 `Stardew` 宿主事实
   - 不重写参考 mod 的成熟机制
