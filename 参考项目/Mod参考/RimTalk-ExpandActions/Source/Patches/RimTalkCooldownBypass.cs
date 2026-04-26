using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RimTalkExpandActions.Patches
{
    /// <summary>
    /// RimTalk 对话冷却绕过工具
    /// 通过反射重置 RimTalk 的 PawnState 状态，绕过"每个角色仅限一次"的限制
    /// </summary>
    public static class RimTalkCooldownBypass
    {
        private static bool initialized = false;
        
        // RimTalk 类型缓存
        private static Type cacheType;
        private static Type pawnStateType;
        
        // 方法缓存
        private static MethodInfo getCacheMethod;
        
        // 字段缓存
        private static FieldInfo lastStatusField;
        private static FieldInfo rejectCountField;
        private static FieldInfo lastTalkTickField;
        private static FieldInfo isGeneratingTalkField;
        
        /// <summary>
        /// 初始化反射缓存
        /// </summary>
        public static bool Initialize()
        {
            if (initialized) return true;
            
            try
            {
                // 查找 RimTalk 类型
                cacheType = AccessTools.TypeByName("RimTalk.Data.Cache");
                pawnStateType = AccessTools.TypeByName("RimTalk.Data.PawnState");
                
                if (cacheType == null)
                {
                    Log.Warning("[RimTalk-ExpandActions] RimTalkCooldownBypass: Cache 类型未找到");
                    return false;
                }
                
                if (pawnStateType == null)
                {
                    Log.Warning("[RimTalk-ExpandActions] RimTalkCooldownBypass: PawnState 类型未找到");
                    return false;
                }
                
                // 获取 Cache.Get(Pawn) 方法
                getCacheMethod = AccessTools.Method(cacheType, "Get", new Type[] { typeof(Pawn) });
                if (getCacheMethod == null)
                {
                    Log.Warning("[RimTalk-ExpandActions] RimTalkCooldownBypass: Cache.Get 方法未找到");
                    return false;
                }
                
                // 获取 PawnState 的字段
                lastStatusField = AccessTools.Field(pawnStateType, "LastStatus");
                rejectCountField = AccessTools.Field(pawnStateType, "RejectCount");
                lastTalkTickField = AccessTools.Field(pawnStateType, "LastTalkTick");
                isGeneratingTalkField = AccessTools.Field(pawnStateType, "IsGeneratingTalk");
                
                // 检查是否找到所有字段（可能是属性）
                if (lastStatusField == null)
                {
                    // 尝试作为属性获取
                    var prop = AccessTools.Property(pawnStateType, "LastStatus");
                    if (prop != null)
                    {
                        Log.Message("[RimTalk-ExpandActions] LastStatus 是属性，将使用 PropertyInfo");
                    }
                }
                
                initialized = true;
                Log.Message("[RimTalk-ExpandActions] RimTalkCooldownBypass: 初始化成功");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] RimTalkCooldownBypass 初始化失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 重置 Pawn 的对话状态，绕过冷却限制
        /// 应在调用 TalkService.GenerateTalk 之前调用
        /// </summary>
        /// <param name="pawn">要重置状态的 Pawn</param>
        /// <returns>是否成功重置</returns>
        public static bool ResetPawnTalkState(Pawn pawn)
        {
            if (pawn == null)
            {
                Log.Warning("[RimTalk-ExpandActions] ResetPawnTalkState: pawn 为 null");
                return false;
            }
            
            if (!Initialize())
            {
                return false;
            }
            
            try
            {
                // 获取 PawnState
                object pawnState = getCacheMethod.Invoke(null, new object[] { pawn });
                
                if (pawnState == null)
                {
                    // PawnState 不存在是正常的，RimTalk 会自动创建
                    if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Message($"[RimTalk-ExpandActions] {pawn.LabelShort} 没有 PawnState（正常情况）");
                    }
                    return true;
                }
                
                // 重置 LastStatus - 强制状态变化
                if (lastStatusField != null)
                {
                    lastStatusField.SetValue(pawnState, "");
                }
                else
                {
                    var prop = AccessTools.Property(pawnStateType, "LastStatus");
                    prop?.SetValue(pawnState, "");
                }
                
                // 重置 RejectCount - 清除拒绝计数
                if (rejectCountField != null)
                {
                    rejectCountField.SetValue(pawnState, 0);
                }
                else
                {
                    var prop = AccessTools.Property(pawnStateType, "RejectCount");
                    prop?.SetValue(pawnState, 0);
                }
                
                // 重置 LastTalkTick - 清除冷却时间
                if (lastTalkTickField != null)
                {
                    lastTalkTickField.SetValue(pawnState, 0);
                }
                else
                {
                    var prop = AccessTools.Property(pawnStateType, "LastTalkTick");
                    prop?.SetValue(pawnState, 0);
                }
                
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[RimTalk-ExpandActions] ✓ 已重置 {pawn.LabelShort} 的对话状态（绕过冷却）");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ResetPawnTalkState 失败: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// 检查 Pawn 是否正在生成对话
        /// </summary>
        public static bool IsPawnGeneratingTalk(Pawn pawn)
        {
            if (pawn == null || !Initialize())
            {
                return false;
            }
            
            try
            {
                object pawnState = getCacheMethod.Invoke(null, new object[] { pawn });
                if (pawnState == null)
                {
                    return false;
                }
                
                if (isGeneratingTalkField != null)
                {
                    return (bool)isGeneratingTalkField.GetValue(pawnState);
                }
                else
                {
                    var prop = AccessTools.Property(pawnStateType, "IsGeneratingTalk");
                    if (prop != null)
                    {
                        return (bool)prop.GetValue(pawnState);
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] IsPawnGeneratingTalk 失败: {ex.Message}");
                return false;
            }
        }
    }
}