# Superpowers Governance Hard Gates Appendix

## 1. 文档定位

本文档定义 `All Game In AI / superpowers` 在 AFW 治理层上的两条 repo-private 硬门禁：

- `UI / 前台实现必须使用 ui-ux-pro-max`
- `AI 功能必须锚定参考 mod 方案`

本文档不替代：

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
- `.codex/skills/afw-unified-governance/SKILL.md`
- `.codex/skills/allgameinai-project-governance/SKILL.md`

它的作用是把 `Superpowers` 私有治理从通用 AFW 基线中拆出来，形成单独真相源，供：

- kickoff
- design
- tasks
- implementation
- review

统一回链。

## 2. 一句话结论

从现在开始：

- 任何 `Superpowers` 玩家可见 UI / 前台 / 表现层任务，都必须显式使用 `ui-ux-pro-max`
- 任何 `Superpowers` AI 功能任务，都必须显式锚定 `GGBH_OpenAIWorld_20260326` 的成熟闭环，而不是自由发挥 AI 架构

## 3. 治理落位

这两条门禁属于：

- `Superpowers` 仓库私有治理

不属于：

- 所有 AFW 项目的通用默认规则

因此它们的正确落位是：

1. 先使用 `.codex/skills/afw-unified-governance/SKILL.md`
2. 再使用 `.codex/skills/allgameinai-project-governance/SKILL.md`
3. 命中 UI 或 AI 范围时，再回链本附录

## 4. 门禁 A：UI / 前台实现必须使用 ui-ux-pro-max

### 4.1 命中范围

只要任务涉及以下任一内容，就视为命中 UI / 前台范围：

- `Launcher`
- `Mod` 玩家可见前台
- 页面、组件、布局、视觉层次
- 交互、状态反馈、动效
- 空态、失败态、延后态、恢复态
- copy 呈现方式与前台信息组织

### 4.2 强制动作

命中范围后，必须：

1. 读取 `.codex/skills/ui-ux-pro-max/SKILL.md`
2. 在 design、implementation、review 中显式说明已经使用该 skill
3. 产出至少一组可核查的 UI 依据：
   - 设计系统
   - 关键 UX 规则
   - 可访问性检查点
   - 状态反馈与响应式检查点

### 4.3 禁止项

不允许：

- 只做功能，不做前台质量约束
- 把“后面再美化”当作绕过理由
- 把“沿用现有风格”当作跳过 `ui-ux-pro-max` 的理由
- 只写内部链路，不写玩家最终看到什么

### 4.4 Review 证据

UI 相关任务在 review 中至少要回答：

- 使用了 `ui-ux-pro-max` 的哪些规则
- 当前页面或组件的设计系统依据是什么
- 空态、失败态、延后态、恢复态如何露出
- 响应式、可访问性、交互反馈如何校验

任一项答不清，不能判定 ready / done。

## 5. 门禁 B：AI 功能必须锚定参考 mod 方案

### 5.1 命中范围

只要任务涉及以下任一内容，就视为命中 AI 范围：

- prompt
- orchestration
- NPC AI
- 私聊、群聊、主动对话
- 世界事件 / 世界推演
- 记忆压缩 / 长期记忆
- 上下文摘要构建
- 结构化 AI 输出
- AI 驱动宿主行为

### 5.2 强制阅读

命中范围后，必须先读：

- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/成熟MOD源码锚定_通用AI游戏方案提炼.md`

然后必须再读与当前链路直接相关的参考材料，至少覆盖：

- 相关分析文档
- 相关反编译源码
- 相关 decoded prompts

所有 AI 任务默认最少回看这些锚点：

- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.ailm/ChatCompletions.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/AIServer.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/jsonrepair/JsonRepair.cs`

如果任务是对话、群聊、世界事件、记忆等链路，还必须补看对应：

- `recovered_mod/GGBH_OpenAIWorld_20260326/decoded_prompts/*`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/*`

### 5.3 必须保持一致的骨架

`Superpowers` 的 AI 实现必须能映射到参考 mod 的成熟闭环：

1. `Trigger`
2. `Snapshot`
3. `Summary Builder`
4. `Intent Schema`
5. `Parser / Repair / Normalizer`
6. `Projector / Executor`

必须保持以下边界：

- AI 只产出结构化意图
- 宿主或执行层负责白名单投射
- 不能让模型直接拥有宿主执行权
- 不能省略 repair / normalize / projector

### 5.4 允许替换的部分

允许：

- 使用 `Agent Framework` 承载 orchestration
- 参考参考 mod 的 prompts 后重写为 `Superpowers` 语境
- 结合当前 `gameId`、phase、治理约束做字段裁剪

### 5.5 禁止项

不允许：

- 自由发挥另一套 AI 骨架
- 只写 prompt，不写 schema / repair / projector
- 让模型输出直接落到宿主执行
- 声称“参考了 mod”，但没有列出文档锚点、源码锚点、prompt 锚点

### 5.6 Review 证据

AI 相关任务在 review 中至少要回答：

- 读了哪些参考文档
- 读了哪些源码锚点
- 读了哪些 decoded prompts
- 当前方案和参考 mod 的 6 层映射关系是什么
- 哪些地方是技术栈替换，哪些地方保持同构

任一项答不清，不能判定 ready / done。

## 6. 阶段检查字段

### 6.1 Kickoff

除了通用 AFW kickoff，还必须回答：

- 这次是否命中 UI 范围
- 这次是否命中 AI 范围
- 如果命中 UI，准备使用 `ui-ux-pro-max` 的哪些依据
- 如果命中 AI，参考 mod 的哪些链路是本次锚点

### 6.2 Design

设计阶段必须明确：

- 玩家最终看到什么
- UI 质量如何约束
- AI 链路如何映射参考 mod 的 6 层骨架
- 哪些是 `Superpowers` 私有扩展，哪些只是技术栈替换

### 6.3 Tasks / Implementation

实现任务必须能落到：

- UI 任务回链 `ui-ux-pro-max`
- AI 任务回链参考 mod 文档 / 源码 / prompts
- 验证任务能证明门禁没有被绕过

### 6.4 Review

review 必须把这两条门禁当 blocker，而不是建议项。

## 7. Stop Rule

出现以下任一情况，必须停止并回到设计或 review：

- 命中 UI 范围，但没有使用 `ui-ux-pro-max`
- 命中 AI 范围，但没有列出参考 mod 锚点
- 方案里缺少 `Intent Schema`、`Repair / Normalizer`、或 `Projector / Executor`
- 任务声称“无前台影响”，但实际修改了 `Launcher`、`Mod` 前台、copy、状态露出或恢复路径
- 任务声称“参考了 mod”，但拿不出对应文件

## 8. 最终结论

这两条门禁从现在开始属于 `Superpowers` 的 repo-private 硬治理：

1. UI 必须显式走 `ui-ux-pro-max`
2. AI 必须显式锚定参考 mod 成熟闭环

任何文档、计划、实现、review 只要绕过其中任一条，都不应被判定为 ready / done。
