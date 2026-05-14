# 00-星露谷私聊自然回复合法终态提案

## 00-为什么要改

当前 Stardew 私聊里，父层 NPC agent 如果只给玩家自然回复、没有成功调用 `stardew_submit_host_task` 或 `npc_no_world_action`，宿主会追加一次 parent self-check，再跑一轮 LLM。这让普通私聊体感变慢，也把“缺少工具调用”错误地升级成宿主驱动的二次决策。

这个变更需要现在做，因为项目边界已经明确：宿主不能判断玩家这句话是不是要 NPC 做事，也不能从模型自然语言里猜行动意图。私聊中的自然回复本身就是玩家可见产物；只有模型可见工具调用成功，才代表真实世界动作或明确无世界动作。

## 01-变更内容

- 将“无工具自然回复”定义为 Stardew 私聊里的合法终态：
  - 如果 agent 成功调用 `stardew_submit_host_task`，宿主按现有 host task lifecycle 执行真实世界动作；
  - 如果 agent 成功调用 `npc_no_world_action`，宿主记录本轮明确无世界动作；
  - 如果两者都没有成功调用，宿主只把自然回复展示给玩家，不追加第二轮 LLM，不推断动作。
- 删除或停用私聊热路径上的 delegation self-check：
  - 不再因为缺少 `npc_no_world_action` 而追加 parent self-check；
  - 不再让“普通聊天、拒绝、解释、闲聊、记忆、澄清”等有效私聊回复支付第二次模型请求成本。
- 保留“成功提交世界动作但空回复”的补回复 self-check：
  - 如果 `stardew_submit_host_task` 已成功入队，但最终回复为空，仍允许一次 bounded parent self-check，只补玩家可见自然回复；
  - self-check 不得重复提交 host task，不得由宿主合成 NPC 台词。
- 收紧私聊 prompt 和工具契约：
  - 要即时改变游戏世界，必须调用 `stardew_submit_host_task`；
  - 不调用世界动作工具时，只能自然说话，不能承诺“我马上过去”这类即时动作；
  - `npc_no_world_action` 保留为可用的明确收口/诊断工具，但不再是触发第二轮 LLM 的硬门槛。
- 区分私聊和后台 autonomy：
  - 私聊的自然回复是玩家可见结果；
  - 后台 autonomy 的无工具自然语言不应被当成有效世界进展，应继续按诊断、冷却或降频处理，避免无人可见的 token 空转。

本次变更明确不做：

- 不引入 `local_executor`、小模型 gameplay 执行层、隐藏 fallback 或第二工具 lane。
- 不让宿主解析玩家自然语言、NPC 回复文本或 JSON-like 文本来判断动作意图。
- 不让宿主自动写入、关闭或推断 agent-owned `todo`。
- 不硬编码地点、路径、NPC 台词或兜底回复。
- 不重做私聊 UI、手机 UI、NPC 记忆系统或 Stardew host task runner。
- 不改变后台 autonomy 的“无工具自然语言不算世界进展”边界。

## 02-能力影响

### 02.1-新增能力

无。本次不是新增平行能力，而是收紧既有 Stardew 私聊与 host task 边界。

### 02.2-修改的能力

- `stardew-ui-task-lifecycle`：私聊响应中没有成功世界动作工具调用时，宿主必须只展示自然回复，不创建 movement、todo、UI action，也不追加二次 LLM 来替 agent 判断是否漏掉动作。
- `stardew-host-task-runner`：继续保持“真实世界动作只能来自成功的模型可见 host task 工具调用”；自然语言或 JSON-like 文本不能触发游戏执行。成功提交 host task 但缺玩家可见回复时，仍允许 bounded agent-native 补回复 self-check。
- `stardew-orchestration-harness`：新增或更新回归用例，证明普通私聊自然回复只发生一次父层 LLM 调用，且不会提交 host task；成功 host task 空回复仍只补回复，不重复提交动作。

能力复用矩阵：

- 私聊 UI 生命周期仍归 `stardew-ui-task-lifecycle` 管，不新增第二套私聊协议。
- 世界动作执行仍归 `stardew-host-task-runner` 管，不绕过现有 ingress/action-slot/bridge terminal fact 主链。
- 回归证明仍归 `stardew-orchestration-harness` 管，不新增独立测试体系。

## 03-影响范围

- 代码：
  - `src/games/stardew/StardewPrivateChatOrchestrator.cs`
  - 必要时仅做窄改动，不触碰无关 runtime、memory、todo 或 bridge 主链。
- Prompt / 文案：
  - `StardewNpcPrivateChatAgentRunner` 构造的私聊 system prompt。
- 测试：
  - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
  - 必要时补充 `StardewPrivateChatOrchestrator` 或 harness 层测试。
- Specs：
  - `stardew-ui-task-lifecycle` 的 delta spec。
  - `stardew-host-task-runner` 的 delta spec。
  - `stardew-orchestration-harness` 的 delta spec。
- 日志 / 可观测性：
  - 可保留无工具自然回复的诊断日志，便于手测确认单次 LLM 回复路径。
  - 不把诊断日志升级成决策逻辑。
- 依赖：
  - 不新增运行时依赖。
  - 不新增外部服务或新模型 lane。
