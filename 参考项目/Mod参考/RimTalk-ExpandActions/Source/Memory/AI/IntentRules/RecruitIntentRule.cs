using System.Collections.Generic;

namespace RimTalkExpandActions.Memory.AI.IntentRules
{
    /// <summary>
    /// 招募意图识别规则
    /// 识别NPC是否同意加入玩家殖民地
    /// </summary>
    public class RecruitIntentRule : BaseIntentRule
    {
        public override string IntentId => "recruit_agree";
        public override string DisplayName => "招募同意";
        public override RiskLevel RiskLevel => RiskLevel.High;
        
        /// <summary>
        /// 强肯定关键词（权重 +0.4）
        /// 表示明确同意加入的表达
        /// </summary>
        protected override string[] StrongPositiveKeywords => new[]
        {
            // 直接同意
            "愿意加入", "我加入", "算我一个", "带我走",
            "跟你走", "加入你们", "投奔你", "效忠于你",
            "我是你的人", "一起干", "收留我", "接纳我",
            "留下来", "定居", "安家", "落脚", "扎根",
            
            // 承诺跟随
            "愿意追随", "甘愿效力", "为你效劳", "听从你",
            "跟随你", "追随你", "服从你", "你说了算",
            "誓死追随", "永远跟随", "生死与共",
            
            // 归属表达
            "我属于这里", "这是我的家", "我的归宿", "加入队伍",
            "成为一员", "并肩作战", "共同奋斗", "并肩", "与你并肩",
            "守护这片", "保护这里", "捍卫", "一同守护",
            "携手", "同行", "同路", "战友", "伙伴",
            
            // 决定性表达
            "我决定加入", "我选择加入", "答应你", "同意了",
            "就这么定了", "好的", "没问题", "我同意",
            "荣幸", "是我的荣幸", "荣耀", "光荣",
            
            // 情感归属
            "家人", "家庭", "一家人", "兄弟姐妹",
            "归宿感", "安全感", "温暖",
            
            // 决心表达
            "下定决心", "已经决定", "毫不犹豫", "义无反顾",
            "心意已决", "绝不后悔"
        };
        
        /// <summary>
        /// 弱肯定关键词（权重 +0.2）
        /// 表示可能同意但不够确定的表达
        /// </summary>
        protected override string[] WeakPositiveKeywords => new[]
        {
            // 犹豫但倾向同意
            "好吧", "可以考虑", "那就这样", "行吧",
            "同意", "好的", "没问题", "可以",
            
            // 条件性同意
            "如果你需要", "既然你说", "看在你", "也罢",
            
            // 弱承诺
            "试试看", "暂时", "先跟着", "看看情况",
            
            // 被动同意
            "你说的对", "有道理", "说服我了", "认可"
        };
        
        /// <summary>
        /// 否定关键词（权重 -0.5）
        /// 表示拒绝加入的表达
        /// </summary>
        protected override string[] NegativeKeywords => new[]
        {
            // 直接拒绝
            "不会加入", "拒绝", "别想", "休想",
            "不可能", "我不愿意", "绝不", "算了",
            "不要", "不行", "免谈", "没门",
            
            // 敌意表达
            "滚开", "离我远点", "不需要你", "自己的路",
            
            // 质疑
            "凭什么", "为什么要", "我不信任", "不相信",
            
            // 条件拒绝
            "除非", "不然不会", "不可能答应", "想都别想",
            
            // 保留意见
            "再考虑", "以后再说", "不是现在", "还没决定"
        };
        
        /// <summary>
        /// 语义模式（正则匹配）
        /// </summary>
        protected override List<SemanticPattern> SemanticPatterns => new List<SemanticPattern>
        {
            // 强肯定模式
            new SemanticPattern(
                @"我.*?(?:决定|选择|愿意).*?(?:加入|跟随|追随)",
                0.5f,
                "决定加入模式"
            ),
            new SemanticPattern(
                @"(?:好|行|可以).*?(?:我加入|跟你走|带上我)",
                0.4f,
                "同意加入模式"
            ),
            new SemanticPattern(
                @"(?:从今|从此|现在|以后).*?(?:跟着你|听你的|效忠)",
                0.4f,
                "承诺效忠模式"
            ),
            new SemanticPattern(
                @"(?:算|把|让).*?我.*?(?:一员|一份子|其中)",
                0.4f,
                "请求加入模式"
            ),
            
            // 中性偏肯定模式
            new SemanticPattern(
                @"(?:那|好|行).*?(?:就这样|这么定了|说定了)",
                0.3f,
                "确认同意模式"
            ),
            
            // 否定模式
            new SemanticPattern(
                @"(?:不|没|别|休|勿).*?(?:加入|跟你|听你|效忠)",
                -0.6f,
                "拒绝加入模式"
            ),
            new SemanticPattern(
                @"(?:我不会|我不想|我拒绝|我反对).*?(?:加入|跟随|投靠)",
                -0.6f,
                "明确拒绝模式"
            ),
            new SemanticPattern(
                @"(?:凭什么|为什么要|我为何).*?(?:加入|跟你|听你)",
                -0.3f,
                "质疑模式"
            )
        };
        
        /// <summary>
        /// 上下文加成计算
        /// </summary>
        protected override float CalculateContextBoost(ConversationContext context)
        {
            float boost = 0f;
            
            if (context == null)
                return boost;
            
            // 如果玩家明确提出招募请求，增加基础置信度
            if (context.PlayerAskedToRecruit)
            {
                boost += 0.15f;
            }
            
            // 如果对话历史中包含招募相关话题
            if (context.TopicTags != null && context.TopicTags.Contains("招募"))
            {
                boost += 0.1f;
            }
            
            // 检查玩家输入中是否有招募相关词汇
            if (!string.IsNullOrEmpty(context.PlayerInput))
            {
                string playerInput = context.PlayerInput.ToLower();
                if (playerInput.Contains("加入") || 
                    playerInput.Contains("跟我") || 
                    playerInput.Contains("一起") ||
                    playerInput.Contains("收留"))
                {
                    boost += 0.1f;
                }
            }
            
            return boost;
        }
    }
}