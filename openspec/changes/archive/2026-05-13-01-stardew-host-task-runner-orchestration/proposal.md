## Why

Stardew NPC 编排目前经历过小模型执行层、local executor、私聊委托、action lifecycle 等多条思路，已经出现复杂度上升、归因困难和双轨风险。现在需要对齐 `external/hermescraft-main` 的参考模式：主 agent 通过可见工具做决策，host/bridge task runner 负责机械执行、状态、超时、watchdog 和事实回传。

这个变更的目的，是在继续扩展制造、交易、任务、采集等窗口能力前，先统一底层编排 contract，并彻底退役 Stardew v1 gameplay 路径中的小模型执行层，避免后续能力建立在错误抽象上。

## What Changes

- **BREAKING**：Stardew v1 gameplay 执行路径不再保留小模型执行层；移动、说话投递、微动作、窗口操作、私聊即时行动闭环、`todo` 收口都不得交给本地小模型或 hidden executor。
- 建立统一的 host task runner contract：主 agent 调用模型可见工具后，runtime 创建 host task / work item，host/bridge 执行机械动作，并通过 task status、terminal fact、runtime log 和 wake reason 回传结果。
- 将 private chat、scheduled ingress、autonomy、MCP wrapper、native Stardew tools 收敛到同一 host task lifecycle，而不是各自维护一套完成/失败语义。
- 明确 host task 与长期 `todo` 的边界：host task 只记录机械执行；长期承诺、关系判断、台词内容、`todo` 完成/阻塞/失败由主 agent 根据事实显式处理。
- 引入窗口类任务的一致编排要求：制造、交易、任务、采集等窗口都必须通过 UI lease、active menu 检查、bounded mechanical steps、observable validation、timeout/cancel/release 来完成。
- 建立 harness 门禁：不启动 Stardew/SMAPI 也要证明 task id、status、terminal fact、wake、UI lease cleanup、ID correlation、MCP/native parity，以及没有小模型执行 lane / hidden fallback。
- 清退或重标旧路径：相关 prompt、tool surface、harness、配置、文档、测试期望不得继续暗示 Stardew gameplay 由小模型执行；废弃能力必须同步退役，禁止双轨、影子实现和兼容分叉。

## Capabilities

### New Capabilities

- `stardew-host-task-runner`: 定义 Stardew NPC host task / work item 的生命周期、状态、ID 关联、terminal fact、watchdog、wake policy，以及主 agent 与 host/bridge 的职责边界。
- `stardew-ui-task-lifecycle`: 定义制造、交易、任务、采集、私聊等窗口类任务的 UI lease、active menu 冲突、bounded operation、状态验证、关闭/取消/超时/恢复要求。
- `stardew-orchestration-harness`: 定义统一 harness 的验收矩阵，覆盖 native/MCP/private-chat/scheduled/autonomy 入口、ID 配对、runtime fact、replay/idempotency、负向门禁和退役小模型执行路径。

### Modified Capabilities

- 无。当前 `openspec/specs/` 下没有已存在的 capability 规格可修改；本变更先新增 Stardew 编排相关 capability。

## Impact

- 影响运行时编排：`src/games/stardew/StardewNpcAutonomyBackgroundService.cs`、`src/games/stardew/StardewNpcTools.cs`、`src/games/stardew/StardewPrivateChatOrchestrator.cs`、`src/runtime/NpcRuntimeInstance.cs`、`src/runtime/NpcRuntimeDriver.cs`、`src/runtime/NpcRuntimeStateStore.cs`。
- 影响旧 local executor / delegation 相关路径：`src/runtime/NpcLocalExecutorRunner.cs`、`src/runtime/NpcAutonomyLoop.cs`、`src/games/stardew/StardewNpcTools.cs` 中涉及 Stardew gameplay 执行的 prompt、tool surface、测试期望需要迁移或退役。
- 影响 MCP/native/private chat parity：`src/runtime/AgentCapabilityAssembler.cs`、Stardew MCP wrapper、private chat delegated action 入口需要统一到 host task lifecycle。
- 影响测试与 harness：`Desktop/HermesDesktop.Tests/Stardew/*`、`Desktop/HermesDesktop.Tests/Runtime/*`、`Desktop/HermesDesktop.Tests/Mcp/McpServerTests.cs` 需要补齐 host task、UI/window、replay/idempotency、无小模型执行 lane 的回归验证。
- 影响项目文档与约束：`AGENTS.md`、`.omx/project-memory.json`、`.omx/specs/星露谷主Agent与宿主任务Runner统一编排方案.md` 已确立“不允许双轨、废弃能力必须退役”的原则，后续 proposal/design/tasks 必须遵守。
- 不引入新外部依赖，不新增外部 MCP server，不新增第二 tool/model lane。
