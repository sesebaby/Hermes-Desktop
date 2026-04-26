# GGBH_OpenAIWorld 统一分析文档实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 按已批准 spec 重构 `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档`，产出一套“总览 + 共用系统 + 玩法功能 + 附录”的统一分析文档体系，并在用户确认后删除旧稿。

**Architecture:** 先固定恢复来源与证据边界，再固定源码锚点和宿主挂点，之后归整旧稿并建立新目录。正文按“总览 -> 附录底座 -> 共用系统 -> 玩法功能”推进，每篇文档都要求源码可追溯、证据分级、可回链 prompt 和更新日志。

**Tech Stack:** Markdown、PowerShell、`rg`、`git`

---

## 范围说明

这份计划只做文档重构，不改业务代码。

这份计划覆盖：

1. 新分析目录结构
2. 证据边界与源码锚点底座
3. 总览与迁移提炼
4. 附录索引
5. 共用系统分析文档
6. 玩法功能分析文档
7. 用户确认后的旧稿删除收尾

这份计划不做：

- `OpenAIWorld` 业务实现改动
- 反编译源码修复
- prompt 内容重写
- 任何“为了计划方便”删减功能覆盖范围

## 目标文件结构

### 新目录与暂存目录

- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/00_总览/`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/archive_legacy_2026-04-03/`

### 总览

- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/00_总览/GGBH_OpenAIWorld_总览与功能矩阵.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/00_总览/GGBH_OpenAIWorld_迁移复刻总提炼.md`

### 共用系统

- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/01_整体架构与主链路.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/02_Prompt体系与角色卡机制.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/03_AI请求层_服务配置与能力探测.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/04_AI路由_会话能力与通道编排.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/05_消息模型_通信通道与消息持久化.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/06_关系_记忆与摘要机制.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/07_行为协议_解析与执行.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/08_世界事件数据模型与落地机制.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/09_宿主挂点_触发面与回写边界.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/10_容错_修复_持久化与版本演进.md`

### 玩法功能

- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/01_私聊功能.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/02_群聊功能.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/03_联系人群与固定群聊.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/04_传音与远程通信.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/05_信息裂变与社会传播.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/06_主动对话与接触触发.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/07_主动世界演化.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/08_交易与给物互动.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/09_道具使用与装备反馈.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/10_身体交互与双修场景.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/11_竞技场相关AI功能.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/12_战败AI对话与战败后处理.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/13_自定义物品生成.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/14_自定义状态_气运生成.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/15_角色卡与NPC创建.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/16_解签大师与特殊场景功能.md`

### 附录

- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/00_恢复来源_证据等级与边界.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/01_已解密Prompt总表.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/02_源码锚点索引.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/03_版本更新功能演进表.md`

## Task 1: 固定恢复来源与证据边界

**Files:**
- Modify: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/00_恢复来源_证据等级与边界.md`
- Test: `rg` 检查真实 DLL、反编译目录、prompt 来源、更新日志来源、证据等级规则

- [x] **Step 1: 先跑缺口检查，记录当前 repo 状态与缺口结论**

Run:

```powershell
Test-Path 'C:\Users\karlo\.config\superpowers\worktrees\AllGameInAI\docs-openaiworld-unified-analysis-20260403\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档\90_附录\00_恢复来源_证据等级与边界.md'
```

Expected:

- 原始计划预期为返回 `False`
- 当前 repo 实际返回 `True`，说明该附录在本任务开始前已存在
- 该差异必须在实施与汇报中明确记录，并按“审计现状后补齐到目标状态”继续推进，而不是假设文件不存在

- [x] **Step 2: 写证据边界附录正文**

在 `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/00_恢复来源_证据等级与边界.md` 写入至少下面这些段落和关键内容：

```markdown
# GGBH_OpenAIWorld 恢复来源、证据等级与边界

## 1. 本轮分析对象
- 真实程序集：`artifacts/GGBH_OpenAIWorld.recovered.dll`
- 外层 Loader：`artifacts/MOD_OpenAIWorld.outer.dll`
- 反编译目录：`decompiled/GGBH_OpenAIWorld_src`
- 已解密 prompt 目录：`decoded_prompts`
- 更新日志来源：`参考项目/Mod参考/AI开放修仙世界_9.1.3/OpenAIWorldData/update/OpenAIWorld@updatelog.json`

## 2. 恢复方法边界
- 外层 DLL 不是最终业务 DLL
- 当前分析建立在运行时截获的真实 DLL 上
- 对混淆命名方法，允许文件级或调用链级分析

## 3. 证据等级规则
- `A`：源码、数据结构、prompt 明文、更新日志直接可证
- `B`：多处直接证据交叉支持
- `C`：当前主要为分析推断

## 4. 本次统一文档允许引用的证据源
- `decompiled/GGBH_OpenAIWorld_src/**`
- `decoded_prompts/**`
- `分析文档/archive_legacy_2026-04-03/**`
- `参考项目/.../OpenAIWorld@updatelog.json`

## 5. 当前明确不作为直接证据的内容
- 单纯大白话总结
- 未回链源码或 prompt 的猜测
- 没有来源标注的二次结论
```

- [x] **Step 3: 跑检查，确认关键字段都已写入**

Run:

```powershell
rg -n "GGBH_OpenAIWorld\.recovered\.dll|MOD_OpenAIWorld\.outer\.dll|decompiled/GGBH_OpenAIWorld_src|decoded_prompts|OpenAIWorld@updatelog\.json|`A`|`B`|`C`" recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/00_恢复来源_证据等级与边界.md
```

Expected:

- 命中真实 DLL、反编译目录、prompt 来源、更新日志来源和 `A/B/C` 规则

- [x] **Step 4: 提交证据边界附录**

```powershell
git add recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/00_恢复来源_证据等级与边界.md
git commit -m "docs(openaiworld): add recovery evidence boundary appendix"
```

## Task 2: 建立源码锚点索引与宿主挂点文档

**Files:**
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/02_源码锚点索引.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/09_宿主挂点_触发面与回写边界.md`
- Test: `rg` 检查 `PatchClass.cs`、`Patch_UINPCInfo.cs`、`Patch_ConfRoleCreateFeature.cs`、`CreateAction`

- [x] **Step 1: 先提取宿主挂点与关键补丁**

Run:

```powershell
rg -n "HarmonyPatch|CreateAction|UIBattleDefeat|OptionCallBack102|RoleCreateFeature|MapEvent|UINPCInfo" recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.ui.ext
```

Expected:

- 命中 `OpenAIWorld/PatchClass.cs`
- 命中 `OpenAIWorld.ui.ext/Patch_UINPCInfo.cs`
- 命中 `OpenAIWorld.mod/Patch_ConfRoleCreateFeature.cs`

- [x] **Step 2: 写源码锚点索引固定列**

在 `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/02_源码锚点索引.md` 写入至少下面这个表头和首批行：

```markdown
# GGBH_OpenAIWorld 源码锚点索引

| 功能域 | 文件路径 | 类/方法 | 作用说明 | 证据等级 |
| --- | --- | --- | --- | --- |
| 宿主挂点 | `OpenAIWorld/PatchClass.cs` | `CreateAction_Prefix/Postfix` | 动作创建前后统一挂点 | A |
| 宿主挂点 | `OpenAIWorld/PatchClass.cs` | `OptionCallBack102_Postfix` | 剧情选项触发面 | A |
| 宿主挂点 | `OpenAIWorld/PatchClass.cs` | `OpenWindow102_Postfix` | 战败 UI 接入面 | A |
| UI扩展 | `OpenAIWorld.ui.ext/Patch_UINPCInfo.cs` | `Prefix/Postfix` | NPC 信息 UI 注入 | A |
| 角色创建 | `OpenAIWorld.mod/Patch_ConfRoleCreateFeature.cs` | `Prefix/Postfix` | 角色创建配置挂点 | A |
```

- [x] **Step 3: 写宿主挂点文档**

在 `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/09_宿主挂点_触发面与回写边界.md` 写入以下结构：

```markdown
# 宿主挂点、触发面与回写边界

## 1. 这是什么
这是 `OpenAIWorld` 把 AI 链路接进宿主游戏的入口集合。

## 2. 它解决什么问题
- 决定 AI 何时触发
- 决定玩家从哪些界面看到 AI 结果
- 决定哪些结果会 authoritative writeback

## 3. 核心挂点
- 世界循环 / 日推进
- 地图事件生命周期
- 剧情选项
- 战败界面
- NPC 信息界面
- 角色创建配置

## 4. 关键源码锚点
- `OpenAIWorld/PatchClass.cs`
- `OpenAIWorld.ui.ext/Patch_UINPCInfo.cs`
- `OpenAIWorld.mod/Patch_ConfRoleCreateFeature.cs`
```

- [x] **Step 4: 跑检查，确认两个文件都有固定列和关键源码路径**

Run:

```powershell
rg -n "PatchClass\.cs|Patch_UINPCInfo\.cs|Patch_ConfRoleCreateFeature\.cs|CreateAction|OptionCallBack102|OpenWindow102" recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/02_源码锚点索引.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/09_宿主挂点_触发面与回写边界.md
```

Expected:

- 两个文件都命中关键挂点

- [x] **Step 5: 提交源码锚点与宿主挂点文档**

```powershell
git add recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/02_源码锚点索引.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/09_宿主挂点_触发面与回写边界.md
git commit -m "docs(openaiworld): add source anchor and host hook docs"
```

## Task 3: 建立新目录并归整旧稿

**Files:**
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/00_总览/`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/archive_legacy_2026-04-03/`
- Modify: 旧分析文档移动到 `archive_legacy_2026-04-03/`
- Test: `Get-ChildItem` 检查目录和旧稿迁移结果

审计说明（2026-04-03，当前 worktree 复核）：

- 目标目录 `00_总览`、`10_共用系统`、`20_玩法功能`、`90_附录`、`archive_legacy_2026-04-03` 已存在
- `分析文档` 根目录当前无根级 `.md` 文件，旧稿已先行归整到 `archive_legacy_2026-04-03`
- 本任务按“核验现状并确认迁移结果”执行，不重复搬运已归整文件

- [x] **Step 1: 先盘点当前根目录 markdown 现状**

Run:

```powershell
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档' -File -Filter '*.md' | Select-Object Name | Sort-Object Name
```

Expected:

- 当前结果为空，说明根级旧稿已不在 `分析文档` 根目录

- [x] **Step 2: 确认新目录结构已就位**

Run:

```powershell
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档' | Select-Object Mode,Name | Sort-Object Name
```

Expected:

- 看到 `00_总览`、`10_共用系统`、`20_玩法功能`、`90_附录`、`archive_legacy_2026-04-03`

- [x] **Step 3: 确认旧 markdown 已归整到 legacy 目录**

Run:

```powershell
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档\archive_legacy_2026-04-03' -File -Filter '*.md' | Select-Object Name | Sort-Object Name
```

Expected:

- 能看到旧稿清单，说明 legacy 目录已承接归整结果

- [x] **Step 4: 跑检查，确认旧稿已归整，新目录齐备**

Run:

```powershell
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档' | Select-Object Mode,Name
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档\archive_legacy_2026-04-03' -File | Select-Object Name | Sort-Object Name
```

Expected:

- 根目录看到 `00_总览`、`10_共用系统`、`20_玩法功能`、`90_附录`、`archive_legacy_2026-04-03`
- legacy 目录看到旧稿文件

- [x] **Step 5: 提交 Task 3 复核与计划回写**

```powershell
git add docs/superpowers/plans/2026-04-03-GGBH_OpenAIWorld-统一分析文档实施计划.md
git commit -m "docs(openaiworld): align task 3 verification wording"
```

## Task 4: 写总览与迁移复刻总提炼

**Files:**
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/00_总览/GGBH_OpenAIWorld_总览与功能矩阵.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/00_总览/GGBH_OpenAIWorld_迁移复刻总提炼.md`
- Test: `rg` 检查矩阵、依赖图、迁移边界、优先复刻顺序

- [x] **Step 1: 先抽取功能矩阵输入**

Run:

```powershell
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\decoded_prompts' | Select-Object Name | Sort-Object Name
Get-Content -LiteralPath 'D:\Projects\AllGameInAI\参考项目\Mod参考\AI开放修仙世界_9.1.3\OpenAIWorldData\update\OpenAIWorld@updatelog.json' | Select-Object -First 160
```

Expected:

- prompt 清单可作为玩法矩阵输入
- 更新日志前半段可作为功能演进输入

- [x] **Step 2: 写总览主文档**

在 `00_总览/GGBH_OpenAIWorld_总览与功能矩阵.md` 至少写入以下内容：

```markdown
# GGBH_OpenAIWorld 总览与功能矩阵

## 1. 一句话总结这个 mod
它不是单纯 AI 聊天，而是“消息、记忆、行为、世界演化、对象生成、宿主回写”闭环系统。

## 2. 全功能矩阵表
| 功能 | 类型 | 依赖共用系统 | 关键 prompt | 关键源码锚点 | 证据等级 |
| --- | --- | --- | --- | --- | --- |
| 私聊 | 玩法 | 消息、记忆、行为 | `对话.md` | `F.cs` / `O.cs` | A |
| 联系人群与固定群聊 | 玩法 | 通道、持久化、群消息镜像 | `传音群聊.md` | `ContactGroup.cs` / `Y.cs` | A |

## 3. 共用系统 -> 玩法依赖图
## 4. 推荐阅读顺序
## 5. 最值得优先复刻的能力
## 6. 文档导航
```

- [x] **Step 3: 写迁移复刻总提炼**

在 `00_总览/GGBH_OpenAIWorld_迁移复刻总提炼.md` 至少写入以下内容：

```markdown
# GGBH_OpenAIWorld 迁移复刻总提炼

## 1. 共享内核
- Prompt 资产体系
- Snapshot / Summary
- 结构化动作协议
- Parse / Repair / Normalize
- Projector / Executor

## 2. 强宿主耦合点
- 宿主挂点
- UnitAction 落地
- UI 注入面
- 地图事件与世界对象写回

## 3. 最小复刻顺序
1. 私聊
2. 消息持久化与长期记忆
3. 群聊 / 联系人群
4. 行为协议与传播
5. 主动世界
```

- [x] **Step 4: 跑检查，确认总览和迁移文档都有硬结构**

Run:

```powershell
rg -n "全功能矩阵表|依赖图|推荐阅读顺序|最值得优先复刻的能力|共享内核|强宿主耦合点|最小复刻顺序" recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/00_总览/*.md
```

Expected:

- 两篇文档都命中对应段名

- [x] **Step 5: 提交总览与迁移文档**

```powershell
git add recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/00_总览
git commit -m "docs(openaiworld): add overview and migration synthesis"
```

## Task 5: 写 Prompt 总表与版本演进表

**Files:**
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/01_已解密Prompt总表.md`
- Create: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/03_版本更新功能演进表.md`
- Test: `rg` 检查固定列

审计说明（2026-04-03，Task 5 质量复核后回写）：

- `90_附录/01_已解密Prompt总表.md` 已按 `90_附录/decoded_prompts_manifest.csv` 扩展为 31 行全量索引，不再停留在“首批行”示例规模
- `90_附录/03_版本更新功能演进表.md` 当前按分支真实状态使用 `是 / 部分 / 否` 口径：专题文档未落盘、但总览或附录已承接的版本项统一记为 `部分`
- 本任务以“固定表头并按当前事实回写附录”完成，不以示例行里的初始状态值作为最终事实

- [x] **Step 1: 先抽取 prompt manifest 和更新日志**

Run:

```powershell
Get-Content -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档\90_附录\decoded_prompts_manifest.csv' | Select-Object -First 20
Get-Content -LiteralPath 'D:\Projects\AllGameInAI\参考项目\Mod参考\AI开放修仙世界_9.1.3\OpenAIWorldData\update\OpenAIWorld@updatelog.json' | Select-Object -First 220
```

Expected:

- 能看到 prompt manifest 列名
- 能看到版本号与功能描述

- [x] **Step 2: 写 Prompt 总表**

在 `90_附录/01_已解密Prompt总表.md` 写入固定表头，并从首批行扩展到 manifest 全量索引：

```markdown
# 已解密 Prompt 总表

| Prompt名 | 解密文件名 | 原始Zip路径 | 功能归属 | 主要用途 | 证据等级 |
| --- | --- | --- | --- | --- | --- |
| 世界定义 | `世界定义.md` | `ModAssets/prompt/...` | 主动世界 | 世界规则定义 | A |
| 世界推演 | `世界推演.md` | `ModAssets/prompt/...` | 主动世界 | 事件推演 | A |
| 记忆压缩 | `记忆压缩.md` | `ModAssets/prompt/...` | 记忆 | 长期记忆摘要 | A |
```

- [x] **Step 3: 写版本更新功能演进表**

在 `90_附录/03_版本更新功能演进表.md` 写入固定表头，并按当前分支事实填写版本状态：

```markdown
# 版本更新功能演进表

| 版本号 | 功能变化 | 功能归属 | 是否已在新体系落位 | 证据等级 |
| --- | --- | --- | --- | --- |
| 9.1.0 | 角色卡自动创建 NPC、动态可选项 | 角色卡 / 私聊 | 部分 | A |
| 8.7.0 | 战败 AI 对话、AI 创建气运和物品 | 战败 / 自定义生成 | 部分 | A |
| 6.13.0 | 传音阵盘固定群聊 | 联系人群 / 远程群聊 | 部分 | A |
```

- [x] **Step 4: 跑固定列检查**

Run:

```powershell
rg -n "Prompt名|解密文件名|原始Zip路径|功能归属|主要用途|版本号|功能变化|是否已在新体系落位|证据等级" recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/01_已解密Prompt总表.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/03_版本更新功能演进表.md
```

Expected:

- 两份附录都命中固定列

- [x] **Step 5: 提交附录底座**

```powershell
git add recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/01_已解密Prompt总表.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录/03_版本更新功能演进表.md
git commit -m "docs(openaiworld): add prompt and version appendices"
```

## Task 6: 写共用系统文档第一批

**Files:**
- Create: `10_共用系统/01_整体架构与主链路.md`
- Create: `10_共用系统/02_Prompt体系与角色卡机制.md`
- Create: `10_共用系统/03_AI请求层_服务配置与能力探测.md`
- Create: `10_共用系统/04_AI路由_会话能力与通道编排.md`
- Create: `10_共用系统/05_消息模型_通信通道与消息持久化.md`
- Test: `rg` 检查 `证据等级 / 证据来源`、关键源码路径、关键 prompt

审计说明（2026-04-03，main 分支复核后回写）：

- `10_共用系统/01` 到 `05` 已在 `main` 分支落盘
- 本任务已完成过一次质量修订，重点收紧了 `02` 的角色卡证据口径、`04/05` 的 `B` 级链路表述，并同步修正了总览导航中的已存在状态
- 当前以 `docs(openaiworld): add core system docs batch 1` + `docs(openaiworld): tighten task 6 evidence wording` 两个提交共同构成 Task 6 的最终通过状态

- [x] **Step 1: 先抽取第一批文档证据**

Run:

```powershell
rg -n "class AIAgents|class AIServer|class AIServerScheme|class LLMSession|class MessageData|class PrivateMessageData|class GroupMessageData|class ContactGroupMessageData|class ContactGroup" recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src
```

Expected:

- 命中 `AIAgents.cs`、`AIServer.cs`、`AIServerScheme.cs`、`LLMSession.cs`
- 命中 `MessageData.cs`、`PrivateMessageData.cs`、`GroupMessageData.cs`、`ContactGroupMessageData.cs`、`ContactGroup.cs`

- [x] **Step 2: 用统一模板写 01 到 05**

每篇文档至少使用下面这个骨架：

```markdown
# [文档标题]

## 1. 这是什么
## 2. 它解决什么问题
## 3. 核心组成
## 4. 证据等级 / 证据来源
## 5. 关键源码锚点
## 6. 关键数据结构
## 7. 关键 prompt / 配置
## 8. 与哪些玩法功能相连
## 9. 可迁移性判断
## 10. 已确认事实
## 11. 分析判断与待确认点
```

其中 `05_消息模型_通信通道与消息持久化.md` 必须显式写到：

```markdown
- `MessageData.message`
- `PrivateMessageData.fromUnit / toUnit / type / point / weather`
- `GroupMessageData`
- `ContactGroupMessageData.fromUnit`
- 原始消息记录
- JSON sidecar
- 群消息镜像回私聊
```

- [x] **Step 3: 跑检查，确认第一批共用系统文档都带模板与关键锚点**

Run:

```powershell
rg -n "证据等级 / 证据来源|关键源码锚点|AIAgents\.cs|AIServer\.cs|LLMSession\.cs|MessageData\.cs|ContactGroup\.cs|JSON sidecar|镜像" recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/0*.md
```

Expected:

- 第一批文档都命中统一模板字段
- 指定源码与消息持久化术语都出现

- [x] **Step 4: 提交第一批共用系统文档**

```powershell
git add recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/01_整体架构与主链路.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/02_Prompt体系与角色卡机制.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/03_AI请求层_服务配置与能力探测.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/04_AI路由_会话能力与通道编排.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/05_消息模型_通信通道与消息持久化.md
git commit -m "docs(openaiworld): add core system docs batch 1"
```

## Task 7: 写共用系统文档第二批

**Files:**
- Create: `10_共用系统/06_关系_记忆与摘要机制.md`
- Create: `10_共用系统/07_行为协议_解析与执行.md`
- Create: `10_共用系统/08_世界事件数据模型与落地机制.md`
- Create: `10_共用系统/10_容错_修复_持久化与版本演进.md`
- Test: `rg` 检查 `ExperienceData`、`WorldEventData`、`MapEventData`、`JsonRepair`、`行为指令.md`

审计说明（2026-04-03，main 分支复核后回写）：

- `10_共用系统/06`、`07`、`08`、`10` 已在 `main` 分支落盘
- 本任务已完成一次质量修订，重点把 `06/07/08` 的 `B` 级链路边界前移，并为 `10` 增加章节 ownership 说明，避免与 `05/06/08` 专项章职责重叠
- 当前以 `docs(openaiworld): add core system docs batch 2` + `docs(openaiworld): tighten task 7 evidence wording` 两个提交共同构成 Task 7 的最终通过状态

- [x] **Step 1: 先抽取第二批文档证据**

Run:

```powershell
rg -n "class ExperienceData|class WorldEventData|class MapEventData|JsonRepair|supportFormatJsonObject|supportThinking|CreateAction" recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src
```

Expected:

- 命中 `ExperienceData.cs`
- 命中 `WorldEventData.cs`
- 命中 `MapEventData.cs`
- 命中 `JsonRepair.cs`

- [x] **Step 2: 用统一模板写 06 到 10**

其中必须显式写入以下术语：

```markdown
06_关系_记忆与摘要机制.md
- `ExperienceData`
- 长期记忆压缩
- 角色摘要
- 原始经历与摘要分层

07_行为协议_解析与执行.md
- `行为指令.md`
- `CreateAction`
- `ConveyMessage`
- 动作分发
- 传播再入

08_世界事件数据模型与落地机制.md
- `MapEventData`
- `WorldEventData`
- 创建 NPC
- 创建物品
- 地图事件与世界事件双写

10_容错_修复_持久化与版本演进.md
- `JsonRepair`
- `supportFormatJsonObject`
- `supportThinking`
- 版本演进驱动功能扩张
```

- [x] **Step 3: 跑检查，确认第二批共用系统文档具备关键术语**

Run:

```powershell
rg -n "ExperienceData|WorldEventData|MapEventData|JsonRepair|ConveyMessage|CreateAction|supportFormatJsonObject|supportThinking" recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/0*.md
```

Expected:

- 第二批文档命中关键类名、方法名和容错术语

- [x] **Step 4: 提交第二批共用系统文档**

```powershell
git add recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/06_关系_记忆与摘要机制.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/07_行为协议_解析与执行.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/08_世界事件数据模型与落地机制.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统/10_容错_修复_持久化与版本演进.md
git commit -m "docs(openaiworld): add core system docs batch 2"
```

## Task 8: 写玩法功能文档第一批

**Files:**
- Create: `20_玩法功能/01_私聊功能.md`
- Create: `20_玩法功能/02_群聊功能.md`
- Create: `20_玩法功能/03_联系人群与固定群聊.md`
- Create: `20_玩法功能/04_传音与远程通信.md`
- Create: `20_玩法功能/05_信息裂变与社会传播.md`
- Create: `20_玩法功能/06_主动对话与接触触发.md`
- Test: `rg` 检查 `功能定义`、`证据等级 / 证据来源`、`动态可选项对话`、`发送传音符`

- [x] **Step 1: 先抽取第一批玩法证据**

Run:

```powershell
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\decoded_prompts' | Select-Object Name | Sort-Object Name
rg -n "ContactGroup|ContactGroupMessageData|ConveyMessage|动态可选项|发送传音符|主动对话" recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/archive_legacy_2026-04-03
```

Expected:

- 命中 `群聊.md`、`传音群聊.md`、`信息裂变.md`、`主动对话.md`
- 命中 `ContactGroup` 和 `ConveyMessage`

- [x] **Step 2: 用统一玩法模板写 01 到 06**

统一模板：

```markdown
# [玩法标题]

## 1. 功能定义
## 2. 玩家可见表现
## 3. 触发方式
## 4. 证据等级 / 证据来源
## 5. 输入上下文来自哪里
## 6. 依赖哪些共用系统
## 7. 关键 prompt
## 8. 关键源码锚点
## 9. 输出如何落地到游戏
## 10. 对记忆 / 存档 / 传播的影响
## 11. 与其他功能的关系
## 12. 迁移到自己项目时的价值与难点
## 13. 已确认事实
## 14. 分析判断与待确认点
```

额外硬要求：

```markdown
01_私聊功能.md
- 必写：动态可选项对话

03_联系人群与固定群聊.md
- 必写：`ContactGroup`
- 必写：固定群与一次性多人线程的区别

04_传音与远程通信.md
- 必写：互留传音符 / 发送传音符

05_信息裂变与社会传播.md
- 必写：`ConveyMessage`

06_主动对话与接触触发.md
- 必写：接触式触发
- 必写：邀请洞府做客
```

- [x] **Step 3: 跑检查，确认第一批玩法文档带上必写子场景**

Run:

```powershell
rg -n "功能定义|证据等级 / 证据来源|动态可选项对话|ContactGroup|发送传音符|ConveyMessage|邀请洞府做客" recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/0*.md
```

Expected:

- 第一批玩法文档都带统一模板
- 指定必写子场景均命中

- [x] **Step 4: 提交第一批玩法文档**

```powershell
git add recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/01_私聊功能.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/02_群聊功能.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/03_联系人群与固定群聊.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/04_传音与远程通信.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/05_信息裂变与社会传播.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/06_主动对话与接触触发.md
git commit -m "docs(openaiworld): add feature docs batch 1"
```

## Task 9: 写玩法功能文档第二批

**Files:**
- Create: `20_玩法功能/07_主动世界演化.md`
- Create: `20_玩法功能/08_交易与给物互动.md`
- Create: `20_玩法功能/09_道具使用与装备反馈.md`
- Create: `20_玩法功能/10_身体交互与双修场景.md`
- Create: `20_玩法功能/11_竞技场相关AI功能.md`
- Create: `20_玩法功能/12_战败AI对话与战败后处理.md`
- Test: `rg` 检查地标、双修、竞技场、战败

- [x] **Step 1: 先抽取第二批玩法证据**

Run:

```powershell
rg -n "世界定义|世界推演|交易|道具使用|身体交互|双修场景|竞技场|战败" recovered_mod/GGBH_OpenAIWorld_20260326/decoded_prompts recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/archive_legacy_2026-04-03 'D:\Projects\AllGameInAI\参考项目\Mod参考\AI开放修仙世界_9.1.3\OpenAIWorldData\update\OpenAIWorld@updatelog.json'
```

Expected:

- 命中主动世界、交易、道具使用、身体交互、双修、竞技场、战败相关证据

- [x] **Step 2: 用统一玩法模板写 07 到 12**

额外硬要求：

```markdown
07_主动世界演化.md
- 必写：地标生成、可摧毁地标、自定义地标

10_身体交互与双修场景.md
- 必写：身体交互
- 必写：双修场景

11_竞技场相关AI功能.md
- 必写：匹配、战前对话、赛前下注、赛后评价

12_战败AI对话与战败后处理.md
- 必写：杀死、抽魂、禁锢、放走
```

- [x] **Step 3: 跑检查，确认第二批玩法文档具备必写场景**

Run:

```powershell
rg -n "地标|可摧毁|自定义地标|双修|竞技场|赛前下注|赛后评价|抽魂|禁锢|放走" recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/0*.md
```

Expected:

- 第二批玩法文档均命中对应强制场景

- [x] **Step 4: 提交第二批玩法文档**

```powershell
git add recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/07_主动世界演化.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/08_交易与给物互动.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/09_道具使用与装备反馈.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/10_身体交互与双修场景.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/11_竞技场相关AI功能.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/12_战败AI对话与战败后处理.md
git commit -m "docs(openaiworld): add feature docs batch 2"
```

## Task 10: 写玩法功能文档第三批并做总体验证

**Files:**
- Create: `20_玩法功能/13_自定义物品生成.md`
- Create: `20_玩法功能/14_自定义状态_气运生成.md`
- Create: `20_玩法功能/15_角色卡与NPC创建.md`
- Create: `20_玩法功能/16_解签大师与特殊场景功能.md`
- Modify: `00_总览/GGBH_OpenAIWorld_总览与功能矩阵.md`
- Test: `rg` 检查所有玩法文件都被总览矩阵覆盖

- [x] **Step 1: 先抽取第三批玩法证据**

Run:

```powershell
rg -n "自定义物品生成|自定义状态生成|角色卡模板|创建NPC|解签大师" recovered_mod/GGBH_OpenAIWorld_20260326/decoded_prompts recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/archive_legacy_2026-04-03 'D:\Projects\AllGameInAI\参考项目\Mod参考\AI开放修仙世界_9.1.3\OpenAIWorldData\update\OpenAIWorld@updatelog.json'
```

Expected:

- 命中自定义物品、自定义状态、角色卡、创建 NPC、解签大师相关证据

- [x] **Step 2: 用统一玩法模板写 13 到 16**

额外硬要求：

```markdown
13_自定义物品生成.md
- 必写：可食用类、可御器类、储物法器

14_自定义状态_气运生成.md
- 必写：玩家自定义
- 必写：AI 创建与移除

15_角色卡与NPC创建.md
- 必写：角色卡模板
- 必写：自动创建 NPC
- 必写：世界演变创建 NPC

16_解签大师与特殊场景功能.md
- 必写：解签大师
- 必写：把无法独立成文但有明确 prompt 的特殊场景列为子节
```

- [x] **Step 3: 回写总览矩阵，确保第三批玩法被纳入**

在 `00_总览/GGBH_OpenAIWorld_总览与功能矩阵.md` 追加或补齐下列矩阵行：

```markdown
| 自定义物品生成 | 玩法 | Prompt体系、行为协议 | `自定义物品生成_*.md` | `A.cs` / 相关生成链 | A |
| 自定义状态/气运生成 | 玩法 | Prompt体系、行为协议 | `自定义状态生成.md` | `A.cs` / 相关状态链 | A |
| 角色卡与NPC创建 | 玩法 | 角色卡、世界事件、宿主挂点 | `角色卡模板.md` | `Patch_ConfRoleCreateFeature.cs` / `O.cs` | A |
| 解签大师与特殊场景 | 玩法 | Prompt体系 | `解签大师.md` | 对应特殊场景链 | A |
```

- [x] **Step 4: 跑全局覆盖检查**

Run:

```powershell
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档\20_玩法功能' -File | Select-Object Name | Sort-Object Name
rg -n "自定义物品生成|自定义状态|角色卡与NPC创建|解签大师" recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/00_总览/GGBH_OpenAIWorld_总览与功能矩阵.md
git diff --check
```

Expected:

- 玩法目录完整
- 总览矩阵包含第三批玩法
- `git diff --check` 无格式错误

- [x] **Step 5: 提交第三批玩法文档与总览回写**

```powershell
git add recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/13_自定义物品生成.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/14_自定义状态_气运生成.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/15_角色卡与NPC创建.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能/16_解签大师与特殊场景功能.md recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/00_总览/GGBH_OpenAIWorld_总览与功能矩阵.md
git commit -m "docs(openaiworld): add feature docs batch 3"
```

## Task 11: 总验收与用户确认后的旧稿删除

**Files:**
- Modify: 全部新文档
- Delete: `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/archive_legacy_2026-04-03/*` 仅在用户明确确认后
- Test: `rg`、`Get-ChildItem`、`git diff --check`

- [x] **Step 1: 跑新体系完整性检查**

Run:

```powershell
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档\00_总览' -File | Select-Object Name
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档\10_共用系统' -File | Select-Object Name
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档\20_玩法功能' -File | Select-Object Name
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档\90_附录' -File | Select-Object Name
```

Expected:

- 四个目录文件齐全

- [x] **Step 2: 跑证据等级与模板完整性检查**

Run:

```powershell
rg -n "证据等级 / 证据来源|已确认事实|分析判断与待确认点" recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/10_共用系统 recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/20_玩法功能
rg -n "功能域|文件路径|类/方法|作用说明|证据等级|Prompt名|解密文件名|版本号|功能变化" recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/90_附录
git diff --check
```

Expected:

- 共用系统和玩法文档都命中模板字段
- 附录都命中固定列
- `git diff --check` 无错误

- [x] **Step 3: 提交总验收通过前的最终文档状态**

```powershell
git add recovered_mod/GGBH_OpenAIWorld_20260326/分析文档
git commit -m "docs(openaiworld): complete unified analysis documentation"
```

- [x] **Step 4: 等用户确认后再删除旧稿**

Run only after explicit user approval:

```powershell
Get-ChildItem -LiteralPath 'D:\Projects\AllGameInAI\recovered_mod\GGBH_OpenAIWorld_20260326\分析文档\archive_legacy_2026-04-03' -File | Remove-Item
```

Expected:

- legacy 目录清空
- 新体系保留

- [x] **Step 5: 提交旧稿删除收尾**

```powershell
git add recovered_mod/GGBH_OpenAIWorld_20260326/分析文档
git commit -m "docs(openaiworld): remove legacy analysis drafts after approval"
```
