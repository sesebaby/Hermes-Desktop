using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Verse;
using RimWorld;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 指令解析器 - 解析 AI 返回的格式化指令
    /// 支持格式: Execute CommandName(参数1, 参数2, ...)
    /// 或 `Execute CommandName(参数1, 参数2)`
    /// </summary>
    public static class CommandParser
    {
        /// <summary>
        /// 解析结果
        /// </summary>
        public class ParseResult
        {
            public bool Success { get; set; }
            public string CommandName { get; set; }
            public List<string> Arguments { get; set; } = new List<string>();
            public string RawCommand { get; set; }
        }

        // 匹配 Execute 指令的正则表达式
        // 支持: Execute CommandName(arg1, arg2) 或 `Execute CommandName(arg1, arg2)`
        private static readonly Regex CommandPattern = new Regex(
            @"`?Execute\s+(\w+)\s*\(\s*([^)]*)\s*\)`?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// 尝试从文本中解析指令
        /// </summary>
        /// <param name="text">包含指令的文本</param>
        /// <returns>解析结果</returns>
        public static ParseResult TryParse(string text)
        {
            var result = new ParseResult { Success = false };

            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return result;
                }

                Log.Message($"[RimTalk-ExpandActions] CommandParser: 尝试解析文本: {text}");

                Match match = CommandPattern.Match(text);

                if (!match.Success)
                {
                    Log.Message("[RimTalk-ExpandActions] CommandParser: 未找到 Execute 指令格式");
                    return result;
                }

                result.Success = true;
                result.RawCommand = match.Value;
                result.CommandName = match.Groups[1].Value.Trim();
                
                // 解析参数
                string argsString = match.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(argsString))
                {
                    // 使用更智能的参数分割（处理引号内的逗号）
                    result.Arguments = ParseArguments(argsString);
                }

                Log.Message($"[RimTalk-ExpandActions] CommandParser: ✓ 成功解析指令");
                Log.Message($"[RimTalk-ExpandActions]   命令: {result.CommandName}");
                Log.Message($"[RimTalk-ExpandActions]   参数: [{string.Join(", ", result.Arguments)}]");

                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] CommandParser.TryParse 失败: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// 解析参数字符串，处理引号和逗号
        /// </summary>
        private static List<string> ParseArguments(string argsString)
        {
            var args = new List<string>();
            var currentArg = "";
            bool inQuotes = false;
            char quoteChar = '"';

            for (int i = 0; i < argsString.Length; i++)
            {
                char c = argsString[i];

                // 处理引号
                if ((c == '"' || c == '\'') && (i == 0 || argsString[i - 1] != '\\'))
                {
                    if (!inQuotes)
                    {
                        inQuotes = true;
                        quoteChar = c;
                    }
                    else if (c == quoteChar)
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        currentArg += c;
                    }
                    continue;
                }

                // 处理逗号分隔符
                if (c == ',' && !inQuotes)
                {
                    args.Add(currentArg.Trim());
                    currentArg = "";
                    continue;
                }

                currentArg += c;
            }

            // 添加最后一个参数
            if (!string.IsNullOrWhiteSpace(currentArg))
            {
                args.Add(currentArg.Trim());
            }

            return args;
        }

        /// <summary>
        /// 执行解析出的指令
        /// </summary>
        /// <param name="parseResult">解析结果</param>
        /// <param name="speaker">说话者</param>
        /// <param name="listener">听众</param>
        /// <returns>是否成功执行</returns>
        public static bool ExecuteCommand(ParseResult parseResult, Pawn speaker, Pawn listener)
        {
            if (!parseResult.Success || string.IsNullOrEmpty(parseResult.CommandName))
            {
                return false;
            }

            try
            {
                Log.Message($"[RimTalk-ExpandActions] CommandParser: 执行指令 {parseResult.CommandName}");

                string command = parseResult.CommandName.ToLower();
                var args = parseResult.Arguments;

                switch (command)
                {
                    case "joincolony":
                        return ExecuteJoinColony(speaker, listener, args);

                    case "recruit":
                        return ExecuteRecruit(speaker, listener, args);

                    case "romance":
                    case "acceptromance":
                        return ExecuteRomance(speaker, listener, args);

                    case "breakup":
                        return ExecuteBreakup(speaker, listener, args);

                    case "rest":
                    case "sleep":
                        return ExecuteRest(speaker, args);

                    case "inspire":
                    case "inspiration":
                        return ExecuteInspire(speaker, args);

                    case "gift":
                    case "giveitem":
                        return ExecuteGift(speaker, listener, args);

                    case "socialdining":
                    case "eattogether":
                        return ExecuteSocialDining(speaker, listener, args);

                    case "socialrelax":
                    case "relax":
                        return ExecuteSocialRelax(speaker, listener, args);

                    default:
                        Log.Warning($"[RimTalk-ExpandActions] CommandParser: 未知指令 '{command}'");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] CommandParser.ExecuteCommand 失败: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        #region 指令执行方法

        /// <summary>
        /// 执行加入殖民地指令
        /// </summary>
        private static bool ExecuteJoinColony(Pawn speaker, Pawn listener, List<string> args)
        {
            try
            {
                Log.Message($"[RimTalk-ExpandActions] ✓ 执行 JoinColony 指令");

                // 参数可能包含角色名和殖民地名
                string pawnName = args.Count > 0 ? args[0] : null;
                string colonyName = args.Count > 1 ? args[1] : null;

                Log.Message($"[RimTalk-ExpandActions]   角色: {pawnName ?? speaker?.Name?.ToStringShort ?? "未知"}");
                Log.Message($"[RimTalk-ExpandActions]   殖民地: {colonyName ?? "玩家殖民地"}");

                // 确定要招募的角色
                Pawn pawnToRecruit = speaker;
                
                // 如果提供了角色名，尝试查找该角色
                if (!string.IsNullOrEmpty(pawnName) && speaker != null && 
                    !speaker.Name.ToStringShort.Contains(pawnName) && 
                    !pawnName.Contains(speaker.Name.ToStringShort))
                {
                    Pawn foundPawn = FindPawnByName(pawnName, speaker.Map);
                    if (foundPawn != null)
                    {
                        pawnToRecruit = foundPawn;
                    }
                }

                if (pawnToRecruit == null)
                {
                    Log.Error("[RimTalk-ExpandActions] JoinColony: 未找到要招募的角色");
                    return false;
                }

                // 使用已有的招募逻辑
                ActionExecutor.Execute("recruit_agree", pawnToRecruit, listener);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteJoinColony 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行招募指令
        /// </summary>
        private static bool ExecuteRecruit(Pawn speaker, Pawn listener, List<string> args)
        {
            ActionExecutor.Execute("recruit_agree", speaker, listener);
            return true;
        }

        /// <summary>
        /// 执行浪漫关系指令
        /// </summary>
        private static bool ExecuteRomance(Pawn speaker, Pawn listener, List<string> args)
        {
            ActionExecutor.Execute("romance_accept", speaker, listener);
            return true;
        }

        /// <summary>
        /// 执行分手指令
        /// </summary>
        private static bool ExecuteBreakup(Pawn speaker, Pawn listener, List<string> args)
        {
            ActionExecutor.Execute("romance_breakup", speaker, listener);
            return true;
        }

        /// <summary>
        /// 执行休息指令
        /// </summary>
        private static bool ExecuteRest(Pawn speaker, List<string> args)
        {
            ActionExecutor.Execute("force_rest", speaker, null);
            return true;
        }

        /// <summary>
        /// 执行灵感指令
        /// </summary>
        private static bool ExecuteInspire(Pawn speaker, List<string> args)
        {
            string type = args.Count > 0 ? args[0].ToLower() : "work";
            
            if (type.Contains("fight") || type.Contains("combat") || type.Contains("战斗"))
            {
                ActionExecutor.Execute("inspire_fight", speaker, null);
            }
            else
            {
                ActionExecutor.Execute("inspire_work", speaker, null);
            }
            return true;
        }

        /// <summary>
        /// 执行赠送物品指令
        /// </summary>
        private static bool ExecuteGift(Pawn speaker, Pawn listener, List<string> args)
        {
            ActionExecutor.Execute("give_item", speaker, listener);
            return true;
        }

        /// <summary>
        /// 执行社交聚餐指令
        /// </summary>
        private static bool ExecuteSocialDining(Pawn speaker, Pawn listener, List<string> args)
        {
            ActionExecutor.Execute("social_dining", speaker, listener);
            return true;
        }

        /// <summary>
        /// 执行社交休闲指令
        /// </summary>
        private static bool ExecuteSocialRelax(Pawn speaker, Pawn listener, List<string> args)
        {
            ActionExecutor.Execute("social_relax", speaker, listener);
            return true;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 根据名字查找 Pawn
        /// </summary>
        private static Pawn FindPawnByName(string name, Map map)
        {
            if (string.IsNullOrWhiteSpace(name) || map == null)
            {
                return null;
            }

            try
            {
                string normalizedName = name.ToLower().Replace(" ", "");

                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn?.Name == null) continue;

                    string shortName = pawn.Name.ToStringShort?.ToLower().Replace(" ", "") ?? "";
                    
                    if (shortName.Contains(normalizedName) || normalizedName.Contains(shortName))
                    {
                        return pawn;
                    }

                    // 检查昵称
                    if (pawn.Name is NameTriple nameTriple && nameTriple.Nick != null)
                    {
                        string nickname = nameTriple.Nick.ToLower().Replace(" ", "");
                        if (nickname.Contains(normalizedName) || normalizedName.Contains(nickname))
                        {
                            return pawn;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] FindPawnByName 失败: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}
