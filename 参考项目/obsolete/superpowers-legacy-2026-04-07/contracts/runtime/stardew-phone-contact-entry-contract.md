# Stardew Phone Contact Entry Contract

状态：

- active design baseline

owner：

- stardew integration owner

用途：

- 用大白话冻结 Stardew 手机联系人入口怎么建、联系人从哪里来、点击联系人后到底打开什么，不再允许实现时自由发挥。

固定回链：

- `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
- `docs/superpowers/specs/attachments/2026-04-07-superpowers-stardew-mod-rebuild-and-implementation-appendix.md`
- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/MobilePhoneApp.cs`
- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/PhoneInput.cs`
- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/CallableNPC.cs`

authoritative boundary：

- `Launcher` 不拥有联系人列表
- `Cloud` 不拥有联系人入口 UI
- `Superpowers.Stardew.Mod` 拥有手机联系人入口与联系人卡片宿主壳
- `RemoteDirectAvailabilityResolver` 拥有“现在能不能联系”的宿主事实判断

固定入口：

1. 手机 app 入口固定仿照 `Mobile Phone.OpenPhoneBook`
2. 点击 app 后先打开联系人列表
3. 点击联系人卡片后，才允许进入私信线程
4. 不允许 `F6`、调试命令、菜单 shell 继续冒充正式联系人入口

contact list source：

1. 原版 NPC 先纳入
2. 玩家宠物、孩子是否纳入，按 title profile 单独声明
3. 扩展 Mod NPC 不默认纳入，必须有 title-local 白名单
4. 联系人列表来源固定为：
   - 当前存档里的宿主可联系角色事实
   - 当前 build / title support matrix
   - 当前宿主可见头像和显示名

contact card minima：

- `contactId`
- `displayName`
- `portraitRef`
- `relationshipLabel`
- `availabilityState`
- `availabilityReasonCode`
- `threadKey`
- `isNewMessageHintVisible`

callable rules：

1. 联系人是否出现，和“现在能不能接通”分开
2. 能出现在联系人列表，不代表当前就能私信
3. `availabilityState` 只能来自宿主事实，不得来自 build 开关

open-contact action：

1. 点击联系人卡片时，先计算并固定：
   - `threadKey = gameId + actorId + targetId + channelType`
2. 若线程已存在，复用同一线程
3. 若线程不存在，创建新线程壳
4. 再进入 `remote_direct_one_to_one` surface

player-visible states：

- `loading_contacts`
- `ready`
- `empty`
- `failure`

固定失败 copy：

1. 联系人读取失败，只能显示联系人页失败
2. 不允许联系人页失败时直接弹“AI 对话失败”

绝对禁止：

1. 不允许 `BuildExposureConfig` 决定联系人是否可联系
2. 不允许按 NPC 名字硬编码联系人列表
3. 不允许点击联系人后临时造一个新 threadKey
4. 不允许把联系人入口和私信线程 owner 混成一个类

update trigger：

- 联系人纳入范围变化
- 联系人卡片字段变化
- thread key 规则变化
- 手机 app 入口变化

