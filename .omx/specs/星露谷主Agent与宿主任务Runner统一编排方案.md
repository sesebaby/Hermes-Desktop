# 星露谷主 Agent 与宿主任务 Runner 统一编排方案

## 元信息

- 文档类型：深访后的执行规格
- 当前结论：Stardew v1 彻底移除小模型 gameplay 执行层
- 上下文快照：`.omx/context/stardew-host-task-runner-reference-alignment-20260512T131149Z.md`
- 替代旧方案：此前“主 agent + 小模型执行单”的方向已废弃
- 参考项目：
  - `external/hermescraft-main`
  - `external/hermes-agent-main`

## 一句话结论

Stardew v1 不做“小模型执行层”。统一编排改成：

`主 agent 调可见工具 -> 宿主/bridge 创建任务 -> 宿主机械执行 -> 状态/超时/watchdog/事实回传 -> 主 agent 继续决策`

主 agent 是脑子，宿主/bridge 是身体。小模型不进入 gameplay 执行路径。

## 目标

降低 Stardew NPC 编排复杂度，对齐参考项目的任务执行方式。

系统只保留一条主路径：

1. 主 agent 负责判断和表达。
2. 宿主/bridge 负责真实机械执行。
3. 任务状态、失败、超时、卡住、完成都通过统一事实回给主 agent。

不再保留小模型作为第二执行者、窗口操作者、移动执行者或隐藏 fallback。

## 分工边界

### 主 agent 负责

- 要不要行动
- 行动目标是什么
- 为什么这么做
- 要不要说话
- 具体说什么
- 社交时机和关系判断
- 长期承诺和 `todo` 收口
- 任务完成后下一步怎么做

### 宿主 / bridge task runner 负责

- 移动
- 说话投递
- 微动作
- 私聊窗口和输入框生命周期
- 后续制造、交易、任务、采集窗口
- UI lease / active menu 安全
- `taskId` / `workItemId`
- 状态查询
- 超时、取消、watchdog
- 写入 runtime 事实和日志
- 唤醒主 agent

### 明确不保留

- 小模型执行 gameplay 动作
- 小模型操作窗口
- 小模型移动 NPC
- 小模型投递说话
- 小模型改 `todo`
- 小模型做私聊即时行动闭环
- host 从自由文本推断动作
- hidden executor fallback
- 第二 tool lane / 第二执行链路

## 核心架构

核心抽象不是“小模型执行单”，而是 **宿主任务 / work item**。

标准流程：

1. 主 agent 调用模型可见工具。
2. runtime 创建稳定的宿主任务。
3. 宿主/bridge 执行真实机械动作。
4. runtime 可查询任务状态。
5. watchdog 把长期 running 或卡住状态转成 terminal fact。
6. terminal fact 写入 runtime。
7. 主 agent 在下一轮看到事实，再决定下一步。

这更接近 `external/hermescraft-main` 的：

- `commandQueue`
- `currentTask`
- `task_id`
- `briefState()`
- stuck watchdog

也对齐 `external/hermes-agent-main` 的工具事件原则：工具开始/完成要靠稳定 ID 配对，不能靠工具名猜。

## 任务契约

宿主任务至少需要这些字段：

- `taskId` / `workItemId`
- `traceId`
- `npcId`
- `gameId`
- `sessionId`
- `source`：`autonomy`、`private_chat`、`scheduled`、`mcp`、`debug`
- `action`：`move`、`speak`、`idle_micro_action`、`open_private_chat`、`craft`、`trade`、`quest`、`gather`、`wait`、`status`
- `parameters`：机械参数，只描述怎么执行，不承载人格判断
- `authoredSpeech`：主 agent 已写好的台词，宿主只负责投递
- `idempotencyKey`
- `status`：`queued`、`running`、`completed`、`blocked`、`failed`、`cancelled`、`timeout`、`stuck`
- `startedAtUtc`
- `updatedAtUtc`
- `timeoutAtUtc`
- `resourceClaims`：NPC action slot、UI lease、目标资源或窗口
- `terminalFact`：事实结果、原因码、错误码、最终 command id

宿主任务不是长期 `todo`。
`todo` 是主 agent 的生活连续性层，宿主任务只是机械执行记录。

## 窗口类动作要求

制造、交易、任务、采集窗口都必须走宿主任务，不允许引入小模型窗口执行器。

窗口任务统一流程：

1. 打开或确认目标窗口。
2. 获取 UI lease。
3. 执行有限、明确的机械步骤。
4. 验证可观察状态变化。
5. 关闭或释放自己持有的窗口/lease。
6. 返回 `completed` / `blocked` / `failed` 和原因。

必须能处理这些 blocked 场景：

- 已有其他菜单占用 UI
- 私聊窗口占用 UI
- 目标物品、配方、任务选项不存在
- 材料、金钱、背包空间不足
- active menu 与预期不一致
- timeout
- replay 会导致重复提交

## Harness 要求

harness 要证明的是 **宿主任务生命周期**，不是小模型协议。

### Harness 分层

- 主 agent harness：fake agent 发出模型可见工具调用和已写好的台词。
- 宿主任务 harness：创建、推进、完成 durable work item。
- bridge/action harness：fake bridge command service，支持脚本化状态序列。
- UI/window harness：模拟 active menu、lease、关闭、取消、冲突。
- 持久化 harness：验证 state snapshot、replay、idempotency。
- transcript/runtime harness：验证 tool call、task fact、日志、wake prompt、ID 关联。

### 必测场景

1. `move`：tool call -> host task -> bridge command -> completed fact -> 主 agent 看到 `last_action_result`。
2. `speak`：主 agent 写好台词 -> 宿主原样投递 -> terminal fact。
3. `idle_micro_action`：指定 kind -> 宿主执行 -> terminal fact。
4. `open_private_chat`：UI lease 和菜单生命周期可追踪并释放。
5. `craft`：打开制造 UI -> 执行有限操作 -> 验证物品变化 -> 关闭。
6. `trade`：打开交易 UI -> 买/卖限定物品和数量 -> 验证金钱/物品变化 -> 关闭。
7. `quest`：打开/操作任务 UI -> 验证任务状态 -> 关闭。
8. `gather`：观察资源 -> 交互 -> 验证背包或世界结果。
9. UI 冲突：active menu 或 private chat lease 已存在时 blocked，不覆盖。
10. timeout/stuck：running 任务必须转成 `timeout` 或 `stuck`。
11. malformed/unsupported task：返回 blocked fact，不创建 action slot。
12. 重复同名工具调用：tool call id、task id、command id 不错配。
13. restart/replay：in-flight task 不重复提交已提交命令。
14. 私聊即时行动：主 agent 写/更新承诺，提交 host task，之后根据 terminal fact 收口 `todo`。
15. MCP/native parity：MCP 和 native wrapper 产生同一类 host task fact，不走另一套逻辑。
16. 负向门禁：Stardew v1 gameplay 路径不能调用小模型执行器。

### Harness 证据

每个场景至少断言：

- tool call id
- `taskId` / `workItemId`
- `traceId`
- host command id
- task 状态变化
- terminal fact 形状
- runtime jsonl 记录
- wake reason
- action slot / UI lease 已清理
- 没有 hidden fallback
- 没有小模型执行 lane

## 参考项目对齐

### 借鉴 `external/hermescraft-main`

- 玩家/agent 命令进入 queue 或 background task。
- 长动作返回 task id。
- task status 独立查询。
- 每次响应可以带 brief state。
- watchdog 把 stuck/running forever 转成 terminal task state。
- AI 做决策，bridge 做机械执行。

### 不照搬 `external/hermescraft-main`

- 不照搬 Minecraft action 列表。
- 不照搬单个全局 `currentTask`，Stardew 可能需要每 NPC / 每资源槽。
- 不绕过 Hermes/NPC 边界直接假设世界状态。

### 借鉴 `external/hermes-agent-main`

- tool start / complete 用稳定 ID 关联。
- 重复同名 tool call 不能靠名字配对。
- UI/progress 证据必须 keyed by ID，不能靠工具名猜。

### 不照搬 `external/hermes-agent-main`

- 不照搬 ACP/editor transport 细节。

## 当前代码依据

- `StardewNpcAutonomyBackgroundService` 已经按 pending action -> ingress -> private chat lease -> cooldown -> LLM turn 处理。
- `ActionSlot` 和 `PendingWorkItem` 已经保护真实机械动作。
- `LastTerminalCommandStatus` 已经把 terminal fact 带到下一轮 autonomy。
- `IngressWorkItems` 已经表示 queued private-chat / scheduled work。
- `NpcRuntimeStateStore` 已经持久化 pending work、action slot、ingress、terminal status。
- 现有 MCP 测试已经覆盖 move、speak、open chat、idle action 的 terminal feedback。

这些更接近 HermesCraft 的 `currentTask`，不是模型持有的执行循环。

## 验收标准

1. Stardew v1 gameplay 动作不依赖小模型执行层。
2. 主 agent 的可见工具调用足以创建 host task。
3. host task runner 负责机械推进、状态、超时、取消、watchdog、terminal fact。
4. 主 agent 负责台词、社交判断、长期 `todo` 连续性。
5. private chat、scheduled ingress、autonomy、MCP、native tools 收敛到同一 host task lifecycle。
6. 后续窗口类能力通过扩展 host task action 增加，不增加模型编排层。
7. harness 不启动 Stardew/SMAPI 也能证明 task id、status、fact、wake。
8. harness 证明没有 hidden fallback，没有 model-in-the-middle。

## 后续 `$ralplan` 需要产出

1. 统一 host task runner contract 的 PRD。
2. host task lifecycle 和 UI/window harness 的 test spec。
3. 从 Stardew v1 gameplay 路径移除 `NpcLocalExecutorRunner` 的迁移计划。
4. `npc_delegate_action` 的退役方案：删除或替换成更直白的 host task tool，不能保留 `delegate` 语义入口。
5. 日志与 ID 关联契约。
6. 防止重新引入小模型 gameplay execution 的硬门禁。

## 推荐交接

下一步用这份文档进入 `$ralplan`：

`$plan --consensus --direct .omx/specs/星露谷主Agent与宿主任务Runner统一编排方案.md`
