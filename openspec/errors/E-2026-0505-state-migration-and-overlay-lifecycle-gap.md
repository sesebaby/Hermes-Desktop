---
id: E-2026-0505-state-migration-and-overlay-lifecycle-gap
title: 新字段和 overlay 重构只测新路径会漏掉旧库迁移与旧生命周期残留
updated_at: 2026-05-05
keywords:
  - state.db
  - sqlite migration
  - overlay lifecycle
  - activeClickableMenu
  - reasoning_content
  - private_chat
---

## symptoms

- 给 `messages` 表新增字段后，新建库测试通过，但旧 `state.db` 写入时报 `no such column`。
- 把 Stardew 私聊从 `activeClickableMenu` 菜单迁到 overlay 后，旧的菜单关闭监听仍可能误发 `player_private_message_cancelled`。
- 主 agent tool-call 路径保存了 `reasoning_content` 后，后台 `MemoryReviewService` 自己组装的 review tool-call 消息仍可能丢掉 reasoning 字段，继续触发 OpenAI/DeepSeek thinking mode `400 Bad Request`。

## trigger_scope

- SQLite schema 新增列。
- 从阻塞菜单迁移到非阻塞 overlay。
- 改动 provider replay/persistence 字段，例如 OpenAI `reasoning_content`。
- 改动 Stardew 私聊输入、关闭、提交、取消生命周期。

## root_cause

实现和测试只证明“新流程能跑”，没有证明“旧数据结构能升级”和“旧生命周期状态已经退役”。

## bad_fix_paths

- 只在 `CREATE TABLE IF NOT EXISTS` 中加列，不写 `ALTER TABLE` 迁移。
- 只改生产入口，不删除或断开旧 menu/input pending 状态。
- 只断言新文件存在，不断言旧坏路径不存在。
- 把 overlay 关闭/失焦当作 UI 细节，不发明确业务事件。
- 只修 `Agent`/`AgentLoopScaffold` 的 assistant tool-call 保存点，不检查后台 review、memory、skill self-evolution 等独立 tool loop。

## corrective_constraints

- 新增持久化列时必须加旧 schema 升级测试，手工创建旧表后验证新字段可写可读。
- 从 `activeClickableMenu` 改 overlay 时，旧菜单生命周期监听必须删除或隔离，不能继续发业务取消事件。
- 回归测试必须同时覆盖新路径存在和旧坏路径不存在。
- overlay 的提交、取消、关闭、失焦必须有明确状态和日志，不能依赖 Stardew 菜单事件推断。
- 每个独立 LLM tool loop 在把 `ChatResponse` 转成 assistant `Message` 时，都必须复制 `Reasoning`、`ReasoningContent`、`ReasoningDetails`、`CodexReasoningItems`。
- 克隆消息快照时也必须保留 reasoning 字段，否则后台任务会二次丢失 provider 要求回放的字段。

## verification_evidence

- `Constructor_UpgradesExistingStateDbWithReasoningColumns` 覆盖旧 `state.db` 升级后 reasoning 字段 roundtrip。
- `PrivateChatInputCloseWithoutEnterRecordsCancellation` 改为断言 `ModEntry` 和 `BridgeCommandQueue` 不再使用旧 menu 关闭取消路径。
- `SubmitAsync_OpenPrivateChat_AcceptsPhoneThreadOpenStates` 覆盖 `Opened=false + openState=thread_opened` 的 accepted 分支。
- `ReviewConversationAsync_ReplaysReasoningFieldsAfterReviewToolCall` 覆盖后台 memory/skill review tool loop 的 reasoning 回放。
