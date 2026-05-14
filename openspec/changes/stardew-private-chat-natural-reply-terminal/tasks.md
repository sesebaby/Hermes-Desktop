## 1. 00-准备与现状锁定

- [x] 1.1 阅读 `StardewPrivateChatOrchestrator.cs` 中私聊 parent agent 调用链，确认 delegation self-check、commitment todo self-check、reply self-check 的触发条件。
- [x] 1.2 阅读 `StardewNpcPrivateChatAgentRunnerTests.cs` 中现有 self-check 测试，标出需要保留、改写或删除的断言。
- [x] 1.3 确认当前成功工具结果判断仍基于 `stardew_submit_host_task` / `npc_no_world_action` 的成功 tool result，而不是 assistant tool-call 请求存在。

## 2. 01-测试先行

- [x] 2.1 新增或改写测试：普通私聊自然回复没有 `stardew_submit_host_task`、没有 `npc_no_world_action` 时，只调用一次父层 agent，直接返回回复，不提交 host task。
- [x] 2.2 新增或改写测试：无工具回复里即使包含“我马上过去”这类文字，宿主也不提交 bridge 命令、不创建 host task、不运行第二次 LLM。
- [x] 2.3 保留并确认测试：成功 `npc_no_world_action` 后返回自然回复，不提交 host task，不追加 self-check。
- [x] 2.4 保留并确认测试：成功 `stardew_submit_host_task` 但最终回复为空时，只追加一次 bounded reply self-check，不重复提交 host task。
- [x] 2.5 更新旧测试：失败的 `npc_no_world_action` 不再因为缺少成功 no-world closure 触发 delegation self-check；它应按无成功世界动作工具的自然回复路径处理，除非已有成功 host task 需要补回复。

## 3. 02-实现私聊终态边界

- [x] 3.1 删除或停用 `ShouldRunDelegationSelfCheck` 在私聊热路径中的调用，确保无成功世界动作工具时不会追加第二次 `ChatAsync`。
- [x] 3.2 保留 `ShouldRunReplySelfCheck`：只有成功 `stardew_submit_host_task` 且最终回复为空时，才运行补回复 self-check。
- [x] 3.3 保留成功工具结果判定逻辑，确保失败的 `stardew_submit_host_task` 不会被当成已提交动作。
- [x] 3.4 如需诊断日志，仅记录“私聊自然回复无世界动作工具调用”这类观测信息；日志不得参与动作推断或二次 LLM 决策。

## 4. 03-Prompt 与契约收紧

- [x] 4.1 更新私聊 system prompt：要即时改变游戏世界，必须调用 `stardew_submit_host_task`。
- [x] 4.2 更新私聊 system prompt：不调用世界动作工具就只能自然说话，不能承诺即时移动、即时执行或其他游戏世界动作。
- [x] 4.3 更新私聊 system prompt：`npc_no_world_action` 是推荐的明确无世界动作收口/诊断工具，但不是宿主触发第二轮 LLM 的硬门槛。
- [x] 4.4 确认 prompt 不引入宿主解析自然语言、硬编码地点、隐藏 executor 或第二工具 lane 的暗示。

## 5. 04-验证与收口

- [x] 5.1 运行 `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests" -p:UseSharedCompilation=false`。
- [x] 5.2 如相关 harness 或 orchestrator 测试被改动，运行对应筛选测试并记录结果。
- [x] 5.3 运行 `openspec status --change "stardew-private-chat-natural-reply-terminal"`，确认产物状态完整。
- [x] 5.4 汇报改动文件、验证命令、剩余风险，并明确普通私聊自然回复现在不再触发第二次 LLM。
