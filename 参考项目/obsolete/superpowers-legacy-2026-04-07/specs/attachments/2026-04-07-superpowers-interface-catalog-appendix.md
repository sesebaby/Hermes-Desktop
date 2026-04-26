# Superpowers 接口总表附件

## 1. 文档定位

这份表是正式接口总目录。  
目的不是讲概念，而是回答 3 个问题：

1. 哪一层对哪一层开放了什么接口
2. 这个接口做什么
3. 这个接口回链哪份正式合同

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/contracts/product/launcher-game-settings-surface-contract.md`
- `docs/superpowers/contracts/runtime/runtime-local-endpoint-catalog-contract.md`
- `docs/superpowers/contracts/runtime/hosted-narrative-endpoint-contract.md`
- `docs/superpowers/contracts/runtime/stardew-npc-panel-bundle-contract.md`
- `docs/superpowers/contracts/runtime/stardew-phone-contact-list-contract.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-current-implementation-divergence-appendix.md`

## 2. 7 层接口地图

### 2.1 Cloud

#### Cloud internal narrative endpoints

1. `POST /narrative/{gameId}/private-dialogue/candidate`
   - 调用方：
     - `Runtime.Local`
   - 作用：
     - 吃事实包
     - 云端编排 prompt
     - 调 provider
     - 回结构化 candidate
   - 正式合同：
     - `cloud-orchestration-fact-package-contract`
     - `cloud-prompt-assembly-contract`
     - `hosted-narrative-endpoint-contract`
2. `POST /narrative/{gameId}/private-dialogue/candidate-stream`
   - 调用方：
     - `Runtime.Local`
   - 作用：
     - 以 streaming / pseudo-streaming 方式回玩家可见文本增量
   - 正式合同：
     - `hosted-narrative-endpoint-contract`
3. `POST /narrative/{gameId}/private-dialogue/pending`
   - 调用方：
     - `Runtime.Local`
   - 作用：
     - 建立 `pending_visible`
4. `POST /narrative/{gameId}/private-dialogue/{canonicalRecordId}/finalize`
   - 调用方：
     - `Runtime.Local`
   - 作用：
     - 升 committed / render_failed
5. `POST /narrative/{gameId}/private-dialogue/{canonicalRecordId}/recover`
   - 调用方：
     - `Runtime.Local`
   - 作用：
     - 恢复 `pending_visible`
6. `GET /narrative/{gameId}/memory/{actorId}/snapshot`
   - 调用方：
     - `Runtime.Local`
   - 作用：
     - 读 canonical memory snapshot

### 2.2 Launcher

#### 桌面产品前台接口面

当前已明确必须存在的接口面：

1. `account session`
2. `game workspace`
3. `mod package install/update/rollback`
4. `support ticket and diagnostic bundle`
5. `notification feed`
6. `product catalog`
7. `entitlement visibility`
8. `purchase handoff`
9. `redeem`
10. `game settings`

固定回链：

1. `launcher-auth-session-contract`
2. `stardew-launcher-workspace-ia`
3. `stardew-mod-package-install-update-rollback-contract`
4. `support-ticket-and-diagnostic-bundle-contract`
5. `launcher-notification-feed-contract`
6. `launcher-product-catalog-visibility-contract`
7. `player-entitlement-visibility-contract`
8. `purchase-handoff-state-machine`
9. `redeem-request-and-receipt-contract`
10. `launcher-game-settings-surface-contract`

### 2.3 Launcher.Supervisor

#### 本地运行管家接口面

1. `readiness evaluate`
2. `runtime local start`
3. `runtime local stop`
4. `repair / update / recheck`
5. `health collect`

固定规则：

- Supervisor 给单一 readiness truth
- Launcher 只消费，不重算

### 2.4 Runtime.Local

#### 正式 runtime endpoints

1. `POST /runtime/{gameId}/private-dialogue`
   - 调用方：
     - `Game Mod`
   - 合同：
     - `private-dialogue-request-contract`
2. `POST /runtime/{gameId}/private-dialogue/stream`
   - 调用方：
     - `Game Mod`
   - 合同：
     - `runtime-local-endpoint-catalog-contract`
3. `POST /runtime/{gameId}/private-dialogue/{canonicalRecordId}/finalize`
   - 调用方：
     - `Game Mod`
   - 合同：
     - `private-dialogue-commit-state-machine-contract`
4. `POST /runtime/{gameId}/private-dialogue/{canonicalRecordId}/recover`
   - 调用方：
     - `Game Mod`
5. `POST /runtime/{gameId}/remote-direct`
   - 调用方：
     - `Game Mod`
   - 合同：
     - `remote-direct-request-contract`
6. `POST /runtime/{gameId}/group-chat-turn`
   - 调用方：
     - `Game Mod`
   - 合同：
     - `group-chat-turn-request-contract`
7. `POST /runtime/{gameId}/thought-preview`
   - 调用方：
     - `Game Mod`
   - 合同：
     - `thought-request-contract`
8. `POST /runtime/{gameId}/npc-panel-bundle`
   - 调用方：
     - `Game Mod`
   - 合同：
     - `stardew-npc-panel-bundle-contract`
9. `POST /runtime/{gameId}/phone-contacts`
   - 调用方：
     - `Game Mod`
   - 合同：
     - `stardew-phone-contact-list-contract`
10. `GET /runtime/{gameId}/health`
   - 调用方：
     - `Launcher.Supervisor`

### 2.5 Runtime.<game> Adapter

#### title-local adapter 接口面

它不是 HTTP 面，而是进程内正式接口面。

最少必须存在：

1. `BuildFactPackage`
2. `TranslateCandidateToHostApplyPlan`
3. `ResolveTitleLocalBlockedReason`
4. `ResolveSurfacePlan`
5. `ResolveFinalizeMapping`

固定说明：

1. 这里统一叫 `HostApplyPlan`
2. 不再使用 `ExecutionPlan`
3. 不再使用 `hostMutationPlan`

### 2.6 Game Mod

#### 宿主桥接接口面

最少必须存在：

1. `HostFactCapture`
2. `HostDialogueSurface`
3. `NpcPanelSurface`
4. `PhoneDirectThreadSurface`
5. `GroupChatSurface`
6. `ThoughtSurface`
7. `ItemCreator`
8. `GiftTradeExecutor`
9. `FinalizeReporter`
10. `HostEvidenceReporter`
11. `PhoneContactBookSurface`

这些接口面的细类职责统一回链：

- `2026-04-07-superpowers-stardew-mod-rebuild-and-implementation-appendix.md`

## 3. 能力到接口的绑定表

### 3.1 Private Dialogue

1. `Mod -> Runtime.Local`
   - `POST /runtime/{gameId}/private-dialogue`
2. `Mod -> Runtime.Local`
   - `POST /runtime/{gameId}/private-dialogue/stream`
   - 用于玩家可见文字的 streaming / pseudo-streaming 快反馈
3. `Runtime.Local -> Cloud`
   - `POST /narrative/{gameId}/private-dialogue/candidate`
   - `POST /narrative/{gameId}/private-dialogue/candidate-stream`
   - `POST /narrative/{gameId}/private-dialogue/pending`
4. `Mod -> Runtime.Local`
   - `POST /runtime/{gameId}/private-dialogue/{canonicalRecordId}/finalize`
5. `Runtime.Local -> Cloud`
   - `POST /narrative/{gameId}/private-dialogue/{canonicalRecordId}/finalize`

### 3.2 Remote Direct

1. `Mod -> Runtime.Local`
   - `POST /runtime/{gameId}/remote-direct`
2. 历史正本：
   - `Cloud canonical private/direct history`

### 3.3 Group Chat

1. `Mod -> Runtime.Local`
   - `POST /runtime/{gameId}/group-chat-turn`
2. 历史正本：
   - `Cloud canonical group history`
3. 镜像：
   - participant private history projection

### 3.4 Thought Preview

1. `Mod -> Runtime.Local`
   - `POST /runtime/{gameId}/thought-preview`
2. `Runtime.Local -> Cloud`
   - 走 private-dialogue 的 `inner_monologue` 编排分支

### 3.5 Npc Panel Bundle

1. `Mod -> Runtime.Local`
   - `POST /runtime/{gameId}/npc-panel-bundle`
2. bundle 合同：
   - `stardew-npc-panel-bundle-contract`

### 3.6 Phone Contact List

1. `Mod -> Runtime.Local`
   - `POST /runtime/{gameId}/phone-contacts`
2. 列表合同：
   - `stardew-phone-contact-list-contract`

## 4. 旧接口退役表

以下接口不再是正式主线：

1. `/runtime/stardew/private-dialogue`
2. `/runtime/stardew/private-dialogue/{canonicalRecordId}/finalize`
3. `/runtime/remote-direct`
4. `/runtime/group-chat`
5. `/runtime/thought`
6. `/narrative/private-dialogue/provider-candidate`
7. `/narrative/private-dialogue`
8. `/narrative/private-dialogue/{canonicalRecordId}/finalize`
9. `/narrative/private-dialogue/{canonicalRecordId}/recover`
10. `/narrative/internal/memory/{actorId}/snapshot`

## 5. 使用规则

以后任何人要补接口、改接口、退接口，统一先改这份总表，再改代码。
