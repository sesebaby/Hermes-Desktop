using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;

namespace RimTalkExpandActions.Patches
{
    /// <summary>
    /// Job 触发对话补丁
    /// 当小人开始特定 Job 时触发对话
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
    public static class JobTriggerPatch
    {
        private static FieldInfo pawnField;
        private static bool initialized = false;
        private static bool rimTalkAvailable = false;
        
        // RimTalk 类型缓存（使用反射避免硬依赖）
        private static Type talkRequestType;
        private static Type talkServiceType;
        private static Type talkTypeEnum;
        private static MethodInfo generateTalkMethod;
        private static object talkTypeUser;
        
        // 冷却时间控制
        private static Dictionary<int, int> pawnLastTriggerTick = new Dictionary<int, int>();
        private const int TRIGGER_COOLDOWN_TICKS = 2500; // 约1分钟游戏时间

        /// <summary>
        /// 初始化反射缓存
        /// </summary>
        private static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            
            try
            {
                pawnField = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");
                
                // 查找 RimTalk 类型 - 修正命名空间
                talkServiceType = AccessTools.TypeByName("RimTalk.Service.TalkService");
                talkRequestType = AccessTools.TypeByName("RimTalk.Data.TalkRequest");
                talkTypeEnum = AccessTools.TypeByName("RimTalk.Source.Data.TalkType");
                
                if (talkServiceType == null)
                {
                    Log.Warning("[RimTalk-ExpandActions] JobTriggerPatch: TalkService 未找到");
                    return;
                }
                
                if (talkRequestType == null)
                {
                    Log.Warning("[RimTalk-ExpandActions] JobTriggerPatch: TalkRequest 未找到");
                    return;
                }
                
                if (talkTypeEnum == null)
                {
                    Log.Warning("[RimTalk-ExpandActions] JobTriggerPatch: TalkType 枚举未找到");
                    return;
                }
                
                // 获取 TalkType.User 枚举值 - 使用 User 类型绕过冷却限制
                talkTypeUser = Enum.Parse(talkTypeEnum, "User");
                
                // 获取 GenerateTalk 方法
                generateTalkMethod = AccessTools.Method(talkServiceType, "GenerateTalk", new Type[] { talkRequestType });
                
                if (generateTalkMethod == null)
                {
                    Log.Warning("[RimTalk-ExpandActions] JobTriggerPatch: GenerateTalk 方法未找到");
                    return;
                }
                
                rimTalkAvailable = true;
                Log.Message("[RimTalk-ExpandActions] JobTriggerPatch: 初始化成功，行为触发对话功能已启用");
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk-ExpandActions] JobTriggerPatch 初始化失败: {ex.Message}");
                rimTalkAvailable = false;
            }
        }

        public static void Postfix(Pawn_JobTracker __instance, Job newJob, JobCondition lastJobEndCondition)
        {
            try
            {
                // 延迟初始化
                if (!initialized) Initialize();
                
                // 如果 RimTalk 不可用，直接返回
                if (!rimTalkAvailable) return;
                
                // 基本空检查
                if (__instance == null || newJob == null || newJob.def == null) return;
                
                // 检查设置是否初始化
                if (RimTalkExpandActionsMod.Settings == null) return;
                
                // 检查是否启用了任何 Job 触发器
                var triggers = RimTalkExpandActionsMod.Settings.enabledJobTriggers;
                if (triggers == null || triggers.Count == 0) return;
                
                // 检查该 Job 是否在启用列表中
                if (!triggers.Contains(newJob.def.defName)) return;

                // 获取 Pawn
                if (pawnField == null) return;
                Pawn pawn = pawnField.GetValue(__instance) as Pawn;
                
                // 严格的 pawn 状态检查
                if (pawn == null) return;
                if (pawn.Dead) return;
                if (!pawn.Spawned) return;
                if (pawn.Map == null) return;
                if (pawn.Map.mapPawns == null) return;
                if (pawn.RaceProps == null) return;
                
                // 检查是否是玩家派系或囚犯
                if (!pawn.IsColonist && !pawn.IsPrisonerOfColony) return;
                
                // 冷却时间检查（避免触发过于频繁）
                int pawnId = pawn.thingIDNumber;
                int currentTick = GenTicks.TicksGame;
                if (pawnLastTriggerTick.TryGetValue(pawnId, out int lastTick))
                {
                    // 检查是否发生时间倒流（如读档），如果是则重置
                    if (currentTick < lastTick)
                    {
                        pawnLastTriggerTick.Remove(pawnId);
                        if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                        {
                            Log.Message($"[RimTalk-ExpandActions] 检测到时间倒流 (Current: {currentTick}, Last: {lastTick})，重置 {pawn.LabelShort} 的冷却时间");
                        }
                    }
                    else if (currentTick - lastTick < TRIGGER_COOLDOWN_TICKS)
                    {
                        // 还在冷却中，静默返回
                        return;
                    }
                }
                
                // 尝试查找目标
                Pawn target = null;
                
                // 1. 从 Job 目标中查找
                if (newJob.targetA.Thing is Pawn p && p != null && !p.Dead) target = p;
                else if (newJob.targetB.Thing is Pawn p2 && p2 != null && !p2.Dead) target = p2;
                else if (newJob.targetC.Thing is Pawn p3 && p3 != null && !p3.Dead) target = p3;
                
                // 2. 如果 Job 没有目标 Pawn，尝试找最近的可见 Pawn
                if (target == null)
                {
                    float closestDist = 10f * 10f;
                    var allPawns = pawn.Map.mapPawns.AllPawnsSpawned;
                    if (allPawns == null) return;
                    
                    foreach (Pawn candidate in allPawns)
                    {
                        if (candidate == null || candidate == pawn) continue;
                        if (candidate.RaceProps == null || !candidate.RaceProps.Humanlike) continue;
                        if (candidate.Dead || candidate.Downed) continue;
                        
                        float dist = (candidate.Position - pawn.Position).LengthHorizontalSquared;
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            target = candidate;
                        }
                    }
                }

                // 即使没有目标也可以触发独白
                // 使用反射调用 RimTalk API
                bool success = TriggerTalkViaReflection(pawn, target, newJob);
                
                if (success)
                {
                    // 更新冷却时间
                    pawnLastTriggerTick[pawnId] = currentTick;
                }
            }
            catch (Exception ex)
            {
                // 静默处理异常，不影响游戏
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Warning($"[RimTalk-ExpandActions] JobTriggerPatch 异常: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 使用反射调用 RimTalk API，避免硬依赖
        /// 使用 TalkType.User 绕过 RimTalk 的冷却时间限制
        /// Prompt 留空，让 RimTalk 的 PromptService 自动构建上下文
        /// </summary>
        private static bool TriggerTalkViaReflection(Pawn pawn, Pawn target, Job job)
        {
            try
            {
                if (talkRequestType == null || generateTalkMethod == null || talkTypeUser == null)
                    return false;
                
                // ★ 关键：在触发对话前重置 RimTalk 的状态，绕过 "状态未变化" 的限制
                // 这可以解决 "每个角色仅限一次" 的问题
                RimTalkCooldownBypass.ResetPawnTalkState(pawn);
                if (target != null)
                {
                    RimTalkCooldownBypass.ResetPawnTalkState(target);
                }
                
                // Prompt 留空，让 RimTalk 的 PromptService.DecoratePrompt 自动处理
                // RimTalk 会根据 pawn 当前状态自动构建合适的上下文
                string prompt = "";
                
                // 使用反射创建 TalkRequest
                // 签名: TalkRequest(string prompt, Pawn initiator, Pawn recipient = null, TalkType talkType = TalkType.Other)
                var constructor = talkRequestType.GetConstructor(new Type[] {
                    typeof(string),
                    typeof(Pawn),
                    typeof(Pawn),
                    talkTypeEnum
                });
                
                if (constructor == null)
                {
                    Log.Warning("[RimTalk-ExpandActions] TalkRequest 构造函数未找到");
                    return false;
                }
                
                object request = constructor.Invoke(new object[] { prompt, pawn, target, talkTypeUser });
                
                // 调用 TalkService.GenerateTalk(request)
                object result = generateTalkMethod.Invoke(null, new object[] { request });
                bool success = result is bool b && b;
                
                if (success)
                {
                    if (RimTalkExpandActionsMod.Settings?.showActionMessages == true)
                    {
                        string jobLabel = job.def.label ?? job.def.defName;
                        string targetInfo = target != null ? $" -> {target.LabelShort}" : "";
                        Messages.Message($"[行为触发] {pawn.LabelShort}: {jobLabel}{targetInfo}", pawn, MessageTypeDefOf.SilentInput, false);
                    }
                }
                
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[RimTalk-ExpandActions] 行为触发: {pawn.LabelShort} 开始 {job.def.defName}, 结果: {(success ? "成功" : "失败")}");
                }
                
                return success;
            }
            catch (TargetInvocationException ex)
            {
                // 反射调用时内部方法抛出的异常会被包装为 TargetInvocationException
                // 需要获取 InnerException 来查看真正的错误
                var innerEx = ex.InnerException ?? ex;
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Warning($"[RimTalk-ExpandActions] TriggerTalkViaReflection 异常: {innerEx.Message}\nRef 78BD83AA");
                }
                return false;
            }
            catch (Exception ex)
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Warning($"[RimTalk-ExpandActions] TriggerTalkViaReflection 异常: {ex.Message}\n{ex.StackTrace}");
                }
                return false;
            }
        }
        
        /// <summary>
        /// 清理过期的冷却记录（可在游戏加载时调用）
        /// </summary>
        public static void ClearCooldowns()
        {
            pawnLastTriggerTick.Clear();
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message("[RimTalk-ExpandActions] JobTriggerPatch: 冷却记录已清除");
            }
        }
    }

    /// <summary>
    /// 游戏生命周期补丁，用于重置状态
    /// </summary>
    [HarmonyPatch(typeof(Game), "FinalizeInit")]
    public static class GameLifecyclePatch
    {
        public static void Postfix()
        {
            // 游戏加载完成或新游戏开始时，清理冷却记录
            JobTriggerPatch.ClearCooldowns();
        }
    }
}