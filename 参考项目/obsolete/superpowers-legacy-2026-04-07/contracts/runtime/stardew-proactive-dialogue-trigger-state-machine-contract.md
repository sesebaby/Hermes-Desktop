# Stardew Proactive Dialogue Trigger State Machine Contract

状态：

- active design baseline

owner：

- stardew integration owner

用途：

- 用大白话冻结主动对话、接触触发、自然二段对话的状态机。

states：

- `idle`
- `host_dialogue_opened`
- `host_dialogue_exhausted`
- `ai_eligible`
- `opening_ai_surface`
- `ai_surface_ready`
- `failed_closed`
- `cooldown`

state owner：

- `PrivateDialogueRouteCoordinator`

trigger inputs：

- 玩家接触 NPC
- 宿主原版对话打开
- 宿主原版对话关闭
- 宿主原版对话是否已耗尽
- 当前冷却状态
- 当前 scene / lock 状态

transition rules：

- `idle -> host_dialogue_opened`
  - 玩家第一次正常接触，原版对话接管
- `host_dialogue_opened -> host_dialogue_exhausted`
  - 原版对话真实结束且记录已写入
- `host_dialogue_exhausted -> ai_eligible`
  - 满足二段 AI 条件
- `ai_eligible -> opening_ai_surface`
  - 玩家再次触发或命中主动触发策略
- `opening_ai_surface -> ai_surface_ready`
  - AI 菜单真实打开
- 任意状态 -> `failed_closed`
  - 请求失败 / surface 失败 / lock 冲突
- `failed_closed -> cooldown`
  - 失败后进入短冷却
- `cooldown -> idle`
  - 冷却结束

固定规则：

1. 原版对话优先于 AI 对话
2. 原版对话未耗尽前，不得开 AI
3. 主动触发失败后，必须 fail-closed，不得强开 surface
4. 必须有冷却和去重，不能无限连发

绝对禁止：

1. 不允许把状态机散在 `ModEntry`、controller、menu 三处
2. 不允许“第一次原版、第二次 AI”继续只靠隐式 if/else 维持
3. 不允许失败后直接回到 AI ready

update trigger：

- 主动触发规则变化
- 冷却规则变化
- 原版优先级规则变化

