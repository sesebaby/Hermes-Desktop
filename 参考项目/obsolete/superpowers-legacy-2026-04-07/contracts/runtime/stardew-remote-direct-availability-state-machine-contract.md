# Stardew Remote Direct Availability State Machine Contract

状态：

- active design baseline

owner：

- stardew integration owner

用途：

- 用大白话把手机私信“现在能不能联系”写成正式状态机，不允许再靠 build 开关硬填。

固定 owner：

- `RemoteDirectAvailabilityResolver`

states：

- `available_now`
- `unavailable_now`

reason codes：

- `npc_sleeping`
- `festival_locked`
- `cutscene_locked`
- `phone_channel_disabled`
- `remote_contact_forbidden`
- `npc_temporarily_unreachable`

inputs：

- 当前时间
- 当前日期
- 节日 / 剧情锁定
- NPC 睡眠状态
- title-local 远程接触支持矩阵
- 当前手机远程频道是否开放

state rules：

1. 只要命中任一阻断原因，就进入 `unavailable_now`
2. 第一阶段不做：
   - `delayed`
   - `queued`
   - `retry_later`
3. `DayStarted` 只重算 availability，不自动重发旧消息

transition rules：

- `available_now -> unavailable_now`
  - 睡觉
  - 节日 / 剧情
  - 渠道关闭
- `unavailable_now -> available_now`
  - 宿主事实恢复可联系

player-visible rules：

1. `unavailable_now` 必须显示明确原因
2. `unavailable_now` 时：
   - 不创建 `pending_visible`
   - 不创建待发送队列
   - 不追加 committed turn

绝对禁止：

1. 不允许用 `BuildExposureConfig` 直接替代 availability resolver
2. 不允许把 `unavailable_now` 伪装成发送失败
3. 不允许本地偷偷排队以后再发

update trigger：

- 宿主可联系规则变化
- 远程频道支持矩阵变化

