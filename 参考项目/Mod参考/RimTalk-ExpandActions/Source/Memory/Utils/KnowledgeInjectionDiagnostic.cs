using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace RimTalkExpandActions.Memory.Utils
{
    /// <summary>
    /// 常识库注入诊断工具 v2.0
    /// 帮助排查常识库无法注入的问题
    /// 
    /// 使用方式：
    /// 1. 在游戏中打开开发者模式
    /// 2. 调用 KnowledgeInjectionDiagnostic.RunDiagnostic() 检查状态
    /// 3. 调用 KnowledgeInjectionDiagnostic.TestAPIConnection() 测试 API 连接
    /// 
    /// v2.0 更新：
    /// - 支持新版 CommonKnowledgeLibrary API
    /// - 改进诊断输出格式
    /// - 添加详细的版本检测
    /// </summary>
    public static class KnowledgeInjectionDiagnostic
    {
        /// <summary>
        /// 运行完整诊断并输出报告
        /// </summary>
        public static string RunDiagnostic()
        {
            var sb = new StringBuilder();
            sb.AppendLine("====== RimTalk-ExpandActions 常识库注入诊断 v2.0 ======");
            sb.AppendLine($"诊断时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 1. 检查游戏状态
            sb.AppendLine("=== 1. 游戏状态检查 ===");
            sb.AppendLine($"Current.Game: {(Current.Game != null ? "✓ OK" : "✗ NULL - 需要加载存档")}");
            sb.AppendLine($"Find.World: {(Find.World != null ? "✓ OK" : "✗ NULL - 需要加载存档")}");
            sb.AppendLine();

            // 2. 检查 CommonKnowledgeLibrary 是否可用
            sb.AppendLine("=== 2. CommonKnowledgeLibrary 可用性检查 ===");
            var apiCheckResult = CheckCommonKnowledgeAPI();
            sb.AppendLine(apiCheckResult);
            sb.AppendLine();

            // 3. 检查常识库条目数量
            sb.AppendLine("=== 3. 常识库条目检查 ===");
            var entriesCheckResult = CheckKnowledgeEntries();
            sb.AppendLine(entriesCheckResult);
            sb.AppendLine();

            // 4. 检查 ExpandActions 规则定义
            sb.AppendLine("=== 4. ExpandActions 规则定义检查 ===");
            var rulesCheckResult = CheckBehaviorRules();
            sb.AppendLine(rulesCheckResult);
            sb.AppendLine();

            // 5. 检查启用的行为
            sb.AppendLine("=== 5. 启用的行为检查 ===");
            var enabledCheckResult = CheckEnabledBehaviors();
            sb.AppendLine(enabledCheckResult);
            sb.AppendLine();

            // 6. 输出建议
            sb.AppendLine("=== 6. 诊断建议 ===");
            var suggestions = GetSuggestions();
            sb.AppendLine(suggestions);
            sb.AppendLine();

            sb.AppendLine("====== 诊断完成 ======");

            string report = sb.ToString();
            Log.Message(report);
            return report;
        }

        /// <summary>
        /// 检查 CommonKnowledgeLibrary 是否可用
        /// </summary>
        private static string CheckCommonKnowledgeAPI()
        {
            var sb = new StringBuilder();

            try
            {
                // 检查 Library 类型是否存在
                var libraryType = Type.GetType("RimTalk.Memory.CommonKnowledgeLibrary, RimTalkMemoryPatch");
                sb.AppendLine($"CommonKnowledgeLibrary 类型: {(libraryType != null ? "✓ 找到" : "✗ 未找到")}");

                if (libraryType == null)
                {
                    sb.AppendLine("  ⚠ 请确保 RimTalk-ExpandMemory Mod 已启用并正确加载");
                    return sb.ToString();
                }

                // 列出所有公共静态方法
                var methods = libraryType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                sb.AppendLine($"可用方法数量: {methods.Length}");
                
                // 检查关键方法（v4.0）
                var v4Methods = new[] { "AddKnowledgeEx", "RemoveByIdPrefix", "GetAllKnowledge", "GetStats" };
                bool hasV4Methods = false;
                
                foreach (var methodName in v4Methods)
                {
                    var found = methods.Any(m => m.Name == methodName);
                    sb.AppendLine($"  - {methodName}: {(found ? "✓ 存在" : "✗ 缺失")}");
                    if (found && methodName == "AddKnowledgeEx") hasV4Methods = true;
                }
                
                // 检查旧版方法
                if (!hasV4Methods)
                {
                    var v3Methods = new[] { "AddKnowledge", "RemoveKnowledge" };
                    sb.AppendLine("  检查旧版 API:");
                    foreach (var methodName in v3Methods)
                    {
                        var found = methods.Any(m => m.Name == methodName);
                        sb.AppendLine($"  - {methodName}: {(found ? "✓ 存在 (兼容模式)" : "✗ 缺失")}");
                    }
                }

                // 检查 KeywordMatchMode 枚举
                var matchModeType = Type.GetType("RimTalk.Memory.KeywordMatchMode, RimTalkMemoryPatch");
                sb.AppendLine($"KeywordMatchMode 枚举: {(matchModeType != null ? "✓ 找到" : "✗ 未找到")}");
                
                if (hasV4Methods)
                {
                    sb.AppendLine("✓ API 版本: v4.0+ (最新版)");
                }
                else
                {
                    sb.AppendLine("⚠ API 版本: v3.x (兼容模式)");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"✗ 检查异常: {ex.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 检查常识库条目数量
        /// </summary>
        private static string CheckKnowledgeEntries()
        {
            var sb = new StringBuilder();

            try
            {
                var libraryType = Type.GetType("RimTalk.Memory.CommonKnowledgeLibrary, RimTalkMemoryPatch");
                if (libraryType == null)
                {
                    sb.AppendLine("✗ Library 类型未找到，无法检查条目");
                    return sb.ToString();
                }

                // 调用 GetStats
                var getStatsMethod = libraryType.GetMethod("GetStats", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (getStatsMethod != null)
                {
                    var stats = getStatsMethod.Invoke(null, null);
                    if (stats != null)
                    {
                        var statsType = stats.GetType();
                        var totalCountField = statsType.GetField("TotalCount");
                        var enabledCountField = statsType.GetField("EnabledCount");
                        
                        int totalCount = totalCountField != null ? (int)totalCountField.GetValue(stats) : -1;
                        int enabledCount = enabledCountField != null ? (int)enabledCountField.GetValue(stats) : -1;
                        
                        sb.AppendLine($"常识库总条目: {totalCount}");
                        sb.AppendLine($"启用的条目: {enabledCount}");
                        
                        if (totalCount == 0)
                        {
                            sb.AppendLine("⚠ 常识库为空！请调用 ExpandMemoryKnowledgeInjector.ManualInject() 注入规则");
                        }
                        else if (totalCount > 0)
                        {
                            sb.AppendLine("✓ 常识库已包含条目");
                        }
                    }
                    else
                    {
                        sb.AppendLine("⚠ GetStats 返回 null");
                    }
                }
                else
                {
                    sb.AppendLine("⚠ GetStats 方法未找到");
                }

                // 尝试获取所有条目
                var getAllMethod = libraryType.GetMethod("GetAllKnowledge", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (getAllMethod != null)
                {
                    var allEntries = getAllMethod.Invoke(null, null);
                    if (allEntries != null)
                    {
                        var list = allEntries as System.Collections.IList;
                        if (list != null)
                        {
                            sb.AppendLine($"GetAllKnowledge 返回: {list.Count} 条");
                            
                            // 列出前5个条目的标签
                            if (list.Count > 0)
                            {
                                sb.AppendLine("前5个条目标签:");
                                int count = 0;
                                foreach (var entry in list)
                                {
                                    if (count >= 5) break;
                                    string tag = null;
                                    
                                    var tagField = entry.GetType().GetField("tag");
                                    if (tagField != null)
                                    {
                                        tag = tagField.GetValue(entry) as string;
                                    }
                                    else
                                    {
                                        var tagProperty = entry.GetType().GetProperty("tag");
                                        if (tagProperty != null)
                                        {
                                            tag = tagProperty.GetValue(entry) as string;
                                        }
                                    }
                                    
                                    // 截取标签前50个字符
                                    if (tag != null && tag.Length > 50)
                                    {
                                        tag = tag.Substring(0, 50) + "...";
                                    }
                                    
                                    sb.AppendLine($"  {count + 1}. [{tag ?? "未知"}]");
                                    count++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"✗ 检查异常: {ex.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 检查行为规则定义
        /// </summary>
        private static string CheckBehaviorRules()
        {
            var sb = new StringBuilder();

            try
            {
                var allRules = BehaviorRuleContents.GetAllRules();
                sb.AppendLine($"已定义的规则数量: {allRules.Count}");
                
                foreach (var kvp in allRules)
                {
                    var rule = kvp.Value;
                    string tagPreview = rule.Tag?.Substring(0, Math.Min(50, rule.Tag?.Length ?? 0)) ?? "无";
                    sb.AppendLine($"  • {kvp.Key}:");
                    sb.AppendLine($"    ID: {rule.Id}");
                    sb.AppendLine($"    标签预览: [{tagPreview}...]");
                    sb.AppendLine($"    重要性: {rule.Importance}");
                    sb.AppendLine($"    关键词数: {rule.Keywords?.Length ?? 0}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"✗ 检查异常: {ex.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 检查启用的行为
        /// </summary>
        private static string CheckEnabledBehaviors()
        {
            var sb = new StringBuilder();

            try
            {
                var enabledBehaviors = ExpandMemoryKnowledgeInjector.EnabledBehaviors;
                sb.AppendLine($"启用的行为数量: {enabledBehaviors.Count}");
                
                if (enabledBehaviors.Count > 0)
                {
                    foreach (var behaviorId in enabledBehaviors)
                    {
                        sb.AppendLine($"  ✓ {behaviorId}");
                    }
                }
                else
                {
                    sb.AppendLine("  ⚠ 没有启用任何行为！");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"✗ 检查异常: {ex.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 生成诊断建议
        /// </summary>
        private static string GetSuggestions()
        {
            var sb = new StringBuilder();
            var issues = new List<string>();

            // 检查游戏状态
            if (Current.Game == null || Find.World == null)
            {
                issues.Add("请先加载一个游戏存档");
            }

            // 检查 API
            var libraryType = Type.GetType("RimTalk.Memory.CommonKnowledgeLibrary, RimTalkMemoryPatch");
            if (libraryType == null)
            {
                issues.Add("确保 RimTalk-ExpandMemory Mod 已启用");
                issues.Add("检查 Mod 加载顺序：RimTalk-ExpandMemory 应在 RimTalk-ExpandActions 之前");
            }

            // 检查启用的行为
            var enabledBehaviors = ExpandMemoryKnowledgeInjector.EnabledBehaviors;
            if (enabledBehaviors.Count == 0)
            {
                issues.Add("在 Mod 设置中启用至少一个行为");
            }

            // 通用建议
            if (issues.Count == 0)
            {
                sb.AppendLine("✓ 基础检查通过");
                sb.AppendLine("");
                sb.AppendLine("如果常识库仍未注入，请尝试：");
                sb.AppendLine("1. 在 Mod 设置中点击「注入规则到当前存档」按钮");
                sb.AppendLine("2. 或在开发者控制台调用: ExpandMemoryKnowledgeInjector.ManualInject()");
                sb.AppendLine("3. 检查游戏日志中是否有 [RimTalk-ExpandActions] 的错误信息");
                sb.AppendLine("4. 确保对话内容包含规则中定义的关键词");
            }
            else
            {
                sb.AppendLine("发现以下问题：");
                foreach (var issue in issues)
                {
                    sb.AppendLine($"  ⚠ {issue}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 测试 API 连接 - 尝试添加一个测试条目
        /// </summary>
        public static bool TestAPIConnection()
        {
            Log.Message("[RimTalk-ExpandActions] 开始测试 CommonKnowledgeLibrary 连接...");

            try
            {
                var libraryType = Type.GetType("RimTalk.Memory.CommonKnowledgeLibrary, RimTalkMemoryPatch");
                if (libraryType == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ✗ CommonKnowledgeLibrary 类型未找到");
                    return false;
                }

                // 尝试调用 AddKnowledgeEx（v4.0）
                var addExMethod = libraryType.GetMethod(
                    "AddKnowledgeEx",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                );

                if (addExMethod != null)
                {
                    Log.Message("[RimTalk-ExpandActions] 使用 AddKnowledgeEx (v4.0)");
                    
                    Type matchModeType = Type.GetType("RimTalk.Memory.KeywordMatchMode, RimTalkMemoryPatch");
                    object matchMode = matchModeType != null ? Enum.Parse(matchModeType, "Any") : 0;
                    
                    string testId = "test-expand-actions-diagnostic";
                    string testTag = "测试,诊断,RimTalk-ExpandActions";
                    string testContent = $"这是一个诊断测试条目，创建于 {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    
                    var result = addExMethod.Invoke(null, new object[] 
                    { 
                        testId,
                        testTag,
                        testContent,
                        0.5f,
                        new string[] { "测试", "诊断" },
                        matchMode
                    });
                    
                    if (result != null)
                    {
                        Log.Message($"[RimTalk-ExpandActions] ✓ 测试条目添加成功，ID: {result}");
                        
                        // 尝试删除测试条目
                        var removeMethod = libraryType.GetMethod(
                            "RemoveKnowledge",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                        );
                        
                        if (removeMethod != null)
                        {
                            bool removed = (bool)removeMethod.Invoke(null, new object[] { result });
                            if (removed)
                            {
                                Log.Message("[RimTalk-ExpandActions] ✓ 测试条目已删除");
                            }
                        }
                        
                        return true;
                    }
                }
                else
                {
                    // 尝试调用 AddKnowledge（旧版）
                    var addMethod = libraryType.GetMethod(
                        "AddKnowledge",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null,
                        new Type[] { typeof(string), typeof(string), typeof(float) },
                        null
                    );

                    if (addMethod == null)
                    {
                        Log.Error("[RimTalk-ExpandActions] ✗ AddKnowledge 和 AddKnowledgeEx 方法都未找到");
                        return false;
                    }
                    
                    Log.Message("[RimTalk-ExpandActions] 使用 AddKnowledge (兼容模式)");

                    // 添加测试条目
                    string testTag = "RimTalk-ExpandActions,诊断测试";
                    string testContent = $"这是一个测试条目，创建于 {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    
                    var result = addMethod.Invoke(null, new object[] { testTag, testContent, 0.5f });
                    
                    if (result != null)
                    {
                        Log.Message($"[RimTalk-ExpandActions] ✓ 测试条目添加成功，ID: {result}");
                        
                        // 尝试删除测试条目
                        var removeMethod = libraryType.GetMethod(
                            "RemoveKnowledge",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                            null,
                            new Type[] { typeof(string) },
                            null
                        );
                        
                        if (removeMethod != null)
                        {
                            removeMethod.Invoke(null, new object[] { result });
                            Log.Message("[RimTalk-ExpandActions] ✓ 测试条目已删除");
                        }
                        
                        return true;
                    }
                    else
                    {
                        Log.Warning("[RimTalk-ExpandActions] ⚠ AddKnowledge 返回 null（可能游戏未加载存档）");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ✗ 测试失败: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Log.Error($"[RimTalk-ExpandActions] 内部异常: {ex.InnerException.Message}");
                }
                return false;
            }
            
            return false;
        }

        /// <summary>
        /// 强制重新注入所有规则（清除旧规则并重新注入）
        /// </summary>
        public static void ForceReinject()
        {
            Log.Message("[RimTalk-ExpandActions] 开始强制重新注入常识规则...");
            
            try
            {
                // 先清除旧规则
                var result = ExpandMemoryKnowledgeInjector.ClearAndReinject();
                
                if (result.Success)
                {
                    Log.Message($"[RimTalk-ExpandActions] ✓ 重新注入完成: {result.InjectedRules}/{result.TotalRules} 条规则成功");
                    
                    if (result.FailedRules > 0)
                    {
                        Log.Warning($"[RimTalk-ExpandActions] ⚠ {result.FailedRules} 条规则注入失败");
                        foreach (var failedRule in result.FailedRuleNames)
                        {
                            string reason = result.FailureReasons.ContainsKey(failedRule) ? result.FailureReasons[failedRule] : "未知原因";
                            Log.Warning($"[RimTalk-ExpandActions]   - {failedRule}: {reason}");
                        }
                    }
                }
                else
                {
                    Log.Error($"[RimTalk-ExpandActions] ✗ 重新注入失败: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ✗ 重新注入异常: {ex.Message}");
            }
        }
    }
}
