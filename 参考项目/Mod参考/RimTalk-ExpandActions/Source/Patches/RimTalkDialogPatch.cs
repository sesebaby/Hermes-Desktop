using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimTalkExpandActions.Memory.AI;

namespace RimTalkExpandActions.Patches
{
    /// <summary>
    /// RimTalk 对话补丁
    /// Hook TalkService.GetTalk 方法，解析并触发行为
    /// v3.0 简化版 - 只使用 TalkService.GetTalk Hook
    /// </summary>
    [HarmonyPatch]
    public static class RimTalkDialogPatch
    {
        /// <summary>
        /// 应用 Harmony 补丁
        /// 只使用 TalkService.GetTalk Hook
        /// </summary>
        public static void ApplyPatches(Harmony harmony)
        {
            try
            {
                Log.Message("[RimTalk-ExpandActions] ════════════════════════════════════════");
                Log.Message("[RimTalk-ExpandActions] 开始Hook RimTalk对话系统 (v3.0)");
                Log.Message("[RimTalk-ExpandActions] ════════════════════════════════════════");
                
                // 查找 TalkService 类型
                var talkServiceType = FindTalkServiceType();
                if (talkServiceType == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ✗ 未找到 TalkService 类型");
                    return;
                }
                
                Log.Message($"[RimTalk-ExpandActions] ★ 找到 TalkService: {talkServiceType.FullName}");
                
                // 列出所有方法
                Log.Message("[RimTalk-ExpandActions] TalkService 方法列表:");
                var allMethods = talkServiceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var m in allMethods)
                {
                    if (m.DeclaringType == talkServiceType)
                    {
                        var paramStr = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Log.Message($"[RimTalk-ExpandActions]   {m.Name}({paramStr}) -> {m.ReturnType.Name}");
                    }
                }
                
                // Hook GetTalk 方法
                var getTalkMethod = AccessTools.Method(talkServiceType, "GetTalk");
                if (getTalkMethod == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ✗ 未找到 TalkService.GetTalk 方法");
                    return;
                }
                
                Log.Message($"[RimTalk-ExpandActions] ★ 找到 GetTalk 方法");
                Log.Message($"[RimTalk-ExpandActions]   参数: {string.Join(", ", getTalkMethod.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))}");
                Log.Message($"[RimTalk-ExpandActions]   返回: {getTalkMethod.ReturnType.Name}");
                
                // 应用 Postfix 补丁
                harmony.Patch(
                    getTalkMethod,
                    postfix: new HarmonyMethod(typeof(RimTalkDialogPatch), nameof(GetTalk_Postfix))
                );
                
                Log.Message("[RimTalk-ExpandActions] ★ 成功 Hook TalkService.GetTalk");
                
                // 同时 Hook PlayLogEntry_RimTalkInteraction.ToGameStringFromPOV_Worker 作为备用
                TryHookPlayLogEntry(harmony);
                
                Log.Message("[RimTalk-ExpandActions] ════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] Hook失败: {ex.Message}");
                Log.Error($"[RimTalk-ExpandActions] 堆栈:\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 查找 TalkService 类型
        /// </summary>
        private static Type FindTalkServiceType()
        {
            // 尝试直接查找
            string[] possibleTypeNames = {
                "RimTalk.Service.TalkService",
                "RimTalk.TalkService",
                "RimTalk.Services.TalkService"
            };
            
            foreach (var typeName in possibleTypeNames)
            {
                var type = AccessTools.TypeByName(typeName);
                if (type != null)
                {
                    return type;
                }
            }
            
            // 在所有程序集中搜索
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name == "TalkService" && type.Namespace != null && type.Namespace.Contains("RimTalk"))
                        {
                            return type;
                        }
                    }
                }
                catch { }
            }
            
            return null;
        }
        
        /// <summary>
        /// GetTalk 后置补丁
        /// 在 AI 生成对话后触发
        /// TalkService.GetTalk(Pawn pawn) 返回 string 类型
        /// </summary>
        private static void GetTalk_Postfix(ref string __result, Pawn pawn)
        {
            try
            {
                if (string.IsNullOrEmpty(__result))
                {
                    return;
                }
                
                string originalText = __result;
                
                Log.Message("[RimTalkDialogPatch] ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Log.Message($"[RimTalkDialogPatch] GetTalk 返回: {(originalText.Length > 100 ? originalText.Substring(0, 100) + "..." : originalText)}");
                Log.Message($"[RimTalkDialogPatch] 说话者: {pawn?.Name?.ToStringShort ?? "null"}");
                
                // ★★★ 清洗 LLM 标签 ★★★
                if (LLMTagParser.ContainsTag(originalText))
                {
                    string cleanedText = LLMTagParser.RemoveTags(originalText);
                    Log.Message($"[RimTalkDialogPatch] ★ 检测到标签，清洗中...");
                    Log.Message($"[RimTalkDialogPatch] ★ 清洗前: {originalText}");
                    Log.Message($"[RimTalkDialogPatch] ★ 清洗后: {cleanedText}");
                    
                    // 直接修改返回值
                    __result = cleanedText;
                    Log.Message($"[RimTalkDialogPatch] ★ 已清洗标签并更新返回值");
                }
                
                // 使用混合识别器处理（用于触发行为）
                // 注意：这里 pawn 是说话者，listener 需要从其他地方获取
                ProcessResponse(originalText, pawn, null);
                
                Log.Message("[RimTalkDialogPatch] ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalkDialogPatch] GetTalk_Postfix 异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从返回对象中提取回复文本（带字段信息）
        /// 返回: (文本, 字段名, 是否是属性)
        /// </summary>
        private static Tuple<string, string, bool> ExtractResponseTextWithFieldInfo(object result)
        {
            if (result == null) return new Tuple<string, string, bool>(null, null, false);
            
            // 如果直接是字符串
            if (result is string str)
            {
                return new Tuple<string, string, bool>(str, null, false);
            }
            
            var resultType = result.GetType();
            
            // 尝试各种可能的属性/字段名
            string[] possibleNames = { "Text", "Response", "Content", "Message", "text", "response", "content", "message" };
            
            foreach (var name in possibleNames)
            {
                // 尝试属性
                var prop = AccessTools.Property(resultType, name);
                if (prop != null)
                {
                    var value = prop.GetValue(result, null) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        return new Tuple<string, string, bool>(value, name, true);
                    }
                }
                
                // 尝试字段
                var field = AccessTools.Field(resultType, name);
                if (field != null)
                {
                    var value = field.GetValue(result) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        return new Tuple<string, string, bool>(value, name, false);
                    }
                }
            }
            
            // 最后尝试 ToString
            return new Tuple<string, string, bool>(result.ToString(), null, false);
        }
        
        /// <summary>
        /// 通过名称查找 Pawn
        /// </summary>
        private static Pawn FindPawnByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            
            try
            {
                // 尝试使用 RimTalk.Data.Cache
                var cacheType = AccessTools.TypeByName("RimTalk.Data.Cache");
                if (cacheType != null)
                {
                    var getByNameMethod = AccessTools.Method(cacheType, "GetByName");
                    if (getByNameMethod != null)
                    {
                        var pawnState = getByNameMethod.Invoke(null, new object[] { name });
                        if (pawnState != null)
                        {
                            var pawnProp = AccessTools.Property(pawnState.GetType(), "Pawn");
                            if (pawnProp != null)
                            {
                                return pawnProp.GetValue(pawnState, null) as Pawn;
                            }
                        }
                    }
                }
            }
            catch { }
            
            return null;
        }

        /// <summary>
        /// 尝试 Hook PlayLogEntry_RimTalkInteraction.ToGameStringFromPOV_Worker
        /// 作为备用方案，确保在日志显示时也能清洗标签
        /// </summary>
        private static void TryHookPlayLogEntry(Harmony harmony)
        {
            try
            {
                // 查找 PlayLogEntry_RimTalkInteraction 类型
                Type playLogEntryType = null;
                string[] possibleTypeNames = {
                    "RimTalk.PlayLogEntry_RimTalkInteraction",
                    "RimTalk.Log.PlayLogEntry_RimTalkInteraction",
                    "RimTalk.Interaction.PlayLogEntry_RimTalkInteraction"
                };
                
                foreach (var typeName in possibleTypeNames)
                {
                    playLogEntryType = AccessTools.TypeByName(typeName);
                    if (playLogEntryType != null)
                    {
                        break;
                    }
                }
                
                // 在所有程序集中搜索
                if (playLogEntryType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            foreach (var type in assembly.GetTypes())
                            {
                                if (type.Name == "PlayLogEntry_RimTalkInteraction")
                                {
                                    playLogEntryType = type;
                                    break;
                                }
                            }
                            if (playLogEntryType != null) break;
                        }
                        catch { }
                    }
                }
                
                if (playLogEntryType == null)
                {
                    Log.Message("[RimTalk-ExpandActions] 未找到 PlayLogEntry_RimTalkInteraction 类型（可选）");
                    return;
                }
                
                Log.Message($"[RimTalk-ExpandActions] ★ 找到 PlayLogEntry_RimTalkInteraction: {playLogEntryType.FullName}");
                
                // 尝试 Hook ToGameStringFromPOV_Worker 方法
                var toGameStringMethod = AccessTools.Method(playLogEntryType, "ToGameStringFromPOV_Worker");
                if (toGameStringMethod != null)
                {
                    harmony.Patch(
                        toGameStringMethod,
                        postfix: new HarmonyMethod(typeof(RimTalkDialogPatch), nameof(ToGameStringFromPOV_Worker_Postfix))
                    );
                    Log.Message("[RimTalk-ExpandActions] ★ 成功 Hook ToGameStringFromPOV_Worker");
                }
                else
                {
                    Log.Message("[RimTalk-ExpandActions] 未找到 ToGameStringFromPOV_Worker 方法（可选）");
                }
                
                // 尝试 Hook _cachedString 字段的 getter（如果存在）
                var cachedStringField = AccessTools.Field(playLogEntryType, "_cachedString");
                if (cachedStringField != null)
                {
                    Log.Message($"[RimTalk-ExpandActions] ★ 找到 _cachedString 字段: {cachedStringField.FieldType.Name}");
                }
            }
            catch (Exception ex)
            {
                Log.Message($"[RimTalk-ExpandActions] Hook PlayLogEntry 失败（非致命）: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ToGameStringFromPOV_Worker 后置补丁
        /// 清洗日志显示中的 LLM 标签
        /// 注意：此处不能使用 Log.Message，因为该方法是在绘制日志窗口时调用的，
        /// 在此处写日志会导致 "Collection was modified" 异常并可能导致闪退。
        /// </summary>
        private static void ToGameStringFromPOV_Worker_Postfix(ref string __result, object __instance)
        {
            try
            {
                if (string.IsNullOrEmpty(__result))
                {
                    return;
                }
                
                // 检查是否包含 LLM 标签
                if (LLMTagParser.ContainsTag(__result))
                {
                    // 仅执行清洗，不记录日志以避免死循环/集合修改异常
                    __result = LLMTagParser.RemoveTags(__result);
                }
            }
            catch
            {
                // 忽略异常，确保不影响 UI 绘制
            }
        }
        
        /// <summary>
        /// 处理AI回复文本
        /// v3.0: 支持本地NLU分析，不再依赖AI输出格式化指令
        /// </summary>
        private static void ProcessResponse(string response, Pawn speaker, Pawn listener)
        {
            if (string.IsNullOrEmpty(response))
            {
                return;
            }

            // 记录原始回复
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message("[RimTalkDialogPatch] ━━━━━━━━ 处理AI回复 v3.0 ━━━━━━━━");
                Log.Message($"[RimTalkDialogPatch] 说话者: {speaker?.Name?.ToStringShort ?? "null"}");
                Log.Message($"[RimTalkDialogPatch] 听众: {listener?.Name?.ToStringShort ?? "null"}");
                Log.Message($"[RimTalkDialogPatch] 回复文本: {(response.Length > 100 ? response.Substring(0, 100) + "..." : response)}");
                Log.Message($"[RimTalkDialogPatch] 文本长度: {response.Length} 字符");
            }

            // 如果缺少 speaker 或 listener，仍然尝试处理（可能只是清洗标签）
            if (speaker == null || listener == null)
            {
                Log.Message("[RimTalkDialogPatch] 缺少 speaker 或 listener，跳过行为触发");
                return;
            }

            // 使用混合识别器（v3.0 本地NLU分析优先）
            var hybridResult = HybridIntentRecognizer.RecognizeIntent(
                "",  // userInput - 从TalkResponse中无法直接获取
                response,
                speaker,
                listener
            );

            // 记录识别结果
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message($"[RimTalkDialogPatch] ──────────────────────────────");
                Log.Message($"[RimTalkDialogPatch] 识别结果: {(hybridResult.Success ? "成功" : "失败")}");
                Log.Message($"[RimTalkDialogPatch] 来源: {hybridResult.Source}");
                Log.Message($"[RimTalkDialogPatch] 意图: {hybridResult.IntentName ?? "无"}");
                Log.Message($"[RimTalkDialogPatch] 置信度: {hybridResult.Confidence:F2}");
                Log.Message($"[RimTalkDialogPatch] 说明: {hybridResult.Message}");
            }

            // 如果识别失败或被明确拒绝，直接返回
            if (!hybridResult.Success)
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[RimTalkDialogPatch] → 跳过触发（识别失败或置信度不足）");
                    Log.Message("[RimTalkDialogPatch] ━━━━━━━━━━━━━━━━━━━━━━━━━━");
                }
                return;
            }

            // v3.0: 处理本地NLU分析结果
            if (hybridResult.Source == "LocalNLU")
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[RimTalkDialogPatch] ──────────────────────────────");
                    Log.Message($"[RimTalkDialogPatch] ★ 本地NLU分析触发");
                    Log.Message($"[RimTalkDialogPatch] 意图ID: {hybridResult.IntentId}");
                    Log.Message($"[RimTalkDialogPatch] 置信度: {hybridResult.Confidence:F2}");
                    Log.Message($"[RimTalkDialogPatch] 风险等级: {hybridResult.RiskLevel}");
                    Log.Message($"[RimTalkDialogPatch] 延迟: {hybridResult.SuggestedDelay:F1}秒");
                }
                
                // 决策结果已在HybridIntentRecognizer中处理，行为已加入延迟队列
                if (hybridResult.DecisionResult != null)
                {
                    var decision = hybridResult.DecisionResult.Decision;
                    Log.Message($"[RimTalkDialogPatch] ★ 决策: {IntentDecisionMatrix.GetDecisionDisplayText(decision)}");
                    Log.Message($"[RimTalkDialogPatch] ★ {hybridResult.Message}");
                }
                
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message("[RimTalkDialogPatch] ━━━━━━━━━━━━━━━━━━━━━━━━━━");
                }
            }
            // 如果是LLM标签，使用标签触发器（仍然支持，作为增强）
            else if (hybridResult.Source == "LLMTag" && hybridResult.TagResult != null)
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[RimTalkDialogPatch] ──────────────────────────────");
                    Log.Message($"[RimTalkDialogPatch] 使用LLM标签触发器");
                    Log.Message($"[RimTalkDialogPatch] 标签类型: {hybridResult.TagResult.TagType}");
                    Log.Message($"[RimTalkDialogPatch] 标签值: {hybridResult.TagResult.TagValue}");
                }
                
                var triggerResult = LLMActionTrigger.TriggerAction(
                    hybridResult.TagResult,
                    speaker,
                    listener
                );
                
                if (triggerResult.Success)
                {
                    Log.Message($"[RimTalkDialogPatch] ★ {triggerResult.Message} ★");
                }
                else
                {
                    if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Warning($"[RimTalkDialogPatch] ✗ 触发失败: {triggerResult.Message}");
                    }
                }
                
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message("[RimTalkDialogPatch] ━━━━━━━━━━━━━━━━━━━━━━━━━━");
                }
            }
            else
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[RimTalkDialogPatch] 识别来源: {hybridResult.Source}");
                    Log.Message("[RimTalkDialogPatch] ━━━━━━━━━━━━━━━━━━━━━━━━━━");
                }
            }
        }
    }

    /// <summary>
    /// 手动触发器（用于外部调用或测试）
    /// </summary>
    public static class ManualLLMTrigger
    {
        /// <summary>
        /// 手动处理AI回复并触发行为
        /// </summary>
        public static bool ProcessAIResponse(string aiResponse, Pawn speaker, Pawn listener)
        {
            if (string.IsNullOrEmpty(aiResponse) || speaker == null || listener == null)
            {
                return false;
            }

            var parseResult = LLMTagParser.Parse(aiResponse);
            
            if (!parseResult.Success)
            {
                return false;
            }

            var triggerResult = LLMActionTrigger.TriggerAction(parseResult, speaker, listener);
            
            return triggerResult.Success;
        }
    }
}
