# 参考项目与本项目性能/上下文/Loop 差异对比分析（带证据）

> 目的：解释为什么 `external/hermes-agent-main` 参考项目体感更快，而当前 Hermes Desktop 在星露谷 NPC 常驻自治场景中出现明显变慢；同时回答“一个游戏上下文为什么会膨胀到 3~6 万字符”和“loop 为什么这么慢”。

## 一、执行摘要

本次对比后的核心结论如下：

1. **参考项目快，并不是因为它的主循环天然更轻或最大迭代更小**。相反，参考项目默认 `max_iterations=90`，比本项目普通 Agent 的 `MaxToolIterations=25` 还大；但它的主要使用模式是**交互式 turn / cron 任务**，不是“每个 NPC 每 2 秒跑一次常驻自治回合”。
2. **当前项目慢，主瓶颈不在 SMAPI/Bridge，也不主要在 tool 执行，而在 Hermes agent 回合内部**：
   - 首轮上下文过大；
   - 单个 autonomy tick 经常跑 3~6 轮 LLM/tool 循环；
   - 每轮 LLM 请求常见耗时 2~12 秒，叠加后整轮达到 20~43 秒。
3. **“一个游戏上下文怎么会这么大”** 的真正答案是：送进模型的不是“当前这次观察事实”，而是**该 NPC 的整条持久 session 历史 + tool 结果 + memory/soul/plugin/task 注入后的 preparedContext**。
4. **loop 慢的本质** 是：我们把一个偏通用的 agent turn，当成了高频 NPC 大脑来跑；每个 loop 并不是轻量规则判断，而是一次完整 mini-agent deliberation。

---

## 二、真实运行日志先给结论：慢不在 Bridge，而在 Agent 内部

### 2.1 Bridge / SMAPI 查询很快

`SMAPI-latest.txt` 中可见：

- `query_status_completed npc=Penny ... durationMs=5`  
- `query_status_completed npc=Haley ... durationMs=11`  
- 后续大量 `durationMs=0`  
- `phone_message_enqueued`、`phone_thread_opened` 基本都是即时记录

证据：
- `C:\Users\Administrator\AppData\Roaming\StardewValley\ErrorLogs\SMAPI-latest.txt`
- 例如：`17:38:09`、`17:38:28`、`17:38:56`、`17:40:02`、`17:41:05`

这说明：

- 不是 `BridgeHttpHost` 的状态查询慢；
- 不是手机线程打开慢；
- 不是 SMAPI 到 Hermes 的基础桥接慢。

### 2.2 tool 执行本身也很快

`hermes.log` 中同一批 trace 可见：

- `stardew_speak`：约 `70~84ms`
- `stardew_open_private_chat`：约 `16~77ms`
- `stardew_status / stardew_recent_activity / stardew_progress_status / todo / stardew_task_status`：多为 `15~24ms`

这说明：

- tool RPC 本身不是主瓶颈；
- 真正的耗时不在“执行工具”，而在“调用 LLM 决定要执行什么工具”。

### 2.3 真正慢的是 Agent 的 LLM 循环

`hermes.log` 中的关键样本：

- Penny：
  - `09:39:13.835` iteration=1 `durationMs=10281`
  - `09:39:18.225` iteration=2 `durationMs=4298`
  - `09:39:18.237` 整轮 `durationMs=21356`
- Haley：
  - 某轮总计 `durationMs=42975`
- Penny：
  - 另一轮总计 `durationMs=40669`

证据文件：
- `C:\Users\Administrator\AppData\Local\hermes\hermes-cs\logs\hermes.log`

这已经足以说明：

> 当前“回复慢”的主因是 **Hermes agent 内部一整轮决策太重**，不是桥接/UI。

---

## 三、为什么参考项目体感更快：它不是在跑你现在这种负载

## 3.1 参考项目主循环是什么

参考项目主循环在：

- `external/hermes-agent-main/run_agent.py:9588`

```python
while (api_call_count < self.max_iterations and self.iteration_budget.remaining > 0) or self._budget_grace_call:
```

默认迭代上限：

- `external/hermes-agent-main/run_agent.py:844`
- `external/hermes-agent-main/cli.py:360`

```python
max_iterations: int = 90
```

```python
"max_turns": 90
```

### 结论

参考项目虽然也支持长工具循环，但它的核心模式是：

- 用户交互 turn；
- 或 cron/background 任务；
- 不是“每个 NPC 每 2 秒都来一整轮 ChatAsync”。

---

## 3.2 参考项目有强上下文压缩和请求前预处理

### （1）system prompt cache

证据：
- `external/hermes-agent-main/run_agent.py:9382-9432`

关键逻辑：
- system prompt per-session cache；
- continuation session 直接复用前一轮保存的 system prompt；
- 目标是保持 prefix cache 稳定，不重复重建。

### （2）preflight context compression

证据：
- `external/hermes-agent-main/run_agent.py:9434-9449`

关键逻辑：
- 进入主 loop 前先做 token 预估；
- 如果历史过大，先压缩，再发请求。

### （3）上下文压缩器默认开启

证据：
- `external/hermes-agent-main/cli.py:355-358`

```python
"compression": {
    "enabled": True,
    "threshold": 0.50,
}
```

- `external/hermes-agent-main/agent/context_compressor.py:329-375`

```python
threshold_percent: float = 0.50,
protect_first_n: int = 3,
protect_last_n: int = 20,
summary_target_ratio: float = 0.20,
```

这意味着：
- 默认在上下文窗口 50% 时开始压缩；
- 只保留前若干条 + 后若干条；
- 中间大段会摘要化。

### （4）会裁旧 tool result 与大参数

证据：
- `external/hermes-agent-main/agent/context_compressor.py:433-447`

注释明确写了：

```python
Replace old tool result contents with informative 1-line summaries.
...
truncate large tool_call arguments in assistant messages
```

这点非常重要：

> 参考项目不是简单“保留所有 tool 历史”，而是主动把旧 tool 结果压成一行摘要，并截断大 tool_call 参数。

### （5）压缩后不是继续堆同一个 session，而是切 continuation session

证据：
- `external/hermes-agent-main/run_agent.py:8083-8199`

关键逻辑：
- `_compress_context(...)` 完成压缩后会重建 system prompt；
- 结束旧 session：`self._session_db.end_session(self.session_id, "compression")`；
- 创建新 session，并把旧 session 作为 `parent_session_id`；
- 后续请求在新 session 上继续，而不是让旧 session 无限制膨胀。

这意味着：

- 旧历史不会永远挂在同一个 session 上持续污染首轮；
- 压缩不仅是“内容变短”，还是一次明确的 session 边界重建。

### （6）provider 适配层还会再做一轮消息结构瘦身

证据：
- `external/hermes-agent-main/agent/anthropic_adapter.py:1301-1417`

关键逻辑：
- 合并连续 `tool_result` 为一个 user message；
- 清理 orphaned `tool_use` / `tool_result`；
- 强制 role alternation；
- 合并连续 user / assistant messages。

这意味着：

- 就算上游压缩后消息结构变碎，真正发给 provider 前还会再收拾一遍；
- 最终请求体会比原始 transcript 更紧凑。

### 小结：参考项目处理“首轮过肥”的完整方法

参考项目并不是靠单一“摘要一下”解决长历史问题，而是靠一整套链路：

1. **system prompt cache**：稳定前缀，避免每轮重建系统前缀。
2. **preflight compression**：请求发出前先估 token，过阈值先压缩。
3. **old tool result pruning**：旧 tool 输出改成一行摘要、去重、截断大参数。
4. **continuation session**：压缩后切新 session，不让单个 session 无限肥大。
5. **provider-side normalization**：真正发送前再做一次消息结构合并与修复。

一句话总结就是：

> 参考项目不是让“长历史原样进入首轮”，而是先把旧历史降级成“足够继续任务的压缩记忆”，然后才发请求。

---

## 四、为什么我们会慢：架构上是在把通用 Agent 当高频 NPC 大脑

## 4.1 我们的 NPC loop 不是轻量 loop，而是完整 agent turn

`NpcAutonomyLoop.RunOneTickAsync(...)` 中：

证据：
- `src/runtime/NpcAutonomyLoop.cs:123-146`

```csharp
decisionSession = new Session
{
    Id = descriptor.SessionId,
    Platform = descriptor.AdapterId
};
...
decisionResponse = await _agent.ChatAsync(
    decisionMessage,
    decisionSession,
    ct);
```

也就是说，每个 tick 都会：

1. 观察世界；
2. 收集事件；
3. 组装当前事实；
4. 新建/复用 NPC session；
5. 进入完整 `ChatAsync(...)`。

这不是规则判断，而是一次完整 agent deliberation。

---

## 4.2 后台服务默认每 2 秒调度一次

证据：
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:138`

```csharp
_pollInterval = options.PollInterval == default ? TimeSpan.FromSeconds(2) : options.PollInterval;
```

- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs:337-355`

```csharp
while (!ct.IsCancellationRequested)
{
    await DispatchOneIterationAsync(ct);
    await Task.Delay(_pollInterval, ct);
}
```

### 结论

这意味着系统期望它像一个高频后台 loop 一样工作。  
但 loop 里面塞的是完整 Agent 回合，所以天然重。

---

## 4.3 NPC autonomy 虽然限制为 6 轮，但日志显示经常真的跑到 5~6 轮

证据：
- `src/runtime/NpcAutonomyBudget.cs:35-37`

```csharp
public sealed record NpcAutonomyBudgetOptions(
    int MaxToolIterations = 6,
    int MaxConcurrentLlmRequests = 1,
```

而真实日志里：

- Haley 某轮 trace 连续经过：
  - `stardew_speak`
  - `stardew_recent_activity`
  - `todo`
  - `stardew_move`
  - `stardew_task_status`
  - `stardew_open_private_chat`
- 整轮耗时：`42975ms`

### 结论

单个 tick 常常不是“一问一答”，而是：

- 模型先决定 A
- 跑 tool
- 再问模型
- 再决定 B
- 再跑 tool
- 再问模型

……最后叠成 20~40 秒。

---

## 五、一个游戏上下文为什么会膨胀到 3~6 万字符

## 5.1 当前观察事实本身并不大

`NpcAutonomyLoop` 里准备的当前 decision message：

证据：
- `src/runtime/NpcAutonomyLoop.cs:132-141`

```csharp
var decisionMessage = BuildDecisionMessage(descriptor, currentFacts);
_logger?.LogInformation(
    "NPC autonomy decision request prepared; npc={NpcId}; trace={TraceId}; facts={FactCount}; messageChars={MessageChars}; ...",
```

真实日志里常见：
- `messageChars=895`
- `messageChars=1018`
- `messageChars=1581`
- `messageChars=1923`

所以“当前这次现场”并不夸张。

---

## 5.2 首轮真正发给模型的是 preparedContext，不是当前 decisionMessage 裸奔

证据：
- `src/Core/Agent.cs:404-405`

```csharp
var messagesToUse = (iterations == 1 && preparedContext is not null)
    ? preparedContext
```

而 `preparedContext` 来自：
- `src/Core/AgentLoopScaffold.cs:78-123`
- 最终调用 `ContextManager.PrepareContextAsync(...)`

---

## 5.3 `ContextManager` 会先加载整个 session transcript

证据：
- `src/Context/ContextManager.cs:91-97`

```csharp
if (_transcripts.SessionExists(sessionId))
    allMessages = await _transcripts.LoadSessionAsync(sessionId, ct);
else
    allMessages = new List<Message>();
```

然后再：
- 切 recent window：`src/Context/ContextManager.cs:98-100`
- 必要时摘要 evicted：`src/Context/ContextManager.cs:136-152`
- 压力太大时 compact state：`src/Context/ContextManager.cs:154-164`
- 注入 soul/plugin/task：`src/Context/ContextManager.cs:169-205`

### 这意味着什么

送进模型的内容不是单纯：
- 当前 NPC 在哪；
- 当前时间几点；
- 玩家是不是在附近。

而是：
- 该 NPC 的持久 session 历史；
- 里面保存过的 user / assistant / tool / reasoning；
- 还叠加了 memory、soul、plugin system context、active tasks。

---

## 5.4 transcript 会持续保存，导致 session 越跑越肥

### Message 与 SessionSearchIndex 保存逻辑

证据：
- `src/Core/Models.cs:5-17`：`Message` 有 `Role/Content/ToolCalls/Reasoning...`
- `src/transcript/TranscriptStore.cs:47-56`
- `src/search/SessionSearchIndex.cs:656-698`

`InsertMessage(...)` 直接把消息写入 `messages` 表，并累加 `message_count`。

### 真实日志印证上下文膨胀

首轮 `Agent LLM request started` 常见：

- Penny：`messages=33~37, chars=35k~40k`
- Haley：`messages=49~62, chars=54k~59k`

证据示例：
- `09:39:24.184` Penny `messages=35, chars=38086`
- `09:39:26.509` Haley `messages=59, chars=56613`
- `09:40:10.371` Haley `messages=59, chars=57499`
- `09:40:29.660` Haley `messages=60, chars=57207`

### 5.5 为什么我们明明会 summarize evicted messages，首轮仍然会过肥

这是当前设计里最容易误判的一点：

> `ContextManager` 的 summarize 主要作用于 **evictedMessages 和 SessionState**，但首轮真正发给模型的 **recentTurns 仍然是原文消息窗口**。

#### （1）recent window 是按“最近 turn 数”截，不是按最终请求大小二次压缩

证据：
- `src/Context/TokenBudget.cs:19-25`

```csharp
public TokenBudget(int maxTokens = 8000, int recentTurnWindow = 6)
```

- `src/Context/TokenBudget.cs:102-119`

```csharp
public List<Message> TrimToRecentWindow(List<Message> messages)
...
public List<Message> GetEvictedMessages(List<Message> messages)
```

这意味着：
- 系统默认保留最近 **6 个 turn**；
- 被赶出窗口的消息才进入 `evictedMessages` 候选摘要；
- 只要消息还落在 recent window 内，就会继续以原文存在。

#### （2）summarize 只作用于 evictedMessages，不会把 recentTurns 自身压短

证据：
- `src/Context/ContextManager.cs:98-100`

```csharp
var recentTurns = _budget.TrimToRecentWindow(allMessages);
var evictedMessages = _budget.GetEvictedMessages(allMessages);
```

- `src/Context/ContextManager.cs:136-152`

```csharp
var shouldSummarizeEvicted =
    evictedMessages.Count > 0 &&
    (pressure >= BudgetPressure.High ||
     string.IsNullOrEmpty(state.Summary.Content) ||
     IsSummaryStaleBeyond(state, turnsStalenessThreshold: 10));
...
await SummarizeEvictedAsync(state, evictedMessages, ct);
```

这里压缩的是：
- 被逐出的旧消息；
- 结果写回 `state.Summary`。

但 `recentTurns` 变量本身并没有在这之后再做：
- tool result pruning
- tool args truncation
- provider 前二次瘦身
- 按最终 token 再裁一刀

#### （3）PromptBuilder 仍然会把 recentTurns 原样塞回请求

证据：
- `src/Context/PromptBuilder.cs:128-139`

```csharp
if (packet.RecentTurns is { Count: > 0 })
{
    messages.AddRange(packet.RecentTurns);
}

messages.Add(new Message
{
    Role = "user",
    Content = packet.CurrentUserMessage
});
```

也就是说：
- state summary 是一层；
- recent turns 是另一层；
- recent turns 不会因为前面做了 evicted summary 就自动变短。

#### （4）而且 recentTurns 之外还会继续叠加多层 system context

证据：
- `src/Context/PromptBuilder.cs:80-125`

`ToOpenAiMessages(...)` 会按层加入：
1. `SoulContext`
2. `SystemPrompt`
3. `PluginSystemContext`
4. `SessionStateJson`
5. `ActiveTaskContext`
6. `RecentTurns`
7. `CurrentUserMessage`

这意味着，即使 recent window 只有 6 个 turn，最终请求体仍然是：

- 多层 system 内容
- recentTurns 原文
- 当前用户消息

#### （5）所以当前 summarize 机制为什么不够

当前项目的 summarize 更像：

- “把窗口外的旧历史做成摘要，塞进 `SessionState`”

而不是参考项目那种：

- “请求发出前，对窗口内的旧 tool 结果和大参数也继续做瘦身；必要时切 continuation session；provider 前再规整一次消息结构。”

因此会出现这种情况：

- `evictedMessages` 的确被总结了；
- 但 **recentTurns 本身依旧可能很肥**；
- 再叠加 soul/plugin/task/system state 后，首轮仍能达到 `35k~59k chars`。

### 结论

你觉得“一个游戏上下文不该这么大”，直觉没错；  
但系统现在送进去的并不是“一个游戏状态”，而是“一个 NPC 的长会话”，而且这个长会话的 **recent window 仍保留原文层**。

这也是为什么：

> 我们虽然有 summarize evicted messages，但还没有参考项目那种“对首轮最终请求体继续做瘦身”的最后一公里。

---

## 六、为什么 loop 会特别慢

## 6.1 loop 前还有一段前处理空窗

`ChatAsync(...)` 在首个 LLM request started 之前，会做：

证据：
- `src/Core/Agent.cs:306-365`

包括：
- plugin system prompt blocks
- memory 注入
- `AppendUserMessageAsync(...)`
- soul fallback
- `PrepareOptimizedContextAsync(...)`
- `RefreshTransientPluginSystemMessageAsync(...)`

这意味着每个 tick 在真正打到 LLM 之前，就已经在做一轮上下文准备。

---

## 6.2 loop 内部每一轮都要重新问 LLM

证据：
- `src/Core/Agent.cs:397-418`

```csharp
while (iterations < MaxToolIterations)
{
    iterations++;
    ...
    response = await activeClientForTools.CompleteWithToolsAsync(messagesToUse, toolDefs, ct);
```

并且：
- 第一轮用 `preparedContext`
- 后续轮次用 `session.Messages`

也就是说每次 tool 后，都会再回到 LLM。

---

## 6.3 loop 慢的真实构成

一次慢 tick 的典型构成为：

1. 前处理：memory/soul/context 组装
2. LLM 第 1 轮：4~10 秒
3. tool：15~80ms
4. LLM 第 2 轮：1~6 秒
5. tool：15~80ms
6. LLM 第 3 轮：2~6 秒
7. ……
8. 最终整轮 20~43 秒

### 所以本质不是“某个函数慢”

而是：

> **loop 里塞了完整 agent 回合，而且这个 agent 回合经常多轮往返。**

---

## 七、与参考项目的逐项差异清单

| 维度 | 参考项目 `external/hermes-agent-main` | 当前项目 `Hermes-Desktop` | 性能影响 |
|---|---|---|---|
| 主使用模式 | 用户 turn / cron task | NPC 常驻自治 tick | 当前项目更容易高频触发重回合 |
| 主循环上限 | `max_iterations=90` | 普通 Agent 25；NPC autonomy 6 | 关键不是上限数，而是我们真的经常跑满 5~6 轮 |
| 请求前压缩 | 有，preflight compression | 无同等级 request 级裁剪热路径 | 当前项目首轮更容易带大包 |
| system prompt cache | 有 per-session cache | 无同等级稳定前缀缓存热路径 | 当前项目每 tick 组装成本更高 |
| 老 tool result 处理 | 会裁成 1 行摘要、去重、截断参数 | 以 transcript 形式持久化后再回放 | 当前项目 session 更容易膨胀 |
| 上下文来源 | 交互会话 + 压缩 | transcript + memory + soul + plugin + active tasks | 当前项目首轮明显更肥 |
| loop 负载性质 | 一次任务内思考 | 每个 NPC 每 2 秒都在思考 | 当前项目天然更重 |
| tool 执行 | 可并行/串行混合 | 当前这条 NPC 自治链路以串行决策为主 | 不是主瓶颈 |
| 真实慢点 | 取决于具体任务 | LLM 多轮循环 | 当前项目瓶颈更集中在 agent deliberation |

---

## 八、最终判断

### 8.1 参考项目为什么看起来快

不是因为：
- 它模型更快；
- 它 max turn 更小；
- 它 tool 更轻。

而是因为：

1. 它**没有在高频 NPC 常驻 loop 场景**下用通用 agent 做自治；
2. 它有更激进的：
   - system prompt cache
   - preflight context compression
   - tool result pruning
   - tool args truncation

### 8.2 我们为什么慢

根因按优先级排序：

1. **把通用大 Agent 当高频 NPC 大脑使用**；
2. **每个 NPC 的持久 session 首轮上下文太大**；
3. **单个 tick 经常跑 3~6 次 LLM/tool 循环**；
4. **缺少参考项目级别的 request 前上下文瘦身机制**。

### 8.3 一句话总结

> 当前项目的慢，不是“游戏状态太复杂”，而是“NPC 自治 loop 把长会话 Agent 化了”。

---

## 九、后续整改方向（只列方向，不展开方案）

1. 给 autonomy 路径做**专用轻量上下文策略**，不要复用普通长会话上下文。
2. 给 autonomy 首轮加**request 级 preflight 压缩 / tool result 裁剪**。
3. 限制单 tick 的**允许 tool 链长度**，把 5~6 轮压到 2~3 轮。
4. 把一部分“高频判定”从通用 Agent 下沉成 cheap policy / structured state machine。
5. 给 `ChatAsync` 前处理增加更细粒度耗时日志，继续拆掉“LLM 开始前的空窗时间”。

---

## 十、关键证据索引

### 参考项目
- 主循环上限：`external/hermes-agent-main/run_agent.py:844`
- 主 tool loop：`external/hermes-agent-main/run_agent.py:9588`
- system prompt cache：`external/hermes-agent-main/run_agent.py:9382-9432`
- preflight compression：`external/hermes-agent-main/run_agent.py:9434-9449`
- compression 默认开启：`external/hermes-agent-main/cli.py:355-360`
- context compressor 参数：`external/hermes-agent-main/agent/context_compressor.py:329-375`
- old tool result pruning：`external/hermes-agent-main/agent/context_compressor.py:433-447`

### 当前项目
- Agent 普通迭代上限：`src/Core/Agent.cs:43-44`
- Agent 上下文组装入口：`src/Core/AgentLoopScaffold.cs:78-123`
- memory 注入：`src/Core/AgentLoopScaffold.cs:16-47`
- ContextManager transcript 加载与摘要：`src/Context/ContextManager.cs:91-164`
- ContextManager 注入 soul/plugin/task：`src/Context/ContextManager.cs:169-205`
- ChatAsync 首轮使用 preparedContext：`src/Core/Agent.cs:404-405`
- ChatAsync tool loop：`src/Core/Agent.cs:397-418`
- NPC tick 调 ChatAsync：`src/runtime/NpcAutonomyLoop.cs:123-146`
- NPC background pollInterval=2s：`src/games/stardew/StardewNpcAutonomyBackgroundService.cs:138, 337-355`
- NPC autonomy budget：`src/runtime/NpcAutonomyBudget.cs:35-47`
- SessionSearchIndex 持久化消息：`src/search/SessionSearchIndex.cs:656-698`
- TranscriptStore 保存消息：`src/transcript/TranscriptStore.cs:47-56`

### 真实日志
- Hermes 主日志：`C:\Users\Administrator\AppData\Local\hermes\hermes-cs\logs\hermes.log`
- SMAPI/Bridge 日志：`C:\Users\Administrator\AppData\Roaming\StardewValley\ErrorLogs\SMAPI-latest.txt`
