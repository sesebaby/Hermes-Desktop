using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalkExpandActions.SocialDining
{
    public static class FoodSharingUtility
    {
        private const float FoodSearchRadius = 45f;
        private const float TableSearchRadius = 40f;

        #region ���ķ��� - ��������

        /// <summary>
        /// ����������Ϊ - ��Դ Mod ��ֲ�ĺ��ķ���
        /// ���������Ķ���Ԥ���������񴴽��߼�
        /// </summary>
        public static bool TryTriggerShareFood(Pawn initiator, Pawn recipient, Thing food)
        {
            if (initiator == null || recipient == null || food == null)
            {
                Log.Warning("[SocialDining] TryTriggerShareFood: ������Ч");
                return false;
            }

            // Step 1: ����ʳ���������߳��У�
            Thing foodToDrop = null;
            if (initiator.carryTracker?.CarriedThing == food)
            {
                if (initiator.carryTracker.TryDropCarriedThing(initiator.Position, ThingPlaceMode.Near, out foodToDrop))
                {
                    food = foodToDrop;
                }
                else
                {
                    Log.Warning("[SocialDining] �޷�����ʳ��");
                    return false;
                }
            }

            // Step 2: ���ʳ���Ƿ���Ч
            if (food == null || food.Destroyed || !food.Spawned)
            {
                Log.Warning("[SocialDining] ʳ����Ч���ѱ�����");
                return false;
            }

            // Step 3: ����Ԥ����飨�����߼���
            if (!initiator.CanReserve(food, 1, -1, null, false))
            {
                Pawn reserver = initiator.Map?.reservationManager?.FirstRespectedReserver(food, initiator);
                if (reserver != recipient)
                {
                    Log.Warning($"[SocialDining] {initiator.LabelShort} �޷�Ԥ��ʳ�� (�ѱ� {reserver?.LabelShort ?? "δ֪"} Ԥ��)");
                    return false;
                }
            }

            if (!recipient.CanReserve(food, 1, -1, null, false))
            {
                Pawn reserver = recipient.Map?.reservationManager?.FirstRespectedReserver(food, recipient);
                if (reserver != initiator)
                {
                    Log.Warning($"[SocialDining] {recipient.LabelShort} �޷�Ԥ��ʳ�� (�ѱ� {reserver?.LabelShort ?? "δ֪"} Ԥ��)");
                    return false;
                }
            }

            // Step 4: ���Ҳ�����Ұ�͵ص�
            Building table = TryFindTableForTwo(initiator.Map, initiator, recipient, TableSearchRadius);
            
            // Step 5: ��������
            Job initiatorJob = JobMaker.MakeJob(SocialDiningDefOf.SocialDine, food, table, recipient);
            initiatorJob.count = 1;

            Job recipientJob = JobMaker.MakeJob(SocialDiningDefOf.SocialDine, food, table, initiator);
            recipientJob.count = 1;

            // Step 6: ��������
            if (initiator.jobs.TryTakeOrderedJob(initiatorJob, JobTag.Misc, false))
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[SocialDining] {initiator.LabelShort} �����罻��������");
                }
            }
            else
            {
                Log.Warning($"[SocialDining] {initiator.LabelShort} �޷������罻��������");
                return false;
            }

            if (recipient.jobs.TryTakeOrderedJob(recipientJob, JobTag.Misc, false))
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[SocialDining] {recipient.LabelShort} �����罻��������");
                }
            }
            else
            {
                Log.Warning($"[SocialDining] {recipient.LabelShort} �޷������罻��������ȡ������������");
                initiator.jobs.EndCurrentJob(JobCondition.InterruptForced);
                return false;
            }

            return true;
        }

        /// <summary>
        /// �����ʺ����˵Ĳ���
        /// ��Դ Mod ��ֲ
        /// </summary>
        public static Building TryFindTableForTwo(Map map, Pawn pawn1, Pawn pawn2, float maxDistance)
        {
            if (map == null || pawn1 == null || pawn2 == null)
                return null;

            // Calculate midpoint between two pawns
            IntVec3 midPoint = new IntVec3(
                (pawn1.Position.x + pawn2.Position.x) / 2,
                0,
                (pawn1.Position.z + pawn2.Position.z) / 2
            );
            
            Building bestTable = null;
            float bestScore = float.MinValue;

            // Find all dining tables
            List<Building> allTables = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building?.isMealSource == true || b.def.surfaceType == SurfaceType.Eat)
                .ToList();

            foreach (Building table in allTables)
            {
                if (table.Position.DistanceTo(midPoint) > maxDistance)
                    continue;

                // Check for adjacent seats
                int adjacentSeats = 0;
                foreach (IntVec3 cell in GenAdj.CellsAdjacentCardinal(table))
                {
                    Building seat = cell.GetEdifice(map);
                    if (seat != null && seat.def.building?.isSittable == true)
                    {
                        adjacentSeats++;
                    }
                }

                if (adjacentSeats < 2)
                    continue;

                // Score: closer is better
                float score = 100f - table.Position.DistanceTo(midPoint);
                score += adjacentSeats * 5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTable = table;
                }
            }

            return bestTable;
        }

        /// <summary>
        /// ���С���Ƿ���Ա�����ȥ����
        /// ��Դ Mod ��ֲ
        /// </summary>
        public static bool IsSafeToDisturb(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead)
                return false;

            // ����Ƿ�����
            if (pawn.Drafted)
                return false;

            // ����Ƿ������
            if (pawn.InMentalState)
                return false;

            // ����Ƿ���ս��
            if (pawn.mindState?.enemyTarget != null)
                return false;

            // ����Ƿ���ִ�и����ȼ�����
            if (pawn.jobs?.curJob != null)
            {
                if (pawn.jobs.curJob.def == JobDefOf.AttackMelee || 
                    pawn.jobs.curJob.def == JobDefOf.AttackStatic ||
                    pawn.jobs.curJob.def == JobDefOf.FleeAndCower)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// �����ʺϹ��͵�ʳ��
        /// </summary>
        public static Thing FindFoodForSharing(Pawn pawn1, Pawn pawn2)
        {
            if (pawn1 == null || pawn2 == null)
                return null;

            // ���ȼ�鱳��
            if (TryFindFoodInInventory(pawn1, out Thing food1))
                return food1;

            if (TryFindFoodInInventory(pawn2, out Thing food2))
                return food2;

            // �ڵ�ͼ�ϲ���
            if (TryFindFoodOnMap(pawn1, out Thing food3))
                return food3;

            return null;
        }

        #endregion

        #region ��������

        public static SharedFoodTracker TryGetFoodTracker(Thing food)
        {
            return food?.TryGetComp<SharedFoodTracker>();
        }

        public static void MarkFoodAsShared(Thing food, Pawn sharer, Pawn recipient)
        {
            ThingWithComps foodWithComps = food as ThingWithComps;
            if (foodWithComps != null)
            {
                SharedFoodTracker tracker = foodWithComps.TryGetComp<SharedFoodTracker>();
                tracker?.RegisterEater(sharer);
                tracker?.RegisterEater(recipient);
            }
        }

        private static bool TryFindFoodInInventory(Pawn pawn, out Thing food)
        {
            food = null;

            var container = pawn.inventory?.innerContainer;
            if (container == null || container.Count == 0)
            {
                return false;
            }

            Thing bestThing = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < container.Count; i++)
            {
                Thing thing = container[i];
                if (!IsValidFoodToShare(pawn, thing))
                {
                    continue;
                }

                float score = GetFoodScore(thing);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestThing = thing;
                }
            }

            if (bestThing != null)
            {
                food = bestThing;
                return true;
            }

            return false;
        }

        private static bool TryFindFoodOnMap(Pawn pawn, out Thing food)
        {
            food = null;
            if (pawn.Map == null)
            {
                return false;
            }

            ThingRequest request = ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            TraverseParms traverseParms = TraverseParms.For(pawn);
            
            Thing found = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                request,
                PathEndMode.Touch,
                traverseParms,
                FoodSearchRadius,
                t => IsValidFoodToShare(pawn, t));

            if (found != null)
            {
                food = found;
                return true;
            }

            return false;
        }

        private static bool IsValidFoodToShare(Pawn pawn, Thing food)
        {
            if (pawn == null || food == null)
            {
                return false;
            }

            if (food.def.ingestible == null || !food.def.IsIngestible)
            {
                return false;
            }

            if (!food.IngestibleNow || food.IsForbidden(pawn))
            {
                return false;
            }

            // ����Ƿ��ѱ������˹��������ǵ�ǰ���ˣ�
            SharedFoodTracker tracker = TryGetFoodTracker(food);
            if (tracker != null && tracker.ActiveEatersCount >= 2)
            {
                return false;
            }

            return true;
        }

        private static float GetFoodScore(Thing food)
        {
            float nutrition = food.GetStatValue(StatDefOf.Nutrition, true);
            float preferability = (float)(food.def.ingestible?.preferability ?? FoodPreferability.RawBad);
            return nutrition + preferability;
        }

        #endregion
    }
}
