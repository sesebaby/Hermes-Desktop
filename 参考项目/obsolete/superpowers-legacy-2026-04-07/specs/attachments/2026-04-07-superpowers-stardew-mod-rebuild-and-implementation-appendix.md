# Superpowers Stardew Mod 重建与功能落地附件

## 1. 文档定位

本文只做一件事：

- 用大白话把 `games/stardew-valley/Superpowers.Stardew.Mod` 到底怎么改、怎么拆、怎么保留壳、怎么断旧逻辑写死。

本文不再讲愿景，不再重复总架构。  
它专门回答：

1. `Superpowers.Stardew.Mod` 里现有哪些类只是宿主壳，应该保留。
2. 哪些类已经越界拿了业务 authority，必须迁走或断电。
3. 每个 Stardew 功能到底要落到哪些宿主类、哪些新类、哪些参考 mod 分层。
4. 后面写 plan 或写代码时，哪些地方不允许 AI 自由发挥。

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-feature-reference-implementation-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-host-integration-map-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-hook-session-projector-executor-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-current-code-retirement-and-cutover-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-architecture-gap-and-blueprint-appendix.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

## 2. 为什么必须补这份附件

前面几份文档已经把：

1. 总架构
2. 功能清单
3. 参考 mod 对照
4. 系统缺口

讲出来了。

但还缺最关键的一层：

- `Superpowers.Stardew.Mod` 工程内部到底怎么重建

当前最危险的问题不是“没 UI”，而是：

1. `NpcNaturalInteractionController` 已经不只是触发壳，而是在同时做：
   - 触发状态机
   - 上下文组装
   - 历史读取
   - 请求发起
   - reply 提交
   - 信息面板加载
2. `RuntimeClient` 已经不只是 transport 壳，而是在同时做：
   - request builder
   - UI projector
   - finalize 协调
   - item give host receipt 流程
3. 手机私信、手机群聊、现场群聊现在更像：
   - `手动热键调试壳`
   不是正式手机 / 群聊系统
4. 物品生成、交易、主动对话、自动行动这些关键能力：
   - 语义写了
   - 参考提了
   - 但宿主 creator / executor / schedule restore 没写死

所以这份附件的目标不是“补介绍”，而是：

- `把 mod 工程内部施工蓝图钉死`

## 3. 总体重建结论

### 3.1 重建目标

`Superpowers.Stardew.Mod` 以后固定只做 4 类事：

1. `Trigger`
   - 监听 Stardew 宿主事件、输入、菜单、tick、location 变化
2. `Host Snapshot`
   - 从 Stardew 宿主读取真实事实
3. `Player-visible Surface`
   - 打开宿主 UI、手机 UI、overlay、物品 carrier
4. `Authoritative Writeback`
   - 最后真正改宿主

固定不再允许它继续做：

1. 本地 prompt 编排
2. 本地语义决策
3. 本地主线状态 owner
4. 把 transport、projector、业务状态机糊成一个类

### 3.2 当前 mod 内部分层

以后 `Superpowers.Stardew.Mod` 固定按 6 层拆：

1. `Hooks`
   - 只负责监听宿主事件和发 semantic hook
2. `HostSnapshots`
   - 只负责读 Stardew 事实
3. `SurfaceSessions`
   - 只负责 surface 生命周期和本地会话壳
4. `RuntimeTransport`
   - 只负责请求和回包 transport
5. `HostProjectors`
   - 只负责把 runtime 结果翻成 Stardew UI / host apply 输入
6. `HostWriteback`
   - 只负责最终改宿主

### 3.3 两个必须瘦身的类

#### A. `NpcNaturalInteractionController`

当前问题：

- 它已经从“自然触发壳”长成“私聊主链总控”。

以后固定收缩成：

- `NaturalInteractionRouteCoordinator`

它只允许保留：

1. 接触目标判定后的路由
2. 原版对话已开 / 已关 / 已耗尽的宿主状态转移
3. 打开 `AiDialogueMenu`
4. 打开 `NpcInfoPanelMenu`

它必须迁出去的逻辑：

1. `BuildProfileSummary`
2. `BuildRelationFacts`
3. 最近历史读取
4. 私聊 request 拼装
5. reply 提交
6. 面板并行加载

#### B. `RuntimeClient`

当前问题：

- 它已经从“Mod -> Runtime transport 壳”长成“transport + request builder + projector + finalize coordinator”。

以后固定收缩成：

- `StardewRuntimeTransportClient`

它只允许保留：

1. HTTP 请求
2. HTTP 回包
3. finalize 调用
4. health probe

它必须迁出去的逻辑：

1. request builder
2. projector
3. 聊天历史分组
4. item give host receipt 组装
5. carrier 选择

## 4. 当前代码分桶

逐文件旧代码处置、断电点、替代新类和 grep 验收词，不再混在这份蓝图正文里，统一回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-current-code-retirement-and-cutover-appendix.md`

| 当前类 / 文件 | 现在实际问题 | 新定位 | 处置 |
| --- | --- | --- | --- |
| `ModEntry.cs` | 入口类里还在拼 `ResolvedPrivateDialogueContext` | 宿主总入口壳 | 保留壳，迁走上下文组装 |
| `Hooks/NpcInteractionHooks.cs` | 无大问题 | semantic hook 壳 | 保留 |
| `Hooks/MenuLifecycleHooks.cs` | 无大问题 | surface trace 壳 | 保留 |
| `Hooks/WorldLifecycleHooks.cs` | 只有 hook，没有 session owner | 生命周期 hook 壳 | 保留，补 session owner |
| `Hooks/ItemCarrierHooks.cs` | 无大问题 | item carrier trace 壳 | 保留 |
| `Hooks/NpcNaturalInteractionController.cs` | 越界做总控 | 自然触发路由壳 | 保留壳，强瘦身 |
| `Runtime/RuntimeClient.cs` | 越界做 transport + builder + projector + finalize | runtime transport 壳 | 保留 transport，拆出 builder / projector / writeback coordinator |
| `UI/AiDialogueMenu.cs` | 基本是 UI 壳 | 私聊 surface 壳 | 保留 |
| `UI/NpcInfoPanelMenu.cs` | 基本是 UI 壳，但 currentState ready 时机没收死 | 信息面板壳 | 保留，补 session binder |
| `UI/PhoneDirectMessageMenu.cs` | 现在只是线程壳 | 手机私信壳 | 保留 |
| `UI/PhoneActiveGroupChatMenu.cs` | 现在只是线程壳 | 手机群聊壳 | 保留 |
| `UI/OnsiteGroupChatOverlay.cs` | 现在只是现场群聊壳 | 现场群聊壳 | 保留 |
| `UI/Carriers/ItemTextCarrierBase.cs` | 现在只是 carrier 壳 | 物品文本 carrier 壳 | 保留 |
| `UI/ManualTestEntryController.cs` | 仍然是手动调试入口 | 调试 / 验证专用壳 | 保留，但不得当正式入口 |
| `UI/StardewMenuPresenter.cs` | 手机 / 群聊还是 shell menu | 调试 presenter | 保留作 debug，不当正式产品 UI |
| `Exposure/BuildExposureConfig.cs` | 现在混入了 disclosure/availability 语义 | build 露出开关壳 | 保留，但不再冒充真实线程状态 |

## 5. 新增类与目录

以后 `games/stardew-valley/Superpowers.Stardew.Mod` 固定新增以下目录层：

### 5.1 `Application/Routes`

负责：

1. 私聊自然触发路由
2. 手机私信入口路由
3. 手机联系人入口路由
4. 群聊入口路由

建议新增类：

1. `PrivateDialogueRouteCoordinator`
2. `PhoneDirectRouteCoordinator`
3. `PhoneContactBookRouteCoordinator`
4. `GroupChatRouteCoordinator`

### 5.2 `HostSnapshots`

负责：

1. 读取当前对话上下文
2. 读取 NPC 基础资料
3. 读取日程事实
4. 读取交易 / 物品 / 位置 / scene 事实

建议新增类：

1. `HostDialogueSnapshotBuilder`
2. `NpcPanelSnapshotBuilder`
3. `ScheduleSnapshotBuilder`
4. `TradeSnapshotBuilder`
5. `GroupParticipantSnapshotBuilder`

### 5.3 `SurfaceSessions`

负责：

1. surface 生命周期
2. stale / loading / ready / failure
3. thread key / group session / unread / DND 本地壳状态

建议新增类：

1. `AiDialogueSurfaceSession`
2. `NpcInfoPanelSurfaceSession`
3. `RemoteDirectThreadSession`
4. `PhoneGroupThreadSession`
5. `OnsiteGroupSession`

### 5.4 `Projectors`

负责：

1. 把 runtime 回包翻成 Stardew UI model
2. 把 runtime accepted action 翻成 host writeback 输入

建议新增类：

1. `AiDialogueSurfaceProjector`
2. `NpcInfoPanelProjector`
3. `RemoteDirectThreadProjector`
4. `GroupChatProjector`
5. `ItemCarrierProjector`

### 5.5 `HostWriteback`

负责：

1. 最终宿主执行
2. 交易 / 给物 / 自定义物品实例化
3. 主动动作 / 自动行动执行
4. 回日程

建议新增类：

1. `ItemGiftHostExecutor`
2. `TradeHostExecutor`
3. `StardewItemInstantiationExecutor`
4. `NpcAutoActionExecutor`
5. `NpcScheduleRestoreExecutor`

## 6. 分功能落地蓝图

### 6.1 AI 私聊自然触发链

参考锚点：

1. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/01_私聊功能.md`
2. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/05_消息模型_通信通道与消息持久化.md`
3. `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/Behaviors/NpcInteractionBehavior.cs`

当前保留：

1. `ModEntry.OnMenuChanged`
2. `NpcInteractionHooks`
3. `AiDialogueMenu`

必须迁走：

1. 上下文组装
2. 面板并行加载
3. reply 提交

目标实现：

1. `ModEntry`
   - 只监听原版对话打开 / 关闭
2. `PrivateDialogueRouteCoordinator`
   - 只判断：
     - 当前是否应该继续原版
     - 是否允许进入 AI 第二段
3. `HostDialogueSnapshotBuilder`
   - 负责输出：
     - `HostDialogueRecordRef`
     - `SceneSnapshotRef`
     - `RelationSnapshotRef`
     - `RecentPrivateHistoryRef`
     - `HostSummaryEnvelope`
4. `StardewRuntimeTransportClient`
   - 只发 request
5. `AiDialogueSurfaceProjector`
   - 只把 envelope 翻成 `AiDialogueMenu`

固定死规则：

1. 原版对话真实结束前，不得开 AI 私聊。
2. reply 历史追加必须走单独 writer，不得继续散在 route controller。
3. finalize 失败时，surface 只显示失败，不得冒充 committed。

### 6.2 NPC 信息面板与 tab

参考锚点：

1. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/06_关系_记忆与摘要机制.md`
2. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/02_群聊功能.md`
3. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/03_联系人群与固定群聊.md`
4. `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/UI/SquadMemberMenu.cs`

当前保留：

1. `NpcInfoPanelMenu`
2. `MemoryTabView`
3. `RelationTabView`
4. `ThoughtTabView`
5. `ItemTabView`
6. `ChatTabView`
7. `GroupHistoryTabView`

当前问题：

1. 自然链打开面板时，没有显式收口 `MarkAuthorityDataReady`。
2. 只拉了 `thought / memory / item / chat`，没拉 `group history`。
3. 顶部资料和 relation 现在还是临时文案，不是正式分组模型。

目标实现：

1. `NpcInfoPanelSurfaceSession`
   - owner：
     - current npc
     - selected tab
     - active request ids
     - stale handling
2. `NpcPanelSnapshotBuilder`
   - 负责顶部基础资料
3. `NpcInfoPanelProjector`
   - 负责：
     - memory cards
     - relation groups
     - thought surface
     - item tab
     - chat tab
     - group history tab
4. `GroupHistoryTab` 数据必须走独立 loader，不允许继续只靠 build exposure 决定一切

固定死规则：

1. `GroupHistoryTab` 必须区分：
   - 空态
   - 未开放
   - 失败
2. `Thought` 不写回普通聊天历史。
3. `Relation` 必须改成正式生活化分组 view model，不再继续用 `Label:Value` 临时事实列表冒充。

### 6.3 手机私信与联系人入口

参考锚点：

1. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/04_传音与远程通信.md`
2. `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/MobilePhoneApp.cs:44-54`
3. `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/MobilePhoneApp.cs:107-143`
4. `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/PhoneInput.cs:29-126`
5. `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/CallableNPC.cs:8-23`

当前保留：

1. `PhoneDirectMessageMenu`
2. `ChatThreadModels.DirectMessageThreadModel`

当前必须断电的做法：

1. `PhoneDirectMessageMenu` 用 `BuildExposureConfig` 直接给 `AvailabilityState`
2. `ManualTestEntryController.F6` 当正式联系人入口
3. transport 层硬填 `available_now`

目标实现：

1. `PhoneContactBookRouteCoordinator`
   - 按 `MobilePhone` 复刻：
     - 手机 app 入口
     - 通讯录列表
     - 联系人卡片
2. `RemoteDirectThreadSession`
   - owner：
     - `threadKey`
     - open/closed
     - unread
     - failure
3. `RemoteDirectAvailabilityResolver`
   - 按宿主事实判：
     - 睡觉
     - 节日
     - 电话占用
     - 当前不可达
4. `RemoteDirectThreadProjector`
   - 把 envelope 翻成 thread UI

固定死规则：

1. 手机私信不能再靠热键作为正式入口。
2. `availability_now / unavailable_now` 必须按宿主事实算，不得按 build 开关硬填。
3. 同一联系人必须复用同一 `threadKey`。

### 6.4 现场群聊与手机主动群聊

参考锚点：

1. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/02_群聊功能.md`
2. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/03_联系人群与固定群聊.md`
3. `参考项目/Mod参考/CreatureChat/src/client/java/com/owlmaddie/ui/ChatScreen.java:47-149`
4. `参考项目/Mod参考/CreatureChat/src/client/java/com/owlmaddie/network/ClientPackets.java:59-95`

当前保留：

1. `OnsiteGroupChatOverlay`
2. `PhoneActiveGroupChatMenu`
3. `ChatThreadModels.GroupChatThreadModel`
4. `WorldLifecycleHooks`

当前问题：

1. 没有真正的 `groupSession` owner。
2. participant freeze 只是接收传入列表。
3. `UnreadCount` / `DoNotDisturb` 只是内存字段。
4. `contactGroupId` 还没成为真正的 thread owner。

目标实现：

1. `OnsiteGroupSession`
   - owner：
     - group session key
     - participant set
     - active input sequence
2. `PhoneGroupThreadSession`
   - owner：
     - contact group id
     - unread
     - DND
     - bucket state
3. `GroupParticipantSnapshotBuilder`
   - 负责现场 participant 采样与冻结
4. `GroupChatProjector`
   - 负责：
     - onsite overlay
     - phone group thread
     - group history tab mirror

固定死规则：

1. 现场群聊 session 只有玩家发出第一句后才创建。
2. `Warped`、睡觉、切日必须结束现场 session。
3. 手机主动群聊必须以 `contactGroupId` 为 owner，不允许只当一次性窗口。
4. `UnreadCount` / `DoNotDisturb` 不得继续只存在内存字段里。

### 6.5 物品 tab、给物、交易

参考锚点：

1. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/08_交易与给物互动.md`
2. `参考项目/Mod参考/Stardew-GitHub-ChroniclerCherry-ShopTileFramework/ShopTileFramework/ModEntry.cs:223-289`
3. `参考项目/Mod参考/Stardew-GitHub-Mushymato-LivestockBazaar/LivestockBazaar/GUI/BazaarMenu.cs`
4. `参考项目/Mod参考/Stardew-GitHub-Mushymato-LivestockBazaar/LivestockBazaar/GUI/BazaarLivestockEntry.cs`

当前保留：

1. `ItemTextCarrierBase`
2. `MailItemTextCarrier`
3. `RewardItemTextCarrier`
4. `TooltipItemTextCarrier`
5. `ItemCarrierHooks`

当前必须迁走：

1. transactional commit 协调
2. carrier 选择
3. item gift host path 判定

目标实现：

1. `ItemCarrierProjector`
   - 只选：
     - mail
     - reward
     - tooltip
2. `ItemGiftHostExecutor`
   - 只负责：
     - 验证
     - 落物
     - 记权威 receipt
3. `TradeHostExecutor`
   - 只负责：
     - 扣减
     - 交换
     - relation delta / commitment ledger

固定死规则：

1. carrier 成功显示，不等于交易 committed。
2. committed 必须由宿主 executor 真正完成后再确认。
3. `ShopTileFramework` 只抄宿主开店入口，不抄 AI 交易语义。
4. `LivestockBazaar` 只抄宿主购买与实例化路径，不抄其玩法本身。

### 6.6 自定义物品生成

参考锚点：

1. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/13_自定义物品生成.md`
2. `参考项目/Mod参考/StardewValleyMods-spacechase0/framework/JsonAssets/Framework/Api.cs`
3. `参考项目/Mod参考/Stardew-GitHub-Floogen-CustomCompanions/CustomCompanions/CustomCompanions.cs`

当前问题：

1. 现在只有“物品语义应该在云端”的结论。
2. 但 Stardew 宿主层：
   - 谁注册模板
   - 谁实例化对象
   - 谁处理特殊实体
   - 谁写回 item event
   没写死。

目标实现：

1. `StardewItemInstantiationExecutor`
   - owner：
     - 物品模板查找
     - Json Assets id 映射
     - 实例创建
     - `item.modData` 写回
2. `StardewEntityInstantiationExecutor`
   - 只负责特殊 companion / scenery / entity
3. `ItemCarrierProjector`
   - 仍然负责 first-perception carrier

固定死规则：

1. `Json Assets` 只抄注册 / id 映射 / 实例化层。
2. `CustomCompanions` 只抄特殊实体 content pack / spawn 层。
3. 没有宿主 creator 的能力，不得写成“已支持”。

### 6.7 主动对话与接触触发

参考锚点：

1. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/06_主动对话与接触触发.md`
2. `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/InteractionManager.cs`

当前问题：

1. 现在只有“第一次原版，第二次 AI”的自然交互链。
2. 还不是完整的：
   - 接触触发
   - 偶遇触发
   - 自动搭话
   - 冷却
   - 去重
   - 失败回原生

目标实现：

1. `PrivateDialogueRouteCoordinator`
   - 只管接触 / 偶遇触发状态机
2. `HostDialogueSnapshotBuilder`
   - 负责接触前后事实
3. `AiDialogueSurfaceProjector`
   - 负责对话显示

固定死规则：

1. 主动触发状态机必须单独成文。
2. 不允许继续把主动触发逻辑散在 `ModEntry + Controller + Menu` 三处。

### 6.8 自动行动骨架与回日程

参考锚点：

1. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/07_行为协议_解析与执行.md`
2. `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/InteractionManager.cs`
3. `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/Tasks/UnifiedTaskManager.cs`
4. `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/RecruitmentManager.cs`
5. `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/Behaviors/NpcInteractionBehavior.cs`
6. `参考项目/Mod参考/Stardew-Nexus-19305-ScheduleViewer/ScheduleViewer/Schedule.cs`

当前问题：

1. 还没有真正的自动行动 executor。
2. 还没有真正的 schedule restore executor。

目标实现：

1. `NpcAutoActionExecutor`
   - 负责：
     - 任务选择输入
     - 目标点
     - 寻路
     - 执行动作
2. `NpcScheduleRestoreExecutor`
   - 负责：
     - 查询当前 schedule entry
     - 恢复当前应在位置
     - restore failed / blocked 结果

固定死规则：

1. 第一阶段直接抄 `TheStardewSquad` 的任务骨架和回日程链。
2. 不允许 AI 直接控制逐帧移动。
3. 自动行动结束后必须走 schedule restore，不得把 NPC 永久留在错误地点。

## 7. 明确禁止继续存在的错误实现

从现在起，下列做法固定视为错误实现：

1. 在 `ModEntry` 里继续长上下文 builder。
2. 在 `NpcNaturalInteractionController` 里继续长：
   - history writer
   - request builder
   - panel data loader
3. 在 `RuntimeClient` 里继续长：
   - projector
   - request builder
   - host executor
4. 用 `ManualTestEntryController` 的 F6/F7/F11 路径冒充正式手机 / 群聊入口。
5. 用 `BuildExposureConfig` 冒充：
   - remote availability 真相
   - group thread 真相
   - group history 真相
6. 只写“参考了 Mobile Phone / CreatureChat / TheStardewSquad”，却不写清：
   - 抄哪层
   - 不抄哪层
   - 我们自己的 executor / creator 在哪

## 8. 当前已补齐的第一批正式 contract

当前已经补齐：

1. `stardew-phone-contact-entry-contract.md`
2. `stardew-remote-direct-thread-contract.md`
3. `stardew-remote-direct-availability-state-machine-contract.md`
4. `stardew-group-chat-session-contract.md`
5. `stardew-onsite-group-overlay-contract.md`
6. `stardew-phone-group-thread-contract.md`
7. `stardew-npc-panel-bundle-contract.md`
8. `stardew-phone-contact-list-contract.md`
9. `stardew-item-instantiation-creator-contract.md`
10. `stardew-item-gift-and-trade-host-executor-contract.md`
11. `stardew-proactive-dialogue-trigger-state-machine-contract.md`
12. `stardew-auto-action-and-schedule-restore-contract.md`

这批 contract 解决的就是当前最容易漂移的 5 类边界：

1. 手机联系人与私信线程边界
2. 群聊 session / thread / overlay 边界
3. 物品 creator 边界
4. 交易 / 给物 executor 边界
5. 主动触发与自动行动 / 回日程边界

固定结论：

1. 现在继续做 Stardew mod 大重构，已经不能再说“contract 还没补”。
2. 后面进入实现时，必须先按这批 contract 拆类和断电旧逻辑。

## 9. 本附件最终结论

`Superpowers.Stardew.Mod` 现在不是“没东西”，而是：

- `壳很多`
- `边界不硬`
- `creator / executor / session owner 没写死`

所以当前最正确的重建方式不是继续在现有类上修修补补，而是：

1. 保留宿主 UI 壳、hook 壳、carrier 壳
2. 把 `NpcNaturalInteractionController` 和 `RuntimeClient` 强制瘦身
3. 新增：
   - `Routes`
   - `HostSnapshots`
   - `SurfaceSessions`
   - `Projectors`
   - `HostWriteback`
4. 对每个功能都明确：
   - 谁是壳
   - 谁是 session owner
   - 谁是 projector
   - 谁是 creator / executor
   - 抄哪个参考 mod 的哪一层

只有这样，后面做第二个游戏时，`mod` 层才不会又重走一遍“先做出东西，再补边界”的老路。
