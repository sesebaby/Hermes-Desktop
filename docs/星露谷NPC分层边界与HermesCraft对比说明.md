# 星露谷 NPC 分层边界与 HermesCraft 对比说明

## 结论

对照 HermesCraft 的做法，Stardew NPC 的行为语义不应该主要写在 tool 描述里。tool 只负责“能不能调用、参数从哪里来、运行时会绑定什么”；地点为什么有意义、移动失败后怎么恢复、某个 NPC 喜欢哪里，分别属于 skill、navigation 和 persona 层。

最终边界如下：

- `src/games/stardew/StardewNpcTools.cs`：局部可执行契约 owner。只说明 `stardew_move` 参数必须来自最新观察中的 `moveCandidate` / `placeCandidate`，不能编造坐标，不承诺路径一定可走，并说明 runtime 会绑定 `npcId`、`saveId`、`traceId`、`idempotency`。
- `skills/gaming/stardew-world/SKILL.md`：地点意义与候选解释 owner。解释 `placeCandidate` 的 `label`、`tags`、`reason`、`endBehavior`、可用性提示和 endpoint candidate 语义。
- `skills/gaming/stardew-navigation.md`：移动循环与失败恢复 owner。负责“最新观察 -> `stardew_move` -> 查询任务状态 -> `path_blocked` / `path_unreachable` 后重新观察或换目标”，并禁止直接 HTTP。
- persona / `SOUL.md` / `facts.md`：NPC 个体偏好 owner，例如 Haley 喜欢漂亮、上镜、舒适的地点。
- memory：只保存跨会话事实和经历，不承载即时移动规则。
- runtime / bridge：只做安全门控、状态编排、路径探测和执行结果回传，不越过工具契约替 NPC 选择世界内目标。

## HermesCraft 的参考意义

HermesCraft 的工具层更像动作接口和参数边界；世界知识、导航方法、角色偏好和长期记忆分别放在不同文本资产或运行时边界里。参考的 Stardew 模组也常把日程、地点可进入性、任务优先级和路径规划做成数据或执行层能力。这说明 Hermes/Stardew 应该避免把“地点含义”“角色偏好”“失败策略”反复塞进 tool description，同时允许 runtime / bridge 做安全门控、状态编排和路径探测。

## 当前 Stardew 的护栏

`stardew_move` 的 tool description 可以保留这些最小契约：

- 参数来自当前观察的 `moveCandidate` 或 `placeCandidate`。
- `placeCandidate` 是 endpoint candidate，不是 host 命令，也不是永久路线保证。
- `locationName`、`x`、`y`、`reason`、可选 `facingDirection` 必须复制自最新观察，不允许编造坐标。
- `path_blocked` / `path_unreachable` 后应重新观察或换目标。
- runtime 自动绑定身份和幂等上下文。

schema provenance 也是硬护栏，不只是提示文案：`x`、`y`、`reason`、`facingDirection` 的字段描述必须继续说明它们来自当前/最新 observation candidate。

## 测试原则

边界测试不能只依赖临时 fixture。凡是要证明 `skills/gaming/*` owner 的测试，至少要有一层 repo-backed 测试：

- 从 `AppContext.BaseDirectory` 向上查找仓库根或真实 `skills/gaming` 路径，不依赖当前工作目录。
- 至少一条测试必须走 `StardewNpcAutonomyPromptSupplementBuilder` 或 service / prompt supplement 注入路径，证明真实仓库 skill 会被实际注入系统提示。
- direct read 真实 skill 文件只能作为补充，不能替代真实注入路径。

Bridge 回归也必须保留：

- `BridgeMoveFailureMapperTests` 冻结初始不可达为 stable `path_unreachable`。
- `BridgeMoveCommandQueueRegressionTests` 冻结运行时阻塞为 stable `path_blocked`。
- `cross_location_unsupported` 是独立稳定项：跨地点移动必须被 block，不能静默传送，也不能漂移成其他错误词。

## 白话版原则

工具只告诉模型“这把锤子怎么拿、钉子从哪里来”；世界 skill 解释“为什么这颗钉子值得钉”；导航 skill 负责“钉不上怎么办”；persona 决定“这个 NPC 想不想钉”。宿主可以做安全门控、路径探测和状态编排，但不能越过工具契约替 NPC 选择目标。
