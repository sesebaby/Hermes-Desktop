# 星露谷 NPC 任务重启恢复计划

## 目标

让 Stardew NPC 的长期 todo / 承诺在 Hermes Desktop 进程关闭后仍能恢复。恢复来源必须是已经持久化的 transcript tool result，而不是宿主重新解析玩家文本，也不是另建一份 NPC 任务状态表。

这个计划只负责恢复链路设计和执行交接，不在规划阶段改源码。

## 范围

- 持久化 `Message.TaskSessionId`，让 todo tool result 有稳定的长期任务归属键。
- 从 NPC namespace 的 `transcripts/state.db` 查询并重放 `todo` / `todo_write` tool result。
- 在 NPC runtime 启动时恢复 `instance.TodoStore`，不要求先创建 private chat 或 autonomy handle。
- 让桌面 UI 的 task view 直接读 instance-level todo store，首帧可见。
- 补齐 schema migration、混合时代数据、single-flight 并发和 runner 重启测试。

## 不做

- 不新增 `npc_task_state` / `todo_state` 之类的第二权威任务表。
- 不从玩家文本、assistant 文本或观察事件里推断承诺。
- 不增加 host-side promise detector。
- 不让宿主强迫 agent 执行承诺；宿主只恢复 agent 已通过 `todo` 工具写下的状态。
- 不把 NPC 自主 loop 改成纯事件驱动；恢复只是启动期 hydration，私聊仍是私聊，自主仍是自主。

## 当前证据

- `src/Core/Models.cs` 里 `Message.TaskSessionId` 已存在。
- `src/Core/Agent.cs` 当前多处 tool result 写入为 `TaskSessionId = session.ToolSessionId`，root/autonomy 无 `ToolSessionId` 时会落成 `null`。
- `src/games/stardew/StardewPrivateChatOrchestrator.cs` 的 private chat session 使用 `Id = "{descriptor.SessionId}:private_chat:{conversationId}"`，并设置 `ToolSessionId = descriptor.SessionId`。
- `src/runtime/NpcAutonomyLoop.cs` 的 autonomy root session 当前使用 `Id = descriptor.SessionId`，没有单独设置 `ToolSessionId`。
- `src/search/SessionSearchIndex.cs` 当前 `SchemaVersion = 9`，`messages` 表没有 `task_session_id` 列，`LoadMessages()` / `InsertMessage()` 没有 round-trip `TaskSessionId`。
- `src/transcript/TranscriptStore.cs` 保存消息后会通知 observer，live todo 已经能即时投影。
- `src/tasks/SessionTaskProjectionService.cs` 的 `OnMessageSavedAsync()` 已支持从 `Message.TaskSessionId` 投影；但 `HydrateSessionAsync()` 是单 session latest-only，且无 todo 时会 `ClearSession()`，不适合 NPC namespace 级恢复。
- `src/tasks/SessionTodoStore.cs` 的 `Write()` 默认全量覆盖，不是 merge，所以启动恢复必须避免并发回放覆盖 live 写入。
- `src/runtime/NpcRuntimeInstance.cs` 当前 `TryGetTaskView()` 依赖 private/autonomy handle，handle 不存在就返回 false。
- `src/runtime/NpcRuntimeSupervisor.cs` 的 `GetOrStartAsync()` 当前只 start instance，没有任务恢复。
- `src/runtime/NpcNamespace.cs` 已提供 `TranscriptPath` / `TranscriptStateDbPath` 和 `CreateSessionSearchIndex()` / `CreateTranscriptStore()`，可在无 handle 时打开当前 NPC namespace transcript db。
- `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs` 通过 `NpcRuntimeSupervisor.TryGetTaskView()` 给 UI 读取任务视图。

## RALPLAN-DR

### 原则

- transcript 是唯一可恢复事实源。
- 恢复只重放 agent 自己写出的 todo tool result。
- 每 NPC / save / profile 必须严格隔离。
- 启动恢复要可并发、可重试、可观测，不能靠“看起来已经启动了”。
- UI 读取任务视图不能依赖先创建聊天 handle。

### 决策驱动

- 正确性：重启后不能丢掉 NPC 已承诺的 pending / in_progress todo。
- 边界清晰：宿主不能替 NPC 猜承诺，只能恢复 `todo` 工具结果。
- 可维护性：避免第二真源和双写一致性问题。
- 兼容旧数据：旧 transcript 没有 `task_session_id` 时要有受限 fallback。
- 并发安全：启动期多个入口并发时不能互相覆盖任务快照。

### 可选方案

| 方案 | 做法 | 优点 | 问题 | 结论 |
| --- | --- | --- | --- | --- |
| A. 新增 `npc_task_state` 表 | 每次 todo 写入同步聚合表，启动直接读聚合 | 启动读取简单 | 第二真源，双写漂移后很难排查 | 不采用 |
| B. 复用 `HydrateSessionAsync()` | 对 root/private/autonomy session 分别 hydrate | 改动少 | 抽象错误；无 todo session 会清空；不能表达 NPC namespace 级长期任务 | 不采用 |
| C. transcript tool result 专用查询 + NPC runtime 专用 hydrator | 持久化 `task_session_id`，启动按 task session 重放 todo tool result | 单一事实源；和现有 projection 语义一致；可测 | 需要 schema/query/hydration 并发控制 | 采用 |

## 决策

采用方案 C：把 `task_session_id` 补成持久化字段，并新增 NPC runtime 专用 hydrator，从当前 NPC namespace transcript db 读取 todo tool result，按顺序投影到 instance-level `SessionTodoStore`。

## 架构合同

### 1. 新写入的 task session 归属

所有新写入的 todo tool result 都必须持久化非空任务归属键：

```csharp
TaskSessionId = session.ToolSessionId ?? session.Id
```

执行时要修改 `src/Core/Agent.cs` 里所有创建 tool result `Message` 的路径。当前 private chat 仍会通过 `ToolSessionId = descriptor.SessionId` 投影到 NPC root session；root/autonomy 自身没有 `ToolSessionId` 时，也会显式写为自身 `session.Id`。

这个合同是必须项。否则在“已有 private-chat 显式行 + 更晚 root 行仍为 null”的混合数据中，显式查询会禁用 fallback，导致漏掉最新 root 快照。

### 2. SQLite schema 与 round-trip

- `SessionSearchIndex.SchemaVersion` 从 `9` 升到 `10`。
- `messages` 表新增 `task_session_id TEXT`。
- `EnsureMessagesColumns()` 对旧库补 `task_session_id`。
- `InsertMessage()` 写 `message.TaskSessionId`。
- `LoadMessages()` SELECT `task_session_id` 并回填 `Message.TaskSessionId`。
- 新增索引：

```sql
CREATE INDEX IF NOT EXISTS idx_messages_task_session_tool
ON messages(task_session_id, role, tool_name, timestamp, id);
```

旧库升级后已有消息保持原顺序可读，legacy row 的 `TaskSessionId` 允许为 `null`。

### 3. 专用 todo 查询 API

新增 API：

```csharp
public IReadOnlyList<Message> LoadTodoToolResultsByTaskSessionId(
    string taskSessionId,
    string legacyPrivateChatPrefix,
    bool includeLegacyFallback)
```

```csharp
public Task<IReadOnlyList<Message>> LoadTodoToolResultsByTaskSessionIdAsync(
    string taskSessionId,
    string legacyPrivateChatPrefix,
    CancellationToken ct)
```

落点：

- `SessionSearchIndex` 负责 SQL。
- `TranscriptStore` 只做 async 包装和 cancellation check。

显式查询合同：

```sql
WHERE task_session_id = $taskSessionId
  AND LOWER(role) = 'tool'
  AND LOWER(tool_name) IN ('todo', 'todo_write')
ORDER BY timestamp, id
```

如果显式结果非空，立即返回，不混入 fallback。

fallback 只在显式为空且 `includeLegacyFallback=true` 时启用。`legacyPrivateChatPrefix` 必须固定为：

```text
${taskSessionId}:private_chat:
```

必须带尾随冒号，空白 prefix 直接拒绝。

fallback SQL 禁止使用未转义 `LIKE`。使用非通配前缀比较，避免 `_` / `%` 被 SQLite 当成通配符：

```sql
WHERE LOWER(role) = 'tool'
  AND LOWER(tool_name) IN ('todo', 'todo_write')
  AND (
      session_id = $taskSessionId
      OR substr(session_id, 1, length($legacyPrivateChatPrefix)) = $legacyPrivateChatPrefix
  )
ORDER BY timestamp, id
```

### 4. NPC runtime hydrator

新增 `INpcRuntimeTaskHydrator`：

```csharp
public interface INpcRuntimeTaskHydrator
{
    Task HydrateAsync(NpcRuntimeInstance instance, CancellationToken ct);
}
```

新增默认实现 `NpcRuntimeTaskHydrator`：

- 构造参数至少包含 logger / logger factory。
- 基于 `instance.Namespace.CreateSessionSearchIndex(...)` 和 `instance.Namespace.CreateTranscriptStore(sessionIndex, messageObserver: null)` 打开当前 NPC namespace transcript db。
- 不依赖 private chat handle。
- 不依赖 autonomy handle。
- 只读取 `instance.Descriptor.SessionId` 对应的 todo tool result。
- legacy prefix 固定为 `$"{sessionId}:private_chat:"`。
- 使用临时 `SessionTaskProjectionService(instance.TodoStore)` 或等价 helper 按查询顺序投影到 `instance.TodoStore`。
- 不调用现有 `HydrateSessionAsync()`。
- 不在没有 todo result 时清空 session。
- `SessionSearchIndex` 必须 `using` / dispose；`TranscriptStore` 当前不实现 `IDisposable`，不要新增伪 dispose 义务。

`NpcRuntimeSupervisor` 增加可测试构造注入：

- 默认构造使用真实 `NpcRuntimeTaskHydrator`。
- 测试可传 fake `INpcRuntimeTaskHydrator`。

### 5. single-flight 恢复

`NpcRuntimeInstance` 负责 single-flight，因为它拥有唯一 `Descriptor.SessionId` 和 instance-level `TodoStore`。

新增入口：

```csharp
Task EnsureTasksHydratedAsync(INpcRuntimeTaskHydrator hydrator, CancellationToken ct)
```

或等价签名。

状态字段受 `_gate` 保护：

```csharp
private Task? _taskHydrationTask;
private bool _tasksHydrated;
private Exception? _lastTaskHydrationError;
```

合同：

- `_tasksHydrated=true` 时快速返回。
- 已有未完成 `_taskHydrationTask` 时，当前调用 await 同一个 task。
- 没有 in-flight 时创建共享 hydration task。
- 共享 hydration task 不能绑定任一调用者的 `CancellationToken`；使用 `CancellationToken.None` 或实例级内部 token。
- 调用者 `ct` 只能取消自己的等待，不能取消共享回放。
- 成功后设置 `_tasksHydrated=true`，清 `_lastTaskHydrationError`。
- 失败后清 `_taskHydrationTask`，不设置 `_tasksHydrated`，记录 `_lastTaskHydrationError`，允许下一次调用重试。
- 不在 `_gate` 内 await 或执行回放。

`NpcRuntimeSupervisor.GetOrStartAsync()` 在：

```csharp
await instance.StartAsync(ct);
```

之后立即：

```csharp
await instance.EnsureTasksHydratedAsync(_taskHydrator, ct);
```

`GetOrCreatePrivateChatHandleAsync()` / `GetOrCreateAutonomyHandleAsync()` / driver 创建路径都通过 `GetOrStartAsync()`，共享同一恢复入口。

### 6. task view 与 UI

`NpcRuntimeInstance.TryGetTaskView()` 改为直接读取 instance-level `TodoStore`：

```csharp
taskView = new NpcRuntimeTaskView(sessionId, _todoStore.Read(sessionId));
```

不再依赖：

```csharp
_privateChatHandle ?? _autonomyHandle?.AgentHandle
```

`NpcRuntimeSupervisor.TryGetTaskView()` 继续遍历 instances。`NpcRuntimeWorkspaceService` 不需要新增 UI 字符串。

如果执行中新增状态/错误文案，必须同步补：

- `Desktop/HermesDesktop/Strings/*/Resources.resw`

## 实施步骤

1. 修改 `Agent` tool result 写入归属。
   - 文件：`src/Core/Agent.cs`
   - 把所有 `TaskSessionId = session.ToolSessionId` 改为 `TaskSessionId = session.ToolSessionId ?? session.Id`。

2. 修改 SQLite schema 与 message round-trip。
   - 文件：`src/search/SessionSearchIndex.cs`
   - 升级 `SchemaVersion`。
   - 增加 `task_session_id` 列、索引、insert/read 字段。
   - 增加 `LoadTodoToolResultsByTaskSessionId(...)`。

3. 增加 transcript wrapper。
   - 文件：`src/transcript/TranscriptStore.cs`
   - 增加 `LoadTodoToolResultsByTaskSessionIdAsync(...)`。

4. 增加 NPC runtime task hydrator。
   - 文件建议：`src/runtime/NpcRuntimeTaskHydrator.cs`
   - 新增 `INpcRuntimeTaskHydrator` 和默认实现。

5. 接入 runtime single-flight。
   - 文件：`src/runtime/NpcRuntimeInstance.cs`
   - 增加 `EnsureTasksHydratedAsync(...)`、in-flight 状态和直接 `TodoStore` task view。
   - 文件：`src/runtime/NpcRuntimeSupervisor.cs`
   - 增加 hydrator 注入，`GetOrStartAsync()` 后 hydrate。

6. 补测试。
   - `Desktop/HermesDesktop.Tests/Services/TranscriptStoreTests.cs`
   - `Desktop/HermesDesktop.Tests/Services/SessionTaskProjectionServiceTests.cs`
   - `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
   - `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`

## 验收标准

- 新写入 tool result 的 `TaskSessionId` 对 root/autonomy/private-chat 都非空。
- 新库能保存并读回 `TaskSessionId`。
- 旧库升级到 schema version 10 后旧消息不丢、顺序不变、legacy row 的 `TaskSessionId == null`。
- 显式 `task_session_id` 结果存在时不混入 fallback。
- fallback 只在显式为空时启用，只匹配当前 root session 和 `${root}:private_chat:` 前缀。
- prefix 匹配不受 `_` / `%` 影响，不误匹配 `private_chatty` 或相近 root。
- mixed-era 数据恢复到最新正确快照。
- runtime 启动恢复是 single-flight：并发入口共享同一个 in-flight task。
- waiter cancellation 不会取消共享 hydration。
- hydration 失败后可重试，不误标 hydrated。
- 只 `GetOrStartAsync()`、不创建任何 handle，也能 `TryGetTaskView(root)` 看到恢复任务。
- runner/supervisor 重建后，未发新消息前即可看到旧 pending todo。

## 测试计划

### Transcript / schema

- 保存带 `TaskSessionId` 的 tool message，清 cache 或新建 store 后 `LoadSessionAsync()`，断言字段 round-trip。
- 构造 schema 9 或更旧的 db，打开新 store 后断言：
  - `messages` 包含 `task_session_id`。
  - `schema_version=10`。
  - 旧消息仍按原顺序读出。
  - legacy row 的 `TaskSessionId == null`。

### 查询 API

- 显式结果存在时不混 fallback。
- fallback 只匹配 `role='tool'` 且 `tool_name IN ('todo','todo_write')`。
- fallback prefix 安全：
  - `root:private_chat:c1` 匹配。
  - `root:private_chatty:c1` 不匹配。
  - 含 `_` 的 root 不因 SQL 通配误匹配相近 root。
- 排序固定为 `timestamp, id`。

### 混合时代恢复

构造三条 todo tool result：

- T1：旧 root 行，`session_id=root`，`task_session_id=NULL`，内容 `old-root`。
- T2：新 private-chat 行，`session_id=root:private_chat:c1`，`task_session_id=root`，内容 `private-chat-update`。
- T3：新 root/autonomy 行，`session_id=root`，`task_session_id=root`，内容 `root-newest`。

调用查询或 hydrator 后断言：

- 最终投影为 T3 的 `root-newest`。
- 不重复旧 root 行。
- 不把 T1 覆盖到 T3 之后。

### Hydrator

- root session + private chat session 各有 todo tool result，恢复后投影到 root `descriptor.SessionId`。
- 重复 hydrate 不重复、不清空。
- 没有 todo tool result 时不清空已有 live todo。

### 并发 / single-flight

用 fake `INpcRuntimeTaskHydrator` + `TaskCompletionSource` 注入 supervisor：

- 并发调用 `GetOrStartAsync()` 和 `GetOrCreatePrivateChatHandleAsync()`。
- fake hydrator 只调用 1 次。
- 两个 caller 等待同一个 in-flight。
- 取消其中一个 waiter，不取消共享 hydration。
- fake hydrator 第一次抛异常后，下一次 `GetOrStartAsync()` 会重试。
- 如果能稳定模拟 live todo：hydrate 卡住期间写入 live todo，释放后不覆盖 live 最新状态。
- 如果 live todo 模拟不稳定：至少断言 handle 创建发生在 hydration await 之后，并补投影层测试证明后到的 live todo 会覆盖旧快照。

### Runtime / UI

- 只调用 `GetOrStartAsync()`，不创建 private/autonomy handle，也能 `TryGetTaskView(descriptor.SessionId)` 看到恢复任务。
- `StardewNpcPrivateChatAgentRunnerTests`：第一次 runner 写 todo，销毁/recreate supervisor/runner；未发新消息前可见旧 pending todo。

## 验证命令

先跑窄范围：

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~TranscriptStoreTests|FullyQualifiedName~SessionTaskProjectionServiceTests|FullyQualifiedName~NpcRuntimeSupervisorTests|FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~HermesChatServiceTaskLoopTests"
```

再跑桌面测试全量：

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

若修改 bridge 侧文件，本计划原则上不需要；但若执行中触碰 `Mods/StardewHermesBridge/**`，补跑：

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
```

## 风险与缓解

| 风险 | 缓解 |
| --- | --- |
| mixed-era 数据漏掉更晚 root/autonomy 快照 | 新写入统一 `TaskSessionId = session.ToolSessionId ?? session.Id`，并用混合时代测试锁死 |
| fallback prefix 误匹配 | 禁止未转义 LIKE，使用 `substr(...)=prefix` |
| 启动并发回放覆盖 live todo | instance-level single-flight，handle 创建经 `GetOrStartAsync()` 等待恢复完成 |
| UI 首帧看不到任务 | `TryGetTaskView()` 脱离 handle，直接读 `TodoStore` |
| hydrator 打开错 namespace | hydrator 只从 `instance.Namespace` 创建 search index / transcript store |
| 恢复失败后永久不再恢复 | 失败清 in-flight，不设置 hydrated，允许重试 |
| 任务状态变成第二真源 | 不新增权威状态表；性能缓存如果未来需要，必须可重建、不可作为事实源 |

## ADR

### Decision

用 `task_session_id` 持久化 todo tool result 的长期归属，并在 NPC runtime 启动时从当前 NPC namespace transcript db 重放 todo tool result 到 instance-level `SessionTodoStore`。

### Drivers

- 任务恢复必须跨 Hermes Desktop 进程。
- 宿主不能替 agent 推断承诺。
- NPC 私聊和自主 loop 要共享长期任务视图。
- 旧 transcript 需要可迁移、可 fallback。
- 启动并发不能造成任务快照回滚。

### Alternatives considered

- 新增聚合状态表：拒绝，因为会引入第二真源和双写一致性问题。
- 复用 `HydrateSessionAsync()`：拒绝，因为它是单 session latest-only，且无 todo 会清空 session。
- host-side promise detector：拒绝，因为违背“agent 决策、宿主执行”的边界。

### Why chosen

该方案复用现有 `Message.TaskSessionId`、`SessionTaskProjectionService` 和 `SessionTodoStore` 语义，只补齐持久化、查询和启动恢复链路。它不让宿主创造任务，只恢复 agent 自己已经写下的 tool result。

### Consequences

- 需要一次 schema version 升级。
- 需要新增专用查询 API 和 runtime hydrator。
- 需要把 `Agent` tool result 写入合同收紧为 `session.ToolSessionId ?? session.Id`。
- 旧数据可有限恢复，但旧 root 行如果之后出现新显式数据，不应继续混入。

### Follow-ups

- 如果 transcript 增长导致启动恢复慢，只能新增可重建缓存，不得新增权威任务表。
- 后续可增加恢复日志：恢复条数、显式/fallback 模式、耗时、最终 todo 数量。
- 如果新增 UI 错误状态，补齐 `Resources.resw` 多语言资源。

## Agent roster

可用 agent 类型：

- `explore`：快速查文件、符号和现有测试落点。
- `architect`：确认 schema / runtime 边界，处理并发和事实源取舍。
- `executor`：实现源码改动。
- `test-engineer`：补测试和验证边界条件。
- `build-fixer`：处理编译、nullable、测试失败。
- `verifier`：复核验收标准、命令输出和未测风险。
- `code-reviewer`：最后代码审查，重点看双真源、fallback 和并发。

## Ralph handoff

推荐 `$ralph` 单 owner 执行，因为改动跨持久化、runtime 和测试，但实现路径已经收敛，不需要多队伍并行抢同一文件。

建议提示：

```text
$ralph 执行 .omx/plans/星露谷NPC任务重启恢复计划.md。按计划实现，不新增第二任务真源，不从玩家文本推断承诺。先写/改测试锁住 TaskSessionId round-trip、mixed-era 查询、single-flight hydration 和无 handle task view，再实现源码。执行完跑计划里的窄范围测试和 Desktop 测试全量。
```

推荐节奏：

1. `executor` 实现 schema / query / `Agent` 写入合同。
2. `executor` 实现 hydrator / single-flight / task view。
3. `test-engineer` 或同一 owner 补齐测试。
4. `verifier` 跑命令并核对验收。

## Team handoff

如果改用 `$team`，建议拆成 3 条互不重叠写入线：

- Lane A：`src/Core/Agent.cs`、`src/search/SessionSearchIndex.cs`、`src/transcript/TranscriptStore.cs`。
- Lane B：`src/runtime/NpcRuntimeTaskHydrator.cs`、`src/runtime/NpcRuntimeInstance.cs`、`src/runtime/NpcRuntimeSupervisor.cs`、`src/runtime/NpcRuntimeBindings.cs`。
- Lane C：测试文件，先写失败测试，再协同实现修复。

团队启动提示：

```text
$team 执行 .omx/plans/星露谷NPC任务重启恢复计划.md。Lane A 负责持久化和查询；Lane B 负责 runtime hydration 和 UI task view；Lane C 负责测试。所有 lane 必须遵守 transcript 单一事实源、TaskSessionId=session.ToolSessionId??session.Id、substr prefix fallback、single-flight cancellation 合同。
```

Team verification path：

- Team 退出前必须提供每条验收标准对应的测试或手动证据。
- Ralph 或 leader 最后统一跑计划中的窄范围测试和 Desktop 测试全量。

## 共识记录

- Architect 要求补充按 task session 的专用查询、拒绝复用 `HydrateSessionAsync()`、让 `TryGetTaskView()` 直接读 instance store。
- Architect 要求恢复必须 single-flight，避免并发启动回放覆盖 live todo。
- Critic 要求明确 hydrator 在无 handle 时如何打开 transcript、取消语义、测试 seam、legacy prefix 和 migration 存活断言。
- Critic 最终要求补齐 mixed-era 数据合同：新写入必须 `TaskSessionId = session.ToolSessionId ?? session.Id`，prefix 禁止未转义 LIKE，并增加混合时代恢复测试。
- 最终复核 verdict：`APPROVE`，阻断项为无。
