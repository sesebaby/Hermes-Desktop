# Claude Code 超级能力与 Oh My ClaudeCode 安装使用说明

## 介绍

Claude Code 是基于 Claude AI 的代码编辑工具，支持插件和扩展。本文档介绍如何安装和使用两个强大的插件：**超级能力 (Superpowers)** 和 **Oh My ClaudeCode (OMC)**。

- **超级能力 (Superpowers)**：提供工程流程守卫技能，如测试驱动开发 (TDD)、调试、规划等，帮助您自信地交付大型功能。
- **Oh My ClaudeCode (OMC)**：多代理编排框架，实现零学习曲线的团队模式开发。

## 超级能力 (Superpowers) 安装

超级能力插件适用于 Claude Code、Cursor 等平台。以下是 Claude Code 的安装步骤：

### 官方市场安装（推荐）

1. 在 Claude Code 中运行：
   ```
   /plugin install superpowers@claude-plugins-official
   ```

### 替代市场安装

1. 添加市场：
   ```
   /plugin marketplace add obra/superpowers-marketplace
   ```

2. 安装插件：
   ```
   /plugin install superpowers@superpowers-marketplace
   ```

### 验证安装

安装后，运行 `/help` 查看帮助。应看到以下命令：
- `/superpowers:brainstorm` - 交互式设计完善
- `/superpowers:write-plan` - 创建实施计划
- `/superpowers:execute-plan` - 分批执行计划

### 使用说明

超级能力会根据上下文自动激活技能。例如：
- 说 "帮我规划这个功能" 会触发 `/superpowers:write-plan`
- 说 "调试这个问题" 会触发相关调试技能

## Oh My ClaudeCode (OMC) 安装

OMC 支持团队模式编排，提供多种安装方式。

### 插件市场安装（推荐）

1. 添加市场：
   ```
   /plugin marketplace add https://github.com/Yeachan-Heo/oh-my-claudecode
   ```

2. 安装插件：
   ```
   /plugin install oh-my-claudecode
   ```

3. 设置：
   ```
   /omc-setup
   ```

### NPM 安装（命令行工具）

1. 安装：
   ```
   npm install -g claudecode-omc
   ```

2. 设置：
   ```
   omc-manage setup
   ```

### 验证安装

运行 `/help` 查看命令，或使用 `omc-manage doctor` 进行健康检查。

### 使用说明

OMC 提供多种模式：

#### 团队模式（推荐）

启用原生团队：在 `~/.claude/settings.json` 中添加：
```json
{
  "CLAUDE_TEAMS": "true"
}
```

#### CLI 工作器（Codex & Gemini）

使用 `/omc-teams` 在 tmux 分屏中生成真实 CLI 进程。

#### 快速构建

使用 `autopilot: 构建一个 REST API 用于管理任务` 等命令开始自动构建。

### 管理命令

- `omc-manage setup [--force]` - 安装合并的构件
- `omc-manage doctor` - 健康检查
- `omc-manage source sync` - 更新上游源
- `omc-manage artifact list` - 列出构件

## 更新

### 超级能力更新

在 Claude Code 中：
```
 /plugin update superpowers
```

### OMC 更新

通过市场：
```
 /plugin marketplace update omc
 /omc-setup
```

通过 NPM：
```
 npm install -g oh-my-claude-sisyphus@latest
```

## 故障排除

- 如果插件命令未显示，尝试重新安装或重启 Claude Code。
- 确保 tmux 已安装（用于 OMC 的某些功能）。
- 检查设置文件和权限。

## 资源链接

- [超级能力 GitHub](https://github.com/obra/superpowers)
- [Oh My ClaudeCode GitHub](https://github.com/Yeachan-Heo/oh-my-claudecode)
- [Claude Code 文档](https://docs.anthropic.com/claude/docs/claude-code)