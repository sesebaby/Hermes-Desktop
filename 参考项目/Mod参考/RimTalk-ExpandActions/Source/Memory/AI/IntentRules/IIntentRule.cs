using System.Collections.Generic;
using Verse;

namespace RimTalkExpandActions.Memory.AI.IntentRules
{
    /// <summary>
    /// 意图识别规则接口
    /// 每种行为意图（招募、恋爱、投降等）实现此接口
    /// </summary>
    public interface IIntentRule
    {
        /// <summary>
        /// 规则ID，对应 ActionExecutor 的意图名称
        /// 例如: "recruit_agree", "romance_accept"
        /// </summary>
        string IntentId { get; }
        
        /// <summary>
        /// 规则显示名称（用于日志和UI）
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// 风险等级
        /// </summary>
        RiskLevel RiskLevel { get; }
        
        /// <summary>
        /// 执行延迟时间（秒）
        /// </summary>
        float DelaySeconds { get; }
        
        /// <summary>
        /// 分析AI回复文本，返回此意图的置信度
        /// </summary>
        /// <param name="aiResponse">AI回复文本</param>
        /// <param name="context">对话上下文</param>
        /// <returns>0.0 - 1.0 的置信度分数</returns>
        float Analyze(string aiResponse, ConversationContext context);
        
        /// <summary>
        /// 获取分析详情（用于调试）
        /// </summary>
        AnalysisDetails GetLastAnalysisDetails();
    }
    
    /// <summary>
    /// 风险等级枚举
    /// </summary>
    public enum RiskLevel
    {
        /// <summary>低风险：社交聚餐、休闲放松，可逆无持久影响</summary>
        Low = 0,
        
        /// <summary>中风险：赠送物品、休息，有轻微游戏影响</summary>
        Medium = 1,
        
        /// <summary>高风险：招募、恋爱关系，持久影响需确认</summary>
        High = 2,
        
        /// <summary>极高风险：分手、投降，重大负面后果</summary>
        Critical = 3
    }
    
    /// <summary>
    /// 对话上下文信息
    /// 用于增强意图识别的准确性
    /// </summary>
    public class ConversationContext
    {
        /// <summary>说话者（AI角色）</summary>
        public Pawn Speaker { get; set; }
        
        /// <summary>听众（玩家角色）</summary>
        public Pawn Listener { get; set; }
        
        /// <summary>玩家最近的输入（如果可获取）</summary>
        public string PlayerInput { get; set; }
        
        /// <summary>对话历史摘要</summary>
        public List<string> RecentMessages { get; set; } = new List<string>();
        
        /// <summary>玩家是否明确提出招募请求</summary>
        public bool PlayerAskedToRecruit { get; set; }
        
        /// <summary>玩家是否明确表白</summary>
        public bool PlayerConfessedLove { get; set; }
        
        /// <summary>当前对话话题标签</summary>
        public HashSet<string> TopicTags { get; set; } = new HashSet<string>();
    }
    
    /// <summary>
    /// 分析详情（用于调试和日志）
    /// </summary>
    public class AnalysisDetails
    {
        /// <summary>匹配到的强肯定关键词</summary>
        public List<string> MatchedStrongPositive { get; set; } = new List<string>();
        
        /// <summary>匹配到的弱肯定关键词</summary>
        public List<string> MatchedWeakPositive { get; set; } = new List<string>();
        
        /// <summary>匹配到的否定关键词</summary>
        public List<string> MatchedNegative { get; set; } = new List<string>();
        
        /// <summary>匹配到的语义模式</summary>
        public List<PatternMatchInfo> MatchedPatterns { get; set; } = new List<PatternMatchInfo>();
        
        /// <summary>上下文加成</summary>
        public float ContextBoost { get; set; }
        
        /// <summary>最终分数</summary>
        public float FinalScore { get; set; }
        
        /// <summary>各项得分明细</summary>
        public Dictionary<string, float> ScoreBreakdown { get; set; } = new Dictionary<string, float>();
    }
    
    /// <summary>
    /// 模式匹配信息
    /// </summary>
    public class PatternMatchInfo
    {
        public string Pattern { get; set; }
        public string MatchedText { get; set; }
        public float Weight { get; set; }
    }
}