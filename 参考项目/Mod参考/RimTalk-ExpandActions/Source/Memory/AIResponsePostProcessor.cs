using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;
using RimTalkExpandActions.Memory.Actions;

namespace RimTalkExpandActions.Memory
{
    /// <summary>
    /// AI 回复后处理器，用于解析和执行嵌入在回复文本中的动作指令
    /// </summary>
    public static class AIResponsePostProcessor
    {
        // 基础 JSON 匹配正则表达式
        private static readonly Regex BaseJsonRegex = new Regex(
            @"\{[^}]*""action""\s*:\s*""([^""]+)""[^}]*\}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// 处理 AI 回复文本，检测并执行动作指令
        /// </summary>
        public static string ProcessActionResponse(string responseText, Pawn targetPawn, Pawn recruiter = null)
        {
            if (string.IsNullOrEmpty(responseText))
            {
                return responseText;
            }

            try
            {
                // 1. 检测是否包含动作 JSON
                if (!responseText.Contains("{\"action\"") && !responseText.Contains("{ \"action\""))
                {
                    return responseText;
                }

                // 2. 使用正则表达式匹配 JSON
                Match match = BaseJsonRegex.Match(responseText);
                
                if (match.Success)
                {
                    string jsonBlock = match.Value;
                    string action = ExtractJsonField(jsonBlock, "action");
                    if (action != null)
                    {
                        action = action.ToLower();
                    }

                    if (!string.IsNullOrEmpty(action))
                    {
                        // 3. 捕获变量（避免闭包问题）
                        string capturedAction = action;
                        string capturedJsonBlock = jsonBlock;
                        Pawn capturedTarget = targetPawn;
                        Pawn capturedRecruiter = recruiter;

                        // 4. 延迟到主线程执行（线程安全）
                        if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                        {
                            Log.Message($"[RimTalk-ExpandActions] 检测到动作 '{action}'，将在主线程执行");
                        }

                        LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            try
                            {
                                DispatchAction(capturedAction, capturedJsonBlock, capturedTarget, capturedRecruiter);
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"[RimTalk-ExpandActions] 主线程执行失败: {ex.Message}\n{ex.StackTrace}");
                            }
                        });
                    }

                    // 5. 立即移除 JSON 字符串，返回纯净文本
                    string cleanText = responseText.Replace(jsonBlock, "").Trim();
                    return cleanText;
                }

                return responseText;
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[RimTalk-ExpandActions] ProcessActionResponse 处理失败: {0}\n{1}", ex.Message, ex.StackTrace));
                return responseText;
            }
        }

        /// <summary>
        /// 分发动作到对应的处理器
        /// </summary>
        private static void DispatchAction(string action, string jsonBlock, Pawn targetPawn, Pawn recruiter)
        {
            try
            {
                // 获取设置
                var settings = RimTalkExpandActionsMod.Settings;

                // 检查行为是否启用
                if (settings != null && !settings.IsActionEnabled(action))
                {
                    if (settings.enableDetailedLogging)
                    {
                        Log.Message(string.Format("[RimTalk-ExpandActions] 行为 '{0}' 已禁用，跳过执行", action));
                    }
                    return;
                }

                // 检查成功率
                if (settings != null)
                {
                    float successChance = settings.GetSuccessChance(action);
                    float roll = Rand.Value;

                    if (roll > successChance)
                    {
                        if (settings.enableDetailedLogging)
                        {
                            Log.Message(string.Format("[RimTalk-ExpandActions] 行为 '{0}' 成功率检定失败 ({1:F2} > {2:F2})", action, roll, successChance));
                        }

                        if (settings.showActionMessages)
                        {
                            Messages.Message(
                                string.Format("{0} 的 {1} 尝试失败了...", targetPawn.Name.ToStringShort, GetActionDisplayName(action)),
                                targetPawn,
                                MessageTypeDefOf.RejectInput
                            );
                        }
                        return;
                    }

                    if (settings.enableDetailedLogging)
                    {
                        Log.Message(string.Format("[RimTalk-ExpandActions] 行为 '{0}' 成功率检定通过 ({1:F2} <= {2:F2})", action, roll, successChance));
                    }
                }

                // 执行对应的动作
                switch (action)
                {
                    case "recruit":
                        HandleRecruitAction(jsonBlock, targetPawn, recruiter);
                        break;

                    case "drop_weapon":
                        HandleDropWeaponAction(jsonBlock, targetPawn);
                        break;

                    case "romance":
                        HandleRomanceAction(jsonBlock, targetPawn);
                        break;

                    case "give_inspiration":
                        HandleInspirationAction(jsonBlock, targetPawn);
                        break;

                    case "force_rest":
                        HandleRestAction(jsonBlock, targetPawn);
                        break;

                    case "give_item":
                        HandleGiftAction(jsonBlock, targetPawn);
                        break;

                    case "social_dining":
                        HandleSocialDiningAction(jsonBlock, targetPawn, recruiter);
                        break;

                    case "social_relax":
                        HandleSocialRelaxAction(jsonBlock, targetPawn, recruiter);
                        break;

                    default:
                        Log.Warning(string.Format("[RimTalk-ExpandActions] 未知动作类型: {0}", action));
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[RimTalk-ExpandActions] DispatchAction 失败 ({0}): {1}\n{2}", action, ex.Message, ex.StackTrace));
            }
        }

        /// <summary>
        /// 获取动作的显示名称
        /// </summary>
        private static string GetActionDisplayName(string action)
        {
            if (action == null) return "";
            
            switch (action.ToLower())
            {
                case "recruit":
                    return "招募";
                case "drop_weapon":
                    return "投降";
                case "romance":
                    return "恋爱";
                case "give_inspiration":
                    return "灵感";
                case "force_rest":
                    return "休息";
                case "give_item":
                    return "送礼";
                case "social_dining":
                    return "共餐";
                case "social_relax":
                    return "放松";
                default:
                    return action;
            }
        }

        #region 动作处理器

        private static void HandleRecruitAction(string jsonBlock, Pawn targetPawn, Pawn recruiter)
        {
            string targetName = ExtractJsonField(jsonBlock, "target");
            
            if (ValidateTarget(targetName, targetPawn))
            {
                Log.Message(string.Format("[RimTalk-ExpandActions] 检测到招募指令: {0}", targetPawn.Name.ToStringShort));
                RimTalkActions.ExecuteRecruit(targetPawn, recruiter);
            }
        }

        private static void HandleDropWeaponAction(string jsonBlock, Pawn targetPawn)
        {
            string targetName = ExtractJsonField(jsonBlock, "target");
            
            if (ValidateTarget(targetName, targetPawn))
            {
                Log.Message(string.Format("[RimTalk-ExpandActions] 检测到投降指令: {0}", targetPawn.Name.ToStringShort));
                RimTalkActions.ExecuteDropWeapon(targetPawn);
            }
        }

        private static void HandleRomanceAction(string jsonBlock, Pawn targetPawn)
        {
            string targetName = ExtractJsonField(jsonBlock, "target");
            string partnerName = ExtractJsonField(jsonBlock, "partner");
            string type = ExtractJsonField(jsonBlock, "type");

            if (!ValidateTarget(targetName, targetPawn))
            {
                Log.Warning($"[RimTalk-ExpandActions] 恋爱行为：目标名字不匹配，跳过");
                return;
            }

            // 线程安全修复：不再使用 FindPawnByName，要求必须在 JSON 中明确指定 partner
            if (string.IsNullOrEmpty(partnerName))
            {
                Log.Warning("[RimTalk-ExpandActions] 恋爱行为：未指定 partner 参数");
                return;
            }

            // 简化逻辑：partner 参数应该在调用前就已经解析好
            Log.Warning($"[RimTalk-ExpandActions] 恋爱行为需要两个 Pawn 对象，当前实现不支持从名字查找（线程安全限制）");
            Log.Message($"[RimTalk-ExpandActions] 建议：使用其他方式触发恋爱关系（如通过游戏内互动）");
        }

        private static void HandleInspirationAction(string jsonBlock, Pawn targetPawn)
        {
            string targetName = ExtractJsonField(jsonBlock, "target");
            string type = ExtractJsonField(jsonBlock, "type");

            if (ValidateTarget(targetName, targetPawn))
            {
                Log.Message(string.Format("[RimTalk-ExpandActions] 检测到灵感指令: {0}, 类型: {1}", targetPawn.Name.ToStringShort, type));
                RimTalkActions.ExecuteInspiration(targetPawn, type);
            }
        }

        private static void HandleRestAction(string jsonBlock, Pawn targetPawn)
        {
            string targetName = ExtractJsonField(jsonBlock, "target");
            string immediateStr = ExtractJsonField(jsonBlock, "immediate");
            bool immediate = immediateStr != null && immediateStr.ToLower() == "true";

            if (ValidateTarget(targetName, targetPawn))
            {
                Log.Message(string.Format("[RimTalk-ExpandActions] 检测到休息指令: {0}, 立即: {1}", targetPawn.Name.ToStringShort, immediate));
                RimTalkActions.ExecuteRest(targetPawn, immediate);
            }
        }

        private static void HandleGiftAction(string jsonBlock, Pawn targetPawn)
        {
            string targetName = ExtractJsonField(jsonBlock, "target");
            string itemKeyword = ExtractJsonField(jsonBlock, "item_keyword");

            if (ValidateTarget(targetName, targetPawn) && !string.IsNullOrEmpty(itemKeyword))
            {
                Log.Message(string.Format("[RimTalk-ExpandActions] 检测到送礼指令: {0}, 物品: {1}", targetPawn.Name.ToStringShort, itemKeyword));
                RimTalkActions.ExecuteGift(targetPawn, itemKeyword);
            }
        }

        private static void HandleSocialDiningAction(string jsonBlock, Pawn targetPawn, Pawn recruiter)
        {
            // 线程安全修复：直接使用传入的参数，不再查找 Pawn
            string targetName = ExtractJsonField(jsonBlock, "target");
            
            // 使用已有的参数作为发起者和目标
            Pawn finalInitiator = recruiter ?? targetPawn;
            Pawn finalTarget = targetPawn;
            
            // 如果 JSON 明确指定要交换角色
            string initiatorName = ExtractJsonField(jsonBlock, "initiator");
            if (!string.IsNullOrEmpty(initiatorName))
            {
                // 检查是否要求 targetPawn 作为发起者
                if (ValidateTarget(initiatorName, targetPawn))
                {
                    finalInitiator = targetPawn;
                    finalTarget = recruiter;
                }
            }

            // 最终验证
            if (finalInitiator == null)
            {
                Log.Warning("[RimTalk-ExpandActions] 社交用餐：发起者为空，无法执行");
                return;
            }

            if (finalTarget == null)
            {
                Log.Warning("[RimTalk-ExpandActions] 社交用餐：目标为空，无法执行");
                return;
            }

            // 检查是否为同一个人
            if (finalInitiator == finalTarget)
            {
                Log.Warning($"[RimTalk-ExpandActions] 社交用餐：发起者和目标是同一个人 ({finalInitiator.Name.ToStringShort})，无法执行");
                return;
            }

            Log.Message($"[RimTalk-ExpandActions] 检测到社交用餐指令: {finalInitiator.Name.ToStringShort} 邀请 {finalTarget.Name.ToStringShort}");
            
            RimTalkActions.ExecuteSocialDining(finalInitiator, finalTarget);
        }

        private static void HandleSocialRelaxAction(string jsonBlock, Pawn targetPawn, Pawn recruiter)
        {
            // 线程安全修复：只使用当前已知的 Pawn，不再从名字列表查找
            string targets = ExtractJsonField(jsonBlock, "targets");
            
            Pawn initiator = recruiter ?? targetPawn;
            
            Log.Message($"[RimTalk-ExpandActions] 检测到社交放松指令（简化版）：{initiator.Name.ToStringShort} 开始放松");
            
            // 简化实现：只让发起者自己放松，避免线程安全问题
            RimTalkActions.ExecuteSocialRelax(initiator, new List<Pawn> { initiator });
            
            if (!string.IsNullOrEmpty(targets))
            {
                Log.Warning($"[RimTalk-ExpandActions] 注意：多人放松功能因线程安全问题暂时禁用");
                Log.Warning($"[RimTalk-ExpandActions] 原始目标列表: {targets}（已忽略）");
            }
        }

        #endregion

        #region 辅助方法

        private static string ExtractJsonField(string jsonBlock, string fieldName)
        {
            try
            {
                // 使用正则表达式提取字段值
                string pattern = string.Format(@"""{0}""\s*:\s*""([^""]+)""", fieldName);
                Match match = Regex.Match(jsonBlock, pattern, RegexOptions.IgnoreCase);
                
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }

                // 尝试匹配布尔值或数字（不带引号）
                pattern = string.Format(@"""{0}""\s*:\s*(\w+)", fieldName);
                match = Regex.Match(jsonBlock, pattern, RegexOptions.IgnoreCase);
                
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[RimTalk-ExpandActions] ExtractJsonField 失败 ({0}): {1}", fieldName, ex.Message));
                return null;
            }
        }

        private static bool ValidateTarget(string targetName, Pawn targetPawn)
        {
            try
            {
                if (targetPawn == null || string.IsNullOrEmpty(targetName))
                {
                    return false;
                }

                string shortName = targetPawn.Name != null ? targetPawn.Name.ToStringShort : "";
                string fullName = targetPawn.Name != null ? targetPawn.Name.ToStringFull : "";
                string nickname = "";
                NameTriple nameTriple = targetPawn.Name as NameTriple;
                if (nameTriple != null)
                {
                    nickname = nameTriple.Nick ?? "";
                }

                string normalizedTarget = targetName.ToLower().Replace(" ", "");
                string normalizedShort = shortName.ToLower().Replace(" ", "");
                string normalizedFull = fullName.ToLower().Replace(" ", "");
                string normalizedNick = nickname.ToLower().Replace(" ", "");

                bool isMatch = normalizedTarget.Contains(normalizedShort) ||
                              normalizedTarget.Contains(normalizedNick) ||
                              normalizedShort.Contains(normalizedTarget) ||
                              normalizedNick.Contains(normalizedTarget) ||
                              normalizedFull.Contains(normalizedTarget);

                if (!isMatch)
                {
                    Log.Warning(string.Format("[RimTalk-ExpandActions] 名字不匹配 - JSON目标: '{0}', 实际: '{1}' / '{2}'", targetName, shortName, nickname));
                }

                return isMatch;
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[RimTalk-ExpandActions] ValidateTarget 验证失败: {0}", ex.Message));
                return false;
            }
        }

        // ==========================================
        // 警告：此方法已废弃！
        // 原因：访问 map.mapPawns 违反 RimWorld 线程安全规则
        // 错误信息：Accessing map pawns off main thread
        // 修复方案：使用已传入的 Pawn 参数，不要动态查找
        // ==========================================
        /*
        private static Pawn FindPawnByName(string name)
        {
            // 此方法已被禁用以避免崩溃
            Log.Error("[RimTalk-ExpandActions] FindPawnByName 已废弃，不应该被调用！");
            return null;
        }
        */

        public static bool TryParseActionJson(string json, out string action, out string target)
        {
            action = null;
            target = null;

            try
            {
                var match = BaseJsonRegex.Match(json);
                if (match.Success)
                {
                    action = ExtractJsonField(match.Value, "action");
                    target = ExtractJsonField(match.Value, "target");
                    return !string.IsNullOrEmpty(action);
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[RimTalk-ExpandActions] TryParseActionJson 失败: {0}", ex.Message));
                return false;
            }
        }

        #endregion
    }
}
