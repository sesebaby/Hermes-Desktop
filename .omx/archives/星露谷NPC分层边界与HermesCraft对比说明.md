# 星露谷 NPC 分层边界与 HermesCraft 对比说明

## 结论

对照 `external/hermescraft-main` 的实现后，可以确认一件事：**行为语义不应该主要写在 tool 描述里**。

HermesCraft 的做法更接近下面这套分层：

- **tool / action schema**：只负责动作接口和参数边界
- **skills**：放世界知识、地点理解、行动方法
- **SOUL**：放通用行为法则和决策框架
- **persona prompt / facts**：放角色偏好与个体差异
- **memory**：放长期记忆与跨会话状态

这和当前 `StardewNpcTools.cs` 里那种“tool 描述里夹带世界语义解释”的写法，不是同一种层次。

## HermesCraft 的证据

### 1. tool 层更像纯动作接口

`external/hermescraft-main/bin/mc` 和 `external/hermescraft-main/bot/server.js` 主要做的是：

- 暴露可调用动作
- 提供观察接口
- 做参数校验和路由分发

它们没有把“什么时候应该这样做”“这个动作在世界里意味着什么”写进工具实现里。

### 2. 世界知识放在 skills

例如：

- `external/hermescraft-main/skills/minecraft-survival.md`
- `external/hermescraft-main/skills/minecraft-navigation.md`

这些文件承载的是领域知识和行动建议，不是工具实现。

### 3. 行为法则放在 SOUL

例如：

- `external/hermescraft-main/SOUL-minecraft.md`
- `external/hermescraft-main/SOUL-landfolk.md`
- `external/hermescraft-main/SOUL-civilization.md`

这些文件负责定义通用行为原则、卡住时怎么做、如何决策、何时记忆、何时观察。

### 4. 个体偏好放在 persona 文件

例如：

- `external/hermescraft-main/prompts/landfolk/steve.md`
- `external/hermescraft-main/civilization.sh` 里的角色 prompt 组合方式

这说明“谁喜欢什么”是角色层问题，不是工具层问题。

### 5. memory 是独立层

HermesCraft 主要通过每个角色独立的 `HERMES_HOME`、`MEMORY.md`、session history 来隔离记忆。

它不是把长期记忆塞进 tool description，也不是把记忆当成 world knowledge。

## 对当前 Stardew 代码的含义

`src/games/stardew/StardewNpcTools.cs` 里现在有两类内容：

### 合理保留在 tool 层的

- 参数格式
- 必须使用当前 observation 中的候选
- 不能编造坐标
- `path_blocked` / `path_unreachable` 后要重新观察或换目标
- runtime 自动绑定的上下文信息

这些属于工具契约，放在代码里是合理的。

### 应该往外挪的

- `placeCandidate` 的世界语义解释
- schedule-style endpoint 的长篇说明
- 某个候选为什么“更像 Haley 会去的地方”
- 太多重复的 world / persona 解释

这些内容更适合放到：

- `skills/gaming/stardew-world/SKILL.md`
- `src/game/stardew/personas/haley/default/SOUL.md`
- `src/game/stardew/personas/haley/default/facts.md`

## 具体判断

所以，问题不在于 `StardewNpcTools.cs` 有硬编码，而在于它是否只保留了**最小工具契约**。

如果 tool 层开始承担下面这些职责，就会越界：

- 解释世界知识
- 解释角色偏好
- 解释候选地点的语义
- 替 skill / SOUL 反复复述同一套规则

HermesCraft 更倾向于把这些内容放到更高层的文本资产里，而不是塞进 tool 描述。

## 建议

后续可以按这条边界收敛：

1. `StardewNpcTools.cs` 只留最小可执行契约。
2. 世界知识移到 `stardew-world` skill。
3. Haley 偏好留在 `SOUL.md` 和 `facts.md`。
4. memory 只管跨会话状态，不承载运行时行为规则。

这样更接近 HermesCraft 的分层，也更容易维护。
