# Superpowers `U1-U12` 到仓库落点总表附件

## 1. 文档定位

本文只回答：

1. `U1-U12` 在当前仓库里到底落到哪里
2. 哪些现有目录可复用
3. 哪些旧代码必须退役
4. 以后写 `plan / tasks` 时，必须补什么证据

固定回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-reference-code-unit-reconstruction-and-afw-migration-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-runtime-module-interface-and-commit-contract-appendix.md`

## 2. 正式落点表

| 单元 | 正式目标目录 | 当前可复用代码 | 必须退役的旧代码 | 说明 | 进入 plan 时必须补 |
| --- | --- | --- | --- | --- | --- |
| `U1` prompt 资产合同 | `src/Superpowers.CloudControl/PromptAssets/` | `src/Superpowers.CloudControl/Content/ContentWorkspaceStore.cs` 可作内容壳 | `src/Superpowers.Runtime.Stardew/Adapter/StardewPromptAssetCatalog.cs`、`src/Superpowers.Runtime.Stardew/PromptAssets/StardewValley/*` | prompt 明文正本固定进 Cloud | 参考分析文档、参考 prompt 文件、参考源码行号 |
| `U2` Cloud 编排主线 | `src/Superpowers.CloudControl/Narrative/`、`src/Superpowers.CloudControl/Providers/` | `HostedNarrativeOrchestrator.cs`、`GovernedPrivateDialogueProviderClient.cs`、`DashScopeCompatibleChatClient.cs` | 所有本地最终 prompt 组装逻辑 | 云端负责 prompt 编排与 provider 通信 | 同上 |
| `U3` canonical chat 主档 | `src/Superpowers.CloudControl/History/`、`src/Superpowers.Runtime.Contracts/Narrative/` | `CanonicalHistoryStore.cs`、`HostedNarrativeContracts.cs` | 本地另起聊天正本的实现 | 正本固定在 Cloud，本地只保投影 | 同上 |
| `U4` 记忆压缩 | `src/Superpowers.CloudControl/Memory/`、`src/Superpowers.CloudControl/Providers/` | `CanonicalMemoryStore.cs`、`IMemoryCompressionProviderClient.cs`、`MemoryCompressionResponseNormalizer.cs` | `StardewMemoryCompressionPromptBuilder.cs` | 记忆正本和压缩编排固定进 Cloud | 同上 |
| `U5` 行为协议 / repair / gate | `src/Superpowers.Runtime.Local/Endpoints/`、`src/Superpowers.Runtime.Local/Normalization/`、`src/Superpowers.Runtime.Contracts/Narrative/` | `PrivateDialogueEndpoint.cs`、`ItemGiftEndpoint.cs`、`ActionRepairNormalizer.cs` | 把 gate 做进 Mod 或 Cloud 的旧思路 | deterministic gate 固定在 Runtime.Local | 同上 |
| `U6` 群聊 / 远程多人 | `src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs`、`src/Superpowers.Runtime.Stardew/Contracts/GroupChatTurnRequest.cs`、`games/stardew-valley/Superpowers.Stardew.Mod/UI/OnsiteGroupChatOverlay.cs`、`PhoneActiveGroupChatMenu.cs` | 现有群聊 endpoint、contract、UI 壳可复用 | 本地 prompt 路由与本地群聊语义主线 | 当前只保 implementation-only，不进当前 exit gate | 同上 |
| `U7` 传播协议 | `src/Superpowers.CloudControl/Propagation/`、`src/Superpowers.Runtime.Local/Propagation/`、`src/Superpowers.Runtime.Stardew/Propagation/` | 当前没有正式目录，只能复用现有 `HostedNarrativeContracts` 的 sidecar 习惯 | 任何把传播直接写成普通文本效果的旧实现 | 现在先定正式目录，不走生产主链 | 同上 |
| `U8` 世界事件 | `src/Superpowers.CloudControl/World/`、`src/Superpowers.Runtime.Local/World/`、`src/Superpowers.Runtime.Stardew/World/`、`games/stardew-valley/Superpowers.Stardew.Mod/World/` | 当前只能复用 `WorldLifecycleHooks.cs` 这类宿主壳 | 任何把世界事件直接塞进本地 prompt builder 的旧实现 | 世界推演建议在 Cloud，最终创建在宿主 | 同上 |
| `U9` 对象生成 | `src/Superpowers.CloudControl/ObjectGeneration/`、`src/Superpowers.Runtime.Local/ObjectGeneration/`、`src/Superpowers.Runtime.Stardew/ObjectGeneration/` | 当前只能复用 item carrier / hook 壳 | 任何绕过 support matrix 直接建对象的旧实现 | 生成默认挂世界链，不走私聊直建 | 同上 |
| `U10` 宿主接入 | `src/Superpowers.Runtime.Stardew/`、`games/stardew-valley/Superpowers.Stardew.Mod/` | `Contracts/*`、`Hooks/*`、`UI/*`、`Runtime/RuntimeClient.cs` | 本地 prompt 主线 | 这里只保事实冻结、surface、hook、最终写回 | 同上 |
| `U11` Launcher / Supervisor | `src/Superpowers.Launcher/`、`src/Superpowers.Launcher.Supervisor/` | `LauncherShellViewModel.cs`、`StardewGameConfigViewModel.cs`、`SupportViewModel.cs`、`Readiness/*` | 任何桌面二套 readiness / entitlement 真相 | 这是我们自己的产品前台，不从参考 mod 搬 | 同上 |
| `U12` Agent NPC | 第三阶段单独新目录 | 当前无正式可复用生产代码 | 当前全部 | 当前阶段禁止进生产路径 | 单独立项时再补 |

## 3. 当前最重要的退役动作

当前必须最先判死刑的是：

1. 本地 prompt 资产目录
2. 本地最终 prompt builder
3. 本地记忆压缩 prompt builder
4. 任何“本地直连 provider”的旧主线

原因：

1. 它们和 `Cloud` 真源直接打架。
2. 你现在还在开发阶段，不值得为它们背兼容债。

## 4. 进入 `plan / tasks` 的硬要求

每个 `U` 任务到时候必须补齐：

1. 参考分析文档路径
2. 参考 prompt 路径
3. 参考源码文件
4. 参考源码行号
5. 当前仓库目标目录
6. 当前仓库具体类 / 文件
7. 必须退役的旧文件
8. 验证证据怎么留

固定规则：

1. 不允许只写“参考某功能”。
2. 不允许只写“改在 Cloud”。
3. 必须写到文件和目录级别。
