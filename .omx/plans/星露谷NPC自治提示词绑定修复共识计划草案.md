# 星露谷 NPC 自治提示词绑定修复共识计划草案

## Plan Summary

**Plan saved to:** `.omx/plans/星露谷NPC自治提示词绑定修复共识计划草案.md`

**Scope:**
- 1 个定向业务修复，覆盖 autonomy prompt 装配、Stardew gaming skill 根目录解析、runtime/DI 接线与测试回归
- 预计复杂度：MEDIUM

**Key Deliverables:**
1. 让 autonomy agent 稳定收到 persona pack 的 `facts.md` / `voice.md` / `boundaries.md` 与 `skills.json.required` 指向的 Stardew gaming markdown 技能正文
2. 用确定性的 DI 根目录策略、fail-fast 错误语义与 tool surface 一致性测试，锁定这条提示词绑定链路

## Requirements Summary

本次仅修复 **NPC autonomy prompt 绑定缺口**，不做行为架构改道。

必须满足：

1. `NpcAutonomyLoop` 继续保持“观察事实 -> 交给 agent 自主决策”，不新增事件驱动 NPC 行为。
2. host/background service 不替 NPC 决定移动目标、移动时机或动作；移动仍只能由 agent 基于提示词与工具自主决定。
3. 行为调整只能来自提示词、persona、skill guidance；不靠 C# 分支逻辑硬推 `move`。
4. `facts.md`、`voice.md`、`boundaries.md`、`skills.json.required` 必须正式进入 autonomy system prompt。
5. `skills.json.required` 指向的 Stardew skill 正文必须从现有 markdown 文件读取，不把 skill 正文硬编码进 C#。
6. `skills/gaming/*.md` 的根目录解析策略在本轮定死：**由显式 DI 注入 Stardew gaming skill root provider / root path；测试传临时 `skills/gaming`；生产由 Desktop composition 基于现有 skills root / bundled skills resolution 注入 `Path.Combine(skillsRoot, "gaming")`；loader 不自己靠 cwd 猜路径。**
7. 缺失 `skills.json` 文件、缺失 required skill 文件或缺失 persona `facts/voice/boundaries` 文件时必须 fail-fast，错误信息要带 `skill id` 或具体 `file path`，不允许 silent fallback。
8. “无事件驱动 / 无 host 代决策”必须落实为**可执行边界检查**：
   - 允许改动范围限定为 prompt binding、supplement builder/loader、DI/root provider、相关测试。
   - `NpcAutonomyLoop` 不新增事件类型分支、不新增 host-side `action submit`，继续保持 “Events are context only” 语义。
   - `StardewNpcAutonomyBackgroundService` / `StardewAutonomyTickDebugService` 只能构造 supplement 并传入 binding request，不得新增 `event -> move` 或 host-side direct move submit 分支。
   - 审查新增 `new GameAction(...Move...)`、`GameActionType.Move`、`SubmitAsync(action)`、按 event type 决策 move 的新增位置时，必须确认新增位置只存在于工具/既有 action controller 路径或测试中。
9. 不把 visible walking animation 混入本次实现；该项仅列为后续表现层风险。

非目标：

- 不改 `stardew_move` 工具协议
- 不改 bridge move 的瞬移式表现
- 不新增第二套 NPC capability 装配体系
- 不把通用 `SkillManager` 改造成扫描所有普通 markdown
- 不把 visible walking animation、路径寻路表现层或自动行走动画纳入本轮

## Evidence-Based Conclusion

基于当前仓库证据，问题根因不是 move 工具链缺失，而是 **autonomy prompt 缺失人格与 Stardew 行为指导，且 Stardew gaming skill 的根目录契约尚未在该链路上定死**：

1. `src/runtime/NpcRuntimeBindings.cs` 中 `NpcRuntimeAutonomyBindingRequest` 仍没有 `SystemPromptSupplement` 字段，而 private-chat request 已有。
2. `src/runtime/NpcRuntimeSupervisor.cs` 的 autonomy handle 仍以 `systemPromptSupplement: null` 创建 agent，且 autonomy rebind key 未纳入 supplement 维度。
3. `src/runtime/NpcRuntimeContextFactory.cs` 当前只拼接默认 Stardew supplement 与 `SkillManager.BuildSkillsMandatoryPrompt()`；后者只覆盖 `SKILL.md` 型技能，不覆盖 `skills/gaming/stardew-*.md`。
4. `Desktop/HermesDesktop/App.xaml.cs` 当前已存在现成的 skills root / bundled skills resolution 事实源：运行时 `SkillManager` 以 `skillsDir = Path.Combine(projectDir, "skills")` 初始化，并通过 `FindRepoSkillsDir()` 做 bundled skills 对账。这意味着生产路径完全可以从 composition 显式注入 `Path.Combine(skillsRoot, "gaming")`，不需要 loader 自己猜 cwd。
5. `src/runtime/NpcNamespace.cs` 只负责把 persona pack 复制进 runtime namespace 并种 `SOUL.md`，但 autonomy prompt 生产链没有消费 `facts.md` / `voice.md` / `boundaries.md` / `skills.json`。
6. `src/game/stardew/personas/haley/default/skills.json` 与 `penny/default/skills.json` 都要求 `stardew-core`、`stardew-social`、`stardew-navigation`，而这些正文已存在于 `skills/gaming/*.md`，只是没有被 autonomy prompt 装配进去。
7. `stardew_move`、`stardew_status`、`stardew_speak`、`stardew_task_status`、`stardew_open_private_chat` 与现有 `AgentCapabilityAssembler` / `StardewNpcTools` 工具链已存在，因此当前更像“agent 不知道何时、如何应该 move”，不是“系统没有 move 能力”。
8. `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs` 已经验证过一部分 autonomy tool surface 名称，因此本轮应进一步把“required skill guidance 与 move 工具链同时存在”写成显式快照回归，而不是只测 prompt 文本片段。

## RALPLAN-DR Summary

### Principles

1. **自治决策边界不变。** host 只提供事实、persona 和技能指导，不替 NPC 做目标/动作决策。
2. **提示词修复优先于行为硬编码。** 所有修复都应落在 prompt/supplement 装配链，而不是新增事件驱动分支。
3. **资源以文件为事实源。** persona 与 Stardew skill 正文都应从现有 markdown / json 文件读取，不能把正文抄进 C#。
4. **路径事实由 composition 注入。** Stardew gaming skill 根目录由 DI 提供，不允许 loader 依赖 cwd、工作目录猜测或隐式全局状态。
5. **缺失 required 资源显式失败。** 对 `skills.json.required` 或 persona 关键文件的 silent fallback 会再次制造“看起来已接线、实则 agent 不具备能力”的假完成。
6. **根目录边界单一。** `IStardewGamingSkillRootProvider` 只消费 Desktop composition 已解析好的 active skills root；provider/loader 不复制 `FindRepoSkillsDir()`、`AppContext.BaseDirectory`、workspace/cwd 猜测逻辑。

### Decision Drivers

1. **业务闭口驱动。** Haley 能主动私聊却不能稳定自主 move，说明 autonomy 与 private-chat 的 prompt 能力面已经分叉。
2. **约束驱动。** 用户明确禁止事件驱动 NPC 行为、禁止 host 决定移动、禁止把 skill 正文硬编码进 C#，并明确要求 walking animation 不纳入本轮。
3. **验证驱动.** 必须能用测试证明 autonomy prompt 真实包含 persona 与 required Stardew guidance，同时 tool surface 仍保留完整 Stardew 工具链。

### Viable Options

#### 方案 A：在 autonomy binding 链上补 `SystemPromptSupplement`，由独立 supplement builder 从 persona pack + `skills.json.required` + 显式注入的 `skills/gaming` 根目录组装文本

优点：
- 改动面最聚焦，直接修复 autonomy 与 private-chat 的能力分叉
- 不污染 `SkillManager` 的通用语义，不会意外让所有普通 markdown 都进入 mandatory prompt
- 能把 supplement 内容与 tool surface snapshot 一起纳入 rebind / 回归测试
- 根目录策略可完全依附 Desktop composition 的现有 skills root 事实源

缺点：
- 需要新增一个明确的 supplement 组装/解析职责
- 需要补齐 required 资源缺失时的错误语义与测试

#### 方案 B：扩展 `SkillManager.BuildSkillsMandatoryPrompt()`，让它顺带扫描 `skills/gaming/stardew-*.md`

优点：
- 表面上复用现有 mandatory prompt 入口
- 不需要新增 autonomy 专属补充文本 builder

缺点：
- 语义污染大，`SkillManager` 当前明确以 `SKILL.md` 为技能入口，强行混入普通 markdown 会扩大影响面
- 无法自然表达“只对特定 NPC autonomy 注入 skills.json.required”
- 仍然无法自然解决 `skills/gaming` 根目录应由谁负责解析的问题

#### 方案 C：在 host/background service 里根据 NPC id 直接拼一些 move/social 提示文案

优点：
- 实现路径最短

缺点：
- 违反“行为调整只能通过提示词/persona/skill guidance，但不能把正文硬编码进 C#”的硬约束
- 会把 host 推向替 NPC 做策略判断的方向

### Recommended Option

推荐 **方案 A**。

原因：

- 它是唯一同时满足“只改提示词链路”“从文件读取”“不改 `SkillManager` 全局语义”“不让 host 替 NPC 决策”“根目录由 DI 明确注入”的方案。
- 它能精确修复当前缺口：让 autonomy 和 private-chat 一样支持补充 system prompt，但 autonomy 的补充内容来自 persona + required Stardew skill files。
- 它允许把 supplement 内容与 tool surface snapshot 一起纳入 autonomy rebind 与测试闭环，避免“prompt 有了但工具面掉了”的伪修复。

## Implementation Steps

### 步骤 1：补齐 autonomy binding 的 supplement 通道与重绑语义

目标：
- 让 autonomy 与 private-chat 一样具备 `SystemPromptSupplement` 通道，并把其纳入 rebind。

涉及文件：
- `src/runtime/NpcRuntimeBindings.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`

实施要点：
- 给 `NpcRuntimeAutonomyBindingRequest` 增加 `SystemPromptSupplement` 字段。
- 在 `NpcRuntimeSupervisor.GetOrCreateAutonomyHandleAsync(...)` 与 `CreateAutonomyHandle(...)` 的 rebind key 中纳入 supplement 文本或其稳定指纹。
- 在 `CreateAutonomyHandle(...)` -> `CreateAgentHandle(...)` 链路里把 supplement 传入 `NpcRuntimeContextFactory.Create(...)`，不再固定传 `null`。
- 回归测试补一条：当 autonomy supplement 变化、`AdapterKey` 和 tool snapshot 不变时，autonomy handle 必须 rebind。

### 步骤 2：引入 persona + Stardew required skill supplement 组装器

目标：
- 把 persona pack 文件与 `skills.json.required` 正式组装成 autonomy supplement。

涉及文件：
- `src/runtime/NpcNamespace.cs`
- `src/game/core/NpcPackManifest.cs`
- `src/game/core/FileSystemNpcPackLoader.cs`
- `src/games/stardew/StardewNpcRuntimeBindingResolver.cs`
- 建议新增一个 runtime/stardew 邻近的 prompt builder / supplement builder 文件
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`

实施要点：
- 以 runtime namespace 中已 seed 的 persona 目录为人格资源读取源，消费 manifest 指定的 `factsFile`、`voiceFile`、`boundariesFile`。
- 读取 pack 中 `skills.json`，本轮只消费 `required`；`optional` 明确保留到后续需求。
- supplement builder 输出结构化分节文本，至少包含：
  - `## Persona Facts`
  - `## Persona Voice`
  - `## Persona Boundaries`
  - `## Stardew Required Skills`
- builder 负责返回稳定文本或稳定指纹，供 autonomy rebind 使用。
- `skills.json` 文件本身缺失或格式无效时立即失败；错误中必须包含 NPC / pack 上下文与 `skills.json` 路径。
- persona 关键文件缺失时立即失败；错误中必须包含 NPC / pack 上下文与缺失文件路径。

### 步骤 3：把 `skills/gaming` 根目录策略定死为显式 DI provider

目标：
- 让 `skills.json.required` 到 markdown 文件的定位完全可测、可复现、与 cwd 无关。

涉及文件：
- `Desktop/HermesDesktop/App.xaml.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewAutonomyTickDebugService.cs`
- 建议新增 `IStardewGamingSkillRootProvider` / 等价 provider 与 markdown resolver / loader
- 相关 Runtime / Stardew 测试文件

实施要点：
- 新增显式的 `IStardewGamingSkillRootProvider` / 等价 Stardew gaming skill root provider 注入契约。
- provider 只消费 Desktop composition 已解析好的 active skills root，并向 loader 暴露 `Path.Combine(activeSkillsRoot, "gaming")` 或等价结果。
- provider/loader **不得**复制 `FindRepoSkillsDir()`、`AppContext.BaseDirectory`、workspace/cwd 猜测逻辑；路径事实只能来自 composition 注入。
- 测试环境传临时目录下的 `skills/gaming`。
- 生产环境由 Desktop composition 基于现有 skills root / bundled skills resolution 注入 `Path.Combine(skillsRoot, "gaming")`。
- skill id 到 markdown 文件的映射由专门 resolver 负责，例如 `stardew-navigation -> stardew-navigation.md`；正文仍从文件读取，不做字符串硬编码。
- `required` skill 缺文件时 fail-fast；错误信息必须同时包含 `skill id` 和期望文件路径。

### 步骤 4：把 supplement 接入 Stardew autonomy 入口，并固化资源缺失 fail-fast 语义

目标：
- 让 background service/debug service 在创建 autonomy handle 前统一构造 supplement，并把缺失资源处理成可断言的入口语义，而不是无限重试。

涉及文件：
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewAutonomyTickDebugService.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`

实施要点：
- 在 `GetOrCreateAutonomyHandleAsync(...)` 前构造 `SystemPromptSupplement` 并传入 request。
- 不改变 `NpcAutonomyLoop` 的 observe-decide-act 节奏；它仍只拿 observation/event facts + 更完整的 system prompt。
- 不新增任何“当看到某类事件就自动 move”之类的代码路径。
- `StardewAutonomyTickDebugService.RunOneTickAsync(...)` 在 persona/skills 资源缺失时返回 `Success = false`，且 `FailureReason` 必须包含 NPC / pack 上下文，以及缺失的 `skill id + file path` 或 `skills.json path`。
- `StardewNpcAutonomyBackgroundService` 在 persona/skills 资源缺失时不把该错误视为可恢复的普通 LLM tick 失败；推荐语义定为 **paused + LastError/PauseReason**：
  - 该 NPC runtime 被标记为 paused；
  - `LastError` 或 `PauseReason` 明确带缺失资源路径 / `skill id`；
  - snapshot/log 可断言包含上述缺失上下文；
  - 不进入无限重试环。
- `NpcRuntimeContextFactory` 继续负责把默认 Stardew supplement 与传入 supplement 拼接；不在这里做业务判断。

### 步骤 5：迁移现有测试夹具到合法 `skills.json` 结构，并提供临时 `skills/gaming` fixture

目标：
- 先把现有测试前提修正为真实契约，再在其上补 prompt / fail-fast / 边界回归。

涉及文件：
- 现有使用旧 `skills.json` 夹具的 Stardew / Runtime 相关测试文件
- `Desktop/HermesDesktop.Tests` 下对应 fixture / 临时目录构造代码

实施要点：
- 将旧测试中的 `[]` 结构迁移为合法对象结构：最少改为 `{"required":[],"optional":[]}`。
- 对需要声明 required skill 的测试，按场景写成 `{"required":["stardew-core", ...],"optional":[]}`。
- 为测试提供临时 `skills/gaming` fixture，至少覆盖 `stardew-core.md`、`stardew-social.md`、`stardew-navigation.md` 的合法读取路径。
- fixture 迁移后，所有 prompt capture / fail-fast / background pause 语义测试都基于该合法结构，而不是基于历史无效输入偶然通过。

### 步骤 6：用测试、diff 和 grep 守卫同时锁定 prompt 内容、tool surface、fail-fast 与非目标边界

目标：
- 验证的是提示词供应链和能力面闭环，并用可执行边界守卫证明本轮没有偷偷演化成事件驱动或 host-side move 代决策。

涉及文件：
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- 如需要，可补 `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeContextFactoryTests.cs`

实施要点：
- 增加 capturing chat client，断言 autonomy system prompt 实际包含：
  - `facts.md` 内容
  - `voice.md` 内容
  - `boundaries.md` 内容
  - `stardew-core.md` 内容
  - `stardew-social.md` 内容
  - `stardew-navigation.md` 内容
- 增加 autonomy handle 能力/工具快照一致性测试，断言 tool surface 仍包含：
  - `stardew_status`
  - `stardew_move`
  - `stardew_speak`
  - `stardew_task_status`
  - `stardew_open_private_chat`
- 在同一类回归中同时确认：required skill guidance 已进入 prompt，且 `stardew_move` 工具链仍存在，避免只补 prompt 或只保工具的半修复。
- 关键联合回归建议固定命名为 `RunOneTickAsync_HaleyInjectsRequiredPersonaSkillsAndPreservesStardewTools` 或等价名称，避免执行阶段把 prompt 与 tool surface 断言拆散后失去联动语义。
- 增加 fail-fast 测试：
  - `skills.json` 文件缺失或格式无效时，debug 入口应返回带 NPC / pack 上下文与 `skills.json` 路径的明确失败
  - `skills.json.required` 声明了缺失 skill 文件时，应返回带 `skill id + file path` 的明确失败
  - persona `facts/voice/boundaries` 缺失时，应返回带缺失文件路径的明确失败
- 增加 background service 语义测试：
  - 资源缺失时，将该 NPC runtime 标记为 paused；
  - `LastError` 或 `PauseReason`、snapshot、log 至少一处可断言包含缺失资源路径 / `skill id`；
  - 不发生无限重试型重复调度。
- 增加 rebind 测试：supplement 指纹变化时 autonomy handle generation 增长。
- 增加文件边界 / grep 守卫建议，执行阶段至少人工或脚本化审查：
  - `rg -n "GameActionType\\.Move|new\\s+GameAction\\(.*Move|SubmitAsync\\(action\\)" src Desktop/HermesDesktop`
  - `rg -n "event.*move|move.*event|eventType" src/game/stardew src/runtime`
  - 逐条确认新增命中只出现在工具路径、既有 action controller 路径或测试中。
- diff 守卫建议：
  - `git diff -- src/runtime/NpcAutonomyLoop.cs src/games/stardew/StardewNpcAutonomyBackgroundService.cs src/games/stardew/StardewAutonomyTickDebugService.cs`
  - 重点确认 `NpcAutonomyLoop` 没有新增事件类型分支或 host-side `SubmitAsync(action)`；
  - 重点确认两个 service 的新增改动仅为 supplement 构造、binding request 透传、paused/failure 语义与日志/快照断言支撑。

## Acceptance Criteria

1. 对 Haley 或 Penny 创建 autonomy handle 时，发送给 chat client 的 system messages 中必须同时包含：
   - persona facts 文本
   - persona voice 文本
   - persona boundaries 文本
   - `stardew-core` 正文片段
   - `stardew-social` 正文片段
   - `stardew-navigation` 正文片段
2. autonomy handle 的 tool surface 快照必须仍保留：
   - `stardew_status`
   - `stardew_move`
   - `stardew_speak`
   - `stardew_task_status`
   - `stardew_open_private_chat`
3. 至少一条回归测试必须同时证明：required skill guidance 已进入 prompt，且 `stardew_move` 工具链仍存在。
4. `NpcRuntimeAutonomyBindingRequest` 支持 `SystemPromptSupplement`，且 supplement 改变时 autonomy handle 不得继续复用旧 generation。
5. `SkillManager.BuildSkillsMandatoryPrompt()` 的现有行为保持不变；Stardew markdown 注入来自独立 loader/supplement 路径。
6. `skills/gaming` 根目录由显式 `IStardewGamingSkillRootProvider` / 等价 provider 注入，测试传临时 `skills/gaming`，生产通过 Desktop composition 基于已解析好的 active skills root 注入 `Path.Combine(skillsRoot, "gaming")`；provider/loader 中不存在复制 `FindRepoSkillsDir()`、`AppContext.BaseDirectory`、workspace/cwd 猜路径的实现。
7. 缺失 `skills.json` 文件或 `skills.json` 格式无效时：
   - debug 入口 `RunOneTickAsync(...)` 返回 `Success = false`；
   - `FailureReason` 明确包含 NPC / pack 上下文与 `skills.json` 路径。
8. 缺失 `skills.json.required` 对应 markdown 文件时：
   - debug 入口返回显式失败；
   - `FailureReason` 明确包含缺失的 `skill id` 与期望文件路径。
9. 缺失 persona `facts.md`、`voice.md` 或 `boundaries.md` 时，debug 入口必须显式失败，错误信息应指出具体缺失文件路径；不得 silent fallback。
10. background service 遇到 persona/skills 资源缺失时，应将该 NPC runtime 标记为 **paused**，并在 `LastError` 或 `PauseReason`、snapshot/log 中至少一处包含缺失资源路径 / `skill id`；不得把它当作普通可恢复 LLM tick 失败无限重试。
11. 测试夹具已迁移到合法 `skills.json` 结构；旧 `[]` 不再作为有效 fixture 输入，且存在临时 `skills/gaming` fixture 覆盖 required skill 正文读取。
12. `NpcAutonomyLoop` 仍只基于事实调用 agent，不新增事件类型分支，不新增 host-side `action submit`，继续保持 “Events are context only”。
13. `StardewNpcAutonomyBackgroundService` / `StardewAutonomyTickDebugService` 不存在新增 `event -> move` 或 host-side direct move submit 分支；新增代码只限 supplement 构造、binding request 透传、paused/failure 语义、日志/快照与测试支撑。
14. grep/diff 守卫审查结果表明：新增 `new GameAction(...Move...)`、`GameActionType.Move`、`SubmitAsync(action)`、按 event type 决策 move 的命中位置只出现在工具/既有 action controller 路径或测试中。
15. 本轮不引入 visible walking animation 或其它表现层自动行走机制。

## Risks and Mitigations

1. **风险：把 Stardew markdown 注入做成 `SkillManager` 全局行为，扩大影响面。**
   - 缓解：单独增加 Stardew supplement loader，维持 `SkillManager` 只面向 `SKILL.md` 的现状。

2. **风险：autonomy handle 继续缓存旧提示词，修复后看似已接线但运行仍用旧 prompt。**
   - 缓解：把 supplement 文本或其稳定指纹纳入 autonomy rebind key，并补 generation 测试。

3. **风险：路径解析依赖 cwd 或隐式全局状态，桌面运行与测试环境表现不一致。**
   - 缓解：引入显式 `skills/gaming` root provider；测试与生产都通过 DI 注入，loader 不做目录猜测。

4. **风险：执行阶段顺手把 walking animation、工具协议或事件驱动也带进来，导致范围失控。**
   - 缓解：在计划与验收里把动画、工具层和事件驱动明确列为非目标；并用 `git diff` + `rg` 守卫检查 `NpcAutonomyLoop` 与两个 service 没有新增 host-side move 分支。

5. **风险：只验证 prompt 文本，遗漏工具面回归，最终 agent 看到指导却拿不到 move/speak/status 能力。**
   - 缓解：把 required skill guidance 与五个 Stardew 工具的共存写成同一条能力快照回归。

6. **风险：背景服务把资源缺失当作瞬时 LLM 失败处理，导致同一 NPC 无限重试刷日志。**
   - 缓解：计划明确采用 paused + `LastError`/`PauseReason` 语义，并补 snapshot/log 可断言测试。

## Verification Steps

建议执行时至少跑以下验证：

1. `dotnet test .\\Desktop\\HermesDesktop.Tests\\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcRuntimeSupervisorTests|FullyQualifiedName~StardewAutonomyTickDebugServiceTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~NpcRuntimeContextFactoryTests"`
2. 若新增了专门的 Stardew gaming skill root provider / markdown resolver 测试，单独追加该测试类过滤执行。
3. 人工抽查一条 autonomy prompt capture，确认不是只出现标题，而是 persona 与 required skill 正文片段真实进入 system prompt。
4. 人工或自动抽查一条 autonomy tool surface 快照，确认五个 Stardew 工具均存在，且 required skill guidance 与 `stardew_move` 工具链同时存在。
5. grep 守卫：
   - `rg -n "GameActionType\\.Move|new\\s+GameAction\\(.*Move|SubmitAsync\\(action\\)" src Desktop/HermesDesktop`
   - `rg -n "event.*move|move.*event|eventType" src/runtime src/game/stardew`
   - 审查新增命中，确认仅出现在工具/既有 action controller 路径或测试中。
6. diff 守卫：
   - `git diff -- src/runtime/NpcAutonomyLoop.cs src/games/stardew/StardewNpcAutonomyBackgroundService.cs src/games/stardew/StardewAutonomyTickDebugService.cs`
   - 确认 `NpcAutonomyLoop` 无新增事件类型分支或 host-side `SubmitAsync(action)`；
   - 确认两个 service 无新增 `event -> move` 或 direct move submit 分支。

验证重点：

- prompt 内容
- tool surface snapshot
- fail-fast 错误
- debug 入口 `Success=false` / `FailureReason`
- background paused + `LastError`/`PauseReason`
- autonomy rebind generation
- 无新增事件驱动行为分支
- 无 visible walking animation 范围蔓延

## ADR 草案

### Decision

采用“**在现有 autonomy binding 链上补 `SystemPromptSupplement`，并由独立的 persona + Stardew required skill supplement builder，从 runtime persona 文件与 `skills.json.required` 对应 markdown 组装 autonomy prompt；`skills/gaming` 根目录由显式 DI provider 注入**”的方案。

### Drivers

- 需要修复 autonomy 与 private-chat 的提示词能力分叉
- 必须通过文件读取而不是硬编码把人格与技能正文注入 prompt
- 必须把 `skills/gaming` 根目录解析从 cwd 猜测改成确定性的 composition 注入
- 不能引入事件驱动行为或 host 代决策

### Alternatives Considered

- 扩展 `SkillManager.BuildSkillsMandatoryPrompt()` 去扫描普通 markdown：放弃，因为会污染全局技能系统语义
- 在 background service 里按 NPC id 手写 move/social guidance：放弃，因为违反硬约束且不可维护
- 让 loader 自己从 cwd / base directory 猜 `skills/gaming`：放弃，因为测试与生产不可控，且与现有 composition 事实源重复

### Why Chosen

- 变更面最小，直接命中当前缺口
- 不改变自治循环职责边界
- 路径来源清晰，可用 DI 与测试完全固定
- 最容易通过 capture prompt、tool snapshot 与 rebind 测试建立稳定回归保护

### Consequences

- runtime/autonomy binding 将新增 supplement 概念并进入 rebind key
- 需要引入一个新的 Stardew markdown loader / supplement builder 责任
- `SkillManager` 与 Stardew markdown guidance 将形成“通用 mandatory skills + 业务专属 supplement”的双层提示词结构
- Desktop composition 需要显式承担 `skills/gaming` 根目录注入职责

### Follow-ups

- 后续若需要 `skills.json.optional`，可在同一 supplement builder 上扩展为“存在则注入，不存在不阻塞”
- visible walking animation 单独立项处理，不与本轮提示词修复耦合
- 若未来 private-chat 也要复用 persona facts/voice/boundaries，可在本轮 autonomy builder 稳定后评估是否共享

## Available Agent Types Roster

- `planner`
- `architect`
- `critic`
- `executor`
- `debugger`
- `test-engineer`
- `verifier`
- `explore`

## Staffing Guidance

### `ralph` 路线

适用：
- 该修复跨 runtime 绑定、Stardew 入口、composition 注入与测试，但写入面仍相对集中，适合单 owner 顺序推进。

建议编组：
1. `executor`，reasoning `high`
   - 负责 autonomy binding、supplement builder、Stardew gaming skill root provider、Stardew 入口接线
2. `test-engineer`，reasoning `medium`
   - 负责 prompt capture、tool snapshot、fail-fast、rebind regression tests
3. `verifier`，reasoning `high`
   - 负责最终验证“补的是 prompt 链路，不是行为硬编码，也没丢工具面”

启动提示：
- `$ralph 执行 .omx/plans/星露谷NPC自治提示词绑定修复共识计划草案.md，严格按提示词链路修复，不引入事件驱动行为，也不纳入 walking animation`

### `team` 路线

适用：
- 想并行拆成“实现 lane + composition/root provider lane + 测试 lane”，并在最后集中收口。

建议编组：
1. Lane A: `executor`，reasoning `high`
   - `src/runtime/NpcRuntimeBindings.cs`
   - `src/runtime/NpcRuntimeSupervisor.cs`
   - supplement builder / loader
2. Lane B: `executor` 或 `debugger`，reasoning `high`
   - `Desktop/HermesDesktop/App.xaml.cs`
   - `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
   - `src/games/stardew/StardewAutonomyTickDebugService.cs`
   - Stardew gaming skill root provider 与 fail-fast 行为
3. Lane C: `test-engineer`，reasoning `medium`
   - Runtime/Stardew 测试补强与 capture client / tool snapshot 校验
4. 收口：`verifier`，reasoning `high`

启动提示：
- `$team 基于 .omx/plans/星露谷NPC自治提示词绑定修复共识计划草案.md 分 3 lanes 执行：runtime binding、composition/root provider、tests/verification`
- 若用 OMX CLI：`omx team 3:executor "基于 .omx/plans/星露谷NPC自治提示词绑定修复共识计划草案.md 执行 runtime binding / root provider / tests 三条并行 lane"`

### Team Verification Path

1. 先由测试 lane 补 prompt capture、tool snapshot 与 fail-fast 回归。
2. runtime binding lane 完成后，先验证 autonomy rebind generation。
3. composition/root provider lane 完成后，验证 background/debug 两个入口都能把 supplement 与显式 `skills/gaming` 根目录传到 autonomy handle。
4. 最终由 `verifier` 检查：
   - 没有新增事件驱动分支
   - 没有 host 代 NPC 决策
   - prompt capture 真实包含 persona + required skill 正文
   - tool surface 保留五个 Stardew 工具
   - 本轮未把 visible walking animation 带入实现范围

## Does This Plan Capture Your Intent?

- `proceed` - 我给出推荐的 `$ralph` / `$team` 执行起手式
- `adjust [X]` - 我按指定约束、步骤或验收项改计划
- `restart` - 丢弃本草案重新规划
