# Superpowers 状态机总表附件

## 1. 文档定位

这份表把系统里重要状态机集中登记。  
目的很简单：

1. 防止状态散在代码里没人管
2. 防止 UI、审计、恢复路径各写一套
3. 给后面实现和 review 一个统一查表入口

固定回链：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `docs/superpowers/contracts/runtime/private-dialogue-commit-state-machine-contract.md`
- `docs/superpowers/contracts/runtime/stardew-remote-direct-availability-state-machine-contract.md`
- `docs/superpowers/contracts/runtime/stardew-proactive-dialogue-trigger-state-machine-contract.md`
- `docs/superpowers/contracts/runtime/stardew-auto-action-and-schedule-restore-contract.md`

## 2. 当前正式状态机目录

### 2.1 Shared Runtime

#### SM-001 Private Dialogue Commit

- owner：
  - `Runtime.Local finalize verdict owner`
  - `Cloud canonical-history record owner`
- 正式文档：
  - `private-dialogue-commit-state-machine-contract.md`
- 关键状态：
  - `request_received`
  - `fact_package_validated`
  - `candidate_generated`
  - `pending_visible`
  - `committed`
  - `render_failed`
  - `recovered`
  - `rejected`
- 补充规则：
  - `pending_visible` 只表示纯文本可见面已可先显示
  - 不等于 committed
  - 不允许拿来冒充宿主状态变更已经成立

#### SM-002 Deterministic Command Execution

- owner：
  - `Runtime.Local`
- 正式文档：
  - `deterministic-command-event-contract.md`
- 关键状态：
  - `candidate_received`
  - `validated`
  - `lowered`
  - `dispatched`
  - `applied`
  - `rejected`
  - `rolled_back`
  - `failed`

### 2.2 Stardew Title-Local

#### SM-101 Remote Direct Availability

- owner：
  - `Runtime.Stardew`
- 正式文档：
  - `stardew-remote-direct-availability-state-machine-contract.md`
- 关键状态：
  - `available_now`
  - `unavailable_now`

#### SM-102 Remote Direct Thread Session

- owner：
  - `RemoteDirectThreadSession`
- 正式文档：
  - `stardew-remote-direct-thread-contract.md`
- 关键状态：
  - `not_open`
  - `opening`
  - `ready`
  - `unavailable_now`
  - `submission_failed`
  - `render_failed`
  - `closed`

#### SM-103 Group Chat Session

- owner：
  - `OnsiteGroupSession`
  - `PhoneGroupThreadSession`
- 正式文档：
  - `stardew-group-chat-session-contract.md`
- 关键状态：
  - `idle`
  - `participant_set_frozen`
  - `turn_generating`
  - `turn_rendered`
  - `closed`

#### SM-104 Phone Group Thread

- owner：
  - `PhoneGroupThreadSession`
- 正式文档：
  - `stardew-phone-group-thread-contract.md`
- 关键状态：
  - `not_open`
  - `ready`
  - `dnd`
  - `has_unread`
  - `submission_failed`
  - `closed`

#### SM-105 Onsite Group Overlay

- owner：
  - `OnsiteGroupOverlayController`
- 正式文档：
  - `stardew-onsite-group-overlay-contract.md`
- 关键状态：
  - `hidden`
  - `visible`
  - `input_active`
  - `render_failed`

#### SM-106 Proactive Dialogue Trigger

- owner：
  - `ProactiveDialogueRouteCoordinator`
- 正式文档：
  - `stardew-proactive-dialogue-trigger-state-machine-contract.md`
- 关键状态：
  - `idle`
  - `eligible`
  - `cooldown`
  - `blocked`

#### SM-107 Auto Action And Schedule Restore

- owner：
  - `NpcAutoActionExecutor`
  - `NpcScheduleRestoreExecutor`
- 正式文档：
  - `stardew-auto-action-and-schedule-restore-contract.md`
- 关键状态：
  - `idle`
  - `host_apply_running`
  - `restore_pending`
  - `restored`
  - `failed`

#### SM-108 Item Instantiation / Gift Trade

- owner：
  - `StardewItemInstantiationExecutor`
  - `ItemGiftHostExecutor`
  - `TradeHostExecutor`
- 正式文档：
  - `stardew-item-instantiation-creator-contract.md`
  - `stardew-item-gift-and-trade-host-executor-contract.md`
- 关键状态：
  - `intent_received`
  - `instance_created`
  - `carrier_rendered`
  - `delivery_committed`
  - `failed_host_apply`

### 2.3 Launcher / Supervisor

#### SM-201 Launch Readiness Verdict

- owner：
  - `Launcher.Supervisor`
- 正式文档：
  - `launcher-supervisor-boundary-contract.md`
  - `launcher-launch-orchestration-state-machine.md`
- 关键状态最少要有：
  - `checking`
  - `ready`
  - `repair_required`
  - `blocked`

#### SM-202 Launcher Auth Session

- owner：
  - `Launcher`
- 正式文档：
  - `launcher-auth-session-contract.md`
  - `launcher-account-surface-state-machine.md`
- 关键状态最少要有：
  - `anonymous`
  - `logging_in`
  - `logged_in`
  - `expired`

补充：

- 当前正式合同还收了：
  - `registering`
  - `logging_out`
  - `auth_failed`

#### SM-203 Support Ticket Flow

- owner：
  - `Launcher`
- 正式文档：
  - `support-ticket-and-diagnostic-bundle-contract.md`
- 关键状态最少要有：
  - `draft`
  - `bundle_collecting`
  - `submitted`
  - `failed`
  - `closed`

补充：

- 当前正式合同还收了：
  - `submitting`
  - `text_only_fallback`

#### SM-204 Repair / Update / Recheck

- owner：
  - `Launcher.Supervisor`
- 正式文档：
  - `repair-update-recheck-state-machine.md`
- 关键状态：
  - `idle`
  - `checking`
  - `repair_required`
  - `repairing`
  - `update_required`
  - `updating`
  - `rechecking`
  - `ready`
  - `failed`

#### SM-205 Purchase Handoff

- owner：
  - `Launcher`
- 正式文档：
  - `purchase-handoff-state-machine.md`
- 关键状态：
  - `catalog_view`
  - `purchase_intent_created`
  - `handoff_outbound`
  - `awaiting_return`
  - `redeem_required`
  - `refreshing_entitlement`
  - `completed`
  - `failed`

#### SM-206 Support Closure

- owner：
  - `Launcher`
  - `Support operations`
- 正式文档：
  - `support-closure-state-machine.md`
- 关键状态：
  - `draft`
  - `submitted`
  - `waiting_for_response`
  - `needs_player_reply`
  - `resolved`
  - `closed`
  - `failed`

## 3. 使用规则

1. 新增状态机，先登记这份总表
2. 进入正式主线的状态机，必须有单独合同
3. 没有单独合同的状态机，不得宣称“已经收死”
