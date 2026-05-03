# 星露谷 NPC 常驻智能体修复前缺口扫描共识计划

## Plan Summary

**Plan saved to:** `.omx/plans/星露谷NPC常驻智能体修复前缺口扫描共识计划.md`

**Scope:**
- 1 个修复倡议，覆盖 runtime、Stardew host、bridge protocol、测试
- 预计复杂度：HIGH

**Key Deliverables:**
1. 明确 Stardew NPC 常驻智能体当前除 `smapiName/targetEntityId` body binding 与 cron durable inbox 外的执行级功能缺口
2. 给出分阶段、可验收、可直接进入执行模式的修复计划

## Evidence-Based Conclusion

基于当前仓库证据，除了用户已同意的两条主线外，**还必须补 1 个独立功能缺口，且应一并补 1 个协议缺口**：

1. **必须补：replay-safe fanout / per-NPC delivery 语义缺失**
   - `src/runtime/NpcAutonomyLoop.cs` 当前直接遍历 `eventBatch.Records`，没有基于 NPC 已持久化 `EventCursor` 过滤已交付事件。
   - `src/games/stardew/StardewNpcAutonomyBackgroundService.cs` 当前用 host staged batch 对多 NPC fanout，但 worker 执行时传入的仍是整批 `sharedEventBatch`。
   - `src/runtime/NpcObservationFactStore.cs` 仅追加事实，不去重；若 host 在 staged batch commit 前崩溃并重放，同一 NPC 会再次记录并消费相同事件。
   - 这不是“实现细节优化”，而是 crash/replay 下的行为正确性缺口；它独立于“cron due 改成 durable inbox”。

2. **同一修复窗口内应补：bridge `seq/next_seq` 协议未闭合**
   - app 侧 DTO 与 event source 已按 `seq/next_seq` 设计：`src/games/stardew/StardewBridgeDtos.cs`、`src/games/stardew/StardewEventSource.cs`。
   - bridge 侧仍未公开这些字段：`Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`、`Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`、`Mods/StardewHermesBridge/Bridge/BridgeEventBuffer.cs`。
   - 当前 host / runtime state store 已持久化 `GameEventCursor.Sequence`，但真实协议没有兑现，导致 cursor 语义半成品。
   - 这项缺口可以排在 fanout/inbox 之后实现，但不应从本次修复中拆出。

以下项**不是本轮必须新增的独立功能**：

- 不必重新引入独立 private-chat background service；`StardewPrivateChatRuntimeAdapter` 已是统一路径。
- 不必先重构成全新通用 worker 框架；当前 per-NPC `NpcAutonomyTracker` 可保留，只要补齐 inbox append、delivery filtering、cursor commit 契约。

## Architect Revision Applied

Architect 审查后，本计划做三处收口：

1. **body binding 不是只给 command path 补字段。**
   - 修复范围必须覆盖 command、query/status、world snapshot 与 bridge resolver 输入。
   - 优先引入中性 `NpcBodyBinding` / `NpcEntityBinding` value object，由 runtime descriptor 或 Stardew binding 持有，避免把 Stardew-only 字段散落到各调用点。
   - bridge alias fallback 可以保留为兼容/诊断防线，但生产路径不能依赖 fallback 才能找到 NPC。

2. **cron durable inbox 与 host shared-event fanout 分开建模。**
   - cron due 是“待执行 work”，需要独立最小 `InboxItem` / `IngressWorkItem`，不能混用 `PendingWorkItem`。
   - `PendingWorkItem` / `ActionSlot` 只表示已经开始准备或已提交的外部动作。
   - shared bridge events 已有 host staged batch，不能为了“inbox”再把每条 shared event 复制进每个 NPC runtime。

3. **replay-safe delivery 的主过滤点在 host fanout / dispatch。**
   - host 读取每个 NPC persisted `Controller.EventCursor`，先裁剪 batch，再交给 `NpcAutonomyLoop`。
   - ack 只在 tick 成功后推进 watermark。
   - `NpcObservationFactStore` 可加去重作为防线，但不能把去重当成主修复。

## RALPLAN-DR

### Principles

1. **Runtime identity must be explicit.** NPC body binding 必须由 runtime/descriptor 显式携带，不依赖 bridge alias fallback 猜测。
2. **Ingress must become durable before side effects.** cron due 必须先落入可恢复 inbox；host shared events 必须先 host-stage，再按 per-NPC watermark 过滤后投递。
3. **Replay must be idempotent at NPC runtime boundary.** host staged batch 重放不能让同一 NPC 重复消费已交付事件。
4. **Single host poll, per-NPC deterministic delivery.** 继续保持单 host event pump，但每个 NPC 的投递/确认边界必须独立且可恢复。
5. **Protocol and storage semantics must agree.** `GameEventCursor.Sequence`、bridge `seq/next_seq`、host/runtime store 不能继续半联通。

### Decision Drivers

1. **Crash/restart correctness**：当前 staged batch replay 会导致 NPC 重复吃事件，这是执行正确性问题，不是后续优化。
2. **Identity correctness**：`NpcId -> SMAPI NPC` 的 alias fallback 继续存在会让 runtime/body binding 与 bridge 行为分叉。
3. **Incremental delivery with minimal churn**：应优先复用现有 `NpcRuntimeDriver`、`NpcRuntimeStateStore`、`PendingWorkItem`、`ActionSlot`、`StardewRuntimeHostStateStore`，避免重造总线。

### Viable Options

#### Option A: 在现有 runtime controller 上增量补齐 durable cron inbox + fanout filtering + seq protocol

优点：
- 最大化复用现有 `NpcRuntimeDriver` / `PendingWorkItem` / `ActionSlot` / `NpcAutonomyTracker`
- diff 可控，和现有测试结构贴合
- 能逐阶段锁定 body binding、inbox append、replay filter、bridge seq

缺点：
- 需要仔细界定 `PendingWorkItem` 与“inbox item / ingress journal”边界，避免概念继续混杂
- `StardewNpcAutonomyBackgroundService` 里仍保留部分 host-specific 编排复杂度

#### Option B: 先抽象全新通用 runtime inbox service / event bus，再迁移 Stardew

优点：
- 概念最纯，长期更接近架构文档理想态
- 未来多游戏接入可能更整齐

缺点：
- 当前证据不足以证明全局抽象已稳定
- 范围扩大，容易把“修复现有 Stardew 常驻智能体”升级成平台重构
- 会延迟对 crash replay 和 body binding 错误的直接修复

### Recommended Option

**推荐 Option A。**

理由：
- 现有代码已经有 controller state、host staged batch、per-NPC worker、pending work item、action slot 这些可用部件。
- 真正缺的是契约闭合，而不是从零开始的能力缺失。
- 用 Option A 可以把“必须补”的三类问题拆成可验证阶段：explicit body binding、cron durable ingress、replay-safe delivery、bridge seq。
- Architect 修订后，Option A 不再表示“把 shared events 复制到每个 NPC inbox”；shared events 保持 host staged once，通过 per-NPC watermark filter 做幂等投递。

## ADR

### Decision

采用**基于现有 runtime controller 的增量修复路线**：先把 NPC body identity 正式并入 runtime/body binding 并贯通 command/query/bridge 路径；再把 cron due 收敛为独立 durable NPC inbox item；随后对 host-staged shared events 增加 per-NPC watermark filtering；最后闭合 bridge `seq/next_seq` 协议与端到端测试。

### Drivers

- 需要先修复现有错误行为，而不是先做平台级重构
- 当前已有 runtime 持久化与 host staged batch 基础，可承载增量改造
- 必须把 crash/replay 正确性作为和 body binding 同等级的一等目标

### Alternatives Considered

- 新建通用 inbox/event bus 层后再迁移 Stardew：被否决，因范围过大且延迟直接修复
- 只修 body binding + cron due，不处理 replay/filter/seq：被否决，因 host replay 下仍会重复消费事件，修复不闭环

### Why Chosen

- 以最小架构扩张覆盖现有三类执行风险：身份错误、side effect 非持久、replay 非幂等
- 与当前测试目录和类型边界高度一致，易于做回归保护

### Consequences

- `PendingWorkItem` 必须继续限定为“已开始准备或已提交的外部动作”；待消费 ingress work 使用独立 inbox item
- host service 会新增 cron inbox append、per-NPC delivery filtering、delivery ack / replay filtering 责任
- bridge 需要补协议字段与测试，避免 app/bridge DTO 继续漂移

### Follow-ups

- 后续如要泛化到非 Stardew 游戏，可在本轮收敛后的 ingress/delivery 契约上抽象
- 完成本轮后再评估是否把 `NpcAutonomyTracker` 从 Stardew host 内提取为通用 worker 组件

## Recommended Execution Phases

### Phase 0: Regression Locks

目标：
- 先把当前错误与期望语义锁进测试，避免执行时“边修边改判定标准”

Touchpoints：
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeRecoveryTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeEventBufferTests.cs`

Acceptance：
- 新增/调整测试明确失败于当前行为：
  - command envelope 不再允许只靠 `NpcId`
  - cron due 不应直接提交 `open_private_chat`
  - staged batch replay 不应让已交付 NPC 再次记录/消费旧事件
  - bridge events poll 必须公开 `seq/next_seq`

### Phase 1: Explicit Body Binding Closure

目标：
- 引入中性 body binding value object，例如 `NpcBodyBinding` / `NpcEntityBinding`。
- 将 `smapiName` / `targetEntityId` 正式从 pack manifest 传入 runtime/body binding。
- 命令、query/status、world snapshot、tool/runtime 统一使用正式 body binding，不再依赖 alias fallback。

Touchpoints：
- `src/runtime/NpcRuntimeDescriptor.cs`
- `src/runtime/NpcRuntimeDescriptorFactory.cs`
- `src/game/core/GameObservation.cs`
- `src/game/core/GameAction.cs`
- `src/game/core/PrivateChatOrchestrator.cs`
- `src/games/stardew/StardewNpcRuntimeBindingResolver.cs`
- `src/games/stardew/StardewGameAdapter.cs`
- `src/games/stardew/StardewQueryService.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewNpcTools.cs`
- Stardew envelope payload/binding helper
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeNpcResolver.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewQueryServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcBindingResolverTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeNpcTargetResolutionRegressionTests.cs`

Acceptance：
- runtime/body binding 可直接暴露 `SmapiName` / `TargetEntityId`。
- command envelope、status query、world snapshot query 的生产路径使用正式 body id。
- `StardewNpcTools` 生成的 `GameAction` 与 private-chat open/reply 路径能够透传正式 body binding。
- bridge command/query 不再依赖 lowercase `npcId` alias 命中真实 NPC。
- 相关测试覆盖 descriptor factory、binding resolver、command envelope、query envelope、tool action、private-chat orchestrator、bridge command queue / resolver。

### Phase 2: Durable Cron Ingress

目标：
- 将 cron due 收敛到 NPC runtime durable ingress。
- 新增或扩展最小 `InboxItem` / `IngressWorkItem`，用于表达“待执行 scheduled private chat”。
- 明确 append-before-side-effect 契约：先写入 NPC runtime durable inbox，再允许 worker 消费并触发 `open_private_chat`。
- 保持 `PendingWorkItem` / `ActionSlot` 语义为“已开始准备或已提交的外部动作”，不把 due inbox 直接伪装成 action slot。

Touchpoints：
- `src/runtime/NpcRuntimeDriver.cs`
- `src/runtime/NpcRuntimeStateStore.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- 新增/扩展 runtime ingress record 类型或 snapshot 字段
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeRecoveryTests.cs`

Acceptance：
- `HandleCronTaskDueAsync(...)` 不再直接 `adapter.Commands.SubmitAsync(GameActionType.OpenPrivateChat)`
- due work 在进程重启后仍可恢复并被 NPC worker 继续处理
- due work 被 worker 消费后，再通过现有 `StardewRuntimeActionController` 写入 `PendingWorkItem` / `ActionSlot` 并提交 `open_private_chat`
- 如果进程在 durable inbox append 成功后、`open_private_chat` submit 前崩溃，重启后该 due work 仍恢复为“待消费 ingress”，不能提前表现为 `PendingWorkItem` / `ActionSlot`

### Phase 3: Replay-Safe Fanout / Delivery Filtering

目标：
- 为每个 NPC 建立可恢复 delivery 边界，防止 host staged batch replay 导致重复消费
- host 必须在 `tracker.EnqueueAsync(...)` 前按每个 NPC 的 persisted `Controller.EventCursor` 生成过滤后的 per-NPC batch
- worker / `NpcAutonomyLoop` 只处理已过滤结果，不能继续拿整批 `sharedEventBatch` 自行裁剪
- shared bridge events 保持 host-staged once，不复制到每个 NPC inbox

Touchpoints：
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcObservationFactStore.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewRuntimeHostStateStore.cs`
- per-NPC delivery watermark / filtered batch helper
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcObservationFactStoreTests.cs`

Acceptance：
- 同一 staged batch 在 crash/replay 后不会让已确认 NPC 重复记录同一 `EventId`
- host 仍保持单次 poll、多 NPC fanout；进入每个 `tracker.EnqueueAsync(...)` 的 batch 已经按该 NPC delivery watermark 过滤
- worker 和 `NpcAutonomyLoop` 的测试必须证明它们收到的是过滤后 batch；loop/fact-store 去重只能作为防线，不能成为主过滤点
- 测试覆盖“部分 NPC 已完成、host 未 commit、重启 replay”场景
- `NpcObservationFactStore` 去重若实现，只作为额外防线，不能替代 fanout filtering 测试

### Phase 4: Bridge `seq/next_seq` Protocol Closure

目标：
- bridge poll API 与 app DTO 对齐，真正公开 `seq` / `next_seq`
- 让 `GameEventCursor.Sequence` 在端到端协议、host 持久化、runtime 恢复中都具备真实语义
- 这是同轮最后阶段；不是 Phase 1-3 的前置 blocker，但不应从本轮拆出。

Touchpoints：
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeEventBuffer.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `src/games/stardew/StardewEventSource.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewEventSourceTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeEventBufferTests.cs`

Acceptance：
- poll request 可接收 `seq`
- event response 每条事件可带 `seq`，批次可带 `next_seq`
- 空批次与非空批次都能正确推进或维持 sequence watermark
- `seq` 是主 cursor：请求同时带 `seq` 和 `since` 时优先按 `seq` 返回大于该序号的事件；`since` 只作为旧客户端兼容 fallback
- 空批次返回的 `next_seq` 必须等于当前 buffer 已知最大序号，不得倒退；非空批次返回最后交付事件的序号
- `since` 找不到且没有 `seq` 时，按旧客户端兼容语义从 buffer 当前可用头部重放，并返回当前批次对应的 `next_seq`；不得静默跳到最新位置
- 测试必须覆盖三种 cursor：`seq` 优先、有效 `since` fallback、失效 `since` 从 buffer 头部重放

## File Touchpoints

核心代码：
- `src/runtime/NpcRuntimeDescriptor.cs`
- `src/runtime/NpcRuntimeDescriptorFactory.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcRuntimeDriver.cs`
- `src/runtime/NpcRuntimeStateStore.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcObservationFactStore.cs`
- `src/game/core/PrivateChatOrchestrator.cs`
- `src/games/stardew/StardewNpcRuntimeBindingResolver.cs`
- `src/games/stardew/StardewQueryService.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewRuntimeHostStateStore.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `src/games/stardew/StardewEventSource.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandQueue.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeEventBuffer.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`

测试：
- `Desktop/HermesDesktop.Tests/Stardew/StardewCommandServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewQueryServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewEventSourceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcBindingResolverTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewPrivateChatOrchestratorTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeRecoveryTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcObservationFactStoreTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeNpcTargetResolutionRegressionTests.cs`
- `Mods/StardewHermesBridge.Tests/BridgeEventBufferTests.cs`

## Test and Acceptance Strategy

### Unit

- descriptor factory / binding resolver 把 `SmapiName`、`TargetEntityId` 传透
- command service 使用正式 body binding 生成 envelope
- query service / private-chat / Stardew tool actions 使用正式 body binding，不靠 alias fallback
- bridge event models round-trip `seq/next_seq`
- runtime driver/state store 能恢复 ingress/pending/action/delivery watermark

### Integration

- cron due 触发后先 durable append，再由 worker 消费并触发 private chat/open command
- host 单次 poll + 多 NPC fanout 仍成立
- staged batch replay 时，已确认 NPC 不重复记录同一事件

### Recovery

- host 在 staged batch 已写入但 source cursor 未 commit 时崩溃，重启后：
  - 未消费的 ingress work 仍在
  - 已确认 delivery watermark 的 NPC 不重复消费
  - 未确认 NPC 仍能继续处理
- cron due 在 durable inbox append 后、submit 前崩溃，重启后：
  - due item 仍处于待消费 ingress 状态
  - `PendingWorkItem` / `ActionSlot` 尚未被错误恢复
  - worker 再次消费时才创建 action work item 并提交 `open_private_chat`

### Observability

- snapshot / log 能区分：
  - ingress depth
  - pending work item
  - action slot
  - event cursor / delivery watermark
  - pause reason / retry wake

## Available Agent Types

- `planner`
- `architect`
- `critic`
- `executor`
- `debugger`
- `test-engineer`
- `verifier`
- `explore`

## Staffing Guidance

### Ralph Path

- 建议单 owner 顺序执行，reasoning 级别 `high`
- 推荐 lane：
  - `executor`: Phase 0-4 主实现
  - `test-engineer`: 测试增补与 recovery case 设计
  - `verifier`: 末尾验证与 crash/replay 场景复核

### Team Path

- 建议 3 lanes：
  1. `executor`：Phase 1 identity binding + Phase 4 bridge protocol
  2. `executor` / `debugger`：Phase 2 durable ingress + Phase 3 replay-safe delivery
  3. `test-engineer`：全程先行补测试，末尾整体验证
- reasoning 建议：
  - identity/protocol lane：`medium`
  - ingress/replay lane：`high`
  - testing/verification lane：`medium`

### Launch Hints

- `ralph`：
  - `$ralph 执行 .omx/plans/星露谷NPC常驻智能体修复前缺口扫描共识计划.md，按 Phase 0 -> 4 顺序实施并逐阶段验证`
- `team`：
  - `$team 基于 .omx/plans/星露谷NPC常驻智能体修复前缺口扫描共识计划.md 分 3 lanes 执行：identity/protocol、ingress/replay、tests/verification`

### Team Verification Path

- Phase 0 完成后先看新增测试是否能稳定暴露当前缺口
- Phase 1 后验证 command/binding 测试全部通过
- Phase 2-3 后重点验证 recovery / replay / cron durable ingress 场景
- Phase 4 后跑 Stardew + bridge 相关测试全集，再由 `verifier` 做一次按场景复述式验收

## Open Risks

- 如果执行时把 due inbox 偷懒复用成 `PendingWorkItem`，语义会继续混杂；该风险必须通过 Phase 2 测试阻断
- replay-safe 设计若只在 fact store 去重而不在 delivery 层去重，会掩盖而非修复根因
- bridge `seq/next_seq` 若只补 DTO、不补 buffer/poll 语义，仍然是假闭环

## Does This Plan Capture Your Intent?

- `proceed` - 进入执行 handoff，给出推荐的 `ralph` 或 `team` 启动方式
- `adjust [X]` - 按指定阶段、缺口判断或测试策略修改计划
- `restart` - 丢弃本计划并重新规划
