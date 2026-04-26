using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Verse;

namespace RimTalkExpandActions.Memory.AI.IntentRules
{
    /// <summary>
    /// 意图规则基类
    /// 提供通用的关键词匹配和模式分析逻辑
    /// </summary>
    public abstract class BaseIntentRule : IIntentRule
    {
        // 最后一次分析的详情
        protected AnalysisDetails _lastAnalysisDetails;
        
        /// <summary>规则ID</summary>
        public abstract string IntentId { get; }
        
        /// <summary>显示名称</summary>
        public abstract string DisplayName { get; }
        
        /// <summary>风险等级</summary>
        public abstract RiskLevel RiskLevel { get; }
        
        /// <summary>延迟时间（秒）</summary>
        public virtual float DelaySeconds => GetDefaultDelay();
        
        /// <summary>强肯定关键词（权重 +0.4）</summary>
        protected abstract string[] StrongPositiveKeywords { get; }
        
        /// <summary>弱肯定关键词（权重 +0.2）</summary>
        protected abstract string[] WeakPositiveKeywords { get; }
        
        /// <summary>否定关键词（权重 -0.5）</summary>
        protected abstract string[] NegativeKeywords { get; }
        
        /// <summary>语义模式列表</summary>
        protected virtual List<SemanticPattern> SemanticPatterns => new List<SemanticPattern>();
        
        // 权重配置
        protected const float STRONG_POSITIVE_WEIGHT = 0.4f;
        protected const float WEAK_POSITIVE_WEIGHT = 0.2f;
        protected const float NEGATIVE_WEIGHT = -0.5f;
        
        /// <summary>
        /// 分析AI回复文本
        /// </summary>
        public virtual float Analyze(string aiResponse, ConversationContext context)
        {
            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                return 0f;
            }
            
            _lastAnalysisDetails = new AnalysisDetails();
            float score = 0f;
            
            // 预处理文本（转小写，去除多余空格）
            string normalizedText = NormalizeText(aiResponse);
            
            // Layer 1: 关键词匹配
            score += AnalyzeKeywords(normalizedText);
            
            // Layer 2: 语义模式匹配
            score += AnalyzePatterns(normalizedText);
            
            // Layer 3: 上下文增强
            float contextBoost = CalculateContextBoost(context);
            _lastAnalysisDetails.ContextBoost = contextBoost;
            score += contextBoost;
            
            // 归一化到 0-1 范围
            score = Clamp(score, 0f, 1f);
            
            _lastAnalysisDetails.FinalScore = score;
            
            // 日志输出
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                LogAnalysisResult(aiResponse, score);
            }
            
            return score;
        }
        
        /// <summary>
        /// 分析关键词匹配
        /// </summary>
        protected virtual float AnalyzeKeywords(string normalizedText)
        {
            float score = 0f;
            
            // 检查强肯定关键词
            foreach (var keyword in StrongPositiveKeywords)
            {
                if (ContainsKeyword(normalizedText, keyword))
                {
                    _lastAnalysisDetails.MatchedStrongPositive.Add(keyword);
                    score += STRONG_POSITIVE_WEIGHT;
                    _lastAnalysisDetails.ScoreBreakdown[$"强肯定:{keyword}"] = STRONG_POSITIVE_WEIGHT;
                }
            }
            
            // 检查弱肯定关键词
            foreach (var keyword in WeakPositiveKeywords)
            {
                if (ContainsKeyword(normalizedText, keyword))
                {
                    _lastAnalysisDetails.MatchedWeakPositive.Add(keyword);
                    score += WEAK_POSITIVE_WEIGHT;
                    _lastAnalysisDetails.ScoreBreakdown[$"弱肯定:{keyword}"] = WEAK_POSITIVE_WEIGHT;
                }
            }
            
            // 检查否定关键词（重要：否定词优先级高）
            foreach (var keyword in NegativeKeywords)
            {
                if (ContainsKeyword(normalizedText, keyword))
                {
                    _lastAnalysisDetails.MatchedNegative.Add(keyword);
                    score += NEGATIVE_WEIGHT;
                    _lastAnalysisDetails.ScoreBreakdown[$"否定:{keyword}"] = NEGATIVE_WEIGHT;
                }
            }
            
            return score;
        }
        
        /// <summary>
        /// 分析语义模式
        /// </summary>
        protected virtual float AnalyzePatterns(string normalizedText)
        {
            float score = 0f;
            
            foreach (var pattern in SemanticPatterns)
            {
                try
                {
                    var match = Regex.Match(normalizedText, pattern.Pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        _lastAnalysisDetails.MatchedPatterns.Add(new PatternMatchInfo
                        {
                            Pattern = pattern.Pattern,
                            MatchedText = match.Value,
                            Weight = pattern.Weight
                        });
                        score += pattern.Weight;
                        _lastAnalysisDetails.ScoreBreakdown[$"模式:{pattern.Description}"] = pattern.Weight;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[LocalNLU] 模式匹配异常: {pattern.Pattern}, 错误: {ex.Message}");
                }
            }
            
            return score;
        }
        
        /// <summary>
        /// 计算上下文加成
        /// 子类可重写以添加特定逻辑
        /// </summary>
        protected virtual float CalculateContextBoost(ConversationContext context)
        {
            return 0f;
        }
        
        /// <summary>
        /// 获取最后一次分析详情
        /// </summary>
        public AnalysisDetails GetLastAnalysisDetails()
        {
            return _lastAnalysisDetails ?? new AnalysisDetails();
        }
        
        #region 辅助方法
        
        /// <summary>
        /// 文本标准化处理
        /// </summary>
        protected string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            
            // 转小写，去除多余空格
            return Regex.Replace(text.ToLower(), @"\s+", " ").Trim();
        }
        
        /// <summary>
        /// 检查是否包含关键词（支持部分匹配）
        /// </summary>
        protected bool ContainsKeyword(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                return false;
            
            return text.Contains(keyword.ToLower());
        }
        
        /// <summary>
        /// 数值钳制
        /// </summary>
        protected float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
        
        /// <summary>
        /// 根据风险等级获取默认延迟
        /// </summary>
        private float GetDefaultDelay()
        {
            switch (RiskLevel)
            {
                case RiskLevel.Low:
                    return 1.5f;
                case RiskLevel.Medium:
                    return 2.5f;
                case RiskLevel.High:
                    return 3.5f;
                case RiskLevel.Critical:
                    return 4.5f;
                default:
                    return 2f;
            }
        }
        
        /// <summary>
        /// 输出分析日志
        /// </summary>
        protected void LogAnalysisResult(string aiResponse, float score)
        {
            var details = _lastAnalysisDetails;
            Log.Message($"[LocalNLU] ━━━━━━━━ {DisplayName} 分析 ━━━━━━━━");
            Log.Message($"[LocalNLU] 输入文本: {(aiResponse.Length > 50 ? aiResponse.Substring(0, 50) + "..." : aiResponse)}");
            
            if (details.MatchedStrongPositive.Count > 0)
                Log.Message($"[LocalNLU] 强肯定: [{string.Join(", ", details.MatchedStrongPositive)}]");
            
            if (details.MatchedWeakPositive.Count > 0)
                Log.Message($"[LocalNLU] 弱肯定: [{string.Join(", ", details.MatchedWeakPositive)}]");
            
            if (details.MatchedNegative.Count > 0)
                Log.Message($"[LocalNLU] 否定词: [{string.Join(", ", details.MatchedNegative)}]");
            
            if (details.MatchedPatterns.Count > 0)
            {
                foreach (var p in details.MatchedPatterns)
                {
                    Log.Message($"[LocalNLU] 模式匹配: '{p.MatchedText}' (权重: {p.Weight:+0.0;-0.0})");
                }
            }
            
            if (details.ContextBoost != 0)
                Log.Message($"[LocalNLU] 上下文加成: {details.ContextBoost:+0.0;-0.0}");
            
            Log.Message($"[LocalNLU] 最终得分: {score:F2}");
            Log.Message($"[LocalNLU] ━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
        
        #endregion
    }
    
    /// <summary>
    /// 语义模式定义
    /// </summary>
    public class SemanticPattern
    {
        /// <summary>正则表达式模式</summary>
        public string Pattern { get; set; }
        
        /// <summary>权重（正数为肯定，负数为否定）</summary>
        public float Weight { get; set; }
        
        /// <summary>描述（用于日志）</summary>
        public string Description { get; set; }
        
        public SemanticPattern(string pattern, float weight, string description = null)
        {
            Pattern = pattern;
            Weight = weight;
            Description = description ?? pattern;
        }
    }
}