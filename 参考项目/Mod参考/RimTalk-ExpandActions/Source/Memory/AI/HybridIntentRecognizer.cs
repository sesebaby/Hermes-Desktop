using System;
using Verse;
using RimTalkExpandActions.Memory.AI.IntentRules;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 混合行为识别器 v3.0
    /// 
    /// 新策略：
    /// 1. 本地NLU分析优先 - 分析AI回复的自然语言，不再依赖AI输出格式化指令
    /// 2. LLM标签作为增强 - 如果AI恰好输出了标签，使用更高置信度
    /// 3. 决策矩阵判断 - 基于置信度和风险等级决定是否执行
    /// 4. 延迟队列执行 - 添加自然延迟，支持取消
    /// </summary>
    public static class HybridIntentRecognizer
    {
        /// <summary>
        /// 识别结果
        /// </summary>
        public class RecognitionResult
        {
            public bool Success { get; set; }
            public string IntentId { get; set; }
            public string IntentName { get; set; }
            public string Source { get; set; }  // "LocalNLU", "LLMTag", "None"
            public float Confidence { get; set; }
            public string Message { get; set; }
            public RiskLevel RiskLevel { get; set; }
            public float SuggestedDelay { get; set; }
            
            // LLM标签解析结果（如果有）
            public LLMTagParser.ParseResult TagResult { get; set; }
            
            // 本地NLU分析结果
            public LocalNLUAnalyzer.AnalysisResult NLUResult { get; set; }
            
            // 决策结果
            public IntentDecisionMatrix.DecisionResult DecisionResult { get; set; }
        }

        /// <summary>
        /// 混合识别用户意图
        /// v3.0: 本地NLU分析优先，不再依赖AI输出格式化指令
        /// </summary>
        public static RecognitionResult RecognizeIntent(
            string userInput, 
            string aiResponse,
            Pawn speaker,
            Pawn listener)
        {
            if (string.IsNullOrEmpty(aiResponse))
            {
                return new RecognitionResult
                {
                    Success = false,
                    Source = "None",
                    Message = "AI回复为空"
                };
            }

            // 存储AI回复用于轻量LLM确认
            _lastAiResponse = aiResponse;
            
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message("[HybridRecognizer] ════════════════════════════════════════");
                Log.Message("[HybridRecognizer] 开始混合识别 v3.1 (三方案并行)");
                Log.Message("[HybridRecognizer] AI回复: " + (aiResponse.Length > 100 ? aiResponse.Substring(0, 100) + "..." : aiResponse));
            }

            // 构建对话上下文
            var context = BuildContext(userInput, speaker, listener);

            // 第一步：尝试本地NLU分析（主要方法）
            var nluResult = AnalyzeWithLocalNLU(aiResponse, context);
            
            // 第二步：尝试LLM标签解析（增强/备用）
            var tagResult = ParseLLMTag(aiResponse);
            
            // 第三步：综合两种结果，选择最佳
            var finalResult = CombineResults(nluResult, tagResult, speaker, listener);
            
            // 第四步：通过决策矩阵判断
            if (finalResult.Success && finalResult.NLUResult != null)
            {
                var decision = IntentDecisionMatrix.MakeDecision(finalResult.NLUResult);
                finalResult.DecisionResult = decision;
                
                // 根据决策结果处理
                HandleDecision(finalResult, decision, speaker, listener);
            }
            
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message($"[HybridRecognizer] ────────────────────────────────────────");
                Log.Message($"[HybridRecognizer] 最终结果: {(finalResult.Success ? "成功" : "失败")}");
                Log.Message($"[HybridRecognizer] 来源: {finalResult.Source}");
                Log.Message($"[HybridRecognizer] 意图: {finalResult.IntentName ?? "无"}");
                Log.Message($"[HybridRecognizer] 置信度: {finalResult.Confidence:F2}");
                Log.Message($"[HybridRecognizer] ════════════════════════════════════════");
            }
            
            return finalResult;
        }

        /// <summary>
        /// 构建对话上下文
        /// </summary>
        private static ConversationContext BuildContext(string userInput, Pawn speaker, Pawn listener)
        {
            var context = new ConversationContext
            {
                Speaker = speaker,
                Listener = listener,
                PlayerInput = userInput
            };
            
            // 检测玩家输入中的话题
            if (!string.IsNullOrEmpty(userInput))
            {
                string input = userInput.ToLower();
                
                if (input.Contains("加入") || input.Contains("跟我") || input.Contains("一起") || input.Contains("收留"))
                {
                    context.PlayerAskedToRecruit = true;
                    context.TopicTags.Add("招募");
                }
                
                if (input.Contains("喜欢") || input.Contains("爱") || input.Contains("表白") || input.Contains("在一起"))
                {
                    context.PlayerConfessedLove = true;
                    context.TopicTags.Add("恋爱");
                }
            }
            
            return context;
        }

        /// <summary>
        /// 使用本地NLU分析AI回复
        /// </summary>
        private static LocalNLUAnalyzer.AnalysisResult AnalyzeWithLocalNLU(
            string aiResponse, 
            ConversationContext context)
        {
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message($"[HybridRecognizer] ──────────────────────────────");
                Log.Message($"[HybridRecognizer] 阶段1: 本地NLU分析");
            }
            
            return LocalNLUAnalyzer.Analyze(aiResponse, context);
        }

        /// <summary>
        /// 解析LLM标签（如果有）
        /// </summary>
        private static LLMTagParser.ParseResult ParseLLMTag(string aiResponse)
        {
            if (string.IsNullOrEmpty(aiResponse))
            {
                return new LLMTagParser.ParseResult { Success = false };
            }

            var result = LLMTagParser.Parse(aiResponse);
            
            if (result.Success && RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message($"[HybridRecognizer] ──────────────────────────────");
                Log.Message($"[HybridRecognizer] 阶段2: 检测到LLM标签");
                Log.Message($"[HybridRecognizer] 标签: {result.OriginalTag}");
            }
            
            return result;
        }

        /// <summary>
        /// 综合本地NLU和LLM标签的结果
        /// </summary>
        private static RecognitionResult CombineResults(
            LocalNLUAnalyzer.AnalysisResult nluResult,
            LLMTagParser.ParseResult tagResult,
            Pawn speaker,
            Pawn listener)
        {
            var result = new RecognitionResult
            {
                Success = false,
                Source = "None",
                NLUResult = nluResult,
                TagResult = tagResult
            };
            
            // 如果LLM标签存在且有效，优先使用（置信度100%）
            if (tagResult != null && tagResult.Success)
            {
                if (tagResult.TagValue == "NONE")
                {
                    // AI明确拒绝
                    result.Success = false;
                    result.Source = "LLMTag";
                    result.Message = "AI明确拒绝执行操作";
                    result.Confidence = 1.0f;
                    return result;
                }
                
                string intentName = LLMTagParser.TagValueToIntentName(tagResult.TagValue);
                if (!string.IsNullOrEmpty(intentName))
                {
                    result.Success = true;
                    result.Source = "LLMTag";
                    result.IntentId = TagValueToIntentId(tagResult.TagValue);
                    result.IntentName = tagResult.TagValue;
                    result.Confidence = 1.0f;
                    result.Message = $"LLM标签: {tagResult.TagValue}";
                    result.RiskLevel = GetRiskLevelForTag(tagResult.TagValue);
                    result.SuggestedDelay = GetDelayForRisk(result.RiskLevel);
                    return result;
                }
            }
            
            // 使用本地NLU结果
            if (nluResult != null && nluResult.Success && nluResult.Confidence > 0)
            {
                result.Success = true;
                result.Source = "LocalNLU";
                result.IntentId = nluResult.IntentId;
                result.IntentName = nluResult.DisplayName;
                result.Confidence = nluResult.Confidence;
                result.RiskLevel = nluResult.RiskLevel;
                result.SuggestedDelay = nluResult.DelaySeconds;
                result.Message = $"本地NLU分析: {nluResult.DisplayName} (置信度: {nluResult.Confidence:F2})";
                return result;
            }
            
            // 都没有识别到
            result.Message = "未识别到有效意图";
            return result;
        }

        // 存储AI回复用于轻量LLM确认
        private static string _lastAiResponse = "";
        
        /// <summary>
        /// 根据决策结果处理
        /// </summary>
        private static void HandleDecision(
            RecognitionResult result,
            IntentDecisionMatrix.DecisionResult decision,
            Pawn speaker,
            Pawn listener)
        {
            switch (decision.Decision)
            {
                case IntentDecisionMatrix.DecisionType.DoNotExecute:
                    result.Success = false;
                    result.Message = "决策: 不执行 (" + decision.Reason + ")";
                    break;
                    
                case IntentDecisionMatrix.DecisionType.ExecuteDirectly:
                case IntentDecisionMatrix.DecisionType.ExecuteWithDelay:
                    // 添加到延迟队列
                    if (speaker != null)
                    {
                        DelayedActionQueueManager.Enqueue(
                            result.IntentId,
                            result.IntentName,
                            speaker,
                            listener,
                            result.SuggestedDelay,
                            result.RiskLevel,
                            result.Confidence
                        );
                        result.Message = "已加入延迟队列 (" + result.SuggestedDelay.ToString("F1") + "秒后执行)";
                    }
                    break;
                    
                case IntentDecisionMatrix.DecisionType.RequiresConfirmation:
                    // 调用轻量LLM确认
                    HandleLightweightLLMConfirmation(result, speaker, listener);
                    break;
            }
        }
        
        /// <summary>
        /// 处理轻量LLM确认
        /// </summary>
        private static void HandleLightweightLLMConfirmation(
            RecognitionResult result,
            Pawn speaker,
            Pawn listener)
        {
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message("[HybridRecognizer] ──────────────────────────────");
                Log.Message("[HybridRecognizer] 触发轻量LLM确认 (异步)");
                Log.Message("[HybridRecognizer] 意图: " + result.IntentName);
                Log.Message("[HybridRecognizer] 置信度: " + result.Confidence.ToString("F2"));
            }
            
            // 复制需要的数据，避免闭包引用问题
            string intentId = result.IntentId;
            string intentName = result.IntentName;
            string aiResponse = _lastAiResponse;
            float confidence = result.Confidence;
            float suggestedDelay = result.SuggestedDelay;
            RiskLevel riskLevel = result.RiskLevel;
            
            // 启动后台任务进行确认，不阻塞主线程
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var confirmResult = await LightweightLLMConfirmer.ConfirmAsync(
                        intentId,
                        intentName,
                        aiResponse,
                        confidence
                    );

                    // 确认完成后，将结果加入延迟队列
                    // 注意：DelayedActionQueueManager.Enqueue 现已线程安全
                    
                    if (confirmResult.Success)
                    {
                        if (confirmResult.Confirmed)
                        {
                            // LLM确认执行
                            if (speaker != null)
                            {
                                DelayedActionQueueManager.Enqueue(
                                    intentId,
                                    intentName,
                                    speaker,
                                    listener,
                                    suggestedDelay,
                                    riskLevel,
                                    1.0f  // LLM确认后置信度设为1.0
                                );
                                
                                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                                {
                                    Log.Message($"[HybridRecognizer] 异步确认成功: 执行 {intentName} (Token: {confirmResult.TokensUsed})");
                                }
                            }
                        }
                        else
                        {
                            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                            {
                                Log.Message($"[HybridRecognizer] 异步确认结果: 不执行 {intentName} (Token: {confirmResult.TokensUsed})");
                            }
                        }
                    }
                    else
                    {
                        // LLM确认失败，降级处理
                        if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                        {
                            Log.Warning("[HybridRecognizer] 异步确认请求失败: " + confirmResult.Error);
                        }
                        
                        // 降级：如果置信度>=0.7，仍然执行
                        if (confidence >= 0.7f && speaker != null)
                        {
                            DelayedActionQueueManager.Enqueue(
                                intentId,
                                intentName,
                                speaker,
                                listener,
                                suggestedDelay + 1.0f,  // 额外延迟
                                riskLevel,
                                confidence
                            );
                            
                            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                            {
                                Log.Message($"[HybridRecognizer] 降级执行: {intentName} (置信度: {confidence:F2})");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[HybridRecognizer] 异步确认异常: {ex.Message}");
                }
            });
            
            result.Message = "已启动异步LLM确认，将在后台处理";
            
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message("[HybridRecognizer] ──────────────────────────────");
            }
        }
        
        /// <summary>
        /// 设置最后的AI回复（用于轻量LLM确认）
        /// </summary>
        public static void SetLastAiResponse(string aiResponse)
        {
            _lastAiResponse = aiResponse ?? "";
        }

        /// <summary>
        /// 标签值转意图ID
        /// </summary>
        private static string TagValueToIntentId(string tagValue)
        {
            if (string.IsNullOrEmpty(tagValue))
                return null;
            
            switch (tagValue.ToUpper())
            {
                case "RECRUIT": return "recruit_agree";
                case "SURRENDER": return "drop_weapon";
                case "LOVE": return "romance_accept";
                case "BREAKUP": return "romance_breakup";
                case "INSPIRE_BATTLE": return "inspire_fight";
                case "INSPIRE_WORK": return "inspire_work";
                case "REST": return "force_rest";
                case "GIFT": return "give_item";
                case "DINE": return "social_dining";
                case "RELAX": return "social_relax";
                default: return null;
            }
        }

        /// <summary>
        /// 获取标签对应的风险等级
        /// </summary>
        private static RiskLevel GetRiskLevelForTag(string tagValue)
        {
            switch (tagValue?.ToUpper())
            {
                case "RECRUIT":
                case "LOVE":
                    return RiskLevel.High;
                case "BREAKUP":
                case "SURRENDER":
                    return RiskLevel.Critical;
                case "GIFT":
                case "REST":
                case "INSPIRE_BATTLE":
                case "INSPIRE_WORK":
                    return RiskLevel.Medium;
                case "DINE":
                case "RELAX":
                    return RiskLevel.Low;
                default:
                    return RiskLevel.Medium;
            }
        }

        /// <summary>
        /// 根据风险等级获取延迟时间
        /// </summary>
        private static float GetDelayForRisk(RiskLevel risk)
        {
            switch (risk)
            {
                case RiskLevel.Low: return 1.5f;
                case RiskLevel.Medium: return 2.5f;
                case RiskLevel.High: return 3.5f;
                case RiskLevel.Critical: return 4.5f;
                default: return 2.0f;
            }
        }

        /// <summary>
        /// 仅使用本地NLU识别（同步，快速）
        /// </summary>
        public static RecognitionResult RecognizeByNLUOnly(string aiResponse, Pawn speaker = null, Pawn listener = null)
        {
            var context = new ConversationContext
            {
                Speaker = speaker,
                Listener = listener
            };
            
            var nluResult = LocalNLUAnalyzer.Analyze(aiResponse, context);
            
            return new RecognitionResult
            {
                Success = nluResult.Success,
                Source = "LocalNLU",
                IntentId = nluResult.IntentId,
                IntentName = nluResult.DisplayName,
                Confidence = nluResult.Confidence,
                RiskLevel = nluResult.RiskLevel,
                SuggestedDelay = nluResult.DelaySeconds,
                NLUResult = nluResult,
                Message = nluResult.Success ? $"识别到: {nluResult.DisplayName}" : "未识别到意图"
            };
        }

        /// <summary>
        /// 获取统计信息（用于调试）
        /// </summary>
        public static string GetStatistics()
        {
            return "混合识别系统 v3.1\n" +
                   "━━━━━━━━━━━━━━━━━━━━\n" +
                   "策略说明：\n" +
                   "1. 本地NLU分析优先\n" +
                   "   - 分析AI回复的自然语言\n" +
                   "   - 关键词 + 语义模式匹配\n" +
                   "   - 不依赖AI输出格式化指令\n" +
                   "\n" +
                   "2. LLM标签作为增强 (XML格式)\n" +
                   "   - 如果AI输出<Action>XXX</Action>标签\n" +
                   "   - 使用100%置信度\n" +
                   "\n" +
                   "3. 决策矩阵判断\n" +
                   "   - 高置信度 + 低风险 → 直接执行\n" +
                   "   - 中置信度或高风险 → 谨慎处理\n" +
                   "   - 低置信度 → 不执行\n" +
                   "\n" +
                   "4. 延迟队列执行\n" +
                   "   - 2-5秒自然延迟\n" +
                   "   - 支持取消机制\n";
        }
    }
}
