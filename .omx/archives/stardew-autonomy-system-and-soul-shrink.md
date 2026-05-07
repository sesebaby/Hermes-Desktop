# Stardew Autonomy SystemPrompt 与 SoulContext 收缩计划

## Scope

- 仅规划，不实现。
- 仅包含两步：
  1. 缩 shared `SystemPrompt`
  2. 缩 `SoulContext`
- 不把 `supplement` 继续作为主方向。
- 保持单轨，不引入第二套 prompt 架构。
- 继续使用同一套 `PromptBuilder`。

## Repository Facts

- `PromptBuilder` 组装顺序固定为：`SoulContext` -> `SystemPrompt` -> `PluginSystemContext` -> `SessionState` -> `ActiveTaskContext` -> recent turns -> current user，当前这两层都直接进入 system surface。[src/Context/PromptBuilder.cs:76](D:/GitHubPro/Hermes-Desktop/src/Context/PromptBuilder.cs:76)
- `ContextManager` 对 Stardew NPC runtime 已记录 `systemPromptChars`、`soulContextChars`、`pluginSystemContextChars`，所以这轮可直接用现有日志验证，不需要新建第二条观测链。[src/Context/ContextManager.cs:169](D:/GitHubPro/Hermes-Desktop/src/Context/ContextManager.cs:169)
- `NpcRuntimeContextFactory` 已经在 `channel=autonomy` 时关闭 global mandatory skills catalog，因此这轮不应再把 mandatory-skills 处理当主轴。[src/runtime/NpcRuntimeContextFactory.cs:69](D:/GitHubPro/Hermes-Desktop/src/runtime/NpcRuntimeContextFactory.cs:69)
- 当前 shared `SystemPrompt` 仍是通用 desktop agent 口径，包含 conversation、planning、scheduling、subagents、local media、runtime facts 等大量与 Stardew autonomy 无关的说明。[src/Core/SystemPrompts.cs:13](D:/GitHubPro/Hermes-Desktop/src/Core/SystemPrompts.cs:13)
- `SystemPrompts.BuildFromBase` 会额外挂载 `MemoryGuidance`、`SessionSearchGuidance`、`SkillsGuidance`，这些文字现在也是通用 agent 口径，可继续压缩或按 autonomy 模式选择性关闭。[src/Core/SystemPrompts.cs:80](D:/GitHubPro/Hermes-Desktop/src/Core/SystemPrompts.cs:80)
- `SoulService.AssembleSoulContextAsync()` 当前默认拼接完整 `SOUL.md`、截断版 `USER.md`、截断版项目 `AGENTS.md`、mistakes、habits，没有 runtime-mode 区分；这正是 soul 层可继续收缩的位置。[src/soul/SoulService.cs:139](D:/GitHubPro/Hermes-Desktop/src/soul/SoulService.cs:139)

## Principles

1. NPC 行为契约优先于字符数优化，任何删减都必须保住既有 autonomy 行为。
2. 单轨治理：只在现有 `PromptBuilder` / `ContextManager` / `SoulService` 上做 mode-aware 收缩，不引入 shadow prompt path。
3. 删除优先于改写，尤其优先删除与游戏 autonomy 无关的通用 desktop 指导。
4. 共享源继续共享，但允许游戏专用项目把 shared `SystemPrompt` 改成 Stardew-first 版本。
5. supplement 不再作为主方向；若 system/soul 仍超标，再单独复盘，不在本轮预埋补丁路线。

## Decision Drivers

1. 最新证据显示主超量已集中在 `systemPromptChars≈6682-6685` 与 `soulContextChars≈1331-1373`，而不是 supplement 或 builtin memory。
2. 项目已明确是游戏专用，通用 desktop runtime 保守兼容不再是约束，可以直接删除无关指导。
3. `NpcRuntimeContextFactory` 已对 autonomy 关闭 mandatory skills catalog，说明第一优先级应转向 base system prompt 与 soul 注入层，而不是再动 supplement。

## Viable Options

### Option A: 只做措辞压缩，保留通用 desktop prompt 结构

Pros
- 改动最小
- 对其他运行面的潜在影响最小

Cons
- 仍然保留大量与 Stardew autonomy 无关的桌面说明
- 很可能只能拿到小幅收益，难以匹配“游戏专用可删通用指导”的新约束
- 会把未来继续收缩的空间锁死在微调措辞里

### Option B: 把 shared `SystemPrompt` 改写成 Stardew-only 最小契约，并对 soul 注入做 autonomy 视角筛选

Pros
- 最符合“项目是游戏专用”的新约束
- 直接删掉无关块，收益最大
- 仍然保留同一套 `PromptBuilder`，只是 base prompt 与 soul 组装策略变成 autonomy-first

Cons
- 需要更严格的回归测试，防止删掉隐性行为提示
- 会影响所有走 shared prompt 的游戏 runtime 调用点，需要确认它们都能接受 Stardew-first 口径

### Option C: 先只缩 `SoulContext`，`SystemPrompt` 基本不动

Pros
- 对 shared base prompt 风险最低
- 容易快速验证 soul 降幅

Cons
- 无法利用“可删除通用 desktop 指导”的最大收益点
- 预期收益不足；`systemPromptChars` 仍会长期偏高
- 不能解决 shared prompt 里明显的无关内容堆积

## 推荐方案

选 `Option B`。

原因：

- 新硬约束已经把“保留通用 desktop agent 说明”明确排除了，因此继续保留通用 base prompt 只会浪费上下文预算。
- 当前现成架构已经允许在同一 `PromptBuilder` 下按 runtime 输入控制 prompt 组成；推荐继续走这条路，只改 shared base prompt 内容和 soul 组装策略，不新建第二套 builder。[src/runtime/AgentCapabilityAssembler.cs:78](D:/GitHubPro/Hermes-Desktop/src/runtime/AgentCapabilityAssembler.cs:78)
- `SoulContext` 当前是无模式差异的全量拼接，适合改成“autonomy 仅保留行为必需片段”的收缩方式，而不是继续赌后续 budget policy 去兜底。[src/soul/SoulService.cs:143](D:/GitHubPro/Hermes-Desktop/src/soul/SoulService.cs:143)

## 具体改动步骤

### Step 1: 缩 shared `SystemPrompt`

目标：
- 把 `src/Core/SystemPrompts.cs` 从通用 desktop agent 指导改成 Stardew autonomy 需要的最小共享契约。
- 同步压缩 `src/Core/MemoryReferenceText.cs` 中真正进入 autonomy system layer 的 guidance 文案。
- 保持同一 `PromptBuilder` 与 `BuildFromBase()` 入口，不引入第二套 prompt 组装。

涉及文件：
- `src/Core/SystemPrompts.cs`
- `src/Core/MemoryReferenceText.cs`
- `src/runtime/AgentCapabilityAssembler.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`

执行要点：
- 删除 `SystemPrompts.Default` 中与游戏 autonomy 无关的通用桌面说明：conversation/planning/scheduling/subagents/local media/runtime facts 的泛化叙述、重复 communication-style 段、与当前实际 autonomy 决策无关的通用 desktop safeguard。
- 将 shared base prompt 改写为 Stardew-first 最小合同，只保留：
  - 只使用当前注册工具
  - 观察优先，不猜世界状态
  - 缺历史先 `session_search`
  - 任务连续性走 `todo`
  - durable facts 才进 `memory`
  - 不能伪造执行结果
- 评估 `BuildFromBase()` 中的 guidance admission：
  - `MemoryGuidance` 改成更短、工具导向版本
  - `SessionSearchGuidance` 保留但压缩到一行合同
  - `SkillsGuidance` 对 autonomy 默认关闭，除非发现当前行为真依赖它
  - `RuntimeFactsGuidance` 直接删除或并入更短的“不要猜实时状态，靠工具/游戏状态”句子
- 若仍需保留 generic caller 兼容，优先通过 `AgentPromptServices.BaseSystemPrompt` 在 `NpcRuntimeContextFactory` 提供 Stardew-only base prompt；但首选是直接把 shared default 改成游戏专用版本，因为项目已不再需要通用 desktop runtime 说明。[src/runtime/NpcRuntimeContextFactory.cs:69](D:/GitHubPro/Hermes-Desktop/src/runtime/NpcRuntimeContextFactory.cs:69)

完成标准：
- `systemPromptChars` 相比当前 `6682-6685` 明显下降，目标先压到 `<= 3500`。
- autonomy prompt 不再包含通用 desktop runtime 的功能承诺或沟通哲学段落。
- 仍然保留 NPC autonomy 必需的工具纪律与历史/任务连续性合同。

### Step 2: 缩 `SoulContext`

目标：
- 在不改变 NPC 行为语义的前提下，把 `SoulContext` 从“通用 Hermes identity + user + project rules + journals”改成 autonomy 可用的最小注入。
- 继续由 `SoulService` 负责组装，避免新建第二个 soul 管道。

涉及文件：
- `src/soul/SoulService.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/Context/ContextManager.cs`

执行要点：
- 给 `SoulService.AssembleSoulContextAsync()` 增加轻量 options/context 参数，而不是新增独立方法族；例如允许调用方声明 `autonomy` / `npc-runtime` 模式。
- autonomy 模式下的 soul 组装策略建议：
  - `SOUL.md` 不再全量注入，只保留与 NPC autonomy 真相关的身份/行为原则摘要
  - `USER.md` 默认不注入，除非已证明某类 NPC 行为必须读取用户偏好
  - 项目 `AGENTS.md` 不再机械截断前 1500 chars，而是只抽取与 Stardew autonomy 明确相关的规则片段
  - mistakes/habits 默认关闭，除非已有自动化测试证明它们影响当前 NPC 行为
- `ContextManager` 保持同一调用链，只把当前无参 `AssembleSoulContextAsync()` 换成传 mode/options 的调用，日志字段继续复用 `soulContextChars`。[src/Context/ContextManager.cs:169](D:/GitHubPro/Hermes-Desktop/src/Context/ContextManager.cs:169)
- 不动 `pluginSystemContext`，因为 builtin memory 已明确不是本轮主方向。

完成标准：
- `soulContextChars` 相比当前 `1331-1373` 压到 `<= 500`，最好接近 `300-400`。
- 不再把完整通用 Hermes 自我叙述和大段项目规则无差别塞进 autonomy first call。
- 保住 NPC 所需的最小行为边界：工具执行边界、事实/观察优先、会话连续性边界。

## 验证策略

### 单元/结构验证

- 为 `SystemPrompts`/`MemoryReferenceText` 增加结构测试：
  - autonomy system prompt 不再出现 desktop-only 词汇或已删除的通用能力承诺
  - 仍包含 `session_search`、`todo`、`memory`、registered tools-only 这些必要合同
- 为 `SoulService` 增加 mode-aware 组装测试：
  - autonomy 模式下不再全量注入 `USER.md`、完整 `AGENTS.md`、journals
  - 仍保留最小 autonomy 行为边界文本
- 为 `NpcRuntimeContextFactory` / `AgentCapabilityAssembler` 增加装配测试：
  - 仍然只创建同一套 `PromptBuilder`
  - autonomy 继续沿用现有 shared builder，不出现第二条 prompt 组装路径

### 预算回归验证

- 复用现有 `autonomy_prompt_packet_built` 日志，比较实施前后：
  - `systemPromptChars`
  - `soulContextChars`
  - `pluginSystemContextChars`
  - `systemChars`
- 预期：
  - `systemPromptChars <= 3500`
  - `soulContextChars <= 500`
  - `systemChars` 从当前约 `8544-9845` 明显下降，目标先压到 `<= 5000-6500` 区间；若仍超预算，再下一轮专门处理 `SessionStateJson`，不在本轮掺入 supplement 返工

### 行为回归验证

- 跑现有 Stardew autonomy 相关测试，重点看：
  - first-call budget / classification
  - task continuity
  - status-tool budget
  - repo-backed skill coverage
- 增加 contract 测试，确认 prompt 中仍保留：
  - 使用注册工具，不捏造结果
  - 缺历史先 `session_search`
  - 任务连续性走 `todo`
  - durable facts 才进 `memory`

建议命令：

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Soul"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

### 运行时验证

- 检查 `%LOCALAPPDATA%\hermes\hermes-cs\logs\hermes.log`
- 对比 `autonomy_prompt_packet_built` 和 `autonomy_context_budget_completed`
- 关注：
  - `budgetMet`
  - `budgetUnmetReason`
  - `systemChars`
  - `activeTaskChars`
  - `currentUserChars`

## 风险

1. `SystemPrompt` 删得过猛，导致某些 NPC 行为其实依赖了旧的通用措辞。
   - 缓解：先把行为合同抽成显式结构测试，再删通用 prose。

2. `SoulContext` 去掉 `AGENTS.md` / journals 后，隐藏约束不再进入 autonomy 首轮。
   - 缓解：只保留与 Stardew autonomy 明确相关的规则片段，并用针对性测试覆盖 move / task continuity / no-fabrication / session_search contracts。

3. 把 shared default 改成游戏专用后，其他非 autonomy 游戏调用面出现提示词偏差。
   - 缓解：先搜索所有 `SystemPrompts.Default` / `BuildFromBase()` 调用点；若存在明显非游戏面仍依赖旧文案，则退到“同一 builder 下由 `BaseSystemPrompt` 为 NPC runtime 传 Stardew-only base”的同轨替代，不恢复通用 desktop prompt。

4. 本轮完成后 `systemChars` 仍未完全达标。
   - 缓解：按既定边界把下一轮聚焦到 `SessionStateJson`，不回头把 supplement 当主路线。

## ADR

### Decision

在同一套 `PromptBuilder` 架构下，把 shared `SystemPrompt` 改成 Stardew autonomy 优先的最小共享契约，并把 `SoulContext` 改成 mode-aware 的 autonomy 精简注入。

### Drivers

- 当前超量主要集中在 `systemPrompt` 与 `soulContext`
- 项目已明确是游戏专用，不再需要保守保留通用 desktop runtime 指导
- 必须保持单轨并避免回到 supplement 主方向

### Alternatives Considered

- 保留通用 desktop prompt，只做措辞压缩
- 只缩 soul，不动 shared base prompt

### Why Chosen

它最符合新的项目定位，删减收益最大，同时仍然复用同一 prompt 组装链路。

### Consequences

- shared default prompt 会更偏游戏专用
- autonomy first call 的 system layer 会显著变小
- 若后续还有上下文超量，下一轮应转向 `SessionStateJson`，而不是恢复通用说明或补一层 supplement

### Follow-ups

- 搜索并确认所有 shared prompt 调用面是否都能接受游戏专用口径
- 完成后记录新的 `systemPromptChars` / `soulContextChars` / `systemChars` 基线
- 若 `systemChars` 仍高于目标，再单独立项处理 `SessionStateJson`
