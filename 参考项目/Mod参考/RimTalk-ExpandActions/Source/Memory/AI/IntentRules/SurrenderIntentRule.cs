using System.Collections.Generic;

namespace RimTalkExpandActions.Memory.AI.IntentRules
{
    /// <summary>
    /// 投降意图识别规则
    /// 识别NPC是否愿意投降/放下武器
    /// </summary>
    public class SurrenderIntentRule : BaseIntentRule
    {
        public override string IntentId => "drop_weapon";
        public override string DisplayName => "投降";
        public override RiskLevel RiskLevel => RiskLevel.Critical;
        
        protected override string[] StrongPositiveKeywords => new[]
        {
            // 直接投降
            "我投降", "投降", "放下武器", "弃械",
            "不打了", "认输", "别杀我", "饶命",
            
            // 求饶
            "求你了", "放过我", "我服了", "我认栽",
            "别开枪", "不要杀我", "我不想死",
            
            // 表示屈服
            "听你的", "服从你", "臣服", "屈服",
            "跪下", "认输了", "打不过", "败了"
        };
        
        protected override string[] WeakPositiveKeywords => new[]
        {
            // 犹豫
            "考虑投降", "或许可以", "暂停", "停战",
            "休战", "谈判", "和解"
        };
        
        protected override string[] NegativeKeywords => new[]
        {
            // 拒绝投降
            "绝不投降", "死战到底", "宁死不屈", "来战",
            "不可能", "做梦", "想都别想", "杀了我",
            
            // 战斗宣言
            "战斗到底", "血战到底", "誓死", "拼了",
            "鱼死网破", "同归于尽"
        };
        
        protected override List<SemanticPattern> SemanticPatterns => new List<SemanticPattern>
        {
            new SemanticPattern(
                @"(?:我|好|行).*?(?:投降|认输|放弃抵抗)",
                0.6f,
                "同意投降模式"
            ),
            new SemanticPattern(
                @"(?:别|不要|求你).*?(?:杀|打|伤害)",
                0.4f,
                "求饶模式"
            ),
            new SemanticPattern(
                @"(?:放下|丢掉|扔掉).*?(?:武器|枪|刀|剑)",
                0.5f,
                "放下武器模式"
            ),
            new SemanticPattern(
                @"(?:不|绝不|决不).*?(?:投降|屈服|认输)",
                -0.7f,
                "拒绝投降模式"
            )
        };
    }
}