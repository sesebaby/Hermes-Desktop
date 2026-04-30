# Hermes 参考项目功能模块效果差距对比

本文只对比 `external/hermes-agent-main` 参考项目与当前 Hermes Desktop 项目的功能模块效果差距，重点服务于“接入星露谷物语，实现多 NPC 村庄模式，让 AI Agent 像真实玩家一样长期自主生活”的目标。

本文不包含实施计划，也不把 MVP 范围作为讨论重点。

## 总体判断

参考项目的优势是长期自治 Agent 运行时：它能围绕会话、上下文、工具、记忆、技能、后台任务持续循环执行。

当前 Hermes Desktop 的优势是已经有 C# 桌面端、NPC runtime 骨架、Stardew bridge、Haley / Penny persona pack、`move` / `speak` 调试通路。

主要差距不是“有没有 Stardew 按钮”，而是每个 NPC 还没有真正形成独立的长期生活循环：自己观察、自己思考、自己调用工具行动、自己轮询结果、自己写入记忆，并在下一轮继续使用这些经验。

## 模块效果差距表

| 模块 | 对目标是否关键 | 参考项目效果 | 当前项目效果 | 主要差距 | 备注 |
| --- | --- | --- | --- | --- | --- |
| Agent 运行时 / 自治循环 | 最高 | Agent 能长时间自己想、自己调用工具、自己继续干 | 有通用 `Agent` 和 `NpcRuntime*` 骨架 | NPC 还没真正常驻自主生活 | 现在像“人按按钮它动一下”，目标是“它自己醒着过日子” |
| 工具与工具集 | 最高 | 工具注册、组合、过滤比较完整 | 有 typed tools，Stardew 主要有 `move` / `speak` / `status` | Stardew 能做的事还少，工具边界也没完全 NPC 化 | 现在 NPC 手里工具太少，像只会走两步和说一句 |
| 记忆系统 | 最高 | 会把经历沉淀成长期记忆 | 有 `MemoryManager` 和 NPC 目录隔离设计 | 记忆还没驱动 NPC 后续决策 | 现在像“有笔记本”，但 NPC 还没真的养成“记得昨天发生啥”的习惯 |
| Soul / 人格系统 | 最高 | `SOUL.md` 能长期影响 Agent 行为 | Haley / Penny 已有人格 seed pack | 还没证明每个 NPC runtime 都独立加载并持续生效 | 现在有人设卡，但还没完全变成活人性格 |
| Context / Prompt 组装 | 最高 | 会把人格、记忆、技能、上下文拼成可运行 prompt | 有 `ContextManager` / `PromptBuilder` | 多 NPC 场景下的专属 prompt 组装还不完整 | 现在容易变成“同一个脑子换名字”，需要每个 NPC 都有自己的脑内上下文 |
| 技能系统 | 高 | skill 是成熟的行为知识层 | 有 `stardew-core` / `stardew-social` / `stardew-navigation` | Stardew skill 内容还薄 | 现在像只有简短说明书，参考项目更像有完整生存手册 |
| 自动化 / Cron / 后台任务 | 高 | 可以定时、后台、无人值守执行 | 有 Schedule / Todo 基础 | 还没变成 NPC 日程和日常生活 | 目标是“几点去哪里、该干嘛自己安排”，现在还没到 |
| Gateway / Session | 中高 | 多入口、会话状态、控制命令成熟 | Desktop 聊天和 runtime UI 有基础 | Stardew 村庄事件和 NPC 会话路由还弱 | 现在缺“村庄消息总线”，谁听到什么、谁该回应还不完整 |
| 子 Agent / 委派 | 中 | 能派生子 agent 并行做事 | 有通用 `AgentService` | 不适合直接当长期 NPC | 临时帮手不是村民；NPC 需要长期身份，不是一次性任务员 |
| Provider Routing / Fallback | 中 | 多模型路由、失败回退、key 池成熟 | 有 provider 基础 | 多 NPC 并发时成本、失败、限流处理不足 | 多个 NPC 同时想事情时，现在还缺“排队和备用方案” |
| 安全 / 回滚 / 审计 | 中 | 审批、日志、回滚、敏感处理较完整 | 有 bridge token、activity、trace 基础 | NPC 行为链路审计不完整 | 出事时要能查“谁在什么时候为什么做了什么”，现在证据链还不够顺 |
| 消息网关 / 多平台 | 低到中 | Telegram / Discord / Slack 等入口成熟 | Desktop 本地为主 | 对 Stardew 核心目标不是第一优先 | 这更像遥控器，不是村庄大脑 |
| 媒体 / 浏览器 / 图像 | 低 | 参考项目能力丰富 | 当前也有部分工具 | 和 Stardew NPC 自主生活关系不大 | 暂时不是让 NPC 活起来的关键 |
| 研究 / 评测 / 批处理 | 低到中 | 能批量跑轨迹、导出评测数据 | 当前较弱 | 后期评估 NPC 行为质量有用 | 等 NPC 真能生活了，再评估它活得像不像人 |

## 对核心目标最有帮助的模块排序

1. Agent 运行时 / 自治循环
2. Context / Prompt 组装
3. Memory / Soul 独立隔离
4. Stardew 专属工具集
5. Skills 游戏知识层
6. Background / Cron 日程自动化
7. Trace / Activity 审计
8. Gateway / Session 状态管理

## 最核心差距

当前项目已经有“身体”和“骨架”，参考项目有更成熟的“长期自主大脑”。

差距主要不是缺 UI 或缺单次调试按钮，而是 NPC 还没有持续观察、记忆、决策、行动的生活循环。
