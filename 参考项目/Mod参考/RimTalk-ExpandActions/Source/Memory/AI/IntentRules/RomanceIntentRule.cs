using System.Collections.Generic;

namespace RimTalkExpandActions.Memory.AI.IntentRules
{
    /// <summary>
    /// 恋爱意图识别规则
    /// 识别NPC是否接受恋爱表白
    /// </summary>
    public class RomanceAcceptIntentRule : BaseIntentRule
    {
        public override string IntentId => "romance_accept";
        public override string DisplayName => "接受恋爱";
        public override RiskLevel RiskLevel => RiskLevel.High;
        
        protected override string[] StrongPositiveKeywords => new[]
        {
            // 直接表白回应
            "我也喜欢你", "我也爱你", "我爱你", "喜欢你",
            "在一起吧", "做我的人", "我愿意", "答应你",
            
            // 接受表白
            "好啊", "我接受", "我同意", "太好了",
            "终于等到", "等你很久", "我也是", "心意相通",
            
            // 浪漫表达
            "嫁给你", "娶你", "永远在一起", "一辈子",
            "心动", "心跳加速", "脸红", "害羞",
            
            // 承诺
            "不会离开", "永远爱你", "只爱你", "属于你"
        };
        
        protected override string[] WeakPositiveKeywords => new[]
        {
            // 犹豫但倾向接受
            "好感", "不讨厌", "有点喜欢", "或许",
            "考虑一下", "给我时间", "慢慢来", "试试看",
            
            // 暧昧
            "暧昧", "心动过", "曾经心动", "有感觉"
        };
        
        protected override string[] NegativeKeywords => new[]
        {
            // 直接拒绝
            "只当朋友", "不合适", "对不起", "抱歉",
            "不喜欢", "别这样", "我们不可能", "不行",
            
            // 理由
            "有喜欢的人", "不想恋爱", "不是时候", "太快了",
            
            // 强烈拒绝
            "滚", "离我远点", "恶心", "别碰我"
        };
        
        protected override List<SemanticPattern> SemanticPatterns => new List<SemanticPattern>
        {
            new SemanticPattern(
                @"我.*?(?:也|同样|一样).*?(?:喜欢|爱|心动)",
                0.5f,
                "回应表白模式"
            ),
            new SemanticPattern(
                @"(?:愿意|想要|希望).*?(?:在一起|交往|恋爱)",
                0.5f,
                "接受交往模式"
            ),
            new SemanticPattern(
                @"(?:不|没|别).*?(?:喜欢|爱|接受|答应)",
                -0.6f,
                "拒绝表白模式"
            ),
            new SemanticPattern(
                @"(?:只是|只能|只想).*?(?:朋友|同伴|伙伴)",
                -0.5f,
                "友区模式"
            )
        };
        
        protected override float CalculateContextBoost(ConversationContext context)
        {
            float boost = 0f;
            
            if (context == null)
                return boost;
            
            if (context.PlayerConfessedLove)
            {
                boost += 0.15f;
            }
            
            if (context.TopicTags != null && context.TopicTags.Contains("恋爱"))
            {
                boost += 0.1f;
            }
            
            return boost;
        }
    }
    
    /// <summary>
    /// 分手意图识别规则
    /// </summary>
    public class RomanceBreakupIntentRule : BaseIntentRule
    {
        public override string IntentId => "romance_breakup";
        public override string DisplayName => "分手";
        public override RiskLevel RiskLevel => RiskLevel.Critical;
        
        protected override string[] StrongPositiveKeywords => new[]
        {
            // 直接分手
            "分手吧", "我们分手", "结束吧", "分开吧",
            "不合适", "走到尽头", "缘分已尽", "算了吧",
            
            // 决绝表达
            "不爱了", "不喜欢了", "厌倦了", "累了",
            "放过我", "放过彼此", "各自安好", "别再纠缠",
            
            // 告别
            "永别了", "再见了", "珍重", "保重"
        };
        
        protected override string[] WeakPositiveKeywords => new[]
        {
            // 犹豫
            "冷静一下", "需要空间", "暂时分开", "想想",
            "考虑清楚", "不确定", "迷茫"
        };
        
        protected override string[] NegativeKeywords => new[]
        {
            // 挽留
            "不要分手", "别走", "不分开", "不想失去",
            "还爱你", "不会放弃", "再给一次机会",
            
            // 道歉和解
            "对不起", "我错了", "原谅我", "和好"
        };
        
        protected override List<SemanticPattern> SemanticPatterns => new List<SemanticPattern>
        {
            new SemanticPattern(
                @"(?:我们|咱们).*?(?:分手|分开|结束)",
                0.6f,
                "提出分手模式"
            ),
            new SemanticPattern(
                @"(?:不想|不能|无法).*?(?:继续|在一起|维持)",
                0.5f,
                "无法继续模式"
            ),
            new SemanticPattern(
                @"(?:别|不要|不想).*?(?:分手|分开|离开)",
                -0.6f,
                "挽留模式"
            )
        };
    }
}