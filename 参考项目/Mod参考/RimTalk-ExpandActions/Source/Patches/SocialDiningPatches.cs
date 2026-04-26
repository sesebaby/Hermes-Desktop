using HarmonyLib;
using RimWorld;
using Verse;
using RimTalkExpandActions.SocialDining;

namespace RimTalkExpandActions.Patches
{
    /// <summary>
    /// 社交用餐相关的 Harmony 补丁
    /// 防止共享食物被提前销毁
    /// </summary>
    [HarmonyPatch]
    public static class SocialDiningPatches
    {
        /// <summary>
        /// 销毁补丁：防止食物在被多人共享时被提前销毁
        /// 
        /// 场景：A 和 B 同时吃一个食物
        /// - A 吃完想销毁食物
        /// - 但 B 还没吃完
        /// - 此补丁返回 false 阻止销毁
        /// - 直到 B 也吃完（B 成为最后一个用餐者）才允许销毁
        /// </summary>
        [HarmonyPatch(typeof(Thing), "Destroy")]
        public static class Patch_Thing_Destroy
        {
            [HarmonyPrefix]
            public static bool Prefix(Thing __instance, DestroyMode mode)
            {
                try
                {
                    // 安全检查：确保实例和定义有效
                    if (__instance == null) return true;
                    if (__instance.def == null) return true;
                    
                    // 检查是否已销毁或正在销毁
                    if (__instance.Destroyed) return true;
                    
                    // 只处理食物物品
                    if (!__instance.def.IsIngestible) return true;
                    
                    // 检查是否有 comps
                    var thingWithComps = __instance as ThingWithComps;
                    if (thingWithComps == null) return true;
                    if (thingWithComps.AllComps == null) return true;
                    
                    // 尝试获取 SharedFoodTracker
                    SharedFoodTracker tracker = thingWithComps.TryGetComp<SharedFoodTracker>();
                    if (tracker == null) return true;
                    
                    // 检查是否正在被共享
                    if (tracker.IsBeingShared && tracker.ActiveEatersCount > 0)
                    {
                        // 阻止销毁 - 还有人在吃
                        if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                        {
                            Log.Warning($"[SocialDiningPatches] 阻止销毁共享食物 {__instance.Label}，" +
                                       $"还有 {tracker.ActiveEatersCount} 个用餐者");
                        }
                        return false; // 返回 false 阻止销毁
                    }
                }
                catch
                {
                    // 忽略异常，确保不影响正常销毁流程
                }

                return true; // 允许正常销毁
            }
        }

        /// <summary>
        /// 优化补丁：防止共享食物被其他小人选取
        /// 若食物已经被多人使用，降低其被选取优先级
        /// </summary>
        [HarmonyPatch(typeof(FoodUtility), "BestFoodSourceOnMap")]
        public static class Patch_FoodUtility_BestFoodSourceOnMap
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn getter, ref Thing __result)
            {
                try
                {
                    // 安全检查
                    if (__result == null) return;
                    if (__result.def == null) return;
                    if (!__result.def.IsIngestible) return;
                    if (__result.Destroyed) return;
                    
                    // 检查是否有 comps
                    var thingWithComps = __result as ThingWithComps;
                    if (thingWithComps == null) return;
                    if (thingWithComps.AllComps == null) return;
                    
                    // 尝试获取 SharedFoodTracker
                    SharedFoodTracker tracker = thingWithComps.TryGetComp<SharedFoodTracker>();
                    if (tracker == null) return;
                    
                    // 若食物已经被多人使用，不再提供给其他人
                    if (tracker.ActiveEatersCount >= 2)
                    {
                        if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                        {
                            Log.Message($"[SocialDiningPatches] 排除已共享的食物 {__result.Label}，" +
                                       $"当前有 {tracker.ActiveEatersCount} 个用餐者");
                        }
                        __result = null;
                    }
                }
                catch
                {
                    // 忽略异常
                }
            }
        }
    }
}
