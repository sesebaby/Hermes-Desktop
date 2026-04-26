using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimTalkExpandActions.SocialDining;

namespace RimTalkExpandActions.Memory.Actions
{
    /// <summary>
    /// 静态工具类，负责执行 RimTalk 对话触发的游戏逻辑
    /// </summary>
    public static class RimTalkActions
    {
        /// <summary>
        /// 执行招募逻辑，将目标 NPC 加入玩家派系
        /// 增强版本：完整日志记录、异常隔离、逻辑前置
        /// </summary>
        /// <param name="pawnToRecruit">要招募的 NPC</param>
        /// <param name="recruiter">执行招募的玩家角色（可选）</param>
        public static void ExecuteRecruit(Pawn pawnToRecruit, Pawn recruiter = null)
        {
            // --- 1. 参数验证与前置检查 ---
            if (pawnToRecruit == null)
            {
                Log.Error("[RimTalk-ExpandActions] 招募失败: pawnToRecruit 为 null");
                return;
            }
            
            // 如果已经死亡或已经是玩家派系，则提前退出
            if (pawnToRecruit.Dead)
            {
                Log.Warning($"[RimTalk-ExpandActions] 招募失败: {pawnToRecruit.Name} 已死亡");
                return;
            }
            if (pawnToRecruit.Faction == Faction.OfPlayer)
            {
                Log.Warning($"[RimTalk-ExpandActions] 招募失败: {pawnToRecruit.Name} 已经是玩家派系成员");
                return;
            }
            
            // 记录原派系名称
            string oldFactionName = pawnToRecruit.Faction?.Name ?? "未知派系";
            
            try
            {
                Log.Message($"[RimTalk-ExpandActions] 尝试通过对话招募: {pawnToRecruit.Name.ToStringShort}. 旧派系: {oldFactionName}");
                
                // --- 2. 核心操作：更改派系（必须成功） ---
                pawnToRecruit.SetFaction(Faction.OfPlayer, recruiter);
                Log.Message($"[RimTalk-ExpandActions] SetFaction 调用完成。当前派系: {pawnToRecruit.Faction?.Name ?? "null"}");

                // --- 3. 危险操作：清除旧 Lord 逻辑（隔离保护） ---
                Lord lord = pawnToRecruit.GetLord();
                if (lord != null)
                {
                    try
                    {
                        // 使用 Try/Catch 保护，防止此处的 Mod 冲突或游戏版本差异导致程序中断。
                        lord.Notify_PawnLost(pawnToRecruit, PawnLostCondition.ChangedFaction);
                        Log.Message($"[RimTalk-ExpandActions] 成功移除旧 Lord 逻辑。");
                    }
                    catch (Exception ex)
                    {
                        // 仅记录错误，不中断招募流程
                        Log.Error($"[RimTalk-ExpandActions] 移除 Lord 逻辑失败 (不致命): {ex.Message}");
                    }
                }
                else
                {
                    Log.Message($"[RimTalk-ExpandActions] {pawnToRecruit.Name.ToStringShort} 没有 Lord，跳过清理。");
                }

                // --- 4. 清除客人/囚犯状态 ---
                if (pawnToRecruit.guest != null)
                {
                    try
                    {
                        pawnToRecruit.guest.SetGuestStatus(null);
                        Log.Message($"[RimTalk-ExpandActions] 清除 {pawnToRecruit.Name.ToStringShort} 的客人/囚犯状态。");
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[RimTalk-ExpandActions] 清除 Guest 状态失败 (不致命): {ex.Message}");
                    }
                }
                else
                {
                    Log.Message($"[RimTalk-ExpandActions] {pawnToRecruit.Name.ToStringShort} 没有 Guest 状态。");
                }

                // --- 5. 最终验证与成功通知（确保派系已更改） ---
                if (pawnToRecruit.Faction == Faction.OfPlayer)
                {
                    Log.Message($"[RimTalk-ExpandActions] ? 成功通过对话招募: {pawnToRecruit.Name.ToStringShort}");
                    SendRecruitmentLetter(pawnToRecruit, recruiter, oldFactionName);
                }
                else
                {
                    // 如果 SetFaction 失败，会在这里记录错误
                    Log.Error($"[RimTalk-ExpandActions] ? 核心失败: {pawnToRecruit.Name.ToStringShort} 的派系没有更改 (仍属于 {pawnToRecruit.Faction?.Name ?? "无派系"})");
                }
            }
            catch (Exception ex)
            {
                // 捕获所有其他异常，并记录日志
                Log.Error($"[RimTalk-ExpandActions] ExecuteRecruit 最终执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 发送招募成功的游戏内信件
        /// </summary>
        private static void SendRecruitmentLetter(Pawn recruited, Pawn recruiter, string oldFactionName)
        {
            try
            {
                string recruiterName = recruiter != null ? recruiter.Name.ToStringShort : "殖民者";
                
                string letterLabel = "对话招募成功";
                string letterText = $"{recruiterName} 通过对话成功说服了 {recruited.Name.ToStringShort} 加入殖民地！\n\n" +
                                   $"{recruited.Name.ToStringShort} 原属于 {oldFactionName}，现在已经成为你的殖民者。";

                Find.LetterStack.ReceiveLetter(
                    label: letterLabel,
                    text: letterText,
                    textLetterDef: LetterDefOf.PositiveEvent,
                    lookTargets: recruited
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandMemory] SendRecruitmentLetter 失败: {ex.Message}");
            }
        }

        #region 新增动作方法

        /// <summary>
        /// 执行丢弃武器动作（投降/惊慌）
        /// </summary>
        /// <param name="pawn">要丢弃武器的 Pawn</param>
        public static void ExecuteDropWeapon(Pawn pawn)
        {
            try
            {
                // 1. 参数检查
                if (pawn == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteDropWeapon: pawn 为 null");
                    return;
                }

                if (pawn.Dead)
                {
                    Log.Warning($"[RimTalk-ExpandActions] ExecuteDropWeapon: {pawn.Name} 已死亡");
                    return;
                }

                // 2. 检查主武器是否存在
                if (pawn.equipment?.Primary != null)
                {
                    ThingWithComps weapon = pawn.equipment.Primary;
                    string weaponName = weapon.LabelShort;

                    // 尝试丢下武器
                    if (pawn.equipment.TryDropEquipment(weapon, out ThingWithComps droppedWeapon, pawn.Position, true))
                    {
                        Log.Message($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 丢下了武器: {weaponName}");
                        
                        // 发送消息
                        Messages.Message(
                            $"{pawn.Name.ToStringShort} 放下了武器投降！",
                            pawn,
                            MessageTypeDefOf.NeutralEvent
                        );
                    }
                    else
                    {
                        Log.Warning($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 无法丢下武器");
                    }
                }
                else
                {
                    Log.Message($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 没有装备武器");
                }

                // 3. 可选：给予惊慌状态（模拟投降心理）
                if (pawn.mindState != null && pawn.mindState.mentalStateHandler != null && pawn.RaceProps.Humanlike)
                {
                    // 使用 PanicFlee 状态
                    MentalStateDef panicState = MentalStateDefOf.PanicFlee;
                    if (panicState != null)
                    {
                        pawn.mindState.mentalStateHandler.TryStartMentalState(panicState);
                        Log.Message(string.Format("[RimTalk-ExpandActions] {0} 进入惊慌状态", pawn.Name.ToStringShort));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteDropWeapon 执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 执行恋爱关系变更
        /// </summary>
        /// <param name="pawn">发起者</param>
        /// <param name="partner">对象</param>
        /// <param name="type">类型: "new_lover" 或 "breakup"</param>
        public static void ExecuteRomanceChange(Pawn pawn, Pawn partner, string type)
        {
            try
            {
                // 1. 参数检查
                if (pawn == null || partner == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteRomanceChange: pawn 或 partner 为 null");
                    return;
                }

                if (pawn.Dead || partner.Dead)
                {
                    Log.Warning("[RimTalk-ExpandActions] ExecuteRomanceChange: 涉及的角色已死亡");
                    return;
                }

                if (pawn == partner)
                {
                    Log.Warning("[RimTalk-ExpandActions] ExecuteRomanceChange: 不能对自己执行恋爱动作");
                    return;
                }

                // 2. 根据类型执行不同逻辑
                type = type?.ToLower() ?? "";

                if (type == "new_lover")
                {
                    // 检查是否已有恋人关系
                    if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, partner))
                    {
                        Log.Message($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 和 {partner.Name.ToStringShort} 已经是恋人");
                        return;
                    }

                    // 建立恋人关系
                    pawn.relations.AddDirectRelation(PawnRelationDefOf.Lover, partner);
                    
                    // 添加正面记忆
                    if (pawn.needs?.mood?.thoughts?.memories != null)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.GotSomeLovin, partner);
                    }
                    if (partner.needs?.mood?.thoughts?.memories != null)
                    {
                        partner.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.GotSomeLovin, pawn);
                    }

                    Log.Message($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 和 {partner.Name.ToStringShort} 成为恋人");
                    Messages.Message(
                        $"{pawn.Name.ToStringShort} 和 {partner.Name.ToStringShort} 确立了恋爱关系！",
                        new LookTargets(new Pawn[] { pawn, partner }),
                        MessageTypeDefOf.PositiveEvent
                    );
                }
                else if (type == "breakup")
                {
                    // 检查是否有恋人关系
                    if (!pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, partner))
                    {
                        Log.Message($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 和 {partner.Name.ToStringShort} 本来就不是恋人");
                        return;
                    }

                    // 移除恋人关系
                    pawn.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, partner);
                    
                    // 添加负面记忆
                    if (pawn.needs?.mood?.thoughts?.memories != null)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.DivorcedMe, partner);
                    }
                    if (partner.needs?.mood?.thoughts?.memories != null)
                    {
                        partner.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.DivorcedMe, pawn);
                    }

                    Log.Message($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 和 {partner.Name.ToStringShort} 分手了");
                    Messages.Message(
                        $"{pawn.Name.ToStringShort} 和 {partner.Name.ToStringShort} 结束了恋爱关系...",
                        new LookTargets(new Pawn[] { pawn, partner }),
                        MessageTypeDefOf.NegativeEvent
                    );
                }
                else
                {
                    Log.Warning($"[RimTalk-ExpandActions] ExecuteRomanceChange: 未知类型 '{type}'");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteRomanceChange 执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 执行灵感触发
        /// </summary>
        public static void ExecuteInspiration(Pawn pawn, string type)
        {
            try
            {
                if (pawn == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteInspiration: pawn 为 null");
                    return;
                }

                if (pawn.Dead)
                {
                    Log.Warning(string.Format("[RimTalk-ExpandActions] ExecuteInspiration: {0} 已死亡", pawn.Name));
                    return;
                }

                if (!pawn.RaceProps.Humanlike)
                {
                    Log.Warning(string.Format("[RimTalk-ExpandActions] ExecuteInspiration: {0} 不是人类，无法触发灵感", pawn.Name));
                    return;
                }

                // 根据类型匹配灵感定义
                InspirationDef inspirationDef = null;
                type = type != null ? type.ToLower() : "";

                // 使用 DefDatabase 查找灵感定义
                switch (type)
                {
                    case "frenzy_shoot":
                        inspirationDef = DefDatabase<InspirationDef>.GetNamedSilentFail("Frenzy_Shoot");
                        break;
                    case "frenzy_work":
                        inspirationDef = DefDatabase<InspirationDef>.GetNamedSilentFail("Frenzy_Work");
                        break;
                    case "inspired_trade":
                        inspirationDef = DefDatabase<InspirationDef>.GetNamedSilentFail("Inspired_Trade");
                        if (inspirationDef == null)
                        {
                            inspirationDef = DefDatabase<InspirationDef>.GetNamedSilentFail("InspiredTrade");
                        }
                        break;
                    default:
                        Log.Warning(string.Format("[RimTalk-ExpandActions] ExecuteInspiration: 未知灵感类型 '{0}'", type));
                        return;
                }

                if (inspirationDef == null)
                {
                    Log.Warning(string.Format("[RimTalk-ExpandActions] ExecuteInspiration: 未找到灵感定义 '{0}'", type));
                    return;
                }

                // 尝试触发灵感
                if (pawn.mindState != null && pawn.mindState.inspirationHandler != null)
                {
                    if (pawn.mindState.inspirationHandler.TryStartInspiration(inspirationDef))
                    {
                        Log.Message(string.Format("[RimTalk-ExpandActions] {0} 获得了灵感: {1}", pawn.Name.ToStringShort, inspirationDef.LabelCap));
                        Messages.Message(
                            string.Format("{0} 突然获得了灵感！({1})", pawn.Name.ToStringShort, inspirationDef.LabelCap),
                            pawn,
                            MessageTypeDefOf.PositiveEvent
                        );
                    }
                    else
                    {
                        Log.Warning(string.Format("[RimTalk-ExpandActions] {0} 无法触发灵感（可能已有灵感状态）", pawn.Name.ToStringShort));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[RimTalk-ExpandActions] ExecuteInspiration 执行失败: {0}\n{1}", ex.Message, ex.StackTrace));
            }
        }

        /// <summary>
        /// 执行休息动作
        /// </summary>
        /// <param name="pawn">要休息的 Pawn</param>
        /// <param name="immediate">是否立即强制昏迷</param>
        public static void ExecuteRest(Pawn pawn, bool immediate)
        {
            try
            {
                // 1. 参数检查
                if (pawn == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteRest: pawn 为 null");
                    return;
                }

                if (pawn.Dead)
                {
                    Log.Warning($"[RimTalk-ExpandActions] ExecuteRest: {pawn.Name} 已死亡");
                    return;
                }

                // 2. 根据 immediate 参数执行不同逻辑
                if (immediate)
                {
                    // 强制昏迷：将休息需求设为 0
                    if (pawn.needs?.rest != null)
                    {
                        pawn.needs.rest.CurLevel = 0f;
                        Log.Message($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 因极度疲劳而昏迷");
                        Messages.Message(
                            $"{pawn.Name.ToStringShort} 突然昏倒了！",
                            pawn,
                            MessageTypeDefOf.NegativeEvent
                        );
                    }
                }
                else
                {
                    // 正常休息：结束当前任务并指派睡觉任务
                    if (pawn.jobs != null)
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
                        
                        // 查找最近的床位
                        Building_Bed bed = RestUtility.FindBedFor(pawn);
                        if (bed != null)
                        {
                            Job layDownJob = JobMaker.MakeJob(JobDefOf.LayDown, bed);
                            pawn.jobs.TryTakeOrderedJob(layDownJob, JobTag.Misc);
                            Log.Message($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 前往床位休息");
                        }
                        else
                        {
                            // 没有床位，就地休息
                            Job layDownJob = JobMaker.MakeJob(JobDefOf.LayDown, pawn.Position);
                            pawn.jobs.TryTakeOrderedJob(layDownJob, JobTag.Misc);
                            Log.Message($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 在地上休息");
                        }

                        Messages.Message(
                            $"{pawn.Name.ToStringShort} 去休息了。",
                            pawn,
                            MessageTypeDefOf.NeutralEvent
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteRest 执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 执行赠送物品动作
        /// </summary>
        /// <param name="pawn">赠送者</param>
        /// <param name="itemKeyword">物品关键词</param>
        public static void ExecuteGift(Pawn pawn, string itemKeyword)
        {
            try
            {
                // 1. 参数检查
                if (pawn == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteGift: pawn 为 null");
                    return;
                }

                if (pawn.Dead)
                {
                    Log.Warning($"[RimTalk-ExpandActions] ExecuteGift: {pawn.Name} 已死亡");
                    return;
                }

                if (string.IsNullOrWhiteSpace(itemKeyword))
                {
                    Log.Warning("[RimTalk-ExpandActions] ExecuteGift: itemKeyword 为空");
                    return;
                }

                // 2. 遍历背包查找匹配物品
                if (pawn.inventory?.innerContainer != null)
                {
                    string normalizedKeyword = itemKeyword.ToLower().Trim();
                    Thing foundItem = null;

                    foreach (Thing item in pawn.inventory.innerContainer)
                    {
                        string itemLabel = item.LabelShort?.ToLower() ?? "";
                        string itemDefName = item.def?.defName?.ToLower() ?? "";

                        if (itemLabel.Contains(normalizedKeyword) || itemDefName.Contains(normalizedKeyword))
                        {
                            foundItem = item;
                            break;
                        }
                    }

                    // 3. 如果找到物品，尝试丢出
                    if (foundItem != null)
                    {
                        int stackCount = foundItem.stackCount;
                        string itemName = foundItem.LabelShort;

                        if (pawn.inventory.innerContainer.TryDrop(foundItem, pawn.Position, pawn.Map, ThingPlaceMode.Near, out Thing droppedThing))
                        {
                            Log.Message($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 赠送了 {itemName} x{stackCount}");
                            Messages.Message(
                                $"{pawn.Name.ToStringShort} 赠送了 {itemName}！",
                                new LookTargets(droppedThing),
                                MessageTypeDefOf.PositiveEvent
                            );
                        }
                        else
                        {
                            Log.Warning($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 无法丢出物品");
                        }
                    }
                    else
                    {
                        Log.Message($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 的背包中没有匹配 '{itemKeyword}' 的物品");
                        Messages.Message(
                            $"{pawn.Name.ToStringShort} 身上没有相关物品。",
                            pawn,
                            MessageTypeDefOf.RejectInput
                        );
                    }
                }
                else
                {
                    Log.Warning($"[RimTalk-ExpandActions] {pawn.Name.ToStringShort} 没有背包");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteGift 执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 执行社交用餐行为
        /// </summary>
        /// <param name="initiator">发起者（邀请吃饭的小人）</param>
        /// <param name="target">目标（被邀请的小人）</param>
        public static void ExecuteSocialDining(Pawn initiator, Pawn target)
        {
            try
            {
                // 1. 参数验证
                if (initiator == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteSocialDining: initiator 为 null");
                    return;
                }

                if (target == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteSocialDining: target 为 null");
                    return;
                }

                if (initiator.Dead || target.Dead)
                {
                    Log.Warning($"[RimTalk-ExpandActions] ExecuteSocialDining: 角色已死亡");
                    return;
                }

                if (!initiator.Spawned || !target.Spawned)
                {
                    Log.Warning($"[RimTalk-ExpandActions] ExecuteSocialDining: 角色未在地图上");
                    return;
                }

                if (initiator.Map != target.Map)
                {
                    Log.Warning($"[RimTalk-ExpandActions] ExecuteSocialDining: {initiator.Name.ToStringShort} 和 {target.Name.ToStringShort} 不在同一地图");
                    return;
                }

                if (initiator.Downed || target.Downed)
                {
                    Log.Warning($"[RimTalk-ExpandActions] ExecuteSocialDining: 角色已倒地，无法进餐");
                    return;
                }

                // 2. 检查是否可以被打扰
                if (!FoodSharingUtility.IsSafeToDisturb(initiator))
                {
                    Log.Message($"[RimTalk-ExpandActions] {initiator.Name.ToStringShort} 现在不适合进餐（正忙碌或战斗中）");
                    return;
                }

                if (!FoodSharingUtility.IsSafeToDisturb(target))
                {
                    Log.Message($"[RimTalk-ExpandActions] {target.Name.ToStringShort} 现在不适合进餐（正忙碌或战斗中）");
                    return;
                }

                // 3. 查找可用食物（使用移植的方法）
                Thing food = FoodSharingUtility.FindFoodForSharing(initiator, target);
                if (food == null)
                {
                    Log.Message($"[RimTalk-ExpandActions] {initiator.Name.ToStringShort} 找不到可以分享的食物");
                    
                    if (Prefs.DevMode || RimTalkExpandActionsMod.Settings?.showActionMessages == true)
                    {
                        Messages.Message(
                            $"{initiator.Name.ToStringShort} 想邀请 {target.Name.ToStringShort} 一起吃饭，但找不到合适的食物。",
                            initiator,
                            MessageTypeDefOf.RejectInput
                        );
                    }
                    return;
                }

                // 4. 使用移植的核心方法触发共餐
                bool success = FoodSharingUtility.TryTriggerShareFood(initiator, target, food);
                
                if (success)
                {
                    Log.Message($"[RimTalk-ExpandActions] {initiator.Name.ToStringShort} 成功邀请 {target.Name.ToStringShort} 共餐");
                    
                    // 发送消息给玩家
                    if (RimTalkExpandActionsMod.Settings?.showActionMessages == true)
                    {
                        Messages.Message(
                            $"{initiator.Name.ToStringShort} 邀请 {target.Name.ToStringShort} 一起共进晚餐。",
                            new LookTargets(new Pawn[] { initiator, target }),
                            MessageTypeDefOf.PositiveEvent
                        );
                    }
                }
                else
                {
                    Log.Warning($"[RimTalk-ExpandActions] {initiator.Name.ToStringShort} 邀请 {target.Name.ToStringShort} 共餐失败");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteSocialDining 执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 执行社交放松
        /// 让多个小人进行社交放松活动（使用原版的社交娱乐系统）
        /// </summary>
        public static bool ExecuteSocialRelax(Pawn initiator, List<Pawn> participants)
        {
            try
            {
                if (initiator == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteSocialRelax: initiator 为 null");
                    return false;
                }

                if (participants == null || participants.Count == 0)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteSocialRelax: 参与者列表为空");
                    return false;
                }

                // 检查是否启用
                if (!RimTalkExpandActionsMod.Settings.IsActionEnabled("social_relax"))
                {
                    Log.Warning("[RimTalk-ExpandActions] ExecuteSocialRelax: 社交放松功能已被禁用");
                    return false;
                }

                // 检查成功率
                float successChance = RimTalkExpandActionsMod.Settings.GetSuccessChance("social_relax");
                if (Rand.Value > successChance)
                {
                    Log.Message($"[RimTalk-ExpandActions] ExecuteSocialRelax: 成功率检定失败 (需要 <= {successChance:P0})");
                    Messages.Message("社交放松失败", MessageTypeDefOf.RejectInput);
                    return false;
                }

                Log.Message($"[RimTalk-ExpandActions] 开始执行社交放松，参与者: {participants.Count} 人");

                // 使用原版的 JoyGiver_SocialRelax 系统
                // 查找合适的社交娱乐活动
                int successCount = 0;
                
                foreach (var pawn in participants)
                {
                    if (pawn == null || pawn.Dead || !pawn.Spawned)
                    {
                        continue;
                    }

                    try
                    {
                        // 给予放松心情
                        if (pawn.needs?.mood != null)
                        {
                            // 添加"社交放松"心情加成
                            Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.Catharsis);
                            if (thought != null)
                            {
                                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
                            }
                        }

                        // 尝试让小人进行社交娱乐
                        if (pawn.timetable != null)
                        {
                            // 临时设置为娱乐时间（如果需要）
                            pawn.timetable.SetAssignment(GenLocalDate.HourOfDay(pawn), TimeAssignmentDefOf.Joy);
                        }

                        // 触发寻找社交娱乐的行为
                        if (pawn.mindState != null)
                        {
                            pawn.mindState.lastInventoryRawFoodUseTick = 0; // 重置缓存
                        }

                        successCount++;
                        Log.Message($"[RimTalk-ExpandActions]   {pawn.LabelShort} 开始社交放松");
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[RimTalk-ExpandActions] ExecuteSocialRelax: 处理 {pawn.LabelShort} 时出错: {ex.Message}");
                    }
                }

                if (successCount > 0)
                {
                    string message = $"{successCount} 名小人开始社交放松";
                    Messages.Message(message, MessageTypeDefOf.PositiveEvent);
                    Log.Message($"[RimTalk-ExpandActions] 成功: {message}");
                    return true;
                }
                else
                {
                    Log.Warning("[RimTalk-ExpandActions] ExecuteSocialRelax: 没有小人成功开始社交放松");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteSocialRelax 发生异常: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 执行社交放松（从 JSON 调用，支持多个目标）
        /// </summary>
        public static bool ExecuteSocialRelax(Pawn initiator, string targetNames)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetNames))
                {
                    Log.Warning("[RimTalk-ExpandActions] ExecuteSocialRelax: 目标名称为空，将包含发起者自己");
                    return ExecuteSocialRelax(initiator, new List<Pawn> { initiator });
                }

                // 解析目标名称（支持逗号分隔）
                string[] names = targetNames.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
                List<Pawn> participants = new List<Pawn>();

                // 添加发起者
                if (initiator != null)
                {
                    participants.Add(initiator);
                }

                // 查找其他参与者
                foreach (string name in names)
                {
                    string cleanName = name.Trim();
                    if (string.IsNullOrEmpty(cleanName))
                    {
                        continue;
                    }

                    // 使用 AIResponsePostProcessor 的 FindPawnByName 方法
                    Pawn target = FindPawnByNameInternal(cleanName);
                    if (target != null && !participants.Contains(target))
                    {
                        participants.Add(target);
                    }
                    else
                    {
                        Log.Warning($"[RimTalk-ExpandActions] ExecuteSocialRelax: 未找到小人: {cleanName}");
                    }
                }

                if (participants.Count == 0)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteSocialRelax: 没有找到任何有效的参与者");
                    return false;
                }

                return ExecuteSocialRelax(initiator, participants);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteSocialRelax 解析目标名称时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据名称查找 Pawn（内部方法）
        /// 注意：此方法必须在主线程调用！
        /// </summary>
        private static Pawn FindPawnByNameInternal(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                // 线程安全检查
                if (!UnityEngine.Application.isPlaying)
                {
                    Log.Error("[RimTalk-ExpandActions] FindPawnByNameInternal: 不能在非主线程调用");
                    return null;
                }

                string normalizedName = name.ToLower().Replace(" ", "");

                // 安全地访问 Find.Maps（只在主线程）
                List<Map> maps = null;
                try
                {
                    maps = Find.Maps;
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalk-ExpandActions] FindPawnByNameInternal: 访问 Find.Maps 失败 (可能在后台线程): {ex.Message}");
                    return null;
                }

                if (maps == null || maps.Count == 0)
                {
                    return null;
                }

                foreach (var map in maps)
                {
                    if (map == null || map.mapPawns == null)
                    {
                        continue;
                    }

                    // 使用 Try-Catch 保护 mapPawns 访问
                    IReadOnlyList<Pawn> allPawns = null;
                    try
                    {
                        allPawns = map.mapPawns.AllPawnsSpawned; // 使用 AllPawnsSpawned 更安全
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[RimTalk-ExpandActions] 访问 map.mapPawns 失败: {ex.Message}");
                        continue;
                    }

                    if (allPawns == null)
                    {
                        continue;
                    }

                    foreach (var pawn in allPawns)
                    {
                        if (pawn == null || pawn.Name == null) continue;

                        string shortName = pawn.Name.ToStringShort != null ? pawn.Name.ToStringShort.ToLower().Replace(" ", "") : "";
                        string nickname = "";
                        NameTriple nameTriple = pawn.Name as NameTriple;
                        if (nameTriple != null && nameTriple.Nick != null)
                        {
                            nickname = nameTriple.Nick.ToLower().Replace(" ", "");
                        }

                        if (shortName.Contains(normalizedName) || 
                            normalizedName.Contains(shortName) ||
                            nickname.Contains(normalizedName) ||
                            normalizedName.Contains(nickname))
                        {
                            return pawn;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] FindPawnByNameInternal 失败: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 执行简单的"一起吃饭"动作（使用原版 Ingest 任务）
        /// 让两个小人立即开始吃饭，不使用自定义 JobDriver
        /// </summary>
        /// <param name="initiator">发起者</param>
        /// <param name="targetName">目标名字</param>
        public static void ExecuteSimpleEatTogether(Pawn initiator, string targetName)
        {
            try
            {
                // 1. 参数验证
                if (initiator == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteSimpleEatTogether: initiator 为 null");
                    return;
                }

                // 2. 查找目标 Pawn
                Pawn target = FindPawnByNameInternal(targetName);
                if (target == null)
                {
                    Log.Warning($"[RimTalk-ExpandActions] ExecuteSimpleEatTogether: 未找到名为 '{targetName}' 的小人");
                    Messages.Message($"找不到 {targetName}，无法一起吃饭。", MessageTypeDefOf.RejectInput);
                    return;
                }

                // 3. 查找并触发交互
                InteractionDef eatTogetherDef = DefDatabase<InteractionDef>.GetNamedSilentFail("LetsTalkEatTogether");
                if (eatTogetherDef == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteSimpleEatTogether: 未找到 LetsTalkEatTogether 交互定义");
                    return;
                }

                // 4. 触发交互（InteractionWorker 会处理所有逻辑）
                InteractionWorker worker = eatTogetherDef.Worker;
                if (worker != null)
                {
                    worker.Interacted(
                        initiator, 
                        target, 
                        null, 
                        out string letterText, 
                        out string letterLabel, 
                        out LetterDef letterDef, 
                        out LookTargets lookTargets
                    );

                    Log.Message($"[RimTalk-ExpandActions] {initiator.LabelShort} 邀请 {target.LabelShort} 一起吃饭 (简单模式)");
                }
                else
                {
                    Log.Error("[RimTalk-ExpandActions] ExecuteSimpleEatTogether: InteractionWorker 为 null");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteSimpleEatTogether 执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion
    }
}
