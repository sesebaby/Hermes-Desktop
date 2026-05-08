# Deep Interview Spec: Stardew NPC Autonomy Audit Resolution

## Metadata

- Profile: standard
- Context type: brownfield
- Final ambiguity: 0.16
- Threshold: 0.20
- Context snapshot: `.omx/context/autonomy-audit-validation-20260508T084753Z.md`
- Interview transcript: `.omx/interviews/autonomy-audit-resolution-20260508T085754Z.md`
- Source audit file: `agent-autonomy-audit.zh-CN.md`

## Intent

把 `agent-autonomy-audit.zh-CN.md` 里所有合理问题完整理清并持久化，避免后续上下文压缩或中断后丢失判断依据。后续修复不能只记住“高优先级三项”，也不能把配置化、测试滞后、工程边界项用“中长期”一笔带过。

## Desired Outcome

后续执行者打开本规格后，应能直接知道：

- 哪些审查项是源码事实正确且必须修。
- 哪些审查项方向合理但定位文件或措辞过时。
- 哪些审查项不是当前 bug，但必须进入配置/边界决策。
- 哪些审查项应明确保留不改，并用测试/文档防止误修。
- 每项的证据文件、修复意图和验收信号。

## Non-goals

- 不在 deep-interview 阶段直接改源码。
- 不删除 parent cloud decision / local executor split。
- 不把所有 prompt 约束都视为坏事；JSON 输出契约、工具 schema、权限/安全/事实正确性边界可以保留。
- 不新增依赖。
- 不把旧审查报告当权威；以当前源码、测试和 AGENTS.md 产品约束为准。

## Decision Boundaries

Codex/后续执行 lane 可以直接决定：

- 更新过时测试断言。
- 删除或改写明显替 agent 选择工具调用的提示。
- 把硬编码摘要改为更忠实的 skill 披露方式。
- 把 action timeout 的恢复策略从宿主自动决策改为 agent 可见事实/下一轮决策。
- 为合理但不应马上代码大改的配置项写明确计划和测试保护。

需要用户再次确认的事项：

- 是否要改变父云子本地的产品路由原则。
- 是否允许引入新的配置文件格式或迁移现有配置结构。
- 是否要让 autonomy parent 直接恢复普通工具调用，而不是继续输出 JSON intent。

## Issue Ledger

### A1. Skill 摘要由宿主关键词筛选，必须处理

Evidence:
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs:193`
- `BuildCompactSkillContract` 按 `skillId` 写死 `needle`，选择部分行作为 contract。

Why it matters:
- 这会让宿主替 agent 判断 skill 中哪些规则最重要。
- 它不是单纯 token 压缩，而是按代码关键词做注意力裁剪。

Repair intent:
- 用更忠实的披露方式替代关键词摘录。
- 可选方案：完整注入短 skill、按 skill 自身标题结构截取、或由 skill 文件显式提供 compact section。
- 不要用宿主代码维护“关键词优先级”。

Acceptance:
- 测试证明 `stardew-core/social/navigation/task-continuity/world` 的 prompt 不再由硬编码 needle 决定。
- 如果仍压缩，压缩规则必须来自 skill 资产自身，而不是 `switch skillId`。

### A2. 宿主自动补 `skill_view(...)` 调用，必须处理

Evidence:
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs:261`
- 测试当前显式断言这些补入内容：`Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs:195`

Why it matters:
- `skill_view(name=..., file_path=...)` 本身可以作为 skill 中的能力提示存在。
- 但宿主代码自动补具体调用，会从“告诉 agent 可查看”变成“替 agent 选择查看哪个文件”。

Repair intent:
- 如果 skill 原文已经包含 reference 指令，可以忠实保留。
- 不要由宿主代码在摘要缺失时补具体工具调用。
- prompt 应描述“参考文件存在/可查看”，具体是否调用由 agent 决定。

Acceptance:
- 删除 builder 中自动补 `skill_view(...)` 的逻辑。
- 更新测试，不再要求宿主自动补入；可要求 skill 原始资产中保留可查看 reference。

### A3. Action slot timeout 自动 cancel/pause/retry，必须处理

Evidence:
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:798`
- 超时后调用 `CancelAsync`，再 `PauseTrackerAsync`，并设置 `RetryAfterUtc`。

Why it matters:
- 这越过“执行器报告事实”的边界，变成宿主替 agent 决定失败恢复策略。
- agent 没有机会选择继续等待、换目标、观察、放弃或解释阻塞。

Repair intent:
- 宿主仍可释放资源、记录超时事实、保护 bridge 不挂死。
- 恢复策略应交给下一轮 agent 决策：把 `action_slot_timeout` 作为事实写入 runtime state/activity，然后唤醒 agent。
- 如果必须取消底层命令，取消应被建模为执行层资源清理，不应同时决定 agent 冷却/重试策略。

Acceptance:
- 超时测试证明 agent 下一轮能看到 timeout fact/status。
- 宿主不再直接把 timeout 转成固定 restart cooldown。
- 资源释放和 world coordination claim 仍有测试覆盖。

### A4. Autonomy prompt 中“父层不要写工具参数/调用工具”定位需修正后处理

Evidence:
- `src/runtime/NpcRuntimeContextFactory.cs` 当前不含 `Do not call tools`。
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeContextFactoryTests.cs:77` 仍断言包含 `Do not call tools`，该测试当前失败。
- 类似限制实际在 `src/runtime/NpcAutonomyLoop.cs:253-257`。

Why it matters:
- 审查报告定位旧了，但问题方向没有完全消失。
- JSON intent contract 应保留；但“不要写工具参数/不要调用工具”的措辞应尽量表达为协议和工具面边界，而不是思考方式命令。

Repair intent:
- 先修过时测试断言。
- 保留 raw JSON intent contract。
- 把父层/执行器分工写成接口事实：父层输出 intent，本地 executor 只看到 executor 工具。
- 避免让 prompt 承担工具权限控制；权限应由实际 tool surface 保证。

Acceptance:
- `NpcRuntimeContextFactoryTests.Create_AutonomyChannel_OmitsGlobalSkillsMandatoryIndex` 不再断言旧字符串。
- 测试覆盖父层 agent 不暴露 executor-only tools。
- prompt 不再含过时或误导性 “Do not call tools” 断言。

### A5. Local executor tool surface 硬编码，必须进入配置/边界决策

Evidence:
- `src/games/stardew/StardewNpcTools.cs:12`
- `CreateDefault` 和 `CreateLocalExecutorTools` 返回两套固定工具面。
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs:48` 当前断言固定 executor tools。

Why it matters:
- 父层/本地 executor 分工是合理产品设计。
- 但工具面写死在代码中，会让能力边界难以随不同 NPC、地图、阶段或调试模式调整。

Repair intent:
- 不把“分工”当 bug。
- 需要明确配置化方案：默认仍安全，但配置可以声明不同 agent/runtime surface。
- 第一版可以先抽出内部配置对象，不一定立刻暴露 UI。

Acceptance:
- 默认行为不变。
- 工具面来源从硬编码列表迁移到可测试的配置/策略对象。
- 测试验证默认 executor surface 与配置覆盖两条路径。

### B1. `SystemPrompts.Default = StardewNpcRuntime` 需要决策，不应误判为当前纯 bug

Evidence:
- `src/Core/SystemPrompts.cs:13`
- 当前 prompt 已包含 “host does not observe or choose the first step for you” 类边界。
- 审查报告提到的旧句子 `"Decide small next actions from observed game facts"` 当前源码不存在。

Why it matters:
- 默认 prompt Stardew-first 确实不够通用。
- 但当前项目 AGENTS.md 明确仓库主线是 Stardew/NPC，因此它不是最硬越界。

Repair intent:
- 不作为第一刀行为修复。
- 应写入配置化/资产化计划：未来把默认系统 prompt 从代码常量迁到 repo asset 或 guidance 文件。
- 同时保留当前产品约束：Stardew runtime 默认仍可用。

Acceptance:
- 文档/计划明确当前不是“审查报告旧句子”问题。
- 若实施，测试证明 prompt asset 缺失时有安全 fallback。

### B2. `RuntimeFactsGuidance` 是事实正确性边界，默认保留

Evidence:
- `src/Core/SystemPrompts.cs:26`

Why it matters:
- 它命令 agent 对实时事实使用工具。
- 但这属于 runtime truth/safety，不是替 NPC 决定 Stardew 行动。

Repair intent:
- 不作为 autonomy 越界修复。
- 可在后续 prompt 资产化时移动位置，但不要删除事实校验原则。

Acceptance:
- 保持 current time/date/files/git 等实时事实必须查工具的约束。
- 不把该项混入 Stardew autonomy 行为修复。

### B3. App lane 名固定，但 lane 配置已存在；需要配置边界说明

Evidence:
- `Desktop/HermesDesktop/App.xaml.cs:416`
- `src/LLM/ChatRouteResolver.cs:40` 按 lane 从配置解析 provider/model/base_url。

Why it matters:
- 固定 lane name 是代码结构事实。
- lane 背后的模型/provider 已可配置，所以“永久绑定固定模型”这一定性过重。

Repair intent:
- 保留固定语义 lane 名：`stardew_autonomy`、`stardew_private_chat`、`delegation`。
- 如要增强，配置的是 lane routing policy，而不是让 agent 自己决定运行在哪条 lane。

Acceptance:
- 文档/测试说明 lane 名是产品语义，不是模型锁死。
- 如果实现路由规则配置，默认路由保持现状。

### B4. AgentCapabilityAssembler 内建工具集静态注册，需作为通用能力面决策

Evidence:
- `src/runtime/AgentCapabilityAssembler.cs:19`
- `RegisterBuiltInTools` 固定注册 Hermes-native tools。

Why it matters:
- 对桌面主 agent，这是正常组合根能力面。
- 对 NPC/private/autonomy，需要依赖 channel/tool surface 防止过曝。

Repair intent:
- 不把它当 Stardew autonomy 主根因。
- 需要在后续能力面计划中明确哪些 agent/runtime 可以继承 built-ins，哪些必须显式 opt-in。

Acceptance:
- Autonomy parent 当前 `registerCapabilities: false` 的语义被测试保护。
- 私聊/桌面主 agent 的内建工具行为不被误删。

### B5. ChatLaneClientProvider flat-only delegation policy 需要落成真实配置或删除误导日志

Evidence:
- `src/LLM/ChatLaneClientProvider.cs:25`
- 读取 `delegation.max_spawn_depth`，但日志写死 `effectiveMaxSpawnDepth=1`。
- `scripts/sync-stardew-npc-config.ps1:172` 写入 `max_spawn_depth: 1`。

Why it matters:
- 当前更像“配置被记录但未真正参与 enforcement”。
- 它会误导后续执行者以为 depth policy 已完整实现。

Repair intent:
- 二选一：实现真正的 max depth enforcement，或把日志/配置改成明确“reserved/future setting”。
- 不应继续保留“看起来可配，实际固定”的状态。

Acceptance:
- 测试覆盖 max_spawn_depth 配置生效，或测试/文档声明该配置暂不生效且不会误导。

### B6. AgentTool 静态 agent type 是模板边界，不是当前 NPC bug，但应配置化记录

Evidence:
- `src/Tools/AgentTool.cs:98`
- `researcher/coder/analyst/planner/reviewer/general` 静态绑定 prompt 和 allowed tools。

Why it matters:
- 这是子 agent 工具的模板设计。
- 它不直接导致 Stardew NPC 自主循环越界，但属于同类“代码钦定角色/工具面”的能力面问题。

Repair intent:
- 不在 Stardew autonomy 修复中顺手大改。
- 进入通用 agent tool 配置化计划：允许 repo config 定义 agent type、prompt、allowed tools。

Acceptance:
- 默认 agent type 行为保持。
- 配置化时有 schema 和 fallback 测试。

### C1. 过时测试必须修，不可忽略

Evidence:
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeContextFactoryTests.cs:77`
- 已运行窄测试，失败于 `Assert.IsTrue`，因为源码已不含 `Do not call tools`。

Repair intent:
- 测试应表达当前真实需求：autonomy channel 不注入 global skills mandatory index，保留 JSON contract，避免 desktop/global web/browser guidance 污染。
- 不应继续要求旧字符串。

Acceptance:
- 窄测试通过。
- 新断言不把工具禁用写回 prompt。

### C2. 审查报告本身应更新或标注，不应作为修复权威

Evidence:
- `agent-autonomy-audit.zh-CN.md` 混合了当前事实、旧源码事实和过重定性。

Repair intent:
- 后续可以更新该报告，或新建一个 “审查修订 ledger” 文档。
- 执行时以本 spec 和源码为准。

Acceptance:
- 后续计划引用 `.omx/specs/deep-interview-autonomy-audit-resolution.md`，不直接引用旧报告作为唯一依据。

## Repair Ordering Without Dropping Issues

这里不用“中长期”丢问题，而用依赖顺序：

1. 先校准过时测试和真实源码定位：A4、C1。
2. 再移除真实替 agent 选重点/选工具的实现：A1、A2。
3. 再修 timeout 恢复策略边界：A3。
4. 再稳定 parent intent / local executor 协议措辞与工具面：A4、A5。
5. 再处理配置/策略一致性：B3、B4、B5、B6。
6. 最后做 prompt 资产化/文档校准：B1、B2、C2。

所有项都必须在计划中出现；后续执行可以分 PR，但不能从范围里消失。

## Acceptance Criteria

- 后续 `.omx/plans/prd-*` 必须逐项引用本 ledger 的 A/B/C 编号。
- 测试修复后，`NpcRuntimeContextFactoryTests.Create_AutonomyChannel_OmitsGlobalSkillsMandatoryIndex` 不再因旧 `Do not call tools` 断言失败。
- Prompt supplement builder 不再用 hardcoded `skillId -> needles` 决定 skill 重点。
- Builder 不再自动补具体 `skill_view(...)` 调用。
- Action slot timeout 不再直接把恢复策略固定为 cancel + pause + restart cooldown；agent 能收到 timeout fact/status。
- 父层 JSON intent contract 保留，但工具权限由 tool surface 保证。
- 固定 lane/tool/agent type 项都有明确“保留默认 + 配置/策略化”的计划或测试，不再靠上下文记忆。

## Recommended Handoff

下一步建议走 `$ralplan`，输入本 spec：

`$ralplan .omx/specs/deep-interview-autonomy-audit-resolution.md`

RALPLAN 输出应至少包含：

- PRD：逐项覆盖本 spec 的 A/B/C 编号。
- Test spec：每个修复点对应测试。
- 执行切片：先测试校准和 prompt/skill 边界，再 timeout，再配置策略。

