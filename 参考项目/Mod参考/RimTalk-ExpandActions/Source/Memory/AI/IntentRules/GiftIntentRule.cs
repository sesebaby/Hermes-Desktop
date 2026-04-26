using System.Collections.Generic;

namespace RimTalkExpandActions.Memory.AI.IntentRules
{
    /// <summary>
    /// 赠送物品意图识别规则
    /// 识别NPC是否要赠送物品给对方
    /// </summary>
    public class GiftIntentRule : BaseIntentRule
    {
        public override string IntentId => "give_item";
        public override string DisplayName => "赠送物品";
        public override RiskLevel RiskLevel => RiskLevel.Medium;
        
        protected override string[] StrongPositiveKeywords => new[]
        {
            // 直接赠送
            "送给你", "给你", "拿去", "收下",
            "这个给你", "送你", "赠送", "礼物",
            
            // 表达心意
            "一点心意", "小小心意", "表示感谢", "谢礼",
            "作为报答", "回礼", "还礼",
            
            // 分享
            "分你一点", "拿一些", "带上", "留着用"
        };
        
        protected override string[] WeakPositiveKeywords => new[]
        {
            // 询问是否需要
            "需要吗", "要不要", "用得上", "可能有用",
            
            // 犹豫
            "或许", "可能", "如果你想"
        };
        
        protected override string[] NegativeKeywords => new[]
        {
            // 拒绝给予
            "不给", "不能给", "自己留着", "不送",
            "舍不得", "我需要", "我自己用",
            
            // 索取
            "给我", "我要", "还我", "交出来"
        };
        
        protected override List<SemanticPattern> SemanticPatterns => new List<SemanticPattern>
        {
            new SemanticPattern(
                @"(?:这个|这些|这东西).*?(?:送|给|拿去)",
                0.5f,
                "赠送物品模式"
            ),
            new SemanticPattern(
                @"(?:送|给|赠).*?(?:你|拿去|收下)",
                0.4f,
                "给予模式"
            ),
            new SemanticPattern(
                @"(?:作为|当作|表示).*?(?:感谢|心意|礼物)",
                0.4f,
                "表达心意模式"
            ),
            new SemanticPattern(
                @"(?:不|没|别).*?(?:送|给|分)",
                -0.5f,
                "拒绝给予模式"
            )
        };
    }
}