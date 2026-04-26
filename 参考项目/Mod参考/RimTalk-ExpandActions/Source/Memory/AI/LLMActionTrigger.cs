using System;
using RimWorld;
using Verse;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// LLM 行为触发器
    /// 基于解析的标签触发对应的游戏行为
    /// v2.4 新增功能
    /// </summary>
    public static class LLMActionTrigger
    {
        /// <summary>
        /// 触发结果
        /// </summary>
        public class TriggerResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public string IntentName { get; set; }
            public string TagValue { get; set; }
        }

        /// <summary>
        /// 基于解析结果触发行为
        /// </summary>
        /// <param name="parseResult">标签解析结果</param>
        /// <param name="speaker">说话者（NPC）</param>
        /// <param name="listener">收听者（玩家殖民者）</param>
        /// <returns>触发结果</returns>
        public static TriggerResult TriggerAction(
            LLMTagParser.ParseResult parseResult,
            Pawn speaker,
            Pawn listener)
        {
            if (parseResult == null || !parseResult.Success)
            {
                return new TriggerResult
                {
                    Success = false,
                    Message = "无效的解析结果"
                };
            }

            // NONE 标签表示不执行任何操作
            if (parseResult.TagValue == "NONE")
            {
                return new TriggerResult
                {
                    Success = true,
                    Message = "AI 选择不执行操作（NONE）",
                    TagValue = "NONE"
                };
            }

            // 验证标签有效性
            if (!LLMTagParser.IsValidTagValue(parseResult.TagType, parseResult.TagValue))
            {
                return new TriggerResult
                {
                    Success = false,
                    Message = $"无效的标签值: {parseResult.TagType}: {parseResult.TagValue}"
                };
            }

            // 映射到 ActionExecutor 的 intent 名称
            string intentName = TagValueToExecutorIntent(parseResult.TagValue);
            
            if (string.IsNullOrEmpty(intentName))
            {
                return new TriggerResult
                {
                    Success = false,
                    Message = $"无法映射标签到执行器意图: {parseResult.TagValue}"
                };
            }

            // 记录触发信息
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message($"[LLMActionTrigger] 解析到标签: {parseResult.OriginalTag}");
                Log.Message($"[LLMActionTrigger] 映射到执行器意图: {intentName}");
                Log.Message($"[LLMActionTrigger] 说话者: {speaker?.LabelShort}, 收听者: {listener?.LabelShort}");
            }

            // 使用现有的 ActionExecutor 执行行为
            try
            {
                ActionExecutor.Execute(intentName, speaker, listener);
                
                return new TriggerResult
                {
                    Success = true,
                    Message = $"成功触发行为: {intentName}",
                    IntentName = intentName,
                    TagValue = parseResult.TagValue
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[LLMActionTrigger] 执行行为时发生异常: {ex.Message}");
                return new TriggerResult
                {
                    Success = false,
                    Message = $"执行行为失败: {ex.Message}",
                    IntentName = intentName,
                    TagValue = parseResult.TagValue
                };
            }
        }

        /// <summary>
        /// 将标签值映射到 ActionExecutor 的意图名称
        /// </summary>
        private static string TagValueToExecutorIntent(string tagValue)
        {
            if (string.IsNullOrEmpty(tagValue))
            {
                return null;
            }

            tagValue = tagValue.ToUpper();

            // 映射标签到 ActionExecutor 使用的 intent 名称
            switch (tagValue)
            {
                case "RECRUIT":
                    return "recruit_agree";
                
                case "SURRENDER":
                    return "drop_weapon"; // 注意：ActionExecutor中可能没有这个，需要验证
                
                case "LOVE":
                    return "romance_accept";
                
                case "BREAKUP":
                    return "romance_breakup";
                
                case "INSPIRE_BATTLE":
                    return "inspire_fight";
                
                case "INSPIRE_WORK":
                    return "inspire_work";
                
                case "REST":
                    return "force_rest";
                
                case "GIFT":
                    return "give_item";
                
                case "DINE":
                    return "social_dining";
                
                case "RELAX":
                    return "social_relax";
                
                case "NONE":
                    return null;
                
                default:
                    Log.Warning($"[LLMActionTrigger] 未知的标签值: {tagValue}");
                    return null;
            }
        }
    }
}
