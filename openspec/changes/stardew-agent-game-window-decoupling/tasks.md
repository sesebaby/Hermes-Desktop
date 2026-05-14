## 1. 建立事实与回归测试

- [x] 1.1 在修改桌面测试或运行时代码前，重新阅读 `Desktop/HermesDesktop/AGENTS.md` 和要求的桌面 instruction 文件。
- [x] 1.2 重新阅读相关 `openspec/errors` 记录，重点覆盖：私聊移动、隐藏 `local_executor` fallback、回复动作链 guard，以及把 runtime context 暴露成 AI 字段的历史错误。
- [x] 1.3 为 `StardewNpcAutonomyBackgroundServiceTests` 增加一个会先失败的回归测试，证明私聊 host task 在等待回复 UI 生命周期时，即使超过通用 stale / busy defer 次数预算，也仍然应该保持可恢复。
- [x] 1.4 增加或更新一个回归测试，证明通用 action-slot / pending-work busy defer 在达到配置预算后，仍然会正确阻塞。
- [x] 1.5 增加一个会先失败的测试，证明 `stardew_task_status` 在保留 `status` 和 `commandId` 的同时，也会返回 agent 可读的 `summary`。
- [x] 1.6 为 `blocked/action_slot_busy` 增加会先失败的 harness 覆盖：冲突世界动作不能再创建第二个排队世界动作项，也不能在当前动作结束后自动重试。
- [x] 1.7 在修改 prompt 资产前，先增加或更新真实资产 prompt / skill 边界测试；断言指引必须拒绝 `local_executor`、隐藏的 host 代推断下一步动作，以及宽泛的隐藏锁。
- [x] 1.8 在测试中定义本次范围内的状态 / 结果覆盖矩阵：可恢复 UI 等待、`action_slot_busy`、`host_task_submission_deferred_exceeded`、timeout、lease / menu 冲突；在断言前先从现有代码映射 timeout 和 lease / menu reason code。

## 2. 运行时分类修复

- [x] 2.1 在 `StardewNpcAutonomyBackgroundService` 中，把私聊回复 UI 生命周期等待和通用 stale / busy ingress defer 分开处理。
- [x] 2.2 对等待中的私聊 host task submission，保持其为 queued 或其他可恢复状态，并记录诊断日志与下次唤醒调度；不能再累计或阻塞在通用 defer 预算上。
- [x] 2.3 对 malformed payload、unsupported action、硬阻塞，以及真正的通用 busy / stale ingress 循环，继续保留终态阻塞行为。
- [x] 2.4 验证修复没有引入第二套 host task 队列，没有允许身体动作并行，也没有让新的冲突动作替换当前 action slot。

## 3. 文本优先的工具结果摘要

- [x] 3.1 只增加 Stardew 动作 / 状态工具结果真正需要的最小 summary shaping helper；在多个具体调用点真实需要前，不要抽成过宽的抽象。
- [x] 3.2 更新 `stardew_task_status`，让其在 running、waiting、completed、blocked、failed、cancelled、timeout 和 unknown 状态下都返回简洁 `summary`。
- [x] 3.3 为代表性的范围内动作结果补 RED 测试和实现，覆盖：queued 的 host task submission、blocked 的 `action_slot_busy`，以及一个 completed 或其他终态动作 / 状态路径上的 `summary`。
- [x] 3.4 确保 `summary` 文本只陈述事实、不带指挥性：可以解释状态和原因，但不能替 agent 选择下一步动作。

## 4. Prompt 与 Skill 指引

- [x] 4.1 先跑 1.7 中的 prompt / skill 边界 RED 测试，确认在编辑资产前，它们会因为缺少“非阻塞边界”指引而失败。
- [x] 4.2 更新 `skills/gaming/stardew-core/SKILL.md`，明确写出窗口、菜单、动画、事件都是游戏事实 / 状态，而不是 agent 流程锁。
- [x] 4.3 更新 `skills/gaming/stardew-task-continuity/SKILL.md`，要求已知长时或等待任务统一通过 `stardew_task_status` 续接，并把终态 blocked / failed 事实视为 agent 自己的决策点。
- [x] 4.4 更新 `skills/system/stardew-npc-runtime/SYSTEM.md`，让 runtime prompt 保持 host 只提供事实，并由 agent 自己拥有下一步决策权。
- [x] 4.5 重跑 prompt / skill 边界测试，确认真实仓库资产已经包含“非阻塞状态边界”指引，且不存在 host 代推断下一步动作的语言。

## 5. 验证与错误记忆

- [x] 5.1 先运行新的 RED 测试，确认它们会因目标行为缺失而按预期失败。
- [x] 5.2 在运行时修复后，运行定向 `StardewNpcAutonomyBackgroundServiceTests`。证据：`dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests" -p:UseSharedCompilation=false` 通过 60/60，覆盖 slot busy 时的 durable reply delivery、slot busy 时 reply 未送达、reply 送达后的可重试 bridge failure，以及旧的 UI-wait attempt budget 用例。
- [x] 5.3 在 summary 改动后，运行定向 `StardewNpcToolFactoryTests` 或等价工具结果测试。证据：`dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcToolFactoryTests" -p:UseSharedCompilation=false` 通过 39/39。
- [x] 5.4 使用 `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew" -p:UseSharedCompilation=false` 运行更宽的 Stardew 测试覆盖。证据：通过 275/275，其中 2 个 live-AI 测试被跳过。
- [x] 5.5 如果改动触及 bridge 代码，运行 `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug -p:UseSharedCompilation=false`。证据：通过 140/140，保留一个既有的 `BridgeMovementPathProbe.cs` obsolete warning。
- [x] 5.6 更新或新增一条 `openspec/errors` 记录，明确沉淀“可恢复的游戏 UI / 窗口生命周期等待不能复用通用 agent / ingress 重试预算”这个教训。
- [x] 5.7 运行 `openspec status --change "stardew-agent-game-window-decoupling"`，并在实现交接前确认全部产物完整。证据：4/4 artifacts complete。
- [x] 5.8 运行 `openspec validate "stardew-agent-game-window-decoupling" --strict`，并在实现交接前修复所有 OpenSpec 校验失败。证据：change is valid。
