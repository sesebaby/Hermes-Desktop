# Stardew NPC 本地执行层最小验证方案

## 背景

本记录用于固定当前讨论共识，避免后续实现时偏离目标。

当前项目已完成 LLM lane 路由：`stardew_autonomy`、`stardew_private_chat`、`delegation` 可以使用不同模型配置，`AgentTool` 可使用 `delegation` lane。但这一步只解决“子 agent 用哪个模型”，尚未解决“哪些动作必须交给本地小模型执行”。

用户的核心目标不是单纯增加 subagent，而是让 Stardew 小镇更像活的真实小镇，同时把云模型成本压到可长期运行的范围。

## 用户核心需求

1. NPC 真人化：目标是“活的星露谷小镇”，NPC 应该有长期意图、人格一致性、自然反应和可持续任务推进。
2. 低成本：期望每天约 3 小时运行成本最好不超过 3 元。
3. 主 agent 上下文必须尽量短，不能把移动、工具 schema、路径细节、简单动作执行都塞给主 agent。
4. 能交给本地小模型的中间操作应尽量交给本地小模型，包括移动、观察、等待、工具参数准备和简单 task 执行。
5. 不希望只靠提示词让主 agent 自愿调用子 agent；低风险动作应由代码层硬路由到本地执行层。

## 成本假设

DeepSeek 价格按用户提供截图记录：

- 百万 tokens 输入，缓存命中：0.02 元
- 百万 tokens 输入，缓存未命中：1 元
- 百万 tokens 输出：2 元

按 50% 输入缓存估算：

- 输入约 `0.51 元 / 百万 tokens`
- 输出约 `2 元 / 百万 tokens`

结论：

- 输出 token 比输入更贵。
- 大上下文和频繁云模型调用都会破坏成本目标。
- 高频 poll 不应等于高频云 LLM 决策。
- 本地小模型应承担操作级推理和工具参数生成。

## 分层共识

### 主 Agent

主 agent 只负责高层心智：

- 长期意图
- 任务分解
- 人格一致性
- 和玩家/世界相关的高层决策
- 何时改变计划、放弃任务、回应玩家、形成新承诺

主 agent 不应频繁处理：

- 地图细节
- 工具 schema
- `stardew_move` 参数
- `observe` / `wait` / `task_status` 等简单操作
- 已分解小任务的逐步执行

### 本地执行层

本地执行层暂定命名为 `LocalNpcExecutor` 或 `NpcActionExecutor`。

它不是第二个 NPC 大脑，而是执行主 agent 意图合同的本地行动层。

它负责：

- 将主 agent 的高层任务拆成下一步可执行动作
- `move`
- `observe`
- `wait`
- `task_status`
- 简单 todo step 推进
- 路径失败后的低风险重试
- 工具参数生成
- 将执行结果压缩成短摘要

它不负责：

- 改写 NPC 人格
- 决定长期目标
- 生成新承诺
- 代表 NPC 做关系、礼物、交易、重要剧情类决策

### 宿主 / 游戏执行器

宿主负责：

- 校验动作是否合法
- 调用真实 Stardew 工具
- 防止越权
- 记录 trace / runtime log
- 处理超时、重试、失败归因
- 将结果压缩反馈给主 agent 或本地执行层

宿主不能替 NPC 做人格决策，也不能绕过 NPC 意图直接安排长期行为。

## 路由策略

采用混合策略，但分发规则必须主要由代码层控制。

### Hard Routing

以下低风险动作在 v1 中应硬路由到本地执行层，而不是让主 agent 自愿调用：

- `move`
- `observe`
- `wait`
- `task_status`
- 简单 todo step 推进
- 路径失败后的低风险重试

原因：

- 保证主 agent 上下文短。
- 保证成本可控。
- 避免主 agent 又开始直接处理工具细节。
- 避免“提示词说要委派，但模型没委派”的不稳定行为。

### Policy Routing

以下动作默认需要升级回主 agent 或由主 agent 批准：

- 使用物品
- 送礼
- 交易
- 涉及玩家承诺的对话
- 影响关系或长期记忆的行为
- 修改长期 todo 的重要状态
- 多次失败后的计划变更
- 发现重要世界事件

### Prompt Guidance

提示词只用于约束角色边界，不负责架构分发。

主 agent prompt 应强调：

- 只输出高层意图和约束。
- 不处理低层工具参数。
- 不把行动细节写成叙事假动作。

本地执行层 prompt 应强调：

- 你只是执行层。
- 不重写 NPC 人格和长期目标。
- 不扩大授权范围。
- 只能在意图合同允许的动作内选择下一步。

## 意图合同

主 agent 输出应收敛为短的 `intent contract`。

示例：

```json
{
  "goal": "去 Pierre 店附近等玩家",
  "reason": "玩家刚才说可能会去买种子",
  "persona_constraints": [
    "自然",
    "不要像执行命令",
    "如果遇到熟人可以礼貌打招呼"
  ],
  "priority": "medium",
  "allowed_actions": [
    "move",
    "observe",
    "wait"
  ],
  "stop_conditions": [
    "玩家发来私聊",
    "路径失败两次",
    "到达目标地点",
    "发现重要事件"
  ]
}
```

本地执行层输入该合同和短观察事实，输出结构化 action intent。

示例：

```json
{
  "action": "move",
  "destination": "PierreShop",
  "reason": "玩家可能会去买种子，先到附近等待",
  "confidence": 0.82,
  "fallback": "如果路径失败，重新 observe 并选择 Town 作为附近等待点"
}
```

## 最小验证方案

v1 不做完整小镇、多 NPC 社交或复杂经济系统，只验证架构是否可行。

### 范围

只开放本地执行层处理：

- `move`
- `observe`
- `wait`
- `task_status`

暂不开放：

- `use item`
- `gift`
- `trade`
- 重要对话承诺
- 关系变化决策
- 长期记忆写入决策

### 行为目标

当 NPC 有一个高层目标需要移动时：

1. 主 agent 不直接调用 `stardew_move`。
2. 主 agent 输出短 `intent contract`。
3. 宿主识别其中的低风险动作需求。
4. 宿主硬路由到本地执行层。
5. 本地执行层输出结构化 `move` action intent。
6. 宿主校验并调用 `stardew_move`。
7. 执行结果被压缩写入 runtime log，并只把摘要反馈给主 agent。

### 成功标准

- 主 agent prompt / turn input 明显短于旧方案。
- `stardew_move` 由本地执行层触发，而不是主 agent 直接调用。
- 本地执行层使用 `delegation` lane 或本地模型配置。
- 主 agent transcript 不包含冗长路径规划和工具参数推理。
- runtime log 能看到 `intent_contract -> local_executor -> stardew_move -> status/result` 证据链。
- 移动失败时，本地执行层最多做低风险重试；超过阈值升级回主 agent。

## 测试建议

### 单元测试

- 给定主 agent 输出的 `intent contract`，确认 `move/observe/wait/task_status` 被硬路由到本地执行层。
- 确认 `gift/trade/use item/relationship-impacting` 不会被本地执行层直接执行。
- 确认本地执行层使用 `delegation` lane。
- 确认本地执行层输出非法 action 时，宿主拒绝执行并记录错误。

### 集成测试

- 模拟 autonomy tick：主 agent 返回 `intent contract`，本地 executor 返回 `move` intent，最终调用 `stardew_move`。
- 验证主 agent session 中没有直接 `stardew_move` tool call。
- 验证 runtime log 中有完整链路。
- 验证失败两次后升级回主 agent。

### 手测

- 开启本地 OpenAI-compatible endpoint，例如 LM Studio。
- 配置 `delegation.base_url` 和本地模型。
- 让一个 NPC 接到“去某处等玩家”的简单任务。
- 观察本地模型 endpoint 是否收到执行层请求。
- 检查 Hermes runtime log 和 SMAPI bridge log 是否出现移动执行证据。

## 风险

1. 本地执行层权限过大，会偷走 NPC 的人格决策。
2. 如果每个 tick 都调用本地模型，虽然不花云 API 钱，但仍可能带来延迟和资源占用。
3. 如果主 agent 输出的意图合同不稳定，执行层会收到含糊目标。
4. 如果宿主硬路由过多动作，NPC 会变成脚本系统。
5. 如果只靠提示词委派，主上下文和成本目标会失控。

## 当前结论

最小可行方向是：

`主 Agent 低频高层决策 -> 本地执行层高频低层执行 -> 宿主校验和真实工具调用 -> 摘要反馈`

下一步实现不应继续扩展泛化 `AgentTool`，而应先做一个明确的 `LocalNpcExecutor` / `NpcActionExecutor` v1，用代码层硬路由低风险动作，并用日志和测试证明 `move` 已经不由主 agent 直接处理。
