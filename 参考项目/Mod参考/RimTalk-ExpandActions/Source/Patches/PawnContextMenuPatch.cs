using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace RimTalkExpandActions.Patches
{
    /// <summary>
    /// Pawn 右键菜单补丁
    /// 直接右键殖民者显示"立即对话"选项，将该 Pawn 加入到 RimTalk 对话队列最前端
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "GetFloatMenuOptions")]
    public static class PawnContextMenuPatch
    {
        // RimTalk 反射缓存
        private static bool initialized = false;
        private static bool rimTalkAvailable = false;
        
        private static Type talkServiceType;
        private static Type talkRequestType;
        private static Type talkTypeEnum;
        private static MethodInfo generateTalkMethod;
        private static MethodInfo addToQueueFirstMethod;
        private static FieldInfo talkQueueField;
        private static object talkTypeUser;
        
        /// <summary>
        /// 延迟初始化反射缓存
        /// </summary>
        private static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            
            try
            {
                // 查找 RimTalk 类型
                talkServiceType = AccessTools.TypeByName("RimTalk.Service.TalkService");
                talkRequestType = AccessTools.TypeByName("RimTalk.Data.TalkRequest");
                talkTypeEnum = AccessTools.TypeByName("RimTalk.Source.Data.TalkType");
                
                if (talkServiceType == null || talkRequestType == null || talkTypeEnum == null)
                {
                    Log.Message("[RimTalk-ExpandActions] PawnContextMenuPatch: RimTalk 类型未找到");
                    return;
                }
                
                // 获取 TalkType.User 枚举值
                talkTypeUser = Enum.Parse(talkTypeEnum, "User");
                
                // 获取 GenerateTalk 方法
                generateTalkMethod = AccessTools.Method(talkServiceType, "GenerateTalk", new Type[] { talkRequestType });
                
                // 尝试查找 AddToQueueFirst 或类似方法
                string[] possibleMethods = { "AddToQueueFirst", "InsertFirst", "InsertToFront", "AddFirst", "EnqueueFirst", "PushFront", "AddPriority" };
                foreach (string name in possibleMethods)
                {
                    addToQueueFirstMethod = AccessTools.Method(talkServiceType, name);
                    if (addToQueueFirstMethod != null)
                    {
                        Log.Message($"[RimTalk-ExpandActions] 找到优先入队方法: {name}");
                        break;
                    }
                }
                
                // 尝试查找队列字段
                string[] possibleQueueNames = { "_requestQueue", "_talkQueue", "requestQueue", "talkQueue", "TalkQueue", "RequestQueue", "_queue", "queue" };
                foreach (string name in possibleQueueNames)
                {
                    talkQueueField = AccessTools.Field(talkServiceType, name);
                    if (talkQueueField != null)
                    {
                        Log.Message($"[RimTalk-ExpandActions] 找到队列字段: {name}");
                        break;
                    }
                }
                
                rimTalkAvailable = generateTalkMethod != null;
                
                if (rimTalkAvailable)
                {
                    Log.Message("[RimTalk-ExpandActions] PawnContextMenuPatch: 初始化成功");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk-ExpandActions] PawnContextMenuPatch 初始化失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 后置补丁：向右键菜单添加选项
        /// Pawn.GetFloatMenuOptions 方法签名: IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        /// 其中 selPawn 是当前选中的 pawn，this 是被右键点击的 pawn
        /// </summary>
        public static void Postfix(Pawn __instance, Pawn selPawn, ref IEnumerable<FloatMenuOption> __result)
        {
            try
            {
                // 延迟初始化
                if (!initialized) Initialize();
                
                // 如果 RimTalk 不可用，直接返回
                if (!rimTalkAvailable) return;
                
                // 基本检查 - __instance 是被右键点击的 Pawn
                if (__instance == null) return;
                
                // 只对人类殖民者或囚犯显示选项
                if (__instance.RaceProps == null || !__instance.RaceProps.Humanlike) return;
                if (!__instance.IsColonist && !__instance.IsPrisonerOfColony) return;
                if (__instance.Dead) return;
                
                // 创建菜单选项
                Pawn targetPawn = __instance; // 被右键的 Pawn
                string label = "立即对话";
                
                FloatMenuOption option = new FloatMenuOption(label, delegate
                {
                    TryAddToPriorityQueue(targetPawn);
                });
                
                // 将新选项添加到结果中
                List<FloatMenuOption> newResult = new List<FloatMenuOption>(__result);
                newResult.Add(option);
                __result = newResult;
            }
            catch (Exception ex)
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Warning($"[RimTalk-ExpandActions] PawnContextMenuPatch 异常: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 查找附近的人类 Pawn 作为对话对象
        /// </summary>
        private static Pawn FindNearbyPawn(Pawn initiator, float maxDistance = 10f)
        {
            if (initiator?.Map == null) return null;
            
            Pawn closestPawn = null;
            float closestDistSq = maxDistance * maxDistance;
            
            foreach (Pawn candidate in initiator.Map.mapPawns.AllPawnsSpawned)
            {
                // 跳过自己
                if (candidate == initiator) continue;
                
                // 只考虑人类
                if (candidate.RaceProps == null || !candidate.RaceProps.Humanlike) continue;
                
                // 跳过死亡或倒地的
                if (candidate.Dead || candidate.Downed) continue;
                
                // 计算距离
                float distSq = (candidate.Position - initiator.Position).LengthHorizontalSquared;
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestPawn = candidate;
                }
            }
            
            return closestPawn;
        }
        
        /// <summary>
        /// 将目标 Pawn 加入对话队列最前端
        /// </summary>
        private static void TryAddToPriorityQueue(Pawn targetPawn)
        {
            try
            {
                if (!rimTalkAvailable)
                {
                    Messages.Message("RimTalk 未就绪", MessageTypeDefOf.RejectInput, false);
                    return;
                }
                
                // 查找附近的 Pawn 作为对话对象
                Pawn recipient = FindNearbyPawn(targetPawn);
                
                // 创建 TalkRequest - 目标 Pawn 作为发起者（说话者）
                var constructor = talkRequestType.GetConstructor(new Type[] {
                    typeof(string),
                    typeof(Pawn),
                    typeof(Pawn),
                    talkTypeEnum
                });
                
                if (constructor == null)
                {
                    Log.Warning("[RimTalk-ExpandActions] TalkRequest 构造函数未找到");
                    return;
                }
                
                // 使用空 prompt，让 RimTalk 自动构建上下文
                // initiator = targetPawn（说话者），recipient = 附近的 Pawn（如果有）
                string prompt = "";
                object request = constructor.Invoke(new object[] { prompt, targetPawn, recipient, talkTypeUser });
                
                if (recipient != null)
                {
                    Log.Message($"[RimTalk-ExpandActions] {targetPawn.LabelShort} 将与 {recipient.LabelShort} 对话");
                }
                else
                {
                    Log.Message($"[RimTalk-ExpandActions] {targetPawn.LabelShort} 将进行独白（附近没有其他人）");
                }
                
                bool success = false;
                
                // 方案1：尝试使用专门的优先入队方法
                if (addToQueueFirstMethod != null)
                {
                    try
                    {
                        object result = addToQueueFirstMethod.Invoke(null, new object[] { request });
                        success = result == null || (result is bool b && b);
                        if (success)
                        {
                            Log.Message($"[RimTalk-ExpandActions] 已将 {targetPawn.LabelShort} 加入队列最前端 (优先方法)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk-ExpandActions] 优先入队方法调用失败: {ex.Message}");
                    }
                }
                
                // 方案2：尝试直接操作队列，在最前端插入
                if (!success && talkQueueField != null)
                {
                    try
                    {
                        object queue = talkQueueField.GetValue(null);
                        if (queue != null)
                        {
                            // 尝试作为 List 操作
                            if (queue is IList list)
                            {
                                list.Insert(0, request);
                                success = true;
                                Log.Message($"[RimTalk-ExpandActions] 已将 {targetPawn.LabelShort} 插入队列最前端 (IList)");
                            }
                            // 尝试使用反射调用 Insert(0, item)
                            else
                            {
                                var insertMethod = queue.GetType().GetMethod("Insert");
                                if (insertMethod != null)
                                {
                                    insertMethod.Invoke(queue, new object[] { 0, request });
                                    success = true;
                                    Log.Message($"[RimTalk-ExpandActions] 已将 {targetPawn.LabelShort} 插入队列最前端 (反射Insert)");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk-ExpandActions] 队列操作失败: {ex.Message}");
                    }
                }
                
                // 方案3：如果没有队列访问，直接调用 GenerateTalk（立即执行）
                if (!success && generateTalkMethod != null)
                {
                    try
                    {
                        object result = generateTalkMethod.Invoke(null, new object[] { request });
                        success = result is bool b && b;
                        if (success)
                        {
                            Log.Message($"[RimTalk-ExpandActions] 已直接触发 {targetPawn.LabelShort} 的对话");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk-ExpandActions] GenerateTalk 调用失败: {ex.Message}");
                    }
                }
                
                // 显示消息
                if (success)
                {
                    string recipientInfo = recipient != null ? $" (与 {recipient.LabelShort})" : " (独白)";
                    Messages.Message($"{targetPawn.LabelShort} 已加入对话队列{recipientInfo}", targetPawn, MessageTypeDefOf.SilentInput, false);
                }
                else
                {
                    Messages.Message($"无法将 {targetPawn.LabelShort} 加入对话队列", MessageTypeDefOf.RejectInput, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] TryAddToPriorityQueue 失败: {ex.Message}\n{ex.StackTrace}");
                Messages.Message("对话请求失败", MessageTypeDefOf.RejectInput, false);
            }
        }
        
        /// <summary>
        /// 获取 RimTalk 是否可用
        /// </summary>
        public static bool IsRimTalkAvailable
        {
            get
            {
                if (!initialized) Initialize();
                return rimTalkAvailable;
            }
        }
    }
}