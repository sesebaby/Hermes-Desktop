# Superpowers Stardew 功能对照参考 Mod 的落地附件

## 1. 文档定位

本文专门回答 4 件事：

1. 当前正式设计里，`Stardew mod` 到底要承接哪些功能。
2. 这些功能在已经下载的参考 mod 里，哪些有成熟宿主壳可抄。
3. 每个功能在 `Superpowers.Stardew.Mod` 里到底怎么落。
4. 哪些地方必须写死，不能给 AI 自由发挥。

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-host-integration-map-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-core-dialogue-memory-social-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-group-propagation-expansion-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-hook-session-projector-executor-appendix.md`

## 2. 固定死规则

### 2.1 参考真相怎么分

固定分成两层，不允许混：

1. `GGBH_OpenAIWorld` 负责给出玩法语义真相。
   - 用它回答“这个功能本质上是什么”“消息怎么存”“动作怎么回写”“哪些结果要进记忆”。
2. `Stardew 参考 mod` 负责给出宿主壳真相。
   - 用它回答“在 Stardew 里怎么开 UI”“怎么拿联系人列表”“怎么读日程”“怎么开商店”“怎么生成物品实例”“怎么驱动跟随和任务壳”。

死规则：

1. 不允许把 `Stardew 参考 mod` 误写成 AI 玩法 authority。
2. 不允许把 `GGBH_OpenAIWorld` 的 prompt 语义偷偷翻成本地硬编码。
3. 不允许为了“更通用”改写参考主链。

### 2.2 7 段主链怎么固定

以后写 Stardew 方案，必须按这 7 段说清：

1. `Trigger`
2. `Snapshot`
3. `Prompt`
4. `Parse / Normalize`
5. `Projector`
6. `Writeback`
7. `Player-visible Surface`

固定 owner：

1. `Trigger`：`Game Mod`
2. `Snapshot`：`Game Mod + Runtime.Stardew Adapter`
3. `Prompt`：`Cloud`
4. `Parse / Normalize`：`Runtime.Local`
5. `Projector`：`Runtime.Stardew Adapter`
6. `Writeback`：`Game Mod`
7. `Player-visible Surface`：`Game Mod`

### 2.3 本地和云端的硬边界

固定规则：

1. `Cloud` 持有 `stardew-valley` 自己的 prompt 资产明文。
2. `Cloud` 持有聊天正本、记忆正本、审计明文正本。
3. `Runtime.Local` 只收结构化事实包，不拼最终 prompt。
4. `Superpowers.Stardew.Mod` 只做宿主取数、宿主 UI、最终宿主写回。
5. `Superpowers.Stardew.Mod` 不允许本地持有 prompt 资产目录，不允许本地直连 provider。

### 2.4 参考 mod 使用方式

固定结论：

1. `Mobile Phone`
   - 主抄：手机入口、联系人列表、线程壳、远程可用性最基础宿主判断。
   - 不抄：它自己的本地对话内容分支。
2. `TheStardewSquad`
   - 主抄：NPC 管理菜单、共享物品栏壳、任务/自动行动骨架、回到日程。
   - 不抄：招募玩法本身。
3. `ScheduleViewer`
   - 主抄：日程读取、当前位置、今日送礼状态、列表页/详情页展示方式。
   - 不抄：它自己的菜单体系作为最终产品标准。
4. `ShopTileFramework`
   - 主抄：tile property 打开商店、开 vanilla/custom shop 的宿主入口。
   - 不抄：AI 交易决策。
5. `LivestockBazaar`
   - 主抄：复杂商店菜单壳、购买后真实创建宿主对象。
   - 不抄：把它包装成 NPC 自主交易主链。
6. `Json Assets`
   - 主抄：把自定义物品挂进商店、物品模板资产接到宿主系统的办法。
   - 不抄：把它当 AI 生成物品语义 authority。
7. `CustomCompanions`
   - 主抄：content pack 定义格式、地图点位生成、宿主 companion 实体壳。
   - 不抄：拿 companion 系统冒充完整 NPC 社交系统。
8. `CreatureChat`
   - 主抄：聊天 UI 壳、前台消息队列、客户端收发壳。
   - 不抄：本地 prompt、本地 parser、本地 AI 主链。

## 3. 功能总表

| 功能 | GGBH 语义锚点 | Stardew 主参考 | `Superpowers.Stardew.Mod` 主落点 | 当前结论 |
| --- | --- | --- | --- | --- |
| AI 私聊对话框 | `20_玩法功能/01_私聊功能.md`、`10_共用系统/05_消息模型_通信通道与消息持久化.md` | 无成熟 AI 对话壳可直接照搬，按现有宿主壳实现 | `UI/AiDialogueMenu.cs`、`Hooks/NpcInteractionHooks.cs` | 语义抄 `GGBH`，宿主壳走现有代码 |
| 动态选项 | `20_玩法功能/01_私聊功能.md`、`90_附录/01_已解密Prompt总表.md` | 无现成 Stardew AI 选项壳 | `UI/AiDialogueMenu.cs` | 选项由 Cloud 生成，本地只渲染 |
| NPC 信息面板 | `game-integration-profile.md` 第 1.4.4 / 1.5 节 | `TheStardewSquad/SquadMemberMenu.cs` | `UI/NpcInfoPanelMenu.cs` | 面板结构抄壳，内容语义按正式设计 |
| 记忆 Tab | `10_共用系统/06_关系_记忆与摘要机制.md` | 无直接对口；只借现有面板结构 | `UI/NpcInfoPanelMenu.cs` | Cloud 记忆正本，本地只展示摘要 |
| 关系 Tab | `10_共用系统/06_关系_记忆与摘要机制.md` | 无直接对口；只借现有面板结构 | `UI/NpcInfoPanelMenu.cs` | 只做展示，不做快捷互动 |
| 当前想法 Tab | `thought` 口径回链 `private_dialogue + inner_monologue` | 无直接对口；只借现有面板结构 | `UI/NpcInfoPanelMenu.cs` | 单独 surface，不进普通聊天历史 |
| 物品 Tab | `20_玩法功能/08_交易与给物互动.md`、`20_玩法功能/13_自定义物品生成.md` | `TheStardewSquad/SquadInventoryMenu.cs`、`JsonAssets` | `UI/NpcInfoPanelMenu.cs` | 先展示，不做快捷互动 |
| 聊天 Tab | `20_玩法功能/01_私聊功能.md`、`10_共用系统/05_消息模型_通信通道与消息持久化.md` | 无直接对口；按现有面板 tab 壳实现 | `UI/NpcInfoPanelMenu.cs` | 只回看 actor-owned direct history |
| 群聊历史 Tab | `20_玩法功能/02_群聊功能.md`、`20_玩法功能/03_联系人群与固定群聊.md` | 无直接对口；按现有 tab 壳实现 | `UI/Tabs/GroupHistoryTabView.cs` | 必须区分空态、未开放、失败 |
| 手机私信 | `20_玩法功能/04_传音与远程通信.md`、`20_玩法功能/01_私聊功能.md` | `Mobile Phone` | `UI/PhoneDirectMessageMenu.cs` | UI 壳抄 `Mobile Phone`，语义不抄 |
| 联系人入口 | `game-integration-profile.md` 第 1.4.4 节 | `Mobile Phone` | `UI/PhoneDirectMessageMenu.cs`、`UI/NpcInfoPanelMenu.cs` | 联系人列表直接按 `Mobile Phone` 复刻 |
| 现场群聊 | `20_玩法功能/02_群聊功能.md` | 无成熟 Stardew AI 群聊主链；只借 `CreatureChat` 前台壳思路 | `UI/OnsiteGroupChatOverlay.cs` | 现场 UI 自建，顺序与持久化抄 `GGBH` |
| 手机主动群聊 | `20_玩法功能/03_联系人群与固定群聊.md` | 无成熟 Stardew 成熟件；只借 `Mobile Phone` 外壳 | `UI/PhoneActiveGroupChatMenu.cs` | 线程壳走手机，群语义走 `GGBH` |
| 交易 / 给物 / 承诺 | `20_玩法功能/08_交易与给物互动.md`、`10_共用系统/07_行为协议_解析与执行.md` | `ShopTileFramework`、`LivestockBazaar` | `UI/Carriers/ItemTextCarrierBase.cs`、`Hooks/ItemCarrierHooks.cs` | 宿主入口可抄，AI 决策不抄 |
| 物品文本 carrier | `20_玩法功能/08_交易与给物互动.md` | 无完全对口；按现有 carrier 壳实现 | `UI/Carriers/ItemTextCarrierBase.cs` | 先显示文本，再走真实落地 |
| 自定义物品生成 | `20_玩法功能/13_自定义物品生成.md` | `Json Assets`、`CustomCompanions`、`LivestockBazaar` | `Hooks/ItemCarrierHooks.cs`、title-local item creator | 生成语义在 Cloud，实例落地在 Mod |
| 日程读取 | Stardew title-local 宿主能力 | `ScheduleViewer` | `Hooks/WorldLifecycleHooks.cs`、`RuntimeClient.cs` | 直接按 `ScheduleViewer` 读宿主事实 |
| 日程展示 | `game-integration-profile.md` 第 1.5 节 | `ScheduleViewer` | `UI/NpcInfoPanelMenu.cs` | 用于地点/状态/下步去向展示 |
| 主动对话 / 接触触发 | `20_玩法功能/06_主动对话与接触触发.md` | `TheStardewSquad/InteractionManager.cs`、`NpcInteractionBehavior.cs` | `Hooks/NpcInteractionHooks.cs` | 触发壳可抄，内容主链走 Cloud |
| 自动行动骨架 | Stardew title-local 宿主能力 + `GGBH` 行为协议 | `TheStardewSquad/UnifiedTaskManager.cs` | `Hooks/WorldLifecycleHooks.cs` | 任务框架可抄，AI 决策协议不抄 |
| 回到当前日程 | Stardew title-local 宿主能力 | `RecruitmentManager.cs`、`NpcInteractionBehavior.cs` | `Hooks/WorldLifecycleHooks.cs` | 直接按参考 mod 落宿主恢复 |
| 信息传播 | `20_玩法功能/05_信息裂变与社会传播.md` | 无 Stardew 成熟件 | future `Hooks/WorldLifecycleHooks.cs` | 先定接口，不假装已有宿主实现 |
| 世界事件 / 自定义状态 / NPC 创建 | `20_玩法功能/07/14/15` | 无 Stardew 成熟件 | future title-local creators | 先定接口，不假装已有宿主实现 |

## 4. 分功能落地

### 4.1 AI 私聊对话框与动态选项

设计语义锚点：

- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/01_私聊功能.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/05_消息模型_通信通道与消息持久化.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/07_行为协议_解析与执行.md`

Stardew 主参考：

- 宿主触发和菜单打开用现有 `Superpowers.Stardew.Mod` 壳。
- 没有一个已下载 Stardew mod 可以直接提供“成熟 AI 私聊主链”。

7 段主链固定如下：

1. `Trigger`
   - `NpcInteractionHooks.cs` 判断当前是不是该进入 AI 私聊。
   - 前置条件必须先让原版 / SVE / 扩展对话让路规则生效。
2. `Snapshot`
   - `Game Mod` 采当前 NPC、地点、天气、宿主原对话记录、近期私聊历史。
   - `Runtime.Stardew Adapter` 冻成 `privateDialogueRequest`。
3. `Prompt`
   - `Cloud` 选择 `stardew-valley` 自己的私聊 prompt 资产。
   - `Cloud` 自己拼最终 prompt。
4. `Parse / Normalize`
   - `Runtime.Local` 检查文本、`choices[]`、`actions[]`。
   - 不合法就挡，不允许 UI 临时补假选项。
5. `Projector`
   - `Runtime.Stardew Adapter` 把文本和选项翻成 `AiDialogueMenu` 需要的 UI 模型。
6. `Writeback`
   - 玩家看见对话后，由 `Game Mod` 写回宿主显示结果。
   - 只有 `Runtime.Local` 发 `CommitPromotionAck` 后，Cloud 才把聊天升正式。
7. `Player-visible Surface`
   - `UI/AiDialogueMenu.cs`

Stardew 落地死步骤：

1. 原版对话显示完并记宿主记录。
2. 只有宿主原对话耗尽，才允许转 AI 私聊。
3. `Game Mod` 发送结构化事实包，不发送本地拼好的 prompt。
4. `Cloud` 返回 `text + choices + actions`。
5. `Runtime.Local` 校验后才允许打开 `AiDialogueMenu`。
6. 对话真实显示成功，才算这轮 committed。

绝对不允许 AI 自由发挥的点：

1. 不允许 `Mod` 本地根据 NPC 名字、关系等硬拼 prompt。
2. 不允许 `Mod` 本地补“差不多的选项”。
3. 不允许 `Cloud` 一返回结果就记正式历史。

### 4.2 NPC 信息面板、记忆 Tab、关系 Tab、当前想法 Tab、聊天 Tab、群聊历史 Tab

设计语义锚点：

- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md` 第 `1.4.4`、`1.5` 节
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/06_关系_记忆与摘要机制.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/01_私聊功能.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/02_群聊功能.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/03_联系人群与固定群聊.md`

Stardew 主参考：

- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/UI/SquadMemberMenu.cs:48-205`
- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/CallableNPC.cs:8-19`

推荐做法和理由：

1. 推荐用 `TheStardewSquad` 抄面板骨架。
   - 理由：它已经把头像、标题、按钮区、关闭逻辑做成了成熟宿主壳。
2. 推荐用 `Mobile Phone` 抄联系人卡片和联系人入口。
   - 理由：它已经把“联系人列表 -> 选择一个 NPC -> 打开二级界面”这条路跑通了。

7 段主链固定如下：

1. `Trigger`
   - 从 `AiDialogueMenu` 的“查看信息”按钮进入。
   - 也允许从手机联系人列表进入。
2. `Snapshot`
   - `Game Mod` 采基础身份、当前位置、关系摘要、最近私聊、最近群聊、物品关联、记忆摘要引用。
3. `Prompt`
   - 只有 `当前想法` 需要单独走 Cloud。
   - `记忆 / 关系 / 聊天 / 群聊历史 / 物品` 优先吃已有 committed 数据，不重复请求 AI。
4. `Parse / Normalize`
   - `Runtime.Local` 只做 thought 结果、记忆摘要、聊天记录分组的统一归一。
5. `Projector`
   - `Runtime.Stardew Adapter` 把数据翻成 tab view model。
6. `Writeback`
   - 信息面板是展示面，默认不直接改宿主。
   - `当前想法` 也不写进普通聊天历史。
7. `Player-visible Surface`
   - `UI/NpcInfoPanelMenu.cs`
   - `UI/Tabs/GroupHistoryTabView.cs`

每个 tab 必须写死的规则：

1. `记忆 Tab`
   - 只显示摘要卡片，不显示原始流水。
2. `关系 Tab`
   - 只显示生活化分组，不做图谱，不做快捷互动。
3. `当前想法 Tab`
   - 只显示当前一段 thought。
   - 切 NPC 时旧结果必须作废，不能串到新 NPC 身上。
4. `聊天 Tab`
   - 只读 actor-owned direct history。
   - 按天分组，可展开，不拉高整窗。
5. `群聊历史 Tab`
   - 必须严格区分 `空态`、`未开放`、`失败`。
   - 不能把“现在没记录”伪装成“群聊还没做”。

绝对不允许 AI 自由发挥的点：

1. 不允许把 `当前想法` 回填到普通聊天历史。
2. 不允许 `记忆 Tab` 直接展示原始全量聊天。
3. 不允许 UI 层自己编一套 `groupHistoryDisclosureState`。

### 4.3 手机私信与联系人入口

设计语义锚点：

- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/04_传音与远程通信.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/01_私聊功能.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/05_消息模型_通信通道与消息持久化.md`

Stardew 主参考：

- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/MobilePhoneApp.cs:44-55`
- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/MobilePhoneApp.cs:86-135`
- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/PhoneInput.cs:29-70`
- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/CallableNPC.cs:8-19`

7 段主链固定如下：

1. `Trigger`
   - `PhoneInput.cs` 那种“手机打开 / 点 app / 进联系人”的输入路由。
2. `Snapshot`
   - 联系人列表由 `Game Mod` 根据当前宿主事实生成。
   - 线程 key 固定为 `gameId + actorId + targetId + channelType`。
3. `Prompt`
   - 仍走 Cloud 的 direct/private router。
4. `Parse / Normalize`
   - `Runtime.Local` 统一检查 `available_now / unavailable_now`。
5. `Projector`
   - `Runtime.Stardew Adapter` 翻成手机线程 UI 模型。
6. `Writeback`
   - 远程 accepted turn 仍写回同一 actor-owned direct history。
7. `Player-visible Surface`
   - `UI/PhoneDirectMessageMenu.cs`

Stardew 落地死步骤：

1. 按 `Mobile Phone` 复刻联系人列表壳。
2. 只在点击联系人后创建或恢复同一线程 key。
3. `DayStarted` 只重算 availability，不自动补发旧消息。
4. 若 `unavailable_now`，只显示当前线程失败文案，不建待投递队列。

绝对不允许 AI 自由发挥的点：

1. 不允许本地自己判“这个 NPC 现在应该说什么”。
2. 不允许把手机私信另做一套新的聊天正本。
3. 不允许“这次发不出去，先悄悄排队以后再说”。

### 4.4 现场群聊、手机主动群聊、固定联系人群

设计语义锚点：

- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/02_群聊功能.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/03_联系人群与固定群聊.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/05_消息模型_通信通道与消息持久化.md`

Stardew 主参考：

- 前台壳思路可借 `参考项目/Mod参考/CreatureChat/src/client/java/com/owlmaddie/ui/ChatScreen.java:47-178`
- 前台收发可借 `参考项目/Mod参考/CreatureChat/src/client/java/com/owlmaddie/network/ClientPackets.java:59-152`
- 手机外壳和联系人入口可借 `Mobile Phone`

推荐做法和理由：

1. 推荐把 `现场群聊` 和 `手机主动群聊` 视为同一群聊核心的两个 carrier。
   - 理由：这样群顺序、群持久化、群镜像回私聊只做一份。
2. 推荐只借 `CreatureChat` 的聊天窗前台壳，不借它的本地 AI 主链。
   - 理由：它在 UI 这层成熟，但和你现在的云端 prompt 架构冲突。

7 段主链固定如下：

1. `Trigger`
   - 现场：玩家主动发第一句时创建 `groupSession`。
   - 远程：玩家从手机进入固定联系人群线程。
2. `Snapshot`
   - 现场 participant set 由 `Game Mod` 采样后冻结。
   - 远程群线程 key 固定为 `gameId + contactGroupId`。
3. `Prompt`
   - `Cloud` 做 speaker 选择、顺序和逐人发言生成。
4. `Parse / Normalize`
   - `Runtime.Local` 做 participant 校验、旧回包拦截、顺序保护。
5. `Projector`
   - `Runtime.Stardew Adapter` 翻现场气泡模型或手机消息模型。
6. `Writeback`
   - committed group turn 先写群历史，再按来源标记镜像入个人判断链。
7. `Player-visible Surface`
   - `UI/OnsiteGroupChatOverlay.cs`
   - `UI/PhoneActiveGroupChatMenu.cs`
   - `UI/Tabs/GroupHistoryTabView.cs`

Stardew 落地死步骤：

1. 现场 participant set 按当前 location、当前可见、当前可交互、距离 `<= 8 tiles` 采样。
2. 当前轮冻结，加入/离开只影响下一轮。
3. 现场玩家没主动发第一句前，不创建正式群 session。
4. 手机群聊统一复用 `contactGroupId` 线程，不丢 unread。

绝对不允许 AI 自由发挥的点：

1. 不允许 UI 自己临时决定发言顺序。
2. 不允许群聊 turn 不落持久化就先冒充 committed。
3. 不允许把群消息悄悄写成普通私聊，没有来源标记。

### 4.5 物品 Tab 与 NPC 物品栏

设计语义锚点：

- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/08_交易与给物互动.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/13_自定义物品生成.md`

Stardew 主参考：

- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/UI/SquadInventoryMenu.cs:10-65`

推荐做法和理由：

1. 推荐把 `物品 Tab` 和“内部 NPC 物品关联记录”分开。
   - 理由：玩家面板只负责看，不要直接把真实容器编辑能力暴露给 UI。
2. 推荐宿主容器层先复刻 `GlobalInventoryId` 风格共享库存壳。
   - 理由：这条是 Stardew 宿主里最稳的库存壳参考。

7 段主链固定如下：

1. `Trigger`
   - 玩家打开信息面板并切到 `物品 Tab`。
2. `Snapshot`
   - `Game Mod` 读取当前 NPC 关联物品事件、真实实例、借出借入关系。
3. `Prompt`
   - 默认不重新走 AI，只展示已存在数据。
4. `Parse / Normalize`
   - `Runtime.Local` 统一物品关联类型。
5. `Projector`
   - `Runtime.Stardew Adapter` 翻成格子 + 详情区 view model。
6. `Writeback`
   - 当前阶段只展示，不做玩家直接拖拽物品修改。
7. `Player-visible Surface`
   - `UI/NpcInfoPanelMenu.cs`

绝对不允许 AI 自由发挥的点：

1. 不允许 `物品 Tab` 直接当编辑面板。
2. 不允许为了省事，把“提到过的物品”和“真实存在的实例”混成一类。
3. 不允许 UI 自己生成一条从没发生过的物品关系。

### 4.6 交易、给物、承诺、关系最小变化

设计语义锚点：

- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/08_交易与给物互动.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/07_行为协议_解析与执行.md`

Stardew 主参考：

- `参考项目/Mod参考/Stardew-GitHub-ChroniclerCherry-ShopTileFramework/ShopTileFramework/ModEntry.cs:193-289`
- `参考项目/Mod参考/Stardew-GitHub-Mushymato-LivestockBazaar/LivestockBazaar/GUI/BazaarMenu.cs:99-163`
- `参考项目/Mod参考/Stardew-GitHub-Mushymato-LivestockBazaar/LivestockBazaar/GUI/BazaarLivestockEntry.cs:467-490`

7 段主链固定如下：

1. `Trigger`
   - 私聊输出交易/给物/承诺动作，或玩家主动发起宿主交互。
2. `Snapshot`
   - `Game Mod` 提供双方物品、价格、关系、地点、可交易宿主通道。
3. `Prompt`
   - `Cloud` 给结构化 `Agree / GiveItem / LendItem / Transaction / relationship delta` 候选。
4. `Parse / Normalize`
   - `Runtime.Local` 做动作白名单、参数合法性、经济 gate。
5. `Projector`
   - `Runtime.Stardew Adapter` 决定开哪种宿主入口：
     - 商店 UI
     - 直接发物
     - 关系变化
     - 承诺记录
6. `Writeback`
   - `Game Mod` 最终扣货币、给实例、改关系、写承诺或拒绝结果。
7. `Player-visible Surface`
   - `UI/Carriers/ItemTextCarrierBase.cs`
   - 必要时打开宿主商店 UI

Stardew 落地死步骤：

1. 交易动作先由 `Runtime.Local` 过 gate。
2. 需要开商店时，只允许按 `ShopTileFramework` 那种宿主入口打开。
3. 需要真实创建对象时，按 `LivestockBazaar` 那种“扣货币 -> 创建宿主对象 -> 返回对象实例”的顺序执行。
4. 关系变化与承诺写回必须和本轮 accepted action bundle 同步记录。

绝对不允许 AI 自由发挥的点：

1. 不允许 `Cloud` 直接宣布交易已完成。
2. 不允许 `Mod` 绕开 `Runtime.Local` 直接给物。
3. 不允许把 `ShopTileFramework` 的开店壳误当成 AI 交易语义主链。

### 4.7 物品文本 carrier 与自定义物品生成

设计语义锚点：

- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/13_自定义物品生成.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/08_交易与给物互动.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/07_行为协议_解析与执行.md`

Stardew 主参考：

- `参考项目/Mod参考/StardewValleyMods-spacechase0/framework/JsonAssets/Mod.cs:1423-1475`
- `参考项目/Mod参考/Stardew-GitHub-Floogen-CustomCompanions/CustomCompanions/CustomCompanions.cs:236-345`
- `参考项目/Mod参考/Stardew-GitHub-Floogen-CustomCompanions/CustomCompanions/CustomCompanions.cs:374-452`

推荐做法和理由：

1. 推荐把“物品语义生成”和“Stardew 宿主实例化”拆成两步。
   - 理由：这是你要的多游戏复用核心，语义在 Cloud，实例化在 title-local host。
2. 推荐 `Json Assets` 做 item template / shop 接入，`CustomCompanions` 做地图点位生成或特殊实体载体参考。
   - 理由：这两个参考点一个管物，一个管实体，分工清楚。

7 段主链固定如下：

1. `Trigger`
   - 私聊、交易、世界事件、掉落补位都可以触发。
2. `Snapshot`
   - `Game Mod` 提供当前需要的类别、来历、交付场景、落点通道。
3. `Prompt`
   - `Cloud` 生成结构化 item definition。
4. `Parse / Normalize`
   - `Runtime.Local` 校验类别、属性、允许范围、title support。
5. `Projector`
   - `Runtime.Stardew Adapter` 决定：
     - 走 item template
     - 走特殊对象
     - 走地图点位实体
6. `Writeback`
   - `Game Mod` 创建实例，写 `item.modData` 语义，接宿主背包/邮件/奖励/tooltip carrier。
7. `Player-visible Surface`
   - `UI/Carriers/ItemTextCarrierBase.cs`

Stardew 落地死步骤：

1. 先显示文本 carrier，再做真实实例落地。
2. 真实实例创建成功后，才允许把它带入后续对话和记忆 replay。
3. 优先实例级名字/描述，不改全局模板语义。
4. 特殊 companion/召唤物类对象，才允许参考 `CustomCompanions` 的内容包和地图点位生成链。

绝对不允许 AI 自由发挥的点：

1. 不允许 AI 直接给出一个宿主对象指针并跳过 title-local creator。
2. 不允许实例没创建成功，却先把它写成正式历史。
3. 不允许把 `CustomCompanions` 整套搬来充当通用 NPC 系统。

### 4.8 日程读取与日程展示

设计语义锚点：

- 这是 `Stardew` title-local 宿主能力。
- 与 `GGBH` 的关系是：它提供给私聊、主动对话、自动行动、世界状态更多事实，不是 `GGBH` 原样玩法。

Stardew 主参考：

- `参考项目/Mod参考/Stardew-Nexus-19305-ScheduleViewer/ScheduleViewer/Schedule.cs:218-364`
- `参考项目/Mod参考/Stardew-Nexus-19305-ScheduleViewer/ScheduleViewer/modEntry.cs:61-306`
- `参考项目/Mod参考/Stardew-Nexus-19305-ScheduleViewer/ScheduleViewer/SchedulesPage.cs:309-544`
- `参考项目/Mod参考/Stardew-Nexus-19305-ScheduleViewer/ScheduleViewer/ScheduleDetailsPage.cs:187-274`

7 段主链固定如下：

1. `Trigger`
   - `DayStarted`、面板打开、需要重采样时触发。
2. `Snapshot`
   - 直接按 `ScheduleViewer` 读 `npc.Schedule`、当前位置、地点名、赠礼状态。
3. `Prompt`
   - 默认不需要 prompt。
   - 日程事实是直接宿主真相。
4. `Parse / Normalize`
   - `Runtime.Stardew Adapter` 把 Stardew 原生格式转统一事实包。
5. `Projector`
   - 翻成信息面板上的当前位置、当前状态、下步去向。
6. `Writeback`
   - 这是展示型能力，默认不改宿主。
7. `Player-visible Surface`
   - `UI/NpcInfoPanelMenu.cs`

绝对不允许 AI 自由发挥的点：

1. 不允许 AI 凭空编一个 NPC 当前地点。
2. 不允许把“读取日程”写成“生成新日程”。
3. 不允许跳过 `ScheduleViewer` 这类成熟宿主读取方式，自己拍脑袋读半套字段。

### 4.9 主动对话与接触触发

设计语义锚点：

- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/06_主动对话与接触触发.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/09_宿主挂点_触发面与回写边界.md`

Stardew 主参考：

- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/InteractionManager.cs:40-210`
- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/Behaviors/NpcInteractionBehavior.cs:35-165`
- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/HarmonyPatches.cs:387-460`

7 段主链固定如下：

1. `Trigger`
   - 补丁层和交互路由层抓“偶遇、接触、按键、近身交互”。
2. `Snapshot`
   - 采地点、双方关系、刚发生的触碰事件、最近对话上下文。
3. `Prompt`
   - `Cloud` 走主动对话 prompt family。
4. `Parse / Normalize`
   - `Runtime.Local` 做节流、去重、冲突拦截。
5. `Projector`
   - `Runtime.Stardew Adapter` 决定是拉起面对面私聊还是某个承诺/邀约动作。
6. `Writeback`
   - `Game Mod` 打开宿主 UI，必要时写入动作结果。
7. `Player-visible Surface`
   - `UI/AiDialogueMenu.cs`

绝对不允许 AI 自由发挥的点：

1. 不允许本地自己写一套“主动搭话文案模板”替代 Cloud。
2. 不允许主动对话无节流、无去重。
3. 不允许动作先落地、对话后补。

### 4.10 自动行动骨架与回到当前日程

设计语义锚点：

- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/07_行为协议_解析与执行.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/06_主动对话与接触触发.md`

Stardew 主参考：

- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/Tasks/UnifiedTaskManager.cs:75-203`
- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/TaskManager.cs:244-400`
- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Pathfinding/AStarPathfinder.cs:22-165`
- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/RecruitmentManager.cs:226-257`
- `参考项目/Mod参考/Stardew-Nexus-35341-TheStardewSquad/TheStardewSquad/Framework/Behaviors/NpcInteractionBehavior.cs:145-165`

推荐做法和理由：

1. 推荐第一阶段直接复刻 `TheStardewSquad` 的任务挑选器和路径器骨架。
   - 理由：这是目前最稳的 Stardew 宿主动作壳，能马上避免自己发明半套 AI 行动系统。
2. 推荐把“任务挑选”和“任务来源”拆开。
   - 理由：骨架先抄宿主，任务候选以后再逐步换成更标准的结构化动作协议，后面好迁 AFW。

7 段主链固定如下：

1. `Trigger`
   - `WorldLifecycleHooks.cs` 在 tick/day/location 变化时驱动。
2. `Snapshot`
   - 采当前位置、当前 schedule、附近可交互目标、可做任务集合。
3. `Prompt`
   - 当前阶段不要求每个宿主行动都走 AI。
   - 已批准 AI 动作建议，仍先回结构化动作候选。
4. `Parse / Normalize`
   - `Runtime.Local` 统一成 title-local allowlist 内的任务意图。
5. `Projector`
   - `Runtime.Stardew Adapter` 挑可执行任务、找目标点、做 path。
6. `Writeback`
   - `Game Mod` 最终移动 NPC、执行宿主动作。
7. `Player-visible Surface`
   - 行动本身就是可见结果；必要时在信息面板显示“当前状态”。

回到日程固定规则：

1. 自动行动结束后，优先按 `RecruitmentManager.GetCurrentScheduleEntryFor` 找当前应在的日程节点。
2. 若能恢复，就按 `NpcInteractionBehavior.WarpToScheduleEntry` 那种宿主恢复方式回去。
3. 不能恢复时，只能给出明确 blocked / deferred 结果，不能静默丢失。

绝对不允许 AI 自由发挥的点：

1. 不允许 AI 直接控制路径点和逐帧移动。
2. 不允许招募玩法语义污染正式 NPC AI 设计。
3. 不允许执行完临时任务后，把 NPC 永久留在错误地点。

### 4.11 信息传播、世界事件、自定义状态、角色卡 / NPC 创建

设计语义锚点：

- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/05_信息裂变与社会传播.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/07_主动世界演化.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/14_自定义状态_气运生成.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/15_角色卡与NPC创建.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/08_世界事件数据模型与落地机制.md`

当前 Stardew 参考结论：

1. 已下载 Stardew 参考 mod 里，没有一套成熟的“云端编排 + 宿主写回”的现成实现。
2. 这几块现在只能：
   - 先定统一接口
   - 先定 title support matrix
   - 先定 deterministic blocked / deferred 结果
3. 不允许假装“已有成熟 Stardew 参考，只差抄代码”。

Stardew 当前必须先写死的接口规则：

1. `information_propagation`
   - 只允许作为独立协议，不和普通 `GiveItem/Attack/...` 混成一类。
2. `world_event`
   - 只允许走单独 event object，不允许一段文案就算事件 committed。
3. `custom state`
   - 只允许走 title-local state creator，不允许 UI 直接写世界状态。
4. `CreateCharacter`
   - 只允许走 title-local NPC creator，不允许直接在聊天时凭空造一个长期 NPC。

绝对不允许 AI 自由发挥的点：

1. 不允许把没有宿主 creator 的能力写成“已支持”。
2. 不允许 Cloud 直接跳过 `Game Mod` 改宿主世界。
3. 不允许把 blocked 能力静默吞掉不留 trace。

## 5. 当前没有本地成熟参考的地方

下面这些地方，当前只能按“语义抄 `GGBH` + 宿主自己实现壳”处理：

1. AI 私聊对话框本体
2. 动态选项 UI 本体
3. thought surface
4. 现场群聊完整主链
5. 手机主动群聊完整主链
6. 信息传播
7. 世界事件
8. 自定义状态
9. 角色卡 / 长期 NPC 创建

推荐结论和理由：

1. 推荐老老实实把它们标成“没有现成本地成熟件”。
   - 理由：这样后面做 plan 才不会误把“要自己实现的地方”当“已经有参考”。
2. 推荐这些点在实施计划里必须强制带：
   - `GGBH 分析文档路径`
   - `参考源码行号`
   - `当前 title-local 实现文件`
   - `deterministic gate`
   - `blocked / deferred` 规则
   - 理由：这样 AI 实施时活动空间最小。

## 6. 实施前检查单

以后只要开始写 Stardew 相关 plan 或代码，先过这张表：

1. 这个功能的玩法语义，是否已经回链到 `GGBH` 对应分析文档？
2. 这个功能的宿主壳，是否已经指到具体 `Stardew` 参考 mod 文件？
3. `Trigger / Snapshot / Prompt / Parse / Projector / Writeback / Surface` 是否都写了 owner？
4. 是否明确了 `Cloud` 才能拼最终 prompt？
5. 是否明确了 `Runtime.Local` 才能做统一 gate？
6. 是否明确了 `Game Mod` 才能做最终宿主写回？
7. 是否写清了哪些地方没有本地成熟参考？
8. 是否写清了 blocked / deferred 规则？

只要上面有一项答不上来，就不允许进入实现。

## 7. 类落点回链

以后只说“这个功能参考了哪个 mod”还不够。  
还必须同时回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-hook-session-projector-executor-appendix.md`

固定原因：

1. 参考 mod 只告诉我们抄哪层
2. 真正落到 `Superpowers.Stardew.Mod` 哪个类，得看这份施工附件
