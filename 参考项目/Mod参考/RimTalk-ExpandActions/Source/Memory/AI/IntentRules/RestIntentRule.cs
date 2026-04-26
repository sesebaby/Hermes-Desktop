using System.Collections.Generic;

namespace RimTalkExpandActions.Memory.AI.IntentRules
{
    /// <summary>
    /// 休息意图识别规则
    /// 识别NPC是否要去休息/睡觉
    /// </summary>
    public class RestIntentRule : BaseIntentRule
    {
        public override string IntentId => "force_rest";
        public override string DisplayName => "休息";
        public override RiskLevel RiskLevel => RiskLevel.Low;
        
        protected override string[] StrongPositiveKeywords => new[]
        {
            // 直接表达
            "去睡觉", "去休息", "睡一会", "休息一下",
            "好困", "好累", "累死了", "撑不住了",
            
            // 身体状况
            "精疲力尽", "筋疲力尽", "体力不支", "虚弱",
            "昏昏欲睡", "快睡着了", "眼皮打架",
            
            // 意愿
            "想睡觉", "想休息", "需要休息", "必须休息",
            "躺下", "躺一会", "小憩", "打个盹"
        };
        
        protected override string[] WeakPositiveKeywords => new[]
        {
            // 轻微疲劳
            "有点累", "有点困", "略感疲惫", "稍微休息",
            
            // 建议
            "该休息了", "应该休息", "需要睡眠"
        };
        
        protected override string[] NegativeKeywords => new[]
        {
            // 拒绝休息
            "不累", "不困", "精神很好", "还能撑",
            "继续工作", "不需要休息", "休息什么",
            
            // 否定
            "不用休息", "不想睡", "睡不着", "失眠"
        };
        
        protected override List<SemanticPattern> SemanticPatterns => new List<SemanticPattern>
        {
            new SemanticPattern(
                @"(?:我|好|太).*?(?:困|累|疲惫|疲劳)",
                0.4f,
                "疲劳表达模式"
            ),
            new SemanticPattern(
                @"(?:去|想|要).*?(?:睡觉|休息|躺下)",
                0.5f,
                "休息意愿模式"
            ),
            new SemanticPattern(
                @"(?:撑不住|顶不住|扛不住|受不了).*?了",
                0.5f,
                "体力不支模式"
            ),
            new SemanticPattern(
                @"(?:不|没|别).*?(?:累|困|睡|休息)",
                -0.5f,
                "否定疲劳模式"
            )
        };
    }
}