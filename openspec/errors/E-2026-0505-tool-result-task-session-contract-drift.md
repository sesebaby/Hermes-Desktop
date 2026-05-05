---
id: E-2026-0505-tool-result-task-session-contract-drift
title: Tool result 持久化合同只改成功路径会漏掉拒绝分支
updated_at: 2026-05-05
keywords:
  - Agent.cs
  - tool result
  - TaskSessionId
  - permission denied
  - transcript
---

## symptoms

- 计划要求所有新写入的 `Role = "tool"` 消息持久化 `TaskSessionId = session.ToolSessionId ?? session.Id`。
- 普通 tool 执行结果已设置 `TaskSessionId`，但 permission rule deny 和 user deny 分支仍保存 `TaskSessionId = null`。
- 架构复核才发现 denied 的 `todo` / `todo_write` 结果仍可能形成 mixed-era 数据面。

## trigger_scope

- 修改 `Agent` / tool loop / transcript 保存合同。
- 新增 tool result 字段、索引、恢复归属键或 replay 必需字段。
- 权限、异常、取消、fallback 等非成功分支也会保存 `Role = "tool"` 消息时。

## root_cause

实现按“成功执行 tool result”理解合同，没有枚举所有构造并保存 `Role = "tool"` 的路径，导致拒绝分支与主路径语义漂移。

## bad_fix_paths

- 只 grep `TaskSessionId = session.ToolSessionId`，不同时 grep `Role = "tool"` 和 `SaveMessageAsync`。
- 只给成功执行路径补字段，不检查 permission deny、user deny、tool exception、fallback 等旁路。
- 只用成功的 `todo` 测试验证 transcript round-trip，不让 denied 分支落真实 `TranscriptStore`。

## corrective_constraints

- 改动 tool result 持久化合同时，必须同时枚举 `Role = "tool"` 消息创建点和 transcript 保存点。
- 合同字段必须覆盖成功、失败、权限拒绝、用户拒绝、fallback 等所有会发给模型的 tool response。
- 至少用一个真实 `TranscriptStore` 测试覆盖非成功分支，断言新字段已经落库并能读回。

## verification_evidence

- `AgentChatAsync_WhenPermissionRuleDeniesTool_PersistsTaskSessionIdOnDenialResult` 先失败为 `TaskSessionId == null`，修复后通过。
- `AgentChatAsync_WhenPermissionPromptDeniesTool_PersistsTaskSessionIdOnDenialResult` 先失败为 `TaskSessionId == null`，修复后通过。
