# Superpowers 技能列表 (v5.0.7)

> 来源：superpowers-marketplace
> 安装路径：`~/.claude/plugins/cache/superpowers-marketplace/superpowers/5.0.7/`

## 安装与升级

### 安装

```bash
# 方式1：在 Claude Code 输入框中直接输入
/plugin install superpowers@superpowers-marketplace

# 方式2：CLI 命令
npx -y @anthropic-ai/claude-code plugin install superpowers@superpowers-marketplace
```

### 升级

```bash
# 方式1：在 Claude Code 输入框中直接输入
/plugin update superpowers@superpowers-marketplace

# 方式2：CLI 命令
npx -y @anthropic-ai/claude-code plugin update superpowers@superpowers-marketplace
```

### 卸载

```bash
/plugin uninstall superpowers
```

## 使用方法

直接对 Claude 说 **"使用 XXX 技能"** 即可，我会自动调用 `Skill` 工具加载对应的技能内容。

---

## 技能清单

| # | 技能名称 | 用途 | 触发关键词 |
|---|---------|------|-----------|
| 1 | `using-superpowers` | **入口技能** - 会话启动时自动加载，指导如何发现和使用其他技能 | 自动触发 |
| 2 | `test-driven-development` | **TDD 工作流** - 测试驱动开发，红-绿-重构循环 | "使用 TDD 技能" |
| 3 | `systematic-debugging` | **系统化调试** - 结构化调试方法，根因分析 | "使用 debugging 技能" |
| 4 | `brainstorming` | **头脑风暴** - 设计方案前的探索性思考 | "使用 brainstorming 技能" |
| 5 | `writing-plans` | **编写计划** - 生成可执行的实施计划 | "使用写计划技能" |
| 6 | `executing-plans` | **执行计划** - 按计划逐步实施 | "使用执行计划技能" |
| 7 | `subagent-driven-development` | **子代理驱动开发** - 使用子代理并行工作 | "使用子代理开发技能" |
| 8 | `requesting-code-review` | **请求代码审查** - 提交代码审查请求 | "请求代码审查" |
| 9 | `receiving-code-review` | **接收代码审查** - 处理审查反馈 | "接收代码审查" |
| 10 | `verification-before-completion` | **完成前验证** - 任务完成前的检查清单 | "使用验证技能" |
| 11 | `writing-skills` | **编写技能** - 创建和修改 Skill 文件 | "使用写技能技能" |
| 12 | `dispatching-parallel-agents` | **并行代理调度** - 同时派发多个子代理 | "使用并行代理技能" |
| 13 | `finishing-a-development-branch` | **完成开发分支** - 分支收尾工作流 | "使用完成分支技能" |
| 14 | `using-git-worktrees` | **Git Worktree 使用** - 隔离工作区管理 | "使用 worktree 技能" |
| 15 | `frontend-design` | **前端设计** - 高质量前端界面构建 | "使用前端设计技能" |
| 16 | `mcp-builder` | **MCP 服务构建** - 创建 MCP 服务器 | "使用 MCP 构建技能" |
| 17 | `doc-coauthoring` | **文档协作** - 结构化文档编写 | "使用文档协作技能" |
| 18 | `canvas-design` | **画布设计** - 视觉艺术创作 | "使用画布设计技能" |
| 19 | `brand-guidelines` | **品牌指南** - Anthropic 品牌色彩应用 | "使用品牌指南技能" |
| 20 | `internal-comms` | **内部沟通** - 公司内部通讯撰写 | "使用内部沟通技能" |
| 21 | `algorithmic-art` | **算法艺术** - p5.js 生成艺术 | "使用算法艺术技能" |
| 22 | `theme-factory` | **主题工厂** - 文档/幻灯片主题样式 | "使用主题工厂技能" |
| 23 | `web-artifacts-builder` | **Web 构建器** - HTML 构件创建 | "使用 Web 构建器技能" |
| 24 | `webapp-testing` | **Web 测试** - Playwright 前端测试 | "使用 Web 测试技能" |
| 25 | `slack-gif-creator` | **Slack GIF 制作** - 动画 GIF 创建 | "使用 GIF 制作技能" |
| 26 | `docx` | **Word 文档** - .docx 创建与编辑 | "使用 Word 文档技能" |
| 27 | `pptx` | **PPT 演示** - .pptx 创建与编辑 | "使用 PPT 技能" |
| 28 | `xlsx` | **Excel 表格** - .xlsx 创建与编辑 | "使用 Excel 技能" |
| 29 | `pdf` | **PDF 处理** - PDF 创建与提取 | "使用 PDF 技能" |
| 30 | `skill-creator` | **技能创建器** - 创建新 Skill 的指南 | "使用技能创建器" |
| 31 | `jianghu-codex-plan` | **JiangHu 计划** - Codex 任务编排计划 | "使用 JiangHu 计划" |
| 32 | `jianghu-codex-impl` | **JiangHu 实施** - Codex 任务编排执行 | "使用 JiangHu 实施" |
| 33 | `omc-reference` | **OMC 参考** - oh-my-claudecode 代理目录与工具 | "使用 OMC 参考" |

---

## oh-my-claudecode 命令（补充）

oh-my-claudecode (v4.10.2) 提供的是 **slash command 风格** 的命令：

| 命令 | 用途 |
|------|------|
| `/oh-my-claudecode:autopilot` | 自动驾驶模式 |
| `/oh-my-claudecode:ultrawork` | 超工作模式 |
| `/oh-my-claudecode:ralph` | Ralph 模式 |
| `/oh-my-claudecode:team` | 团队编排 |
| `/oh-my-claudecode:ralplan` | Ralph 计划模式 |
| `/oh-my-claudecode:deep-interview` | 深度访谈 |
| `/oh-my-claudecode:ai-slop-cleaner` | AI 垃圾清理 |
| `/oh-my-claudecode:analysis` | 分析模式 |
| `/oh-my-claudecode:tdd` | TDD 模式 |
| `/oh-my-claudecode:deepsearch` | 代码库搜索 |
| `/oh-my-claudecode:ultrathink` | 深度推理 |
| `/oh-my-claudecode:cancel` | 取消执行模式 |

---

*文档生成时间: 2026-04-26*
