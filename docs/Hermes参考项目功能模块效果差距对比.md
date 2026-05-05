# Hermes 参考项目功能模块效果差距对比

更新时间：2026-05-05

本文对比 `external/hermes-agent-main` / `external/hermescraft-main` 参考项目与当前 Hermes Desktop 项目的功能模块效果差距，重点服务于“接入星露谷物语，实现多 NPC 村庄模式，让 AI Agent 像真实玩家一样长期自主生活”的目标。

本文当前定位是 **下一步工作依据**，不是早期愿景说明。结论按当前仓库实际代码、近期规格和测试资产更新；若后续代码继续变化，以代码和测试为准。

## 当前总体判断

早期判断“当前只是骨架，NPC 还像人按按钮动一下”已经不准确。当前项目已经形成了较完整的 Stardew NPC runtime 主干：

- 每个 NPC 已有 `gameId + saveId + npcId + profileId` 命名空间、persona seed pack、独立 `SOUL.md` / memory / transcript / activity 目录。
- 已有 `NpcRuntimeSupervisor`、`NpcRuntimeInstance`、`NpcRuntimeContextFactory`、`NpcAutonomyLoop`，可以为 private chat 与 autonomy 创建 NPC 专属 Agent。
- Stardew bridge 已有 HTTP command / status / discovery / event buffer / private chat / speak / move 通路。
- `todo` 已扩展 `blocked` / `failed` / `reason`，并且 `Session.ToolSessionId` 已用于把 private chat 的工具任务归到 NPC 长期 runtime session。
- 移动链路已完成 Agent 公开契约层的 `destinationId` 收敛：`stardew_move` 不再解析 observation label/坐标，Agent 只提交 `destinationId`，Bridge/CommandService 继续保留隐藏兼容层。
- 桌面端已有 NPC runtime workspace 的只读投影，能展示 runtime snapshot、active task summary、blocked/failed summary、trace/error。

但它还没有达到参考项目那种“长期自治 Agent 自己持续生活”的效果。当前主要缺口已经从“有没有基础模块”转为：

1. **自主循环仍偏单 tick 驱动**：每次 tick 能观察、思考、调用工具，但长期任务推进、等待、恢复、跨天续跑还不够成熟。
2. **移动公开契约已收敛，执行能力仍待增强**：Agent 面已经变成“只提交 `destinationId`，Bridge/host 负责执行”，但真实跨地图移动仍未完成，Bridge 当前仍会明确阻断而不是伪完成。
3. **任务连续性刚打通承载面**：`todo` 状态、`ToolSessionId`、continuity skill 已具备，但还需要证明 private chat 形成的任务能被 autonomy 稳定推进、失败反馈、归档和 UI 观测。
4. **NPC 行为知识层仍薄**：已有 `stardew-core`、`stardew-social`、`stardew-navigation`、`stardew-task-continuity`、`stardew-world`，但内容还不足以覆盖日程、地点意义、社交偏好、失败恢复和长期生活习惯。
5. **多 NPC 村庄体验仍未闭环**：private chat 与单 NPC autonomy 基础较强，群聊、偷听、NPC 之间自然互动、资源互斥和并发成本控制仍是后续重点。

## 当前能力与差距表

| 模块 | 对目标是否关键 | 当前项目实际效果 | 参考项目效果 | 主要剩余差距 | 下一步含义 |
| --- | --- | --- | --- | --- | --- |
| NPC runtime / 生命周期 | 最高 | 已有 `NpcRuntimeSupervisor`、`NpcRuntimeInstance`、driver、snapshot、lease、action slot、pending work、runtime state store | 每个 Agent 独立 home，长期运行到迭代上限 | 当前更像 Desktop 托管的 runtime 实例，还缺成熟恢复、跨天续跑、长期后台节奏 | 继续强化 supervisor/driver，而不是另起“一 NPC 一进程” |
| Agent 自主循环 | 最高 | `NpcAutonomyLoop` 能 observe、poll events、构造中文决策消息、调用 Agent、写 trace/memory/diagnostic | 长会话内持续 observe-think-act，并主动轮询后台 task | 单 tick 后续推进、等待策略、失败后停止重试和恢复旧任务还需更多验证 | 优先让 autonomy 消费 active todo 并推进，而不是只对当前观察反应 |
| private chat | 最高 | 已有状态机、输入菜单、reply display、session lease；runner 使用 NPC 专属 Agent | 对话可打断 Agent，回应后恢复任务 | 私聊已经允许先用工具再回复，但需要证明承诺落到长期任务并被 autonomy 接续 | private chat 不再只是对白器，下一步要测任务化和恢复 |
| 任务系统 / todo | 最高 | `todo` 已支持 `blocked`、`failed`、`reason`；`SessionTaskProjectionService` 能按 `TaskSessionId` 投影 | Agent 自己维护长期任务、轮询 task 状态 | 还需验证失败反馈闭环：todo 状态、runtime trace、玩家可见反馈三者一致 | 不新增 NPC task store，继续扩展 `todo` 单一真相 |
| Session continuity | 最高 | `Session.ToolSessionId` 已存在；private chat transcript session 与 tool task session 已分离 | 每个 Agent 长会话天然承载任务和记忆 | 需要更广测试 `session_search`、active task injection、archive 在分离 session 下不漂移 | 这是当前最关键的正确方向，继续加回归保护 |
| 记忆系统 | 最高 | 每 NPC namespace 创建独立 `MemoryManager`；autonomy tick 会写 memory 摘要 | Agent 主动把稳定事实沉淀进长期记忆 | 当前 memory 写入还偏自动摘要，Agent 主动区分 memory/todo/session_search 的习惯刚开始建立 | 用 prompt/skill 约束 Agent 写稳定事实，不让宿主代写人格摘要 |
| Soul / persona | 最高 | persona pack、Haley/Penny 目录、`StardewNpcAutonomyPromptSupplementBuilder` 真实资产注入已存在 | `SOUL.md` 长期塑造 Agent 行为 | 仍要持续验证 private chat 与 autonomy 都加载同一 NPC persona 和 required skills | 不临场生成人格，不写第二人格摘要 |
| Prompt / Context 组装 | 最高 | 复用 `ContextManager` / `PromptBuilder`；supplement 注入 facts/voice/boundaries/skills | prompt/soul/skill/context 共同驱动行为 | 当前 instruction 已中文化，但 still 需要防 prompt-only：没有工具调用时已有 diagnostic，行为闭环还要验证 | 下一步改 prompt 要配测试和 runtime evidence |
| Stardew skill 知识层 | 高 | 已有 `stardew-core`、`stardew-social`、`stardew-navigation`、`stardew-task-continuity`、`stardew-world` | 参考项目 skill 详细描述生存、导航、战斗、任务轮询 | Stardew skill 内容还薄，日程、地点、NPC 偏好、失败恢复不够完整 | 扩 skill 内容优先于写宿主规则 |
| Stardew move 工具 | 最高 | 已完成公开契约收敛：`stardew_move` 只透传 `destinationId`，不再本地 observe/解析 label/坐标；Bridge 支持 `destinationId` registry；status 有 phase/error/block reason | 高层提交目标，底层 pathfinder/后台 task 执行到终态 | Bridge registry 规模小；跨图和自然移动仍需完善；执行态 status 仍保留坐标诊断信息 | 下一步做真实跨地图执行前，先确保任务连续性闭环能消费移动结果 |
| Bridge executor | 最高 | 有 command queue、idempotency、status、cancel、path probe、failure mapper、blockedReason | 后台 task 有 running/stuck/cancel/status | 当前状态机和 executor 拆分仍不完整，部分语义仍在 Bridge 层混杂 | 下一步收敛 registry/projection/executor 三层 |
| Query / observation | 高 | `StardewQueryService` 输出 NPC status、player、destination facts、world snapshot | 参考项目有 status/nearby/look/overhear | observation 已能服务移动和 autonomy，但地点意义和 world skill 投影还不够丰富 | 增加事实质量，而不是让 Agent 编目标 |
| Event / private chat flow | 高 | Bridge event buffer、vanilla dialogue completed/unavailable、private chat opened/submitted/reply displayed 已有 | 参考项目聊天路由和 overhear 较成熟 | 群聊、偷听、公平感知和消息路由还未达到村庄级 | 后续做 SocialRouter 时保持“投递，不导演” |
| UI / Runtime workspace | 中高 | Dashboard/Agent 侧已有 NPC runtime workspace snapshot、任务摘要、failure summary、runtime directory | 参考项目主要靠 CLI/logs | 当前 UI 仍偏调试摘要，缺 task archive、trace drilldown、per-NPC timeline | UI 只读消费 runtime/task 投影，不生成第二状态 |
| Provider routing / 并发预算 | 中高 | Hermes 有 provider/model/credential 基础；NPC binding 可设置 `MaxToolIterations` | 参考项目通过独立进程、错峰和高 iteration 支撑长运行 | 多 NPC 并发 LLM 成本、限流、排队、恢复策略仍不足 | 需要 runtime budget 和 backoff，不应靠 prompt 解决 |
| 安全 / 审计 | 中高 | bridge token、discovery、activity、trace、runtime logs、permission 基础存在 | 参考项目有本机 HTTP 与 task 状态 | “谁因为什么做了什么”已有线索，但 traceId/commandId/task/player feedback 的证据链还需打通 | 失败和真实写操作必须留下结构化证据 |
| Cron / 日程自动化 | 高 | Hermes 有 schedule/todo 基础；NPC autonomy 可后台 tick | 参考项目靠长会话和后台任务持续推进 | Stardew 日程、跨天、等待到某时刻再行动还没成熟 | 先用 todo + autonomy，不急着写 Stardew 专用 scheduler |
| 多 NPC 社交 | 高 | NPC catalog、runtime identity、private chat lease、world snapshot 基础存在 | landfolk/civilization 有多 Agent 社交范式 | 群聊、私聊、偷听、关系变化、NPC-NPC 互动未闭环 | 下一阶段要从消息路由和事实边界开始 |
| 媒体 / 浏览器 / 图像 | 低 | Hermes 本体有部分工具 | 参考项目能力更丰富 | 与 Stardew NPC 长期生活不是当前瓶颈 | 不作为近期优先级 |
| 研究 / 评测 / 批处理 | 低到中 | runtime jsonl、tests、smoke probe 基础存在 | 参考项目可用日志和脚本观察轨迹 | 缺 NPC 行为质量评估、长跑回放、任务完成率指标 | 等任务连续性和移动稳定后再补评测 |

## 已经发生的关键变化

### 1. 从“无长期任务承载”变成“已有单一任务承载面”

早期文档认为 NPC 还没有把承诺变成长期任务。当前代码已经补上关键基础：

- `Session.ToolSessionId`：允许 private chat transcript session 与 tool task session 分离。
- `TodoTool` / `SessionTodoStore`：已支持 `pending`、`in_progress`、`completed`、`cancelled`、`blocked`、`failed` 和 `reason`。
- `SessionTaskProjectionService`：todo tool result 可按 `TaskSessionId` / explicit task session 投影。
- `StardewNpcPrivateChatAgentRunner`：private chat 使用 `${descriptor.SessionId}:private_chat:{conversationId}` 作为 transcript id，同时设置 `ToolSessionId = descriptor.SessionId`。

这意味着下一步不要再设计第二套 `NpcTaskStore`。正确路径是证明并强化：

`private chat 承诺 -> todo 写入长期 session -> autonomy 读取 active todo -> Stardew 工具推进 -> blocked/failed/completed -> 玩家反馈和 UI 留痕`

### 2. 从“只会 move/speak/status”变成“有一组 Stardew 工具和 runtime action 控制”

当前 Stardew 工具已经不只是调试按钮。`stardew_move`、`stardew_speak`、`stardew_task_status`、private chat 相关路径已经进入 runtime 工具面。

移动公开契约收敛已经完成，但执行能力仍处在半完成状态：

- 已完成：`StardewMoveTool` 不再重新 observe，不再从 `destination[n]` fact 解析 `locationName/x/y/facingDirection`，也不再构造真实执行 tile。
- 已完成：`StardewCommandService` 的 destination-first 路径只传 `destinationId`，legacy `target` 只作为隐藏兼容层保留。
- 已完成：Query 投影只暴露带 `destinationId` 的 executable destination facts；prompt/skill/schema 不再要求 label fallback。
- 正向变化：Bridge 已支持 `destinationId`，并能对未知 destination 返回 `invalid_destination_id`。
- 正向变化：status DTO 已包含 `destinationId`、`phase`、`currentLocationName`、`resolvedStandTile`、`routeRevision`。
- 剩余问题：真实跨地图移动仍未完成；当前 Bridge 会明确返回 `cross_location_unsupported`，不能伪装成已抵达。

所以移动后续不是“让 LLM 再聪明一点”，而是在已收敛契约基础上继续把执行真相下沉到 Bridge：

`Agent 只交 destinationId -> StardewCommandService 只传 destinationId -> Bridge registry resolve -> executor 完整执行 -> status 回报`

### 3. 从“NPC runtime 骨架”变成“已有可调试 runtime workspace”

当前桌面侧 `NpcRuntimeWorkspaceService` 已经能聚合：

- runtime snapshot
- bridge health
- last trace/error
- loop/wait summary
- lease/action summary
- pending/cursor summary
- task summary
- task failure summary

这说明 UI 调试不再是从零开始。下一步重点是补充“能钻进去看”的证据，而不是新建一套 UI 状态：

- active task 列表
- task archive
- 最近 todo tool result
- 最近 failed/blocked 的 traceId / commandId
- 玩家是否已经收到失败反馈

UI 只能展示 runtime/task 只读投影，不能解析玩家话术或写回 task state。

### 4. 从“只有人设卡”变成“persona + skill 已真实资产注入”

当前 `StardewNpcAutonomyPromptSupplementBuilder` 会从 persona pack 读取 facts、voice、boundaries、skills，并从 configured gaming skill root 解析 required skills。`stardew-task-continuity` 已存在，正文是中文白话，覆盖接任务、打断恢复、状态轮询、失败反馈。

下一步不是把 HermesCraft 英文 prompt 复制过来，而是继续把它的行为范式映射成 Stardew 中文 skill：

- 接到玩家任务后，Agent 自己判断是否接受；
- 被玩家打断后，先回应，再恢复；
- 长动作启动后，主动查状态；
- 重要稳定事实写 memory；
- 兑现旧约定前查 session_search。

## 当前最核心差距

当前项目已经有“身体、骨架、部分神经系统和日志仪表盘”。参考项目更成熟的是“长期自治习惯”。

最核心差距不是缺一个按钮，也不是缺一个 UI 面板，而是：

**NPC 还需要被验证为能长期维护自己的承诺：从玩家对话中形成任务，跨 session 保留，后续 autonomy 主动推进，遇到阻塞不假装成功，失败时给玩家反馈，并把证据留在 todo、trace、activity 和 UI 中。**

换句话说，下一步工作应围绕“任务连续性 + 移动可靠性 + 可观察失败”收敛，而不是扩很多新功能。

## 下一步优先级

### P0：锁住任务连续性闭环

目标：证明 private chat 和 autonomy 共用 NPC 长期任务面。

应优先完成：

1. private chat 中玩家提出明确承诺时，Agent 能调用 `todo`，任务归属 `descriptor.SessionId`，不是 private chat 临时 session。
2. private chat transcript 仍保留 `${descriptor.SessionId}:private_chat:{conversationId}` 粒度。
3. autonomy tick 能看到 private chat 创建的 active todo。
4. autonomy 推进任务时使用 Stardew 工具，而不是只输出叙事文本。
5. 工具返回 terminal `blocked` / `failed` 时，Agent 更新 todo reason，并尽可能 `stardew_speak` 或 private chat 告知玩家。

验收依据：

- `TodoToolTests`
- `SessionTaskProjectionService` 相关测试
- `StardewNpcPrivateChatAgentRunner` / private chat continuity 测试
- `NpcAutonomyLoop` / `StardewNpcAutonomyBackgroundService` 测试
- runtime activity JSONL 和 NPC runtime workspace task summary

### P1：move 契约收敛已完成；后续做 Bridge 真实跨地图执行

已完成目标：让移动公开面成为 destination-level command，而不是 observation fact 坐标回填。

已完成：

1. `StardewMoveTool` 参数和内部实现只向 command layer 提交 `destinationId` 与 reason。
2. `GameActionTarget` 不再要求 Agent 侧提供真实执行 tile；必要时使用 placeholder/debug target，但执行真相来自 Bridge registry。
3. Query 投影继续暴露 label/reason/tags，但不得成为执行真相。
4. `nearby[n]` 只保留短距离上下文，不再作为 `stardew_move` 的替代输入。

后续剩余：

1. Bridge executor 按 `destinationId` resolve arrival policy、fallback policy、route plan、phase、terminal status。
2. 扩大 `BridgeDestinationRegistry` 和 endpoint projection，支持房间、室外、入口点、可停留点。
3. 实现真实跨地图移动；跨图失败必须返回明确 terminal status，不能使用 `warpCharacter` 或 `PathFindController` 伪完成。
4. 真实游戏中验证 Haley/Penny 到 bedroom mirror、living room、town fountain 等目的地的状态链路。

验收依据：

- `StardewCommandServiceTests`
- `StardewNpcToolFactoryTests`
- `StardewQueryServiceTests`
- `BridgeMoveCommandQueueRegressionTests`
- Architect verification：2026-05-05 已通过 `destinationId` 公开契约收敛验收。
- 后续跨地图执行还需要真实游戏中 Haley/Penny 到 bedroom mirror、living room、town fountain 等目的地的手测日志。

### P2：扩大 Stardew skill 的生活知识层

目标：让 Agent 有足够游戏知识做合理选择，而不是靠宿主硬编码。

应优先补强：

- `stardew-navigation`：目的地选择、失败后重新观察、不要重复坏目标。
- `stardew-task-continuity`：承诺拆分、等待条件、blocked/failed reason 风格。
- `stardew-world`：地点意义、时间段、季节、节日、公共区域、NPC 常见行为。
- `stardew-social`：群聊、私聊、偷听、礼貌边界、关系记忆。

禁止方向：

- 宿主关键词检测玩家承诺；
- 宿主自动生成 NPC 失败台词；
- 宿主维护 `SOUL.md` / `MEMORY.md` 等人格或记忆摘要；
- 新增第二套 NPC task store。

### P3：完善 runtime workspace 的可观察性

目标：让“看起来对但没生效”的问题能被定位。

应补充：

- 每 NPC active todo 明细；
- task archive；
- 最近 failed/blocked reason；
- 对应 traceId / commandId；
- action slot / pending work item 的最近状态变化；
- private chat lease 和 reopen 状态；
- bridge unavailable / stale discovery / menu blocked 等环境问题。

UI 只读展示，所有状态来自 runtime supervisor、task projection、transcript、activity、bridge status。

### P4：再推进多 NPC 社交和村庄模式

在 P0-P3 没稳定前，不建议先做复杂群聊和经济系统。

后续可做：

- 玩家在场群聊；
- NPC 之间 overhear；
- 群聊消息路由；
- 多 NPC action slot / resource claim；
- 场景内并发预算；
- 跨天任务恢复；
- 日程和地点偏好。

## 近期不应优先做的事

- 不应新建规则引擎来识别“明天”“海边”“帮我”等关键词。
- 不应让 Bridge 或 Desktop 替 NPC 判断是否接受玩家请求。
- 不应新增 `NpcTaskStore`、`PromiseStore`、`StardewTaskStore`。
- 不应把 UI 做成第二任务真相。
- 不应机械照搬 Minecraft 的 `mc` prompt 或命令名。
- 不应为了演示快速扩大 NPC 数量，当前更需要一个 NPC 的完整闭环。
- 不应继续让移动退回坐标级补步和 LLM 微操。

## 最终结论

这份文档的早期版本把项目描述成“已有骨架但 NPC 还没真正自主生活”。截至 2026-05-05，这个判断需要更新：

当前 Hermes Desktop 已经具备 Stardew NPC 长期 runtime 的关键基础设施，尤其是 NPC namespace、persona pack、autonomy loop、private chat、`ToolSessionId`、扩展后的 `todo`、Bridge command/status、destination registry 和 runtime workspace。

下一步最有价值的工作不是继续铺新模块，而是把这些已出现的模块闭环：

1. **任务连续性闭环**：私聊承诺进入长期 `todo`，autonomy 能继续推进。
2. **移动可靠性闭环**：Agent 只选 destination，Bridge 负责执行到终态或明确失败。
3. **失败反馈闭环**：blocked/failed 有 reason、有 trace、有玩家反馈或反馈阻塞原因。
4. **可观察性闭环**：桌面 runtime workspace 能看到 task、trace、action、pending、error 的同一条证据链。

只有这四个闭环稳定后，再扩大到多 NPC 群聊、跨天日程和完整村庄生活，才不会把早期架构债务放大。
