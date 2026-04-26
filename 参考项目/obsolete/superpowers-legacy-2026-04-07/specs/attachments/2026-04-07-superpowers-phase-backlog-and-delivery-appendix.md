# Superpowers 当前阶段 Backlog 与实施顺序附件

## 1. 文档定位

本文只回答两件事：

1. 完整设计在当前 phase 里到底先做什么
2. `U1-U12` 的当前实施顺序和验收口径怎么落

固定回链：

- `docs/superpowers/governance/current-phase-boundary.md`
- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-reference-code-unit-reconstruction-and-afw-migration-appendix.md`

## 2. 当前 phase 能力过滤表

| 能力 | 当前状态 | 说明 |
| --- | --- | --- |
| 私聊 | `in-scope now` | 当前 phase 主闭环 |
| 动态选项 | `in-scope now` | 当前私聊正式输出的一部分 |
| 记忆压缩 | `in-scope now` | 当前 phase 主闭环 |
| 交易 / 给物 / 承诺 | `in-scope now` | 当前 phase 主闭环 |
| Launcher 启动、支持、产品入口 | `in-scope now` | 当前 player-visible proof 必做 |
| 远程一对一 | `implementation-kept-but-not-exit-gate` | 可保留实现与测试，但不是当前完成判定必需项 |
| 群聊 | `implementation-only` | 当前 title 可实现、可留证据，但不进当前退出门 |
| 信息传播 | `deferred` | 完整设计纳入，但不进当前 phase 主实施 |
| 世界事件 / 主动世界演化 | `deferred` | 现在定接口，不进当前 phase 主实施 |
| 自定义物品 / 状态 / NPC 创建 | `deferred` | 现在定接口，不进当前 phase 主实施 |
| 工具入口 | `deferred` | 现在定接口，不进当前 phase 主实施 |
| Agent NPC 多 Agent | `forbidden in current phase` | 当前不做生产实现 |

## 2A. 当前阶段的第一施工原则

当前阶段不是“先保住旧实现能跑”。  
固定先做：

1. 先退役旧业务主链：
   - 本地 prompt 资产
   - 本地 prompt builder
   - 本地记忆压缩 prompt builder
   - 本地 provider 语义主线
2. 再建立新正式主链：
   - `Cloud 编排`
   - `Runtime.Local gate`
   - `Runtime.<game> Adapter`
   - `Game Mod`

固定规则：

1. 不为上述旧主链补兼容层。
2. 若某个任务会继续扩大本地 prompt 主线，直接判错，不进入当前 backlog。

## 3. `U1-U12` 当前 phase 过滤表

| 单元 | 当前状态 | 当前 phase 角色 |
| --- | --- | --- |
| `U1` | `in-scope now` | prompt 资产合同 |
| `U2` | `in-scope now` | 私聊/记忆/动作的 Cloud 编排主线 |
| `U3` | `in-scope now` | 聊天主档和正本骨架 |
| `U4` | `in-scope now` | 记忆压缩与回灌 |
| `U5` | `in-scope now` | 行为协议、repair、deterministic gate |
| `U6` | `implementation-only` | 群聊/远程多方的扩展核心 |
| `U7` | `deferred` | 传播协议 |
| `U8` | `deferred` | 世界事件主骨架 |
| `U9` | `deferred` | 造物 / 造状态 / 造 NPC |
| `U10` | `in-scope now` | Stardew 核心宿主接入；高级宿主接入延后 |
| `U11` | `in-scope now` | Launcher / Supervisor / 支持闭环 |
| `U12` | `forbidden in current phase` | agent NPC 第三阶段单独立项 |

## 4. 当前推荐实施顺序

1. `退役旧业务主链`
   - 先切掉本地 prompt / 本地 provider / 本地编排旧主线
2. `U11`
   - 先把玩家入口、启动检查、readiness verdict、支持闭环收稳
3. `U1`
   - 冻结游戏级 prompt 资产和变量槽位
4. `U3`
   - 先立 canonical chat truth 骨架
5. `U2`
   - 接 Cloud 会话编排与 provider 通信
6. `U5`
   - 补行为协议、repair、deterministic gate
7. `U10`
   - 接 Stardew 私聊/面板/宿主写回
8. `U4`
   - 接记忆压缩与回灌
9. `跨单元提交合同`
   - 把 `candidate -> committed` 收口到正式合同
10. `U6`
   - 再进入群聊 / 远程实现留证据
11. `U7`
   - 传播协议
12. `U8 + U9`
   - 世界 / 生成 / 工具正式接口层
13. `U12`
   - 第三阶段再单独立项

## 5. 依赖关系

| 前置单元 | 产物 | 解锁后续单元 |
| --- | --- | --- |
| `U11` | 玩家入口、启动路径、支持闭环 | 所有 player-visible 能力 |
| `U1` | prompt 资产合同 | `U2 / U4 / U6 / U7 / U8 / U9` |
| `U3` | canonical chat truth | `U2 / U4 / U6` |
| `U2` | Cloud 编排主线 | `U5 / U6 / U7 / U8 / U9` |
| `U5` | repair + deterministic gate | `U10 / U6 / U7 / U8 / U9` |
| `U10` | 宿主接入与宿主回执 | `candidate -> committed` 正式落地 |
| `U4` | 记忆压缩链 | 私聊/记忆主闭环 |
| `commit contract` | committed 升级规则 | 所有需要正式写正本的能力 |

## 6. 当前最小验收口径

| 单元 | 最小验收要求 |
| --- | --- |
| `U1` | prompt 资产按游戏隔离；Cloud 独占明文正本 |
| `U2` | 实际能从 Cloud 走到 provider，再回结构化候选 |
| `U3` | canonical chat truth 能区分候选与正式 |
| `U4` | 只消费 committed 结果进入记忆 |
| `U5` | deterministic gate 能拦非法动作，且不改语义 |
| `U10` | 宿主有真实 visible host、真实回执、真实失败暴露 |
| `U11` | Launcher/Supervisor 有单一 readiness truth 和支持入口 |
| `U6` | 能留 implementation-only 证据，但不冒充当前 exit gate |
| `U7` | 传播必须先过 carrier，不能直接改第三方 |
| `U8` | 世界事件必须由宿主触发节拍 |
| `U9` | 生成能力必须走 support matrix 硬拦截 |
| `U12` | 当前 phase 不得进入生产路径 |

## 7. review 必查点

1. 当前 phase 文档是否仍把 deferred 能力塞进 in-scope backlog
2. `implementation-only` 是否被错误写成当前 exit criteria
3. 每个任务是否都写清了前置依赖和验收口径
4. 是否仍按当前 phase 推荐顺序施工，而不是从世界/工具链乱开
