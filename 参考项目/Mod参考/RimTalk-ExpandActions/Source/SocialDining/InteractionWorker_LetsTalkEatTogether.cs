using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalkExpandActions.SocialDining
{
    /// <summary>
    /// Simple "Let's Eat Together" interaction using vanilla Ingest jobs.
    /// Both pawns immediately start eating when the interaction triggers.
    /// No custom JobDrivers, no waiting toils - maximum stability.
    /// </summary>
    public class InteractionWorker_LetsTalkEatTogether : InteractionWorker
    {
        /// <summary>
        /// Random selection weight - return 0 to prevent automatic triggering
        /// This interaction should only be triggered manually via RimTalk commands
        /// </summary>
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            // Prevent random automatic triggering
            return 0f;
        }

        /// <summary>
        /// Called when the interaction is triggered
        /// Both pawns immediately start eating using vanilla Ingest jobs
        /// </summary>
        public override void Interacted(
            Pawn initiator, 
            Pawn recipient, 
            List<RulePackDef> extraSentencePacks, 
            out string letterText, 
            out string letterLabel, 
            out LetterDef letterDef, 
            out LookTargets lookTargets)
        {
            // Clear output parameters
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;

            // Safety Check 1: Verify both pawns are valid
            if (!ArePawnsValidForEating(initiator, recipient))
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Warning("[LetsTalkEatTogether] Invalid pawns - cannot start eating");
                }
                return;
            }

            // Safety Check 2: Verify both pawns are hungry enough
            if (!ArePawnsHungry(initiator, recipient))
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message("[LetsTalkEatTogether] Pawns are not hungry enough");
                }
                return;
            }

            // Safety Check 3: Find food for initiator
            Thing initiatorFood = FindFoodForPawn(initiator);
            if (initiatorFood == null)
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Warning($"[LetsTalkEatTogether] No valid food found for {initiator.LabelShort}");
                }
                return;
            }

            // Safety Check 4: Find food for recipient
            Thing recipientFood = FindFoodForPawn(recipient);
            if (recipientFood == null)
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Warning($"[LetsTalkEatTogether] No valid food found for {recipient.LabelShort}");
                }
                return;
            }

            // All safety checks passed - start eating!
            bool initiatorStarted = StartEating(initiator, initiatorFood);
            bool recipientStarted = StartEating(recipient, recipientFood);

            // Log results
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                if (initiatorStarted && recipientStarted)
                {
                    Log.Message($"[LetsTalkEatTogether] ? {initiator.LabelShort} and {recipient.LabelShort} started eating together");
                }
                else
                {
                    Log.Warning($"[LetsTalkEatTogether] Partial failure - Initiator: {initiatorStarted}, Recipient: {recipientStarted}");
                }
            }

            // Optional: Add social thoughts/memories
            TryAddSocialThoughts(initiator, recipient);
        }

        #region Safety Checks

        /// <summary>
        /// Safety Check 1: Verify both pawns are spawned, alive, and not downed
        /// </summary>
        private bool ArePawnsValidForEating(Pawn pawn1, Pawn pawn2)
        {
            if (pawn1 == null || pawn2 == null)
            {
                return false;
            }

            // Must be spawned (on the map)
            if (!pawn1.Spawned || !pawn2.Spawned)
            {
                return false;
            }

            // Must be alive
            if (pawn1.Dead || pawn2.Dead)
            {
                return false;
            }

            // Must not be downed
            if (pawn1.Downed || pawn2.Downed)
            {
                return false;
            }

            // Must be on the same map
            if (pawn1.Map != pawn2.Map || pawn1.Map == null)
            {
                return false;
            }

            // Must not be in mental state that prevents eating
            if (pawn1.InMentalState || pawn2.InMentalState)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Safety Check 2: Verify both pawns are hungry enough (< 90% food level)
        /// Optional check - can be removed if you want them to eat regardless
        /// </summary>
        private bool ArePawnsHungry(Pawn pawn1, Pawn pawn2)
        {
            if (pawn1?.needs?.food == null || pawn2?.needs?.food == null)
            {
                return false;
            }

            // At least one pawn should be somewhat hungry
            // You can adjust this threshold (0.9 = 90% full)
            bool pawn1Hungry = pawn1.needs.food.CurLevelPercentage < 0.9f;
            bool pawn2Hungry = pawn2.needs.food.CurLevelPercentage < 0.9f;

            return pawn1Hungry || pawn2Hungry;
        }

        #endregion

        #region Food Finding

        /// <summary>
        /// Safety Check 3 & 4: Find the best food source for a pawn
        /// Uses vanilla FoodUtility.BestFoodSourceOnMap
        /// </summary>
        private Thing FindFoodForPawn(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.needs?.food == null)
            {
                return null;
            }

            // Use vanilla food finding logic
            // This handles all vanilla food restrictions, preferences, etc.
            Thing food = FoodUtility.BestFoodSourceOnMap(
                eater: pawn,
                getter: pawn,
                desperate: false,
                foodDef: out ThingDef foodDef,
                maxPref: FoodPreferability.MealLavish,
                allowPlant: true,
                allowDrug: false,
                allowCorpse: pawn.RaceProps.Humanlike ? false : true,
                allowDispenserFull: true,
                allowDispenserEmpty: false,
                allowForbidden: false,
                allowSociallyImproper: false,
                allowHarvest: false,
                forceScanWholeMap: false,
                ignoreReservations: false,
                calculateWantedStackCount: false
            );

            // Additional validation
            if (food != null && food.Destroyed)
            {
                return null;
            }

            return food;
        }

        #endregion

        #region Job Assignment

        /// <summary>
        /// Start eating using vanilla JobDefOf.Ingest
        /// Forces the pawn to interrupt current job and start eating immediately
        /// </summary>
        private bool StartEating(Pawn pawn, Thing food)
        {
            if (pawn?.jobs == null || food == null)
            {
                return false;
            }

            // Create vanilla Ingest job
            Job ingestJob = JobMaker.MakeJob(JobDefOf.Ingest, food);
            ingestJob.count = FoodUtility.WillIngestStackCountOf(pawn, food.def, food.GetStatValue(StatDefOf.Nutrition));

            // Force the job using TryTakeOrderedJob
            // This will interrupt the current job and start eating immediately
            bool success = pawn.jobs.TryTakeOrderedJob(ingestJob, JobTag.Misc);

            if (!success && RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Warning($"[LetsTalkEatTogether] {pawn.LabelShort} failed to accept Ingest job");
            }

            return success;
        }

        #endregion

        #region Optional Social Effects

        /// <summary>
        /// Optional: Add positive social thoughts for eating together
        /// Can be removed if you don't want social effects
        /// </summary>
        private void TryAddSocialThoughts(Pawn initiator, Pawn recipient)
        {
            // Add "Shared a meal" thought if available
            if (initiator?.needs?.mood != null && SocialDiningDefOf.OfferedFood != null)
            {
                initiator.needs.mood.thoughts.memories.TryGainMemory(
                    SocialDiningDefOf.OfferedFood, 
                    recipient
                );
            }

            if (recipient?.needs?.mood != null && SocialDiningDefOf.ReceivedFoodOffer != null)
            {
                recipient.needs.mood.thoughts.memories.TryGainMemory(
                    SocialDiningDefOf.ReceivedFoodOffer, 
                    initiator
                );
            }
        }

        #endregion
    }
}
