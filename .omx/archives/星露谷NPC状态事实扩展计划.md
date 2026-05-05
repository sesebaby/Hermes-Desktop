# 星露谷 NPC 状态事实扩展计划

## 目标

让 NPC agent 看到更像“人在游戏里应该知道”的事实，但不要把所有东西都塞进每一轮 `stardew_status`。首版要解决两个问题：

- agent 每轮能稳定知道最关键的现场信息，比如 NPC 在哪、玩家在哪、现在几点、玩家手里拿着什么、玩家是否能被打扰。
- agent 想知道更多背景时，可以自己调用更细的工具查，比如玩家详情、游戏进度、任务、最近行动、好感/礼物/婚姻、农场信息。
- 工具返回尽量用自然语言摘要，让 agent 像读游戏观察报告一样理解，而不是面对一大堆难读字段。
- 工具虽然可以多，但不能鼓励每轮全查；普通自主回合默认最多额外查 1 个状态工具。

## 设计原则

- `stardew_status` 只放每轮必需、变化快、字数少的事实。
- 大而慢、不是每轮都要看的内容拆成新工具；工具可以多几个，但每个工具职责要清楚。
- 所有事实都要来自当前源码能拿到的星露谷状态，不能靠猜。
- 涉及 SMAPI/Stardew API 的字段，必须先读 SMAPI 文档、Stardew 1.6 迁移文档、当前游戏程序集或现有源码证据；不能靠字段名猜。
- 每个新增查询都要有日志，能看到耗时、字数、关键字段。
- 不改变 agent 的独立自主循环；除私聊外，不把行为改成事件驱动。
- 工具结果优先给自然语言摘要，同时保留少量稳定 key/value 方便测试和日志。
- 工具返回格式统一：第一段是自然语言摘要，后面最多 12 条 key/value facts。

## 当前源码事实

- bridge 侧 `/query/status` 在 `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs` 的 `BuildStatusResponse` 里生成。
- bridge 侧 status DTO 是 `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs` 的 `NpcStatusData`。
- Hermes 侧 status DTO 是 `src/games/stardew/StardewBridgeDtos.cs` 的 `StardewNpcStatusData`。
- Hermes 侧 facts 映射在 `src/games/stardew/StardewQueryService.cs` 的 `BuildStatusFacts`。
- agent 默认工具由 `src/games/stardew/StardewNpcTools.cs` 的 `StardewNpcToolFactory.CreateDefault` 注册，目前有 `stardew_status`、`stardew_move`、`stardew_speak`、`stardew_open_private_chat`、`stardew_task_status`。
- `src/runtime/NpcAutonomyLoop.cs` 每轮把 observation/event 拼进 `[Observed Facts]`，并已经记录 fact count、message chars、gameTime/location/tile、LLM 耗时。
- `src/runtime/NpcObservationFactStore.cs` 已经保存 observation/event，但还没有作为“最近行动查询工具”暴露给 agent。
- SMAPI 文档说明 mods 应使用 SMAPI API，如事件、数据、输入、日志、反射、工具等；Stardew 1.6 文档显示很多数据资产已迁移到新结构，比如 `Data/Shirts`、`Data/Pants`、`Data/Tools`，并新增/调整 game state query、item query、Quest `modData` 等能力。

## RALPLAN-DR 摘要

### 原则

- 事实要有用，不追求全量。
- 常用事实常驻，少用事实按需查。
- 查询要快，日志要能定位慢在哪里。
- 新字段要先摘要，避免 prompt 越来越胖。
- 首版优先让 agent 少犯低级错，再扩复杂背景。
- 行动优先于查背景：该移动/说话/私聊时，不要因为想查更多状态而拖住行动。

### 决策驱动

- agent 反应速度：不能因为 status 太大导致每轮更慢。
- 行为可靠性：agent 要知道玩家位置、手持物、任务和最近动作，减少胡说和重复动作。
- 可调试性：日志必须能回答“agent 这一轮看到了什么、为什么没动/没说”。
- 工具预算：当前 NPC 工具面已经有行动工具和内建工具，新状态工具不能把有效 tool turn 全吃掉。
- API 可信度：任何衣着、任务、好感、礼物、婚姻、农场扫描字段，必须以 SMAPI 文档或当前游戏程序集验证结果为准。

### 可选方案

#### 方案 A：把所有事实都塞进 `stardew_status`

优点：实现简单，agent 每轮都能看到所有信息。

缺点：prompt 会变大，LLM 每轮更慢；很多事实每轮不需要；后续调试很难区分到底是哪类事实拖慢。

结论：不采用。

#### 方案 B：`stardew_status` 只扩关键现场事实，其他拆成多个自然语言工具

优点：每轮观察仍然轻；agent 需要背景时可以主动查；每个工具能返回更详细但更像人话的摘要；日志也能按工具区分耗时。

缺点：工具数量变多，agent 需要学会什么时候调用哪个；工具说明和 prompt 要写清楚。

结论：采用。

#### 方案 C：先不扩 status，只新增很多工具

优点：status 最轻。

缺点：agent 连玩家是否在身边、手里拿什么这种现场事实都要额外查，容易每轮多一次工具调用，反而慢。

结论：只作为局部补充，不作为主方案。

## 决策

采用方案 B：保留 `stardew_status` 为“现场快照”，但新增几个按需工具。

## 工具调用优先级和预算

这是执行时必须遵守的规则，避免工具变多以后 agent 每轮都查一遍：

- 普通自主回合：`stardew_status` 来自每轮 observation；agent 最多再额外查 1 个状态工具，然后就应该行动或等待。
- 行动优先级高于背景查询：如果当前 facts 已经足够决定 `stardew_move`、`stardew_speak`、`stardew_open_private_chat`，就不要先查任务/进度/农场。
- 长动作进度只用 `stardew_task_status`，不要用 `stardew_recent_activity` 代替。
- `stardew_recent_activity` 只在“我刚才做了什么、上次为什么失败、todo 是否要恢复”这类连续性问题上调用。
- 背景工具选择顺序：
  - 看玩家眼前状态、衣着、手持物：`stardew_player_status`
  - 看当前 NPC 好感、礼物、婚姻：`stardew_social_status`
  - 看玩家任务：`stardew_quest_status`
  - 看游戏阶段：`stardew_progress_status`
  - 看农场情况：`stardew_farm_status`
  - 看自己最近动作：`stardew_recent_activity`

验收时要看日志里的 toolCalls，不能只看功能是否可用。

可观测合同：

- 在 `src/Core/Agent.cs` 或更靠近工具执行汇总的位置，为 NPC autonomy turn 增加工具汇总日志，包含 `sessionId`、`traceId`、`toolCalls`、`toolNames`、`statusToolCalls`、`durationMs`。
- status 工具名单固定维护：`stardew_status`、`stardew_player_status`、`stardew_progress_status`、`stardew_social_status`、`stardew_quest_status`、`stardew_farm_status`、`stardew_recent_activity`。
- 如果一个自主回合额外状态工具超过 1 个，记录 `status_tool_budget_exceeded`，包含超出的工具名。首版只记录和测试，不直接阻断 agent。
- 手测验收必须能从日志里回答：这一轮 agent 查了哪些工具、查了几个状态工具、是否超过预算。

## 首版事实分层

### 1. 每轮常驻：继续放在 `stardew_status`

这些字段每轮变化快、字数少，应该跟 NPC 状态一起返回：

- NPC 当前地点和坐标：已有。
- 游戏内时间、季节、日期、天气：已有。
- NPC 是否可控、是否移动、是否在对话：已有。
- 可去目的地、附近可走格：已有，但继续限制数量。
- 玩家当前地点和坐标：新增。
- 玩家与 NPC 的距离摘要：新增，比如 `playerDistance=near_8`、`sameLocation=true`。
- 玩家是否忙：新增，比如菜单打开、事件中、过图中、对话中。
- 玩家手里东西：新增摘要，比如 `playerHeldItem=Parsnip Seeds` 或 `playerHeldItem=empty`。
- 玩家金钱：首版默认不放进 `stardew_status`，只放进 `stardew_player_status`；除非实现时日志证明新增一行不会让 messageChars 超过预算。

建议把玩家现场 facts 压成低基数字段，减少自由文本：

- `playerReachability=near_same_map|same_map_far|other_map|unknown`
- `playerAvailability=free|menu_open|dialogue_open|event_active|unknown`
- `playerHeldItem=...`

不把衣着、完整背包、完整任务列表、完整游戏进度塞进 `stardew_status`。

### 2. 按需工具：`stardew_player_status`

agent 想理解玩家时调用。返回：

- 玩家地点、坐标、面向。
- 手持物、当前工具、背包前几个关键物品摘要。
- 金钱。
- 衣着/装备摘要：帽子、上衣、裤子、鞋子、饰品。如果字段名在 SDV 1.6.15 里不稳定，首版先用能稳定拿到的显示名，拿不到就返回 `unknown`。
- 体力、生命、是否骑马、是否在矿洞/节日/事件中。
- 婚姻状态的短摘要可以放这里：未婚、已婚、配偶是谁、是否有室友。如果字段读取风险高，则拆到社交工具。

返回风格：

- 第一段自然语言，例如：“玩家现在在 Town 的 64,15，手里拿着防风草种子，有 1234g，当前没有菜单打开。”
- 后面附少量 facts，例如 `playerLocation=Town`、`playerHeldItem=Parsnip Seeds`。

日志：

- `player_status_query_completed`
- npc、trace、durationMs、payloadChars、playerLocation、playerTile、heldItem、money、equipmentCount、status=completed|degraded|failed、unknownFields。

### 3. 按需工具：`stardew_progress_status`

agent 想判断游戏阶段时调用。返回摘要，不返回全量存档：

- 年份、季节、日期、星期、时间。
- 农场名、玩家名。
- 钱、总收入如果稳定可取。
- 技能等级摘要。
- 重要进度摘要：矿洞层数、社区中心/Joja 粗略状态、已解锁地点。实现时以 Stardew API 能稳定读取为准。

日志：

- `progress_status_query_completed`
- npc、trace、durationMs、payloadChars、year、season、day、mineLevel、status=completed|degraded|failed、unknownFields。

### 4. 按需工具：`stardew_social_status`

agent 想理解玩家和 NPC 的关系、礼物、婚姻状态时调用。返回：

- 当前 NPC 和玩家的好感摘要：心数、关系状态、今天是否说过话、今天/本周是否送过礼。
- 如果参数指定某个 NPC，就查那个 NPC；如果不指定，就默认当前 NPC。
- 可选返回全村民关系摘要，但要限量，比如只返回心数最高 5 个、最近互动 5 个。
- 礼物信息：当前 NPC 已知喜欢/讨厌的礼物如果能稳定拿到就给；玩家最近送过什么礼物如果能稳定从存档/事件拿到就给；拿不到就明确说“首版没有可靠来源”。
- 婚姻状态：未婚、恋爱、已婚、配偶/室友是谁、当前 NPC 是否配偶/恋人。

返回风格：

- 自然语言优先，例如：“玩家和 Haley 目前 4 心，今天还没说过话，本周已经送过 1 次礼物。玩家未婚。”
- facts 只保留核心字段，例如 `friendshipHearts=4`、`talkedToday=false`、`giftsThisWeek=1`、`spouse=none`。

日志：

- `social_status_query_completed`
- npc、targetNpc、trace、durationMs、payloadChars、hearts、talkedToday、giftsThisWeek、spouse、status=completed|degraded|failed、unknownFields。

### 5. 按需工具：`stardew_quest_status`

agent 想知道玩家正在忙什么时调用。返回：

- 当前任务数量。
- 最多 5 条活跃任务摘要：标题、目标、是否完成、和当前 NPC 是否相关。
- 如果原版任务 API 取不到稳定标题，就返回内部 id 和安全摘要，不硬猜。

日志：

- `quest_status_query_completed`
- npc、trace、durationMs、payloadChars、questCount、returnedCount、status=completed|degraded|failed、unknownFields。

### 6. 按需工具：`stardew_farm_status`

agent 想理解玩家农场、资源、当天农活时调用。返回：

- 农场名、农场类型如果能稳定拿到。
- 农场当前天气、季节、日期。
- 作物/动物/建筑的轻量摘要：首版不扫描全地图细节，先给能稳定取得的粗略信息。
- 如果可以安全扫描农场地块，再给“需要浇水作物数量”“成熟作物数量”“动物数量”等摘要；扫描要有上限，避免卡顿。
- 当前玩家金钱和背包关键物品可重复给一行，方便 agent 判断能不能做农活。

返回风格：

- 自然语言优先，例如：“农场是 Meadowlands Farm。今天晴天，农场上有一些作物需要照看；首版还没有全地图作物扫描。”
- facts 保留 `farmName`、`farmType`、`needsWateringCount`、`readyCropCount`、`animalCount`，拿不到就省略或 unknown。

日志：

- `farm_status_query_completed`
- npc、trace、durationMs、payloadChars、farmName、farmType、scanTiles、readyCropCount、needsWateringCount、animalCount、status=completed|degraded|failed、unknownFields。

### 7. 按需工具：`stardew_recent_activity`

这是 runtime continuity 工具，不是普通游戏世界事实工具。agent 想避免重复、想恢复刚才动作、想知道自己最近做了什么时调用。首版从 Hermes runtime 事实里取，不从游戏事件驱动新增行为：

- 最近 5 条 observation/event/tick 摘要。
- 最近一次成功 action：move/speak/open_private_chat。
- 最近一次失败 action 和原因。
- 当前 todo 摘要：pending / in_progress 最多 3 条。
- 必须严格按 `gameId/saveId/npcId/profileId/sessionId` 隔离，不能串 NPC、串 profile、串存档。

实现来源：

- `NpcObservationFactStore.Snapshot(...)`
- `NpcRuntimeInstance` 的 last terminal command status
- `SessionTodoStore.Read(sessionId)`
- 必要时读取当前 NPC 的 `runtime.jsonl` 末尾摘要，但首选内存对象，减少 IO。

依赖注入方案：

- 不把 `NpcObservationFactStore`、todo store、last terminal status 强塞进 bridge；`stardew_recent_activity` 是 Hermes runtime 工具，应该在 Hermes 侧读取 runtime 对象。
- 给 `StardewNpcToolFactory.CreateDefault` 增加一个可选 recent-activity provider 参数，或新增一个小接口，例如 `IStardewRecentActivityProvider`。
- provider 由 `NpcRuntimeSupervisor` / runtime binding 创建工具时注入，内部只读当前 `NpcObservationFactStore.Snapshot(descriptor)`、当前 `NpcRuntimeInstance` 的 action/todo 只读视图、当前 session 的 `SessionTodoStore.Read(descriptor.SessionId)`。
- 如果当前对象拿不到某一类数据，工具返回 degraded 摘要，不抛 500。
- 不通过 bridge 新增 `/query/recent_activity`，因为这不是游戏世界状态，而是 Hermes runtime 连续性状态。

日志：

- `recent_activity_query_completed`
- npc、trace、durationMs、payloadChars、factCount、todoCount、lastActionStatus、saveId、profileId、sessionId、status=completed|degraded|failed、unknownFields。

## 实施步骤

1. 扩 bridge DTO
   - 在 `NpcStatusData` 增加玩家现场轻量字段。
   - 在 Hermes 的 `StardewNpcStatusData` 加对应字段。
   - 字段尽量简单：玩家 location/tile、sameLocation、distance/reachability、heldItem、playerBusyReason。
   - money 首版默认不作为常驻字段，除非 messageChars 验证通过后再加入。

2. 扩 `/query/status`
   - 在 `BridgeHttpHost.BuildStatusResponse` 读取 `Game1.player` 的地点、坐标、手持物、忙碌原因。
   - 保持目的地和 nearby 数量上限。
   - 给 bridge 日志加一条 status 查询完成日志，至少包含 durationMs、payloadChars、gameTime、npcLocation、playerReachability、playerAvailability、heldItem。

3. 扩 facts 映射
   - 在 `StardewQueryService.BuildStatusFacts` 增加玩家现场 facts。
   - 控制事实名稳定，例如 `playerLocation`、`playerTile`、`playerReachability`、`playerAvailability`、`playerHeldItem`。
   - 增加单元测试，证明新 facts 出现在 observation 里。

4. 先做 SMAPI / Stardew API 证据确认
   - 阅读 SMAPI 文档和 Stardew 1.6 迁移文档，确认玩家衣着、任务、好感、送礼、婚姻、农场类型、农场扫描能用哪些 API。
   - 必要时用当前引用程序集/反编译/编译探针确认字段存在。
   - 把确认结果写到实现注释或测试名里；不确定的字段走 degraded/unknown，不硬猜。

5. 新增 `stardew_player_status`
   - bridge 新增 `/query/player_status`。
   - Hermes 新增 DTO、QueryService 方法和工具类。
   - 工具说明写清楚：需要理解玩家状态、衣着、背包摘要时才调用。

6. 新增 `stardew_progress_status`
   - bridge 新增 `/query/progress_status`。
   - 只返回摘要，不追求全量存档。
   - 对不稳定字段使用 `unknown` 或省略，不硬凑。

7. 新增 `stardew_social_status`
   - bridge 新增 `/query/social_status`。
   - 首版支持当前 NPC 关系摘要、婚姻状态、送礼次数/是否说话。
   - 礼物偏好和最近送礼记录如果不能稳定拿到，明确返回“无可靠来源”，不瞎编。

8. 新增 `stardew_quest_status`
   - bridge 新增 `/query/quest_status`。
   - 首版最多返回 5 条任务。
   - 对任务标题/目标取不到的情况写测试和日志，不让工具报 500。

9. 新增 `stardew_farm_status`
   - bridge 新增 `/query/farm_status`。
   - 首版先给农场名、类型、天气、轻量农场摘要。
   - 地块扫描必须限量并记录耗时；如果成本高，先关掉全图扫描。

10. 新增 `stardew_recent_activity`
   - Hermes 侧工具优先从 runtime 内存结构取最近 observation/event/todo/action。
   - 不改变 agent 触发机制，不新增事件驱动行为。
   - 如果内存事实不足，再规划后续让 runtime log writer 写更结构化 action 记录。
   - 按上面的 provider 方案接线，不新增 bridge 路由。
   - 增加隔离测试：同 NPC 不同 session、同 save 不同 NPC、同 NPC 不同 profile、autonomy session vs private chat session。

11. 增加工具调用汇总日志
   - 在 Agent 工具执行汇总层或 NPC autonomy turn 收尾处记录 `toolCalls`、`toolNames`、`statusToolCalls`。
   - 如果 `statusToolCalls > 1`，记录 `status_tool_budget_exceeded`。
   - 日志要带 npc/session/trace，能和 `NPC autonomy decision request prepared` 对上。

12. 更新 repo-backed skill 和工具说明
   - 不绕过现有 prompt supplement 架构；优先更新 repo-backed Stardew skill 文档和 persona `skills.json`，让 `StardewNpcAutonomyPromptSupplementBuilder` 正常注入。
   - 告诉 agent：现场判断先看 `stardew_status`；需要玩家细节再查 `stardew_player_status`；需要社交/礼物/婚姻查 `stardew_social_status`；需要农场查 `stardew_farm_status`；需要任务/进度/最近动作再查对应工具。
   - 明确不要每轮无脑调用所有状态工具。
   - 说明工具结果是自然语言摘要，agent 应该自己决定是否需要继续查。
   - 明确普通自主回合最多额外查 1 个状态工具，行动工具优先。

13. 加测试
   - bridge status DTO 序列化/响应测试。
   - `StardewQueryServiceTests` 验证 facts。
   - `StardewNpcToolFactoryTests` 验证新工具注册。
   - 新工具单测覆盖正常、world_not_ready、字段缺失、数量上限。
   - `recent_activity` 隔离矩阵测试：同 NPC 不同 session、同 save 不同 NPC、同 NPC 不同 profile、autonomy session vs private chat session。
   - tool budget 日志测试：0/1 个额外状态工具不报警，2 个额外状态工具记录 `status_tool_budget_exceeded`。

14. 手测验收
   - 启动 Hermes 和游戏后，日志里能看到 status 查询的玩家位置、手持物、reachability、availability。
   - 玩家和 Haley 同地图 8 格内：agent 已有足够事实时直接 `stardew_speak` 或等待，不连续刷多个状态工具。
   - 玩家跨地图：agent 如果需要背景最多查一个合适状态工具，然后用手机/等待/移动之一，不连续刷多个状态工具。
   - agent 需要背景时才调用新工具，不能每轮都刷四个查询。
   - 对比实现前同场景 `messageChars` 基线，超过 20% 算失败。

## 验收标准

- 每轮 observation facts 中至少能看到玩家地点、坐标、reachability、availability、手持物；金钱默认通过 `stardew_player_status` 查。
- 玩家衣着、任务、游戏进度、最近行动不默认塞进每轮 facts，而是通过工具按需查询。
- 好感、送礼、婚姻、农场信息通过独立工具按需查询，工具结果以自然语言摘要为主。
- 新增工具都有 schema、描述、DTO、日志、测试。
- 日志能查到每个查询的耗时、payload 字数和关键摘要字段。
- `stardew_status` 的 facts 数量仍有上限，目的地最多 5 个、附近格最多 3 个，新增玩家现场 facts 控制在 8 条以内。
- 不能只看 fact 条数，还要看 `messageChars`：首版新增后，普通 observation 的消息长度不能明显暴涨；若比当前基线增加超过 20%，必须缩短 facts 或移入按需工具。
- 工具调用行为要受控：普通自主回合默认最多额外查 1 个状态工具；长动作进度只用 `stardew_task_status`；日志里要能看到是否有滥用查询。
- `stardew_recent_activity` 必须证明不会串 NPC、串 profile、串 save。
- 不改变 agent 独立自主循环，不引入除私聊外的事件驱动执行。
- 每个新状态工具返回格式固定：第一段自然语言摘要，后面附最多 12 条 key/value facts。
- `messageChars` 基线用实现前同一 NPC、同一地图、无新增状态工具的一轮 `NPC autonomy decision request prepared` 日志；实现后同场景超过基线 20% 算失败。
- API 证据验收：衣着、任务、好感、送礼、婚姻、农场信息的字段来源必须能指向 SMAPI 文档、Stardew 1.6 迁移文档、当前程序集证据或现有源码；不满足就 degraded/unknown。

## 风险和处理

- 风险：状态事实越加越多，LLM 变慢。
  - 处理：常驻 facts 限量，重信息拆工具，并记录 messageChars/payloadChars。

- 风险：星露谷衣着、任务、社区中心、好感、送礼、婚姻、农场字段不稳定。
  - 处理：实现前读 SMAPI 文档、Stardew 1.6 迁移文档，并按 SDV 1.6.15 当前 API/程序集验证；拿不到就返回 `unknown` 或省略，不硬猜。

- 风险：工具多了以后，agent 每轮乱调用所有工具。
  - 处理：工具说明和 prompt 写清楚调用条件；工具结果自然语言化；日志统计每轮 toolCalls，发现滥用再加预算或提示约束。

- 风险：农场扫描、全 NPC 好感扫描可能卡顿。
  - 处理：首版默认摘要和限量；全图扫描必须有上限、耗时日志，慢就降级。

- 风险：最近行动来源分散。
  - 处理：首版通过 Hermes runtime provider 从 `NpcObservationFactStore`、last terminal status、todo store 摘要取；如果不够，再单独扩 runtime action 结构化日志。

- 风险：字段拿不到时误导 agent。
  - 处理：所有查询支持 degraded；未知字段明确写 `unknown` 或省略，并在日志记录 `unknownFields`。

## ADR

### 决策

采用“轻量 status + 按需状态工具”的方案。

### 驱动

- 保持 agent 响应速度。
- 让 NPC 有足够现场感。
- 后续日志能定位是 status 慢、工具慢，还是 LLM 慢。

### 备选方案

- 全塞进 `stardew_status`：实现快，但 prompt 膨胀，用户已经在关注慢的问题，不适合。
- 只加工具不扩 status：status 轻，但玩家位置/手持物这种现场事实太基础，会逼 agent 多调用工具。

### 为什么这样选

现场事实少量常驻，背景事实按需查询，能兼顾速度和智能程度。

### 后果

- 代码会增加多个 DTO、bridge 路由和工具类。
- prompt 和工具说明必须更清楚，否则 agent 不知道什么时候查什么。
- 后续调试会更容易，因为每类事实都有独立日志。
- 需要维护工具选择规则，否则工具越多越容易消耗有效回合预算。

### 后续待办

- 实现前阅读并记录 SMAPI 文档、Stardew 1.6 迁移文档和当前程序集证据，确认玩家衣着、任务、社区中心/Joja 进度字段。
- 实现前阅读并记录 SMAPI 文档、Stardew 1.6 迁移文档和当前程序集证据，确认好感、送礼、婚姻、农场类型、农场作物扫描字段。
- 如果 `stardew_recent_activity` 首版信息不足，再扩 runtime action log 的结构化字段。
- hold/控制租约仍是移动控制的独立待解决问题，本计划不直接解决。

## 可用 agent 类型和执行建议

- `$ralph` 顺序执行：适合先做 status 轻量扩展，再逐个加工具和测试。
- `$team` 并行执行：适合拆成 bridge DTO/路由、Hermes DTO/工具、测试/日志三个 lane。
- 推荐角色：
  - `explore`：确认 SDV API 字段和当前项目触点。
  - `executor`：实现 bridge + Hermes 工具。
  - `test-engineer`：补测试和手测检查清单。
  - `verifier`：跑测试、看日志、确认 prompt 字数没有暴涨。

## 启动提示

顺序执行可用：

```text
$ralph 执行 .omx/plans/星露谷NPC状态事实扩展计划.md
```

并行执行可用：

```text
$team 按 .omx/plans/星露谷NPC状态事实扩展计划.md 实现，分 bridge、Hermes 工具、测试验证三路
```

## 验证路径

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewQueryServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests"`
- `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug`
- `dotnet build .\src\Hermes.Core.csproj -c Debug`
- `dotnet build .\Mods\StardewHermesBridge\StardewHermesBridge.csproj -c Debug`
- 手测日志检查：
  - `NPC autonomy observation completed`
  - `NPC autonomy decision request prepared`
  - `NPC autonomy tool summary`
  - `status_tool_budget_exceeded`
  - `player_status_query_completed`
  - `progress_status_query_completed`
  - `social_status_query_completed`
  - `quest_status_query_completed`
  - `farm_status_query_completed`
  - `recent_activity_query_completed`

## 计划修订记录

- 初版：根据当前 status 源码、runtime facts、todo store 和用户提示，确定轻量 status 加按需工具的方向。
- 根据用户追加意见修订：工具可以更多更细，结果优先用自然语言；补充好感、礼物、婚姻状态和农场信息工具。
- 根据架构审查修订：补充工具调用优先级、每回合查询预算、repo-backed skill 更新路径、`recent_activity` 的 runtime continuity 边界、messageChars 字符预算验收。
- 根据 critic 审查修订：补充 `recent_activity` provider 接线方案、tool budget 汇总日志、可测验收句子、隔离测试矩阵、degraded/unknownFields 日志规范，并明确 money 默认不常驻。
- 根据用户提醒修订：要求实现前阅读 SMAPI/Stardew 1.6 文档和当前程序集证据，不能靠字段名猜。
