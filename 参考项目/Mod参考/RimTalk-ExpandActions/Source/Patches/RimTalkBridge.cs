using System;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimTalkExpandActions.Memory;
using RimTalkExpandActions.Memory.AI;

namespace RimTalkExpandActions.Patches
{
    /// <summary>
    /// RimTalk �Ի��ŽӲ���
    /// ʹ�÷��䶯̬ Patch RimTalk �ĶԻ�����������Ӳ����
    /// </summary>
    [HarmonyPatch]
    public static class RimTalkBridge
    {
        private static Type rimTalkServiceType = null;
        private static MethodInfo targetMethod = null;
        private static bool isRimTalkAvailable = false;

        /// <summary>
        /// ׼���׶Σ���� RimTalk �Ƿ����
        /// </summary>
        static bool Prepare()
        {
            try
            {
                // ���� RimTalk.Service.TalkService ����
                rimTalkServiceType = AccessTools.TypeByName("RimTalk.Service.TalkService");
                
                if (rimTalkServiceType == null)
                {
                    Log.Message("[RimTalk-ExpandActions] RimTalk Mod δ��װ�������Ի��ŽӲ���");
                    return false;
                }

                // ���� GetTalk ���� (public static string GetTalk(Pawn pawn))
                targetMethod = AccessTools.Method(rimTalkServiceType, "GetTalk", new Type[] { typeof(Pawn) });
                
                if (targetMethod == null)
                {
                    Log.Error("[RimTalk-ExpandActions] δ�ҵ� GetTalk ����");
                    return false;
                }

                isRimTalkAvailable = true;
                Log.Message($"[RimTalk-ExpandActions] �ɹ��ҵ� RimTalk �Ի�����: {targetMethod.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] RimTalkBridge.Prepare ʧ��: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// ָ��Ŀ�귽��
        /// </summary>
        static MethodBase TargetMethod()
        {
            return targetMethod;
        }

        /// <summary>
        /// 后置补丁：处理返回的对话文本
        /// GetTalk 方法签名: public static string GetTalk(Pawn pawn)
        /// </summary>
        static void Postfix(Pawn pawn, ref string __result)
        {
            try
            {
                if (string.IsNullOrEmpty(__result) || pawn == null)
                {
                    return;
                }

                string originalResult = __result;
                bool processed = false;

                Log.Message($"[RimTalkBridge] 开始分析AI回复: {__result.Substring(0, Math.Min(80, __result.Length))}...");
                
                // 【优先级1】检测并处理 XML 标签 - 标签存在时直接触发行为
                if (LLMTagParser.ContainsTag(__result))
                {
                    var parseResult = LLMTagParser.Parse(__result);
                    if (parseResult.Success)
                    {
                        Log.Message($"[RimTalkBridge] ★ 检测到标签: <{parseResult.TagType}>{parseResult.TagValue}</{parseResult.TagType}>");
                        
                        // 直接触发行为
                        var triggerResult = LLMActionTrigger.TriggerAction(parseResult, pawn, null);
                        if (triggerResult.Success)
                        {
                            Log.Message($"[RimTalkBridge] ★★★ 标签触发成功: {triggerResult.Message}");
                        }
                        else
                        {
                            Log.Warning($"[RimTalkBridge] 标签触发失败: {triggerResult.Message}");
                        }
                        
                        // 清洗标签
                        __result = LLMTagParser.RemoveTags(__result);
                        processed = true;
                    }
                }
                
                // 【优先级2】没有标签时，使用本地NLU分析
                if (!processed)
                {
                    var hybridResult = HybridIntentRecognizer.RecognizeIntent(
                        "",  // userInput - 这里无法获取
                        __result,
                        pawn,  // speaker (说话者是AI角色)
                        null   // listener
                    );
                    
                    if (hybridResult.Success)
                    {
                        Log.Message($"[RimTalkBridge] ★ NLU识别成功: {hybridResult.IntentName} (来源: {hybridResult.Source}, 置信度: {hybridResult.Confidence:F2})");
                        Log.Message($"[RimTalkBridge] 处理结果: {hybridResult.Message}");
                        processed = true;
                    }
                    else if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Message($"[RimTalkBridge] NLU未匹配: {hybridResult.Message}");
                    }
                }

                // 清洗 JSON 格式（兼容旧格式）
                if (__result.Contains("{\"action\"") || __result.Contains("{ \"action\""))
                {
                    // 处理 AI 回复 - pawn 是说话者（对话目标）
                    string cleanText = AIResponsePostProcessor.ProcessActionResponse(__result, pawn, null);
                    __result = cleanText;
                    processed = true;
                }

                if (processed && RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[RimTalkBridge] 对话已处理: {pawn.Name.ToStringShort}");
                    Log.Message($"[RimTalkBridge] 原始: {originalResult.Substring(0, Math.Min(100, originalResult.Length))}");
                    Log.Message($"[RimTalkBridge] 清洗: {__result}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalkBridge] Postfix 执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// ��ȡ RimTalk �Ƿ����
        /// </summary>
        public static bool IsRimTalkAvailable => isRimTalkAvailable;
    }
}
