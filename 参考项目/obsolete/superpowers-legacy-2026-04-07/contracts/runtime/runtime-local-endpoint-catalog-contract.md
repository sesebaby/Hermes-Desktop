# Runtime.Local Endpoint Catalog Contract

状态：

- active design baseline

owner：

- runtime architecture owner

用途：

- 用大白话登记 `Runtime.Local` 正式有哪些入口、每个入口谁能调、进来后干什么、结果谁说了算。

固定回链：

- `docs/superpowers/contracts/runtime/runtime-local-vs-title-adapter-boundary-contract.md`
- `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

调用方固定：

- `Game Mod`
- `Launcher.Supervisor`（仅 health / readiness / recovery 相关）

正式 endpoint 目录：

1. `POST /runtime/{gameId}/private-dialogue`
   - 用途：
     - 接 `private_dialogue` 事实包
     - 调 `Cloud`
     - 跑共享 gate
     - 回传待宿主显示的结果
2. `POST /runtime/{gameId}/private-dialogue/stream`
   - 用途：
     - 接 `private_dialogue` 事实包
     - 调 `Cloud candidate-stream`
     - 以 streaming / pseudo-streaming 方式把玩家可见文本增量回给 `Game Mod`
3. `POST /runtime/{gameId}/private-dialogue/{canonicalRecordId}/finalize`
   - 用途：
     - 宿主显示完成后提交 finalize
     - 升 committed / render_failed
4. `POST /runtime/{gameId}/private-dialogue/{canonicalRecordId}/recover`
   - 用途：
     - 对 pending_visible 做恢复
5. `POST /runtime/{gameId}/remote-direct`
   - 用途：
     - 接手机私信线程/发言请求
     - 只返回正式 channel result
6. `POST /runtime/{gameId}/group-chat-turn`
   - 用途：
     - 接现场或远程群聊单轮请求
7. `POST /runtime/{gameId}/thought-preview`
   - 用途：
     - 接当前想法预览请求
     - 只回 preview，不冒充普通私聊 committed
8. `POST /runtime/{gameId}/npc-panel-bundle`
   - 用途：
     - 接 NPC 信息面板请求
     - 回基础资料、关系、记忆摘要、物品 tab、群聊历史 disclosure
9. `POST /runtime/{gameId}/phone-contacts`
   - 用途：
     - 接手机联系人列表请求
     - 回联系人卡片和当前 availability
10. `GET /runtime/{gameId}/health`
   - 用途：
     - 给 `Launcher.Supervisor` 读 runtime health

路径统一规则：

1. 正式路径必须带 `{gameId}`
2. capability 不准靠 path 命名一半、body 再命名一半
3. 不允许保留“有的 endpoint 带游戏名，有的不带”的混搭

request envelope minima：

- `requestId`
- `traceId`（若调用方未传，由 `Runtime.Local` 补）
- `gameId`
- `channelType`
- `capability`
- `requestFamily`

response envelope minima：

- `requestId`
- `traceId`
- `gameId`
- `channelType`
- `capability`
- `reasonCode`
- `statusCode`
- `failureClass`
- `recoveryEntryRef`

stream response minima：

- `requestId`
- `traceId`
- `gameId`
- `channelType`
- `capability`
- `streamChunkDtoName = RuntimeDialogueStreamChunkDto`
- `streamChunk`
  - `chunkType`
  - `sequence`
  - `visibleTextDelta`
  - `isTerminal`
  - `failureClass`（仅失败时）
  - `reasonCode`（仅失败时）

finalize-only minima：

- `canonicalRecordId`
- `surfaceStatus`
- `hostEvidenceRef`
- `surfaceId`
- `transactionCommitmentUpdates`（条件适用）

authority rule：

1. 所有 runtime endpoint 的最终 `statusCode / failureClass / commitOutcome` owner 固定是 `Runtime.Local`
2. `Game Mod` 只能报告：
   - 宿主显示成功没
   - 宿主写回成功没
   - 宿主证据 ref
3. `Runtime.<game> Adapter` 只能报告 title-local blocked / mapping result
4. `stream` endpoint 只负责前台快反馈，不负责单独宣布 committed

streaming rule：

1. 当前 mandatory streaming 入口先固定在：
   - `POST /runtime/{gameId}/private-dialogue/stream`
2. `stream` 只允许用于纯文本玩家可见面。
3. `stream` chunk 可以驱动：
   - thinking
   - incremental text render
   - explicit failure copy
4. `stream` chunk 不得驱动：
   - item committed
   - relation committed
   - world event committed

health endpoint rule：

1. `GET /runtime/{gameId}/health` 只返回运行事实
2. 不单独长第二套 readiness verdict
3. readiness 真相仍在 `Launcher.Supervisor`

retirement rule：

以下旧风格路径进入退役名单：

1. `/runtime/stardew/private-dialogue`
2. `/runtime/stardew/private-dialogue/{canonicalRecordId}/finalize`
3. `/runtime/remote-direct`
4. `/runtime/group-chat`
5. `/runtime/thought`

退役完成标准：

1. 调用方已迁到正式路径
2. 旧路径不再进入正式成功链
3. 旧路径保留时也必须只回明确退役错误

update trigger：

- endpoint 目录变化
- 路径规范变化
- finalize / recover 输入变化
- health 输出变化
