using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimTalkExpandActions.Memory.Utils;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 智能预注入器
    /// 在 AI 生成回复之前，检测对话上下文并向目标注入相关常识
    /// 
    /// 工作流程：
    /// 1. 当检测到 A 对 B 说了包含意图的话（如招募邀请）
    /// 2. 立即向 B 注入相关的行为规则常识
    /// 3. B 在生成回复时就能看到常识指导
    /// </summary>
    public static class SmartPreInjector
    {
        /// <summary>
        /// 最近注入记录（防止重复注入）
        /// Key: PawnID + IntentType, Value: 注入时间
        /// </summary>
        private static Dictionary<string, DateTime> recentInjections = new Dictionary<string, DateTime>();
        
        /// <summary>
        /// 注入冷却时间（秒）
        /// </summary>
        private const int INJECTION_COOLDOWN_SECONDS = 60;

        /// <summary>
        /// 意图关键词映射
        /// </summary>
        private static readonly Dictionary<string, string[]> IntentKeywords = new Dictionary<string, string[]>
        {
            // 招募意图
            {
                "expand-action-recruit",
                new[] { 
                    "加入", "招募", "投降", "归顺", "效忠", "投靠", "入伙",
                    "跟随", "追随", "成为我们", "一起", "我们的一员",
                    "join", "follow", "surrender", "recruit"
                }
            },
            // 浪漫意图
            {
                "expand-action-romance",
                new[] { 
                    "喜欢", "爱", "在一起", "恋人", "交往", "约会", "感情",
                    "love", "like", "together", "romance", "date"
                }
            },
            // 共餐意图
            {
                "expand-action-social-dining",
                new[] { 
                    "一起吃", "吃饭", "聚餐", "共餐", "请你吃", "吃点东西",
                    "eat together", "dine", "meal", "dinner", "lunch"
                }
            },
            // 休闲意图
            {
                "expand-action-social-relax",
                new[] { 
                    "一起玩", "休息", "放松", "娱乐", "休闲", "聊天",
                    "play", "relax", "fun", "chat", "hang out"
                }
            },
            // 赠送物品意图
            {
                "expand-action-gift",
                new[] { 
                    "送给你", "礼物", "给你", "这个给你", "收下",
                    "gift", "give", "present", "for you"
                }
            },
            // 休息意图
            {
                "expand-action-rest",
                new[] { 
                    "休息", "睡觉", "累了", "去睡", "歇歇",
                    "rest", "sleep", "tired"
                }
            },
            // 灵感意图
            {
                "expand-action-inspiration",
                new[] { 
                    "振奋", "激励", "鼓舞", "士气", "加油",
                    "inspire", "motivate", "encourage"
                }
            }
        };

        /// <summary>
        /// 检测输入文本中的意图
        /// </summary>
        /// <param name="text">对话文本</param>
        /// <returns>检测到的意图列表（规则ID）</returns>
        public static List<string> DetectIntents(string text)
        {
            var detectedIntents = new List<string>();
            
            if (string.IsNullOrWhiteSpace(text))
            {
                return detectedIntents;
            }

            string lowerText = text.ToLower();

            foreach (var kvp in IntentKeywords)
            {
                string intentId = kvp.Key;
                string[] keywords = kvp.Value;

                foreach (string keyword in keywords)
                {
                    if (lowerText.Contains(keyword.ToLower()))
                    {
                        if (!detectedIntents.Contains(intentId))
                        {
                            detectedIntents.Add(intentId);
                            Log.Message($"[SmartPreInjector] 检测到意图: {intentId} (关键词: {keyword})");
                        }
                        break;
                    }
                }
            }

            return detectedIntents;
        }

        /// <summary>
        /// 预注入常识到目标小人
        /// 在 AI 生成回复之前调用
        /// </summary>
        /// <param name="speaker">说话者 A（发出邀请/询问的人）</param>
        /// <param name="target">目标 B（需要回复的人）</param>
        /// <param name="speakerText">A 说的话</param>
        /// <returns>是否成功注入</returns>
        public static bool TryPreInject(Pawn speaker, Pawn target, string speakerText)
        {
            try
            {
                if (speaker == null || target == null || string.IsNullOrEmpty(speakerText))
                {
                    return false;
                }

                Log.Message($"[SmartPreInjector] ════════════════════════════════════════════");
                Log.Message($"[SmartPreInjector] 预注入检查");
                Log.Message($"[SmartPreInjector] 说话者 A: {speaker.Name?.ToStringShort ?? "未知"}");
                Log.Message($"[SmartPreInjector] 目标 B: {target.Name?.ToStringShort ?? "未知"}");
                Log.Message($"[SmartPreInjector] A 的话: {speakerText}");

                // 1. 检测意图
                var detectedIntents = DetectIntents(speakerText);
                
                if (detectedIntents.Count == 0)
                {
                    Log.Message($"[SmartPreInjector] ✗ 未检测到需要预注入的意图");
                    Log.Message($"[SmartPreInjector] ════════════════════════════════════════════");
                    return false;
                }

                Log.Message($"[SmartPreInjector] ✓ 检测到 {detectedIntents.Count} 个意图");

                // 2. 检查冷却
                string targetId = target.ThingID;
                int injectedCount = 0;

                foreach (string intentId in detectedIntents)
                {
                    string cacheKey = $"{targetId}_{intentId}";
                    
                    if (recentInjections.TryGetValue(cacheKey, out DateTime lastInjection))
                    {
                        double secondsSinceLastInjection = (DateTime.Now - lastInjection).TotalSeconds;
                        if (secondsSinceLastInjection < INJECTION_COOLDOWN_SECONDS)
                        {
                            Log.Message($"[SmartPreInjector] 跳过 {intentId}：冷却中（{INJECTION_COOLDOWN_SECONDS - secondsSinceLastInjection:F0}秒后可用）");
                            continue;
                        }
                    }

                    // 3. 执行注入
                    bool success = InjectRuleToTarget(target, intentId);
                    
                    if (success)
                    {
                        recentInjections[cacheKey] = DateTime.Now;
                        injectedCount++;
                        Log.Message($"[SmartPreInjector] ✓ 成功向 {target.Name?.ToStringShort} 注入常识: {intentId}");
                    }
                    else
                    {
                        Log.Warning($"[SmartPreInjector] ✗ 注入失败: {intentId}");
                    }
                }

                Log.Message($"[SmartPreInjector] 完成：注入了 {injectedCount}/{detectedIntents.Count} 条常识");
                Log.Message($"[SmartPreInjector] ════════════════════════════════════════════");
                
                return injectedCount > 0;
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartPreInjector] TryPreInject 失败: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 向目标小人注入特定规则的常识
        /// </summary>
        private static bool InjectRuleToTarget(Pawn target, string ruleId)
        {
            try
            {
                // 获取规则内容
                var allRules = BehaviorRuleContents.GetAllRules();
                
                if (!allRules.TryGetValue(ruleId, out RuleDefinition rule))
                {
                    Log.Warning($"[SmartPreInjector] 未找到规则: {ruleId}");
                    return false;
                }

                // 使用 PersonaKnowledgeAPI 注入到特定小人
                return InjectToPersona(target, rule);
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartPreInjector] InjectRuleToTarget 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 注入常识到特定小人的 Persona
        /// 使用 RimTalk-ExpandMemory 的 API
        /// </summary>
        private static bool InjectToPersona(Pawn target, RuleDefinition rule)
        {
            try
            {
                // 尝试使用 PersonaKnowledgeAPI
                var personaApiType = Type.GetType("RimTalk.Memory.PersonaKnowledgeAPI, RimTalkMemoryPatch");
                
                if (personaApiType != null)
                {
                    // 使用 PersonaKnowledgeAPI.AddKnowledge(Pawn, string, string, float)
                    var addMethod = personaApiType.GetMethod(
                        "AddKnowledge",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null,
                        new Type[] { typeof(Pawn), typeof(string), typeof(string), typeof(float) },
                        null
                    );

                    if (addMethod != null)
                    {
                        string tag = $"RimTalk-ExpandActions,行为指令,{rule.Id}";
                        var result = addMethod.Invoke(null, new object[] { target, tag, rule.Content, rule.Importance });
                        
                        if (result != null)
                        {
                            Log.Message($"[SmartPreInjector] ✓ PersonaKnowledgeAPI 注入成功");
                            return true;
                        }
                    }
                }

                // 备用：尝试使用 CommonKnowledgeAPI（全局）
                var commonApiType = Type.GetType("RimTalk.Memory.CommonKnowledgeAPI, RimTalkMemoryPatch");
                
                if (commonApiType != null)
                {
                    var addMethod = commonApiType.GetMethod(
                        "AddKnowledge",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null,
                        new Type[] { typeof(string), typeof(string), typeof(float) },
                        null
                    );

                    if (addMethod != null)
                    {
                        string tag = $"RimTalk-ExpandActions,行为指令,{rule.Id},{target.Name?.ToStringShort ?? "Unknown"}";
                        var result = addMethod.Invoke(null, new object[] { tag, rule.Content, rule.Importance });
                        
                        if (result != null)
                        {
                            Log.Message($"[SmartPreInjector] ✓ CommonKnowledgeAPI 注入成功（全局）");
                            return true;
                        }
                    }
                }

                Log.Warning($"[SmartPreInjector] 未找到可用的知识注入 API");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartPreInjector] InjectToPersona 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 清理过期的注入记录
        /// </summary>
        public static void CleanupExpiredRecords()
        {
            try
            {
                var expiredKeys = recentInjections
                    .Where(kvp => (DateTime.Now - kvp.Value).TotalSeconds > INJECTION_COOLDOWN_SECONDS * 2)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    recentInjections.Remove(key);
                }

                if (expiredKeys.Count > 0)
                {
                    Log.Message($"[SmartPreInjector] 清理了 {expiredKeys.Count} 条过期记录");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartPreInjector] CleanupExpiredRecords 失败: {ex.Message}");
            }
        }
    }
}
