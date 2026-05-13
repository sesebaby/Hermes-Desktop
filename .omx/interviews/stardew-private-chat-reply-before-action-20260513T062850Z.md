# Stardew 私聊先回复再行动深访记录

## 元数据
- Profile: standard
- Context type: brownfield
- Final ambiguity: 0.18
- Threshold: 0.20
- Context snapshot: `.omx/context/stardew-private-chat-reply-before-action-20260513T061017Z.md`

## 背景
用户手测发现：玩家和 NPC 私聊后，有时 NPC 不回复，直接开始行动。用户认为体验很差，期望 NPC 先用原版/私聊回复框回应玩家，然后再开始行动。

## 代码事实
- `src/game/core/PrivateChatOrchestrator.cs` 只在 `ReplyAsync` 返回非空文本后提交 `Speak`。
- 如果 `ReplyAsync` 返回空文本，通用私聊状态机会 `EndSession()`，不会显示回复。
- `src/games/stardew/StardewPrivateChatOrchestrator.cs` 会在私聊中让父层 agent 使用 `todo` 和 `stardew_submit_host_task`，再自然回复玩家。
- 当前 host-task ingress 对私聊 move 只等待 `private_chat_reply_displayed` 或 `private_chat_reply_closed` 其一，因此回复框刚显示时就可能开始行动。
- 现有测试存在“reply displayed 后不等 close 即提交 move”的期望，需要按新决策更新。

## 参考项目对齐
`external/hermescraft-main` 没有 Stardew 的阻塞式对话框 UI，因此没有“关闭回复框后执行”的同构实现。

等价原则：
- 玩家消息先进 pending command / chat log，不由宿主偷偷执行。
- Agent 用可见工具 `mc chat` / `mc whisper` 自己回复玩家。
- 长动作走 background task，让 agent 仍可检查 chat 和任务状态。
- 完成或错误通过状态/工具结果回到 agent。

映射到 Stardew：
- 私聊 accepted host task 必须先有父层 agent 生成的非空可见回复。
- Stardew 回复框会占 UI，因此 host task 必须等 `private_chat_reply_closed` 后才执行。
- 回复失败或等待关闭超时只能变成 deferred/blocked/terminal fact，不能宿主代写台词，也不能偷偷执行。

## 访谈结论
- Round 1: 选择 B。NPC 必须先显示非空回复，并等玩家关闭回复框后才执行 host task。
- Round 2: 选择 A。成功提交 host task 但最终回复为空时，用 agent-native 父层自检补自然回复，不让宿主合成台词。
- Round 3: 选择 A。范围覆盖所有私聊触发的 `stardew_submit_host_task`，不只 `action=move`。
- Round 4: 同意参考项目映射：Stardew 要额外加 `private_chat_reply_closed` gate；失败是事实回传，不是隐藏执行。

## 非目标
- 不引入 `local_executor`、小模型 gameplay 执行层、隐藏 fallback 或 JSON/自然语言动作解析。
- 不让宿主根据玩家文本推断移动、说话、todo 或任务状态。
- 不硬编码地点、NPC 台词或固定兜底句。
- 不让宿主自动关闭或修改 agent-owned `todo`。

## 决策边界
- OMX 可以修改现有“`private_chat_reply_displayed` 后即可执行”的测试期望。
- OMX 可以把私聊 host-task ingress gate 改成等待 `private_chat_reply_closed`。
- OMX 可以加入 bounded parent self-check 来修复空最终回复，但必须保持父层 agent 可见工具闭环。
- OMX 不能在宿主层合成 NPC 角色回复。

## 验收标准
- 私聊中 NPC 接受当前行动请求并成功提交 `stardew_submit_host_task` 后，玩家必须先看到非空 NPC 回复。
- host task 在 `private_chat_reply_closed` 前不得执行。
- 如果父层 agent 工具调用后最终文本为空，应追加一次 bounded agent-native self-check，让 agent 补自然回复且不得重复提交 host task。
- 如果回复显示失败或关闭事件超时，host task 不得偷偷执行；应记录 deferred/blocked/terminal fact 供 agent 后续处理。
- 所有行为保留 visible parent tool call -> visible tool result -> host/bridge task runner -> terminal fact 的单一路径。

## 后续建议
推荐进入 `$ralplan` 产出 OpenSpec/测试计划，再进入 `$autopilot` 实施。
