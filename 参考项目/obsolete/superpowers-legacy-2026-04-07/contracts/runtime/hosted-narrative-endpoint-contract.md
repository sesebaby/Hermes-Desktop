# Hosted Narrative Endpoint Contract

状态：

- active design baseline

owner：

- cloud orchestration owner
- runtime architecture owner

用途：

- 用大白话写死：`Runtime.Local` 调 `Cloud Hosted Narrative` 时，正式能调哪些入口，每个入口交什么、回什么、谁有权升级成 committed。

固定回链：

- `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
- `docs/superpowers/contracts/runtime/cloud-prompt-assembly-contract.md`
- `docs/superpowers/contracts/runtime/canonical-history-sourcing-contract.md`
- `docs/superpowers/contracts/runtime/runtime-local-endpoint-catalog-contract.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`

调用方固定：

- 只允许 `Runtime.Local` 内部服务调用

正式 endpoint 目录：

1. `POST /narrative/{gameId}/private-dialogue/candidate`
   - 用途：
     - `Cloud` 接事实包
     - 云端自己编排 prompt
     - 自己调 provider
     - 回结构化 candidate
2. `POST /narrative/{gameId}/private-dialogue/candidate-stream`
   - 用途：
     - `Cloud` 接事实包
     - 云端自己编排 prompt
     - 自己调 provider
     - 以 streaming / pseudo-streaming 方式持续回玩家可见文本增量
3. `POST /narrative/{gameId}/private-dialogue/pending`
   - 用途：
     - `Runtime.Local` 把 accepted candidate 提交成 `pending_visible`
4. `POST /narrative/{gameId}/private-dialogue/{canonicalRecordId}/finalize`
   - 用途：
     - `Runtime.Local` 把宿主 finalize 结果回给 `Cloud`
5. `POST /narrative/{gameId}/private-dialogue/{canonicalRecordId}/recover`
   - 用途：
     - 恢复 `pending_visible`
6. `GET /narrative/{gameId}/memory/{actorId}/snapshot`
   - 用途：
     - 给 `Runtime.Local` 或 title adapter 读 canonical memory snapshot

candidate request minima：

- `requestId`
- `traceId`
- `gameId`
- `channelType`
- `capability`
- `requestFamily`
- `factPackageRef` 或完整事实包对象

candidate response minima：

- `requestId`
- `traceId`
- `gameId`
- `capability`
- `providerId`
- `modelId`
- `candidate`
  - `content`
  - `rawText`
  - `normalizedObject`
  - `actions`
  - `referenceActionTrace`
  - `choices`
- `promptAuditRef`

固定说明：

1. `candidate.content / rawText` 是允许前台尽快显示的玩家可见文本载荷。
2. 它不等于 committed。
3. 它也不代表会改宿主状态的动作已经成立。
4. endpoint 不得把以下内容作为前台提速载荷回给客户端：
   - 完整 rendered prompt
   - 记忆选取正文
   - 规则链正文

candidate-stream response minima：

- `requestId`
- `traceId`
- `gameId`
- `capability`
- `streamChunkDtoName = HostedNarrativeStreamChunkDto`
- `streamChunk`
  - `chunkType`
  - `sequence`
  - `visibleTextDelta`
  - `isTerminal`
  - `failureClass`（仅失败时）
  - `reasonCode`（仅失败时）

`chunkType` 固定枚举：

- `waiting`
- `delta`
- `completed`
- `failed`

固定规则：

1. `candidate-stream` 只允许承载玩家可见文本增量和等待态。
2. 不允许在 stream chunk 里夹带：
   - prompt 正文
   - 记忆正文
   - 规则链正文
   - 已确认的宿主状态变更
3. `failed` chunk 只表示当前玩家可见文本链失败，不等于 canonical committed 失败已经完成全部治理收口。

pending request minima：

- `requestId`
- `traceId`
- `gameId`
- `channelType`
- `capability`
- `historyOwnerActorId`
- `canonicalRecordId`
- `narrativeTurnId`
- `content`
- `acceptedActions`
- `acceptedDeterministicOutcomes`
- `providerRawText`
- `providerNormalizedObject`
- `referenceActionTrace`
- `choices`

pending response minima：

- `canonicalRecordId`
- `replayState = pending_visible`
- `replayEligible`
- `mirroredWriteback`

固定说明：

1. `pending_visible` 的作用是：
   - 让纯文本可见面先显示
2. 它不是最终 committed 结果。
3. 它不得被 title UI copy 渲染成“物品已经给出”“关系已经变化”“事件已经发生”。

finalize request minima：

- `canonicalRecordId`
- `surfaceStatus`
- `hostEvidenceRef`
- `surfaceId`
- `transactionCommitmentUpdates`（条件适用）

finalize response minima：

- `canonicalRecordId`
- `replayState`
- `replayEligible`
- `mirroredWriteback`
- `outcome`
  - `commitOutcome`
  - `reasonCode`
  - `statusCode`
  - `failureClass`
  - `recoveryEntryRef`

authority rule：

1. `candidate` owner 是 `Cloud`
2. `pending_visible` owner 也是 `Cloud`
3. 是否能从 `pending_visible` 升成 `committed`，必须听 `Runtime.Local finalize`
4. `Cloud` 不允许在没收到 finalize 成功前，自己把聊天/记忆升 committed

security rule：

1. Hosted Narrative endpoint 只允许内部 token 调用
2. 不对玩家客户端直接开放
3. 不对 `Game Mod` 直接开放

forbidden paths：

1. 不允许 `Cloud` 正式接受本地已经拼好的最终 prompt payload 当 candidate 主输入
2. 不允许 `Runtime.Local` 绕过 `candidate` 直接往 `pending` 塞一份没 provider trace 的结果
3. 不允许 finalize 只传 `displayed = true/false`，却没有宿主证据 ref
4. 不允许为了缩短链路，把 `Cloud` 已编排好的最终 prompt 正文回传客户端再让客户端直连 provider

retirement rule：

以下旧式入口进入退役名单：

1. `/narrative/private-dialogue/provider-candidate`
2. `/narrative/private-dialogue`
3. `/narrative/private-dialogue/{canonicalRecordId}/finalize`
4. `/narrative/private-dialogue/{canonicalRecordId}/recover`
5. `/narrative/internal/memory/{actorId}/snapshot`

原因：

1. 路径没带 `{gameId}`
2. `provider-candidate` 暗示本地先拼 prompt，再让云端代发
3. `internal/memory` 命名泄露实现细节，不是正式能力合同命名

update trigger：

- endpoint 目录变化
- candidate/pending/finalize 输入输出变化
- internal auth 规则变化
- canonical memory 读取入口变化
