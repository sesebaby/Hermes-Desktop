# 星露谷 NPC 记忆参考对齐上下文快照

## 任务

按参考项目方案，把星露谷 NPC 的长期记忆写入边界拉回参考项目做法，然后实施。

## 目标结果

- NPC 自主循环普通 tick 不再自动写 `MEMORY.md`。
- `memory` 工具仍是长期记忆的唯一正常写入口。
- `todo` 仍只做当前会话任务列表，不接长期记忆。
- `runtime.jsonl` 继续保存 trace、命令、失败、诊断证据。

## 已知证据

- 参考项目 `external/hermes-agent-main/tools/memory_tool.py`：
  - `MEMORY.md` / `USER.md` 是固定文件型长期记忆。
  - `memory` 工具动作只有 `add`、`replace`、`remove`、`read`。
  - `add` 超容量会拒绝，并要求先 `replace` 或 `remove`。
- 参考项目 `external/hermes-agent-main/tools/todo_tool.py`：
  - `todo` 是当前 agent / 当前会话的任务列表。
  - 压缩后只保留 `pending` 和 `in_progress`。
  - 不是长期记忆，也不保存历史行动日志。
- 当前项目 `src/runtime/NpcAutonomyLoop.cs`：
  - 当前在每轮自主行动后调用 `WriteMemoryAsync(...)`。
  - `WriteMemoryAsync(...)` 会写入 `Autonomy tick {traceId}: ...`。
- 当前项目 `src/memory/MemoryManager.cs` 和 `src/Tools/MemoryTool.cs`：
  - 已有接近参考项目的固定文件型记忆和工具入口。

## 约束

- 严格对齐参考项目，不新增参考项目没有的本地过滤器、黑名单、自动分流或压缩器。
- 这是游戏 NPC，但玩家不确认每条 NPC 记忆。
- 开发阶段旧污染记忆不迁移、不兼容。
- 默认中文汇报。

## 待确认问题

无。用户已经要求开始计划并实施。

## 可能改动点

- `src/runtime/NpcAutonomyLoop.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- 可能还要调整 `Desktop/HermesDesktop.Tests/Runtime/NpcAgentFactoryTests.cs` 中与 `Autonomy tick` 相关的断言。
