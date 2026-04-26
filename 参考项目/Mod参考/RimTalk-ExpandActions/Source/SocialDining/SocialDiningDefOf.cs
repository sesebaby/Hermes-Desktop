using RimWorld;
using Verse;

namespace RimTalkExpandActions.SocialDining
{
    [DefOf]
    public static class SocialDiningDefOf
    {
        static SocialDiningDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SocialDiningDefOf));
        }

        public static JobDef SocialDine;
        public static InteractionDef OfferFood;
        public static ThoughtDef AteWithColonist;
        public static ThoughtDef OfferedFood;
        public static ThoughtDef ReceivedFoodOffer;
    }
}
