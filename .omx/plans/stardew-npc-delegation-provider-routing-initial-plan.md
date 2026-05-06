## Plan Summary

**Plan saved to:** `.omx/plans/stardew-npc-delegation-provider-routing-initial-plan.md`

**Scope:**
- 12 tasks across ~11 likely files
- Estimated complexity: MEDIUM

**Key Deliverables:**
1. 为 Stardew NPC runtime 建立配置驱动的 provider/model/base_url 路由方案，覆盖 `autonomy`、`private_chat`、`delegation` 三条调用车道。
2. 让 `agent` 工具在 NPC runtime 内真正使用 delegation 路由，并补齐当前流式 tool payload 丢失问题与对应测试。

**Consensus mode:**
- RALPLAN-DR: Principles (4), Drivers (top 3), Options (3)
- ADR: Decision, Drivers, Alternatives considered, Why chosen, Consequences, Follow-ups

### RALPLAN-DR Summary

**Principles**
1. 配置优先，不写死 provider/model/base_url；默认值只作为文档建议，不作为代码常量策略。
2. 优先复用现有链路：`AgentTool`、`NpcRuntimeSupervisor`、`ChatClientFactory`、`HermesEnvironment`、`config.yaml`。
3. 先打通最小可验证路径：Stardew NPC 的 `autonomy` / `private_chat` / `delegation`，不顺手重做通用多 agent 平台。
4. 本地小模型承担高频/中间步骤，云端大模型保留给用户可见最终表达；路由边界必须可观测、可测试。

**Decision Drivers**
1. 成本约束明确：玩家 30 分钟成本目标 `<= 1 RMB`，必须把高频调用从云端挪走。
2. 已有能力已足够接近：`agent` 工具已移植、`AgentService` 有更完整工具循环、Dreamer 已证明多 provider/model/base_url 可配置。
3. 当前实现存在两个关键缺口：NPC runtime 只有单一 `IChatClient`，且 `OpenAiClient.StreamAsync` 内部丢了 `tools`。

**Options**

**Option A: 只给 `AgentTool` 增加 delegation override**
- Pros: 改动面最小；能最快验证“NPC 调子 agent 时走本地模型”。
- Cons: `autonomy` / `private_chat` 仍共享单一父模型；不能满足“高频本地、最终表达云端”的完整目标；后续还得二次改注入链。

**Option B: 引入 Stardew 车道化 routing，分开 `autonomy` / `private_chat` / `delegation`**
- Pros: 直接匹配用户目标；沿用现有 DI 与 config 体系；可先只做 Stardew，不扩散到全局；便于加日志与测试。
- Cons: 需要调整 `NpcRuntimeCompositionServices` 和创建句柄时的 client 来源；测试面比 A 更大。

**Option C: 直接把 `agent` 工具重定向到 `AgentService.SpawnAgentAsync` 路径**
- Pros: 可复用现有 `AgentRunner` + `Core.Agent.ChatAsync` 工具循环；长期一致性更好。
- Cons: 改动行为面更大；会把“路由问题”和“执行路径切换”绑在一起；第一阶段回归风险偏高。

**Recommended**
- 采用 **Option B** 作为第一阶段主线。
- 同阶段吸收一个必要补丁：修复 `OpenAiClient.StreamAsync` 的 tool payload 传递，否则 delegation 路由打通后也无法稳定使用工具。
- `AgentService` 路径对齐留作第二阶段候选，不和首版 routing 一起落地。

### Phase 1 Scope

**第一阶段要做**
1. 设计并落地 Stardew NPC 三车道配置模型：
   - `stardew_autonomy`
   - `stardew_private_chat`
   - `delegation`
2. 定义配置解析与继承规则：
   - `base_url` override 优先于 `provider`
   - `provider` override 优先于父级缺省
   - `model` 可单独覆盖
   - 缺失字段回退到父 `LlmConfig`
3. 让 `StardewNpcAutonomyBackgroundService` 使用 autonomy lane client。
4. 让 `StardewNpcPrivateChatAgentRunner` / `StardewPrivateChatOrchestrator` 使用 private-chat lane client。
5. 让 NPC runtime 内 `agent` 工具生成的子 agent 使用 delegation lane client。
6. 将 `delegation.max_concurrent_children` / `delegation.max_spawn_depth` 纳入首版配置与约束读取，即使首版只先严格使用 `max_spawn_depth`，也要把并发字段完整纳入配置模型与日志。
7. 修复 `src/LLM/OpenAiClient.cs` 流式 payload 的 tools 传递问题。
8. 为 routing precedence、NPC lane 选择、delegation tool chain、tool payload 传递补单测。
9. 增加关键日志，能在 `hermes.log` 中区分当前调用落在哪条 lane、用的 provider/model/base_url 来源是 override 还是 inherited。

**第一阶段不做**
1. 不新增依赖。
2. 不新增 Stardew 专用 agent type；继续复用现有 `researcher/coder/analyst/planner/reviewer/general`。
3. 不把整个应用的所有 `IChatClient` 消费者都升级成多车道路由。
4. 不同时重构到 `AgentService.SpawnAgentAsync` 为唯一 delegation 路径。
5. 不做复杂成本计费系统，只做足够的调用面分流与日志验证。
6. 不强制做 Settings UI 首版；首版以 `config.yaml` 驱动为准，UI 暴露可作为 follow-up。

### Proposed Config Shape

首版建议保持与现有 `HermesEnvironment.ReadConfigSetting(section, key)` 兼容，用顶层 section，而不是引入嵌套 YAML 解析器：

```yaml
model:
  provider: openai
  base_url: https://api.openai.com/v1
  default: gpt-5.4-mini

stardew_autonomy:
  provider: openai
  base_url: http://127.0.0.1:1234/v1
  model: qwen3-8b-instruct

stardew_private_chat:
  provider: openai
  model: gpt-5.4-mini
  # base_url omitted => inherit from provider default / parent config

delegation:
  provider: openai
  base_url: http://127.0.0.1:1234/v1
  model: qwen3-4b-instruct
  api_key_env: LM_STUDIO_API_KEY
  max_concurrent_children: 2
  max_spawn_depth: 2
```

说明：
- `LM Studio` 默认文档建议地址可写为 `http://127.0.0.1:1234/v1`。
- 不要求代码里写死 LM Studio；这只是默认配置建议。
- `provider: openai` 适合兼容 OpenAI-style local endpoint。

### Implementation Steps

1. **定义 routing 配置对象与解析器**
   - 可能文件：
     - `src/LLM/IChatClient.cs`
     - `src/LLM/ChatClientFactory.cs`
     - `Desktop/HermesDesktop/Services/HermesEnvironment.cs`
     - 新增候选：`src/LLM/ChatRouteConfig.cs`、`src/LLM/ChatRouteResolver.cs`
   - 产出：
     - 可从 `model` / `stardew_autonomy` / `stardew_private_chat` / `delegation` 组装 `LlmConfig`
     - 明确 `base_url > provider > parent inherit` 与 `model 可独立 override`
     - `api_key_env` / `auth_mode` 类字段沿用现有 `LlmConfig` 能力，不另起秘密存储机制

2. **把当前“单一 IChatClient”改成“按 lane 取 client”而不是全局替换**
   - 可能文件：
     - `src/runtime/NpcRuntimeBindings.cs`
     - `src/runtime/NpcRuntimeContextFactory.cs`
     - `src/runtime/NpcRuntimeSupervisor.cs`
     - `Desktop/HermesDesktop/App.xaml.cs`
   - 方案：
     - 在 `NpcRuntimeCompositionServices` 中增加 route-aware client factory / resolver，而不是只塞一个固定 `IChatClient`
     - `CreateAgentHandle(...)` 按 `channelKey` 获取父 lane client
     - `agent` 工具创建时拿到 delegation lane client

3. **给 Stardew autonomy lane 接本地/低成本模型**
   - 可能文件：
     - `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
     - `Desktop/HermesDesktop/App.xaml.cs`
   - 方案：
     - 构造 `NpcRuntimeAutonomyBindingRequest` 时传 autonomy client 来源
     - 默认继承根 `model:`；配置存在时切到 `stardew_autonomy`
     - 加日志：`lane=autonomy provider=... model=... baseUrlSource=override|inherited`

4. **给 Stardew private chat lane 接云端最终表达模型**
   - 可能文件：
     - `src/games/stardew/StardewPrivateChatOrchestrator.cs`
     - `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
     - `Desktop/HermesDesktop/App.xaml.cs`
   - 方案：
     - `StardewNpcPrivateChatAgentRunner` 从 `stardew_private_chat` 取 client
     - 保证玩家看到的最终回复来自 private-chat lane，而非 autonomy / delegation lane

5. **让 `agent` 工具在 NPC runtime 内真正使用 delegation lane**
   - 可能文件：
     - `src/Tools/AgentTool.cs`
     - `src/runtime/NpcRuntimeSupervisor.cs`
     - `src/runtime/AgentCapabilityAssembler.cs`
   - 方案：
     - `AgentTool` 不再只拿构造时的单一 `_chatClient`
     - 在执行时根据上下文获取 delegation client
     - 首版保留现有 agent types，不做 Stardew 定制 role
     - 读取并执行 `delegation.max_spawn_depth`
     - 读取 `delegation.max_concurrent_children`，首版至少体现在 guard/logging；如果当前 `AgentTool` 没有真实并发 spawn，则不要伪装“已完全实现并发调度”

6. **修复 `OpenAiClient.StreamAsync` tools 丢失**
   - 可能文件：
     - `src/LLM/OpenAiClient.cs`
   - 说明：
     - 这是 routing 生效前的必要修补项
     - 否则本地 delegation 子 agent 即使选对模型，也会因 tools 没传到 provider 而退化为纯文本回答

7. **补测试，优先覆盖行为而不是实现细节**
   - 可能文件：
     - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
     - `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
     - 新增候选：
       - `Desktop/HermesDesktop.Tests/LLM/ChatRouteResolverTests.cs`
       - `Desktop/HermesDesktop.Tests/Tools/AgentToolRoutingTests.cs`
       - `Desktop/HermesDesktop.Tests/LLM/OpenAiClientToolStreamingTests.cs`
   - 重点：
     - lane 选择正确
     - fallback / inherit 正确
     - delegation 走本地 client、private chat 走云端 client
     - `StreamAsync` payload 含 `tools`

8. **验证与日志回路**
   - 可能文件：
     - `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
     - `src/games/stardew/StardewPrivateChatOrchestrator.cs`
     - `src/Tools/AgentTool.cs`
   - 增加结构化日志字段：
     - `lane`
     - `provider`
     - `model`
     - `baseUrl`
     - `configSource`
     - `spawnDepth`
     - `childAgentType`

### Acceptance Criteria

1. `config.yaml` 中未写死 provider/model/base_url 时，Stardew NPC 仍能继承根 `model:` 正常运行。
2. `stardew_autonomy` 配置存在时，autonomy loop 使用其 provider/model/base_url，而不是根 `IChatClient`。
3. `stardew_private_chat` 配置存在时，玩家私聊回复使用其 provider/model/base_url。
4. `delegation` 配置存在时，NPC runtime 内调用 `agent` 工具生成的子 agent 使用 delegation lane。
5. `delegation.base_url`、`delegation.provider`、`delegation.model` 的优先级符合：
   - `base_url override`
   - `provider override`
   - `parent inherit`
   - `model independent override`
6. `OpenAiClient.StreamAsync` 发出的 payload 在有工具时包含 `tools` 和 `tool_choice=auto`。
7. 首版不新增任何 NuGet 依赖。
8. 至少有单测证明：
   - private chat 与 delegation 可走不同 client
   - delegation 深度限制生效
   - tool payload 不再丢失
9. `hermes.log` 能清楚区分 autonomy / private_chat / delegation 三条 lane 的模型路由。

### Verification Plan

**静态/单元验证**
1. `dotnet build .\\Desktop\\HermesDesktop\\HermesDesktop.csproj -c Debug -p:Platform=x64`
2. `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~ChatRouteResolverTests"`
3. `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentToolRoutingTests"`
4. `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~OpenAiClientToolStreamingTests"`
5. `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests"`
6. `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcRuntimeSupervisorTests"`
7. 最后跑全量：`dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug`

**手动验证**
1. 在 `%LOCALAPPDATA%\\hermes\\config.yaml` 填入：
   - `stardew_autonomy.base_url = http://127.0.0.1:1234/v1`
   - `delegation.base_url = http://127.0.0.1:1234/v1`
   - `stardew_private_chat` 指向云端 provider/model
2. 启动桌面壳：`.\\run-desktop.ps1`
3. 触发一个 NPC autonomy tick，确认日志中 autonomy lane 命中本地 endpoint。
4. 触发一个玩家私聊，确认日志中 private-chat lane 命中云端 endpoint。
5. 让 NPC 执行一个需要中间分析/查找的任务，确认 `agent` 工具触发 delegation lane 且 child agent 使用本地 endpoint。

**成本验证**
1. 记录 30 分钟内 private chat 与 autonomy/delegation 的调用次数。
2. 确认高频 autonomy/delegation 未落到云端。
3. 用日志统计估算云端仅剩玩家可见最终表达，满足成本目标的前提被建立。

### ADR Draft

**Decision**
- 为 Stardew NPC runtime 引入三车道 LLM routing：
  - `stardew_autonomy` 负责高频自主行为
  - `stardew_private_chat` 负责玩家可见最终表达
  - `delegation` 负责 NPC 通过 `agent` 工具拉起的中间子任务

**Drivers**
1. 降低高频云端调用成本，使 30 分钟玩家成本目标有现实可达路径。
2. 复用现有 Hermes agent delegation 与现有配置系统，不新增依赖、不写死 provider。
3. 让 NPC runtime 的直接表达与中间推理可分离，避免“一刀切全本地”或“一刀切全云端”。

**Alternatives considered**
- 只改 `AgentTool`：实现快，但无法把 autonomy/private chat 分开。
- 直接切到 `AgentService.SpawnAgentAsync`：长期可能更统一，但首版行为变化过大。
- 全局所有 `IChatClient` 消费者统一改多车道路由：范围过宽，不适合首版。

**Why chosen**
- 三车道方案最贴近业务目标，同时改动仍局限在 Stardew runtime 与 delegation 触点，便于验证和回滚。

**Consequences**
- NPC runtime 组装代码会多一个 route-aware client 解析层。
- 测试数量会增加，但主要是单测，不需要新依赖。
- 首版仍会保留 `AgentTool` 与 `AgentService` 双路径并存，需要后续决定是否统一。

**Follow-ups**
1. 评估是否把 `AgentTool` 内部执行统一切到 `AgentService`。
2. 视用户体验决定是否在 `SettingsPage` 增加 Stardew lane 配置 UI。
3. 若成本统计仍超标，再考虑把更多非玩家可见步骤进一步下沉到本地 lane。

### Agent Staffing

**Available agent types**
- `planner`
- `analyst`
- `researcher`
- `reviewer`
- `coder`
- `general`

**Recommended staffing: ralph**
- 适合这次首版实现。
- 单 owner 顺序：
  1. `analyst` 细化配置 precedence 与现有测试缺口
  2. `coder` 实现 routing + tool payload 修复
  3. `reviewer` 做回归审查与日志/风险检查
- reasoning guidance:
  - `analyst`: medium
  - `coder`: high
  - `reviewer`: high

**Recommended staffing: team**
- 当你希望把“routing 主线”和“测试/验证”并行推进时再用。
- 建议 4 lane：
  1. `analyst`：落 config schema、precedence、日志字段
  2. `coder`：`ChatClientFactory` / resolver / `AgentTool`
  3. `coder`：Stardew runtime 注入链与 lane wiring
  4. `reviewer`：测试计划、风险核查、验收口径
- reasoning guidance:
  - lane 1: medium
  - lane 2: high
  - lane 3: high
  - lane 4: medium

**Launch hints**
- `ralph` 路径：按本计划顺序单线实现，适合先把行为跑通。
- `team` 路径：如果启用并行，先由 leader 锁定共享文件边界：
  - lane 2 主写 `src/LLM/*`, `src/Tools/AgentTool.cs`
  - lane 3 主写 `src/runtime/*`, `src/games/stardew/*`, `Desktop/HermesDesktop/App.xaml.cs`
  - reviewer 只读

**Team verification path**
1. lane 2 提交 routing resolver 与 `OpenAiClient` 工具流修复
2. lane 3 在此基础上接 Stardew runtime lane wiring
3. reviewer 检查：
   - precedence 是否与计划一致
   - 是否出现写死 provider/model/base_url
   - 是否误把 `max_concurrent_children` 宣称为已完整调度实现
4. 合流后统一跑 build + targeted tests + full tests

**Does this plan capture your intent?**
- `proceed` - Show executable next-step commands
- `adjust [X]` - Return to interview to modify
- `restart` - Discard and start fresh
