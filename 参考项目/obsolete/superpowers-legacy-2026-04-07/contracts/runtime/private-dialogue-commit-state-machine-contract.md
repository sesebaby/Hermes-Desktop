# Private Dialogue Commit State Machine Contract

状态：

- active design baseline

owner：

- runtime architecture owner
- cloud canonical-history owner

用途：

- 用大白话写死：一轮私聊从请求到 committed，到底经过哪些状态，谁能推进，谁不能越权。

固定回链：

- `docs/superpowers/contracts/runtime/private-dialogue-request-contract.md`
- `docs/superpowers/contracts/runtime/hosted-narrative-endpoint-contract.md`
- `docs/superpowers/contracts/runtime/canonical-history-sourcing-contract.md`
- `docs/superpowers/contracts/runtime/deterministic-command-event-contract.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`

state owner：

- `Mod`
  - 只拥有宿主触发与宿主显示事实
- `Runtime.Local`
  - 拥有状态晋升仲裁权
- `Cloud`
  - 拥有 candidate / pending / committed canonical record 正本

固定状态：

1. `request_received`
2. `fact_package_validated`
3. `candidate_generated`
4. `pending_visible`
5. `committed`
6. `render_failed`
7. `recovered`
8. `rejected`

状态迁移：

- `request_received -> fact_package_validated`
- `fact_package_validated -> candidate_generated`
- `candidate_generated -> pending_visible`
- `pending_visible -> committed`
- `pending_visible -> render_failed`
- `render_failed -> recovered`
- `request_received -> rejected`
- `fact_package_validated -> rejected`
- `candidate_generated -> rejected`

每步 owner：

1. `request_received`
   - owner：`Runtime.Local`
2. `fact_package_validated`
   - owner：`Runtime.Local`
3. `candidate_generated`
   - owner：`Cloud`
4. `pending_visible`
   - owner：`Cloud`
   - 但必须由 `Runtime.Local` 发起
   - 含义：
     - 玩家已经可以在宿主对话面看到这轮私聊文本
     - 但这轮还不是正式 committed
5. `committed`
   - owner：`Runtime.Local finalize verdict + Cloud canonical-history record`
   - 解释：
     - `Runtime.Local` 负责作出“这轮能不能升 committed”的最终 verdict
     - `Cloud` 负责把 canonical record 真正从 `pending_visible` 升成 `committed`
6. `render_failed`
   - owner：`Runtime.Local finalize verdict`
7. `recovered`
   - owner：`Runtime.Local recover verdict`
8. `rejected`
   - owner：`Runtime.Local` 或 `Cloud`，按拒绝发生位置决定

committed 条件：

1. 宿主可见 surface 已成功显示
2. `Game Mod` 已提交宿主证据 ref
3. `Runtime.Local` finalize 通过
4. `Cloud` 已把 canonical record 从 `pending_visible` 升到 `committed`

pending_visible 条件：

1. `Cloud` 已返回可显示文本
2. 当前 surface 属于纯文本回复面
3. 这轮还没有被宣称为 committed
4. 不得借 `pending_visible` 冒充：
   - 物品已发放
   - 关系已变化
   - 事件已落地

render_failed 条件：

1. 宿主显示失败
2. 或宿主 finalize 明确回了失败
3. 这时 canonical record 不能升 committed
4. 当前 title 的玩家可见面必须明确提示失败，不允许把失败伪装成正常剧情成功

recovered 条件：

1. 当前轮先前已进入 `render_failed`
2. recover 路径成功
3. recover 后允许进入 replay-eligible 状态

rejected 条件：

1. facts 缺字段
2. deterministic validation 不通过
3. candidate 不合法
4. finalize 请求自相矛盾

绝对禁止：

1. `Cloud` 在 `candidate_generated` 后直接升 committed
2. `Game Mod` 直接告诉 `Cloud` committed
3. `pending_visible` 结果进正式记忆
4. `render_failed` 结果进正式聊天回放

required audit joins：

- `requestId`
- `traceId`
- `canonicalRecordId`
- `historyOwnerActorId`
- `surfaceId`
- `hostEvidenceRef`
- `promptAuditRef`
- `chatCanonicalRef`

update trigger：

- 状态集合变化
- 状态晋升 owner 变化
- committed/render_failed/recovered 条件变化
