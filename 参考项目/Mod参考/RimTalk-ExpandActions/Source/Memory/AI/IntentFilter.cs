using System;
using System.Collections.Generic;
using Verse;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 意图过滤器（关键词预筛选）
    /// 在调用 API 之前快速判断文本是否可能包含意图
    /// </summary>
    public static class IntentFilter
    {
        /// <summary>
        /// 敏感关键词列表（中英文）
        /// 包含这些词的文本才会进入语义分析
        /// </summary>
        private static readonly string[] SensitiveKeywords = new[]
        {
            // 招募相关
            "加入", "投降", "同意", "接受", "愿意", "好的", "可以", "跟你们走",
            "join", "surrender", "agree", "accept", "yes", "okay", "ok", "follow",
            
            // 恋爱相关
            "喜欢", "爱", "在一起", "喜爱", "恋", "表白", "喜欢你", "爱你",
            "love", "like", "together", "romance", "relationship", "dating",
            
            // 分手相关
            "分手", "分开", "不爱", "结束", "分离", "离开你",
            "breakup", "break up", "split", "separate", "end relationship",
            
            // 休息相关
            "休息", "睡觉", "累了", "疲惫", "困", "想睡", "去睡",
            "rest", "sleep", "tired", "exhausted", "sleepy", "need sleep",
            
            // 战斗灵感
            "战斗", "打架", "杀敌", "冲锋", "作战", "战意", "勇气",
            "fight", "battle", "combat", "attack", "charge", "warrior",
            
            // 工作灵感
            "工作", "干活", "劳动", "努力", "效率", "干劲",
            "work", "labor", "productive", "efficient", "motivated",
            
            // 赠送物品
            "送给", "给你", "礼物", "赠送", "拿去", "给予",
            "give", "gift", "present", "offer", "take this",
            
            // 社交聚餐
            "一起吃", "吃饭", "聚餐", "共餐", "吃东西",
            "eat together", "dine", "meal", "dinner", "lunch",
            
            // 社交娱乐
            "玩", "娱乐", "聊天", "放松", "休闲",
            "play", "relax", "chat", "entertainment", "fun"
        };

        /// <summary>
        /// 判断文本是否需要进行语义分析
        /// </summary>
        /// <param name="text">待分析的文本</param>
        /// <returns>true 表示应该调用 API 分析，false 表示可以跳过</returns>
        public static bool ShouldAnalyze(string text)
        {
            try
            {
                Log.Message($"[RimTalk-ExpandActions] IntentFilter: 检查文本 '{text}'");
                
                // 空文本直接跳过
                if (string.IsNullOrWhiteSpace(text))
                {
                    Log.Message("[RimTalk-ExpandActions] IntentFilter: 文本为空，返回 false");
                    return false;
                }

                // 转换为小写以便比较
                string lowerText = text.ToLower();
                Log.Message($"[RimTalk-ExpandActions] IntentFilter: 小写文本 '{lowerText}'");

                // 遍历敏感词，检查是否包含
                foreach (string keyword in SensitiveKeywords)
                {
                    string lowerKeyword = keyword.ToLower();
                    if (lowerText.Contains(lowerKeyword))
                    {
                        Log.Message($"[RimTalk-ExpandActions] IntentFilter: ? 匹配到关键词 '{keyword}'，返回 true");
                        return true;
                    }
                }

                Log.Message("[RimTalk-ExpandActions] IntentFilter: ? 未匹配到任何关键词，返回 false");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] IntentFilter.ShouldAnalyze 失败: {ex.Message}");
                // 出错时默认不分析，避免影响游戏流程
                return false;
            }
        }
    }
}
