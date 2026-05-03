# 星露谷 NPC 人格资源根目录解耦测试规格

## 1. 测试目标

验证星露谷 NPC runtime 的 persona / pack 根目录已经从通用 workspace 语义中解耦，并能在错误环境变量、不同启动位置和无有效源目录场景下稳定运行。

本规格只覆盖第一阶段方案：**游戏专用源目录发现 + provider 化 resolver**。不把全局同步目录、缓存镜像纳入本轮通过条件。

## 2. 测试范围

覆盖以下内容：

1. persona 源目录候选搜索
2. 候选目录有效性校验
3. locator 的路径源注入与 process-global 状态隔离
4. resolver 不再固化错误根目录
5. `NpcRuntimeWorkspaceService` 不再从 workspace 拼接路径
6. 错误日志与结构化诊断信息

## 3. 不在本次范围内

1. NPC 对话质量
2. 私聊长期记忆
3. bridge 输入路由细节
4. 全局 persona 镜像/缓存
5. 多 NPC 并发自治的完整长跑

## 4. 单元测试

### 用例 1：错误 workspace 不会被当成真理源

前提：

- `HERMES_DESKTOP_WORKSPACE=C:\`
- `C:\src\game\stardew\personas` 不存在或无效
- 仓库真实 `src/game/stardew/personas` 存在

断言：

- 解析出的 persona 源目录不是 `C:\src\game\stardew\personas`
- 定位器会继续尝试其它候选
- 最终能找到有效源目录
- 有效性判定以“能加载出至少一个有效 Stardew NPC pack”为准，不依赖硬编码 Haley/Penny 目录

### 用例 2：应用目录附近存在随包 personas 时可优先命中

前提：

- `AppContext.BaseDirectory` 旁实际存在有效 personas 目录

断言：

- 定位器优先使用该目录
- 结果中能标明命中来源

说明：

- 这是“若目录存在则命中”的测试，不代表当前发布流程已经保证一定随包。
- 测试里的“随包目录”固定指 `AppContext.BaseDirectory\personas`。

### 用例 3：从 `AppContext.BaseDirectory` 向上回溯能找到仓库 personas

前提：

- 模拟从深层 `bin/...` 目录启动

断言：

- 向上回溯能找到真实仓库 `src/game/stardew/personas`

### 用例 4：从当前工作目录向上回溯也能找到仓库 personas

前提：

- `Environment.CurrentDirectory` 位于仓库深层子目录

断言：

- 当 `AppContext.BaseDirectory` 路线未命中时，当前工作目录路线仍可命中
- 向上回溯最多 8 层，或到盘符根目录即停止

### 用例 5：locator 支持可注入路径源，避免直接耦合静态全局状态

断言：

- `BaseDirectory`、`CurrentDirectory`、`WorkspaceDirectory`、`MaxParentDepth` 可通过 options 或等价抽象注入
- 单元测试不必依赖修改真实 `AppContext.BaseDirectory`
- 若测试必须改 `Environment.CurrentDirectory` 或环境变量，必须在用例内保存并恢复

### 用例 6：没有任何有效源目录时，报错必须准确

断言：

- 错误信息明确说明“没有找到任何有效 Stardew persona 源目录”
- 不允许继续出现误导性的固定 `C:\src\...` 报错
- 结构化诊断里能拿到每个候选的拒绝原因

### 用例 7：`NpcRuntimeWorkspaceService` 不再拼接 `AgentWorkingDirectory`

断言：

- `PackRoot` 不再来自 `HermesEnvironment.AgentWorkingDirectory + src/...`
- `GetSnapshot()` 在无活动 runtime 时，会使用定位器给出的当前 persona 源目录

### 用例 8：resolver 不再固化错误 packRoot，也不会被旧缓存污染

前提：

- 首次候选是错误 workspace
- 后续候选能找到真实 persona 目录

断言：

- `StardewNpcRuntimeBindingResolver` 不会一直持有首次错误路径
- 解析时会通过 provider/accessor 获取当前有效源目录
- 若 provider 有缓存，也必须支持失效后重算；错误首次候选不能污染后续解析

## 5. 集成测试

### 用例 9：DI wiring 使用定位器/provider，而不是固定字符串

断言：

- `App.xaml.cs` 注册了 `StardewNpcPackSourceLocator`
- resolver 注入的是 locator/provider 接口，而不是一次性算好的固定 `packRoot`
- 运行时日志消费的是结构化诊断结果，而不是 locator 内部临时字符串

### 用例 10：错误 workspace 场景下仍能解析 Haley / Penny

前提：

- 用户 workspace 环境变量错误
- 仓库 personas 真实存在

断言：

- `Resolve("haley", saveId)` 成功
- `Resolve("penny", saveId)` 成功

## 6. 手动验证

### 手测 1：当前机器复现路径修复

步骤：

1. 保持当前 `HERMES_DESKTOP_WORKSPACE=C:\`
2. 启动桌面程序
3. 进入一个包含 Haley / Penny 的当前 save；当前机器可用 `1_435026555` 作为复现样本
4. 观察 `hermes.log` 与桌面端 NPC runtime 视图

预期：

1. 不再报 `No Stardew NPC packs were found under 'C:\src\game\stardew\personas'`
2. 日志能看到最终命中的 persona 源目录
3. Haley / Penny 能越过 pack root 初始化阶段

### 手测 2：自治链路不再死在发现阶段

步骤：

1. 修复后重新进入游戏
2. 等待桌面端 attach 到当前 save
3. 观察 `Loop`、`PauseReason`、`LastTraceId`

预期：

1. 不会再因为 pack root 失败而整条自治链路停死
2. 若后续仍有行为问题，应进入新的单独诊断，而不是继续归因于 pack root

## 7. 观测点

需要能看到以下证据：

1. 枚举了哪些 persona 候选目录
2. 每个候选为何被接受或拒绝
3. 最终采用了哪个 persona 源目录
4. resolver 解析 Haley / Penny 是否成功
5. 日志里不再出现被固化的 `C:\src\...` 错误路径
6. 结构化诊断结果至少包含 `SelectedPath`、`SelectedSourceKind`、`RejectedCandidates`

## 8. 通过标准

以下全部满足才算通过：

1. 自动化测试全部通过
2. 当前机器手测不再出现 `C:\src\...` 错误
3. NPC runtime 能发现并加载 Haley / Penny packs
4. 日志能明确看出 pack 根目录来自游戏专用解析链，而不是 workspace 拼接
5. resolver 的依赖注入形态已从“固定字符串”改为“定位器/provider”
