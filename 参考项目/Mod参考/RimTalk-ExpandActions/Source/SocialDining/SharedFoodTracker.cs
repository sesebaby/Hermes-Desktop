using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimTalkExpandActions.SocialDining
{
    /// <summary>
    /// 共享食物追踪器 - 支持多人同时吃同一份食物
    /// 核心功能：
    /// 1. 线程安全的多人注册/注销
    /// 2. "幸存者销毁"逻辑 - 只有最后一个吃完的人才销毁食物
    /// 3. 防止食物在共享期间被意外销毁
    /// </summary>
    public class SharedFoodTracker : ThingComp
    {
        // 使用 HashSet 存储所有正在吃这份食物的 Pawn
        private HashSet<Pawn> activePawns = new HashSet<Pawn>();
        private int initialStackCount = -1;
        private bool isBeingShared = false;

        public bool IsBeingShared => isBeingShared;
        public int ActiveEatersCount
        {
            get
            {
                lock (activePawns)
                {
                    return activePawns.Count;
                }
            }
        }

        public CompProperties_SharedFoodTracker Props => (CompProperties_SharedFoodTracker)props;

        /// <summary>
        /// 注册一个 Pawn 开始吃这份食物
        /// 线程安全 - 支持多人同时注册
        /// </summary>
        public void RegisterEater(Pawn pawn)
        {
            if (pawn == null) return;

            lock (activePawns)
            {
                if (activePawns.Count == 0)
                {
                    // 第一个用餐者 - 记录初始堆叠数量
                    initialStackCount = parent.stackCount;
                    isBeingShared = true;
                }

                activePawns.Add(pawn);
                
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[SharedFoodTracker] 注册用餐者 {pawn.LabelShort}，当前共 {activePawns.Count} 人");
                }
            }
        }

        /// <summary>
        /// 注销一个 Pawn 完成用餐
        /// 返回 true 表示这是最后一个用餐者（应该销毁食物）
        /// </summary>
        public bool UnregisterEater(Pawn pawn)
        {
            if (pawn == null) return false;

            bool isLastEater = false;
            lock (activePawns)
            {
                activePawns.Remove(pawn);
                
                if (activePawns.Count == 0)
                {
                    // 最后一个用餐者完成
                    isBeingShared = false;
                    isLastEater = true;
                }
                
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[SharedFoodTracker] 注销用餐者 {pawn.LabelShort}，剩余 {activePawns.Count} 人，isLastEater={isLastEater}");
                }
            }

            return isLastEater;
        }

        /// <summary>
        /// 检查特定 Pawn 是否已注册为用餐者
        /// </summary>
        public bool IsEaterRegistered(Pawn pawn)
        {
            lock (activePawns)
            {
                return activePawns.Contains(pawn);
            }
        }

        /// <summary>
        /// 是否应该阻止堆叠数量减少
        /// 当有多个用餐者时，只有最后一个会真正消耗食物
        /// </summary>
        public bool ShouldPreventConsumption()
        {
            lock (activePawns)
            {
                // 如果还有多个用餐者，阻止消耗
                return activePawns.Count > 1;
            }
        }

        /// <summary>
        /// 存档支持 - 保存/加载状态
        /// </summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            
            // 将 HashSet 转换为 List 用于序列化
            List<Pawn> pawnList = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                lock (activePawns)
                {
                    pawnList = new List<Pawn>(activePawns);
                }
            }
            
            Scribe_Collections.Look(ref pawnList, "activePawns", LookMode.Reference);
            Scribe_Values.Look(ref initialStackCount, "initialStackCount", -1);
            Scribe_Values.Look(ref isBeingShared, "isBeingShared", false);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                lock (activePawns)
                {
                    activePawns.Clear();
                    if (pawnList != null)
                    {
                        foreach (var pawn in pawnList)
                        {
                            if (pawn != null)
                                activePawns.Add(pawn);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 清理 - 组件被销毁时
        /// </summary>
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            lock (activePawns)
            {
                activePawns.Clear();
            }
        }
    }

    public class CompProperties_SharedFoodTracker : CompProperties
    {
        public int shareDurationTicks = 2500 * 10; // 10 分钟

        public CompProperties_SharedFoodTracker()
        {
            compClass = typeof(SharedFoodTracker);
        }
    }
}
