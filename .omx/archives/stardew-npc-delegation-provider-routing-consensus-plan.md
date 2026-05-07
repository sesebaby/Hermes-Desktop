# Stardew NPC Delegation Provider Routing Consensus Plan

## Requirements Summary

目标是让 Stardew NPC runtime 具备配置驱动的 LLM lane routing：高频中间步骤优先走本地/低成本模型，玩家可见表达保留云端模型，并复用现有 `agent` 委托能力。首版不新增依赖，不写死 provider/model/base_url；LM Studio 只作为推荐默认 endpoint，地址为 `http://127.0.0.1:1234/v1`。

已核对事实：

- 参考项目 `external/hermes-agent-main` 的 `delegation:` 支持 `provider/model/base_url/api_key/max_concurrent_children/max_spawn_depth`，并且语义是 `base_url` 优先于 `provider`，未配置时继承 parent。
- 本项目已把上游 `tools/delegate_tool.py` 映射到 `src/Tools/AgentTool.cs`，但工具名是 `agent`。
- 当前 `AgentTool` 是简化路径：单 child、无 batch，并且 `AgentToolConfig.MaxSubagentDepth/Timeout` 当前未真正参与执行。
- 当前 `OpenAiClient.StreamAsync(systemPrompt, messages, tools)` 的 structured streaming 实现没有使用传入 `systemPrompt`，并且传给 `BuildPayload` 的 `tools` 是 `null`。
- `NpcRuntimeCompositionServices` 当前只注入一个 `IChatClient`，导致 autonomy / private_chat / delegation 不能按 lane 分流。
- `HermesEnvironment.ReadConfigSetting(section, key)` 已支持读取任意 top-level YAML section；`DreamerConfig` 已证明本仓库可以按 section 配置多 provider/model/base_url。

## RALPLAN-DR Summary

### Principles

1. 配置驱动：agent 类型和职责可在代码中定义，但 provider/model/base_url 必须来自 config 或继承链。
2. 复用现有能力：保留 `agent` 工具，不另起第二套 delegation 工具名。
3. Truthful minimum：首版只承诺 single-child、flat delegation，不把 batch 并发或多层 delegation 包装成已完成。
4. 不污染全局模型状态：per-lane client materialization 不能调用全局 `ChatClientFactory.SwitchProvider`。
5. 保游戏效果：玩家私聊/最终表达可以继续走云端 lane；本地小模型主要承接 autonomy 和 delegated middle work。

### Decision Drivers

1. 成本：要把高频 autonomy/delegation 从云端挪到本地或低价模型。
2. 架构现状：Stardew runtime 已经有 `autonomy` / `private_chat` channel 边界，适合在此处接 lane routing。
3. 风险控制：`AgentTool` 当前不够成熟，首版必须收缩承诺，先把 routing 与基本 tool streaming 修实。

### Viable Options

#### Option A: 只给 `AgentTool` 增加独立 delegation client

Pros: 改动最小，最快证明子 agent 可以走本地模型。

Cons: autonomy 和 private_chat 仍然共享一个父模型，不能满足“中间步骤本地、玩家交流云端”的完整目标。

#### Option B: Stardew-scoped 三语义 lane routing，delegation 首版 flat-only

Pros: 匹配业务目标；范围限制在 Stardew runtime；不污染全局模型状态；能明确保留云端 finalizer。

Cons: 需要新增 route resolver，并改 NPC runtime composition wiring；要补测试保证继承和 override 正确。

#### Option C: 直接把 `AgentTool` 改走 `AgentService.SpawnAgentAsync`

Pros: `AgentService` 路径更接近完整 `Core.Agent.ChatAsync` 工具循环。

Cons: `AgentService` 当前也不是 route-aware，`AgentRequest.Model` 没真正驱动 client 选择；直接切换会把执行路径重构和 routing 混在一起。

### Chosen Option

选择 Option B。首版保留三条语义 lane：`stardew_autonomy`、`stardew_private_chat`、`delegation`，但 implementation 收缩为：

- route resolver + per-lane `IChatClient` materialization；
- autonomy/private_chat wiring；
- `agent` 工具使用 delegation lane；
- delegation v1 single child + flat depth；
- `max_concurrent_children` 只作为 reserved config/log 字段，不作为已实现并发能力。

## Scope

### In Scope

1. 新增配置解析/路由层，支持 `model`、`stardew_autonomy`、`stardew_private_chat`、`delegation` 四个 section 的继承合成。
2. `base_url` override 优先于 `provider` override；`model` 可独立 override；缺失字段继承 parent `model:`。
3. 给 Stardew autonomy handle 注入 autonomy lane client。
4. 给 Stardew private chat handle 注入 private_chat lane client。
5. 给 NPC runtime 内注册的 `agent` 工具注入 delegation lane client。
6. 修复 OpenAI-compatible structured streaming 对 `systemPrompt` 和 `tools` 的传递，保证现有 `AgentTool` 路径不丢 child role prompt / tool schema。
7. 增加日志，能看到 lane、provider、model、base_url source、child agent type、spawn depth policy。
8. 增加单元测试覆盖 route precedence、lane wiring、`AgentTool` client selection、streaming tools payload。

### Out of Scope

1. 不新增 Settings UI。
2. 不新增 NuGet 依赖。
3. 不实现 batch child delegation。
4. 不实现真正 nested delegation tree；首版 flat-only。
5. 不把 `AgentTool` 全面改造成上游 `delegate_task` 等价物。
6. 不把 `AgentTool` 立即切到 `AgentService`。
7. 不新增 Stardew 专用 agent role；首版先保留现有 `researcher/coder/analyst/planner/reviewer/general`，后续再做 `world_scout/movement_agent/action_agent/speech_brief_agent`。

## Config Shape

首版使用顶层 section，匹配当前 `HermesEnvironment.ReadConfigSetting(section, key)` 能力。

```yaml
model:
  provider: openai
  base_url: https://api.openai.com/v1
  default: gpt-5.4-mini

stardew_autonomy:
  provider: openai
  base_url: http://127.0.0.1:1234/v1
  model: qwen3-8b-instruct
  api_key: lm-studio
  auth_mode: api_key

stardew_private_chat:
  provider: openai
  model: gpt-5.4-mini

delegation:
  provider: openai
  base_url: http://127.0.0.1:1234/v1
  model: qwen3-4b-instruct
  api_key: lm-studio
  auth_mode: api_key
  max_spawn_depth: 1
  max_concurrent_children: 1 # reserved in v1; logged, not implemented as batch scheduling
```

Current debt: root `model:` still has existing code defaults (`custom`, `http://127.0.0.1:11434/v1`, `minimax-m2.7:cloud`). This plan does not remove those defaults in v1; it adds lane overrides and documents LM Studio 1234 as recommended config, not a global hardcoded replacement.

## Implementation Steps

1. Add a route config/resolver.
   - Likely files: `src/LLM/ChatRouteConfig.cs`, `src/LLM/ChatRouteResolver.cs`.
   - Use `LlmConfig` as output.
   - Keep parser simple: read top-level section keys through a provider delegate so tests do not depend on `%LOCALAPPDATA%`.
   - Do not call `ChatClientFactory.SwitchProvider`; materialize independent clients per lane.

2. Add a lane-aware client factory abstraction.
   - Likely files: `src/LLM/ChatClientFactory.cs` or new `src/LLM/IChatClientProvider.cs`.
   - Required lanes: `main`, `stardew_autonomy`, `stardew_private_chat`, `delegation`.
   - Use existing `OpenAiClient` / `AnthropicClient`; no new providers.

3. Extend NPC composition services.
   - Likely files: `src/runtime/NpcRuntimeBindings.cs`, `src/runtime/NpcRuntimeSupervisor.cs`.
   - Keep backward compatibility by allowing default main client.
   - `CreateAgentHandle` uses channel key for parent agent client and passes delegation client into built-in `AgentTool`.

4. Adjust capability registration for delegation client.
   - Likely files: `src/runtime/AgentCapabilityAssembler.cs`, `src/Tools/AgentTool.cs`.
   - Add optional `DelegationChatClient` or resolver to `AgentCapabilityServices`.
   - Register `AgentTool` with delegation client where present; otherwise inherit parent client.
   - Enforce flat v1: child agent does not expose recursive `agent` unless future depth support is explicitly implemented.

5. Wire Stardew autonomy/private chat.
   - Likely files: `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`, `src/games/stardew/StardewPrivateChatOrchestrator.cs`, `Desktop/HermesDesktop/App.xaml.cs`.
   - Autonomy handle gets `stardew_autonomy` lane client.
   - Private chat handle gets `stardew_private_chat` lane client.
   - Delegation defaults to `delegation` section; if missing, it inherits autonomy/local lane in NPC runtime.

6. Fix OpenAI-compatible structured streaming.
   - File: `src/LLM/OpenAiClient.cs`.
   - Include `systemPrompt` as a system message or equivalent request field.
   - Include `tools` and `tool_choice=auto` when tools are provided.
   - Keep existing non-streaming `CompleteWithToolsAsync` behavior unchanged.

7. Add observability.
   - Log lane name, provider, model, base URL source, model source, child agent type, spawn depth mode, and whether concurrency config is reserved.
   - Do not log API keys.

8. Add tests.
   - Route precedence tests.
   - Lane wiring tests for autonomy/private_chat/delegation.
   - `OpenAiClient.StreamAsync` payload tests.
   - `AgentTool` uses injected delegation client test.
   - Regression tests that existing built-in tool schema still includes `agent`.

## Acceptance Criteria

1. With no lane-specific config, Stardew autonomy/private_chat/delegation inherit root `model:` behavior.
2. With `stardew_autonomy` config, autonomy calls use its lane client and log `lane=stardew_autonomy`.
3. With `stardew_private_chat` config, private chat calls use its lane client and log `lane=stardew_private_chat`.
4. With `delegation` config, NPC runtime `agent` tool uses delegation lane client and log `lane=delegation`.
5. `delegation.base_url` overrides inherited base URL; `delegation.model` can override model independently.
6. `OpenAiClient.StreamAsync(systemPrompt, messages, tools)` sends both system prompt and tools for OpenAI-compatible providers.
7. v1 delegation is single-child and flat-only; `max_concurrent_children` is documented/logged as reserved, not claimed as working batch scheduling.
8. No new NuGet dependencies are added.
9. Tests prove at least two lanes can use different fake clients in the same NPC runtime.

## Verification Steps

Targeted first:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~ChatRouteResolverTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentTool"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcRuntimeSupervisorTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests"
```

Then:

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

Manual smoke after tests:

1. Put LM Studio lane config in `%LOCALAPPDATA%\hermes\config.yaml`, using `http://127.0.0.1:1234/v1`.
2. Start desktop with `.\run-desktop.ps1`.
3. Trigger an autonomy tick and confirm logs show local autonomy lane.
4. Trigger private chat and confirm logs show private-chat lane.
5. Trigger `agent` tool in NPC runtime and confirm child delegation lane does not hit cloud unless configured.

## ADR

Decision: Add Stardew-scoped LLM lane routing with `stardew_autonomy`, `stardew_private_chat`, and `delegation`, using config-driven `LlmConfig` inheritance and independent client materialization.

Drivers:

- High-frequency middle work must be local/cheap to meet cost targets.
- Player-visible dialogue benefits from cloud quality.
- Existing Stardew channel boundaries make lane routing localizable and testable.

Alternatives considered:

- Only change `AgentTool`: rejected because autonomy/private_chat would still share parent client.
- Switch immediately to `AgentService`: rejected because it is not route-aware either and would broaden behavior changes.
- Global multi-client routing for all app surfaces: rejected as too broad for this slice.

Why chosen: This gives the smallest honest path to cost reduction while preserving existing NPC runtime boundaries.

Consequences:

- Adds a route resolver and per-lane client materialization.
- Keeps `AgentTool` simplified in v1.
- Leaves existing root model default debt intact but documented.

Follow-ups:

- Add Stardew-specific agent types (`world_scout`, `movement_agent`, `action_agent`, `speech_brief_agent`) after routing is verified.
- Consider moving delegation execution to `AgentService` once route-aware clients are established.
- Add Settings UI after config-only behavior is stable.
- Implement real batch/nested delegation only after single-child flat routing is verified.

## Staffing Guidance

Ralph path: recommended for v1 because shared files are tightly coupled. Use one owner to implement route resolver, wiring, tests, and verification.

Team path if parallelizing:

- Lane 1 executor: `src/LLM/*` route resolver and `OpenAiClient.StreamAsync`.
- Lane 2 executor: `src/runtime/*` and Stardew wiring.
- Lane 3 test-engineer: route/wiring/streaming tests only.
- Code reviewer/verifier: read-only final pass.

Team verification path: no shutdown until targeted tests and full `HermesDesktop.Tests` pass or failures are documented with exact blockers.

## Review Changelog

- Applied Architect feedback by reducing delegation v1 to single-child flat mode.
- Moved `max_concurrent_children` from implemented acceptance criterion to reserved/logged config.
- Added explicit warning not to use global `SwitchProvider` for per-lane routing.
- Clarified that `OpenAiClient.StreamAsync` is a blocker only for the current `AgentTool` streaming path.
- Documented existing root `model:` fallback defaults as current technical debt.
