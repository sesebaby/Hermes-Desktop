# Current Phase Boundary

状态：

- active

workflow mode：

- solo operator
- manual verification only

effective date：

- 2026-03-29

current phase：

- `post-M1-reference-grade-hardening`

说明：

- 当前仓库按单人开发口径运行。
- 当前完整设计真相固定回链：
  - `docs/superpowers/specs/README.md`（目录入口）
  - `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
  - `docs/superpowers/specs/2026-03-27-superpowers-master-design.md#13`（正式附件注册表）
- `M1-source-faithful` 的首轮主链搭建已经基本完成；当前阶段不再继续把“是否还是 M1”作为执行口径，而是进入 `post-M1-reference-grade-hardening`。
- 当前阶段的重点不是继续扩功能面，而是把已经进入首发主链的能力做 reference-grade 收口、修掉旧口径冲突，并补齐真实玩家可见证据。
- 完整设计已经纳入 `group_chat / remote / propagation / world_event / object_generation / tool_entry` 等全量能力；但当前 phase 只限制“当前实现、当前证据、当前 claim”范围，不改写完整设计覆盖面。
- 当前仓库仍在开发阶段，允许破坏式重构；当前阶段不把“保住旧实现继续运行”当成目标。
- 当前处理历史实现的固定口径是：
  - 旧业务主链若与现行设计冲突，直接退役
  - 只保留还能当宿主壳、桌面壳、审计壳的部分
  - 不为历史错误边界补兼容层
- 当前 phase 的实施过滤、施工顺序、验收口径，统一回链：
  - `docs/superpowers/specs/attachments/2026-04-07-superpowers-phase-backlog-and-delivery-appendix.md`
- 当前阶段不做独立 reviewer、产品审批、host/runtime 审批、`RC / GA` 放行治理。
- 当前文档里的 `claim / waiver / review / gate` 相关载体只保留为未来多人或商业化时的参考骨架，不再作为当前收尾阻塞条件。
- 当前唯一有效的完成标准是：代码范围正确、本地验证通过、手动检查有记录。

hard redlines：

- 当前正式主链固定是 7 层：
  - `Cloud -> Launcher -> Launcher.Supervisor -> Runtime.Local -> Runtime.<game> Adapter -> Game Mod -> Host Game`
- 当前阶段继承上一阶段已经确认的首发主链：`dialogue + memory + social transaction / commitment` 仍为强制收口范围。
- `group_chat`、title-local `remote_direct_one_to_one`、`information_propagation`、`active_world` 不进入当前手动验收主闭环；其中前两者允许保留实现和测试，但不作为当前阶段完成判断的必需能力。
- 当前阶段仍以薄宿主、小协议、强恢复、软降级为优先。
- 不允许为了“未来平台化”改写当前 source-faithful 主链。
- 不允许为了保旧实现，继续让本地 prompt builder、本地 prompt 资产目录、本地 provider 语义留在正式主链里。
- 当前阶段允许以下 provider-backed 使用边界：
  - `private_dialogue` 作为真实 candidate generation 主路径
  - `memory` 作为 `derived monthly compression` 主路径
  - `thought` 只能复用 `private_dialogue` 的 `inner_monologue` 模式，不得单独长成新 authority
- 当前阶段的 provider-backed 放开只代表：
  - `Cloud` 读取 provider 配置并代表玩家或平台与 provider 通信
  - 每个游戏自己的 prompt 资产明文、聊天正本、记忆正本、审计明文固定在 `Cloud`
  - 默认阿里云 provider 可用于 `private_dialogue`
  - `memory` 可在 `Cloud` 侧走 provider-backed monthly compression，但仍然只属于 derived state
  - 本地正式主线固定只允许发送结构化事实包，不允许本地拼最终提示词
  - `Runtime.Local` 继续保留 canonical input builder、deterministic gate 与 final host mutation gate
- 当前阶段不代表：
  - 多 provider 动态切换已完成
  - `social transaction / commitment` 已全部 provider-backed
  - 当前首发主链已整体迁移为 provider-first 执行链
  - 平台控制面 provider 管理 UI 或 BYOK / hosted 双路径已落地
  - 本地直连 provider、Cloud 直接改宿主、或 Launcher 自己重算第二套 readiness 真相被允许

AFW-specific redlines：

- AFW session / checkpoint / memory provider 数据在当前阶段一律视为 `derived state`。
- AFW 不得拥有 runtime truth-source 或 authoritative writeback。
- 当前阶段不把 “AFW workflow 可恢复” 或 “AFW checkpoint resume” 写成产品承诺。

manual verification scope：

- `reference-grade hardening core`
  - `dialogue`
  - `memory`
  - `social transaction / commitment`
  - player-visible proof closeout
- supporting runtime path
  - local runtime startup
  - cloud control startup
  - hosted narrative path
  - Stardew mod publish/sync/load
- player-visible surfaces
  - launcher startup and key navigation
  - Stardew controlled in-host visible proof

manual verification rules：

- 当前完成判断只看 repo-local 事实：
  - `dotnet build`
  - automated tests
  - publish scripts
  - health checks
  - mod sync
  - SMAPI load
  - hosted narrative path
  - 手动或受控截图/日志证据
- 若某项没有实际跑过或没有证据，就记为 `pending`，但不引入额外审批流。
- player-visible surface 仍然必须保留这 4 类证据：
  - `startup proof`
  - `visible-surface proof`
  - `interaction proof`
  - `visual evidence ref`

current completion rule：

- 当前 `post-M1-reference-grade-hardening` 可被记为 `implementation complete, manually verified`，当且仅当：
  - `dialogue + memory + social transaction / commitment` 相关代码与测试通过；
  - `thought` 已按 `private_dialogue + inner_monologue` 口径收口，旧 sidecar / 旧文档口径不再互相冲突；
  - launcher/runtime/cloud/mod 的 build/publish/startup 路径通过；
  - 至少存在一套真实 player-visible 证据；
  - 已知限制被明确写入手动检查文档。
- 当前仓库不输出 `RC`、`GA`、`release approved`、`commercially approved` 这类结论。

out of scope for current workflow：

- 独立 sign-off
- commercial claim approval
- waiver approval
- `RC / GA` gate decision
- gate-time degraded window proof

superseded revisions：

- supersedes the previous governance-heavy phase boundary for the current solo/manual workflow
