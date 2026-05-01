# 桌面与 NPC 智能体能力同源化计划

## 计划摘要

目标：让桌面 agent 与 NPC agent 真正走同一个 capability composition contract；允许差异仅限于人格、会话、命名空间和记忆目录。这个计划不是继续在 NPC 各入口补几个注册调用，而是消除 Desktop 与 NPC 的双源装配事实。

范围内 touchpoints：
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- `src/runtime/AgentCapabilityAssembler.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/runtime/NpcAgentFactory.cs`
- `src/runtime/NpcRuntimeDescriptor.cs`
- `src/runtime/NpcRuntimeHost.cs`
- `src/runtime/NpcNamespace.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewAutonomyTickDebugService.cs`
- `src/games/stardew/StardewNpcCatalog.cs`
- `src/soul/SoulService.cs`
- `Desktop/HermesDesktop.Tests/Runtime/*`
- `Desktop/HermesDesktop.Tests/Stardew/*`

明确排除：
- `Desktop/HermesDesktop.Tests/Services/TranscriptStoreTests.cs`
- `src/transcript/TranscriptStore.cs`
- `openspec/project.md`

证据锚点：
- `Desktop/HermesDesktop/App.xaml.cs:390-394` 当前注册 `StardewPrivateChatBackgroundService` 时未传 `StardewPrivateChatOptions`
- `Desktop/HermesDesktop/App.xaml.cs:511-515,837-851,1023-1027` Desktop prompt、built-in tools、discovered tools 仍在 Desktop 自己的装配路径里
- `src/games/stardew/StardewPrivateChatOrchestrator.cs:27-31,139-150,204-210,340-346` private-chat 仍固定 `haley`，且 descriptor/displayName 仍是局部硬编码
- `src/game/core/PrivateChatOrchestrator.cs:11-17,59-63` 当前 private-chat 状态机是单实例、单状态、单目标过滤
- `src/games/stardew/StardewAutonomyTickDebugService.cs:57-70` autonomy/debug 仍绕过共享能力装配，只走 `StardewNpcToolFactory.CreateDefault(...)`
- `src/runtime/NpcRuntimeContextFactory.cs:66-88` NPC 上下文未接入桌面同款 `SystemPrompts.Build(... skillsMandatoryPrompt ...)`
- `src/runtime/NpcRuntimeDescriptor.cs:3-11` descriptor 已有 `PackRoot` 字段
- `src/runtime/NpcRuntimeHost.cs:16-29` discovered/runtime-host 路径已经掌握 pack provenance
- `src/games/stardew/StardewNpcCatalog.cs:10-37` 仓库已有 NPC manifest/catalog 能力，可作为 private-chat/debug 的 provenance 来源
- `src/runtime/NpcNamespace.cs:52-56` 与 `src/soul/SoulService.cs:207-222,303-339` 组合下，NPC namespace 默认会写入 Hermes 模板而非 NPC persona seed
- `.omx/plans/NPC自主循环-产品需求文档.md:49-53,65-67` 旧 PRD 仍要求 NPC 只能接收 safe subset，与当前用户原则冲突

## RALPLAN-决策记录

### 原则

1. Desktop 与 NPC 必须共享同一个 composition contract，而不是 Desktop 一条、NPC 一条。
2. 差异只能来自 persona、session、namespace、memory path；不能来自 reduced tool subset、skills prompt 差异或另一套注册代码。
3. Persona provenance 必须来自 pack/manifest/catalog，不允许继续用 `haley/penny` 硬编码兜底来定义身份。
4. 真实入口优先于直调 runner。验收必须覆盖 background service、orchestrator、autonomy/debug 和 Desktop 启动接线。
5. 用户新原则优先于旧 PRD。旧 “NPC only safe tools” 约束在本轮被显式废弃，不作为实现差异保留。

### 决策驱动

1. 彻底消除 Desktop/NPC 双源装配，避免继续漂移。
2. 修复 Penny 在真实 private-chat 生产路径下无法启动自己 agent 的问题。
3. 让 private-chat、autonomy/debug、Desktop 三条入口都从同一 contract 得到 prompt/tool/skill surface。
4. 保持 NPC 本地人格与状态隔离，不把 Desktop 全局 `SoulService` / `MemoryManager` / `TranscriptStore` 误复用到 NPC runtime。

### 方案

#### 方案 A：建立单一 Desktop/NPC 组合契约，Desktop 与所有 NPC 入口共同消费

做法：
- 保留 `AgentCapabilityAssembler` 作为 capability 注册核心，但把 Desktop 的 prompt/built-in/discovered tool 装配也迁入共享 contract。
- 新增或抽出一层共享 contract，统一负责：
  - base prompt 组装；
  - built-in tool 注册；
  - discovered tool 注册；
  - skills mandatory prompt 注入；
  - runtime collaborator 绑定；
  - NPC persona provenance 与 seeding 接入。
- `App.xaml.cs`、`StardewNpcPrivateChatAgentRunner`、`StardewAutonomyTickDebugService` 都改为消费这一个 contract。

优点：
- 真正满足“同源装配”。
- 后续新增 capability 时，Desktop 与 NPC 不会再次分叉。
- 便于写出能直接比较 Desktop/NPC capability surface 的测试。

缺点：
- 需要把 Desktop 现有 `App.xaml.cs` 装配逻辑向共享层迁移，改动面中等。
- 会暴露更多历史分叉问题，首轮测试红面可能较大。

#### 方案 B：只统一 NPC 各入口，Desktop 保持现状

做法：
- private-chat 与 autonomy/debug 共用一个 NPC composition flow。
- Desktop 仍留在 `App.xaml.cs` 的原有 prompt/tool 注册路径。

优点：
- 比全面迁移快。
- 对 Desktop 启动链侵入较小。

缺点：
- 只能消除 NPC 内部分叉，无法消除 Desktop/NPC 双源事实。
- 不满足当前目标里“Desktop 和 NPC 同源装配”的硬要求。

#### 方案 C：共享注册源，但对 autonomy 引入运行时策略门控或 reduced subset

做法：
- 使用共享 source 注册 capability。
- 但在 autonomy/debug 场景对 discovered tools、skills mutation、subagent 等再做 policy gating。

优点：
- 可以缓解后台自主 loop 的 side-effect 担忧。

缺点：
- 与用户当前原则直接冲突，因为这会让差异超出 persona/session/namespace/memory path。
- 会把“同源装配”重新变成“同源后再裁剪”，长期仍然是一种能力面分叉。

### 推荐方案

采用 **Option A**。

理由：
- 这是唯一真正满足用户原则的方案：Desktop 与 NPC 共用同一个 composition contract，差异只保留在 persona、session、namespace、memory path。
- Option B 只能修 NPC 内部分叉，修不了 Desktop/NPC 双源。
- Option C 虽然更保守，但它重新引入 runtime-specific capability difference，与当前目标不兼容。本轮不采用 reduced subset，也不引入 policy gating 作为合法差异。

### 验收标准

1. Desktop 启动路径不再独占自己的 prompt/tool/discovered-tool 装配逻辑；Desktop 与 NPC 都共同消费同一个 composition contract。若 Desktop 绕过该 contract，相关 parity 测试必须失败。
2. private-chat 的 MVP 会话模型被明确锁定为：`single active private-chat session per bridge`。bridge 空闲时，任意 NPC 事件都可以开启会话；会话活跃期间，其他 NPC 的私聊事件不会并发抢占当前会话。
3. Penny 的真实事件在 background service + orchestrator 生产路径下会命中 Penny descriptor / Penny namespace / Penny runner，不再被 `haley` 默认目标过滤。
4. `StardewNpcPrivateChatAgentRunner` 与 `StardewAutonomyTickDebugService` 都通过同一个 composition contract 获取完整 capability surface；本轮不保留 autonomy reduced subset。
5. private-chat/debug 构造 NPC descriptor 时，`DisplayName`、`ProfileId`、`PackRoot`、persona 资产来源于 manifest/catalog/pack provenance，而不是 `ResolveDisplayName("haley"/"penny")` 之类局部硬编码。
6. NPC runtime 初始化后，`SOUL.md` 来自对应 pack seed/copy；首次创建 Haley/Penny runtime 时不再落入 `SoulService` 默认 `# Hermes Agent Identity` 模板。
7. NPC prompt 使用与 Desktop 同一 base prompt 组装逻辑，并包含相同的 `skillsMandatoryPrompt`；可变部分只允许是 NPC persona/private-chat 或 autonomy 的场景补充。
8. Desktop 与 NPC 的 capability surface 在工具名与 skill tools 维度一致；允许不同的只有 persona 文本、session id、namespace 路径和 memory/transcript 根目录。
9. Haley 与 Penny 仍保持各自 namespace-local transcript/memory/soul 状态，不发生跨 NPC 污染，也不复用 Desktop 全局状态。

## 实施步骤

### 1. 定义单一 Desktop/NPC 组合契约
- Likely touchpoints:
  - `Desktop/HermesDesktop/App.xaml.cs`
  - `src/runtime/AgentCapabilityAssembler.cs`
  - `src/runtime/NpcRuntimeContextFactory.cs`
  - `src/runtime/NpcAgentFactory.cs`
- Work:
  - 把 Desktop 当前分散在 `App.xaml.cs` 的 base prompt、built-in tools、discovered tools 装配迁移到共享 contract。
  - 让 Desktop 与 NPC 都通过这个 contract 获取：
    - base prompt builder；
    - built-in capability 注册；
    - discovered tool 注册；
    - skills mandatory prompt 注入；
    - runtime collaborator 绑定。
  - 明确 `AgentCapabilityAssembler` 只负责注册核心，contract 负责“谁来用、怎样绑定上下文、怎样补场景 prompt”。
  - 若保留 `NpcAgentFactory`，它应只负责实例化 `Agent`；parity 语义上移到 contract。
- Done when:
  - Desktop 与 NPC 都从同一个 contract 得到 capability assembly，`App.xaml.cs` 不再保留一条语义独立的 Desktop-only 装配路径。

### 2. 把 pack provenance 提升为 private-chat/debug 的必经输入
- Likely touchpoints:
  - `src/runtime/NpcRuntimeDescriptor.cs`
  - `src/runtime/NpcRuntimeHost.cs`
  - `src/games/stardew/StardewNpcCatalog.cs`
  - `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
  - `src/games/stardew/StardewPrivateChatOrchestrator.cs`
  - `src/games/stardew/StardewAutonomyTickDebugService.cs`
- Work:
  - 为 private-chat/debug 引入与 runtime-host 同源的 descriptor resolver，不再手写 `DisplayName` / `PackRoot`。
  - 使用 `PackRoot`、`NpcPackManifest`、`StardewNpcCatalog` 或 pack loader 解析：
    - `NpcId`
    - `DisplayName`
    - `ProfileId`
    - `PackRoot`
    - 其他 persona provenance
  - 删除 `StardewPrivateChatOrchestrator.ResolveDisplayName(...)` 这类身份硬编码；`manual-debug` / `saveId` 不能再伪装成 `PackRoot`。
- Done when:
  - private-chat 与 debug 都能拿到真实 pack provenance，并以此驱动 descriptor 与 persona seed。

### 3. 明确并实现 private-chat 的单桥单活跃会话模型
- Likely touchpoints:
  - `src/game/core/PrivateChatContracts.cs`
  - `src/game/core/PrivateChatOrchestrator.cs`
  - `src/games/stardew/StardewPrivateChatOrchestrator.cs`
  - `Desktop/HermesDesktop/App.xaml.cs`
- Work:
  - 去掉当前固定 `NpcId` 造成的 Haley-only 过滤。
  - 定义本轮 MVP 语义：
    - 当无活跃会话时，第一条合格事件的 `record.NpcId` 成为当前会话目标；
    - 当已有活跃会话时，其他 NPC 的事件不并发开启新私聊；
    - 会话结束后，再由新的事件决定下一次由谁开启。
  - background service 与 orchestrator 的实现必须与上述单会话模型一致，而不是半修 `Penny` 路由、却继续让状态机语义模糊。
- Done when:
  - Penny 能在空闲桥接状态下启动自己的私聊会话；活跃 Haley 会话期间，Penny 事件不会并发抢占。

### 4. 统一 NPC 与 Desktop 的 prompt 组装逻辑
- Likely touchpoints:
  - `Desktop/HermesDesktop/App.xaml.cs`
  - `src/runtime/NpcRuntimeContextFactory.cs`
  - 共享 composition contract
- Work:
  - 对齐 Desktop 当前 `SystemPrompts.Build(... includeSkillsGuidance: true, skillsMandatoryPrompt: ...)` 逻辑。
  - 把 NPC base prompt 改为共享构造器输出，再附加 NPC 场景层：
    - persona/system supplement；
    - private-chat supplement；
    - autonomy supplement。
  - `SkillManager` 不再只传给 `MemoryReviewService`；它必须参与 prompt 组装。
- Done when:
  - NPC prompt snapshot 中能看到与 Desktop 同源的 skills guidance 文案；不同场景只追加薄层 NPC 指令。

### 5. 做 persona seeding，而不是让 `SoulService` 默认模板接管 NPC 身份
- Likely touchpoints:
  - `src/runtime/NpcNamespace.cs`
  - `src/soul/SoulService.cs`
  - 新的 NPC persona seeding helper 或 runtime helper
- Work:
  - 在创建 `SoulService` 之前完成 pack -> namespace 的 seed/copy。
  - seed 责任优先放在 NPC runtime 层，而不是修改 Desktop 全局 `SoulService` 默认行为。
  - 若 NPC namespace 已有定制文件，则不覆写；若没有，则从 `PackRoot` 种子化 `SOUL.md` / persona 资产。
- Done when:
  - 首次创建 Haley/Penny runtime 时，本地 `SOUL.md` 来源于 pack，而不是 `DefaultSoulTemplate`。

### 6. 让 autonomy/debug 与 private-chat 同样 full parity
- Likely touchpoints:
  - `src/games/stardew/StardewAutonomyTickDebugService.cs`
  - 共享 composition contract
  - `src/games/stardew/StardewNpcTools.cs`
- Work:
  - 将 debug/autonomy 从局部 `StardewNpcToolFactory.CreateDefault + NpcAgentFactory.Create` 路径迁移到共享 contract。
  - 本轮不引入 autonomy reduced subset。若 discovered tools 在后台 loop 有副作用风险，把它写成风险和测试要求，而不是合法差异。
  - `StardewNpcToolFactory` 保留其 Stardew 领域工具职责，但不再承担“NPC capability subset”语义。
- Done when:
  - private-chat 与 autonomy/debug 的 built-in/discovered tool surface、prompt 组装、memory wiring 全部同源。

### 7. 重写测试，锁定 Desktop/NPC 契约、Penny 生产路由、pack provenance 与单会话模型
- Likely touchpoints:
  - `Desktop/HermesDesktop.Tests/Runtime/NpcAgentFactoryTests.cs`
  - `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeContextFactoryTests.cs`
  - `Desktop/HermesDesktop.Tests/Runtime/NpcNamespaceTests.cs`
  - `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs`
  - `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatWiringTests.cs`
  - 新增 `BackgroundService` / descriptor resolver / persona seeding / autonomy parity tests
- Work:
  - 增加 Desktop 与 NPC 都消费同一 composition contract 的测试；如果 Desktop 仍绕过 contract，测试必须红。
  - 用真实 orchestrator/background-service 测 Penny 事件路由，而不是只测 `ReplyAsync("penny")`。
  - 为 pack provenance 写测试：private-chat/debug descriptor 的 `DisplayName`/`PackRoot`/persona seed 必须来自 pack。
  - 为 private-chat 单会话模型写测试：空闲可由任意 NPC 打开；活跃会话期间其他 NPC 不并发抢占。
  - 为 autonomy/debug 写 full parity tests，确保其 capability surface 与 private-chat/desktop 一致。
- Done when:
  - 新测试能在移除修复时失败，且覆盖“Desktop 绕过 contract、Penny 生产路由、pack provenance、单会话模型、autonomy parity”五类回归。

### 8. 更新冲突约束并完成回归验证
- Work:
  - 在本次计划与 ADR 中显式声明：旧 PRD 中的 “NPC only safe tools” 被新原则 supersede。
  - 回归验证先跑 Runtime/Stardew 相关最小集，再扩大到 Desktop 测试全集。
- Done when:
  - 代码、测试、计划文字都不再残留 “NPC safe subset” 这一旧原则。

## 验证

### 必跑自动验证

1. `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAgentFactoryTests|FullyQualifiedName~NpcRuntimeContextFactoryTests|FullyQualifiedName~NpcNamespaceTests"`
2. `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewPrivateChatOrchestratorTests|FullyQualifiedName~StardewPrivateChatWiringTests|FullyQualifiedName~StardewPrivateChatBackgroundService|FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests"`
3. `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyTickDebugService|FullyQualifiedName~NpcAutonomyLoopTests"`
4. 若 descriptor resolver / persona seeding 新增专门测试类，再运行对应过滤组。
5. 分组通过后，再运行：`dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug`

### 必须满足的断言

1. Desktop 与 NPC 都通过同一个 composition contract 完成 prompt/tool/discovered-tool 装配；Desktop 绕过 contract 会导致 parity 测试失败。
2. Penny private-chat 事件在真实 background service/orchestrator 入口下会命中 Penny descriptor、Penny namespace 与 Penny runner。
3. private-chat 会话模型符合 `single active private-chat session per bridge`：空闲可由任意 NPC 打开，活跃期间其他 NPC 不并发抢占。
4. private-chat 与 autonomy/debug 的 `AgentCapabilityAssembler.BuiltInToolNames`、discovered tools、skill tools surface 一致，不保留 autonomy reduced subset。
5. NPC prompt 包含 `BuildSkillsMandatoryPrompt()` 产出的同源技能约束文案。
6. private-chat/debug 的 descriptor `DisplayName`、`PackRoot`、persona seed 来源于 pack provenance，而不是 `ResolveDisplayName(...)` 或 `manual-debug` 伪值。
7. NPC runtime `SOUL.md` 初次创建时来自 pack seed，而不是包含 `# Hermes Agent Identity` 默认模板。
8. Haley/Penny 仍各自写入本地 transcript/memory/soul，不发生跨 NPC 或 Desktop 污染。

### 需关注的残余风险

1. full parity 会把 discovered/global capability surface 带入 autonomy/debug；本轮不把它当成合法差异来源，只把它当成必须被测试和记录的运行风险。
2. persona seeding 若误落到 `SoulService` 全局默认逻辑，可能伤到 Desktop 启动语义；应把 seed 责任放在 NPC runtime 层。
3. private-chat 若未来需要并发多 NPC 会话，需要单独的新 ADR；本轮明确不做 per-NPC concurrent conversations。

## 架构决策记录（ADR）

### 决策

采用单一 Desktop/NPC composition contract。Desktop、private-chat NPC、autonomy/debug NPC 都从这个 contract 获得同源 capability surface；差异只允许来自 persona、session、namespace、memory/transcript path。本轮同时明确：
- autonomy/debug 不保留 reduced tool subset；
- private-chat 会话模型为 `single active private-chat session per bridge`；
- private-chat/debug 的 persona provenance 必须来自 pack/manifest/catalog/descriptor，而不是局部硬编码。

### 驱动因素

1. 用户明确要求 Desktop 与 NPC 差异只能是人格、会话、命名空间、记忆目录。
2. 当前问题根因是 Desktop/NPC 双源装配，加上 private-chat/autonomy/debug 的入口分叉。
3. Penny 生产路由、skills guidance、persona seed 都依赖同一个 descriptor/composition provenance 才能稳定。

### 备选方案

1. 只统一 NPC 两条入口，不迁 Desktop：
   - 被拒绝，因为仍保留 Desktop/NPC 双源事实。
2. 共享 source 但对 autonomy 引入 reduced subset 或 runtime policy gating：
   - 被拒绝，因为这会让差异超出 persona/session/namespace/memory path，违背当前目标。
3. 直接复用 Desktop 全局 DI graph：
   - 被拒绝，因为会破坏 NPC 本地记忆、transcript 与 soul 隔离。

### 选择原因

这是唯一同时满足“同源装配”和“NPC 本地隔离”的方案，而且能通过测试直接锁住 Desktop/NPC 是否真的共用一个 contract。

### 影响

1. `App.xaml.cs` 需要把现有 Desktop-only 装配逻辑迁移到共享 contract，改动面中等。
2. private-chat/debug 需要补入 pack provenance 解析链，删除身份硬编码。
3. autonomy/debug 会获得 full parity capability surface，相关 side-effect 风险要靠测试与后续运营决策管理，而不是靠本轮保留 subset。
4. 旧 PRD 里 “NPC only safe tools” 的语义被本轮明确 supersede。

### 后续事项

1. 如果未来业务真的要对 autonomy 引入能力限制，应作为新的产品决策和 ADR，而不是回到隐式分叉注册。
2. 如果未来需要多 NPC 并发 private-chat，会作为新的 session model 变更单独规划。
