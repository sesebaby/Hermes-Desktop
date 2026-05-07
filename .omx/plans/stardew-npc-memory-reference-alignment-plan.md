# 星露谷 NPC 记忆参考对齐实施计划

## 需求摘要

按参考项目做法修正 NPC 长期记忆边界：普通自主行动 tick 不能自动写入 `MEMORY.md`；长期记忆只能通过现有 `memory` 工具由模型主动写入。行动 trace、命令结果、失败和诊断继续留在 `runtime.jsonl`。

## RALPLAN-DR 摘要

### 原则

- 参考项目优先：只保留参考项目已经有的记忆边界。
- 权威入口单一：长期记忆写入走 `memory` 工具，不走自主循环旁路。
- 日志和记忆分开：执行证据留在 `runtime.jsonl`，不混入 `MEMORY.md`。
- 改动要窄：只处理当前污染来源，不重做记忆系统。

### 决策驱动

- 当前污染来自 `NpcAutonomyLoop` 每轮自动写 `Autonomy tick`。
- 现有 `MemoryManager` / `MemoryTool` 已经足够承接参考项目方案。
- 用户明确不要参考项目没有的黑名单、自动分流、复杂压缩。

### 可选方案

#### 方案 A：删除自主循环自动写记忆路径

做法：移除 `NpcAutonomyLoop` 每轮调用 `WriteMemoryAsync(...)` 的逻辑和私有写入方法。测试改为断言普通 tick 不写 `MEMORY.md`。

优点：最贴近参考项目，最小改动，直接切断污染来源。

代价：本地执行器返回的 `MemorySummary` 也不会再自动进入长期记忆；如果 NPC 真要记住，需要模型自己调用 `memory` 工具。

#### 方案 B：保留方法但默认跳过写入

做法：保留 `WriteMemoryAsync(...)`，但让默认路径永远 `skipMemory`。

优点：改动更少。

代价：保留了参考项目没有的旁路，后续容易被重新打开，不够干净。

#### 方案 C：增加过滤规则后继续自动写

做法：对 trace、命令、移动失败做过滤，再自动写剩余内容。

优点：表面上能减少污染。

代价：参考项目没有这种机制，用户已经明确拒绝。

推荐：方案 A。

## ADR

### 决定

采用方案 A：删除自主循环自动写 `MEMORY.md` 的路径，保留现有 `memory` 工具作为长期记忆入口。

### 驱动

- 参考项目没有每轮行动自动写长期记忆。
- 当前污染正来自这条自动写入旁路。
- 现有工具和存储已经能表达参考项目边界。

### 被拒绝方案

- 方案 B：保留旁路但默认跳过。拒绝原因：代码里仍留着参考项目没有的隐性通道。
- 方案 C：加过滤后继续自动写。拒绝原因：这是本地发挥，不是参考项目方案。

### 后果

- 普通自主 tick 不再让 `MEMORY.md` 变大。
- `MemorySummary` 只适合留在日志或返回值语义里，不再自动变成长期记忆。
- NPC 需要长期记住事实时，必须通过 `memory` 工具写。

### 后续

- 开发环境旧污染 `MEMORY.md` 可手动清空或重建 NPC profile。
- 如果未来想加整理器，必须先登记为偏离参考项目。

## 实施步骤

1. 修改 `src/runtime/NpcAutonomyLoop.cs`：
   - 移除 `memorySummary` / `skipMemory` 对自动写记忆的驱动。
   - 移除 `WriteMemoryAsync(...)`。
   - 如果 `_memoryManager` 只服务这条旁路，再移除字段和构造参数；若构造链路影响面过大，可先保留参数但不使用，并用后续清理处理。
2. 修改 `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`：
   - 把 `RunOneTickAsync_WithDecisionResponse_WritesNpcLocalMemory` 改成普通 tick 不写 `MEMORY.md`。
   - 把本地执行器 move intent 测试从“写 summary memory”改成“不写 memory，但日志证据仍在”。
   - 检查其他依赖自动写记忆的断言。
3. 如有需要，调整 `Desktop/HermesDesktop.Tests/Runtime/NpcAgentFactoryTests.cs` 里 `Autonomy tick` 相关断言，让它继续验证“污染不会进入提示词”而不是依赖污染存在。
4. 运行针对性测试：
   - `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcAutonomyLoopTests"`
   - 如被改到，再运行 `NpcAgentFactoryTests`。

## 验收标准

- 自主循环普通 tick 后，`MemoryManager.ReadEntriesAsync("memory")` 返回空。
- 本地执行器 move intent 后，`MEMORY.md` 仍为空。
- `runtime.jsonl` 仍有 local executor 和诊断记录。
- `MemoryToolTests` 不需要因本次改动变化。
- 代码里没有新增黑名单、过滤器、自动分流或新存储。

## 风险和处理

- 风险：构造函数移除 `MemoryManager` 会牵动较多调用点。
  - 处理：如果影响面过大，先保留构造参数但移除自动写行为，保持 diff 窄。
- 风险：旧测试名和断言还在表达自动写记忆。
  - 处理：改测试名和断言，让测试表达参考项目边界。
- 风险：本地执行器 `MemorySummary` 名称残留，容易误导。
  - 处理：本次不扩大协议改动；先保证它不再写入长期记忆，后续单独清理命名。

## 可用执行角色建议

- 单人执行足够：改动窄，主要是删除旁路和改测试。
- 若拆分并行，`executor` 改代码，`test-engineer` 改测试，`verifier` 跑验证。

## 验证路径

先跑 `NpcAutonomyLoopTests`。如果失败集中在构造参数或旧断言，按失败继续修。若 `NpcAgentFactoryTests` 受影响，再跑对应类。最后用 `rg "Autonomy tick"` 确认生产代码没有继续写长期记忆。
