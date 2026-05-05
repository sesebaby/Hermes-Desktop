# 手机 UI 来源说明

本目录用于 Hermes 手机 overlay 的素材和实现来源记录。

用户已说明取得 `Stardew-GitHub-aedenthorn-MobilePhone` 作者授权，可以在 Hermes 中使用其代码和素材。

当前实现参考了：

- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/ModEntry.cs` 的手机状态字段组织方式。
- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/PhoneVisuals.cs` 的右侧手机、未读震动提示思路。
- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/PhoneInput.cs` 的鼠标命中后只 suppress 当前点击的思路。
- `参考项目/Mod参考/Stardew-GitHub-aedenthorn-MobilePhone/MobilePhone/PhoneUtils.cs` 的手机布局、开关和通知提示思路。

没有复用的部分：

- `MobilePhoneCall` 的电话流程。
- `createQuestionDialogue`。
- `DialogueBox` / `Game1.drawDialogue` / `Game1.activeClickableMenu` 通话链路。

Hermes 的硬规则是：手机打开只是 overlay，不设置 `Game1.activeClickableMenu`；只有玩家点进回复输入框时才临时接管键盘。
