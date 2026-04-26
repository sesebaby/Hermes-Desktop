# Cloud Prompt Assembly Contract

状态：

- active design baseline

owner：

- cloud orchestration owner
- prompt governance owner

用途：

- 用大白话写死：最终 prompt 只能在 `Cloud` 组装，而且每个游戏必须用自己独立的 prompt 资产。

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/governance/current-phase-boundary.md`
- `docs/superpowers/contracts/runtime/cloud-orchestration-fact-package-contract.md`
- `docs/superpowers/contracts/runtime/trace-audit-contract.md`
- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`

authoritative boundary：

- `Cloud`
  - 唯一拥有最终 prompt 组装权
  - 唯一拥有 prompt 资产明文正本
  - 唯一代表玩家或平台去调 provider
- `Runtime.Local`
  - 只能发事实包
  - 不能提交 provider-ready prompt
- `Runtime.<game> Adapter`
  - 只能提供 title-local facts / refs
  - 不能内嵌 prompt 段
- `Launcher`
  - 不显示 prompt 资产明文
- `AFW`
  - 只能作为 `Cloud` 内部编排子层
  - 不能拥有 prompt 真相源

prompt assembly inputs：

1. `fact package`
2. `game-scoped prompt assets`
3. `canonical chat history`
4. `canonical memory`
5. `channel rules`
6. `provider route`
7. `audit policy`

game-scoped prompt asset rule：

1. 每个游戏必须有自己的 prompt 资产
2. 不允许把 `stardew-valley` prompt 资产当共享默认资产
3. 共享的只能是：
   - 编排骨架
   - 审计字段
   - provider route 规则
4. 角色语义、世界语义、频道语义必须留在各游戏资产里

assembly stages：

1. `fact resolve`
   - 读取事实包
   - 校验 request family
   - 补 game/channel/capability routing
2. `canonical hydrate`
   - 读聊天正本
   - 读记忆正本
   - 读 prompt 资产
3. `section assembly`
   - 拼 system / world / role / history / memory / output protocol 等段
   - 或拼当前 capability 的主线 prompt 段
4. `provider request build`
   - 生成 provider-ready payload
   - 绑定 provider/model/billingSource
5. `plaintext audit persist`
   - 保存最终 prompt 明文
   - 保存 promptAuditRef
6. `provider dispatch`
   - 由 `Cloud` 发请求

provider communication rule：

1. `billingSource = user_byok`
   - 表示 `Cloud` 代表玩家发起请求
2. `billingSource = platform_hosted`
   - 表示 `Cloud` 用平台 provider 发起请求
3. 两种都不允许：
   - 客户端直连 provider
   - Mod 直连 provider
   - Runtime.Local 直连 provider

provider-ready output minima：

- `requestId`
- `traceId`
- `gameId`
- `capability`
- `channelType`
- `providerId`
- `modelId`
- `billingSource`
- `promptAuditRef`
- `assembledPromptSections`

`assembledPromptSections` 最少包括：

- `systemOrMainline`
- `worldOrScene`
- `history`
- `memory`
- `outputProtocol`

plaintext audit rule：

1. prompt 必须明文审计
2. 记忆必须明文审计
3. 聊天必须明文审计
4. 审计正本固定在 `Cloud`
5. AFW checkpoint / telemetry 默认不保留完整 prompt 明文

forbidden paths：

1. 不允许 `Runtime.Local` 先拼 prompt，再让 `Cloud` 代发
2. 不允许 `Runtime.<game>` 内置 prompt asset catalog 继续当正式主线
3. 不允许 `HostedNarrativeController` 长期接受“本地已组装好的最终 prompt”作为正式接口
4. 不允许 `Cloud` 只做 provider 转发，不做事实包编排

fallback rule：

1. 缺资产就失败
2. 缺 canonical history 就按合同给空 history，不得改成本地 history
3. 缺 canonical memory 就按合同给空 memory，不得改成本地 memory
4. provider route 缺失直接拒绝，不允许偷偷走默认本地 provider

update trigger：

- prompt 组装阶段变化
- 每游戏 prompt 资产隔离规则变化
- BYOK / hosted provider 路由规则变化
- plaintext audit 规则变化
