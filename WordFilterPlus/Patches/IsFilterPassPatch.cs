using HarmonyLib;
using WordFilterPlus.Filter;

namespace WordFilterPlus.Patches;

[HarmonyPatch(typeof(global::Window.SaveData), nameof(global::Window.SaveData.IsFilterPass))]
internal static class IsFilterPassPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        global::Window.SaveData __instance,
        string text,
        ref global::Window.SaveData.FilterResult __result)
    {
        bool hasExt = ExtendedFilter.HasExtendedTokens(__instance.filterStrs);
        if (!hasExt)
            return true; // no extended tokens — let original run, zero overhead

        __result = ExtendedFilter.EvaluateFilter(
            __instance.filterStrs,
            __instance.filterOptions,
            text,
            FilterContext.Current);

        return false; // skip original
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        FilterContext.Current = null;
    }
}
