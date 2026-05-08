# Hermes-Desktop 宿主代替 Agent 决策审计报告

## 审计目标

本报告按项目既定设计原则审计：

- `agent` 应主动决策。
- `tool`、`skill`、prompt 补充、world facts 只应提供事实、能力边界、确认与执行结果。
- 宿主、桥接层、runtime 不应替 agent 预先决定行动、路线、意图、优先级、恢复策略或行为顺序。

本文只记录已经从源码复核过的高置信问题，不把“纯资源限制”“单纯 DI 装配”一概算作越界。

---

## 结论概览

当前项目里，最明显的越界主要集中在 5 类：

1. **宿主直接把行为策略写进 system prompt / supplement**。
2. **宿主把 skill 内容压缩成带倾向性的摘要，而不是把事实原样交给 agent。**
3. **tool description 不只是描述能力，还在指导 agent 该怎么想、何时用、失败后怎么办。**
4. **runtime 通过 channel / lane / tool surface 预先切死“谁能做什么”，而不是让 agent 在能力边界内自己选择。**
5. **部分后台流程会在 agent 没参与的情况下自动暂停、重试、冷却、恢复，已经越过“执行器”边界。**

---

## 一、Prompt / Skill 层越界

### 1. 默认系统提示直接把 NPC runtime 设计成“做小动作”的特定行为体
- **位置**: `src/Core/SystemPrompts.cs:13-24`
- **现状**:
  - `Default = StardewNpcRuntime`
  - `StardewNpcRuntime` 明确要求：
    - “Decide small next actions from observed game facts”
    - “Keep responses brief, action-oriented”
- **问题**:
  这不是单纯事实约束，而是在共享默认 prompt 里直接预设了 agent 的行为风格、动作粒度和响应风格。默认 prompt 已经不是“提供运行环境事实”，而是在替 agent 决定它应该怎样思考和输出。
- **为什么越界**:
  设计要求是宿主提供事实与工具，具体采取“小动作”“简短行动导向”应由 agent 自己决定，或至少由更上层 persona/skill 决定，而不是全局默认 system prompt 直接规定。

### 2. RuntimeFactsGuidance 用强命令式语言直接规定 agent 的查询策略
- **位置**: `src/Core/SystemPrompts.cs:26-31`
- **现状**:
  - “Never answer ... from memory. Use tools to check the live environment.”
  - “Do not use interactive `date` prompts on Windows.”
- **问题**:
  这里不只是陈述“工具更可靠”，而是在 prompt 里直接命令 agent 必须采用某条策略。
- **为什么越界**:
  如果这是平台安全/正确性边界，应该体现在 tool 能力和宿主校验里；现在是宿主通过 prompt 直接替 agent 做方法选择。

### 3. NPC runtime context factory 按 channel 预先决定 prompt 结构和能力曝光方式
- **位置**: `src/runtime/NpcRuntimeContextFactory.cs:17-25, 74-86, 130-137`
- **现状**:
  - interactive channel 使用 `DefaultInteractiveSystemPromptSupplement`
  - autonomy channel 使用 `DefaultAutonomySystemPromptSupplement`
  - autonomy channel 会关闭：
    - `IncludeSkillsMandatoryCatalog`
    - `IncludeMemoryGuidance`
    - `IncludeSessionSearchGuidance`
    - `IncludeSkillsGuidance`
    - `IncludeRuntimeFactsGuidance`
  - autonomy supplement 还写死：
    - “return one JSON intent contract only”
    - “Do not call tools or write tool arguments”
    - “mechanical actions are executed by the host and local executor”
- **问题**:
  宿主不是在暴露能力边界，而是在按 channel 直接规定 agent 的思考模式和输出契约。
- **为什么越界**:
  这等于宿主先决定：这个 agent 只能产出 intent，不能自己决定是否查工具、是否读技能、是否查记忆。这已经是“替 agent 选工作方式”。

### 4. Skill mandatory prompt 强制 agent 先扫 skill，并且“宁可多载入”
- **位置**: `src/skills/SkillManager.cs:164-227`
- **现状**:
  - “Before replying, scan the skills below”
  - “If a skill matches or is even partially relevant ... you MUST load it”
  - “Err on the side of loading”
  - “Load the skill even if you think you could handle the task”
- **问题**:
  宿主不是提供 skill catalog，而是在通过强制性提示替 agent 决定工作流。
- **为什么越界**:
  skill 应提供“能力和偏好事实”；是否加载、何时加载，本来应该是 agent 的主动判断。现在宿主把“先扫 skill、尽量多加载”提升成硬性行为。
   用户意见:
  现状没有问题,不需要修改

### 5. 私聊 system prompt 直接预编排对话顺序与记忆策略
- **位置**: `src/games/stardew/StardewPrivateChatOrchestrator.cs:251-261`
- **现状**:
  - “玩家找你说话时，你先像角色本人一样自然回应”
  - “如果玩家给了以后要兑现的约定...你自己判断要不要接；接了就用 todo 记到长期任务里”
  - “如果玩家告诉你稳定事实...用 memory 记住”
  - “如果你需要想起以前答应过什么，先用 session_search 查旧对话和旧约定”
  - “你可以先用工具处理任务和记忆，再给玩家一句简短自然的回复”
- **问题**:
  宿主直接写死了私聊里的处理顺序、何时记忆、何时 session_search、如何组织答复。
- **为什么越界**:
  这些应由 NPC persona + 当前上下文 + tool/skill 自己驱动，不该由宿主固定成一套对话编排规则。
    用户意见:
  现状没有问题,不需要修改

---

## 二、Skill 摘要层越界

### 6. 宿主不是把 skill 原样交给 agent，而是按 skillId 做选择性摘要
- **位置**: `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs:193-277`
- **现状**:
  - `BuildCompactSkillContract` 对 `stardew-core`、`stardew-social`、`stardew-navigation`、`stardew-task-continuity`、`stardew-world` 分别写死不同的 `needle` 集合
  - 例如会刻意挑出：
    - “本轮目标”
    - “玩家指令优先级最高”
    - “移动开始 / 移动到达 / 闲置”
    - “先回应玩家，再恢复原来的任务”
- **问题**:
  这不是“给 skill”，而是宿主在总结 skill，并决定哪些句子最重要。
- **为什么越界**:
  摘要过程本身就是一次价值判断。宿主已经替 agent 选了注意力重点，相当于把 skill 从“事实/规范资源”变成了“宿主剪辑后的行为导向”。
    用户意见:
  没看懂,用大白话重新详细描述

### 7. 宿主自动补 `skill_view(...)` 指令，直接替 agent 选择工具调用
- **位置**: `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs:261-272`
- **现状**:
  当检测到 `stardew-world` 或 `stardew-navigation` skill 引用了参考文件但摘要里没有 `skill_view(...)` 时，宿主会主动补：
  - ``skill_view(name="stardew-world", file_path="references/stardew-places.md")``
  - ``skill_view(name="stardew-navigation", file_path="references/index.md")``
- **问题**:
  宿主不只是在给参考路径，而是在明确教 agent“现在该怎么调用工具”。
- **为什么越界**:
  这属于直接替 agent 做工具使用决策，不再是单纯提供事实或能力面。
   用户意见:
  没看懂,用大白话重新详细描述

---

## 三、Tool description 越界

### 8. `stardew_player_status` description 直接限制调用策略
- **位置**: `src/games/stardew/StardewNpcTools.cs:232-235`
- **现状**:
  Description 末尾写：`Use at most one extra status tool in a normal autonomy turn.`
- **问题**:
  tool description 本应描述“它能做什么”，这里却在规定“你这一轮最多怎么用”。
- **为什么越界**:
  这是 agent 的轮次策略，不是工具契约。
   用户意见:
  原设定没有问题,不需要修改

### 9. `stardew_progress_status` description 用“仅当...才用”替 agent 决定时机
- **位置**: `src/games/stardew/StardewNpcTools.cs:244-249`
- **现状**:
  Description：`Use only when game-stage context matters.`
- **问题**:
  宿主直接定义调用时机，而不是仅说明返回哪些信息。
- **为什么越界**:
  是否“game-stage context matters”应由 agent 判断。
   用户意见:
  原设定没有问题,不需要修改

### 10. `stardew_social_status` description 替 agent 限定适用场景
- **位置**: `src/games/stardew/StardewNpcTools.cs:256-268`
- **现状**:
  Description：`Use only when relationship context matters.`
- **问题**:
  这不是能力说明，而是策略说明。
- **为什么越界**:
  宿主不该替 agent 决定“关系上下文是否重要”。
   用户意见:
  原设定没有问题,不需要修改

### 11. `stardew_quest_status` description 替 agent 决定使用时机
- **位置**: `src/games/stardew/StardewNpcTools.cs:275-280`
- **现状**:
  Description：`Use only when player tasks or quest context matters.`
- **问题**:
  同样把 agent 的判断变成了宿主的显式要求。
- **为什么越界**:
  这应来自 agent 的任务理解，不该写死在 tool 描述里。
   用户意见:
  原设定没有问题,不需要修改

### 12. `stardew_farm_status` description 替 agent 决定使用时机
- **位置**: `src/games/stardew/StardewNpcTools.cs:287-292`
- **现状**:
  Description：`Use only when farm context matters.`
- **问题**:
  宿主继续把“何时用”编码进 tool。
- **为什么越界**:
  依旧是策略层侵入能力层。
   用户意见:
  原设定没有问题,不需要修改

### 13. `stardew_recent_activity` description 直接规定用途边界
- **位置**: `src/games/stardew/StardewNpcTools.cs:308-310`
- **现状**:
  Description：`This is not a world-state query; use only to avoid repeating or to resume a prior task.`
- **问题**:
  宿主直接决定这类信息应该服务什么决策。
- **为什么越界**:
  tool 可以说明数据来源与局限，但不应替 agent 决定“只能用来做什么”。
   用户意见:
  原设定没有问题,不需要修改

### 14. `stardew_move` description 把失败后的恢复策略写死给 agent
- **位置**: `src/games/stardew/StardewNpcTools.cs:390-392`
- **现状**:
  Description 末尾写：
  `If a move ends with path_blocked, path_unreachable, invalid_destination_id, or interrupted, observe again or choose a different destinationId instead of retrying the same destination.`
- **问题**:
  这里不只是说错误码含义，而是直接规定失败恢复策略。
- **为什么越界**:
  重新观察、换目的地、重试同一目的地，本来都该由 agent 结合上下文判断。宿主不该替它选解法。
   用户意见:
  原设定没有问题,不需要修改

### 15. `stardew_navigate_to_tile` 被定义为“父模型不能见”的 executor-only 工具
- **位置**: `src/games/stardew/StardewNpcTools.cs:500-503`
- **现状**:
  Description：`Executor-only mechanical navigation ... Do not expose this tool to the parent autonomy lane.`
- **问题**:
  宿主先决定哪个 agent 层可以看见这个工具，而不是把工具能力与风险清楚表达后交给更上层策略。
- **为什么越界**:
  这已经不是描述工具，而是在宿主层切分“父决策 / 子执行”角色边界。
   用户意见:
  原设定没有问题,不需要修改

### 16. `stardew_speak` description 直接暗示 agent 应优先告知玩家
- **位置**: `src/games/stardew/StardewNpcTools.cs:609-612`
- **现状**:
  Description：`Use this to keep the player informed instead of silently doing many move/status turns.`
- **问题**:
  这不是纯能力描述，而是在建议 agent 采取哪种交互策略。
- **为什么越界**:
  何时沉默执行、何时先通知玩家，本应由 agent 自己判断。
  用户意见:
  原设定没有问题,不需要修改

### 17. `stardew_idle_micro_action` 明确要求“只能用 parent 已选好的 contract”
- **位置**: `src/games/stardew/StardewNpcTools.cs:671-674`
- **现状**:
  Description：`Choose only from the approved idle micro action contract already selected by the parent autonomy lane.`
- **问题**:
  这里直接承认：真正的动作选择已经被父层宿主/上层结构先做了，本工具只是机械执行。
- **为什么越界**:
  这和你的设计原则正面冲突：宿主/桥接层不该先替 agent 决定动作 contract。'
  用户意见:
  这个应该没啥问题,这是本地小模型执行agent,必须限制他的能力

---

## 四、Tool surface / lane 切分越界

### 18. 宿主硬编码 local executor tool surface
- **位置**: `src/games/stardew/StardewNpcTools.cs:10-19, 21-56, 58-86, 88-100`
- **现状**:
  - `LocalExecutorToolNames` 写死为：
    - `stardew_status`
    - `stardew_move`
    - `stardew_navigate_to_tile`
    - `stardew_idle_micro_action`
    - `stardew_task_status`
  - `CreateDefault` 和 `CreateLocalExecutorTools` 返回两套不同工具面
- **问题**:
  宿主已经先定义了“父层能用什么、执行器能用什么”。
- **为什么越界**:
  如果这是产品设计，也应该体现在 agent 自身可理解的能力边界中，而不是由宿主静态切面。现在是宿主在替 agent 分工。
  用户意见:
  付agent和 子agnet尤其是本地小模型agent分工确实不同,但是这么写死确实不对,应该怎么办?

### 19. App 组合根直接把 StardewAutonomy / StardewPrivateChat / Delegation 绑定到固定 lane
- **位置**: `Desktop/HermesDesktop/App.xaml.cs:416-483`
- **现状**:
  - `StardewAutonomyTickDebugService` 固定拿 `ChatRouteNames.StardewAutonomy` 和 `Delegation`
  - `StardewNpcPrivateChatAgentRunner` 固定拿 `ChatRouteNames.StardewPrivateChat` 和 `Delegation`
  - `StardewNpcAutonomyBackgroundService` 固定拿 `ChatRouteNames.StardewAutonomy` 和 `Delegation`
- **问题**:
  宿主在创建服务时就把不同任务永久绑定到固定 lane。
- **为什么越界**:
  这不是单纯提供“有这些 lane”，而是宿主已经决定：自主循环一定走哪条 lane，私聊一定走哪条 lane，委托一定走哪条 lane。
  用户意见:
  这一条没有问题,考虑到本地小模型和 主agent的功能划分,确实有必要指定.不过,我的意见是做成配置项而不是写死.

### 20. ChatLaneClientProvider 把 delegation policy 直接写死成 flat-only
- **位置**: `src/LLM/ChatLaneClientProvider.cs:25-30`
- **现状**:
  启动日志明确写：`Delegation lane policy is flat-only in v1; effectiveMaxSpawnDepth=1`
- **问题**:
  这说明宿主先决定 delegation 只能平铺一层，而不是由任务或 agent 自行决定委托深度。
- **为什么越界**:
  如果深度真的是安全/资源边界，应作为明确的执行限制；但现在它以“policy”形式被宿主拍板。
  用户意见:
  这一条确实有点问题,不过在游戏中确实不宜过深,另外,这个应该做成配置项

### 21. `agent` 工具把 agent type 与 system prompt / allowed tools 静态绑定
- **位置**: `src/Tools/AgentTool.cs:98-160`
- **现状**:
  - `researcher`、`coder`、`analyst`、`planner`、`reviewer`、`general` 都映射到固定 `SystemPrompt` 和 `AllowedTools`
  - 例如 `coder` 只有 `session_search`、`todo`、`memory`、`skills_list`、`skill_view`
- **问题**:
  宿主通过 agent type 先行决定子 agent 的身份和工具面。
- **为什么越界**:
  这会让“agent 工具”从能力提供层变成预制角色模板层，实际是宿主替调用方决定了子 agent 的工作方式。
  用户意见:
  这一条目前没啥问题,但是应该做成全局配置项.

### 22. `AgentCapabilityAssembler` 全局内建工具面也是宿主静态决定
- **位置**: `src/runtime/AgentCapabilityAssembler.cs:19-32, 34-62`
- **现状**:
  默认注册固定内建工具集：
  - `todo`
  - `todo_write`
  - `schedule_cron`
  - `agent`
  - `memory`
  - `session_search`
  - `skills_list`
  - `skill_view`
  - `skill_manage`
  - `skill_invoke`
  - `checkpoint`
- **问题**:
  工具面是宿主全局钦定的，不区分真实任务边界。
- **为什么越界**:
  这里至少反映出宿主在替 agent 决定“默认应该带哪些能力”。虽然这类越界比 prompt/策略更轻，但仍然属于宿主先选能力面。
  用户意见:
  这一条没有问题,不过这些应该作为全局配置项,而不是写死在代码中

---

## 五、Runtime 自动恢复 / 暂停 / 重试越界

### 23. autonomy loop 在 LLM 超时/异常后自动决定 restart/cooldown
- **位置**: `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:581-600, 620-636`
- **现状**:
  - 超时后自动 `tracker.RestartCount++`
  - 命中上限就 `PauseTrackerAsync(...MaxRestarts...)`
  - 否则自动用 `_budget.Options.EffectiveRestartCooldown` 计算 `nextWakeAtUtc`
- **问题**:
  宿主直接决定何时重启、暂停多久、重试几次。
- **为什么越界**:
  重试/冷却策略已经属于行为决策的一部分，不是单纯执行器职责。
  用户意见:
  没看懂,这一条问题究竟在哪里?影响了agent的哪些主动性?

### 24. 动作超时后由宿主自动取消命令并进入冷却
- **位置**: `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:817-841`
- **现状**:
  `ActionSlot` 超时后，宿主会：
  - 自动 `CancelAsync(...)`
  - 自动写入 cancel status
  - 自动 `PauseTrackerAsync(...)`
  - 自动设置 `RetryAfterUtc`
- **问题**:
  agent 并没有机会决定“继续等 / 换计划 / 再试一次 / 改成别的动作”。
- **为什么越界**:
  这已经不是纯执行反馈，而是宿主直接接管了失败恢复策略。
  用户意见: 这个确实不对,需要修改

### 25. 有私聊租约或 cooldown 时，宿主直接暂停 autonomy agent
- **位置**: `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:473-483`
- **现状**:
  - 如果存在私聊 lease，直接 `PauseTrackerAsync(...)`
  - 如果 `NextWakeAtUtc` 未到，也直接 `PauseTrackerAsync(...)`
- **问题**:
  宿主先决定当前 agent 不该继续行动。
- **为什么越界**:
  就算最终产品确实想在私聊时暂停自主行为，这也依然属于“调度策略先于 agent”。现状不是 agent 主动让渡控制，而是宿主硬切。
  用户意见: 星露谷中,开启私聊界面,游戏会暂停,因此停止agent循环是合理的

---

## 六、其他边界模糊但值得注意的点

### 26. `Agent` 核心把并发安全工具集合写死
- **位置**: `src/Core/Agent.cs:46-59`
- **现状**:
  `ParallelSafeTools` 被写死为：
  - `session_search`
  - `skill_invoke`
  - `skills_list`
  - `skill_view`
  - `memory`
- **问题**:
  虽然这更偏执行器实现，但本质上依旧是宿主预先决定工具并发策略。
- **为什么值得记**:
  它不是 NPC 设计原则里最核心的越界，但仍体现出“宿主先选策略而不是把能力声明交给工具自身”。

### 27. fallback visible speech 提取逻辑会在 agent 没明确调用 speak 时替它补一句可见台词
- **位置**: `src/runtime/NpcAutonomyLoop.cs:653, 1038-1079`
- **现状**:
  若响应文本能被 `TryExtractVisibleLine(...)` 解析出候选台词，runtime 可能把它当成玩家可见输出。
- **问题**:
  这会把“普通文本响应”提升为“可见说话行为”。
- **为什么越界**:
  说不说、��句对玩家可见，本应通过显式 `stardew_speak` 或明确 action contract 决定；fallback 提取本质上是宿主在猜测 agent 行为意图。

---

## 总体判断

高优先级的根因不是单个 bug，而是**当前代码里仍存在一条很强的“父层宿主/上层 runtime 先做策略，agent 再在剩余空间里活动”的实现惯性**。这与项目希望达成的原则有明显冲突：

- 你要的是：**宿主提供事实、工具、确认和执行结果**。
- 当前多处实现实际是：**宿主先决定 prompt、skill 重点、tool 使用时机、执行器权限、失败恢复与暂停策略，agent 只在这个框里行动。**

---

## 最值得优先处理的 8 个点

如果后续要整改，我建议优先处理这 8 项：

1. `src/runtime/NpcRuntimeContextFactory.cs:21-25,74-86,130-137`
2. `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs:193-277`
3. `src/games/stardew/StardewNpcTools.cs:232-235,308-310,390-392,500-503,609-612,671-674`
4. `src/games/stardew/StardewPrivateChatOrchestrator.cs:251-261`
5. `Desktop/HermesDesktop/App.xaml.cs:416-483`
6. `src/LLM/ChatLaneClientProvider.cs:25-30`
7. `src/Tools/AgentTool.cs:98-160`
8. `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:473-483,581-600,620-636,817-841`

这些地方最直接体现了“宿主还在替 agent 做决定”。

---

## 备注

本报告刻意没有把所有“预算限制”“性能限制”“工具注册”都算成违规，因为其中一部分确实可能属于执行器边界。本文只保留了那些已经明显跨过“提供事实/能力”边界，开始**指导、裁剪、预判、编排、替代决策**的实现。
