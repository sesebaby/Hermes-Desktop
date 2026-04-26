# Superpowers 当前代码退役与第一批重建附件

## 1. 文档定位

本文只回答 4 件事：

1. 现在仓库里哪些代码必须退役
2. 哪些代码只能保留为壳
3. 哪些代码已经是可继续复用的 authority core
4. 第一批大改应该先从哪里下刀

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-runtime-module-interface-and-commit-contract-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-reference-unit-to-repo-landing-map-appendix.md`
- `docs/superpowers/contracts/product/narrative-base-pack-contract.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`

大白话总规则：

1. 只要代码还在本地拼最终 prompt、持有游戏 prompt 明文、或把 provider 输入语义定死在本地，它就是旧业务主线，必须退役。
2. 只要代码主要负责宿主 UI、宿主 hook、桌面页面、启动检查、deterministic gate、trace、health、recovery，它就优先保留。
3. 只要代码既像壳又偷偷长业务 authority，就按“壳保留、业务重写”处理。

## 2. 当前仓库的硬结论

当前必须先承认这几个现实：

1. `src/Superpowers.Runtime.Stardew/PromptAssets/StardewValley/*` 仍然在本地工程里。
2. `src/Superpowers.Runtime.Stardew/Adapter/StardewPromptAssetCatalog.cs` 仍然在本地加载 prompt 资产。
3. `src/Superpowers.Runtime.Stardew/Adapter/StardewPrivateDialoguePromptBuilder.cs` 仍然在本地拼完整 prompt。
4. `src/Superpowers.Runtime.Stardew/Adapter/StardewMemoryCompressionPromptBuilder.cs` 仍然在本地拼记忆压缩 prompt。
5. `src/Superpowers.Runtime.Contracts/Narrative/PrivateDialogueProviderContracts.cs` 仍然把完整 prompt payload 当成正式上游输入。
6. `src/Superpowers.Runtime.Local/Endpoints/*` 里多条主线仍是“本地 build prompt -> 发云端”。

所以：

1. 现在这套历史实现不能再叫“待修正式主线”。
2. 它们只能被当成：
   - `待退役旧实现`
   - 或 `可拆解参考`

## 3. 分层处置表

| 层 | 路径 / 文件 | 当前处置 | 原因 | 第一批动作 |
| --- | --- | --- | --- | --- |
| `Launcher` | `src/Superpowers.Launcher/*` | `see launcher retirement appendix` | 桌面程序的逐文件保留壳、断电点、重建顺序已经单独立表，不再在 repo 总表里重复登记第二套 authority | 回链 `2026-04-07-superpowers-launcher-current-code-retirement-and-rebuild-appendix.md` |
| `Launcher.Supervisor` | `src/Superpowers.Launcher.Supervisor/*` | `see launcher retirement appendix` | `readiness / runtime status / package / diagnostics` 的逐文件判词只认 Launcher 专表 | 回链 `2026-04-07-superpowers-launcher-current-code-retirement-and-rebuild-appendix.md` |
| `Runtime.Contracts` | `src/Superpowers.Runtime.Contracts/Narrative/PrivateDialogueProviderContracts.cs` | `rebuild immediately` | 还把完整 prompt payload 当成正式 request，编排权残留在本地 | 第一刀先改 |
| `Runtime.Contracts` | `src/Superpowers.Runtime.Contracts/Narrative/HostedNarrativeContracts.cs` | `kept contract shell` | hosted create/finalize 方向对 | 保留并重挂 |
| `Runtime.Contracts` | `src/Superpowers.Runtime.Contracts/Responses/CommittedOutcomeEnvelope.cs` | `kept contract core` | 属于 committed 合同，不依赖本地 prompt 主线 | 保留 |
| `Runtime.Contracts` | `src/Superpowers.Runtime.Contracts/Determinism/AcceptedDeterministicOutcome.cs` | `kept contract core` | 属于 deterministic 结果合同 | 保留 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Narrative/HostedNarrativeGateway.cs` | `kept authority shell` | `Runtime.Local -> Cloud` 通信骨架方向正确 | 保留，但上游 request 重写 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Determinism/DeterministicValidator.cs` | `kept authority core` | 本地 gate owner 方向正确 | 保留 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Normalization/ActionRepairNormalizer.cs` | `kept authority core` | 适合继续做事实包修复和结果归一 | 保留 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/History/LocalProjectionStore.cs` | `kept support shell` | 本地投影存储仍有价值 | 保留 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Persistence/LocalRuntimeStateRepository.cs` | `kept support shell` | 本地 runtime 状态壳仍可用 | 保留 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Items/LocalItemEventStore.cs` | `kept support shell` | 物品事件本地证据壳可用 | 保留 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Items/LocalCarrierEvidenceStore.cs` | `kept support shell` | carrier 证据壳可用 | 保留 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Items/LocalItemGiftWritebackReceiptStore.cs` | `kept support shell` | host writeback receipt 壳可用 | 保留 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Endpoints/PrivateDialogueEndpoint.cs` | `rewrite immediately` | 仍走本地 build prompt | 第二刀重写 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Endpoints/RemoteDirectEndpoint.cs` | `rewrite immediately` | 仍走本地 build prompt | 第二刀重写 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs` | `rewrite immediately` | 仍走本地 build prompt | 第二刀重写 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Endpoints/ThoughtEndpoint.cs` | `rewrite immediately` | 仍走本地 build prompt | 第二刀重写 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local/Endpoints/MemorySummaryEndpoint.cs` | `rewrite soon` | 记忆主线已改成 Cloud 编排，这里不能继续保留本地编排口子 | 第三刀重写 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Adapter/StardewSnapshotBuilder.cs` | `kept adapter core` | 负责事实冻结，方向对 | 保留 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Adapter/StardewHostSummaryBuilder.cs` | `kept adapter core` | 负责宿主摘要整理，方向对 | 保留 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Adapter/StardewRelationSnapshotBuilder.cs` | `kept adapter core` | 负责关系事实整理，方向对 | 保留 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Adapter/StardewHistoryJoinKeyFactory.cs` | `kept adapter core` | 属于 join key 翻译，不是 prompt 主线 | 保留 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Contracts/PrivateDialogueRequest.cs` | `kept contract shell` | 请求语义位点可继续保留 | 保留，但字段边界按事实包收紧 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Contracts/RemoteDirectRequest.cs` | `kept contract shell` | 同上 | 保留，但字段边界按事实包收紧 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Contracts/GroupChatTurnRequest.cs` | `kept contract shell` | 同上 | 保留，但字段边界按事实包收紧 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Contracts/ThoughtRequest.cs` | `kept contract shell` | 同上 | 保留，但字段边界按事实包收紧 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Adapter/StardewPromptAssetCatalog.cs` | `retired implementation` | 本地 prompt 资产目录 owner，和 Cloud 真源冲突 | 退役 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Adapter/StardewPrivateDialoguePromptBuilder.cs` | `retired implementation` | 本地最终 prompt builder | 退役 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Adapter/StardewMemoryCompressionPromptBuilder.cs` | `retired implementation` | 本地记忆压缩 prompt builder | 退役 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/Adapter/StardewMemoryAssetCompressor.cs` | `retired implementation` | 本地记忆语义压缩主线口子仍在 | 退役 |
| `Runtime.Stardew Adapter` | `src/Superpowers.Runtime.Stardew/PromptAssets/StardewValley/*` | `retired implementation` | 游戏 prompt 明文不应继续在本地工程中打包 | 退役 |
| `Game Mod` | `games/stardew-valley/Superpowers.Stardew.Mod/*` | `see stardew retirement appendix` | `Mod` 逐文件退役、断电 grep、替代新类、UI/hook/transport 壳保留口径已经单独立表，不再在 repo 总表里复制第二套 | 回链 `2026-04-07-superpowers-stardew-current-code-retirement-and-cutover-appendix.md` |

## 4. 第一批必须先做的 5 刀

### 4.1 第一刀：先砍合同

先改：

- `src/Superpowers.Runtime.Contracts/Narrative/PrivateDialogueProviderContracts.cs`

固定改法：

1. 不再让 `PrivateDialogueProviderRequest` 携带完整 `Prompt`。
2. 改成只携带结构化事实包、title-local hint、trace ref、gate 输入 ref。
3. 最终 prompt 只能由 `Cloud` 基于事实包自己编排。

原因：

1. 不先砍这层，下面所有 endpoint 都会继续沿用旧主线。

### 4.2 第二刀：重写 Runtime.Local 的 4 个入口

先改：

- `src/Superpowers.Runtime.Local/Endpoints/PrivateDialogueEndpoint.cs`
- `src/Superpowers.Runtime.Local/Endpoints/RemoteDirectEndpoint.cs`
- `src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs`
- `src/Superpowers.Runtime.Local/Endpoints/ThoughtEndpoint.cs`

固定改法：

1. 从“本地 build prompt -> 发云端”改成“本地组事实包 -> 发云端”。
2. 本地只做 deterministic gate、schema 校验、repair、trace、commit 仲裁。

### 4.3 第三刀：把 Stardew prompt 资产从本地正式工程剥掉

先退役：

- `src/Superpowers.Runtime.Stardew/Adapter/StardewPromptAssetCatalog.cs`
- `src/Superpowers.Runtime.Stardew/Adapter/StardewPrivateDialoguePromptBuilder.cs`
- `src/Superpowers.Runtime.Stardew/Adapter/StardewMemoryCompressionPromptBuilder.cs`
- `src/Superpowers.Runtime.Stardew/Adapter/StardewMemoryAssetCompressor.cs`
- `src/Superpowers.Runtime.Stardew/PromptAssets/StardewValley/*`

固定去向：

1. 游戏 prompt 明文正本迁到 `Cloud`。
2. 本地只保留 request DTO、snapshot builder、summary builder、join key factory。

### 4.4 第四刀：重挂 Mod 的 transport，不动宿主 UI 壳

保留：

- `games/stardew-valley/Superpowers.Stardew.Mod/UI/*`
- `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/*`
- `games/stardew-valley/Superpowers.Stardew.Mod/Runtime/IRuntimeSurfaceClient.cs`

固定规则：

1. UI/hook/transport 继续保留。
2. 但它们以后只能传事实、收结构化结果、做最终宿主写回。
3. 不允许再把 prompt 语义、本地 AI 语义、本地 provider 语义挂回 Mod。
4. `RuntimeClient.cs` 只允许保留 transport 壳，当前里面那些请求组装、UI 填充、finalize、事务协同逻辑必须拆回 `Runtime.Local` 和 adapter。
5. `ManualTestEntryController.cs` 只允许留在开发态，不再进入正式主线设计。

### 4.5 第五刀：Launcher / Supervisor 先稳住，不先乱拆

细节归口：

1. `Launcher / Supervisor` 的逐文件保留壳、断电点、桥接口、服务拆分，只认：
   - `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-current-code-retirement-and-rebuild-appendix.md`
   - `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-interface-and-class-landing-appendix.md`
   - `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-bridge-and-dto-contract-appendix.md`

原因：

1. `Supervisor` 这块不是当前污染主线的地方。
2. 桌面壳、Supervisor facade、产品桥接已经有单独正式附件，不再在这里重复抄第二遍。
3. 当前更应该防止把 Runtime 的旧业务语义继续往桌面端迁。
4. `LauncherShellViewModel` 仍可留页面壳，但它的真相来源和启动执行方式必须按 Launcher 专表重写。

## 5. 施工顺序

当前正式施工顺序固定为：

1. 改 `Runtime.Contracts`
2. 改 `Runtime.Local` 四大入口
3. 退役 `Runtime.Stardew` 本地 prompt 旧主线
4. 重挂 `Cloud` prompt 资产与编排
5. 重挂 `Mod RuntimeClient`
6. 最后再补 `Launcher / Supervisor` 的页面数据接线

固定不允许：

1. 先去美化 Launcher，再把 Runtime 旧主线继续拖着。
2. 先加新功能，再让本地 prompt 主线继续活着。
3. 让 `Cloud` 和本地同时各存一份游戏 prompt 正文。

## 6. 进入正式计划前必须补齐的东西

后续真正写 `plan / tasks` 时，每一项都必须补：

1. 参考分析文档路径
2. 参考 prompt 路径
3. 参考源码文件
4. 参考源码行号
5. 当前仓库目标文件
6. 当前仓库待退役文件
7. 验证证据怎么留

固定规则：

1. 这份附件先把“仓库里该砍什么”钉死。
2. 真正进入实施计划时，必须再把“参考 mod 源码行号”补全。
