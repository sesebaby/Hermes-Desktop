using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using RimTalkExpandActions.Memory.AI;

namespace RimTalkExpandActions
{
    public class RimTalkExpandActionsMod : Mod
    {
        public static RimTalkExpandActionsSettings Settings { get; private set; }
        
        private Vector2 scrollPosition = Vector2.zero;
        
        public RimTalkExpandActionsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimTalkExpandActionsSettings>();
            
            Harmony harmony = new Harmony("sanguo.rimtalk.expandactions");
            harmony.PatchAll();
            
            try
            {
                Patches.RimTalkDialogPatch.ApplyPatches(harmony);
            }
            catch { }

            try
            {
                LocalNLUAnalyzer.Initialize();
            }
            catch { }
        }

        public override string SettingsCategory()
        {
            return "RimTalk-ExpandActions";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            try
            {
                float lineHeight = 24f;
                float gap = 6f;
                float sectionGap = 12f;
                float width = inRect.width - 20f;
                float sliderWidth = width - 200f;
                
                // 计算滚动区域
                float viewHeight = 1600f;
                Rect viewRect = new Rect(0f, 0f, width, viewHeight);
                Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
                
                float y = 0f;
                
                // ============ 通用设置 ============
                Widgets.Label(new Rect(0f, y, width, lineHeight), "== 通用设置 ==");
                y += lineHeight + gap;
                
                Widgets.DrawLineHorizontal(0f, y, width);
                y += gap;
                
                Widgets.CheckboxLabeled(new Rect(0f, y, width, lineHeight), "显示行为消息", ref Settings.showActionMessages);
                y += lineHeight;
                Widgets.CheckboxLabeled(new Rect(0f, y, width, lineHeight), "启用详细日志", ref Settings.enableDetailedLogging);
                y += lineHeight + gap;
                
                if (Widgets.ButtonText(new Rect(0f, y, 150f, 28f), "重置为默认值"))
                {
                    Settings.ResetToDefault();
                }
                y += 28f + sectionGap;
                
                // ============ AI 意图识别设置 ============
                Widgets.Label(new Rect(0f, y, width, lineHeight), "== AI 意图识别设置 ==");
                y += lineHeight + gap;
                Widgets.DrawLineHorizontal(0f, y, width);
                y += gap;
                
                Widgets.Label(new Rect(0f, y, 200f, lineHeight), $"NLU 最低置信度: {Settings.nluMinConfidence:P0}");
                Settings.nluMinConfidence = Widgets.HorizontalSlider(new Rect(200f, y + 4f, sliderWidth, lineHeight), Settings.nluMinConfidence, 0.1f, 1.0f);
                y += lineHeight + gap;
                
                Widgets.Label(new Rect(0f, y, 200f, lineHeight), $"NLU 延迟倍率: {Settings.nluDelayMultiplier:F1}x");
                Settings.nluDelayMultiplier = Widgets.HorizontalSlider(new Rect(200f, y + 4f, sliderWidth, lineHeight), Settings.nluDelayMultiplier, 0.5f, 3.0f);
                y += lineHeight + sectionGap;
                
                // ============ 轻量级 LLM 设置 ============
                Widgets.Label(new Rect(0f, y, width, lineHeight), "== 轻量级 LLM 确认 ==");
                y += lineHeight + gap;
                Widgets.DrawLineHorizontal(0f, y, width);
                y += gap;
                
                Widgets.CheckboxLabeled(new Rect(0f, y, width, lineHeight), "启用轻量级 LLM", ref Settings.enableLightweightLLM);
                y += lineHeight;
                
                if (Settings.enableLightweightLLM)
                {
                    Widgets.Label(new Rect(0f, y, 80f, lineHeight), "API URL:");
                    Settings.lightweightLLMApiUrl = Widgets.TextField(new Rect(90f, y, width - 90f, lineHeight), Settings.lightweightLLMApiUrl ?? "");
                    y += lineHeight + gap;
                    
                    Widgets.Label(new Rect(0f, y, 80f, lineHeight), "API Key:");
                    Settings.lightweightLLMApiKey = Widgets.TextField(new Rect(90f, y, width - 90f, lineHeight), Settings.lightweightLLMApiKey ?? "");
                    y += lineHeight + gap;
                    
                    Widgets.Label(new Rect(0f, y, 80f, lineHeight), "模型名称:");
                    Settings.lightweightLLMModel = Widgets.TextField(new Rect(90f, y, width - 90f, lineHeight), Settings.lightweightLLMModel ?? "");
                    y += lineHeight;
                }
                y += sectionGap;
                
                // ============ 向量服务设置 ============
                Widgets.Label(new Rect(0f, y, width, lineHeight), "== 向量服务设置 ==");
                y += lineHeight + gap;
                Widgets.DrawLineHorizontal(0f, y, width);
                y += gap;
                
                Widgets.Label(new Rect(0f, y, 120f, lineHeight), "SiliconFlow Key:");
                Settings.SiliconFlowApiKey = Widgets.TextField(new Rect(130f, y, width - 130f, lineHeight), Settings.SiliconFlowApiKey ?? "");
                y += lineHeight + gap;
                
                Widgets.Label(new Rect(0f, y, 120f, lineHeight), "嵌入 API URL:");
                Settings.embeddingApiUrl = Widgets.TextField(new Rect(130f, y, width - 130f, lineHeight), Settings.embeddingApiUrl ?? "");
                y += lineHeight + gap;
                
                Widgets.Label(new Rect(0f, y, 120f, lineHeight), "嵌入模型:");
                Settings.embeddingModel = Widgets.TextField(new Rect(130f, y, width - 130f, lineHeight), Settings.embeddingModel ?? "");
                y += lineHeight + sectionGap;
                
                // ============ 扩展行为开关 ============
                Widgets.Label(new Rect(0f, y, width, lineHeight), "== 扩展行为开关 ==");
                y += lineHeight + gap;
                Widgets.DrawLineHorizontal(0f, y, width);
                y += gap;
                
                Widgets.CheckboxLabeled(new Rect(0f, y, width, lineHeight), "启用招募行为", ref Settings.enableRecruit);
                y += lineHeight;
                Widgets.CheckboxLabeled(new Rect(0f, y, width, lineHeight), "启用缴械行为", ref Settings.enableDropWeapon);
                y += lineHeight;
                Widgets.CheckboxLabeled(new Rect(0f, y, width, lineHeight), "启用求爱行为", ref Settings.enableRomance);
                y += lineHeight;
                Widgets.CheckboxLabeled(new Rect(0f, y, width, lineHeight), "启用灵感行为", ref Settings.enableInspiration);
                y += lineHeight;
                Widgets.CheckboxLabeled(new Rect(0f, y, width, lineHeight), "启用休息行为", ref Settings.enableRest);
                y += lineHeight;
                Widgets.CheckboxLabeled(new Rect(0f, y, width, lineHeight), "启用送礼行为", ref Settings.enableGift);
                y += lineHeight;
                Widgets.CheckboxLabeled(new Rect(0f, y, width, lineHeight), "启用社交用餐", ref Settings.enableSocialDining);
                y += lineHeight;
                Widgets.CheckboxLabeled(new Rect(0f, y, width, lineHeight), "启用社交放松", ref Settings.enableSocialRelax);
                y += lineHeight + sectionGap;
                
                // ============ 成功率调整 ============
                Widgets.Label(new Rect(0f, y, width, lineHeight), "== 成功率调整 ==");
                y += lineHeight + gap;
                Widgets.DrawLineHorizontal(0f, y, width);
                y += gap;
                
                Widgets.Label(new Rect(0f, y, 200f, lineHeight), $"招募成功率: {Settings.recruitSuccessChance:P0}");
                Settings.recruitSuccessChance = Widgets.HorizontalSlider(new Rect(200f, y + 4f, sliderWidth, lineHeight), Settings.recruitSuccessChance, 0f, 1f);
                y += lineHeight + gap;
                
                Widgets.Label(new Rect(0f, y, 200f, lineHeight), $"缴械成功率: {Settings.dropWeaponSuccessChance:P0}");
                Settings.dropWeaponSuccessChance = Widgets.HorizontalSlider(new Rect(200f, y + 4f, sliderWidth, lineHeight), Settings.dropWeaponSuccessChance, 0f, 1f);
                y += lineHeight + gap;
                
                Widgets.Label(new Rect(0f, y, 200f, lineHeight), $"求爱成功率: {Settings.romanceSuccessChance:P0}");
                Settings.romanceSuccessChance = Widgets.HorizontalSlider(new Rect(200f, y + 4f, sliderWidth, lineHeight), Settings.romanceSuccessChance, 0f, 1f);
                y += lineHeight + gap;
                
                Widgets.Label(new Rect(0f, y, 200f, lineHeight), $"灵感成功率: {Settings.inspirationSuccessChance:P0}");
                Settings.inspirationSuccessChance = Widgets.HorizontalSlider(new Rect(200f, y + 4f, sliderWidth, lineHeight), Settings.inspirationSuccessChance, 0f, 1f);
                y += lineHeight + gap;
                
                Widgets.Label(new Rect(0f, y, 200f, lineHeight), $"休息成功率: {Settings.restSuccessChance:P0}");
                Settings.restSuccessChance = Widgets.HorizontalSlider(new Rect(200f, y + 4f, sliderWidth, lineHeight), Settings.restSuccessChance, 0f, 1f);
                y += lineHeight + gap;
                
                Widgets.Label(new Rect(0f, y, 200f, lineHeight), $"送礼成功率: {Settings.giftSuccessChance:P0}");
                Settings.giftSuccessChance = Widgets.HorizontalSlider(new Rect(200f, y + 4f, sliderWidth, lineHeight), Settings.giftSuccessChance, 0f, 1f);
                y += lineHeight + gap;
                
                Widgets.Label(new Rect(0f, y, 200f, lineHeight), $"社交用餐成功率: {Settings.socialDiningSuccessChance:P0}");
                Settings.socialDiningSuccessChance = Widgets.HorizontalSlider(new Rect(200f, y + 4f, sliderWidth, lineHeight), Settings.socialDiningSuccessChance, 0f, 1f);
                y += lineHeight + gap;
                
                Widgets.Label(new Rect(0f, y, 200f, lineHeight), $"社交放松成功率: {Settings.socialRelaxSuccessChance:P0}");
                Settings.socialRelaxSuccessChance = Widgets.HorizontalSlider(new Rect(200f, y + 4f, sliderWidth, lineHeight), Settings.socialRelaxSuccessChance, 0f, 1f);
                y += lineHeight + sectionGap;
                
                // ============ 自动触发设置 ============
                Widgets.Label(new Rect(0f, y, width, lineHeight), "== 自动触发设置 ==");
                y += lineHeight + gap;
                Widgets.DrawLineHorizontal(0f, y, width);
                y += gap;
                
                int jobCount = Settings.enabledJobTriggers?.Count ?? 0;
                Widgets.Label(new Rect(0f, y, width, lineHeight), $"已配置的触发 Job 数量: {jobCount}");
                y += lineHeight;
                Widgets.Label(new Rect(0f, y, width, lineHeight), "当小人开始选定的 Job 时，会自动触发对话");
                y += lineHeight + gap;
                
                // 只在游戏内（有 WindowStack）时显示打开窗口按钮
                if (Find.WindowStack != null)
                {
                    if (Widgets.ButtonText(new Rect(0f, y, 200f, 28f), "打开 Job 触发器配置窗口"))
                    {
                        try
                        {
                            Find.WindowStack.Add(new UI.Window_JobTriggerSettings());
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[RimTalk-ExpandActions] 无法打开 Job 配置窗口: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Widgets.Label(new Rect(0f, y, width, lineHeight), "(请在游戏内打开 Job 触发器配置窗口)");
                }
                y += 28f + gap;
                
                // 显示已启用的 Job 列表（前10个）
                if (Settings.enabledJobTriggers != null && Settings.enabledJobTriggers.Count > 0)
                {
                    Widgets.Label(new Rect(0f, y, width, lineHeight), "已启用的 Job:");
                    y += lineHeight;
                    
                    int displayCount = Math.Min(Settings.enabledJobTriggers.Count, 10);
                    for (int i = 0; i < displayCount; i++)
                    {
                        Widgets.Label(new Rect(10f, y, width - 10f, lineHeight), $"• {Settings.enabledJobTriggers[i]}");
                        y += lineHeight;
                    }
                    if (Settings.enabledJobTriggers.Count > 10)
                    {
                        Widgets.Label(new Rect(10f, y, width - 10f, lineHeight), $"... 还有 {Settings.enabledJobTriggers.Count - 10} 个");
                        y += lineHeight;
                    }
                    y += gap;
                    
                    if (Widgets.ButtonText(new Rect(0f, y, 150f, 28f), "清除所有配置"))
                    {
                        Settings.enabledJobTriggers.Clear();
                    }
                }
                
                Widgets.EndScrollView();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] 设置界面错误: {ex}");
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }

    public class RimTalkExpandActionsSettings : ModSettings
    {
        public bool showActionMessages = true;
        public bool enableDetailedLogging = false;

        public float nluMinConfidence = 0.4f;
        public float nluDelayMultiplier = 1.0f;

        public bool enableLightweightLLM = false;
        public string lightweightLLMApiUrl = "";
        public string lightweightLLMApiKey = "";
        public string lightweightLLMModel = "";

        public string SiliconFlowApiKey = "";
        public string embeddingApiUrl = "";
        public string embeddingModel = "";
        
        public string GetEmbeddingApiUrl() => string.IsNullOrWhiteSpace(embeddingApiUrl) ? null : embeddingApiUrl;
        public string GetEmbeddingModel() => string.IsNullOrWhiteSpace(embeddingModel) ? null : embeddingModel;

        public bool enableRecruit = true;
        public bool enableDropWeapon = true;
        public bool enableRomance = true;
        public bool enableInspiration = true;
        public bool enableRest = true;
        public bool enableGift = true;
        public bool enableSocialDining = true;
        public bool enableSocialRelax = true;

        public float recruitSuccessChance = 1.0f;
        public float dropWeaponSuccessChance = 1.0f;
        public float romanceSuccessChance = 1.0f;
        public float inspirationSuccessChance = 1.0f;
        public float restSuccessChance = 1.0f;
        public float giftSuccessChance = 1.0f;
        public float socialDiningSuccessChance = 1.0f;
        public float socialRelaxSuccessChance = 1.0f;

        public List<string> enabledJobTriggers = new List<string>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showActionMessages, "showActionMessages", true);
            Scribe_Values.Look(ref enableDetailedLogging, "enableDetailedLogging", false);
            
            Scribe_Values.Look(ref nluMinConfidence, "nluMinConfidence", 0.4f);
            Scribe_Values.Look(ref nluDelayMultiplier, "nluDelayMultiplier", 1.0f);

            Scribe_Values.Look(ref enableLightweightLLM, "enableLightweightLLM", false);
            Scribe_Values.Look(ref lightweightLLMApiUrl, "lightweightLLMApiUrl", "");
            Scribe_Values.Look(ref lightweightLLMApiKey, "lightweightLLMApiKey", "");
            Scribe_Values.Look(ref lightweightLLMModel, "lightweightLLMModel", "");

            Scribe_Values.Look(ref SiliconFlowApiKey, "SiliconFlowApiKey", "");
            Scribe_Values.Look(ref embeddingApiUrl, "embeddingApiUrl", "");
            Scribe_Values.Look(ref embeddingModel, "embeddingModel", "");

            Scribe_Values.Look(ref enableRecruit, "enableRecruit", true);
            Scribe_Values.Look(ref enableDropWeapon, "enableDropWeapon", true);
            Scribe_Values.Look(ref enableRomance, "enableRomance", true);
            Scribe_Values.Look(ref enableInspiration, "enableInspiration", true);
            Scribe_Values.Look(ref enableRest, "enableRest", true);
            Scribe_Values.Look(ref enableGift, "enableGift", true);
            Scribe_Values.Look(ref enableSocialDining, "enableSocialDining", true);
            Scribe_Values.Look(ref enableSocialRelax, "enableSocialRelax", true);

            Scribe_Values.Look(ref recruitSuccessChance, "recruitSuccessChance", 1.0f);
            Scribe_Values.Look(ref dropWeaponSuccessChance, "dropWeaponSuccessChance", 1.0f);
            Scribe_Values.Look(ref romanceSuccessChance, "romanceSuccessChance", 1.0f);
            Scribe_Values.Look(ref inspirationSuccessChance, "inspirationSuccessChance", 1.0f);
            Scribe_Values.Look(ref restSuccessChance, "restSuccessChance", 1.0f);
            Scribe_Values.Look(ref giftSuccessChance, "giftSuccessChance", 1.0f);
            Scribe_Values.Look(ref socialDiningSuccessChance, "socialDiningSuccessChance", 1.0f);
            Scribe_Values.Look(ref socialRelaxSuccessChance, "socialRelaxSuccessChance", 1.0f);
            
            Scribe_Collections.Look(ref enabledJobTriggers, "enabledJobTriggers", LookMode.Value);
            if (enabledJobTriggers == null) enabledJobTriggers = new List<string>();
        }

        public void ResetToDefault()
        {
            showActionMessages = true;
            enableDetailedLogging = false;
            nluMinConfidence = 0.4f;
            nluDelayMultiplier = 1.0f;
            enableLightweightLLM = false;
            lightweightLLMApiUrl = "";
            lightweightLLMApiKey = "";
            lightweightLLMModel = "";
            SiliconFlowApiKey = "";
            embeddingApiUrl = "";
            embeddingModel = "";
            enableRecruit = true;
            enableDropWeapon = true;
            enableRomance = true;
            enableInspiration = true;
            enableRest = true;
            enableGift = true;
            enableSocialDining = true;
            enableSocialRelax = true;
            recruitSuccessChance = 1.0f;
            dropWeaponSuccessChance = 1.0f;
            romanceSuccessChance = 1.0f;
            inspirationSuccessChance = 1.0f;
            restSuccessChance = 1.0f;
            giftSuccessChance = 1.0f;
            socialDiningSuccessChance = 1.0f;
            socialRelaxSuccessChance = 1.0f;
            enabledJobTriggers.Clear();
        }

        public bool IsActionEnabled(string actionType)
        {
            switch (actionType?.ToLower())
            {
                case "recruit": return enableRecruit;
                case "drop_weapon": return enableDropWeapon;
                case "romance": return enableRomance;
                case "give_inspiration": case "inspiration": return enableInspiration;
                case "force_rest": case "rest": return enableRest;
                case "give_item": case "gift": return enableGift;
                case "social_dining": case "dining": return enableSocialDining;
                case "social_relax": case "relax": return enableSocialRelax;
                default: return true;
            }
        }

        public float GetSuccessChance(string actionType)
        {
            switch (actionType?.ToLower())
            {
                case "recruit": return recruitSuccessChance;
                case "drop_weapon": return dropWeaponSuccessChance;
                case "romance": return romanceSuccessChance;
                case "give_inspiration": case "inspiration": return inspirationSuccessChance;
                case "force_rest": case "rest": return restSuccessChance;
                case "give_item": case "gift": return giftSuccessChance;
                case "social_dining": case "dining": return socialDiningSuccessChance;
                case "social_relax": case "relax": return socialRelaxSuccessChance;
                default: return 1.0f;
            }
        }
    }
}
