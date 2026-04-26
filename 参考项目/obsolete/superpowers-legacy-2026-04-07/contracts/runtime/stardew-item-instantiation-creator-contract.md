# Stardew Item Instantiation Creator Contract

状态：

- active design baseline

owner：

- stardew integration owner

用途：

- 用大白话冻结“云端给结构化物品意图后，Stardew 宿主怎么真正创建物品或特殊实体”。

authoritative boundary：

- `Cloud` 只给结构化 item definition / entity definition
- `Runtime.Local` 只做 gate
- `Superpowers.Stardew.Mod` 的 creator / executor 才能真正创建宿主对象

creator split：

1. `StardewItemInstantiationExecutor`
   - 普通物品
2. `StardewEntityInstantiationExecutor`
   - companion / scenery / 特殊实体

item creator minima：

- `intentId`
- `itemKind`
- `templateKey`
- `displayName`
- `description`
- `stack`
- `deliveryChannel`
- `targetOwner`

creation flow：

1. 先决定：
   - 走已有模板
   - 走 Json Assets 注册对象
   - 走特殊实体 creator
2. 实例创建成功后，才允许写：
   - `item.modData`
   - `authoritativeItemEventRecordId`
3. 然后再进入：
   - 背包
   - 邮件
   - 奖励
   - tooltip
   - world placement

Json Assets usage boundary：

1. 只允许用它的：
   - 注册
   - id 映射
   - 实例化
2. 不允许把 Json Assets 当 AI 语义 owner

CustomCompanions usage boundary：

1. 只允许用它的：
   - content pack
   - spawn
   - 特殊 companion 实体壳
2. 不允许把它整套当通用 NPC 系统

result minima：

- `creationOutcome`
- `itemInstanceId` 或 `entityInstanceId`
- `creatorKind`
- `hostEvidenceRef`
- `failureClass`

绝对禁止：

1. 没创建成功就先写正式历史
2. 直接改全局模板语义代替实例语义
3. 没有宿主 creator 的能力写成已支持

update trigger：

- item creator 分流规则变化
- Json Assets / entity spawn 接法变化

