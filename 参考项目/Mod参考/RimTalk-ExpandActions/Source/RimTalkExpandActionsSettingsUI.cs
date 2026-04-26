using System;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalkExpandActions.Memory.Utils;

namespace RimTalkExpandActions
{
    /// <summary>
    /// RimTalk-ExpandActions 设置界面 UI（优化版 - 无 Text.Font 依赖）
    /// </summary>
    public static class RimTalkExpandActionsSettingsUI
    {
        private static Vector2 scrollPosition = Vector2.zero;
        private static string customContentBuffer = "";
        private static bool initialized = false;

        private const float ROW_HEIGHT = 30f;
        private const float LABEL_WIDTH = 280f;
        private const float GAP = 10f;
        private const float BUTTON_WIDTH = 180f;
        private const float BUTTON_HEIGHT = 35f;
        private const float SLIDER_WIDTH = 280f;
        private const float SECTION_GAP = 20f;

        public static void DoSettingsWindowContents(Rect inRect, RimTalkExpandActionsSettings settings)
        {
            if (!initialized)
            {
                customContentBuffer = settings.customRecruitRuleContent ?? "";
                initialized = true;
            }

            float contentHeight = CalculateContentHeight();
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            
            float curY = 0f;

            // 标题区域
            curY = DrawTitle(viewRect.width, curY);
            
            // 规则状态和注入
            curY = DrawRuleStatus(viewRect.width, curY, settings);
            
            // 全局设置
            curY = DrawGlobalSettings(viewRect.width, curY, settings);
            
            // 行为开关
            curY = DrawBehaviorToggles(viewRect.width, curY, settings);
            
            // 成功难度系数
            curY = DrawSuccessChanceSliders(viewRect.width, curY, settings);
            
            // 高级设置
            curY = DrawAdvancedSettings(viewRect.width, curY, settings);
            
            // 自定义规则
            curY = DrawCustomRuleContent(viewRect.width, curY, settings);
            
            // 操作按钮
            curY = DrawActionButtons(viewRect.width, curY, settings);

            Widgets.EndScrollView();
        }

        #region 绘制区域

        private static float DrawTitle(float width, float curY)
        {
            // 大标题
            Rect titleRect = new Rect(0f, curY, width, 40f);
            
            Widgets.Label(titleRect, "RimTalk-ExpandActions 设置");
            
            curY += 45f;

            // 说明文字
            Rect descRect = new Rect(0f, curY, width, 50f);
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(descRect,
                "本 Mod 提供 6 种对话触发的行为系统。您可以单独启用/禁用每种行为，\n" +
                "并调整成功难度系数（0% = 必定失败，100% = 100%成功）。");
            GUI.color = Color.white;
            curY += 55f;

            DrawSeparator(width, ref curY);
            return curY;
        }

        private static float DrawRuleStatus(float width, float curY, RimTalkExpandActionsSettings settings)
        {
            DrawSectionHeader("规则状态", width, ref curY);

            // 检查依赖 Mod
            bool rimTalkExists = CheckIfRimTalkExists();
            if (!rimTalkExists)
            {
                Rect warningRect = new Rect(10f, curY, width - 20f, 45f);
                DrawBox(warningRect, new Color(0.8f, 0.6f, 0f, 0.2f));
                GUI.color = new Color(1f, 0.8f, 0f);
                Widgets.Label(warningRect.ContractedBy(8f),
                    "警告：未检测到 RimTalk-ExpandMemory Mod！\n" +
                    "请确保该 Mod 已安装并在加载顺序中位于本 Mod 之前。");
                GUI.color = Color.white;
                curY += 50f;
            }

            // 规则注入状态
            bool ruleExists = CrossModRecruitRuleInjector.CheckIfRecruitRuleExists();
            Rect statusRect = new Rect(10f, curY, width - 20f, ROW_HEIGHT);
            
            string statusText = ruleExists ? "系统规则已注入到常识库" : "系统规则尚未注入";
            Color statusColor = ruleExists ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.9f, 0.7f, 0.2f);
            
            GUI.color = statusColor;
            Widgets.Label(statusRect, statusText);
            GUI.color = Color.white;
            curY += ROW_HEIGHT + GAP;

            // 注入/移除按钮
            Rect button1Rect = new Rect(10f, curY, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect button2Rect = new Rect(BUTTON_WIDTH + 20f, curY, BUTTON_WIDTH, BUTTON_HEIGHT);

            if (!ruleExists)
            {
                if (Widgets.ButtonText(button1Rect, "立即注入所有规则", true, true, true))
                {
                    HandleInjectButton(settings);
                }
            }
            else
            {
                if (Widgets.ButtonText(button1Rect, "重新注入规则", true, true, true))
                {
                    CrossModRecruitRuleInjector.RemoveRules(
                        "sys-rule-recruit", "sys-rule-drop-weapon", "sys-rule-romance",
                        "sys-rule-inspiration", "sys-rule-rest", "sys-rule-gift"
                    );
                    System.Threading.Thread.Sleep(100);
                    HandleInjectButton(settings);
                }

                GUI.color = new Color(1f, 0.6f, 0.6f);
                if (Widgets.ButtonText(button2Rect, "移除所有规则", true, true, true))
                {
                    int removed = CrossModRecruitRuleInjector.RemoveRules(
                        "sys-rule-recruit", "sys-rule-drop-weapon", "sys-rule-romance",
                        "sys-rule-inspiration", "sys-rule-rest", "sys-rule-gift"
                    );
                    Messages.Message(string.Format("已移除 {0} 条规则", removed), MessageTypeDefOf.NeutralEvent);
                }
                GUI.color = Color.white;
            }
            curY += BUTTON_HEIGHT + SECTION_GAP;

            DrawSeparator(width, ref curY);
            return curY;
        }

        private static float DrawGlobalSettings(float width, float curY, RimTalkExpandActionsSettings settings)
        {
            DrawSectionHeader("全局设置", width, ref curY);

            // 自动注入开关
            Rect autoInjectRect = new Rect(10f, curY, width - 20f, ROW_HEIGHT);
            bool autoInject = settings.autoInjectRules;
            Widgets.CheckboxLabeled(autoInjectRect, "游戏启动时自动注入规则", ref autoInject);
            settings.autoInjectRules = autoInject;
            curY += ROW_HEIGHT + GAP;

            // 规则重要性滑块
            Rect labelRect = new Rect(10f, curY, LABEL_WIDTH, ROW_HEIGHT);
            Rect sliderRect = new Rect(LABEL_WIDTH + 20f, curY, SLIDER_WIDTH, ROW_HEIGHT);
            Rect valueRect = new Rect(LABEL_WIDTH + SLIDER_WIDTH + 30f, curY, 60f, ROW_HEIGHT);

            Widgets.Label(labelRect, "规则重要性（AI 检索优先级）");
            settings.ruleImportance = Widgets.HorizontalSlider(
                sliderRect,
                settings.ruleImportance,
                0.0f,
                1.0f,
                true,
                null,
                "0.0",
                "1.0"
            );
            GUI.color = new Color(0.7f, 1f, 0.7f);
            Widgets.Label(valueRect, string.Format("{0:F2}", settings.ruleImportance));
            GUI.color = Color.white;
            curY += ROW_HEIGHT + SECTION_GAP;

            DrawSeparator(width, ref curY);
            return curY;
        }

        private static float DrawBehaviorToggles(float width, float curY, RimTalkExpandActionsSettings settings)
        {
            DrawSectionHeader("行为开关", width, ref curY);

            var behaviors = new[]
            {
                new { Label = "启用招募功能", Field = "recruit" },
                new { Label = "启用投降/丢武器功能", Field = "drop_weapon" },
                new { Label = "启用恋爱关系功能", Field = "romance" },
                new { Label = "启用灵感触发功能", Field = "inspiration" },
                new { Label = "启用休息/昏迷功能", Field = "rest" },
                new { Label = "启用物品赠送功能", Field = "gift" }
            };

            float toggleWidth = (width - 40f) / 2f;
            int row = 0;
            int col = 0;

            foreach (var behavior in behaviors)
            {
                float x = 10f + col * (toggleWidth + 10f);
                float y = curY + row * (ROW_HEIGHT + 5f);
                Rect toggleRect = new Rect(x, y, toggleWidth, ROW_HEIGHT);

                bool value = GetBehaviorToggleValue(settings, behavior.Field);
                bool newValue = value;
                Widgets.CheckboxLabeled(toggleRect, behavior.Label, ref newValue);

                if (newValue != value)
                {
                    UpdateBehaviorToggle(settings, behavior.Field, newValue);
                }

                col++;
                if (col >= 2)
                {
                    col = 0;
                    row++;
                }
            }

            curY += (row + 1) * (ROW_HEIGHT + 5f) + SECTION_GAP;
            DrawSeparator(width, ref curY);
            return curY;
        }

        private static float DrawSuccessChanceSliders(float width, float curY, RimTalkExpandActionsSettings settings)
        {
            DrawSectionHeader("成功难度系数（控制行为触发成功率）", width, ref curY);

            DrawChanceSlider("招募成功率", ref settings.recruitSuccessChance, width, ref curY);
            DrawChanceSlider("投降成功率", ref settings.dropWeaponSuccessChance, width, ref curY);
            DrawChanceSlider("恋爱成功率", ref settings.romanceSuccessChance, width, ref curY);
            DrawChanceSlider("灵感触发成功率", ref settings.inspirationSuccessChance, width, ref curY);
            DrawChanceSlider("休息成功率", ref settings.restSuccessChance, width, ref curY);
            DrawChanceSlider("赠送成功率", ref settings.giftSuccessChance, width, ref curY);

            curY += GAP;
            DrawSeparator(width, ref curY);
            return curY;
        }

        private static float DrawAdvancedSettings(float width, float curY, RimTalkExpandActionsSettings settings)
        {
            DrawSectionHeader("高级设置", width, ref curY);

            Rect logRect = new Rect(10f, curY, width - 20f, ROW_HEIGHT);
            bool logging = settings.enableDetailedLogging;
            Widgets.CheckboxLabeled(logRect, "启用详细日志（用于调试和问题排查）", ref logging);
            settings.enableDetailedLogging = logging;
            curY += ROW_HEIGHT + GAP;

            Rect msgRect = new Rect(10f, curY, width - 20f, ROW_HEIGHT);
            bool showMsg = settings.showActionMessages;
            Widgets.CheckboxLabeled(msgRect, "显示行为触发提示消息（游戏内通知）", ref showMsg);
            settings.showActionMessages = showMsg;
            curY += ROW_HEIGHT + SECTION_GAP;

            DrawSeparator(width, ref curY);
            return curY;
        }

        private static float DrawCustomRuleContent(float width, float curY, RimTalkExpandActionsSettings settings)
        {
            DrawSectionHeader("自定义招募规则内容（高级）", width, ref curY);

            Rect hintRect = new Rect(10f, curY, width - 20f, 30f);
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(hintRect, "留空使用默认规则。自定义规则可以调整 AI 招募行为的触发条件。");
            GUI.color = Color.white;
            curY += 35f;

            Rect textAreaRect = new Rect(10f, curY, width - 20f, 100f);
            DrawBox(textAreaRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            customContentBuffer = Widgets.TextArea(textAreaRect.ContractedBy(5f), customContentBuffer);
            settings.customRecruitRuleContent = customContentBuffer;
            curY += 105f + SECTION_GAP;

            DrawSeparator(width, ref curY);
            return curY;
        }

        private static float DrawActionButtons(float width, float curY, RimTalkExpandActionsSettings settings)
        {
            Rect resetButtonRect = new Rect(10f, curY, BUTTON_WIDTH * 0.9f, ROW_HEIGHT);
            GUI.color = new Color(1f, 0.8f, 0.5f);
            if (Widgets.ButtonText(resetButtonRect, "重置为默认值", true, true, true))
            {
                settings.ResetToDefault();
                customContentBuffer = "";
                Messages.Message("设置已重置为默认值", MessageTypeDefOf.NeutralEvent);
            }
            GUI.color = Color.white;
            curY += ROW_HEIGHT + GAP * 2;

            // 帮助信息
            Rect helpRect = new Rect(10f, curY, width - 20f, 85f);
            DrawBox(helpRect, new Color(0.2f, 0.3f, 0.4f, 0.3f));
            GUI.color = new Color(0.8f, 0.9f, 1f);
            Widgets.Label(helpRect.ContractedBy(8f),
                "使用说明\n" +
                "1. 启用您需要的行为功能\n" +
                "2. 调整成功率系数控制触发难度（0% = 必定失败，100% = 必定成功）\n" +
                "3. 点击\"立即注入所有规则\"将规则添加到 RimTalk 常识库\n" +
                "4. AI 在对话时会根据设置自动处理行为指令");
            GUI.color = Color.white;
            curY += 90f;

            return curY;
        }

        #endregion

        #region 辅助方法

        private static void DrawSectionHeader(string title, float width, ref float curY)
        {
            Rect headerRect = new Rect(0f, curY, width, 28f);
            DrawBox(headerRect, new Color(0.2f, 0.4f, 0.6f, 0.3f));
            
            Rect titleRect = new Rect(10f, curY, width - 20f, 28f);
            
            Widgets.Label(titleRect, title);
            
            curY += 33f;
        }

        private static void DrawChanceSlider(string label, ref float value, float width, ref float curY)
        {
            Rect labelRect = new Rect(10f, curY, LABEL_WIDTH - 10f, ROW_HEIGHT);
            Rect sliderRect = new Rect(LABEL_WIDTH + 10f, curY, SLIDER_WIDTH, ROW_HEIGHT);
            Rect valueRect = new Rect(LABEL_WIDTH + SLIDER_WIDTH + 20f, curY, 70f, ROW_HEIGHT);

            Widgets.Label(labelRect, string.Format("{0}:", label));
            
            float newValue = Widgets.HorizontalSlider(sliderRect, value, 0.0f, 1.0f, true, null, "0%", "100%");
            value = newValue;

            Color percentColor = value >= 0.8f ? new Color(0.3f, 1f, 0.3f) :
                                 value >= 0.5f ? new Color(1f, 1f, 0.5f) :
                                 new Color(1f, 0.5f, 0.5f);
            GUI.color = percentColor;
            Widgets.Label(valueRect, string.Format("{0:F0}%", value * 100f));
            GUI.color = Color.white;

            curY += ROW_HEIGHT + 5f;
        }

        private static void DrawBox(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            Widgets.DrawBoxSolid(rect, color);
            GUI.color = oldColor;
        }

        private static void DrawSeparator(float width, ref float curY)
        {
            Rect sepRect = new Rect(10f, curY, width - 20f, 1f);
            GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Widgets.DrawLineHorizontal(sepRect.x, sepRect.y, sepRect.width);
            GUI.color = Color.white;
            curY += GAP;
        }

        private static bool GetBehaviorToggleValue(RimTalkExpandActionsSettings settings, string actionType)
        {
            switch (actionType)
            {
                case "recruit": return settings.enableRecruit;
                case "drop_weapon": return settings.enableDropWeapon;
                case "romance": return settings.enableRomance;
                case "inspiration": return settings.enableInspiration;
                case "rest": return settings.enableRest;
                case "gift": return settings.enableGift;
                default: return true;
            }
        }

        private static void UpdateBehaviorToggle(RimTalkExpandActionsSettings settings, string actionType, bool value)
        {
            switch (actionType)
            {
                case "recruit": settings.enableRecruit = value; break;
                case "drop_weapon": settings.enableDropWeapon = value; break;
                case "romance": settings.enableRomance = value; break;
                case "inspiration": settings.enableInspiration = value; break;
                case "rest": settings.enableRest = value; break;
                case "gift": settings.enableGift = value; break;
            }
        }

        private static float CalculateContentHeight()
        {
            return 1400f;
        }

        private static void HandleInjectButton(RimTalkExpandActionsSettings settings)
        {
            try
            {
                long currentTime = DateTime.UtcNow.Ticks;
                if (currentTime - settings.lastManualInjectTime < TimeSpan.FromSeconds(2).Ticks)
                {
                    Messages.Message("请勿频繁点击注入按钮", MessageTypeDefOf.RejectInput);
                    return;
                }
                settings.lastManualInjectTime = currentTime;

                var allRules = BehaviorRuleContents.GetAllRules();
                int successCount = 0;

                foreach (var ruleKvp in allRules)
                {
                    string ruleId = ruleKvp.Key;
                    RuleDefinition ruleDef = ruleKvp.Value;

                    string content = ruleId == "sys-rule-recruit" && !string.IsNullOrWhiteSpace(settings.customRecruitRuleContent)
                        ? settings.customRecruitRuleContent
                        : ruleDef.Content;

                    if (CrossModRecruitRuleInjector.TryInjectRule(
                        ruleDef.Id, ruleDef.Tag, content, ruleDef.Keywords, ruleDef.Importance))
                    {
                        successCount++;
                    }
                }

                Messages.Message(string.Format("成功注入 {0} 条规则到常识库！", successCount), MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[RimTalk-ExpandActions] HandleInjectButton 失败: {0}\n{1}", ex.Message, ex.StackTrace));
                Messages.Message("注入失败，请查看日志", MessageTypeDefOf.RejectInput);
            }
        }

        private static bool CheckIfRimTalkExists()
        {
            try
            {
                foreach (var mod in LoadedModManager.RunningMods)
                {
                    if (mod.PackageId.ToLower().Contains("rimtalk") &&
                        mod.PackageId.ToLower().Contains("expandmemory"))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
