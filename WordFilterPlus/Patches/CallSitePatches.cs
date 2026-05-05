using HarmonyLib;
using WordFilterPlus.Filter;

namespace WordFilterPlus.Patches;

/// <summary>
/// Patches Thing.GetName to capture the Thing into FilterContext.Current.
/// </summary>
[HarmonyPatch(typeof(Thing), nameof(Thing.GetName), typeof(NameStyle), typeof(int))]
internal static class CallSitePatches
{
    [HarmonyPrefix]
    private static void Prefix(Thing __instance)
    {
        FilterContext.Current = __instance;
    }
}
