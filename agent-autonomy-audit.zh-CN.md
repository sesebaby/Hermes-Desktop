# Hermes-Desktop 宿主代替 Agent 决策审计报告(修订版)

## 审计目标

本报告按项目既定设计原则审计：

- `agent` 应主动决策。
- `tool`、`skill`、prompt 补充、world facts 只应提供事实、能力边界、确认与执行结果。
- 宿主、桥接层、runtime 不应替 agent 预先决定行动、路线、意图、优先级、恢复策略或行为顺序。

本文只记录已经从源码复核过的高置信问题,不把"纯资源限制""单纯 DI 装配"一概算作越界。

---

## 结论概览

经过用户反馈筛选后,当前项目里需要修改的越界主要集中在 5 类:

1. **宿主直接把行为策略写进 system prompt / supplement**
2. **宿主把 skill 内容压缩成带倾向性的摘要,而不是把事实原样交给 agent**
3. **runtime 通过 channel / lane / tool surface 预先切死"谁能做什么",而不是让 agent 在能力边界内自己选择**
4. **配置硬编码:lane 路由、工具面、delegation 深度、agent 类型定义都写死在代码中**
5. **部分后台流程会在 agent 没参与的情况下自动暂停、重试、冷却、恢复,已经越过"执行器"边界**

---

## 一、Prompt / Skill 层越界

### 1. 默认系统提示直接把 NPC runtime 设计成"做小动作"的特定行为体
- **位置**: `src/Core/SystemPrompts.cs:13-24`
- **现状**:
  - `Default = StardewNpcRuntime`
  - `StardewNpcRuntime` 明确要求:
    - "Decide small next actions from observed game facts"
    - "Keep responses brief, action-oriented"
- **问题**:
  这不是单纯事实约束,而是在共享默认 prompt 里直接预设了 agent 的行为风格、动作粒度和响应风格。默认 prompt 已经不是"提供运行环境事实",而是在替 agent 决定它应该怎样思考和输出。
- **为什么越界**:
  设计要求是宿主提供事实与工具,具体采取"小动作""简短行动导向"应由 agent 自己决定,或至少由更上层 persona/skill 决定,而不是全局默认 system prompt 直接规定。
- **修改建议**:
  考虑用一个全局通用的 `guidance.md` 文件专门来存放此类提示词,而不是硬编码在 SystemPrompts.cs 中。这样可以让用户根据不同场景灵活调整,而不是由代码强制规定。

### 2. RuntimeFactsGuidance 用强命令式语言直接规定 agent 的查询策略
- **位置**: `src/Core/SystemPrompts.cs:26-31`
- **现状**:
  - "Never answer ... from memory. Use tools to check the live environment."
  - "Do not use interactive `date` prompts on Windows."
- **问题**:
  这里不只是陈述"工具更可靠",而是在 prompt 里直接命令 agent 必须采用某条策略。
- **为什么越界**:
  如果这是平台安全/正确性边界,应该体现在 tool 能力和宿主校验里;现在是宿主通过 prompt 直接替 agent 做方法选择。
- **修改建议**:
  同样考虑用一个全局通用的 `guidance.md` 文件专门来存放此类提示词。

### 3. NPC runtime context factory 按 channel 预先决定 prompt 结构和能力曝光方式
- **位置**: `src/runtime/NpcRuntimeContextFactory.cs:17-25, 74-86, 130-137`
- **现状**:
  - interactive channel 使用 `DefaultInteractiveSystemPromptSupplement`
  - autonomy channel 使用 `DefaultAutonomySystemPromptSupplement`
  - autonomy channel 会关闭:
    - `IncludeSkillsMandatoryCatalog`
    - `IncludeMemoryGuidance`
    - `IncludeSessionSearchGuidance`
    - `IncludeSkillsGuidance`
    - `IncludeRuntimeFactsGuidance`
  - autonomy supplement 还写死:
    - "return one JSON intent contract only"
    - "Do not call tools or write tool arguments"
    - "mechanical actions are executed by the host and local executor"

**用大白话解释这个问题**:

想象你有两个助手:
- **主 agent(父 agent,云端大模型)**: 负责思考"我要做什么",比如"我要去市场买菜"
- **子 agent(本地小模型 executor)**: 负责执行具体动作,比如"向左走 3 步,向右走 2 步"

**设计意图(合理的)**:
- 主 agent 只做高层决策,输出意图,减少调用次数 → **省钱**
- 子 agent 执行具体动作,可以反复调用 → **不花钱**

这个**分工本身是合理的工程权衡**,不是问题。

**这里要区分两件事**:

#### 1. `必须输出 JSON` 需要保留
这个要求**不是问题**,应该保留。

原因是它本质上不是在替 agent 决定"做什么",而是在定义**父 agent 和执行器之间的接口契约**:
- 父 agent 负责产出结构化 intent
- 宿主 / 本地 executor 负责解析这个 intent 并执行

这和 API 必须返回 JSON、函数必须返回某个结构体是一个性质。它属于**协议约束 / 输出契约**,不是宿主代替 agent 做行为决策。

#### 2. 真正可疑的是其余两类限制
更需要警惕的是:
- `Do not call tools or write tool arguments`
- 因为是 autonomy channel,就整体关闭 `skills` / `memory` / `session_search` / `runtime facts` guidance

这些更像是在替 agent 预先决定:
- 你该不该查工具
- 你该不该参考 skill
- 你该不该使用记忆或会话搜索

**为什么这里仍值得保留为问题?**

因为成本控制目标本身合理,但更稳妥的做法应该是:
- 用**工具面 / lane 能力边界**来限制 agent 能做什么
- 把 JSON 作为**明确协议**保留
- 不要把过多"思考方式""查询方式"混在 prompt 里一起硬编码

**修改建议**:

保留 JSON 输出契约,但把问题收缩为"其余限制的实现方式":

#### 方案 1: 保留 JSON 协议,把能力限制落实到工具面(推荐)
```csharp
// 父 agent: 只暴露允许的工具,并要求输出 JSON intent
RegisterTools(parentAgent, [
    sessionSearch, memory, todo
]);

// 子 agent: 只暴露执行工具
RegisterTools(executorAgent, [
    stardew_move, stardew_navigate_to_tile, ...
]);
```

这样:
- `return one JSON intent contract only` 可以继续保留
- 但"能不能调工具"主要由工具注册决定,而不是只靠 prompt 命令

#### 方案 2: 把限制改写成能力边界陈述
```markdown
# 父 agent prompt
你是高层决策 agent。你的输出契约是一个 JSON intent。
具体游戏操作由本地执行器负责。
你当前可用的工具: session_search, memory, todo

# 子 agent prompt
你是本地执行器。你的职责是执行父 agent 的 intent。
你当前可用的工具: stardew_move, stardew_navigate_to_tile, ...
```

#### 关键区别

| 可以保留 | 更值得调整 |
|------|---------|
| "你必须输出 JSON" - 这是协议契约 | "你不能调工具" - 更适合由工具面限制 |
| "执行器会解析你的 JSON" - 这是接口事实 | "你不需要看 skill / memory" - 更像方法选择 |

**修订后的判断**:
- `必须输出 JSON` **保留,不算问题**
- `Do not call tools or write tool arguments` 与按 channel 批量关闭 guidance **仍然值得保留审计**,但定性应从"分工本身错误"改为"实现方式仍偏硬编码"

### 4. 宿主不是把 skill 原样交给 agent,而是按 skillId 做选择性摘要
- **位置**: `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs:193-277`
- **现状**:
  - `BuildCompactSkillContract` 对 `stardew-core`、`stardew-social`、`stardew-navigation`、`stardew-task-continuity`、`stardew-world` 分别写死不同的 `needle` 集合
  - 例如会刻意挑出:
    - "本轮目标"
    - "玩家指令优先级最高"
    - "移动开始 / 移动到达 / 闲置"
    - "先回应玩家,再恢复原来的任务"

**用大白话解释这个问题**:

想象你有一本操作手册(skill 文件),里面有 100 条规则。

**现在的做法是**:
宿主代码会先读这本手册,然后挑出它认为"最重要"的 5 条规则,只把这 5 条给 agent 看。

比如对于 `stardew-social` 这个 skill:
- 原始文件可能有 50 行内容
- 宿主代码会搜索包含"玩家指令优先级最高"、"stardew_speak"、"移动开始"等关键词的行
- 只把这几行摘要给 agent

**为什么这是越界?**

因为"哪些规则最重要"本身就是一次价值判断:
- 宿主认为"玩家指令优先级最高"很重要,所以挑出来
- 但 agent 可能在某个场景下,更需要看到其他规则

宿主已经替 agent 做了"注意力分配"的决定。

**修改建议**:

1. 要么把完整的 skill 文件内容交给 agent,让它自己决定关注哪部分
2. 要么在 skill 文件本身就按重要性分层(比如用 markdown 标题层级),让 agent 自己选择读取深度
3. 如果确实需要压缩(比如 token 限制),应该用 LLM 做语义压缩,而不是用关键词匹配做机械摘要

### 5. 宿主自动补 `skill_view(...)` 指令,直接替 agent 选择工具调用
- **位置**: `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs:261-272`
- **现状**:
  当检测到 `stardew-world` 或 `stardew-navigation` skill 引用了参考文件但摘要里没有 `skill_view(...)` 时,宿主会主动补:
  - ``skill_view(name="stardew-world", file_path="references/stardew-places.md")``
  - ``skill_view(name="stardew-navigation", file_path="references/index.md")``

**用大白话解释这个问题**:

想象你在给 agent 一份工作说明,说明里提到"如果需要地图,可以查看 references/stardew-places.md"。

**现在的做法是**:
宿主代码不只是告诉 agent"有这个文件可以查",而是直接在说明里加上一句:
"现在就执行 `skill_view(name="stardew-world", file_path="references/stardew-places.md")`"

**为什么这是越界?**

因为宿主已经替 agent 决定了:
- "你现在需要查这个文件"
- "你应该用 skill_view 这个工具"
- "你应该传这些参数"

这不是"提供能力",而是"直接下指令"。

**修改建议**:

只在 skill 摘要里说明"参考文件位于 references/stardew-places.md,可以用 skill_view 查看",让 agent 自己决定是否需要查、什么时候查。

---

## 二、Tool surface / lane 切分越界

### 6. 宿主硬编码 local executor tool surface
- **位置**: `src/games/stardew/StardewNpcTools.cs:10-19, 21-56, 58-86, 88-100`
- **现状**:
  - `LocalExecutorToolNames` 写死为:
    - `stardew_status`
    - `stardew_move`
    - `stardew_navigate_to_tile`
    - `stardew_idle_micro_action`
    - `stardew_task_status`
  - `CreateDefault` 和 `CreateLocalExecutorTools` 返回两套不同工具面
- **问题**:
  宿主已经先定义了"父层能用什么、执行器能用什么"。
- **为什么越界**:
  如果这是产品设计,也应该体现在 agent 自身可理解的能力边界中,而不是由宿主静态切面。现在是宿主在替 agent 分工。
- **修改建议**:
  父 agent 和子 agent(本地小模型 executor)分工确实不同,但这么写死确实不对。建议:
  1. 把工具面配置提取到配置文件(比如 `tool-surface.json`)
  2. 在配置文件里明确定义不同 agent 类型可以使用的工具集
  3. 让宿主从配置文件读取,而不是硬编码在代码里

---

## 三、配置硬编码越界

### 7. App 组合根绑定固定 lane
- **位置**: `Desktop/HermesDesktop/App.xaml.cs:416-483`
- **现状**:
  - `StardewAutonomyTickDebugService` 固定拿 `ChatRouteNames.StardewAutonomy` 和 `Delegation`
  - `StardewNpcPrivateChatAgentRunner` 固定拿 `ChatRouteNames.StardewPrivateChat` 和 `Delegation`
  - `StardewNpcAutonomyBackgroundService` 固定拿 `ChatRouteNames.StardewAutonomy` 和 `Delegation`
- **问题**:
  宿主在创建服务时就把不同任务永久绑定到固定 lane。
- **为什么越界**:
  这不是单纯提供"有这些 lane",而是宿主已经决定:自主循环一定走哪条 lane,私聊一定走哪条 lane,委托一定走哪条 lane。
- **修改建议**:
  做成配置项而不是写死。可以在配置文件中定义 lane 路由规则,让用户可以根据不同场景调整。

### 8. AgentCapabilityAssembler 全局内建工具面也是宿主静态决定
- **位置**: `src/runtime/AgentCapabilityAssembler.cs:19-32, 34-62`
- **现状**:
  默认注册固定内建工具集:
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
  工具面是宿主全局钦定的,不区分真实任务边界。
- **为什么越界**:
  这里至少反映出宿主在替 agent 决定"默认应该带哪些能力"。虽然这类越界比 prompt/策略更轻,但仍然属于宿主先选能力面。
- **修改建议**:
  这些应该作为全局配置项,而不是写死在代码中。可以在配置文件中定义默认工具集,让用户可以根据不同场景启用/禁用特定工具。

### 9. ChatLaneClientProvider 把 delegation policy 直接写死成 flat-only
- **位置**: `src/LLM/ChatLaneClientProvider.cs:25-30`
- **现状**:
  启动日志明确写:`Delegation lane policy is flat-only in v1; effectiveMaxSpawnDepth=1`
- **问题**:
  这说明宿主先决定 delegation 只能平铺一层,而不是由任务或 agent 自行决定委托深度。
- **为什么越界**:
  如果深度真的是安全/资源边界,应作为明确的执行限制;但现在它以"policy"形式被宿主拍板。
- **修改建议**:
  在游戏中确实不宜过深,但这个应该做成配置项,让用户可以根据实际需求调整委托深度限制。

### 10. AgentTool 把 agent type 与 system prompt / allowed tools 静态绑定
- **位置**: `src/Tools/AgentTool.cs:98-160`
- **现状**:
  - `researcher`、`coder`、`analyst`、`planner`、`reviewer`、`general` 都映射到固定 `SystemPrompt` 和 `AllowedTools`
  - 例如 `coder` 只有 `session_search`、`todo`、`memory`、`skills_list`、`skill_view`
- **问题**:
  宿主通过 agent type 先行决定子 agent 的身份和工具面。
- **为什么越界**:
  这会让"agent 工具"从能力提供层变成预制角色模板层,实际是宿主替调用方决定了子 agent 的工作方式。
- **修改建议**:
  目前没啥问题,但应该做成全局配置项。可以在配置文件中定义不同 agent 类型的默认 prompt 和工具集。

---

## 四、Runtime 自动恢复 / 暂停 / 重试越界

### 11. 动作超时后由宿主自动取消命令并进入冷却
- **位置**: `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:798-822`
- **现状**:
  `ActionSlot` 超时后,宿主会:
  - 自动 `CancelAsync(...)`
  - 自动写入 cancel status
  - 自动 `PauseTrackerAsync(...)`
  - 自动设置 `RetryAfterUtc`
- **问题**:
  agent 并没有机会决定"继续等 / 换计划 / 再试一次 / 改成别的动作"。
- **为什么越界**:
  这已经不是纯执行反馈,而是宿主直接接管了失败恢复策略。

**用大白话解释这个问题**:

想象 agent 发出一个命令"去市场",然后等待执行结果。

**现在的做法是**:
如果 30 秒后还没收到结果,宿主代码会:
1. 自动取消这个命令
2. 自动让 agent 暂停 5 分钟
3. 自动设置"5 分钟后再试"

**为什么这是越界?**

因为 agent 可能有其他选择:
- 也许它想继续等(可能只是网络慢)
- 也许它想换个目的地(市场可能关门了)
- 也许它想先做别的事(去市场不是最紧急的)

但宿主直接替它做了"取消+暂停+重试"的决定。

**修改建议**:

1. 超时后,应该把"超时"这个事实告诉 agent
2. 让 agent 自己决定下一步:继续等、取消、换计划、还是做别的
3. 宿主只负责执行 agent 的决定,不应该自己决定恢复策略

---

## 总体判断

高优先级的根因不是单个 bug,而是**当前代码里仍存在一条很强的"父层宿主/上层 runtime 先做策略,agent 再在剩余空间里活动"的实现惯性**。这与项目希望达成的原则有明显冲突:

- 你要的是:**宿主提供事实、工具、确认和执行结果**。
- 当前多处实现实际是:**宿主先决定 prompt、skill 重点、tool 使用时机、执行器权限、失败恢复与暂停策略,agent 只在这个框里行动。**

---

## 最值得优先处理的项目

### 高优先级(纯越界,必须修改):

1. **`src/Core/SystemPrompts.cs:13-31`** - 把行为指导提取到 guidance.md
2. **`src/runtime/NpcRuntimeContextFactory.cs:17-25,74-86,130-137`** - 不要按 channel 硬切能力
3. **`src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs:193-277`** - 不要替 agent 摘要 skill
4. **`src/games/stardew/StardewNpcTools.cs:10-19`** - 把工具面配置提取到配置文件
5. **`src/games/stardew/StardewNpcAutonomyBackgroundService.cs:798-822`** - 超时后让 agent 决定恢复策略

### 中优先级(配置硬编码,应该配置化):

6. **`Desktop/HermesDesktop/App.xaml.cs:416-483`** - 把 lane 路由做成配置项
7. **`src/runtime/AgentCapabilityAssembler.cs:19-32`** - 把默认工具集做成配置项
8. **`src/LLM/ChatLaneClientProvider.cs:25-30`** - 把 delegation 深度做成配置项
9. **`src/Tools/AgentTool.cs:98-160`** - 把 agent 类型定义做成全局配置项

---

## 备注

本报告刻意没有把所有"预算限制""性能限制""工具注册"都算成违规,因为其中一部分确实可能属于执行器边界。本文只保留了那些已经明显跨过"提供事实/能力"边界,开始**指导、裁剪、预判、编排、替代决策**的实现。

经过用户反馈筛选,已删除以下被确认为"没有问题"或"暂且搁置"的条目:
- Skill mandatory prompt 强制 agent 先扫 skill(用户认为现状没有问题)
- 私聊 system prompt 直接预编排对话顺序(用户认为现状没有问题)
- 所有 tool description 的"使用时机限制"(用户认为原设定没有问题)
- autonomy loop 在 LLM 超时/异常后自动 restart/cooldown(用户认为星露谷场景下合理)
- 有私聊租约时暂停 autonomy agent(用户认为星露谷场景下合理)
- ParallelSafeTools 硬编码(用户暂时搁置)
- fallback visible speech 提取逻辑(用户暂且搁置)

以下条目已恢复保留,但定性为"应该配置化"而非"逻辑错误":
- App 组合根绑定固定 lane
- AgentCapabilityAssembler 全局内建工具面
- ChatLaneClientProvider delegation 深度限制
- AgentTool agent type 静态绑定
