# Superpowers Master Design

## 1. 文档定位

本文从现在开始是 `docs/superpowers` 的：

- `唯一设计总主文`

当前 `Superpowers` 的设计真相固定采用：

1. `一个总主文`
2. `一组正式附件`
3. `一组 contract / profile / governance`
4. `其余重复、冲突、被替代文档统一退役到 obsolete`

本文负责：

1. 给出系统设计总目录
2. 给出唯一读取顺序
3. 给出系统级总边界
4. 给出架构总视图
5. 给出正式附件注册表

本文不负责：

1. 在主文里重复所有专题细节
2. 让旧稿继续并行当真相
3. 跳过 contract / profile / governance 直接代替一切细节合同

## 2. 系统设计目录

当前完整系统设计固定按 3 卷组织：

1. `系统概览`
   - 目标
   - 范围
   - 非目标
   - 角色
   - 关键场景
2. `架构视图`
   - 系统上下文
   - 7 层分层
   - 统一主线
   - 提交合同
   - AFW 边界
   - 桌面前台与产品包
   - 全量能力放位
3. `交付治理`
   - 当前 phase 过滤
   - 模块清单
   - 接口目录
   - 历史代码处置
   - 实施顺序
   - 正式附件索引
   - 退役策略

目录入口固定为：

- `docs/superpowers/specs/README.md`

## 3. 唯一读取顺序

以后读 `superpowers` 设计，固定按这个顺序：

1. `docs/superpowers/governance/current-phase-boundary.md`
2. `docs/superpowers/specs/README.md`
3. 本文
4. 本文第 `13` 节登记的正式附件
5. 对应 `contracts / profiles / governance / evidence`

固定规则：

1. `phase boundary` 只决定当前阶段过滤，不再自己登记第二套正式附件清单。
2. 正式附件注册表只允许出现在本文第 `13` 节。

## 4. 唯一设计真相注册表

从本文生效后，`docs/superpowers/specs` 里的当前设计真相只包括：

1. 本文
2. 本文第 `13` 节列出的正式附件

除这两类之外：

- 根目录旧设计稿
- 被替代的平行方案稿
- 历史推导稿
- 旧附件

统一视为：

- `retired reference`

固定规则：

1. 不允许旧稿与新稿并行作为当前 authority。
2. 不允许在实现、review、计划里把退役稿重新当成当前设计入口。
3. 旧稿若仍保留参考价值，必须明确标记为：
   - `retired reference`

## 5. 输入真源

本设计基于以下真源：

1. `docs/superpowers/governance/current-phase-boundary.md`
2. `docs/superpowers/governance/afw-boundary-note.md`
3. `docs/superpowers/contracts/product/narrative-base-pack-contract.md`
4. `docs/superpowers/contracts/product/capability-claim-matrix.md`
5. `docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md`
6. `docs/superpowers/contracts/product/launcher-supervisor-boundary-contract.md`
7. `docs/superpowers/contracts/product/launcher-auth-session-contract.md`
8. `docs/superpowers/contracts/product/stardew-mod-package-install-update-rollback-contract.md`
9. `docs/superpowers/contracts/product/support-ticket-and-diagnostic-bundle-contract.md`
10. `docs/superpowers/contracts/product/launcher-notification-feed-contract.md`
11. `docs/superpowers/contracts/product/repair-update-recheck-state-machine.md`
12. `docs/superpowers/contracts/product/launcher-launch-orchestration-state-machine.md`
13. `docs/superpowers/contracts/product/launcher-account-surface-state-machine.md`
14. `docs/superpowers/contracts/product/stardew-launcher-workspace-ia.md`
15. `docs/superpowers/contracts/product/supervisor-preflight-input-matrix.md`
16. `docs/superpowers/contracts/product/redeem-request-and-receipt-contract.md`
17. `docs/superpowers/contracts/product/launcher-product-catalog-visibility-contract.md`
18. `docs/superpowers/contracts/product/player-entitlement-visibility-contract.md`
19. `docs/superpowers/contracts/product/purchase-handoff-state-machine.md`
20. `docs/superpowers/contracts/product/support-closure-state-machine.md`
21. `docs/superpowers/contracts/product/launcher-support-surface-flow.md`
22. `docs/superpowers/contracts/runtime/deterministic-command-event-contract.md`
23. `docs/superpowers/contracts/runtime/narrative-degradation-contract.md`
24. `docs/superpowers/contracts/runtime/trace-audit-contract.md`
25. `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
26. `docs/superpowers/contracts/runtime/cloud-prompt-assembly-contract.md`
27. `docs/superpowers/contracts/runtime/canonical-history-sourcing-contract.md`
28. `docs/superpowers/contracts/runtime/runtime-local-vs-title-adapter-boundary-contract.md`
29. `docs/superpowers/contracts/runtime/runtime-local-endpoint-catalog-contract.md`
30. `docs/superpowers/contracts/runtime/hosted-narrative-endpoint-contract.md`
31. `docs/superpowers/contracts/runtime/private-dialogue-request-contract.md`
32. `docs/superpowers/contracts/runtime/remote-direct-request-contract.md`
33. `docs/superpowers/contracts/runtime/group-chat-turn-request-contract.md`
34. `docs/superpowers/contracts/runtime/thought-request-contract.md`
35. `docs/superpowers/contracts/runtime/private-dialogue-commit-state-machine-contract.md`
36. `docs/superpowers/profiles/games/<gameId>/game-integration-profile.md`
37. `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/**`

其中必须明确区分：

1. `参考事实`
   - 来自 `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/**`
2. `Superpowers 设计归纳`
   - 来自本文、正式附件、contract、profile

不允许把设计归纳硬写成“参考 mod 已原样证明的事实”。

## 6. 产品目标、范围、非目标

### 6.1 目标

`Superpowers` 的目标固定为：

1. 一条跨游戏 AI Mod 产品线
2. 一个玩家桌面入口
3. 一个本地执行协调核
4. 一个云端持有提示词、聊天、记忆和审计正本的主链
5. 多个按游戏隔离的接入层

### 6.2 范围

完整设计范围固定覆盖：

1. `Cloud`
2. `Launcher`
3. `Launcher.Supervisor`
4. `Runtime.Local`
5. `Runtime.<game> Adapter`
6. `Game Mod`
7. `Host Game`
8. prompt 资产
9. canonical chat / memory / audit
10. dialogue / memory / social / group / propagation / world / tooling

### 6.3 非目标

本文固定不做以下承诺：

1. 本地备用 AI 主线
2. Cloud 直接改宿主
3. 长期双轨主线
4. 开发阶段向前兼容
5. 当前 phase 之外的 support claim
6. 未经 source-faithful 复刻就直接把生产主链迁成 AFW

### 6.4 开发阶段代码处置原则

当前仓库仍处于开发阶段。  
固定口径不是“尽量兼容旧实现”，而是：

1. 旧业务主链只要和现行设计冲突，直接退役。
2. 不为旧错误边界补兼容层。
3. 只允许保留还能当：
   - 宿主 UI 壳
   - 宿主 hook 壳
   - 桌面前台壳
   - readiness / 审计 / canonical store 壳
4. 旧代码后续若移出正式工程，固定进入：
   - `legacy/retired-implementation/`
5. 在真正移出前，也必须先在设计上标成：
   - `retired implementation`
6. 任何 `本地拼 prompt`、`本地持有 prompt 资产明文`、`本地直连 provider` 的实现，不得继续留在正式主链。

## 7. 角色与关键场景

### 7.1 角色

当前设计最少覆盖以下角色：

1. `玩家`
2. `桌面前台`
3. `本地运行管家`
4. `Cloud 编排与产品控制面`
5. `游戏接入适配层`
6. `Game Mod`
7. `审计 / review / support 使用者`

### 7.2 关键场景

当前系统级关键场景固定包括：

1. 玩家登录、选择游戏、启动前检查、进入游戏
2. 私聊 candidate 生成、deterministic gate、宿主落地、正式提交
3. 记忆压缩与后续回灌
4. 社交动作 candidate、宿主执行与失败暴露
5. implementation-only 群聊 / 远程留证据
6. 世界 / 生成 / 工具接口预留
7. 支持与帮助、问题回执、修复入口

## 8. 系统上下文与部署视图

### 8.1 系统上下文

当前系统固定存在 4 个上下文边界：

1. `玩家机器`
   - `Launcher`
   - `Launcher.Supervisor`
   - `Runtime.Local`
   - `Runtime.<game> Adapter`
   - `Game Mod`
   - `Host Game`
2. `Cloud`
   - prompt 资产
   - chat / memory / plaintext audit
   - provider 通信
   - product access / entitlement
3. `AI Provider`
4. `参考 mod / recovered analysis`

### 8.2 部署视图

当前仓库的正式工程落点固定为：

1. `src/Superpowers.CloudControl`
2. `src/Superpowers.Launcher`
3. `src/Superpowers.Launcher.Supervisor`
4. `src/Superpowers.Runtime.Contracts`
5. `src/Superpowers.Runtime.Local`
6. `src/Superpowers.Runtime.Stardew`
7. `games/stardew-valley/Superpowers.Stardew.Mod`

系统模块清单与接口目录见：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-runtime-module-interface-and-commit-contract-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-interface-and-class-landing-appendix.md`

## 9. 架构视图

### 9.1 总体分层

从现在起，`Superpowers` 固定按 7 层拆：

1. `Cloud`
2. `Launcher`
3. `Launcher.Supervisor`
4. `Runtime.Local`
5. `Runtime.<game> Adapter`
6. `Game Mod`
7. `Host Game`

#### 9.1.1 Cloud

负责：

- 每个游戏自己的提示词资产
- 提示词编排
- provider 通信
- 聊天正本
- 记忆正本
- 明文 canonical audit
- 结构化候选结果
- 成本归因
- access / entitlement / claim / listing 真相

不负责：

- 直接改宿主
- 替代本地 deterministic gate
- 宣布宿主已经真正落地成功

#### 9.1.2 Launcher

负责：

- 注册 / 登录 / 账号状态
- 首页 / 游戏 / 产品与兑换 / 通知 / 支持与帮助 / 设置
- 游戏入口、状态、启动、更新、修复
- 产品介绍、外部购买跳转、Key 兑换、我的权益
- 通知和回执
- 问题提交与帮助入口

不负责：

- prompt 真源
- chat / memory 正本
- provider 控制面真相
- claim / listing / waiver 真相

#### 9.1.3 Launcher.Supervisor

负责：

- 启动前检查
- 拉起 / 停止 `Runtime.Local`
- 汇总本地运行事实
- 生成唯一玩家可见的 `launchReadinessVerdict`
- 协调 `Launcher`、`Runtime.Local`、游戏进程
- 受控修复、更新、重启动作

#### 9.1.4 Runtime.Local

负责：

- 收结构化事实包
- 发 Cloud
- 收 Cloud 结构化结果
- 统一校验、修复、归一
- 统一执行前 gate
- runtime outcome 仲裁
- trace / health / recovery / execution evidence

不负责：

- 产品前台
- canonical prompt / chat / memory 正本存储
- 最终宿主执行

#### 9.1.5 Runtime.<game> Adapter

负责：

- 把游戏真实情况翻成统一事实包
- 把统一结果翻成游戏执行清单
- 处理字段映射、频道差异、落点映射
- 按 support matrix 做 title-local 裁决

不负责：

- 拼最终提示词
- 存正本
- 直接改游戏

#### 9.1.6 Game Mod

负责：

- 从宿主里取真实情况
- 渲染玩家可见面
- 最后真正改宿主
- 回传宿主执行结果和宿主证据

#### 9.1.7 Host Game

是最终真实被读取、被展示、被写回的对象。  
它是外部 truth object，不是 repo 内可写 owner。

#### 9.1.8 历史代码处置分桶

当前代码资产固定分 3 桶：

1. `retired business mainline`
   - 旧业务主链
   - 与现行 authority 冲突的本地 prompt / 本地编排 / 本地 provider 逻辑
   - 这些代码只允许参考，不允许继续当正式实现
2. `kept carrier shell`
   - 宿主 UI
   - 宿主 hook
   - 桌面前台外壳
   - 本地 surface / recovery / readiness 展示壳
   - 这些代码允许保留，但不再拥有业务 authority
3. `kept authority core`
   - Cloud canonical history / memory / audit
   - Launcher.Supervisor readiness truth
   - Runtime.Local deterministic gate / commit 仲裁
   - 这些代码允许继续作为正式主链骨架

正式分桶见：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-runtime-module-interface-and-commit-contract-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-reference-unit-to-repo-landing-map-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-current-code-retirement-and-cutover-appendix.md`

### 9.2 桌面前台是完整产品前台，不是启动壳

桌面前台必须完整承接：

1. `账号面`
2. `游戏面`
3. `产品与兑换面`
4. `通知面`
5. `支持与帮助面`
6. `设置面`

并且必须承接以下产品包：

1. `试用包`
2. `基础包-BYOK`
3. `基础包-托管`
4. `高级包-绘画`
5. `高级包-视频`
6. `高级包-语音`

桌面前台详细设计见：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-launcher-and-pack-appendix.md`

### 9.3 统一主线

所有能力最终都必须经过同一条正式主线：

1. `宿主触发`
2. `Mod 取真实情况`
3. `Runtime.<game> Adapter 整理结构化事实包`
4. `Runtime.Local 做统一入口检查`
5. `Runtime.Local 发给 Cloud`
6. `Cloud 做提示词编排并连 provider`
7. `Cloud 返回结构化结果`
8. `Runtime.Local 做统一校验、修复、归一`
9. `Runtime.<game> Adapter 翻成游戏执行清单`
10. `Mod 最后真正改游戏`
11. `执行结果回写`

### 9.4 审计真相拆分

这块必须拆开，不允许再笼统写成“审计都在 Cloud”。

1. `Cloud`
   - prompt / chat / memory 明文 canonical audit
2. `Launcher.Supervisor`
   - launch / readiness 操作审计
3. `Runtime.Local`
   - deterministic command / degradation / commit 审计
4. `Game Mod`
   - host writeback / player-visible surface 审计

正式拆分见：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-runtime-module-interface-and-commit-contract-appendix.md`

### 9.5 严格成功与两段提交

固定规则：

1. `Cloud` 可以保留候选阶段明文。
2. 候选阶段必须明确标成：
   - `未正式生效`
3. 保密优先口径下，prompt 编排正文、记忆选取正文、规则链正文、已渲染完整 prompt 不得回传客户端。
4. 纯文本玩家可见面允许先进入：
   - `pending_visible`
   也就是玩家可以先看到角色已经开始回答，但这不等于正式 committed。
5. `pending_visible` 只允许用于：
   - 私聊文本
   - 远程一对一文本
   - thought 文字面
   不允许拿来冒充物品已发放、关系已变化、事件已落地。
6. 系统默认采用：
   - 前台快反馈
   - 后台严格确认
   也就是玩家先看到角色进入思考和出字，不等待后台审计、记忆、提交全做完。
7. 等待和失败都必须走玩家可见面自己的 in-world copy，不允许前台直接露出：
   - `pending_visible`
   - `committed`
   - `trace`
   - `audit`
   这类系统术语。
8. 只有同时满足以下条件，才算真正成功：
   - `Cloud` 已持有候选和审计
   - `Runtime.Local` 校验通过
   - `Game Mod` 真正在宿主里显示 / 写回成功
9. `Runtime.Local` 是 runtime outcome 的唯一仲裁 owner。
10. 只有 `Runtime.Local` 发出 `CommitPromotionAck` 后，`Cloud` 才能把聊天 / 记忆 / committed audit 升级成正式正本。

固定不允许：

1. `Cloud` 一生成结果就直接记成正式历史
2. 候选结果污染正式记忆
3. `Game Mod` 越过 `Runtime.Local` 直接通知 Cloud committed
4. 本地失败但云端正式历史看起来像已经发生过
5. 为了缩短链路，把云端已编排好的最终 prompt 正文下发到客户端
6. 先把会改宿主状态的结果演给玩家看，再在后台补 committed

正式提交合同见：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-runtime-module-interface-and-commit-contract-appendix.md`

## 10. 全量能力与当前 phase 的关系

完整方案必须纳入参考 mod 已恢复的全量能力。  
但完整设计不自动改写当前 phase 的实施边界。

固定采用 4 层放位：

1. `共用核心层`
2. `共用扩展层`
3. `先定正式接口层`
4. `游戏专题层`

全量能力映射见：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-reference-mod-capability-mapping-appendix.md`

当前 phase 过滤、实施顺序、验收口径见：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-phase-backlog-and-delivery-appendix.md`

## 11. 参考代码单元复刻与 AFW 迁移

以后凡是讨论：

- 第一阶段怎么贴参考 mod
- 第二阶段怎么迁 AFW
- 某个单元是直接搬、包一层搬、还是必须重写

统一回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-reference-code-unit-reconstruction-and-afw-migration-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-reference-unit-to-repo-landing-map-appendix.md`

固定不允许：

1. 双轨主线
2. 旧入口继续跑成功路径
3. 云端直接改宿主
4. Mod 自己拼最终提示词

## 12. AFW 边界

本项目当前命中的 profile 组合固定为：

- `backend-service + generic-interactive-client + repo-local governance overlay`

固定说明：

1. AFW 可以作为 Cloud 侧编排层的一部分。
2. AFW 不是提示词、聊天、记忆、runtime state、host writeback 的 authority owner。
3. AFW 不得拥有玩家可见 readiness verdict。
4. AFW 不得拥有最终宿主执行权。
5. AFW 迁移必须和旧入口退役成对出现。

AFW 治理收口见：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-afw-governance-overlay-appendix.md`

## 13. 正式附件索引表

| 正式附件 | 覆盖主题 | 系统视图类型 | 当前 phase 作用 |
| --- | --- | --- | --- |
| `2026-04-07-superpowers-reference-mod-capability-mapping-appendix.md` | 全量能力映射、社交/世界主链放位 | 能力视图 | 全量设计 |
| `2026-04-07-superpowers-core-dialogue-memory-social-appendix.md` | 私聊、记忆、社交动作主线 | 运行视图 | 当前主闭环 |
| `2026-04-07-superpowers-group-propagation-expansion-appendix.md` | 群聊、远程、传播扩展 | 扩展运行视图 | implementation-only / 后续 |
| `2026-04-07-superpowers-world-generation-and-tooling-appendix.md` | 世界、生成、工具入口 | 正式接口视图 | 现在定接口，后续接实 |
| `2026-04-07-superpowers-reference-code-unit-reconstruction-and-afw-migration-appendix.md` | `U1-U12` 复刻与迁移规则 | 实施视图 | 当前必须遵守 |
| `2026-04-07-superpowers-launcher-and-pack-appendix.md` | 桌面前台、产品包、支持闭环 | 前台与产品视图 | 当前必须遵守 |
| `2026-04-07-superpowers-afw-governance-overlay-appendix.md` | AFW 边界、owner、治理红线 | 治理视图 | 当前必须遵守 |
| `2026-04-07-superpowers-runtime-module-interface-and-commit-contract-appendix.md` | 模块清单、接口目录、提交合同、审计拆分 | 模块与合同视图 | 当前必须遵守 |
| `2026-04-07-superpowers-current-code-retirement-and-rebuild-appendix.md` | 当前仓库哪些代码退役、哪些保留、先从哪里下刀 | 代码处置视图 | 当前必须遵守 |
| `2026-04-07-superpowers-phase-backlog-and-delivery-appendix.md` | 当前 phase 过滤、顺序、依赖、验收 | 交付视图 | 当前必须遵守 |
| `2026-04-07-superpowers-stardew-host-integration-map-appendix.md` | Stardew 宿主接入总地图、surface 与 hook 落点 | 宿主接入视图 | 当前必须遵守 |
| `2026-04-07-superpowers-stardew-feature-reference-implementation-appendix.md` | Stardew 功能清单、参考 mod 对照、逐功能落地与禁止漂移边界 | 参考实现视图 | 当前必须遵守 |
| `2026-04-07-superpowers-stardew-mod-rebuild-and-implementation-appendix.md` | `Superpowers.Stardew.Mod` 工程重建、类职责拆分、壳保留、断电点、逐功能落地蓝图 | Mod 重建视图 | 当前必须遵守 |
| `2026-04-07-superpowers-stardew-mod-hook-session-projector-executor-appendix.md` | Stardew 每个功能的 hook、snapshot、session、transport、projector、executor、DTO 落点 | Mod 施工落点视图 | 当前必须遵守 |
| `2026-04-07-superpowers-stardew-current-code-retirement-and-cutover-appendix.md` | Stardew 当前旧代码逐文件归桶、断电清单、替代新类与 grep 验收词 | Mod 旧代码处置视图 | 当前必须遵守 |
| `2026-04-07-superpowers-architecture-gap-and-blueprint-appendix.md` | 当前系统缺口、三路架构审查汇总、必须补的施工蓝图与文档集 | 架构整改视图 | 当前必须遵守 |
| `2026-04-07-superpowers-reference-unit-to-repo-landing-map-appendix.md` | `U1-U12` 到仓库目录的正式落点与退役表 | 施工落点视图 | 当前必须遵守 |
| `2026-04-07-superpowers-current-implementation-divergence-appendix.md` | 设计要求与当前代码现实的逐条偏差、退役与重建处置表 | 偏差整改视图 | 当前必须遵守 |
| `2026-04-07-superpowers-interface-catalog-appendix.md` | Cloud / Launcher / Supervisor / Runtime / Mod 的正式接口总目录 | 接口目录视图 | 当前必须遵守 |
| `2026-04-07-superpowers-state-machine-catalog-appendix.md` | shared runtime、title-local、launcher/supervisor 的状态机总目录 | 状态机目录视图 | 当前必须遵守 |
| `2026-04-07-superpowers-launcher-current-code-retirement-and-rebuild-appendix.md` | Launcher / Supervisor 当前文件逐条归桶、保留壳、断电点与重建顺序 | 桌面重建视图 | 当前必须遵守 |
| `2026-04-07-superpowers-launcher-implementation-order-and-service-split-appendix.md` | Launcher service 拆分、旧 ViewModel 迁移表、正式施工顺序与完工判断 | 桌面实施视图 | 当前必须遵守 |
| `2026-04-07-superpowers-launcher-interface-and-class-landing-appendix.md` | Launcher 未来代码目录、类职责、桥接接口、页面状态模型、旧类退役条件 | 桌面落点视图 | 当前必须遵守 |
| `2026-04-07-superpowers-launcher-bridge-and-dto-contract-appendix.md` | Launcher 对 Supervisor / Cloud 的桥方法、DTO 名字、旧入口断电清单 | 桌面对接视图 | 当前必须遵守 |

固定规则：

1. 这张表是唯一正式附件注册表。
2. `current-phase-boundary`、`README`、`profile` 都只能引用这张表，不能再各写一套。

## 14. 目录治理与退役策略

从现在起：

1. `attachments/` 目录应只保留当前正式附件，或明确标记为 `retired reference` 的辅助参考。
2. 未被本文第 `13` 节登记的附件，不得再被当成当前 authority。
3. 历史稿统一退役到：
   - `docs/superpowers/specs/obsolete/`
4. 历史业务主链代码统一退役到：
   - `legacy/retired-implementation/`
5. 在真正迁出前，历史业务主链也不得继续挂在正式主链说明里当“待修实现”。

退役后固定规则：

1. 不再作为当前设计入口。
2. 不再作为实施和 review 的当前 authority。
3. 若仍有参考价值，必须明确标记为：
   - `retired reference`
4. 对代码同样成立：
   - `retired implementation`

## 15. 最终结论

以后 `superpowers` 的设计固定采用：

- `系统设计目录入口 + 一个总主文 + 一组正式附件 + 对应 contract / profile / governance + 退役旧稿`

其中：

1. `README` 只负责目录导航
2. 真正的设计真相仍然只来自：
   - 总主文
   - 正式附件

并且必须长期坚持：

1. 桌面前台是完整产品前台，不是启动壳。
2. Cloud 持有 prompt、chat、memory 的 canonical 明文正本。
3. 本地只保留执行协调、deterministic gate、commit 仲裁、trace、health、recovery。
4. 最终宿主执行权固定留在 `Game Mod`。
5. 全量能力纳入完整方案，但按层放位，不全塞进共享核心。
6. 所有能力只允许走一条正式主线。
