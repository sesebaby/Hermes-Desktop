# Context Snapshot: Python Skill Self-Evolution Parity

## Task Statement
全面对齐 C# Hermes Desktop 与 Python `external/hermes-agent-main` 的自我进化能力，基于当前讨论重点：记忆、session_search、技能创建/修补、后台复盘闭环。

## Desired Outcome
C# agent 能像 Python 参考项目一样把复杂任务经验沉淀为 procedural memory：模型可查看技能、创建技能、patch 技能，并在复杂工具调用后由后台复盘触发 memory/skill review，不阻塞用户响应。

## Known Facts / Evidence
- Python `agent/prompt_builder.py` 定义 `SKILLS_GUIDANCE`，只有 `skill_manage` 可用时注入。
- Python `tools/skill_manager_tool.py` 暴露 `skill_manage`，actions: create, patch, edit, delete, write_file, remove_file。
- Python `run_agent.py` 读取 `skills.creation_nudge_interval`，按工具调用迭代数触发后台 skill review。
- C# `SkillManager` 已有 Create/Edit/Patch/Delete 方法，但当前只注册 `SkillInvokeTool`。
- C# `MemoryReviewService` 只暴露 memory tool，不执行 skill review。
- C# `DreamerService` 是沙盒自由联想/构想系统，不是 Python self-evolution parity 链路。

## Constraints
- Reference-first: 先按 `external/hermes-agent-main` 语义对齐，不把 repo-local Dreamer 当成替代。
- 不引入新依赖。
- 不保留 JSONL session storage 回退作为设计目标；此前已转 SQLite/FTS5。
- 不提交 `.omx/logs` 和 `.omx/state/session.json` runtime dirties。
- Commit message 如提交必须遵守 Lore Commit Protocol。

## Unknowns / Open Questions
- 是否要在本轮完整实现 `write_file` / `remove_file` 支持文件目录限制；为避免 partial parity，应实现。
- 是否要同步 Python 的 `skills_list` / `skill_view`，参考后台 review prompt 需要这两个工具；应实现。
- UI 是否需要展示新工具调用细节；现有 ActivityLog 会自动记录工具调用，不新增 UI。

## Likely Codebase Touchpoints
- `src/Core/MemoryReferenceText.cs`
- `src/Core/SystemPrompts.cs`
- `src/Core/Agent.cs`
- `src/Tools/SkillInvokeTool.cs` 或新增 `SkillManageTool`, `SkillsListTool`, `SkillViewTool`
- `src/skills/SkillManager.cs`
- `src/memory/MemoryReviewService.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop.Tests/Services/*`
