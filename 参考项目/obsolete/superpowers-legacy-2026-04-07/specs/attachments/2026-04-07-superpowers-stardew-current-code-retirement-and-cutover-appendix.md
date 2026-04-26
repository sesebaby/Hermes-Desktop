# Superpowers Stardew 旧代码退役与切换附表

## 1. 文档定位

这份附件只做一件事：

- 把 `games/stardew-valley/Superpowers.Stardew.Mod` 的当前文件逐个归桶，写死哪些能留壳，哪些必须重写，哪些必须直接退役。

这份附件不再重复：

1. 功能愿景
2. 参考 mod 功能说明
3. 总架构介绍

它只回答最硬的一层：

1. 当前文件还能不能留
2. 留下后还能干什么
3. 哪些方法、字段、接口必须断电
4. 新类应该顶到哪里
5. 后面 plan 和实现时，应该拿什么 grep 词验收

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-feature-reference-implementation-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-rebuild-and-implementation-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-hook-session-projector-executor-appendix.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

## 2. 大白话死规则

1. 这份附件从现在开始，是 `Superpowers.Stardew.Mod` 旧代码处置的唯一正式附表。
2. 后面任何 plan、task、实现说明，只要碰 `Superpowers.Stardew.Mod`，都必须先回链这份表。
3. 后面写 plan 时，除了回链这份表，还必须把真正会改的旧代码行号附上，不能只写文件名。
4. 没进这张表的旧文件，不允许默认当成“应该继续保留”的正式主线。
5. 这张表如果和旧实现冲突，以这张表为准，不以“现在代码刚好还能跑”为准。

## 3. 归桶说明

| 归桶 | 大白话意思 | 处理口径 |
| --- | --- | --- |
| `kept shell` | 这个文件还能留，但只能当壳或小工具 | 不允许再长业务 authority |
| `rebuild around kept shell` | 文件名或大体位置可以留，但内容要大改 | 旧主线要断电，只保留壳和极少数稳定能力 |
| `retired business mainline` | 这份旧实现不再是正式主线 | 后面迁去 `legacy/retired-implementation/`，或直接被新文件替代 |
| `kept authority core` | 这个文件承载的是稳定公共核心 | 可以继续留，但只能守住小而硬的 authority |

## 4. 逐文件附表

### 4.1 入口、配置、Runtime、调试命令

| 当前文件 | 归桶 | 现在越界点 / 现在价值 | 必删 / 必停 | 替代新类 / 新目录 | 迁完判定 | grep 验收词 |
| --- | --- | --- | --- | --- | --- | --- |
| `ModEntry.cs` | `rebuild around kept shell` | 入口类现在同时在做对象装配、自然交互、手动热键、宿主上下文拼装 | 删 `BuildResolvedPrivateDialogueContext`、`HostDialogueNormalizationFallback`、`currentHostDialogueNormalizedRecord`；不再在入口类里直接拼 request/context | `Application/Routes/PrivateDialogueRouteCoordinator.cs`、`HostSnapshots/HostDialogueSnapshotBuilder.cs`、`ModBootstrap` | `ModEntry` 只负责初始化、事件订阅、把事件转交 route/session 层 | 删：`BuildResolvedPrivateDialogueContext|currentHostDialogueNormalizedRecord|HostDialogueNormalizationFallback`；新：`PrivateDialogueRouteCoordinator` |
| `Config/ModConfig.cs` | `rebuild around kept shell` | 还能留作本地配置壳，但现在和 build 露出耦合太紧 | 不允许把 remote availability、group truth、prompt/provider 语义塞进 config | `Config/ModConfig.cs` + `Exposure/BuildExposurePolicy.cs` | 配置只保留 runtime 地址、debug 开关、experiment 露出，不再冒充业务真相 | 删：`ExposeGroupHistoryToPlayer` 被业务主线直接消费；新：`BuildExposurePolicy` |
| `Exposure/BuildExposureConfig.cs` | `rebuild around kept shell` | 现在把 build 露出、玩家开放状态、group history disclosure 混成一件事 | 断电 `GroupHistoryDisclosureState` 冒充真实可用性；不准再让 menu 构造函数直接从这里算 `available_now` | `Exposure/BuildExposurePolicy.cs`、`SurfaceSessions/*`、`RemoteDirectAvailabilityResolver.cs` | 只保留“这个 build 是否允许露出某类实验面”，不再输出线程状态和 availability 真相 | 删：`GroupHistoryDisclosureState|IsSurfaceEnabled\\(\"remote_direct_one_to_one\"\\)` 进入正式主链；新：`RemoteDirectAvailabilityResolver` |
| `Runtime/IRuntimeSurfaceClient.cs` | `retired business mainline` | 这是典型错口径接口，transport 层直接操作 UI menu | 整个接口退役，不再保留 `Populate*MenuAsync` 这一类 UI 耦合方法 | `RuntimeTransport/IStardewRuntimeTransportClient.cs`、`RuntimeTransport/IRuntimeFinalizeClient.cs` | 新接口只收 DTO、回 DTO，不再认识 `AiDialogueMenu`、`NpcInfoPanelMenu` 这些 UI 类型 | 删：`PopulateAiDialogueMenuAsync|PopulatePhoneDirectMessageMenuAsync|PopulateGroupChatMenusAsync`；新：`SendPrivateDialogueAsync|FinalizeDialogueCommitAsync` |
| `Runtime/RuntimeClient.cs` | `rebuild around kept shell` | 现在一个文件里同时做 HTTP、request builder、UI projector、item host executor 协调 | 删 `BuildPrivateDialogueRequest`、`BuildThoughtRequestWithResolvedContext`、`BuildRemoteDirectRequest`、`BuildGroupChatTurnRequest`、`ApplyDialogueEnvelopeToMenu`、`CommitTransactionalItemActionsAsync`、`CreateCarrier`、`ShouldUseItemGiftHostCommitPath`；不再直接写 UI menu | `RuntimeTransport/StardewRuntimeTransportClient.cs`、`Projectors/*`、`HostWriteback/*`、`HostModels/*` | 类里只剩 HTTP、health、finalize、history lookup，不再引用任何 UI menu | 删：`BuildPrivateDialogueRequest|BuildThoughtRequestWithResolvedContext|BuildRemoteDirectRequest|BuildGroupChatTurnRequest|ApplyDialogueEnvelopeToMenu|CommitTransactionalItemActionsAsync|CreateCarrier|ShouldUseItemGiftHostCommitPath`；删 UI 引用：`AiDialogueMenu|NpcInfoPanelMenu|PhoneDirectMessageMenu|OnsiteGroupChatOverlay` |
| `Commands/NpcWarpCommand.cs` | `kept shell` | 纯 debug 小工具，有用，但不能进入正式产品主线 | 不准让传送命令出现在正式玩家路径、launch readiness、支持承诺里 | 继续留在 `Commands/` | 只当开发调试命令，不参与正式功能 | 保留：`NpcWarpCommand`；不得新增：`Runtime`、`Prompt`、`Commit` |
| `Superpowers.Stardew.Mod.csproj` | `kept shell` | 工程壳本身可留 | 不准继续纵容所有层都堆在同一目录 | 继续留，但按新目录补齐引用 | 目录已出现 `Application/Routes`、`HostSnapshots`、`SurfaceSessions`、`RuntimeTransport`、`Projectors`、`HostWriteback` | 新：`Application\\Routes|HostSnapshots|SurfaceSessions|RuntimeTransport|Projectors|HostWriteback` |
| `manifest.json` | `kept shell` | Mod 清单壳可留 | 不准把功能支持范围、实验面、商业说明偷写到 manifest 当 authority | 继续留 | 只保留 SMAPI 清单信息 | 不得新增：`group_chat_supported` 这类业务 authority 字段 |

### 4.2 Hooks 与通用辅助

| 当前文件 | 归桶 | 现在越界点 / 现在价值 | 必删 / 必停 | 替代新类 / 新目录 | 迁完判定 | grep 验收词 |
| --- | --- | --- | --- | --- | --- | --- |
| `Hooks/NpcNaturalInteractionController.cs` | `rebuild around kept shell` | 当前已经不是 controller 壳，而是在做 route、history、request builder、reply、panel loader 总控 | 删 `PopulateAiDialogueAsync`、`SubmitReplyAsync`、`OpenNpcInfoPanelAsync`、`BuildProfileSummary`、`BuildRelationFacts`；不再直接调 `RuntimeClient.Build*` | `Application/Routes/PrivateDialogueRouteCoordinator.cs`、`HostSnapshots/HostDialogueSnapshotBuilder.cs`、`SurfaceSessions/AiDialogueSurfaceSession.cs`、`Projectors/NpcInfoPanelProjector.cs` | 这个类只剩自然触发状态机和打开哪个 surface 的路由 | 删：`PopulateAiDialogueAsync|SubmitReplyAsync|OpenNpcInfoPanelAsync|BuildProfileSummary|BuildRelationFacts|RuntimeClient\\.Build`；新：`PrivateDialogueRouteCoordinator` |
| `Hooks/NpcInteractionHooks.cs` | `kept authority core` | 这是稳定的 semantic hook 壳 | 不准开始持有 session、snapshot、request builder | 继续留在 `Hooks/` | 只负责发 hook 事件 | 不得新增：`Request|Session|Menu` |
| `Hooks/MenuLifecycleHooks.cs` | `rebuild around kept shell` | hook 壳可留，但现在把 `BuildExposureConfig` 的 disclosure 值直接打进 trace，当成真相 | 断电 `EmitGroupHistoryDisclosureResolved` 从 build exposure 直接得出 authority disclosure | `Hooks/MenuLifecycleHooks.cs` + `SurfaceSessions/NpcInfoPanelSurfaceSession.cs` | 只发 render/open/close hook，不再自己判断 disclosure/availability 真相 | 删：`EmitGroupHistoryDisclosureResolved` 的 authority 用法；新：`NpcInfoPanelSurfaceSession` |
| `Hooks/WorldLifecycleHooks.cs` | `rebuild around kept shell` | 现在只有两个布尔值，假装 remote thread 和 group participant truth | 删 `IsRemoteThreadOpen`、`HasFrozenParticipantSet` 作为 authority；不再让这个类替代 session owner | `SurfaceSessions/RemoteDirectThreadSession.cs`、`SurfaceSessions/OnsiteGroupSession.cs`、`SurfaceSessions/PhoneGroupThreadSession.cs` | 只发 world hook，不再保有线程/session 真相 | 删：`IsRemoteThreadOpen|HasFrozenParticipantSet`；新：`RemoteDirectThreadSession|OnsiteGroupSession|PhoneGroupThreadSession` |
| `Hooks/ItemCarrierHooks.cs` | `kept authority core` | 这是 item text carrier 的 trace 核心，可留 | 不准开始做 item creator、trade executor、carrier 选择 | 继续留在 `Hooks/` | 只负责 item hook ref 和 trace | 不得新增：`CreateItem|Trade|Executor` |
| `Hooks/NpcInteractionTargetResolver.cs` | `kept authority core` | 小而硬的目标判定工具，可留 | 不准在这里长业务 rule、trigger cooldown、AI 语义 | 继续留在 `Hooks/` 或移到 `HostSnapshots/` 工具层 | 只做 NPC 点击目标解析 | 不得新增：`Dialogue|Prompt|History` |
| `Hooks/SemanticHookRegistry.cs` | `kept authority core` | 这是 Stardew semantic hook 集合真源，必须留 | 不准塞入业务 payload 语义、prompt 语义 | 继续留在 `Hooks/` | 只管理 hook 白名单与最近事件缓存 | 不得新增：`prompt|persona|provider` |
| `Hooks/StardewActorId.cs` | `kept authority core` | 小而硬的 NPC actor id 归一工具，可留 | 不准把 title-local actor graph、contact group 规则塞进这里 | 继续留在 `Hooks/` 或 `HostModels/` 公共工具层 | 只做 actor id 归一 | 不得新增：`group|thread|memory` |

### 4.3 主 Surface、Presenter、手动调试入口

| 当前文件 | 归桶 | 现在越界点 / 现在价值 | 必删 / 必停 | 替代新类 / 新目录 | 迁完判定 | grep 验收词 |
| --- | --- | --- | --- | --- | --- | --- |
| `UI/AiDialogueMenu.cs` | `kept shell` | 私聊 UI 壳基本能留 | 不准开始自己 build request 或直接调 runtime；reply submitter 只接 session 回调 | `SurfaceSessions/AiDialogueSurfaceSession.cs`、`Projectors/AiDialogueSurfaceProjector.cs` | 只保留 surface state、玩家输入、显示逻辑 | 不得新增：`RuntimeClient\\.Build|HttpClient|Prompt` |
| `UI/NpcInfoPanelMenu.cs` | `rebuild around kept shell` | 面板壳可留，但 `MarkAuthorityDataReady`、tab 数据 ready 时机还不硬 | 不准再让 controller 直接往里塞 profile/relation/thought/chat/item；group history 不再靠 build disclosure 单独决定 | `SurfaceSessions/NpcInfoPanelSurfaceSession.cs`、`Projectors/NpcInfoPanelProjector.cs` | `NpcInfoPanelSurfaceSession` 成为唯一 session owner；panel 只吃 projector 结果 | 删：`MarkAuthorityDataReady` 被外部散调；新：`NpcInfoPanelSurfaceSession` |
| `UI/PhoneDirectMessageMenu.cs` | `rebuild around kept shell` | 当前构造函数直接用 build 开关算 `available_now` | 删构造时用 `BuildExposureConfig.IsSurfaceEnabled(\"remote_direct_one_to_one\")` 决定 availability | `SurfaceSessions/RemoteDirectThreadSession.cs`、`Projectors/RemoteDirectThreadProjector.cs` | menu 只展示 thread shell，不再自己产 availability | 删：`AvailabilityState = exposureConfig`；新：`RemoteDirectThreadSession` |
| `UI/PhoneActiveGroupChatMenu.cs` | `rebuild around kept shell` | 当前 `contactGroupId`、`UnreadCount`、`DoNotDisturb` 只是 UI 里一层状态壳 | 不准继续把 unread/dnd 真相只存在 menu 里 | `SurfaceSessions/PhoneGroupThreadSession.cs`、`Projectors/GroupChatProjector.cs` | contact group 线程 owner 迁到 session；menu 只做显示 | 删：`UnreadCount|DoNotDisturb` 作为 authority；新：`PhoneGroupThreadSession` |
| `UI/OnsiteGroupChatOverlay.cs` | `rebuild around kept shell` | overlay 壳可留，但 participant freeze 现在只是一层 UI 缓存 | 不准继续让 overlay 持有现场 participant authority | `SurfaceSessions/OnsiteGroupSession.cs`、`Projectors/GroupChatProjector.cs` | 现场 session 和 participant freeze 迁走；overlay 只渲染 | 删：`HasFrozenParticipants|ParticipantIds` 作为 authority；新：`OnsiteGroupSession` |
| `UI/StardewMenuPresenter.cs` | `rebuild around kept shell` | 当前既是 presenter，又替手机/群聊做一堆 shell UI，占了不少调试职责 | 不准继续让 phone/group 正式产品 UI 长在这个 debug presenter 壳里 | `UI/StardewMenuPresenter.cs` 只留 presenter，正式 surface 逻辑迁给各自 session/projector | presenter 只负责“显示哪个菜单”，不再承担业务壳描述 | 删：`Superpowers Phone DM|Superpowers Phone Group|Superpowers Onsite Group Overlay` 这类调试壳承担正式入口 |
| `UI/ManualTestEntryController.cs` | `kept shell` | 调试壳可以留，但现在 F6/F7/F11 很容易被当成正式入口 | 断电 `F6`、`F7`、`F11` 冒充正式手机/群聊入口；不再直接调 `RuntimeClient.Build*` | `Debug/ManualEvidenceEntryController.cs` 或保留原名但显式 debug-only | 只给 debug/manual evidence 用，和正式入口完全隔离 | 删：`BuildRemoteDirectRequest|BuildGroupChatTurnRequest|BuildPrivateDialogueRequest|BuildThoughtRequestWithResolvedContext`；保留：`manual` |
| `UI/MenuIntegratedSurfaceBase.cs` | `kept shell` | 稳定 surface 基类，可留 | 不准加业务语义字段 | 继续留在 `UI/` | 只负责 open/render/close 计数和壳状态 | 不得新增：`Request|Availability|Provider` |
| `UI/IMenuIntegratedSurface.cs` | `kept shell` | 稳定 UI 壳接口，可留 | 不准加 runtime/host writeback 方法 | 继续留在 `UI/` | 只暴露表面状态 | 不得新增：`Submit|Finalize|Commit` |
| `UI/IManualTestMenuPresenter.cs` | `kept shell` | debug presenter 接口可留 | 不准把正式 launcher/phone 产品入口塞进这个接口 | 继续留在 `UI/` 或 `Debug/` | 只服务调试入口 | 不得新增：`Launcher|Catalog|Entitlement` |

### 4.4 Surface model、聊天模型、Tab、carrier、文案辅助

| 当前文件 | 归桶 | 现在越界点 / 现在价值 | 必删 / 必停 | 替代新类 / 新目录 | 迁完判定 | grep 验收词 |
| --- | --- | --- | --- | --- | --- | --- |
| `UI/SurfaceShellState.cs` | `kept authority core` | 通用 surface 壳状态，小而硬 | 不准混入业务 capability 状态 | 继续留 | 只做 UI 壳状态枚举 | 不得新增：`available_now|committed` 这类业务语义枚举 |
| `UI/SurfaceStateModels.cs` | `rebuild around kept shell` | 里面同时放 UI surface model 和一些 runtime 细节字段，边界偏混 | 不准继续长 runtime envelope 原样字段；需要拆 `HostModels/Responses` 和纯 UI view model | `HostModels/Responses/*`、`UI/ViewModels/*` | UI 文件里只保留 projector 后的 surface model | 删：`JsonElement Sidecar` 直接进入 UI surface model；新：`HostModels/Responses` |
| `UI/Chat/ChatThreadModels.cs` | `rebuild around kept shell` | 当前把 availability、hidden_by_build、unread、dnd、thread shell 混在一个文件 | 不准继续用 build 开关直接生成 thread availability；unread/dnd owner 迁走 | `SurfaceSessions/RemoteDirectThreadSession.cs`、`SurfaceSessions/PhoneGroupThreadSession.cs`、`UI/Chat/*ViewModel.cs` | 模型只保留 display state，不再持有业务真相 | 删：`HiddenByBuild|ChatAvailabilityKind|UnreadCount` 的 authority 用法；新：`RemoteDirectThreadSession|PhoneGroupThreadSession` |
| `UI/NpcInfoPanelCopyFormatter.cs` | `kept shell` | 文案整理工具可留 | 不准根据 raw memory 文本自己推业务结论，只能格式化 projector 结果 | 继续留 | 只做 copy 格式化 | 不得新增：`Http|Runtime|Prompt` |
| `UI/Tabs/TabViewBase.cs` | `kept shell` | tab 基类可留 | 不准长业务 authority | 继续留 | 只做 tab 空态/失败态壳 | 不得新增：`Availability|Session` |
| `UI/Tabs/MemoryTabView.cs` | `kept shell` | 纯 memory tab 壳，可留 | 不准自己取 memory | `Projectors/NpcInfoPanelProjector.cs` | 只接收 memory cards | 不得新增：`Runtime|Http` |
| `UI/Tabs/RelationTabView.cs` | `kept shell` | 纯 relation tab 壳，可留 | 不准继续接受 `Label:Value` 临时事实列表作为最终模型 | `Projectors/NpcInfoPanelProjector.cs` + 正式 relation group view model | relation 已变成生活化分组 view model | 删：把最终结构长期停在 `NpcRelationFact` 一维列表 |
| `UI/Tabs/ThoughtTabView.cs` | `kept shell` | 纯 thought tab 壳，可留 | 不准自己决定 request 状态，不准把 thought 混进普通聊天历史 | `Projectors/NpcInfoPanelProjector.cs` | 只展示 thought layers | 不得新增：`ChatHistory` |
| `UI/Tabs/ItemTabView.cs` | `kept shell` | item tab 壳可留 | 不准自己决定 committed，不准自己实例化宿主 item | `Projectors/NpcInfoPanelProjector.cs`、`HostWriteback/StardewItemInstantiationExecutor.cs` | 只展示 item records 和详情 | 不得新增：`CreateItem|Commit` |
| `UI/Tabs/ChatTabView.cs` | `kept shell` | chat history 壳可留 | 不准自己决定 replay truth | `Projectors/NpcInfoPanelProjector.cs` | 只展示 day groups | 不得新增：`Finalize|Commit` |
| `UI/Tabs/GroupHistoryTabView.cs` | `rebuild around kept shell` | 当前 disclosure state 直接吃 build exposure config | 断电 `DisclosureState = exposureConfig.GroupHistoryDisclosureState` 作为 authority | `SurfaceSessions/NpcInfoPanelSurfaceSession.cs`、`Projectors/NpcInfoPanelProjector.cs` | group history 的开放/空态/失败来自 session+contract，不来自 build config | 删：`DisclosureState = exposureConfig.GroupHistoryDisclosureState` |
| `UI/Carriers/ItemTextCarrierBase.cs` | `kept shell` | carrier 壳和值得保留的 hook proof 在这里 | 不准让 carrier 选择和 host apply 决策继续长在 base 类 | `Projectors/ItemCarrierProjector.cs`、`HostWriteback/ItemGiftHostExecutor.cs` | base 类只负责 render proof | 不得新增：`CreateCarrier|Trade|Apply` |
| `UI/Carriers/MailItemTextCarrier.cs` | `kept shell` | mail carrier 壳可留 | 不准自己承诺 committed | `Projectors/ItemCarrierProjector.cs` | 只作为 carrier 实现 | 不得新增：`Commit` |
| `UI/Carriers/RewardItemTextCarrier.cs` | `kept shell` | reward carrier 壳可留 | 不准自己承诺 committed | `Projectors/ItemCarrierProjector.cs` | 只作为 carrier 实现 | 不得新增：`Commit` |
| `UI/Carriers/TooltipItemTextCarrier.cs` | `kept shell` | tooltip carrier 壳可留 | 不准自己承诺 committed | `Projectors/ItemCarrierProjector.cs` | 只作为 carrier 实现 | 不得新增：`Commit` |

## 5. 这一轮最先要断电的文件

按优先级，后面 plan 必须先处理这 8 个文件：

1. `games/stardew-valley/Superpowers.Stardew.Mod/ModEntry.cs`
2. `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/NpcNaturalInteractionController.cs`
3. `games/stardew-valley/Superpowers.Stardew.Mod/Runtime/RuntimeClient.cs`
4. `games/stardew-valley/Superpowers.Stardew.Mod/Runtime/IRuntimeSurfaceClient.cs`
5. `games/stardew-valley/Superpowers.Stardew.Mod/UI/ManualTestEntryController.cs`
6. `games/stardew-valley/Superpowers.Stardew.Mod/Exposure/BuildExposureConfig.cs`
7. `games/stardew-valley/Superpowers.Stardew.Mod/UI/PhoneDirectMessageMenu.cs`
8. `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/GroupHistoryTabView.cs`

理由很简单：

1. 这 8 个文件最容易继续把旧主线偷偷带回来。
2. 只要这 8 个文件没断电，后面再补 session、projector、executor，也会被旧 builder/UI 直连口径重新污染。

## 6. 最终结论

这份表的意思不是“当前代码一无是处”，而是把它说清楚：

1. 哪些只是壳，能留。
2. 哪些是被污染的旧业务主线，必须断电。
3. 哪些是小而硬的核心，可以继续当 authority core。

从现在开始，`Superpowers.Stardew.Mod` 的旧代码处置不再允许散在别的附件里。  
要看旧代码怎么退、怎么切、怎么验收，就只认这份附表。
