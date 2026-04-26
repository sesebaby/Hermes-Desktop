using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;
using RimWorld;

namespace RimTalkExpandActions.Memory.Utils
{
    /// <summary>
    /// RimTalk-ExpandMemory 常识库智能注入器 v4.0
    /// 
    /// 更新说明：
    /// - v4.0: 完全重写以适配新版 CommonKnowledgeLibrary API
    /// - 支持标签分类注入（规则类型自动分类为 Guidelines）
    /// - 优化注入逻辑，使用 AddKnowledgeEx 方法
    /// - 添加批量操作和错误处理
    /// 
    /// 用户说明：
    /// - 常识库是存档绑定数据，新建游戏或加载时自动注入
    /// - 用户需要在加载存档后通过 Mod 设置进行手动注入
    /// - 每个存档拥有独立的常识库实例
    /// </summary>
    public static class ExpandMemoryKnowledgeInjector
    {
        /// <summary>
        /// 注入结果
        /// </summary>
        public class InjectionResult
        {
            public bool Success { get; set; }
            public int TotalRules { get; set; }
            public int InjectedRules { get; set; }
            public int FailedRules { get; set; }
            public List<string> InjectedRuleNames { get; set; } = new List<string>();
            public List<string> FailedRuleNames { get; set; } = new List<string>();
            public Dictionary<string, string> FailureReasons { get; set; } = new Dictionary<string, string>();
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// 获取当前启用的行为列表
        /// </summary>
        public static HashSet<string> EnabledBehaviors
        {
            get
            {
                var enabled = new HashSet<string>();
                var settings = RimTalkExpandActionsMod.Settings;
                
                if (settings == null)
                {
                    // 如果设置未加载，返回全部启用
                    foreach (var rule in BehaviorRuleContents.GetAllRules())
                    {
                        enabled.Add(rule.Key);
                    }
                    return enabled;
                }
                
                if (settings.enableRecruit) enabled.Add("expand-action-recruit");
                if (settings.enableDropWeapon) enabled.Add("expand-action-drop-weapon");
                if (settings.enableRomance) enabled.Add("expand-action-romance");
                if (settings.enableInspiration) enabled.Add("expand-action-inspiration");
                if (settings.enableRest) enabled.Add("expand-action-rest");
                if (settings.enableGift) enabled.Add("expand-action-gift");
                if (settings.enableSocialDining) enabled.Add("expand-action-social-dining");
                if (settings.enableSocialRelax) enabled.Add("expand-action-social-relax");
                
                return enabled;
            }
        }

        /// <summary>
        /// 获取所有行为规则的描述
        /// </summary>
        public static Dictionary<string, string> GetRuleDescriptions()
        {
            return new Dictionary<string, string>
            {
                { "招募系统", "通过对话招募 NPC 到殖民地" },
                { "社交用餐", "邀请他人共进晚餐增进关系" },
                { "投降系统", "让敌人放下武器投降" },
                { "恋爱关系", "建立或结束恋人关系" },
                { "灵感触发", "给予角色工作战斗交易灵感" },
                { "强制休息", "让角色去休息或陷入昏迷" },
                { "赠送物品", "从背包中赠送物品给他人" },
                { "社交放松", "组织多人进行社交娱乐活动" }
            };
        }

        /// <summary>
        /// 清除旧规则并重新注入所有规则
        /// </summary>
        public static InjectionResult ClearAndReinject()
        {
            var result = new InjectionResult();
            
            try
            {
                Log.Message("[RimTalk-ExpandActions] ═══════════ 清除并重新注入常识库 v4.0 ═══════════");
                
                // 1. 先清除旧规则
                var clearResult = ClearOldRules();
                if (!clearResult.Success)
                {
                    Log.Warning($"[RimTalk-ExpandActions] 清除旧规则时出现问题: {clearResult.ErrorMessage}");
                    // 继续尝试注入
                }
                else
                {
                    Log.Message($"[RimTalk-ExpandActions] 已清除 {clearResult.InjectedRules} 条旧规则");
                }
                
                // 2. 重新注入
                return ManualInject();
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"清除并重新注入失败: {ex.Message}";
                Log.Error($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                Log.Error($"[RimTalk-ExpandActions] 堆栈跟踪:\n{ex.StackTrace}");
                return result;
            }
        }

        /// <summary>
        /// 清除所有旧的 ExpandActions 规则
        /// v4.1: 使用实例方法通过 MemoryManager.CommonKnowledge.Entries 列表
        /// </summary>
        public static InjectionResult ClearOldRules()
        {
            var result = new InjectionResult();
            
            try
            {
                Log.Message("[RimTalk-ExpandActions] ═══════════ 清除旧规则 v4.1 ═══════════");
                
                // 1. 检查是否有活跃游戏
                if (Current.Game == null || Find.World == null)
                {
                    result.ErrorMessage = "请先加载或创建游戏存档";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                // 2. 获取 MemoryManager（World组件）
                Type memoryManagerType = FindType("RimTalk.Memory.MemoryManager");
                if (memoryManagerType == null)
                {
                    result.ErrorMessage = "未找到 RimTalk.Memory.MemoryManager";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                // 3. 获取 MemoryManager 实例
                object memoryManager = null;
                try
                {
                    var getComponentMethod = typeof(RimWorld.Planet.World).GetMethod("GetComponent", new Type[] { });
                    var genericMethod = getComponentMethod.MakeGenericMethod(memoryManagerType);
                    memoryManager = genericMethod.Invoke(Find.World, null);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk-ExpandActions] GetComponent<MemoryManager> 失败: {ex.Message}");
                }
                
                if (memoryManager == null)
                {
                    result.ErrorMessage = "未能获取 MemoryManager 实例";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                // 4. 获取 CommonKnowledge 属性
                PropertyInfo commonKnowledgeProp = memoryManagerType.GetProperty("CommonKnowledge");
                if (commonKnowledgeProp == null)
                {
                    result.ErrorMessage = "未找到 CommonKnowledge 属性";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                object commonKnowledge = commonKnowledgeProp.GetValue(memoryManager);
                if (commonKnowledge == null)
                {
                    result.ErrorMessage = "CommonKnowledge 为 null";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                Type commonKnowledgeType = commonKnowledge.GetType();
                
                // 5. 获取 Entries 属性
                PropertyInfo entriesProp = commonKnowledgeType.GetProperty("Entries");
                if (entriesProp == null)
                {
                    result.ErrorMessage = "未找到 Entries 属性";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                var entriesList = entriesProp.GetValue(commonKnowledge) as System.Collections.IList;
                if (entriesList == null)
                {
                    result.Success = true;
                    result.InjectedRules = 0;
                    Log.Message("[RimTalk-ExpandActions] Entries 列表为空，无需清除");
                    return result;
                }
                
                // 6. 获取 RemoveEntry 方法
                Type entryType = FindType("RimTalk.Memory.CommonKnowledgeEntry");
                MethodInfo removeEntryMethod = commonKnowledgeType.GetMethod("RemoveEntry", new Type[] { entryType });
                
                if (removeEntryMethod == null)
                {
                    // 没有 RemoveEntry 方法，尝试直接从列表中移除
                    Log.Warning("[RimTalk-ExpandActions] 未找到 RemoveEntry 方法，尝试直接从列表移除");
                }
                
                // 7. 找到并删除所有 expand-action- 开头的条目
                var entriesToRemove = new List<object>();
                
                foreach (var entry in entriesList)
                {
                    if (entry == null) continue;
                    
                    // 获取 id 字段
                    FieldInfo idField = entryType.GetField("id");
                    if (idField != null)
                    {
                        string id = idField.GetValue(entry) as string;
                        if (!string.IsNullOrEmpty(id) && id.StartsWith("expand-action-"))
                        {
                            entriesToRemove.Add(entry);
                        }
                    }
                }
                
                Log.Message($"[RimTalk-ExpandActions] 找到 {entriesToRemove.Count} 条需要清除的旧规则");
                
                // 8. 删除条目
                int removedCount = 0;
                foreach (var entry in entriesToRemove)
                {
                    try
                    {
                        if (removeEntryMethod != null)
                        {
                            removeEntryMethod.Invoke(commonKnowledge, new object[] { entry });
                        }
                        else
                        {
                            entriesList.Remove(entry);
                        }
                        removedCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk-ExpandActions] 删除条目失败: {ex.Message}");
                    }
                }
                
                result.Success = true;
                result.InjectedRules = removedCount;
                Log.Message($"[RimTalk-ExpandActions] 已清除 {removedCount} 条旧规则");
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"清除失败: {ex.Message}";
                Log.Error($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                Log.Error($"[RimTalk-ExpandActions] 堆栈跟踪:\n{ex.StackTrace}");
                return result;
            }
        }

        /// <summary>
        /// 手动注入常识库到当前存档
        /// v4.1: 使用 MemoryManager.CommonKnowledge.AddEntry 实例方法
        /// </summary>
        public static InjectionResult ManualInject()
        {
            var result = new InjectionResult();
            
            try
            {
                Log.Message("[RimTalk-ExpandActions] ═══════════ 手动注入常识库 v4.1 ═══════════");
                
                // 1. 检查是否有活跃游戏
                if (Current.Game == null || Find.World == null)
                {
                    result.ErrorMessage = "请先加载或创建游戏存档";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                Log.Message("[RimTalk-ExpandActions] 当前有活跃游戏存档");
                
                // 2. 获取 MemoryManager（World组件）
                Type memoryManagerType = FindType("RimTalk.Memory.MemoryManager");
                if (memoryManagerType == null)
                {
                    result.ErrorMessage = "未找到 RimTalk.Memory.MemoryManager，确保 RimTalk-ExpandMemory 已安装";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                Log.Message($"[RimTalk-ExpandActions] 找到 MemoryManager: {memoryManagerType.FullName}");
                
                // 3. 获取 MemoryManager 实例（通过 Find.World.GetComponent）
                object memoryManager = null;
                try
                {
                    var getComponentMethod = typeof(RimWorld.Planet.World).GetMethod("GetComponent", new Type[] { });
                    var genericMethod = getComponentMethod.MakeGenericMethod(memoryManagerType);
                    memoryManager = genericMethod.Invoke(Find.World, null);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimTalk-ExpandActions] GetComponent<MemoryManager> 失败: {ex.Message}");
                }
                
                if (memoryManager == null)
                {
                    result.ErrorMessage = "未能获取 MemoryManager 实例，请确保存档已加载";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                Log.Message("[RimTalk-ExpandActions] 获取到 MemoryManager 实例");
                
                // 4. 获取 CommonKnowledge 属性
                PropertyInfo commonKnowledgeProp = memoryManagerType.GetProperty("CommonKnowledge");
                if (commonKnowledgeProp == null)
                {
                    result.ErrorMessage = "未找到 CommonKnowledge 属性";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                object commonKnowledge = commonKnowledgeProp.GetValue(memoryManager);
                if (commonKnowledge == null)
                {
                    result.ErrorMessage = "CommonKnowledge 为 null";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                Log.Message("[RimTalk-ExpandActions] 获取到 CommonKnowledge 实例");
                
                // 5. 获取 CommonKnowledgeEntry 类型和 AddEntry 方法
                Type entryType = FindType("RimTalk.Memory.CommonKnowledgeEntry");
                if (entryType == null)
                {
                    result.ErrorMessage = "未找到 CommonKnowledgeEntry 类型";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    return result;
                }
                
                Type commonKnowledgeType = commonKnowledge.GetType();
                MethodInfo addEntryMethod = commonKnowledgeType.GetMethod("AddEntry", new Type[] { entryType });
                
                if (addEntryMethod == null)
                {
                    // 尝试获取 AddEntry(string, string) 方法
                    addEntryMethod = commonKnowledgeType.GetMethod("AddEntry", new Type[] { typeof(string), typeof(string) });
                }
                
                if (addEntryMethod == null)
                {
                    result.ErrorMessage = "未找到 AddEntry 方法";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                    
                    // 列出所有可用方法
                    Log.Warning("[RimTalk-ExpandActions] 可用的实例方法:");
                    foreach (var method in commonKnowledgeType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var pars = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Log.Message($"[RimTalk-ExpandActions]   - {method.Name}({pars}) -> {method.ReturnType.Name}");
                    }
                    
                    return result;
                }
                
                Log.Message($"[RimTalk-ExpandActions] 找到注入方法: {addEntryMethod.Name}");
                
                // 6. 准备注入内容
                var allRules = BehaviorRuleContents.GetAllRules();
                var enabledBehaviors = EnabledBehaviors;
                
                // 只注入启用的规则
                var rulesToInject = allRules.Where(kvp => enabledBehaviors.Contains(kvp.Key)).ToList();
                
                result.TotalRules = rulesToInject.Count;
                
                Log.Message($"[RimTalk-ExpandActions] 准备注入 {result.TotalRules} 条行为规则（已启用）...");
                
                // 7. 逐条创建 CommonKnowledgeEntry 并调用 AddEntry
                int successCount = 0;
                var descriptions = GetRuleDescriptions();
                
                // 获取 KeywordMatchMode 枚举类型
                Type matchModeType = FindType("RimTalk.Memory.KeywordMatchMode");
                
                foreach (var ruleKvp in rulesToInject)
                {
                    var rule = ruleKvp.Value;
                    
                    try
                    {
                        // 清理规则内容
                        string cleanContent = CleanRuleContent(rule.Content);
                        
                        bool success = false;
                        
                        // 方式1: 如果 AddEntry 接受 CommonKnowledgeEntry
                        if (addEntryMethod.GetParameters()[0].ParameterType == entryType)
                        {
                            // 创建 CommonKnowledgeEntry 实例
                            object entry = Activator.CreateInstance(entryType);
                            
                            // 设置属性
                            entryType.GetField("id")?.SetValue(entry, rule.Id);
                            entryType.GetField("tag")?.SetValue(entry, rule.Tag);
                            entryType.GetField("content")?.SetValue(entry, cleanContent);
                            entryType.GetField("importance")?.SetValue(entry, rule.Importance);
                            entryType.GetField("isEnabled")?.SetValue(entry, true);
                            
                            // 设置 keywords
                            if (rule.Keywords != null && rule.Keywords.Length > 0)
                            {
                                var keywordsField = entryType.GetField("keywords");
                                if (keywordsField != null)
                                {
                                    var keywordsList = new List<string>(rule.Keywords);
                                    keywordsField.SetValue(entry, keywordsList);
                                }
                            }
                            
                            // 设置 matchMode
                            if (matchModeType != null)
                            {
                                var matchModeField = entryType.GetField("matchMode");
                                if (matchModeField != null)
                                {
                                    object matchMode = Enum.Parse(matchModeType, "Any");
                                    matchModeField.SetValue(entry, matchMode);
                                }
                            }
                            
                            // 调用 AddEntry
                            addEntryMethod.Invoke(commonKnowledge, new object[] { entry });
                            success = true;
                        }
                        // 方式2: 如果 AddEntry 接受 (string, string)
                        else
                        {
                            addEntryMethod.Invoke(commonKnowledge, new object[] { rule.Tag, cleanContent });
                            success = true;
                        }
                        
                        if (success)
                        {
                            successCount++;
                            result.InjectedRuleNames.Add(ruleKvp.Key);
                            Log.Message($"[RimTalk-ExpandActions]   ✓ 已注入: {rule.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedRules++;
                        result.FailedRuleNames.Add(ruleKvp.Key);
                        result.FailureReasons[ruleKvp.Key] = ex.InnerException?.Message ?? ex.Message;
                        Log.Warning($"[RimTalk-ExpandActions]   ✗ 注入失败 {rule.Id}: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
                
                // 7. 处理结果
                result.InjectedRules = successCount;
                result.Success = successCount > 0;
                
                if (result.Success)
                {
                    Log.Message("[RimTalk-ExpandActions] ═══════════════════════════════════");
                    Log.Message($"[RimTalk-ExpandActions] ✓ 成功注入 {successCount}/{result.TotalRules} 条规则到当前存档");
                    Log.Message("[RimTalk-ExpandActions] ═══════════════════════════════════");
                    
                    var ruleDescMap = new Dictionary<string, string>
                    {
                        { "expand-action-recruit", "招募系统" },
                        { "expand-action-social-dining", "社交用餐" },
                        { "expand-action-drop-weapon", "投降系统" },
                        { "expand-action-romance", "恋爱关系" },
                        { "expand-action-inspiration", "灵感触发" },
                        { "expand-action-rest", "强制休息" },
                        { "expand-action-gift", "赠送物品" },
                        { "expand-action-social-relax", "社交放松" }
                    };
                    
                    foreach (var ruleName in result.InjectedRuleNames)
                    {
                        if (ruleDescMap.ContainsKey(ruleName))
                        {
                            Log.Message($"[RimTalk-ExpandActions]   • {ruleDescMap[ruleName]}: {descriptions[ruleDescMap[ruleName]]}");
                        }
                    }
                    
                    if (result.FailedRules > 0)
                    {
                        Log.Warning($"[RimTalk-ExpandActions] ⚠ {result.FailedRules} 条规则注入失败");
                        foreach (var failedRule in result.FailedRuleNames)
                        {
                            string reason = result.FailureReasons.ContainsKey(failedRule) ? result.FailureReasons[failedRule] : "未知原因";
                            Log.Warning($"[RimTalk-ExpandActions]     - {failedRule}: {reason}");
                        }
                    }
                }
                else
                {
                    result.ErrorMessage = "注入完成但没有规则添加";
                    Log.Warning($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"注入失败: {ex.Message}";
                Log.Error($"[RimTalk-ExpandActions] {result.ErrorMessage}");
                Log.Error($"[RimTalk-ExpandActions] 堆栈跟踪:\n{ex.StackTrace}");
            }
            
            return result;
        }

        /// <summary>
        /// 检查常识库状态
        /// </summary>
        public static string CheckStatus()
        {
            try
            {
                if (Current.Game == null || Find.World == null)
                {
                    return "未加载游戏存档";
                }
                
                Type commonKnowledgeType = FindType("RimTalk.Memory.CommonKnowledgeLibrary");
                if (commonKnowledgeType == null)
                {
                    return "未找到 RimTalk-ExpandMemory";
                }
                
                // 检查新版 API
                MethodInfo addKnowledgeExMethod = commonKnowledgeType.GetMethod(
                    "AddKnowledgeEx",
                    BindingFlags.Public | BindingFlags.Static
                );
                
                if (addKnowledgeExMethod != null)
                {
                    return "✓ RimTalk-ExpandMemory v4.0+ 已就绪";
                }
                
                // 检查旧版 API
                MethodInfo addKnowledgeMethod = commonKnowledgeType.GetMethod(
                    "AddKnowledge",
                    BindingFlags.Public | BindingFlags.Static
                );
                
                if (addKnowledgeMethod != null)
                {
                    return "✓ RimTalk-ExpandMemory (兼容模式) 已就绪";
                }
                
                return "⚠ API 方法不存在";
            }
            catch (Exception ex)
            {
                return $"✗ 检查失败: {ex.Message}";
            }
        }

        #region 辅助方法

        /// <summary>
        /// 查找类型（跨程序集）
        /// </summary>
        private static Type FindType(string fullTypeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullTypeName);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        /// <summary>
        /// 清理规则内容：移除换行符和多余空格
        /// </summary>
        private static string CleanRuleContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;
            
            // 1. 移除所有类型的换行符
            content = content.Replace("\r\n", " ");
            content = content.Replace("\r", " ");
            content = content.Replace("\n", " ");
            
            // 2. 移除多余的空格（连续空格替换为单个空格）
            while (content.Contains("  "))
            {
                content = content.Replace("  ", " ");
            }
            
            // 3. 移除字符串开头和结尾的空格
            content = content.Trim();
            
            return content;
        }

        #endregion
    }
}
