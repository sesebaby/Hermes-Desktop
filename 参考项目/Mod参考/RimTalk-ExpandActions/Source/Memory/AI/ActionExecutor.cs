using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using RimTalkExpandActions.Memory.Actions;
using UnityEngine;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 动作执行器：意图名称 -> 游戏动作的映射
    /// 负责识别意图并执行对应的游戏效果
    /// </summary>
    public static class ActionExecutor
    {
        /// <summary>
        /// 执行意图对应的游戏动作
        /// </summary>
        /// <param name="intentName">意图名称（如 "recruit_agree"）</param>
        /// <param name="speaker">说话者（AI 角色）</param>
        /// <param name="listener">听众（玩家角色）</param>
        public static void Execute(string intentName, Pawn speaker, Pawn listener)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(intentName))
                {
                    Log.Error("[RimTalk-ExpandActions] ActionExecutor: intentName 为空");
                    return;
                }

                if (speaker == null)
                {
                    Log.Error("[RimTalk-ExpandActions] ActionExecutor: speaker 为 null");
                    return;
                }

                // listener 可以为 null，各个执行方法内部会自动查找
                Log.Message($"[RimTalk-ExpandActions] ActionExecutor: 执行意图 '{intentName}', speaker={speaker.Name.ToStringShort}, listener={listener?.Name?.ToStringShort ?? "null(将自动查找)"}");

                // 根据意图名称分发到对应的处理逻辑
                switch (intentName.ToLower())
                {
                    case "recruit_agree":
                        ExecuteRecruitAgree(speaker, listener);
                        break;

                    case "romance_accept":
                        ExecuteRomanceAccept(speaker, listener);
                        break;

                    case "romance_breakup":
                        ExecuteRomanceBreakup(speaker, listener);
                        break;

                    case "force_rest":
                        ExecuteForceRest(speaker, listener);
                        break;

                    case "inspire_fight":
                        ExecuteInspireFight(speaker, listener);
                        break;

                    case "inspire_work":
                        ExecuteInspireWork(speaker, listener);
                        break;

                    case "give_item":
                        ExecuteGiveItem(speaker, listener);
                        break;

                    case "social_dining":
                        ExecuteSocialDining(speaker, listener);
                        break;

                    case "social_relax":
                        ExecuteSocialRelax(speaker, listener);
                        break;

                    default:
                        Log.Warning($"[RimTalk-ExpandActions] ActionExecutor: 未知的意图名称 '{intentName}'");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ActionExecutor.Execute 失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 执行"同意招募"意图
        /// </summary>
        private static void ExecuteRecruitAgree(Pawn speaker, Pawn listener)
        {
            try
            {
                // 检查说话者是否已属于玩家派系
                if (speaker.Faction == Faction.OfPlayer)
                {
                    Log.Message($"[RimTalk-ExpandActions] {speaker.Name.ToStringShort} 已经属于玩家殖民地，无需再招募");
                    return;
                }

                // 如果没有 listener，尝试查找一个
                if (listener == null)
                {
                    listener = FindAnyColonist(speaker);
                    if (listener == null)
                    {
                        Log.Warning($"[RimTalk-ExpandActions] 无法找到招募者，招募失败");
                        return;
                    }
                    Log.Message($"[RimTalk-ExpandActions] 自动选择招募者: {listener.Name.ToStringShort}");
                }

                // 执行招募逻辑
                Log.Message($"[RimTalk-ExpandActions] ★ 即将触发招募: {speaker.Name.ToStringShort} 同意加入");
                RimTalkActions.ExecuteRecruit(speaker, listener);

                // 显示视觉反馈
                ShowVisualFeedback(speaker, "[同意加入]", Color.green);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteRecruitAgree 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行"接受浪漫"意图
        /// </summary>
        private static void ExecuteRomanceAccept(Pawn speaker, Pawn listener)
        {
            try
            {
                // 如果没有 listener，尝试查找一个
                if (listener == null)
                {
                    listener = FindAnyColonist(speaker);
                    if (listener == null)
                    {
                        Log.Warning($"[RimTalk-ExpandActions] 无法找到浪漫对象，操作失败");
                        return;
                    }
                    Log.Message($"[RimTalk-ExpandActions] 自动选择浪漫对象: {listener.Name.ToStringShort}");
                }

                // 检查是否已经是恋人
                if (speaker.relations.DirectRelationExists(PawnRelationDefOf.Lover, listener))
                {
                    Log.Message($"[RimTalk-ExpandActions] {speaker.Name.ToStringShort} 和 {listener.Name.ToStringShort} 已经是恋人");
                    return;
                }

                // 执行浪漫关系建立（listener 是发起者，speaker 是接受者）
                Log.Message($"[RimTalk-ExpandActions] ★ 即将触发浪漫: {speaker.Name.ToStringShort} 接受表白");
                RimTalkActions.ExecuteRomanceChange(listener, speaker, "new_lover");

                // 显示视觉反馈（粉色心形）
                ShowVisualFeedback(speaker, "[心动]", new Color(1f, 0.5f, 0.5f));
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteRomanceAccept 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行"分手"意图
        /// </summary>
        private static void ExecuteRomanceBreakup(Pawn speaker, Pawn listener)
        {
            try
            {
                if (listener == null)
                {
                    listener = FindAnyColonist(speaker);
                    if (listener == null)
                    {
                        Log.Warning($"[RimTalk-ExpandActions] 无法找到分手对象，操作失败");
                        return;
                    }
                }

                // 检查是否有恋人关系
                if (!speaker.relations.DirectRelationExists(PawnRelationDefOf.Lover, listener))
                {
                    Log.Message($"[RimTalk-ExpandActions] {speaker.Name.ToStringShort} 和 {listener.Name.ToStringShort} 不是恋人关系");
                    return;
                }

                Log.Message($"[RimTalk-ExpandActions] ★ 即将触发分手: {speaker.Name.ToStringShort} 提出分手");
                RimTalkActions.ExecuteRomanceChange(speaker, listener, "breakup");

                ShowVisualFeedback(speaker, "[分手]", new Color(0.7f, 0.7f, 0.7f));
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteRomanceBreakup 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行"强制休息"意图
        /// </summary>
        private static void ExecuteForceRest(Pawn speaker, Pawn listener)
        {
            try
            {
                Log.Message($"[RimTalk-ExpandActions] ★ 即将触发休息: {speaker.Name.ToStringShort} 去休息");
                RimTalkActions.ExecuteRest(speaker, false); // immediate = false，不会立刻倒地

                ShowVisualFeedback(speaker, "[去休息]", new Color(0.6f, 0.8f, 1f));
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteForceRest 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行"战斗灵感"意图
        /// </summary>
        private static void ExecuteInspireFight(Pawn speaker, Pawn listener)
        {
            try
            {
                Log.Message($"[RimTalk-ExpandActions] ★ 即将触发战斗灵感: {speaker.Name.ToStringShort}");
                RimTalkActions.ExecuteInspiration(speaker, "frenzy_shoot"); // 战斗狂

                ShowVisualFeedback(speaker, "[战意昂扬]", new Color(1f, 0.3f, 0.3f));
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteInspireFight 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行"工作灵感"意图
        /// </summary>
        private static void ExecuteInspireWork(Pawn speaker, Pawn listener)
        {
            try
            {
                Log.Message($"[RimTalk-ExpandActions] ★ 即将触发工作灵感: {speaker.Name.ToStringShort}");
                RimTalkActions.ExecuteInspiration(speaker, "frenzy_work"); // 工作狂

                ShowVisualFeedback(speaker, "[干劲十足]", new Color(1f, 0.8f, 0.2f));
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteInspireWork 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行"赠送物品"意图
        /// </summary>
        private static void ExecuteGiveItem(Pawn speaker, Pawn listener)
        {
            try
            {
                if (listener == null)
                {
                    listener = FindAnyColonist(speaker);
                    if (listener == null)
                    {
                        Log.Warning($"[RimTalk-ExpandActions] 无法找到接收者，赠送失败");
                        return;
                    }
                }

                Log.Message($"[RimTalk-ExpandActions] ★ 即将触发赠送: {speaker.Name.ToStringShort} 赠送物品");
                // 使用通用关键字，让 ExecuteGift 自动选择物品
                RimTalkActions.ExecuteGift(speaker, ""); // 空字符串表示自动选择物品

                ShowVisualFeedback(speaker, "[赠礼]", new Color(1f, 0.7f, 1f));
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteGiveItem 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行"社交聚餐"意图
        /// </summary>
        private static void ExecuteSocialDining(Pawn speaker, Pawn listener)
        {
            try
            {
                if (listener == null)
                {
                    listener = FindAnyColonist(speaker);
                    if (listener == null)
                    {
                        Log.Warning($"[RimTalk-ExpandActions] 无法找到聚餐伙伴，执行失败");
                        return;
                    }
                }

                Log.Message($"[RimTalk-ExpandActions] ★ 即将触发聚餐: {speaker.Name.ToStringShort} 和 {listener.Name.ToStringShort}");
                RimTalkActions.ExecuteSocialDining(speaker, listener);

                ShowVisualFeedback(speaker, "[一起吃饭]", new Color(1f, 0.9f, 0.5f));
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteSocialDining 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行"社交休闲"意图
        /// </summary>
        private static void ExecuteSocialRelax(Pawn speaker, Pawn listener)
        {
            try
            {
                // 构建参与者列表
                List<Pawn> participants = new List<Pawn> { speaker };
                
                if (listener != null)
                {
                    participants.Add(listener);
                }
                else
                {
                    // 如果没有 listener，尝试找一个
                    Pawn partner = FindAnyColonist(speaker);
                    if (partner != null)
                    {
                        participants.Add(partner);
                    }
                }

                if (participants.Count < 2)
                {
                    Log.Warning($"[RimTalk-ExpandActions] 休闲需要 2 人以上进行社交休闲");
                    return;
                }

                Log.Message($"[RimTalk-ExpandActions] ★ 即将触发休闲: {participants.Count} 人");
                RimTalkActions.ExecuteSocialRelax(speaker, participants);

                ShowVisualFeedback(speaker, "[放松]", new Color(0.5f, 1f, 0.8f));
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ExecuteSocialRelax 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示视觉反馈（飘字效果）
        /// </summary>
        private static void ShowVisualFeedback(Pawn pawn, string text, Color color)
        {
            try
            {
                if (pawn == null || !pawn.Spawned)
                {
                    return;
                }

                // 使用 MoteMaker.ThrowText 显示飘字
                MoteMaker.ThrowText(
                    pawn.DrawPos + new Vector3(0f, 0f, 0.5f),
                    pawn.Map,
                    text,
                    color,
                    3.5f
                );

                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[RimTalk-ExpandActions] 显示视觉反馈: {text} (颜色: {color})");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ShowVisualFeedback 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找最近的殖民者，作为 listener 的备选项
        /// v2.7: 增强验证，排除无效角色
        /// </summary>
        private static Pawn FindAnyColonist(Pawn speaker)
        {
            try
            {
                if (speaker?.Map == null)
                {
                    return null;
                }

                // 查找最近的殖民者
                Pawn nearest = null;
                float minDistance = float.MaxValue;

                foreach (Pawn colonist in speaker.Map.mapPawns.FreeColonistsSpawned)
                {
                    // v2.7: 使用增强的验证
                    if (!IsValidColonistForAction(colonist, speaker))
                    {
                        continue;
                    }

                    float distance = speaker.Position.DistanceTo(colonist.Position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = colonist;
                    }
                }

                return nearest;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] FindAnyColonist 失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// v2.7: 验证殖民者是否有效为动作目标
        /// </summary>
        private static bool IsValidColonistForAction(Pawn colonist, Pawn speaker)
        {
            if (colonist == null || colonist == speaker)
            {
                return false;
            }

            // 排除死亡或倒地的角色
            if (colonist.Dead || colonist.Downed)
            {
                return false;
            }

            // 检查是否在地图上
            if (!colonist.Spawned)
            {
                return false;
            }

            // v2.7: 排除囚犯
            if (colonist.IsPrisoner)
            {
                return false;
            }

            // v2.7: 排除奴隶
            if (colonist.IsSlaveOfColony)
            {
                return false;
            }

            // v2.7: 排除精神状态异常
            if (colonist.InMentalState)
            {
                return false;
            }

            return true;
        }
    }
}
