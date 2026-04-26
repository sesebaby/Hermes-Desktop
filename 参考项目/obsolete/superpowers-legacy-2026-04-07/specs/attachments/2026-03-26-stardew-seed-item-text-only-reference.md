# 星露谷“新增种子物品但只做文本感知”参考附件

> Archive/reference note:
>
> This attachment is a narrow host-content reference, not a deployment-architecture contract.
> If any current framework question arises, follow the active superpowers design and governance docs instead of inferring runtime ownership from this note.

## 1. 目的

本附件只回答一个具体问题：

- 在 `M1` 语境下，如果星露谷要做一个“看起来像新种子物品”的礼物，但玩家感知重点只在 `名称` 和 `描述`，应当借鉴哪条开源路线，避免自己造轮子。

这里的“文本感知”指：

- 玩家主要通过对话、邮件、奖励提示、tooltip 或背包名称/描述感知到该物品的特殊性
- 当前不把“完整种植链路、成长阶段、收获产物、商店售卖、平衡数值”作为首要目标

## 2. 结论

结论分两层：

1. 如果严格遵守主设计文档当前 `M1` 红线，即“星露谷先走现有模板实例化，不先做新物品系统”，则应优先采用：
   - `现有 seed 模板`
   - `item.modData`
   - 必要时的 `Harmony tooltip/name patch`
   - 把 AI 生成的名称和描述放在展示层
2. 如果产品上明确要求“背包里确实出现一个新 seed 条目”，但仍然只想做最小文本感知，则最省事、最成熟、最不该造轮子的路线是：
   - `Content Patcher`
   - `Data/Objects`
   - `i18n/*.json`
   - 不引入 `Json Assets` / `DGA` 作为 `M1` 依赖

简化判断如下：

- `M1` 严格模式：复用现有种子模板，只改实例文案显示
- `M1` 放宽模式：允许极少量 game-local 新物品条目，用 `Content Patcher + Data/Objects + i18n`
- 未来若必须“真的可种、可长、可收、可售卖”：再引入 `Json Assets`

## 3. 为什么不该先上更重框架

当前目标只是“新增一个种子物品，但主要让玩家感受到名字和描述是 AI 生成的”。

如果现在直接引入 `Json Assets` 或 `DGA`，虽然可行，但会一起带入这些成本：

- 新物品注册框架依赖
- 更多资产与数据文件组织约束
- 后续种植链路、商店、作物、收获物的实现预期
- 更高的排错和兼容成本

这不符合主设计文档里 `M1` 的收敛原则：

- 先做薄宿主
- 先做最短主链路
- 先做模板实例化和文本展示
- 不提前拉高平台或内容系统复杂度

因此，`Json Assets` 和 `DGA` 在本问题里更适合作为“后续升级路径参考”，而不是 `M1` 首选实现。

## 4. 推荐实现分层

### 4.1 方案 A：严格贴合当前 M1 红线

目标：

- 不注册真正的新物品类型
- 继续复用现有白名单 seed 模板
- 让玩家看到“充满感情的放风草种子”这类动态名称与描述

实现建议：

- 选择一个白名单种子模板作为底座
- 将 AI 生成的 `name` / `description` 写到实例 `modData`
- 在邮件、奖励提示、tooltip、或 inspect UI 层覆写展示

优点：

- 与当前主设计文档 `6.2.2` 最一致
- 不引入新的内容框架
- 不需要承诺真实可种植链路

代价：

- 背包里不一定是“真正新增的独立 seed 条目”
- 更多是“模板实例 + 展示覆写”

### 4.2 方案 B：最小新增条目，但仍然不引入重框架

目标：

- 背包中出现一个新的 seed 类物品条目
- 重点仍是名称和描述
- 不进入完整作物系统

实现建议：

- 用 `Content Patcher` 的 `EditData`
- 在 `Data/Objects` 中增加一个极少量的新 object entry
- `Type` 设为 `Seeds`
- `DisplayName` / `Description` 通过 `i18n` 提供

最小示意：

```json
{
  "Format": "2.9.0",
  "Changes": [
    {
      "Action": "EditData",
      "Target": "Data/Objects",
      "Entries": {
        "AllGameInAI_EmotionalFoxtailSeed": {
          "Name": "AllGameInAI_EmotionalFoxtailSeed",
          "DisplayName": "{{i18n:emotional_foxtail_seed.name}}",
          "Description": "{{i18n:emotional_foxtail_seed.description}}",
          "Type": "Seeds",
          "Category": -74,
          "Price": 100,
          "Texture": "Mods/AllGameInAI/Objects"
        }
      }
    }
  ]
}
```

`i18n/default.json`：

```json
{
  "emotional_foxtail_seed.name": "充满感情的放风草种子",
  "emotional_foxtail_seed.description": "一小包被认真对待过的种子。握在手里时，像是还能感到一点余温。"
}
```

优点：

- 开发成本最低
- 完全复用现成 `Content Patcher` 能力
- 文本与数据分离，后续多语言成本低
- 不需要自己实现物品注册框架

代价：

- 这已经不是“纯模板实例化”，而是星露谷 game-local 的少量新条目
- 因此若采用该方案，需要把它明确记为 `M1` 的受控例外，而不是平台默认能力

### 4.3 方案 C：后续确认为“真种子”时再升级

仅当以下条件成立时，才建议切到 `Json Assets`：

- 该 seed 必须可购买
- 必须可播种
- 必须有成长阶段
- 必须有收获物
- 必须接入更完整的内容包生态

这时应优先参考 `Json Assets` 官方作者文档，而不是自己做一套注册逻辑。

## 5. 本地已下载参考项目

以下仓库已下载到本地 `D:\Projects\AllGameInAI\参考项目\Mod参考`，且不含 `.git` 元数据，可直接查阅：

- [Stardew-Cornucopia](D:/Projects/AllGameInAI/参考项目/Mod参考/Stardew-Cornucopia)
- [Stardew-PPJA](D:/Projects/AllGameInAI/参考项目/Mod参考/Stardew-PPJA)
- [Stardew-BahasaIndonesia](D:/Projects/AllGameInAI/参考项目/Mod参考/Stardew-BahasaIndonesia)
- [StardewMods-Pathoschild](D:/Projects/AllGameInAI/参考项目/Mod参考/StardewMods-Pathoschild)
- [StardewValleyMods-spacechase0](D:/Projects/AllGameInAI/参考项目/Mod参考/StardewValleyMods-spacechase0)

## 6. 具体借鉴点

### 6.1 `Content Patcher` 的最小新增物品条目写法

优先参考：

- [objects.json](D:/Projects/AllGameInAI/参考项目/Mod参考/Stardew-Cornucopia/[CP]%20Cornucopia%20More%20Crops/data/objects.json)
- [action-editdata.md](D:/Projects/AllGameInAI/参考项目/Mod参考/StardewMods-Pathoschild/ContentPatcher/docs/author-guide/action-editdata.md)
- [translations.md](D:/Projects/AllGameInAI/参考项目/Mod参考/StardewMods-Pathoschild/ContentPatcher/docs/author-guide/translations.md)

这里可直接借鉴的点是：

- 如何对 `Data/Objects` 写 `Entries`
- 如何只维护 `DisplayName` / `Description`
- 如何把文本放进 `i18n`

### 6.2 “翻译式文本感知”写法

优先参考：

- [content.json](D:/Projects/AllGameInAI/参考项目/Mod参考/Stardew-BahasaIndonesia/content.json)
- [Objects_Iya.json](D:/Projects/AllGameInAI/参考项目/Mod参考/Stardew-BahasaIndonesia/Assets/Strings/Objects_Iya.json)

这里可直接借鉴的点是：

- `Strings/Objects` 风格的 `*_Name` / `*_Description`
- 多语言文本资产如何组织

这条路线更适合：

- 只想对少量已有物品做翻译式覆写
- 或者需要把 `Data/Objects` 文本再拆分成更清晰的本地化资源

### 6.3 “以后真要变成完整种子”的升级路径

优先参考：

- [author-guide.md](D:/Projects/AllGameInAI/参考项目/Mod参考/StardewValleyMods-spacechase0/framework/JsonAssets/docs/author-guide.md)
- [objects.json](D:/Projects/AllGameInAI/参考项目/Mod参考/Stardew-PPJA/[PPJA]%20Farmer%20to%20Florist/[DGA]%20Farmer%20to%20Florist/objects.json)
- [default.json](D:/Projects/AllGameInAI/参考项目/Mod参考/Stardew-PPJA/[PPJA]%20Farmer%20to%20Florist/[DGA]%20Farmer%20to%20Florist/i18n/default.json)

这里可直接借鉴的点是：

- `SeedName` / `SeedDescription`
- `object.<id>.name` / `object.<id>.description`
- 物品数据与本地化文本分离的组织方式

## 7. 对主设计文档的建议落点

建议把本附件理解为以下决策补充：

1. `M1` 默认仍坚持“模板实例化优先”
2. 星露谷若只是想让玩家感知到一个“有情绪名称和描述的种子礼物”，优先先做展示层与实例文案覆写
3. 只有在产品上明确要求“背包里出现新 seed 条目”时，才允许把 `Content Patcher + Data/Objects + i18n` 作为星露谷的 game-local 受控例外
4. `Json Assets` / `DGA` 不进入 `M1` 依赖面，保留为后续升级路径

## 8. 最终建议

如果你现在要推进开发而不是继续讨论，建议直接选以下口径：

- 文档口径：`M1` 默认模板实例化，星露谷允许受控例外
- 实现口径：若必须有新条目，就用 `Content Patcher + Data/Objects + i18n`
- 架构口径：不把 `Json Assets` / `DGA` 升级成 `M1` 平台依赖

这样既满足“不要自己造轮子”，也不把 `M1` 的内容系统做重。
