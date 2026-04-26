# All Game In AI 跨游戏类型扩展性专项设计

> Historical extensibility note as of 2026-03-27.
>
> This document is still useful for protocol-shape thinking, but any passages that place planning, memory, or heavy AI orchestration primarily inside local `Runtime` should now be read as superseded by the current framework direction:
> - hosted narrative orchestration truth source
> - local deterministic execution truth source
> - cross-game portability built around canonical snapshot / recent-history / deterministic outcome contracts rather than local orchestration ownership

## 1. 文档定位

本文档是《All Game In AI 技术栈与授权架构设计》的专项补充，专门用于解决“接入更多游戏类型时，协议与 Runtime 是否会被迫推倒重来”的问题。

本文档不替代主技术方案，但其中定义的共享身份、时间、世界状态、命令和演化规则，一旦确认，就必须回写为主协议硬约束，而不是长期停留在“补充建议”层。

本文档把以下跨类型问题单独定清：

- 身份模型如何支撑不同游戏类型
- 时间与模拟语义如何统一
- 世界状态如何以可扩展方式表达
- 共享 Facet 如何避免快速退化为 `extensions.<gameId>.*`
- 命令模型如何同时容纳粗粒度事务和未来实时控制

本文档默认约束如下：

- 第一优先级是 `单机游戏`
- 可兼容少量 `轻联机/协作型游戏`，例如类似《饥荒联机版》的协作场景
- 不以 `多人竞技`、`MOBA`、`强 PvP 对抗` 作为第一阶段设计目标
- 但不能把协议写死到“只能支撑两个 RPG/模拟游戏”

## 2. 设计目标

本专项设计的目标不是“一次性抽象完所有游戏”，而是保证：

1. 再接入 `动作游戏`、`格斗游戏`、`策略游戏`、`沙盒经营`、`生存协作` 时，不需要推翻主协议
2. 不同类型游戏可以通过增量扩展进入同一 Runtime 和同一 Shared Contract 体系
3. 通用 Capability 依然尽量依赖稳定语义，而不是依赖每个游戏的私有字段

## 3. 范围边界

本文档重点覆盖以下类型簇：

- `RPG / Action RPG / Adventure`
- `Life Sim / Farm Sim / Cozy / Sandbox / Management`
- `Strategy / 4X / Tactics / Colony / Automation / Deckbuilder`
- `Action / Shooter / Fighting / Racing / Sports`
- `Co-op Survival / Session-based PvE`

本文档当前不深入展开以下方向：

- `MOBA`
- `竞技 PvP`
- `大型多人在线`
- `强服务端权威对战同步`

这些方向不是永久排除，而是不作为当前扩展性基线。

## 4. 核心结论

为了避免“接了两个游戏后推倒重来”，平台必须从现在开始把跨类型扩展性建立在以下五个一等模型上：

- `Identity Graph`
- `Temporal Model`
- `WorldSnapshot / WorldDelta`
- `Facet Registry`
- `Command Lifecycle & Execution Plane`

这五个模型中，任何一个缺失，都会在新增游戏类型时迅速把逻辑打回 `extensions.<gameId>.*` 或把复杂性推回 `Runtime Core`。

## 5. Identity Graph

### 5.1 为什么不能只靠 `gameId + saveId/worldId + characterId`

这组键对单人 RPG 勉强可用，但一旦进入以下场景就会失效：

- 一个游戏同时存在 `存档槽位` 和 `持久世界`
- 一个角色控制多个单位、召唤物、宠物、载具、建筑
- 一个世界里同时存在 `角色`、`物体`、`地块`、`队伍`、`阵营`、`据点`
- 一个协作游戏里同时存在多个参与者和多个被控制实体

因此，平台不能再把 `characterId` 当作默认中心身份。

### 5.2 统一身份图

共享协议必须把身份模型提升为图，而不是单个主键。

最小身份图建议如下：

- `profileId`
- `saveSlotId`
- `worldId`
- `sceneId/locationId`
- `actorId`
- `objectId`
- `ownerId`
- `controllerId`
- `partyId`
- `factionId`
- `subjectRefs[]`

字段含义如下：

- `profileId`：玩家档案或账号侧身份
- `saveSlotId`：存档槽位身份
- `worldId`：世界实例身份
- `sceneId/locationId`：当前场景、区域或地图位置身份
- `actorId`：主动体，可能是角色、单位、宠物、载具、建筑代理
- `objectId`：非主动体对象，如物品、设施、地块、卡牌、建筑块
- `ownerId`：归属关系
- `controllerId`：当前控制关系
- `partyId`：队伍或编组关系
- `factionId`：阵营或势力关系
- `subjectRefs[]`：一次事件涉及的多个主体引用

### 5.3 与现有字段的关系

必须明确：

- `characterId` 仍可保留，但应视为 `actorId` 的特化别名
- `speaker`、`target` 只适合表达单主体交互，不足以支撑复杂事件
- 幂等、状态分区、能力作用域都不得再强依赖 `characterId`

### 5.4 Identity Stability Contract

只有字段名还不够，必须同时固定身份稳定性约束：

- `profileId`、`saveSlotId`、`worldId` 必须声明是否跨启动稳定
- `actorId`、`objectId` 必须声明唯一域、可否重用、可否跨 reload 保持
- `subjectRefs[]` 必须显式声明引用对象类型
- 临时运行时对象 ID 不得伪装成长期身份主键

每个新游戏都必须提交 identity sample 与 collision/reload 测试结果。

## 6. Temporal Model

### 6.1 为什么 wall-clock 不够

`timestamp` 和 `occurredAt` 只能表达现实时间，不能表达：

- 游戏内日期和时段
- 回合、阶段、步骤
- 帧、tick、局内模拟时钟
- 暂停态、时间倍率
- 同步结算或分批结算

这会直接伤到：

- 生活模拟的作息、营业时间、天气和季节
- 策略游戏的回合、阶段和结算窗口
- 动作游戏的 frame/tick 语义

### 6.2 统一时间信封

共享协议必须引入 `simulationContext`，最少包含：

- `simulationMode`
- `timelineId`
- `stateVersion`
- `turnId`
- `roundId`
- `phaseId`
- `stepId`
- `tickId/frameId`
- `inGameTimestamp`
- `dayPart`
- `season`
- `isPaused`

### 6.3 约束

必须明确：

- 单人 RPG 和种田游戏可以不使用 `turnId`，但不能没有 `stateVersion` 和 `inGameTimestamp`
- 策略/卡牌/战棋类必须能表达 `turn/phase/step`
- 动作类必须能表达 `tick/frame`
- 事件和命令都必须携带它们所属的 `simulationContext`

### 6.4 Temporal Consistency Rules

还必须补齐真实项目最容易踩坑的时间一致性规则：

- 存档回读是否创建新 `timelineId`，必须由游戏适配层显式声明
- `stateVersion` 是否单调递增，必须作为接入契约的一部分
- 回滚、重载或 timeline 切换后，旧命令如何失效必须写清
- 同批结算必须允许 `resolutionBatchId`
- 跨帧和长时间命令必须声明绑定的时间窗

## 7. WorldSnapshot / WorldDelta

### 7.1 为什么只靠事件不够

事件适合表达“发生了什么”，但不适合表达“当前世界整体长什么样”。

对于以下类型，仅靠事件会很快失控：

- 策略
- 殖民地经营
- 沙盒建造
- 卡牌/区域管理
- 生存基地

### 7.2 统一世界模型

共享协议必须支持版本化的世界状态表达：

- `WorldSnapshot`
- `WorldDelta`

最少字段建议如下：

- `entityId`
- `entityType`
- `ownerId`
- `controllerId`
- `zoneId`
- `components`
- `relations`
- `topology`
- `stateVersion`
- `removedEntityIds`

### 7.3 设计原则

- `WorldSnapshot` 用于初始化、恢复、重同步
- `WorldDelta` 用于增量变化
- Capability 可以声明自己依赖哪类 graph schema
- 必须支持分页、分块和 checkpoint/watermark

### 7.4 Delta 语义约束

必须避免每个游戏各做一套私有 patch 语义：

- 每个 `WorldDelta` 都必须声明 `schemaVersion`
- 每个 `WorldDelta` 都必须声明 `baseStateVersion`
- 必须明确是替换、merge patch 还是 component diff
- 分块 delta 必须声明 chunk 完整性边界与重放顺序
- 新游戏接入必须提供 snapshot/delta round-trip 证明

## 8. Facet Registry

### 8.1 现有 Facet 不够

当前已有的语义面：

- `conversationFacet`
- `relationshipFacet`
- `inventoryFacet`
- `combatFacet`
- `questFacet`
- `scheduleFacet`

对于扩展到更多类型，这是不够的。

### 8.2 最小扩展 Facet 集

建议从现在起把 Facet 做成可版本化注册表，并补齐至少以下 Facet：

- `progressionFacet`
- `economyFacet`
- `craftingProductionFacet`
- `worldStateFacet`
- `constructionFacet`
- `environmentFacet`
- `statusEffectFacet`
- `navigationFacet`
- `mapFacet`
- `factionFacet`
- `productionFacet`
- `researchFacet`
- `cardFacet`
- `colonyFacet`
- `logisticsFacet`
- `motionFacet`
- `poseFacet`
- `animationFacet`
- `targetingFacet`
- `reactionFacet`

### 8.3 版本化规则

必须明确：

- Facet 不应与整个平台协议完全锁死
- 新共性语义应优先以 `Facet schema` 升级，而不是直接推动整体协议大版本
- `Capability` 依赖声明应支持“最小 Facet 版本”

### 8.4 Extension 治理规则

如果没有治理规则，`Game Extensions` 最终一定会淹没共享协议。

因此必须固定：

- 同一语义若已在 `2` 个以上游戏中重复出现在 `Game Extensions`，下一次接入前必须先评审是否晋升为 `Facet`
- 每个游戏都必须声明自己的 `extension budget`
- 每次接入都必须提交 `promotion candidates`
- 运行时必须统计 extension 命中率、translator fallback 次数和 facet 缺失率

## 9. Command Lifecycle & Execution Plane

### 9.1 为什么现有命令模型不够

当前命令模型更像“一次性事务命令”，适合：

- 显示文本
- 生成物品
- 一次性交互

但不适合：

- 建造队列
- 移动队列
- 长时间执行的任务
- 需要取消、打断、替换的动作
- 连续控制流

### 9.2 两类命令

必须从现在开始区分两类命令：

- `Transactional Command`
- `Control / Intent Command`

前者适合：

- 物品生成
- 记忆写入
- 一次性状态修改

后者适合：

- 移动
- 跟随
- 瞄准
- 连续动作
- 建造计划
- 生产队列

### 9.3 生命周期

`Control / Intent Command` 最少需要支持以下状态：

- `queued`
- `accepted`
- `started`
- `updated`
- `interrupted`
- `cancelled`
- `completed`
- `expired`

并支持以下字段：

- `queueId`
- `batchId`
- `priority`
- `deadline`
- `executeAt`
- `expiresAt`
- `supersedesCommandId`
- `cancelCommandId`

### 9.4 执行层拆分

对于跨类型扩展，不能继续只说“90% Runtime，10% Mod”。

更稳的表述应该是：

- `Deliberation Layer`
  位于 `Runtime`，负责规划、策略、记忆、推理、长期能力编排

- `Edge / Reflex Layer`
  位于 `Mod` 或游戏适配层，负责帧关键、低延迟、紧贴游戏线程的即时执行逻辑

这个拆分在动作类尤其重要，但即使是单机动作/格斗/射击，也不能忽略。

### 9.5 Hosted Execution / Billing Plane

对于平台托管高级能力，执行层不能只剩本地两层，还必须显式补一层：

- `Hosted Execution / Billing Plane`
  位于 `Hosted Capability Service`，负责高成本生成、供应商调用、账本状态机、取消/退款与审计

只要某项能力采用平台托管并按次数收费，它就必须显式落在这一层，而不是偷偷塞回 `Command Lifecycle` 的本地两层模型里。

### 9.6 队列权威与状态权威

命令模型必须明确权威边界，否则 Runtime 与 Mod 一定双写：

- `Runtime` 是意图、队列和 supersede/cancel 决策的权威来源
- `Mod` 是边缘执行回执和即时执行状态的权威来源
- 所有长生命周期命令都必须能回放 `intent -> accepted -> edge_ack -> state_transition` 链路

## 10. 协议演化策略

### 10.1 不能一扩展就全量锁步升级

如果每新增一个重要字段、Facet、graph schema，都要求：

- `Launcher`
- `Runtime`
- `Mod`
- `Capability`

全部一起大改，那么平台会非常脆。

### 10.2 推荐策略

应把演化分成三层：

- `Transport Compatibility`
- `Schema Capability Negotiation`
- `Facet / Graph / Command Schema Versioning`

启动时应下发或协商至少这些能力清单：

- 支持的 `Facet` 及版本
- 支持的 `World graph schema`
- 支持的 `Command semantics`
- 必需 `requiredCapabilities`

这样新游戏可以先带入新的 schema，而不是立刻逼整个平台大版本升级。

### 10.3 新游戏接入门槛

每个新游戏在进入受支持接入前，必须提交 `Game Onboarding Contract Pack`，至少包含：

- `Translator manifest`
- `Facet` 使用清单
- `Identity sample`
- `simulationContext` sample
- `WorldSnapshot / WorldDelta` 样例
- `golden replay` 包
- `Game Extensions` 清单
- `unsupported semantics` 报告
- 性能与诊断基线

缺任何一项，不得进入正式接入。

## 11. 测试重点

为了保证以后不会推倒重来，这份专项设计对应的测试必须补齐：

- Identity graph round-trip 测试
- `simulationContext` 兼容测试
- `WorldSnapshot / WorldDelta` 一致性测试
- 大实体图性能测试
- checkpoint / watermark / replay 测试
- command queue / cancel / interrupt 测试
- 跨 facet 版本兼容测试

同时必须把以下内容设为硬门槛：

- replay 一致率必须达标
- snapshot/delta round-trip 必须达标
- extension 覆盖率与低可信字段比例必须可量化
- 大实体图性能必须有明确基线

## 12. 推荐落地顺序

最稳的落地顺序如下：

1. 先补 `Identity Graph`
2. 再补 `simulationContext`
3. 再补 `Facet Registry`
4. 再补 `WorldSnapshot / WorldDelta`
5. 最后补 `Command Lifecycle` 与实时执行面

原因很简单：

- 身份和时间不稳，后面的世界模型一定会漂
- 世界模型不稳，Facet 和 Capability 依赖会持续返工
- 命令模型应该建立在身份、时间、世界状态都稳定之后

## 13. 最终结论

如果平台只想支撑两三个 RPG 或模拟游戏，现有主方案已经够用。

但如果目标真的是：

- 继续接入动作游戏
- 继续接入格斗游戏
- 继续接入策略和经营游戏
- 避免做到第二、第三类游戏时推倒重来

那么必须尽快把以下五个模型升级为一等公民：

- `Identity Graph`
- `Temporal Model`
- `WorldSnapshot / WorldDelta`
- `Facet Registry`
- `Command Lifecycle & Execution Plane`

这不是“未来优化项”，而是平台是否能跨类型持续扩展的生死线。
