# Remote Direct Request Contract

状态：

- active design baseline

owner：

- runtime architecture owner
- game integration owner

用途：

- 用大白话写死：远程一对一请求不是“随便打开个手机菜单”，它有正式线程键、可用性结果和历史来源。

固定回链：

- `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
- `docs/superpowers/contracts/runtime/stardew-remote-direct-thread-contract.md`
- `docs/superpowers/contracts/runtime/canonical-history-sourcing-contract.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

request family：

- `remote_direct`

base required fields：

- `requestId`
- `gameId`
- `actorId`
- `targetId`
- `channelType`
- `threadKey`
- `hostSummaryRef`
- `recentDirectHistoryRef`
- `availabilityState`
- `contactGroupId`

conditional fields：

- `summarySelectionHint`
  - title-local 想提示云端优先拿哪段摘要时可填

字段语义死规则：

1. `threadKey`
   - 正式一对一线程 key
2. `availabilityState`
   - 只允许：
     - `available_now`
     - `unavailable_now`
3. `contactGroupId`
   - 手机联系人/线程容器 id
4. `recentDirectHistoryRef`
   - 仍然回链 actor-owned private/direct canonical history

可用性规则：

1. 当前不可联系，就返回 `unavailable_now`
2. `unavailable_now` 不创建 pending visible turn
3. `unavailable_now` 不创建 deferred queue
4. `available_now` 才能继续进云端生成

绝对禁止：

1. UI 自己硬改 `availabilityState`
2. 本地为远程私信另存一套 authoritative history
3. 用 `contactGroupId` 代替 `threadKey`
4. 用 `threadKey` 代替 `historyOwnerActorId + canonicalRecordId`

stardew 首发映射：

- `threadKey = gameId + actorId + targetId + channelType`
- `channelType = remote_direct_one_to_one`

update trigger：

- 线程 key 规则变化
- availabilityState 枚举变化
- remote direct 历史来源变化
