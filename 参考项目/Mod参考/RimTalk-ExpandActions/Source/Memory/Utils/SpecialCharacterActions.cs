using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RimTalkExpandActions.Memory.Utils
{
    /// <summary>
    /// 特殊字符动作映射
    /// 使用 <Decision>XXX</Decision> 格式的 XML 标签指令
    /// v2.5 更新：使用 XML 格式标签
    /// </summary>
    public static class SpecialCharacterActions
    {
        /// <summary>
        /// 动作名称到动作类型的映射
        /// </summary>
        private static readonly Dictionary<string, string> ActionNameToType = new Dictionary<string, string>
        {
            { "RECRUIT", "recruit" },
            { "SURRENDER", "drop_weapon" },
            { "LOVE", "romance_new" },
            { "BREAKUP", "romance_breakup" },
            { "REST", "force_rest" },
            { "INSPIRE_BATTLE", "inspire_fight" },
            { "INSPIRE_WORK", "inspire_work" },
            { "GIFT", "give_item" },
            { "DINE", "social_dining" },
            { "RELAX", "social_relax" },
            { "NONE", "none" }  // 明确拒绝或无关
        };

        /// <summary>
        /// 动作信息
        /// </summary>
        public class ActionInfo
        {
            public string ActionType { get; }
            public string DisplayName { get; }

            public ActionInfo(string actionType, string displayName)
            {
                ActionType = actionType;
                DisplayName = displayName;
            }
        }

        /// <summary>
        /// 尝试从文本中提取 <Decision>XXX</Decision> 格式的 XML 标签指令
        /// </summary>
        public static bool TryExtractAction(string text, out ActionInfo actionInfo, out string foundCommand)
        {
            actionInfo = null;
            foundCommand = null;

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // 正则表达式匹配 <Decision>XXX</Decision> XML 格式
            string pattern = @"<Decision>\s*(\w+)\s*</Decision>";
            
            Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                string actionName = match.Groups[1].Value.ToUpper();
                
                // 检查是否是有效的动作
                if (ActionNameToType.ContainsKey(actionName))
                {
                    string actionType = ActionNameToType[actionName];
                    
                    // 如果是 "none" 动作，直接返回 false（不触发任何行为）
                    if (actionType == "none")
                    {
                        return false;
                    }
                    
                    actionInfo = new ActionInfo(actionType, GetDisplayName(actionType));
                    foundCommand = match.Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 从文本中移除指令（只移除末尾的）
        /// </summary>
        public static string RemoveCommand(string text, string command)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(command))
            {
                return text;
            }

            int lastIndex = text.LastIndexOf(command);
            if (lastIndex >= 0)
            {
                return text.Substring(0, lastIndex).TrimEnd();
            }

            return text;
        }

        /// <summary>
        /// 检查文本中是否包含 <Decision>...</Decision> 格式的 XML 标签指令
        /// </summary>
        public static bool ContainsAnyCommand(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return text.Contains("<Decision>") || text.Contains("<decision>");
        }

        /// <summary>
        /// 获取动作的显示名称
        /// </summary>
        private static string GetDisplayName(string actionType)
        {
            switch (actionType)
            {
                case "recruit":
                    return "招募";
                case "drop_weapon":
                    return "投降";
                case "romance_new":
                    return "表白";
                case "romance_breakup":
                    return "分手";
                case "force_rest":
                    return "休息";
                case "inspire_fight":
                    return "战斗灵感";
                case "inspire_work":
                    return "工作灵感";
                case "give_item":
                    return "送礼";
                case "social_dining":
                    return "聚餐";
                case "social_relax":
                    return "放松";
                default:
                    return actionType;
            }
        }

        /// <summary>
        /// 获取所有动作列表（用于展示）
        /// </summary>
        public static Dictionary<string, ActionInfo> GetAllActions()
        {
            var actions = new Dictionary<string, ActionInfo>();
            
            foreach (var kvp in ActionNameToType)
            {
                if (kvp.Key != "NONE")
                {
                    actions[kvp.Key] = new ActionInfo(kvp.Value, GetDisplayName(kvp.Value));
                }
            }
            
            return actions;
        }
    }
}
