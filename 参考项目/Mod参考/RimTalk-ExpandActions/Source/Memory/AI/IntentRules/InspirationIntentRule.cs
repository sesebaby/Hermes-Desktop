using System.Collections.Generic;

namespace RimTalkExpandActions.Memory.AI.IntentRules
{
    /// <summary>
    /// 战斗灵感意图识别规则
    /// 识别NPC是否受到战斗方面的激励
    /// </summary>
    public class InspirationFightIntentRule : BaseIntentRule
    {
        public override string IntentId => "inspire_fight";
        public override string DisplayName => "战斗灵感";
        public override RiskLevel RiskLevel => RiskLevel.Medium;
        
        protected override string[] StrongPositiveKeywords => new[]
        {
            // 战斗激励
            "热血沸腾", "斗志昂扬", "战意高涨", "状态来了",
            "浑身是劲", "充满力量", "无所畏惧", "势不可挡",
            
            // 战斗宣言
            "杀光他们", "冲啊", "上啊", "战斗吧",
            "让他们见识", "准备好了", "瞄准", "开火",
            
            // 激励回应
            "你说得对", "没错", "我明白了", "我懂了",
            "这就去", "看我的", "交给我", "包在我身上"
        };
        
        protected override string[] WeakPositiveKeywords => new[]
        {
            // 轻微激励
            "有点兴奋", "好像有力量", "感觉不错",
            "精神起来了", "打起精神"
        };
        
        protected override string[] NegativeKeywords => new[]
        {
            // 消极
            "害怕", "恐惧", "不敢", "胆怯",
            "打不过", "太强了", "逃跑", "撤退"
        };
        
        protected override List<SemanticPattern> SemanticPatterns => new List<SemanticPattern>
        {
            new SemanticPattern(
                @"(?:我|感觉).*?(?:充满|浑身|全身).*?(?:力量|干劲|战意)",
                0.5f,
                "充满力量模式"
            ),
            new SemanticPattern(
                @"(?:准备好|做好准备).*?(?:战斗|开战|打)",
                0.4f,
                "战斗准备模式"
            ),
            new SemanticPattern(
                @"(?:害怕|恐惧|胆怯).*?(?:了|着)",
                -0.5f,
                "恐惧模式"
            )
        };
    }
    
    /// <summary>
    /// 工作灵感意图识别规则
    /// 识别NPC是否受到工作方面的激励
    /// </summary>
    public class InspirationWorkIntentRule : BaseIntentRule
    {
        public override string IntentId => "inspire_work";
        public override string DisplayName => "工作灵感";
        public override RiskLevel RiskLevel => RiskLevel.Medium;
        
        protected override string[] StrongPositiveKeywords => new[]
        {
            // 工作激励
            "干劲十足", "精神百倍", "状态很好", "感觉良好",
            "充满干劲", "浑身有劲", "活力满满", "精力充沛",
            
            // 工作决心
            "加油干", "努力工作", "好好干", "拼命干",
            "多干活", "提高效率", "加倍努力", "全力以赴",
            
            // 激励回应
            "你说得对", "有道理", "我会努力", "我明白",
            "交给我", "看我的", "没问题", "包在我身上"
        };
        
        protected override string[] WeakPositiveKeywords => new[]
        {
            // 轻微激励
            "好像有干劲", "精神起来", "状态还行",
            "还能干", "继续努力"
        };
        
        protected override string[] NegativeKeywords => new[]
        {
            // 消极
            "不想干", "太累了", "干不动", "没力气",
            "偷懒", "休息", "摸鱼", "划水"
        };
        
        protected override List<SemanticPattern> SemanticPatterns => new List<SemanticPattern>
        {
            new SemanticPattern(
                @"(?:我|感觉).*?(?:充满|浑身).*?(?:干劲|力量|活力)",
                0.5f,
                "充满干劲模式"
            ),
            new SemanticPattern(
                @"(?:加油|努力|好好).*?(?:干|工作|劳动)",
                0.4f,
                "努力工作模式"
            ),
            new SemanticPattern(
                @"(?:不想|不愿|懒得).*?(?:干|工作|劳动)",
                -0.5f,
                "不想工作模式"
            )
        };
    }
}