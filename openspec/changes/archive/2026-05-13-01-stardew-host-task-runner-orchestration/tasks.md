## 1. 盘点与退役范围

- [x] 1.1 搜索并列出 Stardew gameplay 路径中的 `NpcLocalExecutorRunner`、`INpcLocalExecutorRunner`、`local_executor`、`npc_delegate_action`、fallback speak、私聊自检 prompt、旧测试期望和文档引用
- [x] 1.2 标记哪些旧能力必须删除，哪些旧入口必须替换为直白 host task submitter 名称，产出一份迁移清单
- [x] 1.3 增加负向测试，证明 Stardew v1 autonomy/private chat 构造时不会接入小模型 gameplay executor

## 2. Host Task Contract

- [x] 2.1 扩展或整理 `PendingWorkItem`、`ActionSlot`、`IngressWorkItem` 的字段语义，覆盖 source、action、traceId、workItemId/taskId、commandId、idempotencyKey 和 terminal fact
- [x] 2.2 统一 host task 状态机，明确 queued/submitting/running/completed/blocked/failed/cancelled/timeout/stuck 的状态转换
- [x] 2.3 补齐 action slot timeout/stuck watchdog，把超时任务转成 terminal fact 并释放资源
- [x] 2.4 补齐 restart/replay/idempotency 恢复逻辑，避免重复提交已提交 bridge command
- [x] 2.5 更新 runtime jsonl 记录，让每个 host task 可通过 traceId/workItemId/commandId 串起来

## 3. 入口收敛

- [x] 3.1 将 native Stardew tools 的 move/speak/idle/open-private-chat 入口收敛到同一 host task submit/status/terminal fact 路径
- [x] 3.2 将 MCP Stardew wrappers 验证为 native tool 的薄适配层，不保留独立完成/失败语义
- [x] 3.3 将 private chat 即时行动从“小模型/委托动作”语义迁移为主 agent 可见工具提交 host task，并删除或替换 `npc_delegate_action` 命名入口
- [x] 3.4 将 scheduled ingress/autonomy action submission 接入同一 host task lifecycle
- [x] 3.5 删除或断开 Stardew gameplay 中 `NpcLocalExecutorRunner`、`RunDelegatedIntentAsync` 相关执行路径

## 4. UI / Window Task Lifecycle

- [x] 4.1 设计并实现通用 UI lease snapshot 或复用现有 lease 结构，支持 owner、task id、conversation id、timeout 和 release
- [x] 4.2 为 private chat open/reply/close 生命周期补齐 active menu blocked、safe cleanup、terminal interaction fact 测试
- [x] 4.3 为未来 craft/trade/quest/gather 定义 action schema 和 handler 接口，先实现生命周期骨架和 unsupported/blocked 返回
- [x] 4.4 增加 fake UI/window service harness，覆盖 lease busy、unexpected active menu、timeout、already closed、release only owned UI

## 5. Harness 与测试

- [x] 5.1 新增 host task lifecycle harness，覆盖 move、speak、idle_micro_action、open_private_chat 的 task 创建、bridge submit、status、terminal fact、cleanup
- [x] 5.2 新增 ID correlation 测试，覆盖重复同名工具调用不串线
- [x] 5.3 新增 MCP/native parity 测试，证明两条入口生成同等 host task fact
- [x] 5.4 新增 replay/idempotency 测试，覆盖 in-flight state 重启、staged batch replay、重复 idempotency key
- [x] 5.5 新增无工具调用负向测试，证明自然语言/JSON 文本不会触发 move/speak/todo/window action
- [x] 5.6 新增 prompt/skill 资产扫描测试，证明旧 small-model/local-executor gameplay 指令已退役

## 6. 文档与验证

- [x] 6.1 更新 `AGENTS.md`、`.omx/project-memory.json` 和相关中文规格，保持“废弃能力同步退役、禁止双轨”一致
- [x] 6.2 更新开发者诊断说明，把日志判断从 `local_executor` 改为 host task/action slot/terminal fact
- [x] 6.3 运行 `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew|FullyQualifiedName~Npc|FullyQualifiedName~Mcp"` 并修复失败
- [x] 6.4 运行 `dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug`，确认 bridge task/status/window lifecycle 不回归
- [ ] 6.5 手测一轮 NPC 私聊后移动到海边，检查 `hermes.log`、`SMAPI-latest.txt`、`runtime.jsonl` 中只有 host task lifecycle，没有 local executor lane
  - 未执行：当前本机没有运行中的 Stardew/SMAPI/HermesDesktop 进程；现有 `hermes.log` 与 `SMAPI-latest.txt` 最新写入时间为 2026-05-12，不能作为本轮 2026-05-13 手测证据。
