# Superpowers 模块清单、接口目录与提交合同附件

## 1. 文档定位

本文专门补当前总设计里最缺的 3 块：

1. 当前仓库模块清单
2. canonical 接口 / DTO / 事件目录
3. `candidate -> committed` 提交合同

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/contracts/runtime/deterministic-command-event-contract.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`
- `docs/superpowers/contracts/runtime/narrative-degradation-contract.md`

## 2. 当前仓库模块清单

| 层 | 当前工程 / 路径 | 模块职责 | 当前状态 |
| --- | --- | --- | --- |
| `Cloud` | `src/Superpowers.CloudControl` | 游戏 prompt 资产、prompt 编排、provider 通信、聊天/记忆/明文审计正本、产品 access / entitlement / claim 真相 | 已有工程，设计继续收口 |
| `Launcher` | `src/Superpowers.Launcher` | 注册登录、游戏工作区、产品与兑换、通知、支持与帮助、设置 | 已有工程，缺完整模块清单 |
| `Launcher.Supervisor` | `src/Superpowers.Launcher.Supervisor` | 启动前检查、前置框架检测、Runtime.Local 进程管理、readiness verdict、本地修复/更新 | 已有工程，缺 verdict 输入表 |
| `Runtime.Contracts` | `src/Superpowers.Runtime.Contracts` | canonical DTO、command/event contract、trace/audit/degradation contract | 已有工程，缺统一接口索引目录 |
| `Runtime.Local` | `src/Superpowers.Runtime.Local` | 统一入口检查、deterministic gate、结果修补、commit 仲裁、trace/health/recovery | 已有工程，缺 commit promotion 明细 |
| `Runtime.<game> Adapter` | `src/Superpowers.Runtime.Stardew` | title-local 事实冻结、字段映射、执行清单翻译、support matrix 裁决 | 目前只有 `Stardew`，其他游戏待新增 |
| `Game Mod` | `games/stardew-valley/Superpowers.Stardew.Mod` | 宿主取数、宿主 UI、最终宿主写回、宿主证据回传 | 已有工程，缺宿主接入总地图 |
| `Tests` | `tests/Superpowers.Runtime.Tests`、`tests/Superpowers.Launcher.Tests`、`tests/Superpowers.Stardew.Mod.Tests` | 验证 canonical contract、启动路径、宿主接入行为 | 已有工程，缺按能力验收表 |

固定规则：

1. 后续任何 `U1-U12` 任务，必须落到上表某个工程和目录。
2. 不允许只写逻辑层名，不写仓库工程路径。
3. 如果某项能力跨多个工程，必须显式写：
   - 主 owner
   - 配合 owner

## 2A. 历史代码处置分桶

当前仓库不是“所有历史代码继续修”，而是按下面这张表硬切：

| 路径 / 模块 | 当前处置 | 原因 | 新正式替代 |
| --- | --- | --- | --- |
| `src/Superpowers.Runtime.Stardew/Adapter/StardewPromptAssetCatalog.cs` | `retired business mainline` | 本地持有 prompt 资产目录，和 Cloud prompt 真源冲突 | `Cloud` 游戏级 prompt asset store |
| `src/Superpowers.Runtime.Stardew/Adapter/StardewPrivateDialoguePromptBuilder.cs` | `retired business mainline` | 本地拼最终 prompt，和“本地只发结构化事实包”冲突 | `Cloud` prompt orchestration |
| `src/Superpowers.Runtime.Stardew/Adapter/StardewMemoryCompressionPromptBuilder.cs` | `retired business mainline` | 本地拼记忆压缩 prompt，和 Cloud memory 编排冲突 | `Cloud` memory pipeline |
| `src/Superpowers.Runtime.Stardew/PromptAssets/StardewValley/*` | `retired business mainline` | prompt 资产明文不应继续由本地正式主链持有 | `Cloud` per-game prompt assets |
| `games/stardew-valley/Superpowers.Stardew.Mod/UI/*` | `kept carrier shell` | 宿主 UI 壳仍可复用 | 继续作为宿主可见面 |
| `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/*` | `kept carrier shell` | semantic hook 和宿主挂点仍可复用 | 继续作为宿主 hook 壳 |
| `games/stardew-valley/Superpowers.Stardew.Mod/Runtime/RuntimeClient.cs` | `kept carrier shell` | 仍可作为 Mod -> Runtime transport 壳 | 只保留 transport 与 finalize 调用 |
| `src/Superpowers.Launcher.Supervisor/Readiness/*` | `kept authority core` | 单一 readiness truth 已符合现行设计 | 继续作为 readiness 主骨架 |
| `src/Superpowers.CloudControl/History/CanonicalHistoryStore.cs` | `kept authority core` | canonical chat / replay 正本方向正确 | 继续作为 Cloud 正本骨架 |
| `src/Superpowers.CloudControl/Memory/CanonicalMemoryStore.cs` | `kept authority core` | canonical memory 正本方向正确 | 继续作为 Cloud 记忆骨架 |

固定规则：

1. 被标成 `retired business mainline` 的模块，不再允许出现在正式主链说明里当“待修实现”。
2. `kept carrier shell` 只保留壳，不再拥有业务 authority。
3. `kept authority core` 才允许继续长成正式主链。

## 3. canonical 接口 / DTO / 事件目录

当前正式目录固定为：

| 合同对象 | 唯一 owner | 主要产生方 | 主要消费方 | 说明 |
| --- | --- | --- | --- | --- |
| `FactPackage` | `Runtime.<game> Adapter` | `Game Mod` + `Runtime.<game> Adapter` | `Runtime.Local` | 宿主真实事实冻结后的统一事实包 |
| `CloudCandidateEnvelope` | `Cloud` | `Cloud` | `Runtime.Local` | Cloud 返回的结构化候选结果；状态初始为 `未正式生效` |
| `HostApplyPlan` | `Runtime.<game> Adapter` | `Runtime.Local` + `Runtime.<game> Adapter` | `Game Mod` | title-local 宿主执行清单 |
| `HostExecutionReceipt` | `Game Mod` | `Game Mod` | `Runtime.Local` | 宿主执行结果与宿主证据回执 |
| `CommitPromotionAck` | `Runtime.Local` | `Runtime.Local` | `Cloud` | 允许 Cloud 把候选升级成正式正本的唯一确认 |
| `LaunchReadinessVerdict` | `Launcher.Supervisor` | `Launcher.Supervisor` | `Launcher` | 玩家可见 readiness 唯一真相 |
| `DegradationRecord` | `Runtime.Local` | `Runtime.Local` | `Launcher.Supervisor` / `Launcher` | 降级与恢复状态唯一记录 |
| `TraceAuditRecordRef` | 分层 owner | `Cloud` / `Launcher.Supervisor` / `Runtime.Local` / `Game Mod` | review / evidence / support | 各层审计记录引用，不允许互相冒充 |

### 3.1 当前已存在的正式合同类

当前仓库里已经存在、并且能直接回链到这套合同的类包括：

1. `src/Superpowers.Launcher.Supervisor/Readiness/LaunchReadinessVerdict.cs`
2. `src/Superpowers.Runtime.Stardew/Contracts/PrivateDialogueRequest.cs`
3. `src/Superpowers.Runtime.Stardew/Contracts/RemoteDirectRequest.cs`
4. `src/Superpowers.Runtime.Stardew/Contracts/GroupChatTurnRequest.cs`
5. `src/Superpowers.Runtime.Stardew/Contracts/ThoughtRequest.cs`
6. `src/Superpowers.Runtime.Stardew/Contracts/HostSummaryEnvelope.cs`
7. `src/Superpowers.Runtime.Contracts/Narrative/PrivateDialogueProviderContracts.cs`
8. `src/Superpowers.Runtime.Contracts/Narrative/HostedNarrativeContracts.cs`

固定规则：

1. 能直接复用的合同类继续复用。
2. 但若这些类背后仍然依赖本地 prompt builder / 本地 prompt catalog，则只复用合同，不复用旧编排实现。

### 3.2 Readiness Verdict 输入表

`LaunchReadinessVerdict` 的最小 authoritative 输入固定为：

| 输入对象 | 当前类 / 路径 | owner |
| --- | --- | --- |
| `policySnapshotRef` | `src/Superpowers.Launcher.Supervisor/Readiness/LaunchReadinessPolicySnapshot.cs` | `Launcher.Supervisor` |
| `capabilityAccessDecisionRef` | `src/Superpowers.Launcher.Supervisor/Readiness/CapabilityAccessDecision.cs` | `Launcher.Supervisor` |
| `claimStateRef` | `sku-entitlement-claim-matrix` 等产品 contract 回链 | `Cloud` 提供，`Launcher.Supervisor` 消费 |
| `runtimePreflightRef` | `src/Superpowers.Launcher.Supervisor/State/RuntimePreflightFact.cs` | `Launcher.Supervisor` |
| `runtimeHealthRef` | `src/Superpowers.Launcher.Supervisor/State/RuntimeHealthFact.cs` | `Launcher.Supervisor` |
| `recoveryEntryRef` | `src/Superpowers.Launcher.Supervisor/State/RecoveryEntryRef.cs` | `Launcher.Supervisor` |

固定规则：

1. `Launcher` 只消费 verdict，不自己补算第二套真相。
2. `Runtime.Local` 只能提供运行事实，不得越权生成玩家可见 verdict。

固定规则：

1. 以上对象是后续接口目录的最小正式集合。
2. JSON 名字可以改，但语义位点不允许删。
3. `Runtime.Contracts` 后续必须把这组对象落成真正的目录和类型索引。

## 4. `candidate -> committed` 提交合同

### 4.1 单一 owner

固定 owner：

1. `CloudCandidateEnvelope` owner = `Cloud`
2. `HostExecutionReceipt` owner = `Game Mod`
3. `runtime outcome 仲裁 owner` = `Runtime.Local`
4. `canonical chat / memory / prompt plaintext audit owner` = `Cloud`
5. `host writeback / player-visible record owner` = `Game Mod`

### 4.2 固定时序

1. `Runtime.<game> Adapter` 生成 `FactPackage`
2. `Runtime.Local` 通过入口检查后发给 `Cloud`
3. `Cloud` 生成 `CloudCandidateEnvelope`
4. 若当前是纯文本玩家可见面，`Runtime.Local` 可以先建立：
   - `pending_visible`
   让前台先显示角色已经开始回答
5. `Runtime.Local` 做 deterministic gate
6. `Runtime.<game> Adapter` 生成 `HostApplyPlan`
7. `Game Mod` 执行并回传 `HostExecutionReceipt`
8. `Runtime.Local` 读取回执并做最终仲裁
9. 只有 `Runtime.Local` 发出 `CommitPromotionAck` 后，`Cloud` 才能把候选升级成正式正本

### 4.2A 快反馈与后台确认

固定规则：

1. `快反馈` 只解决：
   - 玩家何时先看到角色开始回答
2. `后台确认` 才解决：
   - 这轮是否真的 committed
   - 是否能进正式历史 / 正式记忆
   - 是否有宿主状态变更
3. `pending_visible` 允许玩家先看到文本，但不允许让玩家误以为：
   - 物品已经发放
   - 关系已经变化
   - 事件已经落地
4. 为了提速，允许把：
   - 首次可见文本显示
   放在审计、记忆、commit promotion 之前
5. 为了保密，不允许把：
   - 云端已编排好的最终 prompt 正文
   - 记忆选取正文
   - 规则链正文
   下发到客户端换速度

### 4.3 固定不允许

1. `Cloud` 不得自己把候选直接升级成 committed
2. `Game Mod` 不得越过 `Runtime.Local` 直接通知 `Cloud` committed
3. 候选超时、执行失败、宿主失败，都只能停留在：
   - `未正式生效`
   - 或 `failed / rejected`
4. 不允许本地失败但云端正式历史已经写成“成功发生过”
5. 不允许为了前台更快，把会改宿主状态的动作先演出来再补 committed

### 4.4 幂等与重试

固定 join key：

1. `candidateId`
2. `commandId`
3. `hostReceiptId`
4. `narrativeTurnId`

固定规则：

1. 同一 `hostReceiptId` 不得重复提升 committed
2. 重试必须复用上一轮 `candidateId + commandId`，不能偷偷开第二条成功路径
3. 超时后是否重试，由 `Runtime.Local` 决定；`Cloud` 不能自己猜测重试结果

## 5. 审计真相拆分

这块必须拆开，不允许再笼统写成“审计都在 Cloud”。

| 审计类型 | 唯一 owner | 正本落点 |
| --- | --- | --- |
| prompt / chat / memory 明文审计 | `Cloud` | `Cloud` |
| launch / readiness 操作审计 | `Launcher.Supervisor` | `Launcher.Supervisor` |
| deterministic command / degradation / commit 审计 | `Runtime.Local` | `Runtime.Local` |
| host writeback / player-visible surface 审计 | `Game Mod` | `Game Mod` |

固定规则：

1. `Cloud` 持有的是明文 canonical audit，不等于全系统所有操作审计都归 Cloud。
2. review、support、evidence 回链时，必须先判断是哪一类审计。
3. 不允许把 `Game Mod` 的 host writeback 记录写成 `Cloud` 自己的宿主成功证明。

## 6. review 必查点

1. 每个 `U` 单元是否已经落到仓库工程路径
2. 是否已经产出真正的 DTO / contract 目录
3. `Runtime.Local` 是否仍是提交仲裁唯一 owner
4. `Cloud` 是否仍只在收到 `CommitPromotionAck` 后升级 committed
5. 审计类型是否仍保持分层 owner，不再混成“一切都在 Cloud”
