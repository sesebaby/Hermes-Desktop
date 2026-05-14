# 00-星露谷私聊自然回复合法终态设计

## 00-背景与现状

当前 `StardewNpcPrivateChatAgentRunner` 的私聊热路径会在父层 NPC agent 没有成功调用 `stardew_submit_host_task`，也没有成功调用 `npc_no_world_action` 时，追加一次 delegation self-check。这条路径原本用于避免 agent 口头答应即时行动却没有提交 host task，但它也会覆盖普通闲聊、拒绝、解释、澄清、记忆等合法私聊回复。

现在项目边界已经收紧：宿主不能判断玩家这句话是不是要求 NPC 做事，也不能从 NPC 自然语言里猜行动意图。私聊的自然回复是玩家可见结果，不等同于后台 autonomy 的无人可见空转。真正能改变游戏世界的只有模型可见工具调用成功后的 host task。

## 01-目标 / 非目标

目标：

- 普通私聊自然回复在没有成功世界动作工具调用时，成为合法终态，只显示给玩家。
- 移除“缺少 `npc_no_world_action` 就追加第二次 LLM”的私聊热路径。
- 保留成功 `stardew_submit_host_task` 后空回复的 bounded 补回复 self-check，保证行动前玩家能看到 NPC 回复。
- 让 prompt 明确告诉 agent：不调用工具就只能说话，不能承诺即时世界动作。
- 用回归测试证明普通私聊降为单次父层 LLM 调用，并且不会提交 host task。

非目标：

- 不改变后台 autonomy 的无工具自然语言处理策略。
- 不引入文本意图分类器、隐藏 executor、小模型 gameplay runner 或第二工具 lane。
- 不让宿主合成 NPC 台词、补写 todo、推断地点或提交动作。
- 不重做私聊 UI、桥接层、host task runner、memory、soul 或 transcript 主链。
- 不把 `npc_no_world_action` 删除；它仍可作为明确无世界动作的诊断/协议工具。

## 02-关键决策

### 02.1-无工具自然回复在私聊中直接完成

决策：`ShouldRunDelegationSelfCheck` 代表的二次父层 self-check 不再用于私聊自然回复。若本轮没有成功 `stardew_submit_host_task` 或 `npc_no_world_action`，runner 直接返回最终文本，由私聊 UI 展示。

理由：这符合 `stardew-ui-task-lifecycle` 中“没有 world-action tool call 时只展示回复”的契约，也避免宿主用第二轮提示逼 agent 重新判断玩家意图。

拒绝方案：继续强制 `npc_no_world_action`。拒绝原因是它让普通聊天也可能支付第二次 LLM 成本，并把可选诊断工具变成热路径硬门槛。

### 02.2-世界动作仍只认成功工具结果

决策：真实世界动作只来自成功的 `stardew_submit_host_task` 工具结果。工具调用请求存在但执行失败，不算提交成功；自然语言、JSON-like 文本或“我马上过去”类承诺都不能触发动作。

理由：项目已经记录过工具调用“出现过”不等于成功提交，必须检查工具结果。这个变更只放宽无工具自然回复，不放宽世界动作入口。

拒绝方案：从回复文本识别“我马上过去”并补提交动作。拒绝原因是这会重新引入宿主替 agent 决策和隐藏 executor。

### 02.3-保留成功 host task 后的空回复 self-check

决策：如果 `stardew_submit_host_task` 已成功提交，但 final reply 为空，仍追加一次 bounded reply self-check，只要求 agent 补一条玩家可见回复，不重复提交 host task。

理由：这是上一轮“私聊先回复再行动”规格的核心体验边界。这里的 self-check 不是判断是否要行动，而是在已成功提交动作后补齐玩家可见回复。

拒绝方案：删除所有 self-check。拒绝原因是会恢复“NPC 沉默后直接行动”的体验回归。

### 02.4-Prompt 约束改为工具契约优先

决策：私聊 prompt 不再说“没有即时动作必须调用 `npc_no_world_action`”作为硬要求，而是改成：

- 要即时改变游戏世界，必须调用 `stardew_submit_host_task`；
- 不调用世界动作工具，就只能自然说话，不能承诺即时动作；
- `npc_no_world_action` 是推荐的明确收口/诊断工具。

理由：prompt 仍要训练 agent 使用明确工具，但不能把工具纪律变成宿主触发第二轮 LLM 的理由。

拒绝方案：完全弱化 prompt，不提 `npc_no_world_action`。拒绝原因是会降低日志可读性和协议完整性。

## 03-风险与取舍

- 风险：agent 可能口头说“我马上过去”但没有调用 `stardew_submit_host_task`。缓解：prompt 明确禁止无工具承诺即时动作；测试覆盖无工具承诺不会执行，手测日志可诊断。
- 风险：无工具自然回复少了 `npc_no_world_action` 的结构化记录。缓解：允许保留诊断日志，但诊断不能驱动决策。
- 风险：部分旧测试依赖 delegation self-check。缓解：更新测试期望，证明新契约下单次自然回复才是正确行为。
- 风险：把私聊边界误应用到后台 autonomy。缓解：spec 明确私聊自然回复是玩家可见终态，后台 autonomy 无工具自然语言仍按无进展处理。

## 04-迁移计划

1. 更新私聊 prompt，把 `npc_no_world_action` 从硬门槛改成推荐诊断工具，并加入“无工具不能承诺即时动作”的硬约束。
2. 删除或停用 delegation self-check 的调用路径，保留 reply self-check 和成功工具结果判定。
3. 更新 `StardewNpcPrivateChatAgentRunnerTests` 中无工具、非动作工具、失败 no-world 等测试期望。
4. 增加或调整测试，证明普通自然回复只调用一次父层 LLM，不提交 host task。
5. 跑窄测试，再按需要跑 Stardew 私聊相关测试集合。

回滚策略：如果手测发现普通私聊无法稳定给出可展示回复，可以只回滚 prompt 文案；不得回滚到宿主文本推断动作或隐藏 executor。

## 05-开放问题

无阻塞开放问题。实施时可选择是否增加一条无工具自然回复诊断日志；如果增加，必须保持为观测信息，不参与动作决策。
