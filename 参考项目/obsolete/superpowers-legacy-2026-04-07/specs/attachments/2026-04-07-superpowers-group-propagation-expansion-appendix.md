# Superpowers 群聊、远程、传播扩展主线附件

## 1. 文档定位

本文细化第二阶段必须正式接入的新主线扩展：

1. 群聊
2. 联系人群 / 固定群聊
3. 远程一对一
4. 远程多人群聊
5. 信息传播
6. 主动接触 / 主动对话

固定回链：

- `docs/superpowers/specs/attachments/2026-04-07-superpowers-core-dialogue-memory-social-appendix.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-reference-code-unit-reconstruction-and-afw-migration-appendix.md`

口径关系固定为：

1. 本文是 `对话、记忆、社交动作主线附件` 的第二阶段扩展展开文。
2. 本文只展开：
   - 群聊
   - 远程
   - 传播
   - 主动接触
3. 本文不单独创造新的 authority owner、truth source 或主线顺序。
4. 如果本文和主文、主线附件冲突，按：
   - 总主文
   - `对话、记忆、社交动作主线附件`
   - 本文
   这个顺序收口。

## 2. 总原则

这些能力虽然比私聊复杂，但仍然必须：

1. 并入同一条正式主线
2. 不得另长一套独立系统
3. 继续由 Cloud 持有正本
4. 继续由 Mod 持有最终宿主执行权

## 3. 重点设计

### 3.1 群聊

固定要求：

1. speaker 选择、顺序生成、群聊正本在 Cloud。
2. `Runtime.Local` 负责顺序、并发、线程归一和 fail-closed。
3. `Runtime.<game> Adapter` 负责：
   - participant 集合冻结
   - thread/session key 映射
   - 群聊执行清单映射
4. `Mod` 负责：
   - 打开群聊宿主
   - 渲染消息
   - 最终写回玩家可见结果

必须保留：

1. 群聊历史镜像
2. 群聊回流个人历史
3. 群聊回流记忆

### 3.2 联系人群 / 固定群聊

固定要求：

1. contactGroup 正本在 Cloud。
2. `Runtime.<game> Adapter` 负责 threadKey 和 groupKey 映射。
3. `Mod` 负责远程群聊 UI 和最终提交。

### 3.3 远程一对一 / 远程多人

固定要求：

1. 远程 channel 仍属于统一主线，不另长第二套提示词主链。
2. threadKey 规则必须由 `Runtime.<game> Adapter` 显式冻结。
3. `Runtime.Local` 负责 availability / stale / fail-closed。
4. `Mod` 负责最终渲染远程线程。

### 3.4 信息传播

固定要求：

1. 传播链正本在 Cloud。
2. 每次传播必须带：
   - 来源编号
   - hop
   - 传播目标范围
3. `Runtime.Local` 负责去重和 fail-closed。
4. `Runtime.<game> Adapter` 负责把传播对象翻成游戏可执行目标。
5. `Mod` 负责让目标宿主真正看见传播结果。

### 3.5 主动接触 / 主动对话

固定要求：

1. 主动触发建议在 Cloud。
2. `Runtime.Local` 负责节流、并发、抢占规则。
3. `Runtime.<game> Adapter` 负责接触事件整理。
4. `Mod` 负责真正打开主动宿主 UI。

## 4. 首次可见宿主与失败暴露

首次可见宿主固定为：

1. 游戏内群聊宿主
2. 游戏内远程线程宿主
3. 游戏内目标传播宿主
4. 游戏内主动对话宿主

补看宿主固定为：

1. `Launcher -> 游戏页`
2. `Launcher -> 支持与帮助`

失败暴露点固定为：

1. 当前能力所在的游戏宿主
2. `Launcher -> 支持与帮助`

## 5. 并发 / 时序 contract

固定规则：

1. 群聊一轮只认一个冻结 participant 集合。
2. 线程切换后旧结果不得覆盖新线程。
3. 传播必须带 hop 和来源编号，不能无编号继续跑。
4. 旧回包只能进证据，不能冒充当前 committed。
5. 群聊、远程、传播都不得偷偷长本地备用成功路径。

## 6. 旧入口退役条件

以下成功路径一旦存在旧实现，必须在接管后退役：

1. 绕过统一主线的群聊成功路径
2. 本地自己拼远程提示词的路径
3. 不经过统一 threadKey / sessionKey 的群聊路径
4. 没有 hop / 来源编号的传播成功路径
