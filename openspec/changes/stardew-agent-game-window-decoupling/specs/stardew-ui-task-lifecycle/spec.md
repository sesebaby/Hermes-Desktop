## 新增需求

### Requirement: UI 生命周期等待必须表现为任务事实，而不是通用 ingress 失败
Stardew 的 UI / 窗口生命周期等待必须表示为所属任务、交互、lease 或状态事实。当等待是可恢复的，并且属于游戏或玩家可见 UI 生命周期时，绝不能把它归类成通用 ingress 重试失败。

#### Scenario: 私聊回复生命周期不能消耗 stale 预算
- **WHEN** 私聊回复对话框、电话回复，或同类回复 UI 生命周期仍然处于打开状态，或还在等待玩家交互
- **THEN** 相关的世界动作 host task 不能仅仅因为回复 UI 尚未关闭，就通过通用 stale / busy defer 预算被打成终态 blocked

#### Scenario: 菜单等待必须是可见状态
- **WHEN** 一个依赖 UI 的任务正在等待所属菜单、无关菜单，或某个玩家可见窗口条件结束
- **THEN** 这类等待必须以可读的 task / status 事实表现出来，例如 waiting、blocked 或 lease conflict，而不是隐藏的 host 锁

### Requirement: agent 流程继续推进时，UI 安全仍然必须留在游戏侧
把 agent 流程和 UI 生命周期解耦，不能移除游戏侧的 UI 安全保护。任何会打开、关闭或操作菜单的任务，仍然必须遵守 UI lease、当前激活菜单所有权、玩家自由状态、过场、节日、日切以及清理所有权。违反这些条件时，必须返回可观察的 blocked / failed 事实，并把下一步决策留给 agent。

#### Scenario: 无关的活动菜单阻止窗口操作
- **WHEN** 未来的交易、制作、采集、任务或私聊任务需要使用某个菜单，但当前存在一个无关的活动菜单
- **THEN** 任务必须返回带简短 `summary` 的 blocked 事实，并附带 `menu_blocked`、`ui_lease_busy` 或 `private_chat_active` 之类的 reason；同时不能关闭或覆盖这个无关菜单

#### Scenario: 所属 UI 等待在超时前保持可恢复
- **WHEN** 一个任务拥有 UI lease，并且正在等待某个可恢复的 UI 条件
- **THEN** 任务可以继续保持 running 或 waiting，直到条件改变，或由任务 timeout / watchdog 产生终态事实

#### Scenario: 超时会释放所属 UI 资源
- **WHEN** 一个依赖 UI 的任务在等待过程中超时
- **THEN** host 必须记录终态 timeout 事实，并使用 `action_slot_timeout` 或现有等价 timeout reason，同时只释放该任务自己拥有的 UI lease 和资源

### Requirement: 私聊回复投递必须与世界动作执行解耦
私聊回复的显示和关闭事件必须被视为交互 / 窗口事实。它们不能再被当成通用理由，用来把一个由 agent 明确通过 host task 生命周期提交的世界动作任务直接打成终态 blocked。

#### Scenario: 回复显示期间，世界动作仍保持 pending
- **WHEN** agent 在私聊中自然回复，并且在同一个 private-chat turn 中提交了一个世界动作
- **THEN** 回复显示 / 关闭生命周期必须作为 UI 或交互状态跟踪，而世界动作则继续通过它自己的 host task status 表达

#### Scenario: 没有工具调用就仍然不能创建世界动作
- **WHEN** 私聊回复里没有任何世界动作工具调用
- **THEN** 即使回复文本提到了某种意图，也不能仅靠文本创建世界动作
