# Superpowers AFW 治理收口附件

## 1. 文档定位

本文用于把当前总设计里命中的 AFW 治理字段按能力族收口。  
它不是替代总文，而是给设计、实现、review 一个统一检查表。

## 2. 当前命中的 profile 组合

固定为：

- `backend-service + generic-interactive-client + repo-local governance overlay`

说明：

1. 有服务端编排主链。
2. 有桌面前台和游戏内前台。
3. 当前不是 Godot 项目。

## 3. 能力族治理表

| 能力族 | 首次可见宿主 | 补看宿主 | 失败前台暴露点 | 资源来源真相源 | 输入真相源 | authority owner | 生命周期 owner | 并发 / 时序 contract | 旧入口退役条件 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 私聊 / 动态选项 / 记忆 / 社交动作 | 游戏内对话 / 面板 / carrier | `Launcher -> 游戏页 / 支持与帮助` | 游戏宿主失败文案 + Launcher 支持闭环 | Cloud 提示词、聊天、记忆 | Mod 宿主事实 + Adapter 事实包 | Cloud / Runtime.Local / Mod 分层各自 owner | Mod -> Adapter -> Cloud -> Runtime.Local -> Adapter -> Mod | 一轮一包事实；切 NPC 后旧回包不得覆盖当前面 | 旧本地拼提示词、旧直连 provider、旧绕过 gate 路径全部退役 |
| 群聊 / 远程 / 传播 | 游戏内群聊 / 远程线程 / 目标宿主 | `Launcher -> 支持与帮助` | 群聊宿主失败 + Launcher 支持 | Cloud 群聊 / 传播正本 | Mod 采集 + Adapter 线程映射 | Cloud / Runtime.Local / Adapter / Mod | 同上 | participant 集合冻结；threadKey 固定；传播必须带 hop / 来源编号 | 旧群聊旁路、旧远程 thread 路径、无 hop 传播路径退役 |
| 世界事件 / 对象生成 / 工具入口 | 游戏内世界事件或对象宿主 | `Launcher -> 支持与帮助` | 游戏宿主失败 + Launcher 支持 | Cloud 事件 / 生成正本 | Mod 宿主事实 + Adapter 对象映射 | Cloud 负责建议；Mod 负责最终执行 | 同上 | 旧建议不得覆盖新世界状态；生成动作必须走审批 / schema 校验 | 旧直接写宿主成功路径退役 |
| 桌面前台 / 产品与兑换 / 支持闭环 | Launcher | `通知 / 支持与帮助` | 首页、游戏页、支持页、通知页 | Cloud access / entitlement / listing 正本 | Launcher.Supervisor 本地运行事实 | Launcher.Supervisor 负责 readiness；Cloud 负责 access / entitlement | Launcher + Supervisor + Runtime.Local | verdict 只认最新 policy + runtime facts；旧 verdict 不能覆盖 `blocked` | 旧桌面二套 verdict、二套 entitlement 展示路径退役 |

## 3A. 分层 Owner 表

| 层 | 唯一 authority owner | 可以拥有 | 不得拥有 |
| --- | --- | --- | --- |
| `Cloud` | `CloudControl` | prompt 资产明文、prompt 编排、provider 通信、canonical chat、canonical memory、明文审计、成本归因 | 宿主最终写回、玩家可见 readiness verdict |
| `Launcher` | `Launcher` 前台壳 | 注册登录前台、游戏页、产品与兑换、通知、支持与帮助 | 第二套 readiness truth、第二套 entitlement truth |
| `Launcher.Supervisor` | `Launcher.Supervisor` | `launchReadinessVerdict`、启动前检查、修复动作、本地运行事实汇总 | prompt / chat / memory 正本、宿主写回 |
| `Runtime.Local` | `Runtime.Local` | deterministic gate、repair / normalize、degradation、commit 仲裁、trace 对账 | prompt 真源、宿主最终写回、产品真相 |
| `Runtime.<game> Adapter` | title adapter | 事实冻结、字段映射、执行清单翻译、title support matrix | 最终提示词编排、canonical 正本 |
| `Game Mod` | game-local mod | 宿主取数、宿主 UI、最终宿主写回、宿主可见证据 | prompt 真源、provider 通信、正式 committed 仲裁 |
| `AFW` | 无独立 authority | workflow orchestration、checkpoint、candidate generation、memory planning、tool registry | 上述任一正式 authority |

## 3B. Checkpoint / Sidecar / Diagnostic 合同

| 载体 | owner | 允许保存 | 禁止保存 | 固定性质 |
| --- | --- | --- | --- | --- |
| `AFW checkpoint` | `Cloud` 内 AFW 子层 | policy-safe metadata、流程位置、redacted digest、重试所需最小键 | 完整 prompt 明文、完整 persona 包、完整记忆正本、宿主写回结果冒充正本 | `derived state` |
| `diagnostic sidecar` | `Cloud` 生成，`Runtime.Local` / `Game Mod` 回链 | source-style `actions[]`、repair 记录、projection ref、trace join、失败原因 | 第二套 canonical history、第二套 committed truth | `diagnostic only` |
| `surface replay cache` | `Runtime.Local` / `Game Mod` | 当前 surface 可见文本、surface hook ref、局部 replay 键 | canonical prompt、canonical memory、跨会话 authority | `local projection` |
| `plaintext canonical audit` | `Cloud` | prompt / chat / memory 明文、provider 请求响应、正式候选与正式提交链 | 伪造宿主成功 | `authoritative audit` |

固定规则：

1. `AFW checkpoint` 永远不是产品承诺。
2. `diagnostic sidecar` 永远不是正式历史。
3. `surface replay cache` 永远不是 canonical truth。
4. 如果某段旧实现把 sidecar、cache、checkpoint 当成正式正本，这段实现必须退役。

## 4. AFW 边界硬规则

固定不允许：

1. AFW 拥有提示词、聊天、记忆、runtime state、host writeback 的 authority。
2. AFW 直接产出玩家可见 readiness verdict。
3. AFW 直接改宿主。
4. AFW 继续保留旧成功路径不退役。

固定必须：

1. 结构化输出走 schema 或 typed 校验。
2. streaming / non-streaming 都要受治理。
3. 主链日志、审计链、trace 对账链要闭环。
4. 任何迁移都要成对写“迁移 + 旧入口退役”。
5. 本地 prompt builder / prompt catalog / embedded prompt assets 不得继续作为 AFW 接入前的过渡主线。
6. 若当前正式主线已要求玩家可见文本 streaming / pseudo-streaming，AFW 迁移后必须继续满足，不得以“框架默认行为”回退为整包回传。
