using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimTalkExpandActions.Memory.AI.IntentRules;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 本地NLU分析器
    /// 使用关键词和模式匹配分析AI回复中的行为意图
    /// 不依赖LLM，完全本地执行
    /// </summary>
    public static class LocalNLUAnalyzer
    {
        // 注册的意图规则
        private static readonly List<IIntentRule> _rules = new List<IIntentRule>();
        
        // 是否已初始化
        private static bool _initialized = false;
        
        /// <summary>
        /// 分析结果
        /// </summary>
        public class AnalysisResult
        {
            /// <summary>是否成功识别到意图</summary>
            public bool Success { get; set; }
            
            /// <summary>识别到的意图ID</summary>
            public string IntentId { get; set; }
            
            /// <summary>意图显示名称</summary>
            public string DisplayName { get; set; }
            
            /// <summary>置信度 (0.0 - 1.0)</summary>
            public float Confidence { get; set; }
            
            /// <summary>风险等级</summary>
            public RiskLevel RiskLevel { get; set; }
            
            /// <summary>建议延迟时间（秒）</summary>
            public float DelaySeconds { get; set; }
            
            /// <summary>使用的规则</summary>
            public IIntentRule Rule { get; set; }
            
            /// <summary>所有规则的评分（用于调试）</summary>
            public Dictionary<string, float> AllScores { get; set; } = new Dictionary<string, float>();
            
            /// <summary>分析详情</summary>
            public AnalysisDetails Details { get; set; }
        }
        
        /// <summary>
        /// 初始化分析器，注册所有意图规则
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;
            
            _rules.Clear();
            
            // 注册所有意图规则
            // 招募
            RegisterRule(new RecruitIntentRule());
            
            // 恋爱
            RegisterRule(new RomanceAcceptIntentRule());
            RegisterRule(new RomanceBreakupIntentRule());
            
            // 投降
            RegisterRule(new SurrenderIntentRule());
            
            // 赠送
            RegisterRule(new GiftIntentRule());
            
            // 休息
            RegisterRule(new RestIntentRule());
            
            // 灵感
            RegisterRule(new InspirationFightIntentRule());
            RegisterRule(new InspirationWorkIntentRule());
            
            // 社交
            RegisterRule(new SocialDiningIntentRule());
            RegisterRule(new SocialRelaxIntentRule());
            
            _initialized = true;
            
            Log.Message($"[LocalNLU] 初始化完成，已注册 {_rules.Count} 条意图规则");
        }
        
        /// <summary>
        /// 注册意图规则
        /// </summary>
        public static void RegisterRule(IIntentRule rule)
        {
            if (rule == null)
                return;
            
            // 检查是否已存在相同ID的规则
            var existing = _rules.FirstOrDefault(r => r.IntentId == rule.IntentId);
            if (existing != null)
            {
                _rules.Remove(existing);
                Log.Warning($"[LocalNLU] 替换已存在的规则: {rule.IntentId}");
            }
            
            _rules.Add(rule);
        }
        
        /// <summary>
        /// 分析AI回复文本
        /// </summary>
        /// <param name="aiResponse">AI回复文本</param>
        /// <param name="context">对话上下文</param>
        /// <returns>分析结果</returns>
        public static AnalysisResult Analyze(string aiResponse, ConversationContext context = null)
        {
            // 确保已初始化
            if (!_initialized)
            {
                Initialize();
            }
            
            var result = new AnalysisResult
            {
                Success = false,
                Confidence = 0f
            };
            
            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                return result;
            }
            
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message("[LocalNLU] ════════════════════════════════════════");
                Log.Message($"[LocalNLU] 开始分析AI回复");
                Log.Message($"[LocalNLU] 文本长度: {aiResponse.Length} 字符");
            }
            
            // 对所有规则进行评分
            float bestScore = 0f;
            IIntentRule bestRule = null;
            
            foreach (var rule in _rules)
            {
                try
                {
                    float score = rule.Analyze(aiResponse, context);
                    result.AllScores[rule.IntentId] = score;
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRule = rule;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[LocalNLU] 规则 {rule.IntentId} 分析异常: {ex.Message}");
                }
            }
            
            // 设置结果
            if (bestRule != null && bestScore > 0)
            {
                result.Success = true;
                result.IntentId = bestRule.IntentId;
                result.DisplayName = bestRule.DisplayName;
                result.Confidence = bestScore;
                result.RiskLevel = bestRule.RiskLevel;
                result.DelaySeconds = bestRule.DelaySeconds;
                result.Rule = bestRule;
                result.Details = bestRule.GetLastAnalysisDetails();
            }
            
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message("[LocalNLU] ────────────────────────────────────────");
                Log.Message($"[LocalNLU] 分析完成");
                Log.Message($"[LocalNLU] 最佳匹配: {result.DisplayName ?? "无"}");
                Log.Message($"[LocalNLU] 置信度: {result.Confidence:F2}");
                Log.Message($"[LocalNLU] 风险等级: {result.RiskLevel}");
                Log.Message("[LocalNLU] ════════════════════════════════════════");
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取所有已注册的规则
        /// </summary>
        public static IReadOnlyList<IIntentRule> GetAllRules()
        {
            if (!_initialized)
            {
                Initialize();
            }
            return _rules.AsReadOnly();
        }
        
        /// <summary>
        /// 根据ID获取规则
        /// </summary>
        public static IIntentRule GetRule(string intentId)
        {
            if (!_initialized)
            {
                Initialize();
            }
            return _rules.FirstOrDefault(r => r.IntentId == intentId);
        }
        
        /// <summary>
        /// 重置分析器（用于测试）
        /// </summary>
        public static void Reset()
        {
            _rules.Clear();
            _initialized = false;
        }
    }
}