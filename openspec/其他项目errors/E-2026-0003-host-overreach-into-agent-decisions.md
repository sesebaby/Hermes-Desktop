# E-2026-0003-host-overreach-into-agent-decisions

- id: E-2026-0003
- title: 宿主层越界替 Agent 决策或写内容
- status: active
- updated_at: 2026-04-22
- keywords: [host-overreach, decision-thin, content-thin, script-forcing, agent-autonomy, hermes-home, shadow-brain]
- trigger_scope: [design, spec, implementation, review]

## Symptoms

- `host / bridge / router / orchestrator` 开始输出 `forced_action`、`required_reply`、`dialogue_template_id`、脚本步骤或预写内容。
- 群聊、私聊、送礼、交易等场景被宿主写成固定流程，而不是事实输入加 capability bound。
- 名义上说是 Agent 自主，实际上宿主已经替 NPC 规定了必做动作或必说内容。
- 运行时热路径里由 `host / runtime / adapter` 直接生成或覆盖 `HERMES_HOME/SOUL.md`，把宿主变成 `profile assembler`。
- 运行时热路径里由宿主把自由文本 summary 或 placeholder 写进 `memories/MEMORY.md` / `USER.md`，把宿主变成 Hermes 主记忆入口。
- 运行时热路径里即使不回写 Hermes 原生文件，也由宿主额外维护 `identity snapshot`、`memory summary`、`audit memory` 等 agent 语义副本，并在桥接决策中依赖这些副本。
- 测试把“宿主物化 `SOUL.md/MEMORY.md/USER.md`”或“第二轮对话依赖 host summary 延续记忆”当成正确契约。

## Root Cause

- 决策边界没有冻结，导致宿主从事实层膨胀成厚决策层或厚内容层。
- 没有把 `decision-thin / content-thin` 写成 contract 和 review 检查项。
- 没有把允许约束 Agent 的唯一手段限制为 prompt、skill、MCP/tool contract。
- 没有把 `HERMES_HOME/SOUL.md` 与 `memories/*` 明确冻结为 Hermes 原生权威入口，导致实现层把“挂载既有物料”滑成了“运行时生成物料”。
- 没有把“宿主审计文件必须与 Hermes 原生人格/记忆文件严格分离”写成测试契约，导致 shadow brain 被单测固化。
- 更根本地，没有把“桥接层不得维护任何 agent 语义副本”冻结成硬约束，导致实现者继续把 `memory summary` / `identity snapshot` 当作可接受的中间过渡层。

## Bad Fix Paths

- 为了“先跑通效果”，在宿主层加探测器、脚本编排器、强制调度器。
- 让 router 不只分发窗口和可见性，还顺手生成台词或安排剧情顺序。
- 让 host payload 带 directive 字段，再由 runtime 被动执行。
- 让 RuntimeHost/adapter 在聊天前偷写 `SOUL.md`、在聊天后偷写 `MEMORY.md/USER.md`，并把它说成“临时审计”或“占位 continuity”。
- 不再碰 Hermes 原生文件后，又把 host-owned `MEMORY_SUMMARY.md` / `NPC_IDENTITY.md` 保留在 agent 热路径里，并继续让这些文件承载人格或记忆语义。
- 只把 HTTP 入口改成 reject-only，却不拆默认对象图和测试里仍在背书的宿主私有人格/记忆生成语义。

## Corrective Constraints

- `host / bridge / router` 只允许提供事实、事件、工具、确认、执行结果，不允许替 Agent 决策或写内容。
- 所有 directive-like payload 字段必须被显式禁止，并有稳定拒绝语义。
- review 必须检查是否存在脚本强控、硬编码话术、预写内容、强制动作流。
- RuntimeHost 只允许绑定、挂载、存在性校验和审计 approved per-NPC `HERMES_HOME`，不得在运行时生成、覆盖或补写 `SOUL.md`。
- 桥接层不得在 agent 热路径维护任何人格/记忆语义副本。即使是独立文件，只要它承载“NPC 身份真相”或“长期记忆语义”，也属于越权。
- 如确需宿主侧运维/追踪，只能记录纯技术审计信息：请求 id、时间戳、错误码、工具调用结果、桥接输入输出元数据；不得记录可回流为 agent 语义的身份总结、记忆总结、人格摘要。
- 测试必须显式保护“宿主不改写 Hermes 原生人格/记忆文件”，不得再用 host-generated `SOUL.md/MEMORY.md/USER.md` 或 summary-based continuity 作为 canonical success-path。

## Verification Evidence

- `specs/game-host-bridge/spec.md` 中存在 `decision-thin`、`fact-only payload`、禁止 directive 字段、稳定失败语义要求。
- `specs/social-scene-routing/spec.md` 中存在 `router MUST remain content-thin`、固定窗口规则、主动权来自 Agent。
- `proposal.md` 与 `design.md` 中存在“宿主只做身体，不做编剧”“禁止 detector / 脚本编排器 / 行为强制器”。
- `2026-04-20` 修复中，`IdentitySnapshotWriter` 已收口为仅写 `identity/NPC_IDENTITY.md`，不再生成或覆盖 `profile/hermes-home/SOUL.md`。
- `2026-04-20` 修复中，`MemorySnapshotStore` 已收口为仅写 `memory/MEMORY_SUMMARY.md` 审计材料，不再镜像写入 `profile/hermes-home/memories/MEMORY.md` / `USER.md`。
- `2026-04-20` 修复中，`PrivateChatService` 已改为 fail-closed 校验已挂载的 `HERMES_HOME/SOUL.md`，不再在热路径补写人格/记忆文件。
- `2026-04-20` 验证中，`tests/RuntimeHost.Tests/PrivateChatServiceTests.cs` 与 `tests/RuntimeHost.Tests/NpcStateRepositoryTests.cs` 已改为保护“宿主不改写 Hermes 原生人格/记忆文件”；`dotnet test tests/RuntimeHost.Tests/RuntimeHost.Tests.csproj --no-restore` 通过（39/39）。
- `2026-04-20` 追加教训：上述收口仍不够，因为用户明确拒绝桥接层在热路径维护任何 agent 语义副本；后续整改必须继续移除 `memory summary` / `identity snapshot` 之类宿主语义层，而不是把它们保留为“独立审计”。
- `2026-04-20` 再次修复中，`openspec/changes/p01-di-yi-jie-duan-ke-shi-shi-ban/design.md` 已移除 `runtime/PersonaPacks`、identity snapshot 与 `enabled_mcp_toolsets` 第二 lane 口径，并把 `artifacts/` 明确收窄为纯技术审计目录。
- `2026-04-20` 再次修复中，`verification/8/手动验证清单.md` 已明确区分“当前 reject-only smoke check”和“未来 `8.7 / MVP-1` 真闭环 gate”，不再把未接通成功链路伪装成可立即通过的游戏手测。
- `2026-04-21` 再次追加：仅把 legacy `/v1/private-chat` 做成 reject-only 仍不够；任何直打 RuntimeHost 私聊 listener 的其他 POST 路径也必须明确归类成 forbidden direct ingress，并以独立 `reason_code` 暴露到 `GET /v1/status.last_reason_code`，不能继续伪装成“只是当前不可用”。
- `2026-04-21` 再次追加：`/v1/social-output` 上的 directive-like payload 拒绝不能只靠顶层字段枚举；任意未声明扩展字段，包括嵌套在 `payload` 或 `delivery_target` 下的字段，都必须按宿主越权处理并稳定拒绝。
- `2026-04-22` 再次追加：`/v1/social-output` 的 malformed body 也不能再落成 `HOST_BRIDGE_NOT_READY`；缺字段、类型错、非对象等所有反序列化失败都必须稳定归类到 `HOST_DIRECTIVE_PAYLOAD_FORBIDDEN`，否则 review 仍会把第二脑入口误读成“暂时不可用”。
- `2026-04-22` 再次追加：如果 `group_chat / proactive_approach / overhear` 只存在测试 callsite，phase-1 social breadth 仍属于伪完成；至少要有真实 production emitter 通过 canonical host bridge 排队。
- `2026-04-22` 再次追加：即便宿主不再改写 Hermes 原生文件，只要私有 runtime 继续手工读取并拼接 `SOUL.md / MEMORY.md / USER.md` 进 query，也仍然属于 `prompt builder` 残留；必须改成只透传本轮动态事实，让 Hermes 原生 `HERMES_HOME` 装配链负责长期人格/记忆注入。

## Related Files

- openspec/changes/hermescraft-stardew-replica-runtime/proposal.md
- openspec/changes/hermescraft-stardew-replica-runtime/design.md
- openspec/changes/hermescraft-stardew-replica-runtime/specs/game-host-bridge/spec.md
- openspec/changes/hermescraft-stardew-replica-runtime/specs/social-scene-routing/spec.md
- openspec/changes/p01-di-yi-jie-duan-ke-shi-shi-ban/proposal.md
- openspec/changes/p01-di-yi-jie-duan-ke-shi-shi-ban/design.md
- openspec/changes/p01-di-yi-jie-duan-ke-shi-shi-ban/tasks.md
- apps/HermesRuntimeHost/Chat/PrivateChatService.cs
- adapters/HermesAgentAdapter/Persona/IdentitySnapshotWriter.cs
- adapters/HermesAgentAdapter/Memory/MemorySnapshotStore.cs
- tests/RuntimeHost.Tests/PrivateChatServiceTests.cs
- tests/RuntimeHost.Tests/NpcStateRepositoryTests.cs

## Notes

- 关联通用卡：`semantic-hardcode-and-dual-track`、`implicit-coupling-without-contract`。
- 本仓库特化点：这里的越界不是普通 helper 过厚，而是宿主层直接侵入 Agent 自主决策链。
- `2026-04-20` 这次命中的特化形态是：HTTP 入口已经 reject-only，但默认对象图、热路径实现和单测契约仍在偷偷维持 shadow brain；说明“入口退役”本身不能代替“默认语义退役”。
- `2026-04-20` 第二次追加命中：仅把宿主摘要从 Hermes 原生文件移开仍然不够，因为桥接层的职责不是“另存一份 agent 语义”，而是“完全不拥有 agent 语义”。
- `2026-04-21` 第三次追加命中：如果 direct ingress 的失败分类仍然是 `HOST_BRIDGE_NOT_READY` 这类弱语义，review 仍会把它判成“入口只是暂时没开”，而不是“第二脑入口被明令禁止”；这说明失败 taxonomy 本身也是单路径治理的一部分。
