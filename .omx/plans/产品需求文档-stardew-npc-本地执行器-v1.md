# PRD：Stardew NPC 本地执行器 V1 加固

## 目标

将 Stardew NPC 本地执行器打造成一个真实、可观测、低成本的执行层：父模型保留高层意图、发言文本和任务语义；本地小模型只处理那些值得发起模型调用的、有边界的机械化工具工作。

## 需求

- autonomy 父层保持仅输出 contract、且不暴露工具。
- 本地执行器必须报告每个 action 是 `model_called`、`host_interpreted` 还是 `blocked`。
- `move` 和 `task_status` 继续作为 `delegation` 通道上的本地执行器模型调用动作。
- `observe` 通过复用现有 `stardew_status` 重新读取当前 NPC/世界状态，成为只读的、本地执行器模型调用动作，而不是走新的 personality 或 memory 路径。
- `observeTarget` 在 v1 中仅作提示信息，不意味着宿主已经支持定向观察。
- `wait` 和 `escalate` 继续作为 host-interpreted 快路径，不能消耗本地模型延迟。
- `speech` 继续保持为父层编写的意图，并由宿主提交 `stardew_speak`；它不是本地小模型对话。
- 本地执行器的工具暴露必须按 action 精确限制：
  - `move` 只能看到 `stardew_move`。
  - `task_status` 只能看到 `stardew_task_status`。
  - `observe` 只能看到 `stardew_status` 或选定的现有只读当前状态工具。
  - `wait` 和 `escalate` 不看到任何工具，也不调用模型。
- 如果 delegation client 不可用，`move`、`task_status` 和 `observe` 都必须阻塞。只有 `wait` 和 `escalate` 可以继续保持 host-interpreted。
- 父层意图 contract 和本地执行器 prompt 都应要求模型省略不相关的可选字段。
- 意图序列化必须是 action-specific 的，不能只是过滤 null。像 `escalate=false` 这种默认但无关的字段不能输出。
- 对于本地执行器中的模型调用动作，如果返回结果里没有 tool call，应使用收窄为单工具的 prompt 重试一次，然后带显式诊断结果进入 blocked。宿主不能静默地直接执行 `move` 作为兜底。
- no-tool-call 的重试必须保留 attempt 级别证据：第一次 no-tool-call、重试尝试，以及如果仍未解决时的最终 blocked 结果。
- 运行时日志必须提供足够证据来回答这些问题：
  - 父层是否确实没有任何工具？
  - 选中了哪个 action？
  - 本地执行器结果是 model-called 还是 host-interpreted？
  - 本地模型暴露了哪个受限工具？
  - 该工具是否执行，并产生了 command id？
  - speech 是否来自父层的 speech contract，而不是本地模型？
- `executorMode` 必须以向后兼容的方式加入；现有 JSONL reader/test 必须能容忍旧记录里没有这个字段。
- 在未单独加入 provider usage telemetry 之前，手测指标必须保持非计费用途。需要统计父层 intent 调用数、本地模型调用数、host-interpreted 动作数、blocked 动作数、move/task-status/observe 的工具成功数、no-tool-call 重试次数，以及宿主提交 speech 的次数。

## 非目标

- 不把 `stardew_speak` 文本生成迁移到本地模型。
- 不向本地执行器加入 gift、trade、item use、relationship 变更、memory write 或长期任务创建。
- 不开发 Settings UI，也不引入新的 provider 配置系统。
- 不引入新依赖。
- 除非现有 provider response usage 已经能在不做大范围 client 重构的前提下直接获得，否则本阶段不实现计费级 token 统计。

## 验收标准

- 每一条 `local_executor` 结果的 runtime log 记录都包含 `executorMode=model_called|host_interpreted|blocked`。
- 对 `move` 而言，日志需显示 `intent_contract accepted`、`parent_tool_surface registered_tools=0`、`local_executor selected action=move;lane=delegation`、`executorMode=model_called` 以及 `target=stardew_move`。
- 对 `task_status` 而言，本地模型只能接收到 `stardew_task_status`，父层只能接收到精简后的结果摘要。
- 对 `observe` 而言，本地模型只能接收到只读的 status/observe 工具，且结果会记录为 `executorMode=model_called`。
- 对 `wait` 而言，不发生本地模型 stream 调用，结果会记录为 `executorMode=host_interpreted`。
- 当 `speech.shouldSpeak=true` 时，日志必须显示 `host_action target=stardew_speak`，而不是 `local_executor target=stardew_speak`。
- `host_action target=stardew_speak` 可以和 `local_executor stage=blocked` 同时出现；这是预期行为，不能被计为本地执行器成功。
- 对于 action-specific 的本地执行器意图序列化：
  - `move` 省略 `commandId`、`observeTarget`、`waitReason` 和 `escalate=false`。
  - `observe` 省略 `destinationId`、`commandId` 和 `waitReason`。
  - `wait` 省略 `destinationId`、`commandId` 和 `observeTarget`。
  - `task_status` 省略 `destinationId`、`observeTarget`、`waitReason` 和 `escalate=false`。
  - `escalate` 省略 `destinationId`、`commandId`、`observeTarget` 和 `waitReason`。
- delegation 缺失时，`move`、`task_status` 和 `observe` 都会 blocked；只有 `wait` 和 `escalate` 继续保持 host-interpreted。
- 当本地模型对 model-called action 没有返回 tool call 时，runner 会重试一次；如果仍未解决，就记录 `local_executor_blocked:no_tool_call`。
- no-tool-call 重试证据使用固定诊断记录：`target=local_executor stage=attempt result=no_tool_call;attempt=1`、`target=local_executor stage=retry result=no_tool_call;attempt=2`，然后再写出最终的 blocked local executor 记录。
- 带有 `executorMode` 的新日志记录以及不带该字段的旧日志记录，都必须能被现有 runtime log tests/utilities 正常读取。
- 现有用于证明 autonomy 父层没有工具的测试继续通过。
- 聚焦的 runtime tests 覆盖 `move`、`task_status`、`observe`、`wait` 和 `no_tool_call` 重试行为。

## ADR

决策：加固当前本地执行器，而不是将其替换为通用 `AgentTool` delegation，或让每个低风险 action 都去调用本地模型。

驱动因素：

- 当前线上证据已经证明 `move` 可以走 delegation lane，但日志对 host-interpreted action 仍然存在歧义。
- 成本控制要求云端父模型避免携带工具 schema 和操作细节。
- 真实可观测性很重要：由宿主提交的 speech 与 host-interpreted 的 wait 不能被描述成本地模型输出。

已考虑的备选方案：

- 只增强日志，保留 `observe` 为 host-interpreted。拒绝原因：v1 规范明确把 observe 列入 local executor 范围，而只读 observe 已经足够安全，可以证明第二类 model-called action。
- 把所有低风险 action（包括 `wait`）都送进本地模型。拒绝原因：这会为没有实际模型/工具价值的 action 浪费延迟和算力。
- 当本地模型没有返回 tool call 时，让宿主直接执行 `move`。拒绝原因：这会掩盖本地模型失败，并破坏证据链。
- 把 `speak` 迁移到本地模型。拒绝原因：可见 speech 属于人格/面向玩家的输出，应该继续由父层编写。

影响：

- runtime logs 会变得更明确，也更容易审计。
- local executor 会从一个已被证明的 action（`move`）扩展为两类 model-called 的机械/只读能力（`task_status`、`observe`），并保留已有 `move`。
- 一些 no-tool-call 失败将按设计保持可见，而不是被宿主兜底隐藏。

后续事项：

- 仅在 chat client 能稳定暴露 provider usage 之后，再加入计费级 token usage。
- 在 `move/task_status/observe` 稳定之后，再考虑对路径失败增加低风险重试策略。
- 仅在这个 local executor 边界被证明稳定之后，再考虑为 item/gift/private-chat open 增加新的 contract 字段。
