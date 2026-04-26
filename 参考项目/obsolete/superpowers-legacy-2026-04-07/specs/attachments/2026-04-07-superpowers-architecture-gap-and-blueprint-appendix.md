# Superpowers 架构缺口与施工蓝图附件

## 1. 文档定位

本文不是再讲一次愿景。  
本文只做 3 件事：

1. 站在系统架构师视角，说明当前正式设计和当前代码实现之间还缺哪些硬东西。
2. 把已经发现的结构性冲突直接点出来，不再模糊说“后续优化”。
3. 给出接下来必须补齐的正式蓝图、契约、状态机和实施顺序。

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/governance/current-phase-boundary.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-current-code-retirement-and-rebuild-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-runtime-module-interface-and-commit-contract-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-interface-catalog-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-feature-reference-implementation-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`
- `docs/superpowers/contracts/product/launcher-game-settings-surface-contract.md`
- `docs/superpowers/contracts/runtime/stardew-npc-panel-bundle-contract.md`
- `docs/superpowers/contracts/runtime/stardew-phone-contact-list-contract.md`

## 2. 本次审查输入

本次结论不是只靠一个视角拍脑袋。

输入来源固定包括：

1. 正式设计主文与正式附件。
2. `contracts / governance / profiles` 当前正式文档。
3. 当前正式代码实现。
4. 三路独立架构审查：
   - 系统分层与 authority 审查
   - 接口 / 契约 / 状态机审查
   - Launcher / Supervisor / 产品前台审查

## 3. 当前系统真实状态

### 3.1 已经有的东西

当前仓库不是空壳。  
已经有这些正式工程骨架：

1. `Cloud`
   - `src/Superpowers.CloudControl`
2. `Launcher`
   - `src/Superpowers.Launcher`
3. `Launcher.Supervisor`
   - `src/Superpowers.Launcher.Supervisor`
4. `Runtime.Contracts`
   - `src/Superpowers.Runtime.Contracts`
5. `Runtime.Local`
   - `src/Superpowers.Runtime.Local`
6. `Runtime.Stardew`
   - `src/Superpowers.Runtime.Stardew`
7. `Game Mod`
   - `games/stardew-valley/Superpowers.Stardew.Mod`

而且已经有一批真实 endpoint、UI surface、history/memory/provider 代码在跑。

### 3.2 现在最危险的现实

当前最危险的现实不是“功能还没做完”。  
而是：

1. 总设计已经改成 `Cloud 编排 prompt`。
2. 但正式代码主链里，`Runtime.Local` 还在本地拼 prompt。
3. DTO / endpoint 已经长出来了。
4. 第一批上位正式契约已经补起来了，但总主文、总目录、旧方法 authority 还需要继续收口。
5. Launcher 已经长出页面。
6. 设计上已经把完整产品前台服务层单独立起来了，但代码实现还没有完全按这层落地。

大白话就是：

- `骨架有了`
- `主线没收死`
- `施工图不够`
- `旧线路还通电`

## 4. 总结论

当前项目最缺的，不是再补一个功能点说明。  
而是补一层更上位的：

- `系统施工蓝图`

这层蓝图至少要包含：

1. 接口总目录
2. 能力级契约包
3. 状态机总目录
4. Launcher 产品服务契约
5. title-local support matrix
6. 当前实现偏差表
7. 旧主链退役执行表

当前进展补记：

1. `接口总目录` 已补成正式附件。
2. `Launcher GameSettings` 已补成正式 product contract。
3. `NpcPanelBundle / PhoneContactList` 已补成正式 runtime contract。
4. 下一步重点已经从“缺文档”转成“总主文、总目录、退役专表三边口径继续收死”。

如果不先补这一层，后面继续让 AI 写实现，最容易出现 4 种漂移：

1. 继续在本地偷偷拼 prompt
2. Cloud / Runtime / Mod 三边重新争 authority
3. Launcher 页面先长，产品服务继续空
4. 某功能“看起来能跑”，但其实没有正式契约保证后续不走偏

## 5. 核心 Findings

### 5.1 致命：本地 prompt 主链还在正式成功路径里跑

问题：

1. 正式设计已经写死：
   - 本地只发结构化事实包
   - Cloud 自己编排最终 prompt
2. 但当前代码里：
   - `Runtime.Local` 还在直接调用 `StardewPrivateDialoguePromptBuilder`
   - 本地 prompt catalog 和本地 prompt assets 还在正式工程里参与主链

代码证据：

1. `src/Superpowers.Runtime.Local/Endpoints/PrivateDialogueEndpoint.cs`
2. `src/Superpowers.Runtime.Local/Endpoints/RemoteDirectEndpoint.cs`
3. `src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs`
4. `src/Superpowers.Runtime.Local/Endpoints/ThoughtEndpoint.cs`
5. `src/Superpowers.Runtime.Stardew/Adapter/StardewPrivateDialoguePromptBuilder.cs`
6. `src/Superpowers.Runtime.Stardew/Adapter/StardewPromptAssetCatalog.cs`
7. `src/Superpowers.Runtime.Stardew/PromptAssets/StardewValley/*`

为什么严重：

1. 这不是实现细节偏差。
2. 这是 authority owner 还没真正收口。
3. 后面每多做一个游戏，都会把错误边界复制一遍。

死结论：

1. 这批代码已经不能再算“待优化”。
2. 必须正式进入：
   - `retired business mainline`

### 5.2 致命：Cloud 现在还不是 prompt 编排 owner，只是 provider 转发层

问题：

1. 现在 Cloud 接到的还是已经拼好的 prompt payload。
2. 它主要做的是：
   - provider 路由
   - provider 调用
   - 响应校验
3. 它没有真正拿“结构化事实包”自己完成 prompt 编排。

代码证据：

1. `src/Superpowers.Runtime.Contracts/Narrative/PrivateDialogueProviderContracts.cs`
2. `src/Superpowers.CloudControl/Narrative/HostedNarrativeController.cs`
3. `src/Superpowers.CloudControl/Narrative/HostedNarrativeOrchestrator.cs`

为什么严重：

1. 这会把错误接口永久冻结到 Cloud 边界上。
2. 以后哪怕想把 prompt 真正收回 Cloud，也得拆现有 endpoint 和 DTO。

死结论：

1. Cloud 侧必须新增一层：
   - `fact package -> prompt orchestration`
2. 现在的 `provider prompt payload` 只能视为：
   - 过渡实现

### 5.3 高：`Runtime.Local` 和 `Runtime.<game>` 的边界已经糊了

问题：

1. 设计说：
   - `Runtime.Local` 只做统一 gate / repair / trace / commit 仲裁
   - `Runtime.<game>` 才做 title-local 映射
2. 但现在 `Runtime.Local` endpoint 直接依赖 `Stardew` adapter 和 `Stardew` contract。
3. `Runtime.Local` 已经在做：
   - snapshot build
   - authoritative history fixup
   - prompt build
   - title-local 语义处理

代码证据：

1. `src/Superpowers.Runtime.Local/Endpoints/PrivateDialogueEndpoint.cs`
2. `src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs`
3. `src/Superpowers.Runtime.Local/Endpoints/RemoteDirectEndpoint.cs`

为什么严重：

1. 第二个游戏一来，`Runtime.Local` 会变成懂所有游戏细节的大泥球。
2. 你要的“多游戏可复用”会直接失效。

死结论：

1. 必须把 `Runtime.Local` 和 `Runtime.<game>` 的边界做成单独正式 contract。

### 5.4 高：DTO / endpoint 已经存在，但还没有成套上位契约

问题：

当前已经存在这些代码级合同：

1. `PrivateDialogueRequest`
2. `RemoteDirectRequest`
3. `GroupChatTurnRequest`
4. `ThoughtRequest`
5. Cloud narrative endpoints
6. Runtime.Local endpoints

但这些还只是：

- `代码 DTO`
- `代码接口`

还不是：

- `正式能力级契约`

代码证据：

1. `src/Superpowers.Runtime.Stardew/Contracts/PrivateDialogueRequest.cs`
2. `src/Superpowers.Runtime.Stardew/Contracts/RemoteDirectRequest.cs`
3. `src/Superpowers.Runtime.Stardew/Contracts/GroupChatTurnRequest.cs`
4. `src/Superpowers.Runtime.Stardew/Contracts/ThoughtRequest.cs`
5. `src/Superpowers.Runtime.Local/Program.cs`
6. `src/Superpowers.CloudControl/Narrative/HostedNarrativeController.cs`

为什么严重：

1. 现在是“代码先长、契约后补”。
2. 以后很容易变成谁需要哪个字段，谁就在代码里偷偷加。

死结论：

1. 不允许继续只靠 DTO 维持系统边界。
2. 必须补成正式 contract 文档集。

### 5.5 高：关键状态机还散在代码里，没有被单独冻结

问题：

当前有不少状态机已经存在于代码里，但没有成册。

最典型的有：

1. `private dialogue pending_visible -> committed / recovered / rejected`
2. `remote direct available_now / unavailable_now`
3. `group session create / freeze / append / close`
4. `item carrier rendered / item event recorded / committed`
5. `Launcher readiness verdict`
6. `support submit / failed / submitted`

为什么严重：

1. 状态机不单独冻结，就一定会被不同模块各改一点。
2. 最后审计、trace、UI copy、恢复路径会全部对不上。

死结论：

1. 状态机必须独立成文。
2. 不能继续散在 profile、附件和代码里。

### 5.6 高：Launcher 现在还是“页面先长出来，产品服务没立起来”

问题：

当前 Launcher 已经有页面和 view model，但更像：

- `带页面的原型前台`

还不是：

- `完整产品前台`

代码证据：

1. `src/Superpowers.Launcher/ViewModels/LauncherShellViewModel.cs`
2. `src/Superpowers.Launcher/ViewModels/HomeViewModel.cs`
3. `src/Superpowers.Launcher/ViewModels/ProductRedeemViewModel.cs`
4. `src/Superpowers.Launcher/ViewModels/NotificationsViewModel.cs`
5. `src/Superpowers.Launcher/ViewModels/SupportViewModel.cs`
6. `src/Superpowers.Launcher.Supervisor/Readiness/StardewReadinessEvaluator.cs`

具体表现：

1. 页面有了
2. 但注册登录没有正式实现面
3. mod 下载/安装/更新/回滚没有正式服务契约
4. support / notification / redeem 还偏演示态
5. Launcher 自己还在读本地 verdict 文件、自己拉 SMAPI

为什么严重：

1. 这样会让桌面前台和 Supervisor 重复长逻辑。
2. 后面用户管理、权益、通知、支持都没有稳定产品主语。

死结论：

1. Launcher 必须补一套产品服务蓝图。
2. 不能只保留页面壳。

## 6. 现在必须补的正式文档集

### 6.1 系统总蓝图层

本轮已补齐：

1. `docs/superpowers/specs/attachments/2026-04-07-superpowers-current-implementation-divergence-appendix.md`
   - 用来逐条记录“设计要求 vs 当前代码现实”
2. `docs/superpowers/specs/attachments/2026-04-07-superpowers-interface-catalog-appendix.md`
   - 用来登记 Cloud / Runtime.Local / Runtime.<game> / Mod / Launcher 的正式接口总表
3. `docs/superpowers/specs/attachments/2026-04-07-superpowers-state-machine-catalog-appendix.md`
   - 用来登记系统级状态机总目录

### 6.2 Cloud 编排层

本轮已补齐：

1. `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
2. `docs/superpowers/contracts/runtime/cloud-prompt-assembly-contract.md`
3. `docs/superpowers/contracts/runtime/canonical-history-sourcing-contract.md`

用途：

1. 冻结“Cloud 吃什么输入”
2. 冻结“Cloud 如何从事实包编排 prompt”
3. 冻结“本地 projection 不是 authority”

### 6.3 能力级 contract

现在已经明确：

1. title-local `Stardew` 关键 contract 必须单独成文
2. 不能继续只靠 profile 里的字段列表

当前已落盘的第一批 `Stardew` title-local contract：

1. `stardew-phone-contact-entry-contract.md`
2. `stardew-remote-direct-thread-contract.md`
3. `stardew-remote-direct-availability-state-machine-contract.md`
4. `stardew-group-chat-session-contract.md`
5. `stardew-onsite-group-overlay-contract.md`
6. `stardew-phone-group-thread-contract.md`
7. `stardew-item-instantiation-creator-contract.md`
8. `stardew-item-gift-and-trade-host-executor-contract.md`
9. `stardew-proactive-dialogue-trigger-state-machine-contract.md`
10. `stardew-auto-action-and-schedule-restore-contract.md`

本轮已补齐的共用 runtime contract：

1. `cloud-orchestration-fact-package-contract`
2. `hosted-narrative-endpoint-contract`
3. `runtime-local-vs-title-adapter-boundary-contract`
4. 四个主 request contract 和 commit contract

### 6.4 Runtime 边界层

本轮已补齐：

1. `runtime-local-vs-title-adapter-boundary-contract.md`
2. `runtime-local-endpoint-catalog-contract.md`
3. `hosted-narrative-endpoint-contract.md`

用途：

1. 冻结 `Runtime.Local` 只做什么
2. 冻结 `Runtime.<game>` 必须做什么
3. 冻结 Cloud 和 Runtime.Local 的 endpoint owner

### 6.4A 本轮新增的请求 / 提交合同

本轮已补齐：

1. `private-dialogue-request-contract.md`
2. `remote-direct-request-contract.md`
3. `group-chat-turn-request-contract.md`
4. `thought-request-contract.md`
5. `private-dialogue-commit-state-machine-contract.md`

用途：

1. 冻结 4 类主请求最小字段
2. 冻结私聊从 request 到 committed 的正式状态机
3. 防止代码继续靠 DTO 自己长语义

### 6.5 Launcher / Supervisor / 产品前台层

本轮已补齐第一批：

1. `launcher-supervisor-boundary-contract.md`
2. `launcher-auth-session-contract.md`
3. `stardew-mod-package-install-update-rollback-contract.md`
4. `support-ticket-and-diagnostic-bundle-contract.md`
5. `launcher-notification-feed-contract.md`
6. `repair-update-recheck-state-machine.md`

本轮已把 `Launcher / Supervisor / 产品前台层` 首轮合同补齐，当前剩余桌面端工作重点不再是“缺合同”，而是：

1. 把这些合同继续下沉到接口目录、偏差表、实施计划和代码退役表
2. 让 `Launcher`、`Launcher.Supervisor`、`Stardew` 工作区真正按这些合同收口
3. 按 `2026-04-07-superpowers-launcher-implementation-order-and-service-split-appendix.md` 的固定顺序开改，不再让 ViewModel 自己长业务

### 6.6 退役执行层

必须新增：

1. `retirement-enforcement-checklist.md`

用途：

1. 不是只写“这批代码该退役”
2. 而是写死“退役完成”的判定标准：
   - 不再被正式工程引用
   - 不再进入主成功路径
   - 已迁出或删线
   - 有验证证据

## 7. 推荐施工顺序

### 7.1 第一批先做

推荐顺序：

1. `当前实现偏差表`
2. `Cloud 事实包 / prompt 编排契约`
3. `Runtime.Local vs Runtime.<game> 边界契约`
4. `private dialogue / remote direct / group chat / thought` 四套能力级契约
5. `Stardew` title-local contract 集

理由：

1. 前四步先补，才能先切断“本地拼 prompt”的旧主线。
2. `Stardew` title-local contract 已经补了第一批，后面可以直接拿来约束 mod 重构。
3. 这是后面所有多游戏复用的根。

### 7.2 第二批再做

推荐顺序：

1. `Launcher / Supervisor` 边界契约
2. `Launcher 产品服务契约`
3. `mod 下载更新 / 修复 / support / notification` 全套契约

理由：

1. 桌面层现在也缺，但它不是当前最危险的 authority 冲突点。
2. 先把 AI 主链 authority 收死，再补桌面产品前台更稳。

### 7.3 第三批再进入代码重建

只有满足下面条件，才允许正式进入大重构：

1. 本地 prompt 主链退役边界已经写死
2. Cloud 编排契约已经成文
3. 四个主能力 contract 已经成文
4. Runtime.Local 与 title adapter 边界已经写死

## 8. 本附件的最终结论

当前 `Superpowers` 最缺的不是“更多设计概念”，而是：

- `把设计变成不能乱改的施工蓝图`

现在项目已经进入一个很明确的阶段：

1. 愿景层够了
2. 能力层够了
3. 参考映射已经开始变细
4. 现在真正缺的是：
   - 契约层
   - 状态机层
   - 边界层
   - 偏差收口层

如果这层不先补，后面继续实现只会重复两件事：

1. 把旧边界再做一遍
2. 把新设计再说一遍

这两件事都不值钱。  
真正值钱的是：

- 先把主线 authority、接口、状态、退役规则钉死，再开始重建代码。
