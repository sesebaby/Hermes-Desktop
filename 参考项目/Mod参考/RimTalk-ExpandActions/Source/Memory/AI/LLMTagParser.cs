using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Verse;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// LLM 输出标签解析器
    /// 解析 AI 回复中的行为标签，如 <Action>RECRUIT</Action> 或 <Decision>LOVE</Decision>
    /// v2.5 更新：使用 XML 格式标签
    /// </summary>
    public static class LLMTagParser
    {
        /// <summary>
        /// 标签解析结果
        /// </summary>
        public class ParseResult
        {
            public bool Success { get; set; }
            public string TagType { get; set; }      // Action 或 Decision
            public string TagValue { get; set; }     // RECRUIT, LOVE, SURRENDER 等
            public string OriginalTag { get; set; }  // 完整标签 <Action>RECRUIT</Action>
            public string CleanedText { get; set; }  // 移除标签后的文本
        }

        // XML 格式标签正则表达式：匹配 <Action>XXX</Action> 或 <Decision>XXX</Decision>
        // 使用更宽松的匹配，支持大小写混合和不同的闭合标签大小写
        private static readonly Regex TagRegex = new Regex(
            @"<(?<type>Action|Decision)>\s*(?<value>[A-Za-z_]+)\s*</(?:Action|Decision)>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// 解析文本中的标签
        /// </summary>
        /// <param name="text">AI 输出的文本</param>
        /// <returns>解析结果，如果没有找到标签则 Success = false</returns>
        public static ParseResult Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new ParseResult { Success = false };
            }

            var match = TagRegex.Match(text);
            
            if (!match.Success)
            {
                return new ParseResult 
                { 
                    Success = false,
                    CleanedText = text
                };
            }

            var result = new ParseResult
            {
                Success = true,
                TagType = match.Groups["type"].Value.ToUpper(),
                TagValue = match.Groups["value"].Value.ToUpper(),
                OriginalTag = match.Value,
                CleanedText = TagRegex.Replace(text, "").Trim()
            };

            return result;
        }

        /// <summary>
        /// 解析文本中的所有标签（支持多个标签）
        /// </summary>
        public static List<ParseResult> ParseAll(string text)
        {
            var results = new List<ParseResult>();
            
            if (string.IsNullOrEmpty(text))
            {
                return results;
            }

            var matches = TagRegex.Matches(text);
            
            foreach (Match match in matches)
            {
                results.Add(new ParseResult
                {
                    Success = true,
                    TagType = match.Groups["type"].Value.ToUpper(),
                    TagValue = match.Groups["value"].Value.ToUpper(),
                    OriginalTag = match.Value
                });
            }

            return results;
        }

        /// <summary>
        /// 移除文本中的所有标签
        /// </summary>
        public static string RemoveTags(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            return TagRegex.Replace(text, "").Trim();
        }

        /// <summary>
        /// 检查文本是否包含标签
        /// </summary>
        public static bool ContainsTag(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return TagRegex.IsMatch(text);
        }

        /// <summary>
        /// 验证标签值是否有效
        /// </summary>
        public static bool IsValidTagValue(string tagType, string tagValue)
        {
            if (string.IsNullOrEmpty(tagType) || string.IsNullOrEmpty(tagValue))
            {
                return false;
            }

            tagType = tagType.ToUpper();
            tagValue = tagValue.ToUpper();

            // 定义有效的标签值
            var validValues = new Dictionary<string, HashSet<string>>
            {
                {
                    "ACTION", new HashSet<string>
                    {
                        "RECRUIT", "SURRENDER", "LOVE", "BREAKUP",
                        "INSPIRE_BATTLE", "INSPIRE_WORK", "REST",
                        "GIFT", "DINE", "RELAX", "NONE"
                    }
                },
                {
                    "DECISION", new HashSet<string>
                    {
                        "RECRUIT", "SURRENDER", "LOVE", "BREAKUP",
                        "INSPIRE_BATTLE", "INSPIRE_WORK", "REST",
                        "GIFT", "DINE", "RELAX", "NONE"
                    }
                }
            };

            if (!validValues.ContainsKey(tagType))
            {
                return false;
            }

            return validValues[tagType].Contains(tagValue);
        }

        /// <summary>
        /// 获取标签对应的意图名称（用于映射到 SemanticIntentRecognizer）
        /// </summary>
        public static string TagValueToIntentName(string tagValue)
        {
            if (string.IsNullOrEmpty(tagValue))
            {
                return null;
            }

            tagValue = tagValue.ToUpper();

            // 映射标签到意图名称
            var mapping = new Dictionary<string, string>
            {
                { "RECRUIT", "expand-action-recruit" },
                { "SURRENDER", "expand-action-drop-weapon" },
                { "LOVE", "expand-action-romance" },
                { "BREAKUP", "expand-action-romance" },
                { "INSPIRE_BATTLE", "expand-action-inspiration" },
                { "INSPIRE_WORK", "expand-action-inspiration" },
                { "REST", "expand-action-rest" },
                { "GIFT", "expand-action-gift" },
                { "DINE", "expand-action-social-dining" },
                { "RELAX", "expand-action-social-relax" },
                { "NONE", null }
            };

            return mapping.ContainsKey(tagValue) ? mapping[tagValue] : null;
        }
    }
}
