# E-2026-0006-primary-reference-citation-too-vague

- id: E-2026-0006
- title: 主参考实现只写到仓库级，没有落到具体文件，导致实现阶段自行脑补
- status: active
- updated_at: 2026-04-19
- keywords: [primary-reference, citation-granularity, evidence-gap, implementation-guessing, file-level-sources]
- trigger_scope: [proposal, design, tasks, review]

## Symptoms

- 文档已经写了“参考某个 mod / 某个仓库”，但没有写到具体文件。
- `tasks.md` 在车道级写了默认参考，却没有在真正关键的任务卡里写清具体代码地址。
- 实施 AI 知道应该参考哪个仓库，却不知道应该参考哪部分实现，于是自己补方案。
- review 无法判断“这张任务卡到底是复刻已有结构，还是在空白发挥”。
- 发现 `external/hermescraft` 是主参考后，又反过来把它机械铺到所有任务，连与 `hermes-agent` 没有关键差异的地方也硬塞引用。
- 文档没有说明“这条 hermescraft 引用到底是在补哪一个相对 hermes-agent 的差异点”，导致参考主次失真。

## Root Cause

- 把“知道主要参考对象是谁”误当成“已经完成证据引用”。
- 没有区分仓库级参考与任务级实现证据的粒度要求。
- 没有把“哪些差异点会直接影响实现方案”单独落成文件级 `Sources:`。
- 没有区分“产品级主参考对象”和“任务级必须补证据的差异点”。
- 没有先判断 `hermes-agent` 现状是否已经覆盖该任务，再决定是否需要补 `external/hermescraft` 文件级引用。

## Bad Fix Paths

- 继续只写“参考 hermescraft”或“参考官方文档”。
- 只在 proposal 顶部声明主要参考仓库，不把关键任务落到文件级引用。
- 为了补引用而到处机械加路径，连纯治理、纯校验任务也乱塞参考地址。
- 把不存在的能力硬说成“参考仓库已经这样做了”，但不给具体文件证据。
- 因为 `external/hermescraft` 是最重要参考，就把与 `hermes-agent` 本来一致的部分也全部改写成“默认参考 hermescraft”。
- 只补 `external/hermescraft`，不同时写清当前 `hermes-agent` 现状文件，导致实现 AI 分不清“哪里是复刻参考，哪里是宿主新增契约”。

## Corrective Constraints

- 只要某个任务的实现方案明显受主参考实现影响，就必须在该任务卡的 `Sources:` 中写到具体文件路径。
- 文件级引用只补“相对 `hermes-agent` 有实现差异、且会改变实现方案的差异点”，不要机械铺满所有任务。
- 若 `hermes-agent` 现状已经覆盖该任务的关键实现语义，而 `external/hermescraft` 没有新增会改变方案的差异约束，则不应为了“表示重视主参考”而硬塞 `external/hermescraft`。
- 同一任务卡里必须同时写清：
  - 哪些语义来自参考实现的产品结构
  - 哪些能力当前 `hermes-agent` 并没有现成支持，属于宿主新增装配契约
- 当任务确实引用 `external/hermescraft` 时，必须写明它补的是哪一个差异点，例如：
  - 生命周期组织方式
  - 多角色独立 home / prompt 结构
  - 社交路由边界
  - 偷听碎片而非完整 transcript
- review 必须逐张关键任务卡检查：如果拿掉这些具体文件地址，实施 AI 是否会重新开始猜。
- review 也必须反向检查：如果删掉某张卡里的 `external/hermescraft` 引用后，实现方案其实完全不变，那这条引用就是机械补充，应移除或降级。

## Verification Evidence

- `openspec/changes/p01-di-yi-jie-duan-ke-shi-shi-ban/tasks.md` 中，与运行时装配、身份隔离、社交路由直接相关的关键任务卡都补上了 `external/hermescraft` 的具体文件级 `Sources:`。
- 这些任务卡同时保留了 `hermes-agent` 现状文件引用，用来说明“哪里是复刻参考，哪里是新增装配”。
- review 结论明确说明：实现阶段不再需要靠猜测决定私聊、小群聊、偷听、多 NPC 并发、逐 NPC home/prompt 装配这些关键方案。
- `openspec/changes/p01-di-yi-jie-duan-ke-shi-shi-ban/proposal.md`、`design.md`、`tasks.md` 进一步写明：只有相对 `hermes-agent` 有实现差异、且差异会改变方案时，才必须补 `external/hermescraft` 文件级引用；其余任务应以 `hermes-agent` 现状或本地 spec 为主证据。

## Related Files

- openspec/changes/p01-di-yi-jie-duan-ke-shi-shi-ban/tasks.md
- openspec/changes/hermescraft-stardew-replica-runtime/proposal.md
- external/hermescraft/README.md
- external/hermescraft/bot/server.js
- external/hermescraft/bot/lib/chat.js
- external/hermescraft/civilization.sh
- external/hermescraft/landfolk.sh

## Notes

- 这张卡处理的是“引用粒度不够，导致实现猜测”。
- 这张卡也处理“发现主参考后又机械满铺引用，导致主次失真”。
- 它和 `E-2026-0002` 的区别是：
  - `E-2026-0002` 解决“基线角色混写”
  - 本卡解决“主参考虽然写对了，但没有落到任务级文件证据”
