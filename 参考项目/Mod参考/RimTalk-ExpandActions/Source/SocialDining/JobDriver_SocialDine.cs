using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalkExpandActions.SocialDining
{
    /// <summary>
    /// 社交共餐工作驱动
    /// 逻辑流程：
    /// 1. 检查食物状态，决定谁负责搬运
    /// 2. 搬运者去拿食物，搬到桌子
    /// 3. 非搬运者直接去桌子等待
    /// 4. 双方一起进食
    /// </summary>
    public class JobDriver_SocialDine : JobDriver
    {
        private const TargetIndex FoodInd = TargetIndex.A;
        private const TargetIndex TableInd = TargetIndex.B;
        private const TargetIndex PartnerInd = TargetIndex.C;

        private Thing Food => job.GetTarget(FoodInd).Thing;
        private Thing Table => job.targetB.HasThing ? job.targetB.Thing : null;
        private Pawn Partner => (Pawn)job.GetTarget(PartnerInd).Thing;

        // 追踪是否注册到 SharedFoodTracker
        private bool isRegisteredWithTracker = false;

        /// <summary>
        /// 尝试预订资源
        /// </summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 1. 尝试预订食物
            // 如果 Partner 已经预订了食物，我们允许这种情况（由 Partner 搬运）
            if (!pawn.Reserve(Food, job, 1, -1, null, false))
            {
                Pawn reserver = pawn.Map?.reservationManager?.FirstRespectedReserver(Food, pawn);
                if (reserver != Partner)
                {
                    // 被其他人预订了
                    if (errorOnFailed)
                    {
                        Log.Warning($"[SocialDine] {pawn.LabelShort} 无法预订食物，已被 {reserver?.LabelShort ?? "未知"} 预订");
                    }
                    return false;
                }
                // 是 Partner 预订的，我们可以继续，只需要预订椅子
            }

            // 2. 预订桌子/椅子 (TargetB)
            // 注意：两个人共用同一张桌子，所以 maxPawns 需要至少为 2
            // stackCount 对于建筑物应该设为 0（不适用），否则会产生警告
            if (job.targetB.IsValid)
            {
                int maxPawns = 2; // 允许两人共用桌子
                if (job.targetB.HasThing && job.targetB.Thing.def?.surfaceType == SurfaceType.Eat)
                {
                    maxPawns = 8; // 餐桌可能容纳更多人
                }
                // stackCount = 0 表示不适用（用于建筑物），避免 "maxPawns > 1 and stackCount = All" 警告
                if (!pawn.Reserve(job.targetB, job, maxPawns, 0, null, errorOnFailed))
                {
                    return false;
                }
            }

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // --- Toil 0: 失败条件 ---
            // 注意：不使用 FailOnDestroyedNullOrForbidden(FoodInd)，
            // 因为它会检查 !thing.Spawned，而食物可能正在被 Partner 携带
            // 我们只检查食物是否为 null 或已销毁
            this.FailOn(() => Partner == null || Partner.Dead || !Partner.Spawned);
            this.FailOn(() => {
                if (Food == null || Food.Destroyed) return true;
                // 如果食物在 Partner 或我身上，那是正常的
                if (IsFoodWithPartner() || IsFoodWithMe()) return false;
                // 如果食物 Spawned 在地图上，那也是正常的
                if (Food.Spawned) return false;
                // 其他情况（食物消失了但没销毁）视为失败
                return true;
            });
            
            // 确保任务结束时清理 Tracker
            this.AddFinishAction((JobCondition condition) => CleanupTracker());

            // --- Toil 1: 注册到 Tracker ---
            yield return Toils_General.Do(delegate
            {
                if (Food != null && !Food.Destroyed)
                {
                    ThingWithComps foodWithComps = Food as ThingWithComps;
                    if (foodWithComps != null)
                    {
                        SharedFoodTracker tracker = foodWithComps.TryGetComp<SharedFoodTracker>();
                        if (tracker != null && !isRegisteredWithTracker)
                        {
                            tracker.RegisterEater(pawn);
                            isRegisteredWithTracker = true;
                        }
                    }
                }
            });

            // 定义后续 Toils 以便跳转
            // 注意：如果没有桌子，我们使用 GotoCell 作为备用
            Toil gotoTable;
            if (Table != null)
            {
                gotoTable = Toils_Goto.GotoThing(TableInd, PathEndMode.OnCell);
            }
            else
            {
                // 没有桌子时，去 Partner 附近
                gotoTable = new Toil
                {
                    initAction = delegate
                    {
                        if (Partner != null && Partner.Spawned)
                        {
                            pawn.pather.StartPath(Partner.Position, PathEndMode.Touch);
                        }
                    },
                    defaultCompleteMode = ToilCompleteMode.PatherArrival
                };
            }
            
            // 延迟创建 eatToil，确保在需要时 Food 有效
            Toil eatToil = null;

            // --- Toil 2: 决策逻辑 (谁去搬运) ---
            yield return Toils_General.Do(delegate
            {
                bool foodWithPartner = IsFoodWithPartner();
                bool foodWithMe = IsFoodWithMe();
                bool partnerReserved = pawn.Map.reservationManager.ReservedBy(Food, Partner);

                // 如果食物在 Partner 那里，或者 Partner 预订了（且不在我这里），我去桌子等
                if (foodWithPartner || (partnerReserved && !foodWithMe))
                {
                    pawn.jobs.curDriver.JumpToToil(gotoTable);
                }
                // 如果食物在我这里（已经在搬运），直接去桌子
                else if (pawn.carryTracker.CarriedThing == Food)
                {
                    pawn.jobs.curDriver.JumpToToil(gotoTable);
                }
                // 否则，我去拿食物 (继续执行下一个 Toil)
            });

            // --- Toil 3: 去食物位置 ---
            yield return Toils_Goto.GotoThing(FoodInd, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(FoodInd);

            // --- Toil 4: 拿起食物 ---
            yield return Toils_Haul.StartCarryThing(FoodInd, false, true)
                .FailOnDestroyedNullOrForbidden(FoodInd);

            // --- Toil 5: 去桌子 (所有人都执行) ---
            yield return gotoTable;

            // --- Toil 6: 放下食物 (如果拿着) ---
            yield return new Toil
            {
                initAction = delegate
                {
                    if (pawn.carryTracker.CarriedThing == Food)
                    {
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out Thing _);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };

            // --- Toil 7: 等待食物 (如果还没到) ---
            // 如果食物不在附近（比如 Partner 还没搬过来），等待
            int waitTimeout = 2500; // 约40秒
            Toil waitForFood = new Toil
            {
                initAction = delegate
                {
                    pawn.pather.StopDead();
                    pawn.jobs.curDriver.ticksLeftThisToil = waitTimeout; // 手动设置超时计数器
                },
                tickAction = delegate
                {
                    if (Partner != null && Partner.Spawned) pawn.rotationTracker.FaceTarget(Partner);
                    pawn.jobs.curDriver.ticksLeftThisToil--; // 手动递减
                },
                defaultCompleteMode = ToilCompleteMode.Never
            };
            
            // 结束条件：食物就在附近（在地图上或者在我/Partner手中且我们都在桌子旁）
            waitForFood.AddEndCondition(delegate
            {
                // 食物在地图上且距离足够近
                if (Food != null && Food.Spawned && (Food.Position == pawn.Position || Food.Position.DistanceTo(pawn.Position) < 2f))
                {
                    return JobCondition.Succeeded;
                }
                // 食物在 Partner 手中，且 Partner 已经到桌子附近
                if (IsFoodWithPartner() && Partner != null && Partner.Spawned && Partner.Position.DistanceTo(pawn.Position) < 3f)
                {
                    return JobCondition.Succeeded;
                }
                // 超时检查
                if (pawn.jobs.curDriver.ticksLeftThisToil <= 0)
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            
            // 如果食物已经到位，跳过等待
            Func<bool> foodIsNearby = () =>
            {
                if (Food == null) return false;
                if (Food.Spawned && (Food.Position == pawn.Position || Food.Position.DistanceTo(pawn.Position) < 2f))
                {
                    return true;
                }
                if (IsFoodWithPartner() && Partner != null && Partner.Spawned && Partner.Position.DistanceTo(pawn.Position) < 3f)
                {
                    return true;
                }
                return false;
            };
            
            // --- Toil 8: 进食准备 ---
            // 在这里创建 eatToil，确保 Food 有效
            Toil prepareEating = new Toil
            {
                initAction = delegate
                {
                    // 在这里创建实际的进食 Toil
                    eatToil = MakeEatingToilSafe();
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            
            yield return Toils_Jump.JumpIf(prepareEating, foodIsNearby);
            yield return waitForFood;

            // --- Toil 9: 进食准备（确保在等待后也创建 eatToil）---
            yield return prepareEating;
            
            // --- Toil 10: 进食 ---
            // 使用自定义 Toil 来执行实际的进食逻辑
            yield return MakeActualEatingToil();

            // --- Toil 9: 结束清理 ---
            yield return new Toil
            {
                initAction = delegate
                {
                    CleanupTracker();
                    if (Partner != null && !Partner.Dead && pawn.needs?.mood?.thoughts?.memories != null)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(SocialDiningDefOf.AteWithColonist, Partner);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }

        private bool IsFoodWithPartner()
        {
            if (Food == null || Partner == null) return false;
            return Food.ParentHolder == Partner || 
                   (Food.ParentHolder is Pawn_InventoryTracker inv && inv.pawn == Partner) || 
                   (Food.ParentHolder is Pawn_CarryTracker carry && carry.pawn == Partner);
        }

        private bool IsFoodWithMe()
        {
            if (Food == null) return false;
            return Food.ParentHolder == pawn || 
                   (Food.ParentHolder is Pawn_InventoryTracker inv && inv.pawn == pawn) || 
                   (Food.ParentHolder is Pawn_CarryTracker carry && carry.pawn == pawn);
        }

        // 缓存的进食参数
        private float cachedNutritionPerTick = 0f;
        private int cachedTicksToEat = 500; // 默认值
        
        /// <summary>
        /// 安全地创建进食 Toil（在进食前调用，此时 Food 应该有效）
        /// </summary>
        private Toil MakeEatingToilSafe()
        {
            // 计算并缓存进食参数
            if (Food != null && Food.def != null)
            {
                float nutritionTotal = FoodUtility.GetNutrition(pawn, Food, Food.def);
                cachedTicksToEat = (int)(nutritionTotal * 1600f);
                if (cachedTicksToEat <= 0) cachedTicksToEat = 500; // 防止除以零
                cachedNutritionPerTick = nutritionTotal / cachedTicksToEat;
            }
            else
            {
                // 使用默认值
                cachedTicksToEat = 500;
                cachedNutritionPerTick = 0.5f / cachedTicksToEat;
            }
            
            return new Toil { defaultCompleteMode = ToilCompleteMode.Instant };
        }
        
        /// <summary>
        /// 创建实际执行进食的 Toil
        /// </summary>
        private Toil MakeActualEatingToil()
        {
            Toil eatFood = new Toil
            {
                initAction = delegate
                {
                    pawn.pather.StopDead();
                    // 重新计算以防参数未缓存
                    if (cachedTicksToEat <= 0 && Food != null && Food.def != null)
                    {
                        float nutritionTotal = FoodUtility.GetNutrition(pawn, Food, Food.def);
                        cachedTicksToEat = (int)(nutritionTotal * 1600f);
                        if (cachedTicksToEat <= 0) cachedTicksToEat = 500;
                        cachedNutritionPerTick = nutritionTotal / cachedTicksToEat;
                    }
                    pawn.jobs.curDriver.ticksLeftThisToil = cachedTicksToEat;
                },
                tickAction = delegate
                {
                    if (Partner != null && Partner.Spawned)
                    {
                        pawn.rotationTracker.FaceTarget(Partner);
                    }
                    
                    if (pawn.needs?.food != null && cachedNutritionPerTick > 0)
                    {
                        pawn.needs.food.CurLevel += cachedNutritionPerTick;
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = cachedTicksToEat > 0 ? cachedTicksToEat : 500,
                handlingFacing = true
            };
            
            // 安全地添加效果
            if (Food?.def?.ingestible?.ingestEffect != null)
            {
                eatFood.WithEffect(() => Food?.def?.ingestible?.ingestEffect, FoodInd);
            }
            
            // 安全地添加进度条
            eatFood.WithProgressBar(FoodInd, () =>
            {
                int totalTicks = cachedTicksToEat > 0 ? cachedTicksToEat : 500;
                return 1f - (float)pawn.jobs.curDriver.ticksLeftThisToil / totalTicks;
            }, interpolateBetweenActorAndTarget: false);
            
            if (Food?.def?.ingestible?.ingestSound != null)
            {
                eatFood.PlaySustainerOrSound(() => Food?.def?.ingestible?.ingestSound);
            }
            
            return eatFood;
        }

        private void CleanupTracker()
        {
            if (isRegisteredWithTracker && Food != null && !Food.Destroyed)
            {
                ThingWithComps foodWithComps = Food as ThingWithComps;
                if (foodWithComps != null)
                {
                    SharedFoodTracker tracker = foodWithComps.TryGetComp<SharedFoodTracker>();
                    if (tracker != null)
                    {
                        bool isLastEater = tracker.UnregisterEater(pawn);
                        isRegisteredWithTracker = false;
                        
                        if (isLastEater && !Food.Destroyed)
                        {
                            Food.Destroy(DestroyMode.Vanish);
                        }
                    }
                }
            }
        }

        public override void Notify_PatherFailed()
        {
            base.Notify_PatherFailed();
            CleanupTracker();
        }

        public override string GetReport()
        {
            if (Partner != null)
            {
                return "Dining with " + Partner.LabelShort;
            }
            return base.GetReport();
        }
    }
}
