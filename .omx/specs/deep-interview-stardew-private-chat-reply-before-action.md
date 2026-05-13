# Deep Interview Spec: Stardew 私聊先回复再行动

## Metadata
- Profile: standard
- Rounds: 4
- Final ambiguity: 0.18
- Threshold: 0.20
- Context type: brownfield
- Context snapshot: `.omx/context/stardew-private-chat-reply-before-action-20260513T061017Z.md`
- Interview transcript: `.omx/interviews/stardew-private-chat-reply-before-action-20260513T062850Z.md`

## Intent
修复 Stardew 私聊体验：玩家让 NPC 现在做事时，NPC 不能沉默后直接行动。玩家必须先看到 NPC 作为角色本人的自然回复，然后行动再开始。

## Desired Outcome
私聊 accepted host task 的顺序固定为：

1. 父层 NPC agent 通过可见工具处理承诺、记忆、`todo` 和 `stardew_submit_host_task`。
2. 父层 NPC agent 输出非空、自然、玩家可见的回复。
3. 宿主通过 private-chat reply UI 显示该回复。
4. 玩家关闭/ dismiss 回复框后，宿主才允许对应 host task 进入执行。
5. 执行完成、失败、blocked 或 timeout 作为 terminal fact 回到 agent。

## In Scope
- 私聊 `stardew_submit_host_task` 成功提交后，强制要求非空最终回复。
- 空最终回复通过 bounded parent self-check 修复，保持 agent-native。
- 所有私聊触发的 `stardew_submit_host_task` 都必须等待 `private_chat_reply_closed`，不只 move。
- 更新或新增测试，覆盖空回复、等待关闭、关闭后执行、显示失败/超时不执行。
- 保留/增强可观测日志，便于手测确认顺序。

## Out of Scope / Non-goals
- 不引入 `local_executor`、小模型 gameplay 执行层、隐藏 fallback 或双轨实现。
- 不让宿主解析自然语言玩家请求或 JSON 文本来推断动作。
- 不硬编码地点、路径、NPC 台词或兜底回复。
- 不让宿主自动写入、关闭或推断 agent-owned `todo`。
- 不重做私聊 UI 或手机 UI。

## Decision Boundaries
- 可以修改现有 `private_chat_reply_displayed` 后立即执行的测试期望。
- 可以将私聊 host-task ingress gate 改为 `private_chat_reply_closed`。
- 可以新增 bounded agent-native self-check，只让父层 agent 补回复，不重复提交 host task。
- 不可以由宿主合成 NPC 台词或把失败工具调用当成功提交。

## Constraints
- Stardew v1 只允许一条实现路径：visible parent tool call -> visible tool result -> host/bridge task runner -> terminal fact。
- 私聊与 autonomy 仍使用同一个 NPC runtime / Hermes-native tool surface。
- `stardew_submit_host_task` 的 target、动作参数仍必须来自 agent 读取 skill/reference 后显式提交，宿主不补齐。
- 参考项目对齐的是 agent-native 交互原则，不照搬 Minecraft 非阻塞 chat 的 UI 行为。

## Acceptance Criteria
- AC1: 当私聊父层 agent 成功调用 `stardew_submit_host_task` 且最终回复非空时，host task 在 `private_chat_reply_closed` 前保持 deferred，不执行。
- AC2: 收到匹配 conversation id 的 `private_chat_reply_closed` 后，host task 才进入现有 host/bridge task runner 执行路径。
- AC3: 当成功调用 `stardew_submit_host_task` 后最终回复为空时，runner 追加一次 bounded parent self-check，要求 agent 只补自然回复，不重复提交 host task。
- AC4: 如果 self-check 仍无非空回复，host task 不执行，记录 blocked/terminal fact 供 agent 后续解释或重试。
- AC5: 如果回复显示失败或关闭事件超时，host task 不执行，记录 deferred/blocked/terminal fact。
- AC6: 没有 `local_executor`、隐藏 fallback、宿主合成回复、自然语言动作解析或地点硬编码。
- AC7: 测试证明 private-chat host task 覆盖所有 action 类型，不只 move。

## Brownfield Evidence
- `src/game/core/PrivateChatOrchestrator.cs`: 空回复当前会直接结束私聊，不提交 `Speak`。
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`: 私聊父层 agent 当前负责 `todo`、`stardew_submit_host_task` 和最终回复。
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`: 当前仅对 move 等待 `private_chat_reply_displayed` 或 `private_chat_reply_closed`。
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`: 存在 reply displayed 后立即执行的测试期望，需要更新。

## Assumptions Resolved
- 用户确认要等玩家关闭回复框，不只是显示回复。
- 用户确认空回复修复必须 agent-native。
- 用户确认范围是所有私聊触发的 host task。
- 用户确认参考项目映射后，失败/超时应作为事实回 agent，不应隐藏执行。

## Recommended Handoff
进入 `$ralplan`：用本规格生成 OpenSpec PRD 与测试规范，然后再进入 `$autopilot` 实施。
