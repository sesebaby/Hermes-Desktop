# Superpowers 当前实现偏差表附件

## 1. 文档定位

这份表只做一件事：  
把“正式设计要求”和“当前代码现实”一条条对上，然后给出明确处置。

不允许出现：

- `以后再看`
- `先这样也行`
- `边做边想`

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-architecture-gap-and-blueprint-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-interface-catalog-appendix.md`
- `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
- `docs/superpowers/contracts/runtime/cloud-prompt-assembly-contract.md`
- `docs/superpowers/contracts/runtime/runtime-local-vs-title-adapter-boundary-contract.md`

## 2. 总体结论

当前仓库最大的问题，不是“功能不够多”，而是：

1. 正式设计已经改了
2. 代码主链还在按旧思路跑
3. 某些 UI 壳和值得保留
4. 但业务 authority 还没从旧代码里彻底断电

死结论：

- 旧业务主链按 `retired business mainline` 处理
- 保留 UI 壳、hook 壳、显示壳
- 新主线只允许按正式合同重建

## 3. 偏差总表

### 3.1 Cloud

#### D-CLD-001 Cloud 还不是事实包编排 owner

- 正式要求：
  - `Cloud` 吃结构化事实包
  - `Cloud` 自己组最终 prompt
  - `Cloud` 自己调 provider
- 当前代码现实：
  - `src/Superpowers.CloudControl/Narrative/HostedNarrativeController.cs`
  - `src/Superpowers.CloudControl/Narrative/HostedNarrativeOrchestrator.cs`
  - 更像 `provider forwarder`
- 处置：
  - 保留 `provider dispatch` 壳
  - 退役“只收本地 prompt payload”的主线
  - 按：
    - `cloud-orchestration-fact-package-contract`
    - `cloud-prompt-assembly-contract`
    重建

#### D-CLD-002 Cloud endpoint 命名还带旧过渡味道

- 正式要求：
  - endpoint 按能力和 `{gameId}` 统一命名
- 当前代码现实：
  - `/narrative/private-dialogue/provider-candidate`
  - `/narrative/private-dialogue`
  - `/narrative/internal/memory/...`
- 处置：
  - 统一迁到：
    - `/narrative/{gameId}/private-dialogue/candidate`
    - `/narrative/{gameId}/private-dialogue/pending`
    - `/narrative/{gameId}/memory/{actorId}/snapshot`

### 3.2 Runtime.Local

#### D-RTL-001 Runtime.Local 仍在本地拼 prompt

- 正式要求：
  - 本地只发结构化事实包
- 当前代码现实：
  - `src/Superpowers.Runtime.Local/Endpoints/PrivateDialogueEndpoint.cs`
  - `src/Superpowers.Runtime.Local/Endpoints/RemoteDirectEndpoint.cs`
  - `src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs`
  - `src/Superpowers.Runtime.Local/Endpoints/ThoughtEndpoint.cs`
  - 仍直接调 `StardewPrivateDialoguePromptBuilder`
- 处置：
  - 业务主链退役
  - 按 `fact package -> cloud candidate -> local gate -> finalize` 重建

#### D-RTL-002 Runtime.Local 和 Runtime.Stardew 边界已经糊了

- 正式要求：
  - `Runtime.Local` 只管共享门禁
  - `Runtime.Stardew` 只管 title-local 翻译
- 当前代码现实：
  - `Runtime.Local` endpoint 直接知道 Stardew prompt、Stardew title-local 文本
- 处置：
  - `Runtime.Local` 只保留共享 envelope、gate、finalize
  - `Runtime.Stardew` 只保留 mapping / plan translate

#### D-RTL-003 Runtime.Local endpoint 路径不统一

- 正式要求：
  - 全部统一到 `/runtime/{gameId}/...`
- 当前代码现实：
  - 有的带游戏名
  - 有的不带
- 处置：
  - 旧路径退役
  - 新路径按 `runtime-local-endpoint-catalog-contract`

### 3.3 Runtime.Stardew

#### D-RSD-001 本地 prompt 资产和 catalog 还在正式工程里通电

- 正式要求：
  - prompt 资产明文只在云端
- 当前代码现实：
  - `src/Superpowers.Runtime.Stardew/Adapter/StardewPromptAssetCatalog.cs`
  - `src/Superpowers.Runtime.Stardew/PromptAssets/StardewValley`
- 处置：
  - 标记为 `retired implementation`
  - 迁出正式成功链

#### D-RSD-002 DTO 已有，但上位合同之前缺失

- 正式要求：
  - 请求字段要先被正式合同冻结
- 当前代码现实：
  - DTO 先长出来
- 处置：
  - 现在已补齐：
    - `private-dialogue-request-contract`
    - `remote-direct-request-contract`
    - `group-chat-turn-request-contract`
    - `thought-request-contract`
    - `stardew-npc-panel-bundle-contract`
    - `stardew-phone-contact-list-contract`
  - 后续 DTO 只能跟合同对齐，不准自己长语义

### 3.4 Stardew Mod

#### D-MOD-001 私聊、手机私信、群聊、物品链都还混着写

- 正式要求：
  - 参考 mod 的主链语义要拆成明确类职责
- 当前代码现实：
  - 一个入口常常同时做：
    - 触发
    - 取数
    - 请求
    - UI
    - finalize
- 处置：
  - 按：
    - `2026-04-07-superpowers-stardew-mod-rebuild-and-implementation-appendix.md`
    - `2026-04-07-superpowers-stardew-feature-reference-implementation-appendix.md`
    重建

#### D-MOD-002 thought / phone / group 还偏测试壳

- 正式要求：
  - 线程、session、状态机要成正式系统
- 当前代码现实：
  - 还偏“能打开个菜单”
- 处置：
  - UI 壳可保留
  - session owner、thread owner、unread/DND owner 按合同重建

### 3.5 Launcher / Supervisor

#### D-LCH-001 Launcher 页面有了，但产品服务层不完整

- 正式要求：
  - 桌面程序要承担账号、登录、mod 下载、前置检测、反馈等完整产品职责
- 当前代码现实：
  - 页面和 ViewModel 比较多
  - 正式产品服务合同已经补到设计层，但代码实现还没按这层彻底落地
- 处置：
  - 当前已补：
    - `launcher-auth-session-contract`
    - `stardew-mod-package-install-update-rollback-contract`
    - `support-ticket-and-diagnostic-bundle-contract`
    - `launcher-notification-feed-contract`
    - `launcher-product-catalog-visibility-contract`
    - `player-entitlement-visibility-contract`
    - `purchase-handoff-state-machine`
    - `redeem-request-and-receipt-contract`
    - `launcher-game-settings-surface-contract`
  - 下一步重点：
    - 按这些合同把页面壳、service、bridge 真正接回单一路径

#### D-SUP-001 Supervisor 和 Launcher 前台边界还不够硬

- 正式要求：
  - Supervisor 只给单一 readiness / recovery truth
  - Launcher 只消费，不重算
- 当前代码现实：
  - 仍有页面直接读本地事实并自行解释的风险
- 处置：
  - 已补 `launcher-supervisor-boundary-contract`
  - 下一步按合同把页面壳和 Supervisor 真正接回单一 readiness 主线

## 4. 处置优先级

### 第一优先级

1. 断掉本地 prompt 主链
2. 让 `Cloud` 真正变成事实包编排 owner
3. 把 `Runtime.Local` 和 `Runtime.<game>` 边界收死

### 第二优先级

1. 重建 `Stardew Mod` 的宿主执行链
2. 把手机私信、群聊、物品链按正式 session / creator / executor 拆开

### 第三优先级

1. 按已补好的桌面前台产品服务合同落实现有页面
2. 收 `Launcher / Supervisor` 的单一真相

## 5. 使用规则

以后只要说：

- `当前实现哪里偏了`
- `这段代码该保留还是该断电`
- `新实现应该接到哪条正式主线`

统一先查这张表。
