# Runtime.Local vs Title Adapter Boundary Contract

状态：

- active design baseline

owner：

- runtime architecture owner
- game integration owner

用途：

- 用大白话写死：`Runtime.Local` 做共享门禁，`Runtime.<game>` 只做游戏翻译，谁都不准越界。

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
- `docs/superpowers/contracts/runtime/canonical-history-sourcing-contract.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

authoritative split：

- `Runtime.Local` 是共享 owner：
  - request envelope 校验
  - trace / health / recovery
  - fail-closed
  - deterministic validation
  - finalize outcome 仲裁
  - command / outcome audit
- `Runtime.<game> Adapter` 是 title-local owner：
  - 宿主事实采样字段定义
  - title-local refs
  - channel 差异翻译
  - host apply plan 翻译
  - surface / hook / support matrix 对应表

`Runtime.Local` 只准做：

1. 接统一 request family
2. 校验 base required fields
3. 生成统一 `traceId / requestId / commandId`
4. 调 `Cloud`
5. 校验 `Cloud` 返回的结构化 candidate
6. 跑共享 deterministic gate
7. 下发给 title adapter 做 host apply plan
8. 接 finalize / recover
9. 输出统一 committed / rejected / failed / recovered 结果

`Runtime.Local` 绝对不准做：

1. 拼最终 prompt
2. 管游戏专属 prompt 资产
3. 读写 canonical chat / memory 正本
4. 硬编码 Stardew 或其他游戏的宿主语义
5. 自己决定某个游戏的联系人线程、群聊线程、行程细节、物品模板细节

`Runtime.<game> Adapter` 只准做：

1. 把宿主事实映射成正式 fact package 字段
2. 把结构化 candidate 翻成 title-local host apply plan
3. 给出 title-local support state / blocked reason
4. 把 title-local surface id、hook id、carrier id 对齐到正式合同

`Runtime.<game> Adapter` 绝对不准做：

1. 直接调 provider
2. 直接写 canonical chat / memory
3. 自己宣布 committed
4. 自己持有最终 command state

shared vs title-local freeze：

- shared：
  - `requestId`
  - `traceId`
  - `launchSessionId`
  - `commandId`
  - `commitOutcome`
  - `reasonCode`
  - `failureClass`
  - `recoveryEntryRef`
  - `executionState`
- title-local：
  - `surfaceId`
  - `threadKey`
  - `groupSessionKey`
  - `contactGroupId`
  - `hostDialogueRecordRef`
  - `sceneSnapshotRef`
  - `relationSnapshotRef`
  - `host apply target refs`

host apply plan minima：

- `requestId`
- `traceId`
- `gameId`
- `channelType`
- `capability`
- `canonicalRecordId`
- `surfacePlan`
- `hostApplyPlan`
- `carrierPlan`
- `expectedFinalizePath`

边界判定死规则：

1. 只要某段逻辑需要知道“星露谷 NPC 今天睡觉了不能接电话”，它就属于 title-local
2. 只要某段逻辑需要知道“缺字段就拒绝、trace 怎么打、finalize 怎么升 committed”，它就属于 shared
3. 只要某段逻辑需要拼出自然语言 prompt，它就不属于本地 runtime 任一层

当前明确退役的越界做法：

1. `Runtime.Local` 直接调用 `StardewPrivateDialoguePromptBuilder`
2. `Runtime.Local` 直接依赖 `StardewPromptAssetCatalog`
3. `Runtime.Local` endpoint 自己补 Stardew title-local 文案和 fallback prompt
4. `Runtime.Local` 自己长成“懂全部游戏”的大泥球

review checklist：

1. 新共享代码是否引用了具体游戏 prompt builder
2. 新 title-local adapter 是否开始拥有 canonical history 写权
3. 新 endpoint 是否偷偷吞掉 title-local blocked reason
4. 新 finalize 逻辑是否绕过 `Runtime.Local`

update trigger：

- shared/title-local owner 变化
- host apply plan 最小字段变化
- request family 分工变化
- 退役越界名单变化
