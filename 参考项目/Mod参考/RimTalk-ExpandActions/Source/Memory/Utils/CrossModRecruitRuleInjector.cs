using System;
using System.Linq;
using System.Reflection;
using Verse;
using RimWorld;

namespace RimTalkExpandActions.Memory.Utils
{
    /// <summary>
    /// 跨项目招募规则注入器 [已弃用]
    /// 
    /// 此类已被 ExpandMemoryKnowledgeInjector 取代。
    /// 原因：此类注入到了错误的程序集 (RimTalkMemoryPatch)，而不是正确的 RimTalk-ExpandMemory。
    /// 
    /// 新的注入方式：
    /// - 使用 ExpandMemoryKnowledgeInjector
    /// - 通过 CommonKnowledgeLibrary.ImportFromExternalMod API
    /// - 支持正确的 PackageId: sanguo.rimtalk.expandmemory
    /// </summary>
    [Obsolete("已弃用，请使用 ExpandMemoryKnowledgeInjector")]
    public static class CrossModRecruitRuleInjector
    {
        // 所有方法已禁用
        
        [Obsolete("已弃用，不再使用")]
        public static bool TryInjectRecruitRule(float importance = 1.0f, string customContent = null)
        {
            Log.Warning("[RimTalk-ExpandActions] CrossModRecruitRuleInjector 已弃用，请使用 ExpandMemoryKnowledgeInjector");
            return false;
        }
        
        [Obsolete("已弃用，不再使用")]
        public static bool TryRemoveRecruitRule()
        {
            Log.Warning("[RimTalk-ExpandActions] CrossModRecruitRuleInjector 已弃用");
            return false;
        }
        
        [Obsolete("已弃用，不再使用")]
        public static bool CheckIfRecruitRuleExists()
        {
            return false;
        }
        
        [Obsolete("已弃用，不再使用")]
        public static bool TryInjectRule(string ruleId, string tag, string content, string[] keywords, float importance = 1.0f)
        {
            Log.Warning("[RimTalk-ExpandActions] CrossModRecruitRuleInjector 已弃用，请使用 ExpandMemoryKnowledgeInjector");
            return false;
        }
        
        [Obsolete("已弃用，不再使用")]
        public static int RemoveRules(params string[] ruleIds)
        {
            Log.Warning("[RimTalk-ExpandActions] CrossModRecruitRuleInjector 已弃用");
            return 0;
        }
    }
}
