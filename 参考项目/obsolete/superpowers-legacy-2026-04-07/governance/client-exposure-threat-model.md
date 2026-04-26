# Client Exposure Threat Model

状态：

- active design baseline

owner：

- product owner

co-approval：

- runtime architecture owner
- host-governance authority

用途：

- 明确客户端分发代码一旦落地到玩家机器，就必须假设可被反编译、注入、抓包、重放与资源提取
- 明确 `.NET` 在本项目里是业务开发层，不是商业机密保密层
- 明确哪些资产绝不能下发到客户端、哪些只能以下降级形式存在、哪些即使泄漏也不构成核心商业损失
- 明确真正需要保护的不只是 prompt pack，还包括完整 narrative orchestration truth source
- 为 `M1` 及后续商业化包提供 ship-gate 级别的客户端暴露防护基线

核心假设：

- 客户端程序集、资源包、缓存文件、日志、sidecar、诊断导出、内存快照都默认可被本地拥有者读取
- `.NET` assembly、配置文件、嵌入资源与普通字符串混淆都只能提高时间成本，不能构成可信保密边界
- 任何已经下发到玩家机器的完整明文 prompt、world-rule、商业策略、密钥、entitlement policy、orchestration rule chain，最终都应视为可泄漏
- 因此真正值钱且不可复制的商业资产，必须通过服务端真源、可撤销授权和受控导出策略保护，而不是指望客户端不可逆

主要攻击面：

- 静态反编译：反编译 `.NET` assembly、读取嵌入资源、提取字符串常量、恢复协议与分层关系
- 动态旁路：hook 本地 runtime、注入日志、抓取 prompt resolution 结果、抓取服务端返回的 orchestration result、导出内存中的 pack 内容
- 配置伪造：篡改本地 profile、claim copy、runtime flags、premium 开关、实验开关
- 凭据滥用：重放旧 token、复制本地授权缓存、伪造 entitlement 通过路径
- 诊断泄漏：从 checkpoint、telemetry、diagnostic sidecar、crash dump、导出包中恢复 proprietary asset
- 资源复用：直接搬运 prompt / persona / world-rule pack、编排图、speaker-selection 规则、媒体模板、商业 copy、托管策略与 capability 配置

资产分级：

- `hosted-only`
  - 完整 prompt / persona / world-rule pack
  - 完整 narrative orchestration truth source
  - long-term memory truth source 与 canonical recent-history store
  - premium 媒体工作流策略
  - entitlement policy、sellability policy、计费规则、风控策略
  - 签名私钥、服务端路由密钥、托管供应商凭据
- `developer-only`
  - 完整 rendered prompt
  - 全量 trace replay
  - 带可逆上下文的 sidecar 调试包
  - 用于定位问题的高敏感诊断快照
- `client-safe-structured`
  - 玩家端 UI 绑定、宿主适配器、deterministic execution adapter
  - 已脱敏的 context digest
  - 结构化事实包字段、结构化结果 schema、最小必要 raw channel input 生成逻辑
  - 已签名 profile、manifest、feature flag
  - 对玩家体验必需但泄漏后不会直接复制核心商业能力的最小数据

绝不下发到客户端的内容：

- `hosted-only` prompt asset 明文
- `hosted-only` orchestration asset 明文
- entitlement / sellability / premium unlock 的最终政策真源
- 商业侧 claim policy、waiver 审批权、计费账本真源
- 可直接复用的托管工作流模板、媒体生成配方、供应商密钥
- 可让第三方独立复刻完整体验的 pack 组合、覆盖规则、orchestration rule chain、运营策略全集

可下发但必须假设会泄漏的内容：

- 本地 runtime 可执行逻辑
- 玩家端 UI copy
- 本地缓存的已签名 manifest
- 为单轮调用准备的 canonical snapshot、结构化事实包与经批准的最小 raw channel input

固定补充：

- prompt 资产明文不属于 `client-safe-structured`
- 记忆明文正本不属于 `client-safe-structured`
- 本地正式主线只能持有结构化事实包与结构化结果，不得持有完整 prompt 正文

推荐分层：

- `Cloud`
  - 持有每个游戏自己的 prompt 资产明文、聊天正本、记忆正本、审计明文
  - 承担 prompt 编排、provider 通信、entitlement enforcement、sellability policy 执行、撤销与轮换入口
- `Launcher`
  - 作为完整玩家前台，承接账号、产品与兑换、通知、支持与帮助
- `Launcher.Supervisor`
  - 作为本地 readiness verdict、前置检查、修复与 runtime 拉起 owner
- `Runtime.Local`
  - 作为结构化事实包入口、deterministic gate、trace、health、recovery owner
- `Runtime.<game> Adapter`
  - 负责事实冻结、字段映射、执行清单翻译
- `Game Mod`
  - 负责宿主 UI、宿主取数、最终宿主写回
- `Host Game`
  - 作为最终被读取和被改动的真实对象
- `Prompt Asset Governance`
  - 通过 `hosted-only / developer-only / client-safe-structured` 三分层决定 narrative asset 能否下发，而不是由实现语言决定

`.NET` 使用原则：

- 可以继续作为主业务开发框架
- 不允许把“代码不易逆向”当成商业保护前提
- 可以使用 `NativeAOT`、混淆、资源分段、签名校验、反篡改与字符串最小化来提高攻击成本
- 以上措施只属于 cost-raising layer，不能替代服务端真源、授权撤销、资产分级与导出治理

`M1` 最低防护基线：

- shipped client 不得包含 `hosted-only` prompt asset 明文
- shipped client 不得包含 `hosted-only` orchestration asset 明文
- shipped client 不得包含完整记忆明文正本
- shipped client 不得包含 entitlement policy 真源，只能消费已签名结果
- premium 媒体能力不得通过本地 flag 或本地资源切换直接解锁
- AFW checkpoint / telemetry / diagnostic sidecar 默认只能保留 redacted digest 与 policy-safe metadata
- crash dump、日志、导出包必须经过 prompt-asset redaction
- 本地 manifest、profile、feature toggle 必须可校验签名或可回链到服务端版本
- 泄漏后必须具备 pack 轮换、token 撤销、版本失效与策略下线能力
- `user_byok` 允许把用户 key 临时提交到服务端代表用户发起基础叙事调用，但不得因此把完整编排链或 provider 直连主线下放到客户端

`groupHistoryDisclosureState` freeze：

- 当前只允许：
  - `open_for_player`
  - `not_open_for_player`
- owner 固定为当前 build / title exposure config
- UI、Runtime、Mod 都只能消费同一份 `groupHistoryDisclosureState`
- 不得在客户端派生第二套 disclosure truth

推荐控制措施：

- 政策控制：
  - sellability、listing、entitlement policy 固定由产品 artifact 决定，客户端只拿执行结果
- 资产控制：
  - 完整 prompt pack 与 orchestration rule chain 只在 hosted path 或 developer-only 受控流程可见
- 协议控制：
  - 客户端只消费最小必要的 signed manifest、claim result、route instruction 与当前轮 orchestration result
- 存储控制：
  - 本地缓存默认只存 digest、hash、version、签名与最小必要绑定，不存完整敏感正文
- 导出控制：
  - checkpoint、telemetry、sidecar、diagnostic export 必须有 redaction policy
- 运营控制：
  - 任何已泄漏 asset 必须可通过 version revoke、token revoke、policy rotate 快速失效

基础包与高级包建议：

- 基础包：
  - 基础叙事能力默认走统一的服务端编排链
  - 可允许客户端保留宿主适配逻辑、deterministic execution 与最小输入整理逻辑
  - 但不应让“使基础包真正有差异化价值”的完整 prompt 资产、编排规则、商业策略与可运营规则全部落地
  - 当前 `M1` 下，`user_byok` 与未来 `platform_hosted` 必须保持分离文本路径与路由语义，只能共享受控基础设施，不得提前合并成同一产品路径
- 高级包：
  - `AI voice`、`AI image`、`AI video` 必须采用 `platform_hosted` 路径
  - 高成本能力的路由、成本控制、限额、供应商策略必须保持服务端真源

ship-gate evidence：

- 当前 client asset inventory
  - `docs/superpowers/governance/evidence/client-package-check.md`
- 当前 prompt / orchestration asset classification 清单
  - `docs/superpowers/governance/evidence/prompt-asset-protection.md`
- 样例 checkpoint / telemetry / sidecar / crash dump redaction evidence
  - pending fresh artifact redaction evidence
- entitlement enforcement path 证明
  - `tests/Superpowers.Runtime.Tests/Integration/ReadinessRecoveryIntegrationTests.cs`
- hosted-only asset 未随客户端分发的打包检查证据
  - working baseline recorded in `docs/superpowers/governance/evidence/client-package-check.md`; final hosted-only non-distribution adjudication remains pending
- `user_byok` 临时凭据处理与清理证据
  - pending explicit runtime/cloud publish verification
- 泄漏后 revoke / rotate runbook
  - pending RC review artifact

阻断条件：

- 任何 `hosted-only` asset 被打入 shipped client
- 任何 checkpoint / telemetry / sidecar 默认暴露完整 proprietary prompt asset 或完整 orchestration rule chain 明文
- entitlement / premium unlock 只能靠本地开关成立
- 无法证明 leaked build 可以通过 revoke / rotate 被快速降级或失效
