# Deep Interview Spec: 桌面端开发者页面与 NPC 观察台

## 元数据

- 访谈来源: `.omx/interviews/desktop-developer-page-20260507T022045Z.md`
- 上下文快照: `.omx/context/desktop-developer-page-20260507T020104Z.md`
- 类型: 现有代码库改造
- 档位: Standard
- 最终歧义: 15.5%
- 阈值: 20%
- 状态: 可进入规划阶段，不可直接实现
- 残余风险: `omx question` 结构化提问通道在当前会话不可用；本规格基于用户直接澄清整理，并保留该偏差。

## 意图

用户对当前 Hermes Desktop UI 不满意，核心不满不是缺少后端能力，而是缺少一个能直观看清 NPC 决策全过程的开发者观察台。

目标是让开发者选中一个 NPC 后，可以在一个页面里看到这个 NPC 当前是谁、看到什么、记住什么、收到什么上下文、模型如何回复、调用了哪些工具、是否委托子 agent、本地执行层如何执行、任务和待办如何变化、bridge/SMAPI 是否收到命令，以及失败发生在哪一层。

## 期望结果

新增或改造桌面 UI，提供一个中文的 `开发者` 页面。第一版聚焦 `NPC 观察台`，不是泛化的设置页，也不是另起一套 NPC 控制系统。

页面应支持：

- 选择 NPC。
- 查看 NPC 当前状态、位置、运行时状态、动作槽、私聊锁、下一次唤醒。
- 查看 NPC 的 `SOUL.md`、记忆文件、用户/角色上下文文件和已激活技能摘要。
- 查看一次决策 trace 的上下文注入、AI API 原始回复、解析后的意图、工具调用、委托链路、任务和待办状态变化。
- 查看或跳转相关日志，包括 Hermes 日志、NPC `runtime.jsonl`、SMAPI 日志、bridge discovery 文件。
- 用中文解释可解析状态和诊断结果；原始日志、模型原文、工具参数、文件内容保持原样。

## 推荐方案

采用 **独立 `开发者` 页面 + `NPC 观察台` 首屏**。

理由：

- `Dashboard` 应保持概览，不适合承载细节追踪。
- `Agent` 页目前混合身份配置、人格管理和 NPC runtime 调试，职责已经偏杂。
- 新页面可以成为开发和诊断入口，同时复用现有 runtime/debug/log 服务。
- 可以把现有 `Agent > Runtime` 的开发调试职责逐步收敛到 `开发者` 页面，避免两个入口长期维护同一动作。

## 信息架构

第一版页面结构：

```text
--------------------------------------------------------------------------------+
| 开发者 / NPC 观察台                                                            |
| NPC: [海莉 v]  存档: [当前 v]  追踪: [最新 v]  [刷新] [实时跟随]               |
| 连接: 已连接 | 主模型通道: ... | 委托通道: ... | 最近错误: 无                  |
+--------------------------------------------------------------------------------+
| NPC 列表              | 当前概览                              | 资料与上下文      |
|-----------------------|---------------------------------------|-------------------|
| > 海莉                | 地点: Town  坐标: 52,74               | 人格文件          |
|   潘妮                | 状态: 运行中  下次唤醒: 10:35         | 记忆文件          |
|   阿比盖尔            | 动作槽: 前往海滩                      | 用户资料          |
|                       | 私聊锁: 无                            | 已激活技能        |
|                       | 当前待办: 去海边，不要瞬移            | [只读查看]        |
+-----------------------+---------------------------------------+-------------------+
| 时间线                                                                         |
|--------------------------------------------------------------------------------|
| 10:31:02 观察事实    location=Town tile=52,74 gameTime=...                    |
| 10:31:03 构建上下文  注入人格、记忆、当前待办、地图技能事实                  |
| 10:31:05 模型请求    model=... channel=... tokens=...                         |
| 10:31:08 模型回复    {"action":"move","target":{"locationName":"Beach"...}}   |
| 10:31:08 本地执行    选择委托通道                                             |
| 10:31:10 工具调用    stardew_navigate_to_tile(location=Beach,x=...,y=...)     |
| 10:31:12 游戏桥接    task_move_enqueued commandId=...                         |
| 10:31:18 执行结果    task_completed / failed / blocked                        |
+--------------------------------------------------------------------------------+
| 详情页签                                                                         |
| [模型回复] [工具调用] [委托链路] [任务与待办] [世界事实] [日志] [复盘对比]     |
+--------------------------------------------------------------------------------+
```

UI 文字要求：

- 页面标题、按钮、页签、状态摘要、诊断说明全部使用中文。
- 不出现 `task/TODO/diff/replay` 这类中英文夹杂标签。
- 技术原文不翻译：日志、JSON、模型原始回复、工具名、文件名、路径、异常堆栈保持原样。
- 如果必须显示英文标识符，放在原始详情区域或等宽文本块里，不作为中文 UI 标签的一部分。

## 范围

第一版范围：

- 左侧导航新增 `开发者`。
- 新增 `DeveloperPage` 或等价页面，中文显示为 `开发者`。
- 页面首屏为 `NPC 观察台`。
- 复用现有 NPC runtime snapshot 显示 NPC 列表、bridge 状态、last trace、last error、动作槽、私聊锁、待办摘要。
- 支持只读打开或查看 NPC 相关 `SOUL.md`、记忆文件、用户资料、技能摘要。
- 支持查看最新 trace 的时间线。
- 支持展示 raw AI response、解析后的 intent、工具调用和结果；若现有日志没有足够数据，规划阶段应明确最小诊断补点。
- 支持展示父 agent 到本地执行层/委托通道的链路。
- 支持展示当前任务和待办，不新建任务系统。
- 支持打开 Hermes 日志、NPC runtime 目录、SMAPI 日志、bridge discovery 文件。
- 保留现有调试能力，但开发者页应成为开发调试主入口。

## 非目标

- 不创建第二套 NPC runtime。
- 不创建第二套 private chat、local executor、tool call 或 delegation 协议。
- 不创建第二套任务、待办、记忆或人格存储。
- 不把 `SOUL.md`、记忆文件、用户资料改成开发者页可随意编辑的配置中心。第一版只读。
- 不重做整个桌面 UI。
- 不把普通用户入口和开发调试入口混在一起。
- 不新增依赖。
- 不在 UI 控件文案中中英文混杂。
- 不在第一版实现完整日志数据库、完整 trace 查询语言或完整可视化分析系统。

## 决策边界

OMX/后续实现者可以自行决定：

- 页面内部布局细节，只要保持中文、密集、可追踪、可扫描。
- 是否用 `ListView`、`GridView`、`TabView`、`Expander` 等现有 WinUI 控件组织信息。
- 是否把 `Agent > Runtime` 缩减为跳转/摘要，避免重复调试入口。
- 是否新增窄的只读聚合服务或视图模型，用于把现有 runtime/log/transcript 数据整理给 UI。
- 是否给现有 runtime log 增加最小诊断字段，前提是写入现有日志/trace体系，不另起存储。

需要再次确认或单独评审：

- 是否允许开发者页触发真实移动、说话、私聊等有游戏副作用的动作。
- 是否允许在第一版直接提供自然语言目标输入。
- 是否允许编辑 NPC 人格/记忆文件。
- 是否把开发者页默认展示给所有用户，还是后续加开关隐藏。

## 约束

- 遵守 `Desktop/HermesDesktop/AGENTS.md`。
- 遵守 XAML 可访问性、性能、WinUI、中文化和代码质量规则。
- 复用现有 DI 注册与服务：
  - `NpcRuntimeWorkspaceService`
  - `StardewNpcDebugActionService`
  - `StardewAutonomyTickDebugService`
  - `NpcRuntimeSupervisor`
  - `StardewNpcRuntimeBindingResolver`
  - `INpcPrivateChatAgentRunner`
  - 现有日志路径和 runtime workspace
- 所有用户可见字符串走 `Resources.resw`。
- 交互控件必须有稳定的无障碍名称和自动化标识。
- 大列表使用虚拟化控件。
- 文件读取和日志读取不能阻塞 UI 线程。

## 技术上下文发现

证据：

- `Desktop/HermesDesktop/MainWindow.xaml` 使用左侧 `NavigationView`，当前菜单有 Dashboard、Chat、Agent、Skills、Memory、Buddy、Settings。
- `Desktop/HermesDesktop/MainWindow.xaml.cs` 用 `PageMap` 把 tag 映射到页面类型。
- `Desktop/HermesDesktop/Views/AgentPage.xaml` 已有 `Runtime` 标签页，包含 NPC runtime 列表和 Haley/Penny 调试按钮。
- `Desktop/HermesDesktop/Views/AgentPage.xaml.cs` 已经调用 `NpcRuntimeWorkspaceService`、`StardewNpcDebugActionService`、`StardewAutonomyTickDebugService`。
- `Desktop/HermesDesktop/Views/DashboardPage.xaml` 已有 NPC runtime 概览卡和跳转 Agent Runtime 的按钮。
- `Desktop/HermesDesktop/App.xaml.cs` 已注册 NPC runtime、private chat runner、autonomy debug service、Stardew debug action service 等后端能力。

推断：

- 第一版开发者页可以用现有服务显示 runtime 概览和执行 debug action。
- `Agent > Runtime` 的开发调试内容可以迁移或改成指向开发者页，避免长期双入口。
- Raw AI response、tool call、delegation details 是否已经足够持久化，需要实现前再查 `TranscriptStore`、`NpcRuntimeLogWriter`、`NpcAutonomyLoop`、`NpcLocalExecutorRunner` 和相关 activity 日志结构。

未知：

- 当前是否有统一接口读取某个 NPC 的完整 trace 时间线。
- 当前 runtime log 是否已经包含模型原始响应、工具参数和委托交互细节。
- 当前记忆文件和 `SOUL.md` 的 NPC-specific 路径是否已有现成 resolver 可复用。

## 验收标准

- 左侧导航出现中文 `开发者` 入口。
- 页面所有普通 UI 文案为中文，不出现中英文夹杂的控件标签。
- 原始日志、JSON、工具名、路径、异常堆栈保持原文。
- 选中一个 NPC 后，可以看到该 NPC 的当前状态、地点、坐标、动作槽、私聊锁、下次唤醒、当前任务与待办摘要。
- 可以只读查看该 NPC 的人格文件、记忆文件、用户资料或等价上下文文件。
- 可以看到至少最近一次 NPC 决策的时间线：观察事实、上下文构建、模型回复、本地执行、工具调用、桥接命令、执行结果。
- 可以看到 AI API 回复的原文或已记录原始片段；如果当前后端不记录，规划文档必须列出最小补点。
- 可以看到工具调用名称、参数、结果、耗时或失败原因。
- 可以看到父层到本地执行/委托通道的链路。
- 可以看到任务与待办状态，不新建任务系统。
- 可以一键打开相关日志和 runtime 目录。
- 页面不引入新依赖，不新建第二套后端存储。
- 构建通过：`dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64`。

## 分期建议

### 第一阶段：只读观察台

- 新增中文 `开发者` 导航入口。
- 显示 NPC 列表、状态概览、资料文件、任务与待办摘要、日志入口。
- 读取最新 trace 或 runtime activity，展示可解析时间线。
- 不触发真实游戏动作。

### 第二阶段：受控调试动作

- 把现有 `Agent > Runtime` 的 Haley/Penny debug action 收敛为开发者页里的选择 NPC + 动作模式。
- 明确标注副作用动作。
- 保持所有动作复用现有服务。

### 第三阶段：深度追踪

- 展示完整模型请求/回复、工具参数、子 agent 委托交互、上下文注入差异。
- 增加 trace 过滤和诊断包导出。
- 如果需要补日志，只写入现有 transcript/runtime log 链路。

## 需避免的失败路径

- 把开发者页做成第二个 Dashboard，只放状态卡和跳转。
- 把开发者页做成第二套 NPC 控制器。
- 在 UI 里混用中文标签和英文产品词，造成阅读断裂。
- 只显示最终错误，不显示观察事实、上下文、模型回复和工具过程。
- 只看 `hermes.log`，不把 `runtime.jsonl`、SMAPI、bridge commandId 关联起来。
- 长期保留 `Agent > Runtime` 和 `开发者` 两套等价调试按钮。

## 执行桥接

推荐下一步：`$ralplan`

输入：

- `.omx/specs/deep-interview-desktop-developer-page.md`
- `.omx/interviews/desktop-developer-page-20260507T022045Z.md`
- `.omx/context/desktop-developer-page-20260507T020104Z.md`

规划阶段重点：

- 先确认 raw AI response、tool call、delegation details 的现有数据来源。
- 决定 `Agent > Runtime` 如何与 `开发者` 页收敛。
- 列出第一阶段只读观察台的最小实现切片。
- 给出测试策略，尤其是中文资源、导航、服务复用和 UI 不阻塞。

不建议直接 `$autopilot`，因为数据来源和现有日志粒度还需要一次代码级规划确认。

