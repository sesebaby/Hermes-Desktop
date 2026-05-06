# Context Snapshot: stardew-npc-delegation-provider-routing

Task statement: 用户希望把 Stardew NPC 的中间步骤尽量委托给本地小模型/子 agent，减少云端大模型调用次数和成本，并且要求不要写死模型/provider，而要走配置驱动。用户补充 LM Studio 默认端口是 `1234`。

Desired outcome: 形成一套可执行方案，让 NPC 主 agent 只保留人格、意图和玩家可见最终表达，把 world scout / movement / action / speech brief 等可委托工作按配置路由到合适的 provider/model，优先本地模型，随后进入实现。

Known facts/evidence:
- 参考项目 `external/hermes-agent-main` 的 delegation 配置支持 `delegation.provider`、`delegation.model`、`delegation.base_url`、`delegation.api_key`、`max_concurrent_children`、`max_spawn_depth`，并支持直接指向 OpenAI-compatible endpoint。
- 参考项目的 delegation 语义是：`base_url` 优先于 `provider`，`model` 可单独覆盖，未配置时子代理继承 parent。
- 本项目已有 `AgentTool`，但当前工具名是 `agent`，不是 `delegate_task`；它已在 `AgentCapabilityAssembler.BuiltInToolNames` 中注册。
- `AgentTool` 当前内置的 agent types 还是 desktop-oriented（researcher/coder/analyst/planner/reviewer/general），不是 Stardew 专用的 world_scout/movement/action/speech_brief。
- `AgentTool` 当前注入的是同一个 `IChatClient`，未见独立的 delegation provider/model 路由。
- `OpenAiClient.StreamAsync(systemPrompt, messages, tools)` 接收 tools 参数，但流式实现里调用 `BuildPayload(messages, tools: null, stream: true)`，这会让子 agent 委托路径的工具面失效。
- 本项目已有全局模型配置读取链：`HermesEnvironment.ReadConfigSetting(section, key)`、`CreateLlmConfig()`、`ChatClientFactory.SwitchProvider()`、`ModelRouter.RouterConfig`。
- `HermesEnvironment` 现在默认模型端点来自 config.yaml 的 `model.base_url`，默认值是 `http://127.0.0.1:11434/v1`；用户已明确 LM Studio 默认端口应按 `http://127.0.0.1:1234/v1` 理解。
- `DreamerConfig` 已经证明本仓库可以用 config section 驱动多路 provider/model/base_url，而不是写死。
- `NpcRuntimeCompositionServices` 当前只承载一个 `IChatClient`，还没有 per-agent-type / per-delegation routing 字段。
- Stardew autonomy 入口在 `StardewNpcAutonomyBackgroundService` / `StardewPrivateChatOrchestrator` 里为 NPC 组装 runtime 和工具面。

Constraints:
- 不要为 NPC 再造第二套记忆/任务/人格系统。
- 不要把 provider/model/base_url 写死在代码里，应该由 config 驱动。
- 主 agent 的云端调用次数应下降，但玩家可见最终回复仍要保留云端高质量表达能力。
- 优先保留可逆、可配置、可测试的改动路径。
- 用户希望先形成完善方案，再执行。

Unknowns/open questions:
- delegation 配置是放进现有 `model:` section 里，还是新增 `delegation:` / `stardew_agents:` section 更清楚。
- 子 agent 是否应默认共享主 provider 的 credential pool，还是每个 provider 单独池化。
- NPC 场景中哪些任务类型必须保留云端，哪些可以完全本地化。
- `AgentTool` 现有通用 desktop agent 类型是否保留兼容，还是为 Stardew 增加独立类型表。

Likely touchpoints:
- `src/Tools/AgentTool.cs`
- `src/LLM/OpenAiClient.cs`
- `src/LLM/AnthropicClient.cs`
- `src/LLM/ChatClientFactory.cs`
- `src/LLM/ModelRouter.cs`
- `Desktop/HermesDesktop/Services/HermesEnvironment.cs`
- `src/runtime/NpcRuntimeBindings.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
- `Desktop/HermesDesktop.Tests/...` for delegation / config routing tests
