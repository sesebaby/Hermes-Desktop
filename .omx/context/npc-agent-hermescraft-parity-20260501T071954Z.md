## Task Statement

按 `external/hermescraft-main` 的核心方案修复当前 Desktop agent / NPC agent 不同源装配的问题，目标是让 NPC 真正成为“标准 Hermes agent + 自己的身份隔离”，而不是一条单独拼装的缩水运行时。

## Desired Outcome

- Desktop agent 和 NPC agent 共享同一条能力装配主链。
- 允许差异仅限于：persona、session、namespace、memory directory。
- 常驻 NPC runtime、private-chat、autonomy/debug 都走同一个共享 contract。
- Penny/Hailey 不再有实现级特权或兜底分支。
- 方案显式参考 HermesCraft，但不照搬“一 NPC 一进程”；本仓库继续保持“一个 Desktop 进程内托管多个 NPC runtime”。

## Known Facts / Evidence

### 已确认的问题

1. 常驻 `NpcRuntimeHost` 路径没有真正装配 NPC agent，只注册 instance 并建目录。
   - `src/runtime/NpcRuntimeHost.cs`
   - `src/runtime/NpcRuntimeSupervisor.cs`
   - `src/runtime/NpcRuntimeInstance.cs`

2. NPC prompt 仍有代码级专用补丁，不只靠 persona / session 区分。
   - `src/runtime/NpcRuntimeContextFactory.cs`
   - `src/games/stardew/StardewPrivateChatOrchestrator.cs`
   - `Desktop/HermesDesktop/App.xaml.cs`

3. `haley` 仍是共享解析层的默认兜底。
   - `src/games/stardew/StardewNpcRuntimeBindingResolver.cs`
   - `src/games/stardew/StardewAutonomyTickDebugService.cs`

4. Desktop 与 NPC 的 discovered tools 接入时机不同。
   - Desktop: 先 built-ins，后 `InitializeMcpAsync`
   - NPC: 构造临时 agent 时一次性快照 `McpManager.Tools.Values`

5. Penny 的私聊状态机问题基本已修：
   - wildcard NPC 允许空闲状态下任意 NPC 开会话
   - `_activeNpcId` 固定当前会话 NPC，防止其他 NPC 抢占
   - 证据：`src/game/core/PrivateChatContracts.cs`、`src/game/core/PrivateChatOrchestrator.cs`

### 当前仓库已存在的有利基础

1. 共享 capability 装配器已经出现：
   - `src/runtime/AgentCapabilityAssembler.cs`

2. Desktop prompt builder 已切到共享入口：
   - `Desktop/HermesDesktop/App.xaml.cs`

3. NPC private-chat / autonomy debug 已部分切到共享入口：
   - `src/games/stardew/StardewPrivateChatOrchestrator.cs`
   - `src/games/stardew/StardewAutonomyTickDebugService.cs`

4. pack provenance / persona seeding 基础已出现：
   - `src/runtime/NpcRuntimeDescriptorFactory.cs`
   - `src/runtime/NpcNamespace.cs`
   - `src/games/stardew/StardewNpcRuntimeBindingResolver.cs`

### HermesCraft 参考证据

1. HermesCraft 明确强调：不是单独的假 NPC runtime，而是“正常 Hermes agent + 自己的 home / memory / session / SOUL / 标准工具栈”。
   - `external/hermescraft-main/README.md:24-32`

2. Civilization mode 强调：同一个系统从单 companion 扩展到多 agent 社会，关键是每角色独立 home / memory / prompt。
   - `external/hermescraft-main/docs/CIVILIZATION_MODE.md:3-16`

3. `civilization.sh` 为每个 agent 建独立 `HERMES_HOME`、`memories`、`sessions`、`SOUL.md`，复制 config 并启用 memory/user profile。
   - `external/hermescraft-main/civilization.sh:5-9`
   - `external/hermescraft-main/civilization.sh:197-223`

4. `start-steve.sh` / `landfolk.sh` 也是同样思路：独立 home + 独立 SOUL + 共享 Hermes 能力面。
   - `external/hermescraft-main/start-steve.sh:17-28`

### 本仓库既有设计约束

1. 参考 HermesCraft 的是“同一核心系统 + 多角色独立身份隔离”，不是机械复制“一 NPC 一进程”。
   - `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md:66`

2. prompt 必须继续绑定现有 `ContextManager` / `PromptBuilder` 链路，禁止新造独立 `StardewPromptAssembler`。
   - `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md:68`

3. `npcId` 规范化应统一经过 catalog / manifest，而不是各层硬编码名串。
   - `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md:79`

4. autonomy 应贴近 HermesCraft 的长会话 agent 模式，不能退化成“有事件才临时问一次 AI”。
   - `docs/superpowers/specs/2026-04-27-星露谷多NPC村庄设计方案.md:82`

## Constraints

- 不回退用户已有脏改动。
- 明确忽略无关脏文件：
  - `Desktop/HermesDesktop.Tests/Services/TranscriptStoreTests.cs`
  - `src/transcript/TranscriptStore.cs`
  - `openspec/project.md`
- 不新增第二套 Stardew prompt assembler 或第二套 bridge 访问路径。
- 不把方案改成“一 NPC 一进程”；仍然是 Desktop 进程内托管多个 NPC runtime。
- 目标不是保留旧“NPC safe subset”原则，而是消除能力面分叉。

## Unknowns / Open Questions

1. 常驻 `NpcRuntimeHost` 在当前产品路径里实际是否已经被调用，还是仍处于半成品接线阶段？
2. 共享 contract 最小重构边界应该落在 `AgentCapabilityAssembler` 扩展，还是新增更高一层 runtime builder？
3. discovered tools 的“统一接入时机”应该收敛成懒加载 provider、快照 builder，还是 agent 生命周期事件订阅？
4. private-chat / autonomy 的“场景差异”哪些应该下沉到 persona pack，哪些保留在 session 输入？

## Likely Codebase Touchpoints

- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- `src/runtime/AgentCapabilityAssembler.cs`
- `src/runtime/NpcAgentFactory.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/runtime/NpcRuntimeHost.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcRuntimeInstance.cs`
- `src/runtime/NpcNamespace.cs`
- `src/runtime/NpcRuntimeDescriptorFactory.cs`
- `src/games/stardew/StardewNpcRuntimeBindingResolver.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/games/stardew/StardewAutonomyTickDebugService.cs`
- `src/game/core/PrivateChatContracts.cs`
- `src/game/core/PrivateChatOrchestrator.cs`
- `Desktop/HermesDesktop.Tests/Runtime/*`
- `Desktop/HermesDesktop.Tests/Stardew/*`
