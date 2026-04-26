using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalkExpandActions.SocialDining
{
    /// <summary>
    /// �罻���ͻ���������
    /// �� A ���� B �Է�ʱ��˫��������ʼ�罻����
    /// </summary>
    public class InteractionWorker_OfferFood : InteractionWorker
    {
        /// <summary>
        /// ���ѡ��Ȩ�� - �����Ƿ��Զ������˽���
        /// </summary>
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            // ������֤
            if (!IsValidInteractionPair(initiator, recipient))
            {
                return 0f;
            }

            // ����Ƿ���Թ���
            if (!CanOfferFood(initiator, recipient))
            {
                return 0f;
            }

            // ���ص�Ȩ�أ�����Ƶ������
            return 0.02f;
        }

        /// <summary>
        /// �����ɹ�ʱ���� - ˫��������ʼ�Է�
        /// </summary>
        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, 
            out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            // ����ż�����
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;

            // ��������buff
            GiveMemories(initiator, recipient);

            // ���ģ�������ʼ�罻����
            if (initiator != null && recipient != null)
            {
                bool success = TryStartSocialDining(initiator, recipient);
                
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[SocialDining] InteractionWorker: {initiator.LabelShort} ���� {recipient.LabelShort} ���� - {(success ? "�ɹ�" : "ʧ��")}");
                }
            }
        }

        #region �����߼�

        /// <summary>
        /// ���Կ�ʼ�罻���� - Ϊ˫������ʳ�ﲢ��������
        /// </summary>
        private bool TryStartSocialDining(Pawn initiator, Pawn recipient)
        {
            // Step 1: �������ʳ��
            Thing food = FindBestFoodForDining(initiator, recipient);
            if (food == null)
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Warning($"[SocialDining] �Ҳ����ʺ� {initiator.LabelShort} �� {recipient.LabelShort} ���͵�ʳ��");
                }
                return false;
            }

            // Step 2: ��������߳���ʳ��ȷ���
            if (initiator.carryTracker?.CarriedThing == food)
            {
                if (!initiator.carryTracker.TryDropCarriedThing(initiator.Position, ThingPlaceMode.Near, out Thing droppedFood))
                {
                    Log.Warning($"[SocialDining] {initiator.LabelShort} �޷�����ʳ��");
                    return false;
                }
                food = droppedFood;
            }

            // Step 3: ��֤ʳ����Ч��
            if (food == null || food.Destroyed || !food.Spawned)
            {
                Log.Warning("[SocialDining] ʳ����Ч���ѱ�����");
                return false;
            }

            // Step 4: ���Ԥ����ͻ�����˹��ͺ����߼���
            if (!CanBothPawnsReserveFood(initiator, recipient, food))
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Warning($"[SocialDining] ʳ��Ԥ����ͻ���޷���ʼ����");
                }
                return false;
            }

            // Step 5: ���Ҳ�������ѡ��
            Building table = FoodSharingUtility.TryFindTableForTwo(initiator.Map, initiator, recipient, 40f);

            // Step 6: ��������
            Job initiatorJob = CreateDiningJob(initiator, food, table, recipient);
            Job recipientJob = CreateDiningJob(recipient, food, table, initiator);

            // Step 7: ǿ��ָ�����񣨹ؼ�����
            bool initiatorStarted = StartDiningJob(initiator, initiatorJob);
            bool recipientStarted = StartDiningJob(recipient, recipientJob);

            // Step 8: ����ʧ�����
            if (!initiatorStarted || !recipientStarted)
            {
                // ���һ��ʧ�ܣ�ȡ����һ��
                if (initiatorStarted)
                {
                    initiator.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
                if (recipientStarted)
                {
                    recipient.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
                
                return false;
            }

            // Step 9: ���ʳ��Ϊ����
            FoodSharingUtility.MarkFoodAsShared(food, initiator, recipient);

            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message($"[SocialDining] ? {initiator.LabelShort} �� {recipient.LabelShort} ��ʼ�罻����");
            }

            return true;
        }

        /// <summary>
        /// �������ʳ�� - ���ȱ�����Ȼ���ͼ
        /// </summary>
        private Thing FindBestFoodForDining(Pawn pawn1, Pawn pawn2)
        {
            // ���ȼ� 1: �����߱���
            Thing food = FindFoodInInventory(pawn1);
            if (food != null) return food;

            // ���ȼ� 2: �����߱���
            food = FindFoodInInventory(pawn2);
            if (food != null) return food;

            // ���ȼ� 3: ��ͼ�Ͼ����е������ʳ��
            food = FindFoodOnMap(pawn1, pawn2);
            if (food != null) return food;

            return null;
        }

        /// <summary>
        /// �ڱ����в���ʳ��
        /// </summary>
        private Thing FindFoodInInventory(Pawn pawn)
        {
            if (pawn?.inventory?.innerContainer == null)
            {
                return null;
            }

            Thing bestFood = null;
            float bestScore = float.MinValue;

            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                if (!IsFoodValidForDining(pawn, thing))
                {
                    continue;
                }

                float score = GetFoodQualityScore(thing);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestFood = thing;
                }
            }

            return bestFood;
        }

        /// <summary>
        /// �ڵ�ͼ�ϲ���ʳ��
        /// </summary>
        private Thing FindFoodOnMap(Pawn pawn1, Pawn pawn2)
        {
            if (pawn1?.Map == null)
            {
                return null;
            }

            // �����е�λ��
            IntVec3 midPoint = new IntVec3(
                (pawn1.Position.x + pawn2.Position.x) / 2,
                0,
                (pawn1.Position.z + pawn2.Position.z) / 2
            );

            ThingRequest request = ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            TraverseParms traverseParms = TraverseParms.For(pawn1);

            return GenClosest.ClosestThingReachable(
                midPoint,
                pawn1.Map,
                request,
                PathEndMode.Touch,
                traverseParms,
                45f,
                t => IsFoodValidForDining(pawn1, t) && IsFoodValidForDining(pawn2, t)
            );
        }

        /// <summary>
        /// ���ʳ���Ƿ��ʺϹ���
        /// </summary>
        private bool IsFoodValidForDining(Pawn pawn, Thing food)
        {
            if (food == null || food.def == null)
            {
                return false;
            }

            // �����ǿ�ʳ�õ�
            if (food.def.ingestible == null || !food.def.IsIngestible)
            {
                return false;
            }

            // �������ڿ��Գ�
            if (!food.IngestibleNow)
            {
                return false;
            }

            // ���ܱ���ֹ
            if (food.IsForbidden(pawn))
            {
                return false;
            }

            // ����Ƿ��ѱ����˹���
            SharedFoodTracker tracker = food.TryGetComp<SharedFoodTracker>();
            if (tracker != null && tracker.ActiveEatersCount >= 2)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// ����ʳ����������
        /// </summary>
        private float GetFoodQualityScore(Thing food)
        {
            float nutrition = food.GetStatValue(StatDefOf.Nutrition, true);
            float preferability = (float)(food.def.ingestible?.preferability ?? FoodPreferability.RawBad);
            
            // Ӫ��ֵ + ƫ�ö� * 10
            return nutrition + (preferability * 10f);
        }

        /// <summary>
        /// ���˫���Ƿ���Ԥ��ʳ��
        /// </summary>
        private bool CanBothPawnsReserveFood(Pawn pawn1, Pawn pawn2, Thing food)
        {
            if (food == null || pawn1 == null || pawn2 == null)
            {
                return false;
            }

            // ��� pawn1 �ܷ�Ԥ��
            if (!pawn1.CanReserve(food, 1, -1, null, false))
            {
                Pawn reserver = pawn1.Map?.reservationManager?.FirstRespectedReserver(food, pawn1);
                if (reserver != pawn2)
                {
                    // ��������Ԥ��
                    return false;
                }
            }

            // ��� pawn2 �ܷ�Ԥ��
            if (!pawn2.CanReserve(food, 1, -1, null, false))
            {
                Pawn reserver = pawn2.Map?.reservationManager?.FirstRespectedReserver(food, pawn2);
                if (reserver != pawn1)
                {
                    // ��������Ԥ��
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// �����罻�ò�����
        /// </summary>
        private Job CreateDiningJob(Pawn eater, Thing food, Building table, Pawn diningPartner)
        {
            Job job = JobMaker.MakeJob(SocialDiningDefOf.SocialDine, food, table, diningPartner);
            job.count = 1; // ֻ��һ��
            return job;
        }

        /// <summary>
        /// �����ò�����ǿ��ָ�ɣ�
        /// </summary>
        private bool StartDiningJob(Pawn pawn, Job job)
        {
            if (pawn?.jobs == null || job == null)
            {
                return false;
            }

            // ʹ�� TryTakeOrderedJob ǿ��ָ��
            bool success = pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);

            if (success && RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message($"[SocialDining] {pawn.LabelShort} �����罻��������");
            }
            else if (!success)
            {
                Log.Warning($"[SocialDining] {pawn.LabelShort} �޷������罻��������");
            }

            return success;
        }

        #endregion

        #region ��������

        /// <summary>
        /// ��֤����˫����Ч��
        /// </summary>
        private bool IsValidInteractionPair(Pawn initiator, Pawn recipient)
        {
            if (initiator == null || recipient == null)
            {
                return false;
            }

            if (initiator.Dead || recipient.Dead)
            {
                return false;
            }

            if (!initiator.Spawned || !recipient.Spawned)
            {
                return false;
            }

            if (initiator.Map != recipient.Map)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// ����Ƿ�������빲��
        /// </summary>
        private bool CanOfferFood(Pawn initiator, Pawn recipient)
        {
            // ��鼢����
            if (initiator?.needs?.food == null || recipient?.needs?.food == null)
            {
                return false;
            }

            // ���˫����������������
            if (initiator.needs.food.CurLevelPercentage > 0.9f && 
                recipient.needs.food.CurLevelPercentage > 0.9f)
            {
                return false;
            }

            // ����Ƿ����ҵ�ʳ��
            Thing food = FindBestFoodForDining(initiator, recipient);
            return food != null;
        }

        /// <summary>
        /// ���Ӽ��䣨����buff��
        /// </summary>
        private void GiveMemories(Pawn initiator, Pawn recipient)
        {
            if (initiator?.needs?.mood != null)
            {
                initiator.needs.mood.thoughts.memories.TryGainMemory(SocialDiningDefOf.OfferedFood, recipient);
            }

            if (recipient?.needs?.mood != null)
            {
                recipient.needs.mood.thoughts.memories.TryGainMemory(SocialDiningDefOf.ReceivedFoodOffer, initiator);
            }
        }

        #endregion
    }
}
