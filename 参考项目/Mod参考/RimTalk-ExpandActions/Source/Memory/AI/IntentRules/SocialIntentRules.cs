using System.Collections.Generic;

namespace RimTalkExpandActions.Memory.AI.IntentRules
{
    /// <summary>
    /// 社交聚餐意图识别规则
    /// 识别NPC是否同意一起吃饭
    /// </summary>
    public class SocialDiningIntentRule : BaseIntentRule
    {
        public override string IntentId => "social_dining";
        public override string DisplayName => "社交聚餐";
        public override RiskLevel RiskLevel => RiskLevel.Low;
        
        protected override string[] StrongPositiveKeywords => new[]
        {
            // 直接同意
            "一起吃饭", "一起吃", "一块吃", "共进晚餐",
            "好啊", "走吧", "去吧", "出发",
            
            // 饥饿表达
            "肚子饿了", "饿了", "想吃东西", "好饿",
            "去吃点东西", "找点吃的", "开饭了",
            
            // 邀请回应
            "当然", "没问题", "正好饿了", "正想吃",
            "太好了", "等你这句话", "刚好我也饿"
        };
        
        protected override string[] WeakPositiveKeywords => new[]
        {
            // 犹豫
            "可以考虑", "或许吧", "看情况", "等会儿",
            "待会", "稍后", "先忙完"
        };
        
        protected override string[] NegativeKeywords => new[]
        {
            // 拒绝
            "不饿", "吃过了", "不想吃", "没胃口",
            "减肥", "不吃了", "忙着呢", "没时间",
            
            // 独自
            "自己吃", "一个人吃", "不用了"
        };
        
        protected override List<SemanticPattern> SemanticPatterns => new List<SemanticPattern>
        {
            new SemanticPattern(
                @"(?:一起|一块|一同).*?(?:吃|用餐|进食)",
                0.5f,
                "一起吃饭模式"
            ),
            new SemanticPattern(
                @"(?:好啊|走吧|行啊).*?(?:吃|去)",
                0.4f,
                "同意邀请模式"
            ),
            new SemanticPattern(
                @"(?:不|没|别).*?(?:吃|饿|想)",
                -0.4f,
                "拒绝吃饭模式"
            )
        };
    }
    
    /// <summary>
    /// 社交放松意图识别规则
    /// 识别NPC是否同意一起休闲娱乐
    /// </summary>
    public class SocialRelaxIntentRule : BaseIntentRule
    {
        public override string IntentId => "social_relax";
        public override string DisplayName => "社交放松";
        public override RiskLevel RiskLevel => RiskLevel.Low;
        
        protected override string[] StrongPositiveKeywords => new[]
        {
            // 直接同意
            "一起玩", "一起放松", "一起休闲", "好主意",
            "走吧", "去吧", "好啊", "没问题",
            
            // 娱乐意愿
            "想玩", "想放松", "想休息", "想娱乐",
            "轻松一下", "放松一下", "休闲一下",
            
            // 积极回应
            "太好了", "正好闲着", "无聊呢", "刚好有空",
            "一起聊聊", "说说话", "陪你"
        };
        
        protected override string[] WeakPositiveKeywords => new[]
        {
            // 犹豫
            "可以吧", "看情况", "等会儿", "待会",
            "先忙完", "稍后"
        };
        
        protected override string[] NegativeKeywords => new[]
        {
            // 拒绝
            "忙着呢", "没时间", "没空", "不想玩",
            "没心情", "不行", "改天吧", "下次",
            
            // 独自
            "自己待着", "一个人", "别烦我"
        };
        
        protected override List<SemanticPattern> SemanticPatterns => new List<SemanticPattern>
        {
            new SemanticPattern(
                @"(?:一起|一块|一同).*?(?:玩|放松|休闲|娱乐)",
                0.5f,
                "一起娱乐模式"
            ),
            new SemanticPattern(
                @"(?:好啊|走吧|行啊).*?(?:玩|去|放松)",
                0.4f,
                "同意邀请模式"
            ),
            new SemanticPattern(
                @"(?:不|没|别).*?(?:玩|放松|时间|空)",
                -0.4f,
                "拒绝放松模式"
            )
        };
    }
}