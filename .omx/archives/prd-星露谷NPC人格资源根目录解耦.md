# 星露谷 NPC 人格资源根目录解耦 PRD

## 1. 目标

修复当前星露谷 NPC 常驻自治对通用 workspace 的错误依赖，把 NPC persona / pack 根目录解析改成游戏专用链路，确保桌面端在以下场景都能稳定工作：

1. 从仓库内直接启动桌面程序
2. 从 `bin` / 构建输出启动桌面程序
3. 从独立 exe 或部署目录启动桌面程序
4. 存在错误或无意义的 `HERMES_DESKTOP_WORKSPACE` 环境变量

本轮只解决“人格资源发现链路错误”这个根因，不把范围扩大到全局同步目录、缓存镜像或新的部署打包机制。

## 2. 现象与根因

当前实现里：

- `NpcRuntimeWorkspaceService.PackRoot`
  - 直接使用 `HermesEnvironment.AgentWorkingDirectory + src/game/stardew/personas`
- `HermesEnvironment.AgentWorkingDirectory`
  - 优先信任 `HERMES_DESKTOP_WORKSPACE`
- `StardewNpcRuntimeBindingResolver`
  - 接收并持有一个固定 `packRoot`

错误链路如下：

1. 用户环境变量里存在 `HERMES_DESKTOP_WORKSPACE=C:\`
2. NPC pack 根目录被拼成 `C:\src\game\stardew\personas`
3. resolver 找不到任何 pack
4. 当前 save 的 NPC 常驻自治在启动前就失败

这不是“缺配置”，而是“发现链路设计错误”。对星露谷 NPC runtime 来说，workspace 不是可靠事实源。

## 3. 产品原则

1. **游戏专用资源走游戏专用解析链**
2. **坏环境变量不能拖死 NPC runtime**
3. **默认行为要先服务真实游戏运行，而不是先服务开发者 workspace**
4. **只要能自动发现正确 persona 源，就不要求用户额外配置**
5. **第一阶段先修根因，避免顺手引入不必要的新层次**

## 4. 决策驱动

1. 当前问题已经在真实手测中复现，直接阻断 NPC 常驻自治。
2. 用户明确要求这是一个游戏专用改造，不接受“去配置 workspace”作为主解。
3. 需要同时兼容仓库运行、构建输出运行、部署目录运行。
4. 需要让海莉、潘妮等 NPC 的 persona 发现逻辑稳定一致，不再被桌面 agent 的其它语义误伤。

## 5. 方案对比

### 方案 A：继续使用 workspace，但加校验和回退

做法：

- 仍从 `AgentWorkingDirectory` 出发
- 只有当 `workspace/src/game/stardew/personas` 真存在时才接受
- 否则再回溯仓库

优点：

- 改动小
- 兼容当前代码路径

缺点：

- 语义上仍然把星露谷资源挂在 workspace 之下
- 未来还会有人顺着这个错误边界继续写代码
- 只是“缓解”，不是“纠正”

结论：

只能当弱兜底，不适合作为主方案。

### 方案 B：新增星露谷专用 persona 源目录定位器

做法：

- 新增 `StardewNpcPackSourceLocator`
- 它只负责：
  - 搜索候选源目录
  - 校验目录里是否有有效 NPC packs
  - 返回当前可用的 persona 源目录

优点：

- 彻底把游戏资源发现和 workspace 语义拆开
- 与现有 `NpcNamespace.SeedPersonaPack(...)` 的 per-NPC 运行时机制不冲突
- 改动面可控，先解决真实 bug

缺点：

- 需要新增一个小服务和对应测试
- 需要定义候选目录优先级与错误信息

结论：

推荐采用，作为第一阶段方案。

### 方案 C：第一阶段就做全局托管同步/缓存目录

做法：

- 先找到源目录
- 再把全部 personas 镜像到本地托管目录
- runtime 固定读本地镜像

优点：

- 为未来离线包、自带资源、跨位置运行预留统一目录

缺点：

- 当前问题的最小修复并不需要它
- 会和现有 `NpcNamespace` 的运行时 seed 机制形成概念重叠
- 会提前引入同步策略、覆盖策略、缓存失效等新复杂度

结论：

不作为第一阶段主方案。若后续真有打包/离线需求，再单开第二阶段。

## 6. 最终方案

采用 **方案 B**：第一阶段只新增“星露谷专用 persona 源目录定位器”，并让 runtime 直接使用定位到的有效源目录。

### 6.1 新增 `StardewNpcPackSourceLocator`

新增一个专门服务，例如：

- `Desktop/HermesDesktop/Services/StardewNpcPackSourceLocator.cs`

职责只包括：

1. 枚举候选源目录
2. 校验目录是否真的包含有效 NPC packs
3. 产出当前可用源目录和诊断信息

它不负责：

1. 全局同步
2. 全量缓存
3. 改写 per-NPC home / runtime seed 逻辑

### 6.2 候选源目录顺序

第一阶段建议顺序如下：

1. `AppContext.BaseDirectory\personas`，仅当该目录实际存在时才作为候选
2. 从 `AppContext.BaseDirectory` 向上回溯仓库，查找 `src/game/stardew/personas`
3. 从当前工作目录向上回溯仓库，查找 `src/game/stardew/personas`
4. `HERMES_DESKTOP_WORKSPACE` 仅作为弱候选，且只有在其下真实存在有效目录时才接受
5. 所有候选都要做路径规范化、去重和向上回溯边界限制，避免循环和重复判断

注意：

- 当前仓库并没有证据表明“发布包一定内置 personas”，所以第一阶段不能把“随包目录”写成硬前提，只能是“如果存在就使用”。
- `HERMES_DESKTOP_WORKSPACE` 不再是真理源，只是一个可被验证后才接受的候选。
- `BaseDirectory` / `CurrentDirectory` 这类输入应通过可注入的路径源传入 locator，测试不能直接依赖静态全局值。

为了让实现可测、可复现，定位器还需要一个明确契约：

1. `AppContext.BaseDirectory\personas` 是第一阶段唯一认可的“随包目录”相对路径
2. 向上回溯规则固定为“最多 8 层，或到盘符根目录即停止”
3. 每个候选在进入验证前都要做 `Path.GetFullPath` 级别的绝对路径规范化
4. 规范化后的候选路径按不区分大小写去重
5. `BaseDirectory`、`CurrentDirectory`、`WorkspaceDirectory`、`MaxParentDepth` 应通过 locator options 或等价抽象注入，避免在 locator 内部直接读静态全局状态

### 6.3 有效 pack 判定

定位器需要对候选目录做最小有效性校验，至少保证：

1. 根目录存在
2. 能通过现有的 pack loader / manifest 校验加载出至少一个有效 Stardew NPC pack
3. `haley/default`、`penny/default` 只作为当前回归样本，不写进准入规则
4. 对无效候选记录结构化拒绝原因，便于日志诊断

建议输出一个结构化结果对象，例如：

- `SelectedPath`
- `SelectedSourceKind`
- `Candidates`
- `RejectedCandidates`
- `ValidationSummary`

日志层只消费该结果对象，不把脆弱字符串当唯一验收依据。

### 6.4 运行时使用方式

第一阶段 **不引入全局托管镜像目录**。

运行时改为：

1. 启动时由 `StardewNpcPackSourceLocator` 找到有效 persona 源目录
2. `StardewNpcRuntimeBindingResolver` 在解析时从 provider/accessor 获取当前源目录
3. resolver 不再长期缓存一个错误的固定 `packRoot` 字符串

这样做的原因：

1. 直接消除当前 `C:\src\...` 根因
2. 不和现有 `NpcNamespace.SeedPersonaPack(...)` 的职责打架
3. 在不扩散复杂度的前提下，先让自治恢复
4. 解析器不持有固定 `packRoot` 字符串，也不允许“单例缓存旧路径”把首次错误候选污染后续解析

### 6.5 现有组件职责调整

#### `NpcRuntimeWorkspaceService`

改法：

- 不再负责从 `AgentWorkingDirectory` 拼 Stardew personas 路径
- 改为依赖 `StardewNpcPackSourceLocator`
- `PackRoot` / 快照信息改成展示“当前定位到的游戏 persona 源目录”

它在第一阶段更像 UI / 诊断适配层，而不是路径真理源。

#### `App.xaml.cs`

改法：

- 注册 `StardewNpcPackSourceLocator`
- 给 `StardewNpcRuntimeBindingResolver` 注入 locator/provider，而不是一次性算好的固定字符串

#### `HermesEnvironment`

改法：

- 保留它对普通桌面 agent 的通用 workspace 语义
- 不再承载任何 Stardew 专用 persona 路径语义

#### `StardewNpcRuntimeBindingResolver`

改法：

- 保持“按 NPC id 解析 runtime binding”的核心职责
- 但不能再把错误的 `packRoot` 固化在构造阶段并长期沿用
- 需要在解析时读取最新有效源目录，或通过 provider 惰性获取
- 如果 provider 有缓存，也只能缓存“已验证定位结果”，并且必须支持失效后重算，不能缓存一次错误结果后永久复用

### 6.6 第二阶段明确后置

以下能力不在本轮实现，但允许未来单开第二阶段：

1. 全局 persona 托管镜像目录
2. 发布包内置 personas 的正式装配流程
3. 缓存更新/覆盖策略
4. 离线部署场景的固定资源目录

只有当真实部署需求出现，再补 `StardewNpcPackStagingService` 一类组件。

## 7. 非目标

本轮不做下面这些事：

1. 不改普通桌面 agent 的 workspace 语义
2. 不改 NPC 私聊 / 长期记忆逻辑
3. 不改 bridge 输入路由
4. 不重做 persona pack manifest 结构
5. 不改现有 per-NPC `home` / `soul.md` 种子机制

## 8. 验收标准

满足以下条件才算完成：

1. 存在错误 `HERMES_DESKTOP_WORKSPACE` 时，NPC packs 仍能正确发现
2. 当前 save 不再报 `No Stardew NPC packs were found under 'C:\src\game\stardew\personas'`
3. 海莉、潘妮在启用列表中时，自治能越过 pack root 初始化阶段
4. 日志能明确说明最终采用了哪个 persona 源目录，以及候选拒绝原因
5. 相关单元测试和回归测试通过

## 9. 风险与应对

### 风险 1：构建输出目录和仓库目录都存在候选，优先级不清

应对：

- 在定位器里固定候选顺序
- 把最终命中的候选来源写入日志

### 风险 2：未来发布包内置 personas 后，第一阶段逻辑与打包方式不一致

应对：

- 第一阶段只把“随包目录”当可选候选
- 真正做打包支持时，再补第二阶段 staging 方案

### 风险 3：resolver 仍在某处缓存旧路径

应对：

- 明确把 resolver 改成 provider/accessor 模式
- 用测试锁住“错误 workspace 也能解析 Haley / Penny”

## 10. ADR

### Decision

第一阶段新增 `StardewNpcPackSourceLocator`，把星露谷 persona 发现从 workspace 语义中拆出来，并让 runtime 直接使用定位到的有效源目录。

### Drivers

- 用户明确要求与 workspace 解耦
- 当前真实 bug 已证明 workspace 路径不可靠
- 现有 per-NPC seed 机制已经存在，不需要第一阶段再造一层全局镜像

### Alternatives Considered

- 继续使用 workspace + 校验回退：能缓解，但边界仍错
- 第一阶段直接上全局镜像/缓存：可做，但对当前 bug 来说过重
- 只靠显式配置：把问题推给用户

### Why Chosen

它是纠正设计边界的最小闭环：既能修掉真实 bug，又不会把新复杂度提前拉进来。

### Consequences

- 会新增一个小型定位服务、少量 DI 调整和回归测试
- `NpcRuntimeWorkspaceService` 会退回到 UI / 诊断适配职责
- 后续若真需要打包缓存，可以在正确边界上继续扩展
- 定位结果需要以结构化诊断对象呈现，日志只是消费这份对象，不再直接拼脆弱字符串作为唯一证据

### Follow-ups

1. 若后续需要离线包/随包资源正式支持，再开第二阶段 staging 方案
2. 可在 Dashboard 暴露“当前 persona 源目录”和候选拒绝原因

## 11. 执行编排建议

### 可用 agent 类型

- `architect`：复核边界与职责拆分
- `executor`：落地 locator、DI、resolver 改造
- `debugger`：校对日志路径与失败链路
- `test-engineer`：补单测与集成测试
- `verifier`：跑回归并核对日志证据

### 若走 `$ralph`

建议顺序：

1. 先改 locator / DI / resolver
2. 再补 `HermesDesktop.Tests` 覆盖
3. 最后跑桌面日志手测，确认不再出现 `C:\src\...`

### 若走 `$team`

建议拆成三条并行 lane：

1. `executor`：实现 `StardewNpcPackSourceLocator` 与 DI wiring
2. `test-engineer`：补 discovery / resolver / workspace 回归测试
3. `debugger` 或 `verifier`：整理日志观测点与手测脚本

团队验证路径：

1. `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug`
2. 结合 `hermes.log` 做一次当前机器手测
3. 以 Haley / Penny 能成功越过 pack root 初始化为完成标志
