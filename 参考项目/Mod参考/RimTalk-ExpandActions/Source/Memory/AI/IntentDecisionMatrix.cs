using System;
using Verse;
using RimTalkExpandActions.Memory.AI.IntentRules;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 意图决策矩阵
    /// 基于置信度和风险等级决定如何处理识别到的意图
    /// </summary>
    public static class IntentDecisionMatrix
    {
        /// <summary>
        /// 决策结果
        /// </summary>
        public enum DecisionType
        {
            /// <summary>不执行（置信度过低或明确拒绝）</summary>
            DoNotExecute,
            
            /// <summary>直接执行（高置信度 + 低风险）</summary>
            ExecuteDirectly,
            
            /// <summary>需要轻量确认（中等置信度或高风险）</summary>
            RequiresConfirmation,
            
            /// <summary>延迟执行（已确认，进入延迟队列）</summary>
            ExecuteWithDelay
        }
        
        /// <summary>
        /// 决策结果详情
        /// </summary>
        public class DecisionResult
        {
            public DecisionType Decision { get; set; }
            public string Reason { get; set; }
            public float Confidence { get; set; }
            public RiskLevel RiskLevel { get; set; }
            public float SuggestedDelay { get; set; }
            public string IntentId { get; set; }
        }
        
        // 置信度阈值配置
        private const float HIGH_CONFIDENCE_THRESHOLD = 0.85f;
        private const float MEDIUM_CONFIDENCE_THRESHOLD = 0.60f;
        private const float LOW_CONFIDENCE_THRESHOLD = 0.30f;
        
        /// <summary>
        /// 根据分析结果做出决策
        /// </summary>
        /// <param name="analysisResult">本地NLU分析结果</param>
        /// <returns>决策结果</returns>
        public static DecisionResult MakeDecision(LocalNLUAnalyzer.AnalysisResult analysisResult)
        {
            if (analysisResult == null || !analysisResult.Success)
            {
                return new DecisionResult
                {
                    Decision = DecisionType.DoNotExecute,
                    Reason = "分析结果无效或未识别到意图",
                    Confidence = 0f
                };
            }
            
            float confidence = analysisResult.Confidence;
            RiskLevel risk = analysisResult.RiskLevel;
            
            var result = new DecisionResult
            {
                Confidence = confidence,
                RiskLevel = risk,
                IntentId = analysisResult.IntentId,
                SuggestedDelay = analysisResult.DelaySeconds
            };
            
            // 决策逻辑
            if (confidence < LOW_CONFIDENCE_THRESHOLD)
            {
                // 置信度过低，不执行
                result.Decision = DecisionType.DoNotExecute;
                result.Reason = $"置信度过低 ({confidence:F2} < {LOW_CONFIDENCE_THRESHOLD})";
            }
            else if (confidence < MEDIUM_CONFIDENCE_THRESHOLD)
            {
                // 低置信度，不执行（避免误触）
                result.Decision = DecisionType.DoNotExecute;
                result.Reason = $"置信度不足 ({confidence:F2} < {MEDIUM_CONFIDENCE_THRESHOLD})";
            }
            else if (confidence < HIGH_CONFIDENCE_THRESHOLD)
            {
                // 中等置信度，需要确认（如果启用了确认机制）
                if (ShouldRequireConfirmation(risk))
                {
                    result.Decision = DecisionType.RequiresConfirmation;
                    result.Reason = $"中等置信度 ({confidence:F2})，建议确认";
                }
                else
                {
                    // 低风险行为可以直接执行
                    result.Decision = DecisionType.ExecuteWithDelay;
                    result.Reason = $"中等置信度但低风险，延迟执行";
                }
            }
            else
            {
                // 高置信度
                if (risk >= RiskLevel.High)
                {
                    // 高风险行为，即使高置信度也建议确认
                    if (ShouldRequireConfirmation(risk))
                    {
                        result.Decision = DecisionType.RequiresConfirmation;
                        result.Reason = $"高置信度 ({confidence:F2}) 但高风险行为，建议确认";
                    }
                    else
                    {
                        result.Decision = DecisionType.ExecuteWithDelay;
                        result.Reason = $"高置信度高风险，延迟执行";
                    }
                }
                else
                {
                    // 高置信度 + 低/中风险，直接执行（带延迟）
                    result.Decision = DecisionType.ExecuteWithDelay;
                    result.Reason = $"高置信度 ({confidence:F2}) + 低风险，延迟执行";
                }
            }
            
            // 日志输出
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message($"[DecisionMatrix] ────────────────────────────────");
                Log.Message($"[DecisionMatrix] 意图: {analysisResult.DisplayName}");
                Log.Message($"[DecisionMatrix] 置信度: {confidence:F2}");
                Log.Message($"[DecisionMatrix] 风险等级: {risk}");
                Log.Message($"[DecisionMatrix] 决策: {result.Decision}");
                Log.Message($"[DecisionMatrix] 原因: {result.Reason}");
                if (result.Decision == DecisionType.ExecuteWithDelay)
                {
                    Log.Message($"[DecisionMatrix] 延迟: {result.SuggestedDelay:F1} 秒");
                }
                Log.Message($"[DecisionMatrix] ────────────────────────────────");
            }
            
            return result;
        }
        
        /// <summary>
        /// 判断是否需要确认机制
        /// 高风险行为且启用了轻量LLM时，使用确认机制
        /// </summary>
        private static bool ShouldRequireConfirmation(RiskLevel risk)
        {
            var settings = RimTalkExpandActionsMod.Settings;
            
            // 如果未启用轻量LLM确认，直接返回false
            if (settings == null || !settings.enableLightweightLLM)
            {
                return false;
            }
            
            // 对高风险和关键风险行为启用确认机制
            return risk >= RiskLevel.High;
        }
        
        /// <summary>
        /// 获取决策类型的显示文本
        /// </summary>
        public static string GetDecisionDisplayText(DecisionType decision)
        {
            switch (decision)
            {
                case DecisionType.DoNotExecute:
                    return "不执行";
                case DecisionType.ExecuteDirectly:
                    return "直接执行";
                case DecisionType.RequiresConfirmation:
                    return "需要确认";
                case DecisionType.ExecuteWithDelay:
                    return "延迟执行";
                default:
                    return "未知";
            }
        }
    }
}