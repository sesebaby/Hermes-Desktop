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
- 把 Stardew 远程/主动消息迁到 phone overlay 时，如果误把玩家点击 NPC 的 `open_private_chat` 也迁走，会丢掉真实私聊输入窗口。
- 主 agent tool-call 路径保存了 `reasoning_content` 后，后台 `MemoryReviewService` 自己组装的 review tool-call 消息仍可能丢掉 reasoning 字段，继续触发 OpenAI/DeepSeek thinking mode `400 Bad Request`。

## trigger_scope

- SQLite schema 新增列。
- 从阻塞菜单迁移到非阻塞 overlay，但仅限远程/主动消息 UI，不能误迁玩家点击 NPC 私聊输入窗口。
- 改动 provider replay/persistence 字段，例如 OpenAI `reasoning_content`。
- 改动 Stardew 私聊输入、关闭、提交、取消生命周期。

## root_cause

实现和测试只证明“新流程能跑”，没有证明“旧数据结构能升级”和“旧生命周期状态已经退役”。

## bad_fix_paths

- 只在 `CREATE TABLE IF NOT EXISTS` 中加列，不写 `ALTER TABLE` 迁移。
- 只改生产入口，不校验该入口到底是玩家点击 NPC 私聊还是远程/主动手机消息。
- 只断言新文件存在，不断言旧坏路径不存在。
- 把 overlay 关闭/失焦当作 UI 细节，不发明确业务事件。
- 只修 `Agent`/`AgentLoopScaffold` 的 assistant tool-call 保存点，不检查后台 review、memory、skill self-evolution 等独立 tool loop。

## corrective_constraints

- 新增持久化列时必须加旧 schema 升级测试，手工创建旧表后验证新字段可写可读。
- 从 `activeClickableMenu` 改 overlay 时，必须先确认改的是远程/主动消息链路；玩家点击 NPC 的 `PrivateChatInputMenu` 仍是生产路径，关闭监听必须保留并只服务该输入菜单。
- 回归测试必须同时覆盖新路径存在和旧坏路径不存在。
- overlay 的提交、取消、关闭、失焦必须有明确状态和日志，不能依赖 Stardew 菜单事件推断；`PrivateChatInputMenu` 的提交、取消、关闭仍走菜单生命周期。
- private chat 回复展示必须按提交来源分流：`input_menu` 来源用原版样式 `DialogueBox` 并走菜单关闭生命周期，`phone_overlay` / NPC 主动来源才走 8 格气泡或手机生命周期。
- 每个独立 LLM tool loop 在把 `ChatResponse` 转成 assistant `Message` 时，都必须复制 `Reasoning`、`ReasoningContent`、`ReasoningDetails`、`CodexReasoningItems`。
- 克隆消息快照时也必须保留 reasoning 字段，否则后台任务会二次丢失 provider 要求回放的字段。

## verification_evidence

- `Constructor_UpgradesExistingStateDbWithReasoningColumns` 覆盖旧 `state.db` 升级后 reasoning 字段 roundtrip。
- `PrivateChatInputCloseWithoutEnterRecordsCancellation` 覆盖真实 `PrivateChatInputMenu` 关闭取消路径。
- `SubmitAsync_OpenPrivateChat_AcceptsInputMenuOpenState` 覆盖 `Opened=false + openState=input_menu_opened` 的 accepted 分支。
- `SubmitAsync_OpenPrivateChat_RejectsPhoneThreadOpenState` 覆盖 `thread_opened` 不得被当作 `/action/open_private_chat` 成功。
- `PrivateChatReplyCloseIsRecordedBySourceSpecificUiOwner` 覆盖 `input_menu` 回复用 DialogueBox 关闭事件，phone/bubble 回复用各自生命周期。
- `ReviewConversationAsync_ReplaysReasoningFieldsAfterReviewToolCall` 覆盖后台 memory/skill review tool loop 的 reasoning 回放。
