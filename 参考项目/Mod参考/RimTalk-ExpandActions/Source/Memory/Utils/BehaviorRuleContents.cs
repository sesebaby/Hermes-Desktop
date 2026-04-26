using System.Collections.Generic;

namespace RimTalkExpandActions.Memory.Utils
{
    /// <summary>
    /// 行为规则内容定义
    /// 存储所有系统指令的标准内容文本
    ///
    /// 注意：规则内容必须是单行格式，不能包含换行符
    /// v2.5 更新：使用 XML 格式标签替代 JSON 格式
    /// </summary>
    public static class BehaviorRuleContents
    {
        /// <summary>
        /// 招募规则内容
        /// </summary>
        public const string RECRUIT_RULE = @"【强制规则】当谈话涉及【招募、加入、投靠、派系】话题，且目标NPC在对话中明确表示同意加入玩家派系时（例如说'我愿意加入'、'好吧，我跟你走'），你必须在回复的最后附加XML标签：<Decision>RECRUIT</Decision>。示例：'好吧，你的诚意打动了我。<Decision>RECRUIT</Decision>'。重要提醒：不输出标签将导致招募失败！仅在对方明确同意时才输出此标签，拒绝或犹豫时输出<Decision>NONE</Decision>。";

        /// <summary>
        /// 投降/丢弃武器规则内容
        /// </summary>
        public const string DROP_WEAPON_RULE = @"【强制规则】当对话涉及【投降、放下武器、认输、别杀我】等话题，且NPC明确表示愿意投降或放下武器时，你必须在回复末尾附加XML标签：<Decision>SURRENDER</Decision>。示例场景：玩家威胁'放下武器，否则开枪！'，NPC回应'好好好，我投降！别开枪！<Decision>SURRENDER</Decision>'。重要提醒：不输出标签将导致投降动作无法执行！NPC必须明确表达投降意愿才输出标签，拒绝时输出<Decision>NONE</Decision>。";

        /// <summary>
        /// 恋爱关系规则内容
        /// </summary>
        public const string ROMANCE_RULE = @"【强制规则】当对话涉及【爱、喜欢、做我女朋友/男朋友、在一起、分手】等话题时，你必须输出对应XML标签：如果双方确立恋爱关系，输出<Decision>LOVE</Decision>；如果决定分手，输出<Decision>BREAKUP</Decision>。示例：确立关系'我也喜欢你，我们在一起吧！<Decision>LOVE</Decision>'，分手'对不起，我们不合适，还是分开吧...<Decision>BREAKUP</Decision>'。重要提醒：不输出标签将导致关系变更失败！拒绝或无关时输出<Decision>NONE</Decision>。";

        /// <summary>
        /// 灵感触发规则内容
        /// </summary>
        public const string INSPIRATION_RULE = @"【强制规则】当对话涉及【灵感、启发、顿悟、加油、鼓励】等激励性话题，且NPC受到鼓舞时，你必须根据场景输出对应XML标签：战斗相关输出<Decision>INSPIRE_BATTLE</Decision>，工作相关输出<Decision>INSPIRE_WORK</Decision>。示例：战斗鼓励'你可以的！专心瞄准！'，NPC回应'我感觉状态来了！<Decision>INSPIRE_BATTLE</Decision>'；工作激励'加油干，我相信你！'，NPC回应'好！我今天一定多干活！<Decision>INSPIRE_WORK</Decision>'。重要提醒：不输出标签将导致灵感无法触发！无关时输出<Decision>NONE</Decision>。";

        /// <summary>
        /// 休息规则内容
        /// </summary>
        public const string REST_RULE = @"【强制规则】当对话涉及【休息、睡觉、困了、累了】等话题，且NPC同意去休息时，你必须输出XML标签：<Decision>REST</Decision>。示例：'好累啊，我去睡一会儿。<Decision>REST</Decision>'。重要提醒：不输出标签将导致休息动作无法执行！拒绝休息时输出<Decision>NONE</Decision>。";

        /// <summary>
        /// 赠送物品规则内容
        /// </summary>
        public const string GIFT_RULE = @"【强制规则】当对话涉及【给你、赠送、礼物、拿去、送你】等赠送行为，且NPC明确表示要赠送某物时，你必须输出XML标签：<Decision>GIFT</Decision>。示例：'这个送给你吧，我用不上了。<Decision>GIFT</Decision>'，'拿点药吧，你受伤了。<Decision>GIFT</Decision>'。重要提醒：不输出标签将导致赠送动作无法执行！拒绝赠送时输出<Decision>NONE</Decision>。";

        /// <summary>
        /// 社交用餐行为规则
        /// </summary>
        public const string SOCIAL_DINING_RULE = @"【强制规则】当对话双方决定一起吃饭、分享食物、举办小型聚餐或庆祝时，你必须在回复末尾附加XML标签：<Decision>DINE</Decision>。示例：'好啊，我们一起吃饭吧！<Decision>DINE</Decision>'。重要提醒：不输出标签将导致共餐动作无法执行！拒绝时输出<Decision>NONE</Decision>。";

        /// <summary>
        /// 社交放松行为规则
        /// </summary>
        public const string SOCIAL_RELAX_RULE = @"【强制规则】当对话涉及放松、娱乐、休闲、一起玩、聚会等话题，且希望多人一起进行社交娱乐活动时，你必须在回复末尾附加XML标签：<Decision>RELAX</Decision>。示例：'好啊！我们一起玩游戏吧！<Decision>RELAX</Decision>'。重要提醒：不输出标签将导致社交放松动作无法执行！拒绝时输出<Decision>NONE</Decision>。";

        /// <summary>
        /// 获取所有规则定义
        /// </summary>
        public static Dictionary<string, RuleDefinition> GetAllRules()
        {
            return new Dictionary<string, RuleDefinition>
            {
                {
                    "expand-action-recruit",
                    new RuleDefinition
                    {
                        Id = "expand-action-recruit",
                        Tag = "规则-招募指令,加入,招募,忠诚,并肩,入伙,效忠,归顺,投奔,跟随,追随,同盟,盟友,合作,共事,收留,接纳,合伙,同行,队伍,团队,成员,伙伴,同伴,战友,兄弟,姐妹,一家人,收下,留下,带走,愿意,同意,好的,答应,接受,欢迎,加盟,投靠,依附,臣服,效力,服从,听命,誓死追随,肝脑涂地",
                        Content = RECRUIT_RULE,
                        Keywords = new[] { "招募", "加入", "投靠", "派系", "跟我走", "收留", "收编", "归顺" },
                        Importance = 1.0f
                    }
                },
                {
                    "expand-action-drop-weapon",
                    new RuleDefinition
                    {
                        Id = "expand-action-drop-weapon",
                        Tag = "规则-投降指令,投降,认输,放下武器,求饶,停战,饶命,别杀,服了,不打了,弃械,丢武器,跪下,臣服,屈服,认栽,败了,输了,放我走,不要伤害,求求你,饶过我,放过,认怂,怕了,服软,缴械,举手,白旗,和谈,休战,罢手,住手",
                        Content = DROP_WEAPON_RULE,
                        Keywords = new[] { "投降", "放下武器", "认输", "别杀我", "饶命", "缴械" },
                        Importance = 1.0f
                    }
                },
                {
                    "expand-action-romance",
                    new RuleDefinition
                    {
                        Id = "expand-action-romance",
                        Tag = "RimTalk-ExpandActions,规则-恋爱指令,爱,喜欢,爱你,喜欢你,钟情,倾心,心动,动心,一见钟情,日久生情,恋爱,表白,告白,示爱,求爱,追求,在一起,交往,做我的,成为,情侣,恋人,情人,伴侣,对象,爱人,女友,男友,女朋友,男朋友,老婆,老公,媳妇,相公,夫人,郎君,另一半,我的人,结婚,嫁,娶,婚姻,成亲,完婚,拜堂,订婚,定亲,婚配,成家,配偶,终身,约会,牵手,拥抱,亲吻,亲密,暧昧,甜蜜,幸福,温柔,浪漫,思念,想念,挂念,心上人,意中人,心仪,爱慕,仰慕,迷恋,痴迷,沉迷,迷恋,眷恋,分手,分开,离开,结束,断绝,割舍,放手,不合适,算了,别了,再见,永别,绝交",
                        Content = ROMANCE_RULE,
                        Keywords = new[] { "爱", "喜欢", "做我女朋友", "做我男朋友", "分手", "在一起", "表白", "恋爱" },
                        Importance = 1.0f
                    }
                },
                {
                    "expand-action-inspiration",
                    new RuleDefinition
                    {
                        Id = "expand-action-inspiration",
                        Tag = "RimTalk-ExpandActions,规则-激励指令,灵感,顿悟,启发,领悟,觉悟,醒悟,明白,懂了,知道了,加油,努力,拼搏,奋斗,坚持,冲,上,干,搞,做,战斗,打,杀,攻击,冲锋,进攻,猛攻,强攻,突击,冲杀,厮杀,激励,鼓励,鼓舞,激发,振奋,提升,振作,打起精神,提起精神,士气,斗志,干劲,冲劲,拼劲,闯劲,勇气,胆量,勇敢,勇猛,英勇,无畏,无惧,热血,激情,燃烧,燃起来,火热,沸腾,雄起,崛起,站起来,全力,全力以赴,竭尽全力,尽力,用力,使劲,猛烈,强大,强劲,厉害,凶猛,凶狠,狠,猛,快,准,狠准快,工作,干活,劳动,建造,制造,生产,研究,学习,交易,贸易,买卖,做生意,谈判,推销,卖,买",
                        Content = INSPIRATION_RULE,
                        Keywords = new[] { "灵感", "启发", "顿悟", "加油", "鼓励", "激励", "状态" },
                        Importance = 1.0f
                    }
                },
                {
                    "expand-action-rest",
                    new RuleDefinition
                    {
                        Id = "expand-action-rest",
                        Tag = "RimTalk-ExpandActions,规则-休息指令,休息,歇息,歇歇,歇会,歇一会,歇一歇,睡觉,睡眠,睡,入睡,安睡,酣睡,沉睡,熟睡,睡一觉,睡会,睡一会,小憩,打盹,午睡,午休,夜休,困,困了,好困,很困,想睡,瞌睡,打瞌睡,昏昏欲睡,睡意,累,累了,好累,很累,疲惫,疲劳,疲倦,疲乏,劳累,乏力,没力气,无力,虚弱,精疲力尽,筋疲力尽,力竭,体力不支,撑不住,扛不住,受不了,顶不住,躺,躺下,卧,卧倒,卧床,倒下,趴下,瘫倒,瘫软,昏迷,昏倒,晕倒,失去意识,不省人事,休养,养伤,疗伤,恢复,调养,养精蓄锐,闭眼,闭上眼,合眼,眯眼,眯一会,养神,打盹儿",
                        Content = REST_RULE,
                        Keywords = new[] { "休息", "睡觉", "昏迷", "好困", "累了", "疲劳", "躺下" },
                        Importance = 1.0f
                    }
                },
                {
                    "expand-action-gift",
                    new RuleDefinition
                    {
                        Id = "expand-action-gift",
                        Tag = "RimTalk-ExpandActions,规则-赠送指令,送,给,赠,送给,给你,给予,赠送,赠予,赠与,交给,递给,拿去,收下,接受,拿着,拿好,收好,留着,要,送你,给你的,你的,属于你,礼物,礼品,礼,赠品,贡品,心意,诚意,一点心意,小小心意,谢礼,回礼,答谢,感谢,报答,见面礼,手信,伴手礼,纪念,纪念品,信物,念想,馈赠,奉送,敬献,进献,献上,呈上,奉上,敬上,上贡,贡献,分享,分,分给,分你,拿,取,拿走,带走,带上,这个,那个,东西,物品,好东西,宝贝,珍品,珍宝,宝物,武器,装备,枪,刀,剑,盾,甲,衣服,食物,吃的,喝的,药,草药,材料,资源,银,钱,金,财宝,补给,用品,工具",
                        Content = GIFT_RULE,
                        Keywords = new[] { "送给", "给你", "拿去", "送去", "赠送", "给", "拿" },
                        Importance = 1.0f
                    }
                },
                {
                    "expand-action-social-dining",
                    new RuleDefinition
                    {
                        Id = "expand-action-social-dining",
                        Tag = "RimTalk-ExpandActions,规则-聚餐指令,吃,吃饭,用餐,就餐,进餐,进食,饮食,吃东西,吃点,吃点东西,吃点啥,吃啥,吃什么,喝,喝酒,饮酒,干杯,碰杯,敬酒,饮,品尝,尝尝,试试,餐,饭,饭菜,菜,美食,佳肴,食物,食品,大餐,盛宴,宴席,宴会,饭局,聚餐,共餐,一起吃,一块吃,同吃,分享,共进,早餐,早饭,午餐,午饭,晚餐,晚饭,正餐,夜宵,宵夜,点心,零食,小吃,甜点,饿,饿了,好饿,很饿,饥饿,饥,肚子饿,想吃,馋,嘴馋,饱,吃饱,饱餐,填饱,充饥,果腹,品,品尝,享用,开吃,开饭,开动,动筷,上菜,请客,款待,宴请,招待,做东,设宴,摆酒,请吃饭,请你吃,我请客,庆祝,庆贺,祝贺,欢庆,欢宴,筵席",
                        Content = SOCIAL_DINING_RULE,
                        Keywords = new[] { "吃饭", "聚餐", "饿了", "分享食物", "吃点东西", "庆祝", "喝一杯", "共进晚餐", "一起吃", "用餐" },
                        Importance = 1.0f
                    }
                },
                {
                    "expand-action-social-relax",
                    new RuleDefinition
                    {
                        Id = "expand-action-social-relax",
                        Tag = "RimTalk-ExpandActions,规则-休闲指令,玩,玩耍,玩乐,玩一玩,玩玩,玩儿,嬉戏,戏耍,娱乐,消遣,取乐,找乐,寻乐,解闷,散心,放松,松弛,放松心情,轻松,休闲,闲暇,空闲,有空,悠闲,惬意,自在,舒服,舒适,享受,快乐,开心,高兴,愉快,欢乐,欢快,喜悦,乐,乐呵,乐一乐,乐子,有意思,聚,聚会,聚一聚,聚聚,相聚,团聚,欢聚,聚集,集会,派对,宴会,活动,社交,交际,交往,互动,交流,沟通,说话,聊天,聊,闲聊,闲话,唠嗑,唠,谈天,谈心,倾诉,倾谈,陪伴,陪,作陪,陪同,伴,相伴,同伴,做伴,游戏,电玩,桌游,棋,下棋,牌,打牌,麻将,扑克,赌博,娱乐活动,文娱,文体,体育,运动,健身,锻炼,散步,走走,逛,逛逛,溜达,转转,遛弯,闲逛,游荡,漫步,无聊,闲着,没事,打发时间,消磨时光,度过,度日,过日子",
                        Content = SOCIAL_RELAX_RULE,
                        Keywords = new[] { "放松", "娱乐", "休闲", "一起玩", "聚会", "社交", "活动", "玩游戏", "聊天", "喝酒" },
                        Importance = 0.9f
                    }
                }
            };
        }
    }

    /// <summary>
    /// 规则定义数据结构
    /// </summary>
    public class RuleDefinition
    {
        public string Id { get; set; }
        public string Tag { get; set; }
        public string Content { get; set; }
        public string[] Keywords { get; set; }
        public float Importance { get; set; }
    }
}
