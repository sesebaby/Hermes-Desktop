# Hermes 主要功能模块整理

资料来源：

- Hermes 文档首页：https://hermes-agent.nousresearch.com/docs
- 功能总览：https://hermes-agent.nousresearch.com/docs/user-guide/features/overview
- 工具与工具集：https://hermes-agent.nousresearch.com/docs/user-guide/features/tools
- 记忆系统：https://hermes-agent.nousresearch.com/docs/user-guide/features/memory
- 技能系统：https://hermes-agent.nousresearch.com/docs/user-guide/features/skills
- 消息网关：https://hermes-agent.nousresearch.com/docs/user-guide/messaging/
- 集成总览：https://hermes-agent.nousresearch.com/docs/integrations/
- 架构总览：https://hermes-agent.nousresearch.com/docs/developer-guide/architecture

## 1. 产品定位

Hermes Agent 是一个可长期运行的自治 AI 代理。它不是普通聊天机器人，也不是绑定 IDE 的代码助手，而是一个具备工具调用、跨会话记忆、技能沉淀、多入口访问和自动化执行能力的代理运行时。

Hermes 的核心特色是“闭环学习”：代理能够从使用经验中沉淀记忆，创建和改进技能，跨会话检索历史经验，并逐步形成对用户偏好、项目环境和工作方式的理解。

## 2. 核心功能模块总览

| 模块 | 主要功能 |
| --- | --- |
| 代理运行时 | 多轮模型调用、工具调用、提示词组装、上下文压缩、会话状态管理 |
| 工具与工具集 | Web、终端、文件、浏览器、视觉、图像生成、TTS、记忆、委派、定时任务等工具 |
| 记忆系统 | `MEMORY.md`、`USER.md`、跨会话持久记忆、外部记忆 Provider |
| 技能系统 | `SKILL.md`、按需加载、斜杠命令、开放技能标准、技能创建与改进 |
| 项目上下文 | 自动发现并加载 `.hermes.md`、`AGENTS.md`、`CLAUDE.md`、`SOUL.md`、`.cursorrules` 等上下文文件 |
| 自动化 | Cron 定时任务、子代理委派、代码执行、事件 Hook、批处理 |
| 媒体与 Web | 语音模式、浏览器自动化、视觉分析、图片粘贴、图像生成、TTS/STT |
| 消息网关 | Telegram、Discord、Slack、WhatsApp、Signal、Email、SMS、飞书、企业微信等多平台接入 |
| 集成能力 | AI Provider、Provider Routing、Fallback Provider、Credential Pool、MCP、API Server、ACP 编辑器集成 |
| 定制化 | `SOUL.md`、人格预设、皮肤主题、插件、Hook |
| 安全与回滚 | 命令审批、用户白名单、容器隔离、工作区检查点、`/rollback` |
| 研究与评测 | 批处理、轨迹导出、ShareGPT 格式数据、RL 训练数据生成 |

## 3. 代理运行时

Hermes 的核心是代理循环。代理运行时负责接收用户消息、组装系统提示词、选择模型 Provider、调用模型、解析工具调用、派发工具、接收工具结果，并继续推进多轮推理直到任务完成。

主要能力包括：

- 模型调用：支持多种 Provider 和不同 API 模式。
- 工具调度：通过工具注册表收集工具 schema，并将模型发起的工具调用派发到具体实现。
- 提示词组装：把系统规则、人格、记忆、上下文文件、会话历史和当前输入组合成模型请求。
- 上下文压缩：在会话变长后压缩历史内容，降低 token 消耗。
- 会话存储：使用 SQLite 和 FTS5 保存会话、状态和检索索引。
- 多入口复用：CLI、消息网关、API Server、ACP 编辑器、批处理和 Python Library 都可以进入同一个代理运行时。

## 4. 工具与工具集

Hermes 使用工具扩展代理能力。工具按 toolset 组织，可以根据平台或使用场景启用、禁用或组合。

高层工具类别包括：

- Web：网页搜索、网页内容提取、网页抓取。
- 终端与文件：执行命令、管理进程、读取文件、编辑文件、应用补丁。
- 浏览器：页面导航、页面快照、基于视觉的浏览器自动化。
- 媒体：图像分析、图像生成、文本转语音。
- 代理编排：待办事项、澄清问题、代码执行、子代理委派。
- 记忆与召回：持久记忆、历史会话搜索。
- 自动化与投递：定时任务、消息发送。
- 外部集成：Home Assistant、MCP 工具、强化学习相关工具等。

Hermes 的终端工具支持多种执行后端：

- `local`：在本机执行命令。
- `docker`：在 Docker 容器中执行。
- `ssh`：在远程服务器中执行。
- `singularity`：在 HPC/集群环境中执行。
- `modal`：在云端 serverless 环境中执行。
- `daytona`：在持久化云工作区中执行。
- `vercel_sandbox`：在 Vercel Sandbox 云微虚拟机中执行。

Hermes 还支持后台进程管理，包括启动、轮询、等待、查看日志、终止和向进程写入输入。

## 5. 记忆系统

Hermes 的内置记忆由两个核心文件组成：

- `MEMORY.md`：代理自己的笔记，用于记录环境事实、项目约定、经验和学到的内容。
- `USER.md`：用户画像，用于记录用户偏好、沟通风格和期望。

这两个文件存储在 `~/.hermes/memories/` 下，并在会话开始时以冻结快照的形式注入系统提示词。记忆有字符限制，目的是让长期记忆保持聚焦。当记忆接近容量上限时，代理需要合并、替换或删除旧内容。

除内置文件记忆外，Hermes 还支持外部记忆 Provider，例如 Honcho、OpenViking、Mem0、Hindsight、Holographic、RetainDB 和 ByteRover，用于跨会话用户建模和个性化。

## 6. 技能系统

Hermes 的技能是按需加载的知识文档，通常以 `SKILL.md` 表达。技能可以记录工具使用方法、工作流、领域知识、常见坑点和验证步骤。

技能系统的主要特点：

- 技能默认位于 `~/.hermes/skills/`。
- 技能可以作为斜杠命令调用。
- 技能支持自然语言发现和调用。
- 技能采用 progressive disclosure，先加载技能列表，只有需要时才加载完整技能内容。
- 技能可以包含元数据、平台限制、所需工具集、配置说明和执行步骤。
- 技能兼容 agentskills.io 开放标准。
- 代理可以创建、修改或删除技能，把经验沉淀成可复用流程。

技能加载分为多个层级：

- Level 0：只加载技能名称、描述和分类。
- Level 1：加载某个技能的完整内容和元数据。
- Level 2：加载技能引用的具体文件或补充资料。

## 7. 项目上下文与人格系统

Hermes 会自动发现并加载项目上下文文件，用来塑造代理在所在工作区中的行为方式。

常见上下文文件包括：

- `.hermes.md`
- `AGENTS.md`
- `CLAUDE.md`
- `SOUL.md`
- `.cursorrules`

`SOUL.md` 是人格与身份相关的核心文件。Hermes 支持全局人格定义，也支持在会话中切换内置或自定义 personality 预设。人格系统决定代理的默认表达风格、身份设定、沟通习惯和行为倾向。

## 8. 自动化模块

Hermes 的自动化能力包括定时任务、子代理委派、代码执行、事件 Hook 和批处理。

### 8.1 定时任务

Hermes 内置 cron 能力，可以用自然语言或 cron 表达式创建自动任务。任务可以暂停、恢复、编辑、立即运行和删除。执行结果可以投递到配置好的消息平台。

### 8.2 子代理委派

Hermes 可以通过 `delegate_task` 派生子代理。子代理拥有隔离上下文、受限工具集和独立终端会话，可用于并行处理多个工作流。

### 8.3 代码执行

Hermes 的 `execute_code` 工具允许代理编写 Python 脚本，并在脚本中以程序化方式调用 Hermes 工具。这样可以把多步骤工具流水线压缩成一次模型回合。

### 8.4 事件 Hook

Hermes 有三类 Hook：

- Gateway hooks：通过 `HOOK.yaml` 和 `handler.py` 注册，主要用于消息网关的日志、告警和 Webhook。
- Plugin hooks：通过插件中的 `ctx.register_hook()` 注册，可用于工具拦截、指标和护栏。
- Shell hooks：在配置文件中指向 shell 脚本，可用于阻塞、自动格式化和上下文注入。

Hook 错误会被捕获并记录，不会导致代理崩溃。

### 8.5 批处理

Hermes 可以并行运行大量 prompt，生成结构化轨迹数据，用于训练数据生成、评测或研究。

## 9. 媒体与 Web 模块

Hermes 的媒体与 Web 能力包括：

- 语音模式：支持 CLI、消息平台和 Discord 语音频道中的实时语音交互。
- 浏览器自动化：支持 Browserbase、Browser Use、本地 Chrome CDP、本地 Chromium 等后端。
- 视觉与图片粘贴：可以分析剪贴板图片或上传图片。
- 图像生成：通过图像生成 Provider 调用多种图像模型。
- 文本转语音：支持 Edge TTS、ElevenLabs、OpenAI TTS、MiniMax、NeuTTS 等。
- 语音转文字：支持本地 Whisper、Groq 和 OpenAI Whisper API。

## 10. 消息网关

Hermes 的消息网关是一个后台进程，负责连接多个聊天平台，并把平台消息路由到代理运行时。

支持的平台包括：

- Telegram
- Discord
- Slack
- WhatsApp
- Signal
- SMS
- Email
- Home Assistant
- Mattermost
- Matrix
- DingTalk
- Feishu/Lark
- WeCom
- Weixin
- BlueBubbles
- QQ
- Yuanbao
- Open WebUI
- Webhooks

消息网关的主要职责：

- 接收平台消息。
- 按平台和聊天维护会话。
- 将消息派发给代理运行时。
- 运行 cron 调度器。
- 发送文本、语音、图片、文件和流式更新。
- 展示工具进度。
- 管理用户白名单和 DM 配对。
- 支持中断、排队、steer 注入和后台会话。

消息平台内置命令包括：

- `/new` 或 `/reset`：开始新会话。
- `/model`：查看或切换模型。
- `/personality`：设置人格。
- `/retry`：重试上一条消息。
- `/undo`：移除上一轮对话。
- `/status`：查看会话状态。
- `/stop`：停止当前代理运行。
- `/approve`、`/deny`：审批或拒绝危险命令。
- `/compress`：手动压缩上下文。
- `/resume`：恢复命名会话。
- `/voice`：控制语音能力。
- `/rollback`：查看或恢复文件系统检查点。
- `/background`：启动后台任务。
- `/reload-mcp`：重载 MCP 服务器。
- `/update`：更新 Hermes。
- `/help`：显示帮助。
- `/<skill-name>`：调用已安装技能。

## 11. 集成模块

Hermes 的集成能力覆盖模型 Provider、工具服务器、浏览器、语音、API 和编辑器。

### 11.1 AI Provider 与路由

Hermes 支持 OpenRouter、Anthropic、OpenAI、Google 以及任意 OpenAI-compatible endpoint。它可以检测 Provider 是否支持视觉、流式输出和工具调用。

Provider Routing 支持按成本、速度、质量、白名单、黑名单和优先级控制底层模型选择。

### 11.2 Fallback Provider

当主模型或 Provider 出错时，Hermes 可以自动切换到备用 Provider。辅助任务也可以有独立回退配置，例如视觉、压缩和网页提取。

### 11.3 Credential Pool

Hermes 支持同一 Provider 的多 API Key 池。当遇到限流或失败时，可以自动轮换凭证。

### 11.4 MCP

Hermes 支持通过 Model Context Protocol 连接外部工具服务器。MCP 服务器可通过 stdio 或 HTTP/SSE 传输接入，并支持按服务器过滤工具。

### 11.5 Web Search Backends

Hermes 的 `web_search` 和 `web_extract` 支持多个后端：

- Firecrawl
- Parallel
- Tavily
- Exa

### 11.6 Browser Backends

Hermes 的浏览器自动化支持多个后端：

- Browserbase
- Browser Use
- 本地 Chrome CDP
- 本地 Chromium

### 11.7 API Server

Hermes 可以暴露 OpenAI-compatible HTTP 接口。任何兼容 OpenAI API 格式的前端都可以连接 Hermes，例如 Open WebUI、LobeChat、LibreChat、NextChat 和 ChatBox。

### 11.8 ACP 编辑器集成

Hermes 可以作为 ACP Server 接入 VS Code、Zed、JetBrains 等兼容编辑器，在编辑器内展示聊天、工具活动、文件 diff 和终端命令。

## 12. 插件与定制化

Hermes 支持插件系统，可在不修改核心代码的情况下增加工具、Hook 和集成。

插件类型包括：

- 通用插件：添加工具或 Hook。
- 记忆 Provider 插件：接入外部长期记忆系统。
- 上下文引擎插件：替换或扩展上下文管理机制。

Hermes 还支持 CLI 外观定制，包括 banner 颜色、spinner、响应框标签、品牌文本和工具活动前缀。

## 13. 安全与回滚

Hermes 的安全机制包括：

- 消息网关默认拒绝未授权用户。
- 支持平台级用户白名单。
- 支持 DM 配对授权。
- 危险命令需要审批。
- 终端命令可运行在容器、远程服务器或云沙箱中。
- 容器后端支持只读根文件系统、丢弃 Linux capabilities、禁止权限提升和进程限制。
- 文件修改前创建工作区检查点。
- 支持通过 `/rollback` 恢复检查点。
- 工具输出可进行敏感信息和密钥扫描。

## 14. 研究与评测能力

Hermes 包含面向研究和评测的能力：

- 批量运行大量 prompt。
- 并行生成代理轨迹。
- 导出 ShareGPT 格式数据。
- 导出会话和工具调用轨迹。
- 支持强化学习训练数据生成。
- 支持使用 Atropos 进行 RL 训练相关流程。

## 15. 架构入口

Hermes 的主要入口包括：

- CLI：命令行聊天和本地交互。
- Gateway：消息平台后台网关。
- ACP：编辑器协议适配。
- Batch Runner：批处理运行器。
- API Server：OpenAI-compatible HTTP 服务。
- Python Library：作为 Python 库嵌入使用。

这些入口最终连接到同一个代理运行时，再由代理运行时调度 Provider、工具注册表、会话存储、终端后端、浏览器后端、Web 后端、MCP 和媒体工具。
