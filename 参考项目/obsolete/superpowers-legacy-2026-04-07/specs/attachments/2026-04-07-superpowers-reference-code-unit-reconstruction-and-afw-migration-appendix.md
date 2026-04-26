# Superpowers 参考代码单元复刻与 AFW 迁移规则附件

## 1. 文档定位

本文只回答下面 4 件事：

1. 第一阶段到底按什么颗粒度贴参考 mod
2. 每个参考代码单元到底怎么实现
3. 哪些单元第一阶段必须先复刻稳，哪些本阶段禁止做
4. 后面什么条件下才允许迁到 `Microsoft Agent Framework`

固定规则：

1. 第一阶段不按空泛功能名拆，按 `参考 mod 真实代码单元` 拆。
2. 每个代码单元必须强制归类到以下 4 种实现结论之一：
   - `A 直接搬参考代码`
   - `B 包一层后搬参考代码`
   - `C 参考语义复刻，但必须重写`
   - `D 本阶段禁止做`
3. 第一阶段设计文档必须写清：
   - 参考分析文档
   - 参考 prompt
   - 参考源码文件
   - 当前项目落点
   - AFW 是否允许接管
4. 以后进入 `plan / tasks` 时，必须补齐参考源码 `文件 + 行号`。
5. 不允许再写“尽量贴近”“优先参考”这种口号话。

## 2. 三阶段硬规则

### 2.1 第一阶段

固定目标：

- 先把 `GGBH_OpenAIWorld` 的成熟业务语义复刻稳

固定规则：

1. 参考 mod 里的 `AI 链代码单元`，第一阶段统一按 `B 包一层后搬参考代码` 处理。
2. 这些单元的业务语义、字段形状、上下文组装方式、输出协议，除非写进本文 `受控偏离登记` 并获批准，否则必须按参考 mod 原语义保留。
3. 这些单元的执行位置必须改到 `Cloud`。
4. 第一阶段生产主链不允许 `AFW` 进入。
5. 第一阶段只允许预留：
   - `session / workflow interface`
   - `candidate generation interface`
   - `memory planning interface`
   - `tool registry interface`
6. 第一阶段不允许一边做参考复刻，一边把生产主链偷偷换成 AFW。

### 2.2 第二阶段

固定目标：

- 只迁那些已经被参考复刻版证明跑通的 `Cloud` 单元

固定规则：

1. 某个 `Cloud` 单元只有在满足以下条件后，才允许迁到 AFW：
   - 已经有一版 `非 AFW 的参考复刻版`
   - 已跑通
   - 已验收
   - 已留证据
   - 已明确当前单元的 streaming / pseudo-streaming 契约是否存在，以及迁移后如何保留
2. 迁移时允许短时间双实现并存。
3. 同一时刻只允许一条生产主链。
4. AFW 版验收通过后，旧的参考复刻版必须立即退役。
5. 不允许长期双轨。
6. 若被迁移单元当前已经承担玩家可见文本快反馈，AFW 版必须保留同等级的 streaming / pseudo-streaming 能力，不允许回退成整包返回。

### 2.3 第三阶段

固定目标：

- 让 AFW 逐步接管适合 agent 化的 `Cloud` 编排单元

允许 AFW 接管的典型范围：

1. workflow orchestration
2. candidate generation
3. memory planning
4. group / propagation / world 的编排协作
5. agent NPC 的云端工作流
6. 已有正式 streaming 契约的文本 candidate generation，但必须保留现有 stream chunk 语义

禁止 AFW 接管的范围：

1. `Launcher` readiness verdict
2. entitlement / listing / claim / billing 真相
3. `Runtime.Local` deterministic gate authority
4. `Game Mod` 最终宿主写回
5. 宿主 authoritative mutation

## 3. 代码单元表

| 代码单元 ID | 代码单元名 | 参考分析文档 | 参考 prompt / 配置 | 参考源码锚点 | 第一阶段结论 | 当前项目落点 | 第一阶段怎么实现 | 第二阶段 AFW | AFW 禁止接管点 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `U1` | Prompt 资产正文与变量槽位合同 | `10_共用系统/01_整体架构与主链路.md`、`02_Prompt体系与角色卡机制.md` | `decoded_prompts/**` | `obfuscated_ns/O.cs:12717-12840` | `A` | `Cloud` prompt asset store | 直接复刻 prompt 正文、变量槽位、模板分层；只改存放位置，不改业务语义 | 第二阶段可让 AFW 消费，不接管资产正本 | prompt 资产真源 |
| `U2` | AI 服务方案 / 会话编排 / 输入组装 | `10_共用系统/01_整体架构与主链路.md`、`03_AI请求层_服务配置与能力探测.md`、`04_AI路由_会话能力与通道编排.md` | `对话.md`、`群聊.md`、`传音群聊.md`、`世界推演.md` | `AIAgents.cs`、`AIServer.cs`、`AIServerScheme.cs`、`LLMSession.cs`、`obfuscated_ns/Y.cs:11530-12121` | `B` | `Cloud` orchestration | 保留参考 mod 的服务方案、会话拼装、消息角色分层与输出结构；执行位置改到 Cloud | 第二阶段允许迁 AFW | prompt 真源、产品真相 |
| `U3` | 消息主档 + JSON sidecar 骨架 | `10_共用系统/05_消息模型_通信通道与消息持久化.md` | `对话.md`、`群聊.md` | `MessageData.cs`、`PrivateMessageData.cs`、`GroupMessageData.cs`、`ContactGroupMessageData.cs`、`ContactGroup.cs`、`obfuscated_ns/F.cs:13502-13520` | `A` | `Cloud` canonical chat truth + title read model | 数据结构和来源标记直接复刻；把正式正本留在 Cloud，本地只留投影和 ref | 第二阶段可让 AFW 使用，不接管真相源 | 聊天正本 |
| `U4` | 月度记忆压缩与长期经历回灌 | `10_共用系统/06_关系_记忆与摘要机制.md` | `记忆压缩.md` | `ExperienceData.cs`、`obfuscated_ns/g.cs:10489-10815,11144-11164` | `B` | `Cloud` memory pipeline | 保留按时间桶压缩、摘要回灌、原始经历与摘要分层；执行位置改到 Cloud | 第二阶段允许迁 AFW memory planning | 记忆正本 |
| `U5` | 行为协议 / 解析 / 修补 / 归一 | `10_共用系统/07_行为协议_解析与执行.md` | `行为指令.md` | `PatchClass.cs:234-254`、`obfuscated_ns/A.cs`、`R.cs`、`i.cs` | `B` | `Cloud candidate schema` + `Runtime.Local gate` | Cloud 复刻动作协议语义和结构化输出；本地只做结构修补、白名单和风险拦截，不改意思 | 第二阶段允许迁 AFW candidate workflow，不允许抢 gate | deterministic gate authority |
| `U6` | 群聊参与者 / 发言顺序 / 线程编排 | `20_玩法功能/02_群聊功能.md`、`10_共用系统/05_消息模型_通信通道与消息持久化.md` | `群聊.md`、`群聊_发言顺序引导.md`、`传音群聊.md` | `GroupMessageData.cs`、`obfuscated_ns/Y.cs`、旧稿 `GROUP_CHAT_LOGIC_ANALYSIS.md` | `B` | `Cloud` group orchestration + `Adapter` thread mapping | 云端复刻参考 mod 的 speaker order、线程语义、群历史和回流逻辑；本地只校验和映射 | 第二阶段允许迁 AFW group workflow | 群历史真相、最终落地 |
| `U7` | 传播协议与再入链 | `20_玩法功能/05_信息裂变与社会传播.md`、`10_共用系统/07_行为协议_解析与执行.md` | `信息裂变.md` | `decoded_prompts/信息裂变.md`、旧稿 `INFORMATION_PROPAGATION_CHAIN.md` | `B` | `Cloud` propagation orchestration + `Adapter` carrier mapping | 先复刻 `ConveyMessage` 协议、目标选择和 hop 语义；本地只做合法性、可达性和通道裁决 | 第二阶段允许迁 AFW propagation workflow | 通信载体落地、第三方宿主写回 |
| `U8` | 世界推演 / 世界事件创建语义 | `20_玩法功能/07_主动世界演化.md`、`10_共用系统/08_世界事件数据模型与落地机制.md` | `世界定义.md`、`世界推演.md` | `PatchClass.cs:145-182`、`obfuscated_ns/O.cs:13344,13481,13569-13695`、`WorldEventData.cs`、`MapEventData.cs` | `B` | `Cloud` world orchestration + `Adapter/Mod` world apply | 宿主负责触发节拍，Cloud 复刻推演语义，Adapter/Mod 按支持矩阵决定怎么落 | 第二阶段允许迁 AFW world workflow | 宿主节拍、世界对象最终创建 |
| `U9` | 自定义物品 / 状态 / NPC 生成语义 | `20_玩法功能/13_自定义物品生成.md`、`14_自定义状态_气运生成.md`、`15_角色卡与NPC创建.md` | `自定义物品生成_*.md`、`自定义状态生成.md`、`角色卡模板.md` | `obfuscated_ns/O.cs:13579-13671`、`P.cs:10130,10168`、`Patch_ConfRoleCreateFeature.cs` | `B` | `Cloud` object proposal + `World` chain apply | 对象生成默认挂在世界事件上下文；社交链只允许提议，不允许直接创建 | 第二阶段允许迁 AFW object workflow | 最终创建权、支持矩阵硬拦截 |
| `U10` | 宿主 hook / UI 注入 / 最终写回 | `10_共用系统/09_宿主挂点_触发面与回写边界.md`、各玩法正文 | title-local UI / surface prompts | `PatchClass.cs`、`Patch_UINPCInfo.cs`、`Patch_ConfRoleCreateFeature.cs` | `C` | `Runtime.<game> Adapter` + `Game Mod` | 只复刻触发点、surface、提交语义；因为宿主不同，这部分必须重写 | 不迁 AFW | 宿主 authoritative mutation |
| `U11` | Launcher / Supervisor / 支持与帮助闭环 | `2026-04-07-superpowers-launcher-and-pack-appendix.md`、平台控制面旧附件 | 产品包 contract / support 流程 | 本项目现有 Launcher / Supervisor 代码，不取参考 mod 源码 | `C` | `Launcher` + `Launcher.Supervisor` | 这是我们自己的产品前台与本地管家，不从参考 mod 直接搬 | 不迁 AFW | readiness verdict、账号/权益/支持闭环 |
| `U12` | Agent NPC 自主多 Agent 协作 | 不在参考 mod 当前成熟主链 | 第三阶段单独立项的 agent 资产方案 | 不属于当前参考 mod 成熟源码锚点 | `D` | 第三阶段 `Cloud` | 第一阶段和第二阶段都不做生产实现 | 第三阶段目标 | 当前全部禁止 |

## 4. 受控偏离登记

### 4.1 角色创建双入口收口

参考 mod 味道：

- `CreateCharacter` 同时带有：
  - 行为协议入口
  - 世界演化入口

当前方案批准的偏离：

1. 社交/行为链可以提出 `创建角色` 候选
2. 正式创建权统一收口到 `世界主链`
3. 原因：
   - 不允许 authority 分裂
   - 不允许多条主链各自长一套对象创建落地逻辑

偏离类别：

- `controlled deviation`

## 5. 进入 plan / tasks 时的硬规则

以后进入 `plan / tasks` 时，每个任务必须补齐：

1. 参考分析文档路径
2. 参考 prompt 路径
3. 参考源码文件
4. 参考源码行号
5. 当前实现结论 `A / B / C / D`
6. 当前项目落点
7. 旧实现退役条件
8. 如果涉及 AFW：
   - 当前是否允许迁
   - 迁移前置验收证据
   - 旧实现退役时点

不允许只写：

- “参考某功能”
- “按参考 mod 复刻”
- “后续再迁 AFW”

这些空话。
